using UnityEngine;
using System.Collections.Generic;
using BSCCityBuilder.Core;
using BSCCityBuilder.Components;
using BSCCityBuilder.Plugins;

namespace BSCCityBuilder.Generation
{
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
    // Evita che tolleranze geometriche, miter e patch di intersezione portino
    // il footprint dell'edificio a toccare visivamente la mesh stradale.
    private const float LotSafetyMargin = 0.5f;

    public static List<CityLot> GenerateLotsForBlock(
        CityBlock block,
        ZoneType zoning,
        int blockIndex,
        CityData cityData,
        ILotSelectionPlugin lotSelectionPlugin = null,
        BlockLayoutProfile layoutProfile = null)
    {
        if (block == null || cityData == null)
            return new List<CityLot>();
        if (block.generatedLayoutAreas == null)
            block.generatedLayoutAreas = new List<CityBlockLayoutArea>();
        block.generatedLayoutAreas.Clear();

        if (layoutProfile != null)
        {
            var operationContext = new BlockLayoutOperationContext
            {
                cityData = cityData,
                block = block,
                zoneType = zoning,
                blockIndex = blockIndex,
                lotSelectionPlugin = lotSelectionPlugin
            };
            if (layoutProfile.operations != null)
            {
                for (int i = 0; i < layoutProfile.operations.Count; i++)
                {
                    BlockLayoutOperation operation = layoutProfile.operations[i];
                    if (operation != null && operation.CanExecute(operationContext))
                        operation.Execute(operationContext);
                }
            }
            block.generatedLayoutAreas.AddRange(operationContext.reservedAreas);
            return operationContext.lots;
        }

        // Compatibilità per città senza profilo: semplice frontage interno.
        return GenerateFrontageLotsForBlock(
            block, zoning, blockIndex, cityData, false, lotSelectionPlugin);
    }

