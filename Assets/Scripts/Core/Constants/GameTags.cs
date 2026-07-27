namespace Core.Constants
{
    /// <summary>
    /// チャンネル(タグ付き子オブジェクト)のタグ名。
    /// ProjectSettings/TagManager.asset の Tags と一致させること。
    /// </summary>
    public static class GameTags
    {
        //押し合い・接地判定の形状を持つチャンネル
        public const string GeometryChannel = "Geometry Channel";

        //攻撃の当たり判定を出すチャンネル
        public const string AttackChannel = "Attack Channel";

        //ダメージを受け取るチャンネル
        public const string DamageChannel = "Damage Channel";
    }
}
