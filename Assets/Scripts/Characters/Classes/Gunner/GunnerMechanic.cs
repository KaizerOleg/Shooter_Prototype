
using UnityEngine;

public sealed class GunnerMechanic : ClassMechanic
{
    public override string MechanicId => "gunner";

    public override void Initialize(
        CharacterRoot character,
        CharacterDefinition definition)
    {
        base.Initialize(character, definition);

        Debug.Log(
            $"[GunnerMechanic] Ініціалізовано клас: {definition.DisplayName}",
            this);
    }

    public override void Shutdown()
    {
        base.Shutdown();
    }
}
