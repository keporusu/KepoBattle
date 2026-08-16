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
                
                //相手の攻撃情報を得る
                //TODO: 自身のrootは放射状の攻撃でしか使わないため渡したくないが、現状の設計だと妥協
                var attackInfo = attackInfoGetter.GetAttackInfo(EntityRoot.Require(this).transform.position);

                //当たってきたコリジョンが属するエンティティ
                var otherRoot = EntityRoot.Require(other);

                //キャラクターの位置関係で、どちら向きに吹き飛ばすか決める
                if (_entityRoot_Cache.transform.position.x < otherRoot.transform.position.x)
                {
                    attackInfo.attackVelocity.x = -attackInfo.attackVelocity.x;
                }

                //速度を与える
                //TODO: 現状Prop->Propでも決まったattackVelocityが入る。しかし、ここはPropの場合、速度によって動的に変わるべきである
                _physicsMover_Cache.ForceKnockback(attackInfo.attackVelocity,otherRoot.gameObject);

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
