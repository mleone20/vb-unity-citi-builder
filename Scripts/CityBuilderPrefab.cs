
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Metadati prefab edificio usati dal tool di spawn per valutare footprint e offset.
/// Aggiungere questo componente sul prefab edificio.
/// </summary>
[DisallowMultipleComponent]
public class CityBuilderPrefab : MonoBehaviour
{
    private const float MinFootprint = 0.1f;

    [Tooltip("Ingombro sul piano XZ (X=larghezza, Y=profondità).")]
    public Vector2 footprintSize = new Vector2(8f, 8f);

    [Tooltip("Se attivo, tenta di calcolare automaticamente l'ingombro dai Renderer del prefab.")]
    public bool autoComputeFromRenderers = false;

    [Tooltip("Offset locale dal centro lotto applicato alla posizione finale.")]
    public Vector3 pivotOffset = Vector3.zero;

    public Vector2 GetFootprintSize()
    {
        return new Vector2(Mathf.Max(MinFootprint, footprintSize.x), Mathf.Max(MinFootprint, footprintSize.y));
    }

    private void OnValidate()
    {
        footprintSize = GetFootprintSize();

#if UNITY_EDITOR
        if (!Application.isPlaying && autoComputeFromRenderers)
        {
            AutoComputeFootprintInEditor();
        }
#endif
    }

#if UNITY_EDITOR
    private void AutoComputeFootprintInEditor()
    {
        Vector2 autoSize = CalculateRendererFootprint();
        if (autoSize.x <= 0f || autoSize.y <= 0f)
        {
            return;
        }

        autoSize = new Vector2(Mathf.Max(MinFootprint, autoSize.x), Mathf.Max(MinFootprint, autoSize.y));

        if ((footprintSize - autoSize).sqrMagnitude <= 0.0001f)
        {
            return;
        }

        footprintSize = autoSize;
        EditorUtility.SetDirty(this);
    }
#endif

    private void OnDrawGizmosSelected()
    {
        Vector2 size = GetFootprintSize();
        Vector3 pivotWorld = pivotOffset;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        Gizmos.matrix = Matrix4x4.TRS(pivotWorld, transform.rotation, Vector3.one);
        Gizmos.color = new Color(0.25f, 0.8f, 1f, 1f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(size.x, 0.02f, size.y));
        Gizmos.color = new Color(0.25f, 0.8f, 1f, 0.15f);
        Gizmos.DrawCube(Vector3.zero, new Vector3(size.x, 0.001f, size.y));

        Gizmos.matrix = previousMatrix;

        Gizmos.color = Color.yellow;
        float pivotRadius = Mathf.Max(0.08f, Mathf.Min(size.x, size.y) * 0.03f);
        Gizmos.DrawSphere(pivotWorld, pivotRadius);
        Gizmos.DrawLine(transform.position, pivotWorld);

        Gizmos.color = previousColor;
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
