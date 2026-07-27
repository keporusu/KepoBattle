using System;
using UnityEngine;
using Core.Constants;


//ダメージの通知
public class AttackHitNotifier : MonoBehaviour
{
    
    public event Action<Collider2D> OnHit;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // ダメージのチャンネルじゃないなら通知しない
        if (!other.gameObject.CompareTag(GameTags.DamageChannel))
        {
            return;
        }
        
        OnHit?.Invoke(other);
    }
}