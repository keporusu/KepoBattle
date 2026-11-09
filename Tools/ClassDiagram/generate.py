"""解析を実行し、DevDocs/Architecture にクラス図と集計結果を書き出す。

使い方は同じフォルダの README.md を参照。
"""
from __future__ import annotations

import argparse
import json
import sys
from collections import Counter, defaultdict
from pathlib import Path

#Tools 以下に __pycache__ を残さない
sys.dont_write_bytecode = True

import extract_assets
import extract_code
import views

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "DevDocs" / "Architecture"

#Unity が呼び出すメソッドは、クラスの役割の説明にならないので図から省く
UNITY_MESSAGES = {
    "Awake", "Start", "Update", "FixedUpdate", "LateUpdate", "OnEnable", "OnDisable", "OnDestroy", "OnValidate", "Reset",
    "OnTriggerEnter2D", "OnTriggerExit2D", "OnTriggerStay2D", "OnCollisionEnter2D", "OnCollisionExit2D", "OnCollisionStay2D",
    "OnDrawGizmos", "OnDrawGizmosSelected", "OnGUI", "OnStateEnter", "OnStateUpdate", "OnStateExit",
}
#値を保持するだけの型は、依存の集計から外す
VALUE_KINDS = {"enum", "struct", "delegate", "record"}
#全体図で省略する矢印の説明
QUIET_LABELS = {"シングルトン経由", "静的参照", "呼び出し", "値を参照", "参照", "引数・戻り値で参照"}


class Model:
    def __init__(self, code: dict, assets: dict):
        self.code = code
        self.assets = assets
        self.types = {t["id"]: t for t in code["types"]}
        self.by_name = {t["name"]: t for t in code["types"]}
        self.by_id = {t["id"]: t for t in code["types"]}
        self.relations = {(r["source"], r["target"]): r for r in code["relations"]}
        self.prefabs = {p["name"]: p for p in assets["prefabs"]}

    def chain(self, t: dict) -> list[dict]:
        result, current, seen = [], t, set()
        while current is not None and current["id"] not in seen:
            seen.add(current["id"])
            result.append(current)
            current = next((self.by_name[b] for b in current["bases"] if b in self.by_name and self.by_name[b]["kind"] == "class"), None)
        return result

    def is_monobehaviour(self, t: dict) -> bool:
        return any("MonoBehaviour" in level["bases"] for level in self.chain(t))

    def is_unity_object(self, t: dict) -> bool:
        return self.is_monobehaviour(t) or "ScriptableObject" in t["bases"]

    def has_method_call(self, t: dict, names: list[str]) -> bool:
        """使っているメンバーにメソッドが含まれるか。フィールドを読むだけなら False"""
        methods = {m["name"] for level in self.chain(t) for m in level["methods"]}
        return any(name in methods for name in names)

    def is_singleton(self, t: dict) -> bool:
        for member in t["fields"] + t["properties"]:
            if "static" in member["modifiers"] and "public" in member["modifiers"] and member["type"] == t["name"]:
                return True
        return False

    def member(self, t: dict, name: str) -> str | None:
        """型が宣言しているメンバーを、図に載せる表記にして返す。無ければ None"""
        for level in self.chain(t):
            for m in level["methods"]:
                if m["name"] == name and not m["is_constructor"]:
                    return f"+{name}()" + ("$" if "static" in m["modifiers"] else "")
            for m in level["properties"] + level["fields"]:
                if m["name"] == name:
                    return f"+{name} : {simple_type(m['type'])}" + ("$" if "static" in m["modifiers"] else "")
            for m in level["events"]:
                if m["name"] == name:
                    return f"+{name} event"
        return None

    def attached_scripts(self) -> dict[str, set[str]]:
        usage: dict[str, set[str]] = defaultdict(set)
        for group in ("prefabs", "scenes"):
            for item in self.assets[group]:
                for obj in item["objects"]:
                    for comp in obj["components"]:
                        if comp["kind"] == "script":
                            usage[comp["name"]].add(item["name"])
                for comp in item["added_components"]:
                    if comp["kind"] == "script":
                        usage[comp["name"]].add(item["name"])
        return usage


