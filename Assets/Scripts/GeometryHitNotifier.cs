using System;
using UnityEngine;

public class GeometryHitNotifier : MonoBehaviour
{
    public System.Action<Collider2D> OnHit;

    private void Start()
    {
        if (GetComponent<Collider2D>() == null)
            throw new MissingComponentException($"[{GetType().Name}] Collider2D が {gameObject.name} に見つかりません");
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // ジオメトリチャンネルじゃないなら通知しない
        if (!other.gameObject.CompareTag("Geometry Channel"))
        {
            return;
        }
        
        OnHit.Invoke(other);
    }
}
