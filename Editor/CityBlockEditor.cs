using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor script per il rilevamento e modifica manuale dei blocchi.
/// Fornisce UI per auto-detect e definizione manuale di blocchi.
/// </summary>
public class CityBlockEditor
{
    private static List<List<Vector3>> suggestedBlocks = new List<List<Vector3>>();
    private static bool showingPreview = false;
    private static List<int> selectedManualNodeIds = new List<int>();

    public static void DrawBlockEditorUI(CityManager manager, ref bool isEditingBlocks)
    {
        CityData cityData = manager.GetCityData();
        if (cityData == null) return;

        EditorGUILayout.LabelField("BLOCCHI - Ibrido (Auto + Manuale)", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Creazione Manuale Blocco (da lista nodi)", EditorStyles.label);
        if (GUILayout.Button("Avvia Selezione Nodi (Mode: Crea Blocco)", GUILayout.Height(24)))
        {
            selectedManualNodeIds.Clear();
            manager.SetMode(CityManager.BuildMode.CreateBlock);
        }

        if (selectedManualNodeIds.Count > 0)
        {
            EditorGUILayout.HelpBox($"Nodi selezionati: {string.Join(" -> ", selectedManualNodeIds)}\n" +
                                    "Click sul primo nodo per chiudere e creare il blocco.", MessageType.Info);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Crea Blocco Ora", GUILayout.Height(24)))
        {
            TryCreateManualBlock(manager);
        }

        if (GUILayout.Button("Reset Lista Nodi", GUILayout.Height(24)))
        {
            selectedManualNodeIds.Clear();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // Sezione Auto-Detect
        EditorGUILayout.LabelField("Auto-Detect Blocchi", EditorStyles.label);
        
        if (GUILayout.Button("Suggerisci Blocchi (Rileva su grafo)", GUILayout.Height(30)))
        {
            SuggestBlocksFromGraph(cityData);
        }

        if (showingPreview && GUILayout.Button("Nascondi Anteprima Suggeriti", GUILayout.Height(24)))
        {
            showingPreview = false;
            SceneView.RepaintAll();
        }

        if (GUILayout.Button("Conferma Blocchi Suggeriti", GUILayout.Height(30)))
        {
            if (suggestedBlocks.Count > 0)
            {
                ConfirmSuggestedBlocks(manager);
                showingPreview = false;
            }
            else
            {
                EditorUtility.DisplayDialog("Info", "Nessun blocco suggerito! Clicca 'Suggerisci Blocchi' prima.", "OK");
            }
        }

        EditorGUILayout.Space();

        // Mostra anteprima blocchi suggeriti
        if (showingPreview && suggestedBlocks.Count > 0)
        {
            EditorGUILayout.HelpBox($"Blocchi suggeriti: {suggestedBlocks.Count}. Conferma per salvare.", MessageType.Info);
        }

        EditorGUILayout.Space();

        // Mostra blocchi correnti
        EditorGUILayout.LabelField($"Blocchi totali: {cityData.blocks.Count}", EditorStyles.label);

        for (int i = 0; i < cityData.blocks.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            
            CityBlock block = cityData.blocks[i];
            EditorGUILayout.LabelField($"Block {block.id}: {block.vertices.Count} vertici, Area: {block.GetArea():F2}");
            
            if (GUILayout.Button("Rimuovi", GUILayout.Width(80)))
            {
                cityData.blocks.RemoveAt(i);
                return;
            }
            
            EditorGUILayout.EndHorizontal();
        }
    }

    private static void SuggestBlocksFromGraph(CityData cityData)
    {
        suggestedBlocks.Clear();

        // Chiama algoritmo rilevamento blocchi
        suggestedBlocks = CityBlockDetector.DetectBlocks(cityData);

        if (suggestedBlocks.Count > 0)
        {
            showingPreview = true;
            Debug.Log($"[CityBlockEditor] {suggestedBlocks.Count} blocchi suggeriti!");
            SceneView.RepaintAll();
        }
        else
        {
            showingPreview = false;
            Debug.LogWarning("[CityBlockEditor] Nessun blocco rilevato. Verifica che il grafo stradale sia chiuso.");
            SceneView.RepaintAll();
        }
    }

    private static void ConfirmSuggestedBlocks(CityManager manager)
    {
        List<ZoneType> availableZoneTypes = ZoneTypeEditorUtility.LoadAllZoneTypes();
        ZoneType defaultZoneType = availableZoneTypes.Count > 0 ? availableZoneTypes[0] : null;

        if (!manager.GetCityData().blocks.Count.Equals(0))
        {
            if (!EditorUtility.DisplayDialog("Conferma", 
                "Ci sono già blocchi. Sovrascrivo?", "Sì", "No"))
            {
                return;
            }
            manager.GetCityData().blocks.Clear();
        }

        foreach (var vertexList in suggestedBlocks)
        {
            CityBlock newBlock = manager.AddBlock(vertexList);
            if (newBlock != null && defaultZoneType != null)
            {
                manager.SetBlockZoning(newBlock.id, defaultZoneType);
            }
        }

        Debug.Log($"[CityBlockEditor] {suggestedBlocks.Count} blocchi confermati!");
        suggestedBlocks.Clear();
        showingPreview = false;
        SceneView.RepaintAll();
    }

    public static void AddNodeToManualSelection(CityManager manager, int nodeId)
    {
        if (selectedManualNodeIds.Count == 0)
        {
            selectedManualNodeIds.Add(nodeId);
            return;
        }

        int firstNodeId = selectedManualNodeIds[0];
        int lastNodeId = selectedManualNodeIds[selectedManualNodeIds.Count - 1];

        if (nodeId == firstNodeId && selectedManualNodeIds.Count >= 3)
        {
            TryCreateManualBlock(manager);
            return;
        }

        if (nodeId == lastNodeId)
        {
            return;
        }

        selectedManualNodeIds.Add(nodeId);
    }

    public static void DrawManualSelectionPreview(CityManager manager)
    {
        if (manager == null || manager.GetCurrentMode() != CityManager.BuildMode.CreateBlock)
        {
            return;
        }

        CityData cityData = manager.GetCityData();
        if (cityData == null || selectedManualNodeIds.Count == 0)
        {
            return;
        }

        List<Vector3> points = new List<Vector3>();
        foreach (int nodeId in selectedManualNodeIds)
        {
            CityNode node = cityData.GetNode(nodeId);
            if (node != null)
            {
                points.Add(node.position);
            }
        }

        if (points.Count == 0)
        {
            return;
        }

        // Nodi della catena selezionata.
        Handles.color = Color.cyan;
        for (int i = 0; i < points.Count; i++)
        {
            float size = HandleUtility.GetHandleSize(points[i]) * 0.08f;
            Handles.DrawSolidDisc(points[i], Vector3.up, size);
            Handles.Label(points[i] + Vector3.up * (size * 2.0f), (i + 1).ToString());
        }

        // Segmenti già selezionati.
        if (points.Count >= 2)
        {
            Handles.color = new Color(0f, 1f, 1f, 0.9f);
            Handles.DrawAAPolyLine(4f, points.ToArray());
        }

        // Preview chiusura verso il primo nodo.
        if (points.Count >= 3)
        {
            Handles.color = new Color(1f, 0.8f, 0f, 0.9f);
            Handles.DrawDottedLine(points[points.Count - 1], points[0], 5f);
            Handles.Label(points[0] + Vector3.up * 0.35f, "Click per chiudere");
        }
    }

    private static void TryCreateManualBlock(CityManager manager)
    {
        if (selectedManualNodeIds.Count < 3)
        {
            Debug.LogWarning("[CityBlockEditor] Servono almeno 3 nodi per creare un blocco.");
            return;
        }

        CityData cityData = manager.GetCityData();
        if (cityData == null)
        {
            return;
        }

        // Verifica che il contorno sia effettivamente chiuso da segmenti esistenti.
        for (int i = 0; i < selectedManualNodeIds.Count; i++)
        {
            int a = selectedManualNodeIds[i];
            int b = selectedManualNodeIds[(i + 1) % selectedManualNodeIds.Count];

            bool hasSegment = cityData.segments.Exists(s =>
                s != null &&
                ((s.nodeA_ID == a && s.nodeB_ID == b) || (s.nodeA_ID == b && s.nodeB_ID == a)));

            if (!hasSegment)
            {
                Debug.LogWarning($"[CityBlockEditor] Segmento mancante tra nodi {a} e {b}. Impossibile creare blocco.");
                return;
            }
        }

        List<Vector3> vertices = new List<Vector3>();
        foreach (int nodeId in selectedManualNodeIds)
        {
            CityNode node = cityData.GetNode(nodeId);
            if (node == null)
            {
                Debug.LogWarning($"[CityBlockEditor] Nodo {nodeId} non trovato.");
                return;
            }
            vertices.Add(node.position);
        }

        CityBlock newBlock = manager.AddBlock(vertices);
        if (newBlock != null)
        {
            Debug.Log($"[CityBlockEditor] Blocco manuale creato: ID={newBlock.id}");
            selectedManualNodeIds.Clear();
            manager.SetMode(CityManager.BuildMode.Idle);
        }
    }

    /// <summary>
    /// Disegna anteprima blocchi suggeriti nella scena
    /// </summary>
    public static void DrawSuggestedBlocksPreview()
    {
        if (!showingPreview) return;

        for (int blockIndex = 0; blockIndex < suggestedBlocks.Count; blockIndex++)
        {
            List<Vector3> blockVertices = suggestedBlocks[blockIndex];
            if (blockVertices.Count < 3) continue;

            // Disegna outline
            Handles.color = new Color(1f, 0.85f, 0.2f, 1f);
            for (int i = 0; i < blockVertices.Count; i++)
            {
                Vector3 v1 = blockVertices[i];
                Vector3 v2 = blockVertices[(i + 1) % blockVertices.Count];
                Handles.DrawAAPolyLine(4f, v1, v2);
            }

            // Centroide
            Vector3 center = Vector3.zero;
            foreach (var v in blockVertices) center += v;
            center /= blockVertices.Count;

            float markerSize = HandleUtility.GetHandleSize(center) * 0.08f;
            Handles.color = Color.yellow;
            Handles.DrawSolidDisc(center, Vector3.up, markerSize);
            Handles.Label(center + Vector3.up * (markerSize * 2f), $"Preview B{blockIndex + 1}");
        }

        Handles.color = Color.white;
    }
}
