using DG.Tweening;
using UnityEditor.SceneManagement;
using UnityEngine;

public class StageUIController : MonoBehaviour
{
   // [SerializeField] private GameObject clearPopup;

    void Start()
    {
        Deactivate();
    }

    public void Show()
    {
        Activate();
        gameObject.transform.localScale = Vector3.zero;
        gameObject.transform.DOScale(1.0f, 0.2f);
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
