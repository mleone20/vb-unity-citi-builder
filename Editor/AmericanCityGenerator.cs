using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Generatore procedurale di rete stradale in stile americano.
/// Estende CityGeneratorBase per implementare il contratto astratto
/// (GenerateRoadNetwork / AssignZoningByDistance) con la logica specifica
/// della città americana (griglie radiali + griglia principale + strade locali).
///
/// Genera tre livelli di infrastruttura:
///   1. Autostrade radiali (highway arms) da P0
///   2. Griglia principale (Major Grid) a spaziatura configurabile
///   3. Strade locali (Local Streets) all'interno di ogni cella, fino a localStreetMaxRadius
/// </summary>
public class AmericanCityGenerator : CityGeneratorBase
{
    private readonly AmericanCityConfig config;

    public AmericanCityGenerator(AmericanCityConfig config)
    {
        this.config = config;
    }

    // ========== ENTRY POINTS ==========

    /// <summary>
    /// Genera la rete stradale completa (autostrade + griglia + strade locali).
    /// Aggiunge nodi e segmenti alla rete esistente senza cancellare dati presenti.
    /// </summary>
    public override GenerationReport GenerateRoadNetwork(CityManager manager)
    {
        var report = new GenerationReport { warnings = new List<string>() };

        if (manager == null || config == null)
        {
            Debug.LogError("[AmericanCityGenerator] CityManager o AmericanCityConfig non assegnati.");
            return report;
        }

        CityData cityData = manager.GetCityData();
        if (cityData == null)
        {
            Debug.LogError("[AmericanCityGenerator] CityData null nel CityManager.");
            return report;
        }

        Vector3 p0 = config.centerWorldPosition;
        float capRadius = config.maxGenerationRadius;
        float merge = Mathf.Max(0.1f, config.mergeThreshold);

        // 1. Nodo centrale (P0)
        CityNode centerNode = GetOrCreateNode(manager, p0, merge, ref report);

        // 2. Autostrade radiali
        GenerateHighways(manager, centerNode, p0, capRadius, merge, ref report);

        // 3. Griglia principale
        GenerateMajorGrid(manager, p0, capRadius, merge, ref report);

        // 4. Strade locali (solo dentro min(capRadius, localStreetMaxRadius))
        float localCap = Mathf.Min(capRadius, config.localStreetMaxRadius);
        bool localStreetEnabled =
            localCap > 0f &&
            config.localStreetSpacing > 0f &&
            config.localStreetSpacing < config.majorGridSpacing * 0.95f;

        if (localStreetEnabled)
        {
            GenerateLocalStreets(manager, p0, localCap, merge, ref report);
        }

        EditorUtility.SetDirty(cityData);
        SceneView.RepaintAll();

        Debug.Log($"[AmericanCityGenerator] Rete generata: {report.nodesCreated} nodi, {report.segmentsCreated} segmenti.");
        return report;
    }

    /// <summary>
    /// Assegna il ZoneType ai blocchi esistenti in base alla distanza da P0.
    /// Imposta anche l'orientamento lotti coerente con la densità zonale.
    /// </summary>
    public override GenerationReport AssignZoningByDistance(CityManager manager)
    {
        var report = new GenerationReport { warnings = new List<string>() };

        if (manager == null || config == null)
        {
            Debug.LogError("[AmericanCityGenerator] CityManager o AmericanCityConfig non assegnati.");
            return report;
        }

        CityData cityData = manager.GetCityData();
        if (cityData == null) return report;

        Undo.RecordObject(cityData, "Assign Zoning By Distance");

        Vector3 p0 = config.centerWorldPosition;

        foreach (CityBlock block in cityData.blocks)
        {
            if (block == null) continue;

            Vector3 center = block.GetCenter();
            float dist = Mathf.Sqrt(
                (center.x - p0.x) * (center.x - p0.x) +
                (center.z - p0.z) * (center.z - p0.z));

            ZoneType zone = config.GetZoneTypeForDistance(dist);
            if (zone == null)
            {
                report.warnings.Add($"Block {block.id}: nessuna zona mappata per dist={dist:F0}m");
                continue;
            }

            manager.SetBlockZoning(block.id, zone);
            block.orientation = config.GetOrientationForDistance(dist);
            report.blocksZoned++;
        }

        EditorUtility.SetDirty(cityData);
        SceneView.RepaintAll();

        Debug.Log($"[AmericanCityGenerator] Zoning per distanza: {report.blocksZoned} blocchi.");
        return report;
    }

    // ========== HIGHWAY GENERATION ==========

    private void GenerateHighways(
        CityManager manager, CityNode centerNode, Vector3 p0,
        float capRadius, float merge, ref GenerationReport report)
    {
        int hwCount = Mathf.Clamp(config.highwayCount, 1, 4);
        float step = Mathf.Max(50f, config.majorGridSpacing);
        RoadProfile profile = config.highwayProfile;

        // Ogni autostrada genera 2 bracci opposti distribuiti uniformemente su 180°
        for (int i = 0; i < hwCount; i++)
        {
            float angleDeg = i * (180f / hwCount);
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 dirA = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
            Vector3 dirB = -dirA;

            GenerateHighwayArm(manager, centerNode, p0, dirA, step, capRadius, merge, profile, ref report);
            GenerateHighwayArm(manager, centerNode, p0, dirB, step, capRadius, merge, profile, ref report);
        }
    }

