using System;
using UnityEngine;

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
        
        if (physicsMover_Cache == null)
        {
            Debug.LogError("PhysicsMover component not found");
        }

        if (attackExecutor_Cache == null)
        {
            Debug.LogError("Attack component not found");
        }

        if (animator_Cache == null)
        {
            Debug.LogError("Animator component not found");
        }
        physicsMover_Cache.OnGround+=OnGround;
    }

    private void OnGround()
    {
        //接地状態遷移（AnimController）
        animator_Cache.SetTrigger(Ground);
        Debug.Log("Grounded");
    }
}
