using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Config;
using BSCCityBuilder.Generation;
using BSCCityBuilder.Plugins;
using BSCCityBuilder.Editor.Plugins;
using BSCCityBuilder.Editor.Tools;

namespace BSCCityBuilder.Editor.Plugins.Default
{
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
            merge = Mathf.Max(0.1f, context.config.PlanarizationMergeTolerance);

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

}
