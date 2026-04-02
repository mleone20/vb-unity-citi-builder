using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Utility per suddividere blocchi in lotti.
/// Genera lotti distribuiti uniformemente attorno al perimetro del blocco.
/// </summary>
public static class CityLotGenerator
{
    /// <summary>
    /// Genera lotti per un blocco specifico.
    /// I lotti sono distribuiti uniformemente attorno al perimetro.
    /// </summary>
    public static List<CityLot> GenerateLotsForBlock(CityBlock block, float avgLotSize, ZoneType zoning, int blockIndex, CityData cityData)
    {
        List<CityLot> lots = new List<CityLot>();

        if (block.vertices.Count < 3) return lots;

        float perimeter = block.GetPerimeter();
        int lotCount = Mathf.Max(3, Mathf.RoundToInt(perimeter / avgLotSize));

        // Genera posizioni lotti attorno al perimetro
        for (int i = 0; i < lotCount; i++)
        {
            float t = (float)i / lotCount; // Parametro [0..1] attorno al perimetro

            Vector3 lotPosition = GetPointOnPolygonPerimeter(block.vertices, t);
            Vector3 buildingCenter = GetBuildingCenterForLot(block.vertices, t);
            float buildingHeight = cityData.GetZoneHeight(zoning);

            CityLot lot = new CityLot(blockIndex * 1000 + i, block.id)
            {
                buildingCenter = buildingCenter,
                buildingHeight = buildingHeight
            };

            // Approssima vertici lotto (rettangolo piccolo)
            float lotDepth = avgLotSize * 0.6f;
            lot.vertices = GenerateLotVertices(lotPosition, lotDepth);

            lots.Add(lot);
        }

        return lots;
    }

    /// <summary>
    /// Ottiene punto sul perimetro del poligono in base a parametro t [0..1]
    /// </summary>
    private static Vector3 GetPointOnPolygonPerimeter(List<Vector3> polygon, float t)
    {
        float perimeter = CalculatePerimeter(polygon);
        float targetDistance = perimeter * Mathf.Clamp01(t);

        float currentDistance = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector3 v1 = polygon[i];
            Vector3 v2 = polygon[(i + 1) % polygon.Count];
            float edgeLength = Vector3.Distance(v1, v2);

            if (currentDistance + edgeLength >= targetDistance)
            {
                float ratio = (targetDistance - currentDistance) / edgeLength;
                return Vector3.Lerp(v1, v2, ratio);
            }

            currentDistance += edgeLength;
        }

        return polygon[0];
    }

    /// <summary>
    /// Calcola centro edificio (interno al blocco, leggermente arretrato dal perimetro)
    /// </summary>
    private static Vector3 GetBuildingCenterForLot(List<Vector3> polygon, float tOnPerimeter)
    {
        // Punto sul perimetro
        Vector3 perimeterPoint = GetPointOnPolygonPerimeter(polygon, tOnPerimeter);

        // Centro poligono
        Vector3 centerPos = Vector3.zero;
        foreach (var v in polygon)
        {
            centerPos += v;
        }
        centerPos /= polygon.Count;

        // Direzione verso centro
        Vector3 towardCenter = (centerPos - perimeterPoint).normalized;

        // Posiziona edificio arretrato dal perimetro (setback)
        float setback = 2.0f;
        Vector3 buildingPos = perimeterPoint + towardCenter * setback;

        return buildingPos;
    }

    /// <summary>
    /// Genera vertici approssimati per il lotto (rettangolo semplice)
    /// </summary>
    private static List<Vector3> GenerateLotVertices(Vector3 surfacePoint, float depth)
    {
        List<Vector3> vertices = new List<Vector3>();

        // Semplice rettangolo
        float width = depth * 0.5f;
        vertices.Add(surfacePoint + Vector3.right * width);
        vertices.Add(surfacePoint - Vector3.right * width);
        vertices.Add(surfacePoint - Vector3.right * width + Vector3.forward * depth);
        vertices.Add(surfacePoint + Vector3.right * width + Vector3.forward * depth);

        return vertices;
    }

    private static float CalculatePerimeter(List<Vector3> polygon)
    {
        float perimeter = 0f;
        for (int i = 0; i < polygon.Count; i++)
        {
            Vector3 v1 = polygon[i];
            Vector3 v2 = polygon[(i + 1) % polygon.Count];
            perimeter += Vector3.Distance(v1, v2);
        }
        return perimeter;
    }
}
