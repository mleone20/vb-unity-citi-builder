using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BSCCityBuilder.Rendering;

namespace BSCCityBuilder.Editor.Roads
{
[RoadMeshEngine(
    "bsc.default-road-mesh",
    "City Builder Surface Mesher",
    "Ribbon con giunti miter e intersezioni geometriche anche senza nodi planarizzati.",
    Order = 0)]
public sealed class DefaultRoadMeshGenerationEngine : IRoadMeshGenerationEngine
{
    private const float PointEpsilon = 0.01f;
    private const float CrossingMergeDistance = 0.15f;
    private const float CrossingHeightTolerance = 0.75f;
    private const float SurfaceOffset = 0.015f;
    private const float JunctionOffset = 0.025f;
    private const float MiterLimit = 4f;

    private sealed class JunctionArm
    {
        public Vector3 direction;
        public float width;
        public float clearance;
        public Material material;
    }

    private sealed class Junction
    {
        public Vector3 center;
        public readonly List<JunctionArm> arms = new List<JunctionArm>();
    }

    private sealed class SegmentReference
    {
        public int id;
        public int pathIndex;
        public int segmentIndex;
    }

    public RoadMeshBuildResult Build(RoadNetworkBuildRequest request)
    {
        var result = new RoadMeshBuildResult();
        if (request == null || request.outputParent == null || request.paths == null)
        {
            result.messages.Add("Richiesta stradale incompleta.");
            return result;
        }

        Clear(request);

        GameObject root = new GameObject(request.outputRootName);
        root.transform.SetParent(request.outputParent, true);
        Undo.RegisterCreatedObjectUndo(root, "Generate Road Surfaces");

        var validPaths = new List<RoadPathBuildData>();
        for (int i = 0; i < request.paths.Count; i++)
        {
            RoadPathBuildData path = request.paths[i];
            List<Vector3> points = SanitizePoints(path.points);
            if (points.Count < 2)
            {
                result.messages.Add("Segmento " + path.segmentId + " ignorato: punti insufficienti.");
                continue;
            }
            if (path.material == null)
            {
                result.messages.Add("Segmento " + path.segmentId + " ignorato: materiale assente.");
                continue;
            }

            RoadPathBuildData cleanPath = CopyWithPoints(path, points);
            if (BuildRoadRibbon(cleanPath, root.transform))
            {
                validPaths.Add(cleanPath);
                result.roadsGenerated++;
            }
        }

        List<Junction> junctions = CollectGraphJunctions(request, validPaths);
        CollectGeometricCrossings(validPaths, junctions);

        for (int i = 0; i < junctions.Count; i++)
        {
            if (junctions[i].arms.Count >= 2 && BuildJunctionSurface(junctions[i], root.transform, i))
            {
                result.junctionsGenerated++;
            }
        }

        result.outputRoot = root;
        result.succeeded = result.roadsGenerated > 0;
        if (!result.succeeded)
        {
            result.messages.Add("Nessuna superficie stradale generata.");
        }
        return result;
    }

    public bool Clear(RoadNetworkBuildRequest request)
    {
        if (request == null || request.outputParent == null)
        {
            return false;
        }

        Transform existing = request.outputParent.Find(request.outputRootName);
        if (existing == null)
        {
            return false;
        }
        Undo.DestroyObjectImmediate(existing.gameObject);
        return true;
    }

