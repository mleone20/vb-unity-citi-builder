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
        AssetDatabase.SaveAssets();

        Undo.RecordObject(manager, "Assign City Data");
        manager.SetCityData(data);
        EditorUtility.SetDirty(manager);
        return data;
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
