
using TMPro;
using Unity.Netcode;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class AmmoUI : MonoBehaviour
{
    [Header("Class Settings")]
    [SerializeField]
    private string requiredClassId = "gunner";

    [Header("UI References")]
    [SerializeField]
    private TMP_Text ammoText;

    [SerializeField]
    private TMP_Text reloadText;

    private WeaponController weapon;
    private CharacterRoot character;
    private NetworkManager subscribedManager;

    private void OnEnable()
    {
        SubscribeToNetwork();
        TryFindLocalCharacter();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeFromNetwork();
        UnsubscribeFromCharacter();

        weapon = null;
        character = null;
    }

    private void Update()
    {
        if (character == null ||
            !character.IsSpawned ||
            !character.IsOwner)
        {
            TryFindLocalCharacter();
        }

        if (!IsRequiredClass())
        {
            weapon = null;
            SetUIVisible(false);
            return;
        }

        SetUIVisible(true);

        if (weapon == null ||
            !weapon.IsSpawned ||
            !weapon.IsOwner)
        {
            TryFindLocalWeapon();
        }

        RefreshUI();
    }

    private void SubscribeToNetwork()
    {
        subscribedManager = NetworkManager.Singleton;

        if (subscribedManager == null)
            return;

        subscribedManager.OnClientConnectedCallback += OnClientConnected;
        subscribedManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void UnsubscribeFromNetwork()
    {
        if (subscribedManager == null)
            return;

        subscribedManager.OnClientConnectedCallback -= OnClientConnected;
        subscribedManager.OnClientDisconnectCallback -= OnClientDisconnected;

        subscribedManager = null;
    }

    private void OnClientConnected(ulong clientId)
    {
        TryFindLocalCharacter();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        TryFindLocalCharacter();
    }

    private void TryFindLocalCharacter()
    {
        NetworkManager manager = NetworkManager.Singleton;

        if (manager == null || !manager.IsListening)
            return;

        NetworkObject playerObject = manager.LocalClient?.PlayerObject;

        if (playerObject == null)
            return;

        CharacterRoot foundCharacter =
            playerObject.GetComponent<CharacterRoot>();

        if (foundCharacter == null)
            return;

        if (character == foundCharacter)
            return;

        UnsubscribeFromCharacter();

        character = foundCharacter;
        character.ClassApplied += OnClassApplied;

        weapon = null;

        RefreshUI();
    }

    private void UnsubscribeFromCharacter()
    {
        if (character != null)
            character.ClassApplied -= OnClassApplied;
    }

    private void OnClassApplied(CharacterDefinition definition)
    {
        weapon = null;

        RefreshUI();
    }

    private bool IsRequiredClass()
    {
        if (character == null || !character.IsSpawned)
            return false;

        if (!character.HasSelectedClass)
            return false;

        return string.Equals(
            character.ClassId,
            requiredClassId.Trim(),
            System.StringComparison.OrdinalIgnoreCase);
    }

    private void TryFindLocalWeapon()
    {
        weapon = null;

        if (!IsRequiredClass())
            return;

        if (character.TryGetActiveWeapon(
                out WeaponController activeWeapon))
        {
            weapon = activeWeapon;
        }
    }

    private void RefreshUI()
    {
        if (!IsRequiredClass())
        {
            SetUIVisible(false);
            return;
        }

        SetUIVisible(true);

        if (weapon == null ||
            !weapon.IsSpawned ||
            !weapon.IsOwner ||
            weapon.Data == null)
        {
            if (ammoText != null)
                ammoText.text = "-- / --";

            if (reloadText != null)
            {
                reloadText.text = string.Empty;
                reloadText.gameObject.SetActive(false);
            }

            return;
        }

        if (ammoText != null)
        {
            ammoText.text =
                $"{weapon.CurrentAmmo} / {weapon.MaxAmmo}";
        }

        if (reloadText != null)
        {
            bool reloading = weapon.IsReloading;

            reloadText.text = reloading
                ? "ПЕРЕЗАРЯДЖАННЯ..."
                : string.Empty;

            reloadText.gameObject.SetActive(reloading);
        }
    }

    private void SetUIVisible(bool visible)
    {
        if (ammoText != null &&
            ammoText.gameObject.activeSelf != visible)
        {
            ammoText.gameObject.SetActive(visible);
        }

        if (reloadText != null &&
            reloadText.gameObject.activeSelf != visible)
        {
            reloadText.gameObject.SetActive(visible);
        }
    }
}
