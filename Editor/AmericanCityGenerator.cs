using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Generatore procedurale di rete stradale in stile americano.
///
/// Supporta due modalità (selezionabili in AmericanCityConfig.generationMode):
///
///   Grid      — Legacy: griglia a matrice di punti + autostrade radiali.
///               Veloce e uniforme, produce pattern perfettamente quadrettati.
///
///   Branching — Queue-based iterative branching, ispirato agli L-System
///               e agli algoritmi usati nell'industria (Cities: Skylines, etc).
///               Fa "crescere" la rete dal centro verso l'esterno, un segmento
///               alla volta, con:
///                 • regole organiche diverse per zona (CBD vs Suburbs)
///                 • snapping degli endpoint per chiudere incroci puliti
///                 • taglio automatico dei segmenti che superano il cap
///               Produce reti più realistiche e variabili.
/// </summary>
public class AmericanCityGenerator : CityGeneratorBase
{
    private readonly AmericanCityConfig config;

    public AmericanCityGenerator(AmericanCityConfig config)
    {
        this.config = config;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ENTRY POINTS
    // ──────────────────────────────────────────────────────────────────────────

    public override GenerationReport GenerateRoadNetwork(CityManager manager)
    {
        var report = new GenerationReport { warnings = new List<string>() };

        if (manager == null || config == null)
        {
            Debug.LogError("[AmericanCityGenerator] CityManager o AmericanCityConfig non assegnati.");
            return report;
        }

        CityData cityData = manager.GetCityData();
        if (cityData == null)
        {
            Debug.LogError("[AmericanCityGenerator] CityData null nel CityManager.");
            return report;
        }

        if (config.generationMode == RoadGenerationMode.Branching)
            GenerateRoadNetworkBranching(manager, ref report);
        else
            GenerateRoadNetworkGrid(manager, ref report);

        // Planarizzazione: risolve gli incroci geometrici tra segmenti
        float merge = Mathf.Max(0.1f, config.mergeThreshold);
        int splitsDone = CityRoadPlanarizer.Planarize(manager, merge);
        if (splitsDone > 0)
            report.warnings.Add($"{splitsDone} segmenti planarizzati (incroci risolti).");

        EditorUtility.SetDirty(cityData);
        SceneView.RepaintAll();

        Debug.Log($"[AmericanCityGenerator] Rete generata ({config.generationMode}): {report.nodesCreated} nodi, {report.segmentsCreated} segmenti.");
        return report;
    }

    public IEnumerator GenerateRoadNetworkAsync(
        CityManager manager,
        Action<float, string> onProgress,
        Action<GenerationReport> onCompleted = null)
    {
        var report = new GenerationReport { warnings = new List<string>() };

        if (manager == null || config == null)
        {
            Debug.LogError("[AmericanCityGenerator] CityManager o AmericanCityConfig non assegnati.");
            onCompleted?.Invoke(report);
            yield break;
        }

        CityData cityData = manager.GetCityData();
        if (cityData == null)
        {
            Debug.LogError("[AmericanCityGenerator] CityData null nel CityManager.");
            onCompleted?.Invoke(report);
            yield break;
        }

        onProgress?.Invoke(0.02f, "Preparazione generazione...");

        if (config.generationMode == RoadGenerationMode.Branching)
        {
            Vector3 p0      = config.centerWorldPosition;
            float capRadius = config.maxGenerationRadius;
            float merge     = Mathf.Max(0.1f, config.mergeThreshold);
            int   maxSegs   = config.maxBranchSegments;
            int   maxGen    = config.maxBranchGenerations;
            float majorLen  = Mathf.Max(50f, config.majorGridSpacing);

            float arteryStep = Mathf.Max(50f, Mathf.Min(majorLen, capRadius * 0.2f));
            float snap = Mathf.Max(merge, config.snapRadius);

            var rng = new System.Random(config.randomSeed);

            var confirmedEndpoints = new List<Vector3>();
            var pending = new List<PendingSegment>();

            void Enqueue(PendingSegment s)
            {
                const float sameStartEps = 1.0f;
                const float sameDirDot = 0.985f;

                for (int i = 0; i < pending.Count; i++)
                {
                    PendingSegment p = pending[i];
                    if ((p.start - s.start).sqrMagnitude > sameStartEps * sameStartEps) continue;
                    if (Vector3.Dot(p.direction, s.direction) < sameDirDot) continue;
                    if (Mathf.Abs(p.length - s.length) > merge) continue;
                    return;
                }

                s.priority = Mathf.Sqrt(
                    (s.start.x - p0.x) * (s.start.x - p0.x) +
                    (s.start.z - p0.z) * (s.start.z - p0.z));
                pending.Add(s);
            }

            GetOrCreateNode(manager, p0, merge, ref report);
            confirmedEndpoints.Add(p0);

            int hwCount = Mathf.Clamp(config.highwayCount, 1, 4);
            for (int i = 0; i < hwCount; i++)
            {
                float angleDeg = i * (180f / hwCount);
                float rad = angleDeg * Mathf.Deg2Rad;
                Vector3 dirA = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
                Enqueue(new PendingSegment { start = p0, direction = dirA,  length = arteryStep, generation = 0, isHighway = true });
                Enqueue(new PendingSegment { start = p0, direction = -dirA, length = arteryStep, generation = 0, isHighway = true });

                Vector3 orthoA = Quaternion.Euler(0f,  90f, 0f) * dirA;
                Vector3 orthoB = Quaternion.Euler(0f, -90f, 0f) * dirA;
                Enqueue(new PendingSegment { start = p0, direction = orthoA, length = arteryStep, generation = 0 });
                Enqueue(new PendingSegment { start = p0, direction = orthoB, length = arteryStep, generation = 0 });
            }

            int confirmedCount = 0;
            int loopCount = 0;
            const int yieldEvery = 8;

            while (pending.Count > 0 && confirmedCount < maxSegs)
            {
                PendingSegment seg = DequeueNearest(pending);
                if (seg.generation > maxGen) continue;

                Vector3 proposedEnd = seg.start + seg.direction * seg.length;

                float distEnd = Mathf.Sqrt(
                    (proposedEnd.x - p0.x) * (proposedEnd.x - p0.x) +
                    (proposedEnd.z - p0.z) * (proposedEnd.z - p0.z));

                if (distEnd > capRadius)
                {
                    float distStart = Mathf.Sqrt(
                        (seg.start.x - p0.x) * (seg.start.x - p0.x) +
                        (seg.start.z - p0.z) * (seg.start.z - p0.z));
                    if (distStart >= capRadius) continue;

                    float clampedLen = Mathf.Max(merge * 2f,
                        seg.length * (capRadius - distStart) / Mathf.Max(0.001f, distEnd - distStart));
                    proposedEnd = seg.start + seg.direction * clampedLen;
                    seg.length  = clampedLen;
                }

                float bestSnapDist = snap;
                Vector3 snappedEnd = proposedEnd;
                for (int ei = 0; ei < confirmedEndpoints.Count; ei++)
                {
                    float dx = proposedEnd.x - confirmedEndpoints[ei].x;
                    float dz = proposedEnd.z - confirmedEndpoints[ei].z;
                    float d  = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d < bestSnapDist && d > merge * 0.5f)
                    {
                        bestSnapDist = d;
                        snappedEnd = confirmedEndpoints[ei];
                    }
                }
                if (bestSnapDist < snap)
                {
                    proposedEnd = snappedEnd;
                    seg.length  = Vector3.Distance(seg.start, proposedEnd);
                    if (seg.length < merge) continue;
                }

                CityNode nodeA = GetOrCreateNode(manager, seg.start,   merge, ref report);
                CityNode nodeB = GetOrCreateNode(manager, proposedEnd, merge, ref report);
                if (nodeA == null || nodeB == null || nodeA.id == nodeB.id) continue;

                CitySegment roadSeg = manager.AddSegment(nodeA.id, nodeB.id);
                if (roadSeg == null) continue;

                RoadProfile profile = seg.isHighway ? config.highwayProfile
                                    : (seg.isLocal  ? config.localStreetProfile
                                                    : config.majorGridProfile);
                ApplyProfile(roadSeg, profile);

                report.segmentsCreated++;
                confirmedCount++;
                confirmedEndpoints.Add(proposedEnd);

                float distFromCenter = Mathf.Sqrt(
                    (proposedEnd.x - p0.x) * (proposedEnd.x - p0.x) +
                    (proposedEnd.z - p0.z) * (proposedEnd.z - p0.z));
                bool isCBD     = distFromCenter < capRadius * 0.25f;
                bool isSuburbs = distFromCenter > capRadius * 0.55f;

                if (seg.isHighway)
                    BranchHighway(Enqueue, seg, proposedEnd, arteryStep, rng);
                else
                    BranchMajor(Enqueue, seg, proposedEnd, arteryStep, isCBD, isSuburbs, rng);

                loopCount++;
                if (loopCount % yieldEvery == 0)
                {
                    float p = Mathf.Lerp(0.05f, 0.88f, maxSegs <= 0 ? 1f : (float)confirmedCount / maxSegs);
                    onProgress?.Invoke(p, $"Generazione rete: {confirmedCount}/{maxSegs} segmenti");
                    yield return null;
                }
            }

            if (confirmedCount >= maxSegs)
                report.warnings.Add($"Limite maxBranchSegments ({maxSegs}) raggiunto. Aumenta il valore o riduci il raggio/generazioni.");
        }
        else
        {
            onProgress?.Invoke(0.15f, "Generazione rete griglia...");
            GenerateRoadNetworkGrid(manager, ref report);
            yield return null;
        }

        onProgress?.Invoke(0.92f, "Planarizzazione incroci...");
        float mergePlanarize = Mathf.Max(0.1f, config.mergeThreshold);
        int splitsDone = CityRoadPlanarizer.Planarize(manager, mergePlanarize);
        if (splitsDone > 0)
            report.warnings.Add($"{splitsDone} segmenti planarizzati (incroci risolti).");

        EditorUtility.SetDirty(cityData);
        SceneView.RepaintAll();

        onProgress?.Invoke(1f, "Generazione completata");
        Debug.Log($"[AmericanCityGenerator] Rete generata ({config.generationMode}): {report.nodesCreated} nodi, {report.segmentsCreated} segmenti.");
        onCompleted?.Invoke(report);
    }

