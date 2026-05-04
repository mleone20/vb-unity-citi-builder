using System.Collections.Generic;

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
