using UnityEngine;
using Core.Constants;

namespace Battle.Character
{
    public class AnimatorTrigger : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        public void SetSpeed(float value)
        {
            animator.SetFloat(AnimatorParams.Speed, value);
        }

        public void SetFallSpeed(float value)
        {
            animator.SetFloat(AnimatorParams.FallSpeed, value);
        }

        public void TriggerJump()
        {
            animator.SetTrigger(AnimatorParams.Jump);
        }

        public void TriggerGround()
        {
            animator.SetTrigger(AnimatorParams.Ground);
        }

        public void TriggerAir()
        {
            animator.SetTrigger(AnimatorParams.Jump);
        }

        public void TriggerAttack1()
        {
            animator.SetTrigger(AnimatorParams.Attack1);
        }

        public void TriggerAttack2()
        {
            animator.SetTrigger(AnimatorParams.Attack2);
        }

        public void TriggerAttack3()
        {
            animator.SetTrigger(AnimatorParams.Attack3);
        }

        public void TriggerDamage()
        {
            animator.SetTrigger(AnimatorParams.Damage);
        }

        public void TriggerDeath()
        {
            animator.SetBool(AnimatorParams.IsDead, true);
        }

    }
}