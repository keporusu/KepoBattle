using UnityEngine;

namespace Systems
{
    /// <summary>
    /// ステージ(インゲーム)の管理
    /// ステージのシーンにだけ置き、そのシーンの間だけ Current から取得できる
    /// ステージ内で使う GameUtility などはここで生成し、ここを通して取得する
    ///
    /// シーンをまたいで残さない(DontDestroyOnLoad にしない)ので、ステージ外のシーンでは Current は null になる
    /// 他のコンポーネントの Awake より先に Current と GameUtility を用意するため、実行順を早めている
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class StageManager : MonoBehaviour
    {
        /// <summary>
        /// 今のステージ。ステージのシーン以外では null
        /// </summary>
        public static StageManager Current { get; private set; }

        [SerializeField] private GameObject _gameUtility_Prefab;
        [SerializeField] private GameObject _stageUIController_Prefab;

        //公開
        public GameUtility GameUtility { get; private set; }
        public StageUIController StageUIController { get; private set; }


        void Awake()
        {
            if (Current != null && Current != this)
            {
                Debug.LogWarning("StageManagerの重複を破棄します");
                Destroy(gameObject);
                return;
            }

            Current = this;

            //子にして、ステージと一緒に破棄されるようにする
            var gameUtility = Instantiate(_gameUtility_Prefab, transform);
            GameUtility = gameUtility.GetComponent<GameUtility>();

            var stageUIController = Instantiate(_stageUIController_Prefab, transform);
            StageUIController = stageUIController.GetComponent<StageUIController>();
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        /// <summary>
        /// ドメインのリロードを無効にして再生した時に、前回の Current が残らないようにする
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
        }
    }

}
