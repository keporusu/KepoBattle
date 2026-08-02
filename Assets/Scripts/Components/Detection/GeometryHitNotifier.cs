using System;
using UnityEngine;
using Core.Constants;

namespace Components.Detection
{
    public class GeometryHitNotifier : MonoBehaviour
    {
        public event Action<Collider2D> OnHit;
        public event Action<Collider2D> OnRelease;

        private void Start()
        {
            if (!TryGetComponent(out Collider2D col))
                throw new MissingComponentException($"[{GetType().Name}] Collider2D が {gameObject.name} に見つかりません");
            col.isTrigger = true;
        }

        //ヒット時
        private void OnTriggerEnter2D(Collider2D other)
        {
            // ジオメトリチャンネルじゃないなら通知しない
            if (!other.gameObject.CompareTag(GameTags.GeometryChannel))
            {
                return;
            }

            OnHit?.Invoke(other);
        }

        //離れた時
        private void OnTriggerExit2D(Collider2D other)
        {
            // ジオメトリチャンネルじゃないなら通知しない
            if (!other.gameObject.CompareTag(GameTags.GeometryChannel))
            {
                return;
            }

            OnRelease?.Invoke(other);
        }
    }
}
