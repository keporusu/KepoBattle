# KepoBattle スクリプト設計レビュー

- 実施日: 2026-07-18
- 対象: `Assets/Scripts` 以下の全26ファイル(約1,800行)。サンプルアセットと自動生成の `InputSystem_Actions.cs` は対象外。
- 環境: Unity 6000.4.4f1

---

## 1. 総評

「Channel(タグ付き子オブジェクト)+ Notifier(イベント転送)+ Processor(処理本体)」という
イベント駆動のコンポーネント分割は、この規模のプロジェクトとしてよく整理されています。
当たり判定の「検出」と「処理」が分離されており、`IHealthManager` / `IAttackInfoGetter` による
Character / Prop の差し替えも効いています。

一方で、以下の3点が密結合の中心になっており、機能追加のたびに複数ファイルの同期修正が必要な構造になっています。

1. **攻撃フローが5クラスに分散**し、文字列(Animatorステート名・トリガー名)で同期している
2. **タグ・レイヤー・ステート名などの文字列契約**が約10ファイルに散在している
3. **シーン階層の形(transform.root / parent / 子タグ検索)への暗黙依存**が多く、プレハブ構造の変更に弱い

また、設計以前に **プレイヤービルドを壊しうる `using UnityEditor;` / `using NUnit...;`** と、
**未購読イベントの null 非条件 Invoke** という実害のあるバグ候補があります(§5)。

---

## 2. アーキテクチャ概要(依存図)

```mermaid
graph TD
    subgraph UI・ゲーム管理
        UIController --> GameUtility
        GameUtility --> PlayerController
    end

    subgraph Character
        PlayerController --> CharacterPhysicsMover
        PlayerController --> AttackExecutor
        PlayerController --> AnimatorTrigger
        PlayerController --> CameraController
        EnemyController --> PhysicsMover
        EnemyController --> AttackExecutor
        EnemyController -->|Animator直叩き| Animator
        AnimatorTrigger --> Animator
        AttackExecutor -->|ステート名文字列| StateProgressionNotifier
        StateProgressionNotifier -.イベント.-> AttackExecutor
        AttackExecutor --> CharacterAttackCollisionController
        CharacterPhysicsMover -->|継承+protectedフィールド| PhysicsMover
        CharacterDamageProcessor -->|継承| DamageProcessor
        CharacterDamageProcessor --> AnimatorTrigger
    end

    subgraph 共通(Battle)
        PhysicsMover -->|タグ検索| GeometryHitNotifier
        DamageProcessor -->|タグ検索| DamageHitNotifier
        DamageProcessor --> PhysicsMover
        DamageProcessor --> IHealthManager
        DamageProcessor -->|IAttackInfoGetter| 攻撃コリジョン側
    end

    subgraph Prop
        AttackController --> PropPhysicsMover
        AttackController --> PropAttackCollisionController
        AttackController --> AttackHitNotifier
        PropPhysicsMover -->|継承+protectedフィールド| PhysicsMover
        PropDamageProcessor -->|継承| DamageProcessor
    end
```

---

## 3. 良い点(維持すべき設計)

- **Notifier パターン**: `GeometryHitNotifier` / `DamageHitNotifier` / `AttackHitNotifier` が衝突検出をイベントに変換し、処理側(`PhysicsMover`, `DamageProcessor`)が購読する構造。検出と処理の分離ができている。
- **インターフェースによる抽象化**: `DamageProcessor` が `IHealthManager` / `IAttackInfoGetter` 経由で相手を扱うため、Character と Prop が同じダメージパイプラインに乗れている(`DamageProcessor.cs:51`)。
- **Fail-fast な初期化**: 必須コンポーネント欠落時に `MissingComponentException` / 自作 `MissingChannelException` を即座に投げる方針は、設定ミスの発見が早く良い習慣。
- **`AnimatorTrigger` によるパラメータハッシュの集約**(ただし Enemy 側は未使用、§6)。
- **カスタム PropertyDrawer**(`AttackCollisionSettingDrawer`)で Inspector の編集体験を上げている。
- **`StateProgressionNotifier`**: StateMachineBehaviour をイベント発行専用の汎用部品にした点は再利用性が高い。

