using System;
using Core.Contracts;
using UnityEngine;
using Components.Identity;
using Data;

namespace Components.Combat.Attack
{
    /// <summary>
    /// Normal: 設定されたAttackPowerをそのまま用いる
    /// Velocity: 自身のルートオブジェクトの速度を用いる
    /// Radial: 自身のルートオブジェクトから相手への向きを用いる
    /// </summary>
    public enum AttackPowerType
    {
        Normal,
        Velocity,
        Radial,
    }
    public class PropAttackCollisionController : MonoBehaviour, IAttackInfoGetter
    {
        
        //状態
        private bool _isActive = false;
        private AttackInfo _attackInfo;
        private Collider2D _collider;
        private AttackPowerType _powerType;
        
        public bool IsActive => _isActive;

        //攻撃者の識別
        public EntityId AttackerID { get; private set; }
        
        //攻撃情報取得時のイベント
        public event Func<Vector2> OnGetAttackInfo;

        public void Initialize(AttackCollisionSetting collisionSetting)
        {
            //コリジョンの攻撃情報
            _attackInfo.attackVelocity = collisionSetting.attackPower;
            _attackInfo.damage = collisionSetting.damage;

            //コリジョン形状の設定
            switch (collisionSetting.shape)
            {
                case ColliderShape.Circle:
                    var circleCollider = gameObject.AddComponent<CircleCollider2D>();
                    circleCollider.radius = collisionSetting.circleRadius;
                    circleCollider.offset = collisionSetting.offset;
                    circleCollider.enabled = true;
                    _collider = circleCollider;
                    break;
                case ColliderShape.Capsule:
                    var capsuleCollider = gameObject.AddComponent<CapsuleCollider2D>();
                    capsuleCollider.size =
                        new Vector2(collisionSetting.capsuleRadius * 2, collisionSetting.capsuleHeight);
                    capsuleCollider.direction = collisionSetting.capsuleDirection == CapsuleDirection.X
                        ? CapsuleDirection2D.Horizontal
                        : CapsuleDirection2D.Vertical;
                    capsuleCollider.offset = collisionSetting.offset;
                    capsuleCollider.enabled = true;
                    _collider = capsuleCollider;
                    break;
                case ColliderShape.Box:
                    var boxCollider = gameObject.AddComponent<BoxCollider2D>();
                    boxCollider.size = new Vector2(collisionSetting.boxSize.x, collisionSetting.boxSize.y);
                    boxCollider.offset = collisionSetting.offset;
                    boxCollider.enabled = true;
                    _collider = boxCollider;
                    break;
                default:
                    break;
            }
            
            _isActive = false;
            _collider.enabled = false;
            _collider.isTrigger = true;
        }

        public void Activate(GameObject attacker, AttackPowerType type = AttackPowerType.Velocity)
        {
            _powerType = type;
            _isActive = true;
            _collider.enabled = true;
            
            //nullであれば、攻撃者に変更はなしと判断
            if (attacker != null)
            {
                AttackerID = EntityRoot.Require(attacker).Id;
            }
        }

        public void Deactivate()
        {
            _isActive = false;
            _collider.enabled = false;
        }

        public AttackInfo GetAttackInfo(Vector2 otherPosition)
        {
            if (!_isActive)
                throw new InvalidOperationException($"[{GetType().Name}] コリジョンが非アクティブであるのにも関わらず、攻撃者情報を取得しようとしています");
            
            //_attackInfoを少し改造する
            //TODO: AttackPower.x を乗算してどちらも倍率計算させる予定
            if (_powerType == AttackPowerType.Velocity)
            {
                //自分の速度*αの攻撃速度を持つようにする
                var selfVelocity = OnGetAttackInfo?.Invoke();
                if (selfVelocity.HasValue)
                {
                    var velocity = new Vector2(Mathf.Abs(selfVelocity.Value.x),Mathf.Abs(selfVelocity.Value.y));
                    _attackInfo.attackVelocity = velocity * 0.4f;
                }
            }
            else if (_powerType == AttackPowerType.Radial)
            {
                var rootPos = EntityRoot.Require(this).transform.position;
                var direction = (otherPosition - new Vector2(rootPos.x, rootPos.y)).normalized;
                _attackInfo.attackVelocity = direction * 3.0f;
            }
            
            return _attackInfo;
        }
    }
}

