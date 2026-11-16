
using Unity.Netcode;
using UnityEngine;

public class PlayerHealthUI : NetworkBehaviour
{
    [SerializeField] private Health health;

    private void Awake()
    {
        if (health == null)
            health = GetComponent<Health>();
    }

    private void OnGUI()
    {
        if (!IsSpawned || !IsOwner || health == null)
            return;

        int current = health.CurrentHealth.Value;
        int maximum = health.MaxHealth;

        GUI.Box(new Rect(20, Screen.height - 70, 220, 45),
            $"HP: {current} / {maximum}");

        Rect barBackground = new Rect(
            30, Screen.height - 42, 200, 10);

        GUI.Box(barBackground, "");

        float ratio = maximum > 0
            ? Mathf.Clamp01((float)current / maximum)
            : 0f;

        GUI.Box(new Rect(
            barBackground.x + 2,
            barBackground.y + 2,
            (barBackground.width - 4) * ratio,
            barBackground.height - 4), "");

        if (health.IsDead.Value)
        {
            GUI.Box(new Rect(
                Screen.width / 2f - 100,
                Screen.height / 2f - 30,
                200,
                60), "YOU ARE DEAD");
        }
    }
}
