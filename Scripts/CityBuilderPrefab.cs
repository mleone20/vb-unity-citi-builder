
using UnityEngine;

/// <summary>
/// Metadati prefab edificio usati dal tool di spawn per valutare footprint e offset.
/// Aggiungere questo componente sul prefab edificio.
/// </summary>
[DisallowMultipleComponent]
public class CityBuilderPrefab : MonoBehaviour
{
    [Tooltip("Ingombro sul piano XZ (X=larghezza, Y=profondità).")]
    public Vector2 footprintSize = new Vector2(8f, 8f);

    [Tooltip("Se attivo, tenta di calcolare automaticamente l'ingombro dai Renderer del prefab.")]
    public bool autoComputeFromRenderers = false;

    [Tooltip("Offset locale dal centro lotto applicato alla posizione finale.")]
    public Vector3 pivotOffset = Vector3.zero;

    public Vector2 GetFootprintSize()
    {
        if (autoComputeFromRenderers)
        {
            Vector2 autoSize = CalculateRendererFootprint();
            if (autoSize.x > 0f && autoSize.y > 0f)
            {
                return autoSize;
            }
        }

        return new Vector2(Mathf.Max(0.1f, footprintSize.x), Mathf.Max(0.1f, footprintSize.y));
    }

    private Vector2 CalculateRendererFootprint()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return Vector2.zero;
        }

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                combined.Encapsulate(renderers[i].bounds);
            }
        }

        return new Vector2(Mathf.Max(0.1f, combined.size.x), Mathf.Max(0.1f, combined.size.z));
    }
}
