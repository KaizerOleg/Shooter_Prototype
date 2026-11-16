using UnityEngine;

[CreateAssetMenu(
    fileName = "SpellUpgrade",
    menuName = "Nexus Defense/Magic/Spell Upgrade"
)]
public sealed class SpellUpgradeData : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string upgradeId;

    [Header("Display")]
    [SerializeField] private string upgradeName;

    [TextArea(2, 5)]
    [SerializeField] private string description;

    [SerializeField] private Sprite icon;

    [Header("Upgrade")]
    [SerializeField] private SpellUpgradeType upgradeType;

    [SerializeField, Min(1)] private int maxLevel = 3;
    [SerializeField, Min(1)] private int pointsCost = 1;

    [Header("Effect")]
    [SerializeField] private float valuePerLevel = 5f;

    public string UpgradeId => upgradeId;
    public string UpgradeName => upgradeName;
    public string Description => description;
    public Sprite Icon => icon;

    public SpellUpgradeType UpgradeType => upgradeType;
    public int MaxLevel => maxLevel;
    public int PointsCost => pointsCost;
    public float ValuePerLevel => valuePerLevel;

#if UNITY_EDITOR
    private void OnValidate()
    {
        upgradeId = upgradeId?.Trim();
        upgradeName = upgradeName?.Trim();

        maxLevel = Mathf.Max(1, maxLevel);
        pointsCost = Mathf.Max(1, pointsCost);
    }
#endif
}