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
        Enum,
    }

    //レベルデータに保存する、オブジェクト固有のパラメータ1つ分
    [Serializable]
    public struct LevelParameter
    {
        public string key;
        public LevelParameterType type;
        public string value;
        //Enum のときだけ使う。編集GUIで選択肢を出すための Enum の型名
        public string enumType;
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

        /// <summary>
        /// Enum の値を設定する
        /// 並び順を変えても壊れないよう、値は数値ではなく名前で持つ
        /// </summary>
        public void SetEnum<T>(string key, T value) where T : struct, Enum
        {
            SetEnum(key, typeof(T), value.ToString());
        }

        /// <summary>
        /// Enum の値を名前で設定する
        /// 型を静的に知らない編集GUIから使う
        /// </summary>
        public void SetEnum(string key, Type enumType, string name)
        {
            Set(key, LevelParameterType.Enum, name, ToTypeName(enumType));
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

        public bool TryGetEnum<T>(string key, out T value) where T : struct, Enum
        {
            value = default;
            return TryGet(key, out var text)
                   && Enum.TryParse(text, false, out value)
                   && Enum.IsDefined(typeof(T), value);
        }

        /// <summary>
        /// パラメータに記録された Enum の型を求める
        /// </summary>
        /// <returns>Enum でない、または型が見つからなければ null</returns>
        public static Type ResolveEnumType(LevelParameter parameter)
        {
            if (parameter.type != LevelParameterType.Enum) return null;
            if (string.IsNullOrEmpty(parameter.enumType)) return null;

            var type = Type.GetType(parameter.enumType);
            return type != null && type.IsEnum ? type : null;
        }

        //Type.GetType で引けるよう、アセンブリ名を付ける(バージョンなどは付けない)
        private static string ToTypeName(Type type)
        {
            return $"{type.FullName}, {type.Assembly.GetName().Name}";
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

        private void Set(string key, LevelParameterType type, string value, string enumType = null)
        {
            var parameter = new LevelParameter { key = key, type = type, value = value, enumType = enumType };

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
