
using System.Collections.Generic;
using UnityEngine;

public sealed class SpellRuntime
{
    public SpellDefinition Data { get; }

    public float Damage { get; private set; }
    public float Cooldown { get; private set; }
    public int ManaCost { get; private set; }
    public float Range { get; private set; }

    public float ProjectileSpeed { get; private set; }

    public float AreaRadius { get; private set; }
    public float AreaHeight { get; private set; }
    public float TickInterval { get; private set; }
    public float ChargeTime { get; private set; }

    public float Duration { get; private set; }

    public int ChainCount { get; private set; }
    public float ChainJumpRange { get; private set; }

    public float NextCastTime { get; private set; }

    private readonly Dictionary<string, int> upgradeLevels = new();

    public SpellRuntime(SpellDefinition data)
    {
        if (data == null)
        {
            Debug.LogError("[SpellRuntime] SpellDefinition не призначено.");
            return;
        }

        Data = data;
        ResetStats();
    }

    private void ResetStats()
    {
        if (Data == null)
            return;

        Damage = Data.Damage;
        Cooldown = Data.Cooldown;
        ManaCost = Data.ManaCost;
        Range = Data.Range;

        ProjectileSpeed = Data.ProjectileSpeed;

        AreaRadius = Data.AreaRadius;
        AreaHeight = Data.AreaHeight;
        TickInterval = Data.TickInterval;
        ChargeTime = Data.ChargeTime;

        Duration = Data.Duration;

        ChainCount = Data.ChainCount;
        ChainJumpRange = Data.ChainJumpRange;
    }

    public int GetUpgradeLevel(string upgradeId)
    {
        if (string.IsNullOrWhiteSpace(upgradeId))
            return 0;

        return upgradeLevels.TryGetValue(upgradeId, out int level)
            ? level
            : 0;
    }

    public bool CanUpgrade(SpellUpgradeData upgrade)
    {
        if (upgrade == null ||
            string.IsNullOrWhiteSpace(upgrade.UpgradeId))
            return false;

        return GetUpgradeLevel(upgrade.UpgradeId) < upgrade.MaxLevel;
    }

    public bool TryApplyUpgrade(SpellUpgradeData upgrade, int newLevel)
    {
        if (!CanUpgrade(upgrade))
            return false;

        int currentLevel = GetUpgradeLevel(upgrade.UpgradeId);

        if (newLevel != currentLevel + 1 ||
            newLevel > upgrade.MaxLevel)
            return false;

        upgradeLevels[upgrade.UpgradeId] = newLevel;

        RecalculateStats();

        return true;
    }

    public bool RestoreUpgradeLevel(string upgradeId, int level)
    {
        if (Data == null ||
            string.IsNullOrWhiteSpace(upgradeId) ||
            level < 0 ||
            Data.Upgrades == null)
            return false;

        SpellUpgradeData selectedUpgrade = null;

        foreach (SpellUpgradeData upgrade in Data.Upgrades)
        {
            if (upgrade != null && upgrade.UpgradeId == upgradeId)
            {
                selectedUpgrade = upgrade;
                break;
            }
        }

        if (selectedUpgrade == null || level > selectedUpgrade.MaxLevel)
            return false;

        if (level == 0)
            upgradeLevels.Remove(upgradeId);
        else
            upgradeLevels[upgradeId] = level;

        RecalculateStats();

        return true;
    }

    private void RecalculateStats()
    {
        ResetStats();

        if (Data == null || Data.Upgrades == null)
            return;

        foreach (SpellUpgradeData upgrade in Data.Upgrades)
        {
            if (upgrade == null)
                continue;

            int level = GetUpgradeLevel(upgrade.UpgradeId);

            if (level <= 0)
                continue;

            float bonus = upgrade.ValuePerLevel * level;

            switch (upgrade.UpgradeType)
            {
                case SpellUpgradeType.Damage:
                    Damage += bonus;
                    break;

                case SpellUpgradeType.CooldownReduction:
                    Cooldown = Mathf.Max(0.1f, Cooldown - bonus);
                    break;

                case SpellUpgradeType.ManaCostReduction:
                    ManaCost = Mathf.Max(
                        0,
                        ManaCost - Mathf.RoundToInt(bonus));
                    break;

                case SpellUpgradeType.AreaIncrease:
                    AreaRadius += bonus;
                    break;

                case SpellUpgradeType.DurationIncrease:
                    Duration += bonus;
                    break;

                case SpellUpgradeType.ProjectileSpeed:
                    ProjectileSpeed += bonus;
                    break;

                case SpellUpgradeType.TickIntervalReduction:
                    TickInterval = Mathf.Max(
                        0.1f,
                        TickInterval - bonus);
                    break;

                case SpellUpgradeType.AreaHeightIncrease:
                    AreaHeight += bonus;
                    break;

                case SpellUpgradeType.ChainCount:
                    ChainCount += Mathf.RoundToInt(bonus);
                    break;

                case SpellUpgradeType.ChainRange:
                    ChainJumpRange += bonus;
                    break;
            }
        }
    }

    public bool CanCast(float now)
    {
        return Data != null && now >= NextCastTime;
    }

    public bool TryStartCast(float now)
    {
        if (!CanCast(now))
            return false;

        NextCastTime = now + Cooldown;
        return true;
    }

    public float GetRemainingCooldown(float now)
    {
        return Mathf.Max(0f, NextCastTime - now);
    }
}
