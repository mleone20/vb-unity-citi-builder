using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Editor.Plugins;

namespace BSCCityBuilder.Editor.Tools
{
public static class CityManagerSceneUtility
{
    public static CityManager Find()
    {
        return Object.FindAnyObjectByType<CityManager>();
    }

    public static CityManager[] FindAll()
    {
        return Object.FindObjectsByType<CityManager>();
    }

    public static CityManager Create(string cityName, string cityDataAssetPath)
    {
        if (string.IsNullOrWhiteSpace(cityName) || string.IsNullOrWhiteSpace(cityDataAssetPath))
        {
            return null;
        }

        if (AssetDatabase.LoadAssetAtPath<Object>(cityDataAssetPath) != null)
        {
            EditorUtility.DisplayDialog(
                "Asset già esistente",
                "Esiste già un asset nel percorso:\n" + cityDataAssetPath,
                "OK");
            return null;
        }

        GameObject managerObject = new GameObject(cityName);
        Undo.RegisterCreatedObjectUndo(managerObject, "Create City Manager");
        CityManager manager = Undo.AddComponent<CityManager>(managerObject);
        CreateAndAssignData(manager, cityName, cityDataAssetPath);
        EditorSceneManager.MarkSceneDirty(managerObject.scene);
        return manager;
    }

    public static CityData CreateAndAssignData(
        CityManager manager,
        string cityName,
        string cityDataAssetPath)
    {
        if (manager == null ||
            string.IsNullOrWhiteSpace(cityName) ||
            string.IsNullOrWhiteSpace(cityDataAssetPath))
        {
            return null;
        }

        CityData data = ScriptableObject.CreateInstance<CityData>();
        data.name = cityName;
        AssetDatabase.CreateAsset(data, cityDataAssetPath);
        CityPalette palette = CreatePaletteForData(data, cityName, cityDataAssetPath);
        data.SetPalette(palette);
        EditorUtility.SetDirty(data);
        AssetDatabase.SaveAssets();

        Undo.RecordObject(manager, "Assign City Data");
        manager.SetCityData(data);
        EditorUtility.SetDirty(manager);
        return data;
    }

    public static CityPalette CreatePaletteForData(
        CityData data,
        string cityName,
        string cityDataAssetPath = null)
    {
        if (data == null) return null;

        string dataPath = !string.IsNullOrWhiteSpace(cityDataAssetPath)
            ? cityDataAssetPath
            : AssetDatabase.GetAssetPath(data);
        string directory = string.IsNullOrWhiteSpace(dataPath)
            ? CityBuilderAssetPaths.DataFolder
            : Path.GetDirectoryName(dataPath)?.Replace("\\", "/");
        CityBuilderAssetPaths.EnsureFolder(directory);

        string safeName = string.IsNullOrWhiteSpace(cityName) ? data.name : cityName;
        string palettePath = AssetDatabase.GenerateUniqueAssetPath(
            directory + "/" + safeName + " Palette.asset");
        CityPalette palette = ScriptableObject.CreateInstance<CityPalette>();
        palette.name = safeName + " Palette";

        RoadProfile defaultProfile = data.GetDefaultRoadProfile();
        if (defaultProfile != null) palette.SetDefaultRoadProfile(defaultProfile);
        foreach (CitySegment segment in data.segments)
            if (segment != null) palette.AddRoadProfile(segment.roadProfile);
        foreach (CityBlock block in data.blocks)
            if (block != null) palette.AddZoneType(block.zoning);

        AssetDatabase.CreateAsset(palette, palettePath);
        data.SetPalette(palette);
        EditorUtility.SetDirty(data);
        return palette;
    }

    public static string AskCityDataAssetPath(string defaultCityName)
    {
        CityBuilderAssetPaths.EnsureFolder(CityBuilderAssetPaths.DataFolder);
        string safeDefaultName = string.IsNullOrWhiteSpace(defaultCityName)
            ? "New City"
            : defaultCityName;
        return EditorUtility.SaveFilePanelInProject(
            "Crea città",
            safeDefaultName,
            "asset",
            "Scegli il nome della città e la posizione del relativo asset CityData.",
            CityBuilderAssetPaths.DataFolder);
    }

    public static string GetCityNameFromAssetPath(string assetPath)
    {
        return string.IsNullOrWhiteSpace(assetPath)
            ? string.Empty
            : Path.GetFileNameWithoutExtension(assetPath);
    }
}
}
