using UnityEngine;

namespace Core.Constants
{
    /// <summary>
    /// AnimatorController のパラメータのハッシュ。
    /// AnimatorController 側のパラメータ名と一致させること。
    /// 文字列をここだけに閉じ込めるため、外部にはハッシュのみを公開する。
    /// </summary>
    public static class AnimatorParams
    {
        //Float
        public static readonly int Speed = Animator.StringToHash("Speed");
        public static readonly int FallSpeed = Animator.StringToHash("FallSpeed");

        //Trigger
        public static readonly int Jump = Animator.StringToHash("Jump");
        public static readonly int Ground = Animator.StringToHash("Ground");
        public static readonly int Attack1 = Animator.StringToHash("Attack1");
        public static readonly int Attack2 = Animator.StringToHash("Attack2");
        public static readonly int Attack3 = Animator.StringToHash("Attack3");
        public static readonly int Damage = Animator.StringToHash("Damage");

        //Bool
        public static readonly int IsDead = Animator.StringToHash("IsDead");
    }
}
