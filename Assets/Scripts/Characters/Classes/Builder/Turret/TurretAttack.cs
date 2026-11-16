using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(BuildableStructure))]
public sealed class TurretAttack : NetworkBehaviour
{
    #region Inspector Settings

    [Header("Projectile")]
    [SerializeField] private NetworkObject projectilePrefab;
    [SerializeField] private Transform muzzle;

    [Header("Turret Rotation")]
    [SerializeField] private Transform turretBase;
    [SerializeField] private Transform barrel;

    [SerializeField, Min(1f)]
    private float baseRotationSpeed = 180f;

    [SerializeField, Min(1f)]
    private float barrelRotationSpeed = 120f;

    [SerializeField]
    private bool rotateBase = true;

    [SerializeField]
    private bool rotateBarrel = true;

    [Header("Targeting")]
    [SerializeField, Min(0.1f)]
    private float targetScanInterval = 0.2f;

    [SerializeField]
    private LayerMask targetMask = ~0;

    [SerializeField]
    private bool requireLineOfSight = false;

    [SerializeField]
    private LayerMask obstacleMask = ~0;

    [Header("Movement Prediction")]
    [SerializeField]
    private bool enableMovementPrediction = true;

    [SerializeField, Min(0f)]
    private float predictionMultiplier = 1f;

    [SerializeField, Min(0f)]
    private float maxPredictionTime = 2f;

    [SerializeField]
    private bool predictOnlyHorizontalMovement = true;

    [Header("Projectile Settings")]
    [SerializeField, Min(0.1f)]
    private float projectileSpeed = 20f;

    [SerializeField, Min(0.1f)]
    private float projectileLifetime = 5f;

    [SerializeField, Min(0.01f)]
    private float projectileRadius = 0.08f;

    [Header("Debug")]
    [SerializeField]
    private bool showDebugLogs = true;

    #endregion

    #region Runtime References

    private BuildableStructure structure;
    private EnemyAI currentTarget;

    #endregion

    #region Runtime State

    private float nextScanTime;
    private float nextShotTime;

    private readonly HashSet<EnemyAI> scannedEnemies =
        new HashSet<EnemyAI>();

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        structure = GetComponent<BuildableStructure>();

        if (turretBase == null)
            turretBase = transform;

