using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class TentacleRoot : MonoBehaviour
{
    [Header("Owner")]
    public EnemiesGauna ownerGauna;

    [Header("Structure")]
    public GameObject segmentPrefab;
    public List<Transform> segments = new List<Transform>(); // will be generated if empty
    public int baseSegmentCount = 2;       // stiff base attached near the body
    public int initialSegments = 4;        // minimum length
    public int maxSegments = 16;           // maximum length
    public float segmentSpacing = 0.6f;

    [Header("Target / Distance")]
    public Transform target;
    public float engageDistance = 10f;     // player enters → start expanding
    public float disengageDistance = 13f;  // player leaves → start retracting

    [Header("Length Animation")]
    public float lengthChangeSpeed = 8f;   // how fast target length changes (segments/sec)
    public float segmentSpawnLerpSpeed = 12f; // how fast new segments slide into place

    [Header("Movement")]
    public float reachSpeed = 10f;
    public float smoothSpeed = 15f;

    [Header("Idle / Style")]
    public float idleWobbleStrength = 2.0f;
    public float idleWobbleSpeed = 2.5f;

    [Header("Repulsion & Personality (Anti-Overlap)")]
    [Tooltip("How strongly it pushes away from other tentacles")]
    public float repulsionStrength = 5f;
    [Tooltip("The minimum comfortable distance between two tentacle segments")]
    public float repulsionRadius = 1.2f;
    [Tooltip("Adds random offset to the target so they don't aim at the exact same pixel")]
    public float targetSpreadRandomness = 1.5f; 
    
    // Personality variables
    private Vector3 targetOffsetSpread; 
    private float randomTimeOffset;
    private float wobbleSpeedMultiplier;

    // (Attack parameters kept if you want later, but not used now)
    [Header("Attack (currently not used for dash)")]
    public float attackDistance = 2.5f;
    public float attackCooldown = 0.9f;

    // Internal
    private List<Vector3> positions = new List<Vector3>();
    private List<Vector3> originalLocalPositions = new List<Vector3>();

    private bool activeToTarget = false;
    private float currentLength;      
    private float targetLength;       
    private float lastAttackTime = -999f; 

    // Regen Cooldown Logic
    public float regenCooldown = 3.0f; 
    private float lastChopTime = -999f;

    // ----------------------------------------------------------------------
    // INIT
    // ----------------------------------------------------------------------
    void Start()
    {
        // Setup random personality for this specific tentacle
        targetOffsetSpread = Random.onUnitSphere * targetSpreadRandomness;
        randomTimeOffset = Random.Range(0f, 100f); // Décalage dans le temps pour briser la synchro
        wobbleSpeedMultiplier = Random.Range(0.8f, 1.2f); // Légères variations de vitesse (+/- 20%)

        if (segments.Count == 0 && segmentPrefab != null)
        {
            for (int i = 0; i < initialSegments; i++)
            {
                Vector3 pos = transform.position + transform.up * (i * segmentSpacing);
                GameObject seg = Instantiate(segmentPrefab, pos, Quaternion.identity, transform);
                segments.Add(seg.transform);

                TentacleSegment ts = seg.GetComponent<TentacleSegment>();
                if (ts != null)
                {
                    ts.ownerRoot = this;
                    if (ownerGauna != null && !ownerGauna.allTentacleSegments.Contains(ts))
                        ownerGauna.allTentacleSegments.Add(ts);
                }
            }
        }

        positions.Clear();
        originalLocalPositions.Clear();
        for (int i = 0; i < segments.Count; i++)
        {
            positions.Add(segments[i].position);
            originalLocalPositions.Add(segments[i].localPosition);
        }

        currentLength = segments.Count;
        targetLength = segments.Count;
    }

    void Update()
    {
        if (segments == null) segments = new List<Transform>();

        UpdateActivation();
        UpdateTargetLength();
        ApplyLengthChange();

        if (segments.Count == 0)
            return;

        AnimateTentacle();
        ApplyPositions();
    }

    void EnsureArraysMatch()
    {
        while (positions.Count < segments.Count) positions.Add(transform.position);
        while (originalLocalPositions.Count < segments.Count) originalLocalPositions.Add(Vector3.zero);

        while (positions.Count > segments.Count) positions.RemoveAt(positions.Count - 1);
        while (originalLocalPositions.Count > segments.Count) originalLocalPositions.RemoveAt(originalLocalPositions.Count - 1);
    }

    // ----------------------------------------------------------------------
    // ACTIVATION / TARGET LENGTH
    // ----------------------------------------------------------------------
    void UpdateActivation()
    {
        if (target == null)
        {
            activeToTarget = false;
            return;
        }

        float dist = Vector3.Distance(transform.position, target.position);

        if (!activeToTarget && dist <= engageDistance)
            activeToTarget = true;
        else if (activeToTarget && dist >= disengageDistance)
            activeToTarget = false;
    }

    void UpdateTargetLength()
    {
        float desired = activeToTarget ? maxSegments : initialSegments;
        targetLength = Mathf.Clamp(desired, initialSegments, maxSegments);

        currentLength = Mathf.MoveTowards(currentLength, targetLength, lengthChangeSpeed * Time.deltaTime);
        currentLength = Mathf.Clamp(currentLength, initialSegments, maxSegments);
    }

    void ApplyLengthChange()
    {
        int desiredCount = Mathf.RoundToInt(currentLength);
        desiredCount = Mathf.Clamp(desiredCount, initialSegments, maxSegments);

        if (segmentPrefab == null)
            return;

        if (desiredCount > segments.Count)
        {
            float timeSinceChop = Time.time - lastChopTime;
            if (timeSinceChop < regenCooldown) return;
        }

        if (desiredCount > segments.Count)
        {
            int toAdd = desiredCount - segments.Count;
            for (int i = 0; i < toAdd; i++)
            {
                AddSegmentNearBase();
            }
        }
        else if (desiredCount < segments.Count)
        {
            int toRemove = segments.Count - desiredCount;
            for (int i = 0; i < toRemove; i++)
            {
                if (segments.Count <= initialSegments) break;
                RemoveSegmentAtBase();
            }
        }

        EnsureArraysMatch();
    }

    void AddSegmentNearBase()
    {
        if (segmentPrefab == null) return;

        Vector3 spawnPos = transform.position;
        GameObject segObj = Instantiate(segmentPrefab, spawnPos, Quaternion.identity, transform);

        int insertIndex = Mathf.Min(1, segments.Count);
        segments.Insert(insertIndex, segObj.transform);

        positions.Insert(insertIndex, segObj.transform.position);
        originalLocalPositions.Insert(insertIndex, segObj.transform.localPosition);

        TentacleSegment segComp = segObj.GetComponent<TentacleSegment>();
        if (segComp != null)
        {
            segComp.ownerRoot = this;
            if (ownerGauna != null && !ownerGauna.allTentacleSegments.Contains(segComp))
                ownerGauna.allTentacleSegments.Add(segComp);
        }
    }

    void RemoveSegmentAtBase()
    {
        if (segments.Count <= baseSegmentCount) return;
        if (segments.Count <= initialSegments) return;

        int index = Mathf.Min(1, segments.Count - 1);
        Transform removed = segments[index];

        segments.RemoveAt(index);
        if (index < positions.Count) positions.RemoveAt(index);
        if (index < originalLocalPositions.Count) originalLocalPositions.RemoveAt(index);

        TentacleSegment segComp = removed.GetComponent<TentacleSegment>();
        if (segComp != null && ownerGauna != null && ownerGauna.allTentacleSegments.Contains(segComp))
            ownerGauna.allTentacleSegments.Remove(segComp);

        Destroy(removed.gameObject);
    }

    // ----------------------------------------------------------------------
    // ANIMATION
    // ----------------------------------------------------------------------
    void AnimateTentacle()
    {
        if (segments.Count == 0) return;

        EnsureArraysMatch();

        // 1. Root anchored
        positions[0] = transform.position;

        // 2. Base segments cling
        for (int i = 1; i < Mathf.Min(baseSegmentCount, segments.Count); i++)
        {
            Vector3 goal = transform.TransformPoint(originalLocalPositions[i]);
            positions[i] = Vector3.Lerp(positions[i], goal, Time.deltaTime * smoothSpeed);
        }

        int last = segments.Count - 1;
        Vector3 desiredTip = positions[last];

        // 3. TARGETING
        if (target != null && activeToTarget)
        {
            Vector3 root = transform.position;
            Vector3 targetFocusPoint = target.position + targetOffsetSpread; 
            Vector3 toTarget = targetFocusPoint - root;
            float maxReach = segmentSpacing * (segments.Count - 1);

            if (toTarget.magnitude > maxReach)
                desiredTip = root + toTarget.normalized * maxReach;
            else
                desiredTip = targetFocusPoint;
        }
        else
        {
            for (int i = baseSegmentCount; i < segments.Count; i++)
            {
                Vector3 goal = transform.TransformPoint(originalLocalPositions[Mathf.Min(i, originalLocalPositions.Count - 1)]);
                positions[i] = Vector3.Lerp(positions[i], goal, Time.deltaTime * smoothSpeed);
            }
            desiredTip = positions[last];
        }

        // 4. Smooth tip
        positions[last] = Vector3.Lerp(positions[last], desiredTip, Time.deltaTime * reachSpeed);

        // 5. BODY REPULSION
        if (ownerGauna != null)
        {
            float bodyRepulsionRadius = repulsionRadius * 1.5f;

            for (int i = baseSegmentCount; i <= last; i++)
            {
                Vector3 repulsionVector = Vector3.zero;
                Vector3 myPos = positions[i];

                foreach (var otherSeg in ownerGauna.allTentacleSegments)
                {
                    if (otherSeg == null) continue;
                    if (otherSeg.ownerRoot == this) continue; 

                    Vector3 dirToOther = myPos - otherSeg.transform.position;
                    float sqrDist = dirToOther.sqrMagnitude;

                    if (sqrDist < (repulsionRadius * repulsionRadius) && sqrDist > 0.001f)
                    {
                        float dist = Mathf.Sqrt(sqrDist);
                        float pushForce = (repulsionRadius - dist) / repulsionRadius;
                        repulsionVector += dirToOther.normalized * pushForce;
                    }
                }

                foreach (var extSphere in ownerGauna.externalSpheres)
                {
                    if (extSphere == null || extSphere.IsDestroyed) continue;

                    Vector3 dirToSphere = myPos - extSphere.transform.position;
                    float sqrDist = dirToSphere.sqrMagnitude;

                    if (sqrDist < (bodyRepulsionRadius * bodyRepulsionRadius) && sqrDist > 0.001f)
                    {
                        float dist = Mathf.Sqrt(sqrDist);
                        float pushForce = ((bodyRepulsionRadius - dist) / bodyRepulsionRadius) * 1.5f; 
                        repulsionVector += dirToSphere.normalized * pushForce;
                    }
                }

                if (ownerGauna.internalSphere != null)
                {
                    Vector3 dirToCore = myPos - ownerGauna.internalSphere.transform.position;
                    float sqrDist = dirToCore.sqrMagnitude;
                    
                    if (sqrDist < (bodyRepulsionRadius * bodyRepulsionRadius) && sqrDist > 0.001f)
                    {
                        float dist = Mathf.Sqrt(sqrDist);
                        float pushForce = ((bodyRepulsionRadius - dist) / bodyRepulsionRadius) * 2f; 
                        repulsionVector += dirToCore.normalized * pushForce;
                    }
                }

                positions[i] += repulsionVector * (repulsionStrength * Time.deltaTime);
            }
        }


        // 6. BACKWARD PASS (Keep spacing constraints)
        for (int i = last - 1; i >= 1; i--)
        {
            Vector3 dir = (positions[i] - positions[i + 1]).normalized;
            positions[i] = positions[i + 1] + dir * segmentSpacing;
        }

        // 7. FORWARD PASS (Keep spacing constraints from root)
        positions[0] = transform.position;
        for (int i = 1; i < segments.Count; i++)
        {
            Vector3 dir = (positions[i] - positions[i - 1]).normalized;
            positions[i] = positions[i - 1] + dir * segmentSpacing;
        }

        // 8. IDLE WOBBLE WITH RANDOM OFFSET
        // Ajout du mix de variables de "personnalité" pour briser la synchronisation :
        float time = (Time.time + randomTimeOffset) * (idleWobbleSpeed * wobbleSpeedMultiplier);
        
        for (int i = baseSegmentCount; i < segments.Count; i++)
        {
            float factor = i / (float)Mathf.Max(1, segments.Count - 1);
            
            // Les ondes sont déformées différemment pour ce tentacule
            Vector3 wobble = new Vector3(
                Mathf.Sin(time + i * 0.35f),
                Mathf.Cos(time * 0.9f + i * 0.27f),
                Mathf.Sin(time * 0.65f + i * 0.73f)
            ) * idleWobbleStrength * factor;

            positions[i] += wobble * Time.deltaTime;
        }
    }

    void ApplyPositions()
    {
        for (int i = 0; i < segments.Count; i++)
        {
            Vector3 cur = segments[i].position;
            Vector3 goal = positions[i];

            segments[i].position = Vector3.Lerp(cur, goal, Time.deltaTime * segmentSpawnLerpSpeed);

            if (i > 0)
            {
                Vector3 dir = positions[i] - positions[i - 1];
                if (dir.sqrMagnitude > 0.0001f)
                {
                    segments[i].rotation = Quaternion.Lerp(
                        segments[i].rotation,
                        Quaternion.LookRotation(dir),
                        Time.deltaTime * smoothSpeed
                    );
                }
            }
        }
    }

    // ----------------------------------------------------------------------
    // DECAPITATION / REGEN
    // ----------------------------------------------------------------------
    public void OnSegmentDestroyed(TentacleSegment seg)
    {
        if (seg == null) return;

        if (ownerGauna != null && ownerGauna.allTentacleSegments.Contains(seg))
            ownerGauna.allTentacleSegments.Remove(seg);

        int index = segments.IndexOf(seg.transform);
        if (index < 0) return;

        for (int i = segments.Count - 1; i >= index; i--)
        {
            Transform t = segments[i];
            TentacleSegment ts = t.GetComponent<TentacleSegment>();

            if (ts != null && ownerGauna != null && ownerGauna.allTentacleSegments.Contains(ts))
                ownerGauna.allTentacleSegments.Remove(ts);

            segments.RemoveAt(i);
            if (i < positions.Count) positions.RemoveAt(i);
            if (i < originalLocalPositions.Count) originalLocalPositions.RemoveAt(i);

            if (t != null) Destroy(t.gameObject);
        }

        currentLength = segments.Count;
        targetLength = Mathf.Clamp(targetLength, initialSegments, maxSegments);
        lastChopTime = Time.time;
    }

    public IEnumerator RegenerateSegmentCoroutine(float delay)
    {
        yield return new WaitForSeconds(delay);

        float timeSinceChop = Time.time - lastChopTime;
        if (timeSinceChop < regenCooldown)
            yield return new WaitForSeconds(regenCooldown - timeSinceChop);

        if (segmentPrefab != null && segments.Count < maxSegments)
        {
            AddSegmentNearBase();
            EnsureArraysMatch();
        }
    }
}