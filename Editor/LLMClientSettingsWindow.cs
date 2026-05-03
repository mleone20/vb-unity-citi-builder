using UnityEditor;
using UnityEngine;

public class LLMClientSettingsWindow : EditorWindow
{
    private string baseUrl;
    private string model;
    private string apiKey;

    [MenuItem("Tools/City Builder/LLM Settings")]
    public static void ShowWindow()
    {
        LLMClientSettingsWindow window = GetWindow<LLMClientSettingsWindow>("LLM Settings");
        window.minSize = new Vector2(420f, 220f);
        window.Load();
        window.Show();
    }

    private void OnEnable()
    {
        Load();
    }

    private void Load()
    {
        baseUrl = LLMClientEditorSettings.GetBaseUrl();
        model = LLMClientEditorSettings.GetModel();
        apiKey = LLMClientEditorSettings.GetApiKey();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Configurazione LLM Locale", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Questi valori vengono usati dal tagging automatico prefab.", MessageType.Info);

        EditorGUILayout.Space();
        baseUrl = EditorGUILayout.TextField("Base URL", baseUrl);
        model = EditorGUILayout.TextField("Model", model);
        apiKey = EditorGUILayout.PasswordField("API Key", apiKey);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Salva", GUILayout.Height(28f)))
        {
            Save();
        }

        if (GUILayout.Button("Reset Default", GUILayout.Height(28f)))
        {
            LLMClientEditorSettings.ResetToDefaults();
            Load();
            ShowNotification(new GUIContent("Configurazione ripristinata"));
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Endpoint effettivo", EditorStyles.miniBoldLabel);
        string normalizedBaseUrl = string.IsNullOrWhiteSpace(baseUrl) ? LLMClient.DefaultBaseUrl : baseUrl.TrimEnd('/');
        EditorGUILayout.SelectableLabel(normalizedBaseUrl + LLMClient.DefaultChatCompletionsPath, GUILayout.Height(18f));
    }

    private void Save()
    {
        LLMClientEditorSettings.SetValues(baseUrl, model, apiKey);
        baseUrl = LLMClientEditorSettings.GetBaseUrl();
        model = LLMClientEditorSettings.GetModel();
        apiKey = LLMClientEditorSettings.GetApiKey();
        ShowNotification(new GUIContent("Configurazione LLM salvata"));
    }
}
