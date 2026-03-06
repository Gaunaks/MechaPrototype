using UnityEngine;

public class AsteroidRotationScript : MonoBehaviour
{
    [Header("Orbite (Révolution autour d'un point)")]
    public bool orbitEnabled = true;
    public Vector3 orbitCenter = Vector3.zero;
    [Tooltip("Axe de l'orbite (Y par défaut, comme le soleil)")]
    public Vector3 orbitAxis = Vector3.up;
    public float orbitSpeed = 2f;

    [Header("Rotation (Sur lui-même)")]
    public bool spinEnabled = true;
    public float spinSpeed = 5f;
    public Vector3 spinAxis = Vector3.up;

    [Header("Variations Aléatoires (Recommandé)")]
    [Tooltip("Si activé, modifie légèrement la vitesse et les axes au démarrage pour éviter que tous les astéroïdes soient synchronisés.")]
    public bool randomizeOnStart = true;
    public float minRandomMultiplier = 0.5f;
    public float maxRandomMultiplier = 1.5f;

    void Start()
    {
        if (randomizeOnStart)
        {
            // Modifie la vitesse pour éviter l'uniformité
            orbitSpeed *= Random.Range(minRandomMultiplier, maxRandomMultiplier);
            spinSpeed *= Random.Range(minRandomMultiplier, maxRandomMultiplier);

            // Incline légèrement l'orbite pour ne pas avoir un disque aplati parfait
            orbitAxis = new Vector3(
                Random.Range(-0.1f, 0.1f), 
                1f, 
                Random.Range(-0.1f, 0.1f)
            ).normalized;

            // Axe de rotation sur soi-même totalement aléatoire
            spinAxis = Random.onUnitSphere;
        }
    }

    void Update()
    {
        // 1. Orbite autour du centre de la scène
        if (orbitEnabled)
        {
            transform.RotateAround(orbitCenter, orbitAxis, orbitSpeed * Time.deltaTime);
        }

        // 2. Rotation de l'astéroïde sur lui-même
        if (spinEnabled)
        {
            transform.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.Self);
        }
    }
}
