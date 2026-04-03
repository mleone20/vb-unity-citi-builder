using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Utility editor-only per spawn e pulizia edifici prefab dai dati di zoning.
/// </summary>
public static class CityBuildingSpawner
{
    private const string SpawnRootName = "CitySpawnedBuildings";
    private const float FitTolerance = 0.05f;

    public enum ExistingBuildingsHandling
    {
        KeepExisting,
        ClearExisting
    }

    public struct SpawnReport
    {
        public int processedBlocks;
        public int processedLots;
        public int spawnedBuildings;
        public int clearedObjects;
        public int blocksWithoutZoning;
        public int blocksWithoutPrefabs;
        public int lotsInvalidGeometry;
        public int prefabMissingMetadata;
        public int lotsOutOfFit;

        public string ToMultilineString()
        {
            return
                "Blocchi processati: " + processedBlocks + "\n" +
                "Lotti processati: " + processedLots + "\n" +
                "Edifici spawnati: " + spawnedBuildings + "\n" +
                "Oggetti rimossi prima dello spawn: " + clearedObjects + "\n" +
                "Blocchi senza zoning: " + blocksWithoutZoning + "\n" +
                "Blocchi senza prefab zona: " + blocksWithoutPrefabs + "\n" +
                "Lotti con geometria non valida: " + lotsInvalidGeometry + "\n" +
                "Prefab senza CityBuilderPrefab: " + prefabMissingMetadata + "\n" +
                "Prefab fuori fit lotto (informativo): " + lotsOutOfFit;
        }
    }

    public struct TerrainFlattenReport
    {
        public int processedLots;
        public int modifiedLots;
        public int invalidLots;
        public int lotsOutsideTerrain;
        public int touchedHeightSamples;
        public bool noTerrainFound;

        public string ToMultilineString()
        {
            if (noTerrainFound)
            {
                return "Terrain attivo non trovato. Operazione annullata.";
            }

            return
                "Lotti processati: " + processedLots + "\n" +
                "Lotti modificati: " + modifiedLots + "\n" +
                "Lotti non validi: " + invalidLots + "\n" +
                "Lotti fuori area Terrain: " + lotsOutsideTerrain + "\n" +
                "Campioni heightmap modificati: " + touchedHeightSamples;
        }
    }

    public struct RoadFlattenReport
    {
        public int processedSegments;
        public int modifiedSegments;
        public int invalidSegments;
        public int touchedHeightSamples;
        public int adjustedNodes;
        public bool noTerrainFound;

        public string ToMultilineString()
        {
            if (noTerrainFound)
            {
                return "Terrain attivo non trovato. Operazione annullata.";
            }

            return
                "Segmenti processati: " + processedSegments + "\n" +
                "Segmenti modificati: " + modifiedSegments + "\n" +
                "Segmenti non validi: " + invalidSegments + "\n" +
                "Campioni heightmap modificati: " + touchedHeightSamples + "\n" +
                "Nodi assestati sul Terrain: " + adjustedNodes;
        }
    }

    public struct BlockFlattenReport
    {
        public int processedBlocks;
        public int modifiedBlocks;
        public int invalidBlocks;
        public int blocksOutsideTerrain;
        public int touchedHeightSamples;
        public bool noTerrainFound;

        public string ToMultilineString()
        {
            if (noTerrainFound)
            {
                return "Terrain attivo non trovato. Operazione annullata.";
            }

            return
                "Blocchi processati: " + processedBlocks + "\n" +
                "Blocchi modificati: " + modifiedBlocks + "\n" +
                "Blocchi non validi: " + invalidBlocks + "\n" +
                "Blocchi fuori area Terrain: " + blocksOutsideTerrain + "\n" +
                "Campioni heightmap modificati: " + touchedHeightSamples;
        }
    }

