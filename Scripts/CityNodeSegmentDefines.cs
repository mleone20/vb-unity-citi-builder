using UnityEngine;
using System.Collections.Generic;

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
    public List<GameObject> buildingPrefabs = new List<GameObject>();
    public bool deterministicPrefabSelection = true;
    public int prefabSelectionSeed = 0;

    public string GetDisplayName()
    {
        return string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    }
}

public enum CitySegmentGeometryType
{
    Straight,
    Bezier
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
