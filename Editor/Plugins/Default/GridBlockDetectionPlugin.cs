using UnityEngine;
using System.Collections.Generic;

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

    public CityGenerationReport DetectBlocks(CityGenerationContext ctx)
    {
        var report = new CityGenerationReport { warnings = new List<string>() };

        if (ctx.manager == null || ctx.cityData == null)
        {
            report.warnings.Add("[GridBlockDetectionPlugin] Contesto non valido.");
            return report;
        }

        // Pulisce i blocchi precedenti
        ctx.cityData.blocks.Clear();

        // Determina bounds dalla rete stradale
        var nodes = ctx.cityData.nodes;
        if (nodes == null || nodes.Count == 0)
        {
            report.warnings.Add("[GridBlockDetectionPlugin] Nessun nodo stradale. Generare prima la rete.");
            return report;
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
        int created = 0;
        for (float x = minX; x < maxX - cellSize * 0.5f; x += cellSize)
        {
            for (float z = minZ; z < maxZ - cellSize * 0.5f; z += cellSize)
            {
                var block = new CityBlock
                {
                    polygon = new List<Vector3>
                    {
                        new Vector3(x,           0f, z),
                        new Vector3(x + cellSize, 0f, z),
                        new Vector3(x + cellSize, 0f, z + cellSize),
                        new Vector3(x,           0f, z + cellSize),
                    },
                    orientation = 0f
                };
                ctx.cityData.blocks.Add(block);
                created++;
            }
        }

        report.blocksDetected = created;
        Debug.Log($"[GridBlockDetectionPlugin] Rilevati {created} blocchi su griglia {cellSize}u.");
        return report;
    }
}