def simple_type(type_text: str) -> str:
    """Mermaid で崩れる記号を避ける"""
    return type_text.replace("<", "~").replace(">", "~").replace("?", "").replace(" ", "").replace(",", "/").split(".")[-1]


def safe_id(name: str) -> str:
    return "".join(c if c.isalnum() or c == "_" else "_" for c in name)


def stereotype(model: Model, t: dict) -> str | None:
    if t["kind"] == "interface":
        return "interface"
    if t["kind"] in VALUE_KINDS:
        return t["kind"]
    if model.is_singleton(t):
        return "singleton"
    if "ScriptableObject" in t["bases"]:
        return "ScriptableObject"
    if t["assembly"] == "Assembly-CSharp-Editor":
        return "Editor"
    if "StateMachineBehaviour" in t["bases"]:
        return "StateMachineBehaviour"
    if not model.is_monobehaviour(t) and t["kind"] == "class":
        return "pure C#"
    return None


def used_members(model: Model, target: dict, sources: list[str]) -> list[str]:
    """図の中の他のクラスから実際に使われているメンバー"""
    names: list[str] = []
    for source in sources:
        relation = model.relations.get((source, target["id"]))
        if relation is None:
            continue
        for kind in ("static", "calls", "subscribes"):
            if kind in relation["kinds"]:
                names += relation["kinds"][kind]["details"]
    lines = []
    for name in sorted(set(names)):
        label = model.member(target, name)
        if label is not None:
            lines.append(label)
    return lines


def public_members(model: Model, t: dict) -> list[str]:
    lines = []
    for m in t["events"] + t["properties"]:
        if "public" in m["modifiers"]:
            lines.append(model.member(t, m["name"]))
    for m in t["methods"]:
        if "public" in m["modifiers"] and not m["is_constructor"] and m["name"] not in UNITY_MESSAGES:
            lines.append(model.member(t, m["name"]))
    return list(dict.fromkeys(l for l in lines if l))


def edge_label(relation: dict, model: Model) -> tuple[str, bool]:
    """依存の矢印につける説明と、実線にするか（参照を保持しているか）を返す"""
    kinds = relation["kinds"]
    parts = []
    target = model.types[relation["target"]]
    holding = "field" in kinds or "field_serialized" in kinds
    if "component:same" in kinds:
        parts.append("同じGOから取得")
    elif "component:other" in kinds:
        parts.append("他のGOから取得")
    elif "component:children" in kinds:
        parts.append("子から取得")
    elif "component:parent" in kinds:
        parts.append("親から取得")
    elif "component:add" in kinds:
        parts.append("実行時に追加")
    elif "component:scene" in kinds:
        parts.append("シーンから検索")
    elif "field_serialized" in kinds:
        source = model.types[relation["source"]]
        if not model.is_unity_object(source):
            #JSON などで読み書きするデータクラスは、Inspector を通らない
            parts.append("保持")
        elif model.is_unity_object(target):
            parts.append("Inspector で接続")
        else:
            parts.append("Inspector で設定")
    elif "field" in kinds:
        parts.append("保持")
    if "subscribes" in kinds:
        events = kinds["subscribes"]["details"]
        parts.append("購読 " + ", ".join(events[:2]) + (f" ほか{len(events) - 2}" if len(events) > 2 else ""))
    if "static" in kinds and not parts:
        parts.append("シングルトン経由" if model.is_singleton(target) else "静的参照")
    if "creates" in kinds and not parts:
        parts.append("生成")
    if "calls" in kinds and not parts:
        parts.append("呼び出し" if model.has_method_call(target, kinds["calls"]["details"]) else "値を参照")
    if not parts:
        parts.append("引数・戻り値で参照" if "signature" in kinds else "参照")
    return " / ".join(parts), holding


