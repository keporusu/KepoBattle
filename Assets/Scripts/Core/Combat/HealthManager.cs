using UnityEngine;

namespace Core.Combat
{
    public class HealthManager
    {
        public float Hp { private set; get; }

        public HealthManager(float initialHp)
        {
            Hp = initialHp;
        }

        public void TakeDamage(float damage)
        {
            damage = Mathf.Max(damage, 0f);
            Hp = Mathf.Max(0, Hp - damage);
        }

        public void Heal(float heal)
        {
            heal = Mathf.Max(0, heal);
            Hp = Mathf.Max(0, Hp + heal);
        }
    }
}
