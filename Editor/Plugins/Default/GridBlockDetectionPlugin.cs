using UnityEngine;
using System.Collections.Generic;

// Note: DetectBlocks returns the raw polygon list; warnings are logged directly.

/// <summary>
/// Esempio scheletro di un plugin di rilevamento blocchi alternativo.
/// Implementa una strategia semplificata basata su griglia uniforme invece del
/// rilevamento poligonale completo di DefaultBlockDetectionPlugin.
///
/// Scopo: template di partenza per plugin di terze parti / estensioni custom.
/// Attivare via Window/City Builder/Plugin Browser → categoria BlockDetection.
/// </summary>
[CityPlugin(
    "bsc.grid.block-detection",
    "Grid Block Detection (Demo)",
    CityPluginCategory.BlockDetection,
    "Rilevamento blocchi semplificato su griglia uniforme. Solo per dimostrazione del plugin system.")]
public class GridBlockDetectionPlugin : IBlockDetectionPlugin
{
    /// <summary>Dimensione cella griglia in unità mondo.</summary>
    public float cellSize = 80f;

    public List<List<Vector3>> DetectBlocks(CityGenerationContext ctx)
    {
        if (ctx.manager == null || ctx.cityData == null)
        {
            Debug.LogWarning("[GridBlockDetectionPlugin] Contesto non valido.");
            return new List<List<Vector3>>();
        }

        // Determina bounds dalla rete stradale
        var nodes = ctx.cityData.nodes;
        if (nodes == null || nodes.Count == 0)
        {
            Debug.LogWarning("[GridBlockDetectionPlugin] Nessun nodo stradale. Generare prima la rete.");
            return new List<List<Vector3>>();
        }

        float minX = float.MaxValue, minZ = float.MaxValue;
        float maxX = float.MinValue, maxZ = float.MinValue;
        foreach (var n in nodes)
        {
            if (n.position.x < minX) minX = n.position.x;
            if (n.position.z < minZ) minZ = n.position.z;
            if (n.position.x > maxX) maxX = n.position.x;
            if (n.position.z > maxZ) maxZ = n.position.z;
        }

        // Genera blocchi rettangolari uniformi
        var polygons = new List<List<Vector3>>();
        for (float x = minX; x < maxX - cellSize * 0.5f; x += cellSize)
        {
            for (float z = minZ; z < maxZ - cellSize * 0.5f; z += cellSize)
            {
                polygons.Add(new List<Vector3>
                {
                    new Vector3(x,           0f, z),
                    new Vector3(x + cellSize, 0f, z),
                    new Vector3(x + cellSize, 0f, z + cellSize),
                    new Vector3(x,           0f, z + cellSize),
                });
            }
        }

        Debug.Log($"[GridBlockDetectionPlugin] Rilevati {polygons.Count} blocchi su griglia {cellSize}u.");
        return polygons;
    }
}
