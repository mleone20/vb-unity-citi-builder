using System.Collections.Generic;
using UnityEngine;
using BSCCityBuilder.Core;

namespace BSCCityBuilder.Generation
{
[CreateAssetMenu(fileName = "FrontageLayout", menuName = "City Builder/Layout Operations/Frontage")]
public class FrontageBlockLayoutOperation : BlockLayoutOperation
{
    public bool placeOutsideBlock;

    public override void Execute(BlockLayoutOperationContext context)
    {
        List<CityLot> generated = CityLotGenerator.GenerateLotsForBlock(
            context.block, context.zoneType, context.blockIndex, context.cityData,
            placeOutsideBlock ? BlockOrientation.Exterior : BlockOrientation.Interior,
            context.lotSelectionPlugin, null, true);
        CityLotGenerator.AppendNonOverlappingLots(context, generated);
    }
}
}
