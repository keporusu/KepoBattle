using Unity.VisualScripting;
using UnityEngine;
using Character;

namespace Character
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

        protected override void OnDamagedHitFinished()
        {
            base.OnDamagedHitFinished();

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
