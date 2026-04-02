using UnityEngine;
using UnityEditor;

/// <summary>
/// Menu item per creare asset CityData
/// </summary>
public static class CityBuilderMenu
{
    [MenuItem("Assets/Create/CityData")]
    public static void CreateCityData()
    {
        CityData newData = ScriptableObject.CreateInstance<CityData>();
        
        string path = "Assets/BSCCityBuilder/Assets/CityData.asset";
        
        // Assicura che la cartella esista
        string directory = System.IO.Path.GetDirectoryName(path);
        if (!System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }
        
        // Salva asset
        AssetDatabase.CreateAsset(newData, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = newData;
        
        Debug.Log($"[CityBuilder] CityData asset creato: {path}");
    }

    [MenuItem("GameObject/CityBuilder/Create CityManager")]
    public static void CreateCityManager()
    {
        // Crea una scena vuota se non esiste un gameobject selezionato
        GameObject managerGO = new GameObject("CityManager");
        
        CityManager manager = managerGO.AddComponent<CityManager>();
        
        Debug.Log("[CityBuilder] CityManager creato nella scena!");
        
        Selection.activeGameObject = managerGO;
    }

    [MenuItem("Tools/City Builder/Setup Default Zone Types")]
    public static void SetupDefaultZoneTypes()
    {
        string baseFolder = "Assets/BSCCityBuilder/Assets/ZoneTypes";
        if (!AssetDatabase.IsValidFolder("Assets/BSCCityBuilder/Assets"))
            {
            AssetDatabase.CreateFolder("Assets/BSCCityBuilder", "Assets");
        }

        if (!AssetDatabase.IsValidFolder(baseFolder))
        {
            AssetDatabase.CreateFolder("Assets/BSCCityBuilder/Assets", "ZoneTypes");
        }

        int createdCount = 0;
        createdCount += CreateZoneTypeIfMissing(baseFolder, "Residential", new Color(0.2f, 0.75f, 0.3f), 8f, "Low to medium density housing.");
        createdCount += CreateZoneTypeIfMissing(baseFolder, "Commercial", new Color(0.2f, 0.45f, 0.95f), 14f, "Retail and office frontage.");
        createdCount += CreateZoneTypeIfMissing(baseFolder, "Industrial", new Color(0.9f, 0.75f, 0.2f), 10f, "Warehouses, workshops, logistics.");
        createdCount += CreateZoneTypeIfMissing(baseFolder, "Special", new Color(0.45f, 0.45f, 0.45f), 18f, "Landmarks, civic or unique functions.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string message = createdCount > 0
            ? $"Creati {createdCount} asset ZoneType in {baseFolder}."
            : "Tutti i ZoneType di default sono già presenti.";

        Debug.Log($"[CityBuilder] {message}");
        EditorUtility.DisplayDialog("Setup Zone Types", message, "OK");
    }

    private static int CreateZoneTypeIfMissing(string folder, string assetName, Color color, float buildingHeight, string description)
    {
        string[] existing = AssetDatabase.FindAssets($"t:ZoneType {assetName}");
        for (int i = 0; i < existing.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(existing[i]);
            ZoneType existingAsset = AssetDatabase.LoadAssetAtPath<ZoneType>(path);
            if (existingAsset != null && existingAsset.GetDisplayName() == assetName)
            {
                return 0;
            }
        }

        ZoneType zoneType = ScriptableObject.CreateInstance<ZoneType>();
        zoneType.displayName = assetName;
        zoneType.zoneColor = color;
        zoneType.buildingHeight = buildingHeight;
        zoneType.description = description;

        string pathForAsset = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}.asset");
        AssetDatabase.CreateAsset(zoneType, pathForAsset);
        return 1;
    }
}
