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
    public static CityPluginSettings GetOrCreateSettings()
    {
        string settingsPath = CityBuilderAssetPaths.PluginSettingsPath;
        CityPluginSettings settings = AssetDatabase.LoadAssetAtPath<CityPluginSettings>(settingsPath);
        if (settings == null)
        {
            CityBuilderAssetPaths.EnsureFolder(CityBuilderAssetPaths.SettingsFolder);
            settings = ScriptableObject.CreateInstance<CityPluginSettings>();
            AssetDatabase.CreateAsset(settings, settingsPath);
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
        IReadOnlyList<CityPluginDescriptor> plugins = CityPluginRegistry.GetPlugins(category);

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

}

}
