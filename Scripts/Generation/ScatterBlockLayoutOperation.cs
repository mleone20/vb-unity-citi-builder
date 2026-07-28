using UnityEngine;
using BSCCityBuilder.Core;

namespace BSCCityBuilder.Generation
{
[CreateAssetMenu(fileName = "ScatterLayout", menuName = "City Builder/Layout Operations/Scatter")]
public class ScatterBlockLayoutOperation : BlockLayoutOperation
{
    public override void Execute(BlockLayoutOperationContext context)
    {
        context.lots.AddRange(CityLotGenerator.GenerateScatterLots(context));
    }
}
}
