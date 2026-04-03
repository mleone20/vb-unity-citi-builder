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
        SerializedProperty frontageOffset = serializedObject.FindProperty("frontageOffset");
        SerializedProperty frontageDirection = serializedObject.FindProperty("frontageDirection");
        SerializedProperty frontageDisplayHeight = serializedObject.FindProperty("frontageDisplayHeight");

        using (new EditorGUI.DisabledScope(autoCompute.boolValue))
        {
            EditorGUILayout.PropertyField(footprintSize);
        }

        EditorGUILayout.PropertyField(autoCompute);
        EditorGUILayout.PropertyField(pivotOffset);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Affaccio (Frontage)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(frontageOffset, new GUIContent("Frontage Offset", "Posizione del piano di affaccio in spazio locale. Indica la direzione frontale verso la strada."));
        EditorGUILayout.PropertyField(frontageDirection, new GUIContent("Frontage Direction", "Normale locale del piano di affaccio. Permette di ruotare l'affaccio."));
        EditorGUILayout.PropertyField(frontageDisplayHeight, new GUIContent("Altezza Gizmo", "Altezza visiva del piano arancio (solo estetica)."));

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.Space();

        if (GUILayout.Button("Reset Frontage", GUILayout.Height(24)))
        {
            CityBuilderPrefab comp = (CityBuilderPrefab)target;
            SerializedProperty frontageOffsetInitialized = serializedObject.FindProperty("frontageOffsetInitialized");
            SerializedProperty frontageDirectionInitialized = serializedObject.FindProperty("frontageDirectionInitialized");

            Undo.RecordObject(comp, "Reset Frontage");
            frontageOffsetInitialized.boolValue = false;
            frontageDirectionInitialized.boolValue = false;
            serializedObject.ApplyModifiedProperties();
            comp.SendMessage("OnValidate", SendMessageOptions.DontRequireReceiver);
            EditorUtility.SetDirty(comp);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Utilità Pivot", EditorStyles.boldLabel);

        if (GUILayout.Button("Auto ground pivot", GUILayout.Height(28)))
        {
            ApplyAutoGroundPivot((CityBuilderPrefab)target);
        }
    }

    private void OnSceneGUI()
    {
        CityBuilderPrefab comp = (CityBuilderPrefab)target;
        if (comp == null) return;

        Transform t = comp.transform;
        Vector3 frontageWorld = t.TransformPoint(comp.frontageOffset);

        EditorGUI.BeginChangeCheck();
        Vector3 newFrontageWorld = Handles.PositionHandle(frontageWorld, t.rotation);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(comp, "Sposta Frontage");
            comp.frontageOffset = t.InverseTransformPoint(newFrontageWorld);
            EditorUtility.SetDirty(comp);
        }

        Quaternion currentRotation = Quaternion.LookRotation(t.TransformDirection(comp.GetFrontageDirectionLocal()), Vector3.up);
        EditorGUI.BeginChangeCheck();
        Quaternion newRotation = Handles.RotationHandle(currentRotation, frontageWorld);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(comp, "Ruota Frontage");
            Vector3 worldDirection = newRotation * Vector3.forward;
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude > 0.0001f)
            {
                comp.frontageDirection = t.InverseTransformDirection(worldDirection.normalized);
                comp.frontageDirection.y = 0f;
            }
            EditorUtility.SetDirty(comp);
        }

        Handles.color = new Color(1f, 0.55f, 0f, 0.9f);
        Handles.Label(frontageWorld + Vector3.up * (comp.frontageDisplayHeight + 0.3f), "Frontage");
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

        Undo.RecordObject(component, "Auto ground pivot");
        component.pivotOffset = bottomCenterWorld;
        EditorUtility.SetDirty(component);
    }
}
