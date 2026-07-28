using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using BSCCityBuilder.Core;

namespace BSCCityBuilder.Editor.Tools
{
[InitializeOnLoad]
internal static class LegacyBlockLayoutMigration
{
    private static readonly Regex OrientationPattern =
        new Regex(@"(?m)^\s+orientation:\s*(\d+)\s*$", RegexOptions.Compiled);

    static LegacyBlockLayoutMigration()
    {
        EditorApplication.delayCall += Migrate;
    }

    private static void Migrate()
    {
        BlockLayoutProfile exteriorProfile = FindProfile("Exterior Frontage");
        BlockLayoutProfile sparseProfile = FindProfile("Rural Scatter");
        if (exteriorProfile == null && sparseProfile == null) return;

        string[] guids = AssetDatabase.FindAssets("t:CityData");
        bool changedAny = false;
        for (int assetIndex = 0; assetIndex < guids.Length; assetIndex++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[assetIndex]);
            if (!File.Exists(assetPath)) continue;
            string yaml = File.ReadAllText(assetPath);
            MatchCollection matches = OrientationPattern.Matches(yaml);
            if (matches.Count == 0) continue;

            CityData data = AssetDatabase.LoadAssetAtPath<CityData>(assetPath);
            if (data == null) continue;
            int count = Mathf.Min(matches.Count, data.blocks.Count);
            bool changed = false;
            for (int i = 0; i < count; i++)
            {
                CityBlock block = data.blocks[i];
                if (block == null || block.layoutProfileOverride != null) continue;
                if (!int.TryParse(matches[i].Groups[1].Value, out int legacyValue)) continue;

                // Interior (0) resta senza override e segue il profilo dello ZoneType.
                BlockLayoutProfile migrated = legacyValue == 1
                    ? exteriorProfile
                    : legacyValue == 2 ? sparseProfile : null;
                if (migrated == null) continue;
                block.layoutProfileOverride = migrated;
                changed = true;
            }

            if (changed)
            {
                EditorUtility.SetDirty(data);
                changedAny = true;
            }
        }

        if (changedAny)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[CityBuilder] Migrati i layout legacy Exterior/Sparse verso BlockLayoutProfile.");
        }
    }

    private static BlockLayoutProfile FindProfile(string profileName)
    {
        string[] guids = AssetDatabase.FindAssets(profileName + " t:BlockLayoutProfile");
        for (int i = 0; i < guids.Length; i++)
        {
            BlockLayoutProfile profile = AssetDatabase.LoadAssetAtPath<BlockLayoutProfile>(
                AssetDatabase.GUIDToAssetPath(guids[i]));
            if (profile != null && profile.name == profileName) return profile;
        }
        return null;
    }
}
}
