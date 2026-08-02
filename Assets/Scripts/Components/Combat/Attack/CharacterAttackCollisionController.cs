using System;
using Core.Contracts;
using JetBrains.Annotations;
using UnityEngine;
using Components.Identity;
using Data;

namespace Components.Combat.Attack
{
    public class CharacterAttackCollisionController : MonoBehaviour, IAttackInfoGetter
    {
        private CircleCollider2D _circleCollider;
        private CapsuleCollider2D _capsuleCollider;
        private BoxCollider2D _boxCollider;

        private ColliderShape _lastActiveShape;
        private bool _isActive = false;
        private int _uniqueID = -1;
        private AttackInfo _attackInfo;
        private EntityRoot _entityRoot_Cache;

        public bool IsActive => _isActive;
        
        //コリジョンの識別
        public int UniqueID => _uniqueID;
        
        //攻撃者の識別
        public EntityId AttackerID => _entityRoot_Cache.Id;

        private void Start()
        {
            //このコリジョンを持つエンティティ = 攻撃者
            _entityRoot_Cache = EntityRoot.Require(this);

            _circleCollider = GetComponent<CircleCollider2D>();
            _capsuleCollider = GetComponent<CapsuleCollider2D>();
            _boxCollider = GetComponent<BoxCollider2D>();
            _circleCollider.enabled = false;
            _capsuleCollider.enabled = false;
            _boxCollider.enabled = false;
            gameObject.SetActive(false);
        }

        [CanBeNull]
        public Collider2D Activate(AttackCollisionSetting collisionSetting, int id)
        {
            //すでにアクティブなら何もしない
            if (gameObject.activeSelf) return null;

            _lastActiveShape = collisionSetting.shape;
            gameObject.SetActive(true);
            _isActive = true;
            _uniqueID = id;

            //コリジョンの攻撃情報
            _attackInfo.attackVelocity = collisionSetting.attackPower;
            _attackInfo.damage = collisionSetting.damage;

            //コリジョン形状の設定
            switch (collisionSetting.shape)
            {
                case ColliderShape.Circle:
                    _circleCollider.radius = collisionSetting.circleRadius;
                    _circleCollider.offset = collisionSetting.offset;
                    _circleCollider.enabled = true;
                    return _circleCollider;
                case ColliderShape.Capsule:
                    _capsuleCollider.size =
                        new Vector2(collisionSetting.capsuleRadius * 2, collisionSetting.capsuleHeight);
                    _capsuleCollider.direction = collisionSetting.capsuleDirection == CapsuleDirection.X
                        ? CapsuleDirection2D.Horizontal
                        : CapsuleDirection2D.Vertical;
                    _capsuleCollider.offset = collisionSetting.offset;
                    _capsuleCollider.enabled = true;
                    return _capsuleCollider;
                case ColliderShape.Box:
                    _boxCollider.size = new Vector2(collisionSetting.boxSize.x, collisionSetting.boxSize.y);
                    _boxCollider.offset = collisionSetting.offset;
                    _boxCollider.enabled = true;
                    return _boxCollider;
                default:
                    return null;
            }
        }

        public void Deactivate()
        {
            switch (_lastActiveShape)
            {
                case ColliderShape.Circle:
                    _circleCollider.enabled = false;
                    break;
                case ColliderShape.Capsule:
                    _capsuleCollider.enabled = false;
                    break;
                case ColliderShape.Box:
                    _boxCollider.enabled = false;
                    break;
            }

            gameObject.SetActive(false);
            _isActive = false;
            _uniqueID = -1;
        }
        
        public AttackInfo GetAttackInfo()
        {
            if (!_isActive)
                throw new InvalidOperationException($"[{GetType().Name}] コリジョンが非アクティブであるのにも関わらず、攻撃者情報を取得しようとしています");
            return _attackInfo;
        }
    }
}
