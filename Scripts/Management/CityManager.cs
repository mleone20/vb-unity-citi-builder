using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// MonoBehaviour che gestisce la città nella scena.
/// Detiene il riferimento a CityData e coordina le operazioni di costruzione.
/// Fornisce visualizzazione tramite Gizmos nei metodi OnDrawGizmos/OnDrawGizmosSelected.
/// </summary>
public class CityManager : MonoBehaviour
{
    [Header("Dati Persistenti")]
    [SerializeField] private CityData cityData;

    [Header("Stato Editor")]
    [SerializeField] private BuildMode currentMode = BuildMode.Idle;
    [SerializeField] private int selectedNodeID = -1;
    [SerializeField] private int selectedSegmentID = -1;
    [SerializeField] private int selectedBlockID = -1;
    [SerializeField] private int selectedLotID = -1;

    public enum BuildMode
    {
        Idle,           // Nessuna modalità attiva
        AddNodes,       // Cliccare per aggiungere nodi
        ConnectNodes,   // Cliccare due nodi per collegarli
        AssignZoning,   // Cliccare blocchi per assegnare zona
        CreateBlock     // Cliccare nodi per creare un blocco manualmente
    }

    // ========== LIFECYCLE ==========

    private void OnEnable()
    {
        if (cityData == null)
        {
            Debug.LogWarning("[CityManager] CityData non assegnato! Crea un asset CityData e assegnalo nell'Inspector.");
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (cityData == null) return;
        CityRenderer.DrawRoads(cityData, selectedNodeID, selectedSegmentID);
        CityRenderer.DrawBlocks(cityData, selectedBlockID);
        CityRenderer.DrawBuildings(cityData, selectedLotID);
    }
#endif

#if UNITY_EDITOR
    private void RecordCityDataUndo(string actionName)
    {
        if (cityData != null)
        {
            UnityEditor.Undo.RecordObject(cityData, actionName);
        }
    }
#endif

    // ========== PUBLIC API - Nodi ==========

    public CityNode AddNode(Vector3 position)
    {
        if (cityData == null)
        {
            Debug.LogError("[CityManager] CityData è null, non posso aggiungere nodo!");
            return null;
        }

#if UNITY_EDITOR
        RecordCityDataUndo("Add City Node");
#endif

        int nodeID = cityData.GetNextNodeID();
        CityNode newNode = new CityNode(nodeID, position);
        cityData.nodes.Add(newNode);

        Debug.Log($"[CityManager] Nodo aggiunto: ID={nodeID}, Pos={position}");
        return newNode;
    }

    public void RemoveNode(int nodeID)
    {
        if (cityData == null) return;

        CityNode node = cityData.GetNode(nodeID);
        if (node == null) return;

#if UNITY_EDITOR
        RecordCityDataUndo("Remove City Node");
#endif

        // Rimuovi tutti i segmenti collegati e pulisci i riferimenti sugli altri nodi.
        for (int i = cityData.segments.Count - 1; i >= 0; i--)
        {
            var seg = cityData.segments[i];
            if (seg.nodeA_ID == nodeID || seg.nodeB_ID == nodeID)
            {
                CityNode otherA = cityData.GetNode(seg.nodeA_ID);
                CityNode otherB = cityData.GetNode(seg.nodeB_ID);

                if (otherA != null)
                {
                    otherA.connectedSegmentIDs.Remove(seg.id);
                }

                if (otherB != null)
                {
                    otherB.connectedSegmentIDs.Remove(seg.id);
                }

                cityData.segments.RemoveAt(i);
            }
        }

        cityData.nodes.Remove(node);
        Debug.Log($"[CityManager] Nodo rimosso: ID={nodeID}");
    }

    public CityNode GetNode(int nodeID)
    {
        return cityData?.GetNode(nodeID);
    }

    public CityNode FindNearestNode(Vector3 position, float threshold = 1.0f)
    {
        return cityData?.FindNearestNode(position, threshold);
    }

    public CitySegment FindNearestSegment(Vector3 position, float threshold = 1.5f)
    {
        return cityData?.FindNearestSegment(position, threshold);
    }

    // ========== PUBLIC API - Segmenti ==========

    public CitySegment AddSegment(int nodeA_ID, int nodeB_ID)
    {
        if (cityData == null)
        {
            Debug.LogError("[CityManager] CityData è null, non posso aggiungere segmento!");
            return null;
        }

        CityNode nodeA = cityData.GetNode(nodeA_ID);
        CityNode nodeB = cityData.GetNode(nodeB_ID);

        if (nodeA == null || nodeB == null)
        {
            Debug.LogError($"[CityManager] Uno dei nodi non esiste: A={nodeA_ID}, B={nodeB_ID}");
            return null;
        }

        // Evita segmenti duplicati
        var existing = cityData.segments.Find(s => 
            (s.nodeA_ID == nodeA_ID && s.nodeB_ID == nodeB_ID) ||
            (s.nodeA_ID == nodeB_ID && s.nodeB_ID == nodeA_ID)
        );

        if (existing != null)
        {
            Debug.LogWarning($"[CityManager] Segmento tra {nodeA_ID} e {nodeB_ID} già esiste!");
            return existing;
        }

    #if UNITY_EDITOR
        RecordCityDataUndo("Add City Segment");
    #endif

        int segmentID = cityData.GetNextSegmentID();
        float width = cityData.defaultRoadProfile != null
            ? Mathf.Max(0.5f, cityData.defaultRoadProfile.roadWidth)
            : cityData.globalRoadWidth;
        CitySegment newSegment = new CitySegment(segmentID, nodeA_ID, nodeB_ID, width)
        {
            roadProfile = cityData.defaultRoadProfile
        };
        CityRoadGeometry.ResetBezierHandles(cityData, newSegment);
        
        cityData.segments.Add(newSegment);
        nodeA.connectedSegmentIDs.Add(segmentID);
        nodeB.connectedSegmentIDs.Add(segmentID);

        Debug.Log($"[CityManager] Segmento aggiunto: ID={segmentID}, tra nodi {nodeA_ID}-{nodeB_ID}");
        return newSegment;
    }

    public void RemoveSegment(int segmentID)
    {
        if (cityData == null) return;

        CitySegment segment = cityData.GetSegment(segmentID);
        if (segment == null) return;

#if UNITY_EDITOR
        RecordCityDataUndo("Remove City Segment");
#endif

        CityNode nodeA = cityData.GetNode(segment.nodeA_ID);
        CityNode nodeB = cityData.GetNode(segment.nodeB_ID);

        if (nodeA != null) nodeA.connectedSegmentIDs.Remove(segmentID);
        if (nodeB != null) nodeB.connectedSegmentIDs.Remove(segmentID);

        if (selectedSegmentID == segmentID)
        {
            selectedSegmentID = -1;
        }

        cityData.segments.Remove(segment);
        Debug.Log($"[CityManager] Segmento rimosso: ID={segmentID}");
    }

    public CitySegment GetSegment(int segmentID)
    {
        return cityData?.GetSegment(segmentID);
    }

    // ========== PUBLIC API - Blocchi ==========

    public CityBlock AddBlock(System.Collections.Generic.List<Vector3> vertices)
    {
        if (cityData == null)
        {
            Debug.LogError("[CityManager] CityData è null, non posso aggiungere blocco!");
            return null;
        }

        if (vertices == null || vertices.Count < 3)
        {
            Debug.LogError("[CityManager] Un blocco deve avere almeno 3 vertici!");
            return null;
        }

        int blockID = cityData.GetNextBlockID();
        CityBlock newBlock = new CityBlock(blockID);
        newBlock.vertices.AddRange(vertices);

        cityData.blocks.Add(newBlock);
        Debug.Log($"[CityManager] Blocco aggiunto: ID={blockID}, Area={newBlock.GetArea():F2}");
        return newBlock;
    }

    public void SetBlockZoning(int blockID, ZoneType zoning)
    {
        if (cityData == null) return;

        CityBlock block = cityData.GetBlock(blockID);
        if (block == null)
        {
            Debug.LogWarning($"[CityManager] Blocco {blockID} non trovato!");
            return;
        }

        block.zoning = zoning;
        string zoningName = zoning != null ? zoning.GetDisplayName() : "None";
        Debug.Log($"[CityManager] Zoning blocco {blockID} impostato a {zoningName}");
    }

    public CityBlock GetBlock(int blockID)
    {
        return cityData?.GetBlock(blockID);
    }

    public int GetBlockCount()
    {
        return cityData?.blocks.Count ?? 0;
    }

    // ========== PUBLIC API - Lotti ==========

    public CityLot AddLot(int blockID, System.Collections.Generic.List<Vector3> vertices, Vector3 buildingCenter, float height)
    {
        if (cityData == null) return null;

        CityBlock block = cityData.GetBlock(blockID);
        if (block == null) return null;

        int lotID = cityData.GetNextLotID();
        CityLot newLot = new CityLot(lotID, blockID)
        {
            vertices = new System.Collections.Generic.List<Vector3>(vertices),
            buildingCenter = buildingCenter,
            buildingHeight = height
        };

        cityData.lots.Add(newLot);
        block.lotIDs.Add(lotID);

        return newLot;
    }

    public CityLot GetLot(int lotID)
    {
        return cityData?.GetLot(lotID);
    }

    public int GetLotCount()
    {
        return cityData?.lots.Count ?? 0;
    }

    public int ClearAllLots()
    {
        if (cityData == null)
        {
            return 0;
        }

        int removedCount = cityData.lots.Count;
        cityData.lots.Clear();

        foreach (CityBlock block in cityData.blocks)
        {
            if (block != null)
            {
                block.lotIDs.Clear();
            }
        }

        Debug.Log($"[CityManager] Lotti rimossi: {removedCount}");
        return removedCount;
    }

    public int ClearLotsForBlock(int blockID)
    {
        if (cityData == null)
        {
            return 0;
        }

        CityBlock block = cityData.GetBlock(blockID);
        if (block == null)
        {
            return 0;
        }

        HashSet<int> lotIdsToRemove = new HashSet<int>(block.lotIDs);
        int removedCount = 0;

        for (int i = cityData.lots.Count - 1; i >= 0; i--)
        {
            CityLot lot = cityData.lots[i];
            if (lot != null && (lot.blockID == blockID || lotIdsToRemove.Contains(lot.id)))
            {
                cityData.lots.RemoveAt(i);
                removedCount++;
            }
        }

        block.lotIDs.Clear();

        Debug.Log($"[CityManager] Lotti rimossi dal blocco {blockID}: {removedCount}");
        return removedCount;
    }

    // ========== PUBLIC API - Modalità Editor ==========

    public BuildMode GetCurrentMode() => currentMode;
    public void SetMode(BuildMode mode) => currentMode = mode;

    public int GetSelectedNodeID() => selectedNodeID;
    public void SetSelectedNodeID(int nodeID)
    {
        selectedNodeID = nodeID;
        if (nodeID != -1)
        {
            selectedSegmentID = -1;
            selectedLotID = -1;
        }
    }
    public int GetSelectedSegmentID() => selectedSegmentID;
    public void SetSelectedSegmentID(int segmentID)
    {
        selectedSegmentID = segmentID;
        if (segmentID != -1)
        {
            selectedNodeID = -1;
            selectedLotID = -1;
        }
    }
    public int GetSelectedBlockID() => selectedBlockID;
    public void SetSelectedBlockID(int blockID) => selectedBlockID = blockID;
    public int GetSelectedLotID() => selectedLotID;
    public void SetSelectedLotID(int lotID)
    {
        selectedLotID = lotID;
        if (lotID != -1)
        {
            selectedNodeID = -1;
            selectedSegmentID = -1;
        }
    }

    // ========== PUBLIC API - Parametri Globali ==========

    public void SetGlobalRoadWidth(float width)
    {
        if (cityData != null)
        {
            cityData.globalRoadWidth = Mathf.Clamp(width, 1f, 10f);
            // Aggiorna solo i segmenti senza profilo, che usano il fallback globale.
            foreach (var seg in cityData.segments)
            {
                if (seg != null && seg.roadProfile == null)
                {
                    seg.width = cityData.globalRoadWidth;
                }
            }
        }
    }

    public float GetGlobalRoadWidth() => cityData?.globalRoadWidth ?? 3.0f;

    // ========== UTILITY ==========

    public void ResetCity()
    {
        if (cityData == null) return;
        if (!UnityEditor.EditorUtility.DisplayDialog("Conferma", 
            "Cancellare TUTTI i dati della città?", "Sì", "No"))
        {
            return;
        }
        
        cityData.Clear();
        selectedNodeID = -1;
        selectedSegmentID = -1;
        selectedBlockID = -1;
        selectedLotID = -1;
        currentMode = BuildMode.Idle;
        Debug.Log("[CityManager] Città resettata!");
    }

    public CityData GetCityData() => cityData;
    public void SetCityData(CityData data) => cityData = data;

    public void SetSegmentRoadProfile(int segmentID, RoadProfile roadProfile)
    {
        if (cityData == null)
        {
            return;
        }

        CitySegment segment = cityData.GetSegment(segmentID);
        if (segment == null)
        {
            return;
        }

        segment.roadProfile = roadProfile;
        segment.width = roadProfile != null ? Mathf.Max(0.5f, roadProfile.roadWidth) : cityData.globalRoadWidth;
    }

    public void SetSegmentGeometryType(int segmentID, CitySegmentGeometryType geometryType)
    {
        if (cityData == null)
        {
            return;
        }

        CitySegment segment = cityData.GetSegment(segmentID);
        if (segment == null)
        {
            return;
        }

        segment.geometryType = geometryType;
        if (geometryType == CitySegmentGeometryType.Bezier)
        {
            CityRoadGeometry.ResetBezierHandles(cityData, segment);
        }
    }

    public void ResetSegmentBezierHandles(int segmentID)
    {
        if (cityData == null)
        {
            return;
        }

        CitySegment segment = cityData.GetSegment(segmentID);
        if (segment == null)
        {
            return;
        }

        CityRoadGeometry.ResetBezierHandles(cityData, segment);
    }

    public string AnalyzeIntersections(float mergeThreshold = 0.5f)
    {
        if (cityData == null)
        {
            return "[CityManager] Analisi intersezioni: CityData nullo.";
        }

        List<CityIntersectionCandidate> candidates = CityIntersectionUtility.DetectIntersections(cityData, CityRoadGeometry.DefaultCurveSamples, mergeThreshold);
        string report = $"[CityManager] Intersezioni geometriche rilevate: {candidates.Count}.";
        Debug.Log(report);
        return report;
    }

    public string RepairConnections()
    {
        if (cityData == null)
        {
            return "[CityManager] Ripara Collegamenti: CityData nullo.";
        }

        int removedNullNodes = 0;
        int removedInvalidSegments = 0;
        int removedBrokenNodeRefs = 0;
        int addedMissingEndpointRefs = 0;

        // 1) Rimuove nodi null dalla lista.
        for (int i = cityData.nodes.Count - 1; i >= 0; i--)
        {
            if (cityData.nodes[i] == null)
            {
                cityData.nodes.RemoveAt(i);
                removedNullNodes++;
            }
        }

        // 2) Rimuove segmenti null o con endpoint mancanti.
        for (int i = cityData.segments.Count - 1; i >= 0; i--)
        {
            CitySegment segment = cityData.segments[i];
            if (segment == null)
            {
                cityData.segments.RemoveAt(i);
                removedInvalidSegments++;
                continue;
            }

            CityNode nodeA = cityData.GetNode(segment.nodeA_ID);
            CityNode nodeB = cityData.GetNode(segment.nodeB_ID);
            if (nodeA == null || nodeB == null)
            {
                cityData.segments.RemoveAt(i);
                removedInvalidSegments++;
            }
        }

        // 3) Assicura che ogni segmento valido sia presente nei due endpoint.
        foreach (CitySegment segment in cityData.segments)
        {
            if (segment == null) continue;

            CityNode nodeA = cityData.GetNode(segment.nodeA_ID);
            CityNode nodeB = cityData.GetNode(segment.nodeB_ID);
            if (nodeA == null || nodeB == null) continue;

            if (!nodeA.connectedSegmentIDs.Contains(segment.id))
            {
                nodeA.connectedSegmentIDs.Add(segment.id);
                addedMissingEndpointRefs++;
            }

            if (!nodeB.connectedSegmentIDs.Contains(segment.id))
            {
                nodeB.connectedSegmentIDs.Add(segment.id);
                addedMissingEndpointRefs++;
            }
        }

        // 4) Rimuove riferimenti orfani o duplicati nei nodi.
        foreach (CityNode node in cityData.nodes)
        {
            if (node == null) continue;

            HashSet<int> uniqueSegmentIds = new HashSet<int>();
            for (int i = node.connectedSegmentIDs.Count - 1; i >= 0; i--)
            {
                int segId = node.connectedSegmentIDs[i];
                CitySegment seg = cityData.GetSegment(segId);
                bool invalidRef = seg == null || (seg.nodeA_ID != node.id && seg.nodeB_ID != node.id);
                bool duplicatedRef = !uniqueSegmentIds.Add(segId);

                if (invalidRef || duplicatedRef)
                {
                    node.connectedSegmentIDs.RemoveAt(i);
                    removedBrokenNodeRefs++;
                }
            }
        }

        if (selectedNodeID != -1 && cityData.GetNode(selectedNodeID) == null)
        {
            selectedNodeID = -1;
        }

        if (selectedSegmentID != -1 && cityData.GetSegment(selectedSegmentID) == null)
        {
            selectedSegmentID = -1;
        }

        string report = $"[CityManager] Ripara Collegamenti completato. " +
                        $"Nodi null rimossi: {removedNullNodes}, " +
                        $"Segmenti invalidi rimossi: {removedInvalidSegments}, " +
                        $"Riferimenti nodo rimossi: {removedBrokenNodeRefs}, " +
                        $"Riferimenti endpoint aggiunti: {addedMissingEndpointRefs}.";

        Debug.Log(report);
        return report;
    }

    public string WeldCloseNodes(float distanceThreshold)
    {
        if (cityData == null)
        {
            return "[CityManager] Salda Nodi: CityData nullo.";
        }

        distanceThreshold = Mathf.Max(0.01f, distanceThreshold);
        float thresholdSqr = distanceThreshold * distanceThreshold;

        List<CityNode> nodes = cityData.nodes;
        if (nodes == null || nodes.Count < 2)
        {
            return "[CityManager] Salda Nodi: nodi insufficienti.";
        }

        Dictionary<int, int> mergeMap = new Dictionary<int, int>();
        HashSet<int> nodesToRemove = new HashSet<int>();
        int mergedClusterCount = 0;

        bool[] visited = new bool[nodes.Count];
        for (int i = 0; i < nodes.Count; i++)
        {
            if (visited[i]) continue;
            CityNode seed = nodes[i];
            if (seed == null)
            {
                visited[i] = true;
                continue;
            }

            Queue<int> queue = new Queue<int>();
            List<int> clusterIndices = new List<int>();
            visited[i] = true;
            queue.Enqueue(i);

            while (queue.Count > 0)
            {
                int current = queue.Dequeue();
                clusterIndices.Add(current);
                CityNode nodeA = nodes[current];
                if (nodeA == null) continue;

                for (int j = 0; j < nodes.Count; j++)
                {
                    if (visited[j]) continue;
                    CityNode nodeB = nodes[j];
                    if (nodeB == null)
                    {
                        visited[j] = true;
                        continue;
                    }

                    if ((nodeA.position - nodeB.position).sqrMagnitude <= thresholdSqr)
                    {
                        visited[j] = true;
                        queue.Enqueue(j);
                    }
                }
            }

            if (clusterIndices.Count < 2)
            {
                continue;
            }

            CityNode survivor = nodes[clusterIndices[0]];
            if (survivor == null)
            {
                continue;
            }

            Vector3 centroid = Vector3.zero;
            int validCount = 0;
            foreach (int idx in clusterIndices)
            {
                CityNode node = nodes[idx];
                if (node == null) continue;
                centroid += node.position;
                validCount++;
            }

            if (validCount == 0)
            {
                continue;
            }

            centroid /= validCount;
            survivor.position = centroid;

            foreach (int idx in clusterIndices)
            {
                CityNode node = nodes[idx];
                if (node == null) continue;

                mergeMap[node.id] = survivor.id;
                if (node.id != survivor.id)
                {
                    nodesToRemove.Add(node.id);
                }
            }

            mergedClusterCount++;
        }

        if (nodesToRemove.Count == 0)
        {
            return "[CityManager] Salda Nodi: nessun nodo abbastanza vicino da unire.";
        }

        int removedSelfSegments = 0;
        int removedDuplicateSegments = 0;
        HashSet<string> uniquePairs = new HashSet<string>();

        for (int i = cityData.segments.Count - 1; i >= 0; i--)
        {
            CitySegment seg = cityData.segments[i];
            if (seg == null)
            {
                continue;
            }

            if (mergeMap.ContainsKey(seg.nodeA_ID)) seg.nodeA_ID = mergeMap[seg.nodeA_ID];
            if (mergeMap.ContainsKey(seg.nodeB_ID)) seg.nodeB_ID = mergeMap[seg.nodeB_ID];

            if (seg.nodeA_ID == seg.nodeB_ID)
            {
                cityData.segments.RemoveAt(i);
                removedSelfSegments++;
                continue;
            }

            int minId = Mathf.Min(seg.nodeA_ID, seg.nodeB_ID);
            int maxId = Mathf.Max(seg.nodeA_ID, seg.nodeB_ID);
            string key = minId + "_" + maxId;

            if (!uniquePairs.Add(key))
            {
                cityData.segments.RemoveAt(i);
                removedDuplicateSegments++;
            }
        }

        for (int i = cityData.nodes.Count - 1; i >= 0; i--)
        {
            CityNode node = cityData.nodes[i];
            if (node != null && nodesToRemove.Contains(node.id))
            {
                cityData.nodes.RemoveAt(i);
            }
        }

        if (selectedNodeID != -1 && mergeMap.ContainsKey(selectedNodeID))
        {
            selectedNodeID = mergeMap[selectedNodeID];
        }

        string repairReport = RepairConnections();
        string report = $"[CityManager] Salda Nodi completato. " +
                        $"Cluster uniti: {mergedClusterCount}, " +
                        $"Nodi fusi: {nodesToRemove.Count}, " +
                        $"Segmenti loop rimossi: {removedSelfSegments}, " +
                        $"Segmenti duplicati rimossi: {removedDuplicateSegments}. " +
                        repairReport;

        Debug.Log(report);
        return report;
    }

    public string SimplifyPaths(float maxDeviationFromStraightDeg = 8f)
    {
        if (cityData == null)
        {
            return "[CityManager] Semplifica Percorsi: CityData nullo.";
        }

        maxDeviationFromStraightDeg = Mathf.Clamp(maxDeviationFromStraightDeg, 0.1f, 45f);

#if UNITY_EDITOR
        RecordCityDataUndo("Semplifica Percorsi");
#endif

        int removedNodes = 0;
        int removedSegments = 0;
        int addedSegments = 0;
        bool changed;

        do
        {
            changed = false;

            for (int i = 0; i < cityData.nodes.Count; i++)
            {
                CityNode node = cityData.nodes[i];
                if (node == null)
                {
                    continue;
                }

                if (node.connectedSegmentIDs == null || node.connectedSegmentIDs.Count != 2)
                {
                    continue;
                }

                CitySegment segA = cityData.GetSegment(node.connectedSegmentIDs[0]);
                CitySegment segB = cityData.GetSegment(node.connectedSegmentIDs[1]);
                if (segA == null || segB == null)
                {
                    continue;
                }

                int otherA = segA.nodeA_ID == node.id ? segA.nodeB_ID : segA.nodeA_ID;
                int otherB = segB.nodeA_ID == node.id ? segB.nodeB_ID : segB.nodeA_ID;
                if (otherA == otherB)
                {
                    continue;
                }

                CityNode nodeA = cityData.GetNode(otherA);
                CityNode nodeB = cityData.GetNode(otherB);
                if (nodeA == null || nodeB == null)
                {
                    continue;
                }

                Vector3 dirA = (nodeA.position - node.position);
                Vector3 dirB = (nodeB.position - node.position);
                dirA.y = 0f;
                dirB.y = 0f;
                if (dirA.sqrMagnitude < 0.0001f || dirB.sqrMagnitude < 0.0001f)
                {
                    continue;
                }

                float straightDeviation = Vector3.Angle(dirA.normalized, -dirB.normalized);
                if (straightDeviation > maxDeviationFromStraightDeg)
                {
                    continue;
                }

                bool alreadyConnected = SegmentExistsBetween(otherA, otherB);

                if (!alreadyConnected)
                {
                    CitySegment merged = new CitySegment(
                        cityData.GetNextSegmentID(),
                        otherA,
                        otherB,
                        (segA.width + segB.width) * 0.5f
                    );
                    cityData.segments.Add(merged);
                    addedSegments++;
                }

                cityData.segments.Remove(segA);
                cityData.segments.Remove(segB);
                removedSegments += 2;
                cityData.nodes.Remove(node);
                removedNodes++;

                changed = true;
                break;
            }
        }
        while (changed);

        string repairReport = RepairConnections();
        string report =
            "[CityManager] Semplifica Percorsi completato. " +
            "Nodi rimossi: " + removedNodes + ", " +
            "Segmenti rimossi: " + removedSegments + ", " +
            "Segmenti aggiunti: " + addedSegments + ". " +
            repairReport;

        Debug.Log(report);
        return report;
    }

    private bool SegmentExistsBetween(int nodeAId, int nodeBId)
    {
        if (cityData == null)
        {
            return false;
        }

        for (int i = 0; i < cityData.segments.Count; i++)
        {
            CitySegment seg = cityData.segments[i];
            if (seg == null)
            {
                continue;
            }

            bool sameDirection = seg.nodeA_ID == nodeAId && seg.nodeB_ID == nodeBId;
            bool oppositeDirection = seg.nodeA_ID == nodeBId && seg.nodeB_ID == nodeAId;
            if (sameDirection || oppositeDirection)
            {
                return true;
            }
        }

        return false;
    }

    // Test/Debug
    public void LogStats()
    {
        if (cityData == null)
        {
            Debug.Log("[CityManager] CityData è null");
            return;
        }

        Debug.Log($@"[CityManager] Statistiche:
  - Nodi: {cityData.nodes.Count}
  - Segmenti: {cityData.segments.Count}
  - Blocchi: {cityData.blocks.Count}
  - Lotti: {cityData.lots.Count}
  - Modalità Editor: {currentMode}");
    }
}