def class_diagram(model: Model, names: list[str], focus: set[str] = frozenset(), direction: str = "LR", member_limit: int = 7,
                  focus_edges_only: bool = False, grouped: bool = True, quiet_labels: bool = False) -> str:
    lines = ["classDiagram", f"    direction {direction}"]
    ids = [model.by_id[n]["id"] for n in names]
    groups: dict[str, list[dict]] = defaultdict(list)
    for name in names:
        t = model.by_id[name]
        groups[t["area"]].append(t)

    def body(t: dict) -> list[str]:
        members = public_members(model, t) if t["name"] in focus else used_members(model, t, [i for i in ids if i != t["id"]])
        if member_limit == 0:
            return []
        result = members[:member_limit]
        if len(members) > member_limit:
            result.append(f"… 他{len(members) - member_limit}件")
        return result

    for area, members in groups.items():
        #囲みを付けると矢印が他のクラスを貫きやすくなるので、中心のある図では付けない
        if grouped:
            lines.append(f"    namespace {safe_id(area)} {{")
        for t in members:
            mermaid_id = safe_id(t["id"])
            lines.append(f"        class {mermaid_id} {{")
            mark = stereotype(model, t)
            if mark:
                lines.append(f"            <<{mark}>>")
            for member in body(t):
                lines.append(f"            {member}")
            lines.append("        }")
        if grouped:
            lines.append("    }")

    for source in ids:
        for target in ids:
            relation = model.relations.get((source, target))
            if relation is None:
                continue
            kinds = relation["kinds"]
            src, dst = safe_id(source), safe_id(target)
            if "inherits" in kinds:
                lines.append(f"    {dst} <|-- {src}")
                continue
            if "implements" in kinds:
                lines.append(f"    {dst} <|.. {src}")
                continue
            #周辺クラス同士の依存は、中心のクラスを読む妨げになるので省く
            if focus_edges_only and source not in focus and target not in focus:
                continue
            label, holding = edge_label(relation, model)
            arrow = "-->" if holding else "..>"
            #点線だけで分かる説明は省き、図の文字を減らす
            if quiet_labels and label in QUIET_LABELS:
                lines.append(f"    {src} {arrow} {dst}")
            else:
                lines.append(f"    {src} {arrow} {dst} : {label}")
    return "\n".join(lines)


def layer_edges(model: Model) -> Counter:
    edges: Counter = Counter()
    for relation in model.code["relations"]:
        s, t = model.types[relation["source"]], model.types[relation["target"]]
        if s["layer"] != t["layer"]:
            edges[(s["layer"], t["layer"])] += 1
    return edges


def is_reversed(source: str, target: str) -> bool:
    rank = views.LAYER_RANK
    return source in rank and target in rank and rank[source] > rank[target]


def layer_diagram(model: Model) -> str:
    edges = layer_edges(model)
    counts = Counter(t["layer"] for t in model.code["types"])
    lines = ["flowchart TB"]
    grouped = [
        ("Assembly-CSharp-Editor", ["Editor"]),
        ("Assembly-CSharp", ["UI", "Systems", "Components"]),
        ("KepoBattle.Core", ["Core"]),
        ("KepoBattle.Data", ["Data"]),
    ]
    for assembly, layers in grouped:
        lines.append(f'    subgraph {safe_id(assembly)}["{assembly}"]')
        for layer in layers:
            lines.append(f'        {layer}["{layer}<br/>{counts[layer]} 型"]')
        lines.append("    end")
    reversed_indexes = []
    index = 0
    for (source, target), count in sorted(edges.items()):
        lines.append(f"    {source} -->|{count}| {target}")
        if is_reversed(source, target):
            reversed_indexes.append(index)
        index += 1
    if reversed_indexes:
        lines.append(f"    linkStyle {','.join(map(str, reversed_indexes))} stroke:#d1242f,stroke-width:2px")
    return "\n".join(lines)


