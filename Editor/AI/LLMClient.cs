using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using UnityEngine;

namespace BSCCityBuilder.AI
{

/// <summary>
/// Client per integrazione con LLM locale tramite API OpenAI-compatible (LM Studio).
/// Il trasporto usa esclusivamente HttpClient.
/// </summary>
public class LLMClient
{
    public const string DefaultBaseUrl = "http://100.105.213.67:11434";
    public const string DefaultChatCompletionsPath = "/v1/chat/completions";
    public const string DefaultModel = "local-model";

    private static readonly HttpClient SharedHttpClient = new HttpClient
    {
        Timeout = TimeSpan.FromSeconds(90)
    };

    private readonly string baseUrl;
    private readonly string model;
    private readonly string apiKey;

    public LLMClient(string baseUrl = DefaultBaseUrl, string model = DefaultModel, string apiKey = null)
    {
        this.baseUrl = string.IsNullOrWhiteSpace(baseUrl) ? DefaultBaseUrl : baseUrl.TrimEnd('/');
        this.model = string.IsNullOrWhiteSpace(model) ? DefaultModel : model;
        this.apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
    }

    [System.Serializable]
    public class ZoneTypeCandidate
    {
        public string displayName;
        public string description;
    }

    [System.Serializable]
    public class AutoTagResponse
    {
        public string description;
        public List<string> zoneTypeDisplayNames = new List<string>();
    }

    [Serializable]
    public class AutoTagRequestData
    {
        public string endpointUrl;
        public string model;
        public string systemPrompt;
        public string userPrompt;
        public string imageDataUrl;
        public string requestJson;
        public int imageByteCount;
        public int zoneTypeCount;
        public bool usingApiKey;
    }

    [Serializable]
    private class ChatCompletionsResponse
    {
        public Choice[] choices;
        public ApiError error;
    }

    [Serializable]
    private class Choice
    {
        public ChatMessage message;
    }

    [Serializable]
    private class ChatMessage
    {
        public string role;
        public string content;
    }

    [Serializable]
    private class ApiError
    {
        public string message;
    }

    public bool TryAutoTagPrefab(byte[] prefabScreenshotPng, List<ZoneTypeCandidate> zoneTypeCandidates, out AutoTagResponse response, out string error)
    {
        response = null;
        if (!TryBuildAutoTagRequest(prefabScreenshotPng, zoneTypeCandidates, out AutoTagRequestData requestData, out error))
        {
            return false;
        }

        string responseBody;
        System.Net.HttpStatusCode statusCode;
        bool isSuccess;

        try
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, requestData.endpointUrl))
            {
                request.Content = new StringContent(requestData.requestJson, Encoding.UTF8, "application/json");
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                }

