using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class LLMBulkClassifierWindow : EditorWindow
{
    private readonly List<GameObject> prefabs = new List<GameObject>();
    private Vector2 scroll;
    private int previewResolution = 1024;

    [MenuItem("Tools/City Builder/LLM Bulk Classifier")]
    public static void ShowWindow()
    {
        LLMBulkClassifierWindow window = GetWindow<LLMBulkClassifierWindow>("LLM Bulk Classifier");
        window.minSize = new Vector2(560f, 420f);
        window.Show();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Classificatore Bulk Prefab", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Seleziona piu prefab dal Project, genera preview high-res e invia ogni prefab al modello locale.", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Aggiungi prefab selezionati", GUILayout.Height(24f)))
        {
            AddSelectedPrefabs();
        }

        if (GUILayout.Button("Svuota lista", GUILayout.Height(24f)))
        {
            prefabs.Clear();
        }
        EditorGUILayout.EndHorizontal();

        previewResolution = EditorGUILayout.IntSlider("Risoluzione preview", previewResolution, 256, 2048);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Prefab in coda: " + prefabs.Count, EditorStyles.miniBoldLabel);

        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MinHeight(220f));
        for (int i = 0; i < prefabs.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            prefabs[i] = (GameObject)EditorGUILayout.ObjectField(prefabs[i], typeof(GameObject), false);
            if (GUILayout.Button("X", GUILayout.Width(26f)))
            {
                prefabs.RemoveAt(i);
                i--;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        if (GUILayout.Button("Classifica Bulk", GUILayout.Height(34f)))
        {
            RunBulkClassification();
        }
    }

    private void AddSelectedPrefabs()
    {
        UnityEngine.Object[] selected = Selection.objects;
        for (int i = 0; i < selected.Length; i++)
        {
            GameObject go = selected[i] as GameObject;
            if (go == null)
            {
                continue;
            }

            string path = AssetDatabase.GetAssetPath(go);
            if (string.IsNullOrWhiteSpace(path) || !path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!prefabs.Contains(go))
            {
                prefabs.Add(go);
            }
        }
    }

    private void RunBulkClassification()
    {
        if (prefabs.Count == 0)
        {
            EditorUtility.DisplayDialog("LLM Bulk Classifier", "Nessun prefab in lista.", "OK");
            return;
        }

        List<ZoneType> allZoneTypes = LoadAllZoneTypes();
        if (allZoneTypes.Count == 0)
        {
            EditorUtility.DisplayDialog("LLM Bulk Classifier", "Nessun ZoneType trovato nel progetto.", "OK");
            return;
        }

        List<LLMClient.ZoneTypeCandidate> candidates = BuildZoneTypeCandidates(allZoneTypes);
        LLMClient llmClient = new LLMClient(
            LLMClientEditorSettings.GetBaseUrl(),
            LLMClientEditorSettings.GetModel(),
            LLMClientEditorSettings.GetApiKey());

        int processed = 0;
        int succeeded = 0;
        int zoneAssignments = 0;
        int skippedNoMetadata = 0;
        int failed = 0;

        List<string> errors = new List<string>();

        try
        {
            for (int i = 0; i < prefabs.Count; i++)
            {
                GameObject prefab = prefabs[i];
                if (prefab == null)
                {
                    continue;
                }

                processed++;
                EditorUtility.DisplayProgressBar("LLM Bulk Classifier", "Classificazione " + prefab.name + " (" + (i + 1) + "/" + prefabs.Count + ")", (float)(i + 1) / prefabs.Count);

                CityBuilderPrefab metadata = prefab.GetComponent<CityBuilderPrefab>();
                if (metadata == null)
                {
                    skippedNoMetadata++;
                    errors.Add(prefab.name + ": CityBuilderPrefab non trovato.");
                    continue;
                }

                if (!PrefabHighResPreviewRenderer.TryRenderToPng(prefab, previewResolution, previewResolution, out Texture2D previewTexture, out byte[] pngBytes, out string renderError))
                {
                    failed++;
                    errors.Add(prefab.name + ": " + renderError);
                    continue;
                }

                try
                {
                    if (!llmClient.TryAutoTagPrefab(pngBytes, candidates, out LLMClient.AutoTagResponse response, out string llmError))
                    {
                        failed++;
                        errors.Add(prefab.name + ": " + llmError);
                        continue;
                    }

                    if (response == null)
                    {
                        failed++;
                        errors.Add(prefab.name + ": risposta LLM nulla.");
                        continue;
                    }

                    List<string> normalizedSuggestions = NormalizeSuggestions(response.zoneTypeDisplayNames);

                    Undo.RecordObject(metadata, "Bulk Auto Tag Prefabs");
                    metadata.aiDescription = response.description ?? string.Empty;
                    metadata.aiSuggestedZoneDisplayNames = normalizedSuggestions;
                    EditorUtility.SetDirty(metadata);

                    zoneAssignments += ApplyZoneAssignments(prefab, allZoneTypes, normalizedSuggestions);
                    succeeded++;
                }
                finally
                {
                    if (previewTexture != null)
                    {
                        DestroyImmediate(previewTexture);
                    }
                }
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        AssetDatabase.SaveAssets();

        string report =
            "Prefab processati: " + processed + "\n" +
            "Classificazioni riuscite: " + succeeded + "\n" +
            "Assegnazioni zona aggiunte: " + zoneAssignments + "\n" +
            "Prefab senza CityBuilderPrefab: " + skippedNoMetadata + "\n" +
            "Classificazioni fallite: " + failed;

        if (errors.Count > 0)
        {
            int maxErrorsShown = Mathf.Min(10, errors.Count);
            report += "\n\nErrori (" + maxErrorsShown + "/" + errors.Count + "):\n";
            for (int i = 0; i < maxErrorsShown; i++)
            {
                report += "- " + errors[i] + "\n";
            }
        }

        EditorUtility.DisplayDialog("LLM Bulk Classifier", report, "OK");
    }

    private static List<ZoneType> LoadAllZoneTypes()
    {
        string[] guids = AssetDatabase.FindAssets("t:ZoneType");
        List<ZoneType> result = new List<ZoneType>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            ZoneType zone = AssetDatabase.LoadAssetAtPath<ZoneType>(path);
            if (zone != null)
            {
                result.Add(zone);
            }
        }

        return result;
    }

    private static List<LLMClient.ZoneTypeCandidate> BuildZoneTypeCandidates(List<ZoneType> zones)
    {
        List<LLMClient.ZoneTypeCandidate> candidates = new List<LLMClient.ZoneTypeCandidate>(zones.Count);
        for (int i = 0; i < zones.Count; i++)
        {
            ZoneType zone = zones[i];
            candidates.Add(new LLMClient.ZoneTypeCandidate
            {
                displayName = zone != null ? zone.GetDisplayName() : string.Empty,
                description = zone != null && !string.IsNullOrWhiteSpace(zone.description) ? zone.description : string.Empty
            });
        }

        return candidates;
    }

    private static List<string> NormalizeSuggestions(List<string> suggestions)
    {
        List<string> result = new List<string>();
        if (suggestions == null)
        {
            return result;
        }

        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < suggestions.Count; i++)
        {
            string suggestion = suggestions[i];
            if (string.IsNullOrWhiteSpace(suggestion))
            {
                continue;
            }

            string trimmed = suggestion.Trim();
            if (seen.Add(trimmed))
            {
                result.Add(trimmed);
            }
        }

        return result;
    }

    private static int ApplyZoneAssignments(GameObject prefab, List<ZoneType> allZoneTypes, List<string> suggestedZoneNames)
    {
        int added = 0;
        if (prefab == null || allZoneTypes == null || suggestedZoneNames == null)
        {
            return added;
        }

        for (int i = 0; i < suggestedZoneNames.Count; i++)
        {
            string suggestion = suggestedZoneNames[i];
            ZoneType zone = FindZoneByDisplayName(allZoneTypes, suggestion);
            if (zone == null)
            {
                continue;
            }

            if (zone.buildingPrefabs == null)
            {
                Undo.RecordObject(zone, "Bulk Auto Tag Prefabs");
                zone.buildingPrefabs = new List<GameObject>();
            }

            if (zone.buildingPrefabs.Contains(prefab))
            {
                continue;
            }

            Undo.RecordObject(zone, "Bulk Auto Tag Prefabs");
            zone.buildingPrefabs.Add(prefab);
            EditorUtility.SetDirty(zone);
            added++;
        }

        return added;
    }

    private static ZoneType FindZoneByDisplayName(List<ZoneType> zones, string displayName)
    {
        if (zones == null || string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        for (int i = 0; i < zones.Count; i++)
        {
            ZoneType zone = zones[i];
            if (zone == null)
            {
                continue;
            }

            if (string.Equals(zone.GetDisplayName(), displayName, StringComparison.OrdinalIgnoreCase))
            {
                return zone;
            }
        }

        return null;
    }
}
