using UnityEngine;

/// <summary>
/// Shoots a beam along its forward (blue Z) axis, bounces it off mirrors,
/// bends it through glass, and notifies any LightReceiver it touches.
/// Draws the beam with a LineRenderer.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
public class LightEmitter : MonoBehaviour
{
    [Header("Beam")]
    [Tooltip("Optional child transform the beam starts from (e.g. the tip of a statue's eye). Uses this object if empty.")]
    [SerializeField] private Transform beamOrigin;
    [SerializeField] private int maxSegments = 12;
    [SerializeField] private float maxDistance = 60f;
    [Tooltip("Layers the beam can hit. Exclude your Player layer so the beam doesn't get blocked by the player.")]
    [SerializeField] private LayerMask hitMask = ~0;

    private const float Offset = 0.001f; // avoids re-hitting the surface we just left

    private LineRenderer line;
    private Vector3[] points;
    private int pointCount;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        points = new Vector3[maxSegments * 3 + 2];
    }

    private void Update()
    {
        TraceBeam();
    }

    private void AddPoint(Vector3 p)
    {
        if (pointCount < points.Length) points[pointCount++] = p;
    }

    private void TraceBeam()
    {
        Transform start = beamOrigin != null ? beamOrigin : transform;
        Vector3 origin = start.position;
        Vector3 dir = start.forward;
        float remaining = maxDistance;

        pointCount = 0;
        AddPoint(origin);

        for (int i = 0; i < maxSegments && remaining > 0f; i++)
        {
            if (!Physics.Raycast(origin, dir, out RaycastHit hit, remaining, hitMask, QueryTriggerInteraction.Ignore))
            {
                // Nothing hit: beam travels to max distance
                AddPoint(origin + dir * remaining);
                break;
            }

            AddPoint(hit.point);
            remaining -= hit.distance;

            // 1) Did we hit a receiver? Tell it and stop.
            LightReceiver receiver = hit.collider.GetComponentInParent<LightReceiver>();
            if (receiver != null)
            {
                receiver.RegisterHit();
                break;
            }

            // 2) Mirror / glass? Otherwise the beam is blocked.
            LightSurface surface = hit.collider.GetComponentInParent<LightSurface>();
            if (surface == null) break;

            if (surface.type == LightSurfaceType.Mirror)
            {
                dir = Vector3.Reflect(dir, hit.normal);
                origin = hit.point + dir * Offset;
            }
            else // Glass
            {
                if (!TryPassThroughGlass(surface, hit, dir, out Vector3 newOrigin, out Vector3 newDir))
                    break;

                origin = newOrigin;
                dir = newDir;
            }
        }

        line.positionCount = pointCount;
        for (int i = 0; i < pointCount; i++)
            line.SetPosition(i, points[i]);
    }

    /// <summary>
    /// Bends the beam into the glass, finds where it exits (convex colliders only),
    /// then bends it back out. Handles total internal reflection.
    /// </summary>
    private bool TryPassThroughGlass(LightSurface glass, RaycastHit entryHit, Vector3 incomingDir,
                                     out Vector3 exitOrigin, out Vector3 exitDir)
    {
        exitOrigin = Vector3.zero;
        exitDir = Vector3.zero;

        Collider col = entryHit.collider;

        // Air -> glass
        if (!Refract(incomingDir, entryHit.normal, 1f, glass.refractiveIndex, out Vector3 inside))
            return false;

        Vector3 pos = entryHit.point;
        float reach = col.bounds.size.magnitude + 1f;

        // Allow a few internal bounces (total internal reflection)
        for (int bounce = 0; bounce < 4; bounce++)
        {
            // Trick: raycasts starting inside a collider don't hit it, so we cast
            // BACKWARDS from far outside the collider to find the exit surface.
            Vector3 far = pos + inside * reach;
            if (!col.Raycast(new Ray(far, -inside), out RaycastHit exitHit, reach + 1f))
                return false;

            AddPoint(exitHit.point);

            Vector3 inwardNormal = -exitHit.normal; // faces the beam travelling inside

            // Glass -> air
            if (Refract(inside, inwardNormal, glass.refractiveIndex, 1f, out Vector3 outDir))
            {
                exitDir = outDir;
                exitOrigin = exitHit.point + outDir * Offset;
                return true;
            }

            // Total internal reflection: bounce inside and try again
            inside = Vector3.Reflect(inside, inwardNormal);
            pos = exitHit.point;
        }

        return false;
    }

    /// <summary>
    /// Snell's law. 'normal' must face the side the beam comes from.
    /// Returns false on total internal reflection.
    /// </summary>
    private static bool Refract(Vector3 incident, Vector3 normal, float n1, float n2, out Vector3 refracted)
    {
        float ratio = n1 / n2;
        float cosI = -Vector3.Dot(normal, incident);
        float sinT2 = ratio * ratio * (1f - cosI * cosI);

        if (sinT2 > 1f)
        {
            refracted = Vector3.zero;
            return false;
        }

        float cosT = Mathf.Sqrt(1f - sinT2);
        refracted = (ratio * incident + (ratio * cosI - cosT) * normal).normalized;
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Transform start = beamOrigin != null ? beamOrigin : transform;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(start.position, start.forward * 3f);
    }
}