    public override GenerationReport AssignZoningByDistance(CityManager manager)
    {
        var report = new GenerationReport { warnings = new List<string>() };

        if (manager == null || config == null)
        {
            Debug.LogError("[AmericanCityGenerator] CityManager o AmericanCityConfig non assegnati.");
            return report;
        }

        CityData cityData = manager.GetCityData();
        if (cityData == null) return report;

        Undo.RecordObject(cityData, "Assign Zoning By Distance");

        Vector3 p0 = config.centerWorldPosition;

        foreach (CityBlock block in cityData.blocks)
        {
            if (block == null) continue;

            Vector3 center = block.GetCenter();
            float dist = Mathf.Sqrt(
                (center.x - p0.x) * (center.x - p0.x) +
                (center.z - p0.z) * (center.z - p0.z));

            ZoneType zone = config.GetZoneTypeForDistance(dist);
            if (zone == null)
            {
                report.warnings.Add($"Block {block.id}: nessuna zona mappata per dist={dist:F0}m");
                continue;
            }

            manager.SetBlockZoning(block.id, zone);
            block.orientation = config.GetOrientationForDistance(dist);
            report.blocksZoned++;
        }

        EditorUtility.SetDirty(cityData);
        SceneView.RepaintAll();

        Debug.Log($"[AmericanCityGenerator] Zoning per distanza: {report.blocksZoned} blocchi.");
        return report;
    }

