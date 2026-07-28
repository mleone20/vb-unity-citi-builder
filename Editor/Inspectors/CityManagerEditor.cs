using UnityEditor;
using UnityEngine;
using BSCCityBuilder.Management;
using BSCCityBuilder.Editor.Windows;

namespace BSCCityBuilder.Editor.Inspectors
{
[CustomEditor(typeof(CityManager))]
public sealed class CityManagerEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        GUILayout.Space(4f);
        if (GUILayout.Button("Apri City Builder", GUILayout.Height(34f)))
        {
            CityBuilderWindow.ShowWindow();
        }
    }
}
}
