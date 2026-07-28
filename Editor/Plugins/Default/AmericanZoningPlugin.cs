using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Config;
using BSCCityBuilder.Generation;
using BSCCityBuilder.Plugins;
using BSCCityBuilder.Editor.Plugins;

namespace BSCCityBuilder.Editor.Plugins.Default
{
[CityPlugin("bsc.american.zoning", "American Zoning", CityPluginCategory.Zoning, "Assegna zoning e orientamento per distanza da P0.")]
public class AmericanZoningPlugin : IZoningAssignmentPlugin
{
    public CityGenerationReport AssignZoning(CityGenerationContext context)
    {
        CityGenerationReport report = new CityGenerationReport { warnings = new List<string>() };
        AmericanCityConfig config = context.GetConfig<AmericanCityConfig>();
        if (context.manager == null || config == null)
        {
            report.warnings.Add("CityManager o AmericanCityConfig non assegnati.");
            return report;
        }

        CityData cityData = context.cityData != null ? context.cityData : context.manager.GetCityData();
        if (cityData == null)
        {
            report.warnings.Add("CityData non assegnato nel CityManager.");
            return report;
        }

        Undo.RecordObject(cityData, "Assign Zoning By Distance (Plugin)");
        Vector3 p0 = config.centerWorldPosition;

        foreach (CityBlock block in cityData.blocks)
        {
            if (block == null)
            {
                continue;
            }

            Vector3 center = block.GetCenter();
            float dist = Mathf.Sqrt(
                (center.x - p0.x) * (center.x - p0.x) +
                (center.z - p0.z) * (center.z - p0.z));

            ZoneType zone = config.GetZoneTypeForDistance(dist);
            if (zone == null)
            {
                report.warnings.Add("Block " + block.id + ": nessuna zona mappata per dist=" + dist.ToString("F0") + "m");
                continue;
            }

            context.manager.SetBlockZoning(block.id, zone);
            report.blocksZoned++;
        }

        EditorUtility.SetDirty(cityData);
        SceneView.RepaintAll();
        return report;
    }
}

}
