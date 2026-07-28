using System;
using UnityEditor;
using UnityEngine;
using BSCCityBuilder.Config;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;

namespace BSCCityBuilder.Editor.Plugins.Default
{
public sealed class AmericanCityConfigEditorTools : ICityConfigEditorTools
{
    public Type ConfigType => typeof(AmericanCityConfig);
    public int Order => 100;
    public string Title => "STRUMENTI AMERICAN CITY";

    public void DrawTools(CityConfig config, CityManager manager)
    {
        AmericanCityConfig american = config as AmericanCityConfig;
        CityData data = manager != null ? manager.GetCityData() : null;
        if (american == null || data == null)
        {
            return;
        }

        EditorGUILayout.HelpBox(
            "Strumenti aggiunti dalla configurazione American City attiva.",
            MessageType.None);

        if (!GUILayout.Button("Aggiorna profili strade esistenti", GUILayout.Height(28f)))
        {
            return;
        }

        Undo.RecordObject(data, "Update American Road Profiles");
        int updated = 0;
        foreach (CitySegment segment in data.segments)
        {
            if (segment == null)
            {
                continue;
            }

            RoadProfile profile = segment.roadProfile;
            if (profile == null)
            {
                if (american.highwayProfile != null &&
                    segment.width >= american.highwayProfile.roadWidth * 0.75f)
                    profile = american.highwayProfile;
                else if (american.majorGridProfile != null)
                    profile = american.majorGridProfile;
                else
                    profile = american.localStreetProfile;
            }

            if (profile != null)
            {
                segment.roadProfile = profile;
                segment.width = profile.roadWidth;
                updated++;
            }
        }

        EditorUtility.SetDirty(data);
        SceneView.RepaintAll();
        EditorUtility.DisplayDialog("Profili aggiornati", updated + " segmenti aggiornati.", "OK");
    }
}
}
