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
        
        //攻撃が当たった時の跳ね返り
        [SerializeField] private float reflectRatio = 0.2f; //当たった速度に対する跳ね返り速度の割合
        [SerializeField] private float switchReflectRatio = 0.6f; //相手がスイッチの場合の割合
        [SerializeField] private float switchMinReflectSpeed = 3.0f; //相手がスイッチの場合の、跳ね返り速度の下限
        
        //イベント
        public event Action<Collider2D> OnAttackVelocity; //速度による攻撃を行った時

        private void OnEnable()
        {
            if (!TryGetComponent(out PropPhysicsMover physicsMover))
            {
                throw new MissingComponentException($"[{GetType().Name}] PropPhysicsMover が {gameObject.name} に見つかりません");
            }
            
            if (!TryGetComponent(out AttackCollisionsManager collisionManager))
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
                    var otherRoot = EntityRoot.Require(other);

                    //自分が攻撃者の攻撃は自分に当たらない
                    if (collisionManager.GetAttackerId(id) == otherRoot.Id)
                    {
                        return;
                    }

                    //衝突時の法線を計算する
                    Vector2 closerPoint = other.ClosestPoint(transform.position);
                    Vector2 normal = (physicsMover.Position - closerPoint).normalized;
                    //中心が相手の内側にあると ClosestPoint は中心そのものを返し、法線がゼロになる
                    //その場合は進んできた向きの逆を法線とし、来た方向へ跳ね返す
                    if (normal == Vector2.zero)
                    {
                        normal = -physicsMover.Velocity.normalized;
                    }

                    //相手に近づいていなければ、ぶつかったことにしない
                    //隣り合う相手と一緒に飛ぶ時や遠ざかる時は、判定が重なっただけで衝突ではない
                    var relativeVelocity = physicsMover.Velocity - (otherRoot.Velocity ?? Vector2.zero);
                    if (Vector2.Dot(relativeVelocity, normal) >= 0.0f)
                    {
                        return;
                    }
                    
                    OnAttackVelocity?.Invoke(other);

                    //ベルは自身で相手を弾き返すため、ここでは跳ね返らない
                    //両方で速度を上書きすると、処理順によって結果が変わってしまう
                    if (otherRoot.Identity == ObjectIdentity.Bell)
                    {
                        return;
                    }

                    //法線で反射させる
                    var reflectVelocity = Vector2.Reflect(physicsMover.Velocity, normal);
                    
                    //スイッチは少し強く跳ね返り、遅く当たっても下限の速度では跳ね返る
                    if (otherRoot.Identity == ObjectIdentity.Switch)
                    {
                        reflectVelocity *= switchReflectRatio;
                        if (reflectVelocity.magnitude < switchMinReflectSpeed)
                        {
                            reflectVelocity = reflectVelocity.normalized * switchMinReflectSpeed;
                        }
                    }
                    else
                    {
                        reflectVelocity *= reflectRatio;
                    }
                    
                    physicsMover.AddForceVelocity(reflectVelocity,true);
                }
                );
        }
        
    }

}
