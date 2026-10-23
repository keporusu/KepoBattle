using System;
using Components.Identity;
using Core.Constants;
using Core.Contracts;
using UnityEngine;
using Data;
using Systems;
using UnityEngine.LowLevelPhysics2D;

namespace Components.Controller
{
    
    public class GoalController : PropBehaviourController
    {
        
        [SerializeField] private Renderer renderer;
        [SerializeField] private ObjectIdentity deliveryObject;
        

        private int _goalEventId;
            
        void OnEnable()
        {
            
            //ゴールイベントの設定
            if (TryGetComponent(out EventTriggersManager eventTriggersManager))
            {
                var setting = new TriggerShapeSetting
                {
                    shape = ColliderShape.Circle,
                    circleRadius = 3.0f,
                };

                _goalEventId = eventTriggersManager.Subscribe(setting, TriggerType.Enter, Vector2.down * 0.3f, TriggerGoal);

            }
        }

        

        void TriggerGoal(Collider2D other)
        {
            //ゴール用に設定したオブジェクトではないなら、何もしない
            if (EntityRoot.Require(other).Identity != deliveryObject) return;
            
            //ゴール通知
            StageManager.Current.GameClear();
            renderer.enabled = false;
        }
        
        
        private void Start()
        {
            //自身
            SetIdentity(ObjectIdentity.Goal);
            
            if (StageManager.Current.StageUIView != null)
            {
                StageManager.Current.StageUIView.SetDeliveryTarget(deliveryObject);
            }
        }
        
        
        
    }
    
}

