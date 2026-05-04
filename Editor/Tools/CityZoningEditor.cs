using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Editor script per assegnazione zoning ai blocchi.
/// Fornisce UI per visualizzare e modificare il zoning.
/// </summary>
public static class CityZoningEditor
{
    public static int GetSelectedBlockIDForZoning(CityManager manager)
    {
        return manager != null ? manager.GetSelectedBlockID() : -1;
    }

    public static void SetSelectedBlockForZoning(CityManager manager, int blockID)
    {
        if (manager != null)
        {
            manager.SetSelectedBlockID(blockID);
        }
    }

    public static void DrawZoningEditorUI(CityManager manager)
    {
        CityData cityData = manager.GetCityData();
        if (cityData == null) return;

        EditorGUILayout.LabelField("ZONING - Assegnazione Destinazioni", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        List<ZoneType> zoneTypes = ZoneTypeEditorUtility.LoadAllZoneTypes();
        if (zoneTypes.Count == 0)
        {
            EditorGUILayout.HelpBox("Nessun ZoneType asset trovato. Crea almeno un asset ZoneType da Assets/Create/City Builder/Zone Type.", MessageType.Warning);
            return;
        }

        if (cityData.blocks.Count == 0)
        {
            EditorGUILayout.HelpBox("Nessun blocco disponibile. Crea blocchi prima.", MessageType.Info);
            return;
        }

        int selectedBlockIDForZoning = manager.GetSelectedBlockID();
        if (selectedBlockIDForZoning < 0 || cityData.GetBlock(selectedBlockIDForZoning) == null)
        {
            selectedBlockIDForZoning = cityData.blocks[0].id;
            manager.SetSelectedBlockID(selectedBlockIDForZoning);
        }

        // Selettore blocco
        EditorGUILayout.LabelField("Seleziona Blocco:");
        
        string[] blockLabels = new string[cityData.blocks.Count];
        int currentBlockIndex = -1;

        for (int i = 0; i < cityData.blocks.Count; i++)
        {
            CityBlock block = cityData.blocks[i];
            blockLabels[i] = $"Block {block.id} - {ZoneTypeEditorUtility.GetZoneDisplayName(block.zoning)}";
            
            if (block.id == selectedBlockIDForZoning)
                currentBlockIndex = i;
        }

        int newBlockIndex = EditorGUILayout.Popup(currentBlockIndex >= 0 ? currentBlockIndex : 0, blockLabels);
        
        if (newBlockIndex >= 0)
        {
            selectedBlockIDForZoning = cityData.blocks[newBlockIndex].id;
            manager.SetSelectedBlockID(selectedBlockIDForZoning);
        }

        EditorGUILayout.Space();

        // Mostra dettagli blocco selezionato
        if (selectedBlockIDForZoning >= 0)
        {
            CityBlock selectedBlock = cityData.GetBlock(selectedBlockIDForZoning);
            
            if (selectedBlock != null)
            {
                GUILayout.Box($"Block {selectedBlock.id}\nVertici: {selectedBlock.vertices.Count}\nArea: {selectedBlock.GetArea():F2}\nPerimetro: {selectedBlock.GetPerimeter():F2}", 
                    GUILayout.Height(60));

                EditorGUILayout.Space();

                // Modalità spawn lotti
                EditorGUILayout.LabelField("Modalità Lotti:", EditorStyles.label);
                BlockOrientation newOrientation = (BlockOrientation)EditorGUILayout.EnumPopup(selectedBlock.orientation);
                if (newOrientation != selectedBlock.orientation)
                {
                    Undo.RecordObject(cityData, "Set Block Orientation");
                    selectedBlock.orientation = newOrientation;
                    EditorUtility.SetDirty(cityData);
                }

                EditorGUILayout.Space();

                // Gap override per blocco
                bool hasGapOverride = selectedBlock.lotGapOverride >= 0f;
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Gap personalizzato:", GUILayout.Width(130f));
                bool newHasGapOverride = EditorGUILayout.Toggle(hasGapOverride, GUILayout.Width(18f));
                if (newHasGapOverride != hasGapOverride)
                {
                    Undo.RecordObject(cityData, "Toggle Block Lot Gap");
                    selectedBlock.lotGapOverride = newHasGapOverride ? Mathf.Max(cityData.gapMinimum, 0.5f) : -1f;
                    EditorUtility.SetDirty(cityData);
                    hasGapOverride = newHasGapOverride;
                }
                using (new EditorGUI.DisabledScope(!hasGapOverride))
                {
                    float displayGap = hasGapOverride ? selectedBlock.lotGapOverride : cityData.gapMinimum;
                    float newGap = EditorGUILayout.FloatField(displayGap);
                    newGap = Mathf.Max(0f, newGap);
                    if (hasGapOverride && !Mathf.Approximately(newGap, selectedBlock.lotGapOverride))
                    {
                        Undo.RecordObject(cityData, "Set Block Lot Gap");
                        selectedBlock.lotGapOverride = newGap;
                        EditorUtility.SetDirty(cityData);
                    }
                }
                EditorGUILayout.EndHorizontal();
                if (!hasGapOverride)
                    EditorGUILayout.LabelField($"  (usa globale: {cityData.gapMinimum:F2} – {cityData.gapMaximum:F2})", EditorStyles.miniLabel);

                EditorGUILayout.Space();

                // Selettore zoning
                EditorGUILayout.LabelField("Destinazione d'uso:", EditorStyles.label);

                
                using (new EditorGUI.DisabledScope(selectedBlock.zoning == null))
                {
                    if (GUILayout.Button("Seleziona Asset Zona"))
                    {
                        Selection.activeObject = selectedBlock.zoning;
                        EditorUtility.FocusProjectWindow();
                    }
                }

                EditorGUILayout.Space();

                int selectedZoneIndex = Mathf.Max(0, zoneTypes.IndexOf(selectedBlock.zoning));
                string[] zoneLabels = zoneTypes.Select(z => ZoneTypeEditorUtility.GetZoneDisplayName(z)).ToArray();
                int newZoneIndex = EditorGUILayout.Popup("Zone Type", selectedZoneIndex, zoneLabels);
                ZoneType newZone = zoneTypes[newZoneIndex];

                if (newZone != selectedBlock.zoning)
                {
                    manager.SetBlockZoning(selectedBlock.id, newZone);
                }

                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Assegnazione Rapida:", EditorStyles.label);
                foreach (ZoneType zoneType in zoneTypes)
                {
                    Color previousColor = GUI.color;
                    GUI.color = zoneType.zoneColor;
                    if (GUILayout.Button(ZoneTypeEditorUtility.GetZoneDisplayName(zoneType)))
                    {
                        manager.SetBlockZoning(selectedBlock.id, zoneType);
                    }
                    GUI.color = previousColor;
                }

                EditorGUILayout.Space();

                EditorGUILayout.LabelField($"Lotti in questo blocco: {selectedBlock.lotIDs.Count}");
            }
        }
    }

    public static void DrawZoningStats(CityData cityData)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Statistiche Zoning:", EditorStyles.boldLabel);

        Dictionary<string, int> counts = new Dictionary<string, int>();

        foreach (var block in cityData.blocks)
        {
            string label = ZoneTypeEditorUtility.GetZoneDisplayName(block.zoning);
            if (!counts.ContainsKey(label))
            {
                counts[label] = 0;
            }
            counts[label]++;
        }

        foreach (KeyValuePair<string, int> pair in counts.OrderBy(k => k.Key))
        {
            EditorGUILayout.LabelField($"  {pair.Key}: {pair.Value}", EditorStyles.label);
        }
    }
}
