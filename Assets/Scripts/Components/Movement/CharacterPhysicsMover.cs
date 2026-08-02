using Core.Contracts;
using UnityEngine;

namespace Components.Movement
{
    public class CharacterPhysicsMover : PhysicsMover, IKnockbackReceiver
    {
        public void StartJump(float power)
        {
            AddForceVelocity(new Vector2(0.0f, power), false, gameObject);
        }

        public void StopJump()
        {
            CutVelocity(false, true);
        }

        public void Move(float velocity)
        {
            InputMove(velocity);
        }

        public void StopMove()
        {
            CutMove();
        }

        public void ForceKnockback(Vector2 velocity, GameObject instigator = null)
        {
            AddForceVelocity(velocity,true, instigator);
        }
    }
}