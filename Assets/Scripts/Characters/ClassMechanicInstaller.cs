
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class ClassMechanicInstaller : MonoBehaviour
{
    private readonly List<ClassMechanic> activeMechanics = new();
    private bool initialized;

    public IReadOnlyList<ClassMechanic> ActiveMechanics => activeMechanics;

    public void Initialize(
        CharacterRoot character,
        CharacterDefinition definition)
    {
        Shutdown();

        if (character == null || definition == null)
        {
            Debug.LogError(
                "[ClassMechanicInstaller] Не передано персонажа або клас.",
                this);
            return;
        }

        HashSet<string> mechanicIds = new();

        foreach (GameObject prefab in definition.MechanicPrefabs)
        {
            if (prefab == null)
            {
                Debug.LogError(
                    "[ClassMechanicInstaller] У класі є порожній префаб механіки.",
                    this);
                continue;
            }

            ClassMechanic source = prefab.GetComponent<ClassMechanic>();

            if (source == null)
            {
                Debug.LogError(
                    $"[ClassMechanicInstaller] На префабі {prefab.name} " +
                    "немає ClassMechanic.",
                    prefab);
                continue;
            }

            if (string.IsNullOrWhiteSpace(source.MechanicId))
            {
                Debug.LogError(
                    $"[ClassMechanicInstaller] Порожній MechanicId: {prefab.name}.",
                    prefab);
                continue;
            }

            if (!mechanicIds.Add(source.MechanicId))
            {
                Debug.LogError(
                    $"[ClassMechanicInstaller] Дублікат механіки " +
                    $"'{source.MechanicId}' у класі '{definition.ClassId}'.",
                    this);
                continue;
            }

            GameObject instance = Instantiate(prefab, transform);
            instance.name = prefab.name;

            ClassMechanic mechanic = instance.GetComponent<ClassMechanic>();

            if (mechanic == null)
            {
                Destroy(instance);
                continue;
            }

            activeMechanics.Add(mechanic);
        }

        foreach (ClassMechanic mechanic in activeMechanics)
            mechanic.Initialize(character, definition);

        initialized = true;
    }

    public void Shutdown()
    {
        if (initialized)
        {
            foreach (ClassMechanic mechanic in activeMechanics)
            {
                if (mechanic != null)
                    mechanic.Shutdown();
            }
        }

        foreach (ClassMechanic mechanic in activeMechanics)
        {
            if (mechanic != null)
                Destroy(mechanic.gameObject);
        }

        activeMechanics.Clear();
        initialized = false;
    }

    private void OnDestroy()
    {
        Shutdown();
    }
}
