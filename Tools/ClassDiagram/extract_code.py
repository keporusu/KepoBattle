"""Assets/Scripts の C# を構文解析し、型と型の間の依存を取り出す。

コンパイラを使わない構文解析なので、型は名前で解決する。
解決できなかった名前や、同名の型は model の warnings に残す。
"""
from __future__ import annotations

import json
import re
from collections import defaultdict
from pathlib import Path

import tree_sitter_c_sharp
from tree_sitter import Language, Node, Parser

PARSER = Parser(Language(tree_sitter_c_sharp.language()))

TYPE_NODES = {
    "class_declaration": "class",
    "struct_declaration": "struct",
    "interface_declaration": "interface",
    "enum_declaration": "enum",
    "record_declaration": "record",
}
#コンポーネントをコードから取得する Unity の API と、取得先の範囲
COMPONENT_LOOKUPS = {
    "GetComponent": "same",
    "TryGetComponent": "same",
    "GetComponents": "same",
    "GetComponentInChildren": "children",
    "GetComponentsInChildren": "children",
    "GetComponentInParent": "parent",
    "GetComponentsInParent": "parent",
    "AddComponent": "add",
    "FindObjectOfType": "scene",
    "FindFirstObjectByType": "scene",
    "FindAnyObjectByType": "scene",
    "FindObjectsOfType": "scene",
    "FindObjectsByType": "scene",
}
#文字列で対象を指定する呼び出し
STRING_CALLS = {"PlaySe", "PlayBgm", "Play", "SetTrigger", "SetBool", "SetFloat", "CrossFade"}


def text(node: Node | None) -> str:
    return node.text.decode("utf-8") if node is not None else ""


def walk(node: Node):
    stack = [node]
    while stack:
        current = stack.pop()
        yield current
        stack.extend(reversed(current.named_children))


def modifiers(node: Node) -> list[str]:
    return [text(child) for child in node.children if child.type == "modifier"]


def attributes(node: Node) -> list[str]:
    names = []
    for child in node.children:
        if child.type == "attribute_list":
            names += [text(attr.child_by_field_name("name")) for attr in child.named_children if attr.type == "attribute"]
    return names


def type_names(node: Node | None) -> list[str]:
    """型の構文から、型名をすべて取り出す（List<Foo> なら List と Foo）"""
    if node is None:
        return []
    if node.type == "identifier":
        return [text(node)]
    if node.type == "qualified_name":
        return [text(node.child_by_field_name("name")).split("<")[0]] + type_names_of_args(node)
    if node.type == "generic_name":
        head = next((child for child in node.named_children if child.type == "identifier"), None)
        return [text(head)] + type_names_of_args(node)
    if node.type == "predefined_type":
        return []
    names = []
    for child in node.named_children:
        names += type_names(child)
    return names


def type_names_of_args(node: Node) -> list[str]:
    names = []
    for child in walk(node):
        if child.type == "type_argument_list":
            for arg in child.named_children:
                names += type_names(arg)
            break
    return names


def is_many(node: Node | None) -> bool:
    if node is None:
        return False
    if node.type == "array_type":
        return True
    if node.type == "generic_name":
        head = text(next((c for c in node.named_children if c.type == "identifier"), None))
        return head in {"List", "HashSet", "IEnumerable", "IReadOnlyList", "IReadOnlyCollection", "Queue", "Stack", "Dictionary", "IDictionary", "IReadOnlyDictionary"}
    return False


def is_auto_property(node: Node) -> bool:
    """{ get; private set; } のように、値を保持するだけのプロパティか"""
    accessors = node.child_by_field_name("accessors")
    if accessors is None:
        return False
    return all(a.child_by_field_name("body") is None for a in accessors.named_children if a.type == "accessor_declaration")


