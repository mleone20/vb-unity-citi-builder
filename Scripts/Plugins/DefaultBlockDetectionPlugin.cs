using UnityEngine;
using System.Collections.Generic;

[CityPlugin("bsc.default.block-detection", "Default Block Detection", CityPluginCategory.BlockDetection, "Rileva blocchi dal grafo stradale con algoritmo a facce cicliche.")]
public class DefaultBlockDetectionPlugin : IBlockDetectionPlugin
{
    public List<List<Vector3>> DetectBlocks(CityGenerationContext context)
    {
        if (context.cityData == null)
        {
            return new List<List<Vector3>>();
        }

        return CityBlockDetector.DetectBlocks(context.cityData);
    }
}
