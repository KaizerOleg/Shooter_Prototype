
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "BuilderDatabase",
    menuName = "Nexus Defense/Builder/Database")]
public sealed class BuilderDatabase : ScriptableObject
{
    [Header("Buildings")]
    [SerializeField]
    private BuildingDefinition[] buildings;

    private Dictionary<string, BuildingDefinition> buildingLookup;
    private Dictionary<string, BuildingUpgradeDefinition> upgradeLookup;

    public IReadOnlyList<BuildingDefinition> Buildings => buildings;

    private void OnEnable()
    {
        RebuildLookup();
    }

    public void RebuildLookup()
    {
        buildingLookup =
            new Dictionary<string, BuildingDefinition>();

        upgradeLookup =
            new Dictionary<string, BuildingUpgradeDefinition>();

        if (buildings == null)
            return;

        foreach (BuildingDefinition building in buildings)
        {
            if (building == null ||
                string.IsNullOrWhiteSpace(building.BuildingId))
                continue;

            if (buildingLookup.ContainsKey(building.BuildingId))
            {
                Debug.LogError(
                    $"[BuilderDatabase] Дублікат Building ID: " +
                    $"{building.BuildingId}",
                    this);
                continue;
            }

            buildingLookup.Add(
                building.BuildingId,
                building);

            BuildingUpgradeDefinition[] upgrades =
                building.AvailableUpgrades;

            if (upgrades == null)
                continue;

            foreach (BuildingUpgradeDefinition upgrade in upgrades)
            {
                if (upgrade == null ||
                    string.IsNullOrWhiteSpace(upgrade.UpgradeId))
                    continue;

                if (upgradeLookup.ContainsKey(upgrade.UpgradeId))
                {
                    if (upgradeLookup[upgrade.UpgradeId] != upgrade)
                    {
                        Debug.LogError(
                            $"[BuilderDatabase] Дублікат Upgrade ID: " +
                            $"{upgrade.UpgradeId}",
                            this);
                    }

                    continue;
                }

                upgradeLookup.Add(
                    upgrade.UpgradeId,
                    upgrade);
            }
        }
    }

    public int BuildingCount =>
        buildings != null ? buildings.Length : 0;

    public BuildingDefinition GetBuilding(string id)
    {
        EnsureLookup();

        if (string.IsNullOrWhiteSpace(id))
            return null;

        return buildingLookup.TryGetValue(id, out var result)
            ? result
            : null;
    }

    public BuildingUpgradeDefinition GetUpgrade(string id)
    {
        EnsureLookup();

        if (string.IsNullOrWhiteSpace(id))
            return null;

        return upgradeLookup.TryGetValue(id, out var result)
            ? result
            : null;
    }

    public BuildingDefinition GetBuildingAt(int index)
    {
        if (buildings == null ||
            index < 0 ||
            index >= buildings.Length)
            return null;

        return buildings[index];
    }

    private void EnsureLookup()
    {
        if (buildingLookup == null || upgradeLookup == null)
            RebuildLookup();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        RebuildLookup();
    }
#endif
}
