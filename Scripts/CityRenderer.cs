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

    // ── LOD thresholds ─────────────────────────────────────────────────────
    // Oltre queste distanze dalla camera alcuni elementi vengono nascosti.
    private const float SEG_DETAIL_MAX_DIST  = 400f;   // outline doppio + curve sampling
    private const float NODE_DRAW_MAX_DIST   = 600f;   // nodi visibili
    private const float NODE_LABEL_MAX_DIST  = 80f;    // label ID visibili
    private const float BLOCK_LABEL_MAX_DIST = 200f;   // label blocco visibili
    private const float ERROR_CHECK_MAX_DIST = 300f;   // controllo broken link

    // ========== DISEGNO STRADE ==========

    public static void DrawRoads(CityData cityData, int selectedNodeID = -1, int selectedSegmentID = -1)
    {
        if (cityData == null) return;

        // Disegna segmenti (strade)
        DrawSegments(cityData, selectedSegmentID);

        // Disegna nodi
        DrawNodes(cityData, selectedNodeID);
    }

    private static void DrawSegments(CityData cityData, int selectedSegmentID)
    {
#if UNITY_EDITOR
        Camera cam = Camera.current;
        Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
        Plane[] frustum = cam != null ? GeometryUtility.CalculateFrustumPlanes(cam) : null;

        // Raccolta batch: linee centrali per tutti i segmenti lontani/normali
        // Usiamo liste separate per colore per ridurre i cambio-stato.
        var batchCenterLines = new System.Collections.Generic.Dictionary<Color, List<Vector3>>();

        foreach (var segment in cityData.segments)
        {
            if (segment == null) continue;

            CityNode nodeA = cityData.GetNode(segment.nodeA_ID);
            CityNode nodeB = cityData.GetNode(segment.nodeB_ID);

            if (nodeA == null || nodeB == null)
            {
                // Errori: solo se vicini alla camera
                float errDist = cam != null ? Vector3.Distance(camPos, nodeA != null ? nodeA.position : (nodeB != null ? nodeB.position : Vector3.zero)) : 0f;
                if (errDist < ERROR_CHECK_MAX_DIST)
                    DrawBrokenSegmentError(segment, nodeA, nodeB);
                continue;
            }

            // Frustum culling rapido sull'AABB del segmento
            if (frustum != null)
            {
                Bounds segBounds = new Bounds((nodeA.position + nodeB.position) * 0.5f, Vector3.zero);
                segBounds.Encapsulate(nodeA.position);
                segBounds.Encapsulate(nodeB.position);
                if (!GeometryUtility.TestPlanesAABB(frustum, segBounds)) continue;
            }

            bool isSelected = segment.id == selectedSegmentID;
            float midDist = cam != null
                ? Vector3.Distance(camPos, (nodeA.position + nodeB.position) * 0.5f)
                : 0f;

            if (isSelected || midDist < SEG_DETAIL_MAX_DIST)
            {
                // Modalità dettaglio: sample curva + outline doppio
                DrawRoadSegmentDetail(cityData, segment, isSelected);
            }
            else
            {
                // Modalità LOD: semplice linea centrale, accumulata nel batch
                Color c = CityRoadGeometry.GetRoadColor(segment);
                if (!batchCenterLines.TryGetValue(c, out var lst))
                {
                    lst = new List<Vector3>();
                    batchCenterLines[c] = lst;
                }
                lst.Add(nodeA.position);
                lst.Add(nodeB.position);
            }
        }

        // Flush batch (una sola chiamata Handles.DrawLines per colore)
        foreach (var kv in batchCenterLines)
        {
            Handles.color = kv.Key;
            Handles.DrawLines(kv.Value.ToArray());
        }
#else
        foreach (var segment in cityData.segments)
        {
            if (segment == null) continue;
            CityNode nodeA = cityData.GetNode(segment.nodeA_ID);
            CityNode nodeB = cityData.GetNode(segment.nodeB_ID);
            if (nodeA == null || nodeB == null) continue;
            bool isSelected = segment.id == selectedSegmentID;
            DrawRoadSegmentDetail(cityData, segment, isSelected);
        }
#endif
    }

    private static void DrawRoadSegmentDetail(CityData cityData, CitySegment segment, bool isSelected)
    {
        List<Vector3> sampledPoints = CityRoadGeometry.SampleSegment(cityData, segment, CityRoadGeometry.DefaultCurveSamples);
        if (sampledPoints.Count < 2)
        {
            return;
        }

        float width = CityRoadGeometry.GetRoadWidth(cityData, segment);
        Color roadColor = CityRoadGeometry.GetRoadColor(segment);
        Color borderColor = isSelected ? Color.yellow : roadColor;

        for (int i = 1; i < sampledPoints.Count; i++)
        {
            Vector3 posA = sampledPoints[i - 1];
            Vector3 posB = sampledPoints[i];
            Vector3 direction = (posB - posA);
            if (direction.sqrMagnitude < 0.0001f)
            {
                continue;
            }

            direction.Normalize();
            Vector3 perpendicular = new Vector3(-direction.z, 0f, direction.x).normalized * (width / 2f);
            Vector3 leftA = posA - perpendicular;
            Vector3 rightA = posA + perpendicular;
            Vector3 leftB = posB - perpendicular;
            Vector3 rightB = posB + perpendicular;

            Gizmos.color = borderColor;
            Gizmos.DrawLine(leftA, leftB);
            Gizmos.DrawLine(rightA, rightB);

            if (i == 1)
            {
                Gizmos.DrawLine(leftA, rightA);
            }

            if (i == sampledPoints.Count - 1)
            {
                Gizmos.DrawLine(leftB, rightB);
            }

            Gizmos.color = Color.Lerp(roadColor, Color.black, 0.35f);
            Gizmos.DrawLine(posA, posB);
        }
    }

    private static void DrawNodes(CityData cityData, int selectedNodeID)
    {
#if UNITY_EDITOR
        Camera cam = Camera.current;
        Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
        Plane[] frustum = cam != null ? GeometryUtility.CalculateFrustumPlanes(cam) : null;

        // Batch: nodi normali (bianchi) disegnati come coppie di linee incrociate
        var normalDots = new List<Vector3>();
        float dotHalf = NODE_MIN_SIZE * 0.6f;

        foreach (var node in cityData.nodes)
        {
            float dist = cam != null ? Vector3.Distance(camPos, node.position) : 0f;

            // LOD: salta nodi troppo lontani
            if (dist > NODE_DRAW_MAX_DIST && node.id != selectedNodeID) continue;

            // Frustum culling
            if (frustum != null)
            {
                Bounds nb = new Bounds(node.position, Vector3.one * 2f);
                if (!GeometryUtility.TestPlanesAABB(frustum, nb)) continue;
            }

            float adaptiveSize = GetAdaptiveNodeSize(node.position);

            if (node.id == selectedNodeID)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireCube(node.position, Vector3.one * (adaptiveSize * 1.6f));
                Gizmos.DrawCube(node.position, Vector3.one * adaptiveSize);
                DrawNodeIdLabel(node.id, node.position, adaptiveSize, true);
                if (dist < ERROR_CHECK_MAX_DIST)
                    DrawNodeBrokenLinkError(node, cityData, adaptiveSize);
            }
            else if (dist < NODE_LABEL_MAX_DIST)
            {
                // Vicino: cubo dettagliato + label
                Gizmos.color = Color.white;
                Gizmos.DrawCube(node.position, Vector3.one * adaptiveSize);
                DrawNodeIdLabel(node.id, node.position, adaptiveSize, false);
                if (dist < ERROR_CHECK_MAX_DIST)
                    DrawNodeBrokenLinkError(node, cityData, adaptiveSize);
            }
            else
            {
                // Lontano: accumulato nel batch come incrocio di due linee
                float h = adaptiveSize * 0.5f;
                normalDots.Add(node.position - Vector3.right * h);
                normalDots.Add(node.position + Vector3.right * h);
                normalDots.Add(node.position - Vector3.forward * h);
                normalDots.Add(node.position + Vector3.forward * h);
            }
        }

        // Flush batch nodi normali
        if (normalDots.Count > 0)
        {
            Handles.color = new Color(0.9f, 0.9f, 0.9f, 0.7f);
            Handles.DrawLines(normalDots.ToArray());
        }
