using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Editor script che cattura input dalla Scene View per la modalità City Builder.
/// Gestisce click su nodi, connessioni, e altre interazioni.
/// Si attacca al CityManager e coordina con il BuildMode.
/// </summary>
public class CitySceneHandle
{
    public static bool IsEnabled = false;

    private static CityManager cachedCityManager;
    private static bool isInitialized = false;
    private static int lastAddedNodeID = -1;

    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        if (isInitialized) return;
        isInitialized = true;
        
        // Registra callback per Scene View eventi
        SceneView.duringSceneGui += OnSceneGUIHandler;
        EditorSceneManager.sceneSaved += OnSceneSaved;
    }

    /// <summary>
    /// Callback per OnSceneGUI - attaccata a tutti gli sceneview
    /// </summary>
    private static void OnSceneGUIHandler(SceneView sceneView)
    {
        // Carica il CityManager dalla scena se non già caricato
        if (cachedCityManager == null)
        {
            cachedCityManager = Object.FindAnyObjectByType<CityManager>();
        }

        if (cachedCityManager == null) return;

        if (!IsEnabled) return;

        CityManager.BuildMode mode = cachedCityManager.GetCurrentMode();

        // Overlay in alto a sinistra della SceneView
        DrawSceneOverlay(sceneView);

        // Disegna prima l'handle: così Unity può assegnare il controllo al gizmo.
        DrawSelectedNodeMoveHandle(cachedCityManager);

        // Anteprima live della catena nodi quando si crea un blocco manualmente.
        CityBlockEditor.DrawManualSelectionPreview(cachedCityManager);

        // Anteprima blocchi auto-rilevati (non ancora confermati).
        CityBlockEditor.DrawSuggestedBlocksPreview();

        // Processa eventi keyboard/mouse
        ProcessSceneViewInput(sceneView, cachedCityManager, mode);
    }

    private static void DrawSceneOverlay(SceneView sceneView)
    {
        Handles.BeginGUI();
        var bgStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = Texture2D.whiteTexture }
        };
        var labelStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13,
            normal = { textColor = new Color(0.15f, 0.9f, 0.35f) }
        };
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.Box(new Rect(8f, 8f, 200f, 28f), GUIContent.none, bgStyle);
        GUI.color = Color.white;
        GUI.Label(new Rect(14f, 10f, 192f, 24f), "\u26a1 CITY BUILDER MODE", labelStyle);
        Handles.EndGUI();
    }

    /// <summary>
    /// Elabora input dalla Scene View in base alla modalità attiva
    /// </summary>
    private static void ProcessSceneViewInput(SceneView sceneView, CityManager manager, CityManager.BuildMode mode)
    {
        Event e = Event.current;

        // Cast ray dalla mouse position nella Scene View
        if (e.type == EventType.MouseDown && e.button == 0) // Left click
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);

            // CTRL + click su nodo: rimozione rapida in qualsiasi modalità.
            if (e.control)
            {
                bool removed = HandleRemoveNodeClick(ray, manager);
                if (removed)
                {
                    e.Use();
                    return;
                }
            }

            if (mode == CityManager.BuildMode.Idle)
            {
                // Se un handle ha il controllo, non intercettare il click.
                if (GUIUtility.hotControl != 0)
                {
                    return;
                }

                HandleSelectNodeClick(ray, manager);
                e.Use();
                return;
            }

            if (mode == CityManager.BuildMode.AddNodes)
            {
                HandleAddNodeClick(ray, manager);
                e.Use();
            }
            else if (mode == CityManager.BuildMode.ConnectNodes)
            {
                HandleConnectNodeClick(ray, manager);
                e.Use();
            }
            else if (mode == CityManager.BuildMode.AssignZoning)
            {
                HandleAssignZoningClick(ray, manager);
                e.Use();
            }
            else if (mode == CityManager.BuildMode.CreateBlock)
            {
                HandleCreateBlockNodeClick(ray, manager);
                e.Use();
            }
        }

        // Disegna preview durante interazione
        DrawModePreview(sceneView, manager, mode);
    }

    private static void HandleCreateBlockNodeClick(Ray ray, CityManager manager)
    {
        Vector3 hitPoint = RaycastToGround(ray);
        CityNode nearestNode = manager.FindNearestNode(hitPoint, 2.0f);

        if (nearestNode == null)
        {
            Debug.LogWarning("[CreateBlock] Nessun nodo vicino al click.");
            return;
        }

        CityBlockEditor.AddNodeToManualSelection(manager, nearestNode.id);
    }

    /// <summary>
    /// In modalità Idle seleziona il nodo più vicino al click; click nel vuoto deseleziona.
    /// </summary>
    private static void HandleSelectNodeClick(Ray ray, CityManager manager)
    {
        Vector3 hitPoint = RaycastToGround(ray);
        CityNode nearestNode = manager.FindNearestNode(hitPoint, 2.0f);

        if (nearestNode != null)
        {
            manager.SetSelectedNodeID(nearestNode.id);
            manager.SetSelectedLotID(-1);
            Debug.Log($"Nodo selezionato: {nearestNode.id}");
        }
        else
        {
            manager.SetSelectedNodeID(-1);

            CityData cityData = manager.GetCityData();
            if (cityData != null)
            {
                CityLot selectedLot = cityData.FindLotAtPosition(hitPoint);
                manager.SetSelectedLotID(selectedLot != null ? selectedLot.id : -1);
            }
            else
            {
                manager.SetSelectedLotID(-1);
            }
        }
    }

    /// <summary>
    /// Disegna il PositionHandle e aggiorna la posizione del nodo selezionato.
    /// </summary>
    private static void DrawSelectedNodeMoveHandle(CityManager manager)
    {
        if (manager.GetCurrentMode() != CityManager.BuildMode.Idle)
        {
            return;
        }

        int selectedNodeID = manager.GetSelectedNodeID();
        if (selectedNodeID == -1)
        {
            return;
        }

        CityNode selectedNode = manager.GetNode(selectedNodeID);
        CityData cityData = manager.GetCityData();
        if (selectedNode == null || cityData == null)
        {
            return;
        }

        EditorGUI.BeginChangeCheck();
        Vector3 newPosition = Handles.PositionHandle(selectedNode.position, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(cityData, "Move City Node");
            selectedNode.position = newPosition;
            EditorUtility.SetDirty(cityData);
            SceneView.RepaintAll();
        }
    }

    /// <summary>
    /// CTRL + click su nodo: rimuove il nodo (e i segmenti collegati).
    /// </summary>
    private static bool HandleRemoveNodeClick(Ray ray, CityManager manager)
    {
        Vector3 hitPoint = RaycastToGround(ray);
        CityNode nearestNode = manager.FindNearestNode(hitPoint, 2.0f);

        if (nearestNode == null)
        {
            return false;
        }

        int removedNodeID = nearestNode.id;
        manager.RemoveNode(removedNodeID);

        if (manager.GetSelectedNodeID() == removedNodeID)
        {
            manager.SetSelectedNodeID(-1);
        }

        if (lastAddedNodeID == removedNodeID)
        {
            lastAddedNodeID = -1;
        }

        Debug.Log($"Nodo rimosso: {removedNodeID}");
        return true;
    }

    /// <summary>
    /// Gestisce click in modalità AddNodes: aggiunge nodo alla posizione cliccata
    /// </summary>
    private static void HandleAddNodeClick(Ray ray, CityManager manager)
    {
        // Interseca con piano Y=0 (ground level)
        Vector3 hitPoint = RaycastToGround(ray);
        bool shiftPressed = Event.current.shift;

        if (shiftPressed)
        {
            CityNode existingNode = manager.FindNearestNode(hitPoint, 2.0f);

            if (existingNode != null)
            {
                if (lastAddedNodeID != -1 && lastAddedNodeID != existingNode.id)
                {
                    manager.AddSegment(lastAddedNodeID, existingNode.id);
                }

                lastAddedNodeID = existingNode.id;
                Debug.Log($"Nodo esistente agganciato: {existingNode.id}");
                return;
            }
        }

        CityNode newNode = manager.AddNode(hitPoint);
        if (newNode != null)
        {
            if (shiftPressed)
            {
                // Con Shift premuto collega il nuovo nodo con l'ultimo nodo aggiunto.
                if (lastAddedNodeID != -1 && lastAddedNodeID != newNode.id)
                {
                    manager.AddSegment(lastAddedNodeID, newNode.id);
                }
            }

            lastAddedNodeID = newNode.id;
            Debug.Log($"Nodo aggiunto a {hitPoint}");
        }
    }

    /// <summary>
    /// Gestisce click in modalità ConnectNodes: connette nodi
    /// Primo click = seleziona nodo sorgente
    /// Secondo click = crea segmento al nodo destinazione
    /// </summary>
    private static void HandleConnectNodeClick(Ray ray, CityManager manager)
    {
        Vector3 hitPoint = RaycastToGround(ray);
        CityNode nearestNode = manager.FindNearestNode(hitPoint, 2.0f);

        if (nearestNode == null)
        {
            Debug.LogWarning("Nessun nodo trovato a questa posizione!");
            return;
        }

        int selectedID = manager.GetSelectedNodeID();

        if (selectedID == -1)
        {
            // Primo click: seleziona nodo sorgente
            manager.SetSelectedNodeID(nearestNode.id);
            Debug.Log($"Nodo sorgente selezionato: {nearestNode.id}");
        }
        else if (selectedID == nearestNode.id)
        {
            // Click sullo stesso nodo: deseleziona
            manager.SetSelectedNodeID(-1);
            Debug.Log("Selezione clearata");
        }
        else
        {
            // Secondo click: crea segmento
            CitySegment seg = manager.AddSegment(selectedID, nearestNode.id);
            manager.SetSelectedNodeID(-1); // Reset selezione
            if (seg != null)
            {
                Debug.Log($"Segmento creato tra {selectedID} e {nearestNode.id}");
            }
        }
    }

    /// <summary>
    /// Gestisce click in modalità AssignZoning: seleziona blocco dalla scena
    /// </summary>
    private static void HandleAssignZoningClick(Ray ray, CityManager manager)
    {
        Vector3 hitPoint = RaycastToGround(ray);
        CityData cityData = manager.GetCityData();

        if (cityData == null) return;

        CityBlock block = cityData.FindBlockAtPosition(hitPoint);
        if (block == null)
        {
            Debug.LogWarning("Nessun blocco trovato a questa posizione!");
            return;
        }

        CityZoningEditor.SetSelectedBlockForZoning(manager, block.id);
        Debug.Log($"[AssignZoning] Blocco selezionato: {block.id}");
        SceneView.RepaintAll();
    }

    /// <summary>
    /// Mostra menu contestuale per selezionare zoning
    /// </summary>
    private static void ShowZoningMenu(CityBlock block, CityManager manager)
    {
        GenericMenu menu = new GenericMenu();

        var zoneTypes = ZoneTypeEditorUtility.LoadAllZoneTypes();
        if (zoneTypes.Count == 0)
        {
            menu.AddDisabledItem(new GUIContent("Nessun ZoneType asset disponibile"));
        }
        else
        {
            foreach (ZoneType zoneType in zoneTypes)
            {
                ZoneType capturedZone = zoneType;
                string label = ZoneTypeEditorUtility.GetZoneDisplayName(capturedZone);
                menu.AddItem(new GUIContent(label), block.zoning == capturedZone,
                    () => manager.SetBlockZoning(block.id, capturedZone));
            }
        }

        menu.ShowAsContext();
    }

    /// <summary>
    /// Disegna preview della modalità attuale
    /// </summary>
    private static void DrawModePreview(SceneView sceneView, CityManager manager, CityManager.BuildMode mode)
    {
        CityData cityData = manager.GetCityData();
        if (cityData == null) return;

        if (mode == CityManager.BuildMode.AddNodes)
        {
            // Mostra help text
            Handles.Label(Vector3.zero, "[AddNodes Mode] Click aggiunge nodo | Shift connette ultimo nodo | Ctrl rimuove nodo");
        }
        else if (mode == CityManager.BuildMode.Idle)
        {
            Handles.Label(Vector3.zero, "[Idle Mode] Click seleziona nodo (giallo) | Drag handle per spostare | Ctrl rimuove nodo");
        }
        else if (mode == CityManager.BuildMode.ConnectNodes)
        {
            // Evidenzia nodo selezionato
            int selectedID = manager.GetSelectedNodeID();
            if (selectedID != -1)
            {
                CityNode node = manager.GetNode(selectedID);
                if (node != null)
                {
                    Handles.color = Color.yellow;
                    Handles.DrawWireDisc(node.position, Vector3.up, 0.5f);
                }
            }
            Handles.Label(Vector3.zero, "[ConnectNodes Mode] Click 1° nodo, poi 2° nodo | Ctrl rimuove nodo");
        }
        else if (mode == CityManager.BuildMode.AssignZoning)
        {
            Handles.Label(Vector3.zero, "[AssignZoning Mode] Click area blocco per selezionarlo nella UI Zoning | Ctrl rimuove nodo");
        }
        else if (mode == CityManager.BuildMode.CreateBlock)
        {
            Handles.Label(Vector3.zero, "[CreateBlock Mode] Click su nodi per costruire un blocco | Click primo nodo per chiudere");
        }
    }

    /// <summary>
    /// Utility: interseca ray con piano Y=0
    /// </summary>
    private static Vector3 RaycastToGround(Ray ray)
    {
        float t = -ray.origin.y / ray.direction.y;
        if (t > 0)
        {
            return ray.origin + ray.direction * t;
        }
        return ray.origin + ray.direction * 10f; // Fallback se non interseca correttamente
    }
     
    private static void OnSceneSaved(UnityEngine.SceneManagement.Scene scene)
    {
        // Auto-save CityManager data
        if (cachedCityManager != null && cachedCityManager.GetCityData() != null)
        {
            EditorUtility.SetDirty(cachedCityManager.GetCityData());
        }
    }
}