    public static List<CityLot> GenerateFrontageLotsForBlock(
        CityBlock block,
        ZoneType zoning,
        int blockIndex,
        CityData cityData,
        bool placeOutsideBlock,
        ILotSelectionPlugin lotSelectionPlugin = null)
    {
        bool isExterior = placeOutsideBlock;
        List<CityLot> lots = new List<CityLot>();
        if (block == null || cityData == null || block.vertices.Count < 3) return lots;

        float buildingHeight  = cityData.GetZoneHeight(zoning);
        List<Vector3> verts   = block.vertices;
        Vector3 blockCenter   = block.GetCenter();
        int tempID            = 0;
        float[] edgeRoadClearances = BuildRoadClearances(
            cityData, verts, LotSafetyMargin);

        // Raccolta candidati prefab con metadata valida.
        List<CityLotCandidate> candidates = CollectCandidates(zoning);
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

            // Setback basato sulla larghezza reale della strada su questo edge
            float roadSetback = edgeRoadClearances[edgeIdx];

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
                int prefabIndex = PickCandidateIndex(blockIndex, edgeIdx, lotIdx, candidates, zoning, lotSelectionPlugin);
                if (prefabIndex < 0)
                {
                    break;
                }
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
                    : IsInsideRoadClearances(lotVerts, verts, edgeRoadClearances);

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

    /// <summary>
    /// Implementazione generica del riempimento interno usata dal plugin predefinito.
    /// Plugin esterni possono ignorarla e produrre direttamente i propri CityLot.
    /// </summary>
    public static List<CityLot> GenerateGridFillLots(
        BlockLayoutOperationContext context,
        GridFillBlockLayoutOperation operation)
    {
        var result = new List<CityLot>();
        if (context == null || context.cityData == null || context.block == null ||
            context.zoneType == null || operation == null)
        {
            return result;
        }

        List<CityLotCandidate> candidates = CollectCandidates(context.zoneType);
        if (candidates.Count == 0) return result;

        List<Vector3> polygon = context.block.vertices;
        GetBlockAxes(polygon, out Vector3 tangent, out Vector3 forward);
        GetProjectionBounds(polygon, tangent, forward,
            out float minW, out float maxW, out float minD, out float maxD);

        float cellWidth = 0f;
        float cellDepth = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            Vector2 footprint = candidates[i].meta.GetAlignedFootprintSize();
            cellWidth = Mathf.Max(cellWidth, footprint.x);
            cellDepth = Mathf.Max(cellDepth, footprint.y);
        }
        if (cellWidth <= 0f || cellDepth <= 0f) return result;

        float roadMargin = GetMaximumRoadSetback(context.cityData, polygon);
        int maximumRows = Mathf.Max(1, operation.maximumRows);
        float centerY = context.block.GetCenter().y;

        var occupied = new List<Vector2[]>();
        if (context.lots != null)
        {
            for (int i = 0; i < context.lots.Count; i++)
            {
                CityLot lot = context.lots[i];
                if (lot != null && lot.vertices != null && lot.vertices.Count >= 3)
                    occupied.Add(ToXZ(lot.vertices));
            }
        }

        var reservedPolygons = new List<Vector2[]>();
        for (int i = 0; i < context.reservedAreas.Count; i++)
        {
            CityBlockLayoutArea area = context.reservedAreas[i];
            if (area != null && area.vertices != null && area.vertices.Count >= 3)
                reservedPolygons.Add(ToXZ(area.vertices));
        }

        int row = 0;
        int filledRows = 0;
        int lotSequence = 0;
        for (float d = minD + roadMargin + cellDepth * 0.5f;
             d <= maxD - roadMargin - cellDepth * 0.5f && filledRows < maximumRows;
             d += cellDepth + Mathf.Max(0f, operation.rowGap), row++)
        {
            bool filledThisRow = false;
            int column = 0;
            for (float w = minW + roadMargin + cellWidth * 0.5f;
                 w <= maxW - roadMargin - cellWidth * 0.5f;
                 w += cellWidth + Mathf.Max(0f, operation.columnGap), column++)
            {
                int candidateIndex = PickCandidateIndex(
                    context.blockIndex, 1000 + row, column, candidates,
                    context.zoneType, context.lotSelectionPlugin);
                if (candidateIndex < 0 || candidateIndex >= candidates.Count) continue;

                CityBuilderPrefab metadata = candidates[candidateIndex].meta;
                Vector2 footprint = metadata.GetAlignedFootprintSize();
                Vector3 center = tangent * w + forward * d;
                center.y = centerY;
                List<Vector3> lotVertices = BuildOrientedRectangle(
                    center, tangent, forward, footprint.x, footprint.y);
                Vector2[] lotPolygon = ToXZ(
                    lotVertices[0], lotVertices[1],
                    lotVertices[2], lotVertices[3]);

                if (!IsInsideBuildableArea(lotVertices, polygon, roadMargin) ||
                    OverlapsAny(lotPolygon, occupied) ||
                    OverlapsAny(lotPolygon, reservedPolygons))
                {
                    continue;
                }

                Vector3 desiredFront = -forward;
                Quaternion rotation = Quaternion.FromToRotation(
                    metadata.GetFrontageDirectionLocal(), desiredFront);
                result.Add(new CityLot(context.blockIndex * 100000 + lotSequence, context.block.id)
                {
                    buildingCenter = center,
                    buildingHeight = context.cityData.GetZoneHeight(context.zoneType),
                    vertices = lotVertices,
                    lotGap = operation.columnGap,
                    assignedPrefabIndex = candidateIndex,
                    assignedSpawnRotation = rotation,
                    hasAssignedSpawnRotation = true
                });
                occupied.Add(lotPolygon);
                lotSequence++;
                filledThisRow = true;
            }
            if (filledThisRow) filledRows++;
        }

        return result;
    }

    private static float GetMaximumRoadSetback(CityData cityData, List<Vector3> polygon)
    {
        float maxWidth = cityData.globalRoadWidth;
        for (int i = 0; i < polygon.Count; i++)
        {
            CitySegment segment = cityData.FindSegmentBetweenPositions(
                polygon[i], polygon[(i + 1) % polygon.Count],
                Mathf.Max(2f, cityData.globalRoadWidth));
            if (segment != null)
                maxWidth = Mathf.Max(maxWidth, segment.GetConfiguredWidth(cityData.globalRoadWidth));
        }
        return maxWidth * 0.5f + LotSafetyMargin;
    }

    private static void GetBlockAxes(
        List<Vector3> vertices,
        out Vector3 tangent,
        out Vector3 forward)
    {
        tangent = Vector3.right;
        float longest = 0f;
        for (int i = 0; i < vertices.Count; i++)
        {
            Vector3 edge = vertices[(i + 1) % vertices.Count] - vertices[i];
            edge.y = 0f;
            if (edge.sqrMagnitude > longest)
            {
                longest = edge.sqrMagnitude;
                tangent = edge.normalized;
            }
        }
        forward = new Vector3(-tangent.z, 0f, tangent.x);
    }

