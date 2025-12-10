using UnityEngine;

public class Bulletlife : MonoBehaviour
{
    public float damage = 3f;
    public float lifeTime = 5f;
    public LayerMask hitLayers = ~0;

    public Collider ownerCollider;
    public Transform ownerTransform;
    public bool debugLogs = false;

    private bool firstFrame = true;
    private Rigidbody rb;
    private Vector3 prevPosition;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Reset trail when spawned
        TrailRenderer tr = GetComponentInChildren<TrailRenderer>();
        if (tr != null) tr.Clear();

        Destroy(gameObject, lifeTime);
    }


    void FixedUpdate()
    {
        if (firstFrame)
        {
            prevPosition = transform.position;
            firstFrame = false;
            return;
        }

        Vector3 currentPos = transform.position;
        Vector3 travel = currentPos - prevPosition;
        float dist = travel.magnitude;

        // prevent false hits from tiny motion
        if (dist > 0.02f)
        {
            Ray ray = new Ray(prevPosition, travel.normalized);

            if (Physics.Raycast(ray, out RaycastHit hit, dist + 0.05f, hitLayers, QueryTriggerInteraction.Ignore))
            {
                if (!ShouldIgnoreCollider(hit.collider))
                {
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
            return;

        HandleHit(collision.collider);
    }

    private bool ShouldIgnoreCollider(Collider c)
    {
        if (c == null) return false;

        if (ownerCollider != null && c == ownerCollider)
            return true;

        if (ownerTransform != null && c.transform.IsChildOf(ownerTransform))
            return true;

        return false;
    }

    private void HandleHit(Collider hitCollider)
    {
        ExternalSphere sphere = hitCollider.GetComponentInParent<ExternalSphere>();
        if (sphere) sphere.TakeDamage(damage);

        TentacleSegment tent = hitCollider.GetComponentInParent<TentacleSegment>();
        if (tent) tent.TakeDamage(damage);

        Destroy(gameObject);
    }
}