def collect_declarations(path: Path, rel: str, tree) -> list[dict]:
    """1ファイル内の型宣言とメンバーを集める"""
    results: list[dict] = []

    def visit(node: Node, namespace: str, outer: str | None):
        for child in node.named_children:
            if child.type in ("namespace_declaration", "file_scoped_namespace_declaration"):
                name = text(child.child_by_field_name("name"))
                visit(child, f"{namespace}.{name}".strip("."), None)
            elif child.type == "declaration_list":
                visit(child, namespace, outer)
            elif child.type in TYPE_NODES:
                results.append(read_type(child, namespace, outer, rel))
                body = child.child_by_field_name("body")
                if body is not None:
                    short = text(child.child_by_field_name("name"))
                    visit(body, namespace, f"{outer}.{short}" if outer else short)
            elif child.type == "delegate_declaration":
                results.append({
                    "name": text(child.child_by_field_name("name")), "outer": outer, "namespace": namespace,
                    "kind": "delegate", "file": rel, "line": child.start_point[0] + 1, "modifiers": modifiers(child),
                    "attributes": attributes(child), "bases": [], "fields": [], "properties": [], "methods": [],
                    "events": [], "enum_members": [], "_node": None,
                })

    visit(tree.root_node, "", None)
    return results


def read_type(node: Node, namespace: str, outer: str | None, rel: str) -> dict:
    info = {
        "name": text(node.child_by_field_name("name")),
        "outer": outer,
        "namespace": namespace,
        "kind": TYPE_NODES[node.type],
        "file": rel,
        "line": node.start_point[0] + 1,
        "modifiers": modifiers(node),
        "attributes": attributes(node),
        "bases": [],
        "fields": [],
        "properties": [],
        "methods": [],
        "events": [],
        "enum_members": [],
        "_node": node,
    }
    for child in node.named_children:
        if child.type == "base_list":
            for base in child.named_children:
                info["bases"] += type_names(base)[:1]

    body = node.child_by_field_name("body")
    if body is None:
        return info

    if node.type == "enum_declaration":
        info["enum_members"] = [text(m.child_by_field_name("name")) for m in body.named_children if m.type == "enum_member_declaration"]
        return info

    for member in body.named_children:
        mods = modifiers(member)
        if member.type == "field_declaration":
            decl = next(c for c in member.named_children if c.type == "variable_declaration")
            type_node = decl.child_by_field_name("type")
            for var in (c for c in decl.named_children if c.type == "variable_declarator"):
                info["fields"].append({
                    "name": text(var.child_by_field_name("name")),
                    "type": text(type_node),
                    "type_names": type_names(type_node),
                    "many": is_many(type_node),
                    "modifiers": mods,
                    "attributes": attributes(member),
                    "line": member.start_point[0] + 1,
                })
        elif member.type == "event_field_declaration":
            decl = next(c for c in member.named_children if c.type == "variable_declaration")
            type_node = decl.child_by_field_name("type")
            for var in (c for c in decl.named_children if c.type == "variable_declarator"):
                info["events"].append({"name": text(var.child_by_field_name("name")), "type": text(type_node), "modifiers": mods, "type_names": type_names(type_node)})
        elif member.type == "event_declaration":
            type_node = member.child_by_field_name("type")
            info["events"].append({"name": text(member.child_by_field_name("name")), "type": text(type_node), "modifiers": mods, "type_names": type_names(type_node)})
        elif member.type == "property_declaration":
            type_node = member.child_by_field_name("type")
            info["properties"].append({
                "name": text(member.child_by_field_name("name")),
                "type": text(type_node),
                "type_names": type_names(type_node),
                "many": is_many(type_node),
                "modifiers": mods,
                "line": member.start_point[0] + 1,
                "is_auto": is_auto_property(member),
            })
        elif member.type in ("method_declaration", "constructor_declaration"):
            returns = member.child_by_field_name("returns")
            params = member.child_by_field_name("parameters")
            param_list = []
            if params is not None:
                for p in params.named_children:
                    if p.type == "parameter":
                        ptype = p.child_by_field_name("type")
                        param_list.append({"name": text(p.child_by_field_name("name")), "type": text(ptype), "type_names": type_names(ptype)})
            info["methods"].append({
                "name": text(member.child_by_field_name("name")),
                "returns": text(returns),
                "return_type_names": type_names(returns),
                "params": param_list,
                "modifiers": mods,
                "line": member.start_point[0] + 1,
                "is_constructor": member.type == "constructor_declaration",
            })
    return info


