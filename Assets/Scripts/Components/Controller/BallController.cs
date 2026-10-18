using Core.Constants;
using Systems;
using Data;

namespace Components.Controller
{
    public class BallController : PropBehaviourController
    {
        
        //プレイヤーから攻撃を受けたときの音
        protected override void PlayDamageSoundFromCharacter()
        {
            //デフォルトがない
            SoundManager.Instance.PlaySe("BallDamageFromCharacter");
        }

        void Start()
        {
            //自身
            SetIdentity(ObjectIdentity.Ball);
        }
        
        
    }
}