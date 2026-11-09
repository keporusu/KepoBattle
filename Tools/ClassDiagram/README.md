# ClassDiagram

`Assets/Scripts` の C# と、prefab・シーンの YAML を静的解析して、`DevDocs/Architecture` にクラス図（Mermaid）と集計を生成します。`DevDocs/` は `.gitignore` の対象なので、生成物はコミットされません。

## 使い方

初回だけ、解析に使うライブラリをプロジェクトの外に入れます。

```sh
python3 -m venv ~/.venvs/kepobattle-diagram
~/.venvs/kepobattle-diagram/bin/pip install -r Tools/ClassDiagram/requirements.txt
```

コードや prefab を変更したら、再生成します。Unity やコンパイラは必要ありません。

```sh
~/.venvs/kepobattle-diagram/bin/python Tools/ClassDiagram/generate.py
```

解析結果の全体を見たいときは、`--json 出力先.json` を付けます。

## ファイル

| ファイル | 役割 |
|---|---|
| `generate.py` | 実行の入口。図と集計を書き出す |
| `views.py` | 図に載せるクラスと層の順序の定義。図を増やすときはここを変更する |
| `extract_code.py` | C# の型と依存の抽出 |
| `extract_assets.py` | prefab とシーンの構成の抽出 |

## 精度の限界

コンパイラを使わず構文から型を推定しています。型は名前で解決するため、推定できない式を経由した呼び出しは図に出ません。
