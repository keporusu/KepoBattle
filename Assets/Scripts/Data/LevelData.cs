using System;
using System.Collections.Generic;
using UnityEngine;

namespace Data
{
    /// <summary>
    /// レベル1つ分のデータ
    /// JsonUtility で StreamingAssets/Levels 以下の JSON と相互変換する
    /// JSON に無い項目は既定値で読まれるため、項目を足しても既存のレベルファイルはそのまま読める
    /// </summary>
    [Serializable]
    public class LevelData
    {
        //形式を変えた時に上げる
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;
        public LevelSettings settings = new LevelSettings();
        public List<LevelObjectData> objects = new List<LevelObjectData>();
    }

    /// <summary>
    /// レベル全体の設定
    /// </summary>
    [Serializable]
    public class LevelSettings
    {
        //レベルが無い場合にも使う既定値
        public const float DefaultKillHeight = -10.0f;

        //カメラの移動範囲(カメラが追う位置をこの範囲に収める)
        public Vector2 cameraMin = new Vector2(-1e5f, -1e5f);
        public Vector2 cameraMax = new Vector2(1e5f, 1e5f);

        //これより下に落ちたら、プレイヤーはレベルをやり直し、それ以外は消える
        public float killHeight = DefaultKillHeight;
    }

    /// <summary>
    /// 配置オブジェクト1つ分のデータ
    /// Transform は位置のみ持つ(回転が要るものは固有パラメータで持つ)
    /// </summary>
    [Serializable]
    public class LevelObjectData
    {
        //種別キー。LevelObjectRegistry で Prefab と対応付ける
        public string type;

        //インスタンスごとの永続ID(GUID)
        public string id;

        public Vector2 position;

        public LevelObjectParameters parameters = new LevelObjectParameters();
    }
}
