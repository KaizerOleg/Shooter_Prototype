
using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(ClassMechanicInstaller))]
[RequireComponent(typeof(PlayerUpgradeWallet))]
public sealed class CharacterRoot : NetworkBehaviour
{
    #region Inspector Settings

    [Header("Database")]
    [SerializeField] private CharacterDatabase characterDatabase;
    [SerializeField] private WeaponDatabase weaponDatabase;

    [Header("Weapon Attachment")]
    [SerializeField] private Transform weaponHolder;

    #endregion

    #region Properties

    public Transform WeaponHolder => weaponHolder;
    public CharacterDefinition Definition => definition;
    public string ClassId => classId.Value.ToString();
    public bool HasSelectedClass => classId.Value.Length > 0;

    public int WeaponCount =>
        weaponDatabase != null && weaponDatabase.Weapons != null
            ? weaponDatabase.Weapons.Count
            : 0;

    #endregion

    #region Network State

    private readonly NetworkVariable<FixedString64Bytes> classId = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<NetworkObjectReference> activeWeaponReference =
        new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    #endregion

    #region Runtime State

    private ClassMechanicInstaller mechanicInstaller;
    private NetworkObject activeWeaponObject;

    private CharacterDefinition definition;
    private bool mechanicsInitialized;

    #endregion

    #region Events

    public event Action<CharacterDefinition> ClassApplied;

    #endregion

    #region Network Lifecycle

    public override void OnNetworkSpawn()
    {
        mechanicInstaller = GetComponent<ClassMechanicInstaller>();

        if (characterDatabase == null)
        {
            Debug.LogError(
                "[CharacterRoot] Не призначено CharacterDatabase.", this);
            return;
        }

        classId.OnValueChanged += OnClassIdChanged;

        if (!classId.Value.IsEmpty)
            ApplyClass(classId.Value);

        if (IsServer && !HasSelectedClass)
        {
            Debug.Log(
                $"[CharacterRoot] {name} очікує вибору класу.", this);
        }
    }

    public override void OnNetworkDespawn()
    {
        classId.OnValueChanged -= OnClassIdChanged;

        if (mechanicInstaller != null)
            mechanicInstaller.Shutdown();

        if (IsServer && activeWeaponObject != null &&
            activeWeaponObject.IsSpawned)
        {
            activeWeaponObject.Despawn(true);
        }

        activeWeaponObject = null;
        definition = null;
        mechanicsInitialized = false;
    }

    #endregion

    #region Weapon Database

    public WeaponDefinition GetWeaponAt(int index)
    {
        if (weaponDatabase == null ||
            weaponDatabase.Weapons == null ||
            index < 0 ||
            index >= weaponDatabase.Weapons.Count)
            return null;

        return weaponDatabase.GetAt(index);
    }

    public WeaponDefinition GetWeaponDefinition(string id)
    {
        return weaponDatabase != null
            ? weaponDatabase.GetById(id)
            : null;
    }

    #endregion

    #region Class Selection

    public void RequestClassSelection(string requestedClassId)
    {
        if (!IsSpawned || !IsOwner ||
            string.IsNullOrWhiteSpace(requestedClassId))
            return;

        requestedClassId = requestedClassId.Trim().ToLowerInvariant();

        if (requestedClassId.Length > 64)
            return;

        SelectClassServerRpc(new FixedString64Bytes(requestedClassId));
    }

    [ServerRpc]
    private void SelectClassServerRpc(FixedString64Bytes requestedId)
    {
        if (!IsServer || characterDatabase == null ||
            requestedId.IsEmpty)
            return;

        LobbySessionManager lobby = LobbySessionManager.Instance;

        if (lobby != null && lobby.MatchStarted)
            return;

        CharacterDefinition requestedDefinition =
            characterDatabase.GetById(requestedId.ToString());

        if (requestedDefinition == null)
        {
            Debug.LogWarning(
                $"[CharacterRoot] Невідомий клас: {requestedId}", this);
            return;
        }

        LobbySessionManager.Instance?.SetPlayerClassFromCharacter(
            OwnerClientId,
            requestedDefinition.ClassId);

        if (classId.Value.Equals(requestedId))
            return;

        classId.Value =
            new FixedString64Bytes(requestedDefinition.ClassId);
    }

    private void OnClassIdChanged(
        FixedString64Bytes previous,
        FixedString64Bytes current)
    {
        if (!current.IsEmpty)
            ApplyClass(current);
    }

    #endregion

    #region Class Initialization

