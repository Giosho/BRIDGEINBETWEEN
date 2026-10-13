using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A spot that reacts when a PlaceableObject with the right Item Id (the umbrella) is placed on it.
/// Put it on an empty object (or the treasure spot itself) at the spot's position. When shaded it shows
/// "Reveal Object" (e.g. the key piece, which is a GameItem of type Key with keyId "KeyPiece").
/// Walking into the revealed key piece puts it in the inventory.
/// Reusable: any item id, any reveal object, any UnityEvent - not only umbrellas.
/// </summary>
public class ShadeObject : MonoBehaviour
{
    private static readonly List<ShadeObject> all = new List<ShadeObject>();

    [Tooltip("Item id of the PlaceableObject that shades this spot.")]
    public string requiredItemId = "Umbrella";
    [Tooltip("How close (on the ground) the placed object must be.")]
    public float radius = 0.75f;

    [Header("Reveal")]
    [Tooltip("Hidden at start, shown when shaded (e.g. the key piece). Make it a GameItem so it can be collected.")]
    public GameObject revealObject;
    [Tooltip("Hide it again if the umbrella is picked back up (before it was collected).")]
    public bool hideWhenRemoved = true;

    [Header("Events")]
    public UnityEvent onShaded;
    public UnityEvent onUnshaded;

    public bool IsShaded { get; private set; }
    private PlaceableObject shader;

    void OnEnable()
    {
        all.Add(this);
        GridPlacer.OnPlaced += HandlePlaced;
        GridPlacer.OnRemoved += HandleRemoved;
    }

    void OnDisable()
    {
        all.Remove(this);
        GridPlacer.OnPlaced -= HandlePlaced;
        GridPlacer.OnRemoved -= HandleRemoved;
    }

    void Start()
    {
        if (revealObject != null) revealObject.SetActive(false);
    }

    /// <summary>The nearest matching spot within range (ground distance), or null. Used by GridPlacer to snap.</summary>
    public static ShadeObject FindNear(string itemId, Vector3 point, float range)
    {
        ShadeObject best = null;
        float bestDist = range;
        foreach (var s in all)
        {
            if (s.requiredItemId != itemId) continue;
            float d = Flat(s.transform.position - point).magnitude;
            if (d <= bestDist) { best = s; bestDist = d; }
        }
        return best;
    }

    static Vector3 Flat(Vector3 v) { v.y = 0f; return v; }

    void HandlePlaced(PlaceableObject p)
    {
        if (IsShaded || p.itemId != requiredItemId) return;
        if (Flat(p.transform.position - transform.position).magnitude > radius) return;

        shader = p;
        IsShaded = true;
        if (revealObject != null) revealObject.SetActive(true);
        onShaded?.Invoke();
    }

    void HandleRemoved(PlaceableObject p)
    {
        if (!IsShaded || p != shader) return;

        shader = null;
        IsShaded = false;
        if (hideWhenRemoved && revealObject != null) revealObject.SetActive(false);
        onUnshaded?.Invoke();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = IsShaded ? Color.green : new Color(1f, 0.8f, 0.2f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
