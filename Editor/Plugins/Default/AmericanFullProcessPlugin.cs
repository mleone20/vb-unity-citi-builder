using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Config;
using BSCCityBuilder.Generation;
using BSCCityBuilder.Plugins;
using BSCCityBuilder.Editor.Plugins;

namespace BSCCityBuilder.Editor.Plugins.Default
{
[CityPlugin("bsc.process.american-full", "American Full Process", CityPluginCategory.Process, "Preset completo: road network + planarization + block detection + zoning + lot generation.")]
public class AmericanFullProcessPlugin : ICityProcessPlugin, ICityProcessPluginEditorUI, ICityProcessCapabilities
{
    public CityProcessCapabilities Capabilities => CityProcessCapabilities.All;
    public string ConfigurationLabel => "American City Config";
    public System.Type ConfigurationType => typeof(AmericanCityConfig);

    public CityConfig CreateDefaultConfigurationAsset()
    {
        AmericanCityConfig config = ScriptableObject.CreateInstance<AmericanCityConfig>();
        config.ResetToAmericanDefaults();
        CityBuilderAssetPaths.CreateUniqueAsset(config, "AmericanCityConfig.asset");
        Selection.activeObject = config;
        return config;
    }

    public void DrawConfigurationGUI(CityConfig config)
    {
        AmericanCityConfig american = config as AmericanCityConfig;
        if (american == null)
        {
            EditorGUILayout.HelpBox("Config non valida per il plugin American Full Process.", MessageType.Warning);
            return;
        }

        UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(american);
        if (editor != null)
        {
            editor.OnInspectorGUI();
        }
    }

    public CityGenerationReport GenerateAll(CityGenerationContext context)
    {
        CityGenerationReport total = new CityGenerationReport { warnings = new List<string>() };
        AmericanCityConfig cfg = context.GetConfig<AmericanCityConfig>();
        if (cfg == null)
        {
            total.warnings.Add("AmericanCityConfig non assegnato o non compatibile con American Full Process.");
            return total;
        }

        if (context.manager == null)
        {
            total.warnings.Add("CityManager non assegnato.");
            return total;
        }

        CityData cityData = context.cityData != null ? context.cityData : context.manager.GetCityData();
        if (cityData == null)
        {
            total.warnings.Add("CityData non assegnato.");
            return total;
        }

        CityPluginSettings settings = CityPluginSettingsEditorUtility.GetOrCreateSettings();

        ILotSelectionPlugin lotSelection = CityPluginRegistry.Create<ILotSelectionPlugin>(CityPluginCategory.LotSelection, settings.GetActivePluginId(CityPluginCategory.LotSelection));
        context.lotSelectionPlugin = lotSelection;

        context.config = cfg;

        CityGenerationProgress.Report(0.04f, "Generazione della rete stradale...");
        IRoadNetworkGenerationPlugin road = new AmericanRoadNetworkPlugin();
        total.Merge(road.GenerateRoadNetwork(context));

        if (settings.runPlanarizationInFullGeneration)
        {
            CityGenerationProgress.Report(0.28f, "Planarizzazione delle intersezioni...");
            IRoadPlanarizationPlugin planarization = CityPluginRegistry.Create<IRoadPlanarizationPlugin>(CityPluginCategory.RoadPlanarization, settings.GetActivePluginId(CityPluginCategory.RoadPlanarization));
            if (planarization != null)
            {
                total.Merge(planarization.PlanarizeRoads(context));
            }
        }

        CityGenerationProgress.Report(0.43f, "Preparazione del rilevamento blocchi...");
        Undo.RecordObject(cityData, "Generate All: Clear Blocks");
        foreach (CityBlock block in cityData.blocks)
        {
            if (block != null)
            {
                block.lotIDs.Clear();
            }
        }
        cityData.blocks.Clear();
        cityData.lots.Clear();
        EditorUtility.SetDirty(cityData);

        CityGenerationProgress.Report(0.5f, "Rilevamento dei blocchi...");
        IBlockDetectionPlugin blockDetection = CityPluginRegistry.Create<IBlockDetectionPlugin>(CityPluginCategory.BlockDetection, settings.GetActivePluginId(CityPluginCategory.BlockDetection));
        List<List<Vector3>> detected = blockDetection != null ? blockDetection.DetectBlocks(context) : new List<List<Vector3>>();
        for (int i = 0; i < detected.Count; i++)
        {
            context.manager.AddBlock(detected[i]);
        }
        total.blocksDetected += detected.Count;

        CityGenerationProgress.Report(0.66f, "Assegnazione dello zoning...");
        IZoningAssignmentPlugin zoning = new AmericanZoningPlugin();
        total.Merge(zoning.AssignZoning(context));

        ILotLayoutPlugin lotLayout = CityPluginRegistry.Create<ILotLayoutPlugin>(CityPluginCategory.LotLayout, settings.GetActivePluginId(CityPluginCategory.LotLayout));
        if (lotLayout != null)
        {
            int lotsGenerated = 0;
            cityData.lots.Clear();
            for (int i = 0; i < cityData.blocks.Count; i++)
            {
                float blockProgress = cityData.blocks.Count > 0 ? (float)i / cityData.blocks.Count : 1f;
                CityGenerationProgress.Report(
                    Mathf.Lerp(0.76f, 0.98f, blockProgress),
                    "Generazione lotti: blocco " + (i + 1) + " di " + cityData.blocks.Count);
                CityBlock block = cityData.blocks[i];
                if (block == null)
                {
                    continue;
                }

                block.lotIDs.Clear();
                List<CityLot> generated = lotLayout.GenerateLotsForBlock(context, block, i);
                for (int l = 0; l < generated.Count; l++)
                {
                    CityLot lot = generated[l];
                    lot.id = cityData.GetNextLotID();
                    cityData.lots.Add(lot);
                    block.lotIDs.Add(lot.id);
                    lotsGenerated++;
                }
            }

            total.lotsGenerated += lotsGenerated;
            EditorUtility.SetDirty(cityData);
        }

        SceneView.RepaintAll();
        return total;
    }
}

}
