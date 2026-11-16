
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Events;

public sealed class SpellLoadoutUI : MonoBehaviour
{
    [Header("Menu")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Key toggleKey = Key.K;

    [Header("Database")]
    [SerializeField] private SpellDatabase spellDatabase;

    [Header("Slot Dropdowns")]
    [SerializeField] private TMP_Dropdown slot1Dropdown;
    [SerializeField] private TMP_Dropdown slot2Dropdown;
    [SerializeField] private TMP_Dropdown slot3Dropdown;

    private TMP_Dropdown[] dropdowns;
    private UnityAction<int>[] dropdownListeners;

    private readonly List<SpellDefinition> availableSpells = new();

    private SpellLoadout loadout;

    private bool isMenuOpen;
    private bool isRefreshing;

    public bool IsMenuOpen => isMenuOpen;

    private void Awake()
    {
        dropdowns = new[]
        {
            slot1Dropdown,
            slot2Dropdown,
            slot3Dropdown
        };

        dropdownListeners = new UnityAction<int>[SpellLoadout.SlotCount];
    }

    private void Start()
    {
        SetMenuVisible(false);
    }

    private void OnEnable()
    {
        if (spellDatabase == null)
        {
            Debug.LogError(
                "[SpellLoadoutUI] Не призначено SpellDatabase.",
                this);
            return;
        }

        BuildSpellList();

        for (int i = 0; i < dropdowns.Length; i++)
        {
            int slot = i;

            if (dropdowns[i] == null)
                continue;

            PopulateDropdown(dropdowns[i]);

            dropdownListeners[i] =
                value => OnSlotChanged(slot, value);

            dropdowns[i].onValueChanged.AddListener(
                dropdownListeners[i]);
        }

        TryBindLoadout();
    }

    private void Update()
    {
        if (Keyboard.current != null &&
            Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            ToggleMenu();
        }

        if (loadout == null)
            TryBindLoadout();
    }

    public void ToggleMenu()
    {
        SetMenuVisible(!isMenuOpen);
    }

    public void OpenMenu()
    {
        SetMenuVisible(true);
    }

    public void CloseMenu()
    {
        SetMenuVisible(false);
    }

    private void SetMenuVisible(bool visible)
    {
        isMenuOpen = visible;

        if (menuPanel != null)
            menuPanel.SetActive(visible);
    }

    private void BuildSpellList()
    {
        availableSpells.Clear();

        foreach (SpellDefinition spell in spellDatabase.Spells)
        {
            if (spell != null)
                availableSpells.Add(spell);
        }
    }

    private void TryBindLoadout()
    {
        MageSpellMechanic[] mechanics =
            FindObjectsByType<MageSpellMechanic>(
                FindObjectsSortMode.None);

        foreach (MageSpellMechanic mechanic in mechanics)
        {
            if (mechanic == null ||
                !mechanic.IsLocalOwner ||
                mechanic.Loadout == null)
                continue;

            if (loadout == mechanic.Loadout)
                return;

            if (loadout != null)
                loadout.LoadoutChanged -= OnLoadoutChanged;

            loadout = mechanic.Loadout;
            loadout.LoadoutChanged += OnLoadoutChanged;

            RefreshDropdowns();

            Debug.Log(
                "[SpellLoadoutUI] SpellLoadout підключено.",
                this);

            return;
        }
    }

    private void PopulateDropdown(TMP_Dropdown dropdown)
    {
        dropdown.ClearOptions();

        List<TMP_Dropdown.OptionData> options = new();

        options.Add(
            new TMP_Dropdown.OptionData("Порожньо"));

        foreach (SpellDefinition spell in availableSpells)
        {
            options.Add(
                new TMP_Dropdown.OptionData(spell.DisplayName));
        }

        dropdown.AddOptions(options);
    }

    private void OnSlotChanged(int slot, int optionIndex)
    {
        if (isRefreshing || loadout == null)
            return;

        if (optionIndex < 0 ||
            optionIndex > availableSpells.Count)
            return;

        string selectedSpellId = string.Empty;

        if (optionIndex > 0)
        {
            SpellDefinition selectedSpell =
                availableSpells[optionIndex - 1];

            if (selectedSpell == null)
                return;

            selectedSpellId = selectedSpell.SpellId;
        }

        if (!string.IsNullOrEmpty(selectedSpellId))
        {
            for (int i = 0; i < SpellLoadout.SlotCount; i++)
            {
                if (i == slot)
                    continue;

                if (loadout.GetSpellId(i) == selectedSpellId)
                {
                    Debug.LogWarning(
                        "[SpellLoadoutUI] Це закляття вже призначене в іншому слоті.");

                    RefreshDropdowns();
                    return;
                }
            }
        }

        loadout.RequestSetSpell(slot, selectedSpellId);
    }

    private void OnLoadoutChanged()
    {
        RefreshDropdowns();
    }

    private void RefreshDropdowns()
    {
        if (loadout == null)
            return;

        isRefreshing = true;

        for (int slot = 0; slot < dropdowns.Length; slot++)
        {
            TMP_Dropdown dropdown = dropdowns[slot];

            if (dropdown == null)
                continue;

            string spellId = loadout.GetSpellId(slot);

            int optionIndex = 0;

            if (!string.IsNullOrEmpty(spellId))
            {
                for (int i = 0; i < availableSpells.Count; i++)
                {
                    if (availableSpells[i].SpellId == spellId)
                    {
                        optionIndex = i + 1;
                        break;
                    }
                }
            }

            dropdown.SetValueWithoutNotify(optionIndex);
            dropdown.RefreshShownValue();
        }

        isRefreshing = false;
    }

    private void OnDisable()
    {
        if (loadout != null)
        {
            loadout.LoadoutChanged -= OnLoadoutChanged;
            loadout = null;
        }

        if (dropdowns == null || dropdownListeners == null)
            return;

        for (int i = 0; i < dropdowns.Length; i++)
        {
            if (dropdowns[i] != null &&
                dropdownListeners[i] != null)
            {
                dropdowns[i].onValueChanged.RemoveListener(
                    dropdownListeners[i]);
            }

            dropdownListeners[i] = null;
        }

        isMenuOpen = false;
    }
}
