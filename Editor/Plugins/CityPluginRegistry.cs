using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Config;
using BSCCityBuilder.Generation;
using BSCCityBuilder.Plugins;

namespace BSCCityBuilder.Editor.Plugins
{
public struct CityPluginDescriptor
{
    public string id;
    public string displayName;
    public string description;
    public CityPluginCategory category;
    public Type pluginType;
    public string version;
    public int order;
    public string[] dependencies;
    public bool isValid;
    public string validationMessage;
}

public static class CityPluginRegistry
{
    private static readonly Dictionary<CityPluginCategory, List<CityPluginDescriptor>> _byCategory = new Dictionary<CityPluginCategory, List<CityPluginDescriptor>>();
    private static readonly Dictionary<CityPluginCategory, Type> _contracts = new Dictionary<CityPluginCategory, Type>
    {
        { CityPluginCategory.Process, typeof(ICityProcessPlugin) },
        { CityPluginCategory.RoadNetwork, typeof(IRoadNetworkGenerationPlugin) },
        { CityPluginCategory.RoadPlanarization, typeof(IRoadPlanarizationPlugin) },
        { CityPluginCategory.BlockDetection, typeof(IBlockDetectionPlugin) },
        { CityPluginCategory.Zoning, typeof(IZoningAssignmentPlugin) },
        { CityPluginCategory.LotLayout, typeof(ILotLayoutPlugin) },
        { CityPluginCategory.LotSelection, typeof(ILotSelectionPlugin) }
    };
    private static bool _initialized;

    [InitializeOnLoadMethod]
    private static void InitializeOnLoad()
    {
        Refresh();
    }

    public static void Refresh()
    {
        _byCategory.Clear();

        foreach (CityPluginCategory category in Enum.GetValues(typeof(CityPluginCategory)))
        {
            _byCategory[category] = new List<CityPluginDescriptor>();
        }

        var attributedTypes = TypeCache.GetTypesWithAttribute<CityPluginAttribute>();
        for (int i = 0; i < attributedTypes.Count; i++)
        {
            Type type = attributedTypes[i];
            if (type == null || type.IsAbstract)
            {
                continue;
            }

            object[] attrs = type.GetCustomAttributes(typeof(CityPluginAttribute), false);
            if (attrs == null || attrs.Length == 0)
            {
                continue;
            }

            CityPluginAttribute attr = attrs[0] as CityPluginAttribute;
            if (attr == null || string.IsNullOrWhiteSpace(attr.id))
            {
                continue;
            }

            if (!TypeMatchesCategory(type, attr.category))
            {
                continue;
            }

            CityPluginDescriptor descriptor = new CityPluginDescriptor
            {
                id = attr.id,
                displayName = string.IsNullOrWhiteSpace(attr.displayName) ? type.Name : attr.displayName,
                description = attr.description,
                category = attr.category,
                pluginType = type,
                version = attr.Version,
                order = attr.Order,
                dependencies = GetDependencies(type),
                isValid = true,
                validationMessage = string.Empty
            };

            List<CityPluginDescriptor> list = _byCategory[attr.category];
            bool duplicate = false;
            for (int d = 0; d < list.Count; d++)
            {
                if (string.Equals(list[d].id, descriptor.id, StringComparison.OrdinalIgnoreCase))
                {
                    duplicate = true;
                    Debug.LogWarning("[CityPluginRegistry] Plugin ID duplicato ignorato: " + descriptor.id + " (" + type.FullName + ")");
                    break;
                }
            }

            if (!duplicate)
            {
                list.Add(descriptor);
            }
        }

        ValidateDependencies();
        foreach (List<CityPluginDescriptor> plugins in _byCategory.Values)
        {
            plugins.Sort(CompareDescriptors);
        }

        _initialized = true;
    }

    public static IReadOnlyList<CityPluginDescriptor> GetPlugins(CityPluginCategory category)
    {
        EnsureInitialized();
        return _byCategory[category].AsReadOnly();
    }

    public static CityPluginDescriptor? GetDescriptor(CityPluginCategory category, string pluginId)
    {
        EnsureInitialized();
        List<CityPluginDescriptor> list = _byCategory[category];
        for (int i = 0; i < list.Count; i++)
        {
            if (string.Equals(list[i].id, pluginId, StringComparison.OrdinalIgnoreCase))
            {
                return list[i];
            }
        }

        return null;
    }

    public static bool ContainsPlugin(string pluginId)
    {
        EnsureInitialized();
        if (string.IsNullOrWhiteSpace(pluginId))
        {
            return false;
        }
        foreach (List<CityPluginDescriptor> plugins in _byCategory.Values)
        {
            for (int i = 0; i < plugins.Count; i++)
            {
                if (string.Equals(plugins[i].id, pluginId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }

    public static T Create<T>(CityPluginCategory category, string pluginId) where T : class
    {
        EnsureInitialized();

        CityPluginDescriptor? desc = GetDescriptor(category, pluginId);
        if (desc.HasValue && desc.Value.isValid)
        {
            return CreateInstance<T>(desc.Value);
        }

        List<CityPluginDescriptor> list = _byCategory[category];
        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].isValid)
            {
                return CreateInstance<T>(list[i]);
            }
        }

        return null;
    }

    private static void EnsureInitialized()
    {
        if (!_initialized)
        {
            Refresh();
        }
    }

    private static bool TypeMatchesCategory(Type type, CityPluginCategory category)
    {
        Type contract;
        return _contracts.TryGetValue(category, out contract) && contract.IsAssignableFrom(type);
    }

    private static T CreateInstance<T>(CityPluginDescriptor descriptor) where T : class
    {
        try
        {
            return Activator.CreateInstance(descriptor.pluginType) as T;
        }
        catch (Exception exception)
        {
            Debug.LogException(new InvalidOperationException(
                "Impossibile creare il plugin '" + descriptor.id + "'. Verificare il costruttore pubblico senza parametri.",
                exception));
            return null;
        }
    }

    private static string[] GetDependencies(Type type)
    {
        object[] attributes = type.GetCustomAttributes(typeof(CityPluginDependencyAttribute), false);
        var result = new List<string>();
        for (int i = 0; i < attributes.Length; i++)
        {
            CityPluginDependencyAttribute dependency = attributes[i] as CityPluginDependencyAttribute;
            if (dependency != null && !dependency.Optional && !string.IsNullOrWhiteSpace(dependency.PluginId))
            {
                result.Add(dependency.PluginId);
            }
        }
        return result.ToArray();
    }

    private static void ValidateDependencies()
    {
        var knownIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (List<CityPluginDescriptor> plugins in _byCategory.Values)
        {
            for (int i = 0; i < plugins.Count; i++)
            {
                knownIds.Add(plugins[i].id);
            }
        }

        foreach (List<CityPluginDescriptor> plugins in _byCategory.Values)
        {
            for (int i = 0; i < plugins.Count; i++)
            {
                CityPluginDescriptor descriptor = plugins[i];
                for (int d = 0; d < descriptor.dependencies.Length; d++)
                {
                    if (!knownIds.Contains(descriptor.dependencies[d]))
                    {
                        descriptor.isValid = false;
                        descriptor.validationMessage = "Dipendenza mancante: " + descriptor.dependencies[d];
                        break;
                    }
                }
                plugins[i] = descriptor;
            }
        }
    }

    private static int CompareDescriptors(CityPluginDescriptor left, CityPluginDescriptor right)
    {
        int order = left.order.CompareTo(right.order);
        return order != 0
            ? order
            : string.Compare(left.displayName, right.displayName, StringComparison.OrdinalIgnoreCase);
    }
}

}
