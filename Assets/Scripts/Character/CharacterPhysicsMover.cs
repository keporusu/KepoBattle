using UnityEngine;

namespace Character
{
    public class CharacterPhysicsMover : PhysicsMover
    {
        public void StartJump(float power)
        {
            ForceVelocity(new Vector2(0.0f, power), false);
        }

        public void StopJump()
        {
            forceVelocity.y = 0.0f;
        }

        public void Move(float power)
        {
            isBraking = false;
            movingVelocity = power;
        }

        public void StopMove()
        {
            isBraking = true;
        }
    }
}