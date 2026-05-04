using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;

[CustomEditor(typeof(CityBuilderPrefab))]
[CanEditMultipleObjects]
public class CityBuilderPrefabEditor : Editor
{
    private const string AutoTagUndoName = "Auto Tag Building Prefab";

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty footprintSize = serializedObject.FindProperty("footprintSize");
        SerializedProperty autoCompute = serializedObject.FindProperty("autoComputeFromRenderers");
        SerializedProperty pivotOffset = serializedObject.FindProperty("pivotOffset");
        SerializedProperty frontageOffset = serializedObject.FindProperty("frontageOffset");
        SerializedProperty frontageDirection = serializedObject.FindProperty("frontageDirection");
        SerializedProperty frontageDisplayHeight = serializedObject.FindProperty("frontageDisplayHeight");
        SerializedProperty aiDescription = serializedObject.FindProperty("aiDescription");
        SerializedProperty aiSuggestedZoneDisplayNames = serializedObject.FindProperty("aiSuggestedZoneDisplayNames");

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

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("AI Tagging", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(aiDescription, new GUIContent("AI Description"));
        EditorGUILayout.PropertyField(aiSuggestedZoneDisplayNames, new GUIContent("AI Suggested Zone Types"), true);
    
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

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Auto Tagging LLM", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("LLM Settings", GUILayout.Height(24)))
        {
            LLMClientSettingsWindow.ShowWindow();
        }
        if (GUILayout.Button("Preview Request", GUILayout.Height(24)))
        {
            ShowLlmRequestPreview((CityBuilderPrefab)target);
        }
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Auto Tag with LLM", GUILayout.Height(28)))
        {
            TryAutoTagWithLlm((CityBuilderPrefab)target);
            serializedObject.Update();
        }
    }

    private static void TryAutoTagWithLlm(CityBuilderPrefab selectedComponent)
    {
        if (!TryPrepareAutoTagInput(
            selectedComponent,
            out GameObject prefabAsset,
            out CityBuilderPrefab prefabMetadata,
            out Texture2D previewTexture,
            out byte[] previewPng,
            out List<ZoneType> allZoneTypes,
            out List<LLMClient.ZoneTypeCandidate> candidates, 
            out string preparationError))
        {
            EditorUtility.DisplayDialog("AI Tagging", preparationError, "OK");
            return;
        }

        LLMClient llmClient = CreateConfiguredClient();
        if (!llmClient.TryAutoTagPrefab(previewPng, candidates, out LLMClient.AutoTagResponse response, out string error))
        {
            EditorUtility.DisplayDialog("AI Tagging", string.IsNullOrWhiteSpace(error) ? "LLMClient non disponibile." : error, "OK");
            return;
        }

        if (response == null)
        {
            EditorUtility.DisplayDialog("AI Tagging", "Risposta LLM nulla.", "OK");
            return;
        }

        List<string> normalizedSuggestions = NormalizeSuggestedZoneNames(response.zoneTypeDisplayNames);

        Undo.RecordObject(prefabMetadata, AutoTagUndoName);
        prefabMetadata.aiDescription = response.description ?? string.Empty;
        prefabMetadata.aiSuggestedZoneDisplayNames = normalizedSuggestions;
        EditorUtility.SetDirty(prefabMetadata);

        int zonesUpdated = 0;
        int duplicateEntries = 0;
        int unknownSuggestions = 0;

        for (int i = 0; i < normalizedSuggestions.Count; i++)
        {
            string suggestedName = normalizedSuggestions[i];
            ZoneType matchedZone = FindZoneTypeByDisplayName(allZoneTypes, suggestedName);
            if (matchedZone == null)
            {
                unknownSuggestions++;
                continue;
            }

            if (matchedZone.ContainsPrefab(prefabAsset))
            {
                duplicateEntries++;
                continue;
            }

            Undo.RecordObject(matchedZone, AutoTagUndoName);
            matchedZone.TryAddPrefab(prefabAsset, 1f);
            EditorUtility.SetDirty(matchedZone);
            zonesUpdated++;
        }

        AssetDatabase.SaveAssets();

        string report =
            "Prefab analizzato: " + prefabAsset.name + "\n" +
            "Zone suggerite: " + normalizedSuggestions.Count + "\n" +
            "Zone aggiornate: " + zonesUpdated + "\n" +
            "Duplicati evitati: " + duplicateEntries + "\n" +
            "Suggerimenti senza match: " + unknownSuggestions;

        EditorUtility.DisplayDialog("AI Tagging", report, "OK");
    }

    private static void ShowLlmRequestPreview(CityBuilderPrefab selectedComponent)
    {
        if (!TryPrepareAutoTagInput(
            selectedComponent,
            out GameObject prefabAsset,
            out CityBuilderPrefab prefabMetadata,
            out Texture2D previewTexture,
            out byte[] previewPng,
            out List<ZoneType> allZoneTypes,
            out List<LLMClient.ZoneTypeCandidate> candidates,
            out string preparationError))
        {
            EditorUtility.DisplayDialog("Preview Request LLM", preparationError, "OK");
            return;
        }

        LLMClient llmClient = CreateConfiguredClient();
        if (!llmClient.TryBuildAutoTagRequest(previewPng, candidates, out LLMClient.AutoTagRequestData requestData, out string error))
        {
            EditorUtility.DisplayDialog("Preview Request LLM", string.IsNullOrWhiteSpace(error) ? "Impossibile creare la request." : error, "OK");
            return;
        }

        LLMClientRequestPreviewWindow.ShowWindow(prefabAsset.name, previewTexture, requestData);
    }

