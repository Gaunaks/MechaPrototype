using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemiesGauna : MonoBehaviour
{
    [Header("Core")]
    public InternalSphere internalSphere;

    [Header("External Body Spheres")]
    public List<ExternalSphere> externalSpheres = new List<ExternalSphere>();
    public float overlapRadius = 0.6f; // for connectivity

    [Header("Regeneration")]
    public bool autoRegenerate = true;
    public float regenDelay = 0.75f;
    private Coroutine regenRoutine;

    [Header("Tentacles")]
    [Tooltip("Prefab used for tentacle segments (must have TentacleSegment + Collider)")]
    public GameObject tentacleSegmentPrefab;

    [Tooltip("External spheres that can spawn tentacles (drop them here)")]
    public List<ExternalSphere> tentacleRoots = new List<ExternalSphere>();

    public int initialTentacleSegments = 4;
    public float tentacleSegmentSpacing = 0.6f;

    [Header("Tentacle Targeting")]
    public Transform player;              // player transform
    public float tentacleEngageDistance = 10f;
    public float tentacleDisengageDistance = 13f;

    // Runtime lists
    [HideInInspector] public List<TentacleRoot> tentacles = new List<TentacleRoot>();
    [HideInInspector] public List<TentacleSegment> allTentacleSegments = new List<TentacleSegment>();

    private bool isDead = false;

    private void Awake()
    {
        if (externalSpheres.Count == 0)
            externalSpheres.AddRange(GetComponentsInChildren<ExternalSphere>());

        if (player == null)
        {
            GameObject pgo = GameObject.FindGameObjectWithTag("Player");
            if (pgo != null) player = pgo.transform;
        }

        // set parent reference in ExternalSphere
        foreach (var s in externalSpheres)
        {
            if (s != null) s.parent = this;
        }
    }

    private void Start()
    {
        // spawn tentacles at marked root spheres
        SpawnTentaclesAtRoots();
    }

    // ----------------------------------------------------------------------
    //  DEATH / DISSOLVE
    // ----------------------------------------------------------------------
    public void KillGauna()
    {
        if (isDead) return;
        isDead = true;

        // Arrête les routines de régénération
        autoRegenerate = false;
        if (regenRoutine != null) StopCoroutine(regenRoutine);
        
        // Empêche les tentacules de bouger/attaquer
        foreach (var root in tentacles)
        {
            if (root != null) root.enabled = false;
        }

        // On lance la dissolution sur tous les composants corporels. 
        // Heureusement, ExternalSphere a DEJA la logique StartDissolve!
        foreach (var sphere in externalSpheres)
        {
            if (sphere != null && !sphere.IsDestroyed)
            {
                sphere.StartDissolve(false);
            }
        }

        // Le Core et les tentacules n'ont potentiellement pas le même script alors 
        // on lance une coroutine globale qui va chercher tous les renderers pour les dissoudre
        StartCoroutine(DeathDissolveRoutine());
    }

    private IEnumerator DeathDissolveRoutine()
    {
        float dissolveDuration = 1.0f; // Durée totale de la mort
        float t = 0f;

        // On cherche tous les renderers restants (Core, Tentacules...)
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
        List<Material> matsToDissolve = new List<Material>();

        foreach (var r in allRenderers)
        {
            if (r != null && r.material != null)
            {
                // Un peu lourd, mais on clone le mat pour ne pas altérer le mat original du projet
                r.material = new Material(r.material);
                matsToDissolve.Add(r.material);
            }
        }

        while (t < dissolveDuration)
        {
            t += Time.deltaTime;
            float v = Mathf.Lerp(0f, 1f, t / dissolveDuration);

            foreach (var mat in matsToDissolve)
            {
                if (mat != null)
                    mat.SetFloat("_DissolveAmount", v);
            }

            yield return null;
        }

        // Mort définitive
        Destroy(gameObject);
    }


    // ----------------------------------------------------------------------
    //  BODY CONNECTIVITY / LIMB CUT
    // ----------------------------------------------------------------------
    public void OnExternalSphereDestroyed(ExternalSphere sphere)
    {
        if (isDead) return;
        CheckConnectivity();
    }

    public void CheckConnectivity()
    {
        if (internalSphere == null) return;

        // 1) BFS for external spheres connected to internal
        HashSet<ExternalSphere> connectedBody = new HashSet<ExternalSphere>();

        foreach (var sphere in externalSpheres)
        {
            if (sphere == null || sphere.IsDestroyed) continue;

            if (IsTouchingInternal(sphere))
                connectedBody.Add(sphere);
        }

        Queue<ExternalSphere> queue = new Queue<ExternalSphere>(connectedBody);
        while (queue.Count > 0)
        {
            ExternalSphere current = queue.Dequeue();
            foreach (var neigh in externalSpheres)
            {
                if (neigh == null || neigh.IsDestroyed || neigh == current) continue;

                if (!connectedBody.Contains(neigh) && AreConnected(current, neigh))
                {
                    connectedBody.Add(neigh);
                    queue.Enqueue(neigh);
                }
            }
        }

        // ---------- SAFE SPHERE DESTRUCTION ----------
        List<ExternalSphere> toDestroy = new List<ExternalSphere>();

        foreach (var sphere in externalSpheres)
        {
            if (sphere == null || sphere.IsDestroyed) continue;
            if (!connectedBody.Contains(sphere))
                toDestroy.Add(sphere);
        }

        foreach (var sphere in toDestroy)
        {
            sphere.DestroySphere();
        }

        // 2) Tentacle segments amputated from core/body
        List<Transform> connectedNodes = new List<Transform>();

        foreach (var s in externalSpheres)
            if (s != null && !s.IsDestroyed && connectedBody.Contains(s))
                connectedNodes.Add(s.transform);

        if (internalSphere != null)
            connectedNodes.Add(internalSphere.transform);

        float maxLinkDist = overlapRadius * 2f;

        // ---------- SAFE TENTACLE DESTRUCTION ----------
        List<TentacleSegment> segsToKill = new List<TentacleSegment>();

        foreach (var seg in allTentacleSegments)
        {
            if (seg == null || !seg.gameObject.activeSelf) continue;

            bool foundConnection = false;
            foreach (var node in connectedNodes)
            {
                if (node == null) continue;
                if (Vector3.Distance(seg.transform.position, node.position) <= maxLinkDist)
                {
                    foundConnection = true;
                    break;
                }
            }

            if (!foundConnection)
            {
                segsToKill.Add(seg);
            }
        }

        foreach (var seg in segsToKill)
        {
            if (seg != null)
                seg.TakeDamage(99999f);
        }

        StartRegeneration();
    }

    private bool AreConnected(ExternalSphere a, ExternalSphere b)
    {
        float dist = Vector3.Distance(a.transform.position, b.transform.position);
        return dist <= overlapRadius * 2f;
    }

    private bool IsTouchingInternal(ExternalSphere sphere)
    {
        float dist = Vector3.Distance(sphere.transform.position, internalSphere.transform.position);
        return dist <= overlapRadius * 2f;
    }

    public void StartRegeneration()
    {
        if (!autoRegenerate || isDead) return;
        if (regenRoutine != null) StopCoroutine(regenRoutine);
        regenRoutine = StartCoroutine(RegenerateProcess());
    }

    private IEnumerator RegenerateProcess()
    {
        yield return new WaitForSeconds(1f);

        List<ExternalSphere> destroyed = new List<ExternalSphere>();
        foreach (var s in externalSpheres)
            if (s != null && s.IsDestroyed)
                destroyed.Add(s);

        destroyed.Sort((a, b) =>
            Vector3.Distance(a.transform.position, internalSphere.transform.position)
            .CompareTo(Vector3.Distance(b.transform.position, internalSphere.transform.position)));

        foreach (var sphere in destroyed)
        {
            sphere.RestoreSphere();
            yield return new WaitForSeconds(regenDelay);
        }
    }

    // ----------------------------------------------------------------------
    //  TENTACLE SPAWNING
    // ----------------------------------------------------------------------
    private void SpawnTentaclesAtRoots()
    {
        if (tentacleSegmentPrefab == null)
        {
            Debug.LogWarning("EnemiesGauna: tentacleSegmentPrefab not set, tentacles will not spawn.");
            return;
        }

        if (tentacleRoots.Count == 0) return;

        foreach (var rootSphere in tentacleRoots)
        {
            if (rootSphere == null) continue;
            CreateTentacleAt(rootSphere);
        }
    }

    private void CreateTentacleAt(ExternalSphere sphere)
    {
        GameObject rootObj = new GameObject("TentacleRoot_" + sphere.name);
        rootObj.transform.parent = transform;
        rootObj.transform.position = sphere.transform.position;

        TentacleRoot tr = rootObj.AddComponent<TentacleRoot>();
        tr.ownerGauna = this;
        tr.segmentPrefab = tentacleSegmentPrefab;
        tr.segmentSpacing = tentacleSegmentSpacing;
        tr.baseSegmentCount = 2;
        tr.maxSegments = 12;
        tr.initialSegments = initialTentacleSegments;
        tr.target = player;
        tr.engageDistance = tentacleEngageDistance;
        tr.disengageDistance = tentacleDisengageDistance;

        tr.segments = new List<Transform>();
        for (int i = 0; i < initialTentacleSegments; i++)
        {
            Vector3 pos = rootObj.transform.position + rootObj.transform.up * (i * tentacleSegmentSpacing);
            GameObject seg = Instantiate(tentacleSegmentPrefab, pos, Quaternion.identity, rootObj.transform);
            tr.segments.Add(seg.transform);

            TentacleSegment segComp = seg.GetComponent<TentacleSegment>();
            if (segComp != null)
            {
                segComp.ownerRoot = tr;
                allTentacleSegments.Add(segComp);
            }
        }

        tentacles.Add(tr);
    }
}
