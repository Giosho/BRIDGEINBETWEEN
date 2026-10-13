using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

/// <summary>
/// Teleports the player when they walk into this trigger. Put it on an empty object with a Box Collider
/// (Is Trigger ticked) - for example just inside the pyramid door, so you cross it when the door is open.
/// Set "Destination" to an empty object where the player should appear.
/// </summary>
public class Teleporter : MonoBehaviour
{
    [Header("Where to - same scene")]
    [Tooltip("Empty object marking where the player appears (position, and facing if Match Rotation is on). Leave empty when using Target Scene.")]
    public Transform destination;

    [Header("Where to - other scene")]
    [Tooltip("Name of the scene to load, e.g. EgyptScene. It must be in File > Build Profiles / Build Settings > Scene List. Empty = stay in this scene.")]
    public string targetScene;
    [Tooltip("Optional: name of an empty object in the NEW scene where the player should appear. Empty = wherever that scene's player starts.")]
    public string spawnPointName;
    [Tooltip("Turn the player to face the same way as the destination object (its blue Z arrow).")]
    public bool matchDestinationRotation = true;

    [Header("When")]
    [Tooltip("Untick to keep it off until Activate() is called, e.g. from KeyLock > On Unlocked.")]
    public bool startActive = true;
    [Tooltip("Seconds before it can be used again (stops bouncing back and forth between two teleporters).")]
    public float cooldown = 1f;
    public string playerTag = "Player";

    [Header("Extras")]
    [Tooltip("Optional message at the top of the screen after teleporting.")]
    public string notice;
    public UnityEvent onTeleported;

    private bool active;
    private float readyTime;

    void Start()
    {
        active = startActive;
        if (destination == null && string.IsNullOrEmpty(targetScene))
            Debug.LogWarning("Teleporter: set a Destination (same scene) or a Target Scene (other scene).", this);
        if (!string.IsNullOrEmpty(targetScene) && !Application.CanStreamedLevelBeLoaded(targetScene))
            Debug.LogError($"Teleporter: scene '{targetScene}' can't be loaded. Check the spelling and add it to Build Settings > Scene List.", this);
        var col = GetComponent<Collider>();
        if (col == null) Debug.LogWarning("Teleporter: add a Box Collider with Is Trigger ticked.", this);
        else if (!col.isTrigger) Debug.LogWarning("Teleporter: the Collider needs Is Trigger ticked.", this);
    }

    public void Activate() { active = true; }
    public void Deactivate() { active = false; }

    void OnTriggerEnter(Collider other)
    {
        if (!active || Time.time < readyTime) return;
        if (destination == null && string.IsNullOrEmpty(targetScene)) return;
        if (!PlayerFinder.IsPlayer(other, playerTag)) return;

        Transform player = other.GetComponentInParent<PlayerMovement>() != null
            ? other.GetComponentInParent<PlayerMovement>().transform
            : PlayerFinder.FindPlayer(playerTag);
        if (player == null) return;

        Teleport(player);
    }

    void Teleport(Transform player)
    {
        readyTime = Time.time + cooldown;

        if (!string.IsNullOrEmpty(targetScene))
        {
            if (!Application.CanStreamedLevelBeLoaded(targetScene))
            {
                Debug.LogError($"Teleporter: scene '{targetScene}' can't be loaded. Add it to Build Settings > Scene List.", this);
                return;
            }
            TeleportSpawn.pendingSpawnName = spawnPointName;
            active = false;
            onTeleported?.Invoke();
            SceneManager.LoadScene(targetScene);
            return;
        }

        TeleportSpawn.Place(player, destination.position, destination.eulerAngles.y, matchDestinationRotation);

        if (!string.IsNullOrEmpty(notice)) DialogUI.Instance.ShowNotice(notice);
        onTeleported?.Invoke();
    }

    void OnDrawGizmos()
    {
        Gizmos.color = new Color(0.6f, 0.3f, 1f, 0.9f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * 0.5f);
        if (destination != null)
        {
            Gizmos.DrawLine(transform.position, destination.position);
            Gizmos.DrawWireSphere(destination.position, 0.4f);
            Gizmos.DrawRay(destination.position, destination.forward * 1.2f);
        }
    }
}

/// <summary>
/// Moves the player to a position, and after a scene change to a named spawn point in the new scene.
/// </summary>
public static class TeleportSpawn
{
    /// <summary>Set by Teleporter just before loading a scene.</summary>
    public static string pendingSpawnName;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        string name = pendingSpawnName;
        pendingSpawnName = null;
        if (string.IsNullOrEmpty(name)) return;

        var spawn = GameObject.Find(name);
        var player = PlayerFinder.FindPlayer();
        if (spawn == null) { Debug.LogWarning($"Teleporter: spawn point '{name}' not found in scene '{scene.name}'."); return; }
        if (player == null) { Debug.LogWarning($"Teleporter: no player found in scene '{scene.name}'."); return; }

        Place(player, spawn.transform.position, spawn.transform.eulerAngles.y, true);
    }

    public static void Place(Transform player, Vector3 position, float yaw, bool matchRotation)
    {
        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.position = position;
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = position;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        Physics.SyncTransforms();
        if (cc != null) cc.enabled = true;

        if (matchRotation)
        {
            var look = Object.FindFirstObjectByType<MouseLook3D>();
            if (look != null) look.SetYaw(yaw);
            else player.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
    }
}