class Resolver:
    """型名からプロジェクト内の型を引く。プロジェクト外なら None を返す"""

    def __init__(self, types: list[dict]):
        self.by_name: dict[str, list[dict]] = defaultdict(list)
        for t in types:
            self.by_name[t["name"]].append(t)

    def resolve(self, name: str, context: dict) -> dict | None:
        candidates = self.by_name.get(name)
        if not candidates:
            return None
        if len(candidates) == 1:
            return candidates[0]
        #同名の型があれば、同じ名前空間のものを優先する
        same = [c for c in candidates if c["namespace"] == context["namespace"]]
        return same[0] if same else candidates[0]


def full_id(t: dict) -> str:
    return f"{t['outer']}.{t['name']}" if t.get("outer") else t["name"]


#型引数が「中身」を表さないジェネリック型（Action<Foo> の変数は Foo ではない）
DELEGATE_TYPES = {"Action", "Func", "Predicate", "EventHandler", "UnityAction", "UnityEvent", "Comparison"}
DICTIONARY_TYPES = {"Dictionary", "IDictionary", "IReadOnlyDictionary"}


def value_type(names: list[str], resolver: "Resolver") -> str | None:
    """変数の型名の並びから、その変数の値として扱う型を選ぶ

    List<Foo> は Foo、Dictionary<Foo, Bar> は値の Bar として扱う。
    """
    if not names:
        return None
    if names[0] in DELEGATE_TYPES:
        return None
    ordered = list(reversed(names)) if names[0] in DICTIONARY_TYPES else names
    for name in ordered:
        if resolver.by_name.get(name):
            return name
    return names[0]


