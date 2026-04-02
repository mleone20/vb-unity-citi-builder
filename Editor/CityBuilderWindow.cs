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
        Nodes,
        Roads,
        BlocksAndZoning,
        Tools,
        Statistics
    }

    private CityManager cityManager;
    private CityData cityData;
    private EditorSection currentSection = EditorSection.Nodes;

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

        EditorGUILayout.HelpBox($"Modalità attuale: {cityManager.GetCurrentMode()}", MessageType.Info);
        EditorGUILayout.Space();

        DrawSectionToolbar();
        EditorGUILayout.Space();
        EditorGUILayout.Separator();
        EditorGUILayout.Space();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        switch (currentSection)
        {
            case EditorSection.Nodes:
                DrawNodesSection();
                break;
            case EditorSection.Roads:
                DrawRoadsSection();
                break;
            case EditorSection.BlocksAndZoning:
                DrawBlocksAndZoningSection();
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
        EditorGUILayout.LabelField("🏙 CITY BUILDER TOOL", headerStyle);
        EditorGUILayout.LabelField("Editor-Only Procedural City Layout Designer", EditorStyles.helpBox);
    }

    private void DrawSectionToolbar()
    {
        EditorGUILayout.LabelField("SEZIONI EDITOR", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.BeginHorizontal();

        if (DrawSectionButton("Nodi", EditorSection.Nodes))
        {
            currentSection = EditorSection.Nodes;
        }

        if (DrawSectionButton("Strade", EditorSection.Roads))
        {
            currentSection = EditorSection.Roads;
        }

        if (DrawSectionButton("Blocchi e Zoning", EditorSection.BlocksAndZoning))
        {
            currentSection = EditorSection.BlocksAndZoning;
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

    private void DrawNodesSection()
    {
        EditorGUILayout.LabelField("NODI", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        CityManager.BuildMode currentMode = cityManager.GetCurrentMode();

        EditorGUILayout.BeginHorizontal();

        GUI.color = currentMode == CityManager.BuildMode.Idle ? Color.yellow : Color.white;
        if (GUILayout.Button("Idle"))
        {
            cityManager.SetMode(CityManager.BuildMode.Idle);
        }

        GUI.color = currentMode == CityManager.BuildMode.AddNodes ? Color.yellow : Color.white;
        if (GUILayout.Button("Aggiungi Nodi"))
        {
            cityManager.SetMode(CityManager.BuildMode.AddNodes);
        }

        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Informazioni Nodi", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Nodi totali: {cityData.nodes.Count}");
        EditorGUILayout.LabelField($"Nodo selezionato: {cityManager.GetSelectedNodeID()}");
    }

    private void DrawRoadsSection()
    {
        EditorGUILayout.LabelField("STRADE", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        CityManager.BuildMode currentMode = cityManager.GetCurrentMode();

        EditorGUILayout.BeginHorizontal();

        GUI.color = currentMode == CityManager.BuildMode.ConnectNodes ? Color.yellow : Color.white;
        if (GUILayout.Button("Connetti Nodi"))
        {
            cityManager.SetMode(CityManager.BuildMode.ConnectNodes);
        }

        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Larghezza Strade Globale:");
        float roadWidth = EditorGUILayout.Slider(cityData.globalRoadWidth, 1f, 10f);
        if (roadWidth != cityData.globalRoadWidth)
        {
            cityManager.SetGlobalRoadWidth(roadWidth);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Informazioni Strade", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Segmenti totali: {cityData.segments.Count}");
        EditorGUILayout.LabelField($"Nodi disponibili: {cityData.nodes.Count}");
    }

    private void DrawBlocksAndZoningSection()
    {
        EditorGUILayout.LabelField("BLOCCHI E ZONING", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        CityManager.BuildMode currentMode = cityManager.GetCurrentMode();
        EditorGUILayout.BeginHorizontal();

        GUI.color = currentMode == CityManager.BuildMode.CreateBlock ? Color.yellow : Color.white;
        if (GUILayout.Button("Crea Blocco", buttonStyle))
        {
            cityManager.SetMode(CityManager.BuildMode.CreateBlock);
        }

        GUI.color = currentMode == CityManager.BuildMode.AssignZoning ? Color.yellow : Color.white;
        if (GUILayout.Button("Assegna Zoning", buttonStyle))
        {
            cityManager.SetMode(CityManager.BuildMode.AssignZoning);
        }

        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space();

        bool dummyBlockUiState = true;
        CityBlockEditor.DrawBlockEditorUI(cityManager, ref dummyBlockUiState);

        EditorGUILayout.Space();
        CityZoningEditor.DrawZoningEditorUI(cityManager);
        CityZoningEditor.DrawZoningStats(cityData);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lotti", EditorStyles.boldLabel);
        float avgLotSize = cityData.averageLotSize;
        EditorGUILayout.LabelField("Dimensione Media Lotto:");
        avgLotSize = EditorGUILayout.Slider(avgLotSize, 10f, 100f);
        if (avgLotSize != cityData.averageLotSize)
        {
            cityManager.SetAverageLotSize(avgLotSize);
        }

        if (GUILayout.Button("Genera Lotti per tutti i Blocchi", buttonStyle))
        {
            GenerateAllLots();
        }

        EditorGUILayout.LabelField($"Lotti totali: {cityData.lots.Count}");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Edifici", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Colori e altezze edificio sono definiti negli asset ZoneType. Modifica gli asset ZoneType nell'Inspector per personalizzarli.", MessageType.Info);
        EditorGUILayout.LabelField("Scale Globale Edifici:");
        cityData.buildingScale = EditorGUILayout.Slider(cityData.buildingScale, 0.5f, 2f);

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
                cityData.averageLotSize,
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
}
