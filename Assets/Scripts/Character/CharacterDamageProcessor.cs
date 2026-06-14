using Unity.VisualScripting;
using UnityEngine;
using Character;

namespace Character
{

    [RequireComponent(typeof(AnimatorTrigger))]
    public class CharacterDamageProcessor : DamageProcessor
    {
        private AnimatorTrigger _animatorTrigger_Cache;

        protected override void Start()
        {
            base.Start();
            _animatorTrigger_Cache = GetComponent<AnimatorTrigger>();
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
