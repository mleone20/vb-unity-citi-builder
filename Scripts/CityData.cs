using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject che memorizza tutti i dati della città (nodi, strade, blocchi, lotti).
/// Questo asset è versionabile e persistente per il progetto.
/// </summary>
public class CityData : ScriptableObject
{
    [Header("Rete Stradale")]
    [SerializeField] public List<CityNode> nodes = new List<CityNode>();
    [SerializeField] public List<CitySegment> segments = new List<CitySegment>();
    
    [Header("Blocchi e Zoning")]
    [SerializeField] public List<CityBlock> blocks = new List<CityBlock>();
    
    [Header("Lotti e Edifici")]
    [SerializeField] public List<CityLot> lots = new List<CityLot>();
    
    [Header("Parametri Globali")]
    [Range(1f, 10f)] [SerializeField] public float globalRoadWidth = 3.0f;
    [Range(10f, 100f)] [SerializeField] public float averageLotSize = 30.0f; 
    // Counter per generare ID unici
    private int nextNodeID = 0;
    private int nextSegmentID = 0;
    private int nextBlockID = 0;
    private int nextLotID = 0;

    /// <summary>
    /// Clona questo CityData (per salvataggi backup)
    /// </summary>
    public CityData Clone()
    {
        CityData clone = ScriptableObject.CreateInstance<CityData>();
        
        clone.globalRoadWidth = this.globalRoadWidth;
        clone.averageLotSize = this.averageLotSize; 
        
        // Deep clone collections
        foreach (var node in nodes)
        {
            clone.nodes.Add(new CityNode(node.id, node.position) { connectedSegmentIDs = new List<int>(node.connectedSegmentIDs) });
        }
        
        foreach (var seg in segments)
        {
            clone.segments.Add(new CitySegment(seg.id, seg.nodeA_ID, seg.nodeB_ID, seg.width));
        }
        
        foreach (var block in blocks)
        {
            CityBlock clonedBlock = new CityBlock(block.id)
            {
                zoning = block.zoning,
                vertices = new List<Vector3>(block.vertices),
                lotIDs = new List<int>(block.lotIDs)
            };
            clone.blocks.Add(clonedBlock);
        }
        
        foreach (var lot in lots)
        {
            CityLot clonedLot = new CityLot(lot.id, lot.blockID)
            {
                vertices = new List<Vector3>(lot.vertices),
                buildingCenter = lot.buildingCenter,
                buildingHeight = lot.buildingHeight
            };
            clone.lots.Add(clonedLot);
        }
        
        clone.nextNodeID = this.nextNodeID;
        clone.nextSegmentID = this.nextSegmentID;
        clone.nextBlockID = this.nextBlockID;
        clone.nextLotID = this.nextLotID;
        
        return clone;
    }

    /// <summary>
    /// Cancella tutti i dati della città
    /// </summary>
    public void Clear()
    {
        nodes.Clear();
        segments.Clear();
        blocks.Clear();
        lots.Clear();
        nextNodeID = 0;
        nextSegmentID = 0;
        nextBlockID = 0;
        nextLotID = 0;
    }

    // ========== GETTERS ==========
    
    public CityNode GetNode(int nodeID)
    {
        return nodes.Find(n => n.id == nodeID);
    }

    public CitySegment GetSegment(int segmentID)
    {
        return segments.Find(s => s.id == segmentID);
    }

    public CityBlock GetBlock(int blockID)
    {
        return blocks.Find(b => b.id == blockID);
    }

    public CityLot GetLot(int lotID)
    {
        return lots.Find(l => l.id == lotID);
    }

    public float GetZoneHeight(ZoneType zone)
    {
        if (zone != null)
        {
            return Mathf.Max(0.1f, zone.buildingHeight);
        }

        return 5.0f;
    }

    public Color GetZoneColor(ZoneType zone)
    {
        return zone != null ? zone.zoneColor : Color.white;
    }

    // ========== ID GENERATION ==========
    
    public int GetNextNodeID() => nextNodeID++;
    public int GetNextSegmentID() => nextSegmentID++;
    public int GetNextBlockID() => nextBlockID++;
    public int GetNextLotID() => nextLotID++;

    // ========== QUERY HELPER ==========
    
    public CityNode FindNearestNode(Vector3 position, float threshold = 1.0f)
    {
        CityNode nearest = null;
        float minDistance = threshold;

        foreach (var node in nodes)
        {
            float dist = Vector3.Distance(node.position, position);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearest = node;
            }
        }

        return nearest;
    }

    public CityBlock FindBlockAtPosition(Vector3 position)
    {
        // Semplice check: punto interno al poligono 2D (proiezione XZ)
        foreach (var block in blocks)
        {
            if (IsPointInPolygon(position, block.vertices))
            {
                return block;
            }
        }
        return null;
    }

    private bool IsPointInPolygon(Vector3 point, List<Vector3> polygon)
    {
        if (polygon.Count < 3) return false;

        int count = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector3 v1 = polygon[i];
            Vector3 v2 = polygon[(i + 1) % polygon.Count];

            if ((v1.z <= point.z && point.z < v2.z) || (v2.z <= point.z && point.z < v1.z))
            {
                float xIntersect = v1.x + (point.z - v1.z) / (v2.z - v1.z) * (v2.x - v1.x);
                if (point.x < xIntersect)
                {
                    count++;
                }
            }
        }

        return count % 2 == 1;
    }
}
