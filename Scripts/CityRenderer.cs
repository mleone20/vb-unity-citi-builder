using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Utility statico per disegnare la città tramite Gizmos e Handles.
/// Visualizza strade, blocchi ed edifici direttamente nella Scene View.
/// </summary>
public static class CityRenderer
{
    private const float NODE_BASE_SIZE = 0.35f;
    private const float NODE_MIN_SIZE = 0.12f;
    private const float NODE_MAX_SIZE = 1.5f;
    private const float ROAD_THICKNESS = 8f;
    private const float BUILDING_WIREFRAME_THICKNESS = 2f;

    // ========== DISEGNO STRADE ==========

    public static void DrawRoads(CityData cityData, int selectedNodeID = -1)
    {
        if (cityData == null) return;

        // Disegna segmenti (strade)
        DrawSegments(cityData);

        // Disegna nodi
        DrawNodes(cityData, selectedNodeID);
    }

    private static void DrawSegments(CityData cityData)
    {
        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.8f); // Grigio

        foreach (var segment in cityData.segments)
        {
            if (segment == null)
            {
                continue;
            }

            CityNode nodeA = cityData.GetNode(segment.nodeA_ID);
            CityNode nodeB = cityData.GetNode(segment.nodeB_ID);

            if (nodeA == null || nodeB == null)
            {
                DrawBrokenSegmentError(segment, nodeA, nodeB);
                continue;
            }

            Vector3 posA = nodeA.position;
            Vector3 posB = nodeB.position;

            // Disegna segmento come parallelogramma 3D (strada)
            DrawRoadSegment(posA, posB, segment.width);
        }
    }

    private static void DrawRoadSegment(Vector3 posA, Vector3 posB, float width)
    {
        Vector3 direction = (posB - posA).normalized;
        Vector3 perpendicular = new Vector3(-direction.z, 0, direction.x).normalized * (width / 2f);

        // 4 angoli della strada
        Vector3 p1 = posA - perpendicular;
        Vector3 p2 = posA + perpendicular;
        Vector3 p3 = posB + perpendicular;
        Vector3 p4 = posB - perpendicular;

        // Disegna come wireframe rettangolo
        Gizmos.DrawLine(p1, p2);
        Gizmos.DrawLine(p2, p3);
        Gizmos.DrawLine(p3, p4);
        Gizmos.DrawLine(p4, p1);

        // Linea centrale per maggior chiarezza
        Gizmos.color = new Color(0.3f, 0.3f, 0.3f, 0.5f);
        Gizmos.DrawLine(posA, posB);
        Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.8f);
    }

    private static void DrawNodes(CityData cityData, int selectedNodeID)
    {
        foreach (var node in cityData.nodes)
        {
            float adaptiveSize = GetAdaptiveNodeSize(node.position);

            if (node.id == selectedNodeID)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(node.position, Vector3.one * (adaptiveSize * 1.6f));
                Gizmos.color = Color.yellow;
                Gizmos.DrawCube(node.position, Vector3.one * adaptiveSize);
            }
            else
            {
                Gizmos.color = Color.white;
                Gizmos.DrawCube(node.position, Vector3.one * adaptiveSize);
            }

            DrawNodeIdLabel(node.id, node.position, adaptiveSize);
            DrawNodeBrokenLinkError(node, cityData, adaptiveSize);
        }
    }

    private static void DrawBrokenSegmentError(CitySegment segment, CityNode nodeA, CityNode nodeB)
    {
        Vector3 markerPosition = Vector3.zero;

        if (nodeA != null)
        {
            markerPosition = nodeA.position;
        }
        else if (nodeB != null)
        {
            markerPosition = nodeB.position;
        }

        float markerSize = GetAdaptiveNodeSize(markerPosition) * 1.3f;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(markerPosition, markerSize);

#if UNITY_EDITOR
        Handles.color = Color.red;
        Handles.Label(markerPosition + Vector3.up * (markerSize * 1.2f),
            $"ERR SEG {segment.id} (A:{segment.nodeA_ID} B:{segment.nodeB_ID})");
#endif
    }

    private static void DrawNodeBrokenLinkError(CityNode node, CityData cityData, float adaptiveSize)
    {
        if (node == null || cityData == null)
        {
            return;
        }

        bool hasBrokenLinks = false;
        foreach (int segId in node.connectedSegmentIDs)
        {
            CitySegment seg = cityData.GetSegment(segId);
            if (seg == null)
            {
                hasBrokenLinks = true;
                break;
            }

            bool nodeIsEndpoint = seg.nodeA_ID == node.id || seg.nodeB_ID == node.id;
            if (!nodeIsEndpoint)
            {
                hasBrokenLinks = true;
                break;
            }
        }

        if (!hasBrokenLinks)
        {
            return;
        }

        Gizmos.color = Color.red;
        Gizmos.DrawWireCube(node.position, Vector3.one * (adaptiveSize * 2.0f));

#if UNITY_EDITOR
        Handles.color = Color.red;
        Handles.Label(node.position + Vector3.up * (adaptiveSize * 2.0f), $"ERR NODE {node.id}");
#endif
    }

#if UNITY_EDITOR
    private static void DrawNodeIdLabel(int nodeId, Vector3 nodePosition, float adaptiveSize)
    {
        Handles.color = Color.white;
        Vector3 labelPos = nodePosition + Vector3.up * (adaptiveSize * 1.4f);
        Handles.Label(labelPos, nodeId.ToString());
    }
#else
    private static void DrawNodeIdLabel(int nodeId, Vector3 nodePosition, float adaptiveSize) { }
