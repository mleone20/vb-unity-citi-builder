using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Algoritmo per rilevare blocchi (cicli) da un grafo stradale.
/// Processa nodi e segmenti per trovare poligoni chiusi.
/// </summary>
public static class CityBlockDetector
{
    /// <summary>
    /// Rileva tutti i blocchi (cicli elementari) dal grafo stradale.
    /// Restituisce lista di poligoni rappresentati come liste di Vector3.
    /// </summary>
    public static List<List<Vector3>> DetectBlocks(CityData cityData)
    {
        if (cityData == null || cityData.nodes.Count < 3)
        {
            return new List<List<Vector3>>();
        }

        Dictionary<int, List<int>> adjacency = BuildAdjacency(cityData);
        if (adjacency.Count == 0)
        {
            return new List<List<Vector3>>();
        }

        SortNeighborsCounterClockwise(adjacency, cityData);

        HashSet<string> visitedDirectedEdges = new HashSet<string>();
        HashSet<string> uniqueFaceSignatures = new HashSet<string>();
        List<List<int>> detectedFaces = new List<List<int>>();

        foreach (KeyValuePair<int, List<int>> pair in adjacency)
        {
            int from = pair.Key;
            foreach (int to in pair.Value)
            {
                string edgeKey = DirectedEdgeKey(from, to);
                if (visitedDirectedEdges.Contains(edgeKey))
                {
                    continue;
                }

                List<int> face = TraceFace(from, to, adjacency, visitedDirectedEdges);
                if (face == null || face.Count < 3)
                {
                    continue;
                }

                float signedArea = CalculateSignedArea(face, cityData);
                if (Mathf.Abs(signedArea) < 1.0f)
                {
                    continue;
                }

                string signature = CanonicalCycleSignature(face);
                if (uniqueFaceSignatures.Add(signature))
                {
                    detectedFaces.Add(face);
                }
            }
        }

        if (detectedFaces.Count == 0)
        {
            return new List<List<Vector3>>();
        }

        // Esclude la faccia esterna (area assoluta maggiore).
        int outerFaceIndex = -1;
        float maxArea = 0f;
        for (int i = 0; i < detectedFaces.Count; i++)
        {
            float area = Mathf.Abs(CalculateSignedArea(detectedFaces[i], cityData));
            if (area > maxArea)
            {
                maxArea = area;
                outerFaceIndex = i;
            }
        }

        List<List<Vector3>> result = new List<List<Vector3>>();
        for (int i = 0; i < detectedFaces.Count; i++)
        {
            if (i == outerFaceIndex)
            {
                continue;
            }

            List<Vector3> polygon = NodeIdCycleToPositions(detectedFaces[i], cityData);
            if (polygon.Count < 3)
            {
                continue;
            }

            // Evita riuso area: se il centro cade in un blocco già accettato, scarta.
            Vector3 centroid = ComputeCentroid(polygon);
            bool overlapsAcceptedArea = result.Any(existing => IsPointInPolygonXZ(centroid, existing));
            if (!overlapsAcceptedArea)
            {
                result.Add(polygon);
            }
        }

        return result;
    }

    private static Dictionary<int, List<int>> BuildAdjacency(CityData cityData)
    {
        Dictionary<int, List<int>> adjacency = new Dictionary<int, List<int>>();
        foreach (CitySegment segment in cityData.segments)
        {
            if (segment == null)
            {
                continue;
            }

            CityNode nodeA = cityData.GetNode(segment.nodeA_ID);
            CityNode nodeB = cityData.GetNode(segment.nodeB_ID);
            if (nodeA == null || nodeB == null || nodeA.id == nodeB.id)
            {
                continue;
            }

            if (!adjacency.ContainsKey(nodeA.id)) adjacency[nodeA.id] = new List<int>();
            if (!adjacency.ContainsKey(nodeB.id)) adjacency[nodeB.id] = new List<int>();

            if (!adjacency[nodeA.id].Contains(nodeB.id)) adjacency[nodeA.id].Add(nodeB.id);
            if (!adjacency[nodeB.id].Contains(nodeA.id)) adjacency[nodeB.id].Add(nodeA.id);
        }

        return adjacency;
    }

    private static void SortNeighborsCounterClockwise(Dictionary<int, List<int>> adjacency, CityData cityData)
    {
        foreach (KeyValuePair<int, List<int>> pair in adjacency)
        {
            int nodeId = pair.Key;
            CityNode node = cityData.GetNode(nodeId);
            if (node == null)
            {
                continue;
            }

            pair.Value.Sort((a, b) =>
            {
                CityNode nodeA = cityData.GetNode(a);
                CityNode nodeB = cityData.GetNode(b);
                if (nodeA == null || nodeB == null) return 0;

                Vector3 dirA = nodeA.position - node.position;
                Vector3 dirB = nodeB.position - node.position;
                float angA = Mathf.Atan2(dirA.z, dirA.x);
                float angB = Mathf.Atan2(dirB.z, dirB.x);
                return angA.CompareTo(angB);
            });
        }
    }

