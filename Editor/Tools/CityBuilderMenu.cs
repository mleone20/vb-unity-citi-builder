using UnityEngine;
using UnityEditor;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Generation;
using BSCCityBuilder.Components;
using BSCCityBuilder.Config;
using BSCCityBuilder.Rendering;
using BSCCityBuilder.Plugins;
using BSCCityBuilder.Editor.Plugins;

namespace BSCCityBuilder.Editor.Tools
{
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

    /// <summary>
    /// Collega i 4 ZoneType del preset di gioco ai ring per etichetta keyword.
    /// CBD → Center, Inner City → Commercial, Residential → Residential, Suburban → Suburban.
    /// Funziona sia con ResetToGameDefaults (4 ring) che con configurazioni custom.
    /// </summary>
    public static void LinkGameZoneTypesToConfig(AmericanCityConfig config)
    {
        if (config == null || config.zoneRings == null || config.zoneRings.Count == 0) return;

        // (keyword nel label ring → nome ZoneType asset)
        var keywordToAsset = new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            { "CBD",         "Center"      },
            { "Downtown",    "Center"      },
            { "Inner",       "Commercial"  },
            { "Residential", "Residential" },
            { "Urban",       "Residential" },
            { "Suburb",      "Suburban"    },
            { "Rural",       "Rural"       },
            { "Exurb",       "Rural"       },
        };

