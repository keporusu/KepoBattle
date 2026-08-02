namespace Core.Contracts
{
    public interface IHealthManager
    {
        float CurrentHealth { get; }
        bool IsDead { get; }
        void TakeDamage(float damage);
    }
}