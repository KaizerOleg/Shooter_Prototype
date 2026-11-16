
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CharacterSelectionEntry : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private Image iconImage;

    private CharacterDefinition definition;
    private Action<CharacterDefinition> onSelected;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    public void Setup(
        CharacterDefinition character,
        Action<CharacterDefinition> selectionCallback)
    {
        definition = character;
        onSelected = selectionCallback;

        if (titleText != null)
            titleText.text = character.DisplayName;

        if (iconImage != null)
        {
            iconImage.sprite = character.Icon;
            iconImage.enabled = character.Icon != null;
        }

        if (button != null)
        {
            button.onClick.RemoveListener(HandleClick);
            button.onClick.AddListener(HandleClick);
        }
    }

    private void HandleClick()
    {
        if (definition != null)
            onSelected?.Invoke(definition);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }
}
