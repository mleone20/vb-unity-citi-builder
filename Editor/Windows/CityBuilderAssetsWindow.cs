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

    private enum ViewMode
    {
        List,
        Grid
    }

    private sealed class AssetEntry
    {
        public GameObject prefab;
        public CityBuilderPrefab metadata;
        public string path;
        public string searchableText;
        public Vector2 footprint;
        public List<string> zones = new List<string>();
        public List<string> zoneTags = new List<string>();
        public List<string> warnings = new List<string>();

        public bool HasZoneData => zones.Count > 0 || zoneTags.Count > 0;
    }

    private readonly List<AssetEntry> entries = new List<AssetEntry>();
    private readonly List<AssetEntry> visibleEntries = new List<AssetEntry>();
    private readonly List<string> tagFilterOptions = new List<string>();
    private Vector2 scrollPosition;
    private string searchText = string.Empty;
    private StatusFilter statusFilter;
    private SortMode sortMode;
    private ViewMode viewMode;
    private int selectedTagFilter;
    private string selectedTag = string.Empty;
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
        if (viewMode == ViewMode.Grid)
        {
            DrawAssetGrid();
        }
        else
        {
            for (int i = 0; i < visibleEntries.Count; i++)
            {
                DrawAssetRow(visibleEntries[i]);
            }
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

        int newTagFilter = EditorGUILayout.Popup(
            selectedTagFilter,
            tagFilterOptions.ToArray(),
            EditorStyles.toolbarPopup,
            GUILayout.Width(130f));
        if (newTagFilter != selectedTagFilter &&
            newTagFilter >= 0 && newTagFilter < tagFilterOptions.Count)
        {
            selectedTagFilter = newTagFilter;
            selectedTag = tagFilterOptions[selectedTagFilter];
            RebuildVisibleEntries();
        }

        ViewMode newViewMode = (ViewMode)EditorGUILayout.EnumPopup(
            viewMode, EditorStyles.toolbarPopup, GUILayout.Width(70f));
        if (newViewMode != viewMode)
        {
            viewMode = newViewMode;
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
            if (!entries[i].HasZoneData) unassignedCount++;
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

        string tagText = entry.zoneTags.Count > 0
            ? string.Join(", ", entry.zoneTags)
            : "Nessuno";
        EditorGUILayout.LabelField("Zone tags: " + tagText, EditorStyles.miniLabel);

        string zoneText = entry.zones.Count > 0
            ? string.Join(", ", entry.zones)
            : "Nessun riferimento diretto";
        EditorGUILayout.LabelField("Usato dai ZoneType: " + zoneText, EditorStyles.miniLabel);

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

    private void DrawAssetGrid()
    {
        const float cardWidth = 220f;
        float availableWidth = Mathf.Max(cardWidth, position.width - 28f);
        int columns = Mathf.Max(1, Mathf.FloorToInt(availableWidth / cardWidth));

        for (int start = 0; start < visibleEntries.Count; start += columns)
        {
            EditorGUILayout.BeginHorizontal();
            for (int column = 0; column < columns; column++)
            {
                int index = start + column;
                if (index < visibleEntries.Count)
                {
                    DrawAssetCard(visibleEntries[index], cardWidth - 8f);
                }
                else
                {
                    GUILayout.Space(cardWidth - 8f);
                }
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }
    }

    private static void DrawAssetCard(AssetEntry entry, float cardWidth)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Width(cardWidth));

        Texture preview = AssetPreview.GetAssetPreview(entry.prefab);
        if (preview == null) preview = AssetPreview.GetMiniThumbnail(entry.prefab);
        Rect previewRect = GUILayoutUtility.GetRect(
            cardWidth - 12f, 128f, GUILayout.ExpandWidth(true));
        if (preview != null)
        {
            GUI.DrawTexture(previewRect, preview, ScaleMode.ScaleToFit);
        }

        EditorGUILayout.LabelField(entry.prefab.name, EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            entry.footprint.x.ToString("F1") + " × " +
            entry.footprint.y.ToString("F1") + " m",
            EditorStyles.miniLabel);

        string tags = entry.zoneTags.Count > 0
            ? string.Join(", ", entry.zoneTags)
            : "Nessun tag";
        EditorGUILayout.LabelField(
            new GUIContent("Tags: " + tags, tags),
            EditorStyles.miniLabel,
            GUILayout.Height(18f));

        Color previous = GUI.contentColor;
        GUI.contentColor = entry.warnings.Count > 0
            ? new Color(1f, 0.65f, 0.2f)
            : new Color(0.45f, 0.85f, 0.5f);
        EditorGUILayout.LabelField(
            entry.warnings.Count > 0
                ? "⚠ " + entry.warnings.Count + " avvisi"
                : "✓ Valido",
            EditorStyles.miniLabel);
        GUI.contentColor = previous;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Seleziona"))
        {
            Selection.activeObject = entry.prefab;
            EditorGUIUtility.PingObject(entry.prefab);
        }
        if (GUILayout.Button("Apri"))
        {
            AssetDatabase.OpenAsset(entry.prefab);
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.EndVertical();
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
            if (metadata.zoneTypeTags != null)
            {
                var seenTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                for (int tagIndex = 0; tagIndex < metadata.zoneTypeTags.Count; tagIndex++)
                {
                    string tag = metadata.zoneTypeTags[tagIndex];
                    if (string.IsNullOrWhiteSpace(tag)) continue;
                    string normalized = tag.Trim();
                    if (seenTags.Add(normalized)) entry.zoneTags.Add(normalized);
                }
                entry.zoneTags.Sort(StringComparer.OrdinalIgnoreCase);
            }
            ValidateEntry(entry);
            entry.searchableText = (
                prefab.name + " " + path + " " + string.Join(" ", entry.zones) +
                " " + (metadata.description ?? string.Empty) +
                " " + string.Join(" ", entry.zoneTags)).ToLowerInvariant();
            entries.Add(entry);
        }

        RebuildVisibleEntries();
        RebuildTagFilterOptions();
        Repaint();
    }

    private void RebuildTagFilterOptions()
    {
        string previousSelection = selectedTag;
        var tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < entries.Count; i++)
        {
            for (int t = 0; t < entries[i].zoneTags.Count; t++)
            {
                tags.Add(entries[i].zoneTags[t]);
            }
        }

        var sortedTags = new List<string>(tags);
        sortedTags.Sort(StringComparer.OrdinalIgnoreCase);
        tagFilterOptions.Clear();
        tagFilterOptions.Add("Tutti i tag");
        tagFilterOptions.Add("Senza tag");
        tagFilterOptions.AddRange(sortedTags);

        selectedTagFilter = tagFilterOptions.FindIndex(
            option => string.Equals(
                option, previousSelection, StringComparison.OrdinalIgnoreCase));
        if (selectedTagFilter < 0) selectedTagFilter = 0;
        selectedTag = tagFilterOptions[selectedTagFilter];
        RebuildVisibleEntries();
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
            if (selectedTagFilter == 1 && entry.zoneTags.Count > 0) continue;
            if (selectedTagFilter >= 2 && !ContainsTag(entry.zoneTags, selectedTag)) continue;
            visibleEntries.Add(entry);
        }

        visibleEntries.Sort(CompareEntries);
    }

    private static bool ContainsTag(List<string> tags, string expected)
    {
        for (int i = 0; i < tags.Count; i++)
        {
            if (string.Equals(tags[i], expected, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
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
        float expectedGround = entry.metadata.TryCalculateWallBaseInEditor(out float wallBase)
            ? wallBase
            : bounds.min.y;
        if (Mathf.Abs(entry.metadata.pivotOffset.y - expectedGround) > 0.05f)
        {
            entry.warnings.Add("pivot non alla base pareti");
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
        if (!entry.HasZoneData)
        {
            entry.warnings.Add("nessun tag zona");
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