    private static bool BuildRoadRibbon(RoadPathBuildData path, Transform parent)
    {
        IReadOnlyList<Vector3> points = path.points;
        int count = points.Count;
        float halfWidth = Mathf.Max(0.05f, path.width * 0.5f);
        var vertices = new Vector3[count * 2];
        var uvs = new Vector2[count * 2];
        var triangles = new int[(count - 1) * 6];

        float distance = 0f;
        for (int i = 0; i < count; i++)
        {
            Vector3 offset = CalculateMiterOffset(points, i, halfWidth);
            Vector3 center = points[i] + Vector3.up * SurfaceOffset;
            vertices[i * 2] = center - offset;
            vertices[i * 2 + 1] = center + offset;

            if (i > 0)
            {
                distance += Vector3.Distance(points[i - 1], points[i]);
            }
            float v = distance / Mathf.Max(1f, path.width);
            uvs[i * 2] = new Vector2(0f, v);
            uvs[i * 2 + 1] = new Vector2(1f, v);
        }

        int triangleIndex = 0;
        for (int i = 0; i < count - 1; i++)
        {
            int left = i * 2;
            int right = left + 1;
            int nextLeft = left + 2;
            int nextRight = left + 3;
            triangles[triangleIndex++] = left;
            triangles[triangleIndex++] = nextLeft;
            triangles[triangleIndex++] = right;
            triangles[triangleIndex++] = right;
            triangles[triangleIndex++] = nextLeft;
            triangles[triangleIndex++] = nextRight;
        }

        Mesh mesh = CreateMesh("RoadSurface_" + path.segmentId, vertices, triangles, uvs);
        GameObject road = new GameObject("Road_" + path.segmentId);
        road.transform.SetParent(parent, true);
        road.AddComponent<MeshFilter>().sharedMesh = mesh;
        road.AddComponent<MeshRenderer>().sharedMaterial = path.material;
        Undo.RegisterCreatedObjectUndo(road, "Generate Road Surface");
        return true;
    }

    private static Vector3 CalculateMiterOffset(IReadOnlyList<Vector3> points, int index, float halfWidth)
    {
        Vector3 nextDirection = index < points.Count - 1
            ? FlatDirection(points[index + 1] - points[index])
            : FlatDirection(points[index] - points[index - 1]);
        Vector3 previousDirection = index > 0
            ? FlatDirection(points[index] - points[index - 1])
            : nextDirection;

        Vector3 previousNormal = Vector3.Cross(Vector3.up, previousDirection).normalized;
        Vector3 nextNormal = Vector3.Cross(Vector3.up, nextDirection).normalized;
        Vector3 miter = previousNormal + nextNormal;
        if (miter.sqrMagnitude < 0.0001f)
        {
            return nextNormal * halfWidth;
        }

        miter.Normalize();
        float denominator = Vector3.Dot(miter, nextNormal);
        if (Mathf.Abs(denominator) < 0.1f)
        {
            return nextNormal * halfWidth;
        }

        float length = Mathf.Clamp(
            halfWidth / denominator,
            -halfWidth * MiterLimit,
            halfWidth * MiterLimit);
        return miter * length;
    }

    private static List<Junction> CollectGraphJunctions(
        RoadNetworkBuildRequest request,
        List<RoadPathBuildData> paths)
    {
        var result = new List<Junction>();
        if (request.junctions == null)
        {
            return result;
        }

        var pathsById = new Dictionary<int, RoadPathBuildData>();
        for (int i = 0; i < paths.Count; i++)
        {
            pathsById[paths[i].segmentId] = paths[i];
        }

        for (int i = 0; i < request.junctions.Count; i++)
        {
            RoadJunctionBuildData source = request.junctions[i];
            if (source.connectedSegmentIds == null || source.connectedSegmentIds.Count < 2)
            {
                continue;
            }

            var junction = new Junction { center = source.position };
            for (int s = 0; s < source.connectedSegmentIds.Count; s++)
            {
                RoadPathBuildData path;
                if (!pathsById.TryGetValue(source.connectedSegmentIds[s], out path))
                {
                    continue;
                }
                JunctionArm arm = CreateEndpointArm(path, source.nodeId);
                if (arm != null)
                {
                    junction.arms.Add(arm);
                }
            }
            if (junction.arms.Count >= 2)
            {
                result.Add(junction);
            }
        }
        return result;
    }

    private static JunctionArm CreateEndpointArm(RoadPathBuildData path, int nodeId)
    {
        if (path.points == null || path.points.Count < 2)
        {
            return null;
        }

        Vector3 direction;
        if (path.startNodeId == nodeId)
        {
            direction = FlatDirection(path.points[1] - path.points[0]);
        }
        else if (path.endNodeId == nodeId)
        {
            int last = path.points.Count - 1;
            direction = FlatDirection(path.points[last - 1] - path.points[last]);
        }
        else
        {
            return null;
        }

        return CreateArm(path, direction);
    }

