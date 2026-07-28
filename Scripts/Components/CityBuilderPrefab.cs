
using UnityEngine;
using System.Collections.Generic;
using BSCCityBuilder.Core;

namespace BSCCityBuilder.Components
{
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Metadati prefab edificio usati dal tool di spawn per valutare footprint e offset.
/// Aggiungere questo componente sul prefab edificio.
/// </summary>
public class CityBuilderPrefab : MonoBehaviour
{ 
    private const float MinFootprint = 0.1f;

    [Header("AI Tagging")]
    [TextArea(2, 5)]
    [Tooltip("Descrizione generata dal modello LLM locale per questo prefab.")]
    public string aiDescription;

    [Tooltip("Lista display name degli ZoneType suggeriti dal modello LLM locale.")]
    public List<string> aiSuggestedZoneDisplayNames = new List<string>();

    [Tooltip("Ingombro sul piano XZ (X=larghezza, Y=profondità).")]
    public Vector2 footprintSize = new Vector2(8f, 8f);

    [Tooltip("Se attivo, tenta di calcolare automaticamente l'ingombro dai Renderer del prefab.")]
    public bool autoComputeFromRenderers = true;

    [SerializeField, HideInInspector]
    private Vector2 cachedRendererFootprint;

    [Tooltip("Offset locale dal centro lotto applicato alla posizione finale.")]
    public Vector3 pivotOffset = Vector3.zero;

    [Tooltip("Anchor opzionale che identifica esplicitamente il piano terra. Utile per edifici con piani interrati.")]
    public Transform groundLevelAnchor;

    [Tooltip("Posizione del piano di affaccio (fronte edificio) in spazio locale. Indica la direzione frontale verso la strada.")]
    public Vector3 frontageOffset = new Vector3(0f, 0f, -4f);

    [Tooltip("Direzione locale della normale del piano Frontage. Permette di ruotare l'affaccio senza vincolarlo all'asse Z.")]
    public Vector3 frontageDirection = Vector3.back;

    [Tooltip("Anchor opzionale dell'ingresso principale. Il suo asse Forward deve puntare verso la strada.")]
    public Transform frontageAnchor;

    [Tooltip("Altezza di visualizzazione del piano Frontage nel gizmo (non influenza la logica).")]
    public float frontageDisplayHeight = 4f;

    // Indica se frontageOffset è stato inizializzato almeno una volta (evita di sovrascrivere valori personalizzati).
    [SerializeField] private bool frontageOffsetInitialized = false;
    [SerializeField] private bool frontageDirectionInitialized = false;

    public Vector2 GetFootprintSize()
    {
        return new Vector2(Mathf.Max(MinFootprint, footprintSize.x), Mathf.Max(MinFootprint, footprintSize.y));
    }