    private static LLMClient CreateConfiguredClient()
    {
        string baseUrl = LLMClientEditorSettings.GetBaseUrl();
        string model = LLMClientEditorSettings.GetModel();
        string apiKey = LLMClientEditorSettings.GetApiKey();
        return new LLMClient(baseUrl, model, apiKey);
    }

    private static bool TryPrepareAutoTagInput(
        CityBuilderPrefab selectedComponent,
        out GameObject prefabAsset,
        out CityBuilderPrefab prefabMetadata,
        out Texture2D previewTexture,
        out byte[] previewPng,
        out List<ZoneType> allZoneTypes,
        out List<LLMClient.ZoneTypeCandidate> candidates,
        out string error)
    {
        prefabAsset = null;
        prefabMetadata = null;
        previewTexture = null;
        previewPng = null;
        allZoneTypes = null;
        candidates = null;
        error = null;

        if (selectedComponent == null)
        {
            error = "CityBuilderPrefab non valido.";
            return false;
        }

        prefabAsset = ResolvePrefabAsset(selectedComponent.gameObject);
        if (prefabAsset == null)
        {
            error = "Seleziona un prefab asset del progetto (o una sua istanza) che contiene CityBuilderPrefab.";
            return false;
        }

        prefabMetadata = prefabAsset.GetComponent<CityBuilderPrefab>();
        if (prefabMetadata == null)
        {
            error = "Il prefab selezionato non contiene CityBuilderPrefab.";
            return false;
        }

        if (!TryGetRenderedModelPreview(prefabAsset, out previewTexture, out string previewError))
        {
            error = previewError;
            return false;
        }

        previewPng = EncodeTextureToPng(previewTexture);
        if (previewPng == null || previewPng.Length == 0)
        {
            error = "Impossibile convertire la preview in PNG.";
            return false;
        }

        allZoneTypes = LoadAllZoneTypes();
        if (allZoneTypes.Count == 0)
        {
            error = "Nessun ZoneType trovato nel progetto.";
            return false;
        }

        candidates = BuildZoneTypeCandidates(allZoneTypes);
        return true;
    }

    private static bool TryGetRenderedModelPreview(GameObject prefabAsset, out Texture2D previewTexture, out string error)
    {
        previewTexture = null;
        error = null;

        if (prefabAsset == null)
        {
            error = "Prefab non valido per la preview renderizzata.";
            return false;
        }

        previewTexture = AssetPreview.GetAssetPreview(prefabAsset);
        if (previewTexture != null)
        {
            return true;
        }

        // Forza la richiesta di generazione preview e segnala all'utente di riprovare.
        AssetPreview.GetAssetPreview(prefabAsset);

        bool stillLoading = AssetPreview.IsLoadingAssetPreview(prefabAsset.GetInstanceID()) || AssetPreview.IsLoadingAssetPreviews();
        if (stillLoading)
        {
            error = "La preview renderizzata del modello e in preparazione. Attendi qualche istante e riprova.";
            return false;
        }

        error = "Impossibile generare una preview renderizzata del modello per questo prefab.";
        return false;
    }

    private static GameObject ResolvePrefabAsset(GameObject source)
    {
        if (source == null)
        {
            return null;
        }

        if (PrefabUtility.IsPartOfPrefabAsset(source))
        {
            return source;
        }

        GameObject nearestPrefabRoot = PrefabUtility.GetNearestPrefabInstanceRoot(source);
        if (nearestPrefabRoot == null)
        {
            return null;
        }

        return PrefabUtility.GetCorrespondingObjectFromSource(nearestPrefabRoot);
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
                displayName = zone.GetDisplayName(),
                description = zone.description ?? string.Empty
            });
        }

        return candidates;
    }

    private static List<string> NormalizeSuggestedZoneNames(List<string> names)
    {
        List<string> normalized = new List<string>();
        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (names == null)
        {
            return normalized;
        }

        for (int i = 0; i < names.Count; i++)
        {
            string candidate = names[i];
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            string trimmed = candidate.Trim();
            if (seen.Add(trimmed))
            {
                normalized.Add(trimmed);
            }
        }

        return normalized;
    }

    private static ZoneType FindZoneTypeByDisplayName(List<ZoneType> zones, string displayName)
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

    private static bool ContainsPrefabReference(List<GameObject> prefabs, GameObject prefabAsset)
    {
        if (prefabs == null || prefabAsset == null)
        {
            return false;
        }

        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] == prefabAsset)
            {
                return true;
            }
        }

        return false;
    }

    private static byte[] EncodeTextureToPng(Texture2D source)
    {
        if (source == null)
        {
            return null;
        }

        RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32);
        RenderTexture previous = RenderTexture.active;
        Texture2D readableTexture = null;

        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;

            readableTexture = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false);
            readableTexture.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            readableTexture.Apply(false, false);
            return readableTexture.EncodeToPNG();
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);

            if (readableTexture != null)
            {
                DestroyImmediate(readableTexture);
            }
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
