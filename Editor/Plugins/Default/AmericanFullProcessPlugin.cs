using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CityPlugin("bsc.process.american-full", "American Full Process", CityPluginCategory.Process, "Preset completo: road network + planarization + block detection + zoning + lot generation.")]
public class AmericanFullProcessPlugin : ICityProcessPlugin
{
    public CityGenerationReport GenerateAll(CityGenerationContext context)
    {
        CityGenerationReport total = new CityGenerationReport { warnings = new List<string>() };

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

        ILotSelectionPlugin lotSelection = CityPluginRegistry.Create<ILotSelectionPlugin>(CityPluginCategory.LotSelection, settings.activeLotSelectionPluginId);
        CityPluginRuntime.SetLotSelectionPlugin(lotSelection);

        IRoadNetworkGenerationPlugin road = new AmericanRoadNetworkPlugin();
        total.Merge(road.GenerateRoadNetwork(context));

        if (settings.runPlanarizationInFullGeneration)
        {
            IRoadPlanarizationPlugin planarization = CityPluginRegistry.Create<IRoadPlanarizationPlugin>(CityPluginCategory.RoadPlanarization, settings.activeRoadPlanarizationPluginId);
            if (planarization != null)
            {
                total.Merge(planarization.PlanarizeRoads(context));
            }
        }

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

        IBlockDetectionPlugin blockDetection = CityPluginRegistry.Create<IBlockDetectionPlugin>(CityPluginCategory.BlockDetection, settings.activeBlockDetectionPluginId);
        List<List<Vector3>> detected = blockDetection != null ? blockDetection.DetectBlocks(context) : new List<List<Vector3>>();
        for (int i = 0; i < detected.Count; i++)
        {
            context.manager.AddBlock(detected[i]);
        }
        total.blocksDetected += detected.Count;

        IZoningAssignmentPlugin zoning = new AmericanZoningPlugin();
        total.Merge(zoning.AssignZoning(context));

        ILotLayoutPlugin lotLayout = CityPluginRegistry.Create<ILotLayoutPlugin>(CityPluginCategory.LotLayout, settings.activeLotLayoutPluginId);
        if (lotLayout != null)
        {
            int lotsGenerated = 0;
            cityData.lots.Clear();
            for (int i = 0; i < cityData.blocks.Count; i++)
            {
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
