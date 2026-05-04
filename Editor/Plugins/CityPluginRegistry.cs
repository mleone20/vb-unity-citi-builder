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
}

public static class CityPluginRegistry
{
    private static readonly Dictionary<CityPluginCategory, List<CityPluginDescriptor>> _byCategory = new Dictionary<CityPluginCategory, List<CityPluginDescriptor>>();
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
                pluginType = type
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

        _initialized = true;
    }

    public static List<CityPluginDescriptor> GetPlugins(CityPluginCategory category)
    {
        EnsureInitialized();
        return _byCategory[category];
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

    public static T Create<T>(CityPluginCategory category, string pluginId) where T : class
    {
        EnsureInitialized();

        CityPluginDescriptor? desc = GetDescriptor(category, pluginId);
        if (desc.HasValue)
        {
            object instance = Activator.CreateInstance(desc.Value.pluginType);
            return instance as T;
        }

        List<CityPluginDescriptor> list = _byCategory[category];
        if (list.Count > 0)
        {
            object fallback = Activator.CreateInstance(list[0].pluginType);
            return fallback as T;
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
        switch (category)
        {
            case CityPluginCategory.Process: return typeof(ICityProcessPlugin).IsAssignableFrom(type);
            case CityPluginCategory.RoadNetwork: return typeof(IRoadNetworkGenerationPlugin).IsAssignableFrom(type);
            case CityPluginCategory.RoadPlanarization: return typeof(IRoadPlanarizationPlugin).IsAssignableFrom(type);
            case CityPluginCategory.BlockDetection: return typeof(IBlockDetectionPlugin).IsAssignableFrom(type);
            case CityPluginCategory.Zoning: return typeof(IZoningAssignmentPlugin).IsAssignableFrom(type);
            case CityPluginCategory.LotLayout: return typeof(ILotLayoutPlugin).IsAssignableFrom(type);
            case CityPluginCategory.LotSelection: return typeof(ILotSelectionPlugin).IsAssignableFrom(type);
            default: return false;
        }
    }
}

}
