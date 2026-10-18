using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Systems;

namespace UI
{
    public class UIController : MonoBehaviour
    {
        [SerializeField] private Button helpButton;
        [SerializeField] private Button helpPanel;
        [SerializeField] private Button respawnButton;

        void Start()
        {
            //操作説明の「？」のボタン
            helpButton.onClick.AddListener(() =>
            {
                if (helpPanel.gameObject.activeSelf)
                {
                    helpPanel.gameObject.SetActive(false);
                }
                else
                {
                    helpPanel.gameObject.SetActive(true);
                }
            });
            helpPanel.onClick.AddListener(() =>
            {
                helpPanel.gameObject.SetActive(false);
            });

            //リスポーンするボタン(レベルをやり直す)
            respawnButton.onClick.AddListener(() =>
            {
                //GameUtility は StageManager が実行時に生成するので、インスペクタでは参照できない
                if (StageManager.Current != null)
                {
                    StageManager.Current.GameUtility.RestartLevel();
                }
            });
        }

    }
}
