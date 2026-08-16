using UnityEngine;
using Components.Animation;

namespace Components.Combat.Damage
{

    public class CharacterDamageProcessor : DamageProcessor
    {
        private AnimatorTrigger _animatorTrigger_Cache;

        protected override void Start()
        {
            base.Start();
            if (!TryGetComponent(out _animatorTrigger_Cache))
                throw new MissingComponentException($"[{GetType().Name}] AnimatorTrigger が {gameObject.name} に見つかりません");
        }

        protected override void OnDamagedHitFinished(Collider2D other)
        {
            base.OnDamagedHitFinished(other);

            //ダメージアニメーションに遷移
            _animatorTrigger_Cache.TriggerDamage();
            if (_healthManager_Cache.IsDead)
            {
                //死亡遷移
                _animatorTrigger_Cache.TriggerDeath();
                Debug.Log("Death");
            }
        }
    }
}
