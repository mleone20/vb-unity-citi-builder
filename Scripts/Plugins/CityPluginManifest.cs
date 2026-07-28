using UnityEngine;
using BSCCityBuilder.Core;
using BSCCityBuilder.Management;
using BSCCityBuilder.Config;
using BSCCityBuilder.Generation;

namespace BSCCityBuilder.Plugins
{
/// <summary>
/// ScriptableObject manifest per plugin esterni caricati da DLL esterne.
///
/// FASE 2 - Placeholder strutturale.
/// In questa fase il manifest viene riconosciuto dall'editor ma il loader
/// non effettua nessun caricamento reale di assembly.
///
/// Utilizzo futuro (Fase 2):
///   1. Creare un asset CityPluginManifest via Assets > Create > CityBuilder > Plugin Manifest
///   2. Compilare il plugin come .dll e metterlo nel percorso indicato in dllPath
///   3. CityExternalPluginLoader caricherà l'assembly e registrerà i plugin nel registry
/// </summary>
[CreateAssetMenu(
    menuName = "CityBuilder/Plugin Manifest",
    fileName = "CityPluginManifest",
    order = 202)]
public class CityPluginManifest : ScriptableObject
{
    [Header("Informazioni plugin")]
    [Tooltip("Nome leggibile del plugin esterno.")]
    public string pluginDisplayName = "My Custom Plugin";

    [Tooltip("Autore/organizzazione del plugin.")]
    public string author = "";

    [Tooltip("Versione semantica (es. 1.0.0).")]
    public string version = "1.0.0";

    [Tooltip("Versione del contratto City Builder richiesta dal plugin.")]
    public string apiVersion = "1.0";

    [Tooltip("ID di altri plugin richiesti.")]
    public string[] dependencies = new string[0];

    [Header("Assembly esterno")]
    [Tooltip("Percorso asset della DLL, assoluto da Assets/ o relativo alla cartella del manifest. Lasciare vuoto per plugin interni.")]
    public string dllRelativePath = "";

    [Header("Whitelist categorie")]
    [Tooltip("Categorie per cui questo manifest può registrare plugin. Limita la superficie di attacco.")]
    public CityPluginCategory[] allowedCategories = new CityPluginCategory[0];

    [Header("Stato (sola lettura)")]
    [Tooltip("Impostato a runtime da CityExternalPluginLoader. Non modificare manualmente.")]
    public bool isLoaded = false;

    [TextArea]
    public string loadMessage = "";
}

}
