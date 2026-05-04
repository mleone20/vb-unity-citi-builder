using UnityEngine;

[CreateAssetMenu(fileName = "CityPluginSettings", menuName = "City Builder/Plugin Settings")]
public class CityPluginSettings : ScriptableObject
{
    [Header("Active Plugin IDs")]
    public string activeProcessPluginId = "bsc.process.american-full";
    public string activeRoadNetworkPluginId = "bsc.american.road-network";
    public string activeRoadPlanarizationPluginId = "bsc.default.planarization";
    public string activeBlockDetectionPluginId = "bsc.default.block-detection";
    public string activeZoningPluginId = "bsc.american.zoning";
    public string activeLotLayoutPluginId = "bsc.default.lot-layout";
    public string activeLotSelectionPluginId = "bsc.default.lot-selection";

    [Header("Pipeline Options")]
    public bool runPlanarizationAfterRoadNetwork = true;
    public bool runPlanarizationInFullGeneration = true;

    public string GetActivePluginId(CityPluginCategory category)
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

    public void SetActivePluginId(CityPluginCategory category, string pluginId)
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
