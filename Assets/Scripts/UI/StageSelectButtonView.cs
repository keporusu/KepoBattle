using TMPro;
using UnityEngine;
using UnityEngine.UI;


namespace UI
{
    
    public class StageSelectButtonView : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI text;

        public void SetStageId(string stageId)
        {
            text.text = "Stage " + stageId;
        }

        public void SetColor(Color color)
        {
            if (TryGetComponent(out Image image))
            {
                image.color = color;
            }
        }
    
    }
    
}


