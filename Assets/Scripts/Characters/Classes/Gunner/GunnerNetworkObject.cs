
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlayerWeaponProgression))]
public sealed class GunnerNetworkObject : MonoBehaviour
{
    public NetworkObject NetworkObject { get; private set; }
    public PlayerWeaponProgression Progression { get; private set; }

    private void Awake()
    {
        NetworkObject = GetComponent<NetworkObject>();
        Progression = GetComponent<PlayerWeaponProgression>();
    }
}
