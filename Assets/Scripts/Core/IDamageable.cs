
public interface IDamageable
{
    bool IsAlive { get; }

    void ApplyDamage(int amount);
}
