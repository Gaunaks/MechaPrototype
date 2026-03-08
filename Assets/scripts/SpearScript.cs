using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody), typeof(Collider))]
public class SpearScript : MonoBehaviour
{
    public enum SpearState { OnGround, Equipped, Aiming, Thrown, Thrusting }
    
    [Header("Status")]
    public SpearState currentState = SpearState.OnGround;

    [Header("Equip Settings")]
    [Tooltip("La cible où la lance s'attache (Laissez vide pour utiliser la Main Camera par défaut)")]
    public Transform equipParent; 
    public Vector3 equipLocalPosition = new Vector3(0.5f, -0.5f, 1f);
    public Vector3 equipLocalRotation = new Vector3(0, 0, 0);       
    public float pickupRange = 3f;
    public KeyCode pickupKey = KeyCode.E;

    [Header("Input Settings")]
    public int meleeButton = 1;
    public int throwButton = 2;

    [Header("Attack (Melee) Settings")]
    public float thrustDistance = 1.5f;    
    public float thrustSpeed = 15f;        
    
    [Header("Throw / Aim Settings")]
    public float minThrowForce = 20f;
    public float maxThrowForce = 150f;
    public float chargeTime = 1.5f; 
    public float aimPullbackDistance = 0.5f;
    public float throwSpawnForwardOffset = 0.5f;
    [Tooltip("La rotation locale que prend la lance quand on vise (Ex: (90, 0, 0) pour la coucher)")]
    public Vector3 aimLocalRotation = new Vector3(90, 0, 0); 
    public float aimTransitionSpeed = 10f; // Vitesse pour tourner l'arme vers la pose de visée

    [Header("Throw Physics Settings")]
    [Tooltip("L'épaisseur (Rayon) de la détection anti-tunneling pendant le lancer")]
    public float throwDetectionRadius = 0.5f;

    private Rigidbody rb;
    private Collider col;
    private Camera mainCam;
    private Vector3 startLocalPos;
    private Quaternion startLocalRot;
    
    private float currentChargeTime = 0f;
    private GameObject originalPlayerObject;
    private float timeThrown = 0f;

    private FixedJoint currentJoint;
    private Transform attachedSurface;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();
        mainCam = Camera.main;

