using System;

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
            damage = Math.Max(damage, 0f);
            Hp = Math.Max(0, Hp - damage);
        }

        public void Heal(float heal)
        {
            heal = Math.Max(0, heal);
            Hp = Math.Max(0, Hp + heal);
        }
    }
}
