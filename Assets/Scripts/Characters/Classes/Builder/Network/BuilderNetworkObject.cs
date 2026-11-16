
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(BuilderProgression))]
public sealed class BuilderNetworkObject : MonoBehaviour
{
    public NetworkObject NetworkObject { get; private set; }
    public BuilderProgression Progression { get; private set; }

    private void Awake()
    {
        NetworkObject = GetComponent<NetworkObject>();
        Progression = GetComponent<BuilderProgression>();
    }
}