class BodyAnalyzer:
    """メソッド本体などを走査して、依存を記録する"""

    def __init__(self, owner: dict, resolver: Resolver, events_by_name: dict, add_relation):
        self.owner = owner
        self.resolver = resolver
        self.events_by_name = events_by_name
        self.add = add_relation
        self.scope: dict[str, str] = {}
        self.strings: list[dict] = []

    # --- 型の推定 ---

    def base_chain(self, start: dict | None) -> list[dict]:
        """指定した型から基底クラスへさかのぼった一覧"""
        chain, current, seen = [], start, set()
        while current is not None and current["name"] not in seen:
            seen.add(current["name"])
            chain.append(current)
            next_type = None
            for base in current["bases"]:
                found = self.resolver.resolve(base, current)
                if found is not None and found["kind"] == "class":
                    next_type = found
                    break
            current = next_type
        return chain

    def member_type(self, type_info: dict | None, member: str) -> str | None:
        """型のメンバー（フィールド・プロパティ・メソッド）の型を返す"""
        for level in self.base_chain(type_info):
            for entry in level["fields"] + level["properties"]:
                if entry["name"] == member:
                    return value_type(entry["type_names"], self.resolver)
            for entry in level["methods"]:
                if entry["name"] == member:
                    return value_type(entry["return_type_names"], self.resolver)
        return None

    def is_variable(self, name: str) -> bool:
        return name in self.scope or self.member_type(self.owner, name) is not None

    def type_of(self, node: Node | None) -> str | None:
        """式の型を、分かる範囲で推定する"""
        if node is None:
            return None
        kind = node.type
        if kind == "identifier":
            name = text(node)
            if name in self.scope:
                return self.scope[name]
            own = self.member_type(self.owner, name)
            if own is not None:
                return own
            return name if self.resolver.by_name.get(name) else None
        if kind == "this_expression":
            return self.owner["name"]
        if kind == "member_access_expression":
            return self.member_of(node.child_by_field_name("expression"), text(node.child_by_field_name("name")))
        if kind == "conditional_access_expression":
            binding = next((c for c in node.named_children if c.type == "member_binding_expression"), None)
            if binding is not None:
                return self.member_of(node.child_by_field_name("condition"), text(binding.child_by_field_name("name")))
            return None
        if kind == "invocation_expression":
            return self.invocation_type(node)
        if kind == "object_creation_expression":
            return value_type(type_names(node.child_by_field_name("type")), self.resolver)
        if kind == "cast_expression":
            return value_type(type_names(node.child_by_field_name("type")), self.resolver)
        if kind == "as_expression":
            return value_type(type_names(node.child_by_field_name("right")), self.resolver)
        if kind == "element_access_expression":
            return self.type_of(node.child_by_field_name("expression"))
        if kind == "conditional_expression":
            return self.type_of(node.child_by_field_name("consequence")) or self.type_of(node.child_by_field_name("alternative"))
        if kind == "binary_expression":
            return self.type_of(node.child_by_field_name("left")) or self.type_of(node.child_by_field_name("right"))
        if kind in ("parenthesized_expression", "await_expression", "postfix_unary_expression", "prefix_unary_expression") and node.named_children:
            return self.type_of(node.named_children[0])
        return None

    def member_of(self, receiver: Node | None, member: str) -> str | None:
        member = member.split("<")[0]
        receiver_type = self.type_of(receiver)
        if receiver_type is not None:
            info = self.resolver.resolve(receiver_type, self.owner)
            found = self.member_type(info, member)
            if found is not None:
                return found
            #Nullable<T> の .Value は T のまま
            if member == "Value":
                return receiver_type
            return None
        #名前空間で修飾した型名（Components.Camera.CameraController など）
        if self.resolver.by_name.get(member) and receiver is not None and text(receiver)[:1].isupper():
            return member
        return None

    def invocation_type(self, node: Node) -> str | None:
        func = node.child_by_field_name("function")
        if func is None:
            return None
        name_node = func.child_by_field_name("name") if func.type == "member_access_expression" else func
        if name_node is not None and name_node.type == "generic_name":
            names = type_names_of_args(name_node)
            head = text(next((c for c in name_node.named_children if c.type == "identifier"), None))
            if names and (head in COMPONENT_LOOKUPS or head in ("Load", "LoadAll", "Instantiate")):
                return names[0]
        method = text(name_node).split("<")[0]
        if method == "Instantiate":
            args = node.child_by_field_name("arguments")
            if args is not None and args.named_children and args.named_children[0].named_children:
                return self.type_of(args.named_children[0].named_children[0])
        if func.type == "member_access_expression":
            return self.member_of(func.child_by_field_name("expression"), method)
        return self.member_type(self.owner, method)

    # --- 宣言の収集 ---

    def analyze_member(self, member: Node):
        self.scope = {}
        #先に宣言を集める。推定が前の宣言に依存するので、書かれた順に処理する
        for node in self.in_source_order(member):
            self.collect_local(node)
        for node in walk(member):
            self.visit(node)

    @staticmethod
    def in_source_order(node: Node):
        return sorted(walk(node), key=lambda n: n.start_byte)

    def declare(self, name: str, type_node: Node | None, value: Node | None = None):
        if type_node is not None and text(type_node) != "var":
            found = value_type(type_names(type_node), self.resolver)
        else:
            found = self.type_of(value)
        if found:
            self.scope[name] = found

    def collect_local(self, node: Node):
        kind = node.type
        if kind == "parameter":
            self.declare(text(node.child_by_field_name("name")), node.child_by_field_name("type"))
        elif kind == "variable_declaration":
            type_node = node.child_by_field_name("type")
            for var in (c for c in node.named_children if c.type == "variable_declarator"):
                value = next((c for c in reversed(var.named_children) if c != var.child_by_field_name("name")), None)
                self.declare(text(var.child_by_field_name("name")), type_node, value)
        elif kind == "declaration_expression":
            type_node = node.child_by_field_name("type")
            if text(type_node) == "var":
                #TryGetComponent<T>(out var x) の x は T
                call = node.parent.parent.parent if node.parent and node.parent.parent else None
                value = call if call is not None and call.type == "invocation_expression" else None
                self.declare(text(node.child_by_field_name("name")), type_node, value)
            else:
                self.declare(text(node.child_by_field_name("name")), type_node)
        elif kind == "foreach_statement":
            self.declare(text(node.child_by_field_name("left")), node.child_by_field_name("type"), node.child_by_field_name("right"))
        elif kind == "declaration_pattern":
            self.declare(text(node.child_by_field_name("name")), node.child_by_field_name("type"))

    # --- 依存の記録 ---

    def link(self, name: str | None, kind: str, node: Node, detail: str | None = None):
        if name is None:
            return
        target = self.resolver.resolve(name, self.owner)
        if target is None or target is self.owner:
            return
        self.add(self.owner, target, kind, node.start_point[0] + 1, detail)

    def visit(self, node: Node):
        kind = node.type
        if kind == "invocation_expression":
            self.visit_invocation(node)
        elif kind == "member_access_expression":
            self.visit_member_access(node)
        elif kind == "conditional_access_expression":
            binding = next((c for c in node.named_children if c.type == "member_binding_expression"), None)
            if binding is not None:
                self.link(self.type_of(node.child_by_field_name("condition")), "calls", node, text(binding.child_by_field_name("name")).split("<")[0])
        elif kind == "assignment_expression":
            self.visit_assignment(node)
        elif kind in ("object_creation_expression", "array_creation_expression"):
            for name in type_names(node.child_by_field_name("type")):
                self.link(name, "creates", node)
        elif kind == "variable_declaration":
            #フィールドの宣言は別に記録している
            if node.parent is not None and node.parent.type in ("field_declaration", "event_field_declaration"):
                return
            for name in type_names(node.child_by_field_name("type")):
                self.link(name, "local", node)
        elif kind == "parameter":
            #メソッド自体の引数はシグネチャとして記録済みなので、ラムダの引数だけを見る
            owner = node.parent.parent if node.parent is not None else None
            if owner is not None and owner.type == "lambda_expression":
                for name in type_names(node.child_by_field_name("type")):
                    self.link(name, "local", node)
        elif kind in ("declaration_expression", "foreach_statement", "cast_expression", "typeof_expression", "default_expression", "declaration_pattern", "catch_declaration", "local_function_statement"):
            for name in type_names(node.child_by_field_name("type")):
                self.link(name, "local", node)
        elif kind in ("as_expression", "is_expression"):
            for name in type_names(node.child_by_field_name("right")):
                self.link(name, "local", node)
        elif kind in ("type_pattern", "constant_pattern") and node.named_children:
            first = node.named_children[0]
            if first.type == "identifier":
                self.link(text(first), "local", node)

    def visit_invocation(self, node: Node):
        func = node.child_by_field_name("function")
        if func is None:
            return
        name_node = func.child_by_field_name("name") if func.type == "member_access_expression" else func
        if name_node is None:
            return
        generic_args: list[str] = []
        if name_node.type == "generic_name":
            method = text(next((c for c in name_node.named_children if c.type == "identifier"), None))
            generic_args = type_names_of_args(name_node)
        else:
            method = text(name_node)

        if method in COMPONENT_LOOKUPS:
            targets = generic_args[:1]
            args = node.child_by_field_name("arguments")
            if not targets and args is not None:
                for arg in (a for a in args.named_children if a.type == "argument" and a.named_children):
                    value = arg.named_children[-1]
                    #TryGetComponent(out _field) は、受け取り先の型から対象を判断する
                    if any(text(c) == "out" for c in arg.children):
                        if value.type == "declaration_expression":
                            targets = type_names(value.child_by_field_name("type"))[:1]
                        else:
                            found = self.type_of(value)
                            targets = [found] if found else []
                    elif value.type == "typeof_expression":
                        targets = type_names(value.child_by_field_name("type"))[:1]
            scope = COMPONENT_LOOKUPS[method]
            #x.GetComponent<T>() は x の持つコンポーネントなので、自身の GameObject とは限らない
            if scope == "same" and func.type == "member_access_expression":
                receiver = text(func.child_by_field_name("expression"))
                if receiver not in ("this", "base", "gameObject", "transform"):
                    scope = "other"
            for target in targets:
                self.link(target, f"component:{scope}", node, method)
            return

        if method in STRING_CALLS:
            args = node.child_by_field_name("arguments")
            if args is not None and args.named_children:
                first = args.named_children[0]
                if first.named_children and first.named_children[0].type == "string_literal":
                    self.strings.append({
                        "call": text(func), "value": text(first.named_children[0]).strip('"'),
                        "line": node.start_point[0] + 1,
                    })

        for arg in generic_args:
            self.link(arg, "local", node)

    def visit_member_access(self, node: Node):
        #イベントの購読は visit_assignment で記録する
        parent = node.parent
        if parent is not None and parent.type == "assignment_expression" and parent.child_by_field_name("left") == node \
                and text(parent.child_by_field_name("operator")) in ("+=", "-="):
            if self.event_owner(node) is not None:
                return

        receiver = node.child_by_field_name("expression")
        member = text(node.child_by_field_name("name")).split("<")[0]
        if receiver is None:
            return
        if receiver.type == "identifier":
            name = text(receiver)
            if self.is_variable(name):
                self.link(self.type_of(receiver), "calls", node, member)
            else:
                self.link(name, "static", node, member)
            return
        receiver_type = self.type_of(receiver)
        if receiver_type is None:
            return
        #名前空間で修飾した型名への参照は静的アクセスとして扱う
        if receiver.type == "member_access_expression" and text(receiver.child_by_field_name("name")) == receiver_type and text(receiver)[:1].isupper() \
                and self.type_of(receiver.child_by_field_name("expression")) is None:
            self.link(receiver_type, "static", node, member)
            return
        self.link(receiver_type, "calls", node, member)

    def event_owner(self, left: Node) -> str | None:
        """イベントを宣言している型を探す。派生クラス経由の購読は基底クラスまでさかのぼる"""
        event_name = text(left.child_by_field_name("name"))
        owners = self.events_by_name.get(event_name, [])
        if not owners:
            return None
        receiver_type = self.type_of(left.child_by_field_name("expression"))
        if receiver_type is not None:
            for level in self.base_chain(self.resolver.resolve(receiver_type, self.owner)):
                if any(o["name"] == level["name"] for o in owners):
                    return level["name"]
        if len(owners) == 1:
            return owners[0]["name"]
        self.unresolved_events.append(f"{self.owner['name']}: {text(left)} (line {left.start_point[0] + 1})")
        return None

    unresolved_events: list[str] = []

    def visit_assignment(self, node: Node):
        operator = text(node.child_by_field_name("operator"))
        left = node.child_by_field_name("left")
        if operator not in ("+=", "-=") or left is None or left.type != "member_access_expression":
            return
        target = self.event_owner(left)
        if target is not None:
            self.link(target, "subscribes" if operator == "+=" else "unsubscribes", node, text(left.child_by_field_name("name")))


