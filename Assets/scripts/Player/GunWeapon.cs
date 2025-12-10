using UnityEngine;
using System.Collections.Generic;

public class GunWeapon : MonoBehaviour
{
    [Header("Machine Gun Settings")]
    public float fireRate = 0.06f;            // Sidonia = very fast firing
    public float bulletSpeed = 120f;          // Sidonia kinetic shells are FAST
    public float bulletDamage = 4f;
    public int magazineSize = 60;

    [Header("Dual Barrels")]
    public Transform leftBarrel;
    public Transform rightBarrel;

    [Tooltip("Offset forward from barrel so it doesn't collide with mech")]
    public float muzzleForwardOffset = 0.7f;

    private bool useLeftBarrel = true;

    [Header("References")]
    public GameObject bulletPrefab;
    public Camera playerCamera;

    private float cooldown = 0f;
    private int ammo = 0;

    private List<Collider> playerColliders = new List<Collider>();

    void Start()
    {
        ammo = magazineSize;

        // cache all player colliders so bullets ignore player
        playerColliders.AddRange(GetComponentsInParent<Collider>());

        if (leftBarrel == null || rightBarrel == null)
            Debug.LogError("Assign LEFT and RIGHT barrel transforms!");
    }

    public void HandleGun()
    {
        cooldown -= Time.deltaTime;

        if (Input.GetMouseButton(0) && cooldown <= 0f && ammo > 0)
        {
            FireDualBarrelRound();
            ammo--;
            cooldown = fireRate;
        }

        if (ammo <= 0)
            ammo = magazineSize; // temporary auto-reload
    }

    void FireDualBarrelRound()
    {
        Transform barrel = useLeftBarrel ? leftBarrel : rightBarrel;

        // alternate next shot
        useLeftBarrel = !useLeftBarrel;

        Vector3 direction = playerCamera.transform.forward;

        // spawn position a bit ahead of the barrel
        Vector3 spawnPos = barrel.position + direction * muzzleForwardOffset;

        GameObject bullet = Instantiate(bulletPrefab, spawnPos, Quaternion.LookRotation(direction));

        // ignore player collisions
        Collider bulletCol = bullet.GetComponent<Collider>();
        if (bulletCol != null)
        {
            foreach (var col in playerColliders)
                Physics.IgnoreCollision(bulletCol, col);
        }

        // bullet damage
        Bulletlife bl = bullet.GetComponent<Bulletlife>();
        if (bl != null)
            bl.damage = bulletDamage;

        // propulsion
        Rigidbody rb = bullet.GetComponent<Rigidbody>();
        if (rb != null)
            rb.linearVelocity = direction * bulletSpeed;
    }
}
