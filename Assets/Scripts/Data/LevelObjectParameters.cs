using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Data
{
    //パラメータの型
    //編集GUIで入力欄の種類を決めるために使う
    public enum LevelParameterType
    {
        Float,
        Bool,
        Int,
    }

    //レベルデータに保存する、オブジェクト固有のパラメータ1つ分
    [Serializable]
    public struct LevelParameter
    {
        public string key;
        public LevelParameterType type;
        public string value;
    }

    /// <summary>
    /// オブジェクト固有のパラメータの集まり
    /// JsonUtility は Dictionary を扱えないため、キーと値のリストで持つ
    /// 値は文字列で持ち、ロケールに依存しないよう InvariantCulture で変換する
    /// </summary>
    [Serializable]
    public class LevelObjectParameters
    {
        [SerializeField] private List<LevelParameter> entries = new List<LevelParameter>();

        public IReadOnlyList<LevelParameter> Entries => entries;

        public void SetFloat(string key, float value)
        {
            Set(key, LevelParameterType.Float, value.ToString("R", CultureInfo.InvariantCulture));
        }

        public void SetBool(string key, bool value)
        {
            Set(key, LevelParameterType.Bool, value ? "true" : "false");
        }

        public void SetInt(string key, int value)
        {
            Set(key, LevelParameterType.Int, value.ToString(CultureInfo.InvariantCulture));
        }

        public bool TryGetFloat(string key, out float value)
        {
            value = 0.0f;
            return TryGet(key, out var text)
                   && float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        public bool TryGetBool(string key, out bool value)
        {
            value = false;
            return TryGet(key, out var text) && bool.TryParse(text, out value);
        }

        public bool TryGetInt(string key, out int value)
        {
            value = 0;
            return TryGet(key, out var text)
                   && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// 自身に無いキーだけを other から補う
        /// レベルデータに書かれていないパラメータを、Prefab の既定値で埋めるために使う
        /// </summary>
        public void MergeMissing(LevelObjectParameters other)
        {
            foreach (var entry in other.entries)
            {
                if (TryGet(entry.key, out _)) continue;
                entries.Add(entry);
            }
        }

        private void Set(string key, LevelParameterType type, string value)
        {
            var parameter = new LevelParameter { key = key, type = type, value = value };

            //同じキーがあれば上書きする
            for (var i = 0; i < entries.Count; i++)
            {
                if (entries[i].key != key) continue;
                entries[i] = parameter;
                return;
            }

            entries.Add(parameter);
        }

        private bool TryGet(string key, out string value)
        {
            foreach (var entry in entries)
            {
                if (entry.key != key) continue;
                value = entry.value;
                return true;
            }

            value = null;
            return false;
        }
    }
}
