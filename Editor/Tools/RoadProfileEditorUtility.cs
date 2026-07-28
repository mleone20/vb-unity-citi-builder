using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Generation;
using BSCCityBuilder.Config;
using BSCCityBuilder.Rendering;
using BSCCityBuilder.Plugins;

namespace BSCCityBuilder.Editor.Tools
{
public static class RoadProfileEditorUtility
{
    public static List<RoadProfile> LoadRoadProfiles(CityData cityData)
    {
        List<RoadProfile> result = new List<RoadProfile>();
        CityPalette palette = cityData != null ? cityData.GetPalette() : null;
        if (palette == null)
        {
            return result;
        }

        for (int i = 0; i < palette.RoadProfiles.Count; i++)
        {
            RoadProfile profile = palette.RoadProfiles[i];
            if (profile != null && !result.Contains(profile))
            {
                result.Add(profile);
            }
        }

        return result;
    }

    public static List<RoadProfile> LoadAllRoadProfiles()
    {
        List<RoadProfile> profiles = new List<RoadProfile>();
        string[] guids = AssetDatabase.FindAssets("t:RoadProfile");
        
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            RoadProfile profile = AssetDatabase.LoadAssetAtPath<RoadProfile>(path);
            if (profile != null)
            {
                profiles.Add(profile);
            }
        }
        
        return profiles;
    }

    public static string GetRoadProfileDisplayName(RoadProfile profile)
    {
        if (profile == null)
        {
            return "None";
        }

        return profile.GetDisplayName();
    }
}

}
