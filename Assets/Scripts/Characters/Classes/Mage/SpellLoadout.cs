using System;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class SpellLoadout : NetworkBehaviour
{
    public const int SlotCount = 3;

    private readonly NetworkList<FixedString64Bytes> selectedSpells =
        new(
            null,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    private PlayerSpellProgression progression;

    public event Action LoadoutChanged;

    public override void OnNetworkSpawn()
    {
        progression = GetComponent<PlayerSpellProgression>();

        selectedSpells.OnListChanged += OnSelectedSpellsChanged;

        if (IsServer)
        {
            while (selectedSpells.Count < SlotCount)
                selectedSpells.Add(default);

            while (selectedSpells.Count > SlotCount)
                selectedSpells.RemoveAt(selectedSpells.Count - 1);
        }
    }

    public override void OnNetworkDespawn()
    {
        selectedSpells.OnListChanged -= OnSelectedSpellsChanged;
        progression = null;
    }

    private void OnSelectedSpellsChanged(
        NetworkListEvent<FixedString64Bytes> change)
    {
        LoadoutChanged?.Invoke();
    }

    public string GetSpellId(int slot)
    {
        if (slot < 0 || slot >= selectedSpells.Count)
            return string.Empty;

        return selectedSpells[slot].ToString();
    }

    public SpellDefinition GetSpell(int slot)
    {
        if (progression == null)
            return null;

        string spellId = GetSpellId(slot);

        return string.IsNullOrEmpty(spellId)
            ? null
            : progression.GetSpellDefinition(spellId);
    }

    public void RequestSetSpell(int slot, string spellId)
    {
        if (!IsSpawned || !IsOwner)
            return;

        if (slot < 0 || slot >= SlotCount)
            return;

        if (spellId == null || spellId.Length > 64)
            return;

        SetSpellServerRpc(
            slot,
            new FixedString64Bytes(spellId));
    }

    [ServerRpc]
    private void SetSpellServerRpc(
        int slot,
        FixedString64Bytes requestedSpellId)
    {
        if (!IsServer ||
            slot < 0 ||
            slot >= SlotCount ||
            progression == null ||
            selectedSpells.Count != SlotCount)
            return;

        string spellId = requestedSpellId.ToString();

        if (!requestedSpellId.IsEmpty &&
            progression.GetSpellDefinition(spellId) == null)
            return;

        if (!requestedSpellId.IsEmpty)
        {
            for (int i = 0; i < selectedSpells.Count; i++)
            {
                if (i == slot)
                    continue;

                if (selectedSpells[i].Equals(requestedSpellId))
                    return;
            }
        }

        selectedSpells[slot] = requestedSpellId;
    }
}