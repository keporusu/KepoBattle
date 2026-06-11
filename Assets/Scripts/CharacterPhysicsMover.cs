using UnityEngine;

public class CharacterPhysicsMover : PhysicsMover
{
    public void StartJump(float power)
    {
        ForcePower(new Vector2(0.0f,power),false);
    }

    public void StopJump()
    {
        forcePower.y = 0.0f;
    }

    public void Move(float power)
    {
        isBraking = false;
        movingPower = power;
    }

    public void StopMove()
    {
        isBraking = true;
    }
}
