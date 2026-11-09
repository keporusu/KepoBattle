"""prefab とシーンの YAML から、コンポーネントの構成と Inspector での接続を取り出す。

コードだけでは分からない「どのオブジェクトにどのスクリプトが載っているか」
「SerializeField に実際に何が刺さっているか」を補うために使う。
"""
from __future__ import annotations

import re
from pathlib import Path

import yaml

try:
    from yaml import CSafeLoader as Loader
except ImportError:
    from yaml import SafeLoader as Loader

DOC_HEADER = re.compile(r"^--- !u!(\d+) &(-?\d+)( stripped)?[ \t]*$", re.M)
#解析の対象外にするフォルダ（サンプルと復旧用の一時ファイル）
EXCLUDED_DIRS = ("Assets/2D Animated Characters samples/", "Assets/_Recovery/", "Assets/Plugins/")
GUID_LINE = re.compile(r"^guid: ([0-9a-f]{32})", re.M)
UNITY_INTERNAL = {"m_ObjectHideFlags", "m_CorrespondingSourceObject", "m_PrefabInstance", "m_PrefabAsset", "m_GameObject",
                  "m_Enabled", "m_EditorHideFlags", "m_Script", "m_Name", "m_EditorClassIdentifier"}


def build_guid_map(root: Path) -> dict[str, str]:
    """GUID → Assets 以下のパス"""
    table = {}
    for meta in (root / "Assets").rglob("*.meta"):
        with meta.open(encoding="utf-8", errors="ignore") as f:
            match = GUID_LINE.search(f.read(200))
        if match:
            table[match.group(1)] = meta.relative_to(root).as_posix()[:-5]
    #Unity 標準 UI などパッケージのスクリプトも名前で表示できるようにする
    package_cache = root / "Library" / "PackageCache"
    if package_cache.exists():
        for meta in package_cache.rglob("*.cs.meta"):
            with meta.open(encoding="utf-8", errors="ignore") as f:
                match = GUID_LINE.search(f.read(200))
            if match:
                table.setdefault(match.group(1), "package:" + meta.name[:-5])
    return table


def read_documents(path: Path) -> dict[int, dict]:
    source = path.read_text(encoding="utf-8", errors="ignore")
    headers = list(DOC_HEADER.finditer(source))
    documents = {}
    for i, header in enumerate(headers):
        end = headers[i + 1].start() if i + 1 < len(headers) else len(source)
        body = yaml.load(source[header.end():end], Loader=Loader) or {}
        type_name, data = next(iter(body.items()))
        documents[int(header.group(2))] = {
            "class_id": int(header.group(1)),
            "stripped": bool(header.group(3)),
            "type": type_name,
            "data": data or {},
        }
    return documents


def references(value, path: str = ""):
    """シリアライズされた値から、オブジェクト参照だけを取り出す"""
    if isinstance(value, dict):
        if "fileID" in value and set(value) <= {"fileID", "guid", "type"}:
            if value["fileID"] != 0:
                yield path, value
            return
        for key, child in value.items():
            yield from references(child, f"{path}.{key}" if path else key)
    elif isinstance(value, list):
        for child in value:
            yield from references(child, f"{path}[]")


def describe_external(guid: str, guid_map: dict) -> dict:
    asset = guid_map.get(guid)
    if asset is None:
        return {"kind": "package", "name": guid}
    if asset.startswith("package:"):
        return {"kind": "package", "name": Path(asset[len("package:"):]).stem}
    name = Path(asset).stem
    if asset.endswith(".prefab"):
        return {"kind": "prefab", "name": name, "path": asset}
    if asset.endswith(".cs"):
        return {"kind": "script", "name": name, "path": asset}
    return {"kind": "asset", "name": name, "path": asset}


def find_in_source(guid: str, file_id: int, root: Path, guid_map: dict, cache: dict, depth: int = 0) -> dict | None:
    """prefab の fileID が指す実体を、元の prefab までたどって探す"""
    asset = guid_map.get(guid)
    if asset is None or not asset.endswith(".prefab") or depth > 8:
        return None
    if asset not in cache:
        cache[asset] = read_documents(root / asset)
    documents = cache[asset]
    doc = documents.get(file_id)
    if doc is not None:
        if not doc["stripped"]:
            return doc
        source = doc["data"].get("m_CorrespondingSourceObject") or {}
        return find_in_source(source["guid"], source["fileID"], root, guid_map, cache, depth + 1) if source.get("guid") else None
    #入れ子の prefab 内を指す fileID は「内側の fileID XOR インスタンスの fileID」の下位63ビットになっている
    for instance_id, instance in documents.items():
        if instance["type"] != "PrefabInstance":
            continue
        inner_guid = (instance["data"].get("m_SourcePrefab") or {}).get("guid")
        if inner_guid:
            found = find_in_source(inner_guid, (file_id ^ instance_id) & 0x7FFFFFFFFFFFFFFF, root, guid_map, cache, depth + 1)
            if found is not None:
                return found
    return None


