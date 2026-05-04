using UnityEngine;

/// <summary>
/// Plugin di selezione lotto completamente casuale (nessun determinismo).
/// Utile per test visivi rapidi o per scene in cui la variazione massima è desiderata.
///
/// Per attivarlo: aprire Window/City Builder/Plugin Browser e selezionare
/// "Random Lot Selection" nella categoria LotSelection.
/// </summary>
[CityPlugin(
    "bsc.random.lot-selection",
    "Random Lot Selection",
    CityPluginCategory.LotSelection,
    "Seleziona il prefab del lotto in modo completamente casuale, ignorando qualsiasi seed.")]
public class RandomLotSelectionPlugin : ILotSelectionPlugin
{
    public int PickCandidateIndex(CityLotSelectionContext ctx)
    {
        if (ctx.candidates == null || ctx.candidates.Count == 0)
            return 0;

        // Accumula pesi e sceglie con Random.value (non deterministico)
        float totalWeight = 0f;
        foreach (var c in ctx.candidates)
            totalWeight += Mathf.Max(c.weight, 0f);

        if (totalWeight <= 0f)
            return Random.Range(0, ctx.candidates.Count);

        float roll = Random.value * totalWeight;
        float acc = 0f;
        for (int i = 0; i < ctx.candidates.Count; i++)
        {
            acc += Mathf.Max(ctx.candidates[i].weight, 0f);
            if (roll <= acc)
                return i;
        }
        return ctx.candidates.Count - 1;
    }
}
