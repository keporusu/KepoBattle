using System;
using System.Collections.Generic;
using UnityEngine;

namespace Data
{
    //レベル作成システムでの分類
    //編集モード(オブジェクトモード / 地形モード)の候補リストを分けるために使う
    public enum LevelObjectCategory
    {
        Object,
        Terrain,
    }

    /// <summary>
    /// レベルデータの種別キーと Prefab の対応表
    /// キーは Prefab 名とは独立させ、Prefab の名前を変えてもレベルデータが壊れないようにする
    /// </summary>
    [CreateAssetMenu(menuName="ScriptableObjects/LevelObjectRegistry")]
    public class LevelObjectRegistry : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public string key;
            public LevelObjectCategory category;
            public GameObject prefab;
        }

        [SerializeField] private List<Entry> entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public bool TryGetPrefab(string key, out GameObject prefab)
        {
            prefab = TryGetEntry(key, out var entry) ? entry.prefab : null;
            return prefab != null;
        }

        public bool TryGetEntry(string key, out Entry entry)
        {
            foreach (var candidate in entries)
            {
                if (candidate.key != key) continue;
                entry = candidate;
                return true;
            }

            entry = default;
            return false;
        }
    }
}
