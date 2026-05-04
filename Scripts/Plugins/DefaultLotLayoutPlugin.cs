using System.Collections.Generic;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Config;
using BSCCityBuilder.Generation;

namespace BSCCityBuilder.Plugins
{
[CityPlugin("bsc.default.lot-layout", "Default Lot Layout", CityPluginCategory.LotLayout, "Genera i lotti per ogni blocco usando la logica frontage/sparse esistente.")]
public class DefaultLotLayoutPlugin : ILotLayoutPlugin
{
    public List<CityLot> GenerateLotsForBlock(CityGenerationContext context, CityBlock block, int blockIndex)
    {
        if (context.cityData == null || block == null)
        {
            return new List<CityLot>();
        }

        return CityLotGenerator.GenerateLotsForBlock(block, block.zoning, blockIndex, context.cityData, block.orientation);
    }
}

}
