namespace Interfaces
{
    public interface IHealthManager
    {
        float CurrentHealth { get; }
        bool IsDead { get; }
        void TakeDamage(float damage);
    }
}