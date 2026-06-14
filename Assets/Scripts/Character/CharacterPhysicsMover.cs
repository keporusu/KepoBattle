using UnityEngine;

namespace Character
{
    public class CharacterPhysicsMover : PhysicsMover
    {
        public void StartJump(float power)
        {
            AddForceVelocity(new Vector2(0.0f, power), false);
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
    }
}