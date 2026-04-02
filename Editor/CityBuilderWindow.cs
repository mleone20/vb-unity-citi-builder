using UnityEngine;
using UnityEditor;

/// <summary>
/// EditorWindow principale per City Builder Tool.
/// Centralizza controlli, modalità builder, parametri globali e azioni batch.
/// </summary>
public class CityBuilderWindow : EditorWindow
{
    private CityManager cityManager;
    private CityData cityData;

    private bool showRoadSettings = true;
    private bool showBlockSettings = true;
    private bool showZoningSettings = true;
    private bool showLotSettings = true;
    private bool showBuildingSettings = true;
    private bool showToolsSettings = true;

    private GUIStyle headerStyle;
    private GUIStyle buttonStyle;

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

        // Scrollable main content
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

        DrawModeSelector();
        EditorGUILayout.Space();
        EditorGUILayout.Separator();
        EditorGUILayout.Space();

        DrawRoadSettings();
        EditorGUILayout.Space();

        DrawBlockSettings();
        EditorGUILayout.Space();

        DrawZoningSettings();
        EditorGUILayout.Space();

        DrawLotSettings();
        EditorGUILayout.Space();

        DrawBuildingSettings();
        EditorGUILayout.Space();

        DrawToolsSection();
        EditorGUILayout.Space();

        DrawActionsSection();
        EditorGUILayout.Space();

        DrawStatsSection();

        EditorGUILayout.EndScrollView();
    }

    private Vector2 scrollPosition = Vector2.zero;

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
    }

    private void DrawHeader()
    {
        EditorGUILayout.LabelField("🏙 CITY BUILDER TOOL", headerStyle);
        EditorGUILayout.LabelField("Editor-Only Procedural City Layout Designer", EditorStyles.helpBox);
    }

    private void DrawModeSelector()
    {
        EditorGUILayout.LabelField("MODALITA' EDITOR", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        CityManager.BuildMode currentMode = cityManager.GetCurrentMode();

        // Bottoni modalità
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

        GUI.color = currentMode == CityManager.BuildMode.ConnectNodes ? Color.yellow : Color.white;
        if (GUILayout.Button("Connetti Nodi"))
        {
            cityManager.SetMode(CityManager.BuildMode.ConnectNodes);
        }

        GUI.color = currentMode == CityManager.BuildMode.AssignZoning ? Color.yellow : Color.white;
        if (GUILayout.Button("Assegna Zoning"))
        {
            cityManager.SetMode(CityManager.BuildMode.AssignZoning);
        }

        GUI.color = currentMode == CityManager.BuildMode.CreateBlock ? Color.yellow : Color.white;
        if (GUILayout.Button("Crea Blocco"))
        {
            cityManager.SetMode(CityManager.BuildMode.CreateBlock);
        }

        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox($"Modalità attuale: {currentMode}", MessageType.Info);
    }

    private void DrawRoadSettings()
    {
        showRoadSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showRoadSettings, "IMPOSTAZIONI STRADE");

        if (showRoadSettings)
        {
            EditorGUILayout.Space();

            float roadWidth = cityData.globalRoadWidth;
            EditorGUILayout.LabelField("Larghezza Strade Globale:");
            roadWidth = EditorGUILayout.Slider(roadWidth, 1f, 10f);

            if (roadWidth != cityData.globalRoadWidth)
            {
                cityManager.SetGlobalRoadWidth(roadWidth);
            }

            EditorGUILayout.LabelField($"Nodi: {cityData.nodes.Count}, Segmenti: {cityData.segments.Count}");
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawBlockSettings()
    {
        showBlockSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showBlockSettings, "BLOCCHI (ISOLATI)");

        if (showBlockSettings)
        {
            EditorGUILayout.Space();
            
            CityBlockEditor.DrawBlockEditorUI(cityManager, ref showBlockSettings);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawZoningSettings()
    {
        showZoningSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showZoningSettings, "ZONING - DESTINAZIONI D'USO");

        if (showZoningSettings)
        {
            EditorGUILayout.Space();

            CityZoningEditor.DrawZoningEditorUI(cityManager);
            CityZoningEditor.DrawZoningStats(cityData);
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawLotSettings()
    {
        showLotSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showLotSettings, "LOTTI - SUDDIVISIONE URBANA");

        if (showLotSettings)
        {
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Parametri Lotti:");
            
            float avgLotSize = cityData.averageLotSize;
            EditorGUILayout.LabelField("Dimensione Media Lotto:");
            avgLotSize = EditorGUILayout.Slider(avgLotSize, 10f, 100f);

            if (avgLotSize != cityData.averageLotSize)
            {
                cityManager.SetAverageLotSize(avgLotSize);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Genera Lotti per tutti i Blocchi", buttonStyle))
            {
                GenerateAllLots();
            }

            EditorGUILayout.LabelField($"Lotti totali: {cityData.lots.Count}");
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawBuildingSettings()
    {
        showBuildingSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showBuildingSettings, "EDIFICI - GENERAZIONE PROCEDURALE");

        if (showBuildingSettings)
        {
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Scale Globale Edifici:");
            float scale = EditorGUILayout.Slider(cityData.buildingScale, 0.5f, 2f);
            cityData.buildingScale = scale;

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Altezze per Zona (in meter):");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Residenziale:");
            cityData.heightResidential = EditorGUILayout.FloatField(cityData.heightResidential, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Commerciale:");
            cityData.heightCommercial = EditorGUILayout.FloatField(cityData.heightCommercial, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Industriale:");
            cityData.heightIndustrial = EditorGUILayout.FloatField(cityData.heightIndustrial, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Speciale:");
            cityData.heightSpecial = EditorGUILayout.FloatField(cityData.heightSpecial, GUILayout.Width(50));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Edifici visualizzati: {cityData.lots.Count}");
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawActionsSection()
    {
        EditorGUILayout.LabelField("AZIONI GLOBALI", EditorStyles.boldLabel);
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

    private void DrawToolsSection()
    {
        showToolsSettings = EditorGUILayout.BeginFoldoutHeaderGroup(showToolsSettings, "STRUMENTI");

        if (showToolsSettings)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Utility di manutenzione dati per correggere collegamenti rotti nel grafo stradale.", MessageType.Info);

            if (GUILayout.Button("Ripara Collegamenti", buttonStyle))
            {
                Undo.RecordObject(cityData, "Ripara Collegamenti");
                string repairReport = cityManager.RepairConnections();
                EditorUtility.SetDirty(cityData);
                SceneView.RepaintAll();
                EditorUtility.DisplayDialog("Ripara Collegamenti", repairReport, "OK");
            }
        }

        EditorGUILayout.EndFoldoutHeaderGroup();
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