    public static SpawnReport SpawnBuildings(CityManager manager, ExistingBuildingsHandling handling)
    {
        SpawnReport report = new SpawnReport();
        if (manager == null)
        {
            Debug.LogWarning("[CityBuildingSpawner] CityManager nullo, spawn annullato.");
            return report;
        }

        CityData cityData = manager.GetCityData();
        if (cityData == null)
        {
            Debug.LogWarning("[CityBuildingSpawner] CityData nullo, spawn annullato.");
            return report;
        }

        Transform root = GetOrCreateRoot();
        if (handling == ExistingBuildingsHandling.ClearExisting)
        {
            report.clearedObjects = ClearRoot(root);
            root = GetOrCreateRoot();
        }

        for (int i = 0; i < cityData.blocks.Count; i++)
        {
            CityBlock block = cityData.blocks[i];
            if (block == null)
            {
                continue;
            }

            report.processedBlocks++;

            if (block.zoning == null)
            {
                report.blocksWithoutZoning++;
                continue;
            }

            List<GameObject> validPrefabs = CollectValidPrefabs(block.zoning.buildingPrefabs);
            if (validPrefabs.Count == 0)
            {
                report.blocksWithoutPrefabs++;
                continue;
            }

            List<GameObject> frontagePrefabs = CollectPrefabsWithMetadata(validPrefabs);
            if (frontagePrefabs.Count == 0)
            {
                report.blocksWithoutPrefabs++;
                report.prefabMissingMetadata++;
                continue;
            }

            Transform blockParent = GetOrCreateBlockParent(root, block.id);
            List<CityLot> lots = GetLotsForBlock(cityData, block.id);

            for (int lotIndex = 0; lotIndex < lots.Count; lotIndex++)
            {
                CityLot lot = lots[lotIndex];
                report.processedLots++;

                if (!TryGetLotPose(lot, out Vector3 lotCenter, out Quaternion lotRotation, out float lotWidth, out float lotDepth))
                {
                    report.lotsInvalidGeometry++;
                    continue;
                }

                if (lot.assignedPrefabIndex < 0 || lot.assignedPrefabIndex >= frontagePrefabs.Count)
                {
                    report.lotsOutOfFit++;
                    continue;
                }

                // Ogni lotto spawna esclusivamente il prefab scelto in fase di generazione.
                GameObject prefab = frontagePrefabs[lot.assignedPrefabIndex];

                if (prefab == null)
                {
                    report.blocksWithoutPrefabs++;
                    continue;
                }

                CityBuilderPrefab metadata = prefab.GetComponent<CityBuilderPrefab>();
                Vector3 spawnPosition = lotCenter;
                Quaternion spawnRotation = lotRotation;

                if (metadata != null)
                {
                    Vector2 footprint = metadata.GetAlignedFootprintSize();
                    if (footprint.x > lotWidth + FitTolerance || footprint.y > lotDepth + FitTolerance)
                    {
                        report.lotsOutOfFit++;
                        continue;
                    }

                    if (!lot.hasAssignedSpawnRotation)
                    {
                        report.lotsOutOfFit++;
                        continue;
                    }

                    spawnRotation = lot.assignedSpawnRotation;
                    spawnPosition = ComputeLotMatchedSpawnPosition(metadata, lotCenter, spawnRotation);
                }
                else
                {
                    report.prefabMissingMetadata++;
                }

                GameObject instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (instance == null)
                {
                    instance = Object.Instantiate(prefab);
                }

                instance.name = prefab.name + "_B" + block.id + "_L" + lot.id;
                Undo.RegisterCreatedObjectUndo(instance, "Spawn Building");
                instance.transform.SetParent(blockParent, true);
                instance.transform.SetPositionAndRotation(spawnPosition, spawnRotation);

                report.spawnedBuildings++;
            }
        }

        Debug.Log("[CityBuildingSpawner] Spawn completato.\n" + report.ToMultilineString());
        return report;
    }

