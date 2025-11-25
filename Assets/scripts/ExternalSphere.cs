using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExternalSphere : MonoBehaviour
{
    public EnemiesGauna parent;

    [Header("Points de vie")]
    public float maxHP = 10f;
    private float currentHP;
    private bool isDestroyed = false;

    [Header("Dissolve Settings")]
    public float dissolveDuration = 0.75f;
    public Material dissolveMaterial;

    private Material[] runtimeMaterials;
    private Coroutine dissolveRoutine;

    // caches
    private Collider[] cachedColliders;
    private Renderer[] cachedRenderers;
    private Rigidbody[] cachedRigidbodies;

    public bool IsDestroyed => isDestroyed;

    void Awake()
    {
        currentHP = maxHP;

        cachedColliders = GetComponentsInChildren<Collider>(true);
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedRigidbodies = GetComponentsInChildren<Rigidbody>(true);

        // instantiate materials so each sphere dissolves independently
        runtimeMaterials = new Material[cachedRenderers.Length];
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            if (dissolveMaterial != null)
                runtimeMaterials[i] = new Material(dissolveMaterial);
            else
                runtimeMaterials[i] = new Material(cachedRenderers[i].material);

            cachedRenderers[i].material = runtimeMaterials[i];
        }
    }


    // ------------------------------
    //    DISSOLVE ANIMATION
    // ------------------------------
    public void StartDissolve(bool restoring)
    {
        if (dissolveRoutine != null)
            StopCoroutine(dissolveRoutine);

        dissolveRoutine = StartCoroutine(DissolveAnimation(restoring));
    }

    private IEnumerator DissolveAnimation(bool restoring)
    {
        float t = 0f;

        float start = restoring ? 1f : 0f;
        float end = restoring ? 0f : 1f;

        // make visible when restoring
        if (restoring)
        {
            foreach (var r in cachedRenderers)
                r.enabled = true;
        }

        while (t < dissolveDuration)
        {
            t += Time.deltaTime;
            float v = Mathf.Lerp(start, end, t / dissolveDuration);

            foreach (var mat in runtimeMaterials)
            {
                if (mat != null)
                    mat.SetFloat("_DissolveAmount", v);
            }

            yield return null;
        }

        // hide when fully dissolved
        if (!restoring)
        {
            foreach (var r in cachedRenderers)
                r.enabled = false;
        }
    }



    // ------------------------------
    //           DAMAGE
    // ------------------------------
    public void TakeDamage(float amount)
    {
        if (isDestroyed) return;

        currentHP -= amount;
        if (currentHP <= 0f)
        {
            DestroySphere();
        }
    }



    // ------------------------------
    //          DESTROY
    // ------------------------------
    public void DestroySphere()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        Debug.Log($"DISSOLVE DestroySphere on {name}");

        // disable physics
        foreach (var c in cachedColliders)
            c.enabled = false;

        foreach (var rb in cachedRigidbodies)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        // dissolve out
        StartDissolve(restoring: false);

        // notify parent
        parent.OnExternalSphereDestroyed(this);
    }



    // ------------------------------
    //           RESTORE
    // ------------------------------
    public void RestoreSphere()
    {
        if (!isDestroyed) return;

        isDestroyed = false;
        currentHP = maxHP;

        // re-enable physics
        foreach (var c in cachedColliders)
            c.enabled = true;

        foreach (var rb in cachedRigidbodies)
        {
            rb.isKinematic = false;
            rb.detectCollisions = true;
        }

        // dissolve back in
        StartDissolve(restoring: true);

        Debug.Log($"Restored sphere {name}");
    }
}