---

## 4. 密結合の分析(重点)

### 4.1 攻撃フロー: 1つのライフサイクルが5クラスに分散【最重要】

攻撃1回の流れが次のように分散しています。

```
PlayerController.OnAttack
  ├─ AttackExecutor.StartAttack1()      … 実態は CancelAttack のみ
  └─ AnimatorTrigger.TriggerAttack1()   … Animator に "Attack1" トリガー
        ↓ Animator がステート遷移
StateProgressionNotifier("Attack1")     … ステート名文字列で AttackExecutor が検索
        ↓ OnStateBegin / OnStateProgress / OnStateEnd
AttackExecutor                          … 進行率からコリジョンON/OFF
  └─ CharacterAttackCollisionController … 形状設定と攻撃情報の保持
```

問題点:

- `PlayerController.OnAttack`(`PlayerController.cs:183`)が `StartAttack1()` と `TriggerAttack1()` を**2連呼び出し**しており、この2つは常にペアで呼ばなければ壊れる暗黙の契約になっている。
- 「どの攻撃が進行中か」を `AttackExecutor` が **Animator のステート遷移から逆算**している(`SetTypeAttack1` 等、`AttackExecutor.cs:105-118`)。入力→ロジック→アニメではなく、アニメ→ロジックへの逆流があり、制御の流れが追いにくい。
- **攻撃を1種類追加するのに必要な変更箇所が6箇所**: ①Animatorにステート追加 ②`StateProgressionNotifier` を貼って StateName 設定 ③`AnimatorTrigger` にトリガーメソッド追加 ④`AttackExecutor` にコリジョン設定リスト追加+`SetCollisionAttack` の分岐追加 ⑤`AttackType` enum 追加 ⑥呼び出し側(Controller)の修正。

**改善案:**

1. **エントリポイントの一本化**: `AttackExecutor.StartAttack(AttackType type)` が内部で Animator トリガーも発火するようにし、Controller からは1回の呼び出しにする。「攻撃の開始・進行・終了」の責任者を `AttackExecutor` 1つに定める。
2. **データ駆動化**: 攻撃1種を ScriptableObject(例: `AttackDefinition` = トリガー名 + ステート名 + `List<AttackCollisionSettingForAction>`)にまとめ、`AttackExecutor` は `List<AttackDefinition>` を回すだけにする。これで `SetCollisionAttack` 内の if 分岐(`AttackExecutor.cs:124-132`)と設定リスト3本が消え、攻撃追加はアセット追加+Animator編集だけになる。

### 4.2 文字列による暗黙契約の散在

| 文字列 | 使用箇所 |
|---|---|
| タグ `"Geometry Channel"` | `PhysicsMover.cs:59,171,203`, `GeometryHitNotifier.cs:18` |
| タグ `"Attack Channel"` | `AttackExecutor.cs:66`, `AttackController.cs:23`, `DamageHitNotifier.cs:20` |
| タグ `"Damage Channel"` | `DamageProcessor.cs:25`, `AttackHitNotifier.cs:14` |
| レイヤー `"Character"` `"Prop"` `"Ground"` | `PhysicsMover.cs:72-74` |
| ステート名 `"Attack1"` `"Attack2"` `"SpecialAttack"` | `AttackExecutor.cs:53-55`(+Animator側の `StateProgressionNotifier` の Inspector 設定) |
| Animator パラメータ名 | `AnimatorTrigger.cs:8-16`, `EnemyController.cs:11`(重複定義) |

タイプミスや Animator 側のリネームがコンパイルエラーにならず、実行時まで発覚しません。
特にステート名は「Attack3 = SpecialAttack」という**名前のねじれ**が既にあり
(`_spNotifierAttack3_Cache` が `"SpecialAttack"` を探す)、混乱の芽になっています。

