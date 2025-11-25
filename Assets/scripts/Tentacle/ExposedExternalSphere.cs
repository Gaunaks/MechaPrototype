using System.Collections.Generic;
using UnityEngine;


public static class ExposedSphereUtility
{
    public static List<ExternalSphere> GetExposedSpheres(List<ExternalSphere> spheres, float minOpenAngle = 60f)
    {
        List<ExternalSphere> exposed = new List<ExternalSphere>();

        foreach (var s in spheres)
        {
            if (s.IsDestroyed) continue;

            Vector3 pos = s.transform.position;
            Vector3 outwards = (pos - s.parent.internalSphere.transform.position).normalized;

            int hits = 0;
            int samples = 8;

            for (int i = 0; i < samples; i++)
            {
                float angle = (360f / samples) * i;
                Vector3 dir = Quaternion.AngleAxis(angle, outwards) * outwards;

                if (Physics.Raycast(pos, dir, out RaycastHit hit, 0.8f))
                {
                    if (hit.collider.GetComponent<ExternalSphere>() != null)
                        hits++;
                }
            }

            float openFraction = 1f - (float)hits / samples;

            if (openFraction > 0.5f)
                exposed.Add(s);
        }

        return exposed;
    }
}
