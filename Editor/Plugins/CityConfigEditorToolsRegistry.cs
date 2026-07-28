using System;
using System.Collections.Generic;
using UnityEditor;
using BSCCityBuilder.Config;
using BSCCityBuilder.Management;

namespace BSCCityBuilder.Editor.Plugins
{
public interface ICityConfigEditorTools
{
    Type ConfigType { get; }
    int Order { get; }
    string Title { get; }
    void DrawTools(CityConfig config, CityManager manager);
}

[InitializeOnLoad]
public static class CityConfigEditorToolsRegistry
{
    private static readonly List<ICityConfigEditorTools> Tools = new List<ICityConfigEditorTools>();

    static CityConfigEditorToolsRegistry()
    {
        Refresh();
    }

    public static void Refresh()
    {
        Tools.Clear();
        TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<ICityConfigEditorTools>();
        for (int i = 0; i < types.Count; i++)
        {
            Type type = types[i];
            if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null)
            {
                continue;
            }

            try
            {
                ICityConfigEditorTools tools = Activator.CreateInstance(type) as ICityConfigEditorTools;
                if (tools != null && tools.ConfigType != null && typeof(CityConfig).IsAssignableFrom(tools.ConfigType))
                {
                    Tools.Add(tools);
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }
        Tools.Sort((left, right) => left.Order.CompareTo(right.Order));
    }

    public static void DrawTools(CityConfig config, CityManager manager)
    {
        if (config == null)
        {
            return;
        }

        Type activeType = config.GetType();
        for (int i = 0; i < Tools.Count; i++)
        {
            ICityConfigEditorTools tools = Tools[i];
            if (!tools.ConfigType.IsAssignableFrom(activeType))
            {
                continue;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(tools.Title, EditorStyles.boldLabel);
            tools.DrawTools(config, manager);
        }
    }
}
}
