using UnityEngine;

[CreateAssetMenu(
    fileName = "WeaponUpgrade",
    menuName = "Nexus Defense/Weapons/Upgrade"
)]
public sealed class WeaponUpgradeData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string upgradeId;

    [Header("Display")]
    [SerializeField] private string upgradeName;
    [TextArea(2, 5)]
    [SerializeField] private string description;
    [SerializeField] private Sprite icon;

    [Header("Upgrade")]
    [SerializeField] private WeaponUpgradeType upgradeType;

    [SerializeField, Min(1)]
    private int maxLevel = 3;

    [SerializeField, Min(1)]
    private int pointsCost = 1;

    [Header("Effect")]
    [SerializeField] private float valuePerLevel = 5f;

    [Header("Requirements")]
    [SerializeField] private WeaponUpgradeData[] requiredUpgrades;

    public string UpgradeId => upgradeId;
    public string UpgradeName => upgradeName;
    public string Description => description;
    public Sprite Icon => icon;

    public WeaponUpgradeType UpgradeType => upgradeType;

    public int MaxLevel => maxLevel;
    public int PointsCost => pointsCost;

    public float ValuePerLevel => valuePerLevel;

    public WeaponUpgradeData[] RequiredUpgrades =>
        requiredUpgrades;
}