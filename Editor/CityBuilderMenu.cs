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
            AssetDatabase.CreateFolder("Assets/BSCCityBuilder", "Assets");

        if (!AssetDatabase.IsValidFolder(baseFolder))
            AssetDatabase.CreateFolder("Assets/BSCCityBuilder/Assets", "ZoneTypes");

        // I 5 ZoneType corrispondono 1:1 ai ring del preset americano:
        // Center→CBD, Commercial→Inner City, Residential→Urban Residential,
        // Suburban→Suburbs, Rural→Exurbs
        int createdCount = 0;
        createdCount += CreateZoneTypeIfMissing(baseFolder, "Center",      new Color(1.0f, 0.42f, 0.21f), 30f, "CBD/Downtown ad alta densità. Grattacieli e commercio.");
        createdCount += CreateZoneTypeIfMissing(baseFolder, "Commercial",  new Color(0.29f, 0.56f, 0.85f), 14f, "Inner city: retail, uffici e uso misto.");
        createdCount += CreateZoneTypeIfMissing(baseFolder, "Residential", new Color(0.3f, 0.68f, 0.31f),  8f,  "Residenziale urbano a media densità.");
        createdCount += CreateZoneTypeIfMissing(baseFolder, "Suburban",    new Color(0.55f, 0.76f, 0.29f), 5f,  "Periferia: case unifamiliari e villette.");
        createdCount += CreateZoneTypeIfMissing(baseFolder, "Rural",       new Color(0.80f, 0.73f, 0.56f), 3f,  "Exurbs: aree rurali e insediamenti sparsi.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string message = createdCount > 0
            ? $"Creati {createdCount} asset ZoneType in {baseFolder}."
            : "Tutti i ZoneType di default sono già presenti.";

        Debug.Log($"[CityBuilder] {message}");
        EditorUtility.DisplayDialog("Setup Zone Types", message, "OK");
    }

    /// <summary>
    /// Collega automaticamente i 5 ZoneType di default ai ring del config americano.
    /// Richiede che i ZoneType siano stati creati con SetupDefaultZoneTypes().
    /// </summary>
    public static void LinkAmericanZoneTypesToConfig(AmericanCityConfig config)
    {
        if (config == null || config.zoneRings == null || config.zoneRings.Count != 5) return;

        string[] names = { "Center", "Commercial", "Residential", "Suburban", "Rural" };
        for (int i = 0; i < 5; i++)
        {
            string[] guids = AssetDatabase.FindAssets($"t:ZoneType {names[i]}");
            foreach (string guid in guids)
            {
                ZoneType zt = AssetDatabase.LoadAssetAtPath<ZoneType>(AssetDatabase.GUIDToAssetPath(guid));
                if (zt != null && zt.GetDisplayName() == names[i])
                {
                    config.zoneRings[i].zoneType = zt;
                    break;
                }
            }
        }
        EditorUtility.SetDirty(config);
    }

    [MenuItem("Tools/City Builder/Setup Default Road Profiles")]
    public static void SetupDefaultRoadProfiles()
    {
        string baseFolder = "Assets/BSCCityBuilder/Assets/RoadProfiles";
        if (!AssetDatabase.IsValidFolder("Assets/BSCCityBuilder/Assets"))
        {
            AssetDatabase.CreateFolder("Assets/BSCCityBuilder", "Assets");
        }

        if (!AssetDatabase.IsValidFolder(baseFolder))
        {
            AssetDatabase.CreateFolder("Assets/BSCCityBuilder/Assets", "RoadProfiles");
        }

        int createdCount = 0;
        createdCount += CreateRoadProfileIfMissing(baseFolder, "Autostrada", RoadHierarchyLevel.Highway, 9.5f, new Color(0.75f, 0.25f, 0.2f), 6f, "Asse veloce ad alta capacità.");
        createdCount += CreateRoadProfileIfMissing(baseFolder, "Strada Principale", RoadHierarchyLevel.MainRoad, 6.5f, new Color(0.95f, 0.65f, 0.2f), 4.5f, "Collega quartieri e distribuisce il traffico.");
        createdCount += CreateRoadProfileIfMissing(baseFolder, "Via Locale", RoadHierarchyLevel.LocalStreet, 4.0f, new Color(0.45f, 0.65f, 0.95f), 3f, "Strada urbana di quartiere.");
        createdCount += CreateRoadProfileIfMissing(baseFolder, "Vicolo", RoadHierarchyLevel.Alley, 2.2f, new Color(0.5f, 0.8f, 0.55f), 2f, "Connessione minuta o di servizio.");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string message = createdCount > 0
            ? $"Creati {createdCount} asset RoadProfile in {baseFolder}."
            : "Tutti i RoadProfile di default sono già presenti.";

        Debug.Log($"[CityBuilder] {message}");
        EditorUtility.DisplayDialog("Setup Road Profiles", message, "OK");
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

    private static int CreateRoadProfileIfMissing(string folder, string assetName, RoadHierarchyLevel hierarchyLevel, float roadWidth, Color color, float intersectionClearanceRadius, string description)
    {
        string[] existing = AssetDatabase.FindAssets($"t:RoadProfile {assetName}");
        for (int i = 0; i < existing.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(existing[i]);
            RoadProfile existingAsset = AssetDatabase.LoadAssetAtPath<RoadProfile>(path);
            if (existingAsset != null && existingAsset.GetDisplayName() == assetName)
            {
                return 0;
            }
        }

        RoadProfile roadProfile = ScriptableObject.CreateInstance<RoadProfile>();
        roadProfile.displayName = assetName;
        roadProfile.hierarchyLevel = hierarchyLevel;
        roadProfile.roadWidth = roadWidth;
        roadProfile.debugColor = color;
        roadProfile.intersectionClearanceRadius = intersectionClearanceRadius;
        roadProfile.description = description;

        string pathForAsset = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}.asset");
        AssetDatabase.CreateAsset(roadProfile, pathForAsset);
        return 1;
    }

    [MenuItem("Tools/City Builder/Create American City Config")]
    public static void CreateAmericanCityConfig()
    {
        string folder = "Assets/BSCCityBuilder/Assets";

        if (!AssetDatabase.IsValidFolder("Assets/BSCCityBuilder/Assets"))
            AssetDatabase.CreateFolder("Assets/BSCCityBuilder", "Assets");

        // Crea prima i ZoneType di default (no-op se già presenti)
        string baseFolder = "Assets/BSCCityBuilder/Assets/ZoneTypes";
        if (!AssetDatabase.IsValidFolder(baseFolder))
            AssetDatabase.CreateFolder("Assets/BSCCityBuilder/Assets", "ZoneTypes");
        CreateZoneTypeIfMissing(baseFolder, "Center",      new Color(1.0f, 0.42f, 0.21f), 30f, "CBD/Downtown ad alta densità.");
        CreateZoneTypeIfMissing(baseFolder, "Commercial",  new Color(0.29f, 0.56f, 0.85f), 14f, "Inner city: retail, uffici e uso misto.");
        CreateZoneTypeIfMissing(baseFolder, "Residential", new Color(0.3f, 0.68f, 0.31f),  8f,  "Residenziale urbano a media densità.");
        CreateZoneTypeIfMissing(baseFolder, "Suburban",    new Color(0.55f, 0.76f, 0.29f), 5f,  "Periferia: case unifamiliari e villette.");
        CreateZoneTypeIfMissing(baseFolder, "Rural",       new Color(0.80f, 0.73f, 0.56f), 3f,  "Exurbs: aree rurali e insediamenti sparsi.");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        AmericanCityConfig config = ScriptableObject.CreateInstance<AmericanCityConfig>();
        config.ResetToAmericanDefaults();
        string path = AssetDatabase.GenerateUniqueAssetPath($"{folder}/AmericanCityConfig.asset");

        AssetDatabase.CreateAsset(config, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // Collega i ZoneType ai ring
        LinkAmericanZoneTypesToConfig(config);
        AssetDatabase.SaveAssets();

        EditorUtility.FocusProjectWindow();
        Selection.activeObject = config;

        Debug.Log($"[CityBuilder] AmericanCityConfig creato con ZoneType collegati: {path}");
    }

    [MenuItem("Tools/City Builder/Planarize Road Network")]
    public static void PlanarizeExistingNetworkMenu()
    {
        CityManager manager = Object.FindFirstObjectByType<CityManager>();
        if (manager == null)
        {
            EditorUtility.DisplayDialog("Planarizza Rete", "Nessun CityManager trovato nella scena.", "OK");
            return;
        }
        string result = PlanarizeExistingNetwork(manager, 2f);
        UnityEditor.EditorUtility.SetDirty(manager.GetCityData());
        SceneView.RepaintAll();
        EditorUtility.DisplayDialog("Planarizza Rete", result, "OK");
    }

    /// <summary>
    /// Planarizza la rete stradale esistente risolvendo gli incroci geometrici.
    /// Ritorna una stringa di report.
    /// </summary>
    public static string PlanarizeExistingNetwork(CityManager manager, float mergeTol = 2f)
    {
        if (manager == null) return "CityManager non valido.";
        CityData cityData = manager.GetCityData();
        if (cityData == null) return "CityData non assegnato.";

        Undo.RecordObject(cityData, "Planarizza Rete Stradale");

        int nodesBefore = cityData.nodes.Count;
        int segsBefore  = cityData.segments.Count;

        int splitsDone = CityRoadPlanarizer.Planarize(manager, mergeTol);

        int nodesAdded = cityData.nodes.Count    - nodesBefore;
        int segsAdded  = cityData.segments.Count - segsBefore;

        string msg = $"Segmenti planarizzati: {splitsDone}\nNodi aggiunti: {nodesAdded}\nSegmenti delta: {segsAdded}";
        Debug.Log($"[CityBuilder] Planarizzazione: {msg}");
        return msg;
    }
}
