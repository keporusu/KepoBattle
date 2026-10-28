using System;
using Systems;
using UnityEngine;

namespace Components.Controller
{
    public class SwitchBlockController : MonoBehaviour
    {
        [SerializeField] private int switchId;
        [SerializeField] private Collider2D geometry;
        [SerializeField] private SpriteRenderer renderer;


        private void Start()
        {
            switch (switchId)
            {
                case 0:
                    renderer.color = Color.red;
                    break;
                case 1:
                    renderer.color = Color.green;
                    break;
                case 2:
                    renderer.color = Color.blue;
                    break;
                default:
                    break;
            }
        }
        
        private void OnEnable()
        {
            if (StageManager.Current != null)
            {
                StageManager.Current.OnSwitchChanged += OnSwitchChanged;
            }
        }

        private void OnSwitchChanged(SwitchEvent switchEvent)
        {
            if (switchEvent.Id != switchId) return;

            if (switchEvent.On)
            {
                geometry.enabled = false;
                var color = renderer.color;
                color.a = 1.0f;
                renderer.color = color;
            }
            else
            {
                geometry.enabled = true;
                var color = renderer.color;
                color.a = 0.5f;
                renderer.color = color;
            }
            
        }
    }

}

