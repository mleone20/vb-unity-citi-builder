using UnityEngine;
using BSCCityBuilder.Rendering;
using BSCCityBuilder.Editor.Tools;

namespace BSCCityBuilder.Editor.Roads
{
[RoadMeshEngine(
    "bsc.default-road-mesh",
    "City Builder Mesh",
    "Genera strip mesh e giunzioni con il motore integrato.",
    Order = 0)]
public sealed class DefaultRoadMeshGenerationEngine : IRoadMeshGenerationEngine
{
    public RoadMeshBuildResult Build(RoadNetworkBuildRequest request)
    {
        var result = new RoadMeshBuildResult();
        if (request == null || request.sourceManager == null)
        {
            result.messages.Add("La richiesta non contiene il CityManager sorgente.");
            return result;
        }

        CityRoadMeshBuilder.Build(request.sourceManager, request.outputRootName, false);
        Transform output = request.sourceManager.transform.Find(request.outputRootName);
        result.outputRoot = output != null ? output.gameObject : null;
        result.roadsGenerated = request.paths != null ? request.paths.Count : 0;
        result.junctionsGenerated = CountJunctions(request);
        result.succeeded = result.outputRoot != null;
        return result;
    }

    public bool Clear(RoadNetworkBuildRequest request)
    {
        return request != null &&
               CityRoadMeshBuilder.DeleteMesh(request.sourceManager, request.outputRootName);
    }

    private static int CountJunctions(RoadNetworkBuildRequest request)
    {
        if (request.junctions == null)
        {
            return 0;
        }

        int count = 0;
        for (int i = 0; i < request.junctions.Count; i++)
        {
            if (request.junctions[i].connectedSegmentIds != null &&
                request.junctions[i].connectedSegmentIds.Count >= 2)
            {
                count++;
            }
        }
        return count;
    }
}
}
