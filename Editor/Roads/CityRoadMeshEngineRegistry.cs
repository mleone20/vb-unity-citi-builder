using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using BSCCityBuilder.Rendering;

namespace BSCCityBuilder.Editor.Roads
{
public interface IRoadMeshGenerationEngineEditorUI
{
    void DrawSettings(BSCCityBuilder.Management.CityManager manager);
}

public readonly struct RoadMeshEngineDescriptor
{
    public readonly string id;
    public readonly string displayName;
    public readonly string description;
    public readonly int order;
    public readonly Type engineType;

    public RoadMeshEngineDescriptor(
        string id,
        string displayName,
        string description,
        int order,
        Type engineType)
    {
        this.id = id;
        this.displayName = displayName;
        this.description = description;
        this.order = order;
        this.engineType = engineType;
    }
}

[InitializeOnLoad]
public static class CityRoadMeshEngineRegistry
{
    private static readonly List<RoadMeshEngineDescriptor> Engines = new List<RoadMeshEngineDescriptor>();

    static CityRoadMeshEngineRegistry()
    {
        Refresh();
    }

    public static IReadOnlyList<RoadMeshEngineDescriptor> GetEngines()
    {
        return Engines.AsReadOnly();
    }

    public static void Refresh()
    {
        Engines.Clear();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        TypeCache.TypeCollection types = TypeCache.GetTypesWithAttribute<RoadMeshEngineAttribute>();
        for (int i = 0; i < types.Count; i++)
        {
            Type type = types[i];
            if (type.IsAbstract || !typeof(IRoadMeshGenerationEngine).IsAssignableFrom(type))
            {
                continue;
            }

            RoadMeshEngineAttribute attribute =
                Attribute.GetCustomAttribute(type, typeof(RoadMeshEngineAttribute)) as RoadMeshEngineAttribute;
            if (attribute == null || string.IsNullOrWhiteSpace(attribute.Id) || !ids.Add(attribute.Id))
            {
                Debug.LogWarning("[CityRoadMeshEngineRegistry] Engine ignorato: ID assente o duplicato su " + type.FullName);
                continue;
            }

            Engines.Add(new RoadMeshEngineDescriptor(
                attribute.Id,
                string.IsNullOrWhiteSpace(attribute.DisplayName) ? type.Name : attribute.DisplayName,
                attribute.Description,
                attribute.Order,
                type));
        }

        Engines.Sort((left, right) =>
        {
            int order = left.order.CompareTo(right.order);
            return order != 0
                ? order
                : string.Compare(left.displayName, right.displayName, StringComparison.OrdinalIgnoreCase);
        });
    }

    public static IRoadMeshGenerationEngine Create(string engineId)
    {
        RoadMeshEngineDescriptor descriptor;
        if (!TryGetDescriptor(engineId, out descriptor) && Engines.Count > 0)
        {
            descriptor = Engines[0];
        }

        if (descriptor.engineType == null)
        {
            return null;
        }

        try
        {
            return Activator.CreateInstance(descriptor.engineType) as IRoadMeshGenerationEngine;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return null;
        }
    }

    public static bool TryGetDescriptor(string engineId, out RoadMeshEngineDescriptor descriptor)
    {
        for (int i = 0; i < Engines.Count; i++)
        {
            if (string.Equals(Engines[i].id, engineId, StringComparison.OrdinalIgnoreCase))
            {
                descriptor = Engines[i];
                return true;
            }
        }

        descriptor = default;
        return false;
    }
}
}
