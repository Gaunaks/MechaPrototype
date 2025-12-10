using UnityEngine;

public class FirstPersonLook : MonoBehaviour
{
    [SerializeField] Transform character;
    public float sensitivity = 2f;
    public float smoothing = 1.5f;

    Vector2 velocity;
    Vector2 frameVelocity;

    FirstPersonMovement movement;

    void Reset()
    {
        character = GetComponentInParent<FirstPersonMovement>().transform;
    }

    void Awake()
    {
        movement = GetComponentInParent<FirstPersonMovement>();
    }

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Mouse input
        Vector2 mouseDelta = new Vector2(
            Input.GetAxisRaw("Mouse X"),
            Input.GetAxisRaw("Mouse Y")
        );

        Vector2 rawFrameVelocity = mouseDelta * sensitivity;
        frameVelocity = Vector2.Lerp(frameVelocity, rawFrameVelocity, 1f / smoothing);
        velocity += frameVelocity;
        velocity.y = Mathf.Clamp(velocity.y, -90, 90);

        // Camera pitch always happens:
        Quaternion pitchRot = Quaternion.AngleAxis(-velocity.y, Vector3.right);
        transform.localRotation = pitchRot;

        if (!movement.isFlyingExternally)
        {
            // NORMAL FPS YAW (GROUND MODE)
            character.localRotation = Quaternion.AngleAxis(velocity.x, Vector3.up);
        }
        else
        {
            // FLIGHT MODE — DO NOT ROTATE THE BODY HERE
            // The flight script controls rotation (6DOF).
            // Camera should follow mech roll automatically.
            transform.localRotation = character.rotation * pitchRot;
        }
    }
}
