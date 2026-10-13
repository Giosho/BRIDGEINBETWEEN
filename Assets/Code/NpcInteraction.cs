using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// An NPC the player can talk to (look at it, get close, press the Interact key - E by default).
/// Pick what the NPC can do with "Mode":
///  - DialogOnly: it can only talk. Shows "Dialog Lines" as captions, left click = next line, closes after the last.
///  - KeyQuest:   puzzle NPC. Talks (Intro) -> player does the task -> CompleteGoal() is called ->
///                player gets a Key Piece -> player talks to the NPC again -> NPC takes the piece and
///                hands over the fixed key (e.g. for the pyramid door, see KeyLock).
/// Needs a Collider on the NPC only if you want one; the player just needs the tag "Player".
/// </summary>
public class NpcInteraction : MonoBehaviour
{
    public enum NpcMode { DialogOnly, KeyQuest }
    private enum Stage { Intro, WaitingForGoal, HasPiece, Done }

    [Header("General")]
    public string npcName = "Villager";
    [Tooltip("What this NPC can do.")]
    public NpcMode mode = NpcMode.DialogOnly;
    public string playerTag = "Player";
    public float interactRange = 3f;
    [Tooltip("Player camera. Used to check the player is looking at the NPC. Defaults to Camera.main.")]
    public Camera playerCamera;
    [Range(-1f, 1f)] public float lookDot = 0.6f;
    [Tooltip("Optional: your input actions asset (uses the Gameplay/Interact action). Empty = the E key.")]
    public InputActionAsset inputActions;
    public string prompt = "Press E to talk";

    [Header("DialogOnly")]
    [TextArea(2, 4)] public string[] dialogLines = { "Hello, traveller." };

    [Header("KeyQuest - dialog")]
    [TextArea(2, 4)] public string[] introLines =
    {
        "Oh, it is so hot here!",
        "Could you put a shadow on that spot over there?",
        "Help me out and I will reward you."
    };
    [TextArea(2, 4)] public string[] waitingLines = { "Please, put a shadow on that spot. It is burning!" };
    [TextArea(2, 4)] public string[] tradeLines =
    {
        "Ahh, so cool now, thank you!",
        "Is that a piece of a key? Let me see...",
        "I can fix it. Here, take this key. It opens the pyramid."
    };
    [TextArea(2, 4)] public string[] doneLines = { "Good luck inside the pyramid!" };

    [Header("KeyQuest - items")]
    [Tooltip("Id of the broken key piece the player receives when the goal is done.")]
    public string keyPieceId = "KeyPiece";
    public string keyPieceName = "Piece of a Key";
    [Tooltip("Id of the fixed key the NPC gives back. Use the same id on the KeyLock.")]
    public string fixedKeyId = "PyramidKey";
    public string fixedKeyName = "Pyramid Key";

    [Header("Events")]
    public UnityEvent onGoalCompleted;
    public UnityEvent onKeyGiven;

    private Stage stage = Stage.Intro;
    private bool goalDone;
    private Transform player;
    private InputAction interactAction;

    void Awake()
    {
        if (inputActions != null)
        {
            var map = inputActions.FindActionMap("Gameplay");
            interactAction = map?.FindAction("Interact");
        }
    }

    void OnEnable() { interactAction?.Enable(); }
    void OnDisable() { interactAction?.Disable(); }

    void Start()
    {
        if (playerCamera == null) playerCamera = Camera.main;
        var p = GameObject.FindGameObjectWithTag(playerTag);
        if (p != null) player = p.transform;
        else Debug.LogWarning($"NpcInteraction ({npcName}): no object tagged \"{playerTag}\" found.", this);
    }

    void Update()
    {
        if (player == null || DialogUI.IsOpen) return;

        bool inReach = CanTalk();
        if (inReach)
        {
            DialogUI.Instance.SetPrompt(prompt);
            promptOwner = this;
        }
        else if (promptOwner == this)
        {
            DialogUI.Instance.SetPrompt(null);
            promptOwner = null;
        }

        if (inReach && InteractPressed()) Talk();
    }

    private static NpcInteraction promptOwner;

    bool CanTalk()
    {
        if ((player.position - transform.position).sqrMagnitude > interactRange * interactRange) return false;
        if (playerCamera == null) return true;
        Vector3 to = (transform.position - playerCamera.transform.position).normalized;
        return Vector3.Dot(playerCamera.transform.forward, to) >= lookDot;
    }

    bool InteractPressed()
    {
        if (interactAction != null) return interactAction.triggered;
        return Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
    }

    void Talk()
    {
        var ui = DialogUI.Instance;
        if (mode == NpcMode.DialogOnly)
        {
            ui.Show(npcName, dialogLines);
            return;
        }

        switch (stage)
        {
            case Stage.Intro:
                ui.Show(npcName, introLines, () =>
                {
                    stage = Stage.WaitingForGoal;
                    TryGivePiece();
                });
                break;
            case Stage.WaitingForGoal:
                ui.Show(npcName, waitingLines);
                break;
            case Stage.HasPiece:
                ui.Show(npcName, tradeLines, GiveFixedKey);
                break;
            default:
                ui.Show(npcName, doneLines);
                break;
        }
    }

    /// <summary>
    /// Call this when the player finished the NPC's task (hook ShadowSpot.onShadowed to it).
    /// If the player has not heard the intro yet, the piece is given right after it.
    /// </summary>
    public void CompleteGoal()
    {
        if (mode != NpcMode.KeyQuest || goalDone) return;
        goalDone = true;
        onGoalCompleted?.Invoke();
        TryGivePiece();
    }

    void TryGivePiece()
    {
        if (!goalDone || stage != Stage.WaitingForGoal) return;
        var inv = PlayerInventory.Instance;
        if (inv == null) { Debug.LogWarning("NpcInteraction: add a PlayerInventory to the player.", this); return; }

        inv.Add(keyPieceId);
        stage = Stage.HasPiece;
        DialogUI.Instance.ShowNotice($"You got: {keyPieceName}. Bring it to {npcName}!");
    }

    void GiveFixedKey()
    {
        var inv = PlayerInventory.Instance;
        if (inv == null) return;

        inv.Remove(keyPieceId);   // the NPC takes the broken piece...
        inv.Add(fixedKeyId);      // ...and hands back the fixed key
        stage = Stage.Done;
        DialogUI.Instance.ShowNotice($"You got: {fixedKeyName}");
        onKeyGiven?.Invoke();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
