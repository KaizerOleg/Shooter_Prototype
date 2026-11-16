
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Health))]
public class EnemyAI : NetworkBehaviour
{
    #region Inspector Settings

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 3.5f;
    [SerializeField] private float attackRange = 2f;

    [Header("Attack")]
    [SerializeField] private int attackDamage = 20;
    [SerializeField] private float attackCooldown = 1.5f;

    [Header("Wall Attack")]
    [SerializeField, Min(0.1f)]
    private float wallAttackRange = 3f;

    [SerializeField, Min(0.1f)]
    private float wallScanInterval = 0.4f;

    [SerializeField, Min(0.1f)]
    private float wallSearchWidth = 3.5f;

    [SerializeField, Min(0.1f)]
    private float wallApproachDistance = 1f;

    [Header("Navigation")]
    [SerializeField, Min(0.5f)]
    private float nexusNavMeshSearchRadius = 5f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private string runParameter = "Run";
    [SerializeField] private string attackTrigger = "Attack";

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true;

    #endregion

    #region Components

    private NavMeshAgent agent;
    private Health enemyHealth;
    private NexusController nexus;
    private Collider nexusCollider;
    private NetworkAnimator networkAnimator;

    private NavMeshPath navigationPath;

    #endregion

    #region Animation State

    private int runParameterHash;
    private int attackTriggerHash;

    private bool isRunning;

    #endregion

    #region Runtime State

    private float nextAttackTime;
    private float nextWallScanTime;

    private bool initialized;
    private bool warnedAboutNavMesh;

    private BuildableStructure currentBlockingWall;
    private Vector3 wallApproachPosition;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        enemyHealth = GetComponent<Health>();
        networkAnimator = GetComponent<NetworkAnimator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        runParameterHash =
            Animator.StringToHash(runParameter);

        attackTriggerHash =
            Animator.StringToHash(attackTrigger);

