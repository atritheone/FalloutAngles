using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[DefaultExecutionOrder(50)]
public class NPCLineOfSightRenderer : MonoBehaviour
{
    private const float MinRayDistance = 0.01f;
    private static readonly string[] PlayerEyeOriginNameHints =
    {
        "head",
        "Head",
        "DEF-head",
        "ORG-head",
        "neck",
        "Neck"
    };

    [Header("Runtime References")]
    [SerializeField] private bool autoResolveReferences = true;
    [SerializeField, HideInInspector] private Transform playerRoot;
    [SerializeField, HideInInspector] private PlayerAim playerAim;
    [SerializeField, HideInInspector] private Transform playerEyeOrigin;

    [Header("Line Of Sight")]
    [SerializeField] private LayerMask obstructionLayers = ~0;
    [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;
    [SerializeField, Min(0f)] private float playerEyeHeight = 1.6f;
    [SerializeField, Min(0f)] private float targetPointSkin = 0.03f;
    [SerializeField, Min(0f)] private float updateInterval = 0.05f;
    [SerializeField, Min(0f)] private float hideAfterNoLineOfSightSeconds = 0.25f;

    [Header("Player Sight Cone")]
    // When a PlayerAim is present, its look cone drives this value so sight and head IK match.
    [SerializeField] private bool requirePlayerSightCone = true;

    // Fallback full horizontal cone width centered on the player's constrained look direction when PlayerAim cannot be resolved.
    [SerializeField, Range(1f, 179f)] private float playerSightConeAngleDegrees = 120f;

    [Header("Sampling")]
    [SerializeField] private bool sampleCombinedBounds = true;
    [SerializeField] private bool sampleIndividualRendererBounds = true;
    [SerializeField, Min(1)] private int maxIndividualRenderersToSample = 12;

    [Header("Ignore")]
    [SerializeField] private List<Transform> extraIgnoredRoots = new List<Transform>();

    private readonly List<Renderer> renderers = new List<Renderer>(16);
    private readonly List<Renderer> activeRenderers = new List<Renderer>(16);
    private readonly RaycastHit[] lineOfSightHits = new RaycastHit[64];

    private float nextUpdateTime;
    private bool isRenderingSuppressed;
    private bool hasRendererCache;
    private bool hasVisibilitySample;
    private float lastLineOfSightTime;

    private void Awake()
    {
        ResolveReferences();
        RebuildRendererCache();
        UpdateRenderVisibility();
    }

    private void OnEnable()
    {
        nextUpdateTime = 0f;
        ResolveReferences();
        RebuildRendererCache();
        UpdateRenderVisibility();
    }

    private void OnDisable()
    {
        SetRenderersSuppressed(false);
    }

    private void OnDestroy()
    {
        SetRenderersSuppressed(false);
    }

    private void LateUpdate()
    {
        if (updateInterval > 0f && Time.time < nextUpdateTime)
            return;

        nextUpdateTime = Time.time + updateInterval;
        UpdateRenderVisibility();
    }

    public bool IsRenderingSuppressed()
    {
        return isRenderingSuppressed;
    }

    public void RefreshRenderers()
    {
        RebuildRendererCache();
        UpdateRenderVisibility();
    }

    private void UpdateRenderVisibility()
    {
        if (!hasRendererCache)
            RebuildRendererCache();

        CollectActiveRenderers();
        if (activeRenderers.Count == 0)
        {
            ClearVisibilityStability();
            SetRenderersSuppressed(false);
            return;
        }

        Transform player = ResolvePlayerRoot();
        if (!player)
        {
            ClearVisibilityStability();
            SetRenderersSuppressed(false);
            return;
        }

        bool shouldRender = HasPlayerLineOfSightToAnyRenderer(player);
        ApplyStableVisibility(shouldRender);
    }

    private void ApplyStableVisibility(bool shouldRender)
    {
        float now = Time.time;
        if (shouldRender)
        {
            hasVisibilitySample = true;
            lastLineOfSightTime = now;
            SetRenderersSuppressed(false);
            return;
        }

        if (!hasVisibilitySample)
        {
            hasVisibilitySample = true;
            lastLineOfSightTime = now;
        }

        bool shouldHide = hideAfterNoLineOfSightSeconds <= 0f ||
            now - lastLineOfSightTime >= hideAfterNoLineOfSightSeconds;
        SetRenderersSuppressed(shouldHide);
    }

    private void ClearVisibilityStability()
    {
        hasVisibilitySample = false;
        lastLineOfSightTime = 0f;
    }

    private bool HasPlayerLineOfSightToAnyRenderer(Transform player)
    {
        if (!TryGetCombinedBounds(out Bounds combinedBounds))
            return true;

        Vector3 origin = ResolvePlayerLineOfSightOrigin(player);
        if (sampleCombinedBounds && HasLineOfSightToAnyBoundsPoint(origin, combinedBounds))
            return true;

        if (!sampleIndividualRendererBounds)
            return false;

        int sampledCount = 0;
        int maxSamples = Mathf.Max(1, maxIndividualRenderersToSample);
        for (int i = 0; i < activeRenderers.Count; i++)
        {
            Renderer renderer = activeRenderers[i];
            if (!renderer)
                continue;

            Bounds bounds = renderer.bounds;
            if (HasLineOfSightToAnyBoundsPoint(origin, bounds))
                return true;

            sampledCount++;
            if (sampledCount >= maxSamples)
                break;
        }

        return false;
    }

    private bool HasLineOfSightToAnyBoundsPoint(Vector3 origin, Bounds bounds)
    {
        Vector3 center = bounds.center;
        Vector3 extents = bounds.extents;

        if (HasLineOfSightToPoint(origin, center))
            return true;

        if (HasLineOfSightToPoint(origin, center + new Vector3(extents.x, 0f, 0f))) return true;
        if (HasLineOfSightToPoint(origin, center + new Vector3(-extents.x, 0f, 0f))) return true;
        if (HasLineOfSightToPoint(origin, center + new Vector3(0f, extents.y, 0f))) return true;
        if (HasLineOfSightToPoint(origin, center + new Vector3(0f, -extents.y, 0f))) return true;
        if (HasLineOfSightToPoint(origin, center + new Vector3(0f, 0f, extents.z))) return true;
        if (HasLineOfSightToPoint(origin, center + new Vector3(0f, 0f, -extents.z))) return true;

        for (int x = -1; x <= 1; x += 2)
        {
            for (int y = -1; y <= 1; y += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Vector3 point = center + Vector3.Scale(extents, new Vector3(x, y, z));
                    if (HasLineOfSightToPoint(origin, point))
                        return true;
                }
            }
        }

        return false;
    }

