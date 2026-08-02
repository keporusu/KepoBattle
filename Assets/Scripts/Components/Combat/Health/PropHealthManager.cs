using Core.Contracts;
using UnityEngine;

namespace Components.Combat.Health
{
    public class PropHealthManager : MonoBehaviour, IHealthManager
    {
        [SerializeField] private int maxHealth;
        [SerializeField] private bool unbreakable = true;
        
        //TODO: 破壊処理のような物を入れる
        public float CurrentHealth { get; }
        public bool IsDead => CurrentHealth <= 0 && !unbreakable;

        public void TakeDamage(float damage)
        {
        }
    
    }
}

