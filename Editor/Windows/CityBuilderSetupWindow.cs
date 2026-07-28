using UnityEditor;
using UnityEngine;
using BSCCityBuilder.Editor.Tools;

namespace BSCCityBuilder.Editor.Windows
{
public sealed class CityBuilderSetupWindow : EditorWindow
{
    private Vector2 _scroll;

    [MenuItem("Window/City Builder/Setup")]
    public static void ShowWindow()
    {
        CityBuilderSetupWindow window = GetWindow<CityBuilderSetupWindow>("City Builder Setup");
        window.minSize = new Vector2(420f, 360f);
        window.Show();
        window.Focus();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("CITY BUILDER SETUP", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Crea e configura gli asset iniziali del City Builder. Le operazioni sono idempotenti salvo la creazione delle configurazioni, che usa nomi univoci.",
            MessageType.Info);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);

        DrawSection(
            "Asset fondamentali",
            "Zone Type",
            "Crea i cinque ZoneType predefiniti.",
            CityBuilderMenu.SetupDefaultZoneTypes);

        DrawAction(
            "Road Profile",
            "Crea i profili Autostrada, Strada Principale, Via Locale e Vicolo.",
            CityBuilderMenu.SetupDefaultRoadProfiles);

        DrawAction(
            "Plugin Settings",
            "Crea o seleziona l'asset delle impostazioni plugin.",
            CityBuilderMenu.SetupPluginSettings);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Contenuti iniziali", EditorStyles.boldLabel);

        DrawAction(
            "Configurazione città americana",
            "Crea una AmericanCityConfig e collega i ZoneType predefiniti.",
            CityBuilderMenu.CreateAmericanCityConfig);

        DrawAction(
            "Prefab e dati di esempio",
            "Crea materiali, prefab dimostrativi e collegamenti ai ZoneType.",
            CityBuilderMenu.CreateExamplePrefabsAndZoneData);

        EditorGUILayout.EndScrollView();
    }

    private static void DrawSection(string title, string button, string description, System.Action action)
    {
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        DrawAction(button, description, action);
    }

    private static void DrawAction(string button, string description, System.Action action)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField(description, EditorStyles.wordWrappedMiniLabel);
        if (GUILayout.Button(button, GUILayout.Height(30f)))
        {
            action();
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(3);
    }
}
}