        if (muzzle == null)
            muzzle = transform;
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned)
            return;

        if (structure == null ||
            structure.CurrentHealth.Value <= 0f ||
            projectilePrefab == null)
            return;

        if (Time.time >= nextScanTime)
        {
            nextScanTime =
                Time.time + Mathf.Max(0.1f, targetScanInterval);

            currentTarget = FindTarget();
        }

        if (!IsValidTarget(currentTarget))
        {
            currentTarget = null;
            return;
        }

        Vector3 predictedPosition =
            GetPredictedTargetPosition(currentTarget);

        RotateTurret(predictedPosition);

        if (Time.time < nextShotTime)
            return;

        float attacksPerSecond =
            structure.AttacksPerSecond;

        if (attacksPerSecond <= 0f ||
            structure.Damage <= 0f)
            return;

        if (requireLineOfSight &&
            !HasLineOfSight(currentTarget))
            return;

        if (Shoot(currentTarget, predictedPosition))
        {
            nextShotTime =
                Time.time + 1f / attacksPerSecond;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Transform origin = muzzle != null ? muzzle : transform;

        float range = structure != null
            ? structure.AttackRange
            : 0f;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(origin.position, range);

        if (currentTarget != null)
        {
            Gizmos.color = Color.red;

            Gizmos.DrawLine(
                origin.position,
                GetTargetPosition(currentTarget));
        }
    }

    #endregion

    #region Network Lifecycle

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        nextScanTime = 0f;
        nextShotTime = 0f;

        if (projectilePrefab == null)
        {
            Debug.LogError(
                $"[TurretAttack] {name}: Projectile Prefab не призначений!",
                this);
        }

        if (barrel == null)
        {
            Debug.LogWarning(
                $"[TurretAttack] {name}: Barrel не призначений. " +
                "Вертикальне наведення вимкнено.",
                this);
        }

        if (showDebugLogs)
        {
            Debug.Log(
                $"[TurretAttack] {name} запущена. " +
                $"Range={structure.AttackRange}, " +
                $"Damage={structure.Damage}, " +
                $"APS={structure.AttacksPerSecond}.",
                this);
        }
    }

    #endregion

    #region Target Detection

    private EnemyAI FindTarget()
    {
        float range = structure.AttackRange;

        if (range <= 0f)
            return null;

        Vector3 origin = muzzle.position;

        Collider[] colliders = Physics.OverlapSphere(
            origin,
            range,
            targetMask,
            QueryTriggerInteraction.Collide);

        scannedEnemies.Clear();

        EnemyAI bestTarget = null;
        float bestDistanceSquared = range * range;

        foreach (Collider targetCollider in colliders)
        {
            if (targetCollider == null)
                continue;

            EnemyAI enemy =
                targetCollider.GetComponentInParent<EnemyAI>();

            if (enemy == null || !scannedEnemies.Add(enemy))
                continue;

            if (!IsValidTarget(enemy))
                continue;

            Vector3 targetPosition =
                GetTargetPosition(enemy);

            float distanceSquared =
                (targetPosition - origin).sqrMagnitude;

            if (distanceSquared > bestDistanceSquared)
                continue;

            bestDistanceSquared = distanceSquared;
            bestTarget = enemy;
        }

        if (showDebugLogs && bestTarget != null)
        {
            Debug.Log(
                $"[TurretAttack] {name}: ціль {bestTarget.name}, " +
                $"відстань {Mathf.Sqrt(bestDistanceSquared):F2}.",
                this);
        }

        return bestTarget;
    }

    private bool IsValidTarget(EnemyAI enemy)
    {
        if (enemy == null ||
            !enemy.isActiveAndEnabled ||
            !enemy.IsSpawned)
            return false;

        Health health = enemy.GetComponent<Health>();

        if (health == null)
            health = enemy.GetComponentInChildren<Health>();

        return health != null &&
               health.IsSpawned &&
               health.IsAlive;
    }

    private Vector3 GetTargetPosition(EnemyAI enemy)
    {
        Collider[] colliders =
            enemy.GetComponentsInChildren<Collider>();

        bool found = false;
        Bounds bounds = default;

        foreach (Collider col in colliders)
        {
            if (col == null || !col.enabled)
                continue;

            if (!found)
            {
                bounds = col.bounds;
                found = true;
            }
            else
            {
                bounds.Encapsulate(col.bounds);
            }
        }

        return found
            ? bounds.center
            : enemy.transform.position + Vector3.up;
    }

    #endregion

    #region Movement Prediction

    private Vector3 GetPredictedTargetPosition(EnemyAI enemy)
    {
        Vector3 currentPosition = GetTargetPosition(enemy);

        if (!enableMovementPrediction)
            return currentPosition;

        NavMeshAgent agent =
            enemy.GetComponent<NavMeshAgent>();

        if (agent == null ||
            !agent.enabled ||
            !agent.isOnNavMesh)
            return currentPosition;

        Vector3 velocity = agent.velocity;

        if (predictOnlyHorizontalMovement)
            velocity.y = 0f;

        float distance = Vector3.Distance(
            muzzle.position,
            currentPosition);

        float travelTime =
            distance / Mathf.Max(0.1f, projectileSpeed);

        travelTime = Mathf.Min(
            travelTime,
            maxPredictionTime);

        Vector3 predictedPosition =
            currentPosition +
            velocity * travelTime * predictionMultiplier;

        if (showDebugLogs)
        {
            Debug.DrawLine(
                currentPosition,
                predictedPosition,
                Color.cyan,
                targetScanInterval);
        }

        return predictedPosition;
    }

    #endregion

    #region Turret Rotation

    private void RotateTurret(Vector3 targetPosition)
    {
        if (rotateBase && turretBase != null)
        {
            Vector3 direction =
                targetPosition - turretBase.position;

            direction.y = 0f;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(direction);

                turretBase.rotation =
                    Quaternion.RotateTowards(
                        turretBase.rotation,
                        targetRotation,
                        baseRotationSpeed * Time.deltaTime);
            }
        }

        if (rotateBarrel && barrel != null)
        {
            Vector3 direction =
                targetPosition - barrel.position;

            if (direction.sqrMagnitude > 0.001f)
            {
                Quaternion targetRotation =
                    Quaternion.LookRotation(direction);

                barrel.rotation =
                    Quaternion.RotateTowards(
                        barrel.rotation,
                        targetRotation,
                        barrelRotationSpeed * Time.deltaTime);
            }
        }
    }

    #endregion

    #region Line Of Sight

    private bool HasLineOfSight(EnemyAI enemy)
    {
        Vector3 origin = muzzle.position;
        Vector3 destination = GetTargetPosition(enemy);

        Vector3 direction = destination - origin;
        float distance = direction.magnitude;

        if (distance <= 0.01f)
            return true;

        RaycastHit[] hits = Physics.RaycastAll(
            origin,
            direction.normalized,
            distance,
            obstacleMask,
            QueryTriggerInteraction.Ignore);

        System.Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (hit.collider.transform == transform ||
                hit.collider.transform.IsChildOf(transform))
                continue;

            return hit.collider.GetComponentInParent<EnemyAI>() == enemy;
        }

        return true;
    }

    #endregion

    #region Projectile Firing

    private bool Shoot(
        EnemyAI target,
        Vector3 predictedPosition)
    {
        if (!IsValidTarget(target))
            return false;

        Vector3 spawnPosition = muzzle.position;

        Vector3 direction =
            predictedPosition - spawnPosition;

        if (direction.sqrMagnitude <= 0.0001f)
            return false;

        direction.Normalize();

        Quaternion rotation =
            Quaternion.LookRotation(direction);

        NetworkObject projectileObject = Instantiate(
            projectilePrefab,
            spawnPosition,
            rotation);

        TurretProjectile projectile =
            projectileObject.GetComponent<TurretProjectile>();

        if (projectile == null)
        {
            Debug.LogError(
                "[TurretAttack] Projectile Prefab не має TurretProjectile!",
                projectileObject);

            Destroy(projectileObject.gameObject);
            return false;
        }

        projectile.ConfigureServer(
            direction,
            structure.Damage,
            projectileSpeed,
            projectileLifetime,
            projectileRadius,
            transform);

        projectileObject.Spawn(true);

        if (showDebugLogs)
        {
            Debug.Log(
                $"[TurretAttack] Постріл у {target.name}. " +
                $"Prediction={predictedPosition}.",
                this);
        }

        return true;
    }

    #endregion
}