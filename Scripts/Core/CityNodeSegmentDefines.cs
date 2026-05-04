using UnityEngine;
using System.Collections.Generic;

namespace BSCCityBuilder.Core
{
/// <summary>
/// Asset che descrive una destinazione d'uso della città.
/// Colore e altezza edificio sono ora definiti per zona tramite ScriptableObject.
/// </summary>
[CreateAssetMenu(fileName = "ZoneType", menuName = "City Builder/Zone Type")]
public class ZoneType : ScriptableObject
{
    public string displayName = "New Zone";
    public Color zoneColor = Color.white;
    public float buildingHeight = 5.0f;
    [TextArea] public string description;

    [Header("Building Prefabs")]
    public List<ZonePrefabSpawnEntry> buildingPrefabEntries = new List<ZonePrefabSpawnEntry>();

    [HideInInspector]
    public List<GameObject> buildingPrefabs = new List<GameObject>();

    public bool deterministicPrefabSelection = true;
    public int prefabSelectionSeed = 0;

    private void OnValidate()
    {
        EnsurePrefabEntries();
        SyncLegacyPrefabListFromEntries();
    }

    public void EnsurePrefabEntries()
    {
        if (buildingPrefabEntries == null)
        {
            buildingPrefabEntries = new List<ZonePrefabSpawnEntry>();
        }

        if (buildingPrefabEntries.Count > 0)
        {
            return;
        }

        if (buildingPrefabs == null || buildingPrefabs.Count == 0)
        {
            return;
        }

        for (int i = 0; i < buildingPrefabs.Count; i++)
        {
            GameObject prefab = buildingPrefabs[i];
            if (prefab == null)
            {
                continue;
            }

            buildingPrefabEntries.Add(new ZonePrefabSpawnEntry
            {
                prefab = prefab,
                spawnProbability = 1f
            });
        }
    }

    public List<ZonePrefabSpawnEntry> GetValidPrefabEntries(bool includeZeroProbability = false)
    {
        EnsurePrefabEntries();

        List<ZonePrefabSpawnEntry> result = new List<ZonePrefabSpawnEntry>();
        for (int i = 0; i < buildingPrefabEntries.Count; i++)
        {
            ZonePrefabSpawnEntry entry = buildingPrefabEntries[i];
            if (entry == null || entry.prefab == null)
            {
                continue;
            }

            float probability = Mathf.Clamp01(entry.spawnProbability);
            if (!includeZeroProbability && probability <= 0f)
            {
                continue;
            }

            result.Add(new ZonePrefabSpawnEntry
            {
                prefab = entry.prefab,
                spawnProbability = probability
            });
        }

        return result;
    }

    public bool ContainsPrefab(GameObject prefab)
    {
        if (prefab == null)
        {
            return false;
        }

        EnsurePrefabEntries();
        for (int i = 0; i < buildingPrefabEntries.Count; i++)
        {
            ZonePrefabSpawnEntry entry = buildingPrefabEntries[i];
            if (entry != null && entry.prefab == prefab)
            {
                return true;
            }
        }

        return false;
    }

    public bool TryAddPrefab(GameObject prefab, float spawnProbability = 1f)
    {
        if (prefab == null)
        {
            return false;
        }

        EnsurePrefabEntries();
        if (ContainsPrefab(prefab))
        {
            return false;
        }

        buildingPrefabEntries.Add(new ZonePrefabSpawnEntry
        {
            prefab = prefab,
            spawnProbability = Mathf.Clamp01(spawnProbability)
        });

        SyncLegacyPrefabListFromEntries();
        return true;
    }

    public void SetPrefabs(IEnumerable<GameObject> prefabs)
    {
        EnsurePrefabEntries();
        buildingPrefabEntries.Clear();

        if (prefabs != null)
        {
            foreach (GameObject prefab in prefabs)
            {
                if (prefab == null)
                {
                    continue;
                }

                buildingPrefabEntries.Add(new ZonePrefabSpawnEntry
                {
                    prefab = prefab,
                    spawnProbability = 1f
                });
            }
        }

        SyncLegacyPrefabListFromEntries();
    }

    private void SyncLegacyPrefabListFromEntries()
    {
        if (buildingPrefabs == null)
        {
            buildingPrefabs = new List<GameObject>();
        }
        else
        {
            buildingPrefabs.Clear();
        }

        if (buildingPrefabEntries == null)
        {
            return;
        }

        for (int i = 0; i < buildingPrefabEntries.Count; i++)
        {
            ZonePrefabSpawnEntry entry = buildingPrefabEntries[i];
            if (entry == null || entry.prefab == null)
            {
                continue;
            }

            if (!buildingPrefabs.Contains(entry.prefab))
            {
                buildingPrefabs.Add(entry.prefab);
            }
        }
    }

