
using UnityEngine;

public abstract class AttackStrategy : ScriptableObject
{
    public abstract bool Execute(in AttackContext context);
}

public readonly struct AttackContext
{
    public readonly Vector3 Origin;
    public readonly Vector3 Direction;
    public readonly float Damage;
    public readonly float Range;
    public readonly GameObject Instigator;

    public AttackContext(
        Vector3 origin,
        Vector3 direction,
        float damage,
        float range,
        GameObject instigator)
    {
        Origin = origin;
        Direction = direction.normalized;
        Damage = damage;
        Range = range;
        Instigator = instigator;
    }
}