    public static int ClearSpawnedBuildings()
    {
        GameObject rootObject = GameObject.Find(SpawnRootName);
        if (rootObject == null)
        {
            return 0;
        }

        return ClearRoot(rootObject.transform);
    }

    public static TerrainFlattenReport FlattenTerrainUnderLots(CityManager manager)
    {
        TerrainFlattenReport report = new TerrainFlattenReport();
        if (manager == null)
        {
            return report;
        }

        CityData cityData = manager.GetCityData();
        if (cityData == null)
        {
            return report;
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            report.noTerrainFound = true;
            return report;
        }

        TerrainData terrainData = terrain.terrainData;
        if (terrainData == null)
        {
            report.noTerrainFound = true;
            return report;
        }

        int resolution = terrainData.heightmapResolution;
        if (resolution < 2)
        {
            return report;
        }

        if (cityData.lots == null || cityData.lots.Count == 0)
        {
            return report;
        }

        float lotFalloff = Mathf.Max(0.1f, cityData.lotTerrainFalloff);
        float lotBlendStrength = Mathf.Clamp01(cityData.lotTerrainBlendStrength);

        Undo.RecordObject(terrainData, "Flatten Terrain Under Lots");
        float[,] heights = terrainData.GetHeights(0, 0, resolution, resolution);
        Vector3 terrainPosition = terrain.GetPosition();
        Vector3 terrainSize = terrainData.size;
        if (terrainSize.x <= 0f || terrainSize.y <= 0f || terrainSize.z <= 0f)
        {
            return report;
        }

        for (int i = 0; i < cityData.lots.Count; i++)
        {
            CityLot lot = cityData.lots[i];
            report.processedLots++;

            if (!TryGetLotPolygon(lot, out List<Vector3> polygon))
            {
                report.invalidLots++;
                continue;
            }

            if (!TryGetHeightmapBounds(polygon, terrainPosition, terrainSize, resolution, lotFalloff, out int minX, out int maxX, out int minZ, out int maxZ))
            {
                report.lotsOutsideTerrain++;
                continue;
            }

            float targetHeightNormalized = Mathf.Clamp01((lot.buildingCenter.y - terrainPosition.y) / terrainSize.y);
            int touchedByLot = ApplySoftFlattenPolygon(
                heights,
                resolution,
                terrainPosition,
                terrainSize,
                polygon,
                minX,
                maxX,
                minZ,
                maxZ,
                targetHeightNormalized,
                lotFalloff,
                lotBlendStrength,
                false
            );

            if (touchedByLot > 0)
            {
                report.modifiedLots++;
                report.touchedHeightSamples += touchedByLot;
            }
        }

        if (report.touchedHeightSamples > 0)
        {
            terrainData.SetHeights(0, 0, heights);
            EditorUtility.SetDirty(terrainData);
        }

        return report;
    }