    // ──────────────────────────────────────────────────────────────────────────
    // MODALITÀ BRANCHING
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Struttura interna che rappresenta un "seme di strada" in attesa di essere valutato.
    /// </summary>
    private struct PendingSegment
    {
        public Vector3 start;
        public Vector3 direction;   // normalizzato
        public float length;
        public int generation;
        public bool isHighway;
        public bool isLocal;        // strade locali di secondo livello
        public float priority;      // distanza start→p0; minore = processato prima
    }

    // Estrae dalla lista il PendingSegment con priorità minima (più vicino al centro) in O(n).
    // Rimozione O(1) via swap con l'ultimo elemento.
    private static PendingSegment DequeueNearest(List<PendingSegment> list)
    {
        int minIdx = 0;
        float minP = list[0].priority;
        for (int i = 1; i < list.Count; i++)
            if (list[i].priority < minP) { minP = list[i].priority; minIdx = i; }
        PendingSegment result = list[minIdx];
        list[minIdx] = list[list.Count - 1];
        list.RemoveAt(list.Count - 1);
        return result;
    }

    private void GenerateRoadNetworkBranching(CityManager manager, ref GenerationReport report)
    {
        Vector3 p0      = config.centerWorldPosition;
        float capRadius = config.maxGenerationRadius;
        float merge     = Mathf.Max(0.1f, config.mergeThreshold);
        int   maxSegs   = config.maxBranchSegments;
        int   maxGen    = config.maxBranchGenerations;
        float majorLen  = Mathf.Max(50f, config.majorGridSpacing);

        // arteryStep: passo di ogni segmento di branching.
        // Auto-scalato a capRadius*0.2 → garantisce ≥ 5 passi visibili entro il raggio.
        // Con preset (majorLen=1600, cap=2400) → 480 m.
        // Con città grande (cap=10000, majorLen=1600) → 1600 m (comportamento originale).
        float arteryStep = Mathf.Max(50f, Mathf.Min(majorLen, capRadius * 0.2f));

        // Snap fisso dal config: evita agganci troppo aggressivi su nodi lontani.
        float snap = Mathf.Max(merge, config.snapRadius);

        var rng = new System.Random(config.randomSeed);

        var confirmedEndpoints = new List<Vector3>();
        var pending = new List<PendingSegment>();

        void Enqueue(PendingSegment s)
        {
            const float sameStartEps = 1.0f;
            const float sameDirDot = 0.985f;

            for (int i = 0; i < pending.Count; i++)
            {
                PendingSegment p = pending[i];
                if ((p.start - s.start).sqrMagnitude > sameStartEps * sameStartEps) continue;
                if (Vector3.Dot(p.direction, s.direction) < sameDirDot) continue;
                if (Mathf.Abs(p.length - s.length) > merge) continue;
                return;
            }

            s.priority = Mathf.Sqrt(
                (s.start.x - p0.x) * (s.start.x - p0.x) +
                (s.start.z - p0.z) * (s.start.z - p0.z));
            pending.Add(s);
        }

        // ── Nodo centrale ──────────────────────────────────────────────────────
        GetOrCreateNode(manager, p0, merge, ref report);
        confirmedEndpoints.Add(p0);

        // ── Seed: autostrade radiali + assi principali ────────────────────────
        int hwCount = Mathf.Clamp(config.highwayCount, 1, 4);
        for (int i = 0; i < hwCount; i++)
        {
            float angleDeg = i * (180f / hwCount);
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 dirA = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
            Enqueue(new PendingSegment { start = p0, direction = dirA,  length = arteryStep, generation = 0, isHighway = true });
            Enqueue(new PendingSegment { start = p0, direction = -dirA, length = arteryStep, generation = 0, isHighway = true });

            // Assi ortogonali per griglia principale (evitano buchi se hwCount < 2)
            Vector3 orthoA = Quaternion.Euler(0f,  90f, 0f) * dirA;
            Vector3 orthoB = Quaternion.Euler(0f, -90f, 0f) * dirA;
            Enqueue(new PendingSegment { start = p0, direction = orthoA, length = arteryStep, generation = 0 });
            Enqueue(new PendingSegment { start = p0, direction = orthoB, length = arteryStep, generation = 0 });
        }

        int confirmedCount = 0;

        // ── Loop principale: estrae sempre il segmento con start più vicino al centro ──
        while (pending.Count > 0 && confirmedCount < maxSegs)
        {
            PendingSegment seg = DequeueNearest(pending);
            if (seg.generation > maxGen) continue;

            Vector3 proposedEnd = seg.start + seg.direction * seg.length;

            // ── Cap circolare (taglia al bordo) ────────────────────────────────
            float distEnd = Mathf.Sqrt(
                (proposedEnd.x - p0.x) * (proposedEnd.x - p0.x) +
                (proposedEnd.z - p0.z) * (proposedEnd.z - p0.z));

            if (distEnd > capRadius)
            {
                float distStart = Mathf.Sqrt(
                    (seg.start.x - p0.x) * (seg.start.x - p0.x) +
                    (seg.start.z - p0.z) * (seg.start.z - p0.z));
                if (distStart >= capRadius) continue;

                float clampedLen = Mathf.Max(merge * 2f,
                    seg.length * (capRadius - distStart) / Mathf.Max(0.001f, distEnd - distStart));
                proposedEnd = seg.start + seg.direction * clampedLen;
                seg.length  = clampedLen;
            }

            // ── Snapping: unisci a un nodo vicino ─────────────────────────────
            float bestSnapDist = snap;
            Vector3 snappedEnd = proposedEnd;
            for (int ei = 0; ei < confirmedEndpoints.Count; ei++)
            {
                float dx = proposedEnd.x - confirmedEndpoints[ei].x;
                float dz = proposedEnd.z - confirmedEndpoints[ei].z;
                float d  = Mathf.Sqrt(dx * dx + dz * dz);
                if (d < bestSnapDist && d > merge * 0.5f)
                {
                    bestSnapDist = d;
                    snappedEnd = confirmedEndpoints[ei];
                }
            }
            if (bestSnapDist < snap)
            {
                proposedEnd = snappedEnd;
                seg.length  = Vector3.Distance(seg.start, proposedEnd);
                if (seg.length < merge) continue;
            }

            // ── Aggiunge il segmento al grafo ──────────────────────────────────
            CityNode nodeA = GetOrCreateNode(manager, seg.start,   merge, ref report);
            CityNode nodeB = GetOrCreateNode(manager, proposedEnd, merge, ref report);
            if (nodeA == null || nodeB == null || nodeA.id == nodeB.id) continue;

            CitySegment roadSeg = manager.AddSegment(nodeA.id, nodeB.id);
            if (roadSeg == null) continue; // già esistente

            RoadProfile profile = seg.isHighway ? config.highwayProfile
                                : (seg.isLocal  ? config.localStreetProfile
                                                : config.majorGridProfile);
            ApplyProfile(roadSeg, profile);

            report.segmentsCreated++;
            confirmedCount++;
            confirmedEndpoints.Add(proposedEnd);

            // ── Zona per stile urbanistico ─────────────────────────────────────
            float distFromCenter = Mathf.Sqrt(
                (proposedEnd.x - p0.x) * (proposedEnd.x - p0.x) +
                (proposedEnd.z - p0.z) * (proposedEnd.z - p0.z));
            bool isCBD     = distFromCenter < capRadius * 0.25f;
            bool isSuburbs = distFromCenter > capRadius * 0.55f;

            if (seg.isHighway)
                BranchHighway(Enqueue, seg, proposedEnd, arteryStep, rng);
            else
                BranchMajor(Enqueue, seg, proposedEnd, arteryStep, isCBD, isSuburbs, rng);
        }

        if (confirmedCount >= maxSegs)
            report.warnings.Add($"Limite maxBranchSegments ({maxSegs}) raggiunto. Aumenta il valore o riduci il raggio/generazioni.");
        // Nota: in modalità Branching la rete è generata organicamente dal loop sopra.
        // GenerateLocalStreets (griglia deterministica) è usata solo dalla modalità Grid.
    }

