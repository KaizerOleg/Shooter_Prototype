
using System;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MageUpgradeMenuUI : MonoBehaviour
{
    #region Inspector Settings

    [Header("Panel")]
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;

    [Header("Input")]
    [SerializeField] private Key toggleKey = Key.B;

    [Header("Mechanic")]
    [SerializeField] private string requiredMechanicId = "spell";

    [Header("UI")]
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private Transform spellList;
    [SerializeField] private Transform upgradeList;

    [Header("Prefabs")]
    [SerializeField] private Button spellButtonPrefab;
    [SerializeField] private Button upgradeButtonPrefab;

    #endregion

    #region Runtime References

    private CharacterRoot localCharacter;
    private ClassMechanicInstaller mechanicInstaller;

    private MageSpellMechanic mageMechanic;
    private PlayerSpellProgression progression;
    private SpellLoadout spellLoadout;

    private SpellDefinition selectedSpell;

    #endregion

    #region Initialization State

    private bool characterInitialized;
    private bool progressionSubscribed;
    private bool loadoutSubscribed;

    #endregion

    #region Unity Lifecycle

    private void Start()
    {
        if (upgradePanel != null)
            upgradePanel.SetActive(false);

        if (openButton != null)
            openButton.onClick.AddListener(OpenMenu);

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseMenu);
    }

    private void Update()
    {
        TryInitializeCharacter();
        TryBindMageMechanic();

        if (Keyboard.current != null &&
            toggleKey != Key.None &&
            Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            ToggleMenu();
        }

        if (upgradePanel != null &&
            upgradePanel.activeSelf &&
            !HasRequiredMechanic())
        {
            CloseMenu();
        }
    }

    private void OnDestroy()
    {
        UnsubscribeFromMageSystems();

        if (localCharacter != null)
            localCharacter.ClassApplied -= OnClassApplied;

        if (openButton != null)
            openButton.onClick.RemoveListener(OpenMenu);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseMenu);
    }

    #endregion

    #region Character Initialization

    private void TryInitializeCharacter()
    {
        if (characterInitialized)
            return;

        NetworkManager manager = NetworkManager.Singleton;

        if (manager == null ||
            !manager.IsListening ||
            manager.LocalClient == null)
            return;

        NetworkObject playerObject =
            manager.LocalClient.PlayerObject;

        if (playerObject == null)
            return;

        CharacterRoot character =
            playerObject.GetComponent<CharacterRoot>();

        ClassMechanicInstaller installer =
            playerObject.GetComponent<ClassMechanicInstaller>();

        if (character == null || installer == null)
            return;

        localCharacter = character;
        mechanicInstaller = installer;

        localCharacter.ClassApplied += OnClassApplied;

        characterInitialized = true;

        TryBindMageMechanic();
    }

    #endregion

    #region Mage Mechanic Binding

    private void TryBindMageMechanic()
    {
        if (!characterInitialized ||
            localCharacter == null ||
            mechanicInstaller == null)
            return;

        MageSpellMechanic foundMechanic = null;

        foreach (ClassMechanic mechanic in
                 mechanicInstaller.ActiveMechanics)
        {
            if (mechanic == null)
                continue;

            if (!string.Equals(
                    mechanic.MechanicId,
                    requiredMechanicId,
                    StringComparison.OrdinalIgnoreCase))
                continue;

            MageSpellMechanic candidate =
                mechanic as MageSpellMechanic;

            if (candidate == null || !candidate.IsLocalOwner)
                continue;

            foundMechanic = candidate;
            break;
        }

        if (mageMechanic != foundMechanic)
        {
            UnsubscribeFromMageSystems();

            mageMechanic = foundMechanic;

            progression = null;
            spellLoadout = null;

            selectedSpell = null;

            ClearChildren(spellList);
            ClearChildren(upgradeList);
        }

        if (mageMechanic == null)
            return;

        PlayerSpellProgression newProgression =
            mageMechanic.Progression;

        SpellLoadout newLoadout =
            mageMechanic.Loadout;

        if (progression != newProgression)
        {
            if (progressionSubscribed && progression != null)
                progression.ProgressionChanged -= RefreshUI;

            progression = newProgression;
            progressionSubscribed = false;

            if (progression != null)
            {
                progression.ProgressionChanged += RefreshUI;
                progressionSubscribed = true;
            }
        }

        if (spellLoadout != newLoadout)
        {
            if (loadoutSubscribed && spellLoadout != null)
                spellLoadout.LoadoutChanged -= OnLoadoutChanged;

            spellLoadout = newLoadout;
            loadoutSubscribed = false;

            if (spellLoadout != null)
            {
                spellLoadout.LoadoutChanged += OnLoadoutChanged;
                loadoutSubscribed = true;

                BuildSpellList();
            }
        }
    }

    private bool HasRequiredMechanic()
    {
        if (!characterInitialized ||
            localCharacter == null ||
            mechanicInstaller == null ||
            !localCharacter.IsSpawned ||
            !localCharacter.HasSelectedClass ||
            mageMechanic == null ||
            !mageMechanic.IsLocalOwner)
        {
            return false;
        }

        return string.Equals(
            mageMechanic.MechanicId,
            requiredMechanicId,
            StringComparison.OrdinalIgnoreCase);
    }

    private bool IsReady()
    {
        return HasRequiredMechanic() &&
               progression != null &&
               spellLoadout != null;
    }

    #endregion

    #region Menu Controls

    private void ToggleMenu()
    {
        if (upgradePanel == null)
            return;

        if (upgradePanel.activeSelf)
        {
            CloseMenu();
            return;
        }

        OpenMenu();
    }

    private void OpenMenu()
    {
        TryInitializeCharacter();
        TryBindMageMechanic();

        if (!IsReady() || upgradePanel == null)
            return;

        upgradePanel.SetActive(true);

        BuildSpellList();
        RefreshUI();
    }

    private void CloseMenu()
    {
        if (upgradePanel != null)
            upgradePanel.SetActive(false);
    }

    #endregion

    #region Class Change Handling

    private void OnClassApplied(CharacterDefinition definition)
    {
        selectedSpell = null;

        ClearChildren(spellList);
        ClearChildren(upgradeList);

        mageMechanic = null;
        progression = null;
        spellLoadout = null;

        progressionSubscribed = false;
        loadoutSubscribed = false;

        if (upgradePanel != null)
            upgradePanel.SetActive(false);

        TryBindMageMechanic();
    }

    #endregion

    #region Loadout Events

    private void OnLoadoutChanged()
    {
        if (!IsReady())
            return;

        BuildSpellList();
        RefreshUI();
    }

    #endregion

    #region UI Refresh

    private void RefreshUI()
    {
        if (!IsReady())
            return;

        RefreshPoints();

        if (selectedSpell != null)
            BuildUpgradeList();
    }

    private void RefreshPoints()
    {
        if (pointsText == null || progression == null)
            return;

        pointsText.text =
            $"Очки прокачки заклять: {progression.UpgradePoints}";
    }

    #endregion

    #region Spell List

    private void BuildSpellList()
    {
        ClearChildren(spellList);
        ClearChildren(upgradeList);

        selectedSpell = null;

        if (!IsReady() ||
            spellList == null ||
            spellButtonPrefab == null)
            return;

        for (int i = 0; i < SpellLoadout.SlotCount; i++)
        {
            SpellDefinition spell = spellLoadout.GetSpell(i);

            if (spell == null)
                continue;

            Button button =
                Instantiate(spellButtonPrefab, spellList);

            TMP_Text label =
                button.GetComponentInChildren<TMP_Text>();

            if (label != null)
                label.text = spell.DisplayName;

            SpellDefinition capturedSpell = spell;

            button.onClick.AddListener(
                () => SelectSpell(capturedSpell));

            if (selectedSpell == null)
                selectedSpell = spell;
        }

        if (selectedSpell != null)
            BuildUpgradeList();
    }

    private void SelectSpell(SpellDefinition spell)
    {
        if (!IsReady() || spell == null)
            return;

        selectedSpell = spell;

        BuildUpgradeList();
    }

    #endregion

    #region Upgrade List

    private void BuildUpgradeList()
    {
        ClearChildren(upgradeList);

        if (!IsReady() ||
            selectedSpell == null ||
            selectedSpell.Upgrades == null ||
            upgradeButtonPrefab == null ||
            upgradeList == null)
            return;

        foreach (SpellUpgradeData upgrade in selectedSpell.Upgrades)
        {
            if (upgrade == null)
                continue;

            int currentLevel = progression.GetUpgradeLevel(
                selectedSpell.SpellId,
                upgrade.UpgradeId);

            bool maxLevelReached =
                currentLevel >= upgrade.MaxLevel;

            bool enoughPoints =
                progression.UpgradePoints >= upgrade.PointsCost;

            Button button =
                Instantiate(upgradeButtonPrefab, upgradeList);

            TMP_Text label =
                button.GetComponentInChildren<TMP_Text>();

            if (label != null)
            {
                string status = maxLevelReached
                    ? "МАКСИМУМ"
                    : $"Ціна: {upgrade.PointsCost} очк.";

                label.text =
                    $"{upgrade.UpgradeName}\n" +
                    $"{upgrade.Description}\n" +
                    $"Рівень: {currentLevel}/{upgrade.MaxLevel}\n" +
                    $"{status}";
            }

            button.interactable =
                !maxLevelReached && enoughPoints;

            string spellId = selectedSpell.SpellId;
            string upgradeId = upgrade.UpgradeId;

            button.onClick.AddListener(() =>
            {
                if (progression == null)
                    return;

                progression.RequestUpgrade(
                    spellId,
                    upgradeId);
            });
        }
    }

    #endregion

    #region UI Cleanup

    private void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    #endregion

    #region Event Subscriptions

    private void UnsubscribeFromMageSystems()
    {
        if (progressionSubscribed && progression != null)
            progression.ProgressionChanged -= RefreshUI;

        if (loadoutSubscribed && spellLoadout != null)
            spellLoadout.LoadoutChanged -= OnLoadoutChanged;

        progressionSubscribed = false;
        loadoutSubscribed = false;
    }

    #endregion
}
