
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public sealed class TurretProjectile : NetworkBehaviour
{
    [Header("Collision")]
    [SerializeField] private LayerMask collisionMask = ~0;

    private Vector3 direction;
    private float damage;
    private float speed;
    private float lifetime;
    private float collisionRadius;

    private Transform ownerRoot;

    private float spawnTime;

    private bool configured;
    private bool pendingConfiguration;
    private bool impacted;

    private void Awake()
    {
        if (GetComponent<NetworkTransform>() == null)
        {
            Debug.LogError(
                "[TurretProjectile] Відсутній NetworkTransform!",
                this);
        }
    }

    public void ConfigureServer(
        Vector3 shootDirection,
        float projectileDamage,
        float projectileSpeed,
        float projectileLifetime,
        float radius,
        Transform projectileOwner)
    {
        if (IsSpawned && !IsServer)
            return;

        if (shootDirection.sqrMagnitude <= 0.0001f)
        {
            Debug.LogError(
                "[TurretProjectile] Неправильний напрямок пострілу!",
                this);
            return;
        }

        direction = shootDirection.normalized;
        damage = Mathf.Max(0f, projectileDamage);
        speed = Mathf.Max(0f, projectileSpeed);
        lifetime = Mathf.Max(0.1f, projectileLifetime);
        collisionRadius = Mathf.Max(0.01f, radius);
        ownerRoot = projectileOwner;

        pendingConfiguration = true;

        if (IsSpawned && IsServer)
            ApplyConfiguration();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        if (!pendingConfiguration)
        {
            Debug.LogError(
                $"[TurretProjectile] {name}: Spawn виконано без конфігурації!",
                this);

            DespawnProjectile();
            return;
        }

        ApplyConfiguration();
    }

    private void ApplyConfiguration()
    {
        if (!IsServer || !pendingConfiguration)
            return;

        if (speed <= 0f || lifetime <= 0f)
        {
            Debug.LogError(
                "[TurretProjectile] Некоректні параметри руху!",
                this);

            DespawnProjectile();
            return;
        }

        transform.rotation =
            Quaternion.LookRotation(direction);

        spawnTime = Time.time;

        configured = true;
        pendingConfiguration = false;
        impacted = false;

        Debug.Log(
            $"[TurretProjectile] {name}: конфігурацію застосовано. " +
            $"Damage={damage}, Speed={speed}, " +
            $"Lifetime={lifetime}, Radius={collisionRadius}, " +
            $"Direction={direction}.",
            this);
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned)
            return;

        if (!configured || impacted)
            return;

        if (Time.time >= spawnTime + lifetime)
        {
            if (NetworkManager.Singleton != null &&
                NetworkManager.Singleton.LogLevel == LogLevel.Developer)
            {
                Debug.Log(
                    $"[TurretProjectile] {name}: час життя завершився.",
                    this);
            }

            DespawnProjectile();
            return;
        }

        float movementDistance =
            speed * Time.deltaTime;

        if (movementDistance <= 0f)
            return;

        Vector3 startPosition = transform.position;

        Vector3 endPosition =
            startPosition + direction * movementDistance;

        if (TryGetImpact(
            startPosition,
            direction,
            movementDistance,
            out RaycastHit hit))
        {
            transform.position = hit.point;

            HandleImpact(hit);
            return;
        }

        transform.position = endPosition;
    }

    private bool TryGetImpact(
        Vector3 origin,
        Vector3 moveDirection,
        float distance,
        out RaycastHit closestHit)
    {
        closestHit = default;

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            collisionRadius,
            moveDirection,
            distance,
            collisionMask,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
            return false;

        System.Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            Transform hitTransform = hit.collider.transform;

            if (ownerRoot != null &&
                (hitTransform == ownerRoot ||
                 hitTransform.IsChildOf(ownerRoot)))
            {
                continue;
            }

            closestHit = hit;
            return true;
        }

        return false;
    }

    private void HandleImpact(RaycastHit hit)
    {
        if (!IsServer || impacted)
            return;

        impacted = true;

        EnemyAI enemy =
            hit.collider.GetComponentInParent<EnemyAI>();

        if (enemy != null && enemy.IsSpawned)
        {
            Health health =
                enemy.GetComponent<Health>();

            if (health == null)
                health = enemy.GetComponentInChildren<Health>();

            if (health != null && health.IsAlive)
            {
                int finalDamage =
                    Mathf.Max(1, Mathf.RoundToInt(damage));

                health.ApplyDamage(finalDamage);

                Debug.Log(
                    $"[TurretProjectile] Влучання у {enemy.name}. " +
                    $"Шкода: {finalDamage}.",
                    this);
            }
        }
        else
        {
            Debug.Log(
                $"[TurretProjectile] Снаряд зіткнувся з об'єктом: " +
                $"{hit.collider.name}.",
                this);
        }

        DespawnProjectile();
    }

    private void DespawnProjectile()
    {
        if (!IsServer || !IsSpawned)
            return;

        NetworkObject.Despawn(true);
    }
}
