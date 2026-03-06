using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MechMovementExtensions : MonoBehaviour
{
    Rigidbody rb;
    FirstPersonMovement movement;
    EnergySystem energy;
    Camera cam;
    GroundCheck ground;

    [Header("Dash (Double Tap)")]
    public KeyCode dashKey = KeyCode.LeftShift;
    public float dashForce = 40f;
    public float dashEnergyCostPerSecond = 30f;
    public float dashDrag = 0.5f; // temporary drag during dash
    [Tooltip("Délai maximum entre deux appuis pour valider un Dash")]
    public float doubleTapTimeWindow = 0.3f;
    
    private bool isDashing = false;
    private float lastTapTime = -1f;

    [Header("Flight (Sidonia-style rocket)")]
    [Tooltip("Max horizontal flight speed (m/s)")]
    public float maxFlightSpeed = 90f;
    [Tooltip("Base 'thrust' acceleration in m/s^2")]
    public float baseFlightAcceleration = 80f;
    public float accelerationRampPerSecond = 35f;
    public float maxAccelerationMultiplier = 4f;
    public float verticalAccelFactor = 0.75f;
    public float flightEnergyCostPerSecond = 40f;
    
    [Tooltip("Seuil pour déclencher le vol quand on regarde en l'air")]
    public float upwardLookThreshold = 0.15f;

    [Header("Micro-thrusters / caps")]
    public float microThrusterAccel = 250f;
    public float horizontalAccelCap = 120f;
    public float verticalAccelCap = 80f;
    public float maxVerticalSpeed = 40f;

    [Header("Steering & alignment")]
    public float yawAlignSpeed = 720f;
    public float thrustDirectionCatchupSpeed = 3.5f;
    public float directionalInfluence = 28f;

    [Header("Flight Smoothing")]
    public float camSmoothing = 10f;
    public float pitchDeadzone = 0.08f;

    [Header("Power boost presets")]
    public float maxForwardThrust = 350f;
    public float maxSideThrust = 150f;
    public float maxUpThrust = 150f;
    public float inertiaDampSpeed = 4f;
    public float directionSnappiness = 9f;
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
    float currentAccelMultiplier = 1f;
    Vector3 smoothedCamDir;
    Vector3 thrustDirection;
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

    [Header("Camera & Visual Roll")]
    public Transform visualsTransform = null;
    [Tooltip("Maximum Roll in degrees when pressing Q/D keys in flight")]
    public float maxRollAngle = 45f;
    public float rollSpeed = 6f;
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
        bool airborne = (ground == null || !ground.isGrounded);

        if (movement != null)
        {
            // Le script FPS normal prend le relais si on n'est ni en vol, ni en hover, ni en plein dash
            movement.enabled = !isFlying && !isHovering && !isDashing;
        }

        if (autoTuneFromSlider && !Mathf.Approximately(lastFlightPowerSlider, flightPowerSlider))
        {
            ApplyFlightTuner();
            lastFlightPowerSlider = flightPowerSlider;
        }

        HandleDash();
        HandleFlight();
        HandleHover();
        RestoreStatesIfNeeded();

        if (airborne)
        {
            if (!Input.GetKey(KeyCode.Space))
                spaceReleasedSinceGrounded = true;
        }
        else
        {
            spaceReleasedSinceGrounded = false;
        }
    }

    void LateUpdate()
    {
        UpdateCameraAndVisualRoll();
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

        // --- GESTION DU DOUBLE TAP ---
        if (Input.GetKeyDown(dashKey))
        {
            if (Time.time - lastTapTime <= doubleTapTimeWindow && !isFlying)
            {
                // Un Double-Tap a été détecté ! On éxécute le code de propulsion
                if (energy.TryUse(dashEnergyCostPerSecond * 0.2f))
                {
                    Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
                    Vector3 dashDirection = Vector3.zero;

                    if (input.magnitude > 0.1f && cam != null)
                    {
                        dashDirection = cam.transform.right * input.x + cam.transform.forward * input.y;
                        dashDirection.y = 0;
                        dashDirection.Normalize();
                    }
                    else
                    {
                        Vector3 forwardDir = cam != null ? cam.transform.forward : transform.forward;
                        forwardDir.y = 0;
                        dashDirection = forwardDir.normalized;
                    }

                    rb.AddForce(dashDirection * dashForce, ForceMode.VelocityChange);
                    StartDash();
                }
                
                // On réinitialise pour éviter de spam en boucle sur 3 ou 4 clics
                lastTapTime = -1f; 
            }
            else
            {
                // Enregistre l'heure du premier clic
                lastTapTime = Time.time;
            }
        }

        // On vérifie que la touche est maintenue si l'on veut consommer de l'énergie en prolongeant le slide 
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

        Vector3 rawCamDir = cam.transform.forward.normalized;
        smoothedCamDir = Vector3.Slerp(smoothedCamDir, rawCamDir, Mathf.Clamp01(Time.deltaTime * camSmoothing));

        float verticalDot = Mathf.Clamp(Vector3.Dot(smoothedCamDir, Vector3.up), -1f, 1f);
        if (Mathf.Abs(verticalDot) < pitchDeadzone) verticalDot = 0f;

        // START VOL : Saut + Maintient du bouton (Shift) + Regarder en l'air 
        bool canStartFlight =
            Input.GetKey(dashKey) &&
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

        currentAccelMultiplier += accelerationRampPerSecond * Time.deltaTime / Mathf.Max(0.0001f, baseFlightAcceleration);
        currentAccelMultiplier = Mathf.Clamp(currentAccelMultiplier, 1f, maxAccelerationMultiplier);

        float thrustAccel = baseFlightAcceleration * currentAccelMultiplier;
        thrustAccel = Mathf.Min(thrustAccel, maxForwardThrust);

        rb.AddForce(thrustDirection * thrustAccel, ForceMode.Acceleration);
        rb.AddForce(-Physics.gravity * 1.05f, ForceMode.Acceleration);

        Vector3 currentVel = rb.linearVelocity;
        Vector3 currentHoriz = Vector3.ProjectOnPlane(currentVel, Vector3.up);
        Vector3 desiredHorizVel = Vector3.ProjectOnPlane(desiredThrustDir, Vector3.up) * maxFlightSpeed;

        Vector3 inputDir = cam.transform.forward * Input.GetAxis("Vertical");

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
    // Roulis Physique et Visuel NOUVEAU
    void UpdateCameraAndVisualRoll()
    {
        if (!isFlying)
        {
            targetRoll = 0f;
        }
        else
        {
            float yawInput = Input.GetAxis("Horizontal");
            targetRoll = -yawInput * maxRollAngle;
        }

        currentRoll = Mathf.Lerp(currentRoll, targetRoll, Time.deltaTime * rollSpeed);

        if (visualsTransform != null)
        {
            Vector3 rot = visualsTransform.localEulerAngles;
            visualsTransform.localEulerAngles = new Vector3(rot.x, rot.y, currentRoll);
        }

        if (cam != null)
        {
            Vector3 camRot = cam.transform.localEulerAngles;
            cam.transform.localEulerAngles = new Vector3(camRot.x, camRot.y, currentRoll);
        }
    }
}
