using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Config;
using BSCCityBuilder.Generation;
using BSCCityBuilder.Plugins;

namespace BSCCityBuilder.Editor.Plugins
{
public static class CityPluginSettingsEditorUtility
{
    private const string SettingsAssetPath = "Assets/BSCCityBuilder/Assets/CityPluginSettings.asset";

    public static CityPluginSettings GetOrCreateSettings()
    {
        CityPluginSettings settings = AssetDatabase.LoadAssetAtPath<CityPluginSettings>(SettingsAssetPath);
        if (settings == null)
        {
            EnsureAssetFolder();
            settings = ScriptableObject.CreateInstance<CityPluginSettings>();
            AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            AssetDatabase.SaveAssets();
        }

        EnsureSelections(settings);
        return settings;
    }

    public static void EnsureSelections(CityPluginSettings settings)
    {
        if (settings == null)
        {
            return;
        }

        bool changed = false;
        changed |= EnsureSelection(settings, CityPluginCategory.Process);
        changed |= EnsureSelection(settings, CityPluginCategory.RoadNetwork);
        changed |= EnsureSelection(settings, CityPluginCategory.RoadPlanarization);
        changed |= EnsureSelection(settings, CityPluginCategory.BlockDetection);
        changed |= EnsureSelection(settings, CityPluginCategory.Zoning);
        changed |= EnsureSelection(settings, CityPluginCategory.LotLayout);
        changed |= EnsureSelection(settings, CityPluginCategory.LotSelection);

        if (changed)
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }

    private static bool EnsureSelection(CityPluginSettings settings, CityPluginCategory category)
    {
        string current = settings.GetActivePluginId(category);
        List<CityPluginDescriptor> plugins = CityPluginRegistry.GetPlugins(category);

        if (plugins.Count == 0)
        {
            if (!string.IsNullOrEmpty(current))
            {
                settings.SetActivePluginId(category, string.Empty);
                return true;
            }

            return false;
        }

        for (int i = 0; i < plugins.Count; i++)
        {
            if (plugins[i].id == current)
            {
                return false;
            }
        }

        settings.SetActivePluginId(category, plugins[0].id);
        return true;
    }

    private static void EnsureAssetFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/BSCCityBuilder"))
        {
            AssetDatabase.CreateFolder("Assets", "BSCCityBuilder");
        }

        if (!AssetDatabase.IsValidFolder("Assets/BSCCityBuilder/Assets"))
        {
            AssetDatabase.CreateFolder("Assets/BSCCityBuilder", "Assets");
        }
    }
}

}
