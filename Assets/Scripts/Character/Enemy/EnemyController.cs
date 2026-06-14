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
        private PhysicsMover _physicsMover_Cache;
        private AttackExecutor _attackExecutor_Cache;
        private Animator _animator_Cache;


        private void Start()
        {
            _physicsMover_Cache = GetComponent<PhysicsMover>();
            _attackExecutor_Cache = GetComponent<AttackExecutor>();
            _animator_Cache = animSprite.GetComponent<Animator>();

            if (_animator_Cache == null)
            {
                Debug.LogError("Animator component not found on animSprite", this);
                enabled = false;
                return;
            }

            _physicsMover_Cache.OnGround += OnGround;
        }

        private void OnGround()
        {
            //接地状態遷移（AnimController）
            _animator_Cache.SetTrigger(Ground);
            Debug.Log("Grounded");
        }
    }
}