def prefab_diagram(model: Model, name: str) -> str:
    """prefab のコンポーネント構成。バリアント元ごとに囲みを分ける"""
    prefab = model.prefabs[name]
    chain = []
    current = prefab
    while current is not None:
        chain.append(current)
        current = model.prefabs.get(current["variant_of"]) if current["variant_of"] else None
    chain.reverse()

    #横向きにすると、同じ prefab のコンポーネントが縦に並び、図の幅を抑えられる
    lines = ["flowchart LR"]
    node_of: dict[str, str] = {}
    counter = {"n": 0}

    def node(label: str, kind: str, indent: str = "        ", on_object: str | None = None) -> None:
        counter["n"] += 1
        node_id = f"n{counter['n']}"
        node_of.setdefault(label, node_id)
        text = f"{label}<br/>({on_object} に追加)" if on_object else label
        shape = f'{node_id}["{text}"]' if kind == "script" else f'{node_id}("{text}")'
        lines.append(f"{indent}{shape}")
        lines.append(f"{indent}class {node_id} {kind}")

    def add_prefab_contents(item: dict, indent: str = "        ") -> None:
        for obj in item["objects"]:
            is_root = obj["parent"] is None
            comps = [c for c in obj["components"] if c["kind"] != "package" or c["name"] in ("TextMeshProUGUI",)]
            if not is_root:
                lines.append(f'{indent}subgraph {safe_id(item["name"] + "_" + obj["name"])}["{obj["name"]} (子オブジェクト)"]')
            for comp in comps:
                node(comp["name"], "script" if comp["kind"] == "script" else "native", indent + ("    " if not is_root else ""))
            if not is_root:
                lines.append(f"{indent}end")
        for comp in item["added_components"]:
            on_object = comp.get("on_object")
            #ルートへの追加はわざわざ書かない
            node(comp["name"], "script" if comp["kind"] == "script" else "native", indent, None if on_object == chain[0]["name"] else on_object)

    for level, item in enumerate(chain):
        lines.append(f'    subgraph {safe_id("P_" + item["name"])}["{item["name"]}.prefab"]')
        add_prefab_contents(item)
        for nested in item["nested"]:
            child = model.prefabs.get(nested["prefab"])
            if child is None:
                continue
            lines.append(f'        subgraph {safe_id("N_" + child["name"])}["{child["name"]} (入れ子 prefab)"]')
            #子 prefab の Transform 以外を載せる
            seen = set()
            for comp in extract_assets.effective_components(child, model.prefabs):
                if comp["name"] in seen or comp["kind"] == "package":
                    continue
                seen.add(comp["name"])
                node(comp["name"], "script" if comp["kind"] == "script" else "native", "            ")
            lines.append("        end")
        lines.append("    end")

    #バリアントの関係
    for base, derived in zip(chain, chain[1:]):
        lines.append(f'    {safe_id("P_" + base["name"])} ==>|"バリアント"| {safe_id("P_" + derived["name"])}')

    #Inspector の接続
    edges = []
    assets: dict[str, list[str]] = defaultdict(list)
    runtime = []
    for comp in extract_assets.effective_components(prefab, model.prefabs):
        for ref in comp["refs"]:
            target = ref["target"]
            field = ref["field"].split(".Array")[0].split("[")[0]
            if target["kind"] == "component" and target["name"] in node_of and comp["name"] in node_of:
                edges.append((comp["name"], target["name"], field))
            elif target["kind"] == "prefab" and comp["name"] in node_of:
                runtime.append((comp["name"], target["name"], field))
            elif target["kind"] == "asset" and comp["kind"] == "script" and comp["name"] in node_of and target["path"].endswith(".asset"):
                assets[(comp["name"], field)].append(target["name"])
            elif target["kind"] == "gameobject" and comp["name"] in node_of and target.get("prefab") in model.prefabs:
                lines.append(f'    {node_of[comp["name"]]} -->|"{field}"| {safe_id("N_" + target["prefab"])}')

    for (source, field), names in sorted(assets.items()):
        counter["n"] += 1
        node_id = f"n{counter['n']}"
        lines.append(f'    {node_id}[/"{", ".join(sorted(set(names)))}"/]')
        lines.append(f"    class {node_id} asset")
        lines.append(f'    {node_of[source]} -->|"{field}"| {node_id}')
    for source, target, field in runtime:
        counter["n"] += 1
        node_id = f"n{counter['n']}"
        child = model.prefabs.get(target)
        scripts = []
        if child is not None:
            scripts = [c["name"] for c in extract_assets.effective_components(child, model.prefabs) if c["kind"] == "script"]
        label = f"{target}.prefab" + (f"<br/>{', '.join(scripts)}" if scripts else "")
        lines.append(f'    {node_id}[["{label}"]]')
        lines.append(f"    class {node_id} asset")
        lines.append(f'    {node_of[source]} -.->|"{field} (実行時に生成)"| {node_id}')
    for source, target, field in dict.fromkeys(edges):
        lines.append(f'    {node_of[source]} -->|"{field}"| {node_of[target]}')

    lines.append("    classDef script fill:#ddf4ff,stroke:#0969da,color:#0a3069")
    lines.append("    classDef native fill:#f6f8fa,stroke:#8c959f,color:#424a53")
    lines.append("    classDef asset fill:#fff8c5,stroke:#9a6700,color:#633c01")
    return "\n".join(lines)