def find_script_in_source(guid: str, file_id: int, root: Path, guid_map: dict, cache: dict) -> str | None:
    """prefab の fileID が指すコンポーネントのスクリプト名を返す"""
    doc = find_in_source(guid, file_id, root, guid_map, cache)
    if doc is None or doc["type"] != "MonoBehaviour":
        return None
    script = (doc["data"].get("m_Script") or {}).get("guid")
    return describe_external(script, guid_map)["name"] if script else None


def find_object_name_in_source(guid: str, file_id: int, root: Path, guid_map: dict, cache: dict) -> str | None:
    """prefab の fileID が指す GameObject の名前を返す"""
    doc = find_in_source(guid, file_id, root, guid_map, cache)
    return doc["data"].get("m_Name") if doc is not None else None


def analyze_file(path: Path, root: Path, guid_map: dict, cache: dict) -> dict:
    documents = read_documents(path)

    def script_name(doc: dict) -> dict:
        guid = (doc["data"].get("m_Script") or {}).get("guid")
        return describe_external(guid, guid_map) if guid else {"kind": "missing", "name": "(Missing Script)"}

    def owner_prefab(doc: dict) -> str | None:
        source = doc["data"].get("m_CorrespondingSourceObject") or {}
        return describe_external(source["guid"], guid_map)["name"] if source.get("guid") else None

    def component_label(doc: dict) -> str:
        if doc["type"] == "MonoBehaviour":
            if doc["stripped"]:
                return f"{owner_prefab(doc) or '?'}内のスクリプト"
            return script_name(doc)["name"]
        return doc["type"]

    def game_object_of(doc: dict) -> dict | None:
        ref = (doc["data"].get("m_GameObject") or {}).get("fileID")
        return documents.get(ref)

    def describe_target(ref: dict) -> dict:
        if ref.get("guid"):
            result = describe_external(ref["guid"], guid_map)
            return result
        target = documents.get(ref["fileID"])
        if target is None:
            return {"kind": "unknown", "name": str(ref["fileID"])}
        if target["type"] == "GameObject":
            return {"kind": "gameobject", "name": target["data"].get("m_Name") or f"({owner_prefab(target)}のルート)", "prefab": owner_prefab(target)}
        go = game_object_of(target)
        go_name = go["data"].get("m_Name", "") if go else ""
        return {"kind": "component", "name": component_label(target), "object": go_name}

    #Transform の親子から階層を組み立てる
    transforms = {fid: d for fid, d in documents.items() if d["type"] in ("Transform", "RectTransform")}
    objects = []
    for fid, doc in documents.items():
        if doc["type"] != "GameObject" or doc["stripped"]:
            continue
        components = []
        parent_name = None
        for entry in doc["data"].get("m_Component") or []:
            comp_id = (entry.get("component") or {}).get("fileID")
            comp = documents.get(comp_id)
            if comp is None:
                continue
            if comp["type"] in ("Transform", "RectTransform"):
                father = documents.get((comp["data"].get("m_Father") or {}).get("fileID"))
                if father is not None:
                    father_go = game_object_of(father)
                    parent_name = (father_go["data"].get("m_Name") if father_go else None) or "(prefab)"
                continue
            if comp["type"] == "MonoBehaviour":
                script = script_name(comp)
                refs = []
                for field, ref in references({k: v for k, v in comp["data"].items() if k not in UNITY_INTERNAL}):
                    refs.append({"field": field, "target": describe_target(ref)})
                components.append({"name": script["name"], "kind": script["kind"], "refs": refs})
            else:
                components.append({"name": comp["type"], "kind": "native", "refs": []})
        objects.append({"name": doc["data"].get("m_Name", ""), "parent": parent_name, "components": components})

    #入れ子の prefab / バリアント
    nested = []
    variant_of = None
    overrides = []
    removed_components = []
    for doc in documents.values():
        if doc["type"] != "PrefabInstance":
            continue
        source = (doc["data"].get("m_SourcePrefab") or {}).get("guid")
        info = describe_external(source, guid_map) if source else {"name": "?"}
        modification = doc["data"].get("m_Modification", {})
        for removed in modification.get("m_RemovedComponents") or []:
            if removed.get("guid"):
                removed_components.append(find_script_in_source(removed["guid"], removed["fileID"], root, guid_map, cache) or "(ネイティブ)")
        for entry in modification.get("m_Modifications") or []:
            value = entry.get("objectReference") or {}
            target = entry.get("target") or {}
            if value.get("fileID") and target.get("guid"):
                script = find_script_in_source(target["guid"], target["fileID"], root, guid_map, cache)
                if script is not None:
                    overrides.append({"component": script, "field": entry.get("propertyPath"), "target": describe_target(value)})
        parent = documents.get((modification.get("m_TransformParent") or {}).get("fileID"))
        if parent is None:
            if variant_of is None and path.suffix == ".prefab":
                variant_of = info["name"]
            else:
                nested.append({"prefab": info["name"], "parent": None})
        else:
            go = game_object_of(parent)
            nested.append({"prefab": info["name"], "parent": go["data"].get("m_Name") if go else None})

    #prefab に追加されたコンポーネント（バリアントなど、GameObject が stripped のもの）
    added = []
    for doc in documents.values():
        if doc["stripped"] or doc["type"] in ("GameObject", "Transform", "RectTransform", "PrefabInstance"):
            continue
        go = game_object_of(doc)
        if go is None or not go["stripped"]:
            continue
        source = go["data"].get("m_CorrespondingSourceObject") or {}
        on_object = find_object_name_in_source(source["guid"], source["fileID"], root, guid_map, cache) if source.get("guid") else None
        if doc["type"] == "MonoBehaviour":
            script = script_name(doc)
            refs = [{"field": f, "target": describe_target(r)} for f, r in references({k: v for k, v in doc["data"].items() if k not in UNITY_INTERNAL})]
            added.append({"name": script["name"], "kind": script["kind"], "refs": refs, "on_object": on_object})
        else:
            added.append({"name": doc["type"], "kind": "native", "refs": [], "on_object": on_object})

    return {
        "path": path.relative_to(root).as_posix(),
        "name": path.stem,
        "variant_of": variant_of,
        "objects": objects,
        "nested": nested,
        "added_components": added,
        "overrides": overrides,
        "removed_components": removed_components,
    }


