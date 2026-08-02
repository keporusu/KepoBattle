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
            ForceVelocity.y = 0.0f;
        }

        public void Move(float power)
        {
            IsBraking = false;
            MovingVelocity = power;
        }

        public void StopMove()
        {
            IsBraking = true;
        }

        public void ForceKnockback(Vector2 velocity, GameObject instigator = null)
        {
            AddForceVelocity(velocity,true, instigator);
        }
    }
}