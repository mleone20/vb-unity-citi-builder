using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Genera lotti per un blocco usando l'approccio "Frontage" (affaccio su strada).
/// Per ogni edge del blocco percorre il bordo e ritaglia lotti la cui larghezza
/// corrisponde esattamente a footprintSize.x del prefab selezionato, mentre la
/// profondita' corrisponde a footprintSize.y. Garantisce che ogni lotto abbia
/// il fronte sulla strada.
/// Convenzione vertici: [0]=frontLeft, [1]=frontRight, [2]=backRight, [3]=backLeft.
/// </summary>
public static class CityLotGenerator
{
    private const float LotSafetyMargin = 0.05f;

    public static List<CityLot> GenerateLotsForBlock(CityBlock block, ZoneType zoning, int blockIndex, CityData cityData, BlockOrientation orientation = BlockOrientation.Interior)
    {
        if (orientation == BlockOrientation.Sparse)
            return GenerateSparseLotsForBlock(block, zoning, blockIndex, cityData);

        bool isExterior = orientation == BlockOrientation.Exterior;

        List<CityLot> lots = new List<CityLot>();
        if (block.vertices.Count < 3) return lots;

        float buildingHeight  = cityData.GetZoneHeight(zoning);
        List<Vector3> verts   = block.vertices;
        float roadSetback     = cityData.globalRoadWidth * 0.5f + LotSafetyMargin;
        Vector3 blockCenter   = block.GetCenter();
        int tempID            = 0;

        // Raccolta candidati prefab con metadata valida.
        List<(GameObject go, CityBuilderPrefab meta)> candidates = CollectCandidates(zoning);
        if (candidates.Count == 0)
        {
            return lots;
        }

        // Registro 2D (piano XZ) delle aree gia' occupate (anti-overlap SAT).
        List<Vector2[]> occupied = new List<Vector2[]>();

        for (int edgeIdx = 0; edgeIdx < verts.Count; edgeIdx++)
        {
            Vector3 edgeStart  = verts[edgeIdx];
            Vector3 edgeEnd    = verts[(edgeIdx + 1) % verts.Count];
            float   edgeLength = Vector3.Distance(edgeStart, edgeEnd);

            if (edgeLength < 2f) continue;

            Vector3 edgeDir = (edgeEnd - edgeStart).normalized;
            // Perpendicolare verso l'interno del blocco.
            Vector3 perp    = new Vector3(-edgeDir.z, 0f, edgeDir.x);
            Vector3 edgeMid = (edgeStart + edgeEnd) * 0.5f;
            if (Vector3.Dot(perp, blockCenter - edgeMid) < 0f) perp = -perp;
            Vector3 inward  = perp;
            
            // Se il blocco è orientato verso l'esterno, inverte la direzione
            if (isExterior) inward = -inward;

            float cursor = 0f;
            int   lotIdx = 0;

            while (cursor < edgeLength)
            {
                // ── Seleziona prefab e dimensioni lotto ──────────────────────
                float lotWidth, lotDepth;
                int   prefabIndex = PickCandidateIndex(blockIndex, edgeIdx, lotIdx, candidates.Count);
                Vector2 fp        = candidates[prefabIndex].meta.GetAlignedFootprintSize();
                lotWidth          = fp.x;
                lotDepth          = fp.y;

                // ── Gap procedurale deterministico (o override per blocco) ──────
                float lotGap;
                if (block.lotGapOverride >= 0f)
                {
                    lotGap = block.lotGapOverride;
                }
                else
                {
                    float gapNoise = Mathf.PerlinNoise(blockIndex * 0.13f + edgeIdx * 0.37f + lotIdx * 0.71f, 0.5f);
                    gapNoise       = Mathf.Clamp01(gapNoise);
                    lotGap         = Mathf.Lerp(cityData.gapMinimum, cityData.gapMaximum, gapNoise);
                }

                // ── Posizione lungo l'edge ───────────────────────────────────
                float posFrom = cursor + lotGap;
                float posTo   = posFrom + lotWidth;

                // Spazio insufficiente: interrompi questo edge.
                if (posFrom >= edgeLength) break;

                // Lotto a cavallo della fine: riduci se almeno meta' della larghezza entra.
                if (posTo > edgeLength)
                {
                    float residuo = edgeLength - posFrom;
                    if (residuo < lotWidth * 0.5f) break;
                    posTo = edgeLength;
                }

                // ── Calcolo corners del lotto ────────────────────────────────
                float   tFrom  = posFrom / edgeLength;
                float   tTo    = posTo   / edgeLength;

                Vector3 roadFL = Vector3.Lerp(edgeStart, edgeEnd, tFrom);
                Vector3 roadFR = Vector3.Lerp(edgeStart, edgeEnd, tTo);
                Vector3 frontL = roadFL + inward * roadSetback;
                Vector3 frontR = roadFR + inward * roadSetback;
                Vector3 backL  = isExterior ? frontL + inward * lotDepth : ClampInsidePolygon(frontL, frontL + inward * lotDepth, verts);
                Vector3 backR  = isExterior ? frontR + inward * lotDepth : ClampInsidePolygon(frontR, frontR + inward * lotDepth, verts);

                // Tutti i fronti sullo stesso edge restano allineati sulla stessa frontage line.
                frontL = ProjectPointOnFrontageLine(frontL, roadFL + inward * roadSetback, edgeDir);
                frontR = ProjectPointOnFrontageLine(frontR, roadFR + inward * roadSetback, edgeDir);

                // ── Validazione ──────────────────────────────────────────────
                List<Vector3> lotVerts = new List<Vector3> { frontL, frontR, backR, backL };

                bool isLotValid = isExterior
                    ? IsOutsideBuildableArea(lotVerts, verts, roadSetback)
                    : IsInsideBuildableArea(lotVerts, verts, roadSetback);

                float skipStep = block.lotGapOverride >= 0f ? block.lotGapOverride : cityData.gapMinimum;
                if (skipStep <= 0f) skipStep = 0.1f;

                if (!isLotValid)
                {
                    cursor += skipStep;
                    lotIdx++;
                    continue;
                }

                Vector2[] poly2D = ToXZ(frontL, frontR, backR, backL);
                if (OverlapsAny(poly2D, occupied))
                {
                    cursor += skipStep;
                    lotIdx++;
                    continue;
                }

                // ── Creazione lotto ──────────────────────────────────────────
                occupied.Add(poly2D);

                Vector3 desiredFrontDirection = -inward;
                Vector3 localFrontDirection = candidates[prefabIndex].meta.GetFrontageDirectionLocal();
                Quaternion assignedRotation = Quaternion.FromToRotation(localFrontDirection, desiredFrontDirection);

                lots.Add(new CityLot(blockIndex * 1000 + tempID, block.id)
                {
                    buildingCenter          = (frontL + frontR + backL + backR) * 0.25f,
                    buildingHeight          = buildingHeight,
                    vertices                = lotVerts,
                    lotGap                  = lotGap,
                    assignedPrefabIndex     = prefabIndex,
                    assignedSpawnRotation   = assignedRotation,
                    hasAssignedSpawnRotation = true
                });

                tempID++;
                cursor = posTo + lotGap;
                lotIdx++;
            }
        }

        return lots;
    }

