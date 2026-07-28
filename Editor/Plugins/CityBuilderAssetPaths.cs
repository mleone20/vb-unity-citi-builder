using System.IO;
using UnityEditor;
using UnityEngine;

namespace BSCCityBuilder.Editor.Plugins
{
public static class CityBuilderAssetPaths
{
    private const string FallbackRoot = "Assets/CityBuilder";
    private static string _packageRoot;

    public static string PackageRoot
    {
        get
        {
            if (!string.IsNullOrEmpty(_packageRoot))
            {
                return _packageRoot;
            }

            string[] guids = AssetDatabase.FindAssets("CityBuilderAssetPaths t:Script");
            if (guids.Length > 0)
            {
                string scriptPath = AssetDatabase.GUIDToAssetPath(guids[0]);
                string pluginsFolder = Normalize(Path.GetDirectoryName(scriptPath));
                string editorFolder = Normalize(Path.GetDirectoryName(pluginsFolder));
                _packageRoot = Normalize(Path.GetDirectoryName(editorFolder));
            }

            return string.IsNullOrEmpty(_packageRoot) ? FallbackRoot : _packageRoot;
        }
    }

    public static string DataFolder => PackageRoot + "/Assets";
    public static string SettingsFolder => DataFolder + "/Settings";
    public static string PluginSettingsPath => SettingsFolder + "/CityPluginSettings.asset";

    public static void EnsureFolder(string assetFolder)
    {
        string normalized = Normalize(assetFolder);
        if (AssetDatabase.IsValidFolder(normalized))
        {
            return;
        }

        string parent = Normalize(Path.GetDirectoryName(normalized));
        string name = Path.GetFileName(normalized);
        if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(name))
        {
            Debug.LogError("[CityBuilderAssetPaths] Percorso asset non valido: " + assetFolder);
            return;
        }

        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }

    public static string CreateUniqueAsset<T>(T asset, string fileName, string folder = null)
        where T : Object
    {
        string targetFolder = string.IsNullOrEmpty(folder) ? DataFolder : folder;
        EnsureFolder(targetFolder);
        string path = AssetDatabase.GenerateUniqueAssetPath(targetFolder + "/" + fileName);
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        return path;
    }

    private static string Normalize(string path)
    {
        return string.IsNullOrEmpty(path) ? string.Empty : path.Replace('\\', '/');
    }
}
}