    /// <summary>
    /// Ingombro usato dal layout. Non può essere più piccolo dei Renderer reali:
    /// un footprint manuale maggiore resta valido come spazio di rispetto.
    /// </summary>
    public Vector2 GetLayoutFootprintSize()
    {
        Vector2 configured = GetFootprintSize();
        Vector2 rendered = cachedRendererFootprint;
#if UNITY_EDITOR
        if (rendered.x <= 0f || rendered.y <= 0f)
        {
            rendered = CalculateRendererFootprint();
        }
#endif
        if (rendered.x <= 0f || rendered.y <= 0f)
        {
            return configured;
        }

        return new Vector2(
            Mathf.Max(configured.x, rendered.x),
            Mathf.Max(configured.y, rendered.y));
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
        Vector2 size = GetLayoutFootprintSize();
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
        if (!Application.isPlaying)
        {
            // La cache serve anche ai prefab con footprint manuale: evita di
            // scandire tutti i Renderer per ogni lotto durante la generazione.
            RefreshGeometryMetadataInEditor(autoComputeFromRenderers);
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

    public bool SelectNextFrontageInEditor()
    {
        if (!TryCalculateLocalRendererBounds(out Bounds bounds))
        {
            return false;
        }

        float groundY = TryCalculateWallBaseInEditor(out float detectedGround)
            ? detectedGround
            : bounds.min.y;
        List<FrontageCandidate> candidates = BuildFrontageCandidates(bounds, groundY);
        if (candidates.Count == 0)
        {
            return false;
        }

        int currentIndex = 0;
        float bestMatch = float.MaxValue;
        Vector3 currentDirection = GetFrontageDirectionLocal();
        for (int i = 0; i < candidates.Count; i++)
        {
            float positionDistance =
                (candidates[i].offset - frontageOffset).sqrMagnitude;
            float directionDistance =
                1f - Mathf.Clamp01(Vector3.Dot(
                    candidates[i].direction, currentDirection));
            float match = positionDistance + directionDistance;
            if (match < bestMatch)
            {
                bestMatch = match;
                currentIndex = i;
            }
        }

        FrontageCandidate next = candidates[(currentIndex + 1) % candidates.Count];
        frontageOffset = next.offset;
        frontageDirection = next.direction;
        frontageOffsetInitialized = true;
        frontageDirectionInitialized = true;
        EditorUtility.SetDirty(this);
        return true;
    }

    private struct FrontageCandidate
    {
        public Vector3 offset;
        public Vector3 direction;
    }

    private List<FrontageCandidate> BuildFrontageCandidates(
        Bounds bounds,
        float groundY)
    {
        var candidates = new List<FrontageCandidate>();

        if (frontageAnchor != null && frontageAnchor != transform &&
            frontageAnchor.IsChildOf(transform))
        {
            Vector3 position = transform.InverseTransformPoint(frontageAnchor.position);
            Vector3 direction = transform.InverseTransformDirection(frontageAnchor.forward);
            direction.y = 0f;
            direction = direction.sqrMagnitude > 0.0001f
                ? SnapToCardinalFacade(direction)
                : GetNearestFacadeDirection(position, bounds);
            AddFrontageCandidate(
                candidates,
                ProjectToFacade(position, direction, bounds, groundY),
                direction);
        }

        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < childTransforms.Length; i++)
        {
            Transform candidate = childTransforms[i];
            if (candidate == null || candidate == transform ||
                !IsEntryName(candidate.name))
            {
                continue;
            }

            Vector3 position = transform.InverseTransformPoint(candidate.position);
            Vector3 direction = GetNearestFacadeDirection(position, bounds);
            AddFrontageCandidate(
                candidates,
                ProjectToFacade(position, direction, bounds, groundY),
                direction);
        }

        // Le quattro facciate restano sempre disponibili, anche per prefab
        // privi di oggetti Door/Entry o con nomenclatura non standard.
        AddFrontageCandidate(
            candidates,
            new Vector3(bounds.center.x, groundY, bounds.min.z),
            Vector3.back);
        AddFrontageCandidate(
            candidates,
            new Vector3(bounds.max.x, groundY, bounds.center.z),
            Vector3.right);
        AddFrontageCandidate(
            candidates,
            new Vector3(bounds.center.x, groundY, bounds.max.z),
            Vector3.forward);
        AddFrontageCandidate(
            candidates,
            new Vector3(bounds.min.x, groundY, bounds.center.z),
            Vector3.left);

        return candidates;
    }

    private static void AddFrontageCandidate(
        List<FrontageCandidate> candidates,
        Vector3 offset,
        Vector3 direction)
    {
        direction = SnapToCardinalFacade(direction);
        for (int i = 0; i < candidates.Count; i++)
        {
            if ((candidates[i].offset - offset).sqrMagnitude < 0.01f &&
                Vector3.Dot(candidates[i].direction, direction) > 0.999f)
            {
                return;
            }
        }
        candidates.Add(new FrontageCandidate
        {
            offset = offset,
            direction = direction
        });
    }

    public bool RefreshGeometryMetadataInEditor(bool applyAutomaticValues)
    {
        Vector2 rendererFootprint = CalculateRendererFootprint();
        if (rendererFootprint.x <= 0f || rendererFootprint.y <= 0f)
        {
            if (cachedRendererFootprint != Vector2.zero)
            {
                cachedRendererFootprint = Vector2.zero;
                EditorUtility.SetDirty(this);
            }
            return false;
        }

        bool changed = (cachedRendererFootprint - rendererFootprint).sqrMagnitude > 0.0001f;
        cachedRendererFootprint = rendererFootprint;
        if (applyAutomaticValues)
        {
            if ((footprintSize - rendererFootprint).sqrMagnitude > 0.0001f)
            {
                footprintSize = rendererFootprint;
                changed = true;
            }
            changed |= ApplyAutoGroundPivotInEditor();
        }
        if (changed) EditorUtility.SetDirty(this);
        return true;
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
        ApplyAutoGroundPivotInEditor();
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

        float groundY = TryCalculateWallBaseInEditor(out float detectedGround)
            ? detectedGround
            : localBounds.min.y;
        Vector3 defaultDirection;
        Vector3 defaultOffset;

        if (frontageAnchor != null && frontageAnchor != transform &&
            frontageAnchor.IsChildOf(transform))
        {
            Vector3 anchorPosition = transform.InverseTransformPoint(frontageAnchor.position);
            Vector3 anchorDirection = transform.InverseTransformDirection(frontageAnchor.forward);
            anchorDirection.y = 0f;
            defaultDirection = anchorDirection.sqrMagnitude > 0.0001f
                ? SnapToCardinalFacade(anchorDirection)
                : GetNearestFacadeDirection(anchorPosition, localBounds);
            defaultOffset = ProjectToFacade(
                anchorPosition, defaultDirection, localBounds, groundY);
        }
        else
        {
            Transform bestEntry = FindBestEntryTransform(
                localBounds, groundY, out Vector3 facadeDirection);
            if (bestEntry != null)
            {
                Vector3 entryPosition = transform.InverseTransformPoint(bestEntry.position);
                defaultDirection = facadeDirection;
                defaultOffset = ProjectToFacade(
                    entryPosition, defaultDirection, localBounds, groundY);
            }
            else
            {
                bool frontageAlongX = localBounds.size.x >= localBounds.size.z;
                defaultDirection = frontageAlongX ? Vector3.back : Vector3.left;
                defaultOffset = ProjectToFacade(
                    localBounds.center, defaultDirection, localBounds, groundY);
            }
        }

        frontageOffset = defaultOffset;
        frontageDirection = defaultDirection;
        frontageOffsetInitialized = true;
        frontageDirectionInitialized = true;
        EditorUtility.SetDirty(this);
    }

    private Transform FindBestEntryTransform(
        Bounds bounds,
        float groundY,
        out Vector3 bestFacadeDirection)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        Transform best = null;
        float bestScore = float.MaxValue;
        bestFacadeDirection = Vector3.zero;
        float planarSpan = Mathf.Max(
            MinFootprint, Mathf.Max(bounds.size.x, bounds.size.z));
        float heightSpan = Mathf.Max(MinFootprint, bounds.size.y);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate == null || candidate == transform ||
                !IsEntryName(candidate.name)) continue;

