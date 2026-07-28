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

    public static CityManager FindOrCreate()
    {
        CityManager existing = Find();
        if (existing != null)
        {
            return existing;
        }

        GameObject managerObject = new GameObject("City Manager");
        Undo.RegisterCreatedObjectUndo(managerObject, "Create City Manager");
        CityManager manager = Undo.AddComponent<CityManager>(managerObject);
        CreateAndAssignData(manager);
        EditorSceneManager.MarkSceneDirty(managerObject.scene);
        return manager;
    }

    public static CityData CreateAndAssignData(CityManager manager)
    {
        if (manager == null)
        {
            return null;
        }

        CityData data = ScriptableObject.CreateInstance<CityData>();
        data.name = "CityData";
        CityBuilderAssetPaths.CreateUniqueAsset(data, "CityData.asset");

        Undo.RecordObject(manager, "Assign City Data");
        manager.SetCityData(data);
        EditorUtility.SetDirty(manager);
        return data;
    }
}
}