**改善案:** `public static class GameTags { public const string GeometryChannel = "Geometry Channel"; … }` のような定数クラスに集約する。レイヤーも同様(`GameLayers`)。ステート名は §4.1 の ScriptableObject 化で設定データに寄せるのが理想。

### 4.3 シーン階層の形への暗黙依存

- `PhysicsMover.OnHitGeometry`(`PhysicsMover.cs:176`)は `other.transform.parent.gameObject.GetComponent<Rigidbody2D>()` としており、「Channel はルート直下の子」という**階層1段固定**の前提。Channel を1階層深くした瞬間に壊れる。
- `transform.root` 依存が4箇所(`DamageProcessor.cs:54,63`, `CharacterAttackCollisionController.cs:26`, `AttackController.cs:56`)。キャラクターを整理用の空オブジェクトの下にまとめただけで root が変わり、攻撃者判定(自傷防止)が全て誤動作する。

**改善案:** `GetComponentInParent<Rigidbody2D>()` / `GetComponentInParent<T>()` に置き換えるだけで階層段数の前提が消える。より堅くするなら、エンティティのルートに `EntityRoot`(ID保持)コンポーネントを1つ置き、`GetComponentInParent<EntityRoot>()` で所有者を解決する規約に統一する。タグ検索(`GetComponentsInChildren<Transform>` + `CompareTag`)も、`[SerializeField]` での直接参照か `GetComponentInChildren<GeometryHitNotifier>()` のような型検索に置き換えれば、タグ設定漏れという実行時エラーの原因が減る。

### 4.4 継承による結合: PhysicsMover の protected フィールド共有

`CharacterPhysicsMover` / `PropPhysicsMover` が基底の可変フィールド
`MovingVelocity` / `ForceVelocity` / `IsBraking` を直接書き換えています
(例: `CharacterPhysicsMover.StopJump` が `ForceVelocity.y = 0.0f;`)。

- 基底の内部表現(「ForceVelocity のyが上向きなら空中」等)を派生が知っている必要があり、基底の実装変更が派生を静かに壊す(fragile base class)。
- さらに `PhysicsMover` 自体が、移動積分・重力・摩擦・ブレーキ・キャラ押し合い・接地レイキャスト・接地スナップと**責務過多**(251行)。押し合い定数 `1.5f`(`PhysicsMover.cs:135,139`)、レイ距離 `0.05f/0.08f` などのマジックナンバーも埋まっている。

**改善案:**

1. 派生クラスからのフィールド直接操作をやめ、`Jump(power)` / `CutJump()` / `SetMoveInput(v)` / `Brake()` のような**意図ベースの public/protected メソッド**だけを公開する。
2. 接地判定(レイキャスト+スナップ+ OnGround/OnForceAir 通知)を `GroundSensor` に、押し合い処理を `PushResolver` に分離する。`PhysicsMover` は速度の積分に専念させる。

### 4.5 衝突の Enter / Exit の非対称

Enter は子の `GeometryHitNotifier` 経由のイベントで受けるのに、Exit は `PhysicsMover.OnTriggerExit2D`
(`PhysicsMover.cs:201`)で**直接**受けています(子コライダーのイベントが Rigidbody 側にも届く仕様に依存)。
Notifier を介す設計意図が Exit で崩れており、読み手は2つの経路を追う必要があります。
→ `GeometryHitNotifier` に `OnExit` を追加して両方をイベント経由に統一するべきです。

また `_hasOtherCharacter` / `_otherRigidbody_Cache` は**1体分しか保持できず**、
2体と同時に接触して片方が離れると、まだ接触中のもう1体の押し合いが消えます。
`HashSet<Rigidbody2D>` 等で接触中の相手を集合管理してください。

### 4.6 その他の結合

