
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "NewCharacterDefinition",
    menuName = "Nexus Defense/Characters/Character Definition"
)]
public sealed class CharacterDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string classId;
    [SerializeField] private string displayName;

    [TextArea]
    [SerializeField] private string description;

    [SerializeField] private Sprite icon;

    [Header("Base Stats")]
    [Min(1)]
    [SerializeField] private int maxHealth = 100;

    [Min(0f)]
    [SerializeField] private float movementSpeed = 5f;

    [Header("Mechanics")]
    [Tooltip("Префаби механік, які активуються для цього класу.")]
    [SerializeField] private List<GameObject> mechanicPrefabs = new();

    public string ClassId => classId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;
    public int MaxHealth => maxHealth;
    public float MovementSpeed => movementSpeed;

    public IReadOnlyList<GameObject> MechanicPrefabs => mechanicPrefabs;

#if UNITY_EDITOR
    private void OnValidate()
    {
        classId = classId?.Trim().ToLowerInvariant();
        displayName = displayName?.Trim();

        maxHealth = Mathf.Max(1, maxHealth);
        movementSpeed = Mathf.Max(0f, movementSpeed);
    }
#endif
}
