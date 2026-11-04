using Core.Constants;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace UI
{
    public class StageUIView : MonoBehaviour
    {
        [SerializeField] private GameObject clearPopup;
        [SerializeField] private TextMeshProUGUI clearTimeText;
        [SerializeField] private TextMeshProUGUI clearScoreText;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private TextMeshProUGUI timeText;
        [SerializeField] private Image deliveryTargetImage;
        [SerializeField] private ObjectIconTable iconTable;
        
        
        //公開
        //TODO:少し危険な公開の仕方
        public Button.ButtonClickedEvent NextButtonEvent => nextButton.onClick;
        public Button.ButtonClickedEvent RetryButtonEvent => retryButton.onClick;
        public Button.ButtonClickedEvent BackButtonEvent => backButton.onClick;
        public Button.ButtonClickedEvent QuitButtonEvent => quitButton.onClick;
        public Button.ButtonClickedEvent RestartButtonEvent => restartButton.onClick;

        
        void Start()
        {
            clearPopup.SetActive(false);
        }
        

        /// <summary>
        /// クリア画面を表示する
        /// </summary>
        /// <param name="clearTime">クリアまでにかかった時間(秒)</param>
        /// <param name="score">表示するスコア。null ならスコアを表示しない</param>
        public void ShowClearPopup(float clearTime, int? score)
        {
            //スコアを表示するときは、クリア時間を表示しない
            if (clearTimeText != null)
            {
                clearTimeText.gameObject.SetActive(!score.HasValue);
                clearTimeText.SetText($"Time {FormatTime(clearTime)}");
            }

            if (clearScoreText != null)
            {
                clearScoreText.gameObject.SetActive(score.HasValue);
                if (score.HasValue)
                {
                    clearScoreText.SetText($"score {score.Value}");
                }
            }

            clearPopup.SetActive(true);
            clearPopup.transform.localScale = Vector3.zero;
            clearPopup.transform.DOScale(1.0f, 0.1f);
        }
        public void HideClearPopup()
        {
            clearPopup.transform.localScale = Vector3.one;
            clearPopup.transform.DOScale(0.0f, 0.1f);
            clearPopup.SetActive(false);
        }

        /// <summary>
        /// 画面上部の時間を「分:秒:1/100秒」(mm:ss:ff)の形式で表示する
        /// </summary>
        /// <param name="seconds">表示する時間(秒)。経過時間または残り時間</param>
        public void SetTime(float seconds)
        {
            if (timeText == null) return;
            timeText.SetText(FormatTime(seconds));
        }

        /// <summary>
        /// 秒数を「分:秒:1/100秒」(mm:ss:ff)の文字列にする
        /// </summary>
        /// <param name="seconds">時間(秒)</param>
        private static string FormatTime(float seconds)
        {
            //1/100秒未満は切り捨て、表示が実際の時間より進まないようにする
            var totalCentiseconds = Mathf.FloorToInt(seconds * 100.0f);
            var minutes = totalCentiseconds / 6000;
            var secs = totalCentiseconds / 100 % 60;
            var centiseconds = totalCentiseconds % 100;
            return $"{minutes:00}:{secs:00}:{centiseconds:00}";
        }

        public void SetDeliveryTarget(ObjectIdentity identity)
        {
            if (iconTable == null) return;
            if (!iconTable.TryGetSprite(identity, out var sprite)) return;
            deliveryTargetImage.sprite = sprite;
        }
    
        void Activate()
        {
            gameObject.SetActive(true);
        }

        void Deactivate()
        {
            gameObject.SetActive(false);
        }
    
    }

}
