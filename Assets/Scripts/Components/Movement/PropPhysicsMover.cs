using Core.Contracts;
using UnityEngine;

namespace Components.Movement
{
    public class PropPhysicsMover : PhysicsMover, IKnockbackReceiver
    {
        //どのくらいの速度を「落ち着いた」と判定するか
        private float _relaxSpeed;
        
        //状態
        private bool _isForcing = false;
        
        //イベント
        public event System.Action<GameObject> OnForce;
        public event System.Action OnRelax;

        
        public void SetRelaxSpeed(float relaxSpeed)
        {
            _relaxSpeed = relaxSpeed;
        }
        
        public void ForceKnockback(Vector2 velocity, GameObject instigator = null)
        {
            AddForceVelocity(velocity,true, instigator);
            _isForcing = true;
            OnForce?.Invoke(instigator);
        }

        protected override void FixedUpdate()
        {
            base.FixedUpdate();
            if (Velocity.magnitude <= _relaxSpeed && _isForcing)
            {
                _isForcing = false;
                OnRelax?.Invoke();
            }
            
            //画面外に行き過ぎたら破壊
            if (Position.y < -10.0f)
            {
                Destroy(gameObject);
            }
        }
    }
}

