using System.Collections.Generic;
using UnityEditor;

public static class ZoneTypeEditorUtility
{
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