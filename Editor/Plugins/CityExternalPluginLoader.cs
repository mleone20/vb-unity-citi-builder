using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Config;
using BSCCityBuilder.Generation;
using BSCCityBuilder.Plugins;

namespace BSCCityBuilder.Editor.Plugins
{
/// <summary>
/// Loader di plugin esterni da assembly .dll — FASE 2 PLACEHOLDER.
///
/// In Fase 1 questo loader non carica nessun assembly reale.
/// Fornisce la struttura e l'API che verrà usata in Fase 2 quando si vorrà
/// supportare plugin distribuiti come DLL esterne.
///
/// Per abilitare in Fase 2:
///   1. Rimuovere il commento dal blocco #if CITY_PLUGIN_PHASE2
///   2. Aggiungere CITY_PLUGIN_PHASE2 ai Scripting Define Symbols del progetto
///   3. Implementare la validazione firma assembly se richiesta
/// </summary>
[InitializeOnLoad]
public static class CityExternalPluginLoader
{
    // Manifests caricati nella sessione corrente
    private static readonly List<CityPluginManifest> _loadedManifests = new List<CityPluginManifest>();

    static CityExternalPluginLoader()
    {
        // Scan automatico manifest al caricamento del dominio
        ScanManifests();
    }

    /// <summary>
    /// Scansiona tutti i CityPluginManifest nel progetto e tenta di caricarli.
    /// Chiamato automaticamente a ogni ricompilazione, o manualmente via menu.
    /// </summary>
    public static void ScanManifests()
    {
        _loadedManifests.Clear();

        string[] guids = AssetDatabase.FindAssets("t:CityPluginManifest");
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var manifest = AssetDatabase.LoadAssetAtPath<CityPluginManifest>(path);
            if (manifest != null)
                TryLoadManifest(manifest);
        }

        if (_loadedManifests.Count > 0)
            Debug.Log($"[CityExternalPluginLoader] {_loadedManifests.Count} manifest trovati. " +
                      "Caricamento DLL esterno disabilitato (Fase 1).");
    }

    /// <summary>
    /// Tenta di caricare un manifest. In Fase 1 segna solo il manifest come rilevato.
    /// In Fase 2 caricherà l'assembly dalla <see cref="CityPluginManifest.dllRelativePath"/>.
    /// </summary>
    public static void TryLoadManifest(CityPluginManifest manifest)
    {
        if (manifest == null) return;

        // Fase 1: validazione strutturale solo
        if (string.IsNullOrEmpty(manifest.dllRelativePath))
        {
            // Manifest interno (nessuna DLL) — solo registrazione metadati
            manifest.isLoaded = true;
            _loadedManifests.Add(manifest);
            return;
        }

        // ── FASE 2 (non attiva) ──────────────────────────────────────────────
        // #if CITY_PLUGIN_PHASE2
        //   string fullPath = System.IO.Path.Combine(Application.dataPath, "Plugins", manifest.dllRelativePath);
        //   if (!System.IO.File.Exists(fullPath))
        //   {
        //       Debug.LogWarning($"[CityExternalPluginLoader] DLL non trovata: {fullPath}");
        //       return;
        //   }
        //
        //   // Validazione whitelist categorie
        //   var allowedSet = new System.Collections.Generic.HashSet<CityPluginCategory>(manifest.allowedCategories);
        //
        //   // Caricamento assembly e reflection per trovare tipi [CityPlugin]
        //   var asm = System.Reflection.Assembly.LoadFrom(fullPath);
        //   // CityPluginRegistry.RegisterExternalAssembly(asm, allowedSet);
        //
        //   manifest.isLoaded = true;
        //   _loadedManifests.Add(manifest);
        //   Debug.Log($"[CityExternalPluginLoader] Caricato: {manifest.pluginDisplayName} v{manifest.version}");
        // #endif
        // ────────────────────────────────────────────────────────────────────

        Debug.LogWarning($"[CityExternalPluginLoader] Manifest '{manifest.pluginDisplayName}' ha una dllRelativePath " +
                         "ma il caricamento DLL esterno non è ancora abilitato (Fase 1). " +
                         "Definire CITY_PLUGIN_PHASE2 nei Scripting Define Symbols per abilitarlo.");
    }

    /// <summary>Restituisce tutti i manifest caricati in questa sessione.</summary>
    public static IReadOnlyList<CityPluginManifest> LoadedManifests => _loadedManifests;
}

}
