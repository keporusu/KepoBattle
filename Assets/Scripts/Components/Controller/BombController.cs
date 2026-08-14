using System;
using System.Linq;
using System.Threading;
using Components.Combat.Attack;
using Components.Detection;
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
        [SerializeField] private bool fireOnSpawn = false; //初手から着火しているか？
        [SerializeField] private float explosionRadius = 10.0f;
        [SerializeField] private float explosionDamage = 10.0f;
        
        //設定
        private AttackCollisionSetting _explosionCollisionSetting;
        
        //状態
        private bool _isFire;
        
        //キャッシュ
        private VelocityAttackGenerator _velocityAttackGeneratorCache;
        
        //キャンセル
        private CancellationTokenSource _cts;

        private void Start()
        {
            //キャッシュ
            if(!TryGetComponent(out _velocityAttackGeneratorCache))
                throw new MissingComponentException($"[{GetType().Name}] AttackController が {gameObject.name} に見つかりません");
            
            //初期化
            _explosionCollisionSetting.shape = ColliderShape.Circle;
            _explosionCollisionSetting.damage = explosionDamage;
            _explosionCollisionSetting.circleRadius = explosionRadius;
            
            //着火
            if (fireOnSpawn)
            {
                Fire();
            }
        }
        
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
            _velocityAttackGeneratorCache.OnAttackVelocity += Explode;
        }
        private void OnDisable()
        {
            var damageCollider = GetComponentsInChildren<Transform>()
                                     .FirstOrDefault(obj => obj.gameObject.CompareTag(GameTags.DamageChannel))
                                 ?? throw new MissingChannelException(GameTags.DamageChannel, gameObject.name);
            if(!damageCollider.TryGetComponent(out DamageHitNotifier damagedNotifier))
                throw new MissingComponentException($"[{GetType().Name}] DamageHitNotifier が {gameObject.name} に見つかりません");
            damagedNotifier.OnHit -= FireFromHit;
            
            _velocityAttackGeneratorCache.OnAttackVelocity -= Explode;
        }
        
        
        private void FireFromHit(Collider2D other)
        {
            Fire();
        }

        private async void Fire()
        {
            _isFire = true;
            _cts = new CancellationTokenSource();
            await FireAfterDelay(explodeTime, _cts.Token);
        }

        private async UniTask FireAfterDelay(float delay, CancellationToken token)
        {
            await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: token);
            Explode();
        }
        
        private void Explode()
        {
            //発火による爆発はキャンセル
            _cts?.Cancel();
            //_velocityAttackGeneratorCache.ActivateCollision(_explosionCollisionSetting);
        }
        
        
    }

}
