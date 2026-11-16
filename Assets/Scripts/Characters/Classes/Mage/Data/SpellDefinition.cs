
using UnityEngine;

public enum SpellType
{
    Projectile,
    Area,
    ChainLightning
}

[CreateAssetMenu(
    fileName = "SpellDefinition",
    menuName = "Nexus Defense/Magic/Spell Definition"
)]
public sealed class SpellDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string spellId;
    [SerializeField] private string displayName;

    [TextArea(2, 5)]
    [SerializeField] private string description;

    [SerializeField] private Sprite icon;

    [Header("Type")]
    [SerializeField] private SpellType spellType = SpellType.Projectile;

    [Header("Prefab")]
    [SerializeField] private GameObject spellPrefab;

    [Header("Combat")]
    [SerializeField, Min(0f)] private float damage = 30f;
    [SerializeField, Min(0.1f)] private float cooldown = 3f;
    [SerializeField, Min(0)] private int manaCost = 20;
    [SerializeField, Min(0.1f)] private float range = 20f;

    [Header("Projectile")]
    [SerializeField, Min(0.1f)] private float projectileSpeed = 15f;

    [Header("Area")]
    [SerializeField, Min(0.1f)] private float areaRadius = 2f;
    [SerializeField, Min(0.1f)] private float areaHeight = 2f;
    [SerializeField, Min(0.1f)] private float tickInterval = 1f;

    [Tooltip("Скільки секунд потрібно утримувати кнопку Area-закляття.")]
    [SerializeField, Min(0f)] private float chargeTime = 0.75f;

    [Header("Duration")]
    [SerializeField, Min(0f)] private float duration;

    [Header("Chain Lightning")]
    [SerializeField, Min(1)] private int chainCount = 4;
    [SerializeField, Min(0.1f)] private float chainJumpRange = 6f;

    [Header("Upgrades")]
    [SerializeField] private SpellUpgradeData[] upgrades;

    public string SpellId => spellId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;

    public SpellType Type => spellType;
    public GameObject SpellPrefab => spellPrefab;

    public float Damage => damage;
    public float Cooldown => cooldown;
    public int ManaCost => manaCost;
    public float Range => range;

    public float ProjectileSpeed => projectileSpeed;

    public float AreaRadius => areaRadius;
    public float AreaHeight => areaHeight;
    public float TickInterval => tickInterval;
    public float ChargeTime => chargeTime;

    public float Duration => duration;

    public int ChainCount => chainCount;
    public float ChainJumpRange => chainJumpRange;

    public SpellUpgradeData[] Upgrades => upgrades;

#if UNITY_EDITOR
    private void OnValidate()
    {
        spellId = spellId?.Trim().ToLowerInvariant();
        displayName = displayName?.Trim();

        damage = Mathf.Max(0f, damage);
        cooldown = Mathf.Max(0.1f, cooldown);
        manaCost = Mathf.Max(0, manaCost);
        range = Mathf.Max(0.1f, range);

        projectileSpeed = Mathf.Max(0.1f, projectileSpeed);

        areaRadius = Mathf.Max(0.1f, areaRadius);
        areaHeight = Mathf.Max(0.1f, areaHeight);
        tickInterval = Mathf.Max(0.1f, tickInterval);
        chargeTime = Mathf.Max(0f, chargeTime);

        duration = Mathf.Max(0f, duration);

        chainCount = Mathf.Max(1, chainCount);
        chainJumpRange = Mathf.Max(0.1f, chainJumpRange);
    }
#endif
}
