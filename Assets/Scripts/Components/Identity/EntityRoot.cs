using UnityEngine;

namespace Components.Identity
{
    /// <summary>
    /// エンティティ(キャラクター・Prop など、1つの独立した存在)のルートに1つだけ貼るのを強制する。
    /// 子オブジェクトからは <see cref="Find(Component)"/> で所有者を解決する。
    ///
    /// transform.root は「ヒエラルキーの最上位」を返すため、整理用の空オブジェクトの下に
    /// まとめ直しただけで所有者が変わってしまう。EntityRoot を起点にすれば階層の段数に依存しない。
    /// </summary>
    [DisallowMultipleComponent]
    public class EntityRoot : MonoBehaviour
    {
        /// <summary>
        /// エンティティの識別子。自分の攻撃が自分に当たらないようにする等、同一性の比較に使う。
        /// </summary>
        public EntityId Id => gameObject.GetEntityId();

        /// <summary>
        /// 指定オブジェクトが所属するエンティティのルートを取得する。
        /// EntityRoot が入れ子の場合は、最も近い祖先が所有者になる。
        /// </summary>
        /// <returns>見つからなければ null</returns>
        public static EntityRoot Find(Component target)
        {
            //非アクティブな攻撃コリジョンからも解決できるよう、includeInactive を立てる
            return target == null ? null : target.GetComponentInParent<EntityRoot>(true);
        }

        /// <inheritdoc cref="Find(Component)"/>
        public static EntityRoot Find(GameObject target)
        {
            return target == null ? null : target.GetComponentInParent<EntityRoot>(true);
        }

        /// <summary>
        /// <see cref="Find(Component)"/> と同じだが、見つからない場合は例外を投げる。
        /// 所有者が解決できないまま処理を続けると誤判定になるため、設定漏れは即座に止める。
        /// </summary>
        public static EntityRoot Require(Component target)
        {
            return Find(target) ?? throw MissingFor(target == null ? null : target.gameObject);
        }

        /// <inheritdoc cref="Require(Component)"/>
        public static EntityRoot Require(GameObject target)
        {
            return Find(target) ?? throw MissingFor(target);
        }

        private static MissingComponentException MissingFor(GameObject target)
        {
            var name = target == null ? "(null)" : target.name;
            return new MissingComponentException(
                $"[{nameof(EntityRoot)}] {name} とその親に {nameof(EntityRoot)} が見つかりません");
        }
    }
}
