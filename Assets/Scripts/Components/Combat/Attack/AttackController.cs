using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Core.Exceptions;
using Core.Constants;
using Components.Movement;
using Components.Detection;
using Components.Identity;
using Data;

namespace Components.Combat.Attack
{
    
    /// <summary>
    /// コンポーネント
    /// 速度が速いと攻撃判定が付くデフォルト機能
    /// また、コリジョンを生成・削除もできる
    /// </summary>
    public class AttackController : MonoBehaviour
    {
        
        [SerializeField] private float relaxSpeed; //攻撃判定を生成しない速度
        [SerializeField] private AttackCollisionSetting collisionSetting; //攻撃判定
        
        //キャッシュ
        private List<Transform> atkChannels;
        
        //イベント
        public event Action OnAttackVelocity; //速度による攻撃を行った時

        private void Awake()
        {
            //TODO:手動で追加ではなく、ここで自動的に生成したほうが良い
            //atkChannelをあるだけ保存しておく
            atkChannels = new List<Transform>();
            foreach (var channel in GetComponentsInChildren<Transform>())
            {
                atkChannels.Add(channel);
            }
        }

        private void OnEnable()
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
                OnAttackVelocity?.Invoke();
                
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
        
        
        public void ActivateCollision(
            AttackCollisionSetting setting,
            AttackPowerType powerType=AttackPowerType.Velocity
            )
        {
            
        }
        
    }

}
