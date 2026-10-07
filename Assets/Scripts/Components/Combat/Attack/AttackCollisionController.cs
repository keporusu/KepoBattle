using System;
using Core.Contracts;
using UnityEngine;
using Components.Identity;
using Core.Constants;
using Data;
using UnityEngine.UIElements;

namespace Components.Combat.Attack
{
    
    
    public class AttackCollisionController : MonoBehaviour, IAttackInfoGetter
    {
        //Destination型で、吹き飛ばしの頂点までに上昇する高さの下限
        private const float MinRisingHeight = 0.5f;

        //状態
        private bool _isActive = false;
        private Collider2D _collider;
        private AttackCollisionSetting? _setting;

        //形状ごとのコライダーのキャッシュ
        //Initializeの度に生成すると、以前のコライダーが有効なまま参照を失い
        //Deactivateで消せない当たり判定になってしまう
        private CircleCollider2D _circleCollider;
        private CapsuleCollider2D _capsuleCollider;
        private BoxCollider2D _boxCollider;

        public bool IsActive => _isActive;

        //攻撃者の識別
        public EntityId AttackerID { get; private set; }
        
        public void Initialize(AttackCollisionSetting collisionSetting)
        {
            //コリジョンの攻撃情報
            _setting = collisionSetting;

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
        
        
        /// <summary>
        /// 自身の攻撃情報を返す
        /// </summary>
        /// <param name="additionalInfo">AttackInfoを決めるのに必要な相手の追加情報</param>
        /// <returns>自身の攻撃情報</returns>
        public AttackInfo? CalculateAttackInfo(AdditionalInfoForAttackCalculation additionalInfo)
        {
            if (!_isActive)
                Debug.LogError($"[{GetType().Name}] コリジョンが非アクティブであるのにも関わらず、攻撃者情報を取得しようとしています");


            if (!_setting.HasValue) return null;
            
            AttackInfo attackInfo = new AttackInfo();
            
            switch (_setting.Value.attackPowerType)
            {
                case AttackPowerType.Fixed:
                {
                    //単純にパワーと向きで攻撃方向を計算
                    attackInfo.attackPower = _setting.Value.direction.normalized * _setting.Value.power;
                    break;
                }
                case AttackPowerType.Velocity:
                {
                    //自分の速度*αの攻撃速度を持つようにする
                    var selfVelocity = EntityRoot.Require(this).Velocity;
                    if (selfVelocity.HasValue)
                    {
                        var velocity = selfVelocity.Value;
                        attackInfo.attackPower = velocity * _setting.Value.velocityAlpha;
                    }
                    break;
                }
                case AttackPowerType.Radial:
                {
                    //中心から放射状の向きに攻撃速度を持つ
                    var rootPos = EntityRoot.Require(this).Position;
                    var direction = (additionalInfo.OtherPosition - new Vector2(rootPos.x, rootPos.y)).normalized;
                    attackInfo.attackPower = direction * _setting.Value.power;
                    break;
                }
                case AttackPowerType.Destination:
                {
                    //目標地点（相対）をもとに、attackPowerを算出
                    var destination = _setting.Value.destination * additionalInfo.OtherBlowAlpha + EntityRoot.Require(this).Position;
                    //目標地点は放物運動の頂点なので、相手より下にある場合は到達できない
                    //また高さの差が0に近いと上昇時間も0に近づき、x方向の速度が発散する
                    //そのため上昇する高さに下限を設ける
                    var risingHeight = Mathf.Max(destination.y - additionalInfo.OtherPosition.y, MinRisingHeight);
                    var risingTime = Mathf.Sqrt(risingHeight * 2 / GamePlaySettings.Gravity);
                    var risingVelocityY = risingTime * GamePlaySettings.Gravity;
                    var risingVelocityX = (destination.x - additionalInfo.OtherPosition.x) / risingTime;
                    attackInfo.attackPower = new Vector2(risingVelocityX, risingVelocityY);
                    break;
                }
            }
            
            attackInfo.damage = _setting.Value.damage;
            attackInfo.ignoreDuration = _setting.Value.ignoreDuration;
            
            return attackInfo;
        }
    }
}

