using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Data;

namespace UI
{
    public class StageSelectUIView : MonoBehaviour
    {

        [SerializeField] private GameObject content;
        [SerializeField] private GameObject button_Prefab;
        
       
        private List<Button> _stageLists = new List<Button>();
        public event Action<int> OnSelectStage;
        

        public void Initialize(StagesInfo stagesInfo, UInt64 availableStageFlags, int generateButtonCount=32)
        {
            
            foreach (var button in content.GetComponentsInChildren<Button>())
            {
                Destroy(button.gameObject);
            }
            
            //ボタンを必要なだけ生成する
            
            for (int i = 0; i < generateButtonCount; i++)
            {
                var button = Instantiate(button_Prefab, content.transform);
                var buttonView = button.GetComponent<StageSelectButtonView>();
                _stageLists.Add(button.GetComponent<Button>());
                
                //登録されているステージにはIdを振る
                if (i < stagesInfo.stages.Count)
                {
                    buttonView.SetStageId(stagesInfo.stages[i].Id);
                }
                
                //まだ解放されていないステージは灰色に
                if (((ulong)(1 << i) & availableStageFlags) == 0) 
                {
                    buttonView.SetColor(Color.gray);
                }
                
                
            }
            
            
            int stageIndex = 0;
            foreach (var stageElement in _stageLists)
            {
                var index = stageIndex;
                
                //解放されていなステージなら何も登録しない
                if (((ulong)(1 << stageIndex) & availableStageFlags) == 0) return;
                
                stageElement.onClick.AddListener(() =>
                {
                    //UI監視側に押されたボタンのインデックスを教える
                    OnSelectStage?.Invoke(index);
                });
                stageIndex++;
            }
        }
        
        private void OnDisable()
        {
            foreach (var button in _stageLists)
                button.onClick.RemoveAllListeners();
        }


        //public void Subscribe(int stageIndex)

    }
}
