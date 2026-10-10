using Data;

namespace Core.Contracts
{
    /// <summary>
    /// レベルデータとオブジェクト固有のパラメータを受け渡す口
    /// Transform 以外に保存したい値を持つコンポーネントが実装する
    /// </summary>
    public interface ILevelObject
    {
        /// <summary>
        /// レベルデータの値を自身に反映する
        /// Instantiate 直後(Start より前)にも、生成後の編集中にも呼ばれうる
        /// 含まれていないキーは変更しない
        /// </summary>
        /// <param name="parameters">反映するパラメータ</param>
        void ApplyLevelParameters(LevelObjectParameters parameters);

        /// <summary>
        /// 自身の現在の値をレベルデータへ書き出す
        /// </summary>
        /// <param name="parameters">書き出し先</param>
        void ExportLevelParameters(LevelObjectParameters parameters);
    }
}