def assembly_of(rel: str) -> str:
    """Unity のコンパイル単位を、パスと asmdef の配置から判断する"""
    if rel.startswith("Assets/Scripts/Core/"):
        return "KepoBattle.Core"
    if rel.startswith("Assets/Scripts/Data/"):
        return "KepoBattle.Data"
    if "/Editor/" in rel:
        return "Assembly-CSharp-Editor"
    return "Assembly-CSharp"


def analyze(root: Path) -> dict:
    scripts_root = root / "Assets" / "Scripts"
    types: list[dict] = []
    parse_errors: list[str] = []
    file_usings: dict[str, list[str]] = {}

    for path in sorted(scripts_root.rglob("*.cs")):
        rel = path.relative_to(root).as_posix()
        tree = PARSER.parse(path.read_bytes())
        if tree.root_node.has_error:
            lines = [n.start_point[0] + 1 for n in walk(tree.root_node) if n.type == "ERROR" or n.is_missing]
            parse_errors.append(f"{rel}: {lines[:5]}")
        file_usings[rel] = [
            text(n.child_by_field_name("name") or (n.named_children[-1] if n.named_children else None))
            for n in tree.root_node.named_children if n.type == "using_directive"
        ]
        types += collect_declarations(path, rel, tree)

    resolver = Resolver(types)
    events_by_name: dict[str, list[dict]] = defaultdict(list)
    for t in types:
        for e in t["events"]:
            events_by_name[e["name"]].append(t)

    relations: dict[tuple[str, str], dict] = {}

    def add(source: dict, target: dict, kind: str, line: int, detail: str | None = None):
        key = (full_id(source), full_id(target))
        rel = relations.setdefault(key, {"source": key[0], "target": key[1], "kinds": defaultdict(lambda: {"count": 0, "details": set(), "lines": set()})})
        entry = rel["kinds"][kind]
        entry["count"] += 1
        entry["lines"].add(line)
        if detail:
            entry["details"].add(detail)

    string_refs = []
    for t in types:
        #継承・実装
        for base in t["bases"]:
            target = resolver.resolve(base, t)
            if target is not None:
                add(t, target, "implements" if target["kind"] == "interface" else "inherits", t["line"])
        #[CustomPropertyDrawer(typeof(Foo))] のように、属性で参照している型
        if t.get("_node") is not None:
            for child in t["_node"].children:
                if child.type == "attribute_list":
                    for node in walk(child):
                        if node.type == "typeof_expression":
                            for name in type_names(node.child_by_field_name("type")):
                                target = resolver.resolve(name, t)
                                if target is not None and target is not t:
                                    add(t, target, "attribute", node.start_point[0] + 1, text(child))
        #フィールドやシグネチャに現れる型
        for field in t["fields"]:
            is_serialized = "SerializeField" in field["attributes"] or ("public" in field["modifiers"] and "static" not in field["modifiers"])
            kind = "field_serialized" if is_serialized else "field"
            for name in field["type_names"]:
                target = resolver.resolve(name, t)
                if target is not None and target is not t:
                    add(t, target, kind, field["line"], f"{field['name']}{'[]' if field['many'] else ''}")
        for prop in t["properties"]:
            for name in prop["type_names"]:
                target = resolver.resolve(name, t)
                if target is not None and target is not t:
                    add(t, target, "field" if prop["is_auto"] else "property", prop["line"], prop["name"])
        for event in t["events"]:
            for name in event["type_names"]:
                target = resolver.resolve(name, t)
                if target is not None and target is not t:
                    add(t, target, "signature", t["line"], event["name"])
        for method in t["methods"]:
            names = list(method["return_type_names"])
            for p in method["params"]:
                names += p["type_names"]
            for name in names:
                target = resolver.resolve(name, t)
                if target is not None and target is not t:
                    add(t, target, "signature", method["line"], method["name"])

        node = t.get("_node")
        if node is None:
            continue
        body = node.child_by_field_name("body")
        if body is None or t["kind"] == "enum":
            continue
        analyzer = BodyAnalyzer(t, resolver, events_by_name, add)
        for member in body.named_children:
            if member.type in TYPE_NODES:
                continue
            analyzer.analyze_member(member)
        for s in analyzer.strings:
            string_refs.append({"type": full_id(t), **s})

    duplicates = {name: [full_id(t) + " @" + t["file"] for t in found] for name, found in resolver.by_name.items() if len(found) > 1}

    out_types = []
    for t in types:
        clean = {k: v for k, v in t.items() if not k.startswith("_")}
        clean["id"] = full_id(t)
        clean["assembly"] = assembly_of(t["file"])
        parts = t["file"].split("/")
        clean["layer"] = parts[2]
        clean["area"] = "/".join(parts[2:-1])
        clean["is_abstract"] = "abstract" in t["modifiers"]
        out_types.append(clean)

    out_relations = []
    for rel in relations.values():
        kinds = {}
        for kind, entry in rel["kinds"].items():
            kinds[kind] = {"count": entry["count"], "details": sorted(entry["details"]), "lines": sorted(entry["lines"])}
        out_relations.append({"source": rel["source"], "target": rel["target"], "kinds": kinds})
    out_relations.sort(key=lambda r: (r["source"], r["target"]))

    return {
        "types": out_types,
        "relations": out_relations,
        "string_refs": string_refs,
        "usings": file_usings,
        "warnings": {"parse_errors": parse_errors, "duplicate_type_names": duplicates, "unresolved_events": sorted(set(BodyAnalyzer.unresolved_events))},
    }


if __name__ == "__main__":
    root = Path(__file__).resolve().parents[2]
    model = analyze(root)
    print(json.dumps({"types": len(model["types"]), "relations": len(model["relations"]), "warnings": model["warnings"]}, ensure_ascii=False, indent=2))
