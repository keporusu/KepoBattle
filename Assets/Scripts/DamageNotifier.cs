using System;
using UnityEngine;

//ダメージの通知
public class DamageNotifier : MonoBehaviour
{
    
    public System.Action<Collider2D> OnHit;
    private void OnTriggerEnter2D(Collider2D other)
    {
        // 攻撃チャンネルじゃないなら通知しない
        if (!other.gameObject.CompareTag("Attack Channel"))
        {
            return;
        }
        
        OnHit.Invoke(other);
    }
}