#endif

    private static float GetAdaptiveNodeSize(Vector3 worldPosition)
    {
        Camera cam = Camera.current;
        if (cam == null)
        {
            return NODE_BASE_SIZE;
        }

        float distance = Vector3.Distance(cam.transform.position, worldPosition);
        float scaledSize = distance * 0.03f;
        return Mathf.Clamp(scaledSize, NODE_MIN_SIZE, NODE_MAX_SIZE);
    }

    // ========== DISEGNO BLOCCHI ==========

    public static void DrawBlocks(CityData cityData)
    {
        if (cityData == null) return;

        foreach (var block in cityData.blocks)
        {
            DrawSingleBlock(block, cityData);
        }
    }

    private static void DrawSingleBlock(CityBlock block, CityData cityData)
    {
        if (block.vertices.Count < 3) return;

        Color blockColor = cityData.GetZoneColor(block.zoning);
        Color outlineColor = new Color(0, 0, 0, 0.5f);

        // Disegna outline del blocco
        Gizmos.color = outlineColor;
        for (int i = 0; i < block.vertices.Count; i++)
        {
            Vector3 v1 = block.vertices[i];
            Vector3 v2 = block.vertices[(i + 1) % block.vertices.Count];
            Gizmos.DrawLine(v1, v2);
        }

        // Disegna area riempita (tramite overlay sottile)
        DrawFilledPolygon2D(block.vertices, blockColor * 0.4f);

        // Disegna centro blocco
        Vector3 center = block.GetCenter();
        Gizmos.color = blockColor;
        Gizmos.DrawCube(center, Vector3.one * 0.2f);
        DrawBlockIdLabel(block.id, center);
    }

#if UNITY_EDITOR
    private static void DrawBlockIdLabel(int blockId, Vector3 center)
    {
        Handles.color = Color.white;
        Handles.Label(center + Vector3.up * 0.3f, "B" + blockId);
    }
#else
    private static void DrawBlockIdLabel(int blockId, Vector3 center) { }
#endif

    private static void DrawFilledPolygon2D(List<Vector3> vertices, Color color)
    {
        // Semplice triangolazione from center
        if (vertices.Count < 3) return;

        Vector3 center = Vector3.zero;
        foreach (var v in vertices) center += v;
        center /= vertices.Count;

        Gizmos.color = color;
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 v1 = vertices[i];
            Vector3 v2 = vertices[(i + 1) % vertices.Count];

            // Draw thin triangle
            Gizmos.DrawLine(center, v1);
            Gizmos.DrawLine(center, v2);
        }
    }

    // ========== DISEGNO EDIFICI ==========

    public static void DrawBuildings(CityData cityData)
    {
        if (cityData == null) return;

        foreach (var lot in cityData.lots)
        {
            DrawSingleBuilding(lot, cityData);
        }
    }

    private static void DrawSingleBuilding(CityLot lot, CityData cityData)
    {
        CityBlock block = cityData.GetBlock(lot.blockID);
        if (block == null) return;

        Color buildingColor = cityData.GetZoneColor(block.zoning);
        float height = lot.buildingHeight * cityData.buildingScale;

        Vector3 center = lot.buildingCenter + Vector3.up * (height / 2f);
        Vector3 size = new Vector3(5f, height, 5f);

        // Disegna cubo solido
        Gizmos.color = buildingColor;
        DrawCube(center, size);

        // Disegna outline wireframe
        Gizmos.color = buildingColor * 0.6f;
        DrawWireCube(center, size);
    }

    // ========== UTILITY DRAWING FUNCTIONS ==========

    private static void DrawCube(Vector3 center, Vector3 size)
    {
        // Semplice: usa Gizmos.DrawCube
        Gizmos.DrawCube(center, size);
    }

    private static void DrawWireCube(Vector3 center, Vector3 size)
    {
        Gizmos.DrawWireCube(center, size);
    }

#if UNITY_EDITOR
    /// <summary>
    /// Disegna con Handles per scene view più ricca (usato negli Editor script).
    /// </summary>
    public static void DrawRoadsWithHandles(CityData cityData, int selectedNodeID = -1)
    {
        if (cityData == null) return;

        // Disegna segmenti con spessore via Handles
        Handles.color = Color.gray;
        foreach (var segment in cityData.segments)
        {
            CityNode nodeA = cityData.GetNode(segment.nodeA_ID);
            CityNode nodeB = cityData.GetNode(segment.nodeB_ID);

            if (nodeA == null || nodeB == null) continue;

            Vector3[] points = { nodeA.position, nodeB.position };
            Handles.DrawAAPolyLine(ROAD_THICKNESS, points);
        }

        // Disegna nodi
        foreach (var node in cityData.nodes)
        {
            float adaptiveSize = GetAdaptiveNodeSize(node.position);

            if (node.id == selectedNodeID)
            {
                Handles.color = Color.yellow;
            }
            else
            {
                Handles.color = Color.white;
            }
            Handles.DrawSolidDisc(node.position, Vector3.up, adaptiveSize * 0.5f);
        }
    }

    /// <summary>
    /// Disegna blocchi con Handles (più ricco, per editor window).
    /// </summary>
    public static void DrawBlocksWithHandles(CityData cityData)
    {
        if (cityData == null) return;

        foreach (var block in cityData.blocks)
        {
            if (block.vertices.Count < 3) continue;

            Color blockColor = cityData.GetZoneColor(block.zoning);
            Handles.color = blockColor;

            // Disegna outline
            for (int i = 0; i < block.vertices.Count; i++)
            {
                Vector3 v1 = block.vertices[i];
                Vector3 v2 = block.vertices[(i + 1) % block.vertices.Count];
                Handles.DrawLine(v1, v2);
            }

            // Disegna centro etichettato
            Vector3 center = block.GetCenter();
            Handles.Label(center, $"Block {block.id} - {block.zoning}");
        }
    }
#endif
}