    private static void CollectGeometricCrossings(
        List<RoadPathBuildData> paths,
        List<Junction> junctions)
    {
        float averageWidth = 0f;
        for (int i = 0; i < paths.Count; i++)
        {
            averageWidth += paths[i].width;
        }
        averageWidth = paths.Count > 0 ? averageWidth / paths.Count : 4f;
        float cellSize = Mathf.Max(10f, averageWidth * 4f);

        var cells = new Dictionary<Vector2Int, List<SegmentReference>>();
        int referenceId = 0;
        for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
        {
            RoadPathBuildData path = paths[pathIndex];
            for (int segmentIndex = 0; segmentIndex < path.points.Count - 1; segmentIndex++)
            {
                Vector3 a = path.points[segmentIndex];
                Vector3 b = path.points[segmentIndex + 1];
                int minX = Mathf.FloorToInt(Mathf.Min(a.x, b.x) / cellSize);
                int maxX = Mathf.FloorToInt(Mathf.Max(a.x, b.x) / cellSize);
                int minZ = Mathf.FloorToInt(Mathf.Min(a.z, b.z) / cellSize);
                int maxZ = Mathf.FloorToInt(Mathf.Max(a.z, b.z) / cellSize);
                var reference = new SegmentReference
                {
                    id = referenceId++,
                    pathIndex = pathIndex,
                    segmentIndex = segmentIndex
                };

                for (int x = minX; x <= maxX; x++)
                {
                    for (int z = minZ; z <= maxZ; z++)
                    {
                        var key = new Vector2Int(x, z);
                        List<SegmentReference> bucket;
                        if (!cells.TryGetValue(key, out bucket))
                        {
                            bucket = new List<SegmentReference>();
                            cells[key] = bucket;
                        }
                        bucket.Add(reference);
                    }
                }
            }
        }

        var testedPairs = new HashSet<ulong>();
        foreach (List<SegmentReference> bucket in cells.Values)
        {
            for (int a = 0; a < bucket.Count; a++)
            {
                SegmentReference segmentA = bucket[a];
                for (int b = a + 1; b < bucket.Count; b++)
                {
                    SegmentReference segmentB = bucket[b];
                    int minId = Mathf.Min(segmentA.id, segmentB.id);
                    int maxId = Mathf.Max(segmentA.id, segmentB.id);
                    ulong pairKey = ((ulong)(uint)minId << 32) | (uint)maxId;
                    if (!testedPairs.Add(pairKey) || AreAdjacent(paths, segmentA, segmentB))
                    {
                        continue;
                    }

                    RoadPathBuildData pathA = paths[segmentA.pathIndex];
                    RoadPathBuildData pathB = paths[segmentB.pathIndex];
                    Vector3 intersection;
                    if (!TrySegmentIntersectionXZ(
                        pathA.points[segmentA.segmentIndex],
                        pathA.points[segmentA.segmentIndex + 1],
                        pathB.points[segmentB.segmentIndex],
                        pathB.points[segmentB.segmentIndex + 1],
                        out intersection))
                    {
                        continue;
                    }

                    Junction junction = FindOrCreateJunction(junctions, intersection);
                    Vector3 directionA = FlatDirection(
                        pathA.points[segmentA.segmentIndex + 1] -
                        pathA.points[segmentA.segmentIndex]);
                    Vector3 directionB = FlatDirection(
                        pathB.points[segmentB.segmentIndex + 1] -
                        pathB.points[segmentB.segmentIndex]);
                    AddArmIfUnique(junction, CreateArm(pathA, directionA));
                    AddArmIfUnique(junction, CreateArm(pathA, -directionA));
                    AddArmIfUnique(junction, CreateArm(pathB, directionB));
                    AddArmIfUnique(junction, CreateArm(pathB, -directionB));
                }
            }
        }
    }

