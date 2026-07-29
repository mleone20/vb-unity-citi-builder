using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BSCCityBuilder.Core;
using BSCCityBuilder.Generation;
using BSCCityBuilder.Management;
using BSCCityBuilder.Editor.Tools;

namespace BSCCityBuilder.Editor.Windows
{
public sealed class CityRoadElementInspectorWindow : EditorWindow
{
    private CityManager manager;
    private Vector2 scrollPosition;

    [MenuItem("Tools/City Builder/Road Selection Inspector")]
    public static void OpenFromMenu()
    {
        ShowForManager(CityManagerSceneUtility.Find());
    }

    public static void ShowForManager(CityManager cityManager)
    {
        CityRoadElementInspectorWindow window = FindOpenWindow();
        if (window == null)
        {
            window = CreateInstance<CityRoadElementInspectorWindow>();
            window.titleContent = new GUIContent("Road Inspector");
            window.minSize = new Vector2(320f, 330f);
            window.ShowUtility();
        }

        window.manager = cityManager;
        window.Repaint();
    }

    private static CityRoadElementInspectorWindow FindOpenWindow()
    {
        CityRoadElementInspectorWindow[] windows =
            Resources.FindObjectsOfTypeAll<CityRoadElementInspectorWindow>();
        return windows.Length > 0 ? windows[0] : null;
    }

    private void OnEnable()
    {
        if (manager == null)
        {
            manager = CityManagerSceneUtility.Find();
        }
        EditorApplication.update += RepaintIfOpen;
    }

    private void OnDisable()
    {
        EditorApplication.update -= RepaintIfOpen;
    }

    private void RepaintIfOpen()
    {
        Repaint();
    }