    private static List<int> TraceFace(
        int startFrom,
        int startTo,
        Dictionary<int, List<int>> adjacency,
        HashSet<string> visitedDirectedEdges)
    {
        List<int> face = new List<int>();
        int currentFrom = startFrom;
        int currentTo = startTo;
        int safety = 0;

        while (safety++ < 1024)
        {
            string key = DirectedEdgeKey(currentFrom, currentTo);
            if (!visitedDirectedEdges.Add(key))
            {
                if (currentFrom == startFrom && currentTo == startTo)
                {
                    break;
                }
                return null;
            }

            face.Add(currentFrom);

            if (!adjacency.TryGetValue(currentTo, out List<int> neighbors) || neighbors.Count < 2)
            {
                return null;
            }

            int incomingIndex = neighbors.IndexOf(currentFrom);
            if (incomingIndex < 0)
            {
                return null;
            }

            // Regola "mano sinistra": scegli il vicino precedente nell'ordine CCW.
            int nextIndex = (incomingIndex - 1 + neighbors.Count) % neighbors.Count;
            int nextTo = neighbors[nextIndex];

            currentFrom = currentTo;
            currentTo = nextTo;

            if (currentFrom == startFrom && currentTo == startTo)
            {
                break;
            }
        }

        if (safety >= 1024 || face.Count < 3)
        {
            return null;
        }

        return face;
    }

    private static List<Vector3> NodeIdCycleToPositions(List<int> cycleNodeIds, CityData cityData)
    {
        List<Vector3> points = new List<Vector3>();
        foreach (int nodeId in cycleNodeIds)
        {
            CityNode node = cityData.GetNode(nodeId);
            if (node == null)
            {
                return new List<Vector3>();
            }
            points.Add(node.position);
        }
        return points;
    }

    private static float CalculateSignedArea(List<int> cycleNodeIds, CityData cityData)
    {
        if (cycleNodeIds == null || cycleNodeIds.Count < 3)
        {
            return 0f;
        }

        float area = 0f;
        for (int i = 0; i < cycleNodeIds.Count; i++)
        {
            CityNode n1 = cityData.GetNode(cycleNodeIds[i]);
            CityNode n2 = cityData.GetNode(cycleNodeIds[(i + 1) % cycleNodeIds.Count]);
            if (n1 == null || n2 == null)
            {
                return 0f;
            }

            Vector3 v1 = n1.position;
            Vector3 v2 = n2.position;
            area += v1.x * v2.z - v2.x * v1.z;
        }

        return area * 0.5f;
    }

    private static string DirectedEdgeKey(int from, int to)
    {
        return from + "->" + to;
    }

    private static string CanonicalCycleSignature(List<int> cycle)
    {
        string forward = MinRotationSignature(cycle);
        List<int> reversed = new List<int>(cycle);
        reversed.Reverse();
        string backward = MinRotationSignature(reversed);
        return string.CompareOrdinal(forward, backward) < 0 ? forward : backward;
    }

    private static string MinRotationSignature(List<int> cycle)
    {
        int n = cycle.Count;
        string best = null;
        for (int start = 0; start < n; start++)
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            for (int i = 0; i < n; i++)
            {
                if (i > 0) sb.Append('-');
                sb.Append(cycle[(start + i) % n]);
            }

            string candidate = sb.ToString();
            if (best == null || string.CompareOrdinal(candidate, best) < 0)
            {
                best = candidate;
            }
        }

        return best ?? string.Empty;
    }

    private static Vector3 ComputeCentroid(List<Vector3> polygon)
    {
        Vector3 c = Vector3.zero;
        for (int i = 0; i < polygon.Count; i++)
        {
            c += polygon[i];
        }
        return c / polygon.Count;
    }

    private static bool IsPointInPolygonXZ(Vector3 point, List<Vector3> polygon)
    {
        if (polygon == null || polygon.Count < 3)
        {
            return false;
        }

        bool inside = false;
        for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
        {
            Vector3 pi = polygon[i];
            Vector3 pj = polygon[j];

            bool intersects = ((pi.z > point.z) != (pj.z > point.z)) &&
                              (point.x < (pj.x - pi.x) * (point.z - pi.z) / (pj.z - pi.z + Mathf.Epsilon) + pi.x);
            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
