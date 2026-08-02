using System;
using UnityEngine;
using Core.Constants;

//ダメージの通知
public class DamageHitNotifier : MonoBehaviour
{
    
    public event Action<Collider2D> OnHit;

    private void Start()
    {
        if (!TryGetComponent(out Collider2D col))
            throw new MissingComponentException($"[{GetType().Name}] Collider2D が {gameObject.name} に見つかりません");
        col.isTrigger = true;
    }
    
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 攻撃チャンネルじゃないなら通知しない
        if (!other.gameObject.CompareTag(GameTags.AttackChannel))
        {
            return;
        }
        
        OnHit?.Invoke(other);
    }
}
