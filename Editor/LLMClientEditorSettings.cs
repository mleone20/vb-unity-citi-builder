using UnityEditor;

public static class LLMClientEditorSettings
{
    private const string BaseUrlKey = "BSCCityBuilder.LLM.BaseUrl";
    private const string ModelKey = "BSCCityBuilder.LLM.Model";
    private const string ApiKeyKey = "BSCCityBuilder.LLM.ApiKey";

    public static string GetBaseUrl()
    {
        return EditorPrefs.GetString(BaseUrlKey, LLMClient.DefaultBaseUrl);
    }

    public static string GetModel()
    {
        return EditorPrefs.GetString(ModelKey, LLMClient.DefaultModel);
    }

    public static string GetApiKey()
    {
        return EditorPrefs.GetString(ApiKeyKey, string.Empty);
    }

    public static void SetValues(string baseUrl, string model, string apiKey)
    {
        EditorPrefs.SetString(BaseUrlKey, string.IsNullOrWhiteSpace(baseUrl) ? LLMClient.DefaultBaseUrl : baseUrl.Trim());
        EditorPrefs.SetString(ModelKey, string.IsNullOrWhiteSpace(model) ? LLMClient.DefaultModel : model.Trim());
        EditorPrefs.SetString(ApiKeyKey, string.IsNullOrWhiteSpace(apiKey) ? string.Empty : apiKey.Trim());
    }

    public static void ResetToDefaults()
    {
        SetValues(LLMClient.DefaultBaseUrl, LLMClient.DefaultModel, string.Empty);
    }
}
