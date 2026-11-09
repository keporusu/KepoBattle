using System;
using UnityEngine;

namespace Data
{
    /// <summary>
    /// バネの大ジャンプモードの設定
    /// バネで飛ぶ前後にジャンプボタンを押すと、上昇中の重力が弱くなる
    /// </summary>
    [Serializable]
    public struct BigJumpSetting
    {
        //バネで飛ぶ前後、何秒以内にジャンプボタンを押せばモードに入るか
        public float timingWindow;
        //バネで飛んでから、何秒でモードを強制的に解除するか
        public float duration;
        //モード中、上昇しているときに重力に掛ける倍率
        [Range(0.0f, 1.0f)] public float risingGravityScale;
    }
}
