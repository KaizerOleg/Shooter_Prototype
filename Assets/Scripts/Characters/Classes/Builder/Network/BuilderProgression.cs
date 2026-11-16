using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class BuilderProgression : NetworkBehaviour
{
    #region Inspector Settings

    [Header("Database")]
    [SerializeField] private BuilderDatabase database;

    [Header("Legacy Starting Values")]
    [SerializeField, Min(0)]
    private int startingBuildPoints = 0;

    [Header("Build Limit")]
    [SerializeField, Min(0)]
    private int maxBuildables = 10;

    [Header("Placement Validation")]
    [SerializeField, Min(1f)]
    private float maxPlacementDistance = 10f;

    [SerializeField, Min(0.1f)]
    private float groundCheckHeight = 3f;

    [SerializeField, Min(0.1f)]
    private float groundCheckDistance = 8f;

    [SerializeField]
    private LayerMask placementMask = ~0;

    #endregion

    #region Network State

    public NetworkVariable<int> BuildPoints = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<int> ActiveBuildables = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    #endregion

    #region Runtime References

    private CharacterRoot character;
    private PlayerUpgradeWallet wallet;

    #endregion

    #region Runtime State

    private bool walletSearchWarningShown;

    #endregion

    #region Properties

    public int MaxBuildables => maxBuildables;

    public int RemainingBuildableSlots =>
        Mathf.Max(0, maxBuildables - ActiveBuildables.Value);

    public bool CanBuild =>
        IsSpawned &&
        wallet != null &&
        RemainingBuildableSlots > 0;

    public BuilderDatabase Database => database;

    #endregion

    #region Network Lifecycle

    public override void OnNetworkSpawn()
    {
        TryBindWallet();

        if (!IsServer)
            return;

        ActiveBuildables.Value = 0;

        if (database == null)
        {
            Debug.LogError(
                "[BuilderProgression] Не призначено BuilderDatabase.",
                this);
        }
        else
        {
            database.RebuildLookup();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (wallet != null)
            wallet.PointsChanged -= OnWalletPointsChanged;

        character = null;
        wallet = null;
        walletSearchWarningShown = false;
    }

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        if (wallet == null && IsSpawned)
            TryBindWallet();
    }

    #endregion

    #region Wallet Binding

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
                    "[BuilderProgression] Не знайдено PlayerUpgradeWallet " +
                    "на корені персонажа. Пошук буде повторено.",
                    this);

                walletSearchWarningShown = true;
            }

            return;
        }

        walletSearchWarningShown = false;
        wallet.PointsChanged += OnWalletPointsChanged;

        if (IsServer && IsSpawned)
            BuildPoints.Value = wallet.Points;
    }

    private void OnWalletPointsChanged(int previous, int current)
    {
        if (IsServer && IsSpawned)
            BuildPoints.Value = current;
    }

    #endregion

    #region Build Points Management

    public void AwardWavePoints(int amount)
    {
        if (!IsServer || !IsSpawned || wallet == null || amount <= 0)
            return;

        wallet.AddPoints(amount);

        Debug.Log(
            $"[BuilderProgression] Гравець {OwnerClientId} отримав " +
            $"{amount} очок. Спільний баланс: {wallet.Points}.",
            this);
    }

    public void AddBuildPoints(int amount)
    {
        if (!IsServer || !IsSpawned || wallet == null || amount <= 0)
            return;

        wallet.AddPoints(amount);
    }

    public bool TrySpendBuildPoints(int amount)
    {
        if (!IsServer ||
            !IsSpawned ||
            wallet == null ||
            amount < 0)
        {
            return false;
        }

        return wallet.TrySpendPoints(amount);
    }

    #endregion

    #region Buildable Management

    public bool TryPurchaseBuildable(int cost)
    {
        if (!IsServer ||
            !IsSpawned ||
            wallet == null ||
            cost < 0 ||
            ActiveBuildables.Value >= maxBuildables)
        {
            return false;
        }

        if (!wallet.TrySpendPoints(cost))
            return false;

        ActiveBuildables.Value++;
        return true;
    }

    public bool UnregisterBuildable()
    {
        if (!IsServer ||
            !IsSpawned ||
            ActiveBuildables.Value <= 0)
        {
            return false;
        }

        ActiveBuildables.Value--;
        return true;
    }

    public bool RefundBuildable(int refundPoints)
    {
        if (!IsServer ||
            !IsSpawned ||
            ActiveBuildables.Value <= 0)
        {
            return false;
        }

        if (refundPoints > 0 && wallet == null)
            return false;

        ActiveBuildables.Value--;

        if (refundPoints > 0)
            wallet.AddPoints(refundPoints);

        return true;
    }

    #endregion

    #region Building Database

    public BuildingDefinition GetBuilding(string id)
    {
        return database != null
            ? database.GetBuilding(id)
            : null;
    }

    #endregion

    #region Building Placement Request

    public void RequestPlaceBuilding(
        string buildingId,
        Vector3 position,
        Quaternion rotation)
    {
        if (!IsOwner ||
            !IsSpawned ||
            string.IsNullOrWhiteSpace(buildingId) ||
            buildingId.Length > 64)
        {
            return;
        }

        RequestPlaceBuildingServerRpc(
            new FixedString64Bytes(buildingId),
            position,
            rotation);
    }

    #endregion

    #region Building Placement Server RPC

    [ServerRpc]
    private void RequestPlaceBuildingServerRpc(
        FixedString64Bytes buildingId,
        Vector3 position,
        Quaternion rotation,
        ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (!IsServer ||
            !IsSpawned ||
            database == null ||
            wallet == null)
        {
            return;
        }

        BuildingDefinition definition =
            database.GetBuilding(buildingId.ToString());

        if (definition == null)
        {
            Debug.LogWarning(
                $"[BuilderProgression] Не знайдено будівлю " +
                $"з ID '{buildingId}'.",
                this);
            return;
        }

        if (definition.BuildingPrefab == null)
        {
            Debug.LogError(
                $"[BuilderProgression] У {definition.name} " +
                "не призначено BuildingPrefab.",
                definition);
            return;
        }

        if (ActiveBuildables.Value >= maxBuildables)
            return;

        float allowedDistance =
            Mathf.Min(maxPlacementDistance, definition.PlacementRange) + 1f;

        if (Vector3.Distance(transform.position, position) > allowedDistance)
            return;

        if (!ValidateGround(position, definition))
            return;

        if (!TryPurchaseBuildable(definition.BuildCost))
            return;

        GameObject instance = Instantiate(
            definition.BuildingPrefab,
            position,
            rotation);

        if (instance == null)
        {
            RefundBuildable(definition.BuildCost);
            return;
        }

        NetworkObject spawnedNetworkObject =
            instance.GetComponent<NetworkObject>();

        BuildableStructure structure =
            instance.GetComponent<BuildableStructure>();

        if (spawnedNetworkObject == null || structure == null)
        {
            Debug.LogError(
                $"[BuilderProgression] Prefab {definition.name} " +
                "повинен мати NetworkObject і BuildableStructure " +
                "на кореневому GameObject.",
                instance);

            Destroy(instance);
            RefundBuildable(definition.BuildCost);
            return;
        }

        structure.ConfigureServer(
            database,
            definition,
            OwnerClientId);

        spawnedNetworkObject.Spawn(true);

        Debug.Log(
            $"[BuilderProgression] Створено {definition.DisplayName}. " +
            $"ID: {definition.BuildingId}, " +
            $"Database: {database.name}, " +
            $"Owner: {OwnerClientId}, " +
            $"NetworkSpawned: {spawnedNetworkObject.IsSpawned}.",
            instance);
    }

    #endregion

    #region Placement Validation

    private bool ValidateGround(
        Vector3 position,
        BuildingDefinition definition)
    {
        Vector3 origin = position + Vector3.up * groundCheckHeight;

        if (!Physics.Raycast(
                origin,
                Vector3.down,
                out RaycastHit hit,
                groundCheckDistance,
                placementMask,
                QueryTriggerInteraction.Ignore))
        {
            return false;
        }

        if (hit.collider == null)
            return false;

        if (hit.collider.GetComponentInParent<EnemyAI>() != null)
            return false;

        if (hit.collider.GetComponentInParent<CharacterRoot>() != null)
            return false;

        if (hit.normal.y < 0.3f)
            return false;

        return true;
    }

    #endregion
}