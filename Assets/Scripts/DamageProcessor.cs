using Interfaces;
using UnityEngine;

[RequireComponent(typeof(PhysicsMover))]
public class DamageProcessor : MonoBehaviour
{
    [SerializeField] private GameObject damagedCollider;
    private PhysicsMover physicsMover_Cache;
    protected IHealthManager healthManager_Cache;

    //なにか処理させたいことがあれば子供が実装
    protected virtual void OnDamagedHitFinished(){}
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected virtual void Start()
    {
        var damagedNotifier=damagedCollider.GetComponent<DamagedNotifier>();
        Debug.Assert(
            damagedNotifier != null,
            "DamageNotifierがありません"
        );
        damagedNotifier.OnHit += DamagedHit;
        
        //強制吹き飛ばし用、HP減算
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
        
        OnDamagedHitFinished();
        
        Debug.Log("DamagedHit");
    }
    
}
