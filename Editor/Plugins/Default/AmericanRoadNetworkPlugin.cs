using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

[CityPlugin("bsc.american.road-network", "American Road Network", CityPluginCategory.RoadNetwork, "Generazione rete stradale in stile americano.")]
public class AmericanRoadNetworkPlugin : IRoadNetworkGenerationPlugin
{
    public CityGenerationReport GenerateRoadNetwork(CityGenerationContext context)
    {
        CityGenerationReport report = new CityGenerationReport { warnings = new List<string>() };
        if (context.manager == null || context.config == null)
        {
            report.warnings.Add("CityManager o AmericanCityConfig non assegnati.");
            return report;
        }

        CityData cityData = context.cityData != null ? context.cityData : context.manager.GetCityData();
        if (cityData == null)
        {
            report.warnings.Add("CityData non assegnato nel CityManager.");
            return report;
        }

        GenerateRoadNetworkGrid(context.manager, context.config, ref report);

        EditorUtility.SetDirty(cityData);
        SceneView.RepaintAll();
        return report;
    }

    private static CityNode GetOrCreateNode(CityManager manager, Vector3 position, float mergeThreshold, ref CityGenerationReport report)
    {
        CityNode existing = manager.FindNearestNode(position, mergeThreshold);
        if (existing != null)
        {
            return existing;
        }

        CityNode node = manager.AddNode(position);
        if (node != null)
        {
            report.nodesCreated++;
        }

        return node;
    }

    private static void ApplyProfile(CitySegment segment, RoadProfile profile)
    {
        if (segment == null || profile == null)
        {
            return;
        }

        segment.roadProfile = profile;
        segment.width = profile.roadWidth;
    }

    private static void GenerateRoadNetworkGrid(CityManager manager, AmericanCityConfig config, ref CityGenerationReport report)
    {
        Vector3 p0 = config.centerWorldPosition;
        float capRadius = config.maxGenerationRadius;
        float merge = Mathf.Max(0.1f, config.mergeThreshold);

        CityNode centerNode = GetOrCreateNode(manager, p0, merge, ref report);
        if (centerNode == null)
        {
            return;
        }

        GenerateHighways(manager, config, centerNode, p0, capRadius, merge, ref report);
        GenerateMajorGrid(manager, config, p0, capRadius, merge, ref report);

        float localCap = Mathf.Min(capRadius, config.localStreetMaxRadius);
        bool localEnabled = localCap > 0f &&
            config.localStreetSpacing > 0f &&
            config.localStreetSpacing < config.majorGridSpacing * 0.95f;

        if (localEnabled)
        {
            GenerateLocalStreets(manager, config, p0, localCap, merge, ref report);
        }
    }

    private static void GenerateHighways(CityManager manager, AmericanCityConfig config, CityNode centerNode, Vector3 p0, float capRadius, float merge, ref CityGenerationReport report)
    {
        int hwCount = Mathf.Clamp(config.highwayCount, 1, 4);
        float step = Mathf.Max(50f, config.majorGridSpacing);
        RoadProfile profile = config.highwayProfile;

        for (int i = 0; i < hwCount; i++)
        {
            float angleDeg = i * (180f / hwCount);
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 dirA = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;

            GenerateHighwayArm(manager, centerNode, p0, dirA, step, capRadius, merge, profile, ref report);
            GenerateHighwayArm(manager, centerNode, p0, -dirA, step, capRadius, merge, profile, ref report);
        }
    }

    private static void GenerateHighwayArm(CityManager manager, CityNode centerNode, Vector3 p0, Vector3 direction, float step, float capRadius, float merge, RoadProfile profile, ref CityGenerationReport report)
    {
        CityNode prevNode = centerNode;
        float dist = step;

        while (dist <= capRadius + step * 0.01f)
        {
            Vector3 pos = p0 + direction * dist;
            CityNode node = GetOrCreateNode(manager, pos, merge, ref report);
            if (node == null)
            {
                break;
            }

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

    private static void GenerateMajorGrid(CityManager manager, AmericanCityConfig config, Vector3 p0, float capRadius, float merge, ref CityGenerationReport report)
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

                if ((x - p0.x) * (x - p0.x) + (z - p0.z) * (z - p0.z) > capRadius * capRadius)
                {
                    continue;
                }

                CityNode node = GetOrCreateNode(manager, new Vector3(x, p0.y, z), merge, ref report);
                if (node != null)
                {
                    gridNodes[(ix, iz)] = node;
                }
            }
        }

        for (int iz = -halfSteps; iz <= halfSteps; iz++)
        {
            for (int ix = -halfSteps; ix < halfSteps; ix++)
            {
                if (!gridNodes.TryGetValue((ix, iz), out CityNode a) || !gridNodes.TryGetValue((ix + 1, iz), out CityNode b))
                {
                    continue;
                }

                CitySegment seg = manager.AddSegment(a.id, b.id);
                if (seg != null)
                {
                    ApplyProfile(seg, profile);
                    report.segmentsCreated++;
                }
            }
        }

