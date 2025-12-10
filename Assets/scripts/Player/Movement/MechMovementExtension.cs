using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MechMovementExtensions : MonoBehaviour
{
    Rigidbody rb;
    FirstPersonMovement movement;
    EnergySystem energy;
    Camera cam;
    GroundCheck ground;

    [Header("Dash")]
    public KeyCode dashKey = KeyCode.LeftShift;
    public float dashForce = 40f;
    public float dashEnergyCostPerSecond = 30f;
    public float dashDrag = 0.5f; // temporary drag during dash
    bool isDashing = false;

    [Header("Flight (Sidonia-style rocket)")]
    [Tooltip("Max horizontal flight speed (m/s)")]
    public float maxFlightSpeed = 90f;
    [Tooltip("Base 'thrust' acceleration in m/s^2 (spool multiplies this)")]
    public float baseFlightAcceleration = 80f;
    [Tooltip("How quickly the spool multiplier grows (higher = faster spool)")]
    public float accelerationRampPerSecond = 35f;
    [Tooltip("Top multiplier applied to base acceleration after spool-up")]
    public float maxAccelerationMultiplier = 4f;
    [Tooltip("Reduces vertical influence of camera direction (0..1)")]
    public float verticalAccelFactor = 0.75f;
    public float flightEnergyCostPerSecond = 40f;
    public float upwardLookThreshold = 0.15f;

    [Header("Micro-thrusters / caps")]
    [Tooltip("Maximum lateral (micro-thruster) accel m/s^2 for quick course correction")]
    public float microThrusterAccel = 250f;
    [Tooltip("Maximum allowed change in horizontal speed per second (soft cap)")]
    public float horizontalAccelCap = 120f;
    [Tooltip("Maximum vertical accel m/s^2")]
    public float verticalAccelCap = 80f;
    [Tooltip("Maximum vertical speed m/s")]
    public float maxVerticalSpeed = 40f;

    [Header("Steering & alignment")]
    [Tooltip("How strongly mech aligns its facing/yaw to the camera while flying (deg/sec)")]
    public float yawAlignSpeed = 720f;
    [Tooltip("How quickly thrustDirection rotates toward desired thrust (higher = less drift)")]
    public float thrustDirectionCatchupSpeed = 3.5f;
    [Tooltip("Quick lateral steering factor from player inputs (adds micro-thruster influence)")]
    public float directionalInfluence = 28f;

    [Header("Flight Smoothing")]
    public float camSmoothing = 10f;
    public float pitchDeadzone = 0.08f;

    [Header("Power boost presets (optional)")]
    [Tooltip("Max forward thrust (m/s^2) — limits the main thrust magnitude")]
    public float maxForwardThrust = 350f;
    [Tooltip("Max lateral thrust (m/s^2) — micro thruster cap")]
    public float maxSideThrust = 150f;
    [Tooltip("Max upward thrust (m/s^2)")]
    public float maxUpThrust = 150f;
    [Tooltip("Controls how quickly inertia is damped (higher = faster damping)")]
    public float inertiaDampSpeed = 4f;
    [Tooltip("How snappy thrust direction catches camera (higher = less drift)")]
    public float directionSnappiness = 9f;
    [Tooltip("Absolute safety cap for total speed (m/s)")]
    public float absoluteMaxSpeed = 150f;

    [Header("Tuner")]
    public bool autoTuneFromSlider = true;
    [Range(0.5f, 4f)]
    public float flightPowerSlider = 1f;

    [SerializeField, HideInInspector] private float refMaxFlightSpeed = 90f;
    [SerializeField, HideInInspector] private float refBaseFlightAcceleration = 80f;
    [SerializeField, HideInInspector] private float refHorizontalAccelCap = 120f;
    [SerializeField, HideInInspector] private float refVerticalAccelCap = 80f;
    [SerializeField, HideInInspector] private float refMaxVerticalSpeed = 40f;
    [SerializeField, HideInInspector] private float refDirectionalInfluence = 28f;

    bool isFlying = false;
    float currentAccelMultiplier = 1f; // spool-up (1..maxAccelerationMultiplier)
    Vector3 smoothedCamDir;
    Vector3 thrustDirection; // persistent thrust vector (world-space)

    float lastFlightPowerSlider = -1f;

    [Header("Hover (Space in mid-air)")]
    public float hoverEnergyCostPerSecond = 10f;
    public float hoverDrag = 3f;
    bool isHovering = false;
    bool spaceReleasedSinceGrounded = false;

    [Header("Slide / General")]
    public float slideDrag = 0.02f;
    public float slideLerp = 5f;

    float originalDrag;
    bool gravityWasEnabled = true;

    [Header("Visual Roll")]
    public Transform visualsTransform = null; // recommended: child model root
    public float maxRollAngle = 35f;
    public float rollSpeed = 8f;
    float currentRoll = 0f;
    float targetRoll = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        movement = GetComponent<FirstPersonMovement>();
        energy = GetComponent<EnergySystem>();
        cam = GetComponentInChildren<Camera>();
        ground = GetComponentInChildren<GroundCheck>();

        originalDrag = rb.linearDamping;
        gravityWasEnabled = rb.useGravity;

        if (cam != null)
            smoothedCamDir = cam.transform.forward;
        else
            smoothedCamDir = transform.forward;

        thrustDirection = transform.forward;

        // init ref values if zero
        refMaxFlightSpeed = Mathf.Approximately(refMaxFlightSpeed, 0f) ? maxFlightSpeed : refMaxFlightSpeed;
        refBaseFlightAcceleration = Mathf.Approximately(refBaseFlightAcceleration, 0f) ? baseFlightAcceleration : refBaseFlightAcceleration;
        refHorizontalAccelCap = Mathf.Approximately(refHorizontalAccelCap, 0f) ? horizontalAccelCap : refHorizontalAccelCap;
        refVerticalAccelCap = Mathf.Approximately(refVerticalAccelCap, 0f) ? verticalAccelCap : refVerticalAccelCap;
        refMaxVerticalSpeed = Mathf.Approximately(refMaxVerticalSpeed, 0f) ? maxVerticalSpeed : refMaxVerticalSpeed;
        refDirectionalInfluence = Mathf.Approximately(refDirectionalInfluence, 0f) ? directionalInfluence : refDirectionalInfluence;

        thrustDirectionCatchupSpeed = Mathf.Max(thrustDirectionCatchupSpeed, directionSnappiness);

        if (autoTuneFromSlider)
            ApplyFlightTuner();
        lastFlightPowerSlider = flightPowerSlider;
    }

    void Update()
    {
        if (autoTuneFromSlider && !Mathf.Approximately(lastFlightPowerSlider, flightPowerSlider))
        {
            ApplyFlightTuner();
            lastFlightPowerSlider = flightPowerSlider;
        }

        HandleDash();
        HandleFlight();
        HandleHover();
        RestoreStatesIfNeeded();

        if (ground != null && !ground.isGrounded)
        {
            if (!Input.GetKey(KeyCode.Space))
                spaceReleasedSinceGrounded = true;
        }
        else
        {
            spaceReleasedSinceGrounded = false;
        }

        UpdateVisualRoll();
    }

    void OnValidate()
    {
        if (refMaxFlightSpeed <= 0f) refMaxFlightSpeed = maxFlightSpeed;
        if (refBaseFlightAcceleration <= 0f) refBaseFlightAcceleration = baseFlightAcceleration;
        if (refHorizontalAccelCap <= 0f) refHorizontalAccelCap = horizontalAccelCap;
        if (refVerticalAccelCap <= 0f) refVerticalAccelCap = verticalAccelCap;
        if (refMaxVerticalSpeed <= 0f) refMaxVerticalSpeed = maxVerticalSpeed;
        if (refDirectionalInfluence <= 0f) refDirectionalInfluence = directionalInfluence;

        if (autoTuneFromSlider)
            ApplyFlightTuner();
    }

    public void ApplyFlightTuner()
    {
        float s = Mathf.Clamp(flightPowerSlider, 0.01f, 10f);

        maxFlightSpeed = refMaxFlightSpeed * s;
        baseFlightAcceleration = refBaseFlightAcceleration * Mathf.Lerp(1f, s, 0.95f);
        horizontalAccelCap = refHorizontalAccelCap * Mathf.Lerp(1f, s, 0.95f);
        verticalAccelCap = refVerticalAccelCap * Mathf.Lerp(1f, s, 0.6f);
        maxVerticalSpeed = refMaxVerticalSpeed * Mathf.Lerp(1f, s, 0.5f);
        directionalInfluence = refDirectionalInfluence * Mathf.Lerp(1f, s, 0.6f);

        maxFlightSpeed = Mathf.Max(1f, maxFlightSpeed);
        baseFlightAcceleration = Mathf.Max(0.1f, baseFlightAcceleration);
        horizontalAccelCap = Mathf.Max(0.1f, horizontalAccelCap);
        verticalAccelCap = Mathf.Max(0.1f, verticalAccelCap);
        maxVerticalSpeed = Mathf.Max(0.1f, maxVerticalSpeed);
        directionalInfluence = Mathf.Max(0f, directionalInfluence);

        maxForwardThrust = Mathf.Max(10f, maxForwardThrust * Mathf.Lerp(1f, s, 0.6f));
        maxSideThrust = Mathf.Max(10f, maxSideThrust * Mathf.Lerp(1f, s, 0.6f));
        maxUpThrust = Mathf.Max(10f, maxUpThrust * Mathf.Lerp(1f, s, 0.4f));
        absoluteMaxSpeed = Mathf.Max(maxFlightSpeed, absoluteMaxSpeed);
    }

    // -------------------------------
    // DASH
    void HandleDash()
    {
        if (energy == null || energy.isShutdown)
        {
            StopDash();
            return;
        }

        if (Input.GetKeyDown(dashKey) && !isFlying)
        {
            if (energy.TryUse(dashEnergyCostPerSecond * 0.2f))
            {
                rb.AddForce(transform.forward * dashForce, ForceMode.VelocityChange);
                StartDash();
            }
        }

        if (Input.GetKey(dashKey) && isDashing)
        {
            if (!energy.TryUse(dashEnergyCostPerSecond * Time.deltaTime))
                StopDash();
        }

        if (Input.GetKeyUp(dashKey))
            StopDash();
    }

    void StartDash()
    {
        if (isDashing) return;
        isDashing = true;
        rb.linearDamping = dashDrag;
    }

    void StopDash()
    {
        if (!isDashing) return;
        isDashing = false;
        rb.linearDamping = originalDrag;
    }

    // -------------------------------
    // FLIGHT
    void HandleFlight()
    {
        if (cam == null) return;
        if (energy != null && energy.isShutdown) { StopFlight(); return; }

        bool isAirborne = (ground == null || !ground.isGrounded);

        // Smooth camera direction
        Vector3 rawCamDir = cam.transform.forward.normalized;
        smoothedCamDir = Vector3.Slerp(smoothedCamDir, rawCamDir, Mathf.Clamp01(Time.deltaTime * camSmoothing));

        float verticalDot = Mathf.Clamp(Vector3.Dot(smoothedCamDir, Vector3.up), -1f, 1f);
        if (Mathf.Abs(verticalDot) < pitchDeadzone) verticalDot = 0f;

        // START flight: hold dash + W + airborne + look upwards
        bool canStartFlight =
            Input.GetKey(dashKey) &&
            Input.GetKey(KeyCode.W) &&
            isAirborne &&
            verticalDot > upwardLookThreshold;

        if (!isFlying && canStartFlight)
        {
            isFlying = true;
            currentAccelMultiplier = 1f;

            Vector3 initDir = smoothedCamDir;
            initDir.y *= verticalAccelFactor;
            if (initDir.sqrMagnitude > 0.0001f) initDir.Normalize();
            thrustDirection = initDir;

            rb.linearDamping = slideDrag;
        }

        if (!isFlying) return;

        // CONTINUE / STOP flight:
        // 👉 We no longer require W to be held.
        if (!Input.GetKey(dashKey) || !isAirborne ||
            (energy != null && !energy.TryUse(flightEnergyCostPerSecond * Time.deltaTime)))
        {
            StopFlight();
            return;
        }

        rb.linearDamping = slideDrag;

        Vector3 desiredThrustDir = smoothedCamDir;
        desiredThrustDir.y *= verticalAccelFactor;
        if (desiredThrustDir.sqrMagnitude > 0.0001f) desiredThrustDir.Normalize();
        else desiredThrustDir = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;

        thrustDirection = Vector3.Slerp(
            thrustDirection,
            desiredThrustDir,
            1f - Mathf.Exp(-thrustDirectionCatchupSpeed * Time.deltaTime)
        );
        thrustDirection.Normalize();

        // spool-up
        currentAccelMultiplier += accelerationRampPerSecond * Time.deltaTime / Mathf.Max(0.0001f, baseFlightAcceleration);
        currentAccelMultiplier = Mathf.Clamp(currentAccelMultiplier, 1f, maxAccelerationMultiplier);

        float thrustAccel = baseFlightAcceleration * currentAccelMultiplier;
        thrustAccel = Mathf.Min(thrustAccel, maxForwardThrust);

        rb.AddForce(thrustDirection * thrustAccel, ForceMode.Acceleration);

        rb.AddForce(-Physics.gravity * 1.05f, ForceMode.Acceleration);

        Vector3 currentVel = rb.linearVelocity;
        Vector3 currentHoriz = Vector3.ProjectOnPlane(currentVel, Vector3.up);
        Vector3 desiredHorizVel = Vector3.ProjectOnPlane(desiredThrustDir, Vector3.up) * maxFlightSpeed;

        Vector3 inputDir = cam.transform.right * Input.GetAxis("Horizontal") +
                           cam.transform.forward * Input.GetAxis("Vertical");

        Vector3 inputHoriz = Vector3.ProjectOnPlane(inputDir, Vector3.up);
        if (inputHoriz.sqrMagnitude > 0.0001f) inputHoriz.Normalize();
        else inputHoriz = Vector3.zero;

        Vector3 steeringInfluence =
            desiredHorizVel +
            inputHoriz * directionalInfluence * maxFlightSpeed * 0.1f;

        Vector3 horizDelta = steeringInfluence - currentHoriz;

        float effectiveMicro = Mathf.Max(1f, Mathf.Min(microThrusterAccel, maxSideThrust));
        Vector3 horizAccel = Vector3.ClampMagnitude(horizDelta / Mathf.Max(0.0001f, Time.deltaTime), effectiveMicro);

        float horizAccelCapThisFrame = horizontalAccelCap;
        if (horizAccel.magnitude > horizAccelCapThisFrame)
            horizAccel = horizAccel.normalized * horizAccelCapThisFrame;

        if (horizAccel.magnitude > maxSideThrust)
            horizAccel = horizAccel.normalized * maxSideThrust;

        rb.AddForce(horizAccel, ForceMode.Acceleration);

        float desiredVerticalVel = Mathf.Clamp(smoothedCamDir.y * maxVerticalSpeed, -maxVerticalSpeed, maxVerticalSpeed);
        float currentY = currentVel.y;
        float desiredYDelta = desiredVerticalVel - currentY;
        float maxYDelta = verticalAccelCap * Time.deltaTime;
        float appliedYDelta = Mathf.Clamp(desiredYDelta, -maxYDelta, maxYDelta);
        float vertAccelNeeded = appliedYDelta / Mathf.Max(0.0001f, Time.deltaTime);

        vertAccelNeeded = Mathf.Clamp(vertAccelNeeded, -maxUpThrust, maxUpThrust);
        rb.AddForce(Vector3.up * vertAccelNeeded, ForceMode.Acceleration);

        Vector3 postVel = rb.linearVelocity;
        Vector3 postHoriz = Vector3.ProjectOnPlane(postVel, Vector3.up);
        if (postHoriz.magnitude > maxFlightSpeed)
        {
            Vector3 clamped = postHoriz.normalized * maxFlightSpeed;
            rb.linearVelocity = new Vector3(clamped.x, rb.linearVelocity.y, clamped.z);
        }

        if (rb.linearVelocity.y > maxVerticalSpeed)
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, maxVerticalSpeed, rb.linearVelocity.z);
        if (rb.linearVelocity.y < -maxVerticalSpeed)
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, -maxVerticalSpeed, rb.linearVelocity.z);

        Vector3 finalVel = rb.linearVelocity;
        if (finalVel.magnitude > absoluteMaxSpeed)
            rb.linearVelocity = finalVel.normalized * absoluteMaxSpeed;
    }

    void StopFlight()
    {
        if (!isFlying) return;
        isFlying = false;
        currentAccelMultiplier = 1f;
        rb.linearDamping = originalDrag;
        EnsureGravityEnabled();
        targetRoll = 0f;
    }

    // -------------------------------
    // HOVER
    void HandleHover()
    {
        bool wantsHover =
            Input.GetKey(KeyCode.Space) &&
            spaceReleasedSinceGrounded &&
            (ground == null || !ground.isGrounded) &&
            !isFlying &&
            (energy == null ? true : !energy.isShutdown);

        if (!wantsHover)
        {
            if (isHovering)
            {
                isHovering = false;
                RestoreGravityAfterHover();
            }
            return;
        }

        if (energy != null && !energy.TryUse(hoverEnergyCostPerSecond * Time.deltaTime))
        {
            isHovering = false;
            RestoreGravityAfterHover();
            return;
        }

        if (!isHovering)
        {
            isHovering = true;
            DisableGravityForHover();
            Vector3 vel = rb.linearVelocity;
            vel.y *= 0.2f;
            rb.linearVelocity = vel;
        }

        rb.linearDamping = hoverDrag;
    }

    void DisableGravityForHover()
    {
        gravityWasEnabled = rb.useGravity;
        rb.useGravity = false;
    }

    void RestoreGravityAfterHover()
    {
        rb.useGravity = gravityWasEnabled;
        rb.linearDamping = originalDrag;
    }

    void EnsureGravityEnabled()
    {
        if (!rb.useGravity) rb.useGravity = true;
    }

    // -------------------------------
    // Restore states
    void RestoreStatesIfNeeded()
    {
        if (!isFlying && !isDashing && !isHovering)
        {
            rb.linearDamping = Mathf.Lerp(rb.linearDamping, originalDrag, Time.deltaTime * slideLerp);
            EnsureGravityEnabled();
        }
    }

    // -------------------------------
    // Visual roll (only in air)
    void UpdateVisualRoll()
    {
        if (visualsTransform == null) return;

        bool airborne = (ground == null || !ground.isGrounded);

        if (!airborne)
        {
            // reset roll on ground
            currentRoll = Mathf.Lerp(currentRoll, 0f, Time.deltaTime * rollSpeed);
            Vector3 e = visualsTransform.localEulerAngles;
            visualsTransform.localEulerAngles = new Vector3(
                NormalizeAngle(e.x),
                NormalizeAngle(e.y),
                currentRoll
            );
            return;
        }

        // Only roll from horizontal input while in air (flying / falling / jumping / hovering)
        float yawInput = Input.GetAxis("Horizontal");
        float desiredRoll = -yawInput * maxRollAngle;

        targetRoll = desiredRoll;
        currentRoll = Mathf.Lerp(currentRoll, targetRoll, Time.deltaTime * rollSpeed);

        Vector3 rot = visualsTransform.localEulerAngles;
        visualsTransform.localEulerAngles = new Vector3(
            NormalizeAngle(rot.x),
            NormalizeAngle(rot.y),
            currentRoll
        );
    }

    // Helper to keep angles in [-180, 180]
    float NormalizeAngle(float a)
    {
        a = Mathf.Repeat(a + 180f, 360f) - 180f;
        return a;
    }
}
