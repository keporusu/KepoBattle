using Core.Constants;
using Systems;
using UnityEngine;
using Data;

namespace Components.Controller
{
    public class SwitchController : PropBehaviourController
    {

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

            if (onInitialize)
            {
                renderer.sprite = switchOn;
            }
            else
            {
                renderer.sprite = switchOff;
            }
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
