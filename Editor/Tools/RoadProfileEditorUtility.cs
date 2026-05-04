using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public static class RoadProfileEditorUtility
{
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
