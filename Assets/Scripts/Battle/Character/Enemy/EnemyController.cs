using System;
using UnityEngine;
using Battle.Character;
using Core.Constants;

namespace Battle.Character.Enemy
{

    public class EnemyController : MonoBehaviour
    {

        //AnimatorでSpriteを動かすGameObjectを期待する
        [SerializeField] private GameObject animSprite;

        //キャッシュ
        private PhysicsMover _physicsMover_Cache;
        private AttackExecutor _attackExecutor_Cache;
        private Animator _animator_Cache;


        private void Start()
        {
            if (!TryGetComponent(out _physicsMover_Cache))
                throw new MissingComponentException($"[{GetType().Name}] PhysicsMover が {gameObject.name} に見つかりません");

            if (!TryGetComponent(out _attackExecutor_Cache))
                throw new MissingComponentException($"[{GetType().Name}] AttackExecutor が {gameObject.name} に見つかりません");

            if (!animSprite.TryGetComponent(out _animator_Cache))
                throw new MissingComponentException($"[{GetType().Name}] Animator が animSprite ({animSprite.name}) に見つかりません");

            _physicsMover_Cache.OnGround += OnGround;
        }

        private void OnGround()
        {
            //接地状態遷移（AnimController）
            _animator_Cache.SetTrigger(AnimatorParams.Ground);
            Debug.Log("Grounded");
        }
    }
}
