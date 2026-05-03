using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Modalità di generazione della rete stradale.
/// </summary>
public enum RoadGenerationMode
{
    Grid       // Modalità supportata
}

/// <summary>
/// Rappresenta una fascia di distanza da P0 con la zona e l'orientamento lotti associati.
/// Gli anelli devono essere ordinati per maxRadius crescente; l'ultimo ring cattura
/// tutto ciò che supera il suo maxRadius.
/// </summary>
[System.Serializable]
public class ZoneRing
{
    [Tooltip("Nome descrittivo (es. CBD, Inner City, Suburbs).")]
    public string label = "Zone Ring";

    [Tooltip("Limite superiore della fascia in metri. I blocchi la cui distanza da P0 è ≤ maxRadius (e > maxRadius del ring precedente) ricevono questa zona.")]
    [Min(0f)]
    public float maxRadius = 1000f;

    [Tooltip("ZoneType da assegnare ai blocchi in questa fascia.")]
    public ZoneType zoneType;

    [Tooltip("Orientamento lotti per i blocchi in questa fascia.")]
    public BlockOrientation orientation = BlockOrientation.Interior;
}

/// <summary>
/// Configurazione per la generazione procedurale di una città in stile americano.
/// Usa una lista libera di ZoneRing per definire le fasce zonali per distanza:
/// aggiungi, rimuovi o modifica i ring senza limiti predefiniti.
/// </summary>
[CreateAssetMenu(fileName = "AmericanCityConfig", menuName = "City Builder/American City Config")]
public class AmericanCityConfig : ScriptableObject
{
    [Header("Centro Città (P0)")]
    [Tooltip("Punto di massima densità (CBD). Tutte le distanze zonali sono misurate da qui.")]
    public Vector3 centerWorldPosition = Vector3.zero;

    [Header("Cap Generazione")]
    [Tooltip("Raggio massimo di generazione in unità world (1 u = 1 m). Ridurre per scene di gioco più piccole. Default: 3000 m.")]
    [Min(1f)]
    public float maxGenerationRadius = 3000f;

    [Header("Algoritmo di Generazione")]
    [Tooltip("Modalità di generazione stradale. Branching rimosso: il sistema usa solo Grid.")]
    public RoadGenerationMode generationMode = RoadGenerationMode.Grid;

    [Header("Griglia Stradale")]
    [Tooltip("Spaziatura griglia principale (Major Grid) in metri. Default americano: 1600 m = 1 miglio.")]
    [Min(50f)]
    public float majorGridSpacing = 1600f;

    [Tooltip("Spaziatura strade locali all'interno di ogni cella della griglia principale, in metri.")]
    [Min(20f)]
    public float localStreetSpacing = 300f;

    [Tooltip("Raggio massimo entro cui vengono generate strade locali (sub-griglia). 0 = disabilita.")]
    [Min(0f)]
    public float localStreetMaxRadius = 5000f;

    [Tooltip("Variazione casuale della posizione delle strade locali interne. 0 = griglia perfetta, 0.4 = fino al 40% di spostamento per strada.")]
    [Range(0f, 0.45f)]
    public float blockSizeVariation = 0.25f;

    [Tooltip("Seme per la variazione casuale dei blocchi (stesso seme = stessa città).")]
    public int randomSeed = 42;

    [Tooltip("Numero di autostrade radiali complete (ogni autostrada genera 2 bracci opposti dal centro).")]
    [Range(1, 4)]
    public int highwayCount = 2;

    [Tooltip("Distanza soglia (m) entro cui due nodi vengono uniti per evitare duplicati.")]
    [Min(0.1f)]
    public float mergeThreshold = 2f;

    [Header("Branching - Parametri Avanzati")]
    [Tooltip("Numero massimo di segmenti generabili dal branching (limite di sicurezza).")]
    [Min(10)]
    public int maxBranchSegments = 8000;

    [Tooltip("Profondità massima di ramificazione (generazioni). Più alto = città più grande.")]
    [Min(1)]
    public int maxBranchGenerations = 12;

    [Tooltip("Probabilità (0-1) che una strada continui dritto ad ogni iterazione (zona CBD).")]
    [Range(0f, 1f)]
    public float cbdStraightProbability = 0.95f;

    [Tooltip("Probabilità (0-1) che una strada generi una diramazione laterale a 90° (zona CBD).")]
    [Range(0f, 1f)]
    public float cbdBranchProbability = 0.80f;

    [Tooltip("Probabilità che una strada locale continui dritto nei suburbs.")]
    [Range(0f, 1f)]
    public float suburbStraightProbability = 0.75f;

    [Tooltip("Probabilità di diramazione organica nei suburbs (angolo casuale 30-70°).")]
    [Range(0f, 1f)]
    public float suburbBranchProbability = 0.40f;

