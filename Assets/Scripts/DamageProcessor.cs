using Interfaces;
using UnityEngine;
using Character;

[RequireComponent(typeof(PhysicsMover))]
public class DamageProcessor : MonoBehaviour
{
    [SerializeField] private GameObject damagedCollider;
    [SerializeField] private float invincibleDuration = 0.1f;

    //キャッシュ
    private PhysicsMover _physicsMover_Cache;
    protected IHealthManager _healthManager_Cache;

    private float _lastDamagedTime = float.NegativeInfinity;

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
        _physicsMover_Cache = GetComponent<PhysicsMover>();
        
        var healthManager=_healthManager_Cache = GetComponent<IHealthManager>();
        Debug.Assert(
            healthManager != null,
            "HealthManagerがありません"
        );
    }

    void DamagedHit(Collider2D other)
    {
        if (Time.time < _lastDamagedTime + invincibleDuration) return;

        //コリジョン処理
        var damageCollisionManager = other.GetComponent<DamageCollisionManager>();
        if (damageCollisionManager != null)
        {
            var attackInfo = damageCollisionManager.GetAttackInfo();
            
            //キャラクターの位置関係で、どちら向きに吹き飛ばすか決める
            if (transform.root.position.x < other.transform.root.position.x)
            {
                attackInfo.attackVelocity.x = -attackInfo.attackVelocity.x;
            }
            //速度を与える
            _physicsMover_Cache.AddForceVelocity(attackInfo.attackVelocity,true);
            
            //ダメージ処理
            _healthManager_Cache.TakeDamage(attackInfo.damage);

            _lastDamagedTime = Time.time;
            OnDamagedHitFinished();
            
            Debug.Log("DamagedHit");
        }
        
        
    }
    
}
