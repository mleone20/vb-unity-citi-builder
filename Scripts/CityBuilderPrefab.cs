
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
    public bool autoComputeFromRenderers = true;

    [Tooltip("Offset locale dal centro lotto applicato alla posizione finale.")]
    public Vector3 pivotOffset = Vector3.zero;

    [Tooltip("Posizione del piano di affaccio (fronte edificio) in spazio locale. Indica la direzione frontale verso la strada.")]
    public Vector3 frontageOffset = new Vector3(0f, 0f, -4f);

    [Tooltip("Direzione locale della normale del piano Frontage. Permette di ruotare l'affaccio senza vincolarlo all'asse Z.")]
    public Vector3 frontageDirection = Vector3.back;

    [Tooltip("Altezza di visualizzazione del piano Frontage nel gizmo (non influenza la logica).")]
    public float frontageDisplayHeight = 4f;

    // Indica se frontageOffset è stato inizializzato almeno una volta (evita di sovrascrivere valori personalizzati).
    [SerializeField] private bool frontageOffsetInitialized = false;
    [SerializeField] private bool frontageDirectionInitialized = false;

    public Vector2 GetFootprintSize()
    {
        return new Vector2(Mathf.Max(MinFootprint, footprintSize.x), Mathf.Max(MinFootprint, footprintSize.y));
    }

    public Vector3 GetFrontageDirectionLocal()
    {
        Vector3 direction = new Vector3(frontageDirection.x, 0f, frontageDirection.z);
        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = new Vector3(frontageOffset.x, 0f, frontageOffset.z);
        }

        if (direction.sqrMagnitude < 0.0001f)
        {
            direction = Vector3.back;
        }

        return direction.normalized;
    }

    public Vector2 GetAlignedFootprintSize()
    {
        Vector2 size = GetFootprintSize();
        Vector3 front = GetFrontageDirectionLocal();
        Vector3 inward = -front;
        Vector3 tangent = new Vector3(-front.z, 0f, front.x).normalized;
        Vector3 localRight = Vector3.right;
        Vector3 localForward = Vector3.forward;

        float width = Mathf.Abs(Vector3.Dot(localRight, tangent)) * size.x + Mathf.Abs(Vector3.Dot(localForward, tangent)) * size.y;
        float depth = Mathf.Abs(Vector3.Dot(localRight, inward)) * size.x + Mathf.Abs(Vector3.Dot(localForward, inward)) * size.y;
        return new Vector2(Mathf.Max(MinFootprint, width), Mathf.Max(MinFootprint, depth));
    }

    private void OnValidate()
    {
        footprintSize = GetFootprintSize();

#if UNITY_EDITOR
        if (!Application.isPlaying && autoComputeFromRenderers)
        {
            AutoComputeFootprintInEditor();
            AutoComputePivotOffsetInEditor();
        }

        if (!frontageOffsetInitialized)
        {
            AutoConfigureFrontageInEditor(false);
        }

        if (!frontageDirectionInitialized)
        {
            AutoConfigureFrontageInEditor(false);
        }

        frontageDirection = GetFrontageDirectionLocal();
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

    private void AutoComputePivotOffsetInEditor()
    {
        ApplyAutoGroundPivot(this);
    }

    private static void ApplyAutoGroundPivot(CityBuilderPrefab component)
    {
        Renderer[] renderers = component.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            EditorUtility.DisplayDialog("Auto ground pivot", "Nessun Renderer trovato nel prefab.", "OK");
            return;
        }

        Bounds combined = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                combined.Encapsulate(renderers[i].bounds);
            }
        }

        Vector3 bottomCenterWorld = new Vector3(combined.center.x, combined.min.y, combined.center.z);

        Undo.RecordObject(component, "Auto ground pivot");
        component.pivotOffset = bottomCenterWorld;
        EditorUtility.SetDirty(component);
    }

    private void Reset()
    {
        if (Application.isPlaying)
        {
            return;
        }

        AutoComputeFootprintInEditor();
        AutoComputePivotOffsetInEditor();
        AutoConfigureFrontageInEditor(true);
    }
#endif

    private void OnDrawGizmosSelected()
    {
        Vector2 size = GetFootprintSize();
        Vector3 pivotWorld = pivotOffset;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;

        // -- Gizmo footprint (ciano) --
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

        // -- Gizmo Frontage (arancio) --
        Vector3 frontageWorld = transform.TransformPoint(frontageOffset);

        // Direzione frontale esplicita del prefab, indipendente dall'offset.
        Vector3 frontageLocalDir = GetFrontageDirectionLocal();
        Vector3 frontageFwdWorld = transform.TransformDirection(frontageLocalDir);

        // Orientamento del piano: piano verticale la cui normale è frontageFwdWorld.
        Quaternion frontageRot = Quaternion.LookRotation(frontageFwdWorld, Vector3.up);

        Gizmos.matrix = Matrix4x4.TRS(frontageWorld, frontageRot, Vector3.one);
        Gizmos.color = new Color(1f, 0.55f, 0f, 1f);
        Gizmos.DrawWireCube(new Vector3(0f, frontageDisplayHeight * 0.5f, 0f),
                            new Vector3(size.x, frontageDisplayHeight, 0.02f));
        Gizmos.color = new Color(1f, 0.55f, 0f, 0.12f);
        Gizmos.DrawCube(new Vector3(0f, frontageDisplayHeight * 0.5f, 0f),
                        new Vector3(size.x, frontageDisplayHeight, 0.001f));

        // Freccia direzione affaccio
        Gizmos.matrix = previousMatrix;
        Gizmos.color = new Color(1f, 0.55f, 0f, 1f);
        float arrowLen = Mathf.Max(0.5f, size.x * 0.25f);
        Gizmos.DrawLine(frontageWorld, frontageWorld + frontageFwdWorld * arrowLen);
        Gizmos.DrawSphere(frontageWorld + frontageFwdWorld * arrowLen, arrowLen * 0.12f);

        Gizmos.matrix = previousMatrix;
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