- `DamageProcessor` → `PhysicsMover` が**具象クラス依存**(`DamageProcessor.cs:13`)。ノックバックを受けない物(壁掛けオブジェクト等)を作れない。`IKnockbackReceiver` を切るか、少なくとも「無ければスキップ」にすると柔軟になる。
- `CameraController` がプレイヤーのコンポーネントで、`PlayerController.Update` から毎フレーム呼ばれている(`PlayerController.cs:102`)。カメラ側に独立した追従スクリプト(`LateUpdate` でターゲット追従)を置けば、Player からカメラ知識が消える。なおフィールド名 `camera` は継承メンバーを隠すため `targetCamera` 等へ。
- `UIController → GameUtility → PlayerController` の直列参照は、現規模なら許容範囲。ただし `GameUtility` がデバッグスポーン(`Keyboard.current` 直叩き = InputSystem_Actions と別経路の入力)とリスポーン管理を兼ねているので、肥大化する前に「デバッグ用」と「ゲーム進行管理」を分けておくと良い。

---

## 5. バグ・リスク(設計以前に直すべき点)

優先度順です。

1. **ビルド破壊リスク: ランタイムコードのエディタ専用 using**
   - `DamageProcessor.cs:6` の `using UnityEditor;`
   - `PropAttackCollisionController.cs:3` の `using NUnit.Framework.Constraints;`(+ `using Object = System.Object;` も未使用)
   - プレイヤービルドではこれらの名前空間が存在せず、通常はコンパイルエラーになります。未使用なので削除するだけでOK。`PhysicsMover.cs:3` / `CharacterDamageProcessor.cs:1` の `using Unity.VisualScripting;` も未使用のため削除推奨。
2. **`OnAttackFinish.Invoke()` の null 非条件呼び出し**(`AttackExecutor.cs:162`)
   - `EnemyController` は `AttackExecutor` を取得するだけで `OnAttackFinish` を購読していない。敵が攻撃ステートを再生した瞬間に NullReferenceException。`?.Invoke()` にし、フィールドも `public Action` ではなく `public event Action` に。
3. **Notifier の `public System.Action OnHit` フィールド**(3つの Notifier 共通)
   - `event` でないため外部から `= null` 代入や勝手な Invoke が可能。かつ `OnHit.Invoke(other)` は購読者ゼロで NRE。`public event Action<Collider2D> OnHit;` + `?.Invoke` へ。
4. **`_isExecuting` の固定長5**(`AttackExecutor.cs:39,96`)
   - コリジョン設定リストが6要素以上になると IndexOutOfRange。`collisionSettings.Count` に合わせて確保するか、`HashSet<int>` にする。
5. **攻撃ステート終了時にコリジョンを強制 Deactivate していない**
   - `OnStateEnd`(`AttackFinishCallback`)は通知のみ。`spanEnd` が 1.0 付近だと `normalizedTime > spanEnd` を踏まずにステートを抜け、**ヒットボックスが出っぱなし**になり得る(次の `StartAttack1` の `CancelAttack` まで残留)。`AttackFinishCallback` 内で `CancelAttack()` 相当の掃除を行うべき。
   - 併せて `CharacterAttackCollisionController.Deactivate` で `_uniqueID = -1` にリセットしておくと、`DeactivateCollider` の `FirstOrDefault(x => x.UniqueID == id)` が古いIDの非アクティブコライダーを拾う事故を防げる。
6. **`PropHealthManager` が未実装**(`PropHealthManager.cs`)
   - `TakeDamage` が空、`CurrentHealth` は常に0、`maxHealth` 未使用。現状 `IsDead` は `!unbreakable` と等価。意図的な仮実装なら `// TODO` を明記、そうでなければ `CharacterHealthManager` と同じ実装を(§6 の共通化も参照)。
7. **`AttackExecutor.Start` の Notifier null チェックが LogError のみ**(`AttackExecutor.cs:56-59`)
   - ログを出した直後に `_spNotifierAttack1_Cache.OnStateBegin += …` で NRE。他クラス同様に throw で止めるべき。
8. **`GameUtility.cs:4` の `using Vector2 = System.Numerics.Vector2;`**
   - UnityEngine.Vector2 と紛らわしく、将来 Vector2 を使った瞬間に型不一致で混乱する。削除推奨。
