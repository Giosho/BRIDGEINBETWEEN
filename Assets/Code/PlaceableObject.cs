using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Put this on anything the player can pick up and place on the grid (umbrella, crate, statue...).
/// GridPlacer on the player does the picking up and placing. "Item Id" is the name used in the
/// inventory and by ShadeObject / other puzzle scripts. The object needs a Collider to be picked up.
/// </summary>
public class PlaceableObject : MonoBehaviour
{
    public string itemId = "Umbrella";
    public string displayName = "Umbrella";

    public UnityEvent onPickedUp;
    public UnityEvent onPlaced;

    /// <summary>True while it sits in the world on a grid spot.</summary>
    public bool IsPlaced { get; internal set; }
    /// <summary>True while it is in the player's inventory.</summary>
    public bool IsCarried { get; internal set; }

    private Collider[] cols;
    private bool[] colWasEnabled;

    internal void SetCollidersEnabled(bool on)
    {
        if (cols == null) cols = GetComponentsInChildren<Collider>(true);
        if (!on)
        {
            colWasEnabled = new bool[cols.Length];
            for (int i = 0; i < cols.Length; i++) { colWasEnabled[i] = cols[i].enabled; cols[i].enabled = false; }
        }
        else if (colWasEnabled != null)
        {
            for (int i = 0; i < cols.Length; i++) cols[i].enabled = colWasEnabled[i];
            colWasEnabled = null;
        }
    }

    internal Bounds GetBounds()
    {
        var rs = GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return new Bounds(transform.position, Vector3.one * 0.5f);
        Bounds b = rs[0].bounds;
        for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
        return b;
    }

    internal void Tint(Color? c)
    {
        var rs = GetComponentsInChildren<Renderer>();
        MaterialPropertyBlock mpb = null;
        if (c.HasValue)
        {
            mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", c.Value); // URP
            mpb.SetColor("_Color", c.Value);     // built-in shaders
        }
        foreach (var r in rs) r.SetPropertyBlock(mpb);
    }
}
