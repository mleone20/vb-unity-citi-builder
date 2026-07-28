using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Config;
using BSCCityBuilder.Generation;
using BSCCityBuilder.Plugins;
using BSCCityBuilder.Editor.Plugins;

namespace BSCCityBuilder.Editor.Plugins.Default
{
[CityPlugin("bsc.process.default-random", "Default Random Scatter", CityPluginCategory.Process, "Generazione base non tematica con scattering casuale e opzioni minime.")]
public class DefaultRandomScatterProcessPlugin : ICityProcessPlugin, ICityProcessPluginEditorUI, ICityProcessCapabilities
{
    public CityProcessCapabilities Capabilities =>
        CityProcessCapabilities.LotGeneration | CityProcessCapabilities.FullGeneration;
    public string ConfigurationLabel => "Random Scatter Config";
    public Type ConfigurationType => typeof(RandomScatterCityConfig);

    public CityConfig CreateDefaultConfigurationAsset()
    {
        var cfg = ScriptableObject.CreateInstance<RandomScatterCityConfig>();
        CityBuilderAssetPaths.CreateUniqueAsset(cfg, "RandomScatterCityConfig.asset");
        Selection.activeObject = cfg;
        return cfg;
    }

    public void DrawConfigurationGUI(CityConfig config)
    {
        var cfg = config as RandomScatterCityConfig;
        if (cfg == null)
        {
            EditorGUILayout.HelpBox("Config non valida per Default Random Scatter.", MessageType.Warning);
            return;
        }

        EditorGUI.BeginChangeCheck();
        cfg.centerWorldPosition = EditorGUILayout.Vector3Field("Center", cfg.centerWorldPosition);
        cfg.radius = EditorGUILayout.Slider("Radius", cfg.radius, 50f, 5000f);
        cfg.nodeCount = EditorGUILayout.IntSlider("Node Count", cfg.nodeCount, 10, 3000);
        cfg.nearestConnections = EditorGUILayout.IntSlider("Nearest Connections", cfg.nearestConnections, 1, 8);

        EditorGUILayout.Space(4);
        cfg.blockCount = EditorGUILayout.IntSlider("Block Count", cfg.blockCount, 10, 600);
        cfg.minBlockSize = EditorGUILayout.Slider("Min Block Size", cfg.minBlockSize, 10f, 300f);
        cfg.maxBlockSize = EditorGUILayout.Slider("Max Block Size", cfg.maxBlockSize, 10f, 300f);
        if (cfg.maxBlockSize < cfg.minBlockSize)
            cfg.maxBlockSize = cfg.minBlockSize;

        EditorGUILayout.Space(4);
        cfg.randomSeed = EditorGUILayout.IntField("Random Seed", cfg.randomSeed);
        SerializedObject so = new SerializedObject(cfg);
        so.Update();
        EditorGUILayout.PropertyField(so.FindProperty("zoneTypes"), true);
        so.ApplyModifiedProperties();

        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(cfg);
        }
    }

    public CityGenerationReport GenerateAll(CityGenerationContext context)
    {
        var report = new CityGenerationReport { warnings = new List<string>() };
        var cfg = context.GetConfig<RandomScatterCityConfig>();

        if (context.manager == null || context.cityData == null)
        {
            report.warnings.Add("CityManager/CityData non assegnati.");
            return report;
        }

        if (cfg == null)
        {
            report.warnings.Add("RandomScatterCityConfig non assegnato.");
            return report;
        }

        CityData cityData = context.cityData;
        CityPluginSettings settings = CityPluginSettingsEditorUtility.GetOrCreateSettings();

        Undo.RecordObject(cityData, "Default Random Scatter Generate All");
        cityData.Clear();

        var rng = new System.Random(cfg.randomSeed);
        var nodeIds = new List<int>(Mathf.Max(0, cfg.nodeCount));

        for (int i = 0; i < cfg.nodeCount; i++)
        {
            Vector2 p = UnityEngine.Random.insideUnitCircle;
            // Usa RNG deterministico locale invece dello stato globale di UnityEngine.Random.
            float angle = (float)(rng.NextDouble() * Math.PI * 2.0);
            float dist = Mathf.Sqrt((float)rng.NextDouble()) * cfg.radius;
            p = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;

            Vector3 pos = cfg.centerWorldPosition + new Vector3(p.x, 0f, p.y);
            CityNode node = context.manager.AddNode(pos);
            if (node != null)
            {
                nodeIds.Add(node.id);
                report.nodesCreated++;
            }
        }

        for (int i = 0; i < nodeIds.Count; i++)
        {
            CityNode from = cityData.GetNode(nodeIds[i]);
            if (from == null) continue;

            var distances = new List<(int id, float sqr)>();
            for (int j = 0; j < nodeIds.Count; j++)
            {
                if (i == j) continue;
                CityNode to = cityData.GetNode(nodeIds[j]);
                if (to == null) continue;
                float sqr = (to.position - from.position).sqrMagnitude;
                distances.Add((to.id, sqr));
            }

            distances.Sort((a, b) => a.sqr.CompareTo(b.sqr));
            int links = Mathf.Min(cfg.nearestConnections, distances.Count);
            for (int k = 0; k < links; k++)
            {
                CitySegment seg = context.manager.AddSegment(from.id, distances[k].id);
                if (seg != null)
                    report.segmentsCreated++;
            }
        }

        ZoneType[] zones = cfg.zoneTypes != null ? cfg.zoneTypes : Array.Empty<ZoneType>();
        for (int b = 0; b < cfg.blockCount; b++)
        {
            float a = (float)(rng.NextDouble() * Math.PI * 2.0);
            float d = Mathf.Sqrt((float)rng.NextDouble()) * cfg.radius;
            Vector3 c = cfg.centerWorldPosition + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);

            float sx = Mathf.Lerp(cfg.minBlockSize, cfg.maxBlockSize, (float)rng.NextDouble());
            float sz = Mathf.Lerp(cfg.minBlockSize, cfg.maxBlockSize, (float)rng.NextDouble());

            var poly = new List<Vector3>
            {
                c + new Vector3(-sx * 0.5f, 0f, -sz * 0.5f),
                c + new Vector3( sx * 0.5f, 0f, -sz * 0.5f),
                c + new Vector3( sx * 0.5f, 0f,  sz * 0.5f),
                c + new Vector3(-sx * 0.5f, 0f,  sz * 0.5f),
            };

            CityBlock block = context.manager.AddBlock(poly);
            if (block != null)
            {
                report.blocksDetected++;
                if (zones.Length > 0)
                {
                    ZoneType z = zones[rng.Next(0, zones.Length)];
                    block.zoning = z;
                    if (z != null)
                        report.blocksZoned++;
                }
            }
        }

        ILotSelectionPlugin lotSelection = CityPluginRegistry.Create<ILotSelectionPlugin>(CityPluginCategory.LotSelection, settings.GetActivePluginId(CityPluginCategory.LotSelection));
        context.lotSelectionPlugin = lotSelection;

        ILotLayoutPlugin lotLayout = CityPluginRegistry.Create<ILotLayoutPlugin>(CityPluginCategory.LotLayout, settings.GetActivePluginId(CityPluginCategory.LotLayout));
        if (lotLayout != null)
        {
            cityData.lots.Clear();
            int lotsGenerated = 0;
            for (int i = 0; i < cityData.blocks.Count; i++)
            {
                CityBlock block = cityData.blocks[i];
                if (block == null) continue;

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
            report.lotsGenerated += lotsGenerated;
        }

        EditorUtility.SetDirty(cityData);
        SceneView.RepaintAll();
        return report;
    }
}

}
