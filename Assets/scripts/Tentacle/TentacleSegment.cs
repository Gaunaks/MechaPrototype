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
        isDestroyed = true;
        gameObject.SetActive(false);

        if (ownerRoot != null)
            ownerRoot.OnSegmentDestroyed(this);

        // optional: regen later
        StartCoroutine(RegenerateAfterDelay());
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
