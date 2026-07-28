using UnityEngine;

namespace BSCCityBuilder.Config
{
/// <summary>Base comune per ogni tipologia di città supportata.</summary>
public abstract class CityConfig : ScriptableObject
{
    [SerializeField, HideInInspector] private string configId;

    public virtual string DisplayName => GetType().Name;
    public virtual float PlanarizationMergeTolerance => 2f;
    public string ConfigId => configId;

    protected virtual void OnValidate()
    {
        if (string.IsNullOrEmpty(configId))
        {
            configId = System.Guid.NewGuid().ToString("N");
        }
    }
}
}
