using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Utility per suddividere blocchi in lotti.
/// Ogni lotto è allineato al bordo stradale e la profondità viene trovata tramite binary-search
/// controllando che non collida con nessuna area già occupata (SAT 2-D).
/// vertices[0]=frontSinistra, [1]=frontDestra, [2]=retroDestra, [3]=retroSinistra.
/// </summary>
public static class CityLotGenerator
{
    public static List<CityLot> GenerateLotsForBlock(CityBlock block, float avgLotSize, ZoneType zoning, int blockIndex, CityData cityData)
    {
        List<CityLot> lots    = new List<CityLot>();
        if (block.vertices.Count < 3) return lots;

        float buildingHeight = cityData.GetZoneHeight(zoning);
        List<Vector3> verts  = block.vertices;
        float roadSetback    = cityData.globalRoadWidth * 0.5f;
        Vector3 blockCenter  = block.GetCenter();
        int tempID           = 0;

        // Registro 2-D (piano XZ) di tutte le aree già occupate da lotti precedenti.
        List<Vector2[]> occupied = new List<Vector2[]>();

        for (int edgeIdx = 0; edgeIdx < verts.Count; edgeIdx++)
        {
            Vector3 edgeStart = verts[edgeIdx];
            Vector3 edgeEnd   = verts[(edgeIdx + 1) % verts.Count];
            float edgeLength  = Vector3.Distance(edgeStart, edgeEnd);

            if (edgeLength < avgLotSize * 0.4f) continue;

            Vector3 edgeDir = (edgeEnd - edgeStart).normalized;
            Vector3 perp    = new Vector3(-edgeDir.z, 0f, edgeDir.x);
            Vector3 edgeMid = (edgeStart + edgeEnd) * 0.5f;
            if (Vector3.Dot(perp, blockCenter - edgeMid) < 0f) perp = -perp;
            Vector3 inward = perp;

            // Profondità geometrica massima: raycast verso il bordo opposto al 90%.
            Vector3 frontMid = edgeMid + inward * roadSetback;
            float available  = RaycastToOtherEdges(verts, edgeIdx, frontMid, inward);
            float maxDepth   = Mathf.Min(avgLotSize * 0.9f, available * 0.90f);
            maxDepth         = Mathf.Max(maxDepth, 3f);

            int   count      = Mathf.Max(1, Mathf.RoundToInt(edgeLength / avgLotSize));
            float baseWidth  = edgeLength / count;
            float currentPos = 0f;  // Traccia la posizione corrente lungo l'edge

            int lotIdx = 0;
            while (lotIdx < count && currentPos < edgeLength)
            {
                // Calcola un sizeFactor proceduralmente deterministico
                float sizeFactor = CalculateLotSizeFactor(block.id, edgeIdx, lotIdx, cityData);
                
                // Calcola la larghezza di questo lotto scalata dal sizeFactor
                float lotWidth = baseWidth * sizeFactor;
                
                // Calcola il gap in base al sizeFactor (lotti più grandi = gap più ampio)
                float normalizedSizeFactor = (sizeFactor - cityData.minLotSizeFactor) / (cityData.maxLotSizeFactor - cityData.minLotSizeFactor);
                normalizedSizeFactor = Mathf.Clamp01(normalizedSizeFactor);
                float lotGap = Mathf.Lerp(cityData.gapMinimum, cityData.gapMaximum, normalizedSizeFactor);
                
                // Calcola le posizioni reali del lotto lungo l'edge
                float posFrom = currentPos + lotGap;
                float posTo   = posFrom + lotWidth;
                
                // Controlla se il lotto rientra nell'edge
                if (posTo > edgeLength)
                {
                    // Riduci la larghezza per adattarsi alla fine dell'edge
                    lotWidth = edgeLength - posFrom;
                    if (lotWidth < baseWidth * 0.3f) break;  // Troppo piccolo, interrompi
                    posTo = edgeLength - lotGap;
                }
                
                // Normalizza le posizioni a parametri t [0, 1]
                float tFrom = Mathf.Clamp01(posFrom / edgeLength);
                float tTo   = Mathf.Clamp01(posTo / edgeLength);

                Vector3 roadFL = Vector3.Lerp(edgeStart, edgeEnd, tFrom);
                Vector3 roadFR = Vector3.Lerp(edgeStart, edgeEnd, tTo);
                Vector3 frontL = roadFL + inward * roadSetback;
                Vector3 frontR = roadFR + inward * roadSetback;

                // Binary-search: trova la profondità massima NON sovrapposta.
                float depth = FindMaxDepthNoOverlap(frontL, frontR, inward, maxDepth, occupied, verts);
                if (depth < 2f)
                {
                    // Se questo lotto non può avere profondità sufficiente, passa al prossimo
                    currentPos = posTo + lotGap;
                    lotIdx++;
                    continue;
                }

                Vector3 backL = ClampInsidePolygon(frontL, frontL + inward * depth, verts);
                Vector3 backR = ClampInsidePolygon(frontR, frontR + inward * depth, verts);

                List<Vector3> lotVertices = new List<Vector3> { frontL, frontR, backR, backL };
                float lotArea = CalculatePolygonAreaXZ(lotVertices);
                if (lotArea < cityData.minLotArea)
                {
                    currentPos = posTo + lotGap;
                    lotIdx++;
                    continue;
                }

                float frontage = Vector3.Distance(frontL, frontR);
                float depthLeft = Vector3.Distance(frontL, backL);
                float depthRight = Vector3.Distance(frontR, backR);
                float depth2 = (depthLeft + depthRight) * 0.5f;
                float shorterSide = Mathf.Max(0.01f, Mathf.Min(frontage, depth2));
                float longerSide = Mathf.Max(frontage, depth2);
                float aspectRatio = longerSide / shorterSide;

                if (aspectRatio > cityData.maxLotAspectRatio)
                {
                    currentPos = posTo + lotGap;
                    lotIdx++;
                    continue;
                }

                // Registra l'area come occupata PRIMA di passare al lotto successivo.
                occupied.Add(ToXZ(frontL, frontR, backR, backL));

                lots.Add(new CityLot(blockIndex * 1000 + tempID, block.id)
                {
                    buildingCenter = (frontL + frontR + backL + backR) * 0.25f,
                    buildingHeight = buildingHeight,
                    vertices       = lotVertices,
                    sizeFactor     = sizeFactor,
                    lotGap         = lotGap
                });
                tempID++;
                
                // Avanza alla posizione successiva
                currentPos = posTo + lotGap;
                lotIdx++;
            }
        }

        return lots;
    }

