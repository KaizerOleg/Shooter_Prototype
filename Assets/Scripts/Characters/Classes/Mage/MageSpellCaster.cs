
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class MageSpellCaster : NetworkBehaviour
{
    #region Inspector Settings

    [Header("Validation")]
    [SerializeField, Min(0.5f)]
    private float maxCastOriginDistance = 3f;

    [Header("Projectile Spawn")]
    [SerializeField, Min(0f)]
    private float spawnForwardOffset = 1.2f;

    [Header("Target Detection")]
    [SerializeField]
    private LayerMask targetMask = ~0;

    [SerializeField, Min(0.05f)]
    private float lightningDetectionRadius = 0.5f;

    [Header("Diagnostics")]
    [SerializeField]
    private bool enableDiagnostics = true;

    #endregion

    #region Runtime References

    private PlayerSpellProgression progression;
    private SpellLoadout loadout;
    private PlayerMana mana;
    private CharacterRoot character;

    #endregion

    #region Runtime State

    private readonly SpellRuntime[] runtimes =
        new SpellRuntime[SpellLoadout.SlotCount];

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        if (!IsServer || !IsSpawned || character != null)
            return;

        character = GetComponentInParent<CharacterRoot>();
    }

    #endregion

    #region Network Lifecycle

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        progression = GetComponent<PlayerSpellProgression>();
        loadout = GetComponent<SpellLoadout>();
        mana = GetComponent<PlayerMana>();

        if (IsServer)
            character = GetComponentInParent<CharacterRoot>();

        Log(
            $"OnNetworkSpawn. Server={IsServer}, Owner={OwnerClientId}, " +
            $"Progression={progression != null}, Loadout={loadout != null}, " +
            $"Mana={mana != null}");
    }

    public override void OnNetworkDespawn()
    {
        progression = null;
        loadout = null;
        mana = null;
        character = null;

        for (int i = 0; i < runtimes.Length; i++)
            runtimes[i] = null;

        base.OnNetworkDespawn();
    }

    #endregion

    #region Diagnostics

    private void Log(string message)
    {
        if (enableDiagnostics)
            Debug.Log($"[MageSpellCaster] {message}", this);
    }

    private void Warn(string message)
    {
        if (enableDiagnostics)
            Debug.LogWarning($"[MageSpellCaster] {message}", this);
    }

    private void Error(string message)
    {
        Debug.LogError($"[MageSpellCaster] {message}", this);
    }

    #endregion

    #region Spell Casting API

    public void RequestCast(int slot, Vector3 origin, Vector3 direction)
    {
        if (!IsSpawned || !IsOwner)
            return;

        if (slot < 0 || slot >= SpellLoadout.SlotCount)
            return;

        if (!IsFinite(origin) ||
            !IsFinite(direction) ||
            direction.sqrMagnitude < 0.001f)
            return;

        CastSpellServerRpc(slot, origin, direction.normalized);
    }

    public void RequestCastArea(
        int slot,
        Vector3 origin,
        Vector3 direction)
    {
        if (!IsSpawned || !IsOwner)
            return;

        if (slot < 0 || slot >= SpellLoadout.SlotCount)
            return;

        if (!IsFinite(origin) ||
            !IsFinite(direction) ||
            direction.sqrMagnitude < 0.001f)
            return;

        CastAreaServerRpc(slot, origin, direction.normalized);
    }

    #endregion

    #region Spell Casting RPC

    [ServerRpc]
    private void CastSpellServerRpc(
        int slot,
        Vector3 requestedOrigin,
        Vector3 requestedDirection)
    {
        if (!IsServer || !IsSpawned)
            return;

        if (!TryGetSpell(slot, out SpellDefinition spell, out SpellRuntime runtime))
            return;

        if (spell.Type == SpellType.Area)
        {
            Warn("Area-закляття повинно викликатися через RequestCastArea.");
            return;
        }

        if (!IsValidAim(requestedOrigin, requestedDirection))
            return;

        if (!CanPayForSpell(runtime))
            return;

        if (!ValidateSpellPrefab(spell))
            return;

        if (!runtime.TryStartCast(Time.time))
            return;

        if (!mana.TrySpend(runtime.ManaCost))
            return;

        switch (spell.Type)
        {
            case SpellType.Projectile:
                CastProjectile(
                    spell,
                    runtime,
                    requestedOrigin,
                    requestedDirection);
                break;

            case SpellType.ChainLightning:
                CastChainLightning(
                    runtime,
                    requestedOrigin,
                    requestedDirection);
                break;
        }
    }

    [ServerRpc]
    private void CastAreaServerRpc(
        int slot,
        Vector3 requestedOrigin,
        Vector3 requestedDirection)
    {
        if (!IsServer || !IsSpawned)
            return;

        if (!TryGetSpell(slot, out SpellDefinition spell, out SpellRuntime runtime))
            return;

        if (spell.Type != SpellType.Area)
            return;

        if (!IsValidAim(requestedOrigin, requestedDirection))
            return;

        if (!TryCalculateAreaPosition(
            requestedOrigin,
            requestedDirection,
            runtime.Range,
            out Vector3 spawnPosition,
            out _))
        {
            Warn("Не вдалося знайти поверхню для Area-закляття.");
            return;
        }

        if (!CanPayForSpell(runtime))
            return;

        if (!ValidateSpellPrefab(spell))
            return;

        if (!runtime.TryStartCast(Time.time))
            return;

        if (!mana.TrySpend(runtime.ManaCost))
            return;

        CastAreaAtPosition(spell, runtime, spawnPosition);
    }

    #endregion

    #region Spell Resolution

    private bool TryGetSpell(
        int slot,
        out SpellDefinition spell,
        out SpellRuntime runtime)
    {
        spell = null;
        runtime = null;

        if (slot < 0 || slot >= SpellLoadout.SlotCount)
            return false;

        if (character == null)
            character = GetComponentInParent<CharacterRoot>();

        if (character == null)
            return false;

        if (progression == null)
            progression = GetComponent<PlayerSpellProgression>();

        if (loadout == null)
            loadout = GetComponent<SpellLoadout>();

        if (mana == null)
            mana = GetComponent<PlayerMana>();

        if (progression == null || loadout == null || mana == null)
            return false;

        string spellId = loadout.GetSpellId(slot);

        if (string.IsNullOrWhiteSpace(spellId))
            return false;

        spell = progression.GetSpellDefinition(spellId);

        if (spell == null)
        {
            Warn($"SpellDefinition не знайдено: {spellId}");
            return false;
        }

        runtime = runtimes[slot];

        if (runtime == null || runtime.Data != spell)
        {
            runtime = progression.CreateSpellRuntime(spellId);
            runtimes[slot] = runtime;
        }

        return runtime != null;
    }

    private bool CanPayForSpell(SpellRuntime runtime)
    {
        return runtime != null &&
               runtime.CanCast(Time.time) &&
               mana != null &&
               mana.CanSpend(runtime.ManaCost);
    }

    private bool ValidateSpellPrefab(SpellDefinition spell)
    {
        if (spell.Type == SpellType.ChainLightning)
            return true;

        if (spell.SpellPrefab == null)
        {
            Warn($"Для {spell.SpellId} не призначено SpellPrefab.");
            return false;
        }

        if (spell.Type == SpellType.Projectile)
        {
            if (spell.SpellPrefab.GetComponent<NetworkObject>() == null ||
                spell.SpellPrefab.GetComponent<FireballProjectile>() == null)
            {
                Error(
                    $"Projectile prefab {spell.SpellId} повинен мати " +
                    "NetworkObject та FireballProjectile на корені.");
                return false;
            }
        }

        if (spell.Type == SpellType.Area)
        {
            if (spell.SpellPrefab.GetComponent<NetworkObject>() == null ||
                spell.SpellPrefab.GetComponent<ArcaneZone>() == null)
            {
                Error(
                    $"Area prefab {spell.SpellId} повинен мати " +
                    "NetworkObject та ArcaneZone на корені.");
                return false;
            }
        }

        return true;
    }

    #endregion

    #region Projectile Spell

    private void CastProjectile(
        SpellDefinition spell,
        SpellRuntime runtime,
        Vector3 origin,
        Vector3 direction)
    {
        Vector3 spawnPosition =
            origin + direction * spawnForwardOffset;

        Quaternion rotation =
            Quaternion.LookRotation(direction, Vector3.up);

        GameObject instance = Instantiate(
            spell.SpellPrefab,
            spawnPosition,
            rotation);

        FireballProjectile projectile =
            instance.GetComponent<FireballProjectile>();

        NetworkObject networkObject =
            instance.GetComponent<NetworkObject>();

        if (projectile == null || networkObject == null)
        {
            Destroy(instance);
            return;
        }

        projectile.Configure(
            character.transform,
            runtime.Damage,
            runtime.AreaRadius,
            runtime.ProjectileSpeed,
            runtime.Range);

        networkObject.SpawnWithOwnership(OwnerClientId);

        Log($"Projectile заспавнений: {spell.SpellId}");
    }

    #endregion

    #region Area Spell

    private bool TryCalculateAreaPosition(
        Vector3 origin,
        Vector3 direction,
        float range,
        out Vector3 groundPosition,
        out Vector3 groundNormal)
    {
        groundPosition = Vector3.zero;
        groundNormal = Vector3.up;

        Vector3 targetPosition = origin + direction * range;

        if (Physics.Raycast(
            origin,
            direction,
            out RaycastHit aimHit,
            range,
            targetMask,
            QueryTriggerInteraction.Ignore))
        {
            targetPosition = aimHit.point;
        }

        return TryFindGroundBelow(
            targetPosition,
            out groundPosition,
            out groundNormal);
    }

    private bool TryFindGroundBelow(
        Vector3 targetPosition,
        out Vector3 groundPosition,
        out Vector3 groundNormal)
    {
        groundPosition = Vector3.zero;
        groundNormal = Vector3.up;

        Ray ray = new Ray(
            targetPosition + Vector3.up * 20f,
            Vector3.down);

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            40f,
            targetMask,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (IsCharacterCollider(hit.collider))
                continue;

            if (hit.normal.y < 0.3f)
                continue;

            groundPosition = hit.point;
            groundNormal = hit.normal.normalized;

            return true;
        }

        return false;
    }

    private bool IsCharacterCollider(Collider targetCollider)
    {
        if (targetCollider == null)
            return true;

        if (targetCollider.GetComponentInParent<EnemyAI>() != null)
            return true;

        if (targetCollider.GetComponentInParent<CharacterRoot>() != null)
            return true;

        return false;
    }

    private void CastAreaAtPosition(
        SpellDefinition spell,
        SpellRuntime runtime,
        Vector3 groundPosition)
    {
        float height = runtime.AreaHeight;

        Vector3 spawnPosition =
            groundPosition + Vector3.up * (height * 0.5f);

        GameObject instance = Instantiate(
            spell.SpellPrefab,
            spawnPosition,
            Quaternion.identity);

        ArcaneZone zone = instance.GetComponent<ArcaneZone>();
        NetworkObject networkObject = instance.GetComponent<NetworkObject>();

        if (zone == null || networkObject == null)
        {
            Destroy(instance);
            Error("ArcaneZone prefab має неправильні компоненти.");
            return;
        }

        zone.Configure(
            character.transform,
            runtime.Damage,
            runtime.AreaRadius,
            runtime.AreaHeight,
            runtime.Duration,
            runtime.TickInterval,
            targetMask);

        networkObject.SpawnWithOwnership(OwnerClientId);

        Log($"Arcane Zone створена: {spell.SpellId}");
    }

    #endregion

    #region Chain Lightning

    private void CastChainLightning(
        SpellRuntime runtime,
        Vector3 origin,
        Vector3 direction)
    {
        List<IDamageable> damagedTargets = new();
        List<Vector3> visualPoints = new();

        RaycastHit[] hits = Physics.SphereCastAll(
            origin,
            lightningDetectionRadius,
            direction,
            runtime.Range,
            targetMask,
            QueryTriggerInteraction.Ignore);

        Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance));

        IDamageable firstTarget = null;
        Vector3 firstPosition = Vector3.zero;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (!TryGetEnemyTarget(
                hit.collider,
                out IDamageable target,
                out Vector3 targetPosition))
                continue;

            firstTarget = target;
            firstPosition = targetPosition;
            break;
        }

        if (firstTarget == null)
        {
            Warn("Chain Lightning: ворога в напрямку прицілювання не знайдено.");
            return;
        }

        Vector3 casterPosition =
            character.transform.position + Vector3.up * 1.2f;

        visualPoints.Add(casterPosition);

        IDamageable currentTarget = firstTarget;
        Vector3 currentPosition = firstPosition;

        for (int i = 0; i < runtime.ChainCount; i++)
        {
            if (currentTarget == null || !currentTarget.IsAlive)
                break;

            if (damagedTargets.Contains(currentTarget))
                break;

            damagedTargets.Add(currentTarget);
            visualPoints.Add(currentPosition);

            currentTarget.ApplyDamage(
                Mathf.Max(0, Mathf.RoundToInt(runtime.Damage)));

            Log(
                $"Lightning hit: {((Component)currentTarget).name}, " +
                $"Damage={runtime.Damage}");

            IDamageable nextTarget = FindNearestUndamagedTarget(
                currentPosition,
                runtime.ChainJumpRange,
                damagedTargets,
                out Vector3 nextPosition);

            if (nextTarget == null)
                break;

            currentTarget = nextTarget;
            currentPosition = nextPosition;
        }

        if (visualPoints.Count >= 2)
            ShowLightningClientRpc(visualPoints.ToArray());

        Log(
            $"Chain Lightning завершено. " +
            $"Пошкоджено ворогів: {damagedTargets.Count}");
    }

    private bool TryGetEnemyTarget(
        Collider hitCollider,
        out IDamageable target,
        out Vector3 targetPosition)
    {
        target = null;
        targetPosition = Vector3.zero;

        if (hitCollider == null)
            return false;

        EnemyAI enemy = hitCollider.GetComponentInParent<EnemyAI>();

        if (enemy == null)
            return false;

        Health health = enemy.GetComponent<Health>();

        if (health == null || !health.IsAlive)
            return false;

        target = health;
        targetPosition = health.transform.position;

        return true;
    }

    private IDamageable FindNearestUndamagedTarget(
        Vector3 position,
        float radius,
        List<IDamageable> alreadyDamaged,
        out Vector3 targetPosition)
    {
        targetPosition = Vector3.zero;

        Collider[] colliders = Physics.OverlapSphere(
            position,
            radius,
            targetMask,
            QueryTriggerInteraction.Ignore);

        IDamageable bestTarget = null;
        float bestDistance = float.MaxValue;

        foreach (Collider collider in colliders)
        {
            if (!TryGetEnemyTarget(
                collider,
                out IDamageable candidate,
                out Vector3 candidatePosition))
                continue;

            if (alreadyDamaged.Contains(candidate))
                continue;

            float distance = Vector3.Distance(
                position,
                candidatePosition);

            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestTarget = candidate;
            targetPosition = candidatePosition;
        }

        return bestTarget;
    }

    #endregion

    #region Lightning Visuals

    [ClientRpc]
    private void ShowLightningClientRpc(Vector3[] points)
    {
        if (points == null || points.Length < 2)
            return;

        GameObject visualObject =
            new GameObject("ChainLightningVisual");

        ChainLightningVisual visual =
            visualObject.AddComponent<ChainLightningVisual>();

        visual.Initialize(points);
    }

    #endregion

    #region Aim Validation

    private bool IsValidAim(Vector3 origin, Vector3 direction)
    {
        if (!IsFinite(origin) || !IsFinite(direction))
            return false;

        if (direction.sqrMagnitude < 0.9f ||
            direction.sqrMagnitude > 1.1f)
            return false;

        if (character == null)
            return false;

        return Vector3.Distance(
            character.transform.position,
            origin) <= maxCastOriginDistance;
    }

    private bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x) &&
               float.IsFinite(value.y) &&
               float.IsFinite(value.z);
    }

    #endregion
}
