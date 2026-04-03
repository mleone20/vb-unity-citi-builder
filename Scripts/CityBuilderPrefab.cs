
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
    public void ResetFrontageToAutoDetectedDefault()
    {
        frontageOffsetInitialized = false;
        frontageDirectionInitialized = false;
        AutoConfigureFrontageInEditor(true);
        frontageDirection = GetFrontageDirectionLocal();
        EditorUtility.SetDirty(this);
    }

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

    private void AutoConfigureFrontageInEditor(bool force)
    {
        if (!force && frontageOffsetInitialized && frontageDirectionInitialized)
        {
            return;
        }

        Bounds localBounds;
        if (!TryCalculateLocalRendererBounds(out localBounds))
        {
            Vector2 footprint = GetFootprintSize();
            localBounds = new Bounds(Vector3.zero, new Vector3(footprint.x, 0.1f, footprint.y));
        }

        Vector3 boundsCenter = localBounds.center;
        Vector3 boundsMin = localBounds.min;
        Vector3 boundsMax = localBounds.max;
        float sizeX = Mathf.Max(MinFootprint, localBounds.size.x);
        float sizeZ = Mathf.Max(MinFootprint, localBounds.size.z);
        bool longestSideIsX = sizeX >= sizeZ;

        Vector3 defaultDirection = longestSideIsX ? Vector3.back : Vector3.left;
        Vector3 defaultOffset = longestSideIsX
            ? new Vector3(boundsCenter.x, 0f, boundsMin.z)
            : new Vector3(boundsMin.x, 0f, boundsCenter.z);

        Transform bestEntry = FindOutermostEntryTransform(longestSideIsX, boundsMin, boundsMax);
        if (bestEntry != null)
        {
            Vector3 localEntry = transform.InverseTransformPoint(bestEntry.position);
            Vector3 doorForwardLocal = transform.InverseTransformDirection(bestEntry.forward);
            Vector3 inverseDoorForward = new Vector3(-doorForwardLocal.x, 0f, -doorForwardLocal.z);
            bool hasDoorDirection = inverseDoorForward.sqrMagnitude > 0.0001f;

            if (longestSideIsX)
            {
                float distToMinZ = Mathf.Abs(localEntry.z - boundsMin.z);
                float distToMaxZ = Mathf.Abs(boundsMax.z - localEntry.z);
                bool useMaxSide = distToMaxZ < distToMinZ;
                defaultDirection = hasDoorDirection
                    ? inverseDoorForward.normalized
                    : (useMaxSide ? Vector3.forward : Vector3.back);
                defaultOffset = new Vector3(
                    Mathf.Clamp(localEntry.x, boundsMin.x, boundsMax.x),
                    0f,
                    useMaxSide ? boundsMax.z : boundsMin.z);
            }
            else
            {
                float distToMinX = Mathf.Abs(localEntry.x - boundsMin.x);
                float distToMaxX = Mathf.Abs(boundsMax.x - localEntry.x);
                bool useMaxSide = distToMaxX < distToMinX;
                defaultDirection = hasDoorDirection
                    ? inverseDoorForward.normalized
                    : (useMaxSide ? Vector3.right : Vector3.left);
                defaultOffset = new Vector3(
                    useMaxSide ? boundsMax.x : boundsMin.x,
                    0f,
                    Mathf.Clamp(localEntry.z, boundsMin.z, boundsMax.z));
            }
        }

        frontageOffset = defaultOffset;
        frontageDirection = defaultDirection;
        frontageOffsetInitialized = true;
        frontageDirectionInitialized = true;
        EditorUtility.SetDirty(this);
    }

    private Transform FindOutermostEntryTransform(bool longestSideIsX, Vector3 boundsMin, Vector3 boundsMax)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        Transform best = null;
        float bestScore = float.MaxValue;
        Vector3 pivotLocal = pivotOffset;

        float heightSpan = 1f;
        if (TryCalculateLocalRendererBounds(out Bounds localBounds))
        {
            heightSpan = Mathf.Max(MinFootprint, localBounds.size.y);
        }

        float sideSpan = longestSideIsX
            ? Mathf.Max(MinFootprint, boundsMax.x - boundsMin.x)
            : Mathf.Max(MinFootprint, boundsMax.z - boundsMin.z);

        float depthSpan = longestSideIsX
            ? Mathf.Max(MinFootprint, boundsMax.z - boundsMin.z)
            : Mathf.Max(MinFootprint, boundsMax.x - boundsMin.x);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null)
            {
                continue;
            }

            string lowerName = candidate.name.ToLowerInvariant();
            if (!lowerName.Contains("door") && !lowerName.Contains("entry"))
            {
                continue;
            }

            
            Vector3 localPos = transform.InverseTransformPoint(candidate.position);
            float edgeDistance = longestSideIsX
                ? Mathf.Min(Mathf.Abs(localPos.z - boundsMin.z), Mathf.Abs(boundsMax.z - localPos.z))
                : Mathf.Min(Mathf.Abs(localPos.x - boundsMin.x), Mathf.Abs(boundsMax.x - localPos.x));

            // Peso prioritario: vicinanza al pivot lungo il lato lungo dell'edificio.
            float pivotAlongLongSide = longestSideIsX
                ? Mathf.Abs(localPos.x - pivotLocal.x)
                : Mathf.Abs(localPos.z - pivotLocal.z);

            // Penalizza fortemente candidate lontane in altezza dal pivot (es. porte su tetto).
            float pivotVerticalDistance = Mathf.Abs(localPos.y - pivotLocal.y);

            float normalizedPivotDistance = pivotAlongLongSide / sideSpan;
            float normalizedVerticalDistance = pivotVerticalDistance / heightSpan;
            float normalizedEdgeDistance = edgeDistance / depthSpan;

            // Distanza 3D dal pivot sui due assi davvero rilevanti per l'ingresso: lato lungo + quota.
            float normalizedPivot3D = Mathf.Sqrt(
                normalizedPivotDistance * normalizedPivotDistance +
                normalizedVerticalDistance * normalizedVerticalDistance);

            // Priorita': vicinanza al pivot (soprattutto in quota) >> posizione esterna sul bordo.
            float score = normalizedPivot3D * 0.9f + normalizedEdgeDistance * 0.1f;

            Debug.Log($"Valutando candidate '{candidate.name}': pivotPlanar={pivotAlongLongSide:F2} (norm {normalizedPivotDistance:F2}), pivotY={pivotVerticalDistance:F2} (norm {normalizedVerticalDistance:F2}), edgeDist={edgeDistance:F2} (norm {normalizedEdgeDistance:F2}), score={score:F3}");
            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best != null)
        {
            best = GetFurthestDoorAncestor(best);
        }

        Debug.Log($"Auto frontage: best entry candidate is '{best?.name}' with score {bestScore:F3}");

        return best;
    }

    private Transform GetFurthestDoorAncestor(Transform start)
    {
        if (start == null)
        {
            return null;
        }

        Transform furthestDoorNode = start;
        Transform current = start.parent;

        while (current != null && current != transform.parent)
        {
            string lowerName = current.name.ToLowerInvariant();
            if (lowerName.Contains("door") || lowerName.Contains("entry"))
            {
                furthestDoorNode = current;
            }

            if (current == transform)
            {
                break;
            }

            current = current.parent;
        }

        return furthestDoorNode;
    }

    private bool TryCalculateLocalRendererBounds(out Bounds localBounds)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            localBounds = default;
            return false;
        }

        bool initialized = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }

            Bounds worldBounds = renderer.bounds;
            Vector3 extents = worldBounds.extents;
            Vector3 center = worldBounds.center;
            Vector3[] corners = new Vector3[8]
            {
                center + new Vector3(-extents.x, -extents.y, -extents.z),
                center + new Vector3(-extents.x, -extents.y,  extents.z),
                center + new Vector3(-extents.x,  extents.y, -extents.z),
                center + new Vector3(-extents.x,  extents.y,  extents.z),
                center + new Vector3( extents.x, -extents.y, -extents.z),
                center + new Vector3( extents.x, -extents.y,  extents.z),
                center + new Vector3( extents.x,  extents.y, -extents.z),
                center + new Vector3( extents.x,  extents.y,  extents.z)
            };

            for (int c = 0; c < corners.Length; c++)
            {
                Vector3 localCorner = transform.InverseTransformPoint(corners[c]);
                if (!initialized)
                {
                    min = localCorner;
                    max = localCorner;
                    initialized = true;
                }
                else
                {
                    min = Vector3.Min(min, localCorner);
                    max = Vector3.Max(max, localCorner);
                }
            }
        }

        if (!initialized)
        {
            localBounds = default;
            return false;
        }

        localBounds = new Bounds((min + max) * 0.5f, max - min);
        return true;
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
