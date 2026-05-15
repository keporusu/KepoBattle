using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class Attack : MonoBehaviour
{
    [SerializeField] private GameObject damageCollider;
    [SerializeField] private float attackDuration = 0.5f;

    private CancellationTokenSource _attackCts;

    public void InitializeAttack(Animator animator)
    {
        //全てのNotifierを取得
        var spNotifiers = animator.GetBehaviours<StateProgressionNotifier>();
        //それぞれの攻撃のNotifierを取得
        var spNotifierAttack1 = System.Array.Find(spNotifiers,x=>x.StateName == "Attack1");
        var spNotifierAttack2 = System.Array.Find(spNotifiers,x=>x.StateName == "Attack2");
        var spNotifierSpecialAttack = System.Array.Find(spNotifiers,x=>x.StateName == "SpecialAttack");
        if (spNotifierAttack1 == null || spNotifierAttack2 == null || spNotifierSpecialAttack == null)
        {
            Debug.LogError("Some behaviours are missing");
        }

        spNotifierAttack1.OnStateProgress += OnStateUpdateAttack1;
        spNotifierAttack2.OnStateProgress += OnStateUpdateAttack2;
        spNotifierSpecialAttack.OnStateProgress += OnStateUpdateSpecialAttack;
    }

    public void StartAttack()
    {
        CancelAttack();
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
    
    
    private void OnStateUpdateAttack1(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // TODO: これを毎フレームAnimControllerのステート中に呼ばれるようにして、コリジョンの位置を調整する
    }
    private void OnStateUpdateAttack2(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // TODO: これを毎フレームAnimControllerのステート中に呼ばれるようにして、コリジョンの位置を調整する
    }
    private void OnStateUpdateSpecialAttack(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        // TODO: これを毎フレームAnimControllerのステート中に呼ばれるようにして、コリジョンの位置を調整する
    }

    private void OnDestroy()
    {
        CancelAttack();
    }
}
