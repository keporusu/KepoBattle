using System.Linq;
using Battle.Interfaces;
using UnityEngine;
using Battle;
using Battle.Character;
using Exceptions;
using Core.Constants;

public class DamageProcessor : MonoBehaviour
{
    [SerializeField] private float invincibleDuration = 0.1f;

    //キャッシュ
    protected PhysicsMover _physicsMover_Cache;
    protected IHealthManager _healthManager_Cache;
    private EntityRoot _entityRoot_Cache;

    private float _lastDamagedTime = float.NegativeInfinity;

    //なにか処理させたいことがあれば子供が実装
    protected virtual void OnDamagedHitFinished(){}
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    protected virtual void Start()
    {
        var damagedCollider = GetComponentsInChildren<Transform>()
            .FirstOrDefault(obj => obj.gameObject.CompareTag(GameTags.DamageChannel))
            ?? throw new MissingChannelException(GameTags.DamageChannel, gameObject.name);
        
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

        //自分が属するエンティティ(自傷判定・吹き飛ばし方向の基準)
        _entityRoot_Cache = EntityRoot.Require(this);
    }

    private void DamagedHit(Collider2D other)
    {
        if (Time.time < _lastDamagedTime + invincibleDuration) return;

        //コリジョン処理
        var attackInfoGetter = other.GetComponent<IAttackInfoGetter>();
        if (attackInfoGetter != null)
        {
            if (attackInfoGetter.AttackerID == _entityRoot_Cache.Id)
            {
                //自分の攻撃は自分に当たらない
                return;
            }

            var attackInfo = attackInfoGetter.GetAttackInfo();

            //当たってきたコリジョンが属するエンティティ
            var otherRoot = EntityRoot.Require(other);

            //キャラクターの位置関係で、どちら向きに吹き飛ばすか決める
            if (_entityRoot_Cache.transform.position.x < otherRoot.transform.position.x)
            {
                attackInfo.attackVelocity.x = -attackInfo.attackVelocity.x;
            }
            //速度を与える
            _physicsMover_Cache.AddForceVelocity(attackInfo.attackVelocity,true,otherRoot.gameObject);
            
            //ダメージ処理
            _healthManager_Cache.TakeDamage(attackInfo.damage);

            _lastDamagedTime = Time.time;
            OnDamagedHitFinished();
            
            Debug.Log("DamagedHit");
        }
        
        
    }
    
}
