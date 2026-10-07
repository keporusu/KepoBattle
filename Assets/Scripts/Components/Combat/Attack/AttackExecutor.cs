using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using JetBrains.Annotations;
using Components.Animation;
using Components.Controller;
using Components.Identity;
using Core.Constants;
using Data;
using Systems;

namespace Components.Combat.Attack
{
    public enum AttackType
    {
        Attack1,
        Attack2,
        Attack3,
        None,
    }

    public class AttackExecutor : MonoBehaviour
    {

        //使用するアニメーター
        [SerializeField] private Animator animator;
        //攻撃設定
        [SerializeField] private List<AttackData> _attackDatas;

        //各攻撃に対応するステートの進行状況取得用
        private List<StateProgressionNotifier> _spNotifierAttacks_Cache = new List<StateProgressionNotifier>();
        private AnimatorTrigger _animatorTrigger_Cache;
        
        //攻撃キャンセル
        private CancellationTokenSource _attackCts;
        
        //コリジョン管理
        private AttackCollisionsManager _collisionsManager;
        private List<int?> _collisionIds = new List<int?>();

        //進行中の攻撃
        private AttackType _progressAttack = AttackType.None;

        //攻撃終了通知
        public event Action OnAttackFinish;
        
        //各攻撃で使うコールバック
        private Dictionary<AnimatorStates, List<Action<Animator,AnimatorStateInfo,int>>> _animatorStateCallbacks;
        
        //設定されたチャージ
        private float? _attackCharge; 

        void Awake()
        {
            
            //TODO: 以下の処理はうまくやれば_attackDatasから動的に自動生成できるはず
            //TODO: 具体的には、attackDatasを走査して、動的に無名関数をmapに登録する
            var attack1 = _attackDatas.FirstOrDefault(x => x.attackName == AttackName.Attack1);
            var attack2 = _attackDatas.FirstOrDefault(x => x.attackName == AttackName.Attack2);
            var attack3 = _attackDatas.FirstOrDefault(x => x.attackName == AttackName.Attack3);
            _animatorStateCallbacks = new Dictionary<AnimatorStates, List<Action<Animator,AnimatorStateInfo,int>>>()
            {
                { AnimatorStates.Attack1 , new List<Action<Animator,AnimatorStateInfo,int>>()
                {
                    //開始
                    (Animator animator, AnimatorStateInfo stateInfo, int layerIndex) =>
                    {
                        _progressAttack = AttackType.Attack1;
                    },
                    //途中
                    (Animator animator, AnimatorStateInfo stateInfo, int layerIndex) =>
                    {
                        SetCollisionAttack(attack1,stateInfo);
                    },
                    //終了
                    (Animator animator, AnimatorStateInfo stateInfo, int layerIndex) =>
                    {
                        CancelAttack();
                        OnAttackFinish?.Invoke();
                    }
                }},
                { AnimatorStates.Attack2 , new List<Action<Animator,AnimatorStateInfo,int>>()
                {
                    //開始
                    (Animator animator, AnimatorStateInfo stateInfo, int layerIndex) =>
                    {
                        _progressAttack = AttackType.Attack2;
                    },
                    //途中
                    (Animator animator, AnimatorStateInfo stateInfo, int layerIndex) =>
                    {
                        SetCollisionAttack(attack2,stateInfo);
                    },
                    //終了
                    (Animator animator, AnimatorStateInfo stateInfo, int layerIndex) =>
                    {
                        CancelAttack();
                        OnAttackFinish?.Invoke();
                    }
                }},
                { AnimatorStates.Attack3 , new List<Action<Animator,AnimatorStateInfo,int>>()
                {
                    //開始
                    (Animator animator, AnimatorStateInfo stateInfo, int layerIndex) =>
                    {
                        _progressAttack = AttackType.Attack1;
                    },
                    //途中
                    (Animator animator, AnimatorStateInfo stateInfo, int layerIndex) =>
                    {
                        SetCollisionAttack(attack3,stateInfo);
                    },
                    //終了
                    (Animator animator, AnimatorStateInfo stateInfo, int layerIndex) =>
                    {
                        CancelAttack();
                        OnAttackFinish?.Invoke();
                    }
                }},
            };
        }
        
        
        void Start()
        {
            //全てのNotifierを取得
            var spNotifiers = animator.GetBehaviours<StateProgressionNotifier>();
            //それぞれの攻撃のNotifierを取得 Attack ↔ Notifier
            //TODO: Notifierのステート名と攻撃設定の名前が一致していれば対応付けることにしているが、少し雑
            foreach(var attackData in _attackDatas)
            {
                var spNotifier=System.Array.Find(spNotifiers, x => x.StateName.ToString() == attackData.attackName.ToString());
                _spNotifierAttacks_Cache.Add(spNotifier);

                if (!spNotifier)
                {
                    Debug.LogError($"Notifier for {attackData.attackName} is missing.");
                }
            }
            
            //AttackCollisionsManagerの取得
            if (!TryGetComponent(out _collisionsManager))
            {
                throw new MissingComponentException($"[{GetType().Name}] AttackCollisionsManager が {gameObject.name} に見つかりません");
            }
            
            
            //AnimatorTriggerの取得
            if (!TryGetComponent(out _animatorTrigger_Cache))
            {
                throw new MissingComponentException($"[{GetType().Name}] AnimatorTrigger が {gameObject.name} に見つかりません");
            }

            //アニメーション再生中のコリジョン反映イベント
            for (int i = 0; i < _spNotifierAttacks_Cache.Count; i++)
            {
                var state = (AnimatorStates)Enum.Parse(typeof(AnimatorStates),
                    _spNotifierAttacks_Cache[i].StateName.ToString());
                _spNotifierAttacks_Cache[i].OnStateBegin += _animatorStateCallbacks[state][0];
                _spNotifierAttacks_Cache[i].OnStateProgress += _animatorStateCallbacks[state][1];
                _spNotifierAttacks_Cache[i].OnStateEnd+= _animatorStateCallbacks[state][2];
            }
        }


