using System;
using System.Linq;
using System.Threading;
using Components.Combat.Attack;
using Components.Detection;
using Components.Identity;
using Core.Constants;
using Core.Exceptions;
using Cysharp.Threading.Tasks;
using Data;
using Unity.VisualScripting;
using UnityEngine;

namespace Components.Controller
{
    public class BombController : MonoBehaviour
    {
        [SerializeField] private float explodeTime = 1.0f;
        [SerializeField] private float collisionTime = 0.2f;
        [SerializeField] private bool fireOnSpawn = false; //初手から着火しているか？
        [SerializeField] private float explosionRadius = 10.0f;
        [SerializeField] private float explosionPower = 2.0f;
        [SerializeField] private float explosionDamage = 10.0f;
        
        //設定
        private AttackCollisionSetting _explosionCollisionSetting;
        
        //キャッシュ
        private CollisionManager _collisionManager_Cache;
        private SpriteRenderer _spriteRenderer_Cache;
        
        //状態
        private bool _isFire;
        
        //キャンセル
        private CancellationTokenSource _cts;
        
        private void OnEnable()
        {
            
            //ダメージを受けたら着火し、キャンセル付きのタイマーをスタート
            var damageCollider = GetComponentsInChildren<Transform>()
                                     .FirstOrDefault(obj => obj.gameObject.CompareTag(GameTags.DamageChannel))
                                 ?? throw new MissingChannelException(GameTags.DamageChannel, gameObject.name);
            if(!damageCollider.TryGetComponent(out DamageHitNotifier damagedNotifier))
                throw new MissingComponentException($"[{GetType().Name}] DamageHitNotifier が {gameObject.name} に見つかりません");
            damagedNotifier.OnHit += FireFromHit;
            
            
            //自身が体当たりで攻撃したら爆発させる
            if(!TryGetComponent(out VelocityAttackGenerator velocityAttackGenerator))
                throw new MissingComponentException($"[{GetType().Name}] AttackController が {gameObject.name} に見つかりません");
            velocityAttackGenerator.OnAttackVelocity += ExplodeFromHit;
        }
        private void OnDisable()
        {
            var damageCollider = GetComponentsInChildren<Transform>()
                                     .FirstOrDefault(obj => obj.gameObject.CompareTag(GameTags.DamageChannel))
                                 ?? throw new MissingChannelException(GameTags.DamageChannel, gameObject.name);
            if(!TryGetComponent(out VelocityAttackGenerator velocityAttackGenerator))
                throw new MissingComponentException($"[{GetType().Name}] VelocityAttackGenerator が {gameObject.name} に見つかりません");
            velocityAttackGenerator.OnAttackVelocity -= ExplodeFromHit;
        }
        
        private void Start()
        {
            //キャッシュ
            if(!TryGetComponent(out _collisionManager_Cache))
                throw new MissingComponentException($"[{GetType().Name}] CollisionManager が {gameObject.name} に見つかりません");
            
            if(!TryGetComponent(out _spriteRenderer_Cache))
                throw new MissingComponentException($"[{GetType().Name}] SpriteRenderer が {gameObject.name} に見つかりません");
            
            //初期化
            _explosionCollisionSetting.shape = ColliderShape.Circle;
            _explosionCollisionSetting.damage = explosionDamage;
            _explosionCollisionSetting.circleRadius = explosionRadius;
            _explosionCollisionSetting.attackPower.x = explosionPower;
            
            //着火
            if (fireOnSpawn)
            {
                Fire();
            }
        }
        
        private void FireFromHit(Collider2D other)
        {
            Fire();
        }

        private async void Fire()
        {
            //着火済みなら着火しない
            if(_isFire)return;
            
            _isFire = true;
            _cts = new CancellationTokenSource();
            await FireAfterDelay(explodeTime, _cts.Token);
        }

        private async UniTask FireAfterDelay(float delay, CancellationToken token)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: token);
            Explode();
        }

        private void ExplodeFromHit()
        {
            Explode();
        }
        
        private async void Explode()
        {
            //発火による爆発はキャンセル
            _cts?.Cancel();
            var id = _collisionManager_Cache.GetAvailableCollisionId();
            _collisionManager_Cache.ActivateCollision(id,gameObject,_explosionCollisionSetting,AttackPowerType.Radial);
            _spriteRenderer_Cache.enabled = false;
            await ExplodeAfterDelay(collisionTime);
        }

        private async UniTask ExplodeAfterDelay(float delay)
        {
            //数秒後に破壊処理
            await UniTask.Delay(System.TimeSpan.FromSeconds(delay));
            var root = EntityRoot.Require(this);
            Destroy(root.gameObject);
        }
        
        
    }

}
