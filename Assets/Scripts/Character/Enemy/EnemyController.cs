using System;
using UnityEngine;
using Character;

namespace Character.Enemy
{

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
            if (_physicsMover_Cache == null)
                throw new MissingComponentException($"[{GetType().Name}] PhysicsMover が {gameObject.name} に見つかりません");

            _attackExecutor_Cache = GetComponent<AttackExecutor>();
            if (_attackExecutor_Cache == null)
                throw new MissingComponentException($"[{GetType().Name}] AttackExecutor が {gameObject.name} に見つかりません");

            _animator_Cache = animSprite.GetComponent<Animator>();
            if (_animator_Cache == null)
                throw new MissingComponentException($"[{GetType().Name}] Animator が animSprite ({animSprite.name}) に見つかりません");

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
