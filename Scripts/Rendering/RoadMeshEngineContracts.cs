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
    public readonly List<string> messages = new List<string>();

    public string ToMultilineString()
    {
        var lines = new List<string>
        {
            succeeded ? "Generazione completata." : "Generazione non completata.",
            "Strade: " + roadsGenerated,
            "Giunzioni: " + junctionsGenerated
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
