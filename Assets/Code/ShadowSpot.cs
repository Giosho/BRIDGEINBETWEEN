using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A spot that must be in shadow. Every frame it checks whether anything solid is between the spot
/// and the sun. If it stays shaded for "Hold Time" seconds, onShadowed fires once
/// (hook it to NpcInteraction.CompleteGoal). Put a movable block / pickable crate over the spot to shade it.
/// The blocking object needs a Collider.
/// </summary>
public class ShadowSpot : MonoBehaviour
{
    [Tooltip("The sun. Empty = RenderSettings.sun or the first directional light found.")]
    public Light sun;
    public float holdTime = 0.5f;
    public float checkHeight = 0.2f;
    public LayerMask blockers = ~0;
    public bool latch = true;

    public UnityEvent onShadowed;
    public UnityEvent onLit;

    public bool IsShadowed { get; private set; }
    private float timer;
    private bool fired;

    void Start()
    {
        if (sun == null) sun = RenderSettings.sun;
        if (sun == null)
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
        if (sun == null) Debug.LogWarning("ShadowSpot: no directional light found.", this);
    }

    void Update()
    {
        if (sun == null || (latch && fired)) return;

        Vector3 origin = transform.position + Vector3.up * checkHeight;
        bool shaded = Physics.Raycast(origin, -sun.transform.forward, 200f, blockers, QueryTriggerInteraction.Ignore);

        timer = shaded ? timer + Time.deltaTime : 0f;
        bool now = timer >= holdTime;

        if (now && !IsShadowed) { IsShadowed = true; fired = true; onShadowed?.Invoke(); }
        else if (!now && IsShadowed) { IsShadowed = false; onLit?.Invoke(); }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = IsShadowed ? Color.blue : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        if (sun != null) Gizmos.DrawLine(transform.position, transform.position - sun.transform.forward * 5f);
    }
}
