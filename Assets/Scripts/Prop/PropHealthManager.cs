using Interfaces;
using UnityEngine;

namespace Prop
{
    public class PropHealthManager : MonoBehaviour, IHealthManager
    {
        [SerializeField] private int maxHealth;
        [SerializeField] private bool unbreakable = true;
        
        public float CurrentHealth { get; }
        public bool IsDead => CurrentHealth <= 0 && !unbreakable;

        public void TakeDamage(float damage)
        {
        }
    
    }
}