    [Tooltip("Raggio di snapping: due endpoint entro questa distanza vengono uniti in un nodo condiviso.")]
    [Min(0.5f)]
    public float snapRadius = 8f;

    [Header("Branching - Seed 360")]
    [Tooltip("Numero di direzioni iniziali dal centro P0 per la modalità Branching. Le direzioni sono distribuite uniformemente su 360°.")]
    [Range(2, 16)]
    public int initialNumDirections = 4;

    [Header("Branching - Ventaglio Parametrico")]
    [Tooltip("Numero totale di rami per segmento in CBD (include il ramo dritto).")]
    [Range(1, 6)]
    public int cbdBranchCount = 3;

    [Tooltip("Sweep totale in gradi del ventaglio CBD.")]
    [Range(0f, 360f)]
    public float cbdBranchSweepAngle = 180f;

    [Tooltip("Jitter angolare casuale in gradi applicato ai rami laterali CBD.")]
    [Range(0f, 30f)]
    public float cbdBranchJitter = 0f;

    [Tooltip("Numero totale di rami per segmento in Suburbs (include il ramo dritto).")]
    [Range(1, 6)]
    public int suburbBranchCount = 3;

    [Tooltip("Sweep totale in gradi del ventaglio Suburbs.")]
    [Range(0f, 360f)]
    public float suburbBranchSweepAngle = 120f;

    [Tooltip("Jitter angolare casuale in gradi applicato ai rami laterali Suburbs.")]
    [Range(0f, 45f)]
    public float suburbBranchJitter = 15f;

    [Tooltip("Se attivo, i rami laterali vengono distribuiti in modo simmetrico rispetto alla direzione corrente.")]
    public bool suburbBranchSymmetric = true;

    [Tooltip("Numero totale di rami per segmento in Local (include il ramo dritto).")]
    [Range(1, 4)]
    public int localBranchCount = 2;

    [Tooltip("Sweep totale in gradi del ventaglio Local.")]
    [Range(0f, 180f)]
    public float localBranchSweepAngle = 90f;

    [Tooltip("Jitter angolare casuale in gradi applicato ai rami laterali Local.")]
    [Range(0f, 30f)]
    public float localBranchJitter = 10f;

    [Header("Blocchi - Proporzioni")]
    [Tooltip("Rapporto profondità/larghezza dei blocchi locali. 2.0 = blocchi 1:2 (es. 100×200 m con localStreetSpacing=100).")]
    [Range(1f, 4f)]
    public float blockDepthMultiplier = 2.0f;

    [Header("Vicoli (Alley)")]
    [Tooltip("Abilita la generazione di vicoli al centro di ogni blocco (modalità Grid e alley-pass finale in Branching).")]
    public bool alleyEnabled = true;

    [Tooltip("Profilo stradale per i vicoli. Usa tipicamente 'Vicolo' (larghezza 5 m).")]
    public RoadProfile alleyProfile;

    [Tooltip("Posizione del vicolo come frazione della profondità del blocco. 0.5 = centro esatto.")]
    [Range(0.3f, 0.7f)]
    public float alleyPositionFraction = 0.5f;

    [Tooltip("Raggio massimo entro cui vengono generati i vicoli (m). 0 = disabilita.")]
    [Min(0f)]
    public float alleyMaxRadius = 2400f;

    [Header("Zone Rings (fascia distanza → zona)")]
    [Tooltip("Fasce zonali ordinate per maxRadius crescente. L'ultimo ring cattura tutto ciò che supera il suo raggio.")]
    public List<ZoneRing> zoneRings = new List<ZoneRing>();

    [Header("Mapping Road Profiles")]
    [Tooltip("Profilo stradale per le autostrade radiali.")]
    public RoadProfile highwayProfile;

    [Tooltip("Profilo stradale per la griglia principale (Major Grid).")]
    public RoadProfile majorGridProfile;

    [Tooltip("Profilo stradale per le strade locali all'interno delle celle.")]
    public RoadProfile localStreetProfile;

    // ========== HELPERS ==========

    /// <summary>
    /// Restituisce il ZoneType del ring corrispondente alla distanza da P0.
    /// </summary>
    public ZoneType GetZoneTypeForDistance(float distance)
    {
        return GetRingForDistance(distance)?.zoneType;
    }

    /// <summary>
    /// Restituisce l'orientamento lotti del ring corrispondente alla distanza da P0.
    /// </summary>
    public BlockOrientation GetOrientationForDistance(float distance)
    {
        ZoneRing ring = GetRingForDistance(distance);
        return ring != null ? ring.orientation : BlockOrientation.Interior;
    }