    // ── Branching: autostrade ─────────────────────────────────────────────────

    private void BranchHighway(System.Action<PendingSegment> enqueue, PendingSegment seg,
        Vector3 end, float step, System.Random rng)
    {
        // Continua sempre dritto
        enqueue(new PendingSegment
        {
            start = end, direction = seg.direction,
            length = step, generation = seg.generation + 1, isHighway = true
        });

        // Svincolo opzionale singolo (sinistra o destra): evita fan-out esplosivo.
        if (rng.NextDouble() < (double)config.cbdBranchProbability)
        {
            float side = rng.NextDouble() < 0.5 ? -90f : 90f;
            enqueue(new PendingSegment
            {
                start = end, direction = Quaternion.Euler(0f, side, 0f) * seg.direction,
                length = step, generation = seg.generation + 1, isHighway = false
            });
        }
    }

    // ── Branching: strade principali ──────────────────────────────────────────

    private void BranchMajor(System.Action<PendingSegment> enqueue, PendingSegment seg,
        Vector3 end, float step,
        bool isCBD, bool isSuburbs, System.Random rng)
    {
        float t = isSuburbs ? 0f : (isCBD ? 1f : 0.5f);
        float straightProb = Mathf.Lerp((float)config.suburbStraightProbability, (float)config.cbdStraightProbability, t);
        float branchProb   = Mathf.Lerp((float)config.suburbBranchProbability,   (float)config.cbdBranchProbability,   t);

        // Continua dritto (con deviazione organica fuori CBD)
        if (rng.NextDouble() < straightProb)
        {
            Vector3 dir = seg.direction;
            if (!isCBD)
            {
                float jitter = isSuburbs ? 25f : 12f;
                dir = Quaternion.Euler(0f, (float)(rng.NextDouble() * 2.0 - 1.0) * jitter, 0f) * dir;
            }
            enqueue(new PendingSegment { start = end, direction = dir, length = step, generation = seg.generation + 1 });
        }

        if (isCBD)
        {
            if (rng.NextDouble() < branchProb)
            {
                float angle = -90f;
                enqueue(new PendingSegment { start = end, direction = Quaternion.Euler(0f, angle, 0f) * seg.direction, length = step, generation = seg.generation + 1 });
            }
            if (rng.NextDouble() < branchProb)
            {
                float angle = 90f;
                enqueue(new PendingSegment { start = end, direction = Quaternion.Euler(0f, angle, 0f) * seg.direction, length = step, generation = seg.generation + 1 });
            }
        }
        else if (rng.NextDouble() < branchProb)
        {
            float sign = rng.NextDouble() < 0.5 ? -1f : 1f;
            float angle = sign * (30f + (float)(rng.NextDouble() * 40f));
            enqueue(new PendingSegment { start = end, direction = Quaternion.Euler(0f, angle, 0f) * seg.direction, length = step, generation = seg.generation + 1 });
        }
    }

