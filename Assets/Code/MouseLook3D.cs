using UnityEngine;
using UnityEngine.InputSystem;

public class MouseLook3D : MonoBehaviour
{
    [Header("References")]
    public Transform playerBody;

    [Header("Settings")]
    [Tooltip("Degrees per pixel of mouse movement, left/right. Higher = faster turning.")]
    public float yawSensitivity = 0.3f;
    [Tooltip("Degrees per pixel of mouse movement, up/down.")]
    public float pitchSensitivity = 0.2f;
    public float maxLookAngle = 89f;

    private float yaw;
    private float pitch;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        yaw = playerBody.eulerAngles.y;

        Rigidbody rb = playerBody.GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Only this script controls rotation
            rb.freezeRotation = true;

            // Interpolation smooths rotation over several frames, which feels like turning delay.
            // Turn it off so the view follows the mouse instantly.
            rb.interpolation = RigidbodyInterpolation.None;
        }
    }

    /// <summary>Turn the player to face a direction (used by Teleporter). The look script owns the rotation.</summary>
    public void SetYaw(float degrees)
    {
        yaw = degrees;
        if (playerBody != null) playerBody.rotation = Quaternion.Euler(0f, yaw, 0f);
    }

    void Update()
    {
        // Camera is frozen while rotating a held object (holding R)
        if (PickUpScript.IsRotatingObject) return;
        // Camera is frozen while an NPC dialog is open
        if (DialogUI.IsOpen) return;
        if (Mouse.current == null) return;
        if (Cursor.lockState != CursorLockMode.Locked) return;

        // Raw mouse movement this frame: no smoothing, no damping, no delay
        Vector2 delta = Mouse.current.delta.ReadValue();

        yaw += delta.x * yawSensitivity;
        pitch = Mathf.Clamp(pitch - delta.y * pitchSensitivity, -maxLookAngle, maxLookAngle);

        // Applied immediately in the same frame
        playerBody.rotation = Quaternion.Euler(0f, yaw, 0f);
        transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}