def strongly_connected(graph: dict[str, set[str]]) -> list[list[str]]:
    index: dict[str, int] = {}
    low: dict[str, int] = {}
    stack: list[str] = []
    on_stack: set[str] = set()
    result: list[list[str]] = []

    def visit(v: str):
        index[v] = low[v] = len(index)
        stack.append(v)
        on_stack.add(v)
        for w in graph.get(v, ()):
            if w not in index:
                visit(w)
                low[v] = min(low[v], low[w])
            elif w in on_stack:
                low[v] = min(low[v], index[w])
        if low[v] == index[v]:
            component = []
            while True:
                w = stack.pop()
                on_stack.discard(w)
                component.append(w)
                if w == v:
                    break
            if len(component) > 1:
                result.append(sorted(component))

    sys.setrecursionlimit(10000)
    for v in list(graph):
        if v not in index:
            visit(v)
    return result


def class_graph(model: Model) -> dict[str, set[str]]:
    """クラスとインターフェースの間の依存。値型は除く"""
    graph: dict[str, set[str]] = defaultdict(set)
    for relation in model.code["relations"]:
        s, t = model.types[relation["source"]], model.types[relation["target"]]
        if s["kind"] in VALUE_KINDS or t["kind"] in VALUE_KINDS:
            continue
        graph[relation["source"]].add(relation["target"])
    return graph


def describe_relation(relation: dict) -> str:
    names = {
        "inherits": "継承", "implements": "実装", "field_serialized": "Inspector/公開フィールド", "field": "フィールド",
        "property": "プロパティ", "component:same": "GetComponent", "component:other": "他のGOへの GetComponent", "component:children": "GetComponentInChildren",
        "component:parent": "GetComponentInParent", "component:add": "AddComponent", "component:scene": "シーン検索",
        "static": "静的参照", "calls": "呼び出し", "subscribes": "イベント購読", "unsubscribes": "イベント解除",
        "signature": "引数・戻り値", "creates": "生成", "local": "ローカル変数", "attribute": "属性",
    }
    return "、".join(names.get(kind, kind) for kind in relation["kinds"])


def write_overview(model: Model):
    usage = model.attached_scripts()
    types = model.code["types"]
    kind_counts = Counter(t["kind"] for t in types)
    edges = layer_edges(model)
    table = "\n".join(
        f"| {s} | {t} | {c} | {'**逆向き**' if is_reversed(s, t) else ''} |"
        for (s, t), c in sorted(edges.items())
    )
    text = f"""# 全体図

> このファイルは `Tools/ClassDiagram/generate.py` が生成します。直接編集しないでください。

対象: `Assets/Scripts` の型 {len(types)} 個（クラス {kind_counts['class']}、構造体 {kind_counts['struct']}、列挙型 {kind_counts['enum']}、インターフェース {kind_counts['interface']}）

## 層の間の依存

数字は依存している型の組の数です。赤い矢印は、`Components → UI / Systems → Core → Data` の向きに逆らう依存です。

```mermaid
{layer_diagram(model)}
```

| 依存元 | 依存先 | 型の組の数 | 備考 |
|---|---|---:|---|
{table}

## 主要クラスのつながり

実線は参照をフィールドで保持している依存、点線は呼び出しやイベントだけの依存です。メンバーは省き、説明のない点線はシングルトンや静的メソッドなどの呼び出しです。

```mermaid
{class_diagram(model, views.OVERVIEW_CLASSES, direction="LR", member_limit=0, grouped=False, quiet_labels=True)}
```
"""
    (OUT / "01-overview.md").write_text(text, encoding="utf-8")


CLASS_LEGEND = "実線はフィールドで参照を保持している依存、点線は呼び出しやイベントだけの依存です。"
PREFAB_LEGEND = "実線は Inspector での接続、点線は実行時の生成です。青はプロジェクトのスクリプト、灰色は Unity とパッケージのコンポーネント、黄色はアセットです。"


