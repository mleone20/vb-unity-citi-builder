using UnityEngine;

namespace BSCCityBuilder.Plugins
{
/// <summary>
/// Manifest per plugin distribuiti come assembly Unity.
/// Il loader valida API, dipendenze, assembly importato e categorie consentite.
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
    [Tooltip("Percorso della DLL, assoluto da Assets/ o relativo al manifest.")]
    public string dllRelativePath = "";

    [Header("Whitelist categorie")]
    [Tooltip("Categorie che l'assembly può registrare.")]
    public CityPluginCategory[] allowedCategories = new CityPluginCategory[0];

    [Header("Stato (sola lettura)")]
    public bool isLoaded;

    [TextArea]
    public string loadMessage = "";
}
}
