using UnityEngine;
using BSCCityBuilder.Core;

namespace BSCCityBuilder.Generation
{
[CreateAssetMenu(fileName = "GridFillLayout", menuName = "City Builder/Layout Operations/Grid Fill")]
public class GridFillBlockLayoutOperation : BlockLayoutOperation
{
    [Min(1)] public int maximumRows = 8;
    [Min(0f)] public float columnGap = 1.5f;
    [Min(0f)] public float rowGap = 4f;

    public override void Execute(BlockLayoutOperationContext context)
    {
        context.lots.AddRange(CityLotGenerator.GenerateGridFillLots(context, this));
    }
}
}
