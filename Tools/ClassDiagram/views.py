"""どの図に何を載せるかの定義。図を増やすときはここに追記する"""

#全体図に載せる主要クラス
OVERVIEW_CLASSES = [
    "StageManager", "LevelManager", "LevelEditor", "GameUtility", "SoundManager", "StageUIView",
    "CameraController", "PlayerController", "EnemyController", "PropBehaviourController", "ILevelObject",
    "EntityRoot", "LevelObjectIdentity", "PhysicsMover", "MovementSolver", "AttackExecutor", "DamageProcessor",
]

#層の順序。数字の小さい層から大きい層へ向かう依存を正とする
#Components は Systems の状態を読む設計なので、Components → Systems が正の向きになる
#UI の位置づけは未確定なので、Systems と同じ順位にして両者の間を問題にしない
LAYER_RANK = {"Components": 0, "UI": 1, "Systems": 1, "Core": 2, "Data": 3}

#機能別の図。kind が classes ならクラス図、prefab なら prefab の構成図
#  focus: 公開メンバーをすべて載せるクラス（他のクラスは図内で使われているメンバーのみ）
#  focus_edges_only: focus のクラスに関わる依存だけを描く
#  implementers_of: 実装クラスの一覧を本文に添えるインターフェース
FEATURE_VIEWS = [
    {
        "file": "02-player.md",
        "title": "プレイヤー周り",
        "summary": "`PlayerController` の依存と、`KPPlayer.prefab` の構成",
        "sections": [
            {
                "kind": "classes",
                "heading": "PlayerController の依存",
                "text": "周辺クラス同士の依存は省いています（継承・実装を除く）。",
                "classes": ["PlayerController", "ILevelObject", "PlayerUIController", "AnimatorTrigger", "AttackExecutor",
                            "CharacterPhysicsMover", "PhysicsMover", "EntityRoot", "LevelObjectParameters",
                            "StageManager", "LevelManager", "LevelEditor", "GameUtility", "SoundManager"],
                "focus": ["PlayerController"],
                "focus_edges_only": True,
                "member_limit": 8,
            },
            {
                "kind": "prefab",
                "heading": "KPPlayer.prefab のコンポーネント構成",
                "text": "`KPObject` → `KPCharacter` → `KPPlayer` の順にバリアントで積み重なっています。",
                "prefab": "KPPlayer",
            },
        ],
    },
    {
        "file": "04-attack.md",
        "title": "攻撃",
        "summary": "攻撃の開始から、当たり判定の生成と攻撃情報の計算まで",
        "sections": [
            {
                "kind": "classes",
                "heading": "攻撃の流れ",
                "text": "`AttackExecutor` がアニメーションの進行に合わせて当たり判定を出し、`AttackCollisionController` が相手に渡す攻撃情報を計算します。`DamageProcessor` は受ける側の入口です。",
                "classes": ["PlayerController", "EnemyController", "AttackExecutor", "AttackData", "AttackCollisionSettingForAction",
                            "AttackCollisionSetting", "StateProgressionNotifier", "AnimatorTrigger", "AttackCollisionsManager",
                            "AttackCollisionController", "AttackHitNotifier", "IAttackInfoGetter", "AttackInfo",
                            "VelocityAttackGenerator", "DamageProcessor"],
                "member_limit": 6,
            },
        ],
    },
    {
        "file": "05-movement.md",
        "title": "移動",
        "summary": "`PhysicsMover` の派生と、計算を担う `MovementSolver`",
        "sections": [
            {
                "kind": "classes",
                "heading": "移動の構成",
                "text": "`PhysicsMover` が Unity との接点で、速度や衝突の計算は `MonoBehaviour` ではない `MovementSolver` が担当します。外から速度を与える `SpringController`、`BellController`、`LauncherController` と、落下判定の高さを読む `LevelManager` は省いています。",
                "classes": ["PlayerController", "EnemyController", "PropBehaviourController", "VelocityAttackGenerator", "DamageProcessor",
                            "IKnockbackReceiver", "PropPhysicsMover", "CharacterPhysicsMover", "PhysicsMover", "GeometryHitNotifier",
                            "MovementSolver", "SurfaceHit", "MoveStep", "MovementSettings"],
                "member_limit": 7,
            },
        ],
    },
    {
        "file": "06-damage.md",
        "title": "ダメージ",
        "summary": "攻撃を受けてから、HP の減少とノックバックまで",
        "sections": [
            {
                "kind": "classes",
                "heading": "ダメージ処理の構成",
                "text": "`DamageHitNotifier` の通知を `DamageProcessor` が受け、`IAttackInfoGetter`、`IHealthManager`、`IKnockbackReceiver` の3つのインターフェースを通して処理します。`CharacterDamageProcessor` がダメージモーションの再生に使う `AnimatorTrigger` と、所有者の解決に使う `EntityRoot` は省いています。",
                "classes": ["CharacterDamageProcessor", "PropDamageProcessor", "DamageProcessor", "DamageHitNotifier",
                            "IAttackInfoGetter", "AttackCollisionController", "AttackInfo", "AdditionalInfoForAttackCalculation",
                            "IHealthManager", "CharacterHealthManager", "PropHealthManager", "HealthManager", "IKnockbackReceiver"],
                "member_limit": 6,
                "implementers_of": "IKnockbackReceiver",
            },
        ],
    },
    {
        "file": "07-stage.md",
        "title": "ステージ進行",
        "summary": "`StageManager` を中心とした進行と、タイトル・ステージセレクト",
        "sections": [
            {
                "kind": "classes",
                "heading": "ステージ中の進行",
                "text": "`StageManager` に関わる依存だけを描いています。",
                "classes": ["StageManager", "GameUtility", "StageUIView", "StagesInfo", "StageInfo", "SwitchEvent", "UIController",
                            "PlayerController", "CameraController", "GoalController", "BellController", "LauncherController",
                            "SwitchController", "SwitchBlockController", "SoundManager", "LevelManager"],
                "focus": ["StageManager"],
                "focus_edges_only": True,
                "member_limit": 12,
            },
            {
                "kind": "classes",
                "heading": "タイトルとステージセレクト",
                "text": "ステージ中とは別のシーンで動くクラスです。",
                "classes": ["TitleController", "SoundManager", "StageSelectController", "StageSelectUIView", "StageSelectButtonView",
                            "StagesInfo", "StageInfo"],
                "member_limit": 8,
            },
        ],
    },
    {
        "file": "08-level-editor.md",
        "title": "レベルエディタ",
        "summary": "レベルデータからの構築と、エディタでの編集",
        "sections": [
            {
                "kind": "classes",
                "heading": "レベルの構築",
                "text": "`LevelManager` がレベルデータと `LevelObjectRegistry` からオブジェクトを生成し、`ILevelObject` を通してパラメータを渡します。",
                "classes": ["LevelManager", "LevelObjectRegistry", "LevelObjectRegistry.Entry", "LevelData", "LevelSettings", "LevelObjectData",
                            "LevelObjectIdentity", "ILevelObject", "LevelObjectParameters", "LevelParameter"],
                "member_limit": 7,
                "implementers_of": "ILevelObject",
            },
            {
                "kind": "classes",
                "heading": "編集とプレビュー",
                "text": "`LevelEditor` はプレイ中の編集画面、`LevelScenePreview` は Unity エディタのシーン上でのプレビューです。この2つに関わる依存だけを描いています。",
                "classes": ["LevelEditor", "LevelScenePreview", "LevelManager", "LevelGrid", "LevelObjectIdentity", "LevelObjectRegistry",
                            "LevelObjectRegistry.Entry", "LevelData", "LevelObjectData", "LevelObjectParameters", "LevelParameter", "ILevelObject"],
                "focus": ["LevelEditor", "LevelScenePreview"],
                "focus_edges_only": True,
                "member_limit": 8,
            },
        ],
    },
]
