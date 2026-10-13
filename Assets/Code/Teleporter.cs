using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Teleports the player when they walk into this trigger. Put it on an empty object with a Box Collider
/// (Is Trigger ticked) - for example just inside the pyramid door, so you cross it when the door is open.
/// Set "Destination" to an empty object where the player should appear.
/// </summary>
public class Teleporter : MonoBehaviour
{
    [Header("Where to")]
    [Tooltip("Empty object marking where the player appears (position, and facing if Match Rotation is on).")]
    public Transform destination;
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
        if (destination == null) Debug.LogWarning("Teleporter: no Destination set.", this);
        var col = GetComponent<Collider>();
        if (col == null) Debug.LogWarning("Teleporter: add a Box Collider with Is Trigger ticked.", this);
        else if (!col.isTrigger) Debug.LogWarning("Teleporter: the Collider needs Is Trigger ticked.", this);
    }

    public void Activate() { active = true; }
    public void Deactivate() { active = false; }

    void OnTriggerEnter(Collider other)
    {
        if (!active || destination == null || Time.time < readyTime) return;
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

        var cc = player.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;

        player.position = destination.position;
        var rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.position = destination.position;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        Physics.SyncTransforms();
        if (cc != null) cc.enabled = true;

        if (matchDestinationRotation)
        {
            var look = FindFirstObjectByType<MouseLook3D>();
            if (look != null) look.SetYaw(destination.eulerAngles.y);
            else player.rotation = Quaternion.Euler(0f, destination.eulerAngles.y, 0f);
        }

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