#else
        foreach (var node in cityData.nodes)
        {
            float adaptiveSize = GetAdaptiveNodeSize(node.position);
            bool sel = node.id == selectedNodeID;
            Gizmos.color = sel ? Color.yellow : Color.white;
            Gizmos.DrawCube(node.position, Vector3.one * adaptiveSize);
        }
#endif
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
    private static void DrawNodeIdLabel(int nodeId, Vector3 nodePosition, float adaptiveSize, bool isSelected)
    {
        Handles.color = isSelected ? Color.red : Color.white;
        Vector3 labelPos = nodePosition + Vector3.up * (adaptiveSize * 1.4f);
        Handles.Label(labelPos, nodeId.ToString());
    }
#else
    private static void DrawNodeIdLabel(int nodeId, Vector3 nodePosition, float adaptiveSize, bool isSelected) { }
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

    public static void DrawBlocks(CityData cityData, int selectedBlockID = -1)
    {
        if (cityData == null) return;

#if UNITY_EDITOR
        Camera cam = Camera.current;
        Vector3 camPos = cam != null ? cam.transform.position : Vector3.zero;
        Plane[] frustum = cam != null ? GeometryUtility.CalculateFrustumPlanes(cam) : null;

        foreach (var block in cityData.blocks)
        {
            if (block == null || block.vertices.Count < 3) continue;

            // Frustum culling sul centro del blocco
            Vector3 center = block.GetCenter();
            if (frustum != null)
            {
                Bounds bb = new Bounds(center, Vector3.one * 5f);
                if (!GeometryUtility.TestPlanesAABB(frustum, bb)) continue;
            }

            float dist = cam != null ? Vector3.Distance(camPos, center) : 0f;
            bool isSelected = block.id == selectedBlockID;
            DrawSingleBlock(block, cityData, isSelected, dist);
        }
#else
        foreach (var block in cityData.blocks)
            DrawSingleBlock(block, cityData, block != null && block.id == selectedBlockID, 0f);
#endif
    }

    private static void DrawSingleBlock(CityBlock block, CityData cityData, bool isSelected, float distFromCamera = 0f)
    {
        if (block.vertices.Count < 3) return;

        Color blockColor = cityData.GetZoneColor(block.zoning);
        Color outlineColor = new Color(0, 0, 0, 0.5f);

        if (isSelected)
        {
            Color selectedFill = new Color(blockColor.r, blockColor.g, blockColor.b, 0.35f);
            DrawSolidBlockArea(block.vertices, selectedFill);
        }

        // Disegna outline del blocco
        Gizmos.color = outlineColor;
        for (int i = 0; i < block.vertices.Count; i++)
        {
            Vector3 v1 = block.vertices[i];
            Vector3 v2 = block.vertices[(i + 1) % block.vertices.Count];
            Gizmos.DrawLine(v1, v2);
        }

        if (isSelected)
        {
            Gizmos.color = Color.Lerp(blockColor, Color.white, 0.2f);
            for (int i = 0; i < block.vertices.Count; i++)
            {
                Vector3 v1 = block.vertices[i];
                Vector3 v2 = block.vertices[(i + 1) % block.vertices.Count];
                Gizmos.DrawLine(v1, v2);
            }
        }

        // Disegna area riempita (tramite overlay sottile)
        DrawFilledPolygon2D(block.vertices, blockColor * 0.4f);

        // Disegna centro blocco
        Vector3 center = block.GetCenter();
        Gizmos.color = blockColor;
        Gizmos.DrawCube(center, Vector3.one * 0.2f);
        if (isSelected || distFromCamera < BLOCK_LABEL_MAX_DIST)
            DrawBlockIdLabel(block.id, center, isSelected);

        if (isSelected)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(center, 0.45f);
        }
    }