    private void GenerateHighwayArm(
        CityManager manager, CityNode centerNode, Vector3 p0,
        Vector3 direction, float step, float capRadius,
        float merge, RoadProfile profile, ref GenerationReport report)
    {
        CityNode prevNode = centerNode;
        float dist = step;

        while (dist <= capRadius + step * 0.01f)
        {
            Vector3 pos = p0 + direction * dist;
            CityNode node = GetOrCreateNode(manager, pos, merge, ref report);
            if (node == null) break;

            CitySegment seg = manager.AddSegment(prevNode.id, node.id);
            if (seg != null)
            {
                ApplyProfile(seg, profile);
                report.segmentsCreated++;
            }

            prevNode = node;
            dist += step;
        }
    }

    // ========== MAJOR GRID GENERATION ==========

    private void GenerateMajorGrid(
        CityManager manager, Vector3 p0, float capRadius,
        float merge, ref GenerationReport report)
    {
        float spacing = Mathf.Max(50f, config.majorGridSpacing);
        RoadProfile profile = config.majorGridProfile;
        int halfSteps = Mathf.CeilToInt(capRadius / spacing);

        var gridNodes = new Dictionary<(int, int), CityNode>();

        for (int ix = -halfSteps; ix <= halfSteps; ix++)
        {
            for (int iz = -halfSteps; iz <= halfSteps; iz++)
            {
                float x = p0.x + ix * spacing;
                float z = p0.z + iz * spacing;
                float dx = x - p0.x;
                float dz = z - p0.z;

                if (dx * dx + dz * dz > capRadius * capRadius) continue;

                Vector3 pos = new Vector3(x, p0.y, z);
                CityNode node = GetOrCreateNode(manager, pos, merge, ref report);
                if (node != null)
                    gridNodes[(ix, iz)] = node;
            }
        }

        // Connessioni orizzontali (asse X)
        for (int iz = -halfSteps; iz <= halfSteps; iz++)
        {
            for (int ix = -halfSteps; ix < halfSteps; ix++)
            {
                if (!gridNodes.TryGetValue((ix, iz), out CityNode a)) continue;
                if (!gridNodes.TryGetValue((ix + 1, iz), out CityNode b)) continue;

                CitySegment seg = manager.AddSegment(a.id, b.id);
                if (seg != null) { ApplyProfile(seg, profile); report.segmentsCreated++; }
            }
        }

        // Connessioni verticali (asse Z)
        for (int ix = -halfSteps; ix <= halfSteps; ix++)
        {
            for (int iz = -halfSteps; iz < halfSteps; iz++)
            {
                if (!gridNodes.TryGetValue((ix, iz), out CityNode a)) continue;
                if (!gridNodes.TryGetValue((ix, iz + 1), out CityNode b)) continue;

                CitySegment seg = manager.AddSegment(a.id, b.id);
                if (seg != null) { ApplyProfile(seg, profile); report.segmentsCreated++; }
            }
        }
    }

    // ========== LOCAL STREETS GENERATION ==========

    private void GenerateLocalStreets(
        CityManager manager, Vector3 p0, float localCap,
        float merge, ref GenerationReport report)
    {
        float majorSpacing = Mathf.Max(50f, config.majorGridSpacing);
        float localSpacing = Mathf.Max(20f, config.localStreetSpacing);
        RoadProfile profile = config.localStreetProfile;

        int halfMajorSteps = Mathf.CeilToInt(localCap / majorSpacing);

        for (int cx = -halfMajorSteps; cx < halfMajorSteps; cx++)
        {
            for (int cz = -halfMajorSteps; cz < halfMajorSteps; cz++)
            {
                float ccx = p0.x + (cx + 0.5f) * majorSpacing;
                float ccz = p0.z + (cz + 0.5f) * majorSpacing;
                float cdx = ccx - p0.x;
                float cdz = ccz - p0.z;
                if (cdx * cdx + cdz * cdz > localCap * localCap) continue;

                float xMin = p0.x + cx * majorSpacing;
                float zMin = p0.z + cz * majorSpacing;

                int stepsX = Mathf.Max(2, Mathf.RoundToInt(majorSpacing / localSpacing));
                int stepsZ = Mathf.Max(2, Mathf.RoundToInt(majorSpacing / localSpacing));

                if (stepsX < 2 && stepsZ < 2) continue;

                float subX = majorSpacing / stepsX;
                float subZ = majorSpacing / stepsZ;

                var localNodes = new Dictionary<(int, int), CityNode>();

                for (int lx = 0; lx <= stepsX; lx++)
                {
                    for (int lz = 0; lz <= stepsZ; lz++)
                    {
                        Vector3 pos = new Vector3(xMin + lx * subX, p0.y, zMin + lz * subZ);
                        CityNode node = GetOrCreateNode(manager, pos, merge, ref report);
                        if (node != null)
                            localNodes[(lx, lz)] = node;
                    }
                }

                // Strade orizzontali interne
                for (int lz = 1; lz < stepsZ; lz++)
                {
                    for (int lx = 0; lx < stepsX; lx++)
                    {
                        if (!localNodes.TryGetValue((lx, lz), out CityNode a)) continue;
                        if (!localNodes.TryGetValue((lx + 1, lz), out CityNode b)) continue;
                        if (a.id == b.id) continue;

                        CitySegment seg = manager.AddSegment(a.id, b.id);
                        if (seg != null) { ApplyProfile(seg, profile); report.segmentsCreated++; }
                    }
                }

                // Strade verticali interne
                for (int lx = 1; lx < stepsX; lx++)
                {
                    for (int lz = 0; lz < stepsZ; lz++)
                    {
                        if (!localNodes.TryGetValue((lx, lz), out CityNode a)) continue;
                        if (!localNodes.TryGetValue((lx, lz + 1), out CityNode b)) continue;
                        if (a.id == b.id) continue;

                        CitySegment seg = manager.AddSegment(a.id, b.id);
                        if (seg != null) { ApplyProfile(seg, profile); report.segmentsCreated++; }
                    }
                }
            }
        }
    }
}
