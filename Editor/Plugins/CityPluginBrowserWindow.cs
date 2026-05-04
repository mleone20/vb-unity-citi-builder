using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

public class CityPluginBrowserWindow : EditorWindow
{
    private Vector2 _scroll;

    [MenuItem("Window/City Builder/Plugin Browser")]
    public static void ShowWindow()
    {
        GetWindow<CityPluginBrowserWindow>("Plugin Browser");
    }

    private void OnGUI()
    {
        if (GUILayout.Button("Refresh Discovery", GUILayout.Height(24)))
        {
            CityPluginRegistry.Refresh();
        }

        CityPluginSettings settings = CityPluginSettingsEditorUtility.GetOrCreateSettings();
        if (settings == null)
        {
            EditorGUILayout.HelpBox("Impossibile caricare CityPluginSettings.", MessageType.Error);
            return;
        }

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        DrawCategory(settings, CityPluginCategory.Process, "Process");
        DrawCategory(settings, CityPluginCategory.RoadNetwork, "Road Network");
        DrawCategory(settings, CityPluginCategory.RoadPlanarization, "Road Planarization");
        DrawCategory(settings, CityPluginCategory.BlockDetection, "Block Detection");
        DrawCategory(settings, CityPluginCategory.Zoning, "Zoning");
        DrawCategory(settings, CityPluginCategory.LotLayout, "Lot Layout");
        DrawCategory(settings, CityPluginCategory.LotSelection, "Lot Selection");

        EditorGUILayout.Space(8);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("Pipeline Options", EditorStyles.boldLabel);
        settings.runPlanarizationAfterRoadNetwork = EditorGUILayout.Toggle("Planarize after Generate Road", settings.runPlanarizationAfterRoadNetwork);
        settings.runPlanarizationInFullGeneration = EditorGUILayout.Toggle("Planarize in Generate All", settings.runPlanarizationInFullGeneration);
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();

        if (GUI.changed)
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }
    }

    private void DrawCategory(CityPluginSettings settings, CityPluginCategory category, string label)
    {
        List<CityPluginDescriptor> plugins = CityPluginRegistry.GetPlugins(category);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);

        if (plugins.Count == 0)
        {
            EditorGUILayout.HelpBox("Nessun plugin trovato per questa categoria.", MessageType.Warning);
            EditorGUILayout.EndVertical();
            return;
        }

        string[] names = new string[plugins.Count];
        int selectedIndex = 0;
        string selectedId = settings.GetActivePluginId(category);

        for (int i = 0; i < plugins.Count; i++)
        {
            names[i] = plugins[i].displayName + " [" + plugins[i].id + "]";
            if (plugins[i].id == selectedId)
            {
                selectedIndex = i;
            }
        }

        int newIndex = EditorGUILayout.Popup("Active", selectedIndex, names);
        if (newIndex != selectedIndex)
        {
            settings.SetActivePluginId(category, plugins[newIndex].id);
        }

        CityPluginDescriptor active = plugins[newIndex];
        if (!string.IsNullOrWhiteSpace(active.description))
        {
            EditorGUILayout.HelpBox(active.description, MessageType.None);
        }

        EditorGUILayout.LabelField("Type", active.pluginType.Name, EditorStyles.miniLabel);
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(4);
    }
}
