using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Algoritmo di planarizzazione della rete stradale.
/// Rileva gli incroci geometrici tra segmenti non connessi (es. autostrade diagonali
/// che attraversano la griglia ortogonale), inserisce nodi agli incroci e spezza
/// i segmenti coinvolti preservando il RoadProfile originale.
/// </summary>
public static class CityRoadPlanarizer
{
    /// <summary>
    /// Planarizza la rete stradale contenuta nel CityManager.
    /// Ritorna il numero di segmenti spezzati.
    /// </summary>
    public static int Planarize(CityManager manager, float mergeTol)
    {
        CityData cityData = manager.GetCityData();
        if (cityData == null) return 0;

        // Mappa segID -> lista di punti di split (incroci interni al segmento)
        var splitMap = new Dictionary<int, List<Vector3>>();

        // Snapshot degli ID segmenti (la lista verrà modificata durante il loop)
        int[] segIDs = new int[cityData.segments.Count];
        for (int i = 0; i < cityData.segments.Count; i++)
            segIDs[i] = cityData.segments[i].id;

        // Scansione O(N²): tutte le coppie di segmenti non adiacenti
        for (int i = 0; i < segIDs.Length; i++)
        {
            CitySegment segA = cityData.GetSegment(segIDs[i]);
            if (segA == null) continue;
            CityNode nA0 = cityData.GetNode(segA.nodeA_ID);
            CityNode nA1 = cityData.GetNode(segA.nodeB_ID);
            if (nA0 == null || nA1 == null) continue;

            for (int j = i + 1; j < segIDs.Length; j++)
            {
                CitySegment segB = cityData.GetSegment(segIDs[j]);
                if (segB == null) continue;

                // Salta coppie adiacenti (condividono un endpoint)
                if (segA.nodeA_ID == segB.nodeA_ID || segA.nodeA_ID == segB.nodeB_ID ||
                    segA.nodeB_ID == segB.nodeA_ID || segA.nodeB_ID == segB.nodeB_ID)
                    continue;

                CityNode nB0 = cityData.GetNode(segB.nodeA_ID);
                CityNode nB1 = cityData.GetNode(segB.nodeB_ID);
                if (nB0 == null || nB1 == null) continue;

                if (TryIntersectSegmentsXZ(nA0.position, nA1.position,
                                           nB0.position, nB1.position,
                                           mergeTol, out Vector3 cross))
                {
                    if (!splitMap.ContainsKey(segA.id)) splitMap[segA.id] = new List<Vector3>();
                    if (!splitMap.ContainsKey(segB.id)) splitMap[segB.id] = new List<Vector3>();
                    AddIfNotNearby(splitMap[segA.id], cross, mergeTol);
                    AddIfNotNearby(splitMap[segB.id], cross, mergeTol);
                }
            }

            // Rileva nodi esistenti che cadono internamente sul segmento (già creati in passi precedenti)
            foreach (CityNode node in cityData.nodes)
            {
                if (node.id == segA.nodeA_ID || node.id == segA.nodeB_ID) continue;
                if (IsPointOnSegmentXZ(node.position, nA0.position, nA1.position, mergeTol))
                {
                    if (!splitMap.ContainsKey(segA.id)) splitMap[segA.id] = new List<Vector3>();
                    AddIfNotNearby(splitMap[segA.id], node.position, mergeTol);
                }
            }
        }

        if (splitMap.Count == 0) return 0;

        int splitsDone = 0;

        foreach (var kv in splitMap)
        {
            CitySegment seg = cityData.GetSegment(kv.Key);
            if (seg == null) continue; // già rimosso da un precedente split nello stesso passo

            CityNode startNode = cityData.GetNode(seg.nodeA_ID);
            CityNode endNode   = cityData.GetNode(seg.nodeB_ID);
            if (startNode == null || endNode == null) continue;

            RoadProfile profile = seg.roadProfile;
            List<Vector3> crossings = kv.Value;

            // Crea o recupera nodi agli incroci
            var midNodes = new List<CityNode>(crossings.Count);
            foreach (Vector3 pos in crossings)
            {
                CityNode mid = manager.FindNearestNode(pos, mergeTol);
                if (mid == null)
                    mid = manager.AddNode(pos);
                midNodes.Add(mid);
            }

            // Rimuovi endpoint e duplicati
            midNodes.RemoveAll(n => n.id == startNode.id || n.id == endNode.id);
            var seen = new HashSet<int>();
            var uniqueMids = new List<CityNode>();
            foreach (var n in midNodes)
                if (seen.Add(n.id)) uniqueMids.Add(n);

            if (uniqueMids.Count == 0) continue;

            // Ordina per distanza crescente da startNode (XZ)
            float sx = startNode.position.x, sz = startNode.position.z;
            uniqueMids.Sort((a, b) =>
            {
                float da = (a.position.x - sx) * (a.position.x - sx) + (a.position.z - sz) * (a.position.z - sz);
                float db = (b.position.x - sx) * (b.position.x - sx) + (b.position.z - sz) * (b.position.z - sz);
                return da.CompareTo(db);
            });

            // Rimuove il segmento originale e crea la catena spezzata
            manager.RemoveSegment(seg.id);

            CityNode prev = startNode;
            foreach (CityNode mid in uniqueMids)
            {
                CitySegment newSeg = manager.AddSegment(prev.id, mid.id);
                if (newSeg != null) { newSeg.roadProfile = profile; if (profile != null) newSeg.width = profile.roadWidth; }
                prev = mid;
            }
            CitySegment lastSeg = manager.AddSegment(prev.id, endNode.id);
            if (lastSeg != null) { lastSeg.roadProfile = profile; if (profile != null) lastSeg.width = profile.roadWidth; }

            splitsDone++;
        }

        return splitsDone;
    }

