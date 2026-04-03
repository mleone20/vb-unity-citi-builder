using UnityEngine;
using UnityEditor;

/// <summary>
/// EditorWindow principale per City Builder Tool.
/// Centralizza controlli, modalità builder, parametri globali e azioni batch.
/// </summary>
public class CityBuilderWindow : EditorWindow
{
    private enum EditorSection
    {
        Paths,
        Blocks,
        Zoning,
        Buildings,
        Tools,
        Statistics
    }

    private CityManager cityManager;
    private CityData cityData;
    private EditorSection currentSection = EditorSection.Paths;

    private GUIStyle headerStyle;
    private GUIStyle buttonStyle;
    private GUIStyle tabButtonStyle;
    private Vector2 scrollPosition = Vector2.zero;
    private float weldNodesDistance = 1.0f;

    [MenuItem("Window/City Builder/City Builder Tool")]
    public static void ShowWindow()
    {
        GetWindow<CityBuilderWindow>("City Builder");
    }

    private void OnEnable()
    {
        FindCityManager();
    }

    private void OnGUI()
    {
        InitializeStyles();
        DrawHeader();
        EditorGUILayout.Space();

        if (cityManager == null)
        {
            EditorGUILayout.HelpBox("⚠ CityManager non trovato nella scena! Crea uno: GameObject > CityBuilder > Create CityManager", MessageType.Warning);
            
            if (GUILayout.Button("Crea CityManager", GUILayout.Height(30)))
            {
                CityBuilderMenu.CreateCityManager();
                FindCityManager();
            }
            return;
        }

        if (cityData == null)
        {
            EditorGUILayout.HelpBox("⚠ CityData non assegnato! Crea un asset: Assets > Create > CityData", MessageType.Warning);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Crea CityData Asset"))
            {
                CityBuilderMenu.CreateCityData();
                cityData = cityManager.GetCityData();
            }
            EditorGUILayout.EndHorizontal();
            
            return;
        }

        EditorGUILayout.HelpBox($"Modalità attuale: {GetModeDisplayLabel(cityManager.GetCurrentMode())}", MessageType.Info);
        EditorGUILayout.Space();

        DrawSectionToolbar();
        EditorGUILayout.Space();
        EditorGUILayout.Separator();
        EditorGUILayout.Space();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        switch (currentSection)
        {
            case EditorSection.Paths:
                DrawPathsSection();
                break;
            case EditorSection.Blocks:
                DrawBlocksSection();
                break;
            case EditorSection.Zoning:
                DrawZoningSection();
                break;
            case EditorSection.Buildings:
                DrawBuildingsSection();
                break;
            case EditorSection.Tools:
                DrawToolsSection();
                break;
            case EditorSection.Statistics:
                DrawStatsSection();
                break;
        }

        EditorGUILayout.EndScrollView();
    }

