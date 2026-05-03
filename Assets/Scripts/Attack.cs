using System.Threading;
using UnityEngine;
using Cysharp.Threading.Tasks;

public class Attack : MonoBehaviour
{
    [SerializeField] private GameObject damageCollider;
    [SerializeField] private float attackDuration = 0.5f;

    private CancellationTokenSource _attackCts;

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

    private void OnDestroy()
    {
        CancelAttack();
    }
}
