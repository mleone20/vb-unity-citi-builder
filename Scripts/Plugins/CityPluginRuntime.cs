using System.Collections.Generic;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Config;
using BSCCityBuilder.Generation;

namespace BSCCityBuilder.Plugins
{
[System.Obsolete("Passare ILotSelectionPlugin tramite CityGenerationContext. Lo stato globale è mantenuto solo per compatibilità.")]
public static class CityPluginRuntime
{
    private static ILotSelectionPlugin _activeLotSelectionPlugin;

    public static void SetLotSelectionPlugin(ILotSelectionPlugin plugin)
    {
        _activeLotSelectionPlugin = plugin;
    }

    public static int PickLotCandidate(CityLotSelectionContext context)
    {
        ILotSelectionPlugin plugin = _activeLotSelectionPlugin;
        if (plugin == null)
        {
            plugin = new DefaultLotSelectionPlugin();
        }

        return plugin.PickCandidateIndex(context);
    }
}

}
