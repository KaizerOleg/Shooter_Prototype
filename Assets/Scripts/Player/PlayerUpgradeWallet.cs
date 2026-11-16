using System;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class PlayerUpgradeWallet : NetworkBehaviour
{
    private readonly NetworkVariable<int> points = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public int Points => points.Value;

    public event Action<int, int> PointsChanged;

    public override void OnNetworkSpawn()
    {
        points.OnValueChanged += HandlePointsChanged;
    }

    public override void OnNetworkDespawn()
    {
        points.OnValueChanged -= HandlePointsChanged;
    }

    public void AddPoints(int amount)
    {
        if (!IsServer || !IsSpawned || amount <= 0)
            return;

        long newValue = (long)points.Value + amount;

        points.Value = (int)Math.Min(newValue, int.MaxValue);
    }

    public bool TrySpendPoints(int amount)
    {
        if (!IsServer ||
            !IsSpawned ||
            amount < 0 ||
            points.Value < amount)
        {
            return false;
        }

        points.Value -= amount;
        return true;
    }

    private void HandlePointsChanged(int previous, int current)
    {
        PointsChanged?.Invoke(previous, current);
    }
}