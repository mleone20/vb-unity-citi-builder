using UnityEngine;
using System.Collections.Generic;

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

    [Tooltip("Numero di autostrade radiali complete (ogni autostrada genera 2 bracci opposti dal centro).")]
    [Range(1, 4)]
    public int highwayCount = 2;

    [Tooltip("Distanza soglia (m) entro cui due nodi vengono uniti per evitare duplicati.")]
    [Min(0.1f)]
    public float mergeThreshold = 2f;

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
        zoneRings = new List<ZoneRing>
        {
            new ZoneRing { label = "CBD (Downtown)",      maxRadius =  2000f, orientation = BlockOrientation.Interior },
            new ZoneRing { label = "Inner City",          maxRadius =  5000f, orientation = BlockOrientation.Interior },
            new ZoneRing { label = "Urban Residential",   maxRadius = 12000f, orientation = BlockOrientation.Exterior },
            new ZoneRing { label = "Suburbs",             maxRadius = 30000f, orientation = BlockOrientation.Sparse   },
            new ZoneRing { label = "Exurbs",              maxRadius = 60000f, orientation = BlockOrientation.Sparse   },
        };
    }
}
