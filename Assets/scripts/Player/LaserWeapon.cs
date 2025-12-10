using UnityEngine;

public class LaserWeapon : MonoBehaviour
{
    [Header("Laser Settings")]
    public KeyCode fireKey = KeyCode.Mouse1;
    public float laserPowerPerSecond = 1f;
    public float laserDamagePerSecond = 5f;
    public float laserRange = 120f;

    [Header("References")]
    public LineRenderer lineRenderer;
    public Transform muzzlePoint;
    public Camera playerCamera;

    private float charge = 0f;
    private float duration = 0f;
    private float timer = 0f;
    private bool active = false;

    void Start()
    {
        if (!playerCamera)
            playerCamera = Camera.main;

        if (lineRenderer)
            lineRenderer.enabled = false;
    }

    void Update()
    {
        HandleInput();

        if (active)
            FireLaser();
    }

    void HandleInput()
    {
        if (Input.GetKey(fireKey))
            charge += Time.deltaTime;

        if (Input.GetKeyUp(fireKey))
        {
            duration = charge * laserPowerPerSecond;
            timer = 0f;
            active = true;
            charge = 0f;

            if (lineRenderer)
                lineRenderer.enabled = true;
        }

        if (active)
        {
            timer += Time.deltaTime;
            if (timer >= duration)
            {
                active = false;
                if (lineRenderer)
                    lineRenderer.enabled = false;
            }
        }
    }

    void FireLaser()
    {
        Vector3 start = muzzlePoint ? muzzlePoint.position : playerCamera.transform.position;
        Vector3 dir = playerCamera.transform.forward;

        // 1️⃣ MAIN RAYCAST — smooth, stable endpoint
        Vector3 end = start + dir * laserRange;
        if (Physics.Raycast(start, dir, out RaycastHit hit, laserRange))
            end = hit.point;

        // 2️⃣ SPHERECAST FOR DAMAGE — hits EVERYTHING along path
        RaycastHit[] hits = Physics.SphereCastAll(start, 0.2f, dir, laserRange);

        foreach (var h in hits)
        {
            TentacleSegment tent = h.collider.GetComponentInParent<TentacleSegment>();
            if (tent) tent.TakeDamage(laserDamagePerSecond * Time.deltaTime);

            ExternalSphere sphere = h.collider.GetComponentInParent<ExternalSphere>();
            if (sphere) sphere.TakeDamage(laserDamagePerSecond * Time.deltaTime);
        }

        // 3️⃣ DRAW LASER
        if (lineRenderer)
        {
            lineRenderer.SetPosition(0, start);
            lineRenderer.SetPosition(1, end);
        }
    }
}