    // ── Calcolo Size Factor Procedurale ───────────────────────────────────────

    /// <summary>
    /// Calcola un sizeFactor deterministico per un lotto specifico.
    /// Usa Perlin noise per variabilità naturale ma ripetitiva.
    /// </summary>
    private static float CalculateLotSizeFactor(int blockID, int edgeIdx, int lotIdx, CityData cityData)
    {
        // Seed
        float seed = Random.Range(0f, 10000f) + blockID * 100f + edgeIdx * 10f + lotIdx;

        // Combina Perlin noise per un effetto ripetitivo e naturale
        float noiseVal = Mathf.PerlinNoise(seed + edgeIdx * 0.5f, blockID * 0.1f + lotIdx * 0.1f);
        
        // Normalizza il Perlin noise da [0, 1] (PerlinNoise restituisce generalmente 0-1)
        noiseVal = Mathf.Clamp01(noiseVal);
        
        // Aggiungi una variazione basata su lotIdx per maggior diversità
        float lotVariation = Mathf.Sin(lotIdx * 0.7f + blockID * 0.1f) * 0.3f;
        lotVariation = (lotVariation + 1.0f) * 0.5f; // Normalizza a [0, 1]
        
        // Combina noise e variazione
        float combinedFactor = (noiseVal * 0.6f + lotVariation * 0.4f);
        combinedFactor = Mathf.Clamp01(combinedFactor);
        
        // Applica densityInfluence per modulare l'effetto
        if (cityData.densityInfluence < 0.001f)
        {
            combinedFactor = 0.5f; // Se densityInfluence è 0, tutti i lotti hanno la stessa dimensione
        }
        
        // Mappa in range [minLotSizeFactor, maxLotSizeFactor]
        float sizeFactor = Mathf.Lerp(cityData.minLotSizeFactor, cityData.maxLotSizeFactor, combinedFactor);
        
        return sizeFactor;
    }

    private static float FindMaxDepthNoOverlap(
        Vector3 frontL, Vector3 frontR, Vector3 inward,
        float maxDepth, List<Vector2[]> occupied, List<Vector3> blockVerts)
    {
        float lo = 0f, hi = maxDepth;
        for (int iter = 0; iter < 14; iter++)
        {
            float mid  = (lo + hi) * 0.5f;
            Vector3 bL = ClampInsidePolygon(frontL, frontL + inward * mid, blockVerts);
            Vector3 bR = ClampInsidePolygon(frontR, frontR + inward * mid, blockVerts);
            if (OverlapsAny(ToXZ(frontL, frontR, bR, bL), occupied))
                hi = mid;
            else
                lo = mid;
        }
        return lo;
    }

    // ── SAT 2-D ──────────────────────────────────────────────────────────────────

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

            // Richiede almeno 5 cm di gap reale: impedisce ogni overlap visibile.
            if (mx1 + 0.05f <= mn2 || mx2 + 0.05f <= mn1) return true;
        }
        return false;
    }

    // ── Geometria dentro al blocco ───────────────────────────────────────────────

    private static float RaycastToOtherEdges(List<Vector3> verts, int skipEdge, Vector3 origin, Vector3 dir)
    {
        float minDist = float.MaxValue;
        for (int i = 0; i < verts.Count; i++)
        {
            if (i == skipEdge) continue;
            float dist = RaySegmentXZ(origin, dir, verts[i], verts[(i + 1) % verts.Count]);
            if (dist > 0.01f && dist < minDist) minDist = dist;
        }
        return minDist < float.MaxValue ? minDist : 5f;
    }

    private static float RaySegmentXZ(Vector3 o, Vector3 d, Vector3 a, Vector3 b)
    {
        float dx = d.x, dz = d.z, ex = b.x - a.x, ez = b.z - a.z;
        float denom = dx * ez - dz * ex;
        if (Mathf.Abs(denom) < 1e-6f) return -1f;
        float fx = a.x - o.x, fz = a.z - o.z;
        float t  = (fx * ez - fz * ex) / denom;
        float u  = (fx * dz - fz * dx) / denom;
        return (t > 0.001f && u >= 0f && u <= 1f) ? t : -1f;
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

    private static float CalculatePolygonAreaXZ(List<Vector3> vertices)
    {
        if (vertices == null || vertices.Count < 3)
        {
            return 0f;
        }

        float area = 0f;
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 a = vertices[i];
            Vector3 b = vertices[(i + 1) % vertices.Count];
            area += a.x * b.z - b.x * a.z;
        }

        return Mathf.Abs(area) * 0.5f;
    }
}
