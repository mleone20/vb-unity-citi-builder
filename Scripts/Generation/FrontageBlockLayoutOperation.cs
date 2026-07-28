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
        List<CityLot> generated = CityLotGenerator.GenerateFrontageLotsForBlock(
            context.block, context.zoneType, context.blockIndex, context.cityData,
            placeOutsideBlock, context.lotSelectionPlugin);
        CityLotGenerator.AppendNonOverlappingLots(context, generated);
    }
}
}
