using UnityEngine;
using UnityEngine.InputSystem;

// Runs before MouseLook3D so the camera lock is up to date in the same frame
[DefaultExecutionOrder(-50)]
public class PickUpScript : MonoBehaviour
{
    /// <summary>True while the player is holding R with an object in hand. MouseLook3D reads this.</summary>
    public static bool IsRotatingObject { get; private set; }

    [Header("Input System Profiles")]
    public InputActionAsset inputActions;

    [Header("References")]
    public Camera playerCamera;
    public Transform holdPos;

    [Header("Settings")]
    public float pickUpRange = 5f;
    public float throwForce = 600f;
    public float carrySpeed = 20f;

    [Header("Rotation")]
    [Tooltip("Degrees of rotation per pixel of mouse movement.")]
    public float rotationSpeed = 0.3f;
    [Tooltip("Tick this to only allow left/right spinning (good for mirrors).")]
    public bool yawOnly = false;

    private GameObject heldObj;
    private Rigidbody heldObjRb;
    private int holdLayer;
    private int originalLayer;
    private Quaternion heldRotation;

    private InputAction interactAction;
    private InputAction throwAction;
    private InputAction rotateAction; // optional: "Rotate" action in the Gameplay map

    void Awake()
    {
        if (inputActions != null)
        {
            var gameplayMap = inputActions.FindActionMap("Gameplay");
            if (gameplayMap != null)
            {
                interactAction = gameplayMap.FindAction("Interact");
                throwAction = gameplayMap.FindAction("Throw");
                rotateAction = gameplayMap.FindAction("Rotate"); // null is fine, R key is the fallback
            }
        }
    }

    void OnEnable()
    {
        interactAction?.Enable();
        throwAction?.Enable();
        rotateAction?.Enable();
    }

    void OnDisable()
    {
        interactAction?.Disable();
        throwAction?.Disable();
        rotateAction?.Disable();
        IsRotatingObject = false;
    }

    void Start()
    {
        holdLayer = LayerMask.NameToLayer("holdLayer");
        if (holdLayer == -1) holdLayer = 0;
    }

    void Update()
    {
        if (interactAction != null && interactAction.triggered)
        {
            if (heldObj == null)
            {
                TryPickUpObject();
            }
            else
            {
                DropObject();
            }
        }

        if (throwAction != null && throwAction.triggered && heldObj != null)
        {
            ThrowObject();
        }

        HandleRotation();
    }

    void FixedUpdate()
    {
        if (heldObj != null && heldObjRb != null)
        {
            MoveAndLockObject();
        }
    }

    bool IsRotateKeyHeld()
    {
        if (rotateAction != null) return rotateAction.IsPressed();
        return Keyboard.current != null && Keyboard.current.rKey.isPressed;
    }

    void HandleRotation()
    {
        IsRotatingObject = heldObj != null && IsRotateKeyHeld();
        if (!IsRotatingObject || Mouse.current == null) return;

        Vector2 delta = Mouse.current.delta.ReadValue() * rotationSpeed;

        // Yaw around the camera's up axis: mouse right turns the object's near side right
        Quaternion yaw = Quaternion.AngleAxis(-delta.x, playerCamera.transform.up);
        heldRotation = yaw * heldRotation;

        if (!yawOnly)
        {
            // Pitch around the camera's right axis: mouse up tilts the object's near side up
            Quaternion pitch = Quaternion.AngleAxis(delta.y, playerCamera.transform.right);
            heldRotation = pitch * heldRotation;
        }
    }

    void TryPickUpObject()
    {
        RaycastHit hit;
        Vector3 origin = playerCamera.transform.position + playerCamera.transform.forward * 0.2f;

        if (Physics.Raycast(origin, playerCamera.transform.forward, out hit, pickUpRange))
        {
            if (hit.transform.CompareTag("canPickUp"))
            {
                PickUpObject(hit.transform.gameObject);
            }
        }
    }

    void PickUpObject(GameObject pickUpObj)
    {
        if (pickUpObj.TryGetComponent(out Rigidbody rb))
        {
            heldObj = pickUpObj;
            heldObjRb = rb;

            heldRotation = heldObj.transform.rotation;
            originalLayer = heldObj.layer;

            heldObjRb.isKinematic = true;
            heldObjRb.useGravity = false;

            heldObj.layer = holdLayer;

            Collider playerCol = GetComponent<Collider>();
            if (playerCol && heldObj.GetComponent<Collider>())
            {
                Physics.IgnoreCollision(heldObj.GetComponent<Collider>(), playerCol, true);
            }
        }
    }

    void DropObject()
    {
        if (!heldObj) return;

        heldObjRb.isKinematic = false;
        heldObjRb.useGravity = true;

        heldObjRb.linearVelocity = Vector3.zero;
        heldObjRb.angularVelocity = Vector3.zero;

        Collider playerCol = GetComponent<Collider>();
        if (playerCol && heldObj.GetComponent<Collider>())
        {
            Physics.IgnoreCollision(heldObj.GetComponent<Collider>(), playerCol, false);
        }

        heldObj.layer = originalLayer;
        heldObj = null;
        IsRotatingObject = false;
    }

    void ThrowObject()
    {
        if (!heldObj) return;

        heldObjRb.isKinematic = false;
        heldObjRb.useGravity = true;

        heldObjRb.linearVelocity = Vector3.zero;
        heldObjRb.angularVelocity = Vector3.zero;

        Collider playerCol = GetComponent<Collider>();
        if (playerCol && heldObj.GetComponent<Collider>())
        {
            Physics.IgnoreCollision(heldObj.GetComponent<Collider>(), playerCol, false);
        }

        heldObj.layer = originalLayer;
        heldObjRb.AddForce(playerCamera.transform.forward * throwForce);
        heldObj = null;
        IsRotatingObject = false;
    }

    void MoveAndLockObject()
    {
        Vector3 targetPosition = Vector3.Lerp(
            heldObjRb.position,
            holdPos.position,
            Time.fixedDeltaTime * carrySpeed
        );

        heldObjRb.MovePosition(targetPosition);
        // Must go through the Rigidbody. Writing transform.rotation directly makes Unity
        // re-sync the body and cancels the MovePosition above whenever the rotation changes.
        heldObjRb.MoveRotation(heldRotation);
    }
}