def render_section(model: Model, section: dict) -> str:
    if section["kind"] == "prefab":
        return f"""## {section['heading']}

{section['text']}{PREFAB_LEGEND}

```mermaid
{prefab_diagram(model, section['prefab'])}
```
"""
    focus = set(section.get("focus", []))
    members = "各クラスには図内で使われているメンバーを載せています。"
    if focus:
        names = "、".join(f"`{name}`" for name in sorted(focus))
        members = f"{names} には公開メンバー、他のクラスには図内で使われているメンバーを載せています。"
    extra = ""
    if section.get("implementers_of"):
        name = section["implementers_of"]
        implementers = sorted(t["id"] for t in model.code["types"] if name in t["bases"])
        extra = f"\n\n`{name}` を実装しているクラス: " + "、".join(f"`{i}`" for i in implementers)
    diagram = class_diagram(
        model, section["classes"], focus=focus, direction=section.get("direction", "LR"),
        member_limit=section.get("member_limit", 7), focus_edges_only=section.get("focus_edges_only", False), grouped=False,
    )
    return f"""## {section['heading']}

{section['text']}{CLASS_LEGEND}{members}{extra}

```mermaid
{diagram}
```
"""


def write_feature(model: Model, view: dict):
    body = "\n".join(render_section(model, section) for section in view["sections"])
    text = f"""# {view['title']}

> このファイルは `Tools/ClassDiagram/generate.py` が生成します。直接編集しないでください。

{body}"""
    (OUT / view["file"]).write_text(text, encoding="utf-8")


def write_metrics(model: Model):
    graph = class_graph(model)
    fan_out = Counter({k: len(v) for k, v in graph.items()})
    fan_in: Counter = Counter()
    for targets in graph.values():
        for target in targets:
            fan_in[target] += 1
    usage = model.attached_scripts()
    unattached = []
    for t in model.code["types"]:
        if t["kind"] != "class" or not model.is_monobehaviour(t) or t["name"] in usage:
            continue
        subclasses = [o["name"] for o in model.code["types"] if t["name"] in o["bases"]]
        added_by = [r["source"] for r in model.code["relations"] if r["target"] == t["id"] and "component:add" in r["kinds"]]
        if subclasses:
            note = f"基底クラス（派生: {', '.join(subclasses)}）"
        elif added_by:
            note = f"`{', '.join(added_by)}` が実行時に追加"
        else:
            note = "**配置なし・実行時の追加もなし**"
        unattached.append(f"| `{t['name']}` | {note} |")

    cycles = strongly_connected(graph)
    two_way = []
    for (source, target), relation in sorted(model.relations.items()):
        back = model.relations.get((target, source))
        if source < target and back is not None and source in graph and target in graph.get(source, ()) and source in graph.get(target, ()):
            two_way.append(f"| `{source}` | {describe_relation(relation)} | `{target}` | {describe_relation(back)} |")

    cross = []
    for relation in model.code["relations"]:
        s, t = model.types[relation["source"]], model.types[relation["target"]]
        if is_reversed(s["layer"], t["layer"]):
            cross.append(f"| {s['layer']} → {t['layer']} | `{s['id']}` | `{t['id']}` | {describe_relation(relation)} |")

    drawn = set(views.OVERVIEW_CLASSES)
    for view in views.FEATURE_VIEWS:
        for section in view["sections"]:
            drawn.update(section.get("classes", []))
            if section["kind"] == "prefab":
                for comp in extract_assets.effective_components(model.prefabs[section["prefab"]], model.prefabs):
                    if comp["kind"] == "script":
                        drawn.add(comp["name"])
    undrawn = [f"`{t['id']}`" for t in model.code["types"] if t["kind"] in ("class", "interface") and t["id"] not in drawn]

    string_rows = defaultdict(set)
    for ref in model.code["string_refs"]:
        string_rows[(ref["call"].split(".")[-1], ref["value"])].add(ref["type"])
    strings = [f"| `{call}` | `{value}` | {', '.join(f'`{t}`' for t in sorted(users))} |" for (call, value), users in sorted(string_rows.items())]

    warnings = model.code["warnings"]
    text = f"""# 依存の集計

> このファイルは `Tools/ClassDiagram/generate.py` が生成します。直接編集しないでください。

構造体・列挙型を除いた、クラスとインターフェースの間の依存を集計しています。

## 依存先の多いクラス

| クラス | 依存先の数 |
|---|---:|
{chr(10).join(f"| `{k}` | {v} |" for k, v in fan_out.most_common(10))}

## 依存元の多いクラス

| クラス | 依存元の数 |
|---|---:|
{chr(10).join(f"| `{k}` | {v} |" for k, v in fan_in.most_common(10))}

## 循環依存

互いに依存が行き来しているクラスのまとまりです。

{chr(10).join(f"- {len(c)} クラス: {', '.join(f'`{n}`' for n in c)}" for c in cycles) or "なし"}

### 2クラスの間で直接行き来している組

| クラス A | A → B の依存 | クラス B | B → A の依存 |
|---|---|---|---|
{chr(10).join(two_way)}

## 層の向きに逆らう依存

基準は `Components → UI / Systems → Core → Data` です。`Components` が `Systems` の状態を読む設計に合わせています。asmdef で強制されているのは `Core` と `Data` だけです。`Systems` と `UI` は同じ順位として扱い、どちらの向きも逆向きと見なしていません。

| 向き | 依存元 | 依存先 | 依存の種類 |
|---|---|---|---|
{chr(10).join(sorted(cross))}

## prefab にもシーンにも載っていない MonoBehaviour

| クラス | 状況 |
|---|---|
{chr(10).join(unattached)}

## 文字列での参照

型の依存として現れない、名前を文字列で指定している呼び出しです。

| 呼び出し | 文字列 | 呼び出し元 |
|---|---|---|
{chr(10).join(strings)}

## どの図にも載っていないクラス

{'、'.join(undrawn) or 'なし'}

## 解析の警告

- 構文エラー: {len(warnings['parse_errors'])} 件
- 同名の型: {', '.join(warnings['duplicate_type_names']) or 'なし'}
- 購読先を特定できなかったイベント: {len(warnings['unresolved_events'])} 件
- 読み込めなかった prefab / シーン: {len(model.assets['errors'])} 件
"""
    (OUT / "03-metrics.md").write_text(text, encoding="utf-8")


