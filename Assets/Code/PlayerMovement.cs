using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [Header("Input System Profiles")]
    public InputActionAsset inputActions;

    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public bool is2DPlayer = false;

    [Header("Sprint Settings (3D Player Only)")]
    public float sprintMultiplier = 1.6f;   // Sprint speed = moveSpeed * sprintMultiplier
    [SerializeField] private bool isSprinting; // Read-only debug info

    [Header("Jump Settings")]
    public float jumpForce = 8f;
    public float fallMultiplier = 2.5f;
    public float lowJumpMultiplier = 2f;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.3f;
    public LayerMask groundMask;

    [Header("Animation")]
    public Animator animator;                // Your model's Animator (auto-found in children if empty)
    public string speedParameter = "Speed";  // Float parameter in the Animator Controller
    public float animationDamp = 0.1f;       // Smooths idle <-> walk <-> run changes
    public float idleThreshold = 0.1f;       // Below this horizontal speed = idle

    [Header("Debug Info (Read Only)")]
    [SerializeField] private bool isGrounded;

    private Rigidbody rb;
    private Vector3 inputVector;
    private int speedHash;

    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction sprintAction;

    void Awake()
    {
        if (inputActions != null)
        {
            var gameplayMap = inputActions.FindActionMap("Gameplay");
            if (gameplayMap != null)
            {
                if (is2DPlayer)
                {
                    moveAction = gameplayMap.FindAction("Move2D");
                    jumpAction = gameplayMap.FindAction("Jump2D");
                }
                else
                {
                    moveAction = gameplayMap.FindAction("Move");
                    jumpAction = gameplayMap.FindAction("Jump");
                    sprintAction = gameplayMap.FindAction("Sprint");
                }
            }
        }

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // The Rigidbody moves the player, so the animation must NOT move the model too.
        // (Root motion is what makes the model drift away from the player parent.)
        if (animator != null)
            animator.applyRootMotion = false;

        speedHash = Animator.StringToHash(speedParameter);

        // THE FIX: make MouseLook3D turn THIS object (the one that walks).
        // Runs in Awake, before MouseLook3D's Start, so it picks this up.
        // Now "transform.forward" is always the direction you are looking.
        if (!is2DPlayer)
        {
            MouseLook3D look = FindFirstObjectByType<MouseLook3D>();
            if (look != null)
                look.playerBody = transform;
        }
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    void OnEnable()
    {
        moveAction?.Enable();
        jumpAction?.Enable();
        sprintAction?.Enable();
    }

    void OnDisable()
    {
        moveAction?.Disable();
        jumpAction?.Disable();
        sprintAction?.Disable();
    }

    void Update()
    {
        // Ground Check
        if (groundCheck != null)
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
        }

        Vector2 moveInput = Vector2.zero;
        if (moveAction != null)
        {
            moveInput = moveAction.ReadValue<Vector2>();
        }

        // Stand still while an NPC dialog is open
        if (DialogUI.IsOpen) moveInput = Vector2.zero;

        // --- 2D BEAN CONTROLS ---
        if (is2DPlayer)
        {
            inputVector = new Vector3(moveInput.x, 0f, moveInput.y);

            // Jump with "/" (Slash Key)
            if (jumpAction != null && jumpAction.triggered && isGrounded)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            }
        }
        // --- 3D BEAN CONTROLS ---
        else
        {
            // Standard movement: relative to where the player is facing
            inputVector = transform.forward * moveInput.y + transform.right * moveInput.x;

            // Sprint with Shift
            if (sprintAction != null)
            {
                isSprinting = sprintAction.IsPressed();
            }

            // Jump with Space
            if (jumpAction != null && jumpAction.triggered && isGrounded)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
            }
        }

        UpdateAnimation();
    }

    void FixedUpdate()
    {
        float currentSpeed = (!is2DPlayer && isSprinting) ? moveSpeed * sprintMultiplier : moveSpeed;

        Vector3 moveVelocity = inputVector.normalized * currentSpeed;
        rb.linearVelocity = new Vector3(moveVelocity.x, rb.linearVelocity.y, moveVelocity.z);

        // --- SNAPPY JUMP GRAVITY ---
        if (rb.linearVelocity.y < 0)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (fallMultiplier - 1) * Time.fixedDeltaTime;
        }
        else if (rb.linearVelocity.y > 0 && !GetJumpKeyPressed())
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * (lowJumpMultiplier - 1) * Time.fixedDeltaTime;
        }
    }

    // Sets the Animator "Speed" float: 0 = idle, 0.5 = walk, 1 = run
    private void UpdateAnimation()
    {
        if (animator == null || rb == null) return;

        Vector3 v = rb.linearVelocity;
        float horizontalSpeed = new Vector2(v.x, v.z).magnitude;

        float walkSpeed = moveSpeed;
        float runSpeed = moveSpeed * sprintMultiplier;

        float animSpeed;
        if (horizontalSpeed < idleThreshold)
        {
            animSpeed = 0f;
        }
        else if (horizontalSpeed <= walkSpeed)
        {
            animSpeed = Mathf.InverseLerp(0f, walkSpeed, horizontalSpeed) * 0.5f;
        }
        else
        {
            animSpeed = 0.5f + Mathf.InverseLerp(walkSpeed, runSpeed, horizontalSpeed) * 0.5f;
        }

        animator.SetFloat(speedHash, animSpeed, animationDamp, Time.deltaTime);
    }

    private bool GetJumpKeyPressed()
    {
        if (jumpAction != null)
        {
            return jumpAction.IsPressed();
        }
        return false;
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = isGrounded ? Color.green : Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }
}