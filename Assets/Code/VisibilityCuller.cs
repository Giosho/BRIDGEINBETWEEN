using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Hides renderers the player can't see and shows them again when they come into view.
/// An object counts as visible when it is (1) within View Distance, (2) inside the camera
/// frustum and (3) not fully blocked by other colliders (checked with raycasts).
///
/// Only Renderers are toggled - colliders, scripts and physics keep running, so puzzles
/// (LightReceiver, PressurePlate, ...) are not affected.
///
/// Setup: add this to your 3D player camera. Leave "Cull Roots" empty to cull every
/// renderer in the scene, or drag in parent objects (e.g. a "Rooms" object) to only cull
/// their children. For walls to block sight they need colliders.
/// </summary>
[DisallowMultipleComponent]
public class VisibilityCuller : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Camera used for the view test. Defaults to the Camera on this object / Camera.main.")]
    public Camera viewCamera;
    [Tooltip("Optional: only renderers under these objects are culled. Empty = whole scene.")]
    public Transform[] cullRoots;
    [Tooltip("Never culled, and never counted as something that blocks sight (e.g. the player).")]
    public Transform[] ignoreRoots;

    [Header("Visibility")]
    public float viewDistance = 80f;
    [Tooltip("Objects closer than this are always shown (stops pop-in right next to the player).")]
    public float alwaysVisibleDistance = 4f;
    [Tooltip("Extra frustum margin so objects load slightly before they enter the screen.")]
    public float frustumPadding = 1f;
    [Tooltip("Layers that can block line of sight. Don't include the player layer.")]
    public LayerMask occluderMask = ~0;
    [Tooltip("Tick to also hide objects hidden behind walls (raycast test). Untick for frustum + distance only.")]
    public bool useOcclusionRaycasts = true;

    [Header("Performance")]
    [Tooltip("How many objects are re-tested per frame.")]
    public int checksPerFrame = 100;
    [Tooltip("Seconds an object stays visible after it stops being seen (avoids flicker).")]
    public float hideDelay = 0.4f;

    [Header("Debug")]
    public bool logCounts;
    [SerializeField] private int totalTracked;
    [SerializeField] private int currentlyVisible;

    private class Target
    {
        public Renderer renderer;
        public bool shown = true;
        public float lastSeenTime;
    }

    private readonly List<Target> targets = new List<Target>();
    private readonly Plane[] planes = new Plane[6];
    private readonly RaycastHit[] hitBuffer = new RaycastHit[32];
    private int cursor;
    private float nextLog;

    void Awake()
    {
        if (viewCamera == null) viewCamera = GetComponent<Camera>();
        if (viewCamera == null) viewCamera = Camera.main;
    }

    void Start()
    {
        CollectTargets();
    }

    void OnDisable()
    {
        // Leave the scene fully visible if the culler is switched off.
        foreach (var t in targets)
            if (t.renderer != null) t.renderer.enabled = true;
    }

    void OnEnable()
    {
        foreach (var t in targets)
            t.shown = t.renderer != null && t.renderer.enabled;
    }

    /// <summary>Call again if you spawn new objects at runtime (e.g. ItemGenerator).</summary>
    public void CollectTargets()
    {
        targets.Clear();

        Renderer[] all;
        if (cullRoots != null && cullRoots.Length > 0)
        {
            var list = new List<Renderer>();
            foreach (var root in cullRoots)
                if (root != null) list.AddRange(root.GetComponentsInChildren<Renderer>(true));
            all = list.ToArray();
        }
        else
        {
            all = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        foreach (var r in all)
        {
            if (r == null || IsIgnored(r.transform)) continue;
            // Skip things that must always draw.
            if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
            if (viewCamera != null && r.transform.IsChildOf(viewCamera.transform)) continue;

            targets.Add(new Target { renderer = r, shown = r.enabled, lastSeenTime = Time.time });
        }

        cursor = 0;
        totalTracked = targets.Count;
    }

    void LateUpdate()
    {
        if (viewCamera == null || targets.Count == 0) return;

        GeometryUtility.CalculateFrustumPlanes(viewCamera, planes);

        int count = Mathf.Min(checksPerFrame, targets.Count);
        for (int i = 0; i < count; i++)
        {
            if (cursor >= targets.Count) cursor = 0;
            UpdateTarget(targets[cursor]);
            cursor++;
        }

        if (logCounts && Time.time >= nextLog)
        {
            nextLog = Time.time + 1f;
            int vis = 0;
            foreach (var t in targets) if (t.shown) vis++;
            currentlyVisible = vis;
            Debug.Log($"[VisibilityCuller] visible {vis} / {targets.Count}");
        }
    }

    private void UpdateTarget(Target t)
    {
        if (t.renderer == null) return;

        bool seen = IsSeen(t.renderer);
        if (seen) t.lastSeenTime = Time.time;

        bool shouldShow = seen || (Time.time - t.lastSeenTime) < hideDelay;
        if (shouldShow != t.shown)
        {
            t.shown = shouldShow;
            t.renderer.enabled = shouldShow;
        }
    }

    private bool IsSeen(Renderer r)
    {
        Bounds b = r.bounds;
        Vector3 camPos = viewCamera.transform.position;

        float dist = Vector3.Distance(camPos, b.ClosestPoint(camPos));
        if (dist <= alwaysVisibleDistance) return true;
        if (dist > viewDistance) return false;

        // Frustum test (slightly enlarged bounds so things appear just before entering the screen).
        Bounds padded = b;
        padded.Expand(frustumPadding * 2f);
        if (!GeometryUtility.TestPlanesAABB(planes, padded)) return false;

        if (!useOcclusionRaycasts) return true;

        // Line of sight: visible if a ray reaches the center or any corner of the bounds.
        if (HasLineOfSight(camPos, b.center, r)) return true;

        Vector3 c = b.center, e = b.extents * 0.9f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = c + new Vector3(
                (i & 1) == 0 ? -e.x : e.x,
                (i & 2) == 0 ? -e.y : e.y,
                (i & 4) == 0 ? -e.z : e.z);
            if (HasLineOfSight(camPos, p, r)) return true;
        }
        return false;
    }

    private bool HasLineOfSight(Vector3 from, Vector3 to, Renderer target)
    {
        Vector3 dir = to - from;
        float len = dir.magnitude;
        if (len < 0.01f) return true;
        dir /= len;

        // Ray stops just short of the point so surfaces the point sits on don't count as blockers.
        float maxDist = Mathf.Max(0f, len - 0.05f);
        int n = Physics.RaycastNonAlloc(from, dir, hitBuffer, maxDist, occluderMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < n; i++)
        {
            Transform ht = hitBuffer[i].collider.transform;
            if (ht.IsChildOf(target.transform) || target.transform.IsChildOf(ht)) continue; // its own collider
            if (IsIgnored(ht)) continue;                                                   // e.g. the player
            return false;                                                                   // something solid in the way
        }
        return true;
    }

    private bool IsIgnored(Transform t)
    {
        if (ignoreRoots == null) return false;
        foreach (var root in ignoreRoots)
            if (root != null && t.IsChildOf(root)) return true;
        return false;
    }
}