def write_index():
    rows = [("01-overview.md", "層の間の依存と、主要クラスのつながり"), ("03-metrics.md", "依存の多いクラス、循環依存、層の向きに逆らう依存、未配置のスクリプト、文字列での参照")]
    rows += [(view["file"], view["summary"]) for view in views.FEATURE_VIEWS]
    table = "\n".join(f"| [{name}]({name}) | {summary} |" for name, summary in sorted(rows))
    text = f"""# アーキテクチャ図

> このフォルダのファイルは `Tools/ClassDiagram/generate.py` が生成します。直接編集しないでください。

| ファイル | 内容 |
|---|---|
{table}

## 図に出ないもの

- `PlaySe("Jump")` や `animator.Play("Stand")` のような文字列での参照は、矢印になりません（`03-metrics.md` に一覧があります）。
- コンパイラを使わず構文から型を推定しているので、`var` の型が推定できない呼び出しは漏れることがあります。
- 列挙型は図に載せていません。構造体は、機能別の図で役割を説明するものだけ載せています。

## 図を読むときの注意

- 矢印が無関係なクラスの箱の裏を通ることがあります。線が箱から出ているように見えても、依存元はその箱とは限りません。
"""
    (OUT / "README.md").write_text(text, encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--json", type=Path, help="解析結果の JSON を書き出す先（省略時は書き出さない）")
    args = parser.parse_args()

    code = extract_code.analyze(ROOT)
    assets = extract_assets.analyze(ROOT)
    model = Model(code, assets)
    OUT.mkdir(parents=True, exist_ok=True)
    write_overview(model)
    for view in views.FEATURE_VIEWS:
        write_feature(model, view)
    write_metrics(model)
    write_index()
    if args.json:
        args.json.write_text(json.dumps({"code": code, "assets": assets}, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    warnings = code["warnings"]
    print(f"型 {len(code['types'])} / 依存 {len(code['relations'])} / prefab {len(assets['prefabs'])} / シーン {len(assets['scenes'])}")
    print(f"構文エラー {len(warnings['parse_errors'])} / 読み込み失敗 {len(assets['errors'])}")
    print(f"出力先: {OUT.relative_to(ROOT)}")


if __name__ == "__main__":
    main()
