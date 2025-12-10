using UnityEngine;

public class WeaponManager : MonoBehaviour
{
    public LaserWeapon laser;
    public GunWeapon gun;

    void Update()
    {

        if (gun != null)
            gun.HandleGun();
    }
}
