using System;
using System.Collections.Generic;
using System.Linq;
using Components.Detection;
using UnityEngine;

namespace Components.Controller
{
    public enum TriggerType
    {
        Enter,
        Exit,
    }
    
    struct TriggerSet
    {
        public int id;
        public EventTriggerNotifier notifier;
    }
    
    public class EventTriggersManager : MonoBehaviour
    {
        [SerializeField] GameObject triggerPrefab;
        
        private List<TriggerSet> triggerSets;
        private int triggersCounter = 0;
        
        /// <summary>
        /// トリガーを作って、関数を登録する
        /// </summary>
        /// <param name="shapeSetting">コリジョン形状</param>
        /// <param name="triggerType">Enter or Exit</param>
        /// <param name="offset">コリジョンの位置</param>
        /// <param name="callback">登録したい関数</param>
        public void Subscribe(TriggerShapeSetting shapeSetting, TriggerType triggerType, Vector2 offset, Action<Collider2D> callback)
        {
            var triggerPos = new Vector3(offset.x, offset.y, 0);
            var trigger = Instantiate(triggerPrefab, transform);
            trigger.transform.localPosition = triggerPos;
            trigger.transform.localRotation = Quaternion.identity;

            var notifier = trigger.GetComponent<EventTriggerNotifier>();
            if (triggerType == TriggerType.Enter)
            {
                notifier.OnTriggerEnter += callback;
            }
            else
            {
                notifier.OnTriggerExit += callback;
            }
            notifier.Initialize(shapeSetting);

            triggerSets.Add(new TriggerSet { id = triggersCounter, notifier = notifier });
            triggersCounter++;
        }
        
        
        /// <summary>
        /// トリガーの購読をId指定でやめる
        /// </summary>
        /// <param name="id">削除するトリガーID</param>
        public void Unsubscribe(int id)
        {
            
            int removeId = triggerSets.FindIndex(x => x.id == id);
            if (removeId >= 0)
            {
                triggerSets[removeId].notifier.DestroyTrigger();
                triggerSets.RemoveAt(removeId);
            }
        }
    }
}