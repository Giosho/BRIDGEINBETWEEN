using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A floor plate that presses when enough weight is on it.
/// The player counts as 'playerWeight'. Physics objects count as their Rigidbody mass.
/// Needs a trigger collider on this object (added automatically when you add the script).
/// </summary>
[DisallowMultipleComponent]
public class PressurePlate : MonoBehaviour
{
    public enum PlateMode
    {
        Momentary, // pressed only while enough weight is on it
        Latching   // stays pressed once triggered, until ResetPlate() is called
    }

    [Header("Weight")]
    [Tooltip("Total weight needed to press the plate.")]
    [SerializeField] private float requiredWeight = 1f;
    [Tooltip("Tick for weight-check puzzles: the plate releases if there is TOO MUCH weight on it.")]
    [SerializeField] private bool limitMaxWeight = false;
    [SerializeField] private float maxWeight = 10f;
    [Tooltip("How much the player weighs. Physics objects use their Rigidbody mass.")]
    [SerializeField] private float playerWeight = 1f;
    [Tooltip("Held objects are kinematic. Leave this off so a crate only counts once you put it down.")]
    [SerializeField] private bool countKinematicBodies = false;
    [Tooltip("Used to recognise the player (a CharacterController is also recognised automatically).")]
    [SerializeField] private string playerTag = "Player";

    [Header("Behaviour")]
    [SerializeField] private PlateMode mode = PlateMode.Momentary;
    [Tooltip("Seconds the weight must stay missing before a Momentary plate releases. Stops flicker when a crate bounces.")]
    [SerializeField] private float releaseDelay = 0.25f;
    [Tooltip("Puts this object on the Ignore Raycast layer so the pickup ray and laser are not blocked by the trigger volume.")]
    [SerializeField] private bool ignoreRaycasts = true;

    [Header("Visuals (all optional)")]
    [Tooltip("The mesh that sinks down when pressed.")]
    [SerializeField] private Transform plateVisual;
    [SerializeField] private float pressDepth = 0.05f;
    [SerializeField] private float pressSpeed = 0.5f;
    [Tooltip("Renderer that changes colour between idle and pressed.")]
    [SerializeField] private Renderer indicatorRenderer;
    [SerializeField] private Color idleColor = new Color(0.8f, 0.2f, 0.2f);
    [SerializeField] private Color pressedColor = new Color(0.2f, 0.8f, 0.3f);

