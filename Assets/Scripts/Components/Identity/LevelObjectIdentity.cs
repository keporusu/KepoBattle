using System.Collections.Generic;
using Core.Constants;
using Core.Contracts;
using Data;
using UnityEngine;

namespace Components.Identity
{
    /// <summary>
    /// レベルデータから生成されたオブジェクトであることを示す
    /// LevelManager が生成時に付与し、レベルデータへの書き出しに使う
    /// EntityRoot.Id はインスタンスごとに変わるため保存には使わず、ここで永続IDを持つ
    /// </summary>
    [DisallowMultipleComponent]
    public class LevelObjectIdentity : MonoBehaviour
    {
        //種別キー
        public string TypeKey { get; private set; }

        //永続ID(GUID)
        public string InstanceId { get; private set; }

        //選択判定に使う形状(Geometry Channel のコライダー)
        private readonly List<Collider2D> _geometryColliders = new List<Collider2D>();

        public void Initialize(string typeKey, string instanceId)
        {
            TypeKey = typeKey;
            InstanceId = instanceId;

            //体の形を表す Geometry Channel のコライダーを選択判定に使う
            //攻撃や爆発のコライダーは体より大きいことがあり、選択判定には向かない
            foreach (var col in GetComponentsInChildren<Collider2D>(true))
            {
                if (col.CompareTag(GameTags.GeometryChannel))
                {
                    _geometryColliders.Add(col);
                }
            }
        }

        /// <summary>
        /// ワールド上の範囲(選択判定・強調表示用)
        /// Physics2D の AutoSyncTransforms が無効なため、Transform を動かした後は
        /// Physics2D.SyncTransforms() を呼んでから使うこと
        /// </summary>
        public Bounds GetBounds()
        {
            var hasBounds = false;
            var bounds = new Bounds(transform.position, Vector3.zero);

            foreach (var col in _geometryColliders)
            {
                if (col == null) continue;
                if (!hasBounds)
                {
                    bounds = col.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(col.bounds);
                }
            }

            //形状が無ければ位置の周りの小さな範囲にする
            if (!hasBounds)
            {
                bounds.size = Vector3.one * 0.5f;
            }

            return bounds;
        }

        /// <summary>
        /// 現在の状態をレベルデータとして書き出す
        /// </summary>
        public LevelObjectData Export()
        {
            var data = new LevelObjectData
            {
                type = TypeKey,
                id = InstanceId,
                position = transform.position,
            };

            foreach (var levelObject in GetComponentsInChildren<ILevelObject>(true))
            {
                levelObject.ExportLevelParameters(data.parameters);
            }

            return data;
        }
    }
}