    // ── Modalità Sparse ──────────────────────────────────────────────────────

    private static List<CityLot> GenerateSparseLotsForBlock(CityBlock block, ZoneType zoning, int blockIndex, CityData cityData)
    {
        List<CityLot> lots = new List<CityLot>();
        if (block.vertices.Count < 3) return lots;

        float buildingHeight = cityData.GetZoneHeight(zoning);
        List<Vector3> verts  = block.vertices;
        float margin         = cityData.globalRoadWidth * 0.5f + LotSafetyMargin;
        int   tempID         = 0;

        List<(GameObject go, CityBuilderPrefab meta)> candidates = CollectCandidates(zoning);
        if (candidates.Count == 0) return lots;

        // AABB del blocco in XZ.
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (Vector3 v in verts)
        {
            if (v.x < minX) minX = v.x;
            if (v.x > maxX) maxX = v.x;
            if (v.z < minZ) minZ = v.z;
            if (v.z > maxZ) maxZ = v.z;
        }

        // Passo griglia basato su footprint medio + gap massimo.
        float avgW = 0f, avgD = 0f;
        foreach (var (_, meta) in candidates)
        {
            Vector2 fp = meta.GetAlignedFootprintSize();
            avgW += fp.x;
            avgD += fp.y;
        }
        avgW /= candidates.Count;
        avgD /= candidates.Count;

        float sparseGap = block.lotGapOverride >= 0f ? block.lotGapOverride : cityData.gapMaximum;
        float stepX    = avgW + sparseGap;
        float stepZ    = avgD + sparseGap;
        float centerY = block.GetCenter().y;

        List<Vector2[]> occupied = new List<Vector2[]>();
        int cellIdx = 0;

        for (float gz = minZ + margin; gz < maxZ - margin; gz += stepZ)
        {
            for (float gx = minX + margin; gx < maxX - margin; gx += stepX)
            {
                // Offset deterministico via Perlin noise.
                float noiseX = Mathf.PerlinNoise(blockIndex * 0.17f + cellIdx * 0.43f, 0.25f) * 2f - 1f;
                float noiseZ = Mathf.PerlinNoise(0.25f, blockIndex * 0.17f + cellIdx * 0.43f) * 2f - 1f;
                float cx     = gx + noiseX * stepX * 0.3f;
                float cz     = gz + noiseZ * stepZ * 0.3f;

                Vector3 center = new Vector3(cx, centerY, cz);

                // Prefab e rotazione (multipli di 90°, deterministici).
                int   prefabIndex = PickCandidateIndex(blockIndex, cellIdx, 0, candidates.Count);
                int   angleSteps  = Mathf.Abs(blockIndex * 7 + cellIdx * 13) % 4;
                float angleDeg    = angleSteps * 90f;
                Quaternion rot    = Quaternion.Euler(0f, angleDeg, 0f);

                Vector2 footprint = candidates[prefabIndex].meta.GetAlignedFootprintSize();
                float hw = footprint.x * 0.5f;
                float hd = footprint.y * 0.5f;

                // Corner del rettangolo ruotato centrato nel punto.
                Vector3 frontL = center + rot * new Vector3(-hw, 0f, -hd);
                Vector3 frontR = center + rot * new Vector3( hw, 0f, -hd);
                Vector3 backR  = center + rot * new Vector3( hw, 0f,  hd);
                Vector3 backL  = center + rot * new Vector3(-hw, 0f,  hd);

                List<Vector3> lotVerts = new List<Vector3> { frontL, frontR, backR, backL };

                if (!IsInsideBuildableArea(lotVerts, verts, margin))
                {
                    cellIdx++;
                    continue;
                }

                Vector2[] poly2D = ToXZ(frontL, frontR, backR, backL);
                if (OverlapsAny(poly2D, occupied))
                {
                    cellIdx++;
                    continue;
                }

                occupied.Add(poly2D);

                lots.Add(new CityLot(blockIndex * 1000 + tempID, block.id)
                {
                    buildingCenter           = center,
                    buildingHeight           = buildingHeight,
                    vertices                 = lotVerts,
                    lotGap                   = cityData.gapMinimum,
                    assignedPrefabIndex      = prefabIndex,
                    assignedSpawnRotation    = rot,
                    hasAssignedSpawnRotation = true
                });

                tempID++;
                cellIdx++;
            }
        }

        return lots;
    }

