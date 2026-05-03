using UnityEditor;
using UnityEngine;

public class LLMClientRequestPreviewWindow : EditorWindow
{
    private string prefabName;
    private Texture2D previewTexture;
    private LLMClient.AutoTagRequestData requestData;
    private Vector2 scroll;

    public static void ShowWindow(string prefabName, Texture2D previewTexture, LLMClient.AutoTagRequestData requestData)
    {
        LLMClientRequestPreviewWindow window = GetWindow<LLMClientRequestPreviewWindow>("LLM Request Preview");
        window.minSize = new Vector2(700f, 520f);
        window.prefabName = prefabName;
        window.previewTexture = previewTexture;
        window.requestData = requestData;
        window.Show();
        window.Focus();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Preview payload verso LLM", EditorStyles.boldLabel);

        if (requestData == null)
        {
            EditorGUILayout.HelpBox("Nessun payload disponibile.", MessageType.Warning);
            return;
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prefab", string.IsNullOrWhiteSpace(prefabName) ? "(n/d)" : prefabName);
        EditorGUILayout.LabelField("Endpoint", requestData.endpointUrl ?? string.Empty);
        EditorGUILayout.LabelField("Model", requestData.model ?? string.Empty);
        EditorGUILayout.LabelField("ZoneType inviati", requestData.zoneTypeCount.ToString());
        EditorGUILayout.LabelField("Dimensione immagine", requestData.imageByteCount + " bytes");
        EditorGUILayout.LabelField("API key", requestData.usingApiKey ? "Presente" : "Assente");

        EditorGUILayout.Space();
        if (previewTexture != null)
        {
            EditorGUILayout.LabelField("Preview immagine", EditorStyles.boldLabel);
            Rect rect = GUILayoutUtility.GetRect(220f, 220f, GUILayout.ExpandWidth(false));
            GUI.DrawTexture(rect, previewTexture, ScaleMode.ScaleToFit);
        }
        else
        {
            EditorGUILayout.HelpBox("Preview immagine non disponibile.", MessageType.Info);
        }

        EditorGUILayout.Space();
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("System Prompt", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(requestData.systemPrompt ?? string.Empty, GUILayout.MinHeight(64f));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("User Prompt", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(requestData.userPrompt ?? string.Empty, GUILayout.MinHeight(120f));

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Request JSON", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(requestData.requestJson ?? string.Empty, GUILayout.MinHeight(220f));

        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        if (GUILayout.Button("Copia Request JSON", GUILayout.Height(26f)))
        {
            EditorGUIUtility.systemCopyBuffer = requestData.requestJson ?? string.Empty;
            ShowNotification(new GUIContent("Request JSON copiato"));
        }
    }
}
