using System;
using UnityEngine;
using System.Linq;
using Exceptions;
using Core.Constants;

namespace Battle.Prop
{
    public class AttackController : MonoBehaviour
    {
        [SerializeField] private float relaxSpeed;
        [SerializeField] private AttackCollisionSetting collisionSetting;
        
        private Collider2D _collider2D_Cache;

        private void Start()
        {
            if (!TryGetComponent(out PropPhysicsMover physicsMover))
            {
                throw new MissingComponentException($"[{GetType().Name}] PropPhysicsMover が {gameObject.name} に見つかりません");
            }

            var attackCollider = GetComponentsInChildren<Transform>()
                .FirstOrDefault(obj => obj.gameObject.CompareTag(GameTags.AttackChannel))
                ?? throw new MissingChannelException(GameTags.AttackChannel, gameObject.name);


            if (!attackCollider.TryGetComponent(out PropAttackCollisionController attackCollisionController))
            {
                throw new MissingComponentException($"[{GetType().Name}] PropAttackCollisionController が {attackCollider.gameObject.name} に見つかりません");
            }

            if (!attackCollider.TryGetComponent(out AttackHitNotifier attackHitNotifier))
            {
                throw new MissingComponentException($"[{GetType().Name}] attackHitNotifier が {attackCollider.gameObject.name} に見つかりません");
            }
            
            //十分に速度が低下した閾値を設定
            physicsMover.SetRelaxSpeed(relaxSpeed);
            //コリジョン初期化
            attackCollisionController.Initialize(collisionSetting);
            
            //攻撃によってイベントを発火
            physicsMover.OnForce += (GameObject instigator) =>
            {
                attackCollisionController.Activate(instigator);
            };
            physicsMover.OnRelax += () =>
            {
                attackCollisionController.Deactivate();
            };
            
            //攻撃があたったら、自身は跳ね返る
            attackHitNotifier.OnHit += (Collider2D other) =>
            {
                //自分が攻撃者の攻撃は自分に当たらない
                if (attackCollisionController.AttackerID == EntityRoot.Require(other).Id)
                {
                    return;
                }
                
                //衝突時の法線を計算し、その法線で反射させる
                Vector2 closerPoint = other.ClosestPoint(transform.position);
                Vector2 normal = (physicsMover.Position - closerPoint).normalized;
                var reflectVelocity = Vector2.Reflect(physicsMover.Velocity, normal);
                reflectVelocity *= 0.2f;
                physicsMover.AddForceVelocity(reflectVelocity,true);
            };


        }
    }

}
