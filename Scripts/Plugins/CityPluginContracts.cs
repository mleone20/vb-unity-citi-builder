using UnityEngine;
using System;
using System.Collections.Generic;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Config;
using BSCCityBuilder.Generation;
using BSCCityBuilder.Components;

namespace BSCCityBuilder.Plugins
{
public enum CityPluginCategory
{
    Process,
    RoadNetwork,
    RoadPlanarization,
    BlockDetection,
    Zoning,
    LotLayout,
    LotSelection
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class CityPluginAttribute : Attribute
{
    public readonly string id;
    public readonly string displayName;
    public readonly CityPluginCategory category;
    public readonly string description;
    public string Version { get; set; } = "1.0.0";
    public int Order { get; set; }

    public CityPluginAttribute(string id, string displayName, CityPluginCategory category, string description = "")
    {
        this.id = id;
        this.displayName = displayName;
        this.category = category;
        this.description = description;
    }
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
public sealed class CityPluginDependencyAttribute : Attribute
{
    public string PluginId { get; }
    public bool Optional { get; set; }

    public CityPluginDependencyAttribute(string pluginId)
    {
        PluginId = pluginId;
    }
}

public interface ICityPlugin
{
    void Initialize(CityGenerationContext context);
}

public sealed class CityGenerationContext
{
    public CityManager manager;
    public CityData cityData;

    // Config asset del plugin process attivo (agnostico rispetto al tema città)
    public CityConfig config;
    public ILotSelectionPlugin lotSelectionPlugin;
    public IServiceProvider services;

    public T GetConfig<T>() where T : CityConfig
    {
        return config as T;
    }

}

public interface ICityPipelineStep
{
    string Id { get; }
    string DisplayName { get; }
    int Order { get; }
    bool CanExecute(CityGenerationContext context);
    CityGenerationReport Execute(CityGenerationContext context);
}

public interface ICityPipelineContributor
{
    IEnumerable<ICityPipelineStep> CreatePipelineSteps(CityGenerationContext context);
}

[Flags]
public enum CityProcessCapabilities
{
    None = 0,
    RoadNetwork = 1 << 0,
    Zoning = 1 << 1,
    LotGeneration = 1 << 2,
    FullGeneration = 1 << 3,
    All = ~0
}

public interface ICityProcessCapabilities
{
    CityProcessCapabilities Capabilities { get; }
}

public struct CityGenerationReport
{
    public int nodesCreated;
    public int segmentsCreated;
    public int blocksDetected;
    public int blocksZoned;
    public int lotsGenerated;
    public int planarizationSplits;
    public List<string> warnings;

    public void EnsureWarnings()
    {
        if (warnings == null)
        {
            warnings = new List<string>();
        }
    }

    public void Merge(CityGenerationReport other)
    {
        nodesCreated += other.nodesCreated;
        segmentsCreated += other.segmentsCreated;
        blocksDetected += other.blocksDetected;
        blocksZoned += other.blocksZoned;
        lotsGenerated += other.lotsGenerated;
        planarizationSplits += other.planarizationSplits;

        if (other.warnings != null && other.warnings.Count > 0)
        {
            EnsureWarnings();
            warnings.AddRange(other.warnings);
        }
    }

    public string ToMultilineString()
    {
        var lines = new List<string>();
        if (nodesCreated > 0 || segmentsCreated > 0)
        {
            lines.Add("Rete: " + nodesCreated + " nodi, " + segmentsCreated + " segmenti");
        }

        if (planarizationSplits > 0)
        {
            lines.Add("Planarizzazione: " + planarizationSplits + " split");
        }

        if (blocksDetected > 0)
        {
            lines.Add("Blocchi rilevati: " + blocksDetected);
        }

        if (blocksZoned > 0)
        {
            lines.Add("Blocchi zonati: " + blocksZoned);
        }

        if (lotsGenerated > 0)
        {
            lines.Add("Lotti generati: " + lotsGenerated);
        }

        if (warnings != null && warnings.Count > 0)
        {
            lines.Add("Warning (" + warnings.Count + "):");
            for (int i = 0; i < warnings.Count; i++)
            {
                lines.Add("  - " + warnings[i]);
            }
        }

        return string.Join("\n", lines.ToArray());
    }
}

public struct CityLotCandidate
{
    public GameObject prefab;
    public CityBuilderPrefab meta;
    public float weight;
}

public struct CityLotSelectionContext
{
    public int blockIndex;
    public int edgeIndex;
    public int lotIndex;
    public ZoneType zoneType;
    public List<CityLotCandidate> candidates;
}

public interface IRoadNetworkGenerationPlugin
{
    CityGenerationReport GenerateRoadNetwork(CityGenerationContext context);
}

public interface IRoadPlanarizationPlugin
{
    CityGenerationReport PlanarizeRoads(CityGenerationContext context);
}

public interface IBlockDetectionPlugin
{
    List<List<Vector3>> DetectBlocks(CityGenerationContext context);
}

public interface IZoningAssignmentPlugin
{
    CityGenerationReport AssignZoning(CityGenerationContext context);
}

public interface ILotLayoutPlugin
{
    List<CityLot> GenerateLotsForBlock(CityGenerationContext context, CityBlock block, int blockIndex);
}

public interface ILotSelectionPlugin
{
    int PickCandidateIndex(CityLotSelectionContext context);
}

public interface ICityProcessPlugin
{
    CityGenerationReport GenerateAll(CityGenerationContext context);
}

}
