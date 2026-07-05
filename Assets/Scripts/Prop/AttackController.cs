using System;
using UnityEngine;
using System.Linq;
using Exceptions;

namespace Prop
{
    public class AttackController : MonoBehaviour
    {
        [SerializeField] private float relaxSpeed;
        [SerializeField] private AttackCollisionSetting collisionSetting;
        
        private Collider2D _collider2D_Cache;

        private void Start()
        {
            if (!TryGetComponent(out PropPhysicsMover physicsMover))
            {
                throw new MissingComponentException($"[{GetType().Name}] PropPhysicsMover が {gameObject.name} に見つかりません");
            }

            var attackCollider = GetComponentsInChildren<Transform>()
                .FirstOrDefault(obj => obj.gameObject.CompareTag("Attack Channel"))
                ?? throw new MissingChannelException("Attack Channel", gameObject.name);


            if (!attackCollider.TryGetComponent(out PropAttackCollisionController attackCollisionController))
            {
                throw new MissingComponentException($"[{GetType().Name}] PropAttackCollisionController が {attackCollider.gameObject.name} に見つかりません");
            }
            
            physicsMover.SetRelaxSpeed(relaxSpeed);
            attackCollisionController.Initialize(collisionSetting);

            physicsMover.OnForce += (GameObject instigator) =>
            {
                attackCollisionController.Activate(instigator);
            };

            physicsMover.OnRelax += () =>
            {
                attackCollisionController.Deactivate();
            };

        }
    }

}
