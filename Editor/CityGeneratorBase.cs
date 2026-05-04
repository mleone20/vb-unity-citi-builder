using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Classe base astratta per tutti i generatori procedurali di città.
/// </summary>
/// <remarks>
/// DEPRECATO: il pattern di ereditarietà da CityGeneratorBase è sostituito dal plugin system.
/// Implementare invece le interfacce in <c>CityPluginContracts.cs</c> e decorare
/// la classe con <see cref="CityPluginAttribute"/>.
/// Questa classe è mantenuta solo per non rompere eventuali script editor pre-esistenti.
/// </remarks>
[System.Obsolete("Estendere le interfacce plugin in CityPluginContracts invece di ereditare da CityGeneratorBase.")]
public abstract class CityGeneratorBase
{
    // ========== REPORT ==========

    /// <summary>
    /// Risultato di un'operazione di generazione.
    /// Può essere composto con i report di più passaggi (es. rete + zoning).
    /// </summary>
    public struct GenerationReport
    {
        public int nodesCreated;
        public int segmentsCreated;
        public int blocksZoned;
        public List<string> warnings;

        public string ToMultilineString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Nodi creati: {nodesCreated}");
            sb.AppendLine($"Segmenti creati: {segmentsCreated}");
            if (blocksZoned > 0)
                sb.AppendLine($"Blocchi re-zonati: {blocksZoned}");
            if (warnings != null && warnings.Count > 0)
            {
                sb.AppendLine($"Warning ({warnings.Count}):");
                foreach (string w in warnings)
                    sb.AppendLine($"  - {w}");
            }
            return sb.ToString().TrimEnd();
        }
    }

    // ========== ABSTRACT CONTRACT ==========

    /// <summary>
    /// Genera la rete stradale e aggiunge nodi/segmenti al CityManager.
    /// Non cancella dati preesistenti.
    /// </summary>
    public abstract GenerationReport GenerateRoadNetwork(CityManager manager);

    /// <summary>
    /// Assegna ZoneType e orientamento lotti ai blocchi esistenti in base
    /// alla logica di distanza/posizione specifica del generatore concreto.
    /// </summary>
    public abstract GenerationReport AssignZoningByDistance(CityManager manager);

    // ========== PROTECTED UTILITIES ==========

    /// <summary>
    /// Cerca un nodo esistente entro mergeThreshold dalla posizione.
    /// Se non ne trova nessuno, crea un nuovo nodo tramite CityManager.
    /// </summary>
    protected static CityNode GetOrCreateNode(
        CityManager manager, Vector3 position,
        float mergeThreshold, ref GenerationReport report)
    {
        CityNode existing = manager.FindNearestNode(position, mergeThreshold);
        if (existing != null) return existing;

        CityNode newNode = manager.AddNode(position);
        if (newNode != null) report.nodesCreated++;
        return newNode;
    }

    /// <summary>
    /// Applica un RoadProfile a un segmento (larghezza + riferimento profilo).
    /// No-op se segment o profile sono null.
    /// </summary>
    protected static void ApplyProfile(CitySegment segment, RoadProfile profile)
    {
        if (segment == null || profile == null) return;
        segment.roadProfile = profile;
        segment.width = profile.roadWidth;
    }
}
