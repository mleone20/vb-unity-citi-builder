using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using BSCCityBuilder.Components;
using BSCCityBuilder.Core;

namespace BSCCityBuilder.Editor.Windows
{
public sealed class CityBuilderAssetsWindow : EditorWindow
{
    private enum StatusFilter
    {
        All,
        Valid,
        Warnings
    }

    private enum SortMode
    {
        Name,
        Path,
        Footprint
    }

    private sealed class AssetEntry
    {
        public GameObject prefab;
        public CityBuilderPrefab metadata;
        public string path;
        public string searchableText;
        public Vector2 footprint;
        public List<string> zones = new List<string>();
        public List<string> warnings = new List<string>();
    }

    private readonly List<AssetEntry> entries = new List<AssetEntry>();
    private readonly List<AssetEntry> visibleEntries = new List<AssetEntry>();
    private Vector2 scrollPosition;
    private string searchText = string.Empty;
    private StatusFilter statusFilter;
    private SortMode sortMode;
    private bool needsRefresh;

    [MenuItem("Window/City Builder/City Builder Assets")]
    public static void ShowWindow()
    {
        CityBuilderAssetsWindow window = GetWindow<CityBuilderAssetsWindow>();
        window.titleContent = new GUIContent("City Builder Assets");
        window.minSize = new Vector2(620f, 320f);
        window.Show();
    }

    private void OnEnable()
    {
        titleContent = new GUIContent("City Builder Assets");
        EditorApplication.projectChanged += OnProjectChanged;
        RefreshDatabase();
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= OnProjectChanged;
    }

    private void OnProjectChanged()
    {
        needsRefresh = true;
        Repaint();
    }

    private void OnGUI()
    {
        if (needsRefresh && Event.current.type == EventType.Layout)
        {
            RefreshDatabase();
        }

        DrawToolbar();
        DrawSummary();

        if (visibleEntries.Count == 0)
        {
            EditorGUILayout.HelpBox(
                entries.Count == 0
                    ? "Nessun prefab con componente CityBuilderPrefab trovato nel progetto."
                    : "Nessun asset corrisponde ai filtri correnti.",
                MessageType.Info);
            return;
        }

        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        for (int i = 0; i < visibleEntries.Count; i++)
        {
            DrawAssetRow(visibleEntries[i]);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        string newSearch = GUILayout.TextField(
            searchText,
            GUI.skin.FindStyle("ToolbarSearchTextField"),
            GUILayout.MinWidth(180f));
        if (!string.Equals(newSearch, searchText, StringComparison.Ordinal))
        {
            searchText = newSearch;
            RebuildVisibleEntries();
        }

        StatusFilter newFilter = (StatusFilter)EditorGUILayout.EnumPopup(
            statusFilter, EditorStyles.toolbarPopup, GUILayout.Width(90f));
        if (newFilter != statusFilter)
        {
            statusFilter = newFilter;
            RebuildVisibleEntries();
        }

        SortMode newSort = (SortMode)EditorGUILayout.EnumPopup(
            sortMode, EditorStyles.toolbarPopup, GUILayout.Width(90f));
        if (newSort != sortMode)
        {
            sortMode = newSort;
            RebuildVisibleEntries();
        }

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(64f)))
        {
            RefreshDatabase();
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSummary()
    {
        int warningCount = 0;
        int unassignedCount = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].warnings.Count > 0) warningCount++;
            if (entries[i].zones.Count == 0) unassignedCount++;
        }

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.LabelField(
            "Prefab: " + entries.Count +
            "    Visibili: " + visibleEntries.Count +
            "    Con avvisi: " + warningCount +
            "    Senza zona: " + unassignedCount,
            EditorStyles.miniLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }

    private static void DrawAssetRow(AssetEntry entry)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.MinHeight(96f));

        Texture preview = AssetPreview.GetAssetPreview(entry.prefab);
        if (preview == null)
        {
            preview = AssetPreview.GetMiniThumbnail(entry.prefab);
        }
        GUILayout.Label(preview, GUILayout.Width(80f), GUILayout.Height(80f));

        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(entry.prefab.name, EditorStyles.boldLabel);
        EditorGUILayout.LabelField(entry.path, EditorStyles.miniLabel);
        EditorGUILayout.LabelField(
            "Footprint: " + entry.footprint.x.ToString("F1") + " × " +
            entry.footprint.y.ToString("F1") + " m    Frontage: " +
            FormatDirection(entry.metadata.GetFrontageDirectionLocal()),
            EditorStyles.miniLabel);

        string zoneText = entry.zones.Count > 0
            ? string.Join(", ", entry.zones)
            : "Nessuna zona assegnata";
        EditorGUILayout.LabelField("Zone: " + zoneText, EditorStyles.miniLabel);

        if (entry.warnings.Count > 0)
        {
            Color previous = GUI.contentColor;
            GUI.contentColor = new Color(1f, 0.65f, 0.2f);
            EditorGUILayout.LabelField(
                "⚠ " + string.Join(" · ", entry.warnings),
                EditorStyles.miniLabel);
            GUI.contentColor = previous;
        }
        else
        {
            EditorGUILayout.LabelField("✓ Metadati validi", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.BeginVertical(GUILayout.Width(76f));
        if (GUILayout.Button("Seleziona"))
        {
            Selection.activeObject = entry.prefab;
            EditorGUIUtility.PingObject(entry.prefab);
        }
        if (GUILayout.Button("Apri"))
        {
            AssetDatabase.OpenAsset(entry.prefab);
        }
        if (GUILayout.Button("Cartella"))
        {
            UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(
                Path.GetDirectoryName(entry.path)?.Replace('\\', '/'));
            if (folder != null)
            {
                Selection.activeObject = folder;
                EditorGUIUtility.PingObject(folder);
            }
        }
        EditorGUILayout.EndVertical();

        EditorGUILayout.EndHorizontal();
    }

    private void RefreshDatabase()
    {
        needsRefresh = false;
        entries.Clear();

        List<ZoneType> zones = LoadZones();
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            CityBuilderPrefab metadata = prefab.GetComponent<CityBuilderPrefab>();
            if (metadata == null) continue;

            AssetEntry entry = new AssetEntry
            {
                prefab = prefab,
                metadata = metadata,
                path = path,
                footprint = metadata.GetLayoutFootprintSize()
            };

            for (int z = 0; z < zones.Count; z++)
            {
                if (zones[z] != null && zones[z].ContainsPrefab(prefab))
                {
                    entry.zones.Add(zones[z].GetDisplayName());
                }
            }
            entry.zones.Sort(StringComparer.OrdinalIgnoreCase);
            ValidateEntry(entry);
            entry.searchableText = (
                prefab.name + " " + path + " " + string.Join(" ", entry.zones) +
                " " + (metadata.aiDescription ?? string.Empty)).ToLowerInvariant();
            entries.Add(entry);
        }

        RebuildVisibleEntries();
        Repaint();
    }

    private void RebuildVisibleEntries()
    {
        visibleEntries.Clear();
        string query = (searchText ?? string.Empty).Trim().ToLowerInvariant();
        for (int i = 0; i < entries.Count; i++)
        {
            AssetEntry entry = entries[i];
            if (query.Length > 0 && !entry.searchableText.Contains(query)) continue;
            if (statusFilter == StatusFilter.Valid && entry.warnings.Count > 0) continue;
            if (statusFilter == StatusFilter.Warnings && entry.warnings.Count == 0) continue;
            visibleEntries.Add(entry);
        }

        visibleEntries.Sort(CompareEntries);
    }

    private int CompareEntries(AssetEntry left, AssetEntry right)
    {
        switch (sortMode)
        {
            case SortMode.Path:
                return string.Compare(left.path, right.path, StringComparison.OrdinalIgnoreCase);
            case SortMode.Footprint:
                float leftArea = left.footprint.x * left.footprint.y;
                float rightArea = right.footprint.x * right.footprint.y;
                int areaComparison = rightArea.CompareTo(leftArea);
                return areaComparison != 0
                    ? areaComparison
                    : string.Compare(left.prefab.name, right.prefab.name, StringComparison.OrdinalIgnoreCase);
            default:
                return string.Compare(left.prefab.name, right.prefab.name, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static void ValidateEntry(AssetEntry entry)
    {
        if (entry.footprint.x <= 0.1f || entry.footprint.y <= 0.1f)
        {
            entry.warnings.Add("footprint non valido");
        }
        if (!entry.metadata.TryCalculateLocalRendererBounds(out Bounds bounds))
        {
            entry.warnings.Add("Renderer mancanti");
            return;
        }
        if (Mathf.Abs(entry.metadata.pivotOffset.y - bounds.min.y) > 0.05f)
        {
            entry.warnings.Add("pivot non a terra");
        }

        Vector3 direction = entry.metadata.GetFrontageDirectionLocal();
        Vector3 delta = entry.metadata.frontageOffset - bounds.center;
        float expectedExtent =
            Mathf.Abs(direction.x) * bounds.extents.x +
            Mathf.Abs(direction.z) * bounds.extents.z;
        if (Mathf.Abs(Vector3.Dot(delta, direction)) + 0.1f < expectedExtent)
        {
            entry.warnings.Add("frontage interno");
        }
        if (entry.zones.Count == 0)
        {
            entry.warnings.Add("nessuna zona");
        }
    }

    private static List<ZoneType> LoadZones()
    {
        string[] guids = AssetDatabase.FindAssets("t:ZoneType");
        List<ZoneType> result = new List<ZoneType>(guids.Length);
        for (int i = 0; i < guids.Length; i++)
        {
            ZoneType zone = AssetDatabase.LoadAssetAtPath<ZoneType>(
                AssetDatabase.GUIDToAssetPath(guids[i]));
            if (zone != null) result.Add(zone);
        }
        return result;
    }

    private static string FormatDirection(Vector3 direction)
    {
        return "(" + direction.x.ToString("F2") + ", " +
               direction.z.ToString("F2") + ")";
    }
}
}