    public static RoadFlattenReport FlattenTerrainUnderRoads(CityManager manager)
    {
        RoadFlattenReport report = new RoadFlattenReport();
        if (manager == null)
        {
            return report;
        }

        CityData cityData = manager.GetCityData();
        if (cityData == null)
        {
            return report;
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            report.noTerrainFound = true;
            return report;
        }

        TerrainData terrainData = terrain.terrainData;
        if (terrainData == null)
        {
            report.noTerrainFound = true;
            return report;
        }

        int resolution = terrainData.heightmapResolution;
        if (resolution < 2)
        {
            return report;
        }

        if (cityData.segments == null || cityData.segments.Count == 0)
        {
            return report;
        }

        Undo.RecordObject(terrainData, "Flatten Terrain Under Roads");
        float[,] heights = terrainData.GetHeights(0, 0, resolution, resolution);
        Vector3 terrainPosition = terrain.GetPosition();
        Vector3 terrainSize = terrainData.size;
        if (terrainSize.x <= 0f || terrainSize.y <= 0f || terrainSize.z <= 0f)
        {
            return report;
        }

        float roadFalloff = Mathf.Max(0.1f, cityData.roadTerrainFalloff);
        float widthMultiplier = Mathf.Max(0.5f, cityData.roadTerrainWidthMultiplier);
        float roadBlendStrength = Mathf.Clamp01(cityData.roadTerrainBlendStrength);

        for (int i = 0; i < cityData.segments.Count; i++)
        {
            CitySegment segment = cityData.segments[i];
            if (segment == null)
            {
                report.invalidSegments++;
                continue;
            }

            report.processedSegments++;
            float roadWidth = Mathf.Max(0.5f, CityRoadGeometry.GetRoadWidth(cityData, segment) * widthMultiplier);
            float innerRadius = roadWidth * 0.5f;
            float outerRadius = innerRadius + roadFalloff;
            int touchedBySegment = 0;

            List<Vector3> sampledPoints = CityRoadGeometry.SampleSegment(cityData, segment, Mathf.Max(12, CityRoadGeometry.DefaultCurveSamples * 2));
            if (sampledPoints == null || sampledPoints.Count < 2)
            {
                report.invalidSegments++;
                continue;
            }

            float stampStep = Mathf.Max(0.5f, innerRadius * 0.7f);
            for (int j = 1; j < sampledPoints.Count; j++)
            {
                Vector3 from = sampledPoints[j - 1];
                Vector3 to = sampledPoints[j];
                float distance = Vector3.Distance(from, to);
                int stamps = Mathf.Max(1, Mathf.CeilToInt(distance / stampStep));

                for (int s = 0; s <= stamps; s++)
                {
                    float t = stamps > 0 ? s / (float)stamps : 0f;
                    Vector3 stampCenter = Vector3.Lerp(from, to, t);
                    float targetHeight = Mathf.Clamp01((stampCenter.y - terrainPosition.y) / terrainSize.y);
                    touchedBySegment += ApplyRoadBrush(
                        heights,
                        resolution,
                        terrainPosition,
                        terrainSize,
                        stampCenter,
                        innerRadius,
                        outerRadius,
                        targetHeight,
                        roadBlendStrength
                    );
                }
            }

            if (touchedBySegment > 0)
            {
                report.modifiedSegments++;
                report.touchedHeightSamples += touchedBySegment;
            }
        }

        if (report.touchedHeightSamples > 0)
        {
            terrainData.SetHeights(0, 0, heights);
            EditorUtility.SetDirty(terrainData);

            report.adjustedNodes = AlignAllNodesToTerrain(cityData, terrain);
            if (report.adjustedNodes > 0)
            {
                EditorUtility.SetDirty(cityData);
            }
        }

        return report;
    }

