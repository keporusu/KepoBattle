using Unity.VisualScripting;
using UnityEngine;

[RequireComponent(typeof(AnimatorTrigger))]
public class CharacterDamageProcessor : DamageProcessor
{
    private AnimatorTrigger animatorTrigger_Cache;
    protected override void Start()
    {
        base.Start();
        animatorTrigger_Cache = GetComponent<AnimatorTrigger>();
    }
    protected override void OnDamagedHitFinished()
    {
        base.OnDamagedHitFinished();
        
        //ダメージアニメーションに遷移
        animatorTrigger_Cache.TriggerDamage();
        if (healthManager_Cache.IsDead)
        {
            //死亡遷移
            animatorTrigger_Cache.TriggerDeath();
            Debug.Log("Death");
        }
    }
}
