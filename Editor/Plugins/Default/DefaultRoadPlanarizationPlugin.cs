using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CityPlugin("bsc.default.planarization", "Default Road Planarization", CityPluginCategory.RoadPlanarization, "Planarizza la rete spezzando segmenti agli incroci geometrici.")]
public class DefaultRoadPlanarizationPlugin : IRoadPlanarizationPlugin
{
    public CityGenerationReport PlanarizeRoads(CityGenerationContext context)
    {
        CityGenerationReport report = new CityGenerationReport { warnings = new List<string>() };

        if (context.manager == null)
        {
            report.warnings.Add("CityManager non assegnato.");
            return report;
        }

        float merge = 1f;
        if (context.config != null)
        {
            merge = Mathf.Max(0.1f, context.config.mergeThreshold);
        }

        int splits = CityRoadPlanarizer.Planarize(context.manager, merge);
        report.planarizationSplits = splits;

        if (context.cityData != null)
        {
            EditorUtility.SetDirty(context.cityData);
        }

        SceneView.RepaintAll();
        return report;
    }
}
