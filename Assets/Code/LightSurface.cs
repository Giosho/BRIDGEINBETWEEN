using UnityEngine;

public enum LightSurfaceType
{
    Mirror, // reflects the beam
    Glass   // bends the beam (refraction) and lets it pass through
}

/// <summary>
/// Put this on any mirror or glass object the beam should interact with.
/// Objects WITHOUT this component simply block the beam.
/// </summary>
public class LightSurface : MonoBehaviour
{
    public LightSurfaceType type = LightSurfaceType.Mirror;

    [Tooltip("Only used for Glass. Air = 1, water = 1.33, glass = 1.5, diamond = 2.4")]
    [Range(1.0f, 2.5f)]
    public float refractiveIndex = 1.5f;
}