    public string GetDisplayName()
    {
        return string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    }
}

[System.Serializable]
public class ZonePrefabSpawnEntry
{
    public GameObject prefab;
    [Range(0f, 1f)] public float spawnProbability = 1f;
}

public enum CitySegmentGeometryType
{
    Straight,
    Bezier
}

/// <summary>
/// Orientamento del blocco: interno (edifici dentro il blocco), esterno (fuori dalla strada) o sparso (random nel blocco)
/// </summary>
public enum BlockOrientation
{
    Interior,
    Exterior,
    Sparse
}

/// <summary>
/// Nodo di una strada (vertice del grafo stradale)
/// </summary>
[System.Serializable]
public class CityNode
{
    public int id;
    public Vector3 position;
    public List<int> connectedSegmentIDs = new List<int>();

    public CityNode(int id, Vector3 position)
    {
        this.id = id;
        this.position = position;
    }
}

/// <summary>
/// Segmento di strada che connette due nodi
/// </summary>
[System.Serializable]
public class CitySegment
{
    public int id;
    public int nodeA_ID;
    public int nodeB_ID;
    public float width = 3.0f;
    public RoadProfile roadProfile;
    public CitySegmentGeometryType geometryType = CitySegmentGeometryType.Straight;
    public Vector3 controlPointA;
    public Vector3 controlPointB;

    public CitySegment(int id, int nodeA_ID, int nodeB_ID, float width = 3.0f)
    {
        this.id = id;
        this.nodeA_ID = nodeA_ID;
        this.nodeB_ID = nodeB_ID;
        this.width = width;
    }

    public bool IsCurved()
    {
        return geometryType == CitySegmentGeometryType.Bezier;
    }

    public float GetConfiguredWidth(float fallbackWidth = 3.0f)
    {
        if (roadProfile != null)
        {
            return Mathf.Max(0.5f, roadProfile.roadWidth);
        }

        return Mathf.Max(0.5f, width > 0f ? width : fallbackWidth);
    }

    public void ResetBezierHandles(Vector3 start, Vector3 end)
    {
        Vector3 delta = end - start;
        controlPointA = start + delta / 3f;
        controlPointB = end - delta / 3f;
    }
}

/// <summary>
/// Blocco (isolato) - area racchiusa da segmenti stradali
/// </summary>
[System.Serializable]
public class CityBlock
{
    public int id;
    public List<Vector3> vertices = new List<Vector3>();
    public ZoneType zoning;
    public List<int> lotIDs = new List<int>();
    public BlockOrientation orientation = BlockOrientation.Interior;
    /// <summary>Gap fisso tra lotti per questo blocco. Se negativo usa i valori globali di CityData.</summary>
    public float lotGapOverride = -1f;

    public CityBlock(int id)
    {
        this.id = id;
    }

    public float GetArea()
    {
        if (vertices.Count < 3) return 0f;
        
        // Shoelace formula per area poligono 2D (proiezione XZ)
        float area = 0f;
        for (int i = 0; i < vertices.Count; i++)
        {
            int next = (i + 1) % vertices.Count;
            area += vertices[i].x * vertices[next].z;
            area -= vertices[next].x * vertices[i].z;
        }
        return Mathf.Abs(area) * 0.5f;
    }

    public float GetPerimeter()
    {
        if (vertices.Count < 2) return 0f;
        
        float perimeter = 0f;
        for (int i = 0; i < vertices.Count; i++)
        {
            int next = (i + 1) % vertices.Count;
            perimeter += Vector3.Distance(vertices[i], vertices[next]);
        }
        return perimeter;
    }

    public Vector3 GetCenter()
    {
        if (vertices.Count == 0) return Vector3.zero;
        
        Vector3 avg = Vector3.zero;
        foreach (var v in vertices)
        {
            avg += v;
        }
        return avg / vertices.Count;
    }
}

/// <summary>
/// Lotto - piccolo terreno che si affaccia sulla strada, contiene un edificio
/// </summary>
[System.Serializable]
public class CityLot
{
    public int id;
    public int blockID;
    public List<Vector3> vertices = new List<Vector3>();
    public Vector3 buildingCenter;
    public float buildingHeight = 5.0f;
    
    // Proprietà per lotti variabili
    public float sizeFactor = 1.0f;  // Moltiplicatore dimensione (0.6 = piccolo, 1.4 = grande)
    public float lotGap = 0.05f;     // Gap specifico per questo lotto

    // Indice del prefab assegnato in fase di generazione (-1 = non assegnato, usa PickPrefab fallback).
    public int assignedPrefabIndex = -1;

    // Rotazione world del prefab assegnato, calcolata in fase di generazione lotto.
    public Quaternion assignedSpawnRotation = Quaternion.identity;
    public bool hasAssignedSpawnRotation = false;

    public CityLot(int id, int blockID)
    {
        this.id = id;
        this.blockID = blockID;
    }

    public Vector3 GetCenter()
    {
        if (vertices.Count == 0) return buildingCenter;
        
        Vector3 avg = Vector3.zero;
        foreach (var v in vertices)
        {
            avg += v;
        }
        return avg / vertices.Count;
    }
}

}
