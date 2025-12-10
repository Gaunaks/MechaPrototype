using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class GunWeaponSoundController : MonoBehaviour
{
    public AudioSource audioSource;

    [Header("Machine Gun Sounds")]
    public AudioClip startClip;
    public AudioClip loopClip;
    public AudioClip endClip;

    private bool isFiring = false;

    void Update()
    {
        bool firingInput = Input.GetMouseButton(0);

        if (firingInput && !isFiring)
        {
            StartFiring();
        }
        else if (!firingInput && isFiring)
        {
            StopFiring();
        }
    }

    void StartFiring()
    {
        isFiring = true;
        StopAllCoroutines();

        StartCoroutine(PlayStartThenLoop());
    }

    IEnumerator PlayStartThenLoop()
    {
        // Play attack/start sound
        if (startClip != null)
        audioSource.pitch = 1.25f;   // 25% faster

        audioSource.PlayOneShot(startClip);

        // Wait for start sound to finish
        yield return new WaitForSeconds(startClip.length * 0.98f);

        // Start loop
        audioSource.clip = loopClip;
        audioSource.loop = true;
        audioSource.Play();
    }

    void StopFiring()
    {
        isFiring = false;
        StopAllCoroutines();

        // Stop the loop
        audioSource.loop = false;
        audioSource.Stop();

        // Play release sound
        if (endClip != null)
        audioSource.PlayOneShot(endClip);
    }
}
