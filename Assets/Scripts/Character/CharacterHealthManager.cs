using System;
using Interfaces;
using UnityEngine;

namespace Character
{
    public class CharacterHealthManager : MonoBehaviour, IHealthManager
    {
        [SerializeField] private float _maxHealth = 100;
        public float CurrentHealth { get; private set; }
        public bool IsDead => CurrentHealth <= 0;

        private void Start()
        {
            CurrentHealth = _maxHealth;
        }

        public void TakeDamage(float damage)
        {
            CurrentHealth -= damage;
            CurrentHealth = Mathf.Max(0.0f, CurrentHealth);
        }
    }
}
