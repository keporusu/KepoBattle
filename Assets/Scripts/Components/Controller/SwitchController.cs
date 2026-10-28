using Core.Constants;
using Core.Contracts;
using Systems;
using UnityEngine;
using Data;

namespace Components.Controller
{
    public class SwitchController : PropBehaviourController, ILevelObject
    {
        //レベルデータのキー
        private const string OnInitializeKey = "onInitialize";
        private const string SwitchIdKey = "switchId";

        [SerializeField] private bool onInitialize;
        [SerializeField] private int switchId; //0~2
        
        //アニメーション関連
        [SerializeField] private SpriteRenderer renderer;
        [SerializeField] private Sprite switchOn;
        [SerializeField] private Sprite switchOff;


        //状態
        private bool _on = false;
        private int _switchEventId = 0;

        void Start()
        {
            //自身
            SetIdentity(ObjectIdentity.Switch);

            //初期化
            _on = onInitialize;
            ApplyAppearance();

            //On で始まる場合は、同じ switchId のブロックも切り替える
            //ブロックは OnEnable で購読済み(同じフレームに生成されたものも、Start より前に OnEnable が呼ばれる)
            //編集中に切り替えるとブロックの当たり判定が消えて選びにくくなるため、編集中は送らない
            if (_on && !LevelManager.IsEditMode && StageManager.Current != null)
            {
                StageManager.Current.Switch(new SwitchEvent(){Id = switchId, On = _on});
            }
        }

        public void ApplyLevelParameters(LevelObjectParameters parameters)
        {
            if (parameters.TryGetBool(OnInitializeKey, out var on))
            {
                onInitialize = on;
                _on = on;
            }

            if (parameters.TryGetInt(SwitchIdKey, out var id))
            {
                switchId = id;
            }

            //生成後に編集された場合も見た目に反映する
            ApplyAppearance();
        }

        public void ExportLevelParameters(LevelObjectParameters parameters)
        {
            parameters.SetBool(OnInitializeKey, onInitialize);
            parameters.SetInt(SwitchIdKey, switchId);
        }

        /// <summary>
        /// switchId の色と、On/Off のスプライトを見た目に反映する
        /// </summary>
        private void ApplyAppearance()
        {
            switch (switchId)
            {
                case 0:
                    renderer.color = Color.red;
                    break;
                case 1:
                    renderer.color = Color.green;
                    break;
                case 2:
                    renderer.color = Color.blue;
                    break;
                default:
                    break;
            }

            renderer.sprite = _on ? switchOn : switchOff;
        }

        protected override void OnDamageHit(Collider2D other)
        {
            Switch();
        }


        private void Switch()
        {
            //StageManager側からイベントを拾ってもらう
            if (StageManager.Current != null)
            {
                _on = !_on;
                renderer.sprite = _on ? switchOn : switchOff;
                StageManager.Current.Switch(new SwitchEvent(){Id = switchId, On = _on});
            }
        }
        

    }

}
