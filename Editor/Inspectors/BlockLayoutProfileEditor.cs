using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using BSCCityBuilder.Core;

namespace BSCCityBuilder.Editor.Inspectors
{
[CustomEditor(typeof(BlockLayoutProfile))]
public class BlockLayoutProfileEditor : UnityEditor.Editor
{
    private ReorderableList operationsList;
    private SerializedProperty descriptionProperty;
    private SerializedProperty operationsProperty;

    private void OnEnable()
    {
        descriptionProperty = serializedObject.FindProperty("description");
        operationsProperty = serializedObject.FindProperty("operations");
        operationsList = new ReorderableList(
            serializedObject, operationsProperty, true, true, false, false);
        operationsList.drawHeaderCallback = rect =>
            EditorGUI.LabelField(rect, "Pipeline operazioni");
        operationsList.elementHeightCallback = index =>
            EditorGUIUtility.singleLineHeight + 6f;
        operationsList.drawElementCallback = (rect, index, active, focused) =>
        {
            SerializedProperty element = operationsProperty.GetArrayElementAtIndex(index);
            rect.y += 2f;
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(rect, element, GUIContent.none);
        };
        operationsList.drawFooterCallback = DrawFooter;
        operationsList.footerHeight = 24f;
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        EditorGUILayout.PropertyField(descriptionProperty);
        EditorGUILayout.HelpBox(
            "Le operazioni vengono eseguite dall'alto verso il basso. Puoi combinare operazioni built-in e operazioni fornite da plugin esterni.",
            MessageType.Info);
        operationsList.DoLayoutList();
        serializedObject.ApplyModifiedProperties();

        DrawSelectedOperationInspector();
    }

    private void DrawFooter(Rect rect)
    {
        float half = rect.width * 0.5f;
        if (GUI.Button(new Rect(rect.x, rect.y, half, rect.height), "+ Operazione"))
        {
            ShowOperationMenu();
        }
        using (new EditorGUI.DisabledScope(operationsList.index < 0))
        {
            if (GUI.Button(new Rect(rect.x + half, rect.y, half, rect.height), "Rimuovi"))
            {
                RemoveSelectedOperation();
            }
        }
    }

    private void ShowOperationMenu()
    {
        GenericMenu menu = new GenericMenu();
        var types = TypeCache.GetTypesDerivedFrom<BlockLayoutOperation>();
        for (int i = 0; i < types.Count; i++)
        {
            Type type = types[i];
            if (type == null || type.IsAbstract) continue;
            string label = ObjectNames.NicifyVariableName(type.Name.Replace("Operation", ""));
            Type capturedType = type;
            menu.AddItem(new GUIContent(label), false, () => AddOperation(capturedType));
        }
        if (menu.GetItemCount() == 0)
            menu.AddDisabledItem(new GUIContent("Nessuna operazione disponibile"));
        menu.ShowAsContext();
    }

    private void AddOperation(Type type)
    {
        BlockLayoutProfile profile = (BlockLayoutProfile)target;
        BlockLayoutOperation operation =
            ScriptableObject.CreateInstance(type) as BlockLayoutOperation;
        if (operation == null) return;

        operation.name = ObjectNames.NicifyVariableName(type.Name.Replace("Operation", ""));
        Undo.RegisterCreatedObjectUndo(operation, "Add Block Layout Operation");
        AssetDatabase.AddObjectToAsset(operation, profile);
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(profile));

        serializedObject.Update();
        operationsProperty.arraySize++;
        operationsProperty.GetArrayElementAtIndex(operationsProperty.arraySize - 1)
            .objectReferenceValue = operation;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        operationsList.index = operationsProperty.arraySize - 1;
    }

    private void RemoveSelectedOperation()
    {
        int index = operationsList.index;
        if (index < 0 || index >= operationsProperty.arraySize) return;
        UnityEngine.Object operation =
            operationsProperty.GetArrayElementAtIndex(index).objectReferenceValue;

        serializedObject.Update();
        int previousSize = operationsProperty.arraySize;
        operationsProperty.DeleteArrayElementAtIndex(index);
        if (operationsProperty.arraySize == previousSize)
        {
            operationsProperty.DeleteArrayElementAtIndex(index);
        }
        serializedObject.ApplyModifiedProperties();

        if (operation != null &&
            AssetDatabase.GetAssetPath(operation) == AssetDatabase.GetAssetPath(target))
        {
            Undo.DestroyObjectImmediate(operation);
        }
        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();
        operationsList.index = Mathf.Min(index, operationsProperty.arraySize - 1);
    }

    private void DrawSelectedOperationInspector()
    {
        int index = operationsList.index;
        if (index < 0 || index >= operationsProperty.arraySize) return;
        BlockLayoutOperation operation =
            operationsProperty.GetArrayElementAtIndex(index).objectReferenceValue
                as BlockLayoutOperation;
        if (operation == null) return;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Operazione selezionata", EditorStyles.boldLabel);
        bool isEmbedded =
            AssetDatabase.GetAssetPath(operation) == AssetDatabase.GetAssetPath(target);
        if (!isEmbedded)
        {
            EditorGUILayout.HelpBox(
                "Questa operazione è condivisa con altri profili. Duplicala nel profilo prima di personalizzarla senza effetti collaterali.",
                MessageType.Warning);
            if (GUILayout.Button("Duplica nel profilo"))
            {
                DuplicateSelectedOperation(operation, index);
                return;
            }
        }
        UnityEditor.Editor operationEditor = CreateEditor(operation);
        if (operationEditor != null)
        {
            operationEditor.OnInspectorGUI();
            DestroyImmediate(operationEditor);
        }
    }

    private void DuplicateSelectedOperation(BlockLayoutOperation source, int index)
    {
        BlockLayoutProfile profile = (BlockLayoutProfile)target;
        BlockLayoutOperation copy = Instantiate(source);
        copy.name = source.name + " (Custom)";
        Undo.RegisterCreatedObjectUndo(copy, "Duplicate Block Layout Operation");
        AssetDatabase.AddObjectToAsset(copy, profile);

        serializedObject.Update();
        operationsProperty.GetArrayElementAtIndex(index).objectReferenceValue = copy;
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(profile));
    }
}
}
