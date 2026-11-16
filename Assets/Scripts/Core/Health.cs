
using System;
using Unity.Netcode;
using UnityEngine;

public class Health : NetworkBehaviour, IDamageable
{
    [Header("Health Settings")]
    [SerializeField] private int maxHealth = 100;

    public NetworkVariable<int> CurrentHealth = new(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public NetworkVariable<bool> IsDead = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public int MaxHealth => maxHealth;
    private HitFlashEffect hitFlashEffect;
    public event Action<int, int> HealthChanged;
    public event Action Died;

    public bool IsAlive => !IsDead.Value;

    public override void OnNetworkSpawn()
    {
        CurrentHealth.OnValueChanged += OnHealthValueChanged;
        IsDead.OnValueChanged += OnDeathValueChanged;

        hitFlashEffect = GetComponent<HitFlashEffect>();

        if (hitFlashEffect == null)
            hitFlashEffect = GetComponentInChildren<HitFlashEffect>();

        if (IsServer)
        {
            CurrentHealth.Value = maxHealth;
            IsDead.Value = false;
        }

        HealthChanged?.Invoke(CurrentHealth.Value, maxHealth);
    }

    public override void OnNetworkDespawn()
    {
        CurrentHealth.OnValueChanged -= OnHealthValueChanged;
        IsDead.OnValueChanged -= OnDeathValueChanged;
    }

    public void ApplyDamage(int amount)
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                $"ApplyDamage викликано не на сервері: {name}");
            return;
        }

        if (IsDead.Value || amount <= 0)
            return;

        HitFlashClientRpc();

        int previousHealth = CurrentHealth.Value;

        CurrentHealth.Value = Mathf.Max(
            0, CurrentHealth.Value - amount);

        Debug.Log(
            $"[SERVER] {name}: HP {previousHealth} -> {CurrentHealth.Value}");

        if (CurrentHealth.Value == 0)
            IsDead.Value = true;
    }

    [ClientRpc]
    private void HitFlashClientRpc()
    {
        if (hitFlashEffect != null)
            hitFlashEffect.Play();
    }

    public void RestoreHealth(int amount)
    {
        if (!IsServer || IsDead.Value || amount <= 0)
            return;

        CurrentHealth.Value = Mathf.Min(
            maxHealth,
            CurrentHealth.Value + amount);
    }

    public void ResetHealth()
    {
        if (!IsServer)
            return;

        CurrentHealth.Value = maxHealth;
        IsDead.Value = false;
    }

    private void OnHealthValueChanged(int previous, int current)
    {
        HealthChanged?.Invoke(current, maxHealth);
    }

    private void OnDeathValueChanged(bool previous, bool current)
    {
        if (current)
            Died?.Invoke();
    }
}
