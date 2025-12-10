using System.Collections.Generic;
using UnityEngine;

public class FirstPersonMovement : MonoBehaviour
{
    public float speed = 5;

    [Header("Running")]
    public bool canRun = true;
    public bool IsRunning { get; private set; }
    public float runSpeed = 9;
    public KeyCode runningKey = KeyCode.LeftShift;

    Rigidbody rb;
    public List<System.Func<float>> speedOverrides = new List<System.Func<float>>();

    [HideInInspector] public bool isFlyingExternally = false; // <-- ADDED

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void FixedUpdate()
    {
        // 🚫 If the flight script controls movement => DO NOT TOUCH VELOCITY
        if (isFlyingExternally) return;

        // Normal on-ground FPS movement:
        IsRunning = canRun && Input.GetKey(runningKey);

        float targetSpeed = IsRunning ? runSpeed : speed;
        if (speedOverrides.Count > 0)
            targetSpeed = speedOverrides[speedOverrides.Count - 1]();

        Vector2 input = new Vector2(
            Input.GetAxis("Horizontal") * targetSpeed,
            Input.GetAxis("Vertical") * targetSpeed
        );

        Vector3 velocity =
            transform.rotation * new Vector3(input.x, rb.linearVelocity.y, input.y);

        rb.linearVelocity = velocity;
    }
}
