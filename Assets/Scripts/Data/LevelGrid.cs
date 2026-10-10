using UnityEngine;

namespace Data
{
    /// <summary>
    /// レベル作成で使うグリッド
    /// 原点 (0, 0) の1枚のグリッドを共有し、揃える点の細かさだけを対象ごとに変える
    /// 地形ブロックはワールド上で 1マス(CellSize)の大きさになる Scale で作ること
    /// </summary>
    public static class LevelGrid
    {
        //1マスの大きさ(ワールド座標の単位)
        public const float CellSize = 1.0f;

        //オブジェクトを揃える細かさ。1マスの半分にして、ブロックの中心と辺の両方に揃えられるようにする
        public const float ObjectSnapSize = CellSize * 0.5f;

        /// <summary>
        /// オブジェクトの位置を揃える(0.5マス刻み)
        /// </summary>
        public static Vector2 SnapObject(Vector2 position)
        {
            return new Vector2(Round(position.x, ObjectSnapSize), Round(position.y, ObjectSnapSize));
        }

        /// <summary>
        /// 地形ブロックの位置を揃える
        /// ブロックの辺が整数の線に乗るよう、中心をマスの中心(n + 0.5)に揃える
        /// </summary>
        public static Vector2 SnapTerrain(Vector2 position)
        {
            return new Vector2(CellCenter(position.x), CellCenter(position.y));
        }

        private static float Round(float value, float step)
        {
            return Mathf.Round(value / step) * step;
        }

        private static float CellCenter(float value)
        {
            return (Mathf.Floor(value / CellSize) + 0.5f) * CellSize;
        }
    }
}
