using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BSCCityBuilder.Core;
using BSCCityBuilder.Generation;
using BSCCityBuilder.Management;
using BSCCityBuilder.Rendering;

namespace BSCCityBuilder.Editor.Roads
{
public static class CityRoadMeshGenerationHost
{
    private const string DefaultOutputName = "Generated Roads";

    public static string GetActiveEngineId(CityManager manager)
    {
        string id = manager != null ? manager.GetRoadMeshEngineId() : null;
        return string.IsNullOrWhiteSpace(id) ? "bsc.default-road-mesh" : id;
    }

    public static void SetActiveEngineId(CityManager manager, string engineId)
    {
        if (manager == null)
        {
            return;
        }
        Undo.RecordObject(manager, "Set Road Mesh Engine");
        manager.SetRoadMeshEngineId(engineId);
        EditorUtility.SetDirty(manager);
    }

    public static RoadMeshBuildResult Build(CityManager manager)
    {
        RoadNetworkBuildRequest request = CreateRequest(manager);
        if (request == null)
        {
            return Failure("CityManager o CityData non validi.");
        }

        IRoadMeshGenerationEngine engine = CityRoadMeshEngineRegistry.Create(GetActiveEngineId(manager));
        if (engine == null)
        {
            return Failure("Nessun motore di generazione stradale disponibile.");
        }

        return engine.Build(request) ?? Failure("Il motore non ha restituito un risultato.");
    }

    public static bool Clear(CityManager manager)
    {
        RoadNetworkBuildRequest request = CreateRequest(manager);
        IRoadMeshGenerationEngine engine = CityRoadMeshEngineRegistry.Create(GetActiveEngineId(manager));
        return request != null && engine != null && engine.Clear(request);
    }

    public static RoadNetworkBuildRequest CreateRequest(CityManager manager)
    {
        CityData data = manager != null ? manager.GetCityData() : null;
        if (data == null)
        {
            return null;
        }

        var paths = new List<RoadPathBuildData>();
        foreach (CitySegment segment in data.segments)
        {
            if (segment == null ||
                data.GetNode(segment.nodeA_ID) == null ||
                data.GetNode(segment.nodeB_ID) == null)
            {
                continue;
            }

            int samples = segment.IsCurved() ? 32 : 2;
            var points = new List<Vector3>(samples + 1);
            for (int i = 0; i <= samples; i++)
            {
                points.Add(CityRoadGeometry.EvaluatePoint(data, segment, i / (float)samples));
            }

            RoadProfile profile = segment.roadProfile != null
                ? segment.roadProfile
                : data.GetDefaultRoadProfile();
            paths.Add(new RoadPathBuildData
            {
                segmentId = segment.id,
                startNodeId = segment.nodeA_ID,
                endNodeId = segment.nodeB_ID,
                width = CityRoadGeometry.GetRoadWidth(data, segment),
                intersectionClearance = profile != null ? profile.intersectionClearanceRadius : 0f,
                profile = profile,
                material = profile != null ? profile.meshMaterial : null,
                points = points
            });
        }

        var junctions = new List<RoadJunctionBuildData>();
        foreach (CityNode node in data.nodes)
        {
            if (node == null)
            {
                continue;
            }
            junctions.Add(new RoadJunctionBuildData
            {
                nodeId = node.id,
                position = node.position,
                connectedSegmentIds = node.connectedSegmentIDs != null
                    ? new List<int>(node.connectedSegmentIDs)
                    : new List<int>()
            });
        }

        return new RoadNetworkBuildRequest
        {
            cityName = manager.name,
            outputRootName = DefaultOutputName,
            outputParent = manager.transform,
            sourceManager = manager,
            paths = paths,
            junctions = junctions
        };
    }

    private static RoadMeshBuildResult Failure(string message)
    {
        var result = new RoadMeshBuildResult();
        result.messages.Add(message);
        return result;
    }
}
}
