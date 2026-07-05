using System.Linq;
using Battle.Interfaces;
using UnityEngine;
using Battle.Character;
using Exceptions;
using UnityEditor;

public class DamageProcessor : MonoBehaviour
{
    [SerializeField] private float invincibleDuration = 0.1f;

    //キャッシュ
    protected PhysicsMover _physicsMover_Cache;
    protected IHealthManager _healthManager_Cache;

    private float _lastDamagedTime = float.NegativeInfinity;

    //なにか処理させたいことがあれば子供が実装
    protected virtual void OnDamagedHitFinished(){}
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected virtual void Start()
    {
        var damagedCollider = GetComponentsInChildren<Transform>()
            .FirstOrDefault(obj => obj.gameObject.CompareTag("Damage Channel"))
            ?? throw new MissingChannelException("Damage Channel", gameObject.name);
        
        var damagedNotifier=damagedCollider.GetComponent<DamageHitNotifier>();
        Debug.Assert(
            damagedNotifier != null,
            "DamageNotifierがありません"
        );
        damagedNotifier.OnHit += DamagedHit;
        
        //強制吹き飛ばし用、HP減算
        if (!TryGetComponent(out _physicsMover_Cache))
            throw new MissingComponentException($"[{GetType().Name}] PhysicsMover が {gameObject.name} に見つかりません");

        var healthManager=_healthManager_Cache = GetComponent<IHealthManager>();
        Debug.Assert(
            healthManager != null,
            "HealthManagerがありません"
        );
    }

    private void DamagedHit(Collider2D other)
    {
        if (Time.time < _lastDamagedTime + invincibleDuration) return;

        //コリジョン処理
        var attackInfoGetter = other.GetComponent<IAttackInfoGetter>();
        if (attackInfoGetter != null)
        {
            if (attackInfoGetter.AttackerID == transform.root.gameObject.GetEntityId())
            {
                //自分の攻撃は自分に当たらない
                return;
            }
            
            var attackInfo = attackInfoGetter.GetAttackInfo();
            
            //キャラクターの位置関係で、どちら向きに吹き飛ばすか決める
            if (transform.root.position.x < other.transform.root.position.x)
            {
                attackInfo.attackVelocity.x = -attackInfo.attackVelocity.x;
            }
            //速度を与える
            _physicsMover_Cache.AddForceVelocity(attackInfo.attackVelocity,true,other.transform.root.gameObject);
            
            //ダメージ処理
            _healthManager_Cache.TakeDamage(attackInfo.damage);

            _lastDamagedTime = Time.time;
            OnDamagedHitFinished();
            
            Debug.Log("DamagedHit");
        }
        
        
    }
    
}
