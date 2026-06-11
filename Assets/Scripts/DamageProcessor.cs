using Interfaces;
using UnityEngine;

[RequireComponent(typeof(AnimatorTrigger))]
[RequireComponent(typeof(PhysicsMover))]
public class DamageProcessor : MonoBehaviour
{
    [SerializeField] private GameObject damagedCollider;
    private AnimatorTrigger animatorTrigger_Cache;
    private PhysicsMover physicsMover_Cache;
    private IHealthManager healthManager_Cache;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        var damagedNotifier=damagedCollider.GetComponent<DamagedNotifier>();
        Debug.Assert(
            damagedNotifier != null,
            "DamageNotifierがありません"
        );
        damagedNotifier.OnHit += DamagedHit;
        
        //アニメーショントリガ、強制吹き飛ばし用、HP減算
        animatorTrigger_Cache = GetComponent<AnimatorTrigger>();
        physicsMover_Cache = GetComponent<PhysicsMover>();
        
        var healthManager=healthManager_Cache = GetComponent<IHealthManager>();
        Debug.Assert(
            healthManager != null,
            "HealthManagerがありません"
        );
    }

    void DamagedHit(Collider2D other)
    {
        //コリジョン処理
        var damageCollisionManager = other.GetComponent<DamageCollisionManager>();
        if (damageCollisionManager != null)
        {
            var attackInfo = damageCollisionManager.GetAttackInfo();
            
            //キャラクターの位置関係で、どちら向きに吹き飛ばすか決める
            if (transform.root.position.x < other.transform.root.position.x)
            {
                attackInfo.attackPower.x = -attackInfo.attackPower.x;
            }
            physicsMover_Cache.ForcePower(attackInfo.attackPower,true);
            
            //ダメージ処理
            healthManager_Cache.TakeDamage(attackInfo.damage);
        }
        
        //TODO:DamageProcessorはキャラクター専用にしないため、ここの処理は別に逃がすor継承先で行う
        //ダメージアニメーションに遷移
        animatorTrigger_Cache.TriggerDamage();
        if (healthManager_Cache.IsDead)
        {
            //死亡遷移
            animatorTrigger_Cache.TriggerDeath();
            Debug.Log("Death");
        }
        Debug.Log("DamagedHit");
    }
    
}
