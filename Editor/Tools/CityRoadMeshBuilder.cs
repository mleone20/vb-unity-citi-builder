using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Generation;
using BSCCityBuilder.Config;
using BSCCityBuilder.Rendering;
using BSCCityBuilder.Plugins;

namespace BSCCityBuilder.Editor.Tools
{
/// <summary>
/// Genera mesh Unity reali (MeshFilter + MeshRenderer) per tutta la rete stradale
/// memorizzata in un CityManager.
///
/// SEGMENTI  — quad-strip campionata sulla Bezier (32 step per curve, 2 per diritti).
///             L'inizio e la fine sono arretrati di intersectionClearanceRadius per
///             lasciare spazio alla giunzione.
///
/// GIUNZIONI — per ogni nodo con 2+ segmenti si usa il bordo reale delle strip
///             (estremi insetted), si calcola un hull convesso in XZ e si
///             triangola in double-sided per evitare problemi di culling.
/// </summary>
public static class CityRoadMeshBuilder
{
    private const string RootName    = "CityRoadMesh";
    private const int BezierSamples  = 32;   // step per segmenti curvi
    private const int StraightSamples = 2;   // step per segmenti diritti

    private struct NodeEdge
    {
        public Vector3 left;
        public Vector3 right;
        public Material material;
        public float width;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Entry point pubblico
    // ─────────────────────────────────────────────────────────────────────────

    [System.Obsolete("Usare CityRoadMeshGenerationHost con un IRoadMeshGenerationEngine.")]
    public static void Build(CityManager manager)
    {
        Build(manager, RootName);
    }

    public static void Build(CityManager manager, string rootName)
    {
        Build(manager, rootName, true);
    }

    public static void Build(CityManager manager, string rootName, bool showDialog)
    {
        if (manager == null)
        {
            Debug.LogError("[CityRoadMeshBuilder] CityManager non valido.");
            return;
        }

        CityData cityData = manager.GetCityData();
        if (cityData == null)
        {
            Debug.LogError("[CityRoadMeshBuilder] CityData non trovato su CityManager.");
            return;
        }

        if (cityData.segments == null || cityData.segments.Count == 0)
        {
            if (showDialog)
                EditorUtility.DisplayDialog("Genera Mesh Strade", "Nessun segmento stradale trovato in CityData.", "OK");
            return;
        }

        // Rimuove l'eventuale root precedente
        string safeRootName = string.IsNullOrWhiteSpace(rootName) ? RootName : rootName;
        Transform existingTransform = manager.transform.Find(safeRootName);
        GameObject existingRoot = existingTransform != null ? existingTransform.gameObject : null;
        if (existingRoot != null)
            Undo.DestroyObjectImmediate(existingRoot);

        GameObject root = new GameObject(safeRootName);
        root.transform.SetParent(manager.transform, true);
        Undo.RegisterCreatedObjectUndo(root, "Genera Mesh Strade");

        // nodeID → lista dei bordi strip usati per costruire la giunzione.
        var nodeEdges = new Dictionary<int, List<NodeEdge>>();

        int segmentCount = 0;
        foreach (CitySegment segment in cityData.segments)
        {
            if (segment == null) continue;
            if (cityData.GetNode(segment.nodeA_ID) == null) continue;
            if (cityData.GetNode(segment.nodeB_ID) == null) continue;

            if (BuildSegmentMesh(cityData, segment, root.transform, nodeEdges))
                segmentCount++;
        }

        int junctionCount = 0;
        if (cityData.nodes != null)
        {
            foreach (CityNode node in cityData.nodes)
            {
                if (node == null) continue;
                if (node.connectedSegmentIDs == null || node.connectedSegmentIDs.Count < 2) continue;

                List<NodeEdge> edges;
                if (!nodeEdges.TryGetValue(node.id, out edges)) continue;
                if (edges.Count < 2) continue;

                if (BuildJunctionMesh(node, edges, root.transform))
                    junctionCount++;
            }
        }

        if (showDialog)
            EditorUtility.DisplayDialog("Genera Mesh Strade",
                $"Generazione completata.\nSegmenti: {segmentCount}\nGiunzioni: {junctionCount}", "OK");
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Cleanup
    // ─────────────────────────────────────────────────────────────────────────

    [System.Obsolete("Usare CityRoadMeshGenerationHost.Clear.")]
    public static void DeleteMesh()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing == null)
        {
            EditorUtility.DisplayDialog("Cancella Mesh Strade",
                "Nessun GameObject \"" + RootName + "\" trovato nella scena.", "OK");
            return;
        }
        Undo.DestroyObjectImmediate(existing);
    }