    private void OnGUI()
    {
        if (manager == null)
        {
            manager = CityManagerSceneUtility.Find();
        }

        CityData data = manager != null ? manager.GetCityData() : null;
        if (manager == null || data == null)
        {
            EditorGUILayout.HelpBox(
                "Nessun CityManager con CityData trovato nella scena.",
                MessageType.Warning);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        CityNode node = data.GetNode(manager.GetSelectedNodeID());
        CitySegment segment = data.GetSegment(manager.GetSelectedSegmentID());
        if (node != null)
        {
            DrawNode(data, node);
        }
        else if (segment != null)
        {
            DrawSegment(data, segment);
        }
        else
        {
            EditorGUILayout.HelpBox(
                "Seleziona un nodo o un segmento nella Scene View.",
                MessageType.Info);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawNode(CityData data, CityNode node)
    {
        EditorGUILayout.LabelField("NODO / CONGIUNZIONE", EditorStyles.boldLabel);
        EditorGUILayout.Space(3);
        int connections = node.connectedSegmentIDs != null ? node.connectedSegmentIDs.Count : 0;
        EditorGUILayout.LabelField("ID", node.id.ToString());
        EditorGUILayout.LabelField("Strade collegate", connections.ToString());

        CityRoundaboutSettings settings =
            node.roundabout ?? new CityRoundaboutSettings();
        EditorGUI.BeginChangeCheck();
        CityJunctionType junctionType = (CityJunctionType)EditorGUILayout.EnumPopup(
            new GUIContent("Tipo", "Auto crea una rotonda con almeno tre strade."),
            node.junctionType);

        float islandRadius = settings.islandRadius;
        float carriagewayWidth = settings.carriagewayWidth;
        int resolution = settings.resolution;
        bool generateIsland = settings.generateIsland;
        Material islandMaterial = settings.islandMaterial;

        if (junctionType != CityJunctionType.Standard)
        {
            EditorGUILayout.Space(6);
            islandRadius = EditorGUILayout.FloatField("Raggio isola", islandRadius);
            carriagewayWidth = EditorGUILayout.FloatField("Carreggiata", carriagewayWidth);
            resolution = EditorGUILayout.IntSlider("Risoluzione", resolution, 12, 96);
            EditorGUILayout.HelpBox(
                "Il materiale dell'anello viene ricavato automaticamente dal Road Profile della strada collegata più larga.",
                MessageType.None);
            generateIsland = EditorGUILayout.Toggle("Genera isola", generateIsland);
            if (generateIsland)
            {
                islandMaterial = (Material)EditorGUILayout.ObjectField(
                    "Materiale isola",
                    islandMaterial,
                    typeof(Material),
                    false);
            }

            float outerRadius =
                Mathf.Max(1f, islandRadius) + Mathf.Max(2f, carriagewayWidth);
            EditorGUILayout.LabelField("Raggio esterno", outerRadius.ToString("F2") + " m");
            if (connections < 3)
            {
                EditorGUILayout.HelpBox(
                    "La rotonda richiede almeno tre strade collegate.",
                    MessageType.Warning);
            }
        }

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(data, "Edit Road Junction");
            node.junctionType = junctionType;
            if (node.roundabout == null)
            {
                node.roundabout = new CityRoundaboutSettings();
            }
            node.roundabout.islandRadius = Mathf.Max(1f, islandRadius);
            node.roundabout.carriagewayWidth = Mathf.Max(2f, carriagewayWidth);
            node.roundabout.resolution = Mathf.Clamp(resolution, 12, 96);
            node.roundabout.generateIsland = generateIsland;
            node.roundabout.islandMaterial = islandMaterial;
            EditorUtility.SetDirty(data);
            SceneView.RepaintAll();
        }
    }

    private void DrawSegment(CityData data, CitySegment segment)
    {
        EditorGUILayout.LabelField("SEGMENTO", EditorStyles.boldLabel);
        EditorGUILayout.Space(3);
        EditorGUILayout.LabelField("ID", segment.id.ToString());
        EditorGUILayout.LabelField(
            "Nodi",
            segment.nodeA_ID + " \u2192 " + segment.nodeB_ID);
        EditorGUILayout.LabelField(
            "Lunghezza",
            CityRoadGeometry.EstimateLength(data, segment).ToString("F2") + " m");
        EditorGUILayout.LabelField(
            "Larghezza",
            CityRoadGeometry.GetRoadWidth(data, segment).ToString("F2") + " m");

        List<RoadProfile> profiles = RoadProfileEditorUtility.LoadRoadProfiles(data);
        string[] labels = new string[profiles.Count + 1];
        labels[0] = "None";
        int profileIndex = 0;
        for (int i = 0; i < profiles.Count; i++)
        {
            labels[i + 1] = RoadProfileEditorUtility.GetRoadProfileDisplayName(profiles[i]);
            if (segment.roadProfile == profiles[i])
            {
                profileIndex = i + 1;
            }
        }

        int newProfileIndex = EditorGUILayout.Popup("Road Profile", profileIndex, labels);
        RoadProfile newProfile =
            newProfileIndex > 0 ? profiles[newProfileIndex - 1] : null;
        if (newProfile != segment.roadProfile)
        {
            Undo.RecordObject(data, "Set Segment Road Profile");
            manager.SetSegmentRoadProfile(segment.id, newProfile);
            EditorUtility.SetDirty(data);
        }

        CitySegmentGeometryType geometry =
            (CitySegmentGeometryType)EditorGUILayout.EnumPopup(
                "Geometria",
                segment.geometryType);
        if (geometry != segment.geometryType)
        {
            Undo.RecordObject(data, "Set Segment Geometry");
            manager.SetSegmentGeometryType(segment.id, geometry);
            EditorUtility.SetDirty(data);
            SceneView.RepaintAll();
        }

        if (segment.IsCurved() &&
            GUILayout.Button("Reset maniglie Bezier", GUILayout.Height(28f)))
        {
            Undo.RecordObject(data, "Reset Segment Curve Handles");
            manager.ResetSegmentBezierHandles(segment.id);
            EditorUtility.SetDirty(data);
            SceneView.RepaintAll();
        }
    }
}
}
