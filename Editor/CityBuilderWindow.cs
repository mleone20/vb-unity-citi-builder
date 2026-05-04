using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// EditorWindow principale per City Builder Studio.
/// Interfaccia a tab colorati per layout procedurale citta.
/// </summary>
public class CityBuilderWindow : EditorWindow
{
    private enum EditorSection
    {
        Paths,
        Blocks,
        Zoning,
        Buildings,
        ProceduralGeneration,
        Tools,
        Statistics
    }

    private CityManager cityManager;
    private CityData cityData;
    private EditorSection currentSection = EditorSection.Paths;

    private Vector2 scrollPosition = Vector2.zero;
    private Vector2 inspectorScrollPos = Vector2.zero;

    private float weldNodesDistance = 1.0f;
    private float simplifyMaxDeviationDeg = 8f;
    private bool _terrainFoldout = false;

    private ScriptableObject proceduralConfig;
    private string _lastProceduralReport = "";

    // Async generation
    private bool _isGenerating = false;
    private float _generationProgress = 0f;
    private string _generationStatus = "";
    private IEnumerator _genCoroutine;
    private Action _onGenCompleted;

    private GUIStyle headerStyle;
    private GUIStyle buttonStyle;
    private GUIStyle tabButtonStyle;
    private GUIStyle activeTabStyle;
    private GUIStyle phaseHeaderStyle;
    private GUIStyle actionButtonStyle;
    private GUIStyle statusBarStyle;
    private GUIStyle modeButtonStyle;
    private GUIStyle activeModeButtonStyle;
    private bool _stylesInitialized = false;

    private static readonly Color ColPaths    = new Color(0.29f, 0.56f, 0.85f);
    private static readonly Color ColBlocks   = new Color(0.42f, 0.52f, 0.65f);
    private static readonly Color ColZoning   = new Color(0.36f, 0.72f, 0.36f);
    private static readonly Color ColBuildings= new Color(0.91f, 0.63f, 0.22f);
    private static readonly Color ColConfig   = new Color(0.55f, 0.55f, 0.55f);
    private static readonly Color ColProc     = new Color(0.35f, 0.75f, 0.55f);
    private static readonly Color ColTools    = new Color(0.75f, 0.45f, 0.45f);
    private static readonly Color ColStats    = new Color(0.65f, 0.55f, 0.75f);

    private Color GetSectionColor(EditorSection s)
    {
        switch (s)
        {
            case EditorSection.Paths:               return ColPaths;
            case EditorSection.Blocks:              return ColBlocks;
            case EditorSection.Zoning:              return ColZoning;
            case EditorSection.Buildings:           return ColBuildings;
            case EditorSection.ProceduralGeneration:return ColProc;
            case EditorSection.Tools:               return ColTools;
            case EditorSection.Statistics:          return ColStats;
            default:                                return Color.gray;
        }
    }

    [MenuItem("Window/City Builder/City Builder Tool")]
    public static void ShowWindow()
    {
        GetWindow<CityBuilderWindow>("City Builder Studio");
    }

    private void OnEnable()
    {
        FindCityManager();
    }

    private void OnDisable()
    {
        StopAsyncGeneration();
    }

    // ── Async generation helpers ──────────────────────────────────────────────

    private void StartAsyncGeneration(IEnumerator coroutine, Action onDone)
    {
        StopAsyncGeneration();
        _genCoroutine   = coroutine;
        _onGenCompleted = onDone;
        _isGenerating   = true;
        _generationProgress = 0f;
        _generationStatus   = "Avvio...";
        EditorApplication.update += TickAsyncGeneration;
        Repaint();
    }

    private void StopAsyncGeneration()
    {
        if (!_isGenerating) return;
        EditorApplication.update -= TickAsyncGeneration;
        _isGenerating   = false;
        _genCoroutine   = null;
        EditorUtility.ClearProgressBar();
        Repaint();
    }

    private void TickAsyncGeneration()
    {
        if (_genCoroutine == null) { StopAsyncGeneration(); return; }
        const int stepsPerTick = 4;
        for (int i = 0; i < stepsPerTick; i++)
        {
            if (!_genCoroutine.MoveNext())
            {
                EditorApplication.update -= TickAsyncGeneration;
                _isGenerating = false;
                _genCoroutine = null;
                _onGenCompleted?.Invoke();
                EditorUtility.ClearProgressBar();
                Repaint();
                return;
            }
        }
        Repaint();
    }

    private void OnGUI()
    {
        InitializeStyles();
        DrawTopBar();

        if (cityManager == null)
        {
            EditorGUILayout.Space(6);
            EditorGUILayout.HelpBox("CityManager non trovato nella scena!\nCrea un GameObject con il componente CityManager.", MessageType.Warning);
            if (GUILayout.Button("Cerca CityManager", GUILayout.Height(30)))
            {
                FindCityManager();
            }
            return;
        }
 

        DrawTabToolbar();

        EditorGUILayout.BeginHorizontal();

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        DrawPhaseHeader();
        EditorGUILayout.Space(4);
        DrawCurrentSection();
        EditorGUILayout.Space(8);
        EditorGUILayout.EndScrollView();

        int selSeg = cityManager.GetSelectedSegmentID();
        if (selSeg != -1 && cityData.GetSegment(selSeg) != null)
            DrawSegmentInspectorPanel();

        EditorGUILayout.EndHorizontal();
    }

    // ── Stili ─────────────────────────────────────────────────

    private void InitializeStyles()
    {
        if (_stylesInitialized) return;
        _stylesInitialized = true;

        headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13, fontStyle = FontStyle.Bold };

        buttonStyle = new GUIStyle(GUI.skin.button) { fixedHeight = 26, fontSize = 11 };