        for (int ix = -halfSteps; ix <= halfSteps; ix++)
        {
            for (int iz = -halfSteps; iz < halfSteps; iz++)
            {
                if (!gridNodes.TryGetValue((ix, iz), out CityNode a) || !gridNodes.TryGetValue((ix, iz + 1), out CityNode b))
                {
                    continue;
                }

                CitySegment seg = manager.AddSegment(a.id, b.id);
                if (seg != null)
                {
                    ApplyProfile(seg, profile);
                    report.segmentsCreated++;
                }
            }
        }
    }

    private static float[] GetJitteredPositions(int steps, float total, float nominal, float variation, System.Random rng)
    {
        float[] pos = new float[steps + 1];
        pos[0] = 0f;
        pos[steps] = total;
        float minGap = nominal * 0.20f;

        for (int i = 1; i < steps; i++)
        {
            float center = i * nominal;
            float delta = (float)(rng.NextDouble() * 2.0 - 1.0) * variation * nominal;
            float lo = pos[i - 1] + minGap;
            float hi = total - (steps - i) * minGap;
            pos[i] = Mathf.Clamp(center + delta, lo, hi);
        }

        return pos;
    }

    private static void GenerateLocalStreets(CityManager manager, AmericanCityConfig config, Vector3 p0, float localCap, float merge, ref CityGenerationReport report)
    {
        float majorSpacing = Mathf.Max(50f, config.majorGridSpacing);
        float localSpacing = Mathf.Max(20f, config.localStreetSpacing);
        float variation = Mathf.Clamp01(config.blockSizeVariation);
        RoadProfile profile = config.localStreetProfile;
        int halfMajorSteps = Mathf.CeilToInt(localCap / majorSpacing);

        for (int cx = -halfMajorSteps; cx < halfMajorSteps; cx++)
        {
            for (int cz = -halfMajorSteps; cz < halfMajorSteps; cz++)
            {
                float ccx = p0.x + (cx + 0.5f) * majorSpacing;
                float ccz = p0.z + (cz + 0.5f) * majorSpacing;
                if ((ccx - p0.x) * (ccx - p0.x) + (ccz - p0.z) * (ccz - p0.z) > localCap * localCap)
                {
                    continue;
                }

                float xMin = p0.x + cx * majorSpacing;
                float zMin = p0.z + cz * majorSpacing;
                int stepsX = Mathf.Max(2, Mathf.RoundToInt(majorSpacing / localSpacing));
                int stepsZ = Mathf.Max(2, Mathf.RoundToInt(majorSpacing / localSpacing));
                if (stepsX < 2 && stepsZ < 2)
                {
                    continue;
                }

                var rng = new System.Random(config.randomSeed ^ (cx * 73856093) ^ (cz * 19349663));
                float[] xPos = GetJitteredPositions(stepsX, majorSpacing, majorSpacing / stepsX, variation, rng);
                float[] zPos = GetJitteredPositions(stepsZ, majorSpacing, majorSpacing / stepsZ, variation, rng);

                var localNodes = new Dictionary<(int, int), CityNode>();
                for (int lx = 0; lx <= stepsX; lx++)
                {
                    for (int lz = 0; lz <= stepsZ; lz++)
                    {
                        CityNode node = GetOrCreateNode(manager, new Vector3(xMin + xPos[lx], p0.y, zMin + zPos[lz]), merge, ref report);
                        if (node != null)
                        {
                            localNodes[(lx, lz)] = node;
                        }
                    }
                }

                for (int lz = 1; lz < stepsZ; lz++)
                {
                    for (int lx = 0; lx < stepsX; lx++)
                    {
                        if (!localNodes.TryGetValue((lx, lz), out CityNode a) || !localNodes.TryGetValue((lx + 1, lz), out CityNode b) || a.id == b.id)
                        {
                            continue;
                        }

                        CitySegment seg = manager.AddSegment(a.id, b.id);
                        if (seg != null)
                        {
                            ApplyProfile(seg, profile);
                            report.segmentsCreated++;
                        }
                    }
                }

                for (int lx = 1; lx < stepsX; lx++)
                {
                    for (int lz = 0; lz < stepsZ; lz++)
                    {
                        if (!localNodes.TryGetValue((lx, lz), out CityNode a) || !localNodes.TryGetValue((lx, lz + 1), out CityNode b) || a.id == b.id)
                        {
                            continue;
                        }

                        CitySegment seg = manager.AddSegment(a.id, b.id);
                        if (seg != null)
                        {
                            ApplyProfile(seg, profile);
                            report.segmentsCreated++;
                        }
                    }
                }

                if (config.alleyEnabled && config.alleyProfile != null)
                {
                    float alleyFrac = Mathf.Clamp(config.alleyPositionFraction, 0.3f, 0.7f);
                    for (int lz = 0; lz < stepsZ; lz++)
                    {
                        float alleyZ = zMin + zPos[lz] + (zPos[lz + 1] - zPos[lz]) * alleyFrac;
                        CityNode prevAlley = null;

                        for (int lx = 0; lx <= stepsX; lx++)
                        {
                            Vector3 pos = new Vector3(xMin + xPos[lx], p0.y, alleyZ);
                            CityNode node = GetOrCreateNode(manager, pos, merge, ref report);
                            if (node == null)
                            {
                                continue;
                            }

                            if (prevAlley != null && prevAlley.id != node.id)
                            {
                                CitySegment seg = manager.AddSegment(prevAlley.id, node.id);
                                if (seg != null)
                                {
                                    ApplyProfile(seg, config.alleyProfile);
                                    report.segmentsCreated++;
                                }
                            }

                            prevAlley = node;
                        }
                    }
                }
            }
        }
    }
}
