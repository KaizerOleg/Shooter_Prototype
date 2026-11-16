
using UnityEngine;

public enum BuildingUpgradeStat
{
    Damage,
    AttackRange,
    AttacksPerSecond,
    MaxHealth
}

public enum BuildingUpgradeOperation
{
    Add,
    Multiply
}

[CreateAssetMenu(
    fileName = "BuildingUpgrade",
    menuName = "Nexus Defense/Builder/Building Upgrade")]
public sealed class BuildingUpgradeDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string upgradeId;
    [SerializeField] private string displayName;

    [TextArea]
    [SerializeField] private string description;

    [Header("Economy")]
    [SerializeField, Min(0)] private int cost = 10;

    [Header("Effect")]
    [SerializeField] private BuildingUpgradeStat stat;
    [SerializeField] private BuildingUpgradeOperation operation;
    [SerializeField] private float value = 1f;

    [Header("Limits")]
    [SerializeField, Min(1)] private int maxLevel = 1;

    public string UpgradeId => upgradeId;
    public string DisplayName => displayName;
    public string Description => description;

    public int Cost => cost;
    public BuildingUpgradeStat Stat => stat;
    public BuildingUpgradeOperation Operation => operation;
    public float Value => value;
    public int MaxLevel => maxLevel;

    public float Apply(float current)
    {
        switch (operation)
        {
            case BuildingUpgradeOperation.Add:
                return current + value;

            case BuildingUpgradeOperation.Multiply:
                return current * value;

            default:
                return current;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        upgradeId = string.IsNullOrWhiteSpace(upgradeId)
            ? name.Trim().ToLowerInvariant().Replace(" ", "_")
            : upgradeId.Trim().ToLowerInvariant();
    }
#endif
}