    private static bool AreAdjacent(
        List<RoadPathBuildData> paths,
        SegmentReference a,
        SegmentReference b)
    {
        if (a.pathIndex != b.pathIndex)
        {
            return false;
        }

        if (Mathf.Abs(a.segmentIndex - b.segmentIndex) <= 1)
        {
            return true;
        }

        int lastSegment = paths[a.pathIndex].points.Count - 2;
        return (a.segmentIndex == 0 && b.segmentIndex == lastSegment) ||
               (b.segmentIndex == 0 && a.segmentIndex == lastSegment);
    }

    private static bool TrySegmentIntersectionXZ(
        Vector3 a,
        Vector3 b,
        Vector3 c,
        Vector3 d,
        out Vector3 intersection)
    {
        intersection = default;
        Vector2 p = new Vector2(a.x, a.z);
        Vector2 r = new Vector2(b.x - a.x, b.z - a.z);
        Vector2 q = new Vector2(c.x, c.z);
        Vector2 s = new Vector2(d.x - c.x, d.z - c.z);
        float cross = Cross(r, s);
        if (Mathf.Abs(cross) < 0.00001f)
        {
            return false;
        }

        Vector2 qp = q - p;
        float t = Cross(qp, s) / cross;
        float u = Cross(qp, r) / cross;
        const float endpointTolerance = 0.0005f;
        if (t < -endpointTolerance || t > 1f + endpointTolerance ||
            u < -endpointTolerance || u > 1f + endpointTolerance)
        {
            return false;
        }

        float yA = Mathf.Lerp(a.y, b.y, t);
        float yB = Mathf.Lerp(c.y, d.y, u);
        if (Mathf.Abs(yA - yB) > CrossingHeightTolerance)
        {
            return false;
        }

        Vector2 xz = p + r * t;
        intersection = new Vector3(xz.x, (yA + yB) * 0.5f, xz.y);
        return true;
    }

    private static Junction FindOrCreateJunction(List<Junction> junctions, Vector3 position)
    {
        float threshold = CrossingMergeDistance * CrossingMergeDistance;
        for (int i = 0; i < junctions.Count; i++)
        {
            Vector3 delta = junctions[i].center - position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= threshold)
            {
                return junctions[i];
            }
        }

        var junction = new Junction { center = position };
        junctions.Add(junction);
        return junction;
    }

    private static bool BuildJunctionSurface(Junction junction, Transform parent, int index)
    {
        var corners = new List<Vector3>();
        float extension = 0f;
        Material material = null;
        float materialWidth = -1f;

        for (int i = 0; i < junction.arms.Count; i++)
        {
            JunctionArm arm = junction.arms[i];
            extension = Mathf.Max(extension, Mathf.Max(arm.width * 0.75f, arm.clearance));
            if (arm.material != null && arm.width > materialWidth)
            {
                material = arm.material;
                materialWidth = arm.width;
            }
        }
        if (material == null)
        {
            return false;
        }
        extension = Mathf.Max(extension, 0.25f);

        for (int i = 0; i < junction.arms.Count; i++)
        {
            JunctionArm arm = junction.arms[i];
            Vector3 normal = Vector3.Cross(Vector3.up, arm.direction).normalized;
            float halfWidth = arm.width * 0.5f;
            Vector3 end = junction.center + arm.direction * extension;
            corners.Add(end - normal * halfWidth);
            corners.Add(end + normal * halfWidth);
        }

        List<Vector3> hull = ConvexHullXZ(corners);
        if (hull.Count < 3)
        {
            return false;
        }

        var vertices = new Vector3[hull.Count + 1];
        var uvs = new Vector2[vertices.Length];
        var triangles = new int[hull.Count * 3];
        vertices[0] = junction.center + Vector3.up * JunctionOffset;
        uvs[0] = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < hull.Count; i++)
        {
            vertices[i + 1] = hull[i] + Vector3.up * JunctionOffset;
            Vector3 local = hull[i] - junction.center;
            uvs[i + 1] = new Vector2(
                0.5f + local.x / (extension * 2f),
                0.5f + local.z / (extension * 2f));

            int next = (i + 1) % hull.Count;
            triangles[i * 3] = 0;
            triangles[i * 3 + 1] = next + 1;
            triangles[i * 3 + 2] = i + 1;
        }

