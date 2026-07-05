using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public class UIController : MonoBehaviour
{
    [SerializeField] private GameUtility gameUtility;
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
        
        //リスポーンするボタン
        respawnButton.onClick.AddListener(() =>
        {
            gameUtility.RespawnPlayer();
        });
    }
    
}
