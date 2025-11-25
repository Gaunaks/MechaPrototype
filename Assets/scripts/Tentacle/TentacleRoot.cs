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
    public float idleWobbleStrength = 0.8f;
    public float idleWobbleSpeed = 2.5f;

    // (Attack parameters kept if you want later, but not used now)
    [Header("Attack (currently not used for dash)")]
    public float attackDistance = 2.5f;
    public float attackCooldown = 0.9f;

    // Internal
    private List<Vector3> positions = new List<Vector3>();
    private List<Vector3> originalLocalPositions = new List<Vector3>();

    private bool activeToTarget = false;
    private float currentLength;      // current float length (segment count as a float)
    private float targetLength;       // desired length (between initialSegments and maxSegments)
    private float lastAttackTime = -999f; // unused now, but kept

    // *** NEW: Regen Cooldown Logic ***
    public float regenCooldown = 3.0f; // Time before a chopped tentacle can regrow
    private float lastChopTime = -999f;

    // ----------------------------------------------------------------------
    // INIT
    // ----------------------------------------------------------------------
    void Start()
    {
        // If no segments in inspector, build a simple chain
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
        if (segments.Count == 0)
            return;

        UpdateActivation();
        UpdateTargetLength();
        ApplyLengthChange();
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
        // When active → we want full length
        // When inactive → we want initial length
        float desired = activeToTarget ? maxSegments : initialSegments;
        targetLength = Mathf.Clamp(desired, initialSegments, maxSegments);

        // Smoothly approach targetLength (so expansion is one continuous motion)
        currentLength = Mathf.MoveTowards(currentLength, targetLength, lengthChangeSpeed * Time.deltaTime);
        currentLength = Mathf.Clamp(currentLength, initialSegments, maxSegments);
    }

    void ApplyLengthChange()
    {
        // we want number of physical segments = rounded currentLength
        int desiredCount = Mathf.RoundToInt(currentLength);
        desiredCount = Mathf.Clamp(desiredCount, initialSegments, maxSegments);

        if (segmentPrefab == null)
            return;

        // If we're inside the regen cooldown, prevent adding segments (no regrowth)
        if (desiredCount > segments.Count)
        {
            float timeSinceChop = Time.time - lastChopTime;
            if (timeSinceChop < regenCooldown)
            {
                // still cooling down — skip any growth until cooldown passes
                return;
            }
        }

        // Add or remove segments to match desiredCount
        if (desiredCount > segments.Count)
        {
            // need more segments
            int toAdd = desiredCount - segments.Count;
            for (int i = 0; i < toAdd; i++)
            {
                AddSegmentNearBase();
            }
        }
        else if (desiredCount < segments.Count)
        {
            // need fewer segments
            int toRemove = segments.Count - desiredCount;
            for (int i = 0; i < toRemove; i++)
            {
                RemoveSegmentAtBase();
            }
        }

        EnsureArraysMatch();
    }

    void AddSegmentNearBase()
    {
        // spawn very close to root, at its position, so visually it looks like it grows out
        Vector3 spawnPos = transform.position;
        GameObject segObj = Instantiate(segmentPrefab, spawnPos, Quaternion.identity, transform);

        // Insert at index 1 so index 0 is strictly root-adjacent, or at 0 if you prefer
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
        if (segments.Count <= baseSegmentCount)
            return; // keep minimal root

        // remove just after the root, so it visually retracts into the base
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

        // root anchored
        positions[0] = transform.position;

        // base segments: cling to original local positions (like a shoulder)
        for (int i = 1; i < Mathf.Min(baseSegmentCount, segments.Count); i++)
        {
            Vector3 goal = transform.TransformPoint(originalLocalPositions[i]);
            positions[i] = Vector3.Lerp(positions[i], goal, Time.deltaTime * smoothSpeed);
        }

        int last = segments.Count - 1;
        Vector3 desiredTip = positions[last];

        // main behavior: if active, reach toward target; else relax toward original shape
        if (target != null && activeToTarget)
        {
            Vector3 root = transform.position;
            Vector3 toTarget = target.position - root;
            float maxReach = segmentSpacing * (segments.Count - 1);

            if (toTarget.magnitude > maxReach)
                desiredTip = root + toTarget.normalized * maxReach;
            else
                desiredTip = target.position;
        }
        else
        {
            // retract into original rest positions
            for (int i = baseSegmentCount; i < segments.Count; i++)
            {
                Vector3 goal = transform.TransformPoint(originalLocalPositions[Mathf.Min(i, originalLocalPositions.Count - 1)]);
                positions[i] = Vector3.Lerp(positions[i], goal, Time.deltaTime * smoothSpeed);
            }

            desiredTip = positions[last];
        }

        // smooth tip
        positions[last] = Vector3.Lerp(positions[last], desiredTip, Time.deltaTime * reachSpeed);

        // backward pass: keep spacing
        for (int i = last - 1; i >= 1; i--)
        {
            Vector3 dir = (positions[i] - positions[i + 1]).normalized;
            positions[i] = positions[i + 1] + dir * segmentSpacing;
        }

        // forward pass from root
        positions[0] = transform.position;
        for (int i = 1; i < segments.Count; i++)
        {
            Vector3 dir = (positions[i] - positions[i - 1]).normalized;
            positions[i] = positions[i - 1] + dir * segmentSpacing;
        }

        // Idle wobble (stronger toward tip)
        float time = Time.time * idleWobbleSpeed;
        for (int i = baseSegmentCount; i < segments.Count; i++)
        {
            float factor = i / (float)Mathf.Max(1, segments.Count - 1);
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

            // slightly damped to keep motion smooth
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
    // DECAPITATION (unchanged logic)
    // ----------------------------------------------------------------------
    public void OnSegmentDestroyed(TentacleSegment seg)
    {
        if (seg == null) return;

        if (ownerGauna != null && ownerGauna.allTentacleSegments.Contains(seg))
            ownerGauna.allTentacleSegments.Remove(seg);

        int index = segments.IndexOf(seg.transform);
        if (index < 0) return;

        // remove this segment and everything AFTER it (toward the tip)
        for (int i = segments.Count - 1; i >= index; i--)
        {
            Transform t = segments[i];
            TentacleSegment ts = t.GetComponent<TentacleSegment>();

            if (ts != null && ownerGauna != null && ownerGauna.allTentacleSegments.Contains(ts))
                ownerGauna.allTentacleSegments.Remove(ts);

            segments.RemoveAt(i);

            if (i < positions.Count) positions.RemoveAt(i);
            if (i < originalLocalPositions.Count) originalLocalPositions.RemoveAt(i);

            if (t != null)
                Destroy(t.gameObject);
        }

        // update currentLength so it doesn’t try to regrow instantly
        currentLength = segments.Count;
        targetLength = Mathf.Clamp(targetLength, initialSegments, maxSegments);

        // record the chop time to start regen cooldown
        lastChopTime = Time.time;
    }
}