    private bool HasLineOfSightToPoint(Vector3 origin, Vector3 targetPoint)
    {
        Vector3 toTarget = targetPoint - origin;
        float distance = toTarget.magnitude;
        if (distance <= MinRayDistance)
            return true;

        Vector3 direction = toTarget / distance;
        if (!IsInsidePlayerSightCone(origin, direction))
            return false;

        float rayDistance = Mathf.Max(MinRayDistance, distance - Mathf.Max(0f, targetPointSkin));

        return !HasBlockingLineOfSightHit(origin, direction, rayDistance);
    }

    private bool IsInsidePlayerSightCone(Vector3 origin, Vector3 directionToTarget)
    {
        if (!requirePlayerSightCone)
            return true;

        Vector3 sightForward = ResolvePlayerSightForward(origin);
        sightForward.y = 0f;

        Vector3 flatDirectionToTarget = directionToTarget;
        flatDirectionToTarget.y = 0f;

        if (sightForward.sqrMagnitude <= MinRayDistance || flatDirectionToTarget.sqrMagnitude <= MinRayDistance)
            return true;

        float maxSightYaw = ResolvePlayerSightHalfAngleDegrees();
        return Vector3.Angle(sightForward.normalized, flatDirectionToTarget.normalized) <= maxSightYaw;
    }

    private float ResolvePlayerSightHalfAngleDegrees()
    {
        PlayerAim aim = ResolvePlayerAim();
        if (aim)
            return Mathf.Clamp(aim.LookConeAngleDegrees * 0.5f, 0.5f, 89.5f);

        return Mathf.Clamp(playerSightConeAngleDegrees, 1f, 179f) * 0.5f;
    }

    private Vector3 ResolvePlayerSightForward(Vector3 origin)
    {
        PlayerAim aim = ResolvePlayerAim();
        if (aim && aim.TryGetConstrainedFlatLookDirection(origin, out Vector3 constrainedLookDirection))
            return constrainedLookDirection;

        Transform player = ResolvePlayerRoot();
        if (player)
        {
            Vector3 forward = player.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude > MinRayDistance)
                return forward.normalized;
        }

