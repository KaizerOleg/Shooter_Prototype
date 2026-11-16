using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class BuildableStructure : NetworkBehaviour
{
    #region Inspector Settings

    [Header("Database")]
    [SerializeField] private BuilderDatabase database;

    [Header("Highlight")]
    [SerializeField]
    private Color highlightColor =
        new Color(0.1f, 0.8f, 1f, 1f);

    [SerializeField] private string colorProperty = "_BaseColor";
    [SerializeField] private string emissionProperty = "_EmissionColor";

    #endregion

    #region Configuration State

    private BuildingDefinition configuredDefinition;
    private ulong configuredOwnerClientId;
    private bool hasPendingConfiguration;

    #endregion

    #region Network State

    public NetworkVariable<FixedString64Bytes> BuildingId =
        new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public NetworkVariable<ulong> BuilderOwnerClientId =
        new(
            ulong.MaxValue,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public NetworkVariable<float> CurrentHealth =
        new(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    public NetworkList<FixedString64Bytes> PurchasedUpgrades =
        new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    #endregion

    #region Highlight State

    private Renderer[] cachedRenderers;
    private MaterialPropertyBlock propertyBlock;
    private bool highlighted;

    private Color[] originalColors;
    private Color[] originalEmissionColors;
    private bool[] hadOriginalColor;
    private bool[] hadOriginalEmission;

    private static readonly int BaseColorId =
        Shader.PropertyToID("_BaseColor");

    private static readonly int ColorId =
        Shader.PropertyToID("_Color");

    private static readonly int EmissionColorId =
        Shader.PropertyToID("_EmissionColor");

    #endregion

    #region Properties

    public BuilderDatabase Database => database;

    public BuildingDefinition Definition
    {
        get
        {
            if (configuredDefinition != null)
                return configuredDefinition;

            if (database == null)
                return null;

            return database.GetBuilding(
                BuildingId.Value.ToString());
        }
    }

    public bool IsWall =>
        Definition != null && Definition.IsWall;

    public bool IsAlive =>
        IsSpawned && CurrentHealth.Value > 0f;

    public float Damage =>
        GetStat(BuildingUpgradeStat.Damage);

    public float AttackRange =>
        GetStat(BuildingUpgradeStat.AttackRange);

    public float AttacksPerSecond =>
        GetStat(BuildingUpgradeStat.AttacksPerSecond);

    public float MaxHealth =>
        GetStat(BuildingUpgradeStat.MaxHealth);

    #endregion

    #region Configuration

    public void ConfigureServer(
        BuilderDatabase sourceDatabase,
        BuildingDefinition definition,
        ulong ownerClientId)
    {
        if (definition == null)
        {
            Debug.LogError(
                "[BuildableStructure] ConfigureServer отримав " +
                "порожній BuildingDefinition.",
                this);
            return;
        }

        if (sourceDatabase == null)
        {
            Debug.LogError(
                "[BuildableStructure] ConfigureServer отримав " +
                "порожній BuilderDatabase.",
                this);
            return;
        }

        database = sourceDatabase;
        configuredDefinition = definition;
        configuredOwnerClientId = ownerClientId;
        hasPendingConfiguration = true;

        if (IsSpawned)
        {
            if (!IsServer)
            {
                Debug.LogError(
                    "[BuildableStructure] ConfigureServer викликано " +
                    "на вже запущеному об'єкті не з сервера.",
                    this);
                return;
            }

            ApplyPendingConfiguration();
        }

        Debug.Log(
            $"[BuildableStructure] Конфігурацію підготовлено. " +
            $"ID='{definition.BuildingId}', " +
            $"Definition='{definition.name}', " +
            $"Database='{sourceDatabase.name}', " +
            $"Owner={ownerClientId}.",
            this);
    }

    private void ApplyPendingConfiguration()
    {
        if (!IsServer || !hasPendingConfiguration)
            return;

        if (configuredDefinition == null || database == null)
        {
            Debug.LogError(
                "[BuildableStructure] Не вдалося застосувати " +
                "конфігурацію: Definition або Database == null.",
                this);
            return;
        }

        BuildingId.Value =
            new FixedString64Bytes(configuredDefinition.BuildingId);

        BuilderOwnerClientId.Value =
            configuredOwnerClientId;

        CurrentHealth.Value =
            configuredDefinition.MaxHealth;

        hasPendingConfiguration = false;

        Debug.Log(
            $"[BuildableStructure] NetworkVariable налаштовано. " +
            $"BuildingId='{BuildingId.Value}', " +
            $"Owner={BuilderOwnerClientId.Value}, " +
            $"Health={CurrentHealth.Value}.",
            this);
    }

    #endregion

    #region Network Lifecycle

    public override void OnNetworkSpawn()
    {
        CacheRenderers();

        if (!IsServer)
            return;

        if (hasPendingConfiguration)
        {
            ApplyPendingConfiguration();
        }
        else
        {
            BuildingDefinition definition = Definition;

            if (definition == null)
            {
                Debug.LogError(
                    "[BuildableStructure] Не вдалося знайти " +
                    "BuildingDefinition після Spawn. " +
                    $"BuildingId='{BuildingId.Value}', " +
                    $"Database='{(database != null ? database.name : "NULL")}'.",
                    this);
                return;
            }

            CurrentHealth.Value = definition.MaxHealth;
        }

        BuildingDefinition activeDefinition = Definition;

        if (activeDefinition == null)
        {
            Debug.LogError(
                "[BuildableStructure] Після конфігурації Definition == null.",
                this);
            return;
        }

        Debug.Log(
            $"[BuildableStructure] Ініціалізовано " +
            $"{activeDefinition.DisplayName}. " +
            $"ID: {BuildingId.Value}, " +
            $"Owner: {BuilderOwnerClientId.Value}, " +
            $"Wall: {activeDefinition.IsWall}, " +
            $"Damage: {Damage}, " +
            $"AttackRange: {AttackRange}, " +
            $"AttacksPerSecond: {AttacksPerSecond}.",
            this);
    }

    public override void OnNetworkDespawn()
    {
        SetHighlighted(false);
    }

    #endregion

    #region Highlight System

    private void CacheRenderers()
    {
        cachedRenderers =
            GetComponentsInChildren<Renderer>(true);

        propertyBlock = new MaterialPropertyBlock();

        int count = cachedRenderers.Length;

        originalColors = new Color[count];
        originalEmissionColors = new Color[count];
        hadOriginalColor = new bool[count];
        hadOriginalEmission = new bool[count];

        for (int i = 0; i < count; i++)
        {
            Renderer renderer = cachedRenderers[i];

            if (renderer == null)
                continue;

            Material[] materials = renderer.sharedMaterials;

            if (materials == null || materials.Length == 0)
                continue;

            Material material = materials[0];

            if (material == null)
                continue;

            if (material.HasProperty(BaseColorId))
            {
                originalColors[i] =
                    material.GetColor(BaseColorId);

                hadOriginalColor[i] = true;
            }
            else if (material.HasProperty(ColorId))
            {
                originalColors[i] =
                    material.GetColor(ColorId);

                hadOriginalColor[i] = true;
            }

            if (material.HasProperty(EmissionColorId))
            {
                originalEmissionColors[i] =
                    material.GetColor(EmissionColorId);

                hadOriginalEmission[i] = true;
            }
        }
    }

    public void SetHighlighted(bool value)
    {
        if (highlighted == value)
            return;

        highlighted = value;

        if (cachedRenderers == null)
            CacheRenderers();

        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            Renderer renderer = cachedRenderers[i];

            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(propertyBlock);

            Material[] materials = renderer.sharedMaterials;

            if (materials == null || materials.Length == 0)
                continue;

            Material material = materials[0];

            if (material == null)
                continue;

            if (material.HasProperty(BaseColorId))
            {
                propertyBlock.SetColor(
                    BaseColorId,
                    value
                        ? highlightColor
                        : hadOriginalColor[i]
                            ? originalColors[i]
                            : Color.white);
            }
            else if (material.HasProperty(ColorId))
            {
                propertyBlock.SetColor(
                    ColorId,
                    value
                        ? highlightColor
                        : hadOriginalColor[i]
                            ? originalColors[i]
                            : Color.white);
            }

            if (material.HasProperty(EmissionColorId))
            {
                propertyBlock.SetColor(
                    EmissionColorId,
                    value
                        ? highlightColor * 1.5f
                        : hadOriginalEmission[i]
                            ? originalEmissionColors[i]
                            : Color.black);
            }

            renderer.SetPropertyBlock(propertyBlock);
        }
    }

    #endregion

    #region Building Stats

    public float GetStat(BuildingUpgradeStat stat)
    {
        BuildingDefinition definition = Definition;

        if (definition == null)
            return 0f;

        float value = stat switch
        {
            BuildingUpgradeStat.Damage =>
                definition.Damage,

            BuildingUpgradeStat.AttackRange =>
                definition.AttackRange,

            BuildingUpgradeStat.AttacksPerSecond =>
                definition.AttacksPerSecond,

            BuildingUpgradeStat.MaxHealth =>
                definition.MaxHealth,

            _ => 0f
        };

        if (database == null)
            return value;

        foreach (FixedString64Bytes purchased in PurchasedUpgrades)
        {
            BuildingUpgradeDefinition upgrade =
                database.GetUpgrade(purchased.ToString());

            if (upgrade == null || upgrade.Stat != stat)
                continue;

            value = upgrade.Apply(value);
        }

        return Mathf.Max(0f, value);
    }

    #endregion

    #region Upgrade Management

    public int GetUpgradeLevel(string upgradeId)
    {
        int count = 0;

        foreach (FixedString64Bytes purchased in PurchasedUpgrades)
        {
            if (purchased.ToString() == upgradeId)
                count++;
        }

        return count;
    }

    public void RequestUpgrade(string upgradeId)
    {
        if (!IsSpawned ||
            string.IsNullOrWhiteSpace(upgradeId))
            return;

        RequestUpgradeServerRpc(
            new FixedString64Bytes(upgradeId));
    }

    #endregion

    #region Upgrade Server RPC

    [ServerRpc(RequireOwnership = false)]
    private void RequestUpgradeServerRpc(
        FixedString64Bytes upgradeId,
        ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;

        if (sender != BuilderOwnerClientId.Value)
            return;

        if (database == null)
            return;

        BuildingDefinition definition = Definition;

        if (definition == null ||
            !definition.HasUpgrade(upgradeId.ToString()))
            return;

        BuildingUpgradeDefinition upgrade =
            database.GetUpgrade(upgradeId.ToString());

        if (upgrade == null)
            return;

        int currentLevel =
            GetUpgradeLevel(upgrade.UpgradeId);

        if (currentLevel >= upgrade.MaxLevel)
            return;

        BuilderProgression progression =
            FindOwnerProgression(sender);

        if (progression == null)
            return;

        if (!progression.TrySpendBuildPoints(upgrade.Cost))
            return;

        PurchasedUpgrades.Add(upgradeId);

        if (upgrade.Stat == BuildingUpgradeStat.MaxHealth)
        {
            float oldMax = MaxHealth;
            float newMax = GetStat(BuildingUpgradeStat.MaxHealth);

            if (newMax > oldMax)
                CurrentHealth.Value += newMax - oldMax;
        }

        Debug.Log(
            $"[BuildableStructure] {definition.DisplayName}: " +
            $"покращення {upgrade.DisplayName} придбано.",
            this);
    }

    #endregion

    #region Owner Progression Lookup

    private BuilderProgression FindOwnerProgression(ulong clientId)
    {
        if (NetworkManager.Singleton == null)
            return null;

        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(
            clientId,
            out NetworkClient client))
        {
            return null;
        }

        if (client.PlayerObject == null)
            return null;

        return client.PlayerObject
            .GetComponentInChildren<BuilderProgression>(true);
    }

    #endregion

    #region Health Management

    public bool ApplyDamage(float amount)
    {
        if (!IsServer || amount <= 0f || !IsSpawned)
            return false;

        CurrentHealth.Value =
            Mathf.Max(0f, CurrentHealth.Value - amount);

        if (CurrentHealth.Value <= 0f)
        {
            DespawnStructure();
            return true;
        }

        return false;
    }

    public void Repair(float amount)
    {
        if (!IsServer || amount <= 0f || !IsSpawned)
            return;

        CurrentHealth.Value =
            Mathf.Min(MaxHealth, CurrentHealth.Value + amount);
    }

    private void DespawnStructure()
    {
        if (!IsServer || !IsSpawned)
            return;

        BuilderProgression progression =
            FindOwnerProgression(BuilderOwnerClientId.Value);

        if (progression != null)
            progression.UnregisterBuildable();

        NetworkObject.Despawn(true);
    }

    #endregion
}