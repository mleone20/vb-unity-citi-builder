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

    [Header("Active Plugin IDs")]
    [HideInInspector]
    public string activeProcessPluginId = "bsc.process.default-random";
    [HideInInspector]
    public string activeRoadNetworkPluginId = "bsc.american.road-network";
    [HideInInspector]
    public string activeRoadPlanarizationPluginId = "bsc.default.planarization";
    [HideInInspector]
    public string activeBlockDetectionPluginId = "bsc.default.block-detection";
    [HideInInspector]
    public string activeZoningPluginId = "bsc.american.zoning";
    [HideInInspector]
    public string activeLotLayoutPluginId = "bsc.default.lot-layout";
    [HideInInspector]
    public string activeLotSelectionPluginId = "bsc.default.lot-selection";

    [Header("Pipeline Options")]
    public bool runPlanarizationAfterRoadNetwork = true;
    public bool runPlanarizationInFullGeneration = true;

    public string GetActivePluginId(CityPluginCategory category)
    {
        EnsureMigrated();
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
        EnsureMigrated();
        for (int i = 0; i < selections.Count; i++)
        {
            if (selections[i].category == category)
            {
                selections[i].pluginId = pluginId;
                SyncLegacyField(category, pluginId);
                return;
            }
        }
        selections.Add(new PluginSelection { category = category, pluginId = pluginId });
        SyncLegacyField(category, pluginId);
    }

    private void EnsureMigrated()
    {
        if (selections.Count > 0)
        {
            return;
        }

        foreach (CityPluginCategory category in System.Enum.GetValues(typeof(CityPluginCategory)))
        {
            selections.Add(new PluginSelection
            {
                category = category,
                pluginId = GetLegacyField(category)
            });
        }
    }

    private string GetLegacyField(CityPluginCategory category)
    {
        switch (category)
        {
            case CityPluginCategory.Process: return activeProcessPluginId;
            case CityPluginCategory.RoadNetwork: return activeRoadNetworkPluginId;
            case CityPluginCategory.RoadPlanarization: return activeRoadPlanarizationPluginId;
            case CityPluginCategory.BlockDetection: return activeBlockDetectionPluginId;
            case CityPluginCategory.Zoning: return activeZoningPluginId;
            case CityPluginCategory.LotLayout: return activeLotLayoutPluginId;
            case CityPluginCategory.LotSelection: return activeLotSelectionPluginId;
            default: return string.Empty;
        }
    }

    private void SyncLegacyField(CityPluginCategory category, string pluginId)
    {
        switch (category)
        {
            case CityPluginCategory.Process: activeProcessPluginId = pluginId; break;
            case CityPluginCategory.RoadNetwork: activeRoadNetworkPluginId = pluginId; break;
            case CityPluginCategory.RoadPlanarization: activeRoadPlanarizationPluginId = pluginId; break;
            case CityPluginCategory.BlockDetection: activeBlockDetectionPluginId = pluginId; break;
            case CityPluginCategory.Zoning: activeZoningPluginId = pluginId; break;
            case CityPluginCategory.LotLayout: activeLotLayoutPluginId = pluginId; break;
            case CityPluginCategory.LotSelection: activeLotSelectionPluginId = pluginId; break;
        }
    }
}

}
