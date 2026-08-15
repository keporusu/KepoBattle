using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Components.Controller;
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
    /// </summary>
    public class VelocityAttackGenerator : MonoBehaviour
    {
        
        [SerializeField] private float relaxSpeed; //攻撃判定を生成しない速度
        [SerializeField] private AttackCollisionSetting collisionSetting; //攻撃判定
        
        //イベント
        public event Action OnAttackVelocity; //速度による攻撃を行った時

        private void OnEnable()
        {
            if (!TryGetComponent(out PropPhysicsMover physicsMover))
            {
                throw new MissingComponentException($"[{GetType().Name}] PropPhysicsMover が {gameObject.name} に見つかりません");
            }
            
            if (!TryGetComponent(out CollisionManager collisionManager))
            {
                throw new MissingComponentException($"[{GetType().Name}] CollisionManager が {gameObject.name} に見つかりません");
            }
            
            //十分に速度が低下した閾値を設定
            physicsMover.SetRelaxSpeed(relaxSpeed);
            //コリジョン初期化
            var id = collisionManager.GetAvailableCollisionId();
            
            //攻撃によってイベントを発火
            physicsMover.OnForce += (GameObject instigator) =>
            {
                collisionManager.ActivateCollision(id, instigator, collisionSetting);
            };
            physicsMover.OnRelax += () =>
            {
                collisionManager.DeactivateCollision(id);
            };
            
            //攻撃があたったら、自身は跳ね返る
            collisionManager.AddListener(
                id,
                (Collider2D other) =>
                {
                    //自分が攻撃者の攻撃は自分に当たらない
                    if (collisionManager.GetAttackerId(id) == EntityRoot.Require(other).Id)
                    {
                        return;
                    }
                    
                    OnAttackVelocity?.Invoke();
                
                    //衝突時の法線を計算し、その法線で反射させる
                    Vector2 closerPoint = other.ClosestPoint(transform.position);
                    Vector2 normal = (physicsMover.Position - closerPoint).normalized;
                    var reflectVelocity = Vector2.Reflect(physicsMover.Velocity, normal);
                    reflectVelocity *= 0.2f;
                    physicsMover.AddForceVelocity(reflectVelocity,true);
                }
                );
        }
        
    }

}
