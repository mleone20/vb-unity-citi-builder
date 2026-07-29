using System;
using System.Collections.Generic;
using UnityEngine;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;

namespace BSCCityBuilder.Rendering
{
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RoadMeshEngineAttribute : Attribute
{
    public string Id { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public int Order { get; set; }

    public RoadMeshEngineAttribute(string id, string displayName, string description = "")
    {
        Id = id;
        DisplayName = displayName;
        Description = description;
    }
}

public sealed class RoadPathBuildData
{
    public int segmentId;
    public int startNodeId;
    public int endNodeId;
    public float width;
    public float intersectionClearance;
    public RoadProfile profile;
    public Material material;
    public IReadOnlyList<Vector3> points;
}

public sealed class RoadJunctionBuildData
{
    public int nodeId;
    public Vector3 position;
    public IReadOnlyList<int> connectedSegmentIds;
    public CityJunctionType junctionType;
    public float roundaboutIslandRadius;
    public float roundaboutCarriagewayWidth;
    public int roundaboutResolution;
    public Material roundaboutIslandMaterial;
    public bool generateRoundaboutIsland;

    public float RoundaboutOuterRadius =>
        Mathf.Max(1f, roundaboutIslandRadius) + Mathf.Max(2f, roundaboutCarriagewayWidth);

    public float GetRoundaboutConnectionRadius(float roadWidth)
    {
        float innerRadius = Mathf.Max(1f, roundaboutIslandRadius);
        float laneWidth = Mathf.Max(2f, roundaboutCarriagewayWidth);
        return Mathf.Min(
            innerRadius + laneWidth,
            innerRadius + Mathf.Max(laneWidth * 0.5f, Mathf.Max(0.1f, roadWidth) * 0.5f));
    }

    public bool IsRoundabout(int validArmCount)
    {
        return validArmCount >= 3 &&
               (junctionType == CityJunctionType.Roundabout ||
                junctionType == CityJunctionType.Auto);
    }
}

public sealed class RoadNetworkBuildRequest
{
    public string cityName;
    public string outputRootName;
    public Transform outputParent;
    public CityManager sourceManager;
    public IReadOnlyList<RoadPathBuildData> paths;
    public IReadOnlyList<RoadJunctionBuildData> junctions;
}

public sealed class RoadMeshBuildResult
{
    public bool succeeded;
    public GameObject outputRoot;
    public int roadsGenerated;
    public int junctionsGenerated;
    public int roundaboutsGenerated;
    public readonly List<string> messages = new List<string>();

    public string ToMultilineString()
    {
        var lines = new List<string>
        {
            succeeded ? "Generazione completata." : "Generazione non completata.",
            "Strade: " + roadsGenerated,
            "Giunzioni: " + junctionsGenerated,
            "Rotonde: " + roundaboutsGenerated
        };
        lines.AddRange(messages);
        return string.Join("\n", lines);
    }
}

public interface IRoadMeshGenerationEngine
{
    RoadMeshBuildResult Build(RoadNetworkBuildRequest request);
    bool Clear(RoadNetworkBuildRequest request);
}
}
