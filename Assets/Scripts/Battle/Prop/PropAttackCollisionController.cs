using System;
using Battle.Interfaces;
using UnityEngine;

namespace Battle.Prop
{
    public class PropAttackCollisionController : MonoBehaviour, IAttackInfoGetter
    {

        private bool _isActive = false;
        private AttackInfo _attackInfo;
        private Collider2D _collider;

        //攻撃者の識別
        public EntityId AttackerID { get; private set; }

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
        
        public void Activate(GameObject attacker)
        {
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

        public AttackInfo GetAttackInfo()
        {
            if (!_isActive)
                throw new InvalidOperationException($"[{GetType().Name}] コリジョンが非アクティブであるのにも関わらず、攻撃者情報を取得しようとしています");
            return _attackInfo;
        }
    }
}

