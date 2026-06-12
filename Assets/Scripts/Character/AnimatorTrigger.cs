using UnityEngine;

namespace Character
{
    public class AnimatorTrigger : MonoBehaviour
    {
        //AnimController遷移用プロパティ名
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int Jump = Animator.StringToHash("Jump");
        private static readonly int Ground = Animator.StringToHash("Ground");
        private static readonly int FallSpeed = Animator.StringToHash("FallSpeed");
        private static readonly int Attack1 = Animator.StringToHash("Attack1");
        private static readonly int Attack2 = Animator.StringToHash("Attack2");
        private static readonly int Attack3 = Animator.StringToHash("Attack3");
        private static readonly int Damage = Animator.StringToHash("Damage");
        private static readonly int IsDead = Animator.StringToHash("IsDead");

        [SerializeField] private Animator animator;

        public void SetSpeed(float value)
        {
            animator.SetFloat(Speed, value);
        }

        public void SetFallSpeed(float value)
        {
            animator.SetFloat(FallSpeed, value);
        }

        public void TriggerJump()
        {
            animator.SetTrigger(Jump);
        }

        public void TriggerGround()
        {
            animator.SetTrigger(Ground);
        }

        public void TriggerAttack1()
        {
            animator.SetTrigger(Attack1);
        }

        public void TriggerAttack2()
        {
            animator.SetTrigger(Attack2);
        }

        public void TriggerAttack3()
        {
            animator.SetTrigger(Attack3);
        }

        public void TriggerDamage()
        {
            animator.SetTrigger(Damage);
        }

        public void TriggerDeath()
        {
            animator.SetBool(IsDead, true);
        }

    }
}