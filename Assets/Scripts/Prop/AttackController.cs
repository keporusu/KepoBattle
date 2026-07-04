using System;
using UnityEngine;
using System.Linq;

namespace Prop
{
    public class AttackController : MonoBehaviour
    {
        [SerializeField] private float relaxSpeed;
        private PropPhysicsMover physicsMover_Cache;
        private Collider2D collider2D_Cache;

        private void Start()
        {
            if (!TryGetComponent(out physicsMover_Cache))
            {
                throw new MissingComponentException($"[{GetType().Name}] PropPhysicsMover が {gameObject.name} に見つかりません");
            }

            var attackCollider = GetComponentsInChildren<Transform>()
                .FirstOrDefault(obj => obj.gameObject.CompareTag("Attack Channel"))
                ?? throw new MissingChannelException("Attack Channel", gameObject.name);
            
            physicsMover_Cache.SetRelaxSpeed(relaxSpeed);
            attackCollider.gameObject.SetActive(false);

            physicsMover_Cache.OnForce += () =>
            {
                attackCollider.gameObject.SetActive(true);
            };

            physicsMover_Cache.OnRelax += () =>
            {
                attackCollider.gameObject.SetActive(false);
            };

        }
    }

}
