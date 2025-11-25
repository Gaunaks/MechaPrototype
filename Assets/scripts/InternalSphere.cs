using UnityEngine;

public class InternalSphere : MonoBehaviour
{
    [Tooltip("Référence à l'ennemi parent (Gauna)")]
    public EnemiesGauna parent;

    // Exemple de destruction (à adapter selon votre logique de dégâts)
    public void DestroySphere()
    {
        // éventuellement : effets, particules, son, etc.
        // ex: Instantiate(explosionVFX, transform.position, Quaternion.identity);

        gameObject.SetActive(false);

        // Si on veut informer le parent explicitement (optionnel)
        if (parent != null)
        {
            // Tu peux ajouter une méthode sur EnemiesGauna si tu veux
            // parent.OnCoreDestroyed();
        }
    }
}
