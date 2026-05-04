using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public static class CityGenerationPipelineHost
{
    public static CityGenerationReport GenerateRoadNetwork(CityManager manager, AmericanCityConfig config)
    {
        CityGenerationContext context = BuildContext(manager, config);
        CityPluginSettings settings = CityPluginSettingsEditorUtility.GetOrCreateSettings();
        ILotSelectionPlugin lotSelection = CityPluginRegistry.Create<ILotSelectionPlugin>(CityPluginCategory.LotSelection, settings.activeLotSelectionPluginId);
        CityPluginRuntime.SetLotSelectionPlugin(lotSelection);

        IRoadNetworkGenerationPlugin roadPlugin = CityPluginRegistry.Create<IRoadNetworkGenerationPlugin>(CityPluginCategory.RoadNetwork, settings.activeRoadNetworkPluginId);
        if (roadPlugin == null)
        {
            return BuildMissingPluginReport("RoadNetwork");
        }

        CityGenerationReport report = roadPlugin.GenerateRoadNetwork(context);
        if (settings.runPlanarizationAfterRoadNetwork)
        {
            report.Merge(PlanarizeRoads(manager, config));
        }

        return report;
    }

    public static CityGenerationReport PlanarizeRoads(CityManager manager, AmericanCityConfig config)
    {
        CityGenerationContext context = BuildContext(manager, config);
        CityPluginSettings settings = CityPluginSettingsEditorUtility.GetOrCreateSettings();

        IRoadPlanarizationPlugin planarizer = CityPluginRegistry.Create<IRoadPlanarizationPlugin>(CityPluginCategory.RoadPlanarization, settings.activeRoadPlanarizationPluginId);
        if (planarizer == null)
        {
            return BuildMissingPluginReport("RoadPlanarization");
        }

        return planarizer.PlanarizeRoads(context);
    }

    public static List<List<Vector3>> DetectBlocks(CityManager manager, AmericanCityConfig config)
    {
        CityGenerationContext context = BuildContext(manager, config);
        CityPluginSettings settings = CityPluginSettingsEditorUtility.GetOrCreateSettings();

        IBlockDetectionPlugin detector = CityPluginRegistry.Create<IBlockDetectionPlugin>(CityPluginCategory.BlockDetection, settings.activeBlockDetectionPluginId);
        if (detector == null)
        {
            return new List<List<Vector3>>();
        }

        return detector.DetectBlocks(context);
    }

    public static CityGenerationReport AssignZoning(CityManager manager, AmericanCityConfig config)
    {
        CityGenerationContext context = BuildContext(manager, config);
        CityPluginSettings settings = CityPluginSettingsEditorUtility.GetOrCreateSettings();

        IZoningAssignmentPlugin zoningPlugin = CityPluginRegistry.Create<IZoningAssignmentPlugin>(CityPluginCategory.Zoning, settings.activeZoningPluginId);
        if (zoningPlugin == null)
        {
            return BuildMissingPluginReport("Zoning");
        }

        return zoningPlugin.AssignZoning(context);
    }

    public static int GenerateLots(CityManager manager, AmericanCityConfig config)
    {
        CityGenerationContext context = BuildContext(manager, config);
        CityPluginSettings settings = CityPluginSettingsEditorUtility.GetOrCreateSettings();

        ILotLayoutPlugin layoutPlugin = CityPluginRegistry.Create<ILotLayoutPlugin>(CityPluginCategory.LotLayout, settings.activeLotLayoutPluginId);
        ILotSelectionPlugin lotSelection = CityPluginRegistry.Create<ILotSelectionPlugin>(CityPluginCategory.LotSelection, settings.activeLotSelectionPluginId);
        CityPluginRuntime.SetLotSelectionPlugin(lotSelection);

        if (layoutPlugin == null || context.cityData == null)
        {
            return 0;
        }

        context.cityData.lots.Clear();
        foreach (CityBlock block in context.cityData.blocks)
        {
            if (block != null)
            {
                block.lotIDs.Clear();
            }
        }

        int lotCount = 0;
        for (int i = 0; i < context.cityData.blocks.Count; i++)
        {
            CityBlock block = context.cityData.blocks[i];
            if (block == null)
            {
                continue;
            }

            List<CityLot> generated = layoutPlugin.GenerateLotsForBlock(context, block, i);
            for (int l = 0; l < generated.Count; l++)
            {
                CityLot lot = generated[l];
                lot.id = context.cityData.GetNextLotID();
                context.cityData.lots.Add(lot);
                block.lotIDs.Add(lot.id);
                lotCount++;
            }
        }

        EditorUtility.SetDirty(context.cityData);
        SceneView.RepaintAll();
        return lotCount;
    }

    public static CityGenerationReport GenerateAll(CityManager manager, ScriptableObject processConfig)
    {
        CityGenerationContext context = BuildContext(manager, processConfig);
        CityPluginSettings settings = CityPluginSettingsEditorUtility.GetOrCreateSettings();

        ICityProcessPlugin processPlugin = CityPluginRegistry.Create<ICityProcessPlugin>(CityPluginCategory.Process, settings.activeProcessPluginId);
        if (processPlugin != null)
        {
            return processPlugin.GenerateAll(context);
        }

        return GenerateAllWithCurrentStepPlugins(context, settings);
    }

    public static CityGenerationReport GenerateAll(CityManager manager, AmericanCityConfig config)
    {
        return GenerateAll(manager, (ScriptableObject)config);
    }

    public static CityGenerationReport GenerateAllWithCurrentStepPlugins(CityGenerationContext context, CityPluginSettings settings)
    {
        CityGenerationReport total = new CityGenerationReport { warnings = new List<string>() };

        CityGenerationReport road = GenerateRoadNetwork(context.manager, context.config);
        total.Merge(road);

        if (!settings.runPlanarizationAfterRoadNetwork && settings.runPlanarizationInFullGeneration)
        {
            CityGenerationReport planarize = PlanarizeRoads(context.manager, context.config);
            total.Merge(planarize);
        }

        if (context.cityData == null)
        {
            total.EnsureWarnings();
            total.warnings.Add("CityData non assegnato.");
            return total;
        }

        Undo.RecordObject(context.cityData, "Generate All: Clear Blocks");
        foreach (CityBlock block in context.cityData.blocks)
        {
            if (block != null)
            {
                block.lotIDs.Clear();
            }
        }

        context.cityData.blocks.Clear();
        context.cityData.lots.Clear();
        EditorUtility.SetDirty(context.cityData);

        List<List<Vector3>> detected = DetectBlocks(context.manager, context.config);
        for (int i = 0; i < detected.Count; i++)
        {
            context.manager.AddBlock(detected[i]);
        }
        total.blocksDetected += detected.Count;

        CityGenerationReport zoning = AssignZoning(context.manager, context.config);
        total.Merge(zoning);

        int lots = GenerateLots(context.manager, context.config);
        total.lotsGenerated += lots;

        return total;
    }

    private static CityGenerationContext BuildContext(CityManager manager, AmericanCityConfig config)
    {
        return BuildContext(manager, (ScriptableObject)config);
    }

    private static CityGenerationContext BuildContext(CityManager manager, ScriptableObject processConfig)
    {
        CityData data = manager != null ? manager.GetCityData() : null;
        AmericanCityConfig americanConfig = processConfig as AmericanCityConfig;
        return new CityGenerationContext
        {
            manager = manager,
            cityData = data,
            processConfig = processConfig,
            config = americanConfig
        };
    }

    private static CityGenerationReport BuildMissingPluginReport(string category)
    {
        CityGenerationReport report = new CityGenerationReport { warnings = new List<string>() };
        report.warnings.Add("Nessun plugin disponibile per categoria " + category + ".");
        return report;
    }
}
