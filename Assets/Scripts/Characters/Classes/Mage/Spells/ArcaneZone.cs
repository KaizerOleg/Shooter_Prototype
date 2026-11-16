
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class ArcaneZone : NetworkBehaviour
{
    [Header("Diagnostics")]
    [SerializeField] private bool enableDiagnostics = true;

    private Transform owner;
    private float damage;
    private float radius;
    private float height;
    private float duration;
    private float tickInterval;
    private LayerMask targetMask;

    private float elapsed;
    private float nextTickTime;
    private bool configured;

    public void Configure(
        Transform zoneOwner,
        float zoneDamage,
        float zoneRadius,
        float zoneHeight,
        float zoneDuration,
        float zoneTickInterval,
        LayerMask mask)
    {
        owner = zoneOwner;
        damage = Mathf.Max(0f, zoneDamage);
        radius = Mathf.Max(0.1f, zoneRadius);
        height = Mathf.Max(0.1f, zoneHeight);
        duration = Mathf.Max(0.1f, zoneDuration);
        tickInterval = Mathf.Max(0.1f, zoneTickInterval);
        targetMask = mask;

        elapsed = 0f;
        nextTickTime = 0f;
        configured = true;

        transform.rotation = Quaternion.identity;

        transform.localScale = new Vector3(
            radius * 2f,
            height * 0.5f,
            radius * 2f);

        if (enableDiagnostics)
            Debug.Log("[ArcaneZone] Налаштована.", this);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer && !configured)
        {
            Debug.LogError(
                "[ArcaneZone] Зона заспавнена без Configure.",
                this);
        }
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned || !configured)
            return;

        elapsed += Time.deltaTime;

        if (elapsed >= duration)
        {
            NetworkObject.Despawn(true);
            return;
        }

        if (Time.time >= nextTickTime)
        {
            nextTickTime = Time.time + tickInterval;
            ApplyTickDamage();
        }
    }

    private void ApplyTickDamage()
    {
        Collider[] colliders = Physics.OverlapSphere(
            transform.position,
            radius,
            targetMask,
            QueryTriggerInteraction.Ignore);

        HashSet<IDamageable> damagedTargets = new();

        foreach (Collider hitCollider in colliders)
        {
            if (hitCollider == null)
                continue;

            Vector3 targetPosition = hitCollider.bounds.center;

            if (Mathf.Abs(
                targetPosition.y - transform.position.y) > height * 0.5f)
                continue;

            if (owner != null &&
                (hitCollider.transform == owner ||
                 hitCollider.transform.IsChildOf(owner)))
                continue;

            IDamageable target =
                hitCollider.GetComponentInParent<IDamageable>();

            if (target == null ||
                !target.IsAlive ||
                !damagedTargets.Add(target))
                continue;

            target.ApplyDamage(
                Mathf.Max(0, Mathf.RoundToInt(damage)));
        }

        if (enableDiagnostics)
        {
            Debug.Log(
                $"[ArcaneZone] Tick. Пошкоджено цілей: {damagedTargets.Count}",
                this);
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.1f, 0.5f, 1f, 0.5f);

        Gizmos.DrawWireSphere(
            transform.position,
            radius);
    }
#endif
}
