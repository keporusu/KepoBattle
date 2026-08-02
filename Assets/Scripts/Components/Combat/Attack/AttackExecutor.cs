using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using JetBrains.Annotations;
using Components.Animation;
using Core.Constants;
using Data;

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
        private List<bool> _isExecuting = new List<bool>(new bool[5]);
        private List<CharacterAttackCollisionController> _damageColliderControllers = new List<CharacterAttackCollisionController>();

        //進行中の攻撃
        private AttackType _progressAttack = AttackType.None;

        //攻撃終了通知
        public event Action OnAttackFinish;
        
        //各攻撃で使うコールバック
        private Dictionary<AnimatorStates, List<Action<Animator,AnimatorStateInfo,int>>> _animatorStateCallbacks;

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

            //子どものAttackChannelのコリジョンを取得
            var allChildren = transform.GetComponentsInChildren<Transform>(true);
            foreach (var child in allChildren)
            {
                //攻撃チャンネルからそれぞれコリジョンを取得してキャッシュする
                if (child.gameObject.CompareTag(GameTags.AttackChannel))
                {
                    var colliderManager = child.GetComponent<CharacterAttackCollisionController>();
                    _damageColliderControllers.Add(colliderManager);
                }
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



        public void StartAttack(AttackType attackType)
        {
            CancelAttack();
            if (attackType == AttackType.Attack1)
            {
                _animatorTrigger_Cache.TriggerAttack1();
            }
        }

        public void CancelAttack()
        {
            var maxExecuting = _attackDatas.Max(x => x.collisionSettings.Count);
            _isExecuting = new List<bool>(new bool[maxExecuting]);
            _progressAttack = AttackType.None;
            foreach (var manager in _damageColliderControllers)
            {
                manager.Deactivate();
            }
        }

        private void SetCollisionAttack(AttackData attack, AnimatorStateInfo stateInfo)
        {
            // TODO: これを毎フレームAnimControllerのステート中に呼ばれるようにして、コリジョンの位置を調整する

            var collisionSettings = attack.collisionSettings;
            
            int id = 0;
            foreach (var setting in collisionSettings)
            {
                if (stateInfo.normalizedTime >= setting.spanStart && !_isExecuting[id])
                {
                    _isExecuting[id] = true;
                    UseAvailableCollider(setting.collision, id);
                }

                id++;
            }

            id = 0;
            foreach (var setting in collisionSettings)
            {
                if (stateInfo.normalizedTime > setting.spanEnd && _isExecuting[id])
                {
                    _isExecuting[id] = false;
                    DeactivateCollider(id);
                }

                id++;
            }
        }



        //コリジョンの有効化と、パラメータのセット
        private void UseAvailableCollider(AttackCollisionSetting collisionSetting, int id)
        {
            var manager = _damageColliderControllers.FirstOrDefault(x => !x.IsActive);
            if (manager == null)
            {
                throw new InvalidOperationException($"DamageColliderManager が足りません");
            }

            manager.Activate(collisionSetting, id);
        }

        //コリジョンの無効化
        private void DeactivateCollider(int id)
        {
            var manager = _damageColliderControllers.FirstOrDefault(x => x.UniqueID == id);
            if (manager == null)
            {
                throw new InvalidOperationException(
                    $"DeactivateCollider: OwnerID {id} に対応する DamageColliderManager が見つかりません");
            }

            manager.Deactivate();
        }

        private void OnDestroy()
        {
            //CancelAttack();
        }
    }
}