    private static void GetProjectionBounds(
        List<Vector3> vertices,
        Vector3 tangent,
        Vector3 forward,
        out float minW,
        out float maxW,
        out float minD,
        out float maxD)
    {
        minW = minD = float.MaxValue;
        maxW = maxD = float.MinValue;
        for (int i = 0; i < vertices.Count; i++)
        {
            float w = Vector3.Dot(vertices[i], tangent);
            float d = Vector3.Dot(vertices[i], forward);
            minW = Mathf.Min(minW, w);
            maxW = Mathf.Max(maxW, w);
            minD = Mathf.Min(minD, d);
            maxD = Mathf.Max(maxD, d);
        }
    }

    private static List<Vector3> BuildOrientedRectangle(
        Vector3 center,
        Vector3 tangent,
        Vector3 forward,
        float width,
        float depth)
    {
        Vector3 halfW = tangent * (width * 0.5f);
        Vector3 halfD = forward * (depth * 0.5f);
        return new List<Vector3>
        {
            center - halfW - halfD,
            center + halfW - halfD,
            center + halfW + halfD,
            center - halfW + halfD
        };
    }

    // ── Modalità Sparse ──────────────────────────────────────────────────────

    private static List<CityLot> GenerateSparseLotsForBlock(
        CityBlock block,
        ZoneType zoning,
        int blockIndex,
        CityData cityData,
        ILotSelectionPlugin lotSelectionPlugin,
        List<CityLot> existingLots = null,
        List<CityBlockLayoutArea> reservedAreas = null)
    {
        List<CityLot> lots = new List<CityLot>();
        if (block.vertices.Count < 3) return lots;

        float buildingHeight = cityData.GetZoneHeight(zoning);
        List<Vector3> verts  = block.vertices;
        float margin         = cityData.globalRoadWidth * 0.5f + LotSafetyMargin;
        int   tempID         = 0;

        List<CityLotCandidate> candidates = CollectCandidates(zoning);
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
        for (int i = 0; i < candidates.Count; i++)
        {
            Vector2 fp = candidates[i].meta.GetAlignedFootprintSize();
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
        if (existingLots != null)
        {
            for (int i = 0; i < existingLots.Count; i++)
            {
                CityLot existing = existingLots[i];
                if (existing != null && existing.vertices != null && existing.vertices.Count >= 3)
                    occupied.Add(ToXZ(existing.vertices));
            }
        }
        if (reservedAreas != null)
        {
            for (int i = 0; i < reservedAreas.Count; i++)
            {
                CityBlockLayoutArea area = reservedAreas[i];
                if (area != null && area.vertices != null && area.vertices.Count >= 3)
                    occupied.Add(ToXZ(area.vertices));
            }
        }
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
                int prefabIndex = PickCandidateIndex(blockIndex, cellIdx, 0, candidates, zoning, lotSelectionPlugin);
                if (prefabIndex < 0)
                {
                    cellIdx++;
                    continue;
                }
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

    public static List<CityLot> GenerateScatterLots(BlockLayoutOperationContext context)
    {
        if (context == null) return new List<CityLot>();
        return GenerateSparseLotsForBlock(
            context.block,
            context.zoneType,
            context.blockIndex,
            context.cityData,
            context.lotSelectionPlugin,
            context.lots,
            context.reservedAreas);
    }

    public static void AppendNonOverlappingLots(
        BlockLayoutOperationContext context,
        List<CityLot> candidates)
    {
        if (context == null || candidates == null) return;
        var occupied = new List<Vector2[]>();
        for (int i = 0; i < context.lots.Count; i++)
        {
            CityLot lot = context.lots[i];
            if (lot != null && lot.vertices != null && lot.vertices.Count >= 3)
                occupied.Add(ToXZ(lot.vertices));
        }
        for (int i = 0; i < context.reservedAreas.Count; i++)
        {
            CityBlockLayoutArea area = context.reservedAreas[i];
            if (area != null && area.vertices != null && area.vertices.Count >= 3)
                occupied.Add(ToXZ(area.vertices));
        }

        for (int i = 0; i < candidates.Count; i++)
        {
            CityLot candidate = candidates[i];
            if (candidate == null || candidate.vertices == null || candidate.vertices.Count < 3)
                continue;
            Vector2[] polygon = ToXZ(candidate.vertices);
            if (OverlapsAny(polygon, occupied)) continue;
            context.lots.Add(candidate);
            occupied.Add(polygon);
        }
    }

    // ── Selezione prefab ─────────────────────────────────────────────────────

    private static List<CityLotCandidate> CollectCandidates(ZoneType zone)
    {
        var result = new List<CityLotCandidate>();
        if (zone == null)
        {
            return result;
        }

        List<ZonePrefabSpawnEntry> entries = zone.GetValidPrefabEntries();
        for (int i = 0; i < entries.Count; i++)
        {
            ZonePrefabSpawnEntry entry = entries[i];
            if (entry == null || entry.prefab == null)
            {
                continue;
            }

            float weight = Mathf.Clamp01(entry.spawnProbability);
            if (weight <= 0f)
            {
                continue;
            }

            GameObject go = entry.prefab;
            CityBuilderPrefab meta = go.GetComponent<CityBuilderPrefab>();
            if (meta != null)
            {
                result.Add(new CityLotCandidate
                {
                    prefab = go,
                    meta = meta,
                    weight = weight
                });
            }
        }
        return result;
    }

    private static int PickCandidateIndex(
        int blockIdx,
        int edgeIdx,
        int lotIdx,
        List<CityLotCandidate> candidates,
        ZoneType zone,
        ILotSelectionPlugin lotSelectionPlugin)
    {
        CityLotSelectionContext context = new CityLotSelectionContext
        {
            blockIndex = blockIdx,
            edgeIndex = edgeIdx,
            lotIndex = lotIdx,
            zoneType = zone,
            candidates = candidates
        };

        return (lotSelectionPlugin ?? new DefaultLotSelectionPlugin()).PickCandidateIndex(context);
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

    /// <summary>
    /// Valida ogni vertice del lotto rispetto alla carreggiata specifica di
    /// ciascun lato del blocco. È importante agli angoli tra strade di gerarchia
    /// diversa, dove un unico setback non è sufficiente.
    /// </summary>
    private static bool IsInsideRoadClearances(
        List<Vector3> vertices,
        List<Vector3> blockPolygon,
        float[] edgeRoadClearances)
    {
        if (vertices == null || vertices.Count == 0 ||
            blockPolygon == null || blockPolygon.Count < 3 ||
            edgeRoadClearances == null ||
            edgeRoadClearances.Length != blockPolygon.Count)
        {
            return false;
        }

        for (int v = 0; v < vertices.Count; v++)
        {
            Vector3 point = vertices[v];
            if (!PointInPolygonXZ(point, blockPolygon)) return false;

            for (int edge = 0; edge < blockPolygon.Count; edge++)
            {
                Vector3 a = blockPolygon[edge];
                Vector3 b = blockPolygon[(edge + 1) % blockPolygon.Count];
                float requiredClearance = edgeRoadClearances[edge];
                if (DistancePointToSegmentXZ(point, a, b) + 0.01f < requiredClearance)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static float[] BuildRoadClearances(
        CityData cityData,
        List<Vector3> blockPolygon,
        float safetyMargin)
    {
        int edgeCount = blockPolygon != null ? blockPolygon.Count : 0;
        float[] clearances = new float[edgeCount];
        for (int edge = 0; edge < edgeCount; edge++)
        {
            clearances[edge] = GetRoadClearanceForEdge(
                cityData,
                blockPolygon[edge],
                blockPolygon[(edge + 1) % edgeCount],
                safetyMargin);
        }
        return clearances;
    }

    private static float GetRoadClearanceForEdge(
        CityData cityData,
        Vector3 edgeStart,
        Vector3 edgeEnd,
        float safetyMargin)
    {
        float fallbackWidth = cityData != null
            ? Mathf.Max(0.5f, cityData.globalRoadWidth)
            : 3f;
        CitySegment segment = cityData != null
            ? cityData.FindSegmentBetweenPositions(
                edgeStart,
                edgeEnd,
                Mathf.Max(2f, fallbackWidth))
            : null;
        float roadWidth = segment != null
            ? segment.GetConfiguredWidth(fallbackWidth)
            : fallbackWidth;
        float blockInset = segment != null && segment.roadProfile != null
            ? Mathf.Max(0f, segment.roadProfile.blockInset)
            : 0f;
        return roadWidth * 0.5f + blockInset + Mathf.Max(0f, safetyMargin);
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

    private static Vector2[] ToXZ(IList<Vector3> vertices)
    {
        Vector2[] result = new Vector2[vertices.Count];
        for (int i = 0; i < vertices.Count; i++)
            result[i] = new Vector2(vertices[i].x, vertices[i].z);
        return result;
    }
}

}
