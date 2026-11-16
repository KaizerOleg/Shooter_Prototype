using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlayerSpellProgression))]
[RequireComponent(typeof(SpellLoadout))]
[RequireComponent(typeof(PlayerMana))]
[RequireComponent(typeof(MageSpellCaster))]
public sealed class MageSpellNetworkObject : MonoBehaviour
{
    public NetworkObject NetworkObject { get; private set; }
    public PlayerSpellProgression Progression { get; private set; }
    public SpellLoadout Loadout { get; private set; }
    public PlayerMana Mana { get; private set; }
    public MageSpellCaster Caster { get; private set; }

    private void Awake()
    {
        NetworkObject = GetComponent<NetworkObject>();
        Progression = GetComponent<PlayerSpellProgression>();
        Loadout = GetComponent<SpellLoadout>();
        Mana = GetComponent<PlayerMana>();
        Caster = GetComponent<MageSpellCaster>();
    }
}