            Vector3 localPos = transform.InverseTransformPoint(candidate.position);
            Vector3 facadeDirection = GetNearestFacadeDirection(localPos, bounds);
            Vector3 projected = ProjectToFacade(
                localPos, facadeDirection, bounds, groundY);
            float edgeDistance = Vector2.Distance(
                new Vector2(localPos.x, localPos.z),
                new Vector2(projected.x, projected.z));
            float verticalDistance = Mathf.Abs(localPos.y - groundY);

            Vector3 candidateForward =
                transform.InverseTransformDirection(candidate.forward);
            candidateForward.y = 0f;
            float orientationPenalty = 0.5f;
            if (candidateForward.sqrMagnitude > 0.0001f)
            {
                candidateForward.Normalize();
                orientationPenalty = 1f - Mathf.Abs(
                    Vector3.Dot(candidateForward, facadeDirection));
            }

            float score =
                edgeDistance / planarSpan * 0.5f +
                verticalDistance / heightSpan * 0.4f +
                orientationPenalty * 0.1f;

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
                bestFacadeDirection = facadeDirection;
            }
        }

        return best;
    }

    private static Vector3 GetNearestFacadeDirection(Vector3 point, Bounds bounds)
    {
        float minX = Mathf.Abs(point.x - bounds.min.x);
        float maxX = Mathf.Abs(bounds.max.x - point.x);
        float minZ = Mathf.Abs(point.z - bounds.min.z);
        float maxZ = Mathf.Abs(bounds.max.z - point.z);
        float nearest = Mathf.Min(Mathf.Min(minX, maxX), Mathf.Min(minZ, maxZ));
        if (nearest == minX) return Vector3.left;
        if (nearest == maxX) return Vector3.right;
        if (nearest == minZ) return Vector3.back;
        return Vector3.forward;
    }

    private static Vector3 SnapToCardinalFacade(Vector3 direction)
    {
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.z))
        {
            return direction.x >= 0f ? Vector3.right : Vector3.left;
        }
        return direction.z >= 0f ? Vector3.forward : Vector3.back;
    }

    private static Vector3 ProjectToFacade(
        Vector3 point,
        Vector3 direction,
        Bounds bounds,
        float groundY)
    {
        Vector3 result = point;
        result.y = groundY;
        if (direction == Vector3.left) result.x = bounds.min.x;
        else if (direction == Vector3.right) result.x = bounds.max.x;
        else if (direction == Vector3.back) result.z = bounds.min.z;
        else result.z = bounds.max.z;
        result.x = Mathf.Clamp(result.x, bounds.min.x, bounds.max.x);
        result.z = Mathf.Clamp(result.z, bounds.min.z, bounds.max.z);
        return result;
    }

    public bool TryCalculateWallBaseInEditor(out float groundY)
    {
        if (groundLevelAnchor != null && groundLevelAnchor != transform &&
            groundLevelAnchor.IsChildOf(transform))
        {
            groundY = transform.InverseTransformPoint(
                groundLevelAnchor.position).y;
            return true;
        }

        float entryY = 0f;
        bool hasEntry = false;
        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < childTransforms.Length; i++)
        {
            Transform child = childTransforms[i];
            if (child == null || !IsEntryName(child.name)) continue;
            float candidateY = transform.InverseTransformPoint(child.position).y;
            if (!hasEntry || Mathf.Abs(candidateY) < Mathf.Abs(entryY))
            {
                entryY = candidateY;
                hasEntry = true;
            }
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool foundWall = false;
        float bestScore = float.MaxValue;
        float bestY = 0f;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!IsGeometryRenderer(renderer) ||
                !IsAboveGroundWallRenderer(renderer) ||
                !TryGetRendererLocalBounds(renderer, out Bounds rendererBounds))
            {
                continue;
            }

            float candidateY = rendererBounds.min.y;
            float verticalSpan = Mathf.Max(MinFootprint, rendererBounds.size.y);
            float referenceDistance = hasEntry
                ? Mathf.Abs(candidateY - entryY)
                : Mathf.Abs(candidateY);
            // Preferisce una base vicina all'ingresso/root e una geometria
            // sufficientemente verticale rispetto a semplici pavimenti.
            float horizontalMin = Mathf.Max(
                MinFootprint,
                Mathf.Min(rendererBounds.size.x, rendererBounds.size.z));
            float verticalPenalty = verticalSpan < horizontalMin * 0.25f ? 1f : 0f;
            float score = referenceDistance + verticalPenalty * verticalSpan;
            if (score < bestScore)
            {
                bestScore = score;
                bestY = candidateY;
                foundWall = true;
            }
        }

        if (foundWall)
        {
            groundY = bestY;
            return true;
        }

        if (hasEntry)
        {
            groundY = entryY;
            return true;
        }

        groundY = 0f;
        return false;
    }

    private static bool IsEntryName(string objectName)
    {
        string value = (objectName ?? string.Empty).ToLowerInvariant();
        return value.Contains("door") ||
               value.Contains("entry") ||
               value.Contains("entrance") ||
               value.Contains("doorway") ||
               value.Contains("portal") ||
               value.Contains("gate") ||
               value.Contains("porta") ||
               value.Contains("ingresso");
    }

    private static bool IsAboveGroundWallRenderer(Renderer renderer)
    {
        string descriptor = GetRendererDescriptor(renderer);
        if (ContainsAny(
            descriptor,
            "basement", "foundation", "underground", "cellar",
            "seminterr", "interrato", "interrati", "piano-1", "floor-1"))
        {
            return false;
        }

        return ContainsAny(
            descriptor,
            "wall", "walls", "facade", "façade", "exterior", "shell",
            "structure", "building", "body", "house", "parete", "muro");
    }

    private static string GetRendererDescriptor(Renderer renderer)
    {
        string descriptor = renderer != null ? renderer.name : string.Empty;
        if (renderer == null) return descriptor.ToLowerInvariant();

        Transform current = renderer.transform.parent;
        while (current != null)
        {
            descriptor += " " + current.name;
            current = current.parent;
        }
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            if (materials[i] != null) descriptor += " " + materials[i].name;
        }
        return descriptor.ToLowerInvariant();
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        for (int i = 0; i < tokens.Length; i++)
        {
            if (value.Contains(tokens[i])) return true;
        }
        return false;
    }

    private bool TryGetRendererLocalBounds(Renderer renderer, out Bounds localBounds)
    {
        if (renderer == null)
        {
            localBounds = default;
            return false;
        }

        Bounds rendererBounds = renderer.localBounds;
        Matrix4x4 rendererToRoot =
            transform.worldToLocalMatrix * renderer.localToWorldMatrix;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;
        for (int corner = 0; corner < 8; corner++)
        {
            Vector3 point = rendererToRoot.MultiplyPoint3x4(new Vector3(
                (corner & 1) == 0 ? rendererBounds.min.x : rendererBounds.max.x,
                (corner & 2) == 0 ? rendererBounds.min.y : rendererBounds.max.y,
                (corner & 4) == 0 ? rendererBounds.min.z : rendererBounds.max.z));
            if (corner == 0)
            {
                min = point;
                max = point;
            }
            else
            {
                min = Vector3.Min(min, point);
                max = Vector3.Max(max, point);
            }
        }
        localBounds = new Bounds((min + max) * 0.5f, max - min);
        return true;
    }

    public bool TryCalculateLocalRendererBounds(out Bounds localBounds)
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
            if (!IsGeometryRenderer(renderer)) continue;

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

    public bool ApplyAutoGroundPivotInEditor()
    {
        if (!TryCalculateLocalRendererBounds(out Bounds localBounds))
        {
            return false;
        }

        float groundY = TryCalculateWallBaseInEditor(out float detectedGround)
            ? detectedGround
            : localBounds.min.y;
        Vector3 bottomCenterLocal = new Vector3(
            localBounds.center.x, groundY, localBounds.center.z);
        if ((pivotOffset - bottomCenterLocal).sqrMagnitude <= 0.0001f)
        {
            return false;
        }
        pivotOffset = bottomCenterLocal;
        EditorUtility.SetDirty(this);
        return true;
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
        Vector2 size = GetLayoutFootprintSize();
        Vector3 pivotWorld = transform.TransformPoint(pivotOffset);

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

        bool initialized = false;
        Vector3 min = Vector3.zero;
        Vector3 max = Vector3.zero;
        Matrix4x4 worldToRoot = transform.worldToLocalMatrix;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
            {
                continue;
            }
            if (!IsGeometryRenderer(renderer)) continue;

            Bounds rendererBounds = renderer.localBounds;
            Matrix4x4 rendererToRoot = worldToRoot * renderer.localToWorldMatrix;
            Vector3 boundsMin = rendererBounds.min;
            Vector3 boundsMax = rendererBounds.max;
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 localCorner = new Vector3(
                    (corner & 1) == 0 ? boundsMin.x : boundsMax.x,
                    (corner & 2) == 0 ? boundsMin.y : boundsMax.y,
                    (corner & 4) == 0 ? boundsMin.z : boundsMax.z);
                Vector3 rootPoint = rendererToRoot.MultiplyPoint3x4(localCorner);
                if (!initialized)
                {
                    min = rootPoint;
                    max = rootPoint;
                    initialized = true;
                }
                else
                {
                    min = Vector3.Min(min, rootPoint);
                    max = Vector3.Max(max, rootPoint);
                }
            }
        }

        if (!initialized)
        {
            return Vector2.zero;
        }

        Vector3 localSize = max - min;
        Vector3 worldScale = transform.lossyScale;
        return new Vector2(
            Mathf.Max(MinFootprint, localSize.x * Mathf.Abs(worldScale.x)),
            Mathf.Max(MinFootprint, localSize.z * Mathf.Abs(worldScale.z)));
    }

    private static bool IsGeometryRenderer(Renderer renderer)
    {
        return renderer != null &&
               !(renderer is ParticleSystemRenderer) &&
               !(renderer is TrailRenderer) &&
               !(renderer is LineRenderer);
    }
}

}
