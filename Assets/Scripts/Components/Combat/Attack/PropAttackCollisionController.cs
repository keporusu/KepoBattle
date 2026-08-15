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

        //形状ごとのコライダーのキャッシュ
        //Initializeの度に生成すると、以前のコライダーが有効なまま参照を失い
        //Deactivateで消せない当たり判定になってしまう
        private CircleCollider2D _circleCollider;
        private CapsuleCollider2D _capsuleCollider;
        private BoxCollider2D _boxCollider;

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
            //生成済みのコライダーがあれば使い回す
            switch (collisionSetting.shape)
            {
                case ColliderShape.Circle:
                    var circleCollider = GetOrAddCollider(ref _circleCollider);
                    circleCollider.radius = collisionSetting.circleRadius;
                    circleCollider.offset = collisionSetting.offset;
                    _collider = circleCollider;
                    break;
                case ColliderShape.Capsule:
                    var capsuleCollider = GetOrAddCollider(ref _capsuleCollider);
                    capsuleCollider.size =
                        new Vector2(collisionSetting.capsuleRadius * 2, collisionSetting.capsuleHeight);
                    capsuleCollider.direction = collisionSetting.capsuleDirection == CapsuleDirection.X
                        ? CapsuleDirection2D.Horizontal
                        : CapsuleDirection2D.Vertical;
                    capsuleCollider.offset = collisionSetting.offset;
                    _collider = capsuleCollider;
                    break;
                case ColliderShape.Box:
                    var boxCollider = GetOrAddCollider(ref _boxCollider);
                    boxCollider.size = new Vector2(collisionSetting.boxSize.x, collisionSetting.boxSize.y);
                    boxCollider.offset = collisionSetting.offset;
                    _collider = boxCollider;
                    break;
                default:
                    break;
            }

            //前回と違う形状に切り替わったとき、使わないコライダーを有効なまま残さない
            DisableUnusedColliders();

            _isActive = false;
            _collider.enabled = false;
            _collider.isTrigger = true;
        }

        /// <summary>
        /// 指定した形状のコライダーを取得する
        /// まだ無ければ生成してキャッシュする
        /// </summary>
        /// <param name="cache">形状ごとのキャッシュ</param>
        /// <returns>使用するコライダー</returns>
        private T GetOrAddCollider<T>(ref T cache) where T : Collider2D
        {
            if (cache == null)
            {
                cache = gameObject.AddComponent<T>();
            }

            return cache;
        }

        /// <summary>
        /// 今使っている形状以外のコライダーを無効化する
        /// </summary>
        private void DisableUnusedColliders()
        {
            if (_circleCollider != null && _circleCollider != _collider)
            {
                _circleCollider.enabled = false;
            }

            if (_capsuleCollider != null && _capsuleCollider != _collider)
            {
                _capsuleCollider.enabled = false;
            }

            if (_boxCollider != null && _boxCollider != _collider)
            {
                _boxCollider.enabled = false;
            }
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
                Debug.LogError($"[{GetType().Name}] コリジョンが非アクティブであるのにも関わらず、攻撃者情報を取得しようとしています");
            
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
                _attackInfo.attackVelocity = direction * _attackInfo.attackVelocity.x;
            }
            
            return _attackInfo;
        }
    }
}

