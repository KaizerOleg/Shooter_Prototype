using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public struct SpellUpgradeLevel :
    INetworkSerializable,
    IEquatable<SpellUpgradeLevel>
{
    public FixedString64Bytes SpellId;
    public FixedString64Bytes UpgradeId;
    public int Level;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref SpellId);
        serializer.SerializeValue(ref UpgradeId);
        serializer.SerializeValue(ref Level);
    }

    public bool Equals(SpellUpgradeLevel other)
    {
        return SpellId.Equals(other.SpellId) &&
               UpgradeId.Equals(other.UpgradeId) &&
               Level == other.Level;
    }
}

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class PlayerSpellProgression : NetworkBehaviour
{
    [Header("Database")]
    [SerializeField] private SpellDatabase spellDatabase;

    private readonly NetworkList<SpellUpgradeLevel> upgradeLevels = new(
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
                    "[SpellProgression] Не знайдено PlayerUpgradeWallet " +
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
        NetworkListEvent<SpellUpgradeLevel> change)
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
            $"[SpellProgression] Гравець {OwnerClientId} отримав " +
            $"{amount} очок. Спільний баланс: {UpgradePoints}.",
            this);
    }

    public int GetUpgradeLevel(string spellId, string upgradeId)
    {
        for (int i = 0; i < upgradeLevels.Count; i++)
        {
            SpellUpgradeLevel entry = upgradeLevels[i];

            if (entry.SpellId.ToString() == spellId &&
                entry.UpgradeId.ToString() == upgradeId)
            {
                return entry.Level;
            }
        }

        return 0;
    }

    public SpellDefinition GetSpellDefinition(string spellId)
    {
        return spellDatabase != null
            ? spellDatabase.GetById(spellId)
            : null;
    }

    public void RequestUpgrade(string spellId, string upgradeId)
    {
        if (!IsSpawned ||
            !IsOwner ||
            string.IsNullOrWhiteSpace(spellId) ||
            string.IsNullOrWhiteSpace(upgradeId) ||
            spellId.Length > 64 ||
            upgradeId.Length > 64)
        {
            return;
        }

        PurchaseUpgradeServerRpc(
            new FixedString64Bytes(spellId),
            new FixedString64Bytes(upgradeId));
    }

    public SpellRuntime CreateSpellRuntime(string spellId)
    {
        SpellDefinition spell = GetSpellDefinition(spellId);

        if (spell == null)
            return null;

        SpellRuntime runtime = new SpellRuntime(spell);

        if (spell.Upgrades == null)
            return runtime;

        foreach (SpellUpgradeData upgrade in spell.Upgrades)
        {
            if (upgrade == null)
                continue;

            int level = GetUpgradeLevel(
                spellId,
                upgrade.UpgradeId);

            if (level > 0)
            {
                runtime.RestoreUpgradeLevel(
                    upgrade.UpgradeId,
                    level);
            }
        }

        return runtime;
    }

    [ServerRpc]
    private void PurchaseUpgradeServerRpc(
        FixedString64Bytes requestedSpellId,
        FixedString64Bytes requestedUpgradeId)
    {
        if (!IsServer ||
            !IsSpawned ||
            wallet == null ||
            spellDatabase == null)
        {
            return;
        }

        string spellId = requestedSpellId.ToString();
        string upgradeId = requestedUpgradeId.ToString();

        SpellDefinition spell = spellDatabase.GetById(spellId);

        if (spell == null || spell.Upgrades == null)
            return;

        SpellUpgradeData selectedUpgrade = null;

        foreach (SpellUpgradeData upgrade in spell.Upgrades)
        {
            if (upgrade != null && upgrade.UpgradeId == upgradeId)
            {
                selectedUpgrade = upgrade;
                break;
            }
        }

        if (selectedUpgrade == null)
            return;

        int currentLevel = GetUpgradeLevel(spellId, upgradeId);

        if (currentLevel >= selectedUpgrade.MaxLevel ||
            selectedUpgrade.PointsCost < 0)
        {
            return;
        }

        if (!wallet.TrySpendPoints(selectedUpgrade.PointsCost))
            return;

        SpellUpgradeLevel newEntry = new()
        {
            SpellId = requestedSpellId,
            UpgradeId = requestedUpgradeId,
            Level = currentLevel + 1
        };

        bool updated = false;

        for (int i = 0; i < upgradeLevels.Count; i++)
        {
            SpellUpgradeLevel entry = upgradeLevels[i];

            if (entry.SpellId.Equals(requestedSpellId) &&
                entry.UpgradeId.Equals(requestedUpgradeId))
            {
                upgradeLevels[i] = newEntry;
                updated = true;
                break;
            }
        }

        if (!updated)
            upgradeLevels.Add(newEntry);

        Debug.Log(
            $"[SpellProgression] Покращено {spellId}/{upgradeId} " +
            $"до рівня {currentLevel + 1}. Залишок: {wallet.Points}.",
            this);
    }
}