using System;
using UnityEngine;
using Core.Constants;
using Data;

namespace Components.Detection
{
    public class EventTriggerNotifier : MonoBehaviour
    {
        public event Action<Collider2D> OnTriggerEnter;
        public event Action<Collider2D> OnTriggerExit;
        
        public void Initialize(TriggerShapeSetting setting)
        {

            switch (setting.shape)
            {
                case ColliderShape.Circle:{
                    var col=gameObject.AddComponent<CircleCollider2D>();
                    col.isTrigger = true;
                    col.radius = setting.circleRadius;
                    col.enabled = true;
                    break;
                }
                case ColliderShape.Capsule:
                {
                    var col = gameObject.AddComponent<CapsuleCollider2D>();
                    col.isTrigger = true;
                    col.size =
                        new Vector2(setting.capsuleRadius * 2, setting.capsuleHeight);
                    col.direction = setting.capsuleDirection == CapsuleDirection.X
                        ? CapsuleDirection2D.Horizontal
                        : CapsuleDirection2D.Vertical;
                    col.enabled = true;
                    break;
                }
                case ColliderShape.Box:
                {
                    var col = gameObject.AddComponent<BoxCollider2D>();
                    col.isTrigger = true;
                    col.size = new Vector2(setting.boxSize.x, setting.boxSize.y);
                    col.enabled = true;
                    break;
                }
                default:
                    break;
            }
        }

        public void DestroyTrigger()
        {
            OnTriggerEnter = null;
            OnTriggerExit = null;
            Destroy(gameObject);
        }
        
        private void Start()
        {
            if (!TryGetComponent(out Collider2D col))
                throw new MissingComponentException($"[{GetType().Name}] Collider2D が {gameObject.name} に見つかりません");
            col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // 存在判定のチャンネルじゃないなら通知しない
            if (!other.gameObject.CompareTag(GameTags.ExistenceChannel))
            {
                return;
            }

            OnTriggerEnter?.Invoke(other);
        }
        
        private void OnTriggerExit2D(Collider2D other)
        {
            // 存在判定のチャンネルじゃないなら通知しない
            if (!other.gameObject.CompareTag(GameTags.ExistenceChannel))
            {
                return;
            }

            OnTriggerExit?.Invoke(other);
        }
    }
}