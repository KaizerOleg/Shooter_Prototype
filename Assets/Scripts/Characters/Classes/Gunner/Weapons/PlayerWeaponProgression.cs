using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct WeaponUpgradeLevel :
    INetworkSerializable,
    IEquatable<WeaponUpgradeLevel>
{
    public FixedString64Bytes WeaponId;
    public FixedString64Bytes UpgradeId;
    public int Level;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref WeaponId);
        serializer.SerializeValue(ref UpgradeId);
        serializer.SerializeValue(ref Level);
    }

    public bool Equals(WeaponUpgradeLevel other)
    {
        return WeaponId.Equals(other.WeaponId) &&
               UpgradeId.Equals(other.UpgradeId) &&
               Level == other.Level;
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class PlayerWeaponProgression : NetworkBehaviour
{
    private readonly NetworkList<WeaponUpgradeLevel> upgradeLevels = new(
        null,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private CharacterRoot character;
    private PlayerUpgradeWallet wallet;

    private bool walletSearchWarningShown;

    public int UpgradePoints => wallet != null ? wallet.Points : 0;

    public event Action ProgressionChanged;

    public override void OnNetworkSpawn()
    {
        upgradeLevels.OnListChanged += OnUpgradeLevelsChanged;

        TryBindWallet();

        ProgressionChanged?.Invoke();
    }

    private void Update()
    {
        if (wallet == null && IsSpawned)
            TryBindWallet();
    }

    private void TryBindWallet()
    {
        if (wallet != null)
            return;

        character = GetComponentInParent<CharacterRoot>();

        if (character == null)
            return;

        wallet = character.GetComponent<PlayerUpgradeWallet>();

        if (wallet == null)
        {
            if (!walletSearchWarningShown)
            {
                Debug.LogWarning(
                    "[WeaponProgression] Не знайдено PlayerUpgradeWallet " +
                    "на корені персонажа. Пошук буде повторено.",
                    this);

                walletSearchWarningShown = true;
            }

            return;
        }

        walletSearchWarningShown = false;
        wallet.PointsChanged += OnWalletPointsChanged;

        ProgressionChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        if (wallet != null)
            wallet.PointsChanged -= OnWalletPointsChanged;

        upgradeLevels.OnListChanged -= OnUpgradeLevelsChanged;

        character = null;
        wallet = null;
        walletSearchWarningShown = false;
    }

    private void OnWalletPointsChanged(int previous, int current)
    {
        ProgressionChanged?.Invoke();
    }

    private void OnUpgradeLevelsChanged(
        NetworkListEvent<WeaponUpgradeLevel> change)
    {
        ProgressionChanged?.Invoke();
    }

    public void AddUpgradePoints(int amount)
    {
        if (!IsServer || !IsSpawned || wallet == null || amount <= 0)
            return;

        wallet.AddPoints(amount);
    }

    public void AwardWavePoints(int amount)
    {
        if (!IsServer || !IsSpawned || wallet == null || amount <= 0)
            return;

        wallet.AddPoints(amount);

        Debug.Log(
            $"[WeaponProgression] Гравець {OwnerClientId} отримав " +
            $"{amount} очок. Спільний баланс: {UpgradePoints}.",
            this);
    }

    public int GetUpgradeLevel(string weaponId, string upgradeId)
    {
        for (int i = 0; i < upgradeLevels.Count; i++)
        {
            WeaponUpgradeLevel entry = upgradeLevels[i];

            if (entry.WeaponId.ToString() == weaponId &&
                entry.UpgradeId.ToString() == upgradeId)
            {
                return entry.Level;
            }
        }

        return 0;
    }

    public void RequestUpgrade(string weaponId, string upgradeId)
    {
        if (!IsSpawned ||
            !IsOwner ||
            string.IsNullOrWhiteSpace(weaponId) ||
            string.IsNullOrWhiteSpace(upgradeId) ||
            weaponId.Length > 64 ||
            upgradeId.Length > 64)
        {
            return;
        }

        PurchaseUpgradeServerRpc(
            new FixedString64Bytes(weaponId),
            new FixedString64Bytes(upgradeId));
    }

    [ServerRpc]
    private void PurchaseUpgradeServerRpc(
        FixedString64Bytes requestedWeaponId,
        FixedString64Bytes requestedUpgradeId)
    {
        if (!IsServer ||
            !IsSpawned ||
            wallet == null ||
            character == null)
        {
            return;
        }

        string weaponId = requestedWeaponId.ToString();
        string upgradeId = requestedUpgradeId.ToString();

        WeaponDefinition weapon =
            character.GetWeaponDefinition(weaponId);

        if (weapon == null || weapon.Upgrades == null)
            return;

        WeaponUpgradeData selectedUpgrade = null;

        foreach (WeaponUpgradeData upgrade in weapon.Upgrades)
        {
            if (upgrade != null && upgrade.UpgradeId == upgradeId)
            {
                selectedUpgrade = upgrade;
                break;
            }
        }

        if (selectedUpgrade == null)
            return;

        int currentLevel = GetUpgradeLevel(weaponId, upgradeId);

        if (currentLevel >= selectedUpgrade.MaxLevel ||
            selectedUpgrade.PointsCost < 0)
        {
            return;
        }

        if (!wallet.TrySpendPoints(selectedUpgrade.PointsCost))
            return;

        WeaponUpgradeLevel newEntry = new()
        {
            WeaponId = requestedWeaponId,
            UpgradeId = requestedUpgradeId,
            Level = currentLevel + 1
        };

        bool updated = false;

        for (int i = 0; i < upgradeLevels.Count; i++)
        {
            WeaponUpgradeLevel entry = upgradeLevels[i];

            if (entry.WeaponId.Equals(requestedWeaponId) &&
                entry.UpgradeId.Equals(requestedUpgradeId))
            {
                upgradeLevels[i] = newEntry;
                updated = true;
                break;
            }
        }

        if (!updated)
            upgradeLevels.Add(newEntry);

        if (character.TryGetActiveWeapon(
                out WeaponController activeWeapon))
        {
            if (activeWeapon.Data != null &&
                activeWeapon.Data.WeaponId == weaponId)
            {
                activeWeapon.ApplyProgressionUpgrade(
                    upgradeId,
                    currentLevel + 1);
            }
        }

        Debug.Log(
            $"[WeaponProgression] Покращено {weaponId}/{upgradeId} " +
            $"до рівня {currentLevel + 1}. Залишок: {wallet.Points}.",
            this);
    }
}