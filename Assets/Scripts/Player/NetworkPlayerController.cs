
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(NetworkObject))]
public class NetworkPlayerController : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField] private InputActionAsset inputActions;

    [Header("References")]
    [SerializeField] private Transform cameraPivot;
    [SerializeField] private Camera playerCamera;
    [SerializeField] private Animator animator;

    [Header("Animation")]
    [SerializeField] private string runningParameter = "IsRunning";
    [SerializeField] private string jumpTrigger = "Jump";

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float mouseSensitivity = 0.08f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpHeight = 1.2f;

    [Header("UI")]
    [SerializeField] private GameObject classSelectionPanel;
    [SerializeField] private SpellLoadoutUI spellLoadoutUI;

    private CharacterSelectionUI classSelectionUI;

    private CharacterController controller;
    private Health health;
    private NetworkAnimator networkAnimator;

    private InputActionMap playerMap;
    private InputAction moveAction;
    private InputAction lookAction;
    private InputAction jumpAction;
    private InputAction fireAction;
    private InputAction aimAction;
    private InputAction interactAction;
    private InputAction cursorUnlockAction;

    private int runningParameterHash;
    private int jumpTriggerHash;

    private float pitch;
    private float verticalVelocity;

    private bool wasRunning;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        health = GetComponent<Health>();
        networkAnimator = GetComponent<NetworkAnimator>();

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        runningParameterHash =
            Animator.StringToHash(runningParameter);

        jumpTriggerHash =
            Animator.StringToHash(jumpTrigger);

        if (inputActions == null)
        {
            Debug.LogError(
                "Признач Input Actions у Inspector.",
                this);

            enabled = false;
            return;
        }

        playerMap = inputActions.FindActionMap("Player", true);

        moveAction = playerMap.FindAction("Move", true);
        lookAction = playerMap.FindAction("Look", true);
        jumpAction = playerMap.FindAction("Jump", true);
        fireAction = playerMap.FindAction("Fire", true);
        aimAction = playerMap.FindAction("Aim", true);
        interactAction = playerMap.FindAction("Interact", true);

        cursorUnlockAction =
            playerMap.FindAction("CursorUnlock", true);
    }

    public override void OnNetworkSpawn()
    {
        bool local = IsOwner;

        if (playerCamera != null)
            playerCamera.enabled = local;

        AudioListener listener = playerCamera != null
            ? playerCamera.GetComponent<AudioListener>()
            : null;

        if (listener != null)
            listener.enabled = local;

        if (local)
        {
            playerMap.Enable();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            FindUIReferences();
        }
        else
        {
            controller.enabled = false;
        }
    }

    private void Update()
    {
        if (!IsSpawned || !IsOwner)
            return;

        FindUIReferences();

        bool classMenuOpen =
            classSelectionUI != null &&
            classSelectionUI.IsOpen;

        bool spellMenuOpen =
            spellLoadoutUI != null &&
            spellLoadoutUI.IsMenuOpen;

        if (classMenuOpen || spellMenuOpen)
        {
            SetRunningAnimation(false);

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        bool unlockCursor =
            cursorUnlockAction != null &&
            cursorUnlockAction.IsPressed();

        Cursor.lockState = unlockCursor
            ? CursorLockMode.None
            : CursorLockMode.Locked;

        Cursor.visible = unlockCursor;

        if (health != null && health.IsDead.Value)
        {
            SetRunningAnimation(false);
            return;
        }

        if (!unlockCursor)
        {
            HandleInput();
            MoveLocally();
        }
        else
        {
            SetRunningAnimation(false);
        }
    }

    private void FindUIReferences()
    {
        if (classSelectionUI == null)
        {
            classSelectionUI =
                FindFirstObjectByType<CharacterSelectionUI>();
        }

        if (spellLoadoutUI == null)
        {
            spellLoadoutUI =
                FindFirstObjectByType<SpellLoadoutUI>();
        }
    }

    private void HandleInput()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            if (Mouse.current != null &&
                Mouse.current.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            return;
        }

        Vector2 look = lookAction.ReadValue<Vector2>();

        transform.Rotate(
            Vector3.up,
            look.x * mouseSensitivity,
            Space.World);

        pitch = Mathf.Clamp(
            pitch - look.y * mouseSensitivity,
            -85f,
            85f);

        if (cameraPivot != null)
        {
            cameraPivot.localRotation =
                Quaternion.Euler(pitch, 0f, 0f);
        }

        if (fireAction.WasPressedThisFrame())
            Debug.Log("Fire pressed");

        if (aimAction.WasPressedThisFrame())
            Debug.Log("Aim pressed");

        if (interactAction.WasPressedThisFrame())
            Debug.Log("Interact pressed");
    }

    private void MoveLocally()
    {
        Vector2 input = moveAction.ReadValue<Vector2>();
        input = Vector2.ClampMagnitude(input, 1f);

        bool isRunning = input.sqrMagnitude > 0.01f;

        SetRunningAnimation(isRunning);

        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (controller.isGrounded &&
            jumpAction.WasPressedThisFrame())
        {
            verticalVelocity = Mathf.Sqrt(
                jumpHeight * -2f * gravity);

            PlayJumpAnimation();
        }

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 move =
            transform.right * input.x +
            transform.forward * input.y;

        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime * moveSpeed);
    }

    private void SetRunningAnimation(bool running)
    {
        if (animator == null || wasRunning == running)
            return;

        wasRunning = running;

        if (networkAnimator != null && IsSpawned && IsOwner)
        {
            networkAnimator.Animator.SetBool(
                runningParameterHash,
                running);
        }
        else
        {
            animator.SetBool(
                runningParameterHash,
                running);
        }
    }

    private void PlayJumpAnimation()
    {
        if (networkAnimator != null && IsSpawned && IsOwner)
        {
            networkAnimator.SetTrigger(jumpTriggerHash);
        }
        else if (animator != null)
        {
            animator.SetTrigger(jumpTriggerHash);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            playerMap?.Disable();

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
