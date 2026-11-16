
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class UpgradeMenuUI : MonoBehaviour
{
    [Header("Panel")]
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private Button openButton;
    [SerializeField] private Button closeButton;

    [Header("Input")]
    [SerializeField] private Key toggleKey = Key.B;

    [Header("Mechanic")]
    [SerializeField] private string requiredMechanicId = "weapon";

    [Header("UI")]
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private Transform weaponList;
    [SerializeField] private Transform upgradeList;

    [Header("Prefabs")]
    [SerializeField] private Button weaponButtonPrefab;
    [SerializeField] private Button upgradeButtonPrefab;

    private CharacterRoot localCharacter;
    private ClassMechanicInstaller mechanicInstaller;
    private PlayerWeaponProgression progression;

    private WeaponDefinition selectedWeapon;

    private bool initialized;

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
        if (!initialized)
            TryInitialize();

        // GunnerNetworkObject може з'явитися пізніше,
        // ніж кореневий PlayerObject.
        if (initialized && progression == null)
            TryBindProgression();

        if (Keyboard.current != null &&
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

    private void TryInitialize()
    {
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

        initialized = true;

        TryBindProgression();

        RefreshUI();
    }

    private void TryBindProgression()
    {
        if (localCharacter == null)
            return;

        if (progression != null)
            return;

        // PlayerWeaponProgression знаходиться на окремому
        // GunnerNetworkObject, а не на корені персонажа.
        PlayerWeaponProgression foundProgression =
            localCharacter.GetComponentInChildren<PlayerWeaponProgression>(true);

        if (foundProgression == null)
            return;

        if (!foundProgression.IsSpawned)
            return;

        progression = foundProgression;
        progression.ProgressionChanged += RefreshUI;

        Debug.Log(
            "[UpgradeMenuUI] PlayerWeaponProgression підключено: " +
            progression.name);

        RefreshUI();
    }

    private bool HasRequiredMechanic()
    {
        if (!initialized ||
            localCharacter == null ||
            mechanicInstaller == null ||
            !localCharacter.IsSpawned ||
            !localCharacter.HasSelectedClass)
            return false;

        foreach (ClassMechanic mechanic in
                 mechanicInstaller.ActiveMechanics)
        {
            if (mechanic == null)
                continue;

            if (string.Equals(
                    mechanic.MechanicId,
                    requiredMechanicId,
                    System.StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

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
        if (!initialized)
            TryInitialize();

        if (progression == null)
            TryBindProgression();

        if (!HasRequiredMechanic() ||
            progression == null ||
            upgradePanel == null)
            return;

        upgradePanel.SetActive(true);

        BuildWeaponList();
        RefreshUI();

        Debug.Log("[UpgradeMenuUI] Меню прокачки відкрите.");
    }

    private void CloseMenu()
    {
        if (upgradePanel != null)
            upgradePanel.SetActive(false);
    }

    private void OnClassApplied(CharacterDefinition definition)
    {
        selectedWeapon = null;

        ClearChildren(weaponList);
        ClearChildren(upgradeList);

        if (!HasRequiredMechanic())
        {
            CloseMenu();
            return;
        }

        TryBindProgression();

        if (upgradePanel != null && upgradePanel.activeSelf)
            BuildWeaponList();
    }

    private void RefreshUI()
    {
        if (!HasRequiredMechanic())
            return;

        RefreshPoints();

        if (selectedWeapon != null)
            BuildUpgradeList();
    }

    private void RefreshPoints()
    {
        if (pointsText == null)
            return;

        if (progression == null)
        {
            pointsText.text = "Очки прокачки: --";
            return;
        }

        pointsText.text =
            $"Очки прокачки: {progression.UpgradePoints}";
    }

    private void BuildWeaponList()
    {
        ClearChildren(weaponList);
        ClearChildren(upgradeList);

        selectedWeapon = null;

        if (!HasRequiredMechanic() ||
            localCharacter == null ||
            weaponList == null ||
            weaponButtonPrefab == null)
            return;

        for (int i = 0; i < localCharacter.WeaponCount; i++)
        {
            WeaponDefinition weapon =
                localCharacter.GetWeaponAt(i);

            if (weapon == null)
                continue;

            Button button =
                Instantiate(weaponButtonPrefab, weaponList);

            TMP_Text label =
                button.GetComponentInChildren<TMP_Text>();

            if (label != null)
                label.text = weapon.DisplayName;

            WeaponDefinition capturedWeapon = weapon;

            button.onClick.AddListener(
                () => SelectWeapon(capturedWeapon));

            if (selectedWeapon == null)
                selectedWeapon = weapon;
        }

        if (selectedWeapon != null)
            BuildUpgradeList();
    }

    private void SelectWeapon(WeaponDefinition weapon)
    {
        if (!HasRequiredMechanic() || weapon == null)
            return;

        selectedWeapon = weapon;
        BuildUpgradeList();
    }

    private void BuildUpgradeList()
    {
        ClearChildren(upgradeList);

        if (!HasRequiredMechanic() ||
            selectedWeapon == null ||
            selectedWeapon.Upgrades == null ||
            progression == null ||
            upgradeButtonPrefab == null ||
            upgradeList == null)
            return;

        foreach (WeaponUpgradeData upgrade in selectedWeapon.Upgrades)
        {
            if (upgrade == null)
                continue;

            int currentLevel = progression.GetUpgradeLevel(
                selectedWeapon.WeaponId,
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

            string weaponId = selectedWeapon.WeaponId;
            string upgradeId = upgrade.UpgradeId;

            button.onClick.AddListener(() =>
            {
                progression.RequestUpgrade(
                    weaponId,
                    upgradeId);
            });
        }
    }

    private void ClearChildren(Transform parent)
    {
        if (parent == null)
            return;

        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }

    private void OnDestroy()
    {
        if (progression != null)
            progression.ProgressionChanged -= RefreshUI;

        if (localCharacter != null)
            localCharacter.ClassApplied -= OnClassApplied;

        if (openButton != null)
            openButton.onClick.RemoveListener(OpenMenu);

        if (closeButton != null)
            closeButton.onClick.RemoveListener(CloseMenu);
    }
}
