using UnityEngine;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Config;
using BSCCityBuilder.Generation;

namespace BSCCityBuilder.Plugins
{
[CreateAssetMenu(fileName = "RandomScatterCityConfig", menuName = "CityBuilder/Random Scatter Config", order = 203)]
public class RandomScatterCityConfig : ScriptableObject
{
    [Header("Distribuzione")]
    public Vector3 centerWorldPosition = Vector3.zero;
    [Range(50f, 5000f)] public float radius = 500f;
    [Range(10, 3000)] public int nodeCount = 280;
    [Range(1, 8)] public int nearestConnections = 2;

    [Header("Blocchi")]
    [Range(10, 600)] public int blockCount = 80;
    [Range(10f, 300f)] public float minBlockSize = 35f;
    [Range(10f, 300f)] public float maxBlockSize = 90f;

    [Header("Random")]
    public int randomSeed = 1337;

    [Header("Zoning")]
    [Tooltip("Se vuoto, i blocchi resteranno senza zoning assegnato.")]
    public ZoneType[] zoneTypes = new ZoneType[0];
}

}