        foreach (ZoneRing ring in config.zoneRings)
        {
            if (ring == null) continue;
            foreach (var kv in keywordToAsset)
            {
                if (!ring.label.Contains(kv.Key, System.StringComparison.OrdinalIgnoreCase)) continue;
                string[] guids = AssetDatabase.FindAssets($"t:ZoneType {kv.Value}");
                foreach (string guid in guids)
                {
                    ZoneType zt = AssetDatabase.LoadAssetAtPath<ZoneType>(AssetDatabase.GUIDToAssetPath(guid));
                    if (zt != null && zt.GetDisplayName() == kv.Value)
                    {
                        ring.zoneType = zt;
                        break;
                    }
                }
                break;
            }
        }
        EditorUtility.SetDirty(config);
    }

    /// <summary>
    /// Collega i profili stradali di default (Autostrada, Strada Principale, Via Locale, Vicolo)
    /// ai campi corrispondenti di AmericanCityConfig.
    /// </summary>
    public static void LinkDefaultRoadProfilesToConfig(AmericanCityConfig config)
    {
        if (config == null) return;

        void TryLink(string assetName, System.Action<RoadProfile> setter)
        {
            string[] guids = AssetDatabase.FindAssets($"t:RoadProfile {assetName}");
            foreach (string guid in guids)
            {
                RoadProfile rp = AssetDatabase.LoadAssetAtPath<RoadProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (rp != null && rp.GetDisplayName() == assetName)
                {
                    setter(rp);
                    return;
                }
            }
        }

        TryLink("Autostrada",       rp => config.highwayProfile     = rp);
        TryLink("Strada Principale", rp => config.majorGridProfile   = rp);
        TryLink("Via Locale",       rp => config.localStreetProfile  = rp);
        TryLink("Vicolo",           rp => config.alleyProfile        = rp);
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

    // ─────────────────────────────────────────────────────────────────────────
    // CREATE EXAMPLE PREFABS AND ZONE DATA
    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/City Builder/Create Example Prefabs and Zone Data")]
    public static void CreateExamplePrefabsAndZoneData()
    {
        const string examplesRoot  = "Assets/BSCCityBuilder/Assets/Examples";
        const string materialsPath = "Assets/BSCCityBuilder/Assets/Examples/Materials";
        const string prefabsPath   = "Assets/BSCCityBuilder/Assets/Examples/Prefabs";

        EnsureFolder("Assets/BSCCityBuilder/Assets");
        EnsureFolder("Assets/BSCCityBuilder/Assets/Examples");
        EnsureFolder(materialsPath);
        EnsureFolder(prefabsPath);

        // ── Materials ─────────────────────────────────────────────────────────
        Material matSkyscraper  = CreateOrLoadMaterial(materialsPath, "Skyscraper",    new Color(0.40f, 0.50f, 0.70f)); // blu-vetro
        Material matOfficeTower = CreateOrLoadMaterial(materialsPath, "OfficeTower",   new Color(0.20f, 0.25f, 0.35f)); // vetro scuro
        Material matShop        = CreateOrLoadMaterial(materialsPath, "Shop",          new Color(0.85f, 0.55f, 0.20f)); // arancio caldo
        Material matOffice      = CreateOrLoadMaterial(materialsPath, "Office",        new Color(0.45f, 0.60f, 0.75f)); // blu-grigio
        Material matApartment   = CreateOrLoadMaterial(materialsPath, "Apartment",     new Color(0.85f, 0.78f, 0.65f)); // panna
        Material matRowHouse    = CreateOrLoadMaterial(materialsPath, "RowHouse",      new Color(0.70f, 0.38f, 0.28f)); // mattone
        Material matHouseWall   = CreateOrLoadMaterial(materialsPath, "HouseWall",     new Color(0.95f, 0.92f, 0.80f)); // giallo pallido
        Material matRoof        = CreateOrLoadMaterial(materialsPath, "Roof",          new Color(0.55f, 0.25f, 0.20f)); // rosso tetto
        Material matVilla       = CreateOrLoadMaterial(materialsPath, "Villa",         new Color(0.88f, 0.84f, 0.76f)); // bianco avorio
        Material matFarmhouse   = CreateOrLoadMaterial(materialsPath, "Farmhouse",     new Color(0.60f, 0.47f, 0.33f)); // marrone terra
        Material matBarn        = CreateOrLoadMaterial(materialsPath, "Barn",          new Color(0.65f, 0.15f, 0.10f)); // rosso fienile
        Material matPark        = CreateOrLoadMaterial(materialsPath, "Park",          new Color(0.22f, 0.62f, 0.18f)); // verde parco
        Material matPlaza       = CreateOrLoadMaterial(materialsPath, "Plaza",         new Color(0.75f, 0.72f, 0.68f)); // pietra chiara

        AssetDatabase.SaveAssets();

        // ── Prefab buildings ─────────────────────────────────────────────────
        // Center
        GameObject skyscraperPrefab  = CreateBuildingPrefab(prefabsPath, "Skyscraper",      new Vector3(6f, 30f, 6f),  matSkyscraper);
        GameObject officeTowerPrefab = CreateBuildingPrefab(prefabsPath, "OfficeTower",     new Vector3(8f, 20f, 8f),  matOfficeTower);
        // Commercial
        GameObject shopPrefab        = CreateBuildingPrefab(prefabsPath, "Shop",            new Vector3(10f, 5f, 8f),  matShop);
        GameObject officePrefab      = CreateBuildingPrefab(prefabsPath, "OfficeBuilding",  new Vector3(8f, 14f, 8f),  matOffice);
        // Residential
        GameObject apartmentPrefab   = CreateBuildingPrefab(prefabsPath, "ApartmentBlock",  new Vector3(8f, 8f, 8f),   matApartment);
        GameObject rowHousePrefab    = CreateBuildingPrefab(prefabsPath, "RowHouse",        new Vector3(6f, 8f, 6f),   matRowHouse);
        // Suburban
        GameObject housePrefab       = CreateHousePrefab   (prefabsPath, "DetachedHouse",   7f, 4f, 7f, 2.5f,         matHouseWall, matRoof);
        GameObject villaPrefab       = CreateHousePrefab   (prefabsPath, "Villa",           9f, 4.5f, 9f, 2.0f,       matVilla,     matRoof);
        // Rural
        GameObject farmhousePrefab   = CreateBuildingPrefab(prefabsPath, "Farmhouse",       new Vector3(6f, 4f, 8f),   matFarmhouse);
        GameObject barnPrefab        = CreateBuildingPrefab(prefabsPath, "Barn",            new Vector3(10f, 6f, 12f), matBarn);
        // Shared open-space
        GameObject parkPrefab        = CreateGroundPrefab  (prefabsPath, "Park",            new Vector3(16f, 0.25f, 16f), matPark);
        GameObject plazaPrefab       = CreateGroundPrefab  (prefabsPath, "Plaza",           new Vector3(12f, 0.30f, 12f), matPlaza);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        // ── Collega alle ZoneType ─────────────────────────────────────────────
        AssignPrefabsToZoneType("Center",      new[] { skyscraperPrefab, officeTowerPrefab, plazaPrefab });
        AssignPrefabsToZoneType("Commercial",  new[] { shopPrefab,       officePrefab,      plazaPrefab });
        AssignPrefabsToZoneType("Residential", new[] { apartmentPrefab,  rowHousePrefab,    parkPrefab  });
        AssignPrefabsToZoneType("Suburban",    new[] { housePrefab,      villaPrefab,        parkPrefab  });
        AssignPrefabsToZoneType("Rural",       new[] { farmhousePrefab,  barnPrefab,         parkPrefab  });

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Example Prefabs Creati",
            $"Creati 13 prefab di esempio con materiali colorati.\n\nPath: {examplesRoot}\n\nI prefab sono stati collegati alle 5 ZoneType.",
            "OK");

        Debug.Log("[CityBuilder] CreateExamplePrefabsAndZoneData completato.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PLUGIN SYSTEM
    // ─────────────────────────────────────────────────────────────────────────

    [MenuItem("Tools/City Builder/Setup Plugin Settings")]
    public static void SetupPluginSettings()
    {
        var settings = CityPluginSettingsEditorUtility.GetOrCreateSettings();
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = settings;

        string msg = "CityPluginSettings asset pronto.\n\n" +
                     "Aprire Window/City Builder/Plugin Browser per configurare i plugin attivi.";
        EditorUtility.DisplayDialog("Plugin Settings", msg, "OK");
        Debug.Log("[CityBuilder] Plugin settings: " + AssetDatabase.GetAssetPath(settings));
    }

    [MenuItem("Tools/City Builder/Open Plugin Browser")]
    public static void OpenPluginBrowser()
    {
        CityPluginBrowserWindow.ShowWindow();
    }

    [MenuItem("Tools/City Builder/Reload Plugin Registry")]
    public static void ReloadPluginRegistry()
    {
        CityPluginRegistry.Refresh();
        int count = 0;
        foreach (CityPluginCategory cat in System.Enum.GetValues(typeof(CityPluginCategory)))
            count += CityPluginRegistry.GetPlugins(cat).Count;

        string msg = $"Registry ricaricato. {count} plugin totali rilevati.";
        EditorUtility.DisplayDialog("Plugin Registry", msg, "OK");
        Debug.Log($"[CityBuilder] {msg}");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void EnsureFolder(string folderPath)
    {
        if (!AssetDatabase.IsValidFolder(folderPath))
        {
            string parent     = System.IO.Path.GetDirectoryName(folderPath).Replace('\\', '/');
            string folderName = System.IO.Path.GetFileName(folderPath);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }

    private static Material CreateOrLoadMaterial(string folder, string matName, Color color)
    {
        string matPath = $"{folder}/{matName}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (existing != null) return existing;

        Shader shader = Shader.Find("HDRP/Lit")
                     ?? Shader.Find("Universal Render Pipeline/Lit")
                     ?? Shader.Find("Standard");

        Material mat = new Material(shader) { name = matName };
        // _BaseColor works for HDRP/Lit and URP/Lit; _Color for Standard
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        else
            mat.SetColor("_Color", color);

        AssetDatabase.CreateAsset(mat, matPath);
        return mat;
    }

    /// <summary>Prefab con un singolo cubo (pivot al suolo).</summary>
    private static GameObject CreateBuildingPrefab(string folder, string prefabName, Vector3 size, Material material)
    {
        string prefabPath = $"{folder}/{prefabName}.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null) return existing;

        GameObject root = new GameObject(prefabName);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform);
        body.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
        body.transform.localScale    = size;
        body.GetComponent<MeshRenderer>().sharedMaterial = material;
        Object.DestroyImmediate(body.GetComponent<BoxCollider>());

        CityBuilderPrefab cbp       = root.AddComponent<CityBuilderPrefab>();
        cbp.footprintSize           = new Vector2(size.x, size.z);
        cbp.autoComputeFromRenderers = false;
        cbp.frontageOffset          = new Vector3(0f, 0f, -size.z * 0.5f);
        cbp.frontageDirection       = Vector3.back;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Prefab casa con corpo + tetto a padiglione (due cubi).</summary>
    private static GameObject CreateHousePrefab(string folder, string prefabName,
        float width, float height, float depth, float roofHeight,
        Material wallMat, Material roofMat)
    {
        string prefabPath = $"{folder}/{prefabName}.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null) return existing;

        GameObject root = new GameObject(prefabName);

        // Corpo
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(root.transform);
        body.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        body.transform.localScale    = new Vector3(width, height, depth);
        body.GetComponent<MeshRenderer>().sharedMaterial = wallMat;
        Object.DestroyImmediate(body.GetComponent<BoxCollider>());

        // Tetto (cubo schiacciato/allargato sopra il corpo)
        GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = "Roof";
        roof.transform.SetParent(root.transform);
        roof.transform.localPosition = new Vector3(0f, height + roofHeight * 0.5f, 0f);
        roof.transform.localScale    = new Vector3(width + 0.4f, roofHeight, depth + 0.4f);
        roof.GetComponent<MeshRenderer>().sharedMaterial = roofMat;
        Object.DestroyImmediate(roof.GetComponent<BoxCollider>());

        CityBuilderPrefab cbp       = root.AddComponent<CityBuilderPrefab>();
        cbp.footprintSize           = new Vector2(width, depth);
        cbp.autoComputeFromRenderers = false;
        cbp.frontageOffset          = new Vector3(0f, 0f, -depth * 0.5f);
        cbp.frontageDirection       = Vector3.back;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    /// <summary>Prefab piano (parco / piazza): cubo basso con pivot al suolo.</summary>
    private static GameObject CreateGroundPrefab(string folder, string prefabName, Vector3 size, Material material)
    {
        string prefabPath = $"{folder}/{prefabName}.prefab";
        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (existing != null) return existing;

        GameObject root = new GameObject(prefabName);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Ground";
        body.transform.SetParent(root.transform);
        body.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
        body.transform.localScale    = size;
        body.GetComponent<MeshRenderer>().sharedMaterial = material;
        Object.DestroyImmediate(body.GetComponent<BoxCollider>());

        CityBuilderPrefab cbp       = root.AddComponent<CityBuilderPrefab>();
        cbp.footprintSize           = new Vector2(size.x, size.z);
        cbp.autoComputeFromRenderers = false;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static void AssignPrefabsToZoneType(string zoneName, GameObject[] prefabs)
    {
        string[] guids = AssetDatabase.FindAssets($"t:ZoneType {zoneName}");
        foreach (string guid in guids)
        {
            ZoneType zt = AssetDatabase.LoadAssetAtPath<ZoneType>(AssetDatabase.GUIDToAssetPath(guid));
            if (zt != null && zt.GetDisplayName() == zoneName)
            {
                zt.SetPrefabs(prefabs);
                EditorUtility.SetDirty(zt);
                break;
            }
        }
    }

    // ─────────────────────────────────────────────────────────────────────────

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

}
