using UnityEngine;
using System.Collections.Generic;

public struct CityIntersectionCandidate
{
    public Vector3 position;
    public int segmentAID;
    public int segmentBID;

    public CityIntersectionCandidate(Vector3 position, int segmentAID, int segmentBID)
    {
        this.position = position;
        this.segmentAID = segmentAID;
        this.segmentBID = segmentBID;
    }
}

public static class CityIntersectionUtility
{
    public static List<CityIntersectionCandidate> DetectIntersections(CityData cityData, int curveSamples = CityRoadGeometry.DefaultCurveSamples, float mergeThreshold = 0.5f)
    {
        List<CityIntersectionCandidate> results = new List<CityIntersectionCandidate>();
        if (cityData == null || cityData.segments == null)
        {
            return results;
        }

        for (int i = 0; i < cityData.segments.Count; i++)
        {
            CitySegment segmentA = cityData.segments[i];
            if (segmentA == null)
            {
                continue;
            }

            for (int j = i + 1; j < cityData.segments.Count; j++)
            {
                CitySegment segmentB = cityData.segments[j];
                if (segmentB == null || SharesEndpoint(segmentA, segmentB))
                {
                    continue;
                }

                List<Vector3> pathA = CityRoadGeometry.SampleSegment(cityData, segmentA, curveSamples);
                List<Vector3> pathB = CityRoadGeometry.SampleSegment(cityData, segmentB, curveSamples);
                if (pathA.Count < 2 || pathB.Count < 2)
                {
                    continue;
                }

                Vector3 intersectionPoint;
                if (TryFindPolylineIntersection(pathA, pathB, out intersectionPoint) && !ContainsNearby(results, intersectionPoint, mergeThreshold))
                {
                    results.Add(new CityIntersectionCandidate(intersectionPoint, segmentA.id, segmentB.id));
                }
            }
        }

        return results;
    }

    private static bool SharesEndpoint(CitySegment a, CitySegment b)
    {
        return a.nodeA_ID == b.nodeA_ID ||
               a.nodeA_ID == b.nodeB_ID ||
               a.nodeB_ID == b.nodeA_ID ||
               a.nodeB_ID == b.nodeB_ID;
    }

    private static bool ContainsNearby(List<CityIntersectionCandidate> candidates, Vector3 point, float mergeThreshold)
    {
        float sqrThreshold = mergeThreshold * mergeThreshold;
        for (int i = 0; i < candidates.Count; i++)
        {
            if ((candidates[i].position - point).sqrMagnitude <= sqrThreshold)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryFindPolylineIntersection(List<Vector3> pathA, List<Vector3> pathB, out Vector3 intersectionPoint)
    {
        for (int i = 1; i < pathA.Count; i++)
        {
            Vector3 a0 = pathA[i - 1];
            Vector3 a1 = pathA[i];

            for (int j = 1; j < pathB.Count; j++)
            {
                Vector3 b0 = pathB[j - 1];
                Vector3 b1 = pathB[j];

                if (TryIntersectSegmentsXZ(a0, a1, b0, b1, out intersectionPoint))
                {
                    return true;
                }
            }
        }

        intersectionPoint = Vector3.zero;
        return false;
    }

    private static bool TryIntersectSegmentsXZ(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, out Vector3 intersectionPoint)
    {
        Vector2 p = new Vector2(a0.x, a0.z);
        Vector2 r = new Vector2(a1.x - a0.x, a1.z - a0.z);
        Vector2 q = new Vector2(b0.x, b0.z);
        Vector2 s = new Vector2(b1.x - b0.x, b1.z - b0.z);

        float rxs = Cross(r, s);
        float qpxr = Cross(q - p, r);

        if (Mathf.Abs(rxs) < 0.0001f && Mathf.Abs(qpxr) < 0.0001f)
        {
            intersectionPoint = Vector3.zero;
            return false;
        }

        if (Mathf.Abs(rxs) < 0.0001f)
        {
            intersectionPoint = Vector3.zero;
            return false;
        }

        float t = Cross(q - p, s) / rxs;
        float u = Cross(q - p, r) / rxs;

        if (t < 0f || t > 1f || u < 0f || u > 1f)
        {
            intersectionPoint = Vector3.zero;
            return false;
        }

        Vector2 hit = p + t * r;
        float height = (a0.y + a1.y + b0.y + b1.y) * 0.25f;
        intersectionPoint = new Vector3(hit.x, height, hit.y);
        return true;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }
}