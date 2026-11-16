
using Unity.Netcode;
using UnityEngine;

public class NexusHealthUI : MonoBehaviour
{
    [SerializeField] private NexusController nexus;

    private void OnGUI()
    {
        if (nexus == null || !nexus.IsSpawned)
            return;

        int hp = nexus.CurrentHealth.Value;
        int maxHp = nexus.MaxHealth;

        float width = 320f;
        float x = (Screen.width - width) / 2f;

        GUI.Box(
            new Rect(x, 20, width, 50),
            $"NEXUS  {hp} / {maxHp}");

        float ratio = maxHp > 0
            ? Mathf.Clamp01((float)hp / maxHp)
            : 0f;

        GUI.Box(
            new Rect(x + 10, 52, (width - 20) * ratio, 8),
            "");

        if (nexus.IsDestroyed.Value)
        {
            GUI.Box(
                new Rect(
                    Screen.width / 2f - 150,
                    Screen.height / 2f - 40,
                    300,
                    80),
                "DEFEAT — NEXUS DESTROYED");
        }
    }
}
