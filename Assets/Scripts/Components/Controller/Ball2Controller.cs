using Components.Controller;
using Core.Constants;
using Systems;
using UnityEngine;

namespace Components.Controller
{
    public class Ball2Controller : PropBehaviourController
    {
        //プレイヤーから攻撃を受けたときの音
        protected override void PlayBounceSound()
        {
            SoundManager.Instance.PlaySe("Ball2Bounce");
        }

        void Start()
        {
            //自身
            SetIdentity(ObjectIdentity.Ball2);
        }
    }

}
