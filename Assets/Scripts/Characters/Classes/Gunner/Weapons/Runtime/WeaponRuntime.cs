using System.Collections.Generic;
using UnityEngine;

public sealed class WeaponRuntime
{
    public WeaponDefinition Data { get; }

    public int CurrentAmmo { get; private set; }
    public bool IsReloading { get; private set; }
    public float NextFireTime { get; private set; }
    public float ReloadFinishTime { get; private set; }

    // Runtime характеристики
    public float Damage { get; private set; }
    public float ShotsPerSecond { get; private set; }
    public int MagazineSize { get; private set; }
    public float ReloadDuration { get; private set; }
    public float Range { get; private set; }

    private readonly Dictionary<string, int> upgradeLevels = new();

    public WeaponRuntime(WeaponDefinition data)
    {
        Data = data;
        ResetStats();
        CurrentAmmo = MagazineSize;
    }

    public bool RestoreUpgradeLevel(string upgradeId, int level)
    {
        if (string.IsNullOrWhiteSpace(upgradeId) || level < 0)
            return false;

        WeaponUpgradeData upgrade = null;

        foreach (WeaponUpgradeData item in Data.Upgrades)
        {
            if (item != null && item.UpgradeId == upgradeId)
            {
                upgrade = item;
                break;
            }
        }

        if (upgrade == null || level > upgrade.MaxLevel)
            return false;

        if (level == 0)
            upgradeLevels.Remove(upgradeId);
        else
            upgradeLevels[upgradeId] = level;

        RecalculateStats();
        return true;
    }

    private void ResetStats()
    {
        Damage = Data.Damage;
        ShotsPerSecond = Data.ShotsPerSecond;
        MagazineSize = Data.MagazineSize;
        ReloadDuration = Data.ReloadDuration;
        Range = Data.Range;
    }

    public int GetUpgradeLevel(string upgradeId)
    {
        return upgradeLevels.TryGetValue(upgradeId, out int level)
            ? level
            : 0;
    }

    public bool TryApplyUpgrade(
    WeaponUpgradeData upgrade,
    int newLevel)
    {
        if (upgrade == null ||
            string.IsNullOrWhiteSpace(upgrade.UpgradeId))
            return false;

        int currentLevel = GetUpgradeLevel(upgrade.UpgradeId);

        if (newLevel != currentLevel + 1 ||
            newLevel > upgrade.MaxLevel)
            return false;

        upgradeLevels[upgrade.UpgradeId] = newLevel;

        RecalculateStats();

        return true;
    }

    private void RecalculateStats()
    {
        ResetStats();

        foreach (WeaponUpgradeData upgrade in Data.Upgrades)
        {
            if (upgrade == null)
                continue;

            int level = GetUpgradeLevel(upgrade.UpgradeId);

            float bonus = upgrade.ValuePerLevel * level;

            switch (upgrade.UpgradeType)
            {
                case WeaponUpgradeType.Damage:
                    Damage += bonus;
                    break;

                case WeaponUpgradeType.MagazineSize:
                    MagazineSize += Mathf.RoundToInt(bonus);
                    break;

                case WeaponUpgradeType.FireRate:
                    ShotsPerSecond += bonus;
                    break;

                case WeaponUpgradeType.ReloadSpeed:
                    ReloadDuration = Mathf.Max(
                        0.1f,
                        ReloadDuration - bonus);
                    break;
            }
        }

        CurrentAmmo = Mathf.Min(CurrentAmmo, MagazineSize);
    }

    public bool CanUpgrade(WeaponUpgradeData upgrade)
    {
        if (upgrade == null)
            return false;

        return GetUpgradeLevel(upgrade.UpgradeId) <
               upgrade.MaxLevel;
    }

    public bool TryStartFire(float now)
    {
        Update(now);

        if (IsReloading ||
            CurrentAmmo <= 0 ||
            now < NextFireTime)
        {
            return false;
        }

        CurrentAmmo--;

        NextFireTime = now + 1f / ShotsPerSecond;

        return true;
    }

    public bool TryStartReload(float now)
    {
        Update(now);

        if (IsReloading || CurrentAmmo >= MagazineSize)
            return false;

        IsReloading = true;
        ReloadFinishTime = now + ReloadDuration;

        return true;
    }

    public void Update(float now)
    {
        if (!IsReloading || now < ReloadFinishTime)
            return;

        CurrentAmmo = MagazineSize;
        IsReloading = false;
    }
}