    public static BlockFlattenReport FlattenTerrainUnderBlocks(CityManager manager)
    {
        BlockFlattenReport report = new BlockFlattenReport();
        if (manager == null)
        {
            return report;
        }

        CityData cityData = manager.GetCityData();
        if (cityData == null)
        {
            return report;
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain == null)
        {
            report.noTerrainFound = true;
            return report;
        }

        TerrainData terrainData = terrain.terrainData;
        if (terrainData == null)
        {
            report.noTerrainFound = true;
            return report;
        }

        if (cityData.blocks == null || cityData.blocks.Count == 0)
        {
            return report;
        }

        int resolution = terrainData.heightmapResolution;
        if (resolution < 2)
        {
            return report;
        }

        float blockFalloff = Mathf.Max(0.1f, cityData.roadTerrainFalloff);
        float blockBlendStrength = Mathf.Clamp01(cityData.roadTerrainBlendStrength);

        Undo.RecordObject(terrainData, "Flatten Terrain Under Blocks");
        float[,] heights = terrainData.GetHeights(0, 0, resolution, resolution);
        Vector3 terrainPosition = terrain.GetPosition();
        Vector3 terrainSize = terrainData.size;
        if (terrainSize.x <= 0f || terrainSize.y <= 0f || terrainSize.z <= 0f)
        {
            return report;
        }

        for (int i = 0; i < cityData.blocks.Count; i++)
        {
            CityBlock block = cityData.blocks[i];
            report.processedBlocks++;

            if (block == null || block.vertices == null || block.vertices.Count < 3)
            {
                report.invalidBlocks++;
                continue;
            }

            List<Vector3> polygon = block.vertices;
            if (!TryGetHeightmapBounds(polygon, terrainPosition, terrainSize, resolution, blockFalloff, out int minX, out int maxX, out int minZ, out int maxZ))
            {
                report.blocksOutsideTerrain++;
                continue;
            }

            float avgY = 0f;
            for (int v = 0; v < polygon.Count; v++)
            {
                avgY += polygon[v].y;
            }

            avgY /= polygon.Count;
            float targetHeightNormalized = Mathf.Clamp01((avgY - terrainPosition.y) / terrainSize.y);
            int touchedByBlock = ApplySoftFlattenPolygon(
                heights,
                resolution,
                terrainPosition,
                terrainSize,
                polygon,
                minX,
                maxX,
                minZ,
                maxZ,
                targetHeightNormalized,
                blockFalloff,
                blockBlendStrength,
                true
            );

            if (touchedByBlock > 0)
            {
                report.modifiedBlocks++;
                report.touchedHeightSamples += touchedByBlock;
            }
        }

        if (report.touchedHeightSamples > 0)
        {
            terrainData.SetHeights(0, 0, heights);
            EditorUtility.SetDirty(terrainData);
        }

        return report;
    }

    private static int ClearRoot(Transform root)
    {
        if (root == null)
        {
            return 0;
        }

        int removedCount = CountHierarchyObjects(root);
        Undo.DestroyObjectImmediate(root.gameObject);
        return removedCount;
    }

    private static int CountHierarchyObjects(Transform root)
    {
        int count = 0;
        Queue<Transform> queue = new Queue<Transform>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            Transform current = queue.Dequeue();
            count++;
            for (int i = 0; i < current.childCount; i++)
            {
                queue.Enqueue(current.GetChild(i));
            }
        }