        public void StartCharge()
        {
            CancelAttack();
            
            //チャージっぽく見せるためにアニメーションを止める
            animator.speed = 0f;
            animator.Play("Attack1", -1, 0.35f);
        }


        public void StartAttack(AttackType attackType, float charge)
        {
            //パワーの設定
            _attackCharge = charge;
            
            //アニメーションを再開始する
            animator.speed = 1.0f;
            animator.Play("Attack1", -1, 0.5f);
            
            // トリガーは使わない（Chargeで強制的に遷移を行うため）
            // if (attackType == AttackType.Attack1)
            // {
            //     _animatorTrigger_Cache.TriggerAttack1();
            // }
        }

        public void CancelAttack()
        {
            _progressAttack = AttackType.None;
            foreach (var id in _collisionIds)
            {
                if (id.HasValue) _collisionsManager.DeactivateCollision(id.Value);
            }
        }

        private void SetCollisionAttack(AttackData attack, AnimatorStateInfo stateInfo)
        {
            // TODO: これを毎フレームAnimControllerのステート中に呼ばれるようにして、コリジョンの位置を調整する

            var collisionSettings = attack.collisionSettings;
            
            int id = 0;
            foreach (var setting in collisionSettings)
            {
                if (stateInfo.normalizedTime >= setting.spanStart && !_collisionsManager.IsActive(id))
                {
                    var fixedCollisionSetting = setting.collision;
                    
                    //パワーの設定
                    if (_attackCharge.HasValue) fixedCollisionSetting.power = _attackCharge.Value;
                    
                    //キャラクターの向きによって攻撃を出す方向を逆にする
                    var root = EntityRoot.Require(this);
                    var offset = fixedCollisionSetting.offset;
                    
                    //TODO: 左向きなら -offset.x にすべきな気がするが... なぜこれでうまくいくのだろうか
                    if (root.IsRight)
                    {
                        fixedCollisionSetting.offset = new Vector2(-offset.x, offset.y);
                    }
                    
                    //マウスの位置によって目標地点を変える
                    var mousePos = GameUtility.Instance.GetMouseWorldPos();
                    var attackDir = (mousePos - root.Position).normalized;
                    var forward = root.IsRight ? Vector2.right : Vector2.left;
                    if (Vector2.Dot(attackDir, forward) < 0)
                    {
                        //マウスの方向とキャラクターの向きが逆なら、ベクトルを反転させる
                        attackDir *= -1;
                    }

                    if (_attackCharge.HasValue)
                        fixedCollisionSetting.destination = attackDir * _attackCharge.Value;
                    
                    _collisionsManager.ActivateCollision(id, root.gameObject, fixedCollisionSetting);
                }
                id++;
            }

            id = 0;
            foreach (var setting in collisionSettings)
            {
                if (stateInfo.normalizedTime > setting.spanEnd && _collisionsManager.IsActive(id))
                {
                    _collisionsManager.DeactivateCollision(id);
                }

                id++;
            }
        }
        

        private void OnDestroy()
        {
            //CancelAttack();
        }
    }
}