    public static bool DeleteMesh(CityManager manager, string rootName)
    {
        if (manager == null)
        {
            return false;
        }

        string safeRootName = string.IsNullOrWhiteSpace(rootName) ? RootName : rootName;
        Transform existing = manager.transform.Find(safeRootName);
        if (existing == null)
        {
            return false;
        }

        Undo.DestroyObjectImmediate(existing.gameObject);
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Mesh di segmento (quad-strip)
    // ─────────────────────────────────────────────────────────────────────────

    private static bool BuildSegmentMesh(
        CityData cityData,
        CitySegment segment,
        Transform parent,
        Dictionary<int, List<NodeEdge>> nodeEdges)
    {
        int estSamples = segment.IsCurved() ? BezierSamples : StraightSamples;
        float segmentLength = CityRoadGeometry.EstimateLength(cityData, segment, estSamples);
        if (segmentLength < 0.01f) return false;

        float halfWidth = CityRoadGeometry.GetRoadWidth(cityData, segment) * 0.5f;
        Material segmentMaterial = ResolveSegmentMaterial(cityData, segment);
        if (segmentMaterial == null)
        {
            Debug.LogWarning($"[CityRoadMeshBuilder] Segmento {segment.id} senza materiale mesh nel RoadProfile. Segmento saltato.");
            return false;
        }

        float clearance = segment.roadProfile != null
            ? segment.roadProfile.intersectionClearanceRadius
            : 0f;

        float tInsetA = Mathf.Clamp(clearance / segmentLength, 0f, 0.45f);
        float tInsetB = tInsetA;

        if (tInsetA + tInsetB >= 0.9f) return false; // troppo corto, coperto dalle giunzioni

        float tStart = tInsetA;
        float tEnd   = 1f - tInsetB;

        // Campionamento adattivo: più step per le curve
        int steps = segment.IsCurved() ? BezierSamples : StraightSamples;
        int pointCount = steps + 1;

        Vector3[] centers  = new Vector3[pointCount];
        Vector3[] tangents = new Vector3[pointCount];
        for (int i = 0; i < pointCount; i++)
        {
            float t = Mathf.Lerp(tStart, tEnd, i / (float)steps);
            centers[i]  = CityRoadGeometry.EvaluatePoint(cityData, segment, t);
            tangents[i] = CityRoadGeometry.EvaluateTangent(cityData, segment, t);
        }

        int vertCount = pointCount * 2;
        int triCount  = (pointCount - 1) * 2;

        Vector3[] vertices  = new Vector3[vertCount];
        int[]     triangles = new int[triCount * 3];
        Vector2[] uvs       = new Vector2[vertCount];

        // Calcola la V in base alla distanza reale per un UV plausibile
        float totalLength = 0f;
        for (int i = 1; i < pointCount; i++)
            totalLength += Vector3.Distance(centers[i - 1], centers[i]);
        float invLen = totalLength > 0.001f ? 1f / totalLength : 1f;

        float accumLen = 0f;
        for (int i = 0; i < pointCount; i++)
        {
            if (i > 0) accumLen += Vector3.Distance(centers[i - 1], centers[i]);

            Vector3 tan   = tangents[i];
            Vector3 right = Vector3.Cross(Vector3.up, tan);
            if (right.sqrMagnitude < 0.0001f) right = Vector3.right;
            else right.Normalize();

            vertices[i * 2]     = centers[i] - right * halfWidth;  // bordo sinistro
            vertices[i * 2 + 1] = centers[i] + right * halfWidth;  // bordo destro

            float v = accumLen * invLen;
            uvs[i * 2]     = new Vector2(0f, v);
            uvs[i * 2 + 1] = new Vector2(1f, v);
        }

        // Quad-strip: winding CW dall'alto (faccia verso +Y)
        // Verticeing: left[i]=i*2, right[i]=i*2+1
        // Triangolo A: (left[i], left[i+1], right[i])
        // Triangolo B: (right[i], left[i+1], right[i+1])
        int ti = 0;
        for (int i = 0; i < pointCount - 1; i++)
        {
            int ll = i * 2;
            int lr = i * 2 + 1;
            int rl = (i + 1) * 2;
            int rr = (i + 1) * 2 + 1;

            triangles[ti++] = ll; triangles[ti++] = rl; triangles[ti++] = lr;
            triangles[ti++] = lr; triangles[ti++] = rl; triangles[ti++] = rr;
        }

        // Registra i bordi reali ai due estremi strip per costruire giunzioni precise.
        AddNodeEdge(nodeEdges, segment.nodeA_ID, new NodeEdge
        {
            left = vertices[0],
            right = vertices[1],
            material = segmentMaterial,
            width = halfWidth * 2f
        });
        AddNodeEdge(nodeEdges, segment.nodeB_ID, new NodeEdge
        {
            left = vertices[(pointCount - 1) * 2],
            right = vertices[(pointCount - 1) * 2 + 1],
            material = segmentMaterial,
            width = halfWidth * 2f
        });

        Mesh mesh = new Mesh();
        mesh.name      = "SegmentMesh_" + segment.id;
        mesh.vertices  = vertices;
        mesh.triangles = triangles;
        mesh.uv        = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject go = new GameObject("Segment_" + segment.id);
        go.transform.SetParent(parent, worldPositionStays: true);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = segmentMaterial;
        Undo.RegisterCreatedObjectUndo(go, "Genera Mesh Strade");
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Mesh di giunzione da bordo reale strip + hull convesso
    // ─────────────────────────────────────────────────────────────────────────

    private static bool BuildJunctionMesh(
        CityNode node,
        List<NodeEdge> edges,
        Transform parent)
    {
        Vector3 center = node.position;

        Material junctionMaterial = ResolveJunctionMaterial(edges);
        if (junctionMaterial == null)
        {
            return false;
        }

        List<Vector3> points = new List<Vector3>(edges.Count * 2);
        for (int i = 0; i < edges.Count; i++)
        {
            Vector3 l = edges[i].left;
            Vector3 r = edges[i].right;
            l.y = center.y;
            r.y = center.y;
            points.Add(l);
            points.Add(r);
        }

        RemoveNearDuplicates(points, 0.03f);
        if (points.Count < 3)
        {
            return false;
        }

        List<Vector3> hull = BuildConvexHullXZ(points);
        if (hull == null || hull.Count < 3)
        {
            return false;
        }

        int n = hull.Count;

        // vertices[0] = centro, vertices[1..n] = hull
        Vector3[] vertices = new Vector3[n + 1];
        Vector2[] uvs = new Vector2[n + 1];

        // Double-sided: n triangoli fronte + n retro
        int[] triangles = new int[n * 6];

        vertices[0] = center;
        uvs[0]      = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < n; i++)
        {
            vertices[i + 1] = hull[i];
            float ang = Mathf.Atan2(hull[i].z - center.z, hull[i].x - center.x);
            uvs[i + 1] = new Vector2(Mathf.Cos(ang) * 0.5f + 0.5f,
                                      Mathf.Sin(ang) * 0.5f + 0.5f);

            int next = (i + 1) % n;

            // Fronte
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = i + 1;
            triangles[i * 3 + 2] = next + 1;

            // Retro (winding invertito)
            int backBase = n * 3 + i * 3;
            triangles[backBase] = 0;
            triangles[backBase + 1] = next + 1;
            triangles[backBase + 2] = i + 1;
        }

        Mesh mesh = new Mesh();
        mesh.name      = "JunctionMesh_" + node.id;
        mesh.vertices  = vertices;
        mesh.triangles = triangles;
        mesh.uv        = uvs;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject go = new GameObject("Junction_" + node.id);
        go.transform.SetParent(parent, worldPositionStays: true);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = junctionMaterial;
        Undo.RegisterCreatedObjectUndo(go, "Genera Mesh Strade");
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static void AddNodeEdge(
        Dictionary<int, List<NodeEdge>> dict,
        int nodeID,
        NodeEdge edge)
    {
        List<NodeEdge> list;
        if (!dict.TryGetValue(nodeID, out list))
        {
            list = new List<NodeEdge>();
            dict[nodeID] = list;
        }
        list.Add(edge);
    }

    private static void RemoveNearDuplicates(List<Vector3> points, float tolerance)
    {
        if (points == null || points.Count < 2)
        {
            return;
        }

        float sq = tolerance * tolerance;
        for (int i = points.Count - 1; i >= 0; i--)
        {
            for (int j = i - 1; j >= 0; j--)
            {
                if ((points[i] - points[j]).sqrMagnitude <= sq)
                {
                    points.RemoveAt(i);
                    break;
                }
            }
        }
    }

    private static List<Vector3> BuildConvexHullXZ(List<Vector3> points)
    {
        if (points == null || points.Count < 3)
        {
            return null;
        }

        List<Vector3> sorted = new List<Vector3>(points);
        sorted.Sort((a, b) =>
        {
            int cmpX = a.x.CompareTo(b.x);
            if (cmpX != 0) return cmpX;
            return a.z.CompareTo(b.z);
        });

        List<Vector3> lower = new List<Vector3>();
        for (int i = 0; i < sorted.Count; i++)
        {
            while (lower.Count >= 2 && CrossXZ(lower[lower.Count - 2], lower[lower.Count - 1], sorted[i]) <= 0f)
            {
                lower.RemoveAt(lower.Count - 1);
            }
            lower.Add(sorted[i]);
        }

        List<Vector3> upper = new List<Vector3>();
        for (int i = sorted.Count - 1; i >= 0; i--)
        {
            while (upper.Count >= 2 && CrossXZ(upper[upper.Count - 2], upper[upper.Count - 1], sorted[i]) <= 0f)
            {
                upper.RemoveAt(upper.Count - 1);
            }
            upper.Add(sorted[i]);
        }

        if (lower.Count > 0) lower.RemoveAt(lower.Count - 1);
        if (upper.Count > 0) upper.RemoveAt(upper.Count - 1);

        lower.AddRange(upper);
        return lower;
    }

    private static float CrossXZ(Vector3 a, Vector3 b, Vector3 c)
    {
        float abx = b.x - a.x;
        float abz = b.z - a.z;
        float acx = c.x - a.x;
        float acz = c.z - a.z;
        return abx * acz - abz * acx;
    }

    private static Material ResolveSegmentMaterial(CityData cityData, CitySegment segment)
    {
        if (segment != null && segment.roadProfile != null && segment.roadProfile.meshMaterial != null)
        {
            return segment.roadProfile.meshMaterial;
        }

        if (cityData != null && cityData.defaultRoadProfile != null && cityData.defaultRoadProfile.meshMaterial != null)
        {
            return cityData.defaultRoadProfile.meshMaterial;
        }

        return null;
    }

    private static Material ResolveJunctionMaterial(List<NodeEdge> edges)
    {
        if (edges == null || edges.Count == 0)
        {
            return null;
        }

        Material selected = null;
        float widest = -1f;
        for (int i = 0; i < edges.Count; i++)
        {
            if (edges[i].material == null)
            {
                continue;
            }

            if (edges[i].width > widest)
            {
                widest = edges[i].width;
                selected = edges[i].material;
            }
        }

        return selected;
    }
}

}
