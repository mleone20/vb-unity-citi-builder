using System;
using System.Collections.Generic;
using UnityEditor;
using BSCCityBuilder.Management;

namespace BSCCityBuilder.Editor.Plugins
{
public interface ICityEditorExtension
{
    int Order { get; }
}

public interface ICityBuilderToolbarExtension : ICityEditorExtension
{
    void DrawToolbar(CityManager manager);
}

public interface ICityBuilderPanelExtension : ICityEditorExtension
{
    string Title { get; }
    void DrawPanel(CityManager manager);
}

public interface ICitySceneViewExtension : ICityEditorExtension
{
    void OnSceneGUI(SceneView sceneView, CityManager manager);
}

[InitializeOnLoad]
public static class CityEditorExtensionRegistry
{
    private static readonly List<ICityBuilderToolbarExtension> ToolbarItems = new List<ICityBuilderToolbarExtension>();
    private static readonly List<ICityBuilderPanelExtension> Panels = new List<ICityBuilderPanelExtension>();
    private static readonly List<ICitySceneViewExtension> SceneExtensions = new List<ICitySceneViewExtension>();

    static CityEditorExtensionRegistry()
    {
        Refresh();
    }

    public static IReadOnlyList<ICityBuilderToolbarExtension> Toolbar => ToolbarItems;
    public static IReadOnlyList<ICityBuilderPanelExtension> PanelExtensions => Panels;
    public static IReadOnlyList<ICitySceneViewExtension> SceneViewExtensions => SceneExtensions;

    public static void Refresh()
    {
        Discover(ToolbarItems);
        Discover(Panels);
        Discover(SceneExtensions);
    }

    private static void Discover<T>(List<T> target) where T : class, ICityEditorExtension
    {
        target.Clear();
        TypeCache.TypeCollection types = TypeCache.GetTypesDerivedFrom<T>();
        for (int i = 0; i < types.Count; i++)
        {
            Type type = types[i];
            if (type.IsAbstract || type.IsInterface || type.GetConstructor(Type.EmptyTypes) == null)
            {
                continue;
            }

            try
            {
                T extension = Activator.CreateInstance(type) as T;
                if (extension != null)
                {
                    target.Add(extension);
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }
        target.Sort((left, right) => left.Order.CompareTo(right.Order));
    }
}
}
