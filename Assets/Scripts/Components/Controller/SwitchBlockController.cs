using System;
using Core.Contracts;
using Data;
using Systems;
using UnityEngine;

namespace Components.Controller
{
    public class SwitchBlockController : MonoBehaviour, ILevelObject
    {
        //レベルデータのキー
        private const string SwitchIdKey = "switchId";

        [SerializeField] private int switchId;
        [SerializeField] private Collider2D geometry;
        [SerializeField] private SpriteRenderer renderer;

        //状態
        //スイッチから On が届くまでは Off で始める
        private bool _on = false;


        private void Start()
        {
            ApplyAppearance();
        }

        public void ApplyLevelParameters(LevelObjectParameters parameters)
        {
            if (parameters.TryGetInt(SwitchIdKey, out var id))
            {
                switchId = id;
                //生成後に編集された場合も見た目に反映する
                ApplyAppearance();
            }
        }

        public void ExportLevelParameters(LevelObjectParameters parameters)
        {
            parameters.SetInt(SwitchIdKey, switchId);
        }

        /// <summary>
        /// switchId の色と、On/Off の状態を見た目と当たり判定に反映する
        /// スイッチの Start と自身の Start のどちらが先でも同じ結果になるよう、状態は _on から決める
        /// </summary>
        private void ApplyAppearance()
        {
            var color = renderer.color;
            switch (switchId)
            {
                case 0:
                    color = Color.red;
                    break;
                case 1:
                    color = Color.green;
                    break;
                case 2:
                    color = Color.blue;
                    break;
                default:
                    break;
            }
            color.a = _on ? 1.0f : 0.2f;
            renderer.color = color;

            //編集中は選択判定に当たり判定を使うため、Off でも消さない
            geometry.enabled = _on || LevelManager.IsEditMode;
        }
        
        private void OnEnable()
        {
            if (StageManager.Current != null)
            {
                StageManager.Current.OnSwitchChanged += OnSwitchChanged;
            }
        }

        private void OnDisable()
        {
            //レベルの作り直しで破棄された後に、イベントが届かないようにする
            if (StageManager.Current != null)
            {
                StageManager.Current.OnSwitchChanged -= OnSwitchChanged;
            }
        }

        private void OnSwitchChanged(SwitchEvent switchEvent)
        {
            if (switchEvent.Id != switchId) return;

            _on = switchEvent.On;
            ApplyAppearance();
        }
    }

}

