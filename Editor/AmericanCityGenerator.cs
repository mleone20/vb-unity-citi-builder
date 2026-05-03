using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Generatore procedurale di rete stradale in stile americano.
/// </summary>
public class AmericanCityGenerator : CityGeneratorBase
{
    private readonly AmericanCityConfig config;

    public AmericanCityGenerator(AmericanCityConfig config)
    {
        this.config = config;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ENTRY POINTS
    // ──────────────────────────────────────────────────────────────────────────

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

        GenerateRoadNetworkGrid(manager, ref report);

        // Planarizzazione: risolve gli incroci geometrici tra segmenti
        float merge = Mathf.Max(0.1f, config.mergeThreshold);
        int splitsDone = CityRoadPlanarizer.Planarize(manager, merge);
        if (splitsDone > 0)
            report.warnings.Add($"{splitsDone} segmenti planarizzati (incroci risolti).");

        EditorUtility.SetDirty(cityData);
        SceneView.RepaintAll();

        Debug.Log($"[AmericanCityGenerator] Rete generata: {report.nodesCreated} nodi, {report.segmentsCreated} segmenti.");
        return report;
    }

    public IEnumerator GenerateRoadNetworkAsync(
        CityManager manager,
        Action<float, string> onProgress,
        Action<GenerationReport> onCompleted = null)
    {
        var report = new GenerationReport { warnings = new List<string>() };

        if (manager == null || config == null)
        {
            Debug.LogError("[AmericanCityGenerator] CityManager o AmericanCityConfig non assegnati.");
            onCompleted?.Invoke(report);
            yield break;
        }

        CityData cityData = manager.GetCityData();
        if (cityData == null)
        {
            Debug.LogError("[AmericanCityGenerator] CityData null nel CityManager.");
            onCompleted?.Invoke(report);
            yield break;
        }

        onProgress?.Invoke(0.02f, "Preparazione generazione...");

        onProgress?.Invoke(0.15f, "Generazione rete griglia...");
        GenerateRoadNetworkGrid(manager, ref report);
        yield return null;

        onProgress?.Invoke(0.92f, "Planarizzazione incroci...");
        float mergePlanarize = Mathf.Max(0.1f, config.mergeThreshold);
        int splitsDone = 0;
        yield return CityRoadPlanarizer.PlanarizeAsync(
            manager,
            mergePlanarize,
            (p, msg) => onProgress?.Invoke(Mathf.Lerp(0.92f, 0.99f, p), msg),
            done => splitsDone = done);
        if (splitsDone > 0)
            report.warnings.Add($"{splitsDone} segmenti planarizzati (incroci risolti).");

        EditorUtility.SetDirty(cityData);
        SceneView.RepaintAll();

        onProgress?.Invoke(1f, "Generazione completata");
        Debug.Log($"[AmericanCityGenerator] Rete generata: {report.nodesCreated} nodi, {report.segmentsCreated} segmenti.");
        onCompleted?.Invoke(report);
    }

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
  

    // ──────────────────────────────────────────────────────────────────────────
    // MODALITÀ GRID
    // ──────────────────────────────────────────────────────────────────────────

    private void GenerateRoadNetworkGrid(CityManager manager, ref GenerationReport report)
    {
        Vector3 p0      = config.centerWorldPosition;
        float capRadius = config.maxGenerationRadius;
        float merge     = Mathf.Max(0.1f, config.mergeThreshold);

        CityNode centerNode = GetOrCreateNode(manager, p0, merge, ref report);
        GenerateHighways(manager, centerNode, p0, capRadius, merge, ref report);
        GenerateMajorGrid(manager, p0, capRadius, merge, ref report);

        float localCap = Mathf.Min(capRadius, config.localStreetMaxRadius);
        bool localEnabled = localCap > 0f &&
            config.localStreetSpacing > 0f &&
            config.localStreetSpacing < config.majorGridSpacing * 0.95f;

        if (localEnabled)
            GenerateLocalStreets(manager, p0, localCap, merge, ref report);
    }


