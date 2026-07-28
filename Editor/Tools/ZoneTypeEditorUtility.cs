using System.Collections.Generic;
using UnityEditor;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Generation;
using BSCCityBuilder.Config;
using BSCCityBuilder.Rendering;
using BSCCityBuilder.Plugins;

namespace BSCCityBuilder.Editor.Tools
{
public static class ZoneTypeEditorUtility
{
    public static List<ZoneType> LoadZoneTypes(CityData cityData)
    {
        List<ZoneType> result = new List<ZoneType>();
        CityPalette palette = cityData != null ? cityData.GetPalette() : null;
        if (palette == null)
        {
            return result;
        }

        for (int i = 0; i < palette.ZoneTypes.Count; i++)
        {
            ZoneType zoneType = palette.ZoneTypes[i];
            if (zoneType != null && !result.Contains(zoneType))
            {
                result.Add(zoneType);
            }
        }

        return result;
    }

    public static List<ZoneType> LoadAllZoneTypes()
    {
        string[] guids = AssetDatabase.FindAssets("t:ZoneType");
        List<ZoneType> zoneTypes = new List<ZoneType>();

        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ZoneType zoneType = AssetDatabase.LoadAssetAtPath<ZoneType>(path);
            if (zoneType != null)
            {
                zoneTypes.Add(zoneType);
            }
        }

        zoneTypes.Sort((a, b) => string.Compare(GetZoneDisplayName(a), GetZoneDisplayName(b), System.StringComparison.OrdinalIgnoreCase));
        return zoneTypes;
    }

    public static string GetZoneDisplayName(ZoneType zoneType)
    {
        return zoneType != null ? zoneType.GetDisplayName() : "None";
    }
}

}