    private void ApplyClass(FixedString64Bytes selectedId)
    {
        if (characterDatabase == null || mechanicInstaller == null)
            return;

        CharacterDefinition selectedDefinition =
            characterDatabase.GetById(selectedId.ToString());

        if (selectedDefinition == null)
        {
            Debug.LogError(
                $"[CharacterRoot] Не знайдено клас '{selectedId}'.", this);
            return;
        }

        if (mechanicsInitialized && definition == selectedDefinition)
            return;

        // Стару зброю видаляємо до заміни класових механік.
        if (IsServer && activeWeaponObject != null &&
            activeWeaponObject.IsSpawned)
        {
            activeWeaponObject.Despawn(true);
        }

        activeWeaponObject = null;
        activeWeaponReference.Value = default;

        mechanicInstaller.Initialize(this, selectedDefinition);

        definition = selectedDefinition;
        mechanicsInitialized = true;

        ClassApplied?.Invoke(definition);

        Debug.Log(
            $"[CharacterRoot] Застосовано клас: {definition.DisplayName}.",
            this);
    }

    #endregion

    #region Weapon Selection

    public void RequestWeaponSelection(int index)
    {
        if (!IsSpawned || !IsOwner || index < 0)
            return;

        SelectWeaponServerRpc(index);
    }

    [ServerRpc]
    private void SelectWeaponServerRpc(int index)
    {
        if (!IsServer || !HasSelectedClass ||
            weaponDatabase == null || mechanicInstaller == null)
            return;

        if (index < 0 || index >= weaponDatabase.Weapons.Count)
            return;

        bool hasWeaponMechanic = false;

        foreach (ClassMechanic mechanic in mechanicInstaller.ActiveMechanics)
        {
            if (mechanic != null && mechanic.MechanicId == "weapon")
            {
                hasWeaponMechanic = true;
                break;
            }
        }

        if (!hasWeaponMechanic)
            return;

        PlayerWeaponProgression weaponProgression =
            GetComponentInChildren<PlayerWeaponProgression>(true);

        if (weaponProgression == null || !weaponProgression.IsSpawned)
            return;

        WeaponDefinition weapon = weaponDatabase.GetAt(index);

        if (weapon == null || weapon.WeaponPrefab == null)
        {
            Debug.LogError(
                $"[CharacterRoot] Некоректна зброя з індексом {index}.", this);
            return;
        }

        if (weapon.WeaponPrefab.GetComponent<NetworkObject>() == null ||
            weapon.WeaponPrefab.GetComponent<WeaponController>() == null)
        {
            Debug.LogError(
                "[CharacterRoot] Префаб зброї повинен містити " +
                "NetworkObject і WeaponController.",
                weapon.WeaponPrefab);
            return;
        }

        if (weaponHolder == null)
        {
            Debug.LogError(
                "[CharacterRoot] Не призначено WeaponHolder.", this);
            return;
        }

        if (activeWeaponObject != null && activeWeaponObject.IsSpawned)
        {
            activeWeaponObject.Despawn(true);
            activeWeaponObject = null;
        }

        GameObject instance = Instantiate(
            weapon.WeaponPrefab,
            weaponHolder.position,
            weaponHolder.rotation);

        NetworkObject spawnedWeapon =
            instance.GetComponent<NetworkObject>();

        WeaponController controller =
            instance.GetComponent<WeaponController>();

        spawnedWeapon.SpawnWithOwnership(OwnerClientId);

        controller.ConfigureServer(weapon, weaponProgression);

        if (!spawnedWeapon.TrySetParent(NetworkObject, false))
        {
            Debug.LogError(
                "[CharacterRoot] Не вдалося прикріпити зброю до персонажа.",
                this);

            spawnedWeapon.Despawn(true);
            return;
        }

        spawnedWeapon.transform.localPosition = Vector3.zero;
        spawnedWeapon.transform.localRotation = Quaternion.identity;

        activeWeaponObject = spawnedWeapon;

        activeWeaponReference.Value =
            new NetworkObjectReference(spawnedWeapon);

        Debug.Log(
            $"[CharacterRoot] Зброю створено: {spawnedWeapon.name}.",
            spawnedWeapon.gameObject);
    }

    #endregion

    #region Active Weapon

    public bool TryGetActiveWeapon(out WeaponController controller)
    {
        controller = null;

        if (!activeWeaponReference.Value.TryGet(
            out NetworkObject networkObject))
            return false;

        if (networkObject == null || !networkObject.IsSpawned)
            return false;

        controller = networkObject.GetComponent<WeaponController>();
        return controller != null;
    }

    #endregion
}
