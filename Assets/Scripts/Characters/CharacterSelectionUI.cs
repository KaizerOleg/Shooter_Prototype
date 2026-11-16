
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public sealed class CharacterSelectionUI : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private CharacterDatabase characterDatabase;

    [Header("Panel")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Transform content;
    [SerializeField] private CharacterSelectionEntry entryPrefab;

    [Header("Selected Class")]
    [SerializeField] private TMP_Text selectedNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Image selectedIcon;

    [Header("Actions")]
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text statusText;

    public bool IsOpen => panel != null && panel.activeInHierarchy;

    private readonly List<CharacterSelectionEntry> entries = new();

    private CharacterDefinition selectedCharacter;
    private CharacterRoot localCharacter;
    private bool waitingForServer;

    private void OnEnable()
    {
        if (confirmButton != null)
            confirmButton.onClick.AddListener(ConfirmSelection);

        BuildEntries();
    }

    private void Update()
    {
        if (localCharacter == null || !localCharacter.IsSpawned)
            TryBindLocalCharacter();
    }

    private void OnDisable()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(ConfirmSelection);

        UnbindLocalCharacter();
    }

    private void BuildEntries()
    {
        if (characterDatabase == null || content == null || entryPrefab == null)
        {
            Debug.LogError(
                "[CharacterSelectionUI] Признач базу, Content та Entry Prefab.",
                this);
            return;
        }

        foreach (Transform child in content)
            Destroy(child.gameObject);

        entries.Clear();

        foreach (CharacterDefinition character in characterDatabase.Characters)
        {
            if (character == null)
                continue;

            CharacterSelectionEntry entry = Instantiate(entryPrefab, content);
            entry.Setup(character, SelectCharacter);
            entries.Add(entry);
        }

        if (entries.Count == 0)
            SetStatus("Немає доступних класів.");
        else
            SetStatus("Вибери клас.");

        if (confirmButton != null)
            confirmButton.interactable = false;
    }

    private void SelectCharacter(CharacterDefinition character)
    {
        if (waitingForServer || character == null)
            return;

        selectedCharacter = character;

        if (selectedNameText != null)
            selectedNameText.text = character.DisplayName;

        if (descriptionText != null)
            descriptionText.text = character.Description;

        if (selectedIcon != null)
        {
            selectedIcon.sprite = character.Icon;
            selectedIcon.enabled = character.Icon != null;
        }

        if (confirmButton != null)
            confirmButton.interactable = true;

        SetStatus($"Обрано: {character.DisplayName}");
    }

    private void ConfirmSelection()
    {
        if (waitingForServer || selectedCharacter == null)
            return;

        TryBindLocalCharacter();

        if (localCharacter == null || !localCharacter.IsSpawned)
        {
            SetStatus("Гравець ще підключається до сервера.");
            return;
        }

        if (localCharacter.HasSelectedClass)
        {
            SetStatus("Клас уже вибрано.");
            return;
        }

        waitingForServer = true;

        if (confirmButton != null)
            confirmButton.interactable = false;

        SetStatus("Очікуємо підтвердження сервера...");

        localCharacter.RequestClassSelection(selectedCharacter.ClassId);
    }

    private void TryBindLocalCharacter()
    {
        NetworkManager manager = NetworkManager.Singleton;

        if (manager == null || !manager.IsListening ||
            manager.LocalClient == null)
            return;

        NetworkObject playerObject = manager.LocalClient.PlayerObject;

        if (playerObject == null)
            return;

        CharacterRoot root = playerObject.GetComponent<CharacterRoot>();

        if (root == null || root == localCharacter)
            return;

        UnbindLocalCharacter();

        localCharacter = root;
        localCharacter.ClassApplied += HandleClassApplied;

        if (localCharacter.HasSelectedClass &&
            localCharacter.Definition != null)
        {
            HandleClassApplied(localCharacter.Definition);
        }
    }

    private void HandleClassApplied(CharacterDefinition character)
    {
        waitingForServer = false;
        SetStatus($"Клас підтверджено: {character.DisplayName}");

        if (panel != null)
            panel.SetActive(false);
    }

    private void UnbindLocalCharacter()
    {
        if (localCharacter != null)
            localCharacter.ClassApplied -= HandleClassApplied;

        localCharacter = null;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
            statusText.text = message;
    }
}
