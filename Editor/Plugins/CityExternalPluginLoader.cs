using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using BSCCityBuilder.Plugins;

namespace BSCCityBuilder.Editor.Plugins
{
[InitializeOnLoad]
public static class CityExternalPluginLoader
{
    public const string SupportedApiVersion = "1.0";
    private static readonly List<CityPluginManifest> Loaded = new List<CityPluginManifest>();

    static CityExternalPluginLoader()
    {
        EditorApplication.delayCall += ScanManifests;
    }

    public static IReadOnlyList<CityPluginManifest> LoadedManifests => Loaded;

    [MenuItem("Tools/City Builder/Refresh External Plugins")]
    public static void ScanManifests()
    {
        Loaded.Clear();
        CityPluginRegistry.Refresh();
        string[] guids = AssetDatabase.FindAssets("t:CityPluginManifest");
        for (int i = 0; i < guids.Length; i++)
        {
            TryLoadManifest(AssetDatabase.LoadAssetAtPath<CityPluginManifest>(
                AssetDatabase.GUIDToAssetPath(guids[i])));
        }
        CityPluginRegistry.Refresh();
    }

    public static bool TryLoadManifest(CityPluginManifest manifest)
    {
        if (manifest == null)
        {
            return false;
        }

        manifest.isLoaded = false;
        manifest.loadMessage = "";

        if (string.IsNullOrWhiteSpace(manifest.pluginDisplayName) ||
            !Version.TryParse(NormalizeVersion(manifest.version), out _))
        {
            return Fail(manifest, "Nome o versione semantica non validi.");
        }

        if (!string.Equals(manifest.apiVersion, SupportedApiVersion, StringComparison.OrdinalIgnoreCase))
        {
            return Fail(manifest, "API richiesta " + manifest.apiVersion +
                                  ", API supportata " + SupportedApiVersion + ".");
        }

        string[] dependencies = manifest.dependencies ?? new string[0];
        for (int i = 0; i < dependencies.Length; i++)
        {
            if (!CityPluginRegistry.ContainsPlugin(dependencies[i]))
            {
                return Fail(manifest, "Dipendenza plugin mancante: " + dependencies[i]);
            }
        }

        if (!string.IsNullOrWhiteSpace(manifest.dllRelativePath))
        {
            string dllAssetPath = ResolveDllAssetPath(manifest);
            if (string.IsNullOrEmpty(dllAssetPath) || AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(dllAssetPath) == null)
            {
                return Fail(manifest, "DLL non trovata nel progetto: " + manifest.dllRelativePath);
            }

            string assemblyName = Path.GetFileNameWithoutExtension(dllAssetPath);
            Assembly assembly = FindLoadedAssembly(assemblyName);
            if (assembly == null)
            {
                return Fail(manifest,
                    "La DLL è presente ma non è caricata da Unity. Verificare Plugin Import Settings e compatibilità piattaforma.");
            }

            if (!ValidateAssemblyCategories(manifest, assembly, out string validationError))
            {
                return Fail(manifest, validationError);
            }
        }

        manifest.isLoaded = true;
        manifest.loadMessage = "Caricato";
        Loaded.Add(manifest);
        EditorUtility.SetDirty(manifest);
        return true;
    }

    private static bool ValidateAssemblyCategories(
        CityPluginManifest manifest,
        Assembly assembly,
        out string error)
    {
        var allowed = new HashSet<CityPluginCategory>(manifest.allowedCategories ?? new CityPluginCategory[0]);
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            types = exception.Types;
        }

        for (int i = 0; i < types.Length; i++)
        {
            Type type = types[i];
            if (type == null)
            {
                continue;
            }
            CityPluginAttribute attribute = type.GetCustomAttribute<CityPluginAttribute>();
            if (attribute != null && !allowed.Contains(attribute.category))
            {
                error = "Il plugin '" + attribute.id + "' usa la categoria non autorizzata " + attribute.category + ".";
                return false;
            }
        }

        error = "";
        return true;
    }

    private static string ResolveDllAssetPath(CityPluginManifest manifest)
    {
        string requested = manifest.dllRelativePath.Replace('\\', '/');
        if (requested.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return requested;
        }

        string manifestPath = AssetDatabase.GetAssetPath(manifest);
        string folder = Path.GetDirectoryName(manifestPath)?.Replace('\\', '/');
        return string.IsNullOrEmpty(folder) ? requested : folder + "/" + requested;
    }

    private static Assembly FindLoadedAssembly(string assemblyName)
    {
        Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
        for (int i = 0; i < assemblies.Length; i++)
        {
            if (string.Equals(assemblies[i].GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
            {
                return assemblies[i];
            }
        }
        return null;
    }

    private static bool Fail(CityPluginManifest manifest, string message)
    {
        manifest.loadMessage = message;
        EditorUtility.SetDirty(manifest);
        Debug.LogWarning("[CityExternalPluginLoader] " + manifest.pluginDisplayName + ": " + message, manifest);
        return false;
    }

    private static string NormalizeVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return "";
        }
        string[] parts = version.Split('.');
        return parts.Length == 2 ? version + ".0" : version;
    }
}
}
