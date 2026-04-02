using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Utility editor-only per spawn e pulizia edifici prefab dai dati di zoning.
/// </summary>
public static class CityBuildingSpawner
{
    private const string SpawnRootName = "CitySpawnedBuildings";

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

                GameObject prefab = PickPrefab(validPrefabs, block.zoning, block.id, lot.id, lotIndex);
                if (prefab == null)
                {
                    report.blocksWithoutPrefabs++;
                    continue;
                }

                CityBuilderPrefab metadata = prefab.GetComponent<CityBuilderPrefab>();
                Vector3 spawnPosition = lotCenter;

                if (metadata != null)
                {
                    Vector2 footprint = metadata.GetFootprintSize();
                    bool fits = footprint.x <= lotWidth && footprint.y <= lotDepth;
                    if (!fits)
                    {
                        report.lotsOutOfFit++;
                    }

                    // Corregge solo Y: il pivot (tipicamente il fondo del building) deve atterrare
                    // al livello del lotto. pivotOffset.y < 0 quando il fondo è sotto il pivot
                    // del transform, quindi la sottrazione alza l'oggetto della giusta quantità.
                    spawnPosition.y -= metadata.pivotOffset.y;
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
                instance.transform.SetPositionAndRotation(spawnPosition, lotRotation);

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

    private static GameObject PickPrefab(List<GameObject> prefabs, ZoneType zone, int blockId, int lotId, int lotIndex)
    {
        if (prefabs == null || prefabs.Count == 0)
        {
            return null;
        }

        if (prefabs.Count == 1)
        {
            return prefabs[0];
        }

        if (zone != null && zone.deterministicPrefabSelection)
        {
            int hash = 17;
            hash = hash * 31 + zone.prefabSelectionSeed;
            hash = hash * 31 + blockId;
            hash = hash * 31 + lotId;
            hash = hash * 31 + lotIndex;
            int index = Mathf.Abs(hash) % prefabs.Count;
            return prefabs[index];
        }

        return prefabs[Random.Range(0, prefabs.Count)];
    }

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
}
