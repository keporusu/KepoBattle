using Core.Constants;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;


namespace UI
{
    public class StageUIView : MonoBehaviour
    {
        [SerializeField] private GameObject clearPopup;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button nextButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Image deliveryTargetImage;
        [SerializeField] private ObjectIconTable iconTable;
        
        
        //公開
        //TODO:少し危険な公開の仕方
        public Button.ButtonClickedEvent NextButtonEvent => nextButton.onClick;
        public Button.ButtonClickedEvent RetryButtonEvent => retryButton.onClick;
        public Button.ButtonClickedEvent BackButtonEvent => backButton.onClick;

        
        void Start()
        {
            clearPopup.SetActive(false);
        }
        

        public void ShowClearPopup()
        {
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
