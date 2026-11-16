
using Unity.Netcode;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ProjectileStrategy",
    menuName = "Nexus Defense/Weapons/Strategies/Projectile"
)]
public sealed class ProjectileAttackStrategy : AttackStrategy
{
    [Header("Projectile")]
    [SerializeField] private GameObject projectilePrefab;

    [SerializeField, Min(0.1f)]
    private float projectileSpeed = 30f;

    [SerializeField, Min(0.1f)]
    private float projectileLifetime = 5f;

    public override bool Execute(in AttackContext context)
    {
        if (NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.IsServer)
            return false;

        if (projectilePrefab == null ||
            context.Instigator == null ||
            context.Direction.sqrMagnitude < 0.99f)
            return false;

        NetworkObject networkObject =
            projectilePrefab.GetComponent<NetworkObject>();

        ProjectileController projectileController =
            projectilePrefab.GetComponent<ProjectileController>();

        if (networkObject == null || projectileController == null)
        {
            Debug.LogError(
                "[ProjectileAttackStrategy] Префаб снаряда повинен мати " +
                "NetworkObject і ProjectileController.",
                projectilePrefab);
            return false;
        }

        Vector3 direction = context.Direction.normalized;

        GameObject instance = Object.Instantiate(
            projectilePrefab,
            context.Origin,
            Quaternion.LookRotation(direction));

        ProjectileController projectile =
            instance.GetComponent<ProjectileController>();

        projectile.ConfigureServer(
            direction,
            projectileSpeed,
            projectileLifetime,
            context.Damage,
            context.Range,
            context.Instigator);

        NetworkObject spawnedObject =
            instance.GetComponent<NetworkObject>();

        spawnedObject.Spawn();

        return true;
    }
}
