using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "SpellDatabase",
    menuName = "Nexus Defense/Magic/Spell Database"
)]
public sealed class SpellDatabase : ScriptableObject
{
    [SerializeField] private List<SpellDefinition> spells = new();

    public IReadOnlyList<SpellDefinition> Spells => spells;

    public SpellDefinition GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        foreach (SpellDefinition spell in spells)
        {
            if (spell != null && spell.SpellId == id)
                return spell;
        }

        return null;
    }
}