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
        Nodes,
        Roads,
        Blocks,
        Zoning,
        Buildings,
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
    private bool lotFillingEnabled = false;

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

        if (DrawSectionButton("Blocchi", EditorSection.Blocks))
        {
            currentSection = EditorSection.Blocks;
        }

        if (DrawSectionButton("Zoning", EditorSection.Zoning))
        {
            currentSection = EditorSection.Zoning;
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

    private void DrawNodesSection()
    {
        EditorGUILayout.LabelField("NODI", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        CityManager.BuildMode currentMode = cityManager.GetCurrentMode();

        EditorGUILayout.BeginHorizontal();

        GUI.color = currentMode == CityManager.BuildMode.Idle ? Color.yellow : Color.white;
        if (GUILayout.Button("Muovi nodi"))
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

        GUI.color = new Color(0.6f, 0.9f, 1f);
        if (GUILayout.Button("Calcola Valori Ideali da Prefab", buttonStyle))
        {
            ComputeIdealLotParamsFromPrefabs();
        }
        GUI.color = Color.white;
        EditorGUILayout.Space();

        float avgLotSize = cityData.averageLotSize;
        EditorGUILayout.LabelField("Dimensione Media Lotto:");
        avgLotSize = EditorGUILayout.Slider(avgLotSize, 10f, 100f);
        if (avgLotSize != cityData.averageLotSize)
        {
            cityManager.SetAverageLotSize(avgLotSize);
        }

        EditorGUILayout.LabelField("Area Minima Lotto (m²):");
        cityData.minLotArea = EditorGUILayout.Slider(cityData.minLotArea, 1f, 500f);

        EditorGUILayout.LabelField("Rapporto Max Lato Lungo/Corto:");
        cityData.maxLotAspectRatio = EditorGUILayout.Slider(cityData.maxLotAspectRatio, 1f, 10f);

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

        EditorGUILayout.LabelField("Opzioni Spawn", EditorStyles.boldLabel);
        lotFillingEnabled = EditorGUILayout.Toggle(
            new GUIContent("Lot Filling", "Se attivo, riempie ogni lotto con più prefab affiancati lungo la larghezza del lotto, sfruttando tutto lo spazio disponibile. Richiede CityBuilderPrefab su ogni prefab."),
            lotFillingEnabled);
        if (lotFillingEnabled)
            EditorGUILayout.HelpBox("I prefab verranno disposti in fila lungo l'asse frontale del lotto. È necessario il componente CityBuilderPrefab su ogni prefab per leggerne il footprint.", MessageType.Info);
        EditorGUILayout.Space();

        if (GUILayout.Button("Spawn Edifici da ZoneType", buttonStyle))
        {
            SpawnBuildingsFromZoneTypes(lotFillingEnabled);
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

    private void SpawnBuildingsFromZoneTypes(bool lotFilling = false)
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

        CityBuildingSpawner.SpawnReport report = CityBuildingSpawner.SpawnBuildings(cityManager, handling, lotFilling);
        SceneView.RepaintAll();

        EditorUtility.DisplayDialog("Spawn Edifici", report.ToMultilineString(), "OK");
    }

    private void ClearSpawnedBuildings()
    {
        int removedCount = CityBuildingSpawner.ClearSpawnedBuildings();
        SceneView.RepaintAll();
        EditorUtility.DisplayDialog("Cancella Edifici Spawnati", $"Oggetti rimossi: {removedCount}", "OK");
    }

    private void ComputeIdealLotParamsFromPrefabs()
    {
        // Raccoglie tutti i ZoneType unici usati nei blocchi
        var usedZoneTypes = new HashSet<ZoneType>();
        foreach (var block in cityData.blocks)
        {
            if (block.zoning != null)
                usedZoneTypes.Add(block.zoning);
        }

        // Se nessun blocco ha zoning, cerca tutti gli asset ZoneType nel progetto
        if (usedZoneTypes.Count == 0)
        {
            string[] guids = AssetDatabase.FindAssets("t:ZoneType");
            foreach (var guid in guids)
            {
                ZoneType zt = AssetDatabase.LoadAssetAtPath<ZoneType>(AssetDatabase.GUIDToAssetPath(guid));
                if (zt != null) usedZoneTypes.Add(zt);
            }
        }

        // Raccoglie i footprint da tutti i prefab
        var footprints = new List<Vector2>();
        int countWithComponent = 0;
        int countFallback = 0;

        foreach (var zoneType in usedZoneTypes)
        {
            foreach (var prefab in zoneType.buildingPrefabs)
            {
                if (prefab == null) continue;

                CityBuilderPrefab cbp = prefab.GetComponent<CityBuilderPrefab>();
                if (cbp != null)
                {
                    footprints.Add(cbp.GetFootprintSize());
                    countWithComponent++;
                }
                else
                {
                    // Fallback: istanzia temporaneamente e calcola bounds dai renderer
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    instance.hideFlags = HideFlags.HideAndDontSave;
                    try
                    {
                        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
                        bool hasRenderer = false;
                        foreach (var r in instance.GetComponentsInChildren<Renderer>())
                        {
                            if (!hasRenderer) { bounds = r.bounds; hasRenderer = true; }
                            else bounds.Encapsulate(r.bounds);
                        }
                        if (hasRenderer && bounds.size.x > 0f && bounds.size.z > 0f)
                        {
                            footprints.Add(new Vector2(bounds.size.x, bounds.size.z));
                            countFallback++;
                        }
                    }
                    finally
                    {
                        DestroyImmediate(instance);
                    }
                }
            }
        }

        if (footprints.Count == 0)
        {
            EditorUtility.DisplayDialog(
                "Calcolo Valori Ideali",
                "Nessun prefab con dimensioni valide trovato nelle ZoneType.\nAssegna dei prefab alle ZoneType usate nei blocchi.",
                "OK");
            return;
        }

        // Calcola statistiche sui footprint
        float sumMaxDim = 0f;
        float minArea   = float.MaxValue;
        float maxAspect = 0f;

        foreach (var fp in footprints)
        {
            float w = Mathf.Max(0.01f, fp.x);
            float d = Mathf.Max(0.01f, fp.y);
            sumMaxDim += Mathf.Max(w, d);
            minArea    = Mathf.Min(minArea, w * d);
            float aspect = Mathf.Max(w, d) / Mathf.Min(w, d);
            maxAspect  = Mathf.Max(maxAspect, aspect);
        }

        float avgMaxDim = sumMaxDim / footprints.Count;

        // Deriva i parametri con margini adeguati
        float suggestedAvgLotSize  = Mathf.Round(avgMaxDim * 1.5f);                       // 50% margine oltre la dimensione media del prefab
        float suggestedMinLotArea  = Mathf.Round(minArea * 0.8f);                          // 20% sotto l'area del prefab più piccolo
        float suggestedAspectRatio = Mathf.Round(maxAspect * 1.2f * 10f) / 10f;           // 20% buffer sul rapporto più estremo

        // Clamp ai range degli slider
        suggestedAvgLotSize  = Mathf.Clamp(suggestedAvgLotSize,  10f, 100f);
        suggestedMinLotArea  = Mathf.Clamp(suggestedMinLotArea,  1f,  500f);
        suggestedAspectRatio = Mathf.Clamp(suggestedAspectRatio, 1f,  10f);

        string report =
            $"Prefab analizzati: {footprints.Count}" +
            $" ({countWithComponent} con CityBuilderPrefab, {countFallback} dai renderer)\n\n" +
            $"Dimensione Media Lotto:  {suggestedAvgLotSize}  (attuale: {cityData.averageLotSize})\n" +
            $"Area Minima Lotto:       {suggestedMinLotArea} m²  (attuale: {cityData.minLotArea})\n" +
            $"Rapporto Lato Max:       {suggestedAspectRatio}  (attuale: {cityData.maxLotAspectRatio})\n\n" +
            "Applicare i valori suggeriti?";

        if (EditorUtility.DisplayDialog("Calcola Valori Ideali da Prefab", report, "Applica", "Annulla"))
        {
            Undo.RecordObject(cityData, "Calcola Valori Ideali Lotti");
            cityData.averageLotSize    = suggestedAvgLotSize;
            cityData.minLotArea        = suggestedMinLotArea;
            cityData.maxLotAspectRatio = suggestedAspectRatio;
            EditorUtility.SetDirty(cityData);
        }
    }
}
