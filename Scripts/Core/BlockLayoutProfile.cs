using System.Collections.Generic;
using UnityEngine;
using BSCCityBuilder.Plugins;

namespace BSCCityBuilder.Core
{
[CreateAssetMenu(fileName = "BlockLayoutProfile", menuName = "City Builder/Block Layout Profile")]
public class BlockLayoutProfile : ScriptableObject
{
    [TextArea] public string description;
    [Tooltip("Le operazioni vengono eseguite nell'ordine della lista.")]
    public List<BlockLayoutOperation> operations = new List<BlockLayoutOperation>();
}

/// <summary>
/// Base estendibile per un passo di layout. Asset e plugin esterni possono
/// aggiungere nuove operazioni senza modificare CityBlock o ZoneType.
/// </summary>
public abstract class BlockLayoutOperation : ScriptableObject
{
    [SerializeField] private bool operationEnabled = true;
    [Min(0f)] public float minimumBlockArea;
    [Tooltip("0 significa nessun limite massimo.")]
    [Min(0f)] public float maximumBlockArea;
    public bool OperationEnabled => operationEnabled;

    public bool CanExecute(BlockLayoutOperationContext context)
    {
        if (!operationEnabled || context == null || context.block == null) return false;
        float area = context.block.GetArea();
        return area >= minimumBlockArea &&
               (maximumBlockArea <= 0f || area <= maximumBlockArea);
    }

    public abstract void Execute(BlockLayoutOperationContext context);
}

public sealed class BlockLayoutOperationContext
{
    public CityData cityData;
    public CityBlock block;
    public ZoneType zoneType;
    public int blockIndex;
    public ILotSelectionPlugin lotSelectionPlugin;
    public readonly List<CityLot> lots = new List<CityLot>();
    public readonly List<CityBlockLayoutArea> reservedAreas = new List<CityBlockLayoutArea>();
}

[System.Serializable]
public class CityBlockLayoutArea
{
    [Tooltip("Identificatore libero interpretabile da renderer o plugin esterni.")]
    public string typeId = "reserved";
    public string label;
    public List<Vector3> vertices = new List<Vector3>();
}
}
