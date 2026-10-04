using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Components.Combat.Attack;
using Components.Detection;
using Components.Identity;
using Components.Movement;
using Core.Constants;
using Core.Exceptions;
using Cysharp.Threading.Tasks;
using Systems;

public enum TimerEventType
{
    Start,
    End
}
public struct PropTimerEvent
{
    public int TimerId;
    public TimerEventType EventType;
    public float RemainTime;
}

public struct PropTimerActionSet
{
    public Action<int> TimerAction;
    public CancellationTokenSource Cancel;
}

namespace Components.Controller
{
    public class PropBehaviourController : MonoBehaviour
    {
        
        //レイヤー
        private LayerMask _characterLayer;
        private LayerMask _propLayer;
        

        //イベント:攻撃を受けた時
        protected virtual void OnDamageHit(Collider2D other){}
        //イベント:体当たりで攻撃した時
        protected virtual void OnAttackVelocity(Collider2D other){}
        //イベント: バウンド時
        protected virtual void OnBounce(float bounceAmount) {}
        //イベント:タイマー中のなにかしらのイベント
        protected virtual void OnTimerEvent(PropTimerEvent timerEvent){}
        
        //プレイヤーから攻撃を受けたときの音
        protected virtual void PlayDamageSoundFromCharacter()
        {
            //デフォルトがない
            //SoundManager.Instance.PlaySe("PropDamageFromCharacter"a);
        }
        //Propが当たってきたときの音
        protected virtual void PlayDamageSoundFromProp()
        {
            SoundManager.Instance.PlaySe("PropDamageFromProp");
        }
        //バウンド時の音
        protected virtual void PlayBounceSound()
        {
            SoundManager.Instance.PlaySe("PropBounce");
        }


        private void Awake()
        {
            _characterLayer = LayerMask.GetMask(GameLayers.Character);
            _propLayer = LayerMask.GetMask(GameLayers.Prop);
        }

        private void OnEnable()
        {
            
            
            var damageCollider = GetComponentsInChildren<Transform>()
                                     .FirstOrDefault(obj => obj.gameObject.CompareTag(GameTags.DamageChannel))
                                 ?? throw new MissingChannelException(GameTags.DamageChannel, gameObject.name);
            
            //ダメージを受けたら
            if(!damageCollider.TryGetComponent(out DamageHitNotifier damagedNotifier))
                throw new MissingComponentException($"[{GetType().Name}] DamageHitNotifier が {gameObject.name} に見つかりません");
            damagedNotifier.OnHit += OnDamageHit;
            damagedNotifier.OnHit += PlayDamageSound;
            
            
            //自身が体当たりで攻撃したら
            if(!TryGetComponent(out VelocityAttackGenerator velocityAttackGenerator))
                throw new MissingComponentException($"[{GetType().Name}] AttackController が {gameObject.name} に見つかりません");
            velocityAttackGenerator.OnAttackVelocity += OnAttackVelocity;
            
            //接地・壁衝突判定
            if (!TryGetComponent(out PhysicsMover physicsMover))
                throw new MissingComponentException($"[{GetType().Name}] PhysicsMover が {gameObject.name} に見つかりません");
            physicsMover.OnBounce += OnBounce;
            physicsMover.OnBounce += PlayBounceSound;

        }
        private void OnDisable()
        {
            var damageCollider = GetComponentsInChildren<Transform>()
                                     .FirstOrDefault(obj => obj.gameObject.CompareTag(GameTags.DamageChannel))
                                 ?? throw new MissingChannelException(GameTags.DamageChannel, gameObject.name);
            
            //ダメージを受けたら
            if(!damageCollider.TryGetComponent(out DamageHitNotifier damagedNotifier))
                throw new MissingComponentException($"[{GetType().Name}] DamageHitNotifier が {gameObject.name} に見つかりません");
            damagedNotifier.OnHit -= OnDamageHit;
            damagedNotifier.OnHit -= PlayDamageSound;
            
            //自身が体当たりで攻撃したら
            if(!TryGetComponent(out VelocityAttackGenerator velocityAttackGenerator))
                throw new MissingComponentException($"[{GetType().Name}] VelocityAttackGenerator が {gameObject.name} に見つかりません");
            velocityAttackGenerator.OnAttackVelocity -= OnAttackVelocity;
            
            //接地・壁衝突判定
            if (!TryGetComponent(out PhysicsMover physicsMover))
                throw new MissingComponentException($"[{GetType().Name}] PhysicsMover が {gameObject.name} に見つかりません");
            physicsMover.OnBounce -= OnBounce;
            physicsMover.OnBounce -= PlayBounceSound;
        }
        
        
        private Dictionary<int,PropTimerActionSet> timerActions = new Dictionary<int,PropTimerActionSet>();
        private int timerCount = 0;
        /// <summary>
        /// タイマーをセットする
        /// </summary>
        /// <returns>タイマーのID</returns>
        protected int SetTimer(float time)
        {
            PropTimerActionSet timerActionSet;
            timerActionSet.Cancel = new CancellationTokenSource();
            
            async void TimerAction(int id)
            {
                //time秒待つ
                try
                {
                    await UniTask.Delay(System.TimeSpan.FromSeconds(time), cancellationToken: timerActionSet.Cancel.Token);
                }
                catch (OperationCanceledException)
                {
                    //キャンセルは正常処理
                    DestroyTimer(id);
                    return;
                }
                
                //タイマー終了イベントの発火
                PropTimerEvent timerEvent;
                timerEvent.TimerId = id;
                timerEvent.EventType = TimerEventType.End;
                timerEvent.RemainTime = 0.0f;
                OnTimerEvent(timerEvent);
                //タイマー破壊
                DestroyTimer(id);
            }

            timerActionSet.TimerAction = TimerAction;
            
            timerActions[timerCount] = timerActionSet;
            timerActions[timerCount].TimerAction?.Invoke(timerCount);
            
            return timerCount++;
        }
        
        /// <summary>
        /// Idでタイマーを止めてしまう
        /// </summary>
        /// <param name="timerId">タイマーのID</param>
        protected void DestroyTimer(int? timerId)
        {
            if (!timerId.HasValue) return;
            timerActions[timerId.Value].Cancel.Cancel();
        }
        
        private void PlayDamageSound(Collider2D other)
        {
            var otherRoot = EntityRoot.Require(other);

            if ((_characterLayer & (1 << otherRoot.gameObject.layer)) > 0) 
            {
                PlayDamageSoundFromCharacter();
            }

            if ((_propLayer & (1 << otherRoot.gameObject.layer)) > 0)
            {
                PlayDamageSoundFromProp();
            }
        }

        private void PlayBounceSound(float bounceAmount)
        {
            //バウンド時の音を鳴らす
            if (bounceAmount >= 1.0f)
            {
                PlayBounceSound();
            }
        }
        
    }
}