    // ── Branching: strade locali (spawned da BranchMajor nei suburbs) ─────────

    private void BranchLocal(System.Action<PendingSegment> enqueue, PendingSegment seg,
        Vector3 end, float step,
        bool isCBD, bool isSuburbs, System.Random rng)
    {
        float straightProb = isCBD ? (float)config.cbdStraightProbability
                                   : (float)config.suburbStraightProbability;
        float branchProb   = isCBD ? (float)config.cbdBranchProbability  * 0.5f
                                   : (float)config.suburbBranchProbability * 0.4f;

        if (rng.NextDouble() < straightProb)
        {
            Vector3 dir = seg.direction;
            if (!isCBD && isSuburbs)
                dir = Quaternion.Euler(0f, (float)(rng.NextDouble() * 2.0 - 1.0) * 20f, 0f) * dir;
            enqueue(new PendingSegment { start = end, direction = dir, length = step, generation = seg.generation + 1, isLocal = true });
        }

        if (rng.NextDouble() < branchProb)
        {
            float a = 45f + (float)(rng.NextDouble() * 30f);
            enqueue(new PendingSegment { start = end, direction = Quaternion.Euler(0f, -a, 0f) * seg.direction, length = step, generation = seg.generation + 1, isLocal = true });
        }

        if (rng.NextDouble() < branchProb)
        {
            float a = 45f + (float)(rng.NextDouble() * 30f);
            enqueue(new PendingSegment { start = end, direction = Quaternion.Euler(0f, a, 0f) * seg.direction, length = step, generation = seg.generation + 1, isLocal = true });
        }
    }

