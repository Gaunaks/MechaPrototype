using UnityEngine;


public class Bulletlife : MonoBehaviour
{
    public float damage = 3f;
    public float lifeTime = 5f;
    public LayerMask hitLayers = ~0;
    public Collider ownerCollider;   // assign from the spawner to ignore the shooter
    public Transform ownerTransform; // optional: ignore all colliders that are children of this transform
    public bool debugLogs = false;

    private bool firstFrame = true;
    private Rigidbody rb;
    private Vector3 prevPosition;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        prevPosition = transform.position;
        Destroy(gameObject, lifeTime);
    }

    void FixedUpdate()
    {
        // skip the first frame to avoid immediate collision with gun/muzzle
        if (firstFrame)
        {
            prevPosition = transform.position;
            firstFrame = false;
            return;
        }

        Vector3 currentPos = transform.position;
        Vector3 travel = currentPos - prevPosition;
        float dist = travel.magnitude;

        if (dist > Mathf.Epsilon)
        {
            Ray ray = new Ray(prevPosition, travel.normalized);
            if (Physics.Raycast(ray, out RaycastHit hit, dist + 0.01f, hitLayers, QueryTriggerInteraction.Ignore))
            {
                if (ShouldIgnoreCollider(hit.collider))
                {
                    if (debugLogs) Debug.Log($"[Bulletlife] Ignored hit on owner collider: {hit.collider.name}");
                }
                else
                {
                    if (debugLogs) Debug.Log($"[Bulletlife] Raycast hit: {hit.collider.name}");
                    HandleHit(hit.collider);
                    return;
                }
            }
        }

        prevPosition = currentPos;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (ShouldIgnoreCollider(collision.collider))
        {
            if (debugLogs) Debug.Log($"[Bulletlife] Ignored collision with owner: {collision.collider.name}");
            return;
        }

        if (debugLogs) Debug.Log($"[Bulletlife] OnCollisionEnter with: {collision.collider.name}");
        HandleHit(collision.collider);
    }

    private bool ShouldIgnoreCollider(Collider c)
    {
        if (c == null) return false;
        if (ownerCollider != null && c == ownerCollider) return true;
        if (ownerTransform != null && c.transform.IsChildOf(ownerTransform)) return true;
        return false;
    }

    private void HandleHit(Collider hitCollider)
    {
        if (debugLogs) Debug.Log($"[Bulletlife] HandleHit -> {hitCollider.name}");

        ExternalSphere sphere = hitCollider.GetComponent<ExternalSphere>();
        if (sphere != null)
            sphere.TakeDamage(damage);
        
        TentacleSegment tentacleSeg = hitCollider.GetComponent<TentacleSegment>();
        if (tentacleSeg != null)
        {
            tentacleSeg.TakeDamage(damage);
        }

        Destroy(gameObject);
    }
}
