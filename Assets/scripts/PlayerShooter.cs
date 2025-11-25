using UnityEngine;
using System.Collections.Generic;

public class PlayerShooter : MonoBehaviour
{
    [Header("Laser")]
    public KeyCode laserKey = KeyCode.Space;
    public float laserPowerPerSecond = 1f;
    public float laserRange = 100f;
    public float laserDamagePerSecond = 5f;

    [Header("Machine Gun")]
    public int magazineSize = 30;
    public float fireRate = 0.1f;
    public float bulletRange = 50f;
    public float bulletSpeed = 50f;
    public float bulletDamage = 3f;

    [Header("Spawn & Visuals")]
    public LineRenderer laserLineRenderer;
    public GameObject bulletPrefab;
    public Transform bulletSpawnPoint;
    public Vector3 bulletSpawnOffset = Vector3.zero;
    public float laserEffectDuration = 0.1f;

    public Camera playerCamera;

    // Private
    private float laserCharge = 0f;
    private float laserFireTimer = 0f;
    private float laserDuration = 0f;
    private bool laserActive = false;

    private float fireCooldown = 0f;
    private int ammo;

    // Store colliders to ignore for bullets
    private List<Collider> playerColliders = new List<Collider>();

    void Start()
    {
        ammo = magazineSize;

        if (laserLineRenderer != null)
            laserLineRenderer.enabled = false;

        // Cache ALL player colliders to ignore
        playerColliders.AddRange(GetComponentsInChildren<Collider>());
    }

    void Update()
    {
        HandleLaser();
        HandleMachineGun();
    }

    // ======================================================================
    //                           LASER
    // ======================================================================
    void HandleLaser()
    {
        if (Input.GetKey(laserKey))
            laserCharge += Time.deltaTime;

        if (Input.GetKeyUp(laserKey))
        {
            laserDuration = laserCharge * laserPowerPerSecond;
            laserFireTimer = 0f;
            laserActive = true;
            laserCharge = 0f;

            if (laserLineRenderer != null)
                laserLineRenderer.enabled = true;
        }

        if (!laserActive)
            return;

        FireLaserBeam();

        laserFireTimer += Time.deltaTime;
        if (laserFireTimer >= laserDuration)
        {
            laserActive = false;
            if (laserLineRenderer != null)
                laserLineRenderer.enabled = false;
        }
    }

    void FireLaserBeam()
    {
        if (playerCamera == null)
            return;

        Vector3 start = bulletSpawnPoint != null ?
                        bulletSpawnPoint.position :
                        playerCamera.transform.position;

        Vector3 direction = playerCamera.transform.forward;
        Vector3 end = start + direction * laserRange;

        if (Physics.Raycast(start, direction, out RaycastHit hit, laserRange))
        {
            end = hit.point;

            // 🔵 External body spheres
            ExternalSphere bodySphere = hit.collider.GetComponent<ExternalSphere>();
            if (bodySphere != null)
            {
                bodySphere.TakeDamage(laserDamagePerSecond * Time.deltaTime);
            }

            // 🟣 Tentacle segments
            TentacleSegment tentacleSeg = hit.collider.GetComponent<TentacleSegment>();
            if (tentacleSeg != null)
            {
                tentacleSeg.TakeDamage(laserDamagePerSecond * Time.deltaTime);
            }
        }

        if (laserLineRenderer != null)
        {
            laserLineRenderer.SetPosition(0, start);
            laserLineRenderer.SetPosition(1, end);
        }
    }

    // ======================================================================
    //                           MACHINE GUN
    // ======================================================================
    void HandleMachineGun()
    {
        fireCooldown -= Time.deltaTime;

        if (Input.GetMouseButton(0) && fireCooldown <= 0f && ammo > 0)
        {
            ShootBullet();
            ammo--;
            fireCooldown = fireRate;
        }

        if (ammo <= 0)
            ammo = magazineSize;
    }

    void ShootBullet()
    {
        if (playerCamera == null)
            return;

        Vector3 direction = playerCamera.transform.forward;

        // spawn a bit in front so it doesn't hit the camera/body
        Vector3 spawnPos = (bulletSpawnPoint != null ?
                            bulletSpawnPoint.position :
                            playerCamera.transform.position)
                            + direction * 0.7f;

        spawnPos += bulletSpawnOffset;

        GameObject bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(direction));

        // Ignore ALL player colliders
        Collider bulletCol = bullet.GetComponent<Collider>();
        if (bulletCol != null)
        {
            foreach (var col in playerColliders)
            {
                if (col != null)
                    Physics.IgnoreCollision(bulletCol, col);
            }
        }

        // Bullet damage logic
        Bulletlife bl = bullet.GetComponent<Bulletlife>();
        if (bl != null)
        {
            bl.damage = bulletDamage;
            bl.ownerTransform = transform;
            bl.ownerCollider = (playerColliders.Count > 0) ? playerColliders[0] : null;
        }

        // movement
        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = direction * bulletSpeed;

        Destroy(bullet, 5f);
    }
}
