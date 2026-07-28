using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;
using BSCCityBuilder.Core;

namespace BSCCityBuilder.Config
{
/// <summary>
/// Rappresenta una fascia percentuale dal centro al bordo della città.
/// Gli anelli devono essere ordinati per maxRadiusPercent crescente.
/// </summary>
[System.Serializable]
public class ZoneRing
{
    [Tooltip("Nome descrittivo (es. CBD, Inner City, Suburbs).")]
    public string label = "Zone Ring";

    [Tooltip("Limite superiore della fascia, in percentuale del raggio totale della città.")]
    [FormerlySerializedAs("maxRadius")]
    [Range(0f, 100f)]
    public float maxRadiusPercent = 100f;

    [Tooltip("ZoneType da assegnare ai blocchi in questa fascia.")]
    public ZoneType zoneType;

}

/// <summary>
/// Configurazione per la generazione procedurale di una città in stile americano.
/// Usa una lista libera di ZoneRing per definire le fasce zonali per distanza:
/// aggiungi, rimuovi o modifica i ring senza limiti predefiniti.
/// </summary>
[CreateAssetMenu(fileName = "AmericanCityConfig", menuName = "City Builder/American City Config")]
public class AmericanCityConfig : CityConfig
{
    public override string DisplayName => "American City";
    public override float PlanarizationMergeTolerance => Mathf.Max(0.1f, mergeThreshold);
    [Header("Centro Città (P0)")]
    [Tooltip("Punto di massima densità (CBD). Tutte le distanze zonali sono misurate da qui.")]
    public Vector3 centerWorldPosition = Vector3.zero;

    [Header("Cap Generazione")]
    [Tooltip("Raggio massimo di generazione in unità world (1 u = 1 m). Default: 1500 m (diametro città: 3 km).")]
    [Min(1f)]
    public float maxGenerationRadius = 1500f;

    [Header("Griglia Stradale")]
    [Tooltip("Spaziatura griglia principale (Major Grid) in metri.")]
    [Min(50f)]
    public float majorGridSpacing = 500f;

    [Tooltip("Spaziatura strade locali all'interno di ogni cella della griglia principale, in metri.")]
    [Min(20f)]
    public float localStreetSpacing = 100f;

    [Tooltip("Raggio massimo entro cui vengono generate strade locali (sub-griglia). 0 = disabilita.")]
    [Min(0f)]
    public float localStreetMaxRadius = 1500f;

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

    [Tooltip("Raggio di snapping: due endpoint entro questa distanza vengono uniti in un nodo condiviso.")]
    [Min(0.5f)]
    public float snapRadius = 8f;


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
    public float alleyMaxRadius = 1500f;

    [Header("Zone Rings (fascia distanza → zona)")]
    [Tooltip("Fasce zonali ordinate per percentuale crescente (0–100% del raggio città). L'ultimo ring cattura le distanze superiori.")]
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
    /// Restituisce il ZoneRing corrispondente alla distanza data.
    /// Converte la distanza in percentuale del raggio della città e cerca la
    /// soglia minima che la contiene.
    /// </summary>
    public ZoneRing GetRingForDistance(float distance)
    {
        if (zoneRings == null || zoneRings.Count == 0) return null;

        float distancePercent = Mathf.Max(0f, distance) / Mathf.Max(1f, maxGenerationRadius) * 100f;
        ZoneRing best = null;
        float bestMax = float.MaxValue;

        foreach (ZoneRing ring in zoneRings)
        {
            if (ring == null) continue;
            if (distancePercent <= ring.maxRadiusPercent && ring.maxRadiusPercent < bestMax)
            {
                bestMax = ring.maxRadiusPercent;
                best = ring;
            }
        }

        if (best != null) return best;

        // Oltre tutti i ring: usa il ring con il raggio massimo
        ZoneRing outermost = null;
        float largestMax = -1f;
        foreach (ZoneRing ring in zoneRings)
        {
            if (ring != null && ring.maxRadiusPercent > largestMax)
            {
                largestMax = ring.maxRadiusPercent;
                outermost = ring;
            }
        }
        return outermost;
    }

    /// <summary>
    /// Ripristina un preset americano compatto (diametro 3 km) con 5 fasce.
    /// I ZoneType devono essere collegati manualmente nella UI.
    /// </summary>
    public void ResetToAmericanDefaults()
    {
        maxGenerationRadius       = 1500f;
        mergeThreshold            = 3f;
        majorGridSpacing          = 500f;
        localStreetSpacing        = 100f;
        localStreetMaxRadius      = 1500f;
        blockSizeVariation        = 0.08f;
        randomSeed                = 42;
        highwayCount              = 2;
        alleyEnabled              = true;
        alleyPositionFraction     = 0.5f;
        alleyMaxRadius            = 1500f;
        snapRadius                = 12f;
        zoneRings = new List<ZoneRing>
        {
            new ZoneRing { label = "CBD (Downtown)",    maxRadiusPercent = 15f },
            new ZoneRing { label = "Inner City",        maxRadiusPercent = 35f },
            new ZoneRing { label = "Urban Residential", maxRadiusPercent = 60f },
            new ZoneRing { label = "Suburbs",           maxRadiusPercent = 82f },
            new ZoneRing { label = "Exurbs",            maxRadiusPercent = 100f },
        };
    }

    /// <summary>
    /// Preset valori consigliati per una città di gioco con blocchi americani realistici.
    /// Raggio 2400 m, superblocchi da 1 miglio (1600 m), blocchi locali 100×200 m, vicoli centrali.  
    /// I ZoneType devono essere collegati manualmente o via Setup Default Zone Types.
    /// </summary>
    public void ResetToGameDefaults()
    {
        maxGenerationRadius       = 2400f;
        mergeThreshold            = 3f;
        majorGridSpacing          = 1600f;
        localStreetSpacing        = 100f;
        localStreetMaxRadius      = 2400f;
        blockSizeVariation        = 0.05f;
        randomSeed                = 42;
        highwayCount              = 2;
        alleyEnabled              = true;
        alleyPositionFraction     = 0.5f;
        alleyMaxRadius            = 2400f; 
        snapRadius                = 20f; 
        zoneRings = new List<ZoneRing>
        {
            new ZoneRing { label = "CBD",         maxRadiusPercent = 16.67f },
            new ZoneRing { label = "Inner City",  maxRadiusPercent = 33.33f },
            new ZoneRing { label = "Residential", maxRadiusPercent = 66.67f },
            new ZoneRing { label = "Suburban",    maxRadiusPercent = 100f },
        };
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        maxGenerationRadius = Mathf.Max(1f, maxGenerationRadius);

        if (zoneRings == null) return;
        foreach (ZoneRing ring in zoneRings)
        {
            if (ring == null) continue;

            // I vecchi asset serializzavano la soglia in metri. Valori > 100
            // sono quindi convertiti una sola volta nella nuova scala percentuale.
            if (ring.maxRadiusPercent > 100f)
            {
                ring.maxRadiusPercent = ring.maxRadiusPercent / maxGenerationRadius * 100f;
            }
            ring.maxRadiusPercent = Mathf.Clamp(ring.maxRadiusPercent, 0f, 100f);
        }
    }

}

}
