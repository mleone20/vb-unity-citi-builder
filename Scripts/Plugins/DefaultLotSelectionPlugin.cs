using UnityEngine;
using System.Collections.Generic;

[CityPlugin("bsc.default.lot-selection", "Default Lot Selection", CityPluginCategory.LotSelection, "Selezione prefab con peso e tie-break deterministico opzionale.")]
public class DefaultLotSelectionPlugin : ILotSelectionPlugin
{
    public int PickCandidateIndex(CityLotSelectionContext context)
    {
        List<CityLotCandidate> candidates = context.candidates;
        int count = candidates != null ? candidates.Count : 0;
        if (count <= 0)
        {
            return -1;
        }

        if (count == 1)
        {
            return 0;
        }

        float totalWeight = 0f;
        for (int i = 0; i < count; i++)
        {
            totalWeight += Mathf.Max(0f, candidates[i].weight);
        }

        if (totalWeight <= 0f)
        {
            return 0;
        }

        ZoneType zone = context.zoneType;
        bool deterministic = zone != null && zone.deterministicPrefabSelection;
        int seed = zone != null ? zone.prefabSelectionSeed : 0;

        float randomValue;
        if (deterministic)
        {
            randomValue = GetDeterministic01(seed, context.blockIndex, context.edgeIndex, context.lotIndex, 0) * totalWeight;
        }
        else
        {
            randomValue = Random.value * totalWeight;
        }

        int selectedIndex = count - 1;
        float cumulative = 0f;
        for (int i = 0; i < count; i++)
        {
            cumulative += Mathf.Max(0f, candidates[i].weight);
            if (randomValue <= cumulative)
            {
                selectedIndex = i;
                break;
            }
        }

        float selectedWeight = Mathf.Max(0f, candidates[selectedIndex].weight);
        List<int> sameWeight = null;

        for (int i = 0; i < count; i++)
        {
            if (Mathf.Approximately(Mathf.Max(0f, candidates[i].weight), selectedWeight))
            {
                if (sameWeight == null)
                {
                    sameWeight = new List<int>();
                }

                sameWeight.Add(i);
            }
        }

        if (sameWeight == null || sameWeight.Count <= 1)
        {
            return selectedIndex;
        }

        int tieIndex;
        if (deterministic)
        {
            float tieRandom = GetDeterministic01(seed, context.blockIndex, context.edgeIndex, context.lotIndex, 1);
            tieIndex = Mathf.FloorToInt(tieRandom * sameWeight.Count);
        }
        else
        {
            tieIndex = Random.Range(0, sameWeight.Count);
        }

        tieIndex = Mathf.Clamp(tieIndex, 0, sameWeight.Count - 1);
        return sameWeight[tieIndex];
    }

    private static float GetDeterministic01(int seed, int blockIdx, int edgeIdx, int lotIdx, int salt)
    {
        unchecked
        {
            uint hash = 2166136261u;

            hash ^= (uint)seed;
            hash *= 16777619u;

            hash ^= (uint)blockIdx;
            hash *= 16777619u;

            hash ^= (uint)edgeIdx;
            hash *= 16777619u;

            hash ^= (uint)lotIdx;
            hash *= 16777619u;

            hash ^= (uint)salt;
            hash *= 16777619u;

            return (hash & 0x00FFFFFFu) / 16777216f;
        }
    }
}
