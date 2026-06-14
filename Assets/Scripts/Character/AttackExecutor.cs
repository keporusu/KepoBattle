using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using JetBrains.Annotations;
using Animation;

public enum AttackType
{
    Attack1,
    Attack2,
    SpecialAttack,
    None,
}

namespace Character
{
    public class AttackExecutor : MonoBehaviour
    {

        //使用するアニメーター
        [SerializeField] private Animator animator;

        //攻撃時のコリジョンの設定
        [SerializeField] private List<AttackCollisionSetting> attack1CollisionSettings;
        [SerializeField] private List<AttackCollisionSetting> attack2CollisionSettings;
        [SerializeField] private List<AttackCollisionSetting> attack3CollisionSettings;

        //攻撃キャンセル
        private CancellationTokenSource _attackCts;

        //ステートの進行状況取得用
        private StateProgressionNotifier _spNotifierAttack1_Cache;
        private StateProgressionNotifier _spNotifierAttack2_Cache;
        private StateProgressionNotifier _spNotifierAttack3_Cache;

        //コリジョン管理
        private List<bool> _isExecuting = new List<bool>(new bool[5]);
        private List<DamageCollisionManager> _damageColliderManagers = new List<DamageCollisionManager>();

        //進行中の攻撃
        private AttackType _progressAttack = AttackType.None;

        //攻撃終了通知
        public Action OnAttackFinish;

        void Start()
        {
            //全てのNotifierを取得
            var spNotifiers = animator.GetBehaviours<StateProgressionNotifier>();
            //それぞれの攻撃のNotifierを取得
            _spNotifierAttack1_Cache = System.Array.Find(spNotifiers, x => x.StateName == "Attack1");
            _spNotifierAttack2_Cache = System.Array.Find(spNotifiers, x => x.StateName == "Attack2");
            _spNotifierAttack3_Cache = System.Array.Find(spNotifiers, x => x.StateName == "SpecialAttack");
            if (_spNotifierAttack1_Cache == null || _spNotifierAttack2_Cache == null || _spNotifierAttack3_Cache == null)
            {
                Debug.LogError("Some behaviours are missing");
            }

            //子どものAttackChannelのコリジョンを取得
            var allChildren = transform.GetComponentsInChildren<Transform>(true);
            foreach (var child in allChildren)
            {
                //ダメージチャンネルからそれぞれコリジョンを取得してキャッシュする
                if (child.gameObject.CompareTag("Damage Channel"))
                {
                    var colliderManager = child.GetComponent<DamageCollisionManager>();
                    _damageColliderManagers.Add(colliderManager);
                }
            }

            //アニメーション再生中のコリジョン反映イベント
            _spNotifierAttack1_Cache.OnStateBegin += SetTypeAttack1;
            _spNotifierAttack2_Cache.OnStateBegin += SetTypeAttack2;
            _spNotifierAttack3_Cache.OnStateBegin += SetTypeAttack3Attack;
            _spNotifierAttack1_Cache.OnStateProgress += SetCollisionAttack;
            _spNotifierAttack2_Cache.OnStateProgress += SetCollisionAttack;
            _spNotifierAttack3_Cache.OnStateProgress += SetCollisionAttack;
            _spNotifierAttack1_Cache.OnStateEnd += AttackFinishCallback;
            _spNotifierAttack2_Cache.OnStateEnd += AttackFinishCallback;
            _spNotifierAttack3_Cache.OnStateEnd += AttackFinishCallback;
        }



        public void StartAttack1()
        {
            CancelAttack();
            //_attackCts = new CancellationTokenSource();
            //DeactivateAfterDuration(_attackCts.Token).Forget();
        }

        public void CancelAttack()
        {
            _isExecuting = new List<bool>(new bool[5]);
            _progressAttack = AttackType.None;
            foreach (var manager in _damageColliderManagers)
            {
                manager.Deactivate();
            }
        }


        private void SetTypeAttack1(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            _progressAttack = AttackType.Attack1;
        }

        private void SetTypeAttack2(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            _progressAttack = AttackType.Attack2;
        }

        private void SetTypeAttack3Attack(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            _progressAttack = AttackType.SpecialAttack;
        }

        private void SetCollisionAttack(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            // TODO: これを毎フレームAnimControllerのステート中に呼ばれるようにして、コリジョンの位置を調整する

            var collisionSettings = attack1CollisionSettings;
            if (_progressAttack == AttackType.Attack2)
            {
                collisionSettings = attack2CollisionSettings;
            }
            else if (_progressAttack == AttackType.SpecialAttack)
            {
                collisionSettings = attack3CollisionSettings;
            }

            int id = 0;
            foreach (var setting in collisionSettings)
            {
                if (stateInfo.normalizedTime >= setting.spanStart && !_isExecuting[id])
                {
                    _isExecuting[id] = true;
                    UseAvailableCollider(setting, id);
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

        //攻撃終了通知
        private void AttackFinishCallback(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
        {
            OnAttackFinish.Invoke();
        }



        //コリジョンの有効化と、パラメータのセット
        [CanBeNull]
        private void UseAvailableCollider(AttackCollisionSetting collisionSetting, int id)
        {
            var manager = _damageColliderManagers.FirstOrDefault(x => !x.IsActive);
            if (manager == null)
            {
                throw new InvalidOperationException($"DamageColliderManager が足りません");
            }

            manager.Activate(collisionSetting, id);
        }

        //コリジョンの無効化
        private void DeactivateCollider(int id)
        {
            var manager = _damageColliderManagers.FirstOrDefault(x => x.OwnerID == id);
            if (manager == null)
            {
                throw new InvalidOperationException(
                    $"DeactivateCollider: OwnerID {id} に対応する DamageColliderManager が見つかりません");
            }

            manager.Deactivate();
        }

        private void OnDestroy()
        {
            CancelAttack();
        }
    }
}