def analyze(root: Path) -> dict:
    guid_map = build_guid_map(root)
    cache: dict = {}
    results = {"prefabs": [], "scenes": [], "errors": []}
    for suffix, key in ((".prefab", "prefabs"), (".unity", "scenes")):
        for path in sorted((root / "Assets").rglob(f"*{suffix}")):
            rel = path.relative_to(root).as_posix()
            if rel.startswith(EXCLUDED_DIRS):
                continue
            try:
                results[key].append(analyze_file(path, root, guid_map, cache))
            except Exception as error:
                results["errors"].append(f"{rel}: {type(error).__name__}: {error}")
    return results


def effective_components(prefab: dict, prefabs_by_name: dict, seen: set | None = None) -> list[dict]:
    """バリアント元から受け継いだ分も含め、prefab が持つコンポーネントを列挙する"""
    seen = seen or set()
    if prefab["name"] in seen:
        return []
    seen.add(prefab["name"])
    result: list[dict] = []
    base = prefabs_by_name.get(prefab["variant_of"]) if prefab["variant_of"] else None
    if base is not None:
        result += effective_components(base, prefabs_by_name, seen)
    for obj in prefab["objects"]:
        for comp in obj["components"]:
            result.append({**comp, "object": obj["name"], "parent": obj["parent"], "from": prefab["name"]})
    for comp in prefab["added_components"]:
        result.append({**comp, "object": "(追加)", "parent": None, "from": prefab["name"]})
    for removed in prefab["removed_components"]:
        result = [c for c in result if c["name"] != removed]
    #バリアントで差し替えた参照を反映する
    for override in prefab["overrides"]:
        for comp in result:
            if comp["name"] == override["component"]:
                comp["refs"] = [r for r in comp["refs"] if r["field"] != override["field"]] + [{"field": override["field"], "target": override["target"]}]
    return result
