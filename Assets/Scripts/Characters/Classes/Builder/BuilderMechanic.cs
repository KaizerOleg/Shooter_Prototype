using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class BuilderMechanic : ClassMechanic
{
    #region Mechanic Configuration

    public override string MechanicId => "builder";

    #endregion

    #region Inspector Settings

    [Header("Network")]
    [SerializeField] private GameObject builderNetworkPrefab;

    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;
    [SerializeField] private Camera aimCamera;

    [Header("Interaction")]
    [SerializeField, Min(0.5f)] private float interactionRadius = 4f;
    [SerializeField, Min(0.5f)] private float interactionCheckInterval = 0.1f;
    [SerializeField] private LayerMask interactionMask = ~0;

    [Header("Placement")]
    [SerializeField] private LayerMask placementMask = ~0;
    [SerializeField, Min(1f)] private float placementRayDistance = 100f;
    [SerializeField] private float previewVerticalOffset = 0.03f;

    [Header("Build Menu")]
    [SerializeField]
    private Vector2 buildWindowSize =
        new Vector2(380f, 500f);

    #endregion

    #region Runtime References

    private CharacterRoot character;
    private BuilderNetworkObject networkObject;
    private BuilderProgression progression;

    private BuildingPlacementPreview preview;
    private BuildingDefinition selectedBuilding;
    private BuildableStructure nearbyStructure;

    #endregion

    #region Properties

    public BuilderProgression Progression => progression;

    public bool IsLocalOwner =>
        character != null && character.IsOwner;

    #endregion

    #region Input Actions

    private InputAction buildModeAction;
    private InputAction buildRotateAction;
    private InputAction buildConfirmAction;
    private InputAction buildCancelAction;
    private InputAction buildScrollAction;
    private InputAction buildEscapeAction;
    private InputAction interactAction;

    private readonly Dictionary<InputAction, bool> enabledActions =
        new();

    #endregion

    #region Runtime State

    private bool buildModeOpen;
    private bool upgradeWindowOpen;

    private int selectedBuildingIndex;
    private float rotation;
    private float nextInteractionCheck;

    private Vector3 previewPosition;
    private Quaternion previewRotation;
    private bool previewValid;

    private Vector2 scrollPosition;

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
                "[BuilderMechanic] Не передано персонажа або клас.",
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
    }

    #endregion

    #region Network Object Management

    private void SpawnNetworkObject()
    {
        if (character == null ||
            !character.IsSpawned ||
            !character.IsServer)
            return;

        if (FindNetworkObject() != null)
            return;

        if (builderNetworkPrefab == null)
        {
            Debug.LogError(
                "[BuilderMechanic] Не призначено Builder Network Prefab.",
                this);
            return;
        }

        GameObject instance = Instantiate(
            builderNetworkPrefab,
            character.transform.position,
            character.transform.rotation);

        NetworkObject spawnedObject =
            instance.GetComponent<NetworkObject>();

        BuilderNetworkObject wrapper =
            instance.GetComponent<BuilderNetworkObject>();

        if (spawnedObject == null || wrapper == null)
        {
            Debug.LogError(
                "[BuilderMechanic] Builder prefab повинен мати " +
                "NetworkObject і BuilderNetworkObject.",
                instance);

            Destroy(instance);
            return;
        }

        spawnedObject.SpawnWithOwnership(character.OwnerClientId);

        if (!spawnedObject.TrySetParent(character.NetworkObject, false))
        {
            spawnedObject.Despawn(true);
            return;
        }

        spawnedObject.transform.localPosition = Vector3.zero;
        spawnedObject.transform.localRotation = Quaternion.identity;

        TryBindNetworkObject();
    }

    private BuilderNetworkObject FindNetworkObject()
    {
        if (character == null)
            return null;

        BuilderNetworkObject[] children =
            character.GetComponentsInChildren<BuilderNetworkObject>(true);

        foreach (BuilderNetworkObject candidate in children)
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

        BuilderNetworkObject found = FindNetworkObject();

        if (found == null)
            return;

        networkObject = found;
        progression = found.Progression;
    }

    #endregion

    #region Input Initialization

    private void InitializeInput()
    {
        if (inputActions == null)
        {
            Debug.LogError(
                "[BuilderMechanic] Не призначено InputActionAsset.",
                this);
            return;
        }

        buildModeAction =
            inputActions.FindAction("Player/BuildMode", false);

        buildRotateAction =
            inputActions.FindAction("Player/BuildRotate", false);

        buildConfirmAction =
            inputActions.FindAction("Player/BuildConfirm", false);

        buildCancelAction =
            inputActions.FindAction("Player/BuildCancel", false);

        buildScrollAction =
            inputActions.FindAction("Player/BuildScroll", false);

        buildEscapeAction =
            inputActions.FindAction("Player/BuildEscape", false);

        interactAction =
            inputActions.FindAction("Player/Interact", false);

        BindAction(buildModeAction, OnBuildMode);
        BindAction(buildRotateAction, OnBuildRotate);
        BindAction(buildConfirmAction, OnBuildConfirm);
        BindAction(buildCancelAction, OnBuildCancel);
        BindAction(buildScrollAction, OnBuildScroll);
        BindAction(buildEscapeAction, OnBuildEscape);
        BindAction(interactAction, OnInteract);
    }

    private void BindAction(
        InputAction action,
        Action<InputAction.CallbackContext> callback)
    {
        if (action == null)
        {
            Debug.LogWarning(
                "[BuilderMechanic] Не знайдено одну з Input Actions.",
                this);
            return;
        }

        action.performed += callback;

        if (!action.enabled)
        {
            action.Enable();
            enabledActions[action] = true;
        }
        else
        {
            enabledActions[action] = false;
        }
    }

    #endregion

    #region Unity Lifecycle

    private void Update()
    {
        if (character == null || !character.IsOwner)
            return;

        if (networkObject == null || progression == null)
            TryBindNetworkObject();

        if (aimCamera == null)
            aimCamera = Camera.main;

        if (buildModeOpen)
            UpdatePlacementPreview();

        if (Time.time >= nextInteractionCheck)
        {
            nextInteractionCheck =
                Time.time + interactionCheckInterval;

            UpdateNearbyStructure();
        }
    }

    private void OnGUI()
    {
        if (!CanUseInput())
            return;

        if (buildModeOpen)
            DrawBuildWindow();

        if (upgradeWindowOpen)
            DrawUpgradeWindow();

        if (!buildModeOpen &&
            !upgradeWindowOpen &&
            nearbyStructure != null)
        {
            DrawInteractionHint();
        }
    }

    #endregion

    #region Input Callbacks

    private void OnBuildMode(InputAction.CallbackContext context)
    {
        if (!CanUseInput())
            return;

        if (upgradeWindowOpen)
            CloseUpgradeWindow();

        if (buildModeOpen)
        {
            CloseBuildMode();
            return;
        }

        if (progression == null || progression.Database == null)
        {
            Debug.LogWarning(
                "[BuilderMechanic] BuilderDatabase недоступна.",
                this);
            return;
        }

        if (progression.Database.BuildingCount == 0)
            return;

        buildModeOpen = true;
        selectedBuildingIndex = 0;
        rotation = 0f;

        SelectBuilding(selectedBuildingIndex);
        SetCursorVisible(true);
    }

    private void OnBuildRotate(InputAction.CallbackContext context)
    {
        if (!buildModeOpen || selectedBuilding == null)
            return;

        if (!selectedBuilding.AllowRotation)
            return;

        rotation += selectedBuilding.RotationStep;

        if (rotation >= 360f)
            rotation -= 360f;
    }

    private void OnBuildConfirm(InputAction.CallbackContext context)
    {
        if (!buildModeOpen ||
            !previewValid ||
            selectedBuilding == null ||
            progression == null)
            return;

        if (progression.BuildPoints.Value < selectedBuilding.BuildCost)
            return;

        if (!progression.CanBuild)
            return;

        progression.RequestPlaceBuilding(
            selectedBuilding.BuildingId,
            previewPosition,
            previewRotation);
    }

    private void OnBuildCancel(InputAction.CallbackContext context)
    {
        if (buildModeOpen)
        {
            CloseBuildMode();
            return;
        }

        if (upgradeWindowOpen)
            CloseUpgradeWindow();
    }

    private void OnBuildEscape(InputAction.CallbackContext context)
    {
        if (buildModeOpen)
            CloseBuildMode();
        else if (upgradeWindowOpen)
            CloseUpgradeWindow();
    }

    private void OnBuildScroll(InputAction.CallbackContext context)
    {
        if (!buildModeOpen || progression == null ||
            progression.Database == null)
            return;

        float scroll = context.ReadValue<float>();

        if (Mathf.Abs(scroll) < 0.01f)
            return;

        int count = progression.Database.BuildingCount;

        if (count <= 0)
            return;

        selectedBuildingIndex += scroll > 0f ? 1 : -1;

        if (selectedBuildingIndex >= count)
            selectedBuildingIndex = 0;

        if (selectedBuildingIndex < 0)
            selectedBuildingIndex = count - 1;

        SelectBuilding(selectedBuildingIndex);
    }

    private void OnInteract(InputAction.CallbackContext context)
    {
        if (buildModeOpen)
            return;

        if (nearbyStructure == null)
            return;

        if (upgradeWindowOpen)
        {
            CloseUpgradeWindow();
            return;
        }

        OpenUpgradeWindow();
    }

    #endregion

    #region Input Validation

    private bool CanUseInput()
    {
        return character != null &&
               character.IsOwner &&
               character.IsSpawned;
    }

    #endregion

    #region Building Selection

    private void SelectBuilding(int index)
    {
        if (progression == null ||
            progression.Database == null)
            return;

        selectedBuilding =
            progression.Database.GetBuildingAt(index);

        DestroyPreview();

        if (selectedBuilding == null ||
            selectedBuilding.BuildingPrefab == null)
            return;

        GameObject previewObject =
            new GameObject("BuilderPlacementPreview");

        preview = previewObject.AddComponent<BuildingPlacementPreview>();

        preview.Initialize(selectedBuilding.BuildingPrefab);
    }

    #endregion

    #region Placement Preview

    private void UpdatePlacementPreview()
    {
        if (preview == null ||
            selectedBuilding == null ||
            aimCamera == null)
            return;

        Ray ray = aimCamera.ViewportPointToRay(
            new Vector3(0.5f, 0.5f, 0f));

        previewValid = false;

        if (Physics.Raycast(
            ray,
            out RaycastHit hit,
            placementRayDistance,
            selectedBuilding.PlacementMask & placementMask,
            QueryTriggerInteraction.Ignore))
        {
            if (hit.collider != null &&
                hit.collider.GetComponentInParent<EnemyAI>() == null &&
                hit.collider.GetComponentInParent<CharacterRoot>() == null &&
                hit.normal.y >= 0.3f)
            {
                float distance = Vector3.Distance(
                    character.transform.position,
                    hit.point);

                if (distance <= selectedBuilding.PlacementRange &&
                    progression != null &&
                    progression.CanBuild &&
                    progression.BuildPoints.Value >= selectedBuilding.BuildCost)
                {
                    previewValid = true;
                }

                previewPosition =
                    hit.point +
                    hit.normal * previewVerticalOffset +
                    selectedBuilding.PlacementOffset;

                Quaternion surfaceRotation =
                    selectedBuilding.AlignToSurface
                        ? Quaternion.FromToRotation(
                            Vector3.up,
                            hit.normal)
                        : Quaternion.identity;

                previewRotation =
                    surfaceRotation *
                    Quaternion.Euler(0f, rotation, 0f);
            }
        }

        preview.SetPosition(previewPosition, previewRotation);
        preview.SetValid(previewValid);
    }

    private void DestroyPreview()
    {
        if (preview != null)
        {
            Destroy(preview.gameObject);
            preview = null;
        }
    }

    #endregion

    #region Structure Detection

    private void UpdateNearbyStructure()
    {
        if (buildModeOpen)
        {
            SetHighlightedStructure(null);
            return;
        }

        Collider[] colliders = Physics.OverlapSphere(
            character.transform.position,
            interactionRadius,
            interactionMask,
            QueryTriggerInteraction.Ignore);

        BuildableStructure closest = null;
        float closestDistance = float.MaxValue;

        foreach (Collider col in colliders)
        {
            if (col == null)
                continue;

            BuildableStructure structure =
                col.GetComponentInParent<BuildableStructure>();

            if (structure == null || !structure.IsSpawned)
                continue;

            float distance = Vector3.Distance(
                character.transform.position,
                structure.transform.position);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = structure;
            }
        }

        SetHighlightedStructure(closest);
    }

    private void SetHighlightedStructure(BuildableStructure structure)
    {
        if (nearbyStructure == structure)
            return;

        if (nearbyStructure != null)
            nearbyStructure.SetHighlighted(false);

        nearbyStructure = structure;

        if (nearbyStructure != null)
            nearbyStructure.SetHighlighted(true);
    }

    #endregion

    #region Upgrade Window

    private void OpenUpgradeWindow()
    {
        if (nearbyStructure == null)
            return;

        upgradeWindowOpen = true;
        SetCursorVisible(true);
    }

    private void CloseUpgradeWindow()
    {
        upgradeWindowOpen = false;
        RestoreCursorIfNeeded();
    }

    #endregion

    #region Build Mode

    private void CloseBuildMode()
    {
        buildModeOpen = false;
        previewValid = false;

        DestroyPreview();
        RestoreCursorIfNeeded();
    }

    #endregion

    #region Cursor Management

    private void SetCursorVisible(bool visible)
    {
        Cursor.visible = visible;
        Cursor.lockState = visible
            ? CursorLockMode.None
            : CursorLockMode.Locked;
    }

    private void RestoreCursorIfNeeded()
    {
        if (!buildModeOpen && !upgradeWindowOpen)
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    #endregion

    #region Build UI

    private void DrawBuildWindow()
    {
        if (progression == null ||
            progression.Database == null)
            return;

        float width = buildWindowSize.x;
        float height = buildWindowSize.y;

        Rect windowRect = new Rect(
            (Screen.width - width) * 0.05f,
            (Screen.height - height) * 0.7f,
            width,
            height);

        GUILayout.BeginArea(
            windowRect,
            "БУДІВНИЦТВО [B]",
            GUI.skin.window);

        GUILayout.Label(
            $"Очки: {progression.BuildPoints.Value}");

        GUILayout.Label(
            $"Споруди: {progression.ActiveBuildables.Value}/" +
            $"{progression.MaxBuildables}");

        GUILayout.Space(10f);

        scrollPosition = GUILayout.BeginScrollView(
            scrollPosition,
            GUILayout.Height(height * 0.48f));

        for (int i = 0; i < progression.Database.BuildingCount; i++)
        {
            BuildingDefinition building =
                progression.Database.GetBuildingAt(i);

            if (building == null)
                continue;

            GUI.enabled =
                progression.BuildPoints.Value >= building.BuildCost &&
                progression.CanBuild;

            if (GUILayout.Button(
                $"{building.DisplayName} — {building.BuildCost} очок",
                GUILayout.Height(40f)))
            {
                selectedBuildingIndex = i;
                SelectBuilding(i);
            }

            GUI.enabled = true;
        }

        GUILayout.EndScrollView();

        GUILayout.Space(10f);

        if (selectedBuilding != null)
        {
            GUILayout.Label(
                $"Обрано: {selectedBuilding.DisplayName}");

            GUILayout.Label(
                previewValid
                    ? "Місце доступне"
                    : "Місце недоступне");

            GUILayout.Label("ЛКМ — побудувати");
            GUILayout.Label("R — повернути");
            GUILayout.Label("Коліщатко — перемкнути");
        }

        if (GUILayout.Button("Закрити [Esc]"))
            CloseBuildMode();

        GUILayout.EndArea();
    }

    #endregion

    #region Upgrade UI

    private void DrawUpgradeWindow()
    {
        if (nearbyStructure == null)
        {
            CloseUpgradeWindow();
            return;
        }

        BuildingDefinition definition =
            nearbyStructure.Definition;

        if (definition == null)
            return;

        GUILayout.BeginArea(
            new Rect(
                Screen.width - 370f,
                100f,
                350f,
                500f),
            "ПОКРАЩЕННЯ БУДІВЛІ [E]",
            GUI.skin.window);

        GUILayout.Label(definition.DisplayName);

        GUILayout.Label(
            $"Міцність: {nearbyStructure.CurrentHealth.Value:F0}/" +
            $"{nearbyStructure.MaxHealth:F0}");

        GUILayout.Label(
            $"Шкода: {nearbyStructure.Damage:F1}");

        GUILayout.Label(
            $"Дальність: {nearbyStructure.AttackRange:F1}");

        GUILayout.Label(
            $"Швидкість атаки: " +
            $"{nearbyStructure.AttacksPerSecond:F2}");

        GUILayout.Space(15f);

        GUILayout.Label(
            progression != null
                ? $"Твої очки: {progression.BuildPoints.Value}"
                : "Очки недоступні");

        BuildingUpgradeDefinition[] upgrades =
            definition.AvailableUpgrades;

        if (upgrades != null)
        {
            foreach (BuildingUpgradeDefinition upgrade in upgrades)
            {
                if (upgrade == null)
                    continue;

                int level = nearbyStructure.GetUpgradeLevel(
                    upgrade.UpgradeId);

                bool maxed = level >= upgrade.MaxLevel;

                GUILayout.BeginVertical(GUI.skin.box);

                GUILayout.Label(upgrade.DisplayName);
                GUILayout.Label(upgrade.Description);

                GUILayout.Label(
                    $"Рівень: {level}/{upgrade.MaxLevel}");

                if (GUILayout.Button(
                    maxed
                        ? "МАКСИМАЛЬНИЙ РІВЕНЬ"
                        : $"Покращити — {upgrade.Cost} очок"))
                {
                    if (!maxed)
                        nearbyStructure.RequestUpgrade(
                            upgrade.UpgradeId);
                }

                GUILayout.EndVertical();
            }
        }

        if (GUILayout.Button("Закрити [E]"))
            CloseUpgradeWindow();

        GUILayout.EndArea();
    }

    private void DrawInteractionHint()
    {
        GUIStyle style = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            normal = { textColor = Color.white }
        };

        GUI.Box(
            new Rect(
                Screen.width / 2f - 130f,
                Screen.height - 110f,
                260f,
                45f),
            "E — Покращити будівлю",
            style);
    }

    #endregion

    #region Shutdown

    public override void Shutdown()
    {
        CloseBuildMode();

        upgradeWindowOpen = false;

        SetHighlightedStructure(null);

        if (buildModeAction != null)
            buildModeAction.performed -= OnBuildMode;

        if (buildRotateAction != null)
            buildRotateAction.performed -= OnBuildRotate;

        if (buildConfirmAction != null)
            buildConfirmAction.performed -= OnBuildConfirm;

        if (buildCancelAction != null)
            buildCancelAction.performed -= OnBuildCancel;

        if (buildScrollAction != null)
            buildScrollAction.performed -= OnBuildScroll;

        if (buildEscapeAction != null)
            buildEscapeAction.performed -= OnBuildEscape;

        if (interactAction != null)
            interactAction.performed -= OnInteract;

        foreach (var pair in enabledActions)
        {
            if (pair.Key != null && pair.Value)
                pair.Key.Disable();
        }

        enabledActions.Clear();

        if (character != null && character.IsServer)
        {
            BuilderNetworkObject objectToDespawn =
                networkObject != null
                    ? networkObject
                    : FindNetworkObject();

            if (objectToDespawn != null &&
                objectToDespawn.NetworkObject != null &&
                objectToDespawn.NetworkObject.IsSpawned)
            {
                objectToDespawn.NetworkObject.Despawn(true);
            }
        }

        DestroyPreview();

        character = null;
        networkObject = null;
        progression = null;

        aimCamera = null;
        selectedBuilding = null;

        buildModeAction = null;
        buildRotateAction = null;
        buildConfirmAction = null;
        buildCancelAction = null;
        buildScrollAction = null;
        buildEscapeAction = null;
        interactAction = null;

        base.Shutdown();
    }

    #endregion
}