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
using DG.Tweening;
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
        [SerializeField] private SpriteRenderer spRenderer;
        [SerializeField] private Sprite explosionSprite;
        
        //設定
        private AttackCollisionSetting _explosionCollisionSetting;
        
        //キャッシュ
        private CollisionManager _collisionManager_Cache;
        
        //状態
        private bool _isFire;
        
        //キャンセル
        private CancellationTokenSource _ctsFire;
        private CancellationTokenSource _ctsExplode;
        
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
        
        //TODO: UniTaskScheduler.UnhandledExceptionHandler でキャンセル例外を除外するグローバル設定をし、UniTaskVoid + Forget() でキャンセルがエラーとして出ないようにする
        private async void Fire()
        {
            //着火済みなら着火しない
            if(_isFire)return;
            
            _isFire = true;
            _ctsFire = new CancellationTokenSource();
            await FireAfterDelay(explodeTime, _ctsFire.Token);
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
            _ctsFire?.Cancel();
            //爆発コリジョンの生成
            var id = _collisionManager_Cache.GetAvailableCollisionId();
            _collisionManager_Cache.ActivateCollision(id,gameObject,_explosionCollisionSetting,AttackPowerType.Radial);
            //アニメーション開始
            spRenderer.sprite = explosionSprite;
            spRenderer.transform.DOScale(Vector3.one * 6.0f, 0.2f).SetLink(spRenderer.gameObject);
            spRenderer.DOFade(0.0f, 0.2f).SetEase(Ease.OutQuad).SetLink(spRenderer.gameObject);
            //爆発後処理
            _ctsExplode = new CancellationTokenSource();
            await ExplodeAfterDelay(collisionTime,_ctsExplode.Token);
        }

        private async UniTask ExplodeAfterDelay(float delay,CancellationToken token)
        {
            //数秒後に破壊処理
            await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: token);
            var root = EntityRoot.Require(this);
            Destroy(root.gameObject);
        }

        private void OnDestroy()
        {
            _ctsFire?.Cancel();
            _ctsExplode?.Cancel();
        }
    }

}