    private void GenerateHighways(
        CityManager manager, CityNode centerNode, Vector3 p0,
        float capRadius, float merge, ref GenerationReport report)
    {
        int hwCount = Mathf.Clamp(config.highwayCount, 1, 4);
        float step  = Mathf.Max(50f, config.majorGridSpacing);
        RoadProfile profile = config.highwayProfile;

        for (int i = 0; i < hwCount; i++)
        {
            float angleDeg = i * (180f / hwCount);
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 dirA = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
            GenerateHighwayArm(manager, centerNode, p0, dirA,  step, capRadius, merge, profile, ref report);
            GenerateHighwayArm(manager, centerNode, p0, -dirA, step, capRadius, merge, profile, ref report);
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
            if (seg != null) { ApplyProfile(seg, profile); report.segmentsCreated++; }
            prevNode = node;
            dist += step;
        }
    }

    private void GenerateMajorGrid(
        CityManager manager, Vector3 p0, float capRadius,
        float merge, ref GenerationReport report)
    {
        float spacing   = Mathf.Max(50f, config.majorGridSpacing);
        RoadProfile profile = config.majorGridProfile;
        int halfSteps   = Mathf.CeilToInt(capRadius / spacing);
        var gridNodes   = new Dictionary<(int, int), CityNode>();

        for (int ix = -halfSteps; ix <= halfSteps; ix++)
            for (int iz = -halfSteps; iz <= halfSteps; iz++)
            {
                float x = p0.x + ix * spacing;
                float z = p0.z + iz * spacing;
                if ((x - p0.x) * (x - p0.x) + (z - p0.z) * (z - p0.z) > capRadius * capRadius) continue;
                CityNode node = GetOrCreateNode(manager, new Vector3(x, p0.y, z), merge, ref report);
                if (node != null) gridNodes[(ix, iz)] = node;
            }

        for (int iz = -halfSteps; iz <= halfSteps; iz++)
            for (int ix = -halfSteps; ix < halfSteps; ix++)
            {
                if (!gridNodes.TryGetValue((ix, iz), out CityNode a)) continue;
                if (!gridNodes.TryGetValue((ix + 1, iz), out CityNode b)) continue;
                CitySegment seg = manager.AddSegment(a.id, b.id);
                if (seg != null) { ApplyProfile(seg, profile); report.segmentsCreated++; }
            }

        for (int ix = -halfSteps; ix <= halfSteps; ix++)
            for (int iz = -halfSteps; iz < halfSteps; iz++)
            {
                if (!gridNodes.TryGetValue((ix, iz), out CityNode a)) continue;
                if (!gridNodes.TryGetValue((ix, iz + 1), out CityNode b)) continue;
                CitySegment seg = manager.AddSegment(a.id, b.id);
                if (seg != null) { ApplyProfile(seg, profile); report.segmentsCreated++; }
            }
    }

    private static float[] GetJitteredPositions(
        int steps, float total, float nominal, float variation, System.Random rng)
    {
        float[] pos = new float[steps + 1];
        pos[0] = 0f; pos[steps] = total;
        float minGap = nominal * 0.20f;
        for (int i = 1; i < steps; i++)
        {
            float center = i * nominal;
            float delta  = (float)(rng.NextDouble() * 2.0 - 1.0) * variation * nominal;
            float lo     = pos[i - 1] + minGap;
            float hi     = total - (steps - i) * minGap;
            pos[i] = Mathf.Clamp(center + delta, lo, hi);
        }
        return pos;
    }