    // ──────────────────────────────────────────────────────────────────────────
    // MODALITÀ GRID (Legacy)
    // ──────────────────────────────────────────────────────────────────────────

    private void GenerateRoadNetworkGrid(CityManager manager, ref GenerationReport report)
    {
        Vector3 p0      = config.centerWorldPosition;
        float capRadius = config.maxGenerationRadius;
        float merge     = Mathf.Max(0.1f, config.mergeThreshold);

        CityNode centerNode = GetOrCreateNode(manager, p0, merge, ref report);
        GenerateHighways(manager, centerNode, p0, capRadius, merge, ref report);
        GenerateMajorGrid(manager, p0, capRadius, merge, ref report);

        float localCap = Mathf.Min(capRadius, config.localStreetMaxRadius);
        bool localEnabled = localCap > 0f &&
            config.localStreetSpacing > 0f &&
            config.localStreetSpacing < config.majorGridSpacing * 0.95f;

        if (localEnabled)
            GenerateLocalStreets(manager, p0, localCap, merge, ref report);
    }

    private void GenerateHighways(
        CityManager manager, CityNode centerNode, Vector3 p0,
        float capRadius, float merge, ref GenerationReport report)
    {
        int hwCount = Mathf.Clamp(config.highwayCount, 1, 4);
        float step  = Mathf.Max(50f, config.majorGridSpacing);
        RoadProfile profile = config.highwayProfile;

        for (int i = 0; i < hwCount; i++)
        {
            float angleDeg = i * (180f / hwCount);
            float rad = angleDeg * Mathf.Deg2Rad;
            Vector3 dirA = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)).normalized;
            GenerateHighwayArm(manager, centerNode, p0, dirA,  step, capRadius, merge, profile, ref report);
            GenerateHighwayArm(manager, centerNode, p0, -dirA, step, capRadius, merge, profile, ref report);
        }
    }

    private void GenerateHighwayArm(
        CityManager manager, CityNode centerNode, Vector3 p0,
        Vector3 direction, float step, float capRadius,
        float merge, RoadProfile profile, ref GenerationReport report)
    {
        CityNode prevNode = centerNode;
        float dist = step;
        while (dist <= capRadius + step * 0.01f)
        {
            Vector3 pos = p0 + direction * dist;
            CityNode node = GetOrCreateNode(manager, pos, merge, ref report);
            if (node == null) break;
            CitySegment seg = manager.AddSegment(prevNode.id, node.id);
            if (seg != null) { ApplyProfile(seg, profile); report.segmentsCreated++; }
            prevNode = node;
            dist += step;
        }
    }

    private void GenerateMajorGrid(
        CityManager manager, Vector3 p0, float capRadius,
        float merge, ref GenerationReport report)
    {
        float spacing   = Mathf.Max(50f, config.majorGridSpacing);
        RoadProfile profile = config.majorGridProfile;
        int halfSteps   = Mathf.CeilToInt(capRadius / spacing);
        var gridNodes   = new Dictionary<(int, int), CityNode>();

        for (int ix = -halfSteps; ix <= halfSteps; ix++)
            for (int iz = -halfSteps; iz <= halfSteps; iz++)
            {
                float x = p0.x + ix * spacing;
                float z = p0.z + iz * spacing;
                if ((x - p0.x) * (x - p0.x) + (z - p0.z) * (z - p0.z) > capRadius * capRadius) continue;
                CityNode node = GetOrCreateNode(manager, new Vector3(x, p0.y, z), merge, ref report);
                if (node != null) gridNodes[(ix, iz)] = node;
            }

        for (int iz = -halfSteps; iz <= halfSteps; iz++)
            for (int ix = -halfSteps; ix < halfSteps; ix++)
            {
                if (!gridNodes.TryGetValue((ix, iz), out CityNode a)) continue;
                if (!gridNodes.TryGetValue((ix + 1, iz), out CityNode b)) continue;
                CitySegment seg = manager.AddSegment(a.id, b.id);
                if (seg != null) { ApplyProfile(seg, profile); report.segmentsCreated++; }
            }

        for (int ix = -halfSteps; ix <= halfSteps; ix++)
            for (int iz = -halfSteps; iz < halfSteps; iz++)
            {
                if (!gridNodes.TryGetValue((ix, iz), out CityNode a)) continue;
                if (!gridNodes.TryGetValue((ix, iz + 1), out CityNode b)) continue;
                CitySegment seg = manager.AddSegment(a.id, b.id);
                if (seg != null) { ApplyProfile(seg, profile); report.segmentsCreated++; }
            }
    }

    private static float[] GetJitteredPositions(
        int steps, float total, float nominal, float variation, System.Random rng)
    {
        float[] pos = new float[steps + 1];
        pos[0] = 0f; pos[steps] = total;
        float minGap = nominal * 0.20f;
        for (int i = 1; i < steps; i++)
        {
            float center = i * nominal;
            float delta  = (float)(rng.NextDouble() * 2.0 - 1.0) * variation * nominal;
            float lo     = pos[i - 1] + minGap;
            float hi     = total - (steps - i) * minGap;
            pos[i] = Mathf.Clamp(center + delta, lo, hi);
        }
        return pos;
    }

    private void GenerateLocalStreets(
        CityManager manager, Vector3 p0, float localCap,
        float merge, ref GenerationReport report)
    {
        float majorSpacing  = Mathf.Max(50f, config.majorGridSpacing);
        float localSpacing  = Mathf.Max(20f, config.localStreetSpacing);
        float variation     = Mathf.Clamp01(config.blockSizeVariation);
        float depthMult     = Mathf.Max(1f, config.blockDepthMultiplier);
        RoadProfile profile = config.localStreetProfile;
        int halfMajorSteps  = Mathf.CeilToInt(localCap / majorSpacing);

        for (int cx = -halfMajorSteps; cx < halfMajorSteps; cx++)
            for (int cz = -halfMajorSteps; cz < halfMajorSteps; cz++)
            {
                float ccx = p0.x + (cx + 0.5f) * majorSpacing;
                float ccz = p0.z + (cz + 0.5f) * majorSpacing;
                if ((ccx - p0.x) * (ccx - p0.x) + (ccz - p0.z) * (ccz - p0.z) > localCap * localCap) continue;

                float xMin = p0.x + cx * majorSpacing;
                float zMin = p0.z + cz * majorSpacing;
                int stepsX = Mathf.Max(2, Mathf.RoundToInt(majorSpacing / localSpacing));
                int stepsZ = Mathf.Max(2, Mathf.RoundToInt(majorSpacing / (localSpacing * depthMult)));
                if (stepsX < 2 && stepsZ < 2) continue;

                var rng = new System.Random(config.randomSeed ^ (cx * 73856093) ^ (cz * 19349663));
                float[] xPos = GetJitteredPositions(stepsX, majorSpacing, majorSpacing / stepsX, variation, rng);
                float[] zPos = GetJitteredPositions(stepsZ, majorSpacing, majorSpacing / stepsZ, variation, rng);

                var localNodes = new Dictionary<(int, int), CityNode>();
                for (int lx = 0; lx <= stepsX; lx++)
                    for (int lz = 0; lz <= stepsZ; lz++)
                    {
                        CityNode node = GetOrCreateNode(manager, new Vector3(xMin + xPos[lx], p0.y, zMin + zPos[lz]), merge, ref report);
                        if (node != null) localNodes[(lx, lz)] = node;
                    }

                for (int lz = 1; lz < stepsZ; lz++)
                    for (int lx = 0; lx < stepsX; lx++)
                    {
                        if (!localNodes.TryGetValue((lx, lz), out CityNode a)) continue;
                        if (!localNodes.TryGetValue((lx + 1, lz), out CityNode b)) continue;
                        if (a.id == b.id) continue;
                        CitySegment seg = manager.AddSegment(a.id, b.id);
                        if (seg != null) { ApplyProfile(seg, profile); report.segmentsCreated++; }
                    }

                for (int lx = 1; lx < stepsX; lx++)
                    for (int lz = 0; lz < stepsZ; lz++)
                    {
                        if (!localNodes.TryGetValue((lx, lz), out CityNode a)) continue;
                        if (!localNodes.TryGetValue((lx, lz + 1), out CityNode b)) continue;
                        if (a.id == b.id) continue;
                        CitySegment seg = manager.AddSegment(a.id, b.id);
                        if (seg != null) { ApplyProfile(seg, profile); report.segmentsCreated++; }
                    }

                // ── Vicoli ──────────────────────────────────────────────────
                if (config.alleyEnabled && config.alleyProfile != null)
                {
                    float alleyFrac = Mathf.Clamp(config.alleyPositionFraction, 0.3f, 0.7f);
                    // Un vicolo per ogni strip Z (tra lz e lz+1), parallelo all'asse X
                    for (int lz = 0; lz < stepsZ; lz++)
                    {
                        float alleyZ = zMin + zPos[lz] + (zPos[lz + 1] - zPos[lz]) * alleyFrac;
                        CityNode prevAlley = null;
                        for (int lx = 0; lx <= stepsX; lx++)
                        {
                            Vector3 pos = new Vector3(xMin + xPos[lx], p0.y, alleyZ);
                            CityNode node = GetOrCreateNode(manager, pos, merge, ref report);
                            if (node == null) continue;
                            if (prevAlley != null && prevAlley.id != node.id)
                            {
                                CitySegment seg = manager.AddSegment(prevAlley.id, node.id);
                                if (seg != null) { ApplyProfile(seg, config.alleyProfile); report.segmentsCreated++; }
                            }
                            prevAlley = node;
                        }
                    }
                }
            }
    }
}
