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
        private const string OnInitializeKey = "onInitialize";

        [SerializeField] private int switchId;
        [SerializeField] private bool onInitialize;
        [SerializeField] private Collider2D geometry;
        [SerializeField] private SpriteRenderer renderer;

        //状態
        private bool _on = false;


        private void Start()
        {
            _on = onInitialize;
            ApplyAppearance();
        }

        public void ApplyLevelParameters(LevelObjectParameters parameters)
        {
            if (parameters.TryGetInt(SwitchIdKey, out var id))
            {
                switchId = id;
            }

            if (parameters.TryGetBool(OnInitializeKey, out var on))
            {
                onInitialize = on;
                _on = on;
            }

            //生成後に編集された場合も見た目に反映する
            ApplyAppearance();
        }

        public void ExportLevelParameters(LevelObjectParameters parameters)
        {
            parameters.SetInt(SwitchIdKey, switchId);
            parameters.SetBool(OnInitializeKey, onInitialize);
        }

        /// <summary>
        /// switchId の色と、On/Off の状態を見た目と当たり判定に反映する
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

            //スイッチの On/Off に揃えるのではなく、ブロックごとに今の状態を反転する
            _on = !_on;
            ApplyAppearance();
        }
    }

}