        if (currentState == SpearState.OnGround)
        {
            ForceRelease();
        }
    }

    void Update()
    {
        switch (currentState)
        {
            case SpearState.OnGround:
                CheckPickup();
                
                if (attachedSurface != null && (!attachedSurface.gameObject.activeInHierarchy || attachedSurface == null))
                {
                    ForceRelease();
                }
                break;

            case SpearState.Equipped:
                HandleEquippedInputs();
                
                // On s'assure de revenir doucement à la pose normale si on a annulé une visée 
                // ou fini de jeter (pour la prochaine fois qu'on l'equipe par ex)
                transform.localPosition = Vector3.Lerp(transform.localPosition, startLocalPos, Time.deltaTime * aimTransitionSpeed);
                transform.localRotation = Quaternion.Slerp(transform.localRotation, startLocalRot, Time.deltaTime * aimTransitionSpeed);
                break;

            case SpearState.Aiming:
                HandleAiming();
                break;

            case SpearState.Thrusting:
                break;
                
            case SpearState.Thrown:
                if (rb.linearVelocity.sqrMagnitude > 0.1f)
                {
                    // Calcule la direction de la vélocité comme étant l'axe "avant" du monde pour l'objet
                    Quaternion velocityRotation = Quaternion.LookRotation(rb.linearVelocity);
                    
                    // Ajoute le décalage utilisé lors de la visée pour que le mesh soit couché correctement
                    transform.rotation = velocityRotation * Quaternion.Euler(aimLocalRotation);
                }
                
                CheckSphereCastForCore();
                break;
        }
    }

    // -------------------------------------------------------------
    // RAMASSER LA LANCE
    // -------------------------------------------------------------
    void CheckPickup()
    {
        if (mainCam == null) return;
        
        if (Vector3.Distance(transform.position, mainCam.transform.position) <= pickupRange)
        {
            if (Input.GetKeyDown(pickupKey))
            {
                Equip(equipParent != null ? equipParent : mainCam.transform); 
            }
        }
    }

    public void Equip(Transform newParent)
    {
        ForceRelease();
        
        currentState = SpearState.Equipped;
        
        if (newParent != null && newParent.root != null)
        {
            originalPlayerObject = newParent.root.gameObject;
        }
        
        rb.isKinematic = true;
        col.enabled = false;

        transform.SetParent(newParent);
        transform.localPosition = equipLocalPosition;
        transform.localEulerAngles = equipLocalRotation;

        startLocalPos = equipLocalPosition;
        startLocalRot = Quaternion.Euler(equipLocalRotation);
    }

    // -------------------------------------------------------------
    // ATTAQUES ET LANCER
    // -------------------------------------------------------------
    void HandleEquippedInputs()
    {
        if (Input.GetMouseButtonDown(meleeButton))
        {
            StartCoroutine(ThrustRoutine());
        }
        else if (Input.GetMouseButtonDown(throwButton))
        {
            currentState = SpearState.Aiming;
            currentChargeTime = 0f;
        }
    }

    void HandleAiming()
    {
        // 1. Charger la puissance et modifier visuellement la lance tant que le clic est maintenu
        if (Input.GetMouseButton(throwButton))
        {
            currentChargeTime += Time.deltaTime;
            float chargePercent = Mathf.Clamp01(currentChargeTime / chargeTime);

            Vector3 pullbackPos = startLocalPos - Vector3.forward * (aimPullbackDistance * chargePercent);
            Quaternion targetAimRotation = Quaternion.Euler(aimLocalRotation);

            transform.localPosition = Vector3.Lerp(transform.localPosition, pullbackPos, Time.deltaTime * aimTransitionSpeed);
            transform.localRotation = Quaternion.Slerp(transform.localRotation, targetAimRotation, Time.deltaTime * aimTransitionSpeed);
        }

        // 2. Lancer ou annuler
        if (Input.GetMouseButtonUp(throwButton))
        {
            float chargePercent = Mathf.Clamp01(currentChargeTime / chargeTime);
            float finalForce = Mathf.Lerp(minThrowForce, maxThrowForce, chargePercent);
            Throw(finalForce);
        }
    }

    IEnumerator ThrustRoutine()
    {
        currentState = SpearState.Thrusting;

        // Lors de l'estoc, on part du principe que l'arme est bien placée localement
        // Assurons-nous au moins qu'elle est droite via startLocalPos / Rot
        transform.localPosition = startLocalPos;
        transform.localRotation = startLocalRot;

        Vector3 targetPos = startLocalPos + Vector3.forward * thrustDistance;
        
        RaycastHit[] hits = Physics.SphereCastAll(mainCam.transform.position, throwDetectionRadius * 0.5f, mainCam.transform.forward, thrustDistance + 1f);
        
        foreach (var hit in hits)
        {
            if (originalPlayerObject != null && hit.collider.transform.root.gameObject == originalPlayerObject)
                continue;

            if (ProcessHit(hit.collider, false)) break; 
        }

        float t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * thrustSpeed;
            transform.localPosition = Vector3.Lerp(startLocalPos, targetPos, t);
            yield return null;
        }

        t = 0;
        while (t < 1f)
        {
            t += Time.deltaTime * thrustSpeed * 0.8f; 
            transform.localPosition = Vector3.Lerp(targetPos, startLocalPos, t);
            yield return null;
        }

        transform.localPosition = startLocalPos;
        currentState = SpearState.Equipped;
    }

    void Throw(float force)
    {
        ForceRelease();
        currentState = SpearState.Thrown;
        timeThrown = Time.time; 
        
        rb.isKinematic = false;
        rb.useGravity = true; 
        col.enabled = true;
        
        if (originalPlayerObject != null)
        {
            Collider[] playerColliders = originalPlayerObject.GetComponentsInChildren<Collider>();
            foreach(var pCol in playerColliders)
            {
                Physics.IgnoreCollision(col, pCol, true);
            }
        }

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        
        if (mainCam != null)
        {
            transform.forward = mainCam.transform.forward; 
            
            // On s'assure qu'elle débute avec la même rotation au lieu d'un simple "cam.forward" 
            // pour éviter un "à-coup" visuel.
            transform.rotation = Quaternion.LookRotation(mainCam.transform.forward) * Quaternion.Euler(aimLocalRotation);
            
            transform.position += mainCam.transform.forward * throwSpawnForwardOffset;
            rb.linearVelocity = mainCam.transform.forward * force; 
        }
        else
        {
            transform.rotation = Quaternion.LookRotation(transform.forward) * Quaternion.Euler(aimLocalRotation);
            rb.linearVelocity = transform.forward * force;
        }
    }

    // -------------------------------------------------------------
    // GESTION DES IMPACTS
    // -------------------------------------------------------------

    void CheckSphereCastForCore()
    {
        if (currentState != SpearState.Thrown) return;

        float lookAhead = (rb.linearVelocity.magnitude * Time.deltaTime) + 1.5f; 
        if (lookAhead < 0.1f) return;

        RaycastHit[] hits = Physics.SphereCastAll(transform.position, throwDetectionRadius, rb.linearVelocity.normalized, lookAhead);
        
        foreach (var hit in hits)
        {
            if (originalPlayerObject != null && hit.collider.transform.root.gameObject == originalPlayerObject) 
                continue;

            InternalSphere core = hit.collider.GetComponent<InternalSphere>();
            if (core == null) core = hit.collider.GetComponentInParent<InternalSphere>();

            if (core != null)
            {
                ProcessHit(hit.collider, true);
                break;
            }
        }
    }

    void OnCollisionEnter(Collision collision)
    {
        if (currentState != SpearState.Thrown) return; 
        if (Time.time - timeThrown < 0.05f) return;
        if (originalPlayerObject != null && collision.transform.root.gameObject == originalPlayerObject) return;

        currentState = SpearState.OnGround;

        if (ProcessHit(collision.collider, true)) return;

        EmbedIntoSurface(collision);
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (currentState != SpearState.Thrown) return;
        if (Time.time - timeThrown < 0.05f) return;
        if (originalPlayerObject != null && other.transform.root.gameObject == originalPlayerObject) return;

        ProcessHit(other, true);
    }

    bool ProcessHit(Collider hitCollider, bool isThrown)
    {
        InternalSphere core = hitCollider.GetComponent<InternalSphere>();
        if (core == null) core = hitCollider.GetComponentInParent<InternalSphere>();

        if (core != null)
        {
            EnemiesGauna gauna = core.GetComponentInParent<EnemiesGauna>();
            
            transform.SetParent(null);
            ForceRelease(); 
            
            currentState = SpearState.OnGround;
            rb.linearVelocity = Vector3.zero;

            if (gauna != null) 
            {
                Debug.Log("<color=green>CORE DÉTRUIT ! Le Gauna meurt !</color>");
                gauna.KillGauna();
            }

            return true;
        }

        return false; 
    }

    // -------------------------------------------------------------
    // ATTACHEMENT ET DÉTACHEMENT
    // -------------------------------------------------------------
    void EmbedIntoSurface(Collision collision)
    {
        ForceRelease();

        rb.isKinematic = true;
        rb.linearVelocity = Vector3.zero;
        
        attachedSurface = collision.transform;
        
        transform.SetParent(collision.transform, true);
    }

    void ForceRelease()
    {
        transform.SetParent(null);
        attachedSurface = null;
        
        if (currentJoint != null)
        {
            Destroy(currentJoint);
            currentJoint = null;
        }

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }
}
