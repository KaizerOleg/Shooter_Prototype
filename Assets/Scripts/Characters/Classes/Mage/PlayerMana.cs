
using System;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public sealed class PlayerMana : NetworkBehaviour
{
    [Header("Mana")]
    [SerializeField, Min(1)]
    private int maxMana = 100;

    [SerializeField, Min(0f)]
    private float regenerationPerSecond = 5f;

    [Header("Temporary HUD")]
    [SerializeField]
    private bool showTemporaryHUD = true;

    [SerializeField, Min(100)]
    private float hudWidth = 260f;

    [SerializeField, Min(10)]
    private float hudHeight = 22f;

    [SerializeField, Min(0)]
    private float hudMargin = 25f;

    private readonly NetworkVariable<int> currentMana = new(
        100,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public int MaxMana => maxMana;
    public int CurrentMana => currentMana.Value;

    public event Action<int, int> ManaChanged;

    private float regenerationRemainder;

    private GUIStyle manaTextStyle;
    private GUIStyle manaBackgroundStyle;

    public override void OnNetworkSpawn()
    {
        currentMana.OnValueChanged += OnManaChanged;

        if (IsServer)
        {
            currentMana.Value = maxMana;
            regenerationRemainder = 0f;
        }
    }

    public override void OnNetworkDespawn()
    {
        currentMana.OnValueChanged -= OnManaChanged;
        base.OnNetworkDespawn();
    }

    private void Update()
    {
        if (!IsServer || !IsSpawned || regenerationPerSecond <= 0f)
            return;

        if (currentMana.Value >= maxMana)
        {
            regenerationRemainder = 0f;
            return;
        }

        float regeneration = regenerationPerSecond * Time.deltaTime;
        regenerationRemainder += regeneration;

        int wholePoints = Mathf.FloorToInt(regenerationRemainder);

        if (wholePoints <= 0)
            return;

        regenerationRemainder -= wholePoints;

        currentMana.Value = Mathf.Min(
            maxMana,
            currentMana.Value + wholePoints);
    }

    private void OnManaChanged(int previous, int current)
    {
        ManaChanged?.Invoke(previous, current);
    }

    public bool CanSpend(int amount)
    {
        return IsServer &&
               amount >= 0 &&
               currentMana.Value >= amount;
    }

    public bool TrySpend(int amount)
    {
        if (!CanSpend(amount))
            return false;

        currentMana.Value -= amount;
        return true;
    }

    public void Restore(int amount)
    {
        if (!IsServer || amount <= 0)
            return;

        currentMana.Value = Mathf.Min(
            maxMana,
            currentMana.Value + amount);
    }

    private void InitializeHUDStyles()
    {
        if (manaTextStyle != null)
            return;

        manaTextStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            normal =
            {
                textColor = Color.white
            }
        };

        manaBackgroundStyle = new GUIStyle(GUI.skin.box);
    }

    private void OnGUI()
    {
        if (!showTemporaryHUD || !IsSpawned || !IsOwner)
            return;

        InitializeHUDStyles();

        float x = hudMargin * 12;
        float y = Screen.height - hudMargin - hudHeight;

        Rect backgroundRect = new Rect(
            x,
            y,
            hudWidth,
            hudHeight);

        Rect fillRect = new Rect(
            x + 2f,
            y + 2f,
            (hudWidth - 4f) * GetManaPercent(),
            hudHeight - 4f);

        Color previousColor = GUI.color;

        GUI.color = new Color(0.08f, 0.08f, 0.12f, 0.95f);
        GUI.Box(backgroundRect, GUIContent.none, manaBackgroundStyle);

        GUI.color = new Color(0.05f, 0.35f, 1f, 0.95f);
        GUI.DrawTexture(fillRect, Texture2D.whiteTexture);

        GUI.color = Color.white;

        GUI.Label(
            backgroundRect,
            $"{currentMana.Value} / {maxMana} MANA",
            manaTextStyle);

        GUI.color = previousColor;
    }

    private float GetManaPercent()
    {
        if (maxMana <= 0)
            return 0f;

        return Mathf.Clamp01(
            (float)currentMana.Value / maxMana);
    }
}
