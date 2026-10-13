using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// Put this on the pyramid entrance (needs a Collider). Walk up, look at it and press the Interact key (E):
/// if the player holds the key it unlocks and onUnlocked fires. Hook onUnlocked to PuzzleDoor.AddInput
/// (or anything else) in the Inspector.
/// </summary>
public class KeyLock : MonoBehaviour
{
    public string requiredKeyId = "PyramidKey";
    public bool consumeKey = false;
    public float interactRange = 3.5f;
    public Camera playerCamera;
    [Range(-1f, 1f)] public float lookDot = 0.6f;
    public string playerTag = "Player";
    public InputActionAsset inputActions;
    public string lockedText = "It is locked. You need a key.";
    public string unlockedText = "The door unlocks!";
    public string prompt = "Press E to open";

    public UnityEvent onUnlocked;
    public bool IsUnlocked { get; private set; }

    private Transform player;
    private InputAction interactAction;

    void Awake()
    {
        if (inputActions != null) interactAction = inputActions.FindActionMap("Gameplay")?.FindAction("Interact");
    }
    void OnEnable() { interactAction?.Enable(); }
    void OnDisable() { interactAction?.Disable(); }

    void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        var p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null) player = p.transform;
    }

    private bool showingPrompt;

    bool InReach()
    {
        if ((player.position - transform.position).sqrMagnitude > interactRange * interactRange) return false;
        if (playerCamera == null) return true;
        Vector3 to = (transform.position - playerCamera.transform.position).normalized;
        return Vector3.Dot(playerCamera.transform.forward, to) >= lookDot;
    }

    void Update()
    {
        if (IsUnlocked || player == null || DialogUI.IsOpen) return;

        if (!InReach())
        {
            if (showingPrompt) { DialogUI.Instance.SetPrompt(null); showingPrompt = false; }
            return;
        }

        DialogUI.Instance.SetPrompt(prompt);
        showingPrompt = true;
        bool pressed = interactAction != null ? interactAction.triggered
                       : Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
        if (!pressed) return;

        var inv = PlayerInventory.Instance;
        if (inv != null && inv.Has(requiredKeyId))
        {
            if (consumeKey) inv.Remove(requiredKeyId);
            IsUnlocked = true;
            DialogUI.Instance.SetPrompt(null);
            DialogUI.Instance.ShowNotice(unlockedText);
            onUnlocked?.Invoke();
        }
        else
        {
            DialogUI.Instance.ShowNotice(lockedText);
        }
    }
}
