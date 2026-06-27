using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
public class UIController : MonoBehaviour
{
    [SerializeField] private Button helpButton;
    [SerializeField] private Button helpPanel;

    void Start()
    {
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
    }
    
}