    private void GenerateLocalStreets(
        CityManager manager, Vector3 p0, float localCap,
        float merge, ref GenerationReport report)
    {
        float majorSpacing  = Mathf.Max(50f, config.majorGridSpacing);
        float localSpacing  = Mathf.Max(20f, config.localStreetSpacing);
        float variation     = Mathf.Clamp01(config.blockSizeVariation);
        float depthMult     = Mathf.Max(1f, config.blockDepthMultiplier);
        RoadProfile profile = config.localStreetProfile;
        int halfMajorSteps  = Mathf.CeilToInt(localCap / majorSpacing);

        for (int cx = -halfMajorSteps; cx < halfMajorSteps; cx++)
            for (int cz = -halfMajorSteps; cz < halfMajorSteps; cz++)
            {
                float ccx = p0.x + (cx + 0.5f) * majorSpacing;
                float ccz = p0.z + (cz + 0.5f) * majorSpacing;
                if ((ccx - p0.x) * (ccx - p0.x) + (ccz - p0.z) * (ccz - p0.z) > localCap * localCap) continue;

                float xMin = p0.x + cx * majorSpacing;
                float zMin = p0.z + cz * majorSpacing;
                int stepsX = Mathf.Max(2, Mathf.RoundToInt(majorSpacing / localSpacing));
                int stepsZ = Mathf.Max(2, Mathf.RoundToInt(majorSpacing / (localSpacing * depthMult)));
                if (stepsX < 2 && stepsZ < 2) continue;

                var rng = new System.Random(config.randomSeed ^ (cx * 73856093) ^ (cz * 19349663));
                float[] xPos = GetJitteredPositions(stepsX, majorSpacing, majorSpacing / stepsX, variation, rng);
                float[] zPos = GetJitteredPositions(stepsZ, majorSpacing, majorSpacing / stepsZ, variation, rng);

                var localNodes = new Dictionary<(int, int), CityNode>();
                for (int lx = 0; lx <= stepsX; lx++)
                    for (int lz = 0; lz <= stepsZ; lz++)
                    {
                        CityNode node = GetOrCreateNode(manager, new Vector3(xMin + xPos[lx], p0.y, zMin + zPos[lz]), merge, ref report);
                        if (node != null) localNodes[(lx, lz)] = node;
                    }

                for (int lz = 1; lz < stepsZ; lz++)
                    for (int lx = 0; lx < stepsX; lx++)
                    {
                        if (!localNodes.TryGetValue((lx, lz), out CityNode a)) continue;
                        if (!localNodes.TryGetValue((lx + 1, lz), out CityNode b)) continue;
                        if (a.id == b.id) continue;
                        CitySegment seg = manager.AddSegment(a.id, b.id);
                        if (seg != null) { ApplyProfile(seg, profile); report.segmentsCreated++; }
                    }

                for (int lx = 1; lx < stepsX; lx++)
                    for (int lz = 0; lz < stepsZ; lz++)
                    {
                        if (!localNodes.TryGetValue((lx, lz), out CityNode a)) continue;
                        if (!localNodes.TryGetValue((lx, lz + 1), out CityNode b)) continue;
                        if (a.id == b.id) continue;
                        CitySegment seg = manager.AddSegment(a.id, b.id);
                        if (seg != null) { ApplyProfile(seg, profile); report.segmentsCreated++; }
                    }

                // ── Vicoli ──────────────────────────────────────────────────
                if (config.alleyEnabled && config.alleyProfile != null)
                {
                    float alleyFrac = Mathf.Clamp(config.alleyPositionFraction, 0.3f, 0.7f);
                    // Un vicolo per ogni strip Z (tra lz e lz+1), parallelo all'asse X
                    for (int lz = 0; lz < stepsZ; lz++)
                    {
                        float alleyZ = zMin + zPos[lz] + (zPos[lz + 1] - zPos[lz]) * alleyFrac;
                        CityNode prevAlley = null;
                        for (int lx = 0; lx <= stepsX; lx++)
                        {
                            Vector3 pos = new Vector3(xMin + xPos[lx], p0.y, alleyZ);
                            CityNode node = GetOrCreateNode(manager, pos, merge, ref report);
                            if (node == null) continue;
                            if (prevAlley != null && prevAlley.id != node.id)
                            {
                                CitySegment seg = manager.AddSegment(prevAlley.id, node.id);
                                if (seg != null) { ApplyProfile(seg, config.alleyProfile); report.segmentsCreated++; }
                            }
                            prevAlley = node;
                        }
                    }
                }
            }
    }
}
