using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

public enum AttackType
{
    Attack1,
    Attack2,
    SpecialAttack,
    None,
}
public enum CollisionShape
{
    Circle,
    Capsule,
    Box,
}

public enum CapsuleDirection { X, Y, Z }

[Serializable]
public struct AttackCollisionSetting
{
    public CollisionShape shape;

    // Circle
    public float circleRadius;

    // Capsule
    public float capsuleRadius;
    public float capsuleHeight;
    public CapsuleDirection capsuleDirection;

    // Box
    public Vector3 boxSize;

    // 共通
    public Vector3 offset;
    [Range(0f, 1f)] public float spanStart;
    [Range(0f, 1f)] public float spanEnd;
}


public class AttackExecutor : MonoBehaviour
{
    //攻撃設定
    [SerializeField] private GameObject damageCollider;
    [SerializeField] private float attackDuration = 0.5f;
    
    //攻撃時のコリジョンの設定
    [SerializeField] private List<AttackCollisionSetting> attack1CollisionSettings;
    [SerializeField] private List<AttackCollisionSetting> attack2CollisionSettings;
    [SerializeField] private List<AttackCollisionSetting> specialCollisionSettings;
    
    
    private CancellationTokenSource _attackCts;
    
    //ステートの進行状況によってコリジョンを調整する用
    //private Animator animator_Cache;
    private StateProgressionNotifier spNotifierAttack1_Cache;
    private StateProgressionNotifier spNotifierAttack2_Cache;
    private StateProgressionNotifier spNotifierSpecial_Cache;

    private AttackType progressAttack = AttackType.None;

    public void Start()
    {
        //アニメーター取得
        var animator = GetComponent<Animator>();
        if (animator == null)
        {
            Debug.LogError("Animator is null");
        }
        //全てのNotifierを取得
        var spNotifiers = animator.GetBehaviours<StateProgressionNotifier>();
        //それぞれの攻撃のNotifierを取得
        spNotifierAttack1_Cache = System.Array.Find(spNotifiers,x=>x.StateName == "Attack1");
        spNotifierAttack2_Cache = System.Array.Find(spNotifiers,x=>x.StateName == "Attack2");
        spNotifierSpecial_Cache = System.Array.Find(spNotifiers,x=>x.StateName == "SpecialAttack");
        if (spNotifierAttack1_Cache == null || spNotifierAttack2_Cache == null || spNotifierSpecial_Cache == null)
        {
            Debug.LogError("Some behaviours are missing");
        }
        
        //アニメーション再生開始時のイベント登録
        spNotifierAttack1_Cache.OnStateBegin += SetTypeAttack1;
        spNotifierAttack2_Cache.OnStateBegin += SetTypeAttack2;
        spNotifierSpecial_Cache.OnStateBegin += SetTypeSpecialAttack;
        spNotifierAttack1_Cache.OnStateProgress += SetCollisionAttack1;
        spNotifierAttack2_Cache.OnStateBegin += SetCollisionAttack2;
        spNotifierSpecial_Cache.OnStateBegin += SetCollisionSpecialAttack;
    }


    public void StartAttack1()
    {
        CancelAttack();
        
        progressAttack = AttackType.Attack1;
        _attackCts = new CancellationTokenSource();
        DeactivateAfterDuration(_attackCts.Token).Forget();
    }

    public void CancelAttack()
    {
        if (_attackCts != null)
        {
            _attackCts.Cancel();
            _attackCts.Dispose();
            _attackCts = null;
        }
        damageCollider.SetActive(false);
    }
    
    
    private async UniTaskVoid DeactivateAfterDuration(CancellationToken ct)
    {
        damageCollider.SetActive(true);
        await UniTask.Delay((int)(attackDuration * 1000), cancellationToken: ct);
        damageCollider.SetActive(false);
    }
    
    
    private void SetTypeAttack1(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        progressAttack = AttackType.Attack1;
    }
    private void SetCollisionAttack1(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // TODO: これを毎フレームAnimControllerのステート中に呼ばれるようにして、コリジョンの位置を調整する
    }
    private void SetTypeAttack2(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        progressAttack = AttackType.Attack2;
    }
    private void SetCollisionAttack2(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // TODO: これを毎フレームAnimControllerのステート中に呼ばれるようにして、コリジョンの位置を調整する
    }
    private void SetTypeSpecialAttack(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        progressAttack = AttackType.SpecialAttack;
    }
    private void SetCollisionSpecialAttack(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // TODO: これを毎フレームAnimControllerのステート中に呼ばれるようにして、コリジョンの位置を調整する
    }
    
    private void OnDestroy()
    {
        CancelAttack();
    }
}