    /// <summary>
    /// Restituisce il ZoneRing corrispondente alla distanza data.
    /// Cerca il ring con il minimo maxRadius >= distance;
    /// se la distanza supera tutti i ring, restituisce il ring con maxRadius maggiore.
    /// </summary>
    public ZoneRing GetRingForDistance(float distance)
    {
        if (zoneRings == null || zoneRings.Count == 0) return null;

        ZoneRing best = null;
        float bestMax = float.MaxValue;

        foreach (ZoneRing ring in zoneRings)
        {
            if (ring == null) continue;
            if (distance <= ring.maxRadius && ring.maxRadius < bestMax)
            {
                bestMax = ring.maxRadius;
                best = ring;
            }
        }

        if (best != null) return best;

        // Oltre tutti i ring: usa il ring con il raggio massimo
        ZoneRing outermost = null;
        float largestMax = -1f;
        foreach (ZoneRing ring in zoneRings)
        {
            if (ring != null && ring.maxRadius > largestMax)
            {
                largestMax = ring.maxRadius;
                outermost = ring;
            }
        }
        return outermost;
    }

    /// <summary>
    /// Popola zoneRings con i valori di default stile americano (5 fasce).
    /// I ZoneType devono essere collegati manualmente nella UI.
    /// </summary>
    public void ResetToAmericanDefaults()
    {
        initialNumDirections     = 4;
        cbdBranchCount           = 3;
        cbdBranchSweepAngle      = 180f;
        cbdBranchJitter          = 0f;
        suburbBranchCount        = 3;
        suburbBranchSweepAngle   = 120f;
        suburbBranchJitter       = 15f;
        suburbBranchSymmetric    = true;
        localBranchCount         = 2;
        localBranchSweepAngle    = 90f;
        localBranchJitter        = 10f;

        zoneRings = new List<ZoneRing>
        {
            new ZoneRing { label = "CBD (Downtown)",      maxRadius =  2000f, orientation = BlockOrientation.Interior },
            new ZoneRing { label = "Inner City",          maxRadius =  5000f, orientation = BlockOrientation.Interior },
            new ZoneRing { label = "Urban Residential",   maxRadius = 12000f, orientation = BlockOrientation.Exterior },
            new ZoneRing { label = "Suburbs",             maxRadius = 30000f, orientation = BlockOrientation.Sparse   },
            new ZoneRing { label = "Exurbs",              maxRadius = 60000f, orientation = BlockOrientation.Sparse   },
        };
    }

    /// <summary>
    /// Preset valori consigliati per una città di gioco con blocchi americani realistici.
    /// Raggio 2400 m, superblocchi da 1 miglio (1600 m), blocchi locali 100×200 m, vicoli centrali.
    /// Modalità Branching con probabilità calibrate per CBD compatto e suburbs organici.
    /// I ZoneType devono essere collegati manualmente o via Setup Default Zone Types.
    /// </summary>
    public void ResetToGameDefaults()
    {
        generationMode            = RoadGenerationMode.Grid;
        maxGenerationRadius       = 2400f;
        mergeThreshold            = 3f;
        majorGridSpacing          = 1600f;
        localStreetSpacing        = 100f;
        localStreetMaxRadius      = 2400f;
        blockSizeVariation        = 0.05f;
        blockDepthMultiplier      = 2.0f;
        randomSeed                = 42;
        highwayCount              = 2;
        alleyEnabled              = true;
        alleyPositionFraction     = 0.5f;
        alleyMaxRadius            = 2400f;
        maxBranchSegments         = 5000;
        maxBranchGenerations      = 12;
        snapRadius                = 20f;
        initialNumDirections      = 4;
        cbdBranchCount            = 3;
        cbdBranchSweepAngle       = 180f;
        cbdBranchJitter           = 0f;
        cbdStraightProbability    = 0.98f;
        cbdBranchProbability      = 0.90f;
        suburbBranchCount         = 3;
        suburbBranchSweepAngle    = 120f;
        suburbBranchJitter        = 15f;
        suburbBranchSymmetric     = true;
        suburbStraightProbability = 0.72f;
        suburbBranchProbability   = 0.28f;
        localBranchCount          = 2;
        localBranchSweepAngle     = 90f;
        localBranchJitter         = 10f;
        zoneRings = new List<ZoneRing>
        {
            new ZoneRing { label = "CBD",         maxRadius =  400f, orientation = BlockOrientation.Interior },
            new ZoneRing { label = "Inner City",  maxRadius =  800f, orientation = BlockOrientation.Interior },
            new ZoneRing { label = "Residential", maxRadius = 1600f, orientation = BlockOrientation.Exterior },
            new ZoneRing { label = "Suburban",    maxRadius = 2400f, orientation = BlockOrientation.Sparse   },
        };
    }

}
