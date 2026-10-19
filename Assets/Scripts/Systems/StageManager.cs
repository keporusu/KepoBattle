using System;
using UI;
using UnityEngine;

namespace Systems
{
    public enum StageState
    {
        Playing,
        Clear,
    }
    
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
        
        //状態
        StageState _state = StageState.Playing;

        //公開
        public GameUtility GameUtility { get; private set; }
        public StageUIView StageUIView { get; private set; }


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
            StageUIView = stageUIController.GetComponent<StageUIView>();
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        private void OnEnable()
        {
            //重複して破棄される StageManager は、Awake で StageUIController を生成していない
            if (StageUIView == null) return;
            StageUIView.RetryButtonEvent.AddListener(Restart);
        }

        private void OnDisable()
        {
            if (StageUIView == null) return;
            StageUIView.RetryButtonEvent.RemoveListener(Restart);
        }

        /// <summary>
        /// ドメインのリロードを無効にして再生した時に、前回の Current が残らないようにする
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
        }



        public void GameClear()
        {
            //クリア実行済みなら何もしない
            if(_state == StageState.Clear)return;
            _state = StageState.Clear;
            
            StageUIView.Show();
            GameUtility.SetCameraShake(new Vector2(2.0f,2.0f),0.5f);
            SoundManager.Instance.PlaySe("Clear1");
            SoundManager.Instance.PlaySe("Clear2");
            SoundManager.Instance.PlaySe("Clear3");
        }

        public void Restart()
        {
            _state = StageState.Playing;
            StageUIView.Hide();
            GameUtility.RestartLevel();
        }
        
    }

}
