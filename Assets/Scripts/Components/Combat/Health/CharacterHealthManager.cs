using System;
using Core.Contracts;
using Core.Combat;
using UnityEngine;
using UnityEngine.Serialization;

namespace Components.Combat.Health
{
    public class CharacterHealthManager : MonoBehaviour, IHealthManager
    {
        [SerializeField] private float maxHealth = 100;
        
        //状態
        public float CurrentHealth => _healthManager.Hp;
        public bool IsDead => _healthManager.Hp <= 0;
        
        // ロジック
        private HealthManager _healthManager;

        private void Awake()
        {
            _healthManager = new HealthManager(maxHealth);
        }

        public void TakeDamage(float damage)
        {
            _healthManager.TakeDamage(damage);
        }
    }
}