        return count;
    }

    private static Transform GetOrCreateRoot()
    {
        GameObject rootObject = GameObject.Find(SpawnRootName);
        if (rootObject != null)
        {
            return rootObject.transform;
        }

        rootObject = new GameObject(SpawnRootName);
        Undo.RegisterCreatedObjectUndo(rootObject, "Create Building Spawn Root");
        return rootObject.transform;
    }

    private static Transform GetOrCreateBlockParent(Transform root, int blockId)
    {
        string blockName = "Block_" + blockId;
        Transform blockParent = root.Find(blockName);
        if (blockParent != null)
        {
            return blockParent;
        }

        GameObject blockObject = new GameObject(blockName);
        Undo.RegisterCreatedObjectUndo(blockObject, "Create Block Spawn Parent");
        blockObject.transform.SetParent(root, false);
        return blockObject.transform;
    }

    private static List<GameObject> CollectValidPrefabs(List<GameObject> prefabs)
    {
        List<GameObject> validPrefabs = new List<GameObject>();
        if (prefabs == null)
        {
            return validPrefabs;
        }

        for (int i = 0; i < prefabs.Count; i++)
        {
            if (prefabs[i] != null)
            {
                validPrefabs.Add(prefabs[i]);
            }
        }

        return validPrefabs;
    }

    private static List<GameObject> CollectPrefabsWithMetadata(List<GameObject> prefabs)
    {
        List<GameObject> result = new List<GameObject>();
        if (prefabs == null)
        {
            return result;
        }

        for (int i = 0; i < prefabs.Count; i++)
        {
            GameObject prefab = prefabs[i];
            if (prefab != null && prefab.GetComponent<CityBuilderPrefab>() != null)
            {
                result.Add(prefab);
            }
        }

        return result;
    }

    private static List<CityLot> GetLotsForBlock(CityData cityData, int blockId)
    {
        List<CityLot> lots = new List<CityLot>();
        for (int i = 0; i < cityData.lots.Count; i++)
        {
            CityLot lot = cityData.lots[i];
            if (lot != null && lot.blockID == blockId)
            {
                lots.Add(lot);
            }
        }

        return lots;
    }
 
    // ─────────────────────────────────────────────────────────────────────────────

    private static bool TryGetLotPose(CityLot lot, out Vector3 center, out Quaternion rotation, out float width, out float depth)
    {
        center = lot != null ? lot.buildingCenter : Vector3.zero;
        rotation = Quaternion.identity;
        width = 0f;
        depth = 0f;

        if (lot == null || lot.vertices == null || lot.vertices.Count < 4)
        {
            return false;
        }

        Vector3 frontL = lot.vertices[0];
        Vector3 frontR = lot.vertices[1];
        Vector3 backR = lot.vertices[2];
        Vector3 backL = lot.vertices[3];

        width = Vector3.Distance(frontL, frontR);
        float depthL = Vector3.Distance(frontL, backL);
        float depthR = Vector3.Distance(frontR, backR);
        depth = (depthL + depthR) * 0.5f;

        if (width < 0.1f || depth < 0.1f)
        {
            return false;
        }

        Vector3 lotForward = ((backL + backR) * 0.5f - (frontL + frontR) * 0.5f).normalized;
        if (lotForward.sqrMagnitude < 0.0001f)
        {
            lotForward = Vector3.forward;
        }

        rotation = Quaternion.LookRotation(lotForward, Vector3.up);
        center = lot.buildingCenter;

        return true;
    }

    private static Vector3 ComputeLotMatchedSpawnPosition(
        CityBuilderPrefab metadata, Vector3 lotCenter, Quaternion spawnRotation)
    {
        Vector3 pivotOffsetXZ = new Vector3(metadata.pivotOffset.x, 0f, metadata.pivotOffset.z);
        Vector3 worldPivotOffsetXZ = spawnRotation * pivotOffsetXZ;
        Vector3 spawnPosition = lotCenter - worldPivotOffsetXZ;
        spawnPosition.y = lotCenter.y - metadata.pivotOffset.y;
        return spawnPosition;
    }

    private static bool TryGetLotPolygon(CityLot lot, out List<Vector3> polygon)
    {
        polygon = null;
        if (lot == null || lot.vertices == null || lot.vertices.Count < 3)
        {
            return false;
        }

        polygon = lot.vertices;
        return true;
    }

    private static bool TryGetHeightmapBounds(
        List<Vector3> polygon,
        Vector3 terrainPosition,
        Vector3 terrainSize,
        int resolution,
        float paddingWorld,
        out int minX,
        out int maxX,
        out int minZ,
        out int maxZ)
    {
        minX = resolution - 1;
        minZ = resolution - 1;
        maxX = 0;
        maxZ = 0;

        int rawMinX = int.MaxValue;
        int rawMaxX = int.MinValue;
        int rawMinZ = int.MaxValue;
        int rawMaxZ = int.MinValue;

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector3 point = polygon[i];
            float xNormalized = (point.x - terrainPosition.x) / terrainSize.x;
            float zNormalized = (point.z - terrainPosition.z) / terrainSize.z;
            float padX = paddingWorld / terrainSize.x;
            float padZ = paddingWorld / terrainSize.z;

            int xPixelMin = Mathf.FloorToInt((xNormalized - padX) * (resolution - 1));
            int xPixelMax = Mathf.CeilToInt((xNormalized + padX) * (resolution - 1));
            int zPixelMin = Mathf.FloorToInt((zNormalized - padZ) * (resolution - 1));
            int zPixelMax = Mathf.CeilToInt((zNormalized + padZ) * (resolution - 1));

            rawMinX = Mathf.Min(rawMinX, xPixelMin);
            rawMaxX = Mathf.Max(rawMaxX, xPixelMax);
            rawMinZ = Mathf.Min(rawMinZ, zPixelMin);
            rawMaxZ = Mathf.Max(rawMaxZ, zPixelMax);
        }

        if (rawMaxX < 0 || rawMinX >= resolution || rawMaxZ < 0 || rawMinZ >= resolution)
        {
            return false;
        }

        minX = Mathf.Clamp(rawMinX, 0, resolution - 1);
        maxX = Mathf.Clamp(rawMaxX, 0, resolution - 1);
        minZ = Mathf.Clamp(rawMinZ, 0, resolution - 1);
        maxZ = Mathf.Clamp(rawMaxZ, 0, resolution - 1);

        return minX <= maxX && minZ <= maxZ;
    }

    private static int ApplySoftFlattenPolygon(
        float[,] heights,
        int resolution,
        Vector3 terrainPosition,
        Vector3 terrainSize,
        List<Vector3> polygon,
        int minX,
        int maxX,
        int minZ,
        int maxZ,
        float targetHeightNormalized,
        float falloffWorld,
        float blendStrength,
        bool forceInteriorFlat)
    {
        int touchedSamples = 0;
        const float epsilon = 0.0001f;

        float safeFalloff = Mathf.Max(0.0001f, falloffWorld);

        for (int z = minZ; z <= maxZ; z++)
        {
            float zT = (float)z / (resolution - 1);
            float worldZ = terrainPosition.z + zT * terrainSize.z;

            for (int x = minX; x <= maxX; x++)
            {
                float xT = (float)x / (resolution - 1);
                float worldX = terrainPosition.x + xT * terrainSize.x;

                bool insidePolygon = PointInPolygonXZ(worldX, worldZ, polygon);
                float distanceToEdge = DistancePointToPolygonEdgesXZ(worldX, worldZ, polygon);
                float blend = 0f;

                if (insidePolygon)
                {
                    blend = 1f;
                }
                else if (distanceToEdge <= safeFalloff)
                {
                    blend = 1f - Mathf.Clamp01(distanceToEdge / safeFalloff);
                }

                if (blend <= 0f)
                {
                    continue;
                }

                float previous = heights[z, x];
                float effectiveBlend;
                if (insidePolygon && forceInteriorFlat)
                {
                    effectiveBlend = 1f;
                }
                else if (insidePolygon)
                {
                    effectiveBlend = Mathf.Clamp01(blendStrength);
                }
                else
                {
                    effectiveBlend = Mathf.Clamp01(blend * blendStrength);
                }

                float blendedTarget = Mathf.Lerp(previous, targetHeightNormalized, effectiveBlend);
                if (Mathf.Abs(previous - blendedTarget) <= epsilon)
                {
                    continue;
                }

                heights[z, x] = blendedTarget;
                touchedSamples++;
            }
        }

        return touchedSamples;
    }

    private static int ApplyRoadBrush(
        float[,] heights,
        int resolution,
        Vector3 terrainPosition,
        Vector3 terrainSize,
        Vector3 center,
        float innerRadius,
        float outerRadius,
        float targetHeightNormalized,
        float blendStrength)
    {
        if (outerRadius <= 0f || outerRadius < innerRadius)
        {
            return 0;
        }

        float xNormalized = (center.x - terrainPosition.x) / terrainSize.x;
        float zNormalized = (center.z - terrainPosition.z) / terrainSize.z;
        float radiusNormX = outerRadius / terrainSize.x;
        float radiusNormZ = outerRadius / terrainSize.z;

        int minX = Mathf.Clamp(Mathf.FloorToInt((xNormalized - radiusNormX) * (resolution - 1)), 0, resolution - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt((xNormalized + radiusNormX) * (resolution - 1)), 0, resolution - 1);
        int minZ = Mathf.Clamp(Mathf.FloorToInt((zNormalized - radiusNormZ) * (resolution - 1)), 0, resolution - 1);
        int maxZ = Mathf.Clamp(Mathf.CeilToInt((zNormalized + radiusNormZ) * (resolution - 1)), 0, resolution - 1);

        const float epsilon = 0.0001f;
        int touched = 0;
        float falloff = Mathf.Max(0.0001f, outerRadius - innerRadius);

        for (int z = minZ; z <= maxZ; z++)
        {
            float zT = (float)z / (resolution - 1);
            float worldZ = terrainPosition.z + zT * terrainSize.z;

            for (int x = minX; x <= maxX; x++)
            {
                float xT = (float)x / (resolution - 1);
                float worldX = terrainPosition.x + xT * terrainSize.x;
                float distance = Vector2.Distance(new Vector2(worldX, worldZ), new Vector2(center.x, center.z));

                float blend = 0f;
                if (distance <= innerRadius)
                {
                    blend = 1f;
                }
                else if (distance <= outerRadius)
                {
                    blend = 1f - Mathf.Clamp01((distance - innerRadius) / falloff);
                }

                if (blend <= 0f)
                {
                    continue;
                }

                float previous = heights[z, x];
                float effectiveBlend = Mathf.Clamp01(blend * blendStrength);
                float blendedTarget = Mathf.Lerp(previous, targetHeightNormalized, effectiveBlend);
                if (Mathf.Abs(previous - blendedTarget) <= epsilon)
                {
                    continue;
                }

                heights[z, x] = blendedTarget;
                touched++;
            }
        }

        return touched;
    }

    private static float DistancePointToPolygonEdgesXZ(float x, float z, List<Vector3> polygon)
    {
        float minDistance = float.MaxValue;
        Vector2 point = new Vector2(x, z);

        for (int i = 0; i < polygon.Count; i++)
        {
            Vector3 a = polygon[i];
            Vector3 b = polygon[(i + 1) % polygon.Count];
            Vector2 va = new Vector2(a.x, a.z);
            Vector2 vb = new Vector2(b.x, b.z);

            Vector2 ab = vb - va;
            float lengthSq = ab.sqrMagnitude;
            float t = lengthSq > 0.0001f ? Mathf.Clamp01(Vector2.Dot(point - va, ab) / lengthSq) : 0f;
            Vector2 projection = va + ab * t;
            float distance = Vector2.Distance(point, projection);
            if (distance < minDistance)
            {
                minDistance = distance;
            }
        }

        return minDistance;
    }

    private static int AlignAllNodesToTerrain(CityData cityData, Terrain terrain)
    {
        if (cityData == null || terrain == null || cityData.nodes == null)
        {
            return 0;
        }

        int adjusted = 0;
        float yOffset = cityData.nodeTerrainYOffset;
        Vector3 terrainPosition = terrain.GetPosition();
        const float epsilon = 0.0001f;

        for (int i = 0; i < cityData.nodes.Count; i++)
        {
            CityNode node = cityData.nodes[i];
            if (node == null)
            {
                continue;
            }

            float sampledHeight = terrain.SampleHeight(node.position) + terrainPosition.y + yOffset;
            if (Mathf.Abs(sampledHeight - node.position.y) <= epsilon)
            {
                continue;
            }

            Vector3 updatedPosition = node.position;
            updatedPosition.y = sampledHeight;
            node.position = updatedPosition;
            adjusted++;
        }

        return adjusted;
    }

    private static bool PointInPolygonXZ(float x, float z, List<Vector3> polygon)
    {
        bool inside = false;
        int count = polygon.Count;

        for (int i = 0, j = count - 1; i < count; j = i++)
        {
            Vector3 a = polygon[i];
            Vector3 b = polygon[j];
            bool intersects = ((a.z > z) != (b.z > z)) &&
                              (x < (b.x - a.x) * (z - a.z) / (b.z - a.z + Mathf.Epsilon) + a.x);

            if (intersects)
            {
                inside = !inside;
            }
        }

        return inside;
    }
}