        return Vector3.forward;
    }

    private Vector3 ResolvePlayerLineOfSightOrigin(Transform player)
    {
        Transform eyeOrigin = ResolvePlayerEyeOrigin();
        if (eyeOrigin)
            return eyeOrigin.position;

        return player.position + player.up * Mathf.Max(0f, playerEyeHeight);
    }

    private bool HasBlockingLineOfSightHit(Vector3 origin, Vector3 direction, float distance)
    {
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            lineOfSightHits,
            distance,
            obstructionLayers,
            triggerInteraction
        );

        bool saturated = hitCount >= lineOfSightHits.Length;
        if (HasBlockingLineOfSightHit(lineOfSightHits, hitCount))
            return true;

        if (!saturated)
            return false;

        RaycastHit[] allHits = Physics.RaycastAll(origin, direction, distance, obstructionLayers, triggerInteraction);
        return HasBlockingLineOfSightHit(allHits, allHits.Length);
    }

    private bool HasBlockingLineOfSightHit(RaycastHit[] hits, int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = hits[i].collider;
            if (!hitCollider)
                continue;

            if (ShouldIgnoreHit(hitCollider.transform))
                continue;

            return true;
        }

        return false;
    }

    private bool ShouldIgnoreHit(Transform hitTransform)
    {
        if (!hitTransform)
            return true;

        Transform self = transform;
        if (hitTransform == self || hitTransform.IsChildOf(self))
            return true;

        Transform player = playerRoot;
        if (player && (hitTransform == player || hitTransform.IsChildOf(player)))
            return true;

        if (extraIgnoredRoots != null)
        {
            for (int i = 0; i < extraIgnoredRoots.Count; i++)
            {
                Transform ignoredRoot = extraIgnoredRoots[i];
                if (!ignoredRoot)
                    continue;

                if (hitTransform == ignoredRoot || hitTransform.IsChildOf(ignoredRoot))
                    return true;
            }
        }

        return false;
    }

    private bool TryGetCombinedBounds(out Bounds combinedBounds)
    {
        combinedBounds = default;
        bool found = false;

        for (int i = 0; i < activeRenderers.Count; i++)
        {
            Renderer renderer = activeRenderers[i];
            if (!renderer)
                continue;

            if (!found)
            {
                combinedBounds = renderer.bounds;
                found = true;
            }
            else
            {
                combinedBounds.Encapsulate(renderer.bounds);
            }
        }

        return found;
    }

    private void SetRenderersSuppressed(bool suppressed)
    {
        if (isRenderingSuppressed == suppressed)
            return;

        isRenderingSuppressed = suppressed;
        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer)
                renderer.forceRenderingOff = suppressed;
        }
    }

    private void RebuildRendererCache()
    {
        renderers.Clear();
        GetComponentsInChildren(true, renderers);
        hasRendererCache = true;

        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer)
                renderer.forceRenderingOff = isRenderingSuppressed;
        }
    }

    private void CollectActiveRenderers()
    {
        activeRenderers.Clear();

        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (!renderer)
                continue;

            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy)
                continue;

            activeRenderers.Add(renderer);
        }
    }

    private void ResolveReferences()
    {
        ResolvePlayerRoot();
        ResolvePlayerAim();
        ResolvePlayerEyeOrigin();
    }

    private Transform ResolvePlayerRoot()
    {
        if (playerRoot)
            return playerRoot;

        if (autoResolveReferences && !playerRoot)
        {
            PlayerState playerState = FindAnyObjectByType<PlayerState>();
            if (playerState)
                playerRoot = playerState.transform;
        }

        return playerRoot;
    }

    private PlayerAim ResolvePlayerAim()
    {
        if (playerAim)
            return playerAim;

        Transform player = ResolvePlayerRoot();
        if (autoResolveReferences && player)
            playerAim = player.GetComponentInChildren<PlayerAim>();

        if (autoResolveReferences && !playerAim)
            playerAim = FindAnyObjectByType<PlayerAim>();

        return playerAim;
    }

    private Transform ResolvePlayerEyeOrigin()
    {
        Transform player = ResolvePlayerRoot();
        if (!player)
            return null;

        if (playerEyeOrigin && playerEyeOrigin.IsChildOf(player))
            return playerEyeOrigin;

        if (!autoResolveReferences)
            return playerEyeOrigin;

        for (int i = 0; i < PlayerEyeOriginNameHints.Length; i++)
        {
            Transform found = FindChildByName(player, PlayerEyeOriginNameHints[i]);
            if (found)
            {
                playerEyeOrigin = found;
                return playerEyeOrigin;
            }
        }

        return playerEyeOrigin;
    }

    private static Transform FindChildByName(Transform root, string childName)
    {
        if (!root)
            return null;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (child && string.Equals(child.name, childName, System.StringComparison.OrdinalIgnoreCase))
                return child;
        }

        return null;
    }
}