        navigationPath = new NavMeshPath();
    }

    private void Update()
    {
        if (!IsServer || !initialized)
            return;

        if (enemyHealth.IsDead.Value)
        {
            SetRunningAnimation(false);
            NetworkObject.Despawn(true);
            return;
        }

        if (nexus == null ||
            !nexus.IsSpawned ||
            nexus.IsDestroyed.Value)
        {
            agent.isStopped = true;
            SetRunningAnimation(false);
            return;
        }

        if (!agent.isOnNavMesh)
        {
            SetRunningAnimation(false);

            if (!warnedAboutNavMesh)
            {
                Debug.LogError("[EnemyAI] Agent втратив NavMesh!");
                warnedAboutNavMesh = true;
            }

            return;
        }

        float surfaceDistance = GetDistanceToNexusSurface();

        if (surfaceDistance <= attackRange)
        {
            currentBlockingWall = null;

            agent.isStopped = true;
            agent.ResetPath();

            SetRunningAnimation(false);

            AttackNexus();
            return;
        }

        if (Time.time >= nextWallScanTime)
        {
            nextWallScanTime =
                Time.time + wallScanInterval;

            FindBlockingWall();
        }

        if (currentBlockingWall != null)
        {
            if (!currentBlockingWall.IsSpawned ||
                !currentBlockingWall.IsAlive)
            {
                currentBlockingWall = null;
                nextWallScanTime = 0f;
            }
            else
            {
                AttackBlockingWall();
                return;
            }
        }

        Vector3 destination;

        if (!TryGetNexusNavMeshPosition(out destination))
        {
            agent.isStopped = true;
            SetRunningAnimation(false);
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(destination);

        SetRunningAnimation(true);
    }

    #endregion

    #region Network Lifecycle

    public override void OnNetworkSpawn()
    {
        agent = GetComponent<NavMeshAgent>();
        enemyHealth = GetComponent<Health>();

        if (!IsServer)
        {
            agent.enabled = false;
            return;
        }

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (networkAnimator == null)
            networkAnimator = GetComponent<NetworkAnimator>();

        agent.speed = moveSpeed;
        agent.stoppingDistance = 0f;

        nexus = FindFirstObjectByType<NexusController>();

        if (nexus == null)
        {
            Debug.LogError("[EnemyAI] Не знайдено NexusController!");
            return;
        }

        nexusCollider = nexus.GetComponentInChildren<Collider>();

        if (!agent.isOnNavMesh)
        {
            Debug.LogError(
                "[EnemyAI] Ворог не стоїть на NavMesh!");
            return;
        }

        initialized = true;

        SetRunningAnimation(false);

        if (showDebugLogs)
            Debug.Log("[EnemyAI] AI готовий. Nexus знайдено.");
    }

    #endregion

    #region Nexus Navigation

    private bool TryGetNexusNavMeshPosition(out Vector3 position)
    {
        position = Vector3.zero;

        if (nexus == null)
            return false;

        Vector3 target = nexus.transform.position;

        if (nexusCollider != null)
        {
            target = nexusCollider.ClosestPoint(transform.position);
        }

        if (NavMesh.SamplePosition(
            target,
            out NavMeshHit hit,
            nexusNavMeshSearchRadius,
            agent.areaMask))
        {
            position = hit.position;
            return true;
        }

        return false;
    }

    private float GetDistanceToNexusSurface()
    {
        if (nexusCollider != null)
        {
            Vector3 closestPoint =
                nexusCollider.ClosestPoint(transform.position);

            return Vector3.Distance(
                transform.position,
                closestPoint);
        }

        return Vector3.Distance(
            transform.position,
            nexus.transform.position);
    }

    #endregion

    #region Wall Detection

    private void FindBlockingWall()
    {
        currentBlockingWall = null;

        if (nexus == null || !agent.isOnNavMesh)
            return;

        if (!TryGetNexusNavMeshPosition(out Vector3 destination))
            return;

        bool hasPath = NavMesh.CalculatePath(
            agent.nextPosition,
            destination,
            agent.areaMask,
            navigationPath);

        if (!hasPath)
            return;

        if (navigationPath.status == NavMeshPathStatus.PathComplete)
            return;

        currentBlockingWall = FindWallBetweenEnemyAndNexus();

        if (currentBlockingWall == null)
        {
            if (showDebugLogs)
            {
                Debug.LogWarning(
                    $"[EnemyAI] {name}: маршрут неповний, " +
                    "але стіну, яка його блокує, не знайдено.");
            }

            return;
        }

        if (!TryCalculateWallApproachPosition(
            currentBlockingWall,
            out wallApproachPosition))
        {
            wallApproachPosition = currentBlockingWall.transform.position;
        }

        if (showDebugLogs)
        {
            Debug.Log(
                $"[EnemyAI] {name}: маршрут перекритий стіною " +
                $"'{currentBlockingWall.name}'. " +
                "Ворог прямує до неї.");
        }
    }

    private BuildableStructure FindWallBetweenEnemyAndNexus()
    {
        BuildableStructure[] structures =
            FindObjectsByType<BuildableStructure>(
                FindObjectsSortMode.None);

        Vector3 start = transform.position;
        Vector3 end = nexus.transform.position;

        Vector3 direction = end - start;

        float length = direction.magnitude;

        if (length <= 0.01f)
            return null;

        Vector3 normalizedDirection = direction / length;

        BuildableStructure bestWall = null;
        float bestProjection = float.MaxValue;

        foreach (BuildableStructure structure in structures)
        {
            if (structure == null ||
                !structure.IsSpawned ||
                !structure.IsAlive ||
                !structure.IsWall)
            {
                continue;
            }

            Collider[] colliders =
                structure.GetComponentsInChildren<Collider>();

            float closestProjection = float.MaxValue;
            bool blocksDirection = false;

            foreach (Collider wallCollider in colliders)
            {
                if (wallCollider == null ||
                    !wallCollider.enabled ||
                    !wallCollider.gameObject.activeInHierarchy)
                {
                    continue;
                }

                const int sampleCount = 20;

                for (int i = 1; i < sampleCount; i++)
                {
                    float t = i / (float)sampleCount;

                    Vector3 pointOnPath =
                        start + normalizedDirection * (length * t);

                    Vector3 closestPoint =
                        wallCollider.ClosestPoint(pointOnPath);

                    float distance =
                        Vector3.Distance(pointOnPath, closestPoint);

                    if (distance > wallSearchWidth)
                        continue;

                    float projection =
                        Vector3.Dot(
                            closestPoint - start,
                            normalizedDirection);

                    if (projection <= 0f || projection >= length)
                        continue;

                    blocksDirection = true;

                    if (projection < closestProjection)
                        closestProjection = projection;
                }
            }

            if (!blocksDirection)
                continue;

            if (closestProjection < bestProjection)
            {
                bestProjection = closestProjection;
                bestWall = structure;
            }
        }

        return bestWall;
    }

    #endregion

    #region Wall Navigation

    private bool TryCalculateWallApproachPosition(
        BuildableStructure wall,
        out Vector3 approachPosition)
    {
        approachPosition = transform.position;

        if (wall == null || nexus == null)
            return false;

        Collider[] colliders =
            wall.GetComponentsInChildren<Collider>();

        Vector3 closestPoint = wall.transform.position;
        float closestDistance = float.MaxValue;

        foreach (Collider wallCollider in colliders)
        {
            if (wallCollider == null ||
                !wallCollider.enabled ||
                !wallCollider.gameObject.activeInHierarchy)
                continue;

            Vector3 point =
                wallCollider.ClosestPoint(transform.position);

            float distance =
                Vector3.Distance(transform.position, point);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestPoint = point;
            }
        }

        Vector3 awayFromNexus =
            transform.position - nexus.transform.position;

        awayFromNexus.y = 0f;

        if (awayFromNexus.sqrMagnitude < 0.001f)
            awayFromNexus = -transform.forward;

        awayFromNexus.Normalize();

        Vector3 desiredPosition =
            closestPoint + awayFromNexus * wallApproachDistance;

        if (NavMesh.SamplePosition(
            desiredPosition,
            out NavMeshHit hit,
            3f,
            agent.areaMask))
        {
            approachPosition = hit.position;
            return true;
        }

        if (NavMesh.SamplePosition(
            transform.position,
            out hit,
            2f,
            agent.areaMask))
        {
            approachPosition = hit.position;
            return true;
        }

        return false;
    }

    #endregion

    #region Wall Combat

    private void AttackBlockingWall()
    {
        if (currentBlockingWall == null ||
            !currentBlockingWall.IsAlive)
        {
            currentBlockingWall = null;
            nextWallScanTime = 0f;
            return;
        }

        Collider[] wallColliders =
            currentBlockingWall.GetComponentsInChildren<Collider>();

        float closestDistance = float.MaxValue;

        foreach (Collider wallCollider in wallColliders)
        {
            if (wallCollider == null ||
                !wallCollider.enabled ||
                !wallCollider.gameObject.activeInHierarchy)
            {
                continue;
            }

            Vector3 closestPoint =
                wallCollider.ClosestPoint(transform.position);

            float distance =
                Vector3.Distance(transform.position, closestPoint);

            if (distance < closestDistance)
                closestDistance = distance;
        }

        if (closestDistance > wallAttackRange)
        {
            agent.isStopped = false;
            agent.SetDestination(wallApproachPosition);

            SetRunningAnimation(true);
            return;
        }

        agent.isStopped = true;
        agent.ResetPath();

        SetRunningAnimation(false);

        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackCooldown;

        PlayAttackAnimation();

        bool destroyed =
            currentBlockingWall.ApplyDamage(attackDamage);

        if (showDebugLogs)
        {
            Debug.Log(
                $"[EnemyAI] {name} атакує стіну " +
                $"'{currentBlockingWall.name}'. " +
                $"Шкода: {attackDamage}. " +
                $"Залишилося HP: " +
                $"{(destroyed ? 0f : currentBlockingWall.CurrentHealth.Value)}.");
        }

        if (destroyed)
        {
            currentBlockingWall = null;
            nextWallScanTime = 0f;

            agent.isStopped = false;
        }
    }

    #endregion

    #region Nexus Combat

    private void AttackNexus()
    {
        if (Time.time < nextAttackTime)
            return;

        nextAttackTime = Time.time + attackCooldown;

        PlayAttackAnimation();

        nexus.ApplyDamage(attackDamage);

        if (showDebugLogs)
        {
            Debug.Log(
                $"[EnemyAI] Атака Nexus! Шкода: {attackDamage}. " +
                $"HP Nexus: {nexus.CurrentHealth.Value}");
        }
    }

    #endregion

    #region Animation

    private void SetRunningAnimation(bool running)
    {
        if (animator == null || isRunning == running)
            return;

        isRunning = running;

        if (networkAnimator != null && IsSpawned)
        {
            networkAnimator.Animator.SetBool(
                runParameterHash,
                running);
        }
        else
        {
            animator.SetBool(
                runParameterHash,
                running);
        }
    }

    private void PlayAttackAnimation()
    {
        if (networkAnimator != null && IsSpawned)
        {
            networkAnimator.SetTrigger(attackTriggerHash);
        }
        else if (animator != null)
        {
            animator.SetTrigger(attackTriggerHash);
        }
    }

    #endregion
}