    // ========== HELPERS GEOMETRICI (interni) ==========

    private static bool TryIntersectSegmentsXZ(
        Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1,
        float endpointTol, out Vector3 hit)
    {
        float ax = a1.x - a0.x, az = a1.z - a0.z;
        float bx = b1.x - b0.x, bz = b1.z - b0.z;
        float rxs = ax * bz - az * bx;

        if (Mathf.Abs(rxs) < 1e-6f) { hit = Vector3.zero; return false; }

        float dx = b0.x - a0.x, dz = b0.z - a0.z;
        float t = (dx * bz - dz * bx) / rxs;
        float u = (dx * az - dz * ax) / rxs;

        float lenA = Mathf.Sqrt(ax * ax + az * az);
        float lenB = Mathf.Sqrt(bx * bx + bz * bz);
        if (lenA < 1e-6f || lenB < 1e-6f) { hit = Vector3.zero; return false; }

        float epA = endpointTol / lenA;
        float epB = endpointTol / lenB;

        if (t <= epA || t >= 1f - epA || u <= epB || u >= 1f - epB)
        {
            hit = Vector3.zero;
            return false;
        }

        hit = new Vector3(a0.x + t * ax, (a0.y + a1.y + b0.y + b1.y) * 0.25f, a0.z + t * az);
        return true;
    }

    private static bool IsPointOnSegmentXZ(Vector3 point, Vector3 a, Vector3 b, float tol)
    {
        float dx = b.x - a.x, dz = b.z - a.z;
        float lenSq = dx * dx + dz * dz;
        if (lenSq < 1e-6f) return false;

        float t = ((point.x - a.x) * dx + (point.z - a.z) * dz) / lenSq;
        float len = Mathf.Sqrt(lenSq);
        float epT = tol / len;

        if (t <= epT || t >= 1f - epT) return false;

        float projX = a.x + t * dx;
        float projZ = a.z + t * dz;
        float distSq = (point.x - projX) * (point.x - projX) + (point.z - projZ) * (point.z - projZ);
        return distSq <= tol * tol;
    }

    private static void AddIfNotNearby(List<Vector3> list, Vector3 pos, float tol)
    {
        float tolSq = tol * tol;
        foreach (Vector3 v in list)
        {
            float dx = v.x - pos.x, dz = v.z - pos.z;
            if (dx * dx + dz * dz <= tolSq) return;
        }
        list.Add(pos);
    }
}
