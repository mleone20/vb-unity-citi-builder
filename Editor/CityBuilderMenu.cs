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
}
