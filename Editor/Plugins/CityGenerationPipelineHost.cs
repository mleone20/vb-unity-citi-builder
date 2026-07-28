using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Config;
using BSCCityBuilder.Generation;
using BSCCityBuilder.Plugins;

namespace BSCCityBuilder.Editor.Plugins
{
public static class CityGenerationPipelineHost
{
    public static CityGenerationReport GenerateRoadNetwork(CityManager manager, ScriptableObject config)
    {
        CityGenerationContext context = BuildContext(manager, config);
        CityPluginSettings settings = CityPluginSettingsEditorUtility.GetOrCreateSettings();
        IRoadNetworkGenerationPlugin roadPlugin = CityPluginRegistry.Create<IRoadNetworkGenerationPlugin>(CityPluginCategory.RoadNetwork, settings.GetActivePluginId(CityPluginCategory.RoadNetwork));
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

    public static CityGenerationReport PlanarizeRoads(CityManager manager, ScriptableObject config)
    {
        CityGenerationContext context = BuildContext(manager, config);
        CityPluginSettings settings = CityPluginSettingsEditorUtility.GetOrCreateSettings();

        IRoadPlanarizationPlugin planarizer = CityPluginRegistry.Create<IRoadPlanarizationPlugin>(CityPluginCategory.RoadPlanarization, settings.GetActivePluginId(CityPluginCategory.RoadPlanarization));
        if (planarizer == null)
        {
            return BuildMissingPluginReport("RoadPlanarization");
        }

        return planarizer.PlanarizeRoads(context);
    }

    public static List<List<Vector3>> DetectBlocks(CityManager manager, ScriptableObject config)
    {
        CityGenerationContext context = BuildContext(manager, config);
        CityPluginSettings settings = CityPluginSettingsEditorUtility.GetOrCreateSettings();

        IBlockDetectionPlugin detector = CityPluginRegistry.Create<IBlockDetectionPlugin>(CityPluginCategory.BlockDetection, settings.GetActivePluginId(CityPluginCategory.BlockDetection));
        if (detector == null)
        {
            return new List<List<Vector3>>();
        }

        return detector.DetectBlocks(context);
    }

    public static CityGenerationReport AssignZoning(CityManager manager, ScriptableObject config)
    {
        CityGenerationContext context = BuildContext(manager, config);
        CityPluginSettings settings = CityPluginSettingsEditorUtility.GetOrCreateSettings();

        IZoningAssignmentPlugin zoningPlugin = CityPluginRegistry.Create<IZoningAssignmentPlugin>(CityPluginCategory.Zoning, settings.GetActivePluginId(CityPluginCategory.Zoning));
        if (zoningPlugin == null)
        {
            return BuildMissingPluginReport("Zoning");
        }

        return zoningPlugin.AssignZoning(context);
    }

    public static int GenerateLots(CityManager manager, ScriptableObject config)
    {
        CityGenerationContext context = BuildContext(manager, config);
        CityPluginSettings settings = CityPluginSettingsEditorUtility.GetOrCreateSettings();

        ILotLayoutPlugin layoutPlugin = CityPluginRegistry.Create<ILotLayoutPlugin>(CityPluginCategory.LotLayout, settings.GetActivePluginId(CityPluginCategory.LotLayout));
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

        ICityProcessPlugin processPlugin = CityPluginRegistry.Create<ICityProcessPlugin>(CityPluginCategory.Process, settings.GetActivePluginId(CityPluginCategory.Process));
        if (processPlugin != null)
        {
            ICityPlugin initializable = processPlugin as ICityPlugin;
            initializable?.Initialize(context);

            ICityPipelineContributor contributor = processPlugin as ICityPipelineContributor;
            if (contributor != null)
            {
                return ExecutePipeline(context, contributor.CreatePipelineSteps(context));
            }
            return processPlugin.GenerateAll(context);
        }

        return GenerateAllWithCurrentStepPlugins(context, settings);
    }

    public static CityGenerationReport GenerateAllWithCurrentStepPlugins(CityGenerationContext context, CityPluginSettings settings)
    {
        CityGenerationReport total = new CityGenerationReport { warnings = new List<string>() };

        CityGenerationReport road = GenerateRoadNetwork(context.manager, context.processConfig);
        total.Merge(road);

        if (!settings.runPlanarizationAfterRoadNetwork && settings.runPlanarizationInFullGeneration)
        {
            CityGenerationReport planarize = PlanarizeRoads(context.manager, context.processConfig);
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

        List<List<Vector3>> detected = DetectBlocks(context.manager, context.processConfig);
        for (int i = 0; i < detected.Count; i++)
        {
            context.manager.AddBlock(detected[i]);
        }
        total.blocksDetected += detected.Count;

        CityGenerationReport zoning = AssignZoning(context.manager, context.processConfig);
        total.Merge(zoning);

        int lots = GenerateLots(context.manager, context.processConfig);
        total.lotsGenerated += lots;

        return total;
    }

    private static CityGenerationContext BuildContext(CityManager manager, ScriptableObject processConfig)
    {
        CityData data = manager != null ? manager.GetCityData() : null;
        CityPluginSettings settings = CityPluginSettingsEditorUtility.GetOrCreateSettings();
        ILotSelectionPlugin lotSelection = CityPluginRegistry.Create<ILotSelectionPlugin>(
            CityPluginCategory.LotSelection,
            settings.GetActivePluginId(CityPluginCategory.LotSelection));
        AmericanCityConfig americanConfig = processConfig as AmericanCityConfig;
        return new CityGenerationContext
        {
            manager = manager,
            cityData = data,
            processConfig = processConfig,
            config = americanConfig,
            lotSelectionPlugin = lotSelection
        };
    }

    private static CityGenerationReport BuildMissingPluginReport(string category)
    {
        CityGenerationReport report = new CityGenerationReport { warnings = new List<string>() };
        report.warnings.Add("Nessun plugin disponibile per categoria " + category + ".");
        return report;
    }

    public static CityGenerationReport ExecutePipeline(
        CityGenerationContext context,
        IEnumerable<ICityPipelineStep> steps)
    {
        CityGenerationReport total = new CityGenerationReport { warnings = new List<string>() };
        if (steps == null)
        {
            total.warnings.Add("La pipeline non contiene step.");
            return total;
        }

        var ordered = new List<ICityPipelineStep>(steps);
        ordered.RemoveAll(step => step == null);
        ordered.Sort((left, right) => left.Order.CompareTo(right.Order));

        for (int i = 0; i < ordered.Count; i++)
        {
            ICityPipelineStep step = ordered[i];
            if (!step.CanExecute(context))
            {
                total.warnings.Add("Step saltato: " + step.DisplayName + ".");
                continue;
            }

            try
            {
                total.Merge(step.Execute(context));
            }
            catch (System.Exception exception)
            {
                total.warnings.Add("Errore nello step '" + step.DisplayName + "': " + exception.Message);
                Debug.LogException(exception);
                break;
            }
        }

        return total;
    }
}

}
