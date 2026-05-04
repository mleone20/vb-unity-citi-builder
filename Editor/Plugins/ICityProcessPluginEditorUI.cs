using UnityEngine;
using System;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Config;
using BSCCityBuilder.Generation;
using BSCCityBuilder.Plugins;

namespace BSCCityBuilder.Editor.Plugins
{
/// <summary>
/// Contratto editor per i plugin di processo che vogliono gestire la propria UI.
/// La tab Procedurale del CityBuilderWindow delega al plugin attivo il rendering
/// della configurazione, inclusa la creazione dell'asset config di default.
/// </summary>
public interface ICityProcessPluginEditorUI
{
    string ConfigurationLabel { get; }
    Type ConfigurationType { get; }

    ScriptableObject CreateDefaultConfigurationAsset();
    void DrawConfigurationGUI(ScriptableObject config);
}

}
