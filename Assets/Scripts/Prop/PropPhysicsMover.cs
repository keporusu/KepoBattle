using UnityEngine;

namespace Prop
{
    public class PropPhysicsMover : PhysicsMover
    {
        //どのくらいの速度を「落ち着いた」と判定するか
        private float _relaxSpeed;
        
        //状態
        private bool _isForcing = false;
        
        //イベント
        public event System.Action OnForce;
        public event System.Action OnRelax;

        
        public void SetRelaxSpeed(float relaxSpeed)
        {
            _relaxSpeed = relaxSpeed;
        }
        
        public override void AddForceVelocity(Vector2 velocity, bool forceMode)
        {
            base.AddForceVelocity(velocity, forceMode);

            _isForcing = true;
            OnForce?.Invoke();
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (Velocity.magnitude <= _relaxSpeed && _isForcing)
            {
                _isForcing = false;
                OnRelax?.Invoke();
            }
        }
    }
}

