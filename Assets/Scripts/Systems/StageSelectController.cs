using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks.Triggers;
using UnityEngine;
using Data;
using UI;
using UnityEngine.SceneManagement;

namespace Systems
{
    public class StageSelectController : MonoBehaviour
    {
        [SerializeField] StagesInfo stagesInfo;

        private UInt64 availableStageFlags;
        private List<StageInfo> _stages = new List<StageInfo>();
        
        //状態
        private bool isLoadingStage = false;

        void Awake()
        {
            //テンプレートで初期化
            foreach (var x in stagesInfo.stages)
            {
                _stages.Add(x);
            }
            
            //解放ステージ初期化
            availableStageFlags = 0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0001;
        }
        
        void Start()
        {
            if (!TryGetComponent(out StageSelectUIView stageSelectUIView))
                return;

            stageSelectUIView.Initialize(stagesInfo,availableStageFlags);
            stageSelectUIView.OnSelectStage += SelectStage;

        }

        private void SelectStage(int index)
        {
            if (index < 0 || _stages.Count <= index) return;

            var sceneName = _stages[index].SceneName;
            //Build Settingsに登録されていないシーンは読み込まない
            if (string.IsNullOrEmpty(sceneName) || !Application.CanStreamedLevelBeLoaded(sceneName)) return;

            if(isLoadingStage)return;
            isLoadingStage = true;
            
            SoundManager.Instance.PlaySe("Select2");
            StartCoroutine(LoadSceneAfterDelay(sceneName,0.5f));
        }

        private void SelectUnavailableStage()
        {
            
        }


        private IEnumerator LoadSceneAfterDelay(string sceneName, float delay)
        {
            yield return new WaitForSeconds(delay);
            SceneManager.LoadScene(sceneName);
        }
        
    
    }
}

