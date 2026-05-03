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

            Undo.RecordObject(comp, "Reset Frontage");
            comp.ResetFrontageToAutoDetectedDefault();
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
        // Calcola bounds in spazio LOCALE trasformando i corner world di ciascun renderer.
        // L'uso diretto di renderer.bounds (world-space) causava la scrittura di coordinate
        // assolute in pivotOffset, portando allo spawn sottoterra quando OnValidate scattava
        // su istanze già posizionate in scena a Y != 0.
        Renderer[] renderers = component.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            EditorUtility.DisplayDialog("Auto ground pivot", "Nessun Renderer trovato nel prefab.", "OK");
            return;
        }

        bool initialized = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] == null) continue;
            Bounds wb = renderers[i].bounds;
            Vector3 ext = wb.extents;
            Vector3 ctr = wb.center;
            Vector3[] corners =
            {
                ctr + new Vector3(-ext.x, -ext.y, -ext.z),
                ctr + new Vector3(-ext.x, -ext.y,  ext.z),
                ctr + new Vector3(-ext.x,  ext.y, -ext.z),
                ctr + new Vector3(-ext.x,  ext.y,  ext.z),
                ctr + new Vector3( ext.x, -ext.y, -ext.z),
                ctr + new Vector3( ext.x, -ext.y,  ext.z),
                ctr + new Vector3( ext.x,  ext.y, -ext.z),
                ctr + new Vector3( ext.x,  ext.y,  ext.z),
            };
            foreach (Vector3 corner in corners)
            {
                Vector3 lc = component.transform.InverseTransformPoint(corner);
                if (!initialized) { min = lc; max = lc; initialized = true; }
                else { min = Vector3.Min(min, lc); max = Vector3.Max(max, lc); }
            }
        }

        if (!initialized) return;

        Vector3 bottomCenterLocal = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);
        Undo.RecordObject(component, "Auto ground pivot");
        component.pivotOffset = bottomCenterLocal;
        EditorUtility.SetDirty(component);
    }
}
