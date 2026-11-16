
using System;
using Unity.Netcode;
using UnityEngine;

public class NexusController : NetworkBehaviour
{
    [Header("Nexus Settings")]
    [SerializeField] private int maxHealth = 1000;

    public NetworkVariable<int> CurrentHealth = new(
        1000,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> IsDestroyed = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public int MaxHealth => maxHealth;

    public event Action<int, int> HealthChanged;
    public event Action NexusDestroyed;

    public override void OnNetworkSpawn()
    {
        CurrentHealth.OnValueChanged += HandleHealthChanged;
        IsDestroyed.OnValueChanged += HandleDestroyedChanged;

        if (IsServer)
        {
            CurrentHealth.Value = maxHealth;
            IsDestroyed.Value = false;
        }

        HealthChanged?.Invoke(CurrentHealth.Value, maxHealth);
    }

    public override void OnNetworkDespawn()
    {
        CurrentHealth.OnValueChanged -= HandleHealthChanged;
        IsDestroyed.OnValueChanged -= HandleDestroyedChanged;
    }

    public void ApplyDamage(int amount)
    {
        if (!IsServer || IsDestroyed.Value || amount <= 0)
            return;

        CurrentHealth.Value = Mathf.Max(
            0, CurrentHealth.Value - amount);

        Debug.Log(
            $"[SERVER] Nexus HP: {CurrentHealth.Value}/{maxHealth}");

        if (CurrentHealth.Value <= 0)
            IsDestroyed.Value = true;
    }

    public void RestoreHealth(int amount)
    {
        if (!IsServer || IsDestroyed.Value || amount <= 0)
            return;

        CurrentHealth.Value = Mathf.Min(
            maxHealth, CurrentHealth.Value + amount);
    }

    private void HandleHealthChanged(int previous, int current)
    {
        HealthChanged?.Invoke(current, maxHealth);
    }

    private void HandleDestroyedChanged(bool previous, bool current)
    {
        if (!current)
            return;

        Debug.Log("[GAME] NEXUS DESTROYED");
        NexusDestroyed?.Invoke();
    }
}
