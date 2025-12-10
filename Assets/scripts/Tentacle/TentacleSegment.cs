
using UnityEngine;
using System.Collections;

public class TentacleSegment : MonoBehaviour
{
    [Header("HP / Regen")]
    public float maxHP = 5f;
    public float regenDelay = 4f;
    public Color defaultColor = Color.white;
    public Color damagedColor = Color.red;

    [HideInInspector] public TentacleRoot ownerRoot;

    private float currentHP;
    private bool isDestroyed = false;
    private Renderer rend;

    private void Awake()
    {
        currentHP = maxHP;
        rend = GetComponent<Renderer>();
        if (rend != null)
            rend.material.color = defaultColor;
    }

    public void TakeDamage(float amount)
    {
        if (isDestroyed) return;

        currentHP -= amount;
        if (rend != null)
            rend.material.color = damagedColor;

        if (currentHP <= 0f)
            DestroySegment();
    }

    private void DestroySegment()
    {
        if (isDestroyed) return;
        isDestroyed = true;

        // If we have a root, let it handle removal/destruction and schedule regeneration there.
        if (ownerRoot != null)
        {
            // notify root (this may destroy the GameObject)
            ownerRoot.OnSegmentDestroyed(this);

            // schedule regeneration on the root (root is active so coroutine runs even if this GO is destroyed)
            ownerRoot.StartCoroutine(ownerRoot.RegenerateSegmentCoroutine(regenDelay));
        }
        else
        {
            // fallback: deactivate and self-regen if no root
            gameObject.SetActive(false);
            StartCoroutine(RegenerateAfterDelay());
        }
    }

    private IEnumerator RegenerateAfterDelay()
    {
        yield return new WaitForSeconds(regenDelay);
        currentHP = maxHP;
        isDestroyed = false;
        gameObject.SetActive(true);
        if (rend != null)
            rend.material.color = defaultColor;
    }
}