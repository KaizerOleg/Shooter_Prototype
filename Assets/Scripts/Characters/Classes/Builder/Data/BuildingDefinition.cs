using UnityEngine;

[CreateAssetMenu(
    fileName = "BuildingDefinition",
    menuName = "Nexus Defense/Builder/Building Definition")]
public sealed class BuildingDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string buildingId;
    [SerializeField] private string displayName;

    [TextArea]
    [SerializeField] private string description;

    [Header("Prefab")]
    [SerializeField] private GameObject buildingPrefab;
    [SerializeField] private Sprite icon;

    [Header("Building Type")]
    [SerializeField] private bool isWall;

    [Header("Economy")]
    [SerializeField, Min(0)] private int buildCost = 10;

    [Header("Placement")]
    [SerializeField, Min(0.1f)] private float placementRange = 8f;
    [SerializeField] private Vector3 placementOffset;
    [SerializeField] private bool alignToSurface = true;
    [SerializeField] private bool allowRotation = true;
    [SerializeField, Min(1f)] private float rotationStep = 90f;
    [SerializeField] private LayerMask placementMask = ~0;

    [Header("Base Stats")]
    [SerializeField, Min(0)] private float damage;
    [SerializeField, Min(0)] private float attackRange;
    [SerializeField, Min(0)] private float attacksPerSecond;
    [SerializeField, Min(1)] private float maxHealth = 100f;

    [Header("Available Upgrades")]
    [SerializeField]
    private BuildingUpgradeDefinition[] availableUpgrades;

    public string BuildingId => buildingId;
    public string DisplayName => displayName;
    public string Description => description;

    public GameObject BuildingPrefab => buildingPrefab;
    public Sprite Icon => icon;

    public bool IsWall => isWall;

    public int BuildCost => buildCost;

    public float PlacementRange => placementRange;
    public Vector3 PlacementOffset => placementOffset;
    public bool AlignToSurface => alignToSurface;
    public bool AllowRotation => allowRotation;
    public float RotationStep => rotationStep;
    public LayerMask PlacementMask => placementMask;

    public float Damage => damage;
    public float AttackRange => attackRange;
    public float AttacksPerSecond => attacksPerSecond;
    public float MaxHealth => maxHealth;

    public BuildingUpgradeDefinition[] AvailableUpgrades =>
        availableUpgrades;

    public bool HasUpgrade(string upgradeId)
    {
        if (availableUpgrades == null)
            return false;

        foreach (BuildingUpgradeDefinition upgrade in availableUpgrades)
        {
            if (upgrade != null &&
                upgrade.UpgradeId == upgradeId)
            {
                return true;
            }
        }

        return false;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        buildingId = string.IsNullOrWhiteSpace(buildingId)
            ? name.Trim().ToLowerInvariant().Replace(" ", "_")
            : buildingId.Trim().ToLowerInvariant();

        if (rotationStep < 1f)
            rotationStep = 1f;

        if (isWall)
        {
            damage = 0f;
            attackRange = 0f;
            attacksPerSecond = 0f;
        }
    }
#endif
}