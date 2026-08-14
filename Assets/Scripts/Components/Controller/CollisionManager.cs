using System;
using System.Collections.Generic;
using Components.Combat.Attack;
using Components.Detection;
using Data;
using UnityEngine;


namespace Components.Controller
{
    /// <summary>
    /// コンポーネント
    /// コリジョンを生成・削除できる
    /// </summary>
    public class CollisionManager : MonoBehaviour
    {
        [SerializeField] private GameObject atkChannel_Prefab; //生成するコリジョンのオブジェクト
        [SerializeField] private float generateCount = 1;
        
        //キャッシュ
        private List<GameObject> atkChannels;
        
        private void Awake()
        {
            //プレハブに必要なコンポーネントがついているかチェック
            if (!atkChannel_Prefab.TryGetComponent(out PropAttackCollisionController attackCollisionController))
            {
                throw new MissingComponentException($"[{GetType().Name}] PropAttackCollisionController が {atkChannel_Prefab.gameObject.name} に見つかりません");
            }

            if (!atkChannel_Prefab.TryGetComponent(out AttackHitNotifier attackHitNotifier))
            {
                throw new MissingComponentException($"[{GetType().Name}] attackHitNotifier が {atkChannel_Prefab.gameObject.name} に見つかりません");
            }
            
            
            //自動的にコリジョンを生成
            atkChannels = new List<GameObject>();
            for (int i = 0; i < generateCount; i++)
            {
                var atkChannel = Instantiate(atkChannel_Prefab, transform);
                atkChannel.transform.localPosition = Vector3.zero;
                atkChannels.Add(atkChannel);
            }
        }
        
        /// <summary>
        /// 使えるコリジョンのIDを検索
        /// </summary>
        /// <returns>使えるコリジョンのID</returns>
        public int GetAvailableCollisionId()
        {
            for (int id = 0; id < atkChannels.Count; id++)
            {
                var isActive = atkChannels[id].GetComponent<PropAttackCollisionController>().IsActive;
                if (!isActive)
                {
                    return id;
                }
            }
            //ない
            return -1;
        }
        
        
        /// <summary>
        /// 特定のコリジョンの有効化
        /// </summary>
        /// <param name="id">有効化するコリジョンのID</param>
        /// <param name="instigator">この攻撃を引き起こしたもの</param>
        /// <param name="setting">コリジョンの設定</param>
        /// <param name="powerType"></param>
        public void ActivateCollision(
            int id,
            GameObject instigator,
            AttackCollisionSetting setting,
            AttackPowerType powerType=AttackPowerType.Velocity
        )
        {
            if (id >= atkChannels.Count || id < 0)
            {
                return;
            }
            var atk = atkChannels[id].GetComponent<PropAttackCollisionController>();
            atk.Initialize(setting);
            atk.Activate(instigator,powerType);
        }

        public void DeactivateCollision(int id)
        {
            if (id >= atkChannels.Count || id < 0)
            {
                return;
            }
            atkChannels[id].GetComponent<PropAttackCollisionController>().Deactivate();
        }

        public EntityId GetAttackerId(int id)
        {
            return atkChannels[id].GetComponent<PropAttackCollisionController>().AttackerID;
        }
        
        /// <summary>
        /// 攻撃時に発火するアクションの設定
        /// </summary>
        /// <param name="id">コリジョンのID</param>
        /// <param name="action">登録する関数</param>
        public void AddListener(int id, Action<Collider2D> action)
        {
            if (id >= atkChannels.Count || id < 0)
            {
                return;
            }
            atkChannels[id].GetComponent<AttackHitNotifier>().OnHit += action;
        }
    }

}
