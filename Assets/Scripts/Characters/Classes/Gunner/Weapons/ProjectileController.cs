
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public sealed class ProjectileController : NetworkBehaviour
{
    private Rigidbody body;
    private Collider projectileCollider;

    private Vector3 direction;
    private float speed;
    private float lifetime;
    private float damage;
    private float maxRange;
    private float travelledDistance;

    private Transform instigatorRoot;
    private bool configured;
    private bool hasHit;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        projectileCollider = GetComponent<Collider>();
    }

    public void ConfigureServer(
        Vector3 shotDirection,
        float projectileSpeed,
        float projectileLifetime,
        float damageAmount,
        float range,
        GameObject instigator)
    {
        if (!NetworkManager.Singleton.IsServer ||
            instigator == null)
            return;

        direction = shotDirection.normalized;
        speed = projectileSpeed;
        lifetime = projectileLifetime;
        damage = damageAmount;
        maxRange = range;

        CharacterRoot character =
            instigator.GetComponentInParent<CharacterRoot>();

        instigatorRoot = character != null
            ? character.transform
            : instigator.transform.root;

        configured = true;

        IgnoreOwnerCollisions();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer || !configured)
            return;

        body.isKinematic = false;
        body.useGravity = false;
        body.collisionDetectionMode =
            CollisionDetectionMode.ContinuousDynamic;

        body.linearVelocity = direction * speed;

        DestroyProjectileAfterLifetime();
    }

    private void IgnoreOwnerCollisions()
    {
        if (instigatorRoot == null)
            return;

        Collider[] ownerColliders =
            instigatorRoot.GetComponentsInChildren<Collider>();

        foreach (Collider ownerCollider in ownerColliders)
        {
            if (ownerCollider != null &&
                ownerCollider != projectileCollider)
            {
                Physics.IgnoreCollision(
                    projectileCollider,
                    ownerCollider,
                    true);
            }
        }
    }

    private void FixedUpdate()
    {
        if (!IsServer || !configured || hasHit)
            return;

        travelledDistance += speed * Time.fixedDeltaTime;

        if (travelledDistance >= maxRange)
        {
            DespawnProjectile();
        }
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || hasHit || collision.collider == null)
            return;

        Transform hitRoot = collision.collider.transform.root;

        if (instigatorRoot != null &&
            (hitRoot == instigatorRoot ||
             collision.collider.transform.IsChildOf(instigatorRoot)))
        {
            return;
        }

        hasHit = true;

        IDamageable target =
            collision.collider.GetComponentInParent<IDamageable>();

        if (target != null && target.IsAlive)
        {
            target.ApplyDamage(Mathf.CeilToInt(damage));
        }

        DespawnProjectile();
    }

    private async void DestroyProjectileAfterLifetime()
    {
        await Awaitable.WaitForSecondsAsync(lifetime);

        if (this != null && IsSpawned && IsServer)
            DespawnProjectile();
    }

    private void DespawnProjectile()
    {
        if (IsServer && NetworkObject.IsSpawned)
            NetworkObject.Despawn(true);
    }
}
