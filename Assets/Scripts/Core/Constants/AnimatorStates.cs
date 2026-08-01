namespace Core.Constants
{
    /// <summary>
    /// AnimatorController のステート名。
    /// ステートに貼った StateProgressionNotifier の StateName(Inspector 設定)と一致させること。
    /// </summary>
    public enum AnimatorStates
    {
        Attack1,
        Attack2,
        Attack3
    }
    // public static class AnimatorStates
    // {
    //     public const string Attack1 = "Attack1";
    //     public const string Attack2 = "Attack2";
    //
    //     //AttackType.SpecialAttack に対応する。トリガー名は Attack3 なので注意
    //     public const string Attack3 = "Attack3";
    // }
}