    // ── Selezione prefab ─────────────────────────────────────────────────────

    private static List<(GameObject, CityBuilderPrefab)> CollectCandidates(ZoneType zone)
    {
        var result = new List<(GameObject, CityBuilderPrefab)>();
        if (zone == null || zone.buildingPrefabs == null) return result;

        foreach (GameObject go in zone.buildingPrefabs)
        {
            if (go == null) continue;
            CityBuilderPrefab meta = go.GetComponent<CityBuilderPrefab>();
            if (meta != null) result.Add((go, meta));
        }
        return result;
    }

    private static int PickCandidateIndex(int blockIdx, int edgeIdx, int lotIdx, int count)
    {
        if (count <= 1) return 0;
        int hash = 17;
        hash = hash * 31 + blockIdx;
        hash = hash * 31 + edgeIdx;
        hash = hash * 31 + lotIdx;
        return Mathf.Abs(hash) % count;
    }

    // ── SAT 2-D ──────────────────────────────────────────────────────────────

    private static bool OverlapsAny(Vector2[] poly, List<Vector2[]> others)
    {
        for (int i = 0; i < others.Count; i++)
            if (SATOverlap(poly, others[i])) return true;
        return false;
    }

    private static bool SATOverlap(Vector2[] a, Vector2[] b)
    {
        return !HasSeparator(a, b) && !HasSeparator(b, a);
    }

    private static bool HasSeparator(Vector2[] poly1, Vector2[] poly2)
    {
        for (int i = 0; i < poly1.Length; i++)
        {
            Vector2 edge = poly1[(i + 1) % poly1.Length] - poly1[i];
            Vector2 axis = new Vector2(-edge.y, edge.x);

            float mn1 = float.MaxValue, mx1 = float.MinValue;
            foreach (var p in poly1) { float d = Vector2.Dot(p, axis); if (d < mn1) mn1 = d; if (d > mx1) mx1 = d; }

            float mn2 = float.MaxValue, mx2 = float.MinValue;
            foreach (var p in poly2) { float d = Vector2.Dot(p, axis); if (d < mn2) mn2 = d; if (d > mx2) mx2 = d; }

            if (mx1 + 0.05f <= mn2 || mx2 + 0.05f <= mn1) return true;
        }
        return false;
    }

