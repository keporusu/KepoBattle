using System;
using System.Collections.Generic;
using Core.Constants;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// ObjectIdentity と UI に表示する Sprite の対応表
    /// Data アセンブリからは Core の ObjectIdentity を参照できない(Core → Data の参照があり循環する)ため、UI 側に置く
    /// </summary>
    [CreateAssetMenu(menuName="ScriptableObjects/ObjectIconTable")]
    public class ObjectIconTable : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public ObjectIdentity identity;
            public Sprite sprite;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public bool TryGetSprite(ObjectIdentity identity, out Sprite sprite)
        {
            foreach (var entry in entries)
            {
                if (entry.identity != identity) continue;
                sprite = entry.sprite;
                return sprite != null;
            }

            sprite = null;
            return false;
        }
    }
}
