using UnityEngine;
using System.Collections.Generic;

public static class CityRoadGeometry
{
    public const int DefaultCurveSamples = 16;

    public static float GetRoadWidth(CityData cityData, CitySegment segment)
    {
        if (segment == null)
        {
            return cityData != null ? cityData.globalRoadWidth : 3.0f;
        }

        return segment.GetConfiguredWidth(cityData != null ? cityData.globalRoadWidth : 3.0f);
    }

    public static Color GetRoadColor(CitySegment segment)
    {
        if (segment != null && segment.roadProfile != null)
        {
            return segment.roadProfile.debugColor;
        }

        return new Color(0.5f, 0.5f, 0.5f, 0.85f);
    }

    public static void ResetBezierHandles(CityData cityData, CitySegment segment)
    {
        if (cityData == null || segment == null)
        {
            return;
        }

        CityNode nodeA = cityData.GetNode(segment.nodeA_ID);
        CityNode nodeB = cityData.GetNode(segment.nodeB_ID);
        if (nodeA == null || nodeB == null)
        {
            return;
        }

        segment.ResetBezierHandles(nodeA.position, nodeB.position);
    }

    public static Vector3 EvaluatePoint(CityData cityData, CitySegment segment, float t)
    {
        if (cityData == null || segment == null)
        {
            return Vector3.zero;
        }

        CityNode nodeA = cityData.GetNode(segment.nodeA_ID);
        CityNode nodeB = cityData.GetNode(segment.nodeB_ID);
        if (nodeA == null || nodeB == null)
        {
            return Vector3.zero;
        }

        t = Mathf.Clamp01(t);
        if (!segment.IsCurved())
        {
            return Vector3.Lerp(nodeA.position, nodeB.position, t);
        }

        float inv = 1f - t;
        Vector3 p0 = nodeA.position;
        Vector3 p1 = segment.controlPointA;
        Vector3 p2 = segment.controlPointB;
        Vector3 p3 = nodeB.position;

        return inv * inv * inv * p0 +
               3f * inv * inv * t * p1 +
               3f * inv * t * t * p2 +
               t * t * t * p3;
    }

    public static Vector3 EvaluateTangent(CityData cityData, CitySegment segment, float t)
    {
        if (cityData == null || segment == null)
        {
            return Vector3.forward;
        }

        CityNode nodeA = cityData.GetNode(segment.nodeA_ID);
        CityNode nodeB = cityData.GetNode(segment.nodeB_ID);
        if (nodeA == null || nodeB == null)
        {
            return Vector3.forward;
        }

        if (!segment.IsCurved())
        {
            Vector3 linear = nodeB.position - nodeA.position;
            return linear.sqrMagnitude > 0.0001f ? linear.normalized : Vector3.forward;
        }

        t = Mathf.Clamp01(t);
        float inv = 1f - t;
        Vector3 tangent =
            3f * inv * inv * (segment.controlPointA - nodeA.position) +
            6f * inv * t * (segment.controlPointB - segment.controlPointA) +
            3f * t * t * (nodeB.position - segment.controlPointB);

        return tangent.sqrMagnitude > 0.0001f ? tangent.normalized : Vector3.forward;
    }

    public static List<Vector3> SampleSegment(CityData cityData, CitySegment segment, int curveSamples = DefaultCurveSamples)
    {
        List<Vector3> sampledPoints = new List<Vector3>();
        if (cityData == null || segment == null)
        {
            return sampledPoints;
        }

        CityNode nodeA = cityData.GetNode(segment.nodeA_ID);
        CityNode nodeB = cityData.GetNode(segment.nodeB_ID);
        if (nodeA == null || nodeB == null)
        {
            return sampledPoints;
        }

        if (!segment.IsCurved())
        {
            sampledPoints.Add(nodeA.position);
            sampledPoints.Add(nodeB.position);
            return sampledPoints;
        }

        int sampleCount = Mathf.Max(4, curveSamples);
        for (int i = 0; i <= sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            sampledPoints.Add(EvaluatePoint(cityData, segment, t));
        }

        return sampledPoints;
    }

    public static float EstimateLength(CityData cityData, CitySegment segment, int curveSamples = DefaultCurveSamples)
    {
        List<Vector3> sampledPoints = SampleSegment(cityData, segment, curveSamples);
        float length = 0f;
        for (int i = 1; i < sampledPoints.Count; i++)
        {
            length += Vector3.Distance(sampledPoints[i - 1], sampledPoints[i]);
        }

        return length;
    }

    public static float DistancePointToSegmentPathXZ(CityData cityData, CitySegment segment, Vector3 point, int curveSamples = DefaultCurveSamples)
    {
        List<Vector3> sampledPoints = SampleSegment(cityData, segment, curveSamples);
        if (sampledPoints.Count < 2)
        {
            return float.MaxValue;
        }

        float minDistance = float.MaxValue;
        for (int i = 1; i < sampledPoints.Count; i++)
        {
            float distance = DistancePointToLineSegmentXZ(point, sampledPoints[i - 1], sampledPoints[i]);
            if (distance < minDistance)
            {
                minDistance = distance;
            }
        }

        return minDistance;
    }

    public static float DistancePointToLineSegmentXZ(Vector3 point, Vector3 start, Vector3 end)
    {
        Vector2 p = new Vector2(point.x, point.z);
        Vector2 a = new Vector2(start.x, start.z);
        Vector2 b = new Vector2(end.x, end.z);
        Vector2 ab = b - a;

        float magnitude = ab.sqrMagnitude;
        if (magnitude <= 0.0001f)
        {
            return Vector2.Distance(p, a);
        }

        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / magnitude);
        Vector2 projection = a + ab * t;
        return Vector2.Distance(p, projection);
    }
}