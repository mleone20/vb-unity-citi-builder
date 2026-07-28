using System;
using UnityEditor;
using UnityEngine;
using BSCCityBuilder.Config;
using BSCCityBuilder.Management;
using BSCCityBuilder.Plugins;

namespace BSCCityBuilder.Editor.Plugins.Default
{
public sealed class RandomScatterCityConfigEditorTools : ICityConfigEditorTools
{
    public Type ConfigType => typeof(RandomScatterCityConfig);
    public int Order => 100;
    public string Title => "STRUMENTI RANDOM SCATTER";

    public void DrawTools(CityConfig config, CityManager manager)
    {
        RandomScatterCityConfig randomConfig = config as RandomScatterCityConfig;
        if (randomConfig == null)
        {
            return;
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Centro dalla Scene View", GUILayout.Height(28f)))
        {
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                Undo.RecordObject(randomConfig, "Set Random City Center");
                randomConfig.centerWorldPosition = sceneView.pivot;
                EditorUtility.SetDirty(randomConfig);
            }
        }

        if (GUILayout.Button("Nuovo seed", GUILayout.Height(28f)))
        {
            Undo.RecordObject(randomConfig, "Randomize City Seed");
            randomConfig.randomSeed = UnityEngine.Random.Range(int.MinValue, int.MaxValue);
            EditorUtility.SetDirty(randomConfig);
        }
        EditorGUILayout.EndHorizontal();
    }
}
}