#if UNITY_EDITOR
    private static void DrawBlockIdLabel(int blockId, Vector3 center, bool isSelected)
    {
        Handles.color = isSelected ? Color.red : Color.white;
        Handles.Label(center + Vector3.up * 0.3f, "B" + blockId);
    }
#else
    private static void DrawBlockIdLabel(int blockId, Vector3 center, bool isSelected) { }
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

    public static void DrawBuildings(CityData cityData, int selectedLotID = -1)
    {
        if (cityData == null) return;

        foreach (var lot in cityData.lots)
        {
            DrawSingleBuilding(lot, cityData, lot != null && lot.id == selectedLotID);
        }
    }

    private static void DrawSingleBuilding(CityLot lot, CityData cityData, bool isSelected)
    {
        CityBlock block = cityData.GetBlock(lot.blockID);
        if (block == null) return;

        Color buildingColor = cityData.GetZoneColor(block.zoning);
        float height = lot.buildingHeight;

        // Disegna outline del lotto al suolo
        if (lot.vertices != null && lot.vertices.Count >= 4)
        {
            if (isSelected)
            {
                Color selectedFill = new Color(buildingColor.r, buildingColor.g, buildingColor.b, 0.35f);
                DrawSolidLotArea(lot.vertices, selectedFill);
            }

            Gizmos.color = buildingColor * 0.35f;
            for (int i = 0; i < lot.vertices.Count; i++)
            {
                Gizmos.DrawLine(lot.vertices[i], lot.vertices[(i + 1) % lot.vertices.Count]);
            }

            if (isSelected)
            {
                Gizmos.color = Color.Lerp(buildingColor, Color.white, 0.2f);
                for (int i = 0; i < lot.vertices.Count; i++)
                {
                    Gizmos.DrawLine(lot.vertices[i], lot.vertices[(i + 1) % lot.vertices.Count]);
                }
            }

            // Calcola frame locale dal lotto (frontSinistra[0], frontDestra[1], retroDestra[2], retroSinistra[3])
            Vector3 frontL = lot.vertices[0];
            Vector3 frontR = lot.vertices[1];
            Vector3 backR  = lot.vertices[2];
            Vector3 backL  = lot.vertices[3];

            float lotWidth = Vector3.Distance(frontL, frontR);
            float lotDepth = Vector3.Distance(frontL, backL);

            // Margine interno per non coprire l'intero lotto
            float mW = Mathf.Min(0.6f, lotWidth * 0.08f);
            float mD = Mathf.Min(0.6f, lotDepth * 0.08f);
            float buildingW = Mathf.Max(1f, lotWidth - mW * 2f);
            float buildingD = Mathf.Max(1f, lotDepth - mD * 2f);

            // Direzione forward = dall'asse frontale verso il retro
            Vector3 lotForward = ((backL + backR) * 0.5f - (frontL + frontR) * 0.5f).normalized;
            if (lotForward.sqrMagnitude < 0.001f) lotForward = Vector3.forward;

            Quaternion rotation = Quaternion.LookRotation(lotForward, Vector3.up);
            Vector3 buildingCenter3D = lot.buildingCenter + Vector3.up * (height * 0.5f);

            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(buildingCenter3D, rotation, Vector3.one);

            Vector3 bSize = new Vector3(buildingW, height, buildingD);
            Gizmos.color = new Color(buildingColor.r, buildingColor.g, buildingColor.b, 0.25f);
            Gizmos.DrawWireCube(Vector3.zero, bSize);

            Gizmos.matrix = oldMatrix;
        }
        else
        {
            // Fallback per lotti legacy senza vertices
            Vector3 center = lot.buildingCenter + Vector3.up * (height * 0.5f);
            Gizmos.color = new Color(buildingColor.r, buildingColor.g, buildingColor.b, 0.25f);
            DrawWireCube(center, new Vector3(5f, height, 5f));
        }
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
    private static void DrawSolidBlockArea(List<Vector3> vertices, Color color)
    {
        if (vertices == null || vertices.Count < 3)
        {
            return;
        }

        Color previous = Handles.color;
        Handles.color = color;
        Handles.DrawAAConvexPolygon(vertices.ToArray());
        Handles.color = previous;
    }

    private static void DrawSolidLotArea(List<Vector3> vertices, Color color)
    {
        if (vertices == null || vertices.Count < 3)
        {
            return;
        }

        Color previous = Handles.color;
        Handles.color = color;
        Handles.DrawAAConvexPolygon(vertices.ToArray());
        Handles.color = previous;
    }
#endif

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
