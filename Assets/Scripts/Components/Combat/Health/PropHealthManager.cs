using Core.Combat;
using Core.Contracts;
using UnityEngine;

namespace Components.Combat.Health
{
    public class PropHealthManager : MonoBehaviour, IHealthManager
    {
        //ロジック
        private HealthManager _healthManager;
        
        [SerializeField] private int maxHealth;
        [SerializeField] private bool unbreakable = true;
        
        
        //状態
        public float CurrentHealth => _healthManager.Hp;
        public bool IsDead => _healthManager.Hp <= 0 && !unbreakable;
        
        private void Awake()
        {
            _healthManager = new HealthManager(maxHealth);
        }
        public void TakeDamage(float damage)
        {
            _healthManager.TakeDamage(damage);
        }
        
        //TODO: 破壊処理のような物を入れる
    
    }
}

