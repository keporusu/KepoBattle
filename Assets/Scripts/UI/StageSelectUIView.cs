using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace UI
{
    public class StageSelectUIView : MonoBehaviour
    {
        
        //TODO:Presenter側でViewの初期化したいので、AddComponentで追加したほうが良いか？
       
        private List<Button> _stageLists = new List<Button>();
        
        public event Action<int> OnSelectStage;


        private void OnEnable()
        {
            int stageIndex = 0;
            foreach (var stageElement in _stageLists)
            {
                var index = stageIndex;
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
