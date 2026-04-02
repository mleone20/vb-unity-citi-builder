using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Editor script per assegnazione zoning ai blocchi.
/// Fornisce UI per visualizzare e modificare il zoning.
/// </summary>
public static class CityZoningEditor
{
    private static int selectedBlockIDForZoning = -1;

    public static void DrawZoningEditorUI(CityManager manager)
    {
        CityData cityData = manager.GetCityData();
        if (cityData == null) return;

        EditorGUILayout.LabelField("ZONING - Assegnazione Destinazioni", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (cityData.blocks.Count == 0)
        {
            EditorGUILayout.HelpBox("Nessun blocco disponibile. Crea blocchi prima.", MessageType.Info);
            return;
        }

        // Selettore blocco
        EditorGUILayout.LabelField("Seleziona Blocco:");
        
        string[] blockLabels = new string[cityData.blocks.Count];
        int currentBlockIndex = -1;

        for (int i = 0; i < cityData.blocks.Count; i++)
        {
            CityBlock block = cityData.blocks[i];
            blockLabels[i] = $"Block {block.id} - {block.zoning}";
            
            if (block.id == selectedBlockIDForZoning)
                currentBlockIndex = i;
        }

        int newBlockIndex = EditorGUILayout.Popup(currentBlockIndex >= 0 ? currentBlockIndex : 0, blockLabels);
        
        if (newBlockIndex >= 0)
        {
            selectedBlockIDForZoning = cityData.blocks[newBlockIndex].id;
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

                // Selettore zoning
                EditorGUILayout.LabelField("Destinazione d'uso:", EditorStyles.label);

                ZoneType currentZoning = selectedBlock.zoning;
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Residenziale (Verde)"))
                {
                    manager.SetBlockZoning(selectedBlock.id, ZoneType.Residential);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Commerciale (Blu)"))
                {
                    manager.SetBlockZoning(selectedBlock.id, ZoneType.Commercial);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Industriale (Giallo)"))
                {
                    manager.SetBlockZoning(selectedBlock.id, ZoneType.Industrial);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Speciale (Grigio)"))
                {
                    manager.SetBlockZoning(selectedBlock.id, ZoneType.Special);
                }
                EditorGUILayout.EndHorizontal();

                EditorGUILayout.Space();

                EditorGUILayout.LabelField($"Zoning attuale: {selectedBlock.zoning}", EditorStyles.label);
                
                Color zoningColor = cityData.GetZoneColor(selectedBlock.zoning);
                EditorGUILayout.ColorField("Colore zona:", zoningColor);

                EditorGUILayout.Space();
                EditorGUILayout.LabelField($"Lotti in questo blocco: {selectedBlock.lotIDs.Count}");
            }
        }
    }

    public static void DrawZoningStats(CityData cityData)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Statistiche Zoning:", EditorStyles.boldLabel);

        int resCount = 0, comCount = 0, indCount = 0, specCount = 0;

        foreach (var block in cityData.blocks)
        {
            switch (block.zoning)
            {
                case ZoneType.Residential: resCount++; break;
                case ZoneType.Commercial: comCount++; break;
                case ZoneType.Industrial: indCount++; break;
                case ZoneType.Special: specCount++; break;
            }
        }

        EditorGUILayout.LabelField($"  Residenziali: {resCount}", EditorStyles.label);
        EditorGUILayout.LabelField($"  Commerciali: {comCount}", EditorStyles.label);
        EditorGUILayout.LabelField($"  Industriali: {indCount}", EditorStyles.label);
        EditorGUILayout.LabelField($"  Speciali: {specCount}", EditorStyles.label);
    }
}
