using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ZoneType))]
public class ZoneTypeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty displayName = serializedObject.FindProperty("displayName");
        SerializedProperty zoneColor = serializedObject.FindProperty("zoneColor");
        SerializedProperty buildingHeight = serializedObject.FindProperty("buildingHeight");
        SerializedProperty description = serializedObject.FindProperty("description");

        EditorGUILayout.LabelField("Zone Type", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(displayName, new GUIContent("Display Name"));
        EditorGUILayout.PropertyField(description, new GUIContent("Description"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Visual", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(zoneColor, new GUIContent("Zone Color"));
        EditorGUILayout.PropertyField(buildingHeight, new GUIContent("Building Height"));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Preset", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Residential"))
        {
            ApplyPreset(displayName, zoneColor, buildingHeight, description, "Residential", new Color(0.2f, 0.75f, 0.3f), 8f, "Low to medium density housing.");
        }
        if (GUILayout.Button("Commercial"))
        {
            ApplyPreset(displayName, zoneColor, buildingHeight, description, "Commercial", new Color(0.2f, 0.45f, 0.95f), 14f, "Retail and office frontage.");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Industrial"))
        {
            ApplyPreset(displayName, zoneColor, buildingHeight, description, "Industrial", new Color(0.9f, 0.75f, 0.2f), 10f, "Warehouses, workshops, logistics.");
        }
        if (GUILayout.Button("Special"))
        {
            ApplyPreset(displayName, zoneColor, buildingHeight, description, "Special", new Color(0.45f, 0.45f, 0.45f), 18f, "Landmarks, civic or unique functions.");
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        DrawPreview(zoneColor.colorValue, buildingHeight.floatValue);

        serializedObject.ApplyModifiedProperties();
    }

    private static void ApplyPreset(SerializedProperty displayName, SerializedProperty zoneColor, SerializedProperty buildingHeight, SerializedProperty description, string presetName, Color color, float height, string presetDescription)
    {
        displayName.stringValue = presetName;
        zoneColor.colorValue = color;
        buildingHeight.floatValue = height;
        description.stringValue = presetDescription;
    }

    private static void DrawPreview(Color color, float height)
    {
        Rect rect = GUILayoutUtility.GetRect(1f, 96f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.12f, 0.12f, 0.12f));

        float normalizedHeight = Mathf.Clamp01(height / 20f);
        float previewHeight = Mathf.Lerp(20f, rect.height - 16f, normalizedHeight);
        float previewWidth = Mathf.Min(80f, rect.width * 0.3f);
        Rect buildingRect = new Rect(rect.center.x - previewWidth * 0.5f, rect.yMax - previewHeight - 8f, previewWidth, previewHeight);

        EditorGUI.DrawRect(buildingRect, color);
        EditorGUI.DrawRect(new Rect(buildingRect.x, buildingRect.y, buildingRect.width, 1f), Color.white * 0.75f);
        EditorGUI.LabelField(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, 18f), $"Preview height: {height:0.0}m", EditorStyles.whiteLabel);
    }
}