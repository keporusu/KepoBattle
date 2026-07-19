using System;
using UnityEngine;


//ダメージの通知
public class AttackHitNotifier : MonoBehaviour
{
    
    public event Action<Collider2D> OnHit;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // ダメージのチャンネルじゃないなら通知しない
        if (!other.gameObject.CompareTag("Damage Channel"))
        {
            return;
        }
        
        OnHit?.Invoke(other);
    }
}