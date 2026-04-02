using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CityBuilderPrefab))]
public class CityBuilderPrefabEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty footprintSize = serializedObject.FindProperty("footprintSize");
        SerializedProperty autoCompute = serializedObject.FindProperty("autoComputeFromRenderers");
        SerializedProperty pivotOffset = serializedObject.FindProperty("pivotOffset");

        using (new EditorGUI.DisabledScope(autoCompute.boolValue))
        {
            EditorGUILayout.PropertyField(footprintSize);
        }

        EditorGUILayout.PropertyField(autoCompute);
        EditorGUILayout.PropertyField(pivotOffset);

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Utilità Pivot", EditorStyles.boldLabel);

        if (GUILayout.Button("Auto ground pivot", GUILayout.Height(28)))
        {
            ApplyAutoGroundPivot((CityBuilderPrefab)target);
        }
    }

    private static void ApplyAutoGroundPivot(CityBuilderPrefab component)
    {
        Renderer[] renderers = component.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            EditorUtility.DisplayDialog("Auto ground pivot", "Nessun Renderer trovato nel prefab.", "OK");
            return;
        }

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                combined.Encapsulate(renderers[i].bounds);
            }
        }

        Vector3 bottomCenterWorld = new Vector3(combined.center.x, combined.min.y, combined.center.z);
        Vector3 localOffset = component.transform.InverseTransformPoint(bottomCenterWorld);

        Undo.RecordObject(component, "Auto ground pivot");
        component.pivotOffset = localOffset;
        EditorUtility.SetDirty(component);
    }
}