    private void InitializeStyles()
    {
        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold
            };
        }

        if (buttonStyle == null)
        {
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 28,
                fontSize = 11
            };
        }

        if (tabButtonStyle == null)
        {
            tabButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fixedHeight = 30,
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
        }
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("\U0001f3d9 CITY BUILDER TOOL", headerStyle);
        EditorGUILayout.LabelField("Editor-Only Procedural City Layout Designer", EditorStyles.helpBox);
        EditorGUILayout.Space();

        bool isActive = CitySceneHandle.IsEnabled;
        GUI.color = isActive ? new Color(0.3f, 1f, 0.4f) : new Color(1f, 0.75f, 0.3f);
        string toggleLabel = isActive ? "\u25cf  City Builder ATTIVO  (click per disabilitare)" : "\u25cb  City Builder DISABILITATO  (click per abilitare)";
        if (GUILayout.Button(toggleLabel, GUILayout.Height(30)))
        {
            CitySceneHandle.IsEnabled = !CitySceneHandle.IsEnabled;
            SceneView.RepaintAll();
        }
        GUI.color = Color.white;
    }

    private void DrawSectionToolbar()
    {
        EditorGUILayout.LabelField("SEZIONI EDITOR", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();

        if (DrawSectionButton("Percorsi", EditorSection.Paths))
        {
            currentSection = EditorSection.Paths;
            cityManager.SetMode(CityManager.BuildMode.Idle);
        }

        if (DrawSectionButton("Blocchi", EditorSection.Blocks))
        {
            currentSection = EditorSection.Blocks;
            cityManager.SetMode(CityManager.BuildMode.CreateBlock);
        }

        if (DrawSectionButton("Zoning", EditorSection.Zoning))
        {
            currentSection = EditorSection.Zoning;
            cityManager.SetMode(CityManager.BuildMode.AssignZoning);
        }

        if (DrawSectionButton("Edifici", EditorSection.Buildings))
        {
            currentSection = EditorSection.Buildings;
        }

        if (DrawSectionButton("Strumenti", EditorSection.Tools))
        {
            currentSection = EditorSection.Tools;
        }

        if (DrawSectionButton("Statistiche", EditorSection.Statistics))
        {
            currentSection = EditorSection.Statistics;
        }

        EditorGUILayout.EndHorizontal();
    }

    private bool DrawSectionButton(string label, EditorSection section)
    {
        GUI.color = currentSection == section ? Color.yellow : Color.white;
        bool pressed = GUILayout.Button(label, tabButtonStyle);
        GUI.color = Color.white;
        return pressed;
    }

    private string GetModeDisplayLabel(CityManager.BuildMode mode)
    {
        switch (mode)
        {
            case CityManager.BuildMode.Idle:
                return "Seleziona/Sposta Nodi";
            case CityManager.BuildMode.AddNodes:
                return "Aggiungi/Modifica Nodi";
            case CityManager.BuildMode.ConnectNodes:
                return "Connetti Nodi";
            case CityManager.BuildMode.CreateBlock:
                return "Crea Blocco";
            case CityManager.BuildMode.AssignZoning:
                return "Assegna Zoning";
            default:
                return mode.ToString();
        }
    }

    private void DrawPathsSection()
    {
        EditorGUILayout.LabelField("NODI", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        CityManager.BuildMode currentMode = cityManager.GetCurrentMode();

        EditorGUILayout.BeginHorizontal();

        GUI.color = currentMode == CityManager.BuildMode.Idle ? Color.yellow : Color.white;
        if (GUILayout.Button("Seleziona/Sposta Nodi"))
        {
            cityManager.SetMode(CityManager.BuildMode.Idle);
        }

        GUI.color = currentMode == CityManager.BuildMode.AddNodes ? Color.yellow : Color.white;
        if (GUILayout.Button("Aggiungi/Modifica Nodi"))
        {
            cityManager.SetMode(CityManager.BuildMode.AddNodes);
        }

        GUI.color = currentMode == CityManager.BuildMode.ConnectNodes ? Color.yellow : Color.white;
        if (GUILayout.Button("Connetti Nodi"))
        {
            cityManager.SetMode(CityManager.BuildMode.ConnectNodes);
        }

        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        CitySceneHandle.SnapToGridEnabled = EditorGUILayout.Toggle("Allinea nodi alla griglia", CitySceneHandle.SnapToGridEnabled);
        using (new EditorGUI.DisabledScope(!CitySceneHandle.SnapToGridEnabled))
        {
            CitySceneHandle.GridSize = EditorGUILayout.FloatField("Passo griglia", CitySceneHandle.GridSize);
            CitySceneHandle.GridSize = Mathf.Max(0.1f, CitySceneHandle.GridSize);
        }

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Strade", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Larghezza Strade Globale:");
        float roadWidth = EditorGUILayout.Slider(cityData.globalRoadWidth, 1f, 10f);
        if (roadWidth != cityData.globalRoadWidth)
        {
            cityManager.SetGlobalRoadWidth(roadWidth);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Informazioni", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Nodi totali: {cityData.nodes.Count}");
        EditorGUILayout.LabelField($"Nodo selezionato: {cityManager.GetSelectedNodeID()}");
        EditorGUILayout.LabelField($"Segmenti totali: {cityData.segments.Count}");
    }

    private void DrawBlocksSection()
    {
        EditorGUILayout.LabelField("BLOCCHI", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        CityManager.BuildMode currentMode = cityManager.GetCurrentMode();
        EditorGUILayout.BeginHorizontal();

        GUI.color = currentMode == CityManager.BuildMode.CreateBlock ? Color.yellow : Color.white;
        if (GUILayout.Button("Crea Blocco", buttonStyle))
        {
            cityManager.SetMode(CityManager.BuildMode.CreateBlock);
        }

        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        bool dummyBlockUiState = true;
        CityBlockEditor.DrawBlockEditorUI(cityManager, ref dummyBlockUiState);
    }

    private void DrawZoningSection()
    {
        EditorGUILayout.LabelField("ZONING", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        CityManager.BuildMode currentMode = cityManager.GetCurrentMode();
        EditorGUILayout.BeginHorizontal();

        GUI.color = currentMode == CityManager.BuildMode.AssignZoning ? Color.yellow : Color.white;
        if (GUILayout.Button("Assegna Zoning", buttonStyle))
        {
            cityManager.SetMode(CityManager.BuildMode.AssignZoning);
        }

        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        CityZoningEditor.DrawZoningEditorUI(cityManager);
        CityZoningEditor.DrawZoningStats(cityData);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lotti", EditorStyles.boldLabel);

        if (GUILayout.Button("Genera Lotti per tutti i Blocchi", buttonStyle))
        {
            GenerateAllLots();
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Cancella tutti i Lotti", buttonStyle))
        {
            Undo.RecordObject(cityData, "Cancella Tutti i Lotti");
            int removedLots = cityManager.ClearAllLots();
            EditorUtility.SetDirty(cityData);
            SceneView.RepaintAll();
            EditorUtility.DisplayDialog("Cancella Lotti", $"Lotti rimossi: {removedLots}", "OK");
        }

        int selectedBlockID = cityManager.GetSelectedBlockID();
        using (new EditorGUI.DisabledScope(selectedBlockID < 0 || cityData.GetBlock(selectedBlockID) == null))
        {
            if (GUILayout.Button("Cancella Lotti del Blocco Selezionato", buttonStyle))
            {
                Undo.RecordObject(cityData, "Cancella Lotti Blocco");
                int removedLots = cityManager.ClearLotsForBlock(selectedBlockID);
                EditorUtility.SetDirty(cityData);
                SceneView.RepaintAll();
                EditorUtility.DisplayDialog("Cancella Lotti", $"Lotti rimossi dal blocco {selectedBlockID}: {removedLots}", "OK");
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.LabelField($"Lotti totali: {cityData.lots.Count}");
    }

    private void DrawBuildingsSection()
    {
        EditorGUILayout.LabelField("EDIFICI", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox("Lo spawn usa i prefab configurati in ZoneType. Ogni prefab dovrebbe avere il componente CityBuilderPrefab per il calcolo footprint.", MessageType.Info);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox("Lo spawn usa solo il prefab assegnato a ciascun lotto in fase di generazione. Un lotto corrisponde sempre a un edificio.", MessageType.Info);
        EditorGUILayout.Space();

        if (GUILayout.Button("Spawn Edifici da ZoneType", buttonStyle))
        {
            SpawnBuildingsFromZoneTypes();
        }

        if (GUILayout.Button("Cancella Edifici Spawnati", buttonStyle))
        {
            ClearSpawnedBuildings();
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Lotti totali: {cityData.lots.Count}");
        EditorGUILayout.LabelField($"Edifici visualizzati: {cityData.lots.Count}");
    }

    private void DrawToolsSection()
    {
        EditorGUILayout.LabelField("STRUMENTI", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox("Utility di manutenzione dati e azioni globali sul city graph.", MessageType.Info);

        if (GUILayout.Button("Ripara Collegamenti", buttonStyle))
        {
            Undo.RecordObject(cityData, "Ripara Collegamenti");
            string repairReport = cityManager.RepairConnections();
            EditorUtility.SetDirty(cityData);
            SceneView.RepaintAll();
            EditorUtility.DisplayDialog("Ripara Collegamenti", repairReport, "OK");
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Salda Nodi", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Distanza massima di fusione:");
        weldNodesDistance = EditorGUILayout.Slider(weldNodesDistance, 0.1f, 10f);

        if (GUILayout.Button("Salda nodi ravvicinati", buttonStyle))
        {
            Undo.RecordObject(cityData, "Salda Nodi Ravvicinati");
            string weldReport = cityManager.WeldCloseNodes(weldNodesDistance);
            EditorUtility.SetDirty(cityData);
            SceneView.RepaintAll();
            EditorUtility.DisplayDialog("Salda Nodi", weldReport, "OK");
        }

        if (GUILayout.Button("Setup Zone Types di Default", buttonStyle))
        {
            CityBuilderMenu.SetupDefaultZoneTypes();
        }

        EditorGUILayout.Space();

        GUI.color = Color.cyan;
        if (GUILayout.Button("📊 Esporta Statistiche (Console)", buttonStyle))
        {
            cityManager.LogStats();
        }
        GUI.color = Color.white;

        EditorGUILayout.Space();

        GUI.color = new Color(1, 0.5f, 0);
        if (GUILayout.Button("🗑️ Cancella Tutto", buttonStyle))
        {
            cityManager.ResetCity();
            CityBlockEditor.ClearPreview();
        }
        GUI.color = Color.white;
    }

    private void DrawStatsSection()
    {
        EditorGUILayout.LabelField("STATISTICHE CITTÀ", EditorStyles.boldLabel);

        EditorGUILayout.LabelField($"Nodi Stradali: {cityData.nodes.Count}");
        EditorGUILayout.LabelField($"Segmenti Stradali: {cityData.segments.Count}");
        EditorGUILayout.LabelField($"Blocchi: {cityData.blocks.Count}");
        EditorGUILayout.LabelField($"Lotti: {cityData.lots.Count}");

        EditorGUILayout.Space();

        float totalArea = 0;
        foreach (var block in cityData.blocks)
        {
            totalArea += block.GetArea();
        }
        EditorGUILayout.LabelField($"Area totale blocchi: {totalArea:F2} m²");
    }

    private void FindCityManager()
    {
        cityManager = Object.FindAnyObjectByType<CityManager>();
        if (cityManager != null)
        {
            cityData = cityManager.GetCityData();
        }
    }

    private void GenerateAllLots()
    {
        if (cityData.blocks.Count == 0)
        {
            EditorUtility.DisplayDialog("Info", "Nessun blocco disponibile!", "OK");
            return;
        }

        cityData.lots.Clear();
        int lotCount = 0;

        for (int i = 0; i < cityData.blocks.Count; i++)
        {
            CityBlock block = cityData.blocks[i];
            var generatedLots = CityLotGenerator.GenerateLotsForBlock(
                block,
                block.zoning,
                i,
                cityData
            );

            foreach (var lot in generatedLots)
            {
                lot.id = cityData.GetNextLotID();
                cityData.lots.Add(lot);
                block.lotIDs.Add(lot.id);
                lotCount++;
            }
        }

        Debug.Log($"[CityBuilderWindow] Generati {lotCount} lotti!");
        EditorUtility.DisplayDialog("Successo", $"Generati {lotCount} lotti!", "OK");
    }

    private void SpawnBuildingsFromZoneTypes()
    {
        int choice = EditorUtility.DisplayDialogComplex(
            "Spawn Edifici",
            "Come vuoi gestire gli edifici già spawnati?",
            "Cancella precedenti e spawn",
            "Mantieni esistenti e spawn",
            "Annulla"
        );

        if (choice == 2)
        {
            return;
        }

        CityBuildingSpawner.ExistingBuildingsHandling handling =
            choice == 0
                ? CityBuildingSpawner.ExistingBuildingsHandling.ClearExisting
                : CityBuildingSpawner.ExistingBuildingsHandling.KeepExisting;

        CityBuildingSpawner.SpawnReport report = CityBuildingSpawner.SpawnBuildings(cityManager, handling);
        SceneView.RepaintAll();

        EditorUtility.DisplayDialog("Spawn Edifici", report.ToMultilineString(), "OK");
    }

    private void ClearSpawnedBuildings()
    {
        int removedCount = CityBuildingSpawner.ClearSpawnedBuildings();
        SceneView.RepaintAll();
        EditorUtility.DisplayDialog("Cancella Edifici Spawnati", $"Oggetti rimossi: {removedCount}", "OK");
    }
}
