using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Pick up and place objects on a grid. Add to the player (needs PlayerInventory too).
///  E (look at a PlaceableObject) = pick it up into your inventory
///  F = start placing the carried object (F again or right click = cancel)
///  Mouse = aim, the object snaps to the grid. Green = ok, red = blocked.
///  Q = rotate 90 degrees, mouse wheel = switch between carried objects
///  Left click = place it
/// If the object is aimed near a matching ShadeObject spot it snaps exactly onto that spot.
/// </summary>
public class GridPlacer : MonoBehaviour
{
    public static event Action<PlaceableObject> OnPlaced;
    public static event Action<PlaceableObject> OnRemoved;

    [Header("References")]
    public Camera playerCamera;

    [Header("Grid")]
    public float cellSize = 1f;
    [Tooltip("World position the grid lines up with.")]
    public Vector3 gridOrigin = Vector3.zero;

    [Header("Reach")]
    public float pickUpRange = 4f;
    public float placeRange = 6f;
    [Tooltip("What objects can be placed on.")]
    public LayerMask surfaceMask = ~0;
    [Tooltip("What blocks placement (walls, other objects).")]
    public LayerMask blockMask = ~0;
    [Range(0f, 1f)] public float minSurfaceUp = 0.7f;

    [Header("Keys")]
    public Key pickUpKey = Key.E;
    public Key placeKey = Key.F;
    public Key rotateKey = Key.Q;

    [Header("Look")]
    public Color validColor = new Color(0.4f, 1f, 0.4f, 1f);
    public Color invalidColor = new Color(1f, 0.35f, 0.35f, 1f);

    private readonly List<PlaceableObject> carried = new List<PlaceableObject>();
    private PlaceableObject current;
    private bool placing;
    private bool canPlace;
    private bool showingPrompt;
    private float yaw;

    void Start()
    {
        if (playerCamera == null) playerCamera = PlayerFinder.FindCamera(transform);
    }

    void Update()
    {
        if (playerCamera == null || Keyboard.current == null) return;
        if (DialogUI.IsOpen) { if (placing) CancelPlacing(); return; }

        if (placing) UpdatePlacing();
        else UpdateIdle();
    }

    // ---------- idle: look for things to pick up ----------
    void UpdateIdle()
    {
        PlaceableObject target = null;
        if (Physics.Raycast(playerCamera.transform.position, playerCamera.transform.forward,
                out RaycastHit hit, pickUpRange, ~0, QueryTriggerInteraction.Ignore))
            target = hit.collider.GetComponentInParent<PlaceableObject>();

        if (target != null)
        {
            DialogUI.Instance.SetPrompt($"Press {pickUpKey} to pick up {target.displayName}");
            showingPrompt = true;
            if (Keyboard.current[pickUpKey].wasPressedThisFrame) PickUp(target);
        }
        else if (showingPrompt)
        {
            DialogUI.Instance.SetPrompt(null);
            showingPrompt = false;
        }

        if (Keyboard.current[placeKey].wasPressedThisFrame && carried.Count > 0) BeginPlacing();
    }

    void PickUp(PlaceableObject p)
    {
        if (p.IsPlaced) OnRemoved?.Invoke(p);
        p.IsPlaced = false;
        p.IsCarried = true;
        p.gameObject.SetActive(false);
        carried.Add(p);

        PlayerInventory.Instance?.Add(p.itemId);
        DialogUI.Instance.SetPrompt(null);
        showingPrompt = false;
        DialogUI.Instance.ShowNotice($"Picked up: {p.displayName}. Press {placeKey} to place it.");
        p.onPickedUp?.Invoke();
    }

    // ---------- placing ----------
    void BeginPlacing()
    {
        current = carried[carried.Count - 1];
        current.gameObject.SetActive(true);
        current.SetCollidersEnabled(false);
        yaw = current.transform.eulerAngles.y;
        placing = true;
        DialogUI.Instance.SetPrompt($"Left click = place   {rotateKey} = rotate   {placeKey}/right click = cancel");
    }

    void CancelPlacing()
    {
        if (current != null)
        {
            current.Tint(null);
            current.SetCollidersEnabled(true);
            current.gameObject.SetActive(false);
        }
        EndPlacing();
    }

    void EndPlacing()
    {
        placing = false;
        current = null;
        DialogUI.Instance.SetPrompt(null);
    }

    void UpdatePlacing()
    {
        if (Keyboard.current[placeKey].wasPressedThisFrame ||
            (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame))
        {
            CancelPlacing();
            return;
        }

        // switch carried object with the mouse wheel
        if (carried.Count > 1 && Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) > 0.1f)
            {
                current.Tint(null);
                current.gameObject.SetActive(false);
                int i = carried.IndexOf(current);
                i = (i + (scroll > 0 ? 1 : -1) + carried.Count) % carried.Count;
                current = carried[i];
                current.gameObject.SetActive(true);
                current.SetCollidersEnabled(false);
                yaw = current.transform.eulerAngles.y;
            }
        }

        if (Keyboard.current[rotateKey].wasPressedThisFrame) yaw += 90f;

        Vector3 origin = playerCamera.transform.position;
        Vector3 dir = playerCamera.transform.forward;
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);

        if (Physics.Raycast(origin, dir, out RaycastHit hit, placeRange, surfaceMask, QueryTriggerInteraction.Ignore)
            && hit.normal.y >= minSurfaceUp)
        {
            Vector3 p = SnapToGrid(hit.point);

            // magnet onto a matching target spot
            ShadeObject spot = ShadeObject.FindNear(current.itemId, hit.point, Mathf.Max(cellSize, 0.5f));
            if (spot != null) { p.x = spot.transform.position.x; p.z = spot.transform.position.z; }

            current.transform.SetPositionAndRotation(new Vector3(p.x, hit.point.y, p.z), rot);
            Bounds b = current.GetBounds();
            current.transform.position += Vector3.up * (hit.point.y - b.min.y); // bottom rests on the surface
            b = current.GetBounds();

            Vector3 center = b.center + Vector3.up * 0.05f;
            Vector3 half = new Vector3(b.extents.x * 0.95f, Mathf.Max(0.01f, b.extents.y * 0.95f - 0.05f), b.extents.z * 0.95f);
            canPlace = !Physics.CheckBox(center, half, Quaternion.identity, blockMask, QueryTriggerInteraction.Ignore);
        }
        else
        {
            // nothing valid to aim at: float at the end of the ray, can't place
            current.transform.SetPositionAndRotation(origin + dir * placeRange, rot);
            canPlace = false;
        }

        current.Tint(canPlace ? validColor : invalidColor);

        if (canPlace && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            Place();
    }

    Vector3 SnapToGrid(Vector3 world)
    {
        if (cellSize <= 0.001f) return world;
        Vector3 rel = world - gridOrigin;
        rel.x = Mathf.Round(rel.x / cellSize) * cellSize;
        rel.z = Mathf.Round(rel.z / cellSize) * cellSize;
        return gridOrigin + new Vector3(rel.x, 0f, rel.z) + Vector3.up * world.y;
    }

    void Place()
    {
        var p = current;
        p.Tint(null);
        p.SetCollidersEnabled(true);
        p.IsCarried = false;
        p.IsPlaced = true;
        carried.Remove(p);
        PlayerInventory.Instance?.Remove(p.itemId);
        EndPlacing();

        p.onPlaced?.Invoke();
        OnPlaced?.Invoke(p);
    }

    void OnDisable()
    {
        if (placing) CancelPlacing();
    }
}