    [Header("Audio (optional)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pressSound;
    [SerializeField] private AudioClip releaseSound;

    [Header("Events")]
    public UnityEvent onPressed;
    public UnityEvent onReleased;

    [Header("Debug")]
    [SerializeField] private bool logWeight = false;

    /// <summary>C# events used by PlatePuzzle.</summary>
    public event System.Action<PressurePlate> Pressed;
    public event System.Action<PressurePlate> Released;

    public bool IsPressed { get; private set; }
    public float CurrentWeight { get; private set; }

    private readonly HashSet<Collider> inside = new HashSet<Collider>();
    private readonly HashSet<GameObject> countedOwners = new HashSet<GameObject>();
    private readonly List<Collider> stale = new List<Collider>();

    private Vector3 restLocalPosition;
    private Material indicatorMaterial;
    private float releaseTimer;
    private bool needsRelease; // after ResetPlate, weight must leave once before it can press again

    private void Reset()
    {
        if (!TryGetComponent(out Collider col))
            col = gameObject.AddComponent<BoxCollider>();
        col.isTrigger = true;
    }

    private void Awake()
    {
        if (plateVisual != null)
            restLocalPosition = plateVisual.localPosition;

        if (indicatorRenderer != null)
        {
            indicatorMaterial = indicatorRenderer.material; // instance, so other plates are not recoloured
            indicatorMaterial.color = idleColor;
        }

        if (ignoreRaycasts)
        {
            int layer = LayerMask.NameToLayer("Ignore Raycast");
            if (layer >= 0) gameObject.layer = layer;
        }

        bool hasTrigger = false;
        foreach (Collider c in GetComponents<Collider>())
            if (c.isTrigger) hasTrigger = true;
        if (!hasTrigger)
            Debug.LogWarning($"[PressurePlate] '{name}' has no trigger collider. Tick 'Is Trigger' on its collider.", this);
    }

    private void OnDisable()
    {
        inside.Clear();
    }

    private void OnDestroy()
    {
        if (indicatorMaterial != null) Destroy(indicatorMaterial);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other != null) inside.Add(other);
    }

    private void OnTriggerExit(Collider other)
    {
        inside.Remove(other);
    }

    private void FixedUpdate()
    {
        float weight = CalculateWeight();
        if (!Mathf.Approximately(weight, CurrentWeight))
        {
            CurrentWeight = weight;
            if (logWeight) Debug.Log($"[PressurePlate] '{name}' weight = {weight}", this);
        }

        bool satisfied = weight > 0f
                         && weight >= requiredWeight - 0.0001f
                         && (!limitMaxWeight || weight <= maxWeight + 0.0001f);

        if (!satisfied) needsRelease = false;

        if (satisfied)
        {
            releaseTimer = 0f;
            if (!IsPressed && !needsRelease) SetPressed(true);
        }
        else if (IsPressed && mode == PlateMode.Momentary)
        {
            releaseTimer += Time.fixedDeltaTime;
            if (releaseTimer >= releaseDelay) SetPressed(false);
        }
    }

    private void Update()
    {
        if (plateVisual == null) return;
        Vector3 target = restLocalPosition + Vector3.down * (IsPressed ? pressDepth : 0f);
        plateVisual.localPosition = Vector3.MoveTowards(plateVisual.localPosition, target, pressSpeed * Time.deltaTime);
    }

    /// <summary>Releases the plate. If weight is still on it, it will not press again until the weight leaves once.</summary>
    public void ResetPlate()
    {
        needsRelease = true;
        releaseTimer = 0f;
        if (IsPressed) SetPressed(false);
    }

    private float CalculateWeight()
    {
        countedOwners.Clear();
        stale.Clear();
        float total = 0f;

        foreach (Collider col in inside)
        {
            // Destroyed or disabled objects do not always send OnTriggerExit, so clean them up here
            if (col == null || !col.enabled || !col.gameObject.activeInHierarchy)
            {
                stale.Add(col);
                continue;
            }

            // Several colliders on one object must only count once
            if (TryGetWeight(col, out GameObject owner, out float w) && countedOwners.Add(owner))
                total += w;
        }

        foreach (Collider s in stale) inside.Remove(s);
        return total;
    }

    private bool TryGetWeight(Collider col, out GameObject owner, out float weight)
    {
        // Player: CharacterController (on this object or a parent) or the Player tag
        CharacterController cc = col.GetComponentInParent<CharacterController>();
        if (cc != null)
        {
            owner = cc.gameObject;
            weight = playerWeight;
            return true;
        }

        Rigidbody rb = col.attachedRigidbody;
        Transform root = rb != null ? rb.transform : col.transform;

        if (root.CompareTag(playerTag))
        {
            owner = root.gameObject;
            weight = playerWeight;
            return true;
        }

        // Physics objects: weight = mass. Kinematic bodies (e.g. held objects) are ignored by default.
        if (rb != null && (!rb.isKinematic || countKinematicBodies))
        {
            owner = rb.gameObject;
            weight = rb.mass;
            return true;
        }

        owner = null;
        weight = 0f;
        return false;
    }

    private void SetPressed(bool pressed)
    {
        if (IsPressed == pressed) return;
        IsPressed = pressed;

        if (indicatorMaterial != null)
            indicatorMaterial.color = pressed ? pressedColor : idleColor;

        if (audioSource != null)
        {
            AudioClip clip = pressed ? pressSound : releaseSound;
            if (clip != null) audioSource.PlayOneShot(clip);
        }

        if (pressed)
        {
            onPressed.Invoke();
            Pressed?.Invoke(this);
        }
        else
        {
            onReleased.Invoke();
            Released?.Invoke(this);
        }
    }
}
