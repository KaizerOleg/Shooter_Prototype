using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class WeaponController : NetworkBehaviour
{
    #region Inspector Settings

    [Header("Server validation")]
    [SerializeField, Min(0.5f)]
    private float maxAimOriginDistance = 3f;

    [Header("Fire Effects")]
    [SerializeField]
    private ParticleSystem muzzleFlash;

    [SerializeField]
    private bool detachMuzzleFlash = true;

    [Header("Firing")]
    [SerializeField]
    private Transform muzzlePoint;

    [SerializeField]
    private float aimRayMaxDistance = 500f;

    [SerializeField, Min(0f)]
    private float recoilDistance = 0.08f;

    [SerializeField, Min(0.01f)]
    private float recoilDuration = 0.1f;

    #endregion

    #region Network State

    private readonly NetworkVariable<FixedString64Bytes> weaponId = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> currentAmmo = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> isReloading = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<float> aimPitch = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> magazineSize = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    #endregion

    #region Runtime References

    private WeaponDefinition weaponDefinition;
    private WeaponRuntime runtime;

    private PlayerWeaponProgression progression;

    private Transform weaponVisual;
    private Quaternion visualBaseRotation = Quaternion.identity;

    #endregion

    #region Runtime State

    private Vector3 visualBasePosition;

    private float recoilTimer;

    private float lastSentPitch;
    private float nextAimSyncTime;
    private float nextLocalFireTime;

    #endregion

    #region Properties

    public Transform MuzzlePoint => muzzlePoint;

    public WeaponDefinition Data => weaponDefinition;

    public int CurrentAmmo => currentAmmo.Value;

    public int MaxAmmo => magazineSize.Value;

    public bool IsReloading => isReloading.Value;

    public bool IsConfigured => weaponDefinition != null;

    public bool IsAutomatic =>
        weaponDefinition != null && weaponDefinition.IsAutomatic;

    #endregion

    #region Network Lifecycle

    public override void OnNetworkSpawn()
    {
        weaponId.OnValueChanged += OnWeaponIdChanged;
        aimPitch.OnValueChanged += OnAimPitchChanged;

        weaponVisual = transform.Find("Visual");

        if (weaponVisual == null)
        {
            Debug.LogError(
                "[WeaponController] Не знайдено дочірній об'єкт Visual.",
                this);
        }
        else
        {
            visualBaseRotation = weaponVisual.localRotation;
        }

        if (weaponVisual != null)
        {
            visualBaseRotation = weaponVisual.localRotation;
            visualBasePosition = weaponVisual.localPosition;
        }

        if (!weaponId.Value.IsEmpty)
            ResolveWeapon(weaponId.Value);

        // Батько може синхронізуватися пізніше.
        TryAttachVisualToWeaponHolder();

        ApplyAimPitch(aimPitch.Value);

        if (IsServer && weaponDefinition != null)
            CreateRuntime();
    }

    public override void OnNetworkObjectParentChanged(
        NetworkObject parentNetworkObject)
    {
        TryAttachVisualToWeaponHolder();

        if (!weaponId.Value.IsEmpty)
            ResolveWeapon(weaponId.Value);
    }

    public override void OnNetworkDespawn()
    {
        // Модель не повинна залишитися в руці після despawn.
        if (weaponVisual != null && weaponVisual.parent != transform)
            weaponVisual.SetParent(transform, true);

        weaponId.OnValueChanged -= OnWeaponIdChanged;
        aimPitch.OnValueChanged -= OnAimPitchChanged;

        runtime = null;
        weaponDefinition = null;
        progression = null;
    }

    #endregion

    #region Weapon Configuration

    public void ConfigureServer(
        WeaponDefinition definition,
        PlayerWeaponProgression playerProgression)
    {
        if (!IsServer || definition == null || playerProgression == null)
            return;

        weaponDefinition = definition;
        progression = playerProgression;

        if (definition.Strategy == null)
        {
            Debug.LogError(
                "[WeaponController] Не призначено AttackStrategy.",
                this);
            return;
        }

        CreateRuntime();

        weaponId.Value = new FixedString64Bytes(definition.WeaponId);
        SyncRuntimeState();
    }

    private void ResolveWeapon(FixedString64Bytes id)
    {
        Transform parent = transform.parent;

        if (parent == null)
            return;

        NetworkObject parentNetworkObject =
            parent.GetComponent<NetworkObject>();

        if (parentNetworkObject == null)
            return;

        CharacterRoot character =
            parentNetworkObject.GetComponent<CharacterRoot>();

        if (character == null)
        {
            Debug.LogError(
                "[WeaponController] Не знайдено CharacterRoot.",
                this);
            return;
        }

        weaponDefinition =
            character.GetWeaponDefinition(id.ToString());

        if (weaponDefinition == null)
        {
            Debug.LogError(
                $"[WeaponController] Не знайдено WeaponDefinition для '{id}'.",
                this);
            return;
        }

        if (IsServer && runtime == null)
            CreateRuntime();
    }

    private void CreateRuntime()
    {
        if (weaponDefinition == null || progression == null)
            return;

        if (weaponDefinition.Strategy == null)
        {
            Debug.LogError(
                "[WeaponController] Не призначено AttackStrategy.",
                this);
            return;
        }

        runtime = new WeaponRuntime(weaponDefinition);

        foreach (WeaponUpgradeData upgrade in weaponDefinition.Upgrades)
        {
            if (upgrade == null)
                continue;

            int level = progression.GetUpgradeLevel(
                weaponDefinition.WeaponId,
                upgrade.UpgradeId);

            if (level > 0)
                runtime.RestoreUpgradeLevel(upgrade.UpgradeId, level);
        }
    }

    public void ApplyProgressionUpgrade(string upgradeId, int newLevel)
    {
        if (!IsServer ||
            runtime == null ||
            weaponDefinition == null)
            return;

        if (!runtime.RestoreUpgradeLevel(upgradeId, newLevel))
            return;

        SyncRuntimeState();
    }

    #endregion

    #region Weapon Visual Attachment

    private void TryAttachVisualToWeaponHolder()
    {
        if (weaponVisual == null)
            weaponVisual = transform.Find("Visual");

        if (weaponVisual == null)
            return;

        Transform parent = transform.parent;

        if (parent == null)
            return;

        NetworkObject parentNetworkObject =
            parent.GetComponent<NetworkObject>();

        if (parentNetworkObject == null)
            return;

        CharacterRoot character =
            parentNetworkObject.GetComponent<CharacterRoot>();

        if (character == null || character.WeaponHolder == null)
            return;

        Transform holder = character.WeaponHolder;

        if (weaponVisual.parent != holder)
        {
            // Зберігаємо prefab offsets відносно нової точки кріплення.
            weaponVisual.SetParent(holder, false);
        }

        ApplyAimPitch(aimPitch.Value);
    }

    #endregion

    #region Aim Synchronization

    public void SetAimPitch(float pitch)
    {
        if (!IsSpawned || !IsOwner)
            return;

        pitch = Mathf.Clamp(pitch, -85f, 85f);

        // Миттєвий локальний поворот.
        ApplyAimPitch(pitch);

        if (Time.unscaledTime < nextAimSyncTime)
            return;

        if (Mathf.Abs(pitch - lastSentPitch) < 0.5f)
            return;

        lastSentPitch = pitch;
        nextAimSyncTime = Time.unscaledTime + 1f / 15f;

        SetAimPitchServerRpc(pitch);
    }

    [ServerRpc]
    private void SetAimPitchServerRpc(float pitch)
    {
        aimPitch.Value = Mathf.Clamp(pitch, -85f, 85f);
    }

    private void OnAimPitchChanged(float previous, float current)
    {
        ApplyAimPitch(current);
    }

    private void ApplyAimPitch(float pitch)
    {
        if (weaponVisual == null)
            return;

        weaponVisual.localRotation =
            visualBaseRotation *
            Quaternion.Euler(-pitch * 0f, 0f, 0f);
    }

    #endregion

    #region Muzzle Position

    public bool TryGetMuzzlePose(
        out Vector3 origin,
        out Vector3 forward)
    {
        origin = transform.position;
        forward = transform.forward;

        if (muzzlePoint == null)
            return false;

        origin = muzzlePoint.position;
        forward = muzzlePoint.forward;

        return true;
    }

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        UpdateRecoil();

        if (!IsServer || runtime == null)
            return;

        runtime.Update(Time.time);
        SyncRuntimeState();
    }

    #endregion

    #region Firing Effects

    private void PlayFireEffects()
    {
        if (muzzleFlash != null)
            muzzleFlash.Play();

        recoilTimer = recoilDuration;
    }

    [ClientRpc]
    private void FireEffectsClientRpc()
    {
        PlayFireEffects();
    }

    private void UpdateRecoil()
    {
        if (weaponVisual == null)
            return;

        if (recoilTimer > 0f)
        {
            recoilTimer -= Time.deltaTime;

            float t = recoilDuration > 0f
                ? Mathf.Clamp01(recoilTimer / recoilDuration)
                : 0f;

            weaponVisual.localPosition =
                visualBasePosition -
                Vector3.forward * recoilDistance * t;
        }
        else
        {
            weaponVisual.localPosition = visualBasePosition;
        }
    }

    #endregion

    #region Fire Request

    public void RequestFire(
        Vector3 cameraOrigin,
        Vector3 cameraDirection)
    {
        if (!IsSpawned || !IsOwner || !IsConfigured)
            return;

        if (Time.time < nextLocalFireTime)
            return;

        if (CurrentAmmo <= 0 || IsReloading)
            return;

        if (muzzlePoint == null)
        {
            Debug.LogError(
                "[WeaponController] Не призначено MuzzlePoint.",
                this);
            return;
        }

        if (cameraDirection.sqrMagnitude < 0.001f)
            return;

        Vector3 targetPoint =
            cameraOrigin +
            cameraDirection.normalized * weaponDefinition.Range;

        if (Physics.Raycast(
            cameraOrigin,
            cameraDirection.normalized,
            out RaycastHit hit,
            weaponDefinition.Range,
            weaponDefinition.HitMask,
            QueryTriggerInteraction.Ignore))
        {
            targetPoint = hit.point;
        }

        Vector3 direction = targetPoint - muzzlePoint.position;

        if (direction.sqrMagnitude < 0.001f)
            return;

        nextLocalFireTime =
            Time.time + 1f / weaponDefinition.ShotsPerSecond;

        FireServerRpc(targetPoint);
    }

    public void RequestReload()
    {
        if (!IsSpawned || !IsOwner || !IsConfigured)
            return;

        ReloadServerRpc();
    }

    #endregion

    #region Server RPC

    [ServerRpc]
    private void FireServerRpc(Vector3 targetPoint)
    {
        if (runtime == null ||
            weaponDefinition == null ||
            muzzlePoint == null)
            return;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
            OwnerClientId,
            out NetworkClient ownerClient))
            return;

        if (ownerClient.PlayerObject == null)
            return;

        Vector3 origin = muzzlePoint.position;

        if (Vector3.Distance(
            origin,
            ownerClient.PlayerObject.transform.position) > maxAimOriginDistance)
            return;

        Vector3 direction = targetPoint - origin;

        if (direction.sqrMagnitude < 0.001f)
            return;

        direction.Normalize();

        if (!runtime.TryStartFire(Time.time))
        {
            SyncRuntimeState();
            return;
        }

        AttackContext context = new(
            origin,
            direction,
            runtime.Damage,
            runtime.Range,
            gameObject);

        weaponDefinition.Strategy.Execute(context);

        FireEffectsClientRpc();
        SyncRuntimeState();
    }

    [ServerRpc]
    private void ReloadServerRpc()
    {
        if (runtime == null)
            return;

        runtime.TryStartReload(Time.time);
        SyncRuntimeState();
    }

    #endregion

    #region Network State Synchronization

    private void OnWeaponIdChanged(
        FixedString64Bytes previous,
        FixedString64Bytes current)
    {
        if (!current.IsEmpty)
            ResolveWeapon(current);
    }

    private void SyncRuntimeState()
    {
        if (runtime == null)
            return;

        currentAmmo.Value = runtime.CurrentAmmo;
        magazineSize.Value = runtime.MagazineSize;
        isReloading.Value = runtime.IsReloading;
    }

    #endregion
}