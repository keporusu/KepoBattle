namespace Core.Constants
{
    /// <summary>
    /// レイヤー名。
    /// ProjectSettings/TagManager.asset の Layers と一致させること。
    /// </summary>
    public static class GameLayers
    {
        //キャラクター(押し合いの対象)
        public const string Character = "Character";

        //ギミック等のオブジェクト(押し合いの対象)
        public const string Prop = "Prop";

        //足場
        public const string Ground = "Ground";
    }
}