                using (HttpResponseMessage httpResponse = SharedHttpClient.SendAsync(request).GetAwaiter().GetResult())
                {
                    statusCode = httpResponse.StatusCode;
                    isSuccess = httpResponse.IsSuccessStatusCode;
                    responseBody = httpResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                }
            }
        }
        catch (Exception ex)
        {
            error = "Errore HTTP verso LM Studio: " + ex.Message;
            return false;
        }

        if (!isSuccess)
        {
            string apiMessage = TryExtractApiError(responseBody);
            error = string.IsNullOrWhiteSpace(apiMessage)
                ? "LM Studio ha risposto con stato HTTP " + (int)statusCode + "."
                : apiMessage;
            return false;
        }

        ChatCompletionsResponse envelope;
        try
        {
            envelope = JsonUtility.FromJson<ChatCompletionsResponse>(responseBody);
        }
        catch (Exception ex)
        {
            error = "Risposta LM Studio non valida (JSON): " + ex.Message;
            return false;
        }

        if (envelope == null)
        {
            error = "Risposta LM Studio vuota.";
            return false;
        }

        if (envelope.error != null && !string.IsNullOrWhiteSpace(envelope.error.message))
        {
            error = envelope.error.message;
            return false;
        }

        if (envelope.choices == null || envelope.choices.Length == 0 || envelope.choices[0] == null || envelope.choices[0].message == null)
        {
            error = "LM Studio non ha restituito nessuna scelta valida.";
            return false;
        }

        string modelContent = envelope.choices[0].message.content;
        if (string.IsNullOrWhiteSpace(modelContent))
        {
            error = "LM Studio ha restituito contenuto vuoto.";
            return false;
        }

        string normalizedContent = ExtractJsonObject(modelContent);
        if (string.IsNullOrWhiteSpace(normalizedContent))
        {
            error = "Risposta del modello non contiene un JSON valido.";
            return false;
        }

        try
        {
            response = JsonUtility.FromJson<AutoTagResponse>(normalizedContent);
        }
        catch (Exception ex)
        {
            error = "Impossibile leggere il JSON di risposta del modello: " + ex.Message;
            return false;
        }

        if (response == null)
        {
            error = "Risposta del modello non deserializzabile.";
            return false;
        }

        response.description = response.description ?? string.Empty;
        response.zoneTypeDisplayNames = response.zoneTypeDisplayNames ?? new List<string>();
        return true;
    }

    public bool TryBuildAutoTagRequest(byte[] prefabScreenshotPng, List<ZoneTypeCandidate> zoneTypeCandidates, out AutoTagRequestData requestData, out string error)
    {
        requestData = null;
        error = null;

        if (prefabScreenshotPng == null || prefabScreenshotPng.Length == 0)
        {
            error = "Screenshot prefab assente o vuoto.";
            return false;
        }

        if (zoneTypeCandidates == null || zoneTypeCandidates.Count == 0)
        {
            error = "Nessun ZoneType disponibile da inviare al modello.";
            return false;
        }

        string systemPrompt = BuildSystemPrompt();
        string userPrompt = BuildUserPrompt(zoneTypeCandidates);
        string imageDataUrl = "data:image/png;base64," + Convert.ToBase64String(prefabScreenshotPng);
        string requestJson = BuildChatCompletionsRequestJson(systemPrompt, userPrompt, imageDataUrl);

        requestData = new AutoTagRequestData
        {
            endpointUrl = baseUrl + DefaultChatCompletionsPath,
            model = model,
            systemPrompt = systemPrompt,
            userPrompt = userPrompt,
            imageDataUrl = imageDataUrl,
            requestJson = requestJson,
            imageByteCount = prefabScreenshotPng.Length,
            zoneTypeCount = zoneTypeCandidates.Count,
            usingApiKey = !string.IsNullOrWhiteSpace(apiKey)
        };

        return true;
    }

    private string BuildSystemPrompt()
    {
        return "Sei un classificatore di prefab edificio per city builder. " +
               "Rispondi solo in JSON puro senza markdown o testo extra. " +
               "Schema obbligatorio: {\"description\":\"string\",\"zoneTypeDisplayNames\":[\"DisplayName1\",\"DisplayName2\"]}.";
    }

    private static string BuildUserPrompt(List<ZoneTypeCandidate> zoneTypeCandidates)
    {
        StringBuilder sb = new StringBuilder(1024);
        sb.AppendLine("Analizza l'immagine del prefab edificio.");
        sb.AppendLine("Scegli solo DisplayName presenti nel catalogo seguente:");

        for (int i = 0; i < zoneTypeCandidates.Count; i++)
        {
            ZoneTypeCandidate zone = zoneTypeCandidates[i];
            string display = zone != null && !string.IsNullOrWhiteSpace(zone.displayName) ? zone.displayName.Trim() : string.Empty;
            string description = zone != null && !string.IsNullOrWhiteSpace(zone.description) ? zone.description.Trim() : string.Empty;
            sb.Append("- DisplayName: ").Append(display).Append(" | Description: ").Append(description).AppendLine();
        }

        sb.AppendLine("Restituisci una descrizione breve dell'edificio e la lista dei DisplayName compatibili.");
        return sb.ToString();
    }

    private string BuildChatCompletionsRequestJson(string systemPrompt, string userPrompt, string imageDataUrl)
    {
        StringBuilder json = new StringBuilder(4096);
        json.Append("{");
        json.Append("\"model\":\"").Append(EscapeJson(model)).Append("\",");
        json.Append("\"temperature\":0.2,");
        json.Append("\"messages\":[");

        json.Append("{");
        json.Append("\"role\":\"system\",");
        json.Append("\"content\":\"").Append(EscapeJson(systemPrompt)).Append("\"");
        json.Append("},");

        json.Append("{");
        json.Append("\"role\":\"user\",");
        json.Append("\"content\":[");
        json.Append("{");
        json.Append("\"type\":\"text\",");
        json.Append("\"text\":\"").Append(EscapeJson(userPrompt)).Append("\"");
        json.Append("},");
        json.Append("{");
        json.Append("\"type\":\"image_url\",");
        json.Append("\"image_url\":{");
        json.Append("\"url\":\"").Append(EscapeJson(imageDataUrl)).Append("\"");
        json.Append("}");
        json.Append("}");
        json.Append("]");
        json.Append("}");

        json.Append("]");
        json.Append("}");
        return json.ToString();
    }

    private static string EscapeJson(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        StringBuilder sb = new StringBuilder(value.Length + 16);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '"': sb.Append("\\\""); break;
                case '\\': sb.Append("\\\\"); break;
                case '\b': sb.Append("\\b"); break;
                case '\f': sb.Append("\\f"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                case '\t': sb.Append("\\t"); break;
                default:
                    if (c < 32)
                    {
                        sb.Append("\\u").Append(((int)c).ToString("x4"));
                    }
                    else
                    {
                        sb.Append(c);
                    }
                    break;
            }
        }

        return sb.ToString();
    }

    private static string TryExtractApiError(string responseBody)
    {
        if (string.IsNullOrWhiteSpace(responseBody))
        {
            return null;
        }

        try
        {
            ChatCompletionsResponse envelope = JsonUtility.FromJson<ChatCompletionsResponse>(responseBody);
            if (envelope != null && envelope.error != null && !string.IsNullOrWhiteSpace(envelope.error.message))
            {
                return envelope.error.message;
            }
        }
        catch
        {
        }

        return responseBody;
    }

    private static string ExtractJsonObject(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        string trimmed = content.Trim();

        if (trimmed.StartsWith("```") && trimmed.EndsWith("```"))
        {
            int firstNewLine = trimmed.IndexOf('\n');
            if (firstNewLine >= 0)
            {
                trimmed = trimmed.Substring(firstNewLine + 1);
            }

            int lastFence = trimmed.LastIndexOf("```");
            if (lastFence >= 0)
            {
                trimmed = trimmed.Substring(0, lastFence).Trim();
            }
        }

        int start = trimmed.IndexOf('{');
        int end = trimmed.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return null;
        }

        return trimmed.Substring(start, end - start + 1);
    }
}

} // namespace BSCCityBuilder.AI
