using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Hides renderers the player can't see and shows them again when they come into view.
/// An object counts as visible when it is (1) within View Distance, (2) inside the camera
/// frustum and (3) not fully blocked by solid, rendered objects (checked with raycasts).
///
/// Only Renderers are toggled - colliders, scripts and physics keep running, so puzzles
/// (LightReceiver, PressurePlate, ...) are not affected.
///
/// Setup: add this to your 3D player camera. Leave "Cull Roots" empty to cull every
/// renderer in the scene, or drag in parent objects (e.g. a "Rooms" object) to only cull
/// their children. For walls to block sight they need colliders.
///
/// View distance can be changed in the Inspector, with PageUp / PageDown in game, or with the
/// slider in the options menu (saved through SettingsManager).
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

    [Header("View Distance")]
    [Tooltip("How far you can see, in metres. Anything further away is not drawn.")]
    public float viewDistance = 150f;
    public float minViewDistance = 20f;
    public float maxViewDistance = 500f;
    [Tooltip("Also sets the camera's Far Clipping Plane to the view distance, so nothing beyond it is drawn at all.")]
    public bool syncCameraFarClip = true;
    [Tooltip("PageUp / PageDown change the view distance while playing.")]
    public bool enableHotkeys = true;
    public Key increaseKey = Key.PageUp;
    public Key decreaseKey = Key.PageDown;
    public float hotkeyStep = 20f;

    [Header("Visibility")]
    [Tooltip("Objects closer than this are always shown (stops pop-in right next to the player).")]
    public float alwaysVisibleDistance = 6f;
    [Tooltip("Extra frustum margin (metres) so objects load slightly before they enter the screen.")]
    public float frustumPadding = 2f;

    [Header("Hiding behind walls (raycasts)")]
    [Tooltip("Tick to also hide objects hidden behind walls. Untick for distance + camera view only (safest).")]
    public bool useOcclusionRaycasts = true;
    [Tooltip("Layers that can block line of sight. Don't include the player layer.")]
    public LayerMask occluderMask = ~0;
    [Tooltip("Only colliders that belong to something visible (a mesh) or terrain can block sight. Stops invisible colliders from hiding things.")]
    public bool onlyRenderedCollidersBlock = true;
    [Range(1, 27), Tooltip("Rays tried per object before it counts as hidden. More = fewer wrongly hidden objects, a bit more CPU.")]
    public int samplesPerObject = 15;
    [Tooltip("Rays stop this many metres before the target point, so floors / terrain next to it don't count as blockers.")]
    public float occlusionTolerance = 0.3f;
    [Tooltip("Objects bigger than this (floors, big walls, terrain chunks) skip the wall test and only use distance + camera view.")]
    public float skipOcclusionAboveSize = 12f;

    [Header("Performance")]
    [Tooltip("Maximum raycasts per frame for the wall test.")]
    public int maxRaysPerFrame = 600;
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
        public int mode;            // 0 = out of range / view, 1 = visible without ray test, 2 = needs ray test
        public bool rayVisible = true; // last wall-test result
    }

    private static readonly Vector3[] SampleOffsets = BuildOffsets();

    private readonly List<Target> targets = new List<Target>();
    private readonly List<Target> urgent = new List<Target>();   // in view but currently hidden
    private readonly List<Target> routine = new List<Target>();  // in view and currently shown
    private readonly Dictionary<Collider, bool> blockerCache = new Dictionary<Collider, bool>();
    private readonly Plane[] planes = new Plane[6];
    private readonly RaycastHit[] hitBuffer = new RaycastHit[64];
    private int urgentCursor;
    private int routineCursor;
    private float nextLog;

    // Order matters: the most useful sample points first, so most visible objects need 1-2 rays.
    static Vector3[] BuildOffsets()
    {
        var l = new List<Vector3>
        {
            new Vector3(0, 0, 0),     // centre
            new Vector3(0, 1, 0),     // top
            new Vector3(1, 0, 0), new Vector3(-1, 0, 0), new Vector3(0, 0, 1), new Vector3(0, 0, -1), new Vector3(0, -1, 0),
        };
        for (int i = 0; i < 8; i++)   // corners
            l.Add(new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
        foreach (int a in new[] { -1, 1 })   // edge midpoints
            foreach (int b in new[] { -1, 1 })
            {
                l.Add(new Vector3(a, b, 0));
                l.Add(new Vector3(a, 0, b));
                l.Add(new Vector3(0, a, b));
            }
        return l.ToArray();
    }

    void Awake()
    {
        if (viewCamera == null) viewCamera = GetComponent<Camera>();
        if (viewCamera == null) viewCamera = Camera.main;
    }

    void Start()
    {
        // A value saved from the options menu wins over the Inspector value.
        var sm = SettingsManager.Instance;
        if (sm != null && sm.ViewDistance > 0f) viewDistance = Mathf.Clamp(sm.ViewDistance, minViewDistance, maxViewDistance);
        ApplyFarClip();
        CollectTargets();
    }

    void OnEnable()
    {
        SettingsManager.OnViewDistanceChanged += HandleSettingsChanged;
        foreach (var t in targets)
            t.shown = t.renderer != null && t.renderer.enabled;
    }

    void OnDisable()
    {
        SettingsManager.OnViewDistanceChanged -= HandleSettingsChanged;
        // Leave the scene fully visible if the culler is switched off.
        foreach (var t in targets)
            if (t.renderer != null) t.renderer.enabled = true;
    }

    void HandleSettingsChanged(float value)
    {
        value = Mathf.Clamp(value, minViewDistance, maxViewDistance);
        if (Mathf.Approximately(value, viewDistance)) return;
        viewDistance = value;
        ApplyFarClip();
    }

    /// <summary>Change how far you can see (metres). Hook a UI Slider's On Value Changed to this.</summary>
    public void SetViewDistance(float value)
    {
        value = Mathf.Clamp(value, minViewDistance, maxViewDistance);
        if (Mathf.Approximately(value, viewDistance)) return;
        viewDistance = value;
        ApplyFarClip();
        SettingsManager.Instance?.SetViewDistance(value);
    }

    void ApplyFarClip()
    {
        if (syncCameraFarClip && viewCamera != null)
            viewCamera.farClipPlane = Mathf.Max(viewDistance, viewCamera.nearClipPlane + 1f);
    }

    /// <summary>Call again if you spawn new objects at runtime (e.g. ItemGenerator).</summary>
    public void CollectTargets()
    {
        targets.Clear();
        blockerCache.Clear();

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
            if (r.GetComponentInParent<ShadeObject>() != null) continue; // ShadeObject hides / shows these itself

            targets.Add(new Target { renderer = r, shown = r.enabled, lastSeenTime = Time.time });
        }

        urgentCursor = routineCursor = 0;
        totalTracked = targets.Count;
    }

    void Update()
    {
        if (!enableHotkeys || Keyboard.current == null) return;
        if (Keyboard.current[increaseKey].wasPressedThisFrame) ChangeByHotkey(+hotkeyStep);
        else if (Keyboard.current[decreaseKey].wasPressedThisFrame) ChangeByHotkey(-hotkeyStep);
    }

    void ChangeByHotkey(float delta)
    {
        SetViewDistance(viewDistance + delta);
        DialogUI.Instance.ShowNotice($"View distance: {Mathf.RoundToInt(viewDistance)} m", 1.5f);
    }

    void LateUpdate()
    {
        if (viewCamera == null || targets.Count == 0) return;

        GeometryUtility.CalculateFrustumPlanes(viewCamera, planes);
        Vector3 camPos = viewCamera.transform.position;

        // Pass 1 (cheap, every object, every frame): distance + camera view.
        urgent.Clear();
        routine.Clear();
        foreach (var t in targets)
        {
            if (t.renderer == null) { t.mode = 0; continue; }
            t.mode = CheapTest(t.renderer, camPos);
            if (t.mode == 2) (t.shown ? routine : urgent).Add(t);
        }

        // Pass 2 (expensive, limited): wall test. Objects that are in view but hidden go first,
        // so something you turn towards appears right away.
        int raysLeft = maxRaysPerFrame;
        RunRayTests(urgent, ref urgentCursor, ref raysLeft, Mathf.CeilToInt(maxRaysPerFrame * 0.7f), camPos);
        RunRayTests(routine, ref routineCursor, ref raysLeft, raysLeft, camPos);

        // Pass 3: apply.
        int vis = 0;
        foreach (var t in targets)
        {
            if (t.renderer == null) continue;
            bool seen = t.mode == 1 || (t.mode == 2 && t.rayVisible);
            if (seen) t.lastSeenTime = Time.time;

            bool shouldShow = seen || (Time.time - t.lastSeenTime) < hideDelay;
            if (shouldShow != t.shown)
            {
                t.shown = shouldShow;
                t.renderer.enabled = shouldShow;
            }
            if (t.shown) vis++;
        }
        currentlyVisible = vis;

        if (logCounts && Time.time >= nextLog)
        {
            nextLog = Time.time + 1f;
            Debug.Log($"[VisibilityCuller] visible {vis} / {targets.Count}  (view distance {viewDistance:0} m)");
        }
    }

    void RunRayTests(List<Target> list, ref int cursor, ref int raysLeft, int budget, Vector3 camPos)
    {
        if (list.Count == 0) return;
        int start = cursor % list.Count;
        int spent = 0;
        for (int k = 0; k < list.Count && spent < budget && raysLeft > 0; k++)
        {
            int idx = (start + k) % list.Count;
            int used = 0;
            list[idx].rayVisible = HasClearView(list[idx].renderer, camPos, ref used);
            spent += used;
            raysLeft -= used;
            cursor = idx + 1;
        }
    }

    // 0 = hide, 1 = show, 2 = in range and in view: needs the wall test
    int CheapTest(Renderer r, Vector3 camPos)
    {
        Bounds b = r.bounds;

        float dist = Vector3.Distance(camPos, b.ClosestPoint(camPos));
        if (dist <= alwaysVisibleDistance) return 1;
        if (dist > viewDistance) return 0;

        Bounds padded = b;
        padded.Expand(frustumPadding * 2f);
        if (!GeometryUtility.TestPlanesAABB(planes, padded)) return 0;

        if (!useOcclusionRaycasts) return 1;
        if (Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)) > skipOcclusionAboveSize) return 1;
        return 2;
    }

    bool HasClearView(Renderer r, Vector3 camPos, ref int raysUsed)
    {
        Bounds b = r.bounds;
        Vector3 e = b.extents * 0.9f;
        int n = Mathf.Clamp(samplesPerObject, 1, SampleOffsets.Length);
        if (b.extents.sqrMagnitude < 0.01f) n = Mathf.Min(n, 2); // tiny object: more points would be the same spot

        for (int i = 0; i < n; i++)
        {
            Vector3 p = b.center + Vector3.Scale(SampleOffsets[i], e);
            raysUsed++;
            if (HasLineOfSight(camPos, p, r)) return true;
        }
        return false;
    }

    bool HasLineOfSight(Vector3 from, Vector3 to, Renderer target)
    {
        Vector3 dir = to - from;
        float len = dir.magnitude;
        if (len < 0.01f) return true;
        dir /= len;

        // The ray stops a little short of the point, so floors / terrain the point sits on don't count.
        float maxDist = Mathf.Max(0f, len - occlusionTolerance);
        int n = Physics.RaycastNonAlloc(from, dir, hitBuffer, maxDist, occluderMask, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < n; i++)
        {
            Collider col = hitBuffer[i].collider;
            Transform ht = col.transform;
            if (ht.IsChildOf(target.transform) || target.transform.IsChildOf(ht)) continue; // its own collider
            if (IsIgnored(ht)) continue;                                                   // e.g. the player
            if (!BlocksSight(col)) continue;                                               // invisible collider
            return false;                                                                   // something solid in the way
        }
        return true;
    }

    bool BlocksSight(Collider col)
    {
        if (!onlyRenderedCollidersBlock) return true;
        if (blockerCache.TryGetValue(col, out bool cached)) return cached;

        bool blocks = col is TerrainCollider;
        if (!blocks)
        {
            var r = col.GetComponentInParent<Renderer>();
            blocks = r != null && !(r is ParticleSystemRenderer) && !(r is TrailRenderer) && !(r is LineRenderer);
        }
        blockerCache[col] = blocks;
        return blocks;
    }

    bool IsIgnored(Transform t)
    {
        if (ignoreRoots == null) return false;
        foreach (var root in ignoreRoots)
            if (root != null && t.IsChildOf(root)) return true;
        return false;
    }
}