    // ── Geometria dentro al blocco ───────────────────────────────────────────

    private static Vector3 ProjectPointOnFrontageLine(Vector3 point, Vector3 frontageOrigin, Vector3 edgeDirection)
    {
        Vector3 delta = point - frontageOrigin;
        float distanceAlongEdge = Vector3.Dot(delta, edgeDirection);
        return frontageOrigin + edgeDirection * distanceAlongEdge;
    }

    private static bool IsInsideBuildableArea(List<Vector3> vertices, List<Vector3> blockPolygon, float roadSetback)
    {
        if (vertices == null || vertices.Count == 0) return false;

        for (int i = 0; i < vertices.Count; i++)
        {
            if (!PointInPolygonXZ(vertices[i], blockPolygon)) return false;

            float edgeDistance = DistanceToPolygonEdgesXZ(vertices[i], blockPolygon);
            if (edgeDistance + 0.01f < roadSetback)
            {
                return false;
            }
        }

        Vector3 center = Vector3.zero;
        for (int i = 0; i < vertices.Count; i++)
        {
            center += vertices[i];
        }
        center /= vertices.Count;

        return PointInPolygonXZ(center, blockPolygon) && DistanceToPolygonEdgesXZ(center, blockPolygon) + 0.01f >= roadSetback;
    }

    private static bool IsOutsideBuildableArea(List<Vector3> vertices, List<Vector3> blockPolygon, float roadSetback)
    {
        if (vertices == null || vertices.Count == 0) return false;

        for (int i = 0; i < vertices.Count; i++)
        {
            if (PointInPolygonXZ(vertices[i], blockPolygon)) return false;

            float edgeDistance = DistanceToPolygonEdgesXZ(vertices[i], blockPolygon);
            if (edgeDistance + 0.01f < roadSetback)
            {
                return false;
            }
        }

        Vector3 center = Vector3.zero;
        for (int i = 0; i < vertices.Count; i++)
        {
            center += vertices[i];
        }
        center /= vertices.Count;

        return !PointInPolygonXZ(center, blockPolygon) && DistanceToPolygonEdgesXZ(center, blockPolygon) + 0.01f >= roadSetback;
    }

    private static float DistanceToPolygonEdgesXZ(Vector3 point, List<Vector3> polygon)
    {
        float minDistance = float.MaxValue;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector3 a = polygon[i];
            Vector3 b = polygon[(i + 1) % polygon.Count];
            float distance = DistancePointToSegmentXZ(point, a, b);
            if (distance < minDistance)
            {
                minDistance = distance;
            }
        }
        return minDistance;
    }

    private static float DistancePointToSegmentXZ(Vector3 point, Vector3 a, Vector3 b)
    {
        Vector2 p = new Vector2(point.x, point.z);
        Vector2 s0 = new Vector2(a.x, a.z);
        Vector2 s1 = new Vector2(b.x, b.z);
        Vector2 segment = s1 - s0;
        float lengthSq = segment.sqrMagnitude;
        if (lengthSq <= 0.0001f)
        {
            return Vector2.Distance(p, s0);
        }

        float t = Mathf.Clamp01(Vector2.Dot(p - s0, segment) / lengthSq);
        Vector2 projection = s0 + segment * t;
        return Vector2.Distance(p, projection);
    }

    private static Vector3 ClampInsidePolygon(Vector3 front, Vector3 back, List<Vector3> poly)
    {
        if (PointInPolygonXZ(back, poly)) return back;
        float lo = 0f, hi = 1f;
        for (int i = 0; i < 10; i++)
        {
            float mid = (lo + hi) * 0.5f;
            if (PointInPolygonXZ(Vector3.Lerp(front, back, mid), poly)) lo = mid; else hi = mid;
        }
        return Vector3.Lerp(front, back, lo * 0.95f);
    }

    private static bool PointInPolygonXZ(Vector3 pt, List<Vector3> poly)
    {
        bool inside = false;
        int n = poly.Count;
        for (int i = 0, j = n - 1; i < n; j = i++)
        {
            float xi = poly[i].x, zi = poly[i].z, xj = poly[j].x, zj = poly[j].z;
            if (((zi > pt.z) != (zj > pt.z)) && (pt.x < (xj - xi) * (pt.z - zi) / (zj - zi) + xi))
                inside = !inside;
        }
        return inside;
    }

    private static Vector2[] ToXZ(Vector3 a, Vector3 b, Vector3 c, Vector3 d) =>
        new Vector2[] { new Vector2(a.x, a.z), new Vector2(b.x, b.z), new Vector2(c.x, c.z), new Vector2(d.x, d.z) };
}
