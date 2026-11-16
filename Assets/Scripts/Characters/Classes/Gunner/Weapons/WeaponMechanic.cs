
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class WeaponMechanic : ClassMechanic
{
    public override string MechanicId => "weapon";

    [Header("Network")]
    [SerializeField] private NetworkObject gunnerNetworkPrefab;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Camera aimCamera;

    private CharacterRoot character;
    private WeaponController activeWeapon;
    private GunnerNetworkObject gunnerObject;

    public PlayerWeaponProgression Progression =>
        gunnerObject != null ? gunnerObject.Progression : null;

    private InputAction fireAction;
    private InputAction reloadAction;
    private InputAction nextWeaponAction;

    private bool enabledFireHere;
    private bool enabledReloadHere;
    private bool enabledNextHere;

    private bool isFireHeld;
    private bool weaponSelectionRequested;
    private int weaponIndex;

    public override void Initialize(
        CharacterRoot owner,
        CharacterDefinition definition)
    {
        base.Initialize(owner, definition);

        character = owner;

        if (owner.IsServer)
            SpawnGunnerNetworkObject();

        if (!owner.IsOwner)
            return;

        if (inputActions == null)
        {
            Debug.LogError(
                "[WeaponMechanic] Не призначено InputActionAsset.", this);
            return;
        }

        if (aimCamera == null)
            aimCamera = Camera.main;

        fireAction = inputActions.FindAction("Player/Fire", false);
        reloadAction = inputActions.FindAction("Player/Reload", false);
        nextWeaponAction = inputActions.FindAction("Player/NextWeapon", false);

        if (aimCamera == null ||
            fireAction == null ||
            reloadAction == null ||
            nextWeaponAction == null)
        {
            Debug.LogError(
                "[WeaponMechanic] Перевір камеру та дії Fire, Reload, NextWeapon.",
                this);
            return;
        }

        if (!fireAction.enabled)
        {
            fireAction.Enable();
            enabledFireHere = true;
        }

        if (!reloadAction.enabled)
        {
            reloadAction.Enable();
            enabledReloadHere = true;
        }

        if (!nextWeaponAction.enabled)
        {
            nextWeaponAction.Enable();
            enabledNextHere = true;
        }

        fireAction.started += OnFireStarted;
        fireAction.canceled += OnFireCanceled;
        reloadAction.performed += OnReload;
        nextWeaponAction.performed += OnNextWeapon;

        weaponIndex = 0;

        Debug.Log(
            $"[WeaponMechanic] Initialized. Owner={owner.IsOwner}.",
            this);
    }

    private void SpawnGunnerNetworkObject()
    {
        if (character == null || !character.IsServer)
            return;

        if (gunnerNetworkPrefab == null)
        {
            Debug.LogError(
                "[WeaponMechanic] Не призначено Gunner Network Prefab.",
                this);
            return;
        }

        GameObject instance = Instantiate(
            gunnerNetworkPrefab.gameObject,
            character.transform.position,
            character.transform.rotation);

        NetworkObject networkObject =
            instance.GetComponent<NetworkObject>();

        GunnerNetworkObject wrapper =
            instance.GetComponent<GunnerNetworkObject>();

        if (networkObject == null || wrapper == null)
        {
            Debug.LogError(
                "[WeaponMechanic] Gunner Prefab повинен містити " +
                "NetworkObject і GunnerNetworkObject.",
                instance);

            Destroy(instance);
            return;
        }

        networkObject.SpawnWithOwnership(character.OwnerClientId);

        if (!networkObject.TrySetParent(character.NetworkObject, false))
        {
            Debug.LogError(
                "[WeaponMechanic] Не вдалося прикріпити GunnerNetworkObject.",
                instance);

            networkObject.Despawn(true);
            return;
        }

        networkObject.transform.localPosition = Vector3.zero;
        networkObject.transform.localRotation = Quaternion.identity;

        gunnerObject = wrapper;
    }

    private void Update()
    {
        if (character == null || !character.IsOwner)
            return;

        if (gunnerObject == null || Progression == null ||
            !Progression.IsSpawned)
        {
            TryFindGunnerObject();
            return;
        }

        if (!weaponSelectionRequested)
        {
            character.RequestWeaponSelection(weaponIndex);
            weaponSelectionRequested = true;
        }

        if (!character.TryGetActiveWeapon(out WeaponController foundWeapon))
        {
            activeWeapon = null;
            return;
        }

        activeWeapon = foundWeapon;

        if (aimCamera == null)
            return;

        Vector3 localForward =
            character.transform.InverseTransformDirection(
                aimCamera.transform.forward);

        float pitch = Mathf.Atan2(
            localForward.y,
            new Vector2(localForward.x, localForward.z).magnitude
        ) * Mathf.Rad2Deg;

        activeWeapon.SetAimPitch(pitch);

        if (isFireHeld && activeWeapon.IsAutomatic)
            TryFire();
    }

    private void TryFindGunnerObject()
    {
        if (character == null)
            return;

        GunnerNetworkObject found =
            character.GetComponentInChildren<GunnerNetworkObject>(true);

        if (found == null || found.Progression == null ||
            !found.NetworkObject.IsSpawned)
            return;

        gunnerObject = found;
    }

    private void OnFireStarted(InputAction.CallbackContext context)
    {
        isFireHeld = true;
        TryFire();
    }

    private void OnFireCanceled(InputAction.CallbackContext context)
    {
        isFireHeld = false;
    }

    private void TryFire()
    {
        if (activeWeapon == null || aimCamera == null)
            return;

        activeWeapon.RequestFire(
            aimCamera.transform.position,
            aimCamera.transform.forward);
    }

    private void OnReload(InputAction.CallbackContext context)
    {
        activeWeapon?.RequestReload();
    }

    private void OnNextWeapon(InputAction.CallbackContext context)
    {
        if (character == null || character.WeaponCount == 0)
            return;

        isFireHeld = false;

        weaponIndex = (weaponIndex + 1) % character.WeaponCount;
        character.RequestWeaponSelection(weaponIndex);
    }

    public override void Shutdown()
    {
        isFireHeld = false;
        weaponSelectionRequested = false;

        if (fireAction != null)
        {
            fireAction.started -= OnFireStarted;
            fireAction.canceled -= OnFireCanceled;
        }

        if (reloadAction != null)
            reloadAction.performed -= OnReload;

        if (nextWeaponAction != null)
            nextWeaponAction.performed -= OnNextWeapon;

        if (enabledFireHere)
            fireAction?.Disable();

        if (enabledReloadHere)
            reloadAction?.Disable();

        if (enabledNextHere)
            nextWeaponAction?.Disable();

        if (character != null && character.IsServer &&
            gunnerObject != null &&
            gunnerObject.NetworkObject != null &&
            gunnerObject.NetworkObject.IsSpawned)
        {
            gunnerObject.NetworkObject.Despawn(true);
        }

        gunnerObject = null;
        character = null;
        activeWeapon = null;

        base.Shutdown();
    }
}