9. **`PlayerController.Update` の `EventSystem.current`**(`PlayerController.cs:105`)
   - シーンに EventSystem がないと NRE。null チェックを。またこの判定は毎フレームでなく攻撃入力時だけで十分。
10. **その他小物**: `UseAvailableCollider` の `[CanBeNull]` は void メソッドに付いていて無意味(`AttackExecutor.cs:168`)/ `_attackCts` は未使用のデッドコード / `AnimatorTrigger.TriggerAir` が Jump トリガーを流用しており名前と実態が不一致 / `TeamSetting` は空クラス(チーム・フレンドリーファイア判定が未着手であることのマーカーなら TODO を明記)。

---

## 6. 重複コード

- **`AttackCollisionSetting` と `AttackCollisionSettingForAction`**(`CombatData.cs`)は span 2フィールド以外**全フィールドが重複**。さらに `AttackCollisionSettingDrawer.cs` は同じ OnGUI/GetPropertyHeight をほぼ丸ごと2回書いている(157行中 ~120行が重複)。
  → `AttackCollisionSettingForAction` を「`AttackCollisionSetting` + spanStart/spanEnd」の**合成(入れ子struct)**にすれば、Drawer も基本部分を1つにでき、フィールド追加時の修正が1箇所になる。
- **コライダー形状の適用ロジック**が `CharacterAttackCollisionController.Activate`(`:55-78`)と `PropAttackCollisionController.Initialize`(`:26-55`)で重複。
  → `static class ColliderShapeUtility { public static Collider2D Apply(GameObject go, AttackCollisionSetting s) }` のような共通ヘルパーへ。
- **EnemyController が Animator を直接叩いている**(`EnemyController.cs:11,39`)。`AnimatorTrigger` があるのに使っておらず、`Ground` ハッシュが二重定義。Enemy にも `AnimatorTrigger` を載せて統一する。
- **namespace の不統一**: `PhysicsMover` / `DamageProcessor` / `GameUtility` / `UIController` / Notifier 3種 / `CombatData` / `AttackType` enum はグローバル名前空間、他は `Battle.*`。役割上 `Battle` 直下に置けるものばかりなので統一を推奨(`AttackType` は `AttackExecutor.cs` 内の namespace 外に enum が置かれている点も整理対象)。

---

## 7. 改善ロードマップ(推奨順)

### P0 — すぐ直す(バグ・ビルド事故防止、いずれも小修正)
1. エディタ/テスト用 using の削除(§5-1)
2. イベントを `event` + `?.Invoke` に統一(§5-2, 5-3)
3. `_isExecuting` の固定長解消、ステート終了時の強制 Deactivate、`_uniqueID` リセット(§5-4, 5-5)
4. `AttackExecutor.Start` の null チェック強化、`PropHealthManager` の実装(§5-6, 5-7)

### P1 — 結合を下げる(中規模、順に独立して実施可能)
5. タグ・レイヤー・Animatorパラメータの定数クラス集約(§4.2)
6. `transform.root` / `transform.parent` を `GetComponentInParent` / `EntityRoot` 方式へ(§4.3)
7. 攻撃開始の一本化: `AttackExecutor.StartAttack(AttackType)` が Animator トリガーまで面倒を見る(§4.1-1)
8. Enter/Exit を Notifier 経由に統一+接触相手の集合管理(§4.5)

### P2 — 構造改善(機能追加が増える前に)
9. 攻撃定義の ScriptableObject 化(§4.1-2)— 攻撃追加コストが激減する本命
10. `PhysicsMover` の分割(GroundSensor / PushResolver)と意図ベースAPI化(§4.4)
11. コリジョン設定 struct の合成化+Drawer 共通化+形状適用ヘルパー(§6)
12. カメラ追従の独立、namespace 統一、デッドコード掃除

---

*このレポートは Claude Code によるスクリプト静的レビューです。シーン・プレハブ・Animator Controller の設定内容は確認範囲外のため、Inspector 側の設定と食い違う指摘があれば実態を優先してください。*
