using System;
using UnityEngine;
using Character;

namespace Character.Enemy
{

    [RequireComponent(typeof(PhysicsMover))]
    [RequireComponent(typeof(AttackExecutor))]
    public class EnemyController : MonoBehaviour
    {

        private static readonly int Ground = Animator.StringToHash("Ground");

        //AnimatorでSpriteを動かすGameObjectを期待する
        [SerializeField] private GameObject animSprite;

        //キャッシュ
        private PhysicsMover physicsMover_Cache;
        private AttackExecutor attackExecutor_Cache;
        private Animator animator_Cache;


        private void Start()
        {
            physicsMover_Cache = GetComponent<PhysicsMover>();
            attackExecutor_Cache = GetComponent<AttackExecutor>();
            animator_Cache = animSprite.GetComponent<Animator>();

            if (animator_Cache == null)
            {
                Debug.LogError("Animator component not found on animSprite", this);
                enabled = false;
                return;
            }

            physicsMover_Cache.OnGround += OnGround;
        }

        private void OnGround()
        {
            //接地状態遷移（AnimController）
            animator_Cache.SetTrigger(Ground);
            Debug.Log("Grounded");
        }
    }
}
