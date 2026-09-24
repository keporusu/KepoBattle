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
using Systems;
using Unity.VisualScripting;
using UnityEngine;
using Sequence = DG.Tweening.Sequence;

namespace Components.Controller
{
    public enum BombState
    {
        Idle,
        Fire,
        Explode,
    }
    public class BombController : PropBehaviourController
    {
        [SerializeField] private float explodeTime = 1.0f;
        [SerializeField] private float collisionTime = 0.2f;
        [SerializeField] private bool fireOnSpawn = false; //初手から着火しているか？
        [SerializeField] private float explosionRadius = 10.0f;
        [SerializeField] private float explosionPower = 2.0f;
        [SerializeField] private float explosionDamage = 10.0f;
        [SerializeField] private Vector2 explosionShakeSize;
        [SerializeField] private float explosionShakeDuration;
        [SerializeField] private SpriteRenderer spRenderer;
        [SerializeField] private Sprite explosionSprite;
        
        //設定
        private AttackCollisionSetting _explosionCollisionSetting;
        
        //キャッシュ
        private CollisionManager _collisionManager_Cache;
        private SoundManager _soundManager;
        
        //状態
        private BombState _state = BombState.Idle; //発火中か？
        private int? _audioID; //現在再生している音の再生ID
        
        //キャンセル
        private int? _fireTimerId;
        private int? _explodeTimerId;
        
        private void Start()
        {
            //キャッシュ
            if(!TryGetComponent(out _collisionManager_Cache))
                throw new MissingComponentException($"[{GetType().Name}] CollisionManager が {gameObject.name} に見つかりません");
            
            _soundManager = SoundManager.Instance;
            
            
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
        
        
        protected override void OnDamageHit(Collider2D other)
        {
            base.OnDamageHit(other);
            
            //着火済みなら着火しない
            if (_state == BombState.Fire) return;
            //爆発済みなら着火しない
            if (_state == BombState.Explode) return;
            
            Fire();
        }

        protected override void OnAttackVelocity(Collider2D other)
        {
            base.OnAttackVelocity(other);
            
            //爆発済みなら爆発しない
            if (_state == BombState.Explode) return;
            Explode();
        }
        
        protected override void OnTimerEvent(PropTimerEvent timerEvent)
        {
            base.OnTimerEvent(timerEvent);
            
            //着火→爆発
            if (timerEvent.TimerId == _fireTimerId)
            {
                //爆発
                if (timerEvent.EventType == TimerEventType.End)
                {
                    Explode();
                }
            }
            //爆発→破棄
            else if (timerEvent.TimerId == _explodeTimerId) 
            {
                //破棄
                if (timerEvent.EventType == TimerEventType.End)
                {
                    DestroyBomb();
                }
            }
        }
        
        private void Fire()
        {
            _state = BombState.Fire;
            
            //着火タイマー
            _fireTimerId = SetTimer(explodeTime);
            
            //アニメーション,SE
            FireEffect();
        }

        private void FireEffect()
        {
            Sequence seq = DOTween.Sequence();
            for (int i = 0; i < 6; i++)
            {
                Color color;
                if (i % 2 == 0) color = Color.red;
                else color = Color.white;

                var i1 = i;
                seq.Append(
                    spRenderer.DOColor(color,0.001f)
                        .OnStart(() =>
                        {
                            if (i1 % 2 == 0) _audioID = _soundManager.PlaySe(SoundNames.SeExplosionTimer);
                        })
                );
                seq.Append(DOVirtual.DelayedCall(explodeTime/6,()=>{}));
            }
            seq.SetLink(spRenderer.gameObject);
            seq.SetId("Fire"+gameObject.GetEntityId());
            seq.Play();
        }

        
        
        private void Explode()
        {
            _state = BombState.Explode;
            
            //発火中のエフェクト処理はキャンセル
            DOTween.Kill("Fire"+gameObject.GetEntityId());
            //発火による爆発はキャンセル
            DestroyTimer(_fireTimerId);
            
            
            //カメラを揺らす
            GameUtility.Instance.SetCameraShake(explosionShakeSize, explosionShakeDuration);
            
            //爆発後のタイマー
            _explodeTimerId = SetTimer(collisionTime);
            
            //爆発コリジョンの生成
            var id = _collisionManager_Cache.GetAvailableCollisionId();
            _collisionManager_Cache.ActivateCollision(id,gameObject,_explosionCollisionSetting,AttackPowerType.Radial,true);
            
            //アニメーション開始
            ExplodeEffect();
        }

        private void ExplodeEffect()
        {
            var aId=_soundManager.PlaySe(SoundNames.SeExplosion);
            if (aId.HasValue)
            {
                //_soundManager.StopSe(_audioID);
                _audioID = aId;
            }
                
            spRenderer.sprite = explosionSprite;
            spRenderer.transform.DOScale(Vector3.one * 6.0f, 0.2f).SetLink(spRenderer.gameObject);
            spRenderer.DOFade(0.0f, 0.2f).SetEase(Ease.OutQuad).SetLink(spRenderer.gameObject);
        }

        private void DestroyBomb()
        {
            var root = EntityRoot.Require(this);
            Destroy(root.gameObject);
        }
        
    }

}
