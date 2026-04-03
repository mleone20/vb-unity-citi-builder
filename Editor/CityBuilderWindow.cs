using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

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
        Configuration,
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
    private float simplifyMaxDeviationDeg = 8f;

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
            case EditorSection.Configuration:
                DrawConfigurationSection();
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

        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();

        if (DrawSectionButton("Configurazione", EditorSection.Configuration))
        {
            currentSection = EditorSection.Configuration;
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
        EditorGUILayout.LabelField("Terrain", EditorStyles.boldLabel);

        bool alignNodesToTerrain = EditorGUILayout.Toggle("Allinea nodi al Terrain", cityData.alignNodesToTerrain);
        if (alignNodesToTerrain != cityData.alignNodesToTerrain)
        {
            Undo.RecordObject(cityData, "Toggle Terrain Node Alignment");
            cityData.alignNodesToTerrain = alignNodesToTerrain;
            EditorUtility.SetDirty(cityData);
        }

        using (new EditorGUI.DisabledScope(!cityData.alignNodesToTerrain))
        {
            float nodeTerrainYOffset = EditorGUILayout.FloatField("Offset Y nodi", cityData.nodeTerrainYOffset);
            nodeTerrainYOffset = Mathf.Clamp(nodeTerrainYOffset, -2.0f, 2.0f);
            if (!Mathf.Approximately(nodeTerrainYOffset, cityData.nodeTerrainYOffset))
            {
                Undo.RecordObject(cityData, "Set Node Terrain Y Offset");
                cityData.nodeTerrainYOffset = nodeTerrainYOffset;
                EditorUtility.SetDirty(cityData);
            }
        }

        if (cityData.alignNodesToTerrain)
        {
            EditorGUILayout.HelpBox("In AddNodes e drag del nodo, la quota Y viene campionata da Terrain.activeTerrain + Offset Y.", MessageType.Info);
        }

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Strade", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("Larghezza Strade Globale (fallback):");
        float roadWidth = EditorGUILayout.Slider(cityData.globalRoadWidth, 1f, 10f);
        if (roadWidth != cityData.globalRoadWidth)
        {
            cityManager.SetGlobalRoadWidth(roadWidth);
        }

        float roadTerrainWidthMultiplier = EditorGUILayout.Slider("Sculpt Width Multiplier", cityData.roadTerrainWidthMultiplier, 0.5f, 3f);
        if (!Mathf.Approximately(roadTerrainWidthMultiplier, cityData.roadTerrainWidthMultiplier))
        {
            Undo.RecordObject(cityData, "Set Road Terrain Width Multiplier");
            cityData.roadTerrainWidthMultiplier = roadTerrainWidthMultiplier;
            EditorUtility.SetDirty(cityData);
        }

        float roadTerrainFalloff = EditorGUILayout.Slider("Sculpt Falloff", cityData.roadTerrainFalloff, 0.1f, 12f);
        if (!Mathf.Approximately(roadTerrainFalloff, cityData.roadTerrainFalloff))
        {
            Undo.RecordObject(cityData, "Set Road Terrain Falloff");
            cityData.roadTerrainFalloff = roadTerrainFalloff;
            EditorUtility.SetDirty(cityData);
        }

        float roadTerrainBlendStrength = EditorGUILayout.Slider("Sculpt Blend Strength", cityData.roadTerrainBlendStrength, 0.05f, 1f);
        if (!Mathf.Approximately(roadTerrainBlendStrength, cityData.roadTerrainBlendStrength))
        {
            Undo.RecordObject(cityData, "Set Road Terrain Blend Strength");
            cityData.roadTerrainBlendStrength = roadTerrainBlendStrength;
            EditorUtility.SetDirty(cityData);
        }

        if (GUILayout.Button("Flatten Terrain Under Roads", buttonStyle))
        {
            FlattenTerrainUnderRoads();
        }

        EditorGUILayout.HelpBox("Scolpisce il terrain lungo i segmenti stradali con bordo morbido (fall-off).", MessageType.Info);

        EditorGUILayout.Space();
        DrawSelectedSegmentInspector();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Informazioni", EditorStyles.boldLabel);
        EditorGUILayout.LabelField($"Nodi totali: {cityData.nodes.Count}");
        EditorGUILayout.LabelField($"Nodo selezionato: {cityManager.GetSelectedNodeID()}");
        EditorGUILayout.LabelField($"Segmenti totali: {cityData.segments.Count}");
        EditorGUILayout.LabelField($"Segmento selezionato: {cityManager.GetSelectedSegmentID()}");
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

        if (GUILayout.Button("Flatten Terrain Under Lots", buttonStyle))
        {
            FlattenTerrainUnderLots();
        }

        float lotTerrainFalloff = EditorGUILayout.Slider("Falloff Lotti", cityData.lotTerrainFalloff, 0.1f, 10f);
        if (!Mathf.Approximately(lotTerrainFalloff, cityData.lotTerrainFalloff))
        {
            Undo.RecordObject(cityData, "Set Lot Terrain Falloff");
            cityData.lotTerrainFalloff = lotTerrainFalloff;
            EditorUtility.SetDirty(cityData);
        }

        float lotTerrainBlendStrength = EditorGUILayout.Slider("Blend Strength Lotti", cityData.lotTerrainBlendStrength, 0.05f, 1f);
        if (!Mathf.Approximately(lotTerrainBlendStrength, cityData.lotTerrainBlendStrength))
        {
            Undo.RecordObject(cityData, "Set Lot Terrain Blend Strength");
            cityData.lotTerrainBlendStrength = lotTerrainBlendStrength;
            EditorUtility.SetDirty(cityData);
        }

        EditorGUILayout.HelpBox("Flatten lotti con transizione morbida verso il terreno circostante.", MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Lotti totali: {cityData.lots.Count}");
        EditorGUILayout.LabelField($"Edifici visualizzati: {cityData.lots.Count}");
    }

    private void DrawToolsSection()
    {
        EditorGUILayout.LabelField("STRUMENTI", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.HelpBox("Utility di manutenzione dati e azioni globali sul city graph.", MessageType.Info);

        EditorGUILayout.LabelField("Percorsi", EditorStyles.boldLabel);
        simplifyMaxDeviationDeg = EditorGUILayout.Slider("Semplifica: dev. max (deg)", simplifyMaxDeviationDeg, 1f, 25f);

        if (GUILayout.Button("Semplifica percorsi", buttonStyle))
        {
            string simplifyReport = cityManager.SimplifyPaths(simplifyMaxDeviationDeg);
            EditorUtility.SetDirty(cityData);
            SceneView.RepaintAll();
            EditorUtility.DisplayDialog("Semplifica percorsi", simplifyReport, "OK");
        }

        EditorGUILayout.Space();

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

        if (GUILayout.Button("Analizza intersezioni geometriche", buttonStyle))
        {
            string report = cityManager.AnalyzeIntersections();
            EditorUtility.DisplayDialog("Analisi Intersezioni", report, "OK");
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

    private void DrawConfigurationSection()
    {
        EditorGUILayout.LabelField("CONFIGURAZIONE WORKSPACE", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Profili Stradali", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("La larghezza globale resta un fallback per i segmenti senza RoadProfile. I nuovi segmenti usano il profilo di default se assegnato.", MessageType.Info);
        
        RoadProfile defaultRoadProfile = (RoadProfile)EditorGUILayout.ObjectField("Road Profile di default", cityData.defaultRoadProfile, typeof(RoadProfile), false);
        if (defaultRoadProfile != cityData.defaultRoadProfile)
        {
            Undo.RecordObject(cityData, "Set Default Road Profile");
            cityData.defaultRoadProfile = defaultRoadProfile;
            EditorUtility.SetDirty(cityData);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Setup Road Profiles di Default", buttonStyle))
        {
            CityBuilderMenu.SetupDefaultRoadProfiles();
        }

        if (GUILayout.Button("Setup Zone Types di Default", buttonStyle))
        {
            CityBuilderMenu.SetupDefaultZoneTypes();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Larghezza Globale (fallback)", EditorStyles.boldLabel);
        float globalWidth = EditorGUILayout.Slider(cityData.globalRoadWidth, 1f, 10f);
        if (globalWidth != cityData.globalRoadWidth)
        {
            cityManager.SetGlobalRoadWidth(globalWidth);
        }

        EditorGUILayout.HelpBox("Usa questa quando i profili strada non hanno larghezza assegnata o per nuovi segmenti senza profilo.", MessageType.Info);
    }

    private void DrawSelectedSegmentInspector()
    {
        EditorGUILayout.LabelField("Segmento selezionato", EditorStyles.boldLabel);

        int selectedSegmentID = cityManager.GetSelectedSegmentID();
        CitySegment selectedSegment = cityData.GetSegment(selectedSegmentID);
        if (selectedSegment == null)
        {
            EditorGUILayout.HelpBox("In modalità Idle puoi cliccare un segmento per modificarne profilo e geometria.", MessageType.None);
            return;
        }

        CityNode nodeA = cityData.GetNode(selectedSegment.nodeA_ID);
        CityNode nodeB = cityData.GetNode(selectedSegment.nodeB_ID);
        EditorGUILayout.LabelField($"ID: {selectedSegment.id}");
        EditorGUILayout.LabelField($"Nodi: {selectedSegment.nodeA_ID} -> {selectedSegment.nodeB_ID}");
        EditorGUILayout.LabelField($"Lunghezza stimata: {CityRoadGeometry.EstimateLength(cityData, selectedSegment):F2}");
        EditorGUILayout.LabelField($"Larghezza effettiva: {CityRoadGeometry.GetRoadWidth(cityData, selectedSegment):F2}");

        List<RoadProfile> roadProfiles = RoadProfileEditorUtility.LoadAllRoadProfiles();
        
        EditorGUILayout.LabelField("Profilo Strada:");
        
        int selectedProfileIndex = -1;
        string[] profileLabels = new string[roadProfiles.Count + 1];
        profileLabels[0] = "None";
        
        for (int i = 0; i < roadProfiles.Count; i++)
        {
            RoadProfile profile = roadProfiles[i];
            profileLabels[i + 1] = RoadProfileEditorUtility.GetRoadProfileDisplayName(profile);
            
            if (selectedSegment.roadProfile == profile)
            {
                selectedProfileIndex = i + 1;
            }
        }

        if (selectedProfileIndex < 0)
        {
            selectedProfileIndex = 0;
        }

        int newProfileIndex = EditorGUILayout.Popup(selectedProfileIndex, profileLabels);
        RoadProfile newProfile = newProfileIndex > 0 ? roadProfiles[newProfileIndex - 1] : null;

        if (newProfile != selectedSegment.roadProfile)
        {
            Undo.RecordObject(cityData, "Set Segment Road Profile");
            cityManager.SetSegmentRoadProfile(selectedSegment.id, newProfile);
            EditorUtility.SetDirty(cityData);
        }

        CitySegmentGeometryType newGeometryType = (CitySegmentGeometryType)EditorGUILayout.EnumPopup("Geometria", selectedSegment.geometryType);
        if (newGeometryType != selectedSegment.geometryType)
        {
            Undo.RecordObject(cityData, "Set Segment Geometry");
            cityManager.SetSegmentGeometryType(selectedSegment.id, newGeometryType);
            EditorUtility.SetDirty(cityData);
            SceneView.RepaintAll();
        }

        if (selectedSegment.geometryType == CitySegmentGeometryType.Bezier)
        {
            if (GUILayout.Button("Reset maniglie Bézier", buttonStyle))
            {
                Undo.RecordObject(cityData, "Reset Segment Curve Handles");
                cityManager.ResetSegmentBezierHandles(selectedSegment.id);
                EditorUtility.SetDirty(cityData);
                SceneView.RepaintAll();
            }

            if (nodeA != null)
            {
                EditorGUILayout.Vector3Field("Control Point A", selectedSegment.controlPointA);
            }

            if (nodeB != null)
            {
                EditorGUILayout.Vector3Field("Control Point B", selectedSegment.controlPointB);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Converti il segmento in Bézier per modificare le maniglie direttamente in Scene View.", MessageType.Info);
        }
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

    private void FlattenTerrainUnderLots()
    {
        CityBuildingSpawner.TerrainFlattenReport report = CityBuildingSpawner.FlattenTerrainUnderLots(cityManager);
        SceneView.RepaintAll();
        EditorUtility.DisplayDialog("Flatten Terrain Under Lots", report.ToMultilineString(), "OK");
    }

    private void FlattenTerrainUnderRoads()
    {
        CityBuildingSpawner.RoadFlattenReport report = CityBuildingSpawner.FlattenTerrainUnderRoads(cityManager);
        SceneView.RepaintAll();
        EditorUtility.DisplayDialog("Flatten Terrain Under Roads", report.ToMultilineString(), "OK");
    }
}
