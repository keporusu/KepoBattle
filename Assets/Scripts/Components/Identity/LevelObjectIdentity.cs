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

        public void Initialize(string typeKey, string instanceId)
        {
            TypeKey = typeKey;
            InstanceId = instanceId;
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
