using UnityEngine;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Config;
using BSCCityBuilder.Generation;

namespace BSCCityBuilder.Plugins
{
[CreateAssetMenu(fileName = "CityPluginSettings", menuName = "City Builder/Plugin Settings")]
public class CityPluginSettings : ScriptableObject
{
    [System.Serializable]
    public class PluginSelection
    {
        public CityPluginCategory category;
        public string pluginId;
    }

    [SerializeField] private System.Collections.Generic.List<PluginSelection> selections =
        new System.Collections.Generic.List<PluginSelection>();

    [Header("Pipeline Options")]
    public bool runPlanarizationAfterRoadNetwork = true;
    public bool runPlanarizationInFullGeneration = true;

    public string GetActivePluginId(CityPluginCategory category)
    {
        EnsureSelections();
        for (int i = 0; i < selections.Count; i++)
        {
            if (selections[i].category == category)
            {
                return selections[i].pluginId;
            }
        }
        return string.Empty;
    }

    public void SetActivePluginId(CityPluginCategory category, string pluginId)
    {
        EnsureSelections();
        for (int i = 0; i < selections.Count; i++)
        {
            if (selections[i].category == category)
            {
                selections[i].pluginId = pluginId;
                return;
            }
        }
        selections.Add(new PluginSelection { category = category, pluginId = pluginId });
    }

    private void EnsureSelections()
    {
        foreach (CityPluginCategory category in System.Enum.GetValues(typeof(CityPluginCategory)))
        {
            bool exists = false;
            for (int i = 0; i < selections.Count; i++)
            {
                if (selections[i].category == category)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                selections.Add(new PluginSelection
                {
                    category = category,
                    pluginId = GetDefaultPluginId(category)
                });
            }
        }
    }

    private static string GetDefaultPluginId(CityPluginCategory category)
    {
        switch (category)
        {
            case CityPluginCategory.Process: return "bsc.process.default-random";
            case CityPluginCategory.RoadNetwork: return "bsc.american.road-network";
            case CityPluginCategory.RoadPlanarization: return "bsc.default.planarization";
            case CityPluginCategory.BlockDetection: return "bsc.default.block-detection";
            case CityPluginCategory.Zoning: return "bsc.american.zoning";
            case CityPluginCategory.LotLayout: return "bsc.default.lot-layout";
            case CityPluginCategory.LotSelection: return "bsc.default.lot-selection";
            default: return string.Empty;
        }
    }
}

}
