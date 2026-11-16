
using UnityEngine;

public abstract class ClassMechanic : MonoBehaviour
{
    public abstract string MechanicId { get; }

    public virtual void Initialize(
        CharacterRoot character,
        CharacterDefinition definition)
    {
    }

    public virtual void Shutdown()
    {
    }
}
