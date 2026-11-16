
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "WeaponDatabase",
    menuName = "Nexus Defense/Weapons/Weapon Database")]
public sealed class WeaponDatabase : ScriptableObject
{
    [SerializeField] private List<WeaponDefinition> weapons = new();

    public IReadOnlyList<WeaponDefinition> Weapons => weapons;

    public WeaponDefinition GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        foreach (WeaponDefinition weapon in weapons)
        {
            if (weapon != null && weapon.WeaponId == id)
                return weapon;
        }

        return null;
    }

    public int IndexOf(string id)
    {
        for (int i = 0; i < weapons.Count; i++)
        {
            if (weapons[i] != null && weapons[i].WeaponId == id)
                return i;
        }

        return -1;
    }

    public WeaponDefinition GetAt(int index)
    {
        if (index < 0 || index >= weapons.Count)
            return null;

        return weapons[index];
    }
}
