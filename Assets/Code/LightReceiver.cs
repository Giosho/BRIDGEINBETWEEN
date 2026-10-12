using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// The target the light must fall on. Fires UnityEvents when it becomes lit / unlit,
/// so you can hook it to doors, traps, platforms, etc. in the Inspector.
/// </summary>
public class LightReceiver : MonoBehaviour
{
    [Header("Behaviour")]
    [Tooltip("Seconds the beam must keep hitting before activating (prevents flickering).")]
    [SerializeField] private float activationDelay = 0.25f;
    [Tooltip("Seconds without light before deactivating.")]
    [SerializeField] private float deactivationDelay = 0.15f;
    [Tooltip("If true, stays active forever once triggered.")]
    [SerializeField] private bool latch = false;

    [Header("Visual feedback (optional)")]
    [SerializeField] private Renderer glowRenderer;
    [ColorUsage(false, true)]
    [SerializeField] private Color glowColor = new Color(1f, 0.75f, 0.2f) * 2f;

    [Header("Events")]
    public UnityEvent onActivated;
    public UnityEvent onDeactivated;

    public bool IsActive { get; private set; }

    private bool hitThisFrame;
    private float litTime;
    private float unlitTime;
    private Material glowMaterial;

    private void Awake()
    {
        if (glowRenderer != null)
        {
            glowMaterial = glowRenderer.material; // instance, so we don't edit the shared asset
            glowMaterial.EnableKeyword("_EMISSION");
        }
        SetGlow(false);
    }

    /// <summary>Called by LightEmitter every frame the beam touches this receiver.</summary>
    public void RegisterHit()
    {
        hitThisFrame = true;
    }

    // Emitters run in Update, receivers evaluate in LateUpdate, so order is always correct
    // and several emitters can feed the same receiver.
    private void LateUpdate()
    {
        if (hitThisFrame)
        {
            litTime += Time.deltaTime;
            unlitTime = 0f;
        }
        else
        {
            unlitTime += Time.deltaTime;
            litTime = 0f;
        }
        hitThisFrame = false;

        if (!IsActive && litTime >= activationDelay)
        {
            SetActive(true);
        }
        else if (IsActive && !latch && unlitTime >= deactivationDelay)
        {
            SetActive(false);
        }
    }

    private void SetActive(bool value)
    {
        IsActive = value;
        SetGlow(value);

        if (value) onActivated?.Invoke();
        else onDeactivated?.Invoke();
    }

    private void SetGlow(bool on)
    {
        if (glowMaterial == null) return;
        glowMaterial.SetColor("_EmissionColor", on ? glowColor : Color.black);
    }
}
