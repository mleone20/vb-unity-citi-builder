using System.Collections.Generic;
using UnityEngine;

namespace BSCCityBuilder.Core
{
/// <summary>
/// Raccolta degli asset utilizzabili da una città.
/// Consente a città diverse di esporre set differenti di zone e strade.
/// </summary>
[CreateAssetMenu(fileName = "CityPalette", menuName = "City Builder/City Palette", order = 2)]
public class CityPalette : ScriptableObject
{
    [Header("Zone disponibili")]
    [SerializeField] private List<ZoneType> zoneTypes = new List<ZoneType>();

    [Header("Profili stradali disponibili")]
    [SerializeField] private List<RoadProfile> roadProfiles = new List<RoadProfile>();
    [SerializeField] private RoadProfile defaultRoadProfile;

    public IReadOnlyList<ZoneType> ZoneTypes => zoneTypes;
    public IReadOnlyList<RoadProfile> RoadProfiles => roadProfiles;
    public RoadProfile DefaultRoadProfile => defaultRoadProfile;

    public bool Contains(ZoneType zoneType)
    {
        return zoneType != null && zoneTypes.Contains(zoneType);
    }

    public bool Contains(RoadProfile roadProfile)
    {
        return roadProfile != null && roadProfiles.Contains(roadProfile);
    }

    public void SetDefaultRoadProfile(RoadProfile profile)
    {
        defaultRoadProfile = profile;
        if (profile != null && !roadProfiles.Contains(profile))
        {
            roadProfiles.Add(profile);
        }
    }

#if UNITY_EDITOR
    public void AddZoneType(ZoneType zoneType)
    {
        if (zoneType != null && !zoneTypes.Contains(zoneType))
        {
            zoneTypes.Add(zoneType);
        }
    }

    public void AddRoadProfile(RoadProfile roadProfile)
    {
        if (roadProfile != null && !roadProfiles.Contains(roadProfile))
        {
            roadProfiles.Add(roadProfile);
        }
    }
#endif

    private void OnValidate()
    {
        // Non rimuovere slot null o duplicati qui: quando l'utente aumenta la
        // dimensione di una lista, Unity crea prima un elemento null e richiama
        // subito OnValidate. Ripulirlo renderebbe impossibile compilare la lista.
        if (defaultRoadProfile != null && !roadProfiles.Contains(defaultRoadProfile))
        {
            roadProfiles.Add(defaultRoadProfile);
        }
    }
}
}
