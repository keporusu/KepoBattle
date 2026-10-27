using System;
using System.Collections;
using Systems;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace UI
{
    public class TitleController : MonoBehaviour
    {
        [SerializeField] private Button _startGameButton;
        [SerializeField] private Button _exitGameButton;
        [SerializeField] private float _exitDelay = 0.3f;

        private void OnEnable()
        {
            _startGameButton.onClick.AddListener(OnStartGame);
            _exitGameButton.onClick.AddListener(OnExitGame);
        }

        private void OnDisable()
        {
            _startGameButton.onClick.RemoveListener(OnStartGame);
            _exitGameButton.onClick.RemoveListener(OnExitGame);
        }

        private void OnStartGame()
        {
            SoundManager.Instance.PlaySe("Select2");
            SceneManager.LoadScene("StageSelect");
        }

        private void OnExitGame()
        {
            SoundManager.Instance.PlaySe("Select1");
            _startGameButton.interactable = false;
            _exitGameButton.interactable = false;
            StartCoroutine(QuitAfterDelay());
        }

        private IEnumerator QuitAfterDelay()
        {
            // SEが鳴り終わるまで待つ（timeScaleの影響を受けないようRealtime）
            yield return new WaitForSecondsRealtime(_exitDelay);
            QuitGame();
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}