        Mesh mesh = CreateMesh("JunctionSurface_" + index, vertices, triangles, uvs);
        GameObject surface = new GameObject("Junction_" + index);
        surface.transform.SetParent(parent, true);
        surface.AddComponent<MeshFilter>().sharedMesh = mesh;
        surface.AddComponent<MeshRenderer>().sharedMaterial = material;
        Undo.RegisterCreatedObjectUndo(surface, "Generate Road Junction");
        return true;
    }

    private static JunctionArm CreateArm(RoadPathBuildData path, Vector3 direction)
    {
        return new JunctionArm
        {
            direction = direction,
            width = Mathf.Max(0.1f, path.width),
            clearance = Mathf.Max(0f, path.intersectionClearance),
            material = path.material
        };
    }

    private static void AddArmIfUnique(Junction junction, JunctionArm arm)
    {
        for (int i = 0; i < junction.arms.Count; i++)
        {
            if (Vector3.Dot(junction.arms[i].direction, arm.direction) > 0.995f &&
                Mathf.Abs(junction.arms[i].width - arm.width) < 0.05f)
            {
                return;
            }
        }
        junction.arms.Add(arm);
    }

    private static List<Vector3> SanitizePoints(IReadOnlyList<Vector3> source)
    {
        var result = new List<Vector3>();
        if (source == null)
        {
            return result;
        }

        float threshold = PointEpsilon * PointEpsilon;
        for (int i = 0; i < source.Count; i++)
        {
            if (result.Count == 0 || (source[i] - result[result.Count - 1]).sqrMagnitude > threshold)
            {
                result.Add(source[i]);
            }
        }
        return result;
    }

    private static RoadPathBuildData CopyWithPoints(RoadPathBuildData source, List<Vector3> points)
    {
        return new RoadPathBuildData
        {
            segmentId = source.segmentId,
            startNodeId = source.startNodeId,
            endNodeId = source.endNodeId,
            width = source.width,
            intersectionClearance = source.intersectionClearance,
            profile = source.profile,
            material = source.material,
            points = points
        };
    }

    private static Mesh CreateMesh(string name, Vector3[] vertices, int[] triangles, Vector2[] uvs)
    {
        var mesh = new Mesh
        {
            name = name,
            vertices = vertices,
            triangles = triangles,
            uv = uvs
        };
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private static Vector3 FlatDirection(Vector3 direction)
    {
        direction.y = 0f;
        return direction.sqrMagnitude > 0.000001f ? direction.normalized : Vector3.forward;
    }

    private static float Cross(Vector2 a, Vector2 b)
    {
        return a.x * b.y - a.y * b.x;
    }

    private static List<Vector3> ConvexHullXZ(List<Vector3> points)
    {
        points.Sort((left, right) =>
        {
            int x = left.x.CompareTo(right.x);
            return x != 0 ? x : left.z.CompareTo(right.z);
        });

        var hull = new List<Vector3>();
        for (int i = 0; i < points.Count; i++)
        {
            while (hull.Count >= 2 && HullCross(hull[hull.Count - 2], hull[hull.Count - 1], points[i]) <= 0f)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(points[i]);
        }

        int lowerCount = hull.Count;
        for (int i = points.Count - 2; i >= 0; i--)
        {
            while (hull.Count > lowerCount &&
                   HullCross(hull[hull.Count - 2], hull[hull.Count - 1], points[i]) <= 0f)
                hull.RemoveAt(hull.Count - 1);
            hull.Add(points[i]);
        }
        if (hull.Count > 1)
            hull.RemoveAt(hull.Count - 1);
        return hull;
    }

    private static float HullCross(Vector3 a, Vector3 b, Vector3 c)
    {
        return (b.x - a.x) * (c.z - a.z) - (b.z - a.z) * (c.x - a.x);
    }
}
}
