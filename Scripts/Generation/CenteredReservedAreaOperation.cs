using System.Collections.Generic;
using UnityEngine;
using BSCCityBuilder.Core;

namespace BSCCityBuilder.Generation
{
[CreateAssetMenu(fileName = "CenteredArea", menuName = "City Builder/Layout Operations/Centered Reserved Area")]
public class CenteredReservedAreaOperation : BlockLayoutOperation
{
    public string typeId = "reserved";
    public string label = "Reserved Area";
    [Min(0.1f)] public float width = 18f;
    [Min(0.1f)] public float depth = 18f;

    public override void Execute(BlockLayoutOperationContext context)
    {
        if (context.block.vertices.Count < 3) return;
        GetAxes(context.block.vertices, out Vector3 tangent, out Vector3 forward);
        Vector3 center = context.block.GetCenter();
        Vector3 halfWidth = tangent * (width * 0.5f);
        Vector3 halfDepth = forward * (depth * 0.5f);
        context.reservedAreas.Add(new CityBlockLayoutArea
        {
            typeId = string.IsNullOrWhiteSpace(typeId) ? "reserved" : typeId,
            label = label,
            vertices = new List<Vector3>
            {
                center - halfWidth - halfDepth, center + halfWidth - halfDepth,
                center + halfWidth + halfDepth, center - halfWidth + halfDepth
            }
        });
    }

    private static void GetAxes(List<Vector3> vertices, out Vector3 tangent, out Vector3 forward)
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
}
}
