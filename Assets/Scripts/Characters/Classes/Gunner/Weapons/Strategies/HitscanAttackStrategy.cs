
using UnityEngine;

[CreateAssetMenu(
    fileName = "HitscanStrategy",
    menuName = "Nexus Defense/Weapons/Strategies/Hitscan"
)]
public sealed class HitscanAttackStrategy : AttackStrategy
{
    [SerializeField] private LayerMask hitMask = ~0;

    public override bool Execute(in AttackContext context)
    {
        if (context.Instigator == null ||
            context.Direction.sqrMagnitude < 0.99f)
            return false;

        if (!Physics.Raycast(
                context.Origin,
                context.Direction,
                out RaycastHit hit,
                context.Range,
                hitMask,
                QueryTriggerInteraction.Ignore))
        {
            return true;
        }

        if (hit.collider.transform.root.gameObject ==
            context.Instigator.transform.root.gameObject)
        {
            return true;
        }

        IDamageable target =
            hit.collider.GetComponentInParent<IDamageable>();

        if (target != null && target.IsAlive)
        {
            target.ApplyDamage(
                Mathf.CeilToInt(context.Damage)
            );
        }

        return true;
    }
}
