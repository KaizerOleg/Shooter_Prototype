
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "CharacterDatabase",
    menuName = "Nexus Defense/Database/Character Database"
)]
public sealed class CharacterDatabase : ScriptableObject
{
    [SerializeField]
    private List<CharacterDefinition> characters = new();

    public IReadOnlyList<CharacterDefinition> Characters => characters;

    public CharacterDefinition GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        id = id.Trim().ToLowerInvariant();

        foreach (CharacterDefinition character in characters)
        {
            if (character != null && character.ClassId == id)
                return character;
        }

        return null;
    }
}
