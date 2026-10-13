using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>Global yes/no switches any script can set and any NPC stage can wait for.</summary>
public static class QuestFlags
{
    private static readonly HashSet<string> flags = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { flags.Clear(); }
    public static bool Has(string flag) => string.IsNullOrEmpty(flag) || flags.Contains(flag);
    public static void Set(string flag) { if (!string.IsNullOrEmpty(flag)) flags.Add(flag); }
    public static void Clear() => flags.Clear();
}

/// <summary>
/// A reusable NPC. Look at it, get close, press E.
///  - DialogOnly: only talks ("Dialog Lines").
///  - Quest:      works through an ordered list of Stages. Each stage can wait for items and/or a flag,
///                say lines, take items, give items, set a flag and fire a UnityEvent.
///                After the last stage it says "Finished Lines" forever.
/// Reuse it for anything: shopkeepers (take item A, give item B), hint givers, gatekeepers
/// (wait for a flag), multi-step chains (add more stages).
/// </summary>
public class NpcInteraction : MonoBehaviour
{
    public enum NpcMode { DialogOnly, Quest }

    [Serializable]
    public class Stage
    {
        public string label = "Stage";
        [Header("Requirements (all must be true to complete)")]
        [Tooltip("Item ids the player must be carrying, e.g. KeyPiece.")]
        public string[] requireItems;
        [Tooltip("A QuestFlags flag that must be set. Empty = none.")]
        public string requireFlag;

        [Header("Dialog")]
        [Tooltip("Said when the requirements are met. Left click = next line.")]
        [TextArea(2, 4)] public string[] lines = { "..." };
        [Tooltip("Said when the requirements are NOT met yet.")]
        [TextArea(2, 4)] public string[] linesWhileWaiting;

        [Header("When the dialog ends")]
        [Tooltip("Items removed from the player.")]
        public string[] takeItems;
        [Tooltip("Items added to the player.")]
        public string[] giveItems;
        [Tooltip("Flag set for other scripts / NPCs to react to. Empty = none.")]
        public string setFlag;
        [Tooltip("Shown at the top of the screen, e.g. \"You got the Pyramid Key!\". Empty = none.")]
        public string notice;
        public UnityEvent onComplete;
    }

    [Header("General")]
    public string npcName = "Villager";
    public NpcMode mode = NpcMode.DialogOnly;
    public string playerTag = "Player";
    public float interactRange = 3f;
    [Tooltip("Player camera, used to check the player is looking at the NPC. Defaults to Camera.main.")]
    public Camera playerCamera;
    [Range(-1f, 1f)] public float lookDot = 0.6f;
    [Tooltip("Optional: your input actions asset (uses Gameplay/Interact). Empty = the E key.")]
    public InputActionAsset inputActions;
    public string prompt = "Press E to talk";

    [Header("DialogOnly")]
    [TextArea(2, 4)] public string[] dialogLines = { "Hello, traveller." };

    [Header("Quest")]
    public List<Stage> stages = new List<Stage>
    {
        new Stage
        {
            label = "Intro",
            lines = new[]
            {
                "Oh, it is so hot here!",
                "There is a spot over there with something buried. If you shade it, maybe it shows itself.",
                "Find an umbrella and place it on that spot. Bring me what you find!"
            }
        },
        new Stage
        {
            label = "Fix the key",
            requireItems = new[] { "KeyPiece" },
            linesWhileWaiting = new[] { "Put an umbrella on the spot over there. I need whatever is hiding in the sand." },
            lines = new[]
            {
                "Ahh, that's cooler already, thank you!",
                "Is that a piece of a key? Let me see...",
                "I can fix it. Here, take this key. It opens the pyramid."
            },
            takeItems = new[] { "KeyPiece" },
            giveItems = new[] { "PyramidKey" },
            notice = "You got the Pyramid Key!"
        }
    };
    [TextArea(2, 4)] public string[] finishedLines = { "Good luck inside the pyramid!" };

    [Header("Debug")]
    [SerializeField] private int stageIndex;

    private Transform player;
    private InputAction interactAction;
    private static NpcInteraction promptOwner;

    void Awake()
    {
        if (inputActions != null) interactAction = inputActions.FindActionMap("Gameplay")?.FindAction("Interact");
    }

    void OnEnable() { interactAction?.Enable(); }
    void OnDisable() { interactAction?.Disable(); }

    void Start()
    {
        player = PlayerFinder.FindPlayer(playerTag);
        if (player == null) Debug.LogWarning($"NpcInteraction ({npcName}): player not found (no \"{playerTag}\" tag and no PlayerMovement).", this);
        if (playerCamera == null) playerCamera = PlayerFinder.FindCamera(player);
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

    public void Talk()
    {
        var ui = DialogUI.Instance;
        if (mode == NpcMode.DialogOnly) { ui.Show(npcName, dialogLines); return; }

        SkipAheadIfReady();
        if (stageIndex >= stages.Count) { ui.Show(npcName, finishedLines); return; }

        Stage st = stages[stageIndex];
        if (Satisfied(st))
        {
            ui.Show(npcName, st.lines, () => Complete(st));
        }
        else
        {
            var waiting = (st.linesWhileWaiting != null && st.linesWhileWaiting.Length > 0)
                ? st.linesWhileWaiting
                : new[] { "Come back when you have what I need." };
            ui.Show(npcName, waiting);
        }
    }

    // The player may already hold what a LATER stage asks for (e.g. found the key piece before
    // talking). Jump straight to that stage so the NPC doesn't ignore it.
    void SkipAheadIfReady()
    {
        for (int j = stages.Count - 1; j > stageIndex; j--)
        {
            var st = stages[j];
            if (st.requireItems != null && st.requireItems.Length > 0 && Satisfied(st))
            {
                stageIndex = j;
                return;
            }
        }
    }

    bool Satisfied(Stage st)
    {
        if (!QuestFlags.Has(st.requireFlag)) return false;
        if (st.requireItems == null || st.requireItems.Length == 0) return true;

        var inv = PlayerInventory.Instance;
        if (inv == null) return false;
        foreach (var id in st.requireItems)
            if (!string.IsNullOrEmpty(id) && !inv.Has(id)) return false;
        return true;
    }

    void Complete(Stage st)
    {
        var inv = PlayerInventory.Instance;
        if (inv != null)
        {
            if (st.takeItems != null) foreach (var id in st.takeItems) inv.Remove(id);
            if (st.giveItems != null) foreach (var id in st.giveItems) inv.Add(id);
        }
        else if ((st.takeItems?.Length ?? 0) + (st.giveItems?.Length ?? 0) > 0)
        {
            Debug.LogWarning("NpcInteraction: add a PlayerInventory to the player so items can be given / taken.", this);
        }

        QuestFlags.Set(st.setFlag);
        if (!string.IsNullOrEmpty(st.notice)) DialogUI.Instance.ShowNotice(st.notice);
        st.onComplete?.Invoke();
        stageIndex++;
    }

    /// <summary>Hook any UnityEvent to this to set a flag, e.g. ShadowSpot.onShadowed -> SetFlag("ShadowDone").</summary>
    public void SetFlag(string flag) => QuestFlags.Set(flag);

    /// <summary>Jump to a stage (e.g. to reset the NPC).</summary>
    public void GoToStage(int index) => stageIndex = Mathf.Max(0, index);

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