        actionButtonStyle = new GUIStyle(GUI.skin.button) { fixedHeight = 30, fontSize = 11, fontStyle = FontStyle.Bold };

        tabButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fixedHeight = 34, fontSize = 10, fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter, wordWrap = true
        };

        activeTabStyle = new GUIStyle(tabButtonStyle);
        activeTabStyle.normal.textColor = Color.white;

        phaseHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 13, fontStyle = FontStyle.Bold
        };
        phaseHeaderStyle.normal.textColor = Color.white;

        statusBarStyle = new GUIStyle(EditorStyles.miniLabel) { fontSize = 10 };
        statusBarStyle.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

        modeButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fixedHeight = 50, fixedWidth = 80, fontSize = 10,
            fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = true
        };

        activeModeButtonStyle = new GUIStyle(modeButtonStyle);
        activeModeButtonStyle.normal.textColor = Color.white;
    }

    // ── Top Bar ────────────────────────────────────────────────

    private void DrawTopBar()
    {
        Rect barRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(38));
        EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f));
        GUILayout.Space(8);
        GUILayout.Label("\U0001f3d9  CITY BUILDER STUDIO", phaseHeaderStyle, GUILayout.ExpandWidth(false));
        GUILayout.FlexibleSpace();
        if (cityData != null)
        {
            string counts = string.Format("nodi: {0}  seg: {1}  blocchi: {2}  lotti: {3}",
                cityData.nodes.Count, cityData.segments.Count, cityData.blocks.Count, cityData.lots.Count);
            GUILayout.Label(counts, statusBarStyle, GUILayout.ExpandWidth(false));
            GUILayout.Space(10);
        }
        bool isActive = CitySceneHandle.IsEnabled;
        GUI.backgroundColor = isActive ? new Color(0.3f, 0.9f, 0.4f) : new Color(0.9f, 0.5f, 0.2f);
        string toggleLabel = isActive ? "\u25cf ATTIVO" : "\u25cb DISATTIVO";
        if (GUILayout.Button(toggleLabel, GUILayout.Width(90), GUILayout.Height(24)))
        {
            CitySceneHandle.IsEnabled = !CitySceneHandle.IsEnabled;
            SceneView.RepaintAll();
        }
        GUI.backgroundColor = Color.white;
        GUILayout.Space(6);
        EditorGUILayout.EndHorizontal();
    }

    // ── Tab Toolbar ────────────────────────────────────────────

    private void DrawTabToolbar()
    {
        string[] tabIcons   = new string[] { "\U0001f6a7", "\U0001f3d8", "\U0001f7e9", "\U0001f3db", "\U0001f916", "\U0001f527", "\U0001f4ca" };
        string[] tabLabels  = new string[] { "STRADE",     "BLOCCHI",    "ZONE",       "EDIFICI",    "PROC",       "STRUM",      "STAT"       };
        EditorSection[] tabSections = new EditorSection[]
        {
            EditorSection.Paths, EditorSection.Blocks, EditorSection.Zoning, EditorSection.Buildings,
            EditorSection.ProceduralGeneration, EditorSection.Tools, EditorSection.Statistics
        };

        EditorGUILayout.BeginHorizontal(GUILayout.Height(36));
        for (int i = 0; i < tabSections.Length; i++)
        {
            bool isActive = currentSection == tabSections[i];
            Color sectionColor = GetSectionColor(tabSections[i]);
            GUI.backgroundColor = isActive ? sectionColor : new Color(0.25f, 0.25f, 0.25f);
            GUIStyle style = isActive ? activeTabStyle : tabButtonStyle;
            string label = tabIcons[i] + "\n" + tabLabels[i];
            if (GUILayout.Button(label, style, GUILayout.ExpandWidth(true), GUILayout.Height(36)))
            {
                currentSection = tabSections[i];
                OnTabSelected(tabSections[i]);
            }
            GUI.backgroundColor = Color.white;
        }
        EditorGUILayout.EndHorizontal();

        Rect lineRect = GUILayoutUtility.GetRect(0, 3, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(lineRect, GetSectionColor(currentSection));
    }

    private void OnTabSelected(EditorSection section)
    {
        switch (section)
        {
            case EditorSection.Paths:  cityManager.SetMode(CityManager.BuildMode.Idle);         break;
            case EditorSection.Blocks: cityManager.SetMode(CityManager.BuildMode.CreateBlock);  break;
            case EditorSection.Zoning: cityManager.SetMode(CityManager.BuildMode.AssignZoning); break;
            default:                   cityManager.SetMode(CityManager.BuildMode.Idle);         break;
        }
        SceneView.RepaintAll();
    }

    // ── Phase Header ───────────────────────────────────────────

    private void DrawPhaseHeader()
    {
        string icon, title, subtitle;
        switch (currentSection)
        {
            case EditorSection.Paths:               icon="\U0001f6a7"; title="STRADE";         subtitle="Traccia nodi e segmenti stradali"; break;
            case EditorSection.Blocks:              icon="\U0001f3d8"; title="BLOCCHI";         subtitle="Definisci i blocchi urbani"; break;
            case EditorSection.Zoning:              icon="\U0001f7e9"; title="ZONE / LOTTI";    subtitle="Assegna zone e genera lotti"; break;
            case EditorSection.Buildings:           icon="\U0001f3db"; title="EDIFICI";         subtitle="Spawn edifici dai prefab di zona"; break;
            case EditorSection.ProceduralGeneration:icon="\U0001f916"; title="PROCEDURALE";     subtitle="Generazione citta automatica"; break;
            case EditorSection.Tools:               icon="\U0001f527"; title="STRUMENTI";       subtitle="Manutenzione e azioni correttive"; break;
            case EditorSection.Statistics:          icon="\U0001f4ca"; title="STATISTICHE";     subtitle="Dati metrici della citta corrente"; break;
            default:                                icon="";           title="";                subtitle=""; break;
        }
        Color sectionColor = GetSectionColor(currentSection);
        Rect headerRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(32));
        EditorGUI.DrawRect(headerRect, new Color(sectionColor.r * 0.35f, sectionColor.g * 0.35f, sectionColor.b * 0.35f));
        GUILayout.Space(8);
        GUILayout.Label(icon + "  " + title, phaseHeaderStyle, GUILayout.ExpandWidth(false));
        GUILayout.Space(10);
        GUILayout.Label(subtitle, statusBarStyle, GUILayout.ExpandWidth(false));
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawCurrentSection()
    {
        switch (currentSection)
        {
            case EditorSection.Paths:               DrawPathsSection();             break;
            case EditorSection.Blocks:              DrawBlocksSection();            break;
            case EditorSection.Zoning:              DrawZoningSection();            break;
            case EditorSection.Buildings:           DrawBuildingsSection();         break;
            case EditorSection.ProceduralGeneration:DrawProceduralGenerationSection(); break;
            case EditorSection.Tools:               DrawToolsSection();             break;
            case EditorSection.Statistics:          DrawStatsSection();             break;
        }
    }

    // ── Helpers UI ─────────────────────────────────────────────

    private void DrawSubHeader(string label)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(label, EditorStyles.boldLabel);
        Rect r = GUILayoutUtility.GetRect(0, 1, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, new Color(0.4f, 0.4f, 0.4f));
        EditorGUILayout.Space(3);
    }

    private bool DrawActionButton(string label, Color? tint = null)
    {
        if (tint.HasValue) GUI.backgroundColor = tint.Value;
        bool pressed = GUILayout.Button(label, actionButtonStyle, GUILayout.ExpandWidth(true));
        GUI.backgroundColor = Color.white;
        return pressed;
    }

    private void DrawModeButton(string label, CityManager.BuildMode mode, CityManager.BuildMode currentMode, Color sectionColor)
    {
        bool active = currentMode == mode;
        GUI.backgroundColor = active ? sectionColor : new Color(0.3f, 0.3f, 0.3f);
        GUIStyle style = active ? activeModeButtonStyle : modeButtonStyle;
        if (GUILayout.Button(label, style, GUILayout.Width(80), GUILayout.Height(50)))
            cityManager.SetMode(mode);
        GUI.backgroundColor = Color.white;
    }

    // ── STRADE ─────────────────────────────────────────────────

    private void DrawPathsSection()
    {
        DrawSubHeader("MODALITA");
        CityManager.BuildMode currentMode = cityManager.GetCurrentMode();
        EditorGUILayout.BeginHorizontal();
        DrawModeButton("\U0001f5b1\nSeleziona",  CityManager.BuildMode.Idle,         currentMode, ColPaths);
        DrawModeButton("\u2795\nAggiungi",        CityManager.BuildMode.AddNodes,     currentMode, ColPaths);
        DrawModeButton("\U0001f517\nConnetti",    CityManager.BuildMode.ConnectNodes, currentMode, ColPaths);
        EditorGUILayout.EndHorizontal();

        switch (currentMode)
        {
            case CityManager.BuildMode.Idle:
                EditorGUILayout.HelpBox("Clicca un nodo per selezionarlo. Clicca un segmento per modificarne il profilo.", MessageType.Info);
                break;
            case CityManager.BuildMode.AddNodes:
                EditorGUILayout.HelpBox("Clicca nella Scene View per aggiungere nuovi nodi stradali.", MessageType.Info);
                break;
            case CityManager.BuildMode.ConnectNodes:
                EditorGUILayout.HelpBox("Clicca due nodi in sequenza per collegarli con un segmento.", MessageType.Info);
                break;
        }

        EditorGUILayout.Space(6);
        DrawSubHeader("GRIGLIA");
        CitySceneHandle.SnapToGridEnabled = EditorGUILayout.Toggle("Snap alla griglia", CitySceneHandle.SnapToGridEnabled);
        using (new EditorGUI.DisabledScope(!CitySceneHandle.SnapToGridEnabled))
        {
            CitySceneHandle.GridSize = Mathf.Max(0.1f, EditorGUILayout.FloatField("Passo griglia", CitySceneHandle.GridSize));
        }

        EditorGUILayout.Space(6);
        _terrainFoldout = EditorGUILayout.Foldout(_terrainFoldout, "TERRAIN", true, EditorStyles.foldoutHeader);
        if (_terrainFoldout)
        {
            EditorGUI.indentLevel++;

            bool alignNodesToTerrain = EditorGUILayout.Toggle("Allinea nodi al Terrain", cityData.alignNodesToTerrain);
            if (alignNodesToTerrain != cityData.alignNodesToTerrain)
            {
                Undo.RecordObject(cityData, "Toggle Terrain Node Alignment");
                cityData.alignNodesToTerrain = alignNodesToTerrain;
                EditorUtility.SetDirty(cityData);
            }
            using (new EditorGUI.DisabledScope(!cityData.alignNodesToTerrain))
            {
                float nyo = Mathf.Clamp(EditorGUILayout.FloatField("Offset Y nodi", cityData.nodeTerrainYOffset), -2f, 2f);
                if (!Mathf.Approximately(nyo, cityData.nodeTerrainYOffset))
                {
                    Undo.RecordObject(cityData, "Set Node Terrain Y Offset");
                    cityData.nodeTerrainYOffset = nyo;
                    EditorUtility.SetDirty(cityData);
                }
            }

            float rtw = EditorGUILayout.Slider("Sculpt Width Multiplier", cityData.roadTerrainWidthMultiplier, 0.5f, 3f);
            if (!Mathf.Approximately(rtw, cityData.roadTerrainWidthMultiplier))
            {
                Undo.RecordObject(cityData, "Set Road Terrain Width Multiplier");
                cityData.roadTerrainWidthMultiplier = rtw;
                EditorUtility.SetDirty(cityData);
            }
            float rtf = EditorGUILayout.Slider("Sculpt Falloff", cityData.roadTerrainFalloff, 0.1f, 12f);
            if (!Mathf.Approximately(rtf, cityData.roadTerrainFalloff))
            {
                Undo.RecordObject(cityData, "Set Road Terrain Falloff");
                cityData.roadTerrainFalloff = rtf;
                EditorUtility.SetDirty(cityData);
            }
            float rbs = EditorGUILayout.Slider("Sculpt Blend Strength", cityData.roadTerrainBlendStrength, 0.05f, 1f);
            if (!Mathf.Approximately(rbs, cityData.roadTerrainBlendStrength))
            {
                Undo.RecordObject(cityData, "Set Road Terrain Blend Strength");
                cityData.roadTerrainBlendStrength = rbs;
                EditorUtility.SetDirty(cityData);
            }
            if (DrawActionButton("Flatten Terrain Under Roads"))
                FlattenTerrainUnderRoads();

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(6);
        DrawSubHeader("PROFILI STRADALI");
        EditorGUILayout.HelpBox("La larghezza globale e un fallback per segmenti senza RoadProfile assegnato.", MessageType.Info);

        RoadProfile defaultRoadProfile = (RoadProfile)EditorGUILayout.ObjectField(
            "Road Profile di default", cityData.defaultRoadProfile, typeof(RoadProfile), false);
        if (defaultRoadProfile != cityData.defaultRoadProfile)
        {
            Undo.RecordObject(cityData, "Set Default Road Profile");
            cityData.defaultRoadProfile = defaultRoadProfile;
            EditorUtility.SetDirty(cityData);
        }

        float globalWidth = EditorGUILayout.Slider("Larghezza Globale (fallback)", cityData.globalRoadWidth, 1f, 10f);
        if (globalWidth != cityData.globalRoadWidth)
            cityManager.SetGlobalRoadWidth(globalWidth);

        EditorGUILayout.Space(4);
        EditorGUILayout.BeginHorizontal();
        if (DrawActionButton("Setup Road Profiles di Default"))
            CityBuilderMenu.SetupDefaultRoadProfiles();
        if (DrawActionButton("Setup Zone Types di Default"))
            CityBuilderMenu.SetupDefaultZoneTypes();
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        DrawSubHeader("INFORMAZIONI");
        EditorGUILayout.LabelField(string.Format("Nodi: {0}   Segmenti: {1}", cityData.nodes.Count, cityData.segments.Count));
        EditorGUILayout.LabelField(string.Format("Nodo selezionato: {0}   Segmento: {1}", cityManager.GetSelectedNodeID(), cityManager.GetSelectedSegmentID()));
    }

    // ── BLOCCHI ────────────────────────────────────────────────

    private void DrawBlocksSection()
    {
        DrawSubHeader("MODALITA");
        CityManager.BuildMode currentMode = cityManager.GetCurrentMode();
        GUI.backgroundColor = currentMode == CityManager.BuildMode.CreateBlock ? ColBlocks : new Color(0.3f, 0.3f, 0.3f);
        if (GUILayout.Button("\U0001f3d8\nCrea Blocco", modeButtonStyle, GUILayout.Width(90), GUILayout.Height(50)))
            cityManager.SetMode(CityManager.BuildMode.CreateBlock);
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6);
        DrawSubHeader("AZIONI");
        if (DrawActionButton("Flatten Terrain sotto i Blocchi"))
            FlattenTerrainUnderBlocks();

        EditorGUILayout.Space(6);
        bool dummyBlockUiState = true;
        CityBlockEditor.DrawBlockEditorUI(cityManager, ref dummyBlockUiState);
    }

    // ── ZONE / LOTTI ───────────────────────────────────────────

    private void DrawZoningSection()
    {
        DrawSubHeader("MODALITA");
        CityManager.BuildMode currentMode = cityManager.GetCurrentMode();
        GUI.backgroundColor = currentMode == CityManager.BuildMode.AssignZoning ? ColZoning : new Color(0.3f, 0.3f, 0.3f);
        if (GUILayout.Button("\U0001f7e9\nAssegna Zoning", modeButtonStyle, GUILayout.Width(110), GUILayout.Height(50)))
            cityManager.SetMode(CityManager.BuildMode.AssignZoning);
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(6);
        CityZoningEditor.DrawZoningEditorUI(cityManager);
        CityZoningEditor.DrawZoningStats(cityData);

        EditorGUILayout.Space(6);
        DrawSubHeader("LOTTI");
        if (DrawActionButton("Genera Lotti per tutti i Blocchi", ColZoning * 0.7f))
            GenerateAllLots();
        if (DrawActionButton("Flatten Terrain sotto i Lotti"))
            FlattenTerrainUnderLots();

        EditorGUILayout.BeginHorizontal();
        if (DrawActionButton("Cancella tutti i Lotti", ColTools * 0.7f))
        {
            Undo.RecordObject(cityData, "Cancella Tutti i Lotti");
            int removedLots = cityManager.ClearAllLots();
            EditorUtility.SetDirty(cityData);
            SceneView.RepaintAll();
            EditorUtility.DisplayDialog("Cancella Lotti", "Lotti rimossi: " + removedLots, "OK");
        }
        int selectedBlockID = cityManager.GetSelectedBlockID();
        using (new EditorGUI.DisabledScope(selectedBlockID < 0 || cityData.GetBlock(selectedBlockID) == null))
        {
            if (DrawActionButton("Cancella Lotti Blocco Selezionato", ColTools * 0.7f))
            {
                Undo.RecordObject(cityData, "Cancella Lotti Blocco");
                int removedLots = cityManager.ClearLotsForBlock(selectedBlockID);
                EditorUtility.SetDirty(cityData);
                SceneView.RepaintAll();
                EditorUtility.DisplayDialog("Cancella Lotti", "Lotti rimossi dal blocco " + selectedBlockID + ": " + removedLots, "OK");
            }
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Lotti totali: " + cityData.lots.Count, EditorStyles.miniLabel);
    }

    // ── EDIFICI ────────────────────────────────────────────────

    private void DrawBuildingsSection()
    {
        EditorGUILayout.HelpBox("Usa i prefab configurati in ZoneType. Ogni prefab dovrebbe avere il componente CityBuilderPrefab per il calcolo footprint.", MessageType.Info);
        EditorGUILayout.Space(4);

        DrawSubHeader("SPAWN");
        if (DrawActionButton("\U0001f3db  Spawn Edifici da ZoneType", ColBuildings * 0.8f))
            SpawnBuildingsFromZoneTypes();
        if (DrawActionButton("Cancella Edifici Spawnati", ColTools * 0.7f))
            ClearSpawnedBuildings();

        EditorGUILayout.Space(6);
        DrawSubHeader("TERRAIN LOTTI");
        float ltf = EditorGUILayout.Slider("Falloff Lotti", cityData.lotTerrainFalloff, 0.1f, 10f);
        if (!Mathf.Approximately(ltf, cityData.lotTerrainFalloff))
        {
            Undo.RecordObject(cityData, "Set Lot Terrain Falloff");
            cityData.lotTerrainFalloff = ltf;
            EditorUtility.SetDirty(cityData);
        }
        float lbs = EditorGUILayout.Slider("Blend Strength Lotti", cityData.lotTerrainBlendStrength, 0.05f, 1f);
        if (!Mathf.Approximately(lbs, cityData.lotTerrainBlendStrength))
        {
            Undo.RecordObject(cityData, "Set Lot Terrain Blend Strength");
            cityData.lotTerrainBlendStrength = lbs;
            EditorUtility.SetDirty(cityData);
        }
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Lotti totali: " + cityData.lots.Count, EditorStyles.miniLabel);
    }

    // ── STRUMENTI ──────────────────────────────────────────────

    private void DrawToolsSection()
    {
        EditorGUILayout.HelpBox("Utilita di manutenzione dati e azioni correttive sul city graph.", MessageType.Info);

        if (_isGenerating)
        {
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(20)), _generationProgress, _generationStatus);
            if (GUILayout.Button("Annulla Operazione", GUILayout.Height(24)))
                StopAsyncGeneration();
            EditorGUILayout.Space(6);
        }

        DrawSubHeader("PROFILI STRADE");
        AmericanCityConfig americanConfig = proceduralConfig as AmericanCityConfig;
        if (americanConfig != null)
        {
            if (DrawActionButton("Aggiorna Profili Strade Esistenti", ColConfig * 0.8f))
            {
                if (cityData != null && cityData.segments != null)
                {
                    Undo.RecordObject(cityData, "Update Road Profiles");
                    int updated = 0;
                    foreach (CitySegment seg in cityData.segments)
                    {
                        if (seg == null) continue;
                        RoadProfile profile = null;
                        if (seg.roadProfile != null)
                        {
                            seg.width = seg.roadProfile.roadWidth;
                            updated++;
                        }
                        else
                        {
                            if (americanConfig.highwayProfile != null &&
                                seg.width >= americanConfig.highwayProfile.roadWidth * 0.75f)
                                profile = americanConfig.highwayProfile;
                            else if (americanConfig.majorGridProfile != null)
                                profile = americanConfig.majorGridProfile;
                            else if (americanConfig.localStreetProfile != null)
                                profile = americanConfig.localStreetProfile;
                            if (profile != null)
                            {
                                seg.roadProfile = profile;
                                seg.width = profile.roadWidth;
                                updated++;
                            }
                        }
                    }
                    EditorUtility.SetDirty(cityData);
                    SceneView.RepaintAll();
                    EditorUtility.DisplayDialog("Profili aggiornati", updated + " segmenti aggiornati.", "OK");
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Questa azione richiede un AmericanCityConfig attivo nel plugin process corrente.", MessageType.None);
        }

        DrawSubHeader("PERCORSI");
        simplifyMaxDeviationDeg = EditorGUILayout.Slider("Dev. max semplifica (gradi)", simplifyMaxDeviationDeg, 1f, 25f);
        if (DrawActionButton("Semplifica Percorsi"))
        {
            string report = cityManager.SimplifyPaths(simplifyMaxDeviationDeg);
            EditorUtility.SetDirty(cityData);
            SceneView.RepaintAll();
            EditorUtility.DisplayDialog("Semplifica Percorsi", report, "OK");
        }
        if (DrawActionButton("Ripara Collegamenti"))
        {
            Undo.RecordObject(cityData, "Ripara Collegamenti");
            string report = cityManager.RepairConnections();
            EditorUtility.SetDirty(cityData);
            SceneView.RepaintAll();
            EditorUtility.DisplayDialog("Ripara Collegamenti", report, "OK");
        }

        DrawSubHeader("NODI");
        EditorGUILayout.LabelField("Distanza massima di fusione:");
        weldNodesDistance = EditorGUILayout.Slider(weldNodesDistance, 0.1f, 10f);
        if (DrawActionButton("Salda Nodi Ravvicinati"))
        {
            Undo.RecordObject(cityData, "Salda Nodi Ravvicinati");
            string report = cityManager.WeldCloseNodes(weldNodesDistance);
            EditorUtility.SetDirty(cityData);
            SceneView.RepaintAll();
            EditorUtility.DisplayDialog("Salda Nodi", report, "OK");
        }

        DrawSubHeader("ANALISI");
        if (DrawActionButton("Analizza Intersezioni Geometriche"))
        {
            string report = cityManager.AnalyzeIntersections();
            EditorUtility.DisplayDialog("Analisi Intersezioni", report, "OK");
        }
        if (!_isGenerating && DrawActionButton("Planarizza Rete Stradale", ColProc * 0.7f))
        {
            AmericanCityConfig planarizeConfig = proceduralConfig as AmericanCityConfig;
            float mergeTol = planarizeConfig != null ? planarizeConfig.mergeThreshold : 2f;
            Undo.RecordObject(cityData, "Planarizza Rete Stradale");

            int nodesBefore = cityData.nodes.Count;
            int segsBefore  = cityData.segments.Count;

            StartAsyncGeneration(
                CityRoadPlanarizer.PlanarizeAsync(
                    cityManager,
                    mergeTol,
                    (p, s) => { _generationProgress = p; _generationStatus = s; },
                    splitsDone =>
                    {
                        int nodesAdded = cityData.nodes.Count - nodesBefore;
                        int segsAdded  = cityData.segments.Count - segsBefore;

                        string report = $"Segmenti planarizzati: {splitsDone}\nNodi aggiunti: {nodesAdded}\nSegmenti delta: {segsAdded}";
                        _lastProceduralReport = report;

                        EditorUtility.SetDirty(cityData);
                        SceneView.RepaintAll();
                        Debug.Log($"[CityBuilder] Planarizzazione: {report}");
                        EditorUtility.DisplayDialog("Planarizza Rete", report, "OK");
                    }),
                null);
        }
        if (DrawActionButton("Setup Zone Types di Default"))
            CityBuilderMenu.SetupDefaultZoneTypes();

        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.8f);
        if (GUILayout.Button("\U0001f4ca  Esporta Statistiche (Console)", actionButtonStyle, GUILayout.ExpandWidth(true)))
            cityManager.LogStats();
        GUI.backgroundColor = Color.white;

        DrawSubHeader("MESH STRADE");
        EditorGUILayout.HelpBox("Genera mesh Unity reali (MeshFilter + MeshRenderer) leggendo il materiale dal RoadProfile di ciascun segmento (fallback: defaultRoadProfile su CityData).", MessageType.None);
        if (DrawActionButton("Genera Mesh Strade", new Color(0.25f, 0.60f, 0.45f)))
        {
            CityRoadMeshBuilder.Build(cityManager);
            SceneView.RepaintAll();
        }
        if (DrawActionButton("Cancella Mesh Strade", new Color(0.55f, 0.35f, 0.35f)))
        {
            CityRoadMeshBuilder.DeleteMesh();
            SceneView.RepaintAll();
        }

        EditorGUILayout.Space(8);
        DrawSubHeader("PERICOLO");
        GUI.backgroundColor = new Color(0.85f, 0.25f, 0.2f);
        if (GUILayout.Button("\U0001f5d1  Cancella Tutto", actionButtonStyle, GUILayout.ExpandWidth(true)))
        {
            cityManager.ResetCity();
            CityBlockEditor.ClearPreview();
        }
        GUI.backgroundColor = Color.white;
    }

    // ── STATISTICHE ────────────────────────────────────────────

    private void DrawStatsSection()
    {
        DrawSubHeader("DATI CITTA");
        EditorGUILayout.LabelField("Nodi Stradali:      " + cityData.nodes.Count);
        EditorGUILayout.LabelField("Segmenti Stradali:  " + cityData.segments.Count);
        EditorGUILayout.LabelField("Blocchi:            " + cityData.blocks.Count);
        EditorGUILayout.LabelField("Lotti:              " + cityData.lots.Count);
        EditorGUILayout.Space(6);
        float totalArea = 0f;
        foreach (var block in cityData.blocks)
            totalArea += block.GetArea();
        EditorGUILayout.LabelField(string.Format("Area totale blocchi: {0:F2} m", totalArea));
    }

    // ── PROCEDURALE ────────────────────────────────────────────

    private void DrawProceduralGenerationSection()
    {
        CityPluginSettings pluginSettings = CityPluginSettingsEditorUtility.GetOrCreateSettings();
        List<CityPluginDescriptor> processPlugins = CityPluginRegistry.GetPlugins(CityPluginCategory.Process);

        DrawSubHeader("PLUGIN PROCESS");
        if (processPlugins.Count == 0)
        {
            EditorGUILayout.HelpBox("Nessun plugin process disponibile.", MessageType.Warning);
            return;
        }

        int currentIndex = 0;
        string[] labels = new string[processPlugins.Count];
        for (int i = 0; i < processPlugins.Count; i++)
        {
            labels[i] = processPlugins[i].displayName + " (" + processPlugins[i].id + ")";
            if (string.Equals(processPlugins[i].id, pluginSettings.activeProcessPluginId, StringComparison.OrdinalIgnoreCase))
                currentIndex = i;
        }

        int newIndex = EditorGUILayout.Popup("Plugin di Generazione", currentIndex, labels);
        if (newIndex != currentIndex)
        {
            pluginSettings.activeProcessPluginId = processPlugins[newIndex].id;
            EditorUtility.SetDirty(pluginSettings);
            AssetDatabase.SaveAssets();
        }

        if (DrawActionButton("Apri Plugin Browser", ColProc * 0.9f))
            CityPluginBrowserWindow.ShowWindow();

        ICityProcessPlugin process = CityPluginRegistry.Create<ICityProcessPlugin>(CityPluginCategory.Process, pluginSettings.activeProcessPluginId);
        ICityProcessPluginEditorUI processUi = process as ICityProcessPluginEditorUI;

        DrawSubHeader("CONFIGURAZIONE PLUGIN");
        if (processUi == null)
        {
            EditorGUILayout.HelpBox("Il plugin process attivo non espone una UI custom di configurazione.", MessageType.Info);
        }
        else
        {
            Type cfgType = processUi.ConfigurationType;
            string cfgLabel = string.IsNullOrWhiteSpace(processUi.ConfigurationLabel) ? "Process Config" : processUi.ConfigurationLabel;

            EditorGUI.BeginChangeCheck();
            ScriptableObject newConfig = (ScriptableObject)EditorGUILayout.ObjectField(
                cfgLabel,
                proceduralConfig,
                cfgType,
                false);
            if (EditorGUI.EndChangeCheck())
                proceduralConfig = newConfig;

            if (proceduralConfig == null)
            {
                EditorGUILayout.HelpBox("Assegna un asset di configurazione oppure creane uno nuovo.", MessageType.Warning);
                if (DrawActionButton("Crea Configurazione", ColProc * 0.8f))
                    proceduralConfig = processUi.CreateDefaultConfigurationAsset();
                return;
            }

            if (!cfgType.IsInstanceOfType(proceduralConfig))
            {
                EditorGUILayout.HelpBox("La configurazione assegnata non è compatibile con il plugin selezionato.", MessageType.Error);
                return;
            }

            processUi.DrawConfigurationGUI(proceduralConfig);
        }

        DrawSubHeader("PLUGIN ATTIVI");
        EditorGUILayout.LabelField("Process", pluginSettings.activeProcessPluginId, EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Road Network", pluginSettings.activeRoadNetworkPluginId, EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Road Planarization", pluginSettings.activeRoadPlanarizationPluginId, EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Block Detection", pluginSettings.activeBlockDetectionPluginId, EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Zoning", pluginSettings.activeZoningPluginId, EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Lot Layout", pluginSettings.activeLotLayoutPluginId, EditorStyles.miniLabel);
        EditorGUILayout.LabelField("Lot Selection", pluginSettings.activeLotSelectionPluginId, EditorStyles.miniLabel);

        AmericanCityConfig americanConfig = proceduralConfig as AmericanCityConfig;
        DrawSubHeader("AZIONI");

        if (_isGenerating)
        {
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(GUILayout.Height(20)), _generationProgress, _generationStatus);
            if (GUILayout.Button("Annulla Generazione", GUILayout.Height(24)))
                StopAsyncGeneration();
        }
        else
        {
            using (new EditorGUI.DisabledScope(americanConfig == null))
            {
                if (DrawActionButton("Genera Rete Stradale", ColProc * 0.7f))
                {
                    bool ok = EditorUtility.DisplayDialog("Genera Rete Stradale",
                        "Verranno aggiunti nodi e segmenti alla rete stradale esistente. Continuare?", "Genera", "Annulla");
                    if (ok)
                    {
                        CityGenerationReport report = CityGenerationPipelineHost.GenerateRoadNetwork(cityManager, americanConfig);
                        _lastProceduralReport = report.ToMultilineString();
                        EditorUtility.DisplayDialog("Rete Stradale Generata", _lastProceduralReport, "OK");
                    }
                }

                if (DrawActionButton("Assegna Zoning Automatico (per distanza)"))
                {
                    CityGenerationReport report = CityGenerationPipelineHost.AssignZoning(cityManager, americanConfig);
                    _lastProceduralReport = report.ToMultilineString();
                    EditorUtility.DisplayDialog("Zoning Assegnato", _lastProceduralReport, "OK");
                }
            }

            if (americanConfig == null)
                EditorGUILayout.HelpBox("Le azioni step-by-step (Road/Zoning) richiedono un AmericanCityConfig. Usa GENERA TUTTO per process plugin non-americani.", MessageType.None);
        }

        EditorGUILayout.Space(4);
        GUI.backgroundColor = new Color(0.3f, 0.85f, 0.5f);
        bool genAllDisabled = _isGenerating || proceduralConfig == null;
        GUI.enabled = !genAllDisabled;
        if (GUILayout.Button("▶  GENERA TUTTO (via Process Plugin)", GUILayout.Height(40)))
        {
            GUI.backgroundColor = Color.white;
            bool ok = EditorUtility.DisplayDialog("Genera Tutto",
                "La pipeline completa verrà delegata al plugin process selezionato. Continuare?",
                "Genera", "Annulla");
            if (ok)
            {
                CityGenerationReport report = CityGenerationPipelineHost.GenerateAll(cityManager, proceduralConfig);
                _lastProceduralReport = report.ToMultilineString();
                EditorUtility.DisplayDialog("Generazione Completata", _lastProceduralReport, "OK");
            }
        }
        GUI.enabled = true;
        GUI.backgroundColor = Color.white;

        if (!string.IsNullOrEmpty(_lastProceduralReport))
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.HelpBox(_lastProceduralReport, MessageType.Info);
        }
    }

    // ── Inspector segmento (pannello destro) ───────────────────

    private void DrawSegmentInspectorPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(220));
        Rect panelRect = EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(214), GUILayout.ExpandHeight(true));
        EditorGUI.DrawRect(panelRect, new Color(0.18f, 0.18f, 0.18f));

        GUILayout.Label("\U0001f6a7  SEGMENTO", phaseHeaderStyle);
        Rect lr = GUILayoutUtility.GetRect(0, 2, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(lr, ColPaths);

        inspectorScrollPos = EditorGUILayout.BeginScrollView(inspectorScrollPos, GUILayout.ExpandHeight(true));

        int selectedSegmentID = cityManager.GetSelectedSegmentID();
        CitySegment selectedSegment = cityData.GetSegment(selectedSegmentID);
        if (selectedSegment == null)
        {
            EditorGUILayout.HelpBox("Clicca un segmento in Idle per modificarne profilo e geometria.", MessageType.None);
        }
        else
        {
            CityNode nodeA = cityData.GetNode(selectedSegment.nodeA_ID);
            CityNode nodeB = cityData.GetNode(selectedSegment.nodeB_ID);

            EditorGUILayout.LabelField("ID: " + selectedSegment.id, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Nodi: " + selectedSegment.nodeA_ID + " > " + selectedSegment.nodeB_ID, EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Lunghezza: " + CityRoadGeometry.EstimateLength(cityData, selectedSegment).ToString("F2") + " m", EditorStyles.miniLabel);
            EditorGUILayout.LabelField("Larghezza: " + CityRoadGeometry.GetRoadWidth(cityData, selectedSegment).ToString("F2") + " m", EditorStyles.miniLabel);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Profilo:", EditorStyles.boldLabel);
            List<RoadProfile> roadProfiles = RoadProfileEditorUtility.LoadAllRoadProfiles();
            string[] profileLabels = new string[roadProfiles.Count + 1];
            profileLabels[0] = "None";
            int selectedProfileIndex = 0;
            for (int i = 0; i < roadProfiles.Count; i++)
            {
                profileLabels[i + 1] = RoadProfileEditorUtility.GetRoadProfileDisplayName(roadProfiles[i]);
                if (selectedSegment.roadProfile == roadProfiles[i])
                    selectedProfileIndex = i + 1;
            }
            int newProfileIndex = EditorGUILayout.Popup(selectedProfileIndex, profileLabels);
            RoadProfile newProfile = newProfileIndex > 0 ? roadProfiles[newProfileIndex - 1] : null;
            if (newProfile != selectedSegment.roadProfile)
            {
                Undo.RecordObject(cityData, "Set Segment Road Profile");
                cityManager.SetSegmentRoadProfile(selectedSegment.id, newProfile);
                EditorUtility.SetDirty(cityData);
            }

            EditorGUILayout.Space(2);
            CitySegmentGeometryType newGeomType = (CitySegmentGeometryType)EditorGUILayout.EnumPopup("Geometria", selectedSegment.geometryType);
            if (newGeomType != selectedSegment.geometryType)
            {
                Undo.RecordObject(cityData, "Set Segment Geometry");
                cityManager.SetSegmentGeometryType(selectedSegment.id, newGeomType);
                EditorUtility.SetDirty(cityData);
                SceneView.RepaintAll();
            }

            if (selectedSegment.geometryType == CitySegmentGeometryType.Bezier)
            {
                if (GUILayout.Button("Reset maniglie Bezier", buttonStyle))
                {
                    Undo.RecordObject(cityData, "Reset Segment Curve Handles");
                    cityManager.ResetSegmentBezierHandles(selectedSegment.id);
                    EditorUtility.SetDirty(cityData);
                    SceneView.RepaintAll();
                }
                if (nodeA != null) EditorGUILayout.Vector3Field("CP A", selectedSegment.controlPointA);
                if (nodeB != null) EditorGUILayout.Vector3Field("CP B", selectedSegment.controlPointB);
            }
            else
            {
                EditorGUILayout.HelpBox("Converti in Bezier per modificare le maniglie in Scene View.", MessageType.Info);
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndVertical();
    }

    // ── Helper methods ─────────────────────────────────────────

    private string GetModeDisplayLabel(CityManager.BuildMode mode)
    {
        switch (mode)
        {
            case CityManager.BuildMode.Idle:          return "Seleziona/Sposta Nodi";
            case CityManager.BuildMode.AddNodes:       return "Aggiungi/Modifica Nodi";
            case CityManager.BuildMode.ConnectNodes:   return "Connetti Nodi";
            case CityManager.BuildMode.CreateBlock:    return "Crea Blocco";
            case CityManager.BuildMode.AssignZoning:   return "Assegna Zoning";
            default:                                   return mode.ToString();
        }
    }

    private void FindCityManager()
    {
        cityManager = UnityEngine.Object.FindAnyObjectByType<CityManager>();
        if (cityManager != null)
            cityData = cityManager.GetCityData();
    }

    private void GenerateAllLots()
    {
        if (cityData.blocks.Count == 0)
        {
            EditorUtility.DisplayDialog("Info", "Nessun blocco disponibile!", "OK");
            return;
        }
        int lotCount = RunLotGeneration();
        Debug.Log("[CityBuilderWindow] Generati " + lotCount + " lotti!");
        EditorUtility.DisplayDialog("Successo", "Generati " + lotCount + " lotti!", "OK");
    }

    private int RunLotGeneration()
    {
        return CityGenerationPipelineHost.GenerateLots(cityManager, proceduralConfig as AmericanCityConfig);
    }

    private void SpawnBuildingsFromZoneTypes()
    {
        int choice = EditorUtility.DisplayDialogComplex(
            "Spawn Edifici",
            "Come vuoi gestire gli edifici gia spawnati?",
            "Cancella precedenti e spawn",
            "Mantieni esistenti e spawn",
            "Annulla");
        if (choice == 2) return;
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
        EditorUtility.DisplayDialog("Cancella Edifici Spawnati", "Oggetti rimossi: " + removedCount, "OK");
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

    private void FlattenTerrainUnderBlocks()
    {
        CityBuildingSpawner.BlockFlattenReport report = CityBuildingSpawner.FlattenTerrainUnderBlocksConsolidated(cityManager);
        SceneView.RepaintAll();
        EditorUtility.DisplayDialog("Flatten Terrain - Blocchi & Lotti (Consolidato)", report.ToMultilineString(), "OK");
    }
}
