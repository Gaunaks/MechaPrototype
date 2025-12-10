using UnityEngine;

public class EnergySystem : MonoBehaviour
{
    [Header("Energy Settings")]
    public float maxEnergy = 100f;
    public float currentEnergy = 100f;
    public float regenRate = 15f;
    public float shutdownThreshold = 5f;

    [Header("State")]
    public bool isShutdown = false;

    void Update()
    {
        if (!isShutdown && currentEnergy < maxEnergy)
            currentEnergy += regenRate * Time.deltaTime;

        if (isShutdown && currentEnergy > shutdownThreshold * 2f)
            isShutdown = false;

        currentEnergy = Mathf.Clamp(currentEnergy, 0f, maxEnergy);
    }

    public bool TryUse(float amount)
    {
        if (isShutdown) return false;

        if (currentEnergy >= amount)
        {
            currentEnergy -= amount;
            return true;
        }

        currentEnergy = 0;
        isShutdown = true;
        return false;
    }
}
