using System.Linq;
using Core.Contracts;
using UnityEngine;
using Components.Identity;
using Components.Detection;
using Core.Exceptions;
using Core.Constants;
using Systems;
using UnityEngine.Serialization;

namespace Components.Combat.Damage
{
    public class DamageProcessor : MonoBehaviour
    {
        [SerializeField] private float invincibleDuration = 0.1f;
        [SerializeField] private string damageSoundFromCharacter;
        [SerializeField] private string damageSoundFromProp;

        //キャッシュ
        protected IKnockbackReceiver _physicsMover_Cache;
        protected IHealthManager _healthManager_Cache;
        private EntityRoot _entityRoot_Cache;

        private float _lastDamagedTime = float.NegativeInfinity;

        //なにか処理させたいことがあれば子供が実装
        protected virtual void OnDamagedHitFinished(Collider2D other){}

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        protected virtual void Start()
        {
            var damagedCollider = GetComponentsInChildren<Transform>()
                .FirstOrDefault(obj => obj.gameObject.CompareTag(GameTags.DamageChannel))
                ?? throw new MissingChannelException(GameTags.DamageChannel, gameObject.name);

            //ダメージ通知
            var damagedNotifier=damagedCollider.GetComponent<DamageHitNotifier>();
            if(damagedNotifier == null)
                throw new MissingComponentException($"[{GetType().Name}] DamagedNotifier が {gameObject.name} に見つかりません");
            damagedNotifier.OnHit += DamagedHit;

            //強制吹き飛ばし用
            if (!TryGetComponent(out _physicsMover_Cache))
                throw new MissingComponentException($"[{GetType().Name}] PhysicsMover が {gameObject.name} に見つかりません");

            //HP減算用
            if (!TryGetComponent(out _healthManager_Cache))
                throw new MissingComponentException($"[{GetType().Name}] HealthManager が {gameObject.name} に見つかりません");

            //自分が属するエンティティ(自傷判定・吹き飛ばし方向の基準)
            _entityRoot_Cache = EntityRoot.Require(this);
            
        }

        private void DamagedHit(Collider2D other)
        {
            var attackInfoGetter = other.GetComponent<IAttackInfoGetter>();

            //コリジョン処理
            if (attackInfoGetter != null)
            {
                
                if (attackInfoGetter.AttackerID == _entityRoot_Cache.Id)
                {
                    //自分の攻撃は自分に当たらない
                    return;
                }
                
                //相手の攻撃情報を動的に得る
                //TODO: 自身のrootは放射状の攻撃でしか使わないため渡したくないが、現状の設計だと妥協
                var attackInfo = attackInfoGetter.CalculateAttackInfo(EntityRoot.Require(this).Position);
                
                
                //ダメージの間隔が短かったらダメージを受けない
                if (Time.time < _lastDamagedTime + invincibleDuration && !attackInfo.ignoreDuration) return;
                

                //当たってきたコリジョンが属するエンティティ
                var otherRoot = EntityRoot.Require(other);

                //位置関係で、どちら向きに吹き飛ばすか決める
                // if (_entityRoot_Cache.Position.x < otherRoot.Position.x)
                // {
                //     attackInfo.attackPower.x = -attackInfo.attackPower.x;
                // }

                //速度を与える
                _physicsMover_Cache.ForceKnockback(attackInfo.attackPower,otherRoot.gameObject);

                //ダメージ処理
                _healthManager_Cache.TakeDamage(attackInfo.damage);
                _lastDamagedTime = Time.time;

                //ダメージ後処理
                OnDamagedHitFinished(other);

                Debug.Log("DamagedHit");
            }


        }

    }
}
