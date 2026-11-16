using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class MageSpellMechanic : ClassMechanic
{
    #region Mechanic Configuration

    public override string MechanicId => "spell";

    #endregion

    #region Inspector Settings

    [Header("Network")]
    [SerializeField]
    private GameObject mageSpellNetworkPrefab;

    [Header("Input")]
    [SerializeField]
    private InputActionAsset inputActions;

    [SerializeField]
    private Camera aimCamera;

    [Header("Area Preview")]
    [SerializeField]
    private LayerMask previewGroundMask = ~0;

    #endregion

    #region Runtime References

    private CharacterRoot character;
    private MageSpellNetworkObject networkObject;

    private PlayerSpellProgression progression;
    private SpellLoadout loadout;
    private MageSpellCaster caster;

    private AreaTargetPreview areaPreview;

    #endregion

    #region Properties & Events

    public SpellLoadout Loadout => loadout;

    public bool IsLocalOwner =>
        character != null && character.IsOwner;

    public PlayerSpellProgression Progression => progression;
    public MageSpellCaster Caster => caster;

    public event Action SpellLoadoutChanged;

    #endregion

    #region Input State

    private InputAction castSpell1Action;
    private InputAction castSpell2Action;
    private InputAction castSpell3Action;

    private bool enabledSpell1Here;
    private bool enabledSpell2Here;
    private bool enabledSpell3Here;

    #endregion

    #region Area Charge State

    private int chargingSlot = -1;
    private float chargeStartedTime;
    private float requiredChargeTime;
    private bool areaChargeActive;

    #endregion

    #region Initialization

    public override void Initialize(
        CharacterRoot ownerCharacter,
        CharacterDefinition definition)
    {
        Shutdown();

        if (ownerCharacter == null || definition == null)
        {
            Debug.LogError(
                "[MageSpellMechanic] Не передано персонажа або клас.",
                this);
            return;
        }

        character = ownerCharacter;

        if (character.IsServer)
            SpawnNetworkObject();

        if (!character.IsOwner)
            return;

        if (aimCamera == null)
            aimCamera = Camera.main;

        InitializeInput();
        TryBindNetworkObject();

        Debug.Log(
            $"[MageSpellMechanic] Initialized. " +
            $"IsOwner={character.IsOwner}, IsServer={character.IsServer}.",
            this);
    }

    private void SpawnNetworkObject()
    {
        if (!character.IsSpawned || !character.IsServer)
            return;

        if (FindNetworkObject() != null)
            return;

        if (mageSpellNetworkPrefab == null)
        {
            Debug.LogError(
                "[MageSpellMechanic] Не призначено Mage Spell Network Prefab.",
                this);
            return;
        }

        GameObject instance = Instantiate(
            mageSpellNetworkPrefab,
            character.transform.position,
            character.transform.rotation);

        NetworkObject spawnedObject =
            instance.GetComponent<NetworkObject>();

        if (spawnedObject == null)
        {
            Debug.LogError(
                "[MageSpellMechanic] На prefab немає NetworkObject.",
                instance);

            Destroy(instance);
            return;
        }

        spawnedObject.SpawnWithOwnership(character.OwnerClientId);

        if (!spawnedObject.TrySetParent(character.NetworkObject, false))
        {
            Debug.LogError(
                "[MageSpellMechanic] Не вдалося прикріпити мережевий об'єкт.",
                character);

            spawnedObject.Despawn(true);
            return;
        }

        spawnedObject.transform.localPosition = Vector3.zero;
        spawnedObject.transform.localRotation = Quaternion.identity;

        TryBindNetworkObject();
    }

    #endregion

    #region Network Object Binding

    private MageSpellNetworkObject FindNetworkObject()
    {
        if (character == null)
            return null;

        MageSpellNetworkObject[] children =
            character.GetComponentsInChildren<MageSpellNetworkObject>(true);

        foreach (MageSpellNetworkObject candidate in children)
        {
            if (candidate == null ||
                candidate.NetworkObject == null ||
                !candidate.NetworkObject.IsSpawned)
                continue;

            return candidate;
        }

        return null;
    }

    private void TryBindNetworkObject()
    {
        if (character == null)
            return;

        MageSpellNetworkObject found = FindNetworkObject();

        if (found == null || networkObject == found)
            return;

        if (networkObject != null && networkObject.Loadout != null)
            networkObject.Loadout.LoadoutChanged -= OnLoadoutChanged;

        networkObject = found;

        progression = found.Progression;
        loadout = found.Loadout;
        caster = found.Caster;

        if (loadout != null)
            loadout.LoadoutChanged += OnLoadoutChanged;

        SpellLoadoutChanged?.Invoke();

        Debug.Log(
            "[MageSpellMechanic] Мережеву магічну систему підключено.",
            character);
    }

    #endregion

    #region Input Initialization

    private void InitializeInput()
    {
        if (inputActions == null)
        {
            Debug.LogError(
                "[MageSpellMechanic] Не призначено InputActionAsset.",
                this);
            return;
        }

        castSpell1Action =
            inputActions.FindAction("Player/CastSpell1", false);

        castSpell2Action =
            inputActions.FindAction("Player/CastSpell2", false);

        castSpell3Action =
            inputActions.FindAction("Player/CastSpell3", false);

        if (castSpell1Action == null ||
            castSpell2Action == null ||
            castSpell3Action == null)
        {
            Debug.LogError(
                "[MageSpellMechanic] Не знайдено CastSpell1, " +
                "CastSpell2 або CastSpell3.",
                this);
            return;
        }

        EnableAction(castSpell1Action, ref enabledSpell1Here);
        EnableAction(castSpell2Action, ref enabledSpell2Here);
        EnableAction(castSpell3Action, ref enabledSpell3Here);

        castSpell1Action.started += OnCastSpell1Started;
        castSpell1Action.canceled += OnCastSpell1Canceled;

        castSpell2Action.started += OnCastSpell2Started;
        castSpell2Action.canceled += OnCastSpell2Canceled;

        castSpell3Action.started += OnCastSpell3Started;
        castSpell3Action.canceled += OnCastSpell3Canceled;
    }

    private void EnableAction(InputAction action, ref bool enabledHere)
    {
        if (action == null || action.enabled)
            return;

        action.Enable();
        enabledHere = true;
    }

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        if (character == null || !character.IsOwner)
            return;

        if (networkObject == null || caster == null)
            TryBindNetworkObject();

        if (aimCamera == null)
            aimCamera = Camera.main;

        if (areaChargeActive)
            UpdateAreaPreview();
    }

    #endregion

    #region Input Callbacks

    private void OnCastSpell1Started(InputAction.CallbackContext context)
    {
        HandleSpellPressed(0);
    }

    private void OnCastSpell2Started(InputAction.CallbackContext context)
    {
        HandleSpellPressed(1);
    }

    private void OnCastSpell3Started(InputAction.CallbackContext context)
    {
        HandleSpellPressed(2);
    }

    private void OnCastSpell1Canceled(InputAction.CallbackContext context)
    {
        HandleSpellReleased(0);
    }

    private void OnCastSpell2Canceled(InputAction.CallbackContext context)
    {
        HandleSpellReleased(1);
    }

    private void OnCastSpell3Canceled(InputAction.CallbackContext context)
    {
        HandleSpellReleased(2);
    }

    #endregion

    #region Spell Input Handling

    private void HandleSpellPressed(int slot)
    {
        if (!CanUseInput())
            return;

        SpellDefinition spell = GetSpellDefinition(slot);

        if (spell == null)
            return;

        if (spell.Type != SpellType.Area)
        {
            RequestSpellSlot(slot);
            return;
        }

        BeginAreaCharge(slot, spell);
    }

    private void HandleSpellReleased(int slot)
    {
        if (!areaChargeActive || chargingSlot != slot)
            return;

        float heldTime = Time.time - chargeStartedTime;

        SpellDefinition spell = GetSpellDefinition(slot);

        bool charged = heldTime >= requiredChargeTime;

        Vector3 origin = Vector3.zero;
        Vector3 direction = Vector3.zero;

        bool hasAim = aimCamera != null;

        if (hasAim)
        {
            origin = aimCamera.transform.position;
            direction = aimCamera.transform.forward;
        }

        CancelAreaCharge();

        if (!charged)
        {
            Debug.Log(
                "[MageSpellMechanic] Заряджання скасовано: недостатньо часу.",
                this);
            return;
        }

        if (spell == null || !hasAim)
            return;

        if (caster == null)
            TryBindNetworkObject();

        if (caster == null || !caster.IsSpawned)
            return;

        caster.RequestCastArea(slot, origin, direction);

        Debug.Log(
            "[MageSpellMechanic] Відправлено запит Area-закляття.",
            this);
    }

    private void RequestSpellSlot(int slot)
    {
        if (!CanUseInput())
            return;

        if (aimCamera == null)
            aimCamera = Camera.main;

        if (aimCamera == null)
            return;

        caster.RequestCast(
            slot,
            aimCamera.transform.position,
            aimCamera.transform.forward);
    }

    private bool CanUseInput()
    {
        if (character == null ||
            !character.IsOwner ||
            !character.IsSpawned)
            return false;

        if (caster == null)
            TryBindNetworkObject();

        return caster != null && caster.IsSpawned;
    }

    #endregion

    #region Area Spell Charging

    private void BeginAreaCharge(int slot, SpellDefinition spell)
    {
        CancelAreaCharge();

        if (spell == null || aimCamera == null)
            return;

        chargingSlot = slot;
        chargeStartedTime = Time.time;

        SpellRuntime runtime =
            progression.CreateSpellRuntime(spell.SpellId);

        requiredChargeTime = runtime != null
            ? runtime.ChargeTime
            : spell.ChargeTime;

        areaChargeActive = true;

        GameObject previewObject =
            new GameObject("AreaTargetPreview");

        areaPreview =
            previewObject.AddComponent<AreaTargetPreview>();

        areaPreview.SetRadius(
            runtime != null ? runtime.AreaRadius : spell.AreaRadius);

        UpdateAreaPreview();

        Debug.Log(
            $"[MageSpellMechanic] Почалося заряджання {spell.DisplayName}. " +
            $"Час: {requiredChargeTime:F2}s",
            this);
    }

    private void CancelAreaCharge()
    {
        areaChargeActive = false;
        chargingSlot = -1;
        requiredChargeTime = 0f;

        if (areaPreview != null)
        {
            Destroy(areaPreview.gameObject);
            areaPreview = null;
        }
    }

    #endregion

    #region Area Preview

    private void UpdateAreaPreview()
    {
        if (areaPreview == null || aimCamera == null)
            return;

        SpellDefinition spell = GetSpellDefinition(chargingSlot);

        if (spell == null)
            return;

        SpellRuntime runtime =
            progression.CreateSpellRuntime(spell.SpellId);

        float range = runtime != null
            ? runtime.Range
            : spell.Range;

        if (!TryGetPreviewPosition(
            aimCamera.transform.position,
            aimCamera.transform.forward,
            range,
            out Vector3 position,
            out Vector3 normal))
        {
            return;
        }

        areaPreview.SetSurface(position, normal);
    }

    private bool TryGetPreviewPosition(
        Vector3 origin,
        Vector3 direction,
        float range,
        out Vector3 result,
        out Vector3 surfaceNormal)
    {
        result = Vector3.zero;
        surfaceNormal = Vector3.up;

        Vector3 target = origin + direction * range;

        if (Physics.Raycast(
            origin,
            direction,
            out RaycastHit aimHit,
            range,
            previewGroundMask,
            QueryTriggerInteraction.Ignore))
        {
            target = aimHit.point;
        }

        return TryFindPreviewGround(
            target,
            out result,
            out surfaceNormal);
    }

    private bool TryFindPreviewGround(
        Vector3 target,
        out Vector3 groundPosition,
        out Vector3 groundNormal)
    {
        groundPosition = Vector3.zero;
        groundNormal = Vector3.up;

        Ray ray = new Ray(
            target + Vector3.up * 20f,
            Vector3.down);

        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            40f,
            previewGroundMask,
            QueryTriggerInteraction.Ignore);

        if (hits == null || hits.Length == 0)
            return false;

        Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider == null)
                continue;

            if (hit.collider.GetComponentInParent<EnemyAI>() != null)
                continue;

            if (hit.collider.GetComponentInParent<CharacterRoot>() != null)
                continue;

            if (hit.normal.y < 0.3f)
                continue;

            groundPosition = hit.point;
            groundNormal = hit.normal.normalized;

            return true;
        }

        return false;
    }

    #endregion

    #region Spell Lookup

    private SpellDefinition GetSpellDefinition(int slot)
    {
        if (loadout == null || progression == null)
            TryBindNetworkObject();

        if (loadout == null || progression == null)
            return null;

        string spellId = loadout.GetSpellId(slot);

        if (string.IsNullOrWhiteSpace(spellId))
            return null;

        return progression.GetSpellDefinition(spellId);
    }

    #endregion

    #region Loadout Events

    private void OnLoadoutChanged()
    {
        if (areaChargeActive)
            CancelAreaCharge();

        SpellLoadoutChanged?.Invoke();
    }

    #endregion

    #region Shutdown

    public override void Shutdown()
    {
        CancelAreaCharge();

        if (castSpell1Action != null)
        {
            castSpell1Action.started -= OnCastSpell1Started;
            castSpell1Action.canceled -= OnCastSpell1Canceled;
        }

        if (castSpell2Action != null)
        {
            castSpell2Action.started -= OnCastSpell2Started;
            castSpell2Action.canceled -= OnCastSpell2Canceled;
        }

        if (castSpell3Action != null)
        {
            castSpell3Action.started -= OnCastSpell3Started;
            castSpell3Action.canceled -= OnCastSpell3Canceled;
        }

        if (enabledSpell1Here)
            castSpell1Action?.Disable();

        if (enabledSpell2Here)
            castSpell2Action?.Disable();

        if (enabledSpell3Here)
            castSpell3Action?.Disable();

        enabledSpell1Here = false;
        enabledSpell2Here = false;
        enabledSpell3Here = false;

        if (networkObject != null && networkObject.Loadout != null)
            networkObject.Loadout.LoadoutChanged -= OnLoadoutChanged;

        if (character != null &&
            character.IsServer &&
            networkObject != null &&
            networkObject.NetworkObject != null &&
            networkObject.NetworkObject.IsSpawned)
        {
            networkObject.NetworkObject.Despawn(true);
        }

        character = null;
        networkObject = null;

        progression = null;
        loadout = null;
        caster = null;

        castSpell1Action = null;
        castSpell2Action = null;
        castSpell3Action = null;

        aimCamera = null;

        SpellLoadoutChanged = null;

        base.Shutdown();
    }

    #endregion
}