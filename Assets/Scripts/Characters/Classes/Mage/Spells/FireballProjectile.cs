using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class FireballProjectile : NetworkBehaviour
{
    #region Inspector Settings

    [Header("Collision")]
    [SerializeField, Min(0.01f)]
    private float collisionRadius = 0.2f;

    [SerializeField]
    private LayerMask collisionMask = ~0;

    [Header("Lifetime")]
    [SerializeField, Min(0.1f)]
    private float maxLifetime = 10f;

    [Header("Explosion Visual")]
    [SerializeField, Min(0.05f)]
    private float explosionVisualDuration = 0.6f;

    [SerializeField, Range(0.05f, 1f)]
    private float explosionVisualAlpha = 0.3f;

    [Header("Diagnostics")]
    [SerializeField]
    private bool enableDiagnostics = true;

    [SerializeField, Min(0.1f)]
    private float movementLogInterval = 1f;

    #endregion

    #region Runtime State

    private Transform owner;

    private float damage;
    private float explosionRadius;
    private float speed;
    private float maxDistance;

    private Vector3 direction;

    private bool configured;
    private bool exploded;

    private float lifetime;
    private float distanceTravelled;
    private float nextMovementLogTime;

    private const int AllLayers = ~0;

    #endregion

    #region Diagnostics

    private void Log(string message)
    {
        if (enableDiagnostics)
            Debug.Log($"[FireballProjectile] {message}", this);
    }

    private void Warn(string message)
    {
        if (enableDiagnostics)
            Debug.LogWarning($"[FireballProjectile] {message}", this);
    }

    private void Error(string message)
    {
        Debug.LogError($"[FireballProjectile] {message}", this);
    }

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        Log(
            $"Awake. Position={transform.position}, " +
            $"Forward={transform.forward}, " +
            $"HasNetworkObject={GetComponent<NetworkObject>() != null}");
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned || !configured || exploded)
            return;

        lifetime += Time.deltaTime;

        if (lifetime >= maxLifetime)
        {
            Explode(transform.position, "MaxLifetime");
            return;
        }

        if (distanceTravelled >= maxDistance)
        {
            Explode(transform.position, "MaxDistance");
            return;
        }

        float step = speed * Time.deltaTime;

        if (step <= 0f)
            return;

        // 1. Спочатку перевіряємо, чи снаряд уже перетинається
        // з будь-яким твердим колайдером.
        if (TryGetOverlappingCollider(
                transform.position,
                out Collider overlapCollider,
                out Vector3 overlapPoint))
        {
            Log(
                $"ВИЯВЛЕНО ПЕРЕКРИТТЯ: {overlapCollider.name}, " +
                $"Point={overlapPoint}");

            Explode(
                overlapPoint,
                $"Overlap:{overlapCollider.name}");

            return;
        }

        // 2. Перевіряємо шлях снаряда перед переміщенням.
        RaycastHit[] hits;

        try
        {
            hits = Physics.SphereCastAll(
                transform.position,
                collisionRadius,
                direction,
                step,
                AllLayers,
                QueryTriggerInteraction.Ignore);
        }
        catch (Exception exception)
        {
            Error($"SphereCastAll: {exception}");
            return;
        }

        RaycastHit nearestHit = default;
        bool hasHit = false;
        float nearestDistance = float.MaxValue;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            Transform hitTransform = hit.collider.transform;

            if (IsProjectileCollider(hitTransform))
                continue;

            if (IsOwnerCollider(hitTransform))
                continue;

            Log(
                $"SphereCast HIT: {hit.collider.name}, " +
                $"Layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}, " +
                $"Distance={hit.distance:F3}, Point={hit.point}");

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestHit = hit;
                hasHit = true;
            }
        }

        // 3. Будь-яке знайдене зіткнення означає вибух.
        if (hasHit)
        {
            Explode(
                nearestHit.point,
                $"Collision:{nearestHit.collider.name}");

            return;
        }

        // 4. Якщо зіткнень немає, продовжуємо рух.
        transform.position += direction * step;
        distanceTravelled += step;

        if (enableDiagnostics && Time.time >= nextMovementLogTime)
        {
            nextMovementLogTime = Time.time + movementLogInterval;

            Log(
                $"Рух. Position={transform.position}, " +
                $"Lifetime={lifetime:F2}, " +
                $"Distance={distanceTravelled:F2}");
        }
    }

    private void OnDestroy()
    {
        Log(
            $"OnDestroy. Configured={configured}, " +
            $"Exploded={exploded}, Position={transform.position}");
    }

    #endregion

    #region Network Lifecycle

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        Log(
            $"OnNetworkSpawn. IsServer={IsServer}, IsClient={IsClient}, " +
            $"IsOwner={IsOwner}, NetworkObjectId={NetworkObjectId}, " +
            $"Configured={configured}");

        if (!configured)
            Error("Снаряд заспавнений без виклику Configure.");
    }

    public override void OnNetworkDespawn()
    {
        Log(
            $"OnNetworkDespawn. Lifetime={lifetime:F2}, " +
            $"Distance={distanceTravelled:F2}, Exploded={exploded}");

        base.OnNetworkDespawn();
    }

    #endregion

    #region Configuration

    public void Configure(
        Transform projectileOwner,
        float projectileDamage,
        float areaRadius,
        float projectileSpeed,
        float range)
    {
        owner = projectileOwner;
        damage = Mathf.Max(0f, projectileDamage);
        explosionRadius = Mathf.Max(0f, areaRadius);
        speed = Mathf.Max(0.1f, projectileSpeed);
        maxDistance = Mathf.Max(0.1f, range);

        direction = transform.forward.normalized;

        lifetime = 0f;
        distanceTravelled = 0f;
        exploded = false;
        nextMovementLogTime = 0f;

        configured = true;

        Log(
            $"Configure завершено. Owner={owner?.name}, " +
            $"Damage={damage}, Radius={explosionRadius}, " +
            $"Speed={speed}, Range={maxDistance}, " +
            $"Direction={direction}");
    }

    #endregion

    #region Collision Detection

    private bool TryGetOverlappingCollider(
        Vector3 position,
        out Collider foundCollider,
        out Vector3 contactPoint)
    {
        foundCollider = null;
        contactPoint = position;

        Collider[] overlaps = Physics.OverlapSphere(
            position,
            collisionRadius,
            AllLayers,
            QueryTriggerInteraction.Ignore);

        float nearestDistance = float.MaxValue;

        foreach (Collider collider in overlaps)
        {
            if (collider == null)
                continue;

            Transform hitTransform = collider.transform;

            if (IsProjectileCollider(hitTransform))
                continue;

            if (IsOwnerCollider(hitTransform))
                continue;

            Vector3 closestPoint = collider.ClosestPoint(position);
            float distance = (closestPoint - position).sqrMagnitude;

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                foundCollider = collider;
                contactPoint = closestPoint;
            }
        }

        return foundCollider != null;
    }

    private bool IsProjectileCollider(Transform hitTransform)
    {
        if (hitTransform == null)
            return false;

        return hitTransform == transform ||
               hitTransform.IsChildOf(transform);
    }

    private bool IsOwnerCollider(Transform hitTransform)
    {
        if (owner == null || hitTransform == null)
            return false;

        return hitTransform == owner ||
               hitTransform.IsChildOf(owner);
    }

    #endregion

    #region Explosion & Damage

    private void Explode(Vector3 position, string reason)
    {
        if (!IsServer || exploded)
            return;

        exploded = true;

        Log(
            $"EXPLODE. Reason={reason}, Position={position}, " +
            $"Damage={damage}, Radius={explosionRadius}");

        ShowExplosionClientRpc(position, explosionRadius);

        Collider[] colliders;

        try
        {
            colliders = Physics.OverlapSphere(
                position,
                explosionRadius,
                collisionMask,
                QueryTriggerInteraction.Ignore);
        }
        catch (Exception exception)
        {
            Error($"OverlapSphere: {exception}");
            colliders = Array.Empty<Collider>();
        }

        HashSet<IDamageable> damagedTargets = new();

        foreach (Collider hitCollider in colliders)
        {
            if (hitCollider == null)
                continue;

            Transform hitTransform = hitCollider.transform;

            if (IsProjectileCollider(hitTransform))
                continue;

            if (IsOwnerCollider(hitTransform))
                continue;

            IDamageable target =
                hitCollider.GetComponentInParent<IDamageable>();

            if (target == null || !target.IsAlive)
                continue;

            if (!damagedTargets.Add(target))
                continue;

            int finalDamage = Mathf.RoundToInt(damage);

            try
            {
                target.ApplyDamage(finalDamage);

                Log(
                    $"Завдано шкоду: Target={hitTransform.name}, " +
                    $"Damage={finalDamage}");
            }
            catch (Exception exception)
            {
                Error($"ApplyDamage: {exception}");
            }
        }

        Log(
            $"Вибух завершено. Пошкоджено цілей: " +
            $"{damagedTargets.Count}");

        if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            try
            {
                NetworkObject.Despawn(true);
            }
            catch (Exception exception)
            {
                Error($"Despawn: {exception}");
            }
        }
    }

    #endregion

    #region Explosion Visuals

    [ClientRpc]
    private void ShowExplosionClientRpc(Vector3 position, float radius)
    {
        if (radius <= 0f)
            return;

        GameObject sphere = GameObject.CreatePrimitive(
            PrimitiveType.Sphere);

        sphere.name = "FireballExplosionVisual";
        sphere.transform.position = position;
        sphere.transform.localScale = Vector3.one * (radius * 2f);

        Collider sphereCollider = sphere.GetComponent<Collider>();

        if (sphereCollider != null)
            Destroy(sphereCollider);

        Renderer sphereRenderer = sphere.GetComponent<Renderer>();

        if (sphereRenderer != null)
        {
            sphereRenderer.shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            sphereRenderer.receiveShadows = false;

            Material material = CreateTransparentRedMaterial();

            if (material != null)
                sphereRenderer.material = material;
        }

        FireballExplosionVisual visual =
            sphere.AddComponent<FireballExplosionVisual>();

        visual.Initialize(
            explosionVisualDuration,
            explosionVisualAlpha);

        Log($"Візуалізація вибуху створена. Radius={radius}");
    }

    private Material CreateTransparentRedMaterial()
    {
        Shader shader =
            Shader.Find("Universal Render Pipeline/Unlit");

        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
            shader = Shader.Find("Standard");

        if (shader == null)
        {
            Warn("Не знайдено Shader для прозорого матеріалу.");
            return null;
        }

        Material material = new Material(shader);

        Color red = new Color(
            1f,
            0.05f,
            0.05f,
            explosionVisualAlpha);

        material.color = red;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", red);

        if (material.HasProperty("_Color"))
            material.SetColor("_Color", red);

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);

        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);

        if (material.HasProperty("_SrcBlend"))
            material.SetInt(
                "_SrcBlend",
                (int)UnityEngine.Rendering.BlendMode.SrcAlpha);

        if (material.HasProperty("_DstBlend"))
            material.SetInt(
                "_DstBlend",
                (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 0);

        material.SetOverrideTag("RenderType", "Transparent");

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");

        material.renderQueue =
            (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (shader.name == "Standard")
        {
            material.SetFloat("_Mode", 3f);
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        return material;
    }

    #endregion
}

#region Fireball Explosion Visual

public sealed class FireballExplosionVisual : MonoBehaviour
{
    #region Runtime State

    private Renderer targetRenderer;
    private MaterialPropertyBlock propertyBlock;

    private float duration;
    private float elapsed;
    private float initialAlpha;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        targetRenderer = GetComponent<Renderer>();
        propertyBlock = new MaterialPropertyBlock();
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        float progress = Mathf.Clamp01(elapsed / duration);
        float alpha = Mathf.Lerp(initialAlpha, 0f, progress);

        UpdateAlpha(alpha);

        if (elapsed >= duration)
            Destroy(gameObject);
    }

    #endregion

    #region Initialization

    public void Initialize(float visualDuration, float alpha)
    {
        duration = Mathf.Max(0.05f, visualDuration);
        initialAlpha = Mathf.Clamp01(alpha);
        elapsed = 0f;

        UpdateAlpha(initialAlpha);
    }

    #endregion

    #region Visual Update

    private void UpdateAlpha(float alpha)
    {
        if (targetRenderer == null)
            return;

        targetRenderer.GetPropertyBlock(propertyBlock);

        Color color = new Color(1f, 0.05f, 0.05f, alpha);

        propertyBlock.SetColor("_BaseColor", color);
        propertyBlock.SetColor("_Color", color);

        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    #endregion
}

#endregion