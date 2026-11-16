
using UnityEngine;

[CreateAssetMenu(
    fileName = "WeaponDefinition",
    menuName = "Nexus Defense/Weapons/Weapon Definition")]
public sealed class WeaponDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string weaponId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;

    [Header("Prefab")]
    [SerializeField] private GameObject weaponPrefab;

    [Header("Upgrades")]
    [SerializeField]
    private WeaponUpgradeData[] upgrades;

    public WeaponUpgradeData[] Upgrades => upgrades;

    [Header("Combat")]
    [SerializeField, Min(0)] private float damage = 20f;
    [SerializeField, Min(0.01f)] private float shotsPerSecond = 5f;
    [SerializeField, Min(1)] private int magazineSize = 30;
    [SerializeField, Min(0f)] private float reloadDuration = 1.5f;
    [SerializeField, Min(0.1f)] private float range = 100f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private bool isAutomatic;
    public bool IsAutomatic => isAutomatic;

    [Header("Attack")]
    [SerializeField] private AttackStrategy attackStrategy;

    public string WeaponId => weaponId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public GameObject WeaponPrefab => weaponPrefab;
    public float Damage => damage;
    public float ShotsPerSecond => shotsPerSecond;
    public int MagazineSize => magazineSize;
    public float ReloadDuration => reloadDuration;
    public float Range => range;
    public LayerMask HitMask => hitMask;
    public AttackStrategy Strategy => attackStrategy;
}
