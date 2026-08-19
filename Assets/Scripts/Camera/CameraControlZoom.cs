// imports
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;



// methods
public class CameraControlZoom : MonoBehaviour
{
    private const float ScrollDeadzone = 0.01f;
    private const int DefaultObstructionLayerMask = 1 << 7;
    private const float MinObstructionCastDistance = 0.001f;
    private const string SwitchCameraSideActionPath = "Player/Switch Camera Side";

    [System.Serializable]
    private struct CrouchStateVerticalOffset
    {
        // Animator state short name (exact state name shown in Animator).
        public string stateName;

        // Vertical offset to add while this state is active.
        public float verticalOffset;
    }

    private struct CrouchStateOffsetCacheEntry
    {
        // Cached hash for fast state matching each frame.
        public int stateShortNameHash;

        // Vertical offset mapped to the cached state hash.
        public float verticalOffset;
    }

    [Header("References")]
    // The player's transform that the rig should stay centered on.
    [SerializeField] private Transform playerTarget;

    // Player state used to detect combat mode for shoulder offset behavior.
    [SerializeField] private PlayerState playerState;

    // Reference to the Cinemachine Camera component (your Virtual Camera).
    [SerializeField] private CinemachineCamera virtualCamera;

    // Animator that owns crouch animation states.
    [SerializeField] private Animator playerAnimator;

    [Header("Input")]
    // Input action: Mouse scroll (Vector2) for zoom.
    [SerializeField] private InputActionReference cameraZoom;

    // Input action: toggles between right-shoulder and left-shoulder camera side.
    [SerializeField] private InputActionReference switchCameraSide;

    // Whether zoom input and zoom smoothing should be processed.
    private bool inputEnabled = true;

    // Prevents a UI scroll delta from being applied on the first frame after re-enabling zoom.
    private bool suppressZoomInputUntilScrollReleased;

    [Header("Zoom Limits")]
    // How fast zoom changes per scroll tick.
    [SerializeField] private float zoomSpeed = 0.04f;

    // Minimum allowed camera distance (NORMAL zoom only).
    [SerializeField] private float minDistance = 6.0f;

    // Maximum allowed camera distance (NORMAL zoom only).
    [SerializeField] private float maxDistance = 22.0f;

    [Header("Smoothing")]
    // How smooth zoom feels (lower = snappier, higher = floatier).
    [SerializeField] private float zoomSmoothTime = 0.08f;

    // When we are within this distance, snap and stop smoothing (prevents micro-updates).
    [SerializeField] private float zoomSnapEpsilon = 0.01f;

    [Header("Tracking Offset")]
    // If true, scales the tracked object X offset based on zoom distance.
    [SerializeField] private bool scaleTrackedOffsetWithZoom = true;

    // Scale applied to the base X offset at minimum zoom distance.
    [SerializeField] private float minTrackedOffsetScale = 0.4f;

    // Scale applied to the base X offset at maximum zoom distance.
    [SerializeField] private float maxTrackedOffsetScale = 1.0f;

    [Header("Combat Tracking Offset")]
    // If true, combat mode uses its own zoom-scaled shoulder offset range.
    [SerializeField] private bool useCombatTrackedOffsetScale = true;

    // Scale applied to the base X offset at minimum zoom while in combat mode.
    [SerializeField] private float combatMinTrackedOffsetScale = 1.15f;

    // Scale applied to the base X offset at maximum zoom while in combat mode.
    [SerializeField] private float combatMaxTrackedOffsetScale = 1.85f;

    [Header("Crouch Vertical Offset")]
    // If true, applies vertical TargetOffset Y modifiers per crouch animation state.
    [SerializeField] private bool useCrouchStateVerticalOffsets = true;

    // Animator layer index used to read crouch states.
    [SerializeField] private int crouchAnimatorLayer = 0;

    // How smooth the crouch vertical offset transitions are.
    [SerializeField] private float crouchVerticalOffsetSmoothTime = 0.08f;

    // Snap epsilon to prevent tiny per-frame Y writes.
    [SerializeField] private float crouchVerticalOffsetSnapEpsilon = 0.001f;

    // Per-state vertical offsets for crouch variants.
    [SerializeField] private CrouchStateVerticalOffset[] crouchStateVerticalOffsets = new CrouchStateVerticalOffset[0];

    [Header("Camera Obstruction")]
    // If true, zooms the camera in whenever world geometry sits between player and camera.
    [SerializeField] private bool preventWallObstruction = true;

    // Layers that can push the camera closer. Defaults to the project World layer.
    [SerializeField] private LayerMask obstructionLayers = DefaultObstructionLayerMask;

    // Radius of the camera clearance probe. Use a sphere so the camera does not skim wall edges.
    [SerializeField] private float obstructionProbeRadius = 0.25f;

    // Extra distance to keep between the camera and the hit wall.
    [SerializeField] private float obstructionWallBuffer = 0.2f;

    // Smallest emergency distance allowed when the player is tight against a wall.
    [SerializeField] private float obstructionMinimumDistance = 0.35f;

    // If true, wall avoidance cannot push the camera inside the player's model.
    [SerializeField] private bool preventPlayerModelIntersection = true;

    // Hard lower bound used only for obstruction zoom, measured from the obstruction origin toward the camera.
    [SerializeField] private float obstructionPlayerClearanceDistance = 1.2f;

    // If true, casts from the same tracked offset the camera composes around.
    [SerializeField] private bool useTrackedOffsetForObstructionOrigin = true;

    // Vertical fallback origin if tracked offset use is disabled.
    [SerializeField] private float obstructionFallbackTargetHeight = 1.5f;

    // Cached Position Composer (because your vcam is using it in the inspector).
    private CinemachinePositionComposer positionComposer;

    // The target distance we want to zoom to (NORMAL mode).
    private float targetDistance;

    // SmoothDamp velocity holder for zoom smoothing.
    private float zoomVelocity;

    // Whether ADS (or anything) is currently overriding zoom.
    private bool isZoomOverridden;

    // The fixed distance we want while overridden (ADS).
    private float overriddenDistance;

    // The exact distance we were at when override began (so we restore perfectly).
    private float storedDistanceBeforeOverride;

    // Baseline tracked offset from the Position Composer (used for scaling X only).
    private Vector3 baseTrackedOffset;

    // Side multiplier for the base tracked X offset. Initial side is whatever the scene has configured.
    private float shoulderSideSign = 1f;

    // Runtime-smoothed crouch vertical offset currently applied to camera Y.
    private float currentCrouchVerticalOffset;

    // SmoothDamp velocity holder for crouch vertical offset transitions.
    private float crouchVerticalOffsetVelocity;

    // Runtime cache for quick animator state -> vertical offset lookups.
    private CrouchStateOffsetCacheEntry[] crouchStateOffsetCache = new CrouchStateOffsetCacheEntry[0];

    // Non-alloc buffer for camera obstruction probes. Dense modular geometry can exceed small buffers while orbiting.
    private readonly RaycastHit[] obstructionHits = new RaycastHit[64];

    // Runtime fallback action found from the input asset when no explicit switch reference is assigned.
    private InputAction resolvedSwitchCameraSideAction;

    // The action instance currently carrying the performed callback.
    private InputAction subscribedSwitchCameraSideAction;

    // Tracks callback registration so enable/disable cycles do not double-subscribe.
    private bool switchCameraSideSubscribed;



    private void Awake()
    {
        CinemachineCamera vcam = virtualCamera;

        // Stop if we have no virtual camera assigned.
        if (!vcam) return;

        // Auto-wire player state if missing.
        if (!playerState && playerTarget)
            playerState = playerTarget.GetComponentInParent<PlayerState>();

        // Auto-wire animator if missing.
        if (!playerAnimator && playerTarget)
            playerAnimator = playerTarget.GetComponentInChildren<Animator>();
        if (!playerAnimator)
            playerAnimator = GetComponentInParent<Animator>();

        // Build hash cache from configured crouch state names.
        RebuildCrouchStateOffsetCache();

        // Cache the Position Composer so we can change CameraDistance.
        positionComposer = vcam.GetComponent<CinemachinePositionComposer>();

        // Stop if this camera doesn't have a Position Composer.
        if (!positionComposer) return;

        // Cache the base tracked offset so we only scale X relative to this value.
        baseTrackedOffset = positionComposer.TargetOffset;
        shoulderSideSign = baseTrackedOffset.x < 0f ? -1f : 1f;

        // Start target distance at current camera distance.
        targetDistance = positionComposer.CameraDistance;

        // Initialize crouch vertical offset so the first frame does not pop.
        currentCrouchVerticalOffset = GetTargetCrouchVerticalOffset();
    }


    private void OnValidate()
    {
        // Rebuild state-hash cache when inspector values change.
        RebuildCrouchStateOffsetCache();
    }


    private void OnEnable()
    {
        SubscribeSwitchCameraSideAction();
        SetInputActionsEnabled(inputEnabled);
    }


    private void OnDisable()
    {
        SetInputActionsEnabled(false);
        UnsubscribeSwitchCameraSideAction();
    }


    private void Update()
    {
        // Stop if we don't have a player target.
        if (!playerTarget) return;

        // Stop while a UI owns input.
        if (!inputEnabled) return;

        // If zoom is overridden (ADS), we ignore scroll input entirely.
        if (isZoomOverridden) return;

        // Read scroll input and update the target zoom distance only when the wheel moves.
        HandleZoomInput();
    }


    private void LateUpdate()
    {
        // Stop while a UI owns input.
        if (!inputEnabled) return;

        // Smoothly move toward the active target distance.
        SmoothZoomTowardTarget();
    }


    public void SetInputEnabled(bool enabled)
    {
        bool wasInputEnabled = inputEnabled;
        inputEnabled = enabled;
        SetInputActionsEnabled(inputEnabled && isActiveAndEnabled);

        if (!inputEnabled)
        {
            SyncTargetToCurrent();
            return;
        }

        if (!wasInputEnabled)
            suppressZoomInputUntilScrollReleased = true;
    }


    private void SetInputActionsEnabled(bool enabled)
    {
        SetInputActionEnabled(cameraZoom ? cameraZoom.action : null, enabled);
        SetInputActionEnabled(GetSwitchCameraSideAction(), enabled);
    }


    private static void SetInputActionEnabled(InputAction action, bool enabled)
    {
        if (action == null) return;

        if (enabled)
            action.Enable();
        else
            action.Disable();
    }


    private void SubscribeSwitchCameraSideAction()
    {
        InputAction action = GetSwitchCameraSideAction();

        if (action == null) return;

        if (switchCameraSideSubscribed) return;

        action.performed += OnSwitchCameraSidePerformed;
        subscribedSwitchCameraSideAction = action;
        switchCameraSideSubscribed = true;
    }


    private void UnsubscribeSwitchCameraSideAction()
    {
        if (!switchCameraSideSubscribed) return;

        InputAction action = subscribedSwitchCameraSideAction;

        if (action == null)
        {
            switchCameraSideSubscribed = false;
            return;
        }

        action.performed -= OnSwitchCameraSidePerformed;
        subscribedSwitchCameraSideAction = null;
        switchCameraSideSubscribed = false;
    }


    private InputAction GetSwitchCameraSideAction()
    {
        InputAction explicitAction = switchCameraSide ? switchCameraSide.action : null;

        if (explicitAction != null)
            return explicitAction;

        if (resolvedSwitchCameraSideAction != null)
            return resolvedSwitchCameraSideAction;

        InputAction zoomAction = cameraZoom ? cameraZoom.action : null;
        InputActionAsset asset = zoomAction?.actionMap?.asset;

        if (asset == null) return null;

        resolvedSwitchCameraSideAction = asset.FindAction(SwitchCameraSideActionPath, false);

        return resolvedSwitchCameraSideAction;
    }


    private void OnSwitchCameraSidePerformed(InputAction.CallbackContext context)
    {
        if (!inputEnabled) return;

        ToggleCameraSide();
    }


    public void ToggleCameraSide()
    {
        shoulderSideSign = shoulderSideSign >= 0f ? -1f : 1f;

        CinemachinePositionComposer composer = positionComposer;

        if (!composer) return;

        UpdateTrackedOffset(composer.CameraDistance);
    }


    public bool IsUsingInitialCameraSide()
    {
        return shoulderSideSign == (baseTrackedOffset.x < 0f ? -1f : 1f);
    }


    private void HandleZoomInput()
    {
        CinemachinePositionComposer composer = positionComposer;

        // Stop if we don't have a position composer.
        if (!composer) return;

        InputAction zoomAction = cameraZoom ? cameraZoom.action : null;

        // Stop if we have no zoom action.
        if (zoomAction == null) return;

        // Read scroll input (Y is usually the useful axis).
        float scrollY = zoomAction.ReadValue<Vector2>().y;

        if (suppressZoomInputUntilScrollReleased)
        {
            if (Mathf.Abs(scrollY) < ScrollDeadzone)
                suppressZoomInputUntilScrollReleased = false;

            return;
        }

        // Ignore tiny or zero scroll values so we don't churn.
        if (Mathf.Abs(scrollY) < ScrollDeadzone) return;

        // Convert scroll into a distance delta (invert if you prefer).
        float delta = -scrollY * zoomSpeed;

        // Update target distance with the scroll delta.
        float requestedTargetDistance = targetDistance + delta;

        // Clamp NORMAL target distance so we can't zoom too far.
        requestedTargetDistance = Mathf.Clamp(requestedTargetDistance, minDistance, maxDistance);

        targetDistance = requestedTargetDistance;
    }


    private void SmoothZoomTowardTarget()
    {
        CinemachinePositionComposer composer = positionComposer;

        // Stop if we don't have a position composer.
        if (!composer) return;

        // Read the current distance.
        float currentDistance = composer.CameraDistance;

        // Decide what we are trying to reach this frame (override beats normal).
        float activeTargetDistance = isZoomOverridden ? overriddenDistance : targetDistance;

        // Pull the live camera inside the nearest wall between player and camera.
        activeTargetDistance = GetObstructionLimitedDistance(activeTargetDistance, out bool isObstructionLimited);

        // Wall avoidance should not wait for normal zoom smoothing, otherwise the wall can hide the player.
        if (isObstructionLimited && currentDistance > activeTargetDistance)
        {
            if (!Mathf.Approximately(currentDistance, activeTargetDistance))
                composer.CameraDistance = activeTargetDistance;

            UpdateTrackedOffset(activeTargetDistance);

            zoomVelocity = 0f;

            return;
        }

        // Work out how far we are from the active target.
        float distanceToTarget = Mathf.Abs(currentDistance - activeTargetDistance);

        // If we're basically at the target, snap and stop updating to avoid micro-writes.
        if (distanceToTarget <= zoomSnapEpsilon)
        {
            // Apply the exact active target distance.
            if (!Mathf.Approximately(currentDistance, activeTargetDistance))
                composer.CameraDistance = activeTargetDistance;

            // Update tracking offset to match the snapped distance.
            UpdateTrackedOffset(activeTargetDistance);

            // Clear velocity so the next zoom starts clean.
            zoomVelocity = 0f;

            // Stop here.
            return;
        }

        // Smoothly move the current distance toward the active target distance.
        float smoothDistance = Mathf.SmoothDamp(currentDistance, activeTargetDistance, ref zoomVelocity, zoomSmoothTime);

        // Apply the smoothed distance to Cinemachine (only while transitioning).
        composer.CameraDistance = smoothDistance;

        // Update tracking offset using the live distance.
        UpdateTrackedOffset(smoothDistance);
    }


    // ADS API: get the current camera distance (not the target).
    public float GetCurrentDistance()
    {
        CinemachinePositionComposer composer = positionComposer;

        // Stop if we don't have a composer.
        if (!composer) return 0f;

        // Return the live Cinemachine camera distance.
        return composer.CameraDistance;
    }


    // ADS API: begin an override ONCE (stores the pre-ADS distance only the first time).
    public void BeginZoomOverride(float fixedDistance)
    {
        CinemachinePositionComposer composer = positionComposer;

        // Stop if we don't have a composer.
        if (!composer) return;

        // If we are already overridden, do NOT re-store the pre-ADS distance.
        if (!isZoomOverridden)
        {
            // Store the exact live distance so we can restore perfectly.
            storedDistanceBeforeOverride = composer.CameraDistance;
        }

        // Store the fixed distance we want during override.
        overriddenDistance = fixedDistance;

        // Mark override as active.
        isZoomOverridden = true;

        // Clear velocity so the override feels crisp and doesn't inherit old smoothing.
        zoomVelocity = 0f;
    }


    // ADS API: update only the override distance (no re-storing).
    public void SetOverrideDistance(float fixedDistance)
    {
        // Stop if we are not overridden.
        if (!isZoomOverridden) return;

        // Update the target override distance.
        overriddenDistance = fixedDistance;
    }


    // ADS API: end the override and restore to the exact previous distance.
    public void EndZoomOverride()
    {
        CinemachinePositionComposer composer = positionComposer;

        // Stop if we don't have a composer.
        if (!composer) return;

        // Disable override.
        isZoomOverridden = false;

        // Restore both the live camera distance and the normal target to what we had.
        composer.CameraDistance = storedDistanceBeforeOverride;

        // Keep normal target aligned with the restored value (prevents snap on next scroll).
        targetDistance = storedDistanceBeforeOverride;

        // Clear velocity so we don't drift after restoring.
        zoomVelocity = 0f;
    }


    // Normal API: read the current NORMAL zoom target distance.
    public float GetTargetDistance()
    {
        // Return the current desired zoom distance.
        return targetDistance;
    }


    // Normal API: set the NORMAL zoom target distance (still clamped).
    public void SetTargetDistance(float newTargetDistance)
    {
        // Clamp the requested distance so it respects NORMAL camera limits.
        targetDistance = Mathf.Clamp(newTargetDistance, minDistance, maxDistance);
    }


    // Normal API: force the NORMAL target distance to whatever the camera is currently at.
    public void SyncTargetToCurrent()
    {
        CinemachinePositionComposer composer = positionComposer;

        // Stop if we don't have a composer to read from.
        if (!composer) return;

        // Copy the current camera distance into the target distance.
        targetDistance = composer.CameraDistance;

        // Clear smoothing velocity so we don't overshoot.
        zoomVelocity = 0f;
    }


    private void UpdateTrackedOffset(float currentDistance)
    {
        CinemachinePositionComposer composer = positionComposer;

        // Stop if we don't have a composer.
        if (!composer) return;

        // Start with no X scaling so we preserve base offset when scaling is disabled.
        float xScale = 1f;

        // Apply zoom/combat scaling for X only when enabled.
        if (scaleTrackedOffsetWithZoom)
        {
            // Normalize distance across the zoom range.
            float t = Mathf.InverseLerp(minDistance, maxDistance, currentDistance);

            // Scale the base offset X between the configured scales.
            xScale = Mathf.Lerp(minTrackedOffsetScale, maxTrackedOffsetScale, t);

            // In combat mode, push farther over the right shoulder while preserving zoom relation.
            if (useCombatTrackedOffsetScale && IsCombatModeActive())
                xScale = Mathf.Lerp(combatMinTrackedOffsetScale, combatMaxTrackedOffsetScale, t);
        }

        // Resolve the configured crouch-state vertical offset target.
        float targetCrouchVerticalOffset = GetTargetCrouchVerticalOffset();

        // Smoothly approach the state target so state changes don't hard-snap the camera.
        currentCrouchVerticalOffset = Mathf.SmoothDamp(
            currentCrouchVerticalOffset,
            targetCrouchVerticalOffset,
            ref crouchVerticalOffsetVelocity,
            crouchVerticalOffsetSmoothTime
        );

        // Snap tiny deltas to remove micro-updates.
        if (Mathf.Abs(currentCrouchVerticalOffset - targetCrouchVerticalOffset) <= crouchVerticalOffsetSnapEpsilon)
        {
            currentCrouchVerticalOffset = targetCrouchVerticalOffset;
            crouchVerticalOffsetVelocity = 0f;
        }

        // Adjust X by zoom scale and Y by crouch-state offset.
        Vector3 offset = baseTrackedOffset;
        offset.x = Mathf.Abs(baseTrackedOffset.x) * shoulderSideSign * xScale;
        offset.y = baseTrackedOffset.y + currentCrouchVerticalOffset;

        // Apply the adjusted tracked object offset only when it changed.
        if (composer.TargetOffset != offset)
            composer.TargetOffset = offset;
    }


    private float GetTargetCrouchVerticalOffset()
    {
        // Stop if crouch-state vertical offsets are disabled.
        if (!useCrouchStateVerticalOffsets) return 0f;

        Animator anim = playerAnimator;

        // Stop if no animator is available.
        if (!anim) return 0f;

        int layer = crouchAnimatorLayer;

        // Stop if configured layer index is invalid.
        if (layer < 0 || layer >= anim.layerCount) return 0f;

        // Read current animator state on the configured layer.
        AnimatorStateInfo currentState = anim.GetCurrentAnimatorStateInfo(layer);
        float currentOffset = GetConfiguredCrouchStateOffset(currentState.shortNameHash);

        // If not transitioning, current state is the full answer.
        if (!anim.IsInTransition(layer))
            return currentOffset;

        // While transitioning, blend toward the next state's configured offset.
        AnimatorStateInfo nextState = anim.GetNextAnimatorStateInfo(layer);
        float nextOffset = GetConfiguredCrouchStateOffset(nextState.shortNameHash);
        AnimatorTransitionInfo transition = anim.GetAnimatorTransitionInfo(layer);
        float transitionT = Mathf.Clamp01(transition.normalizedTime);

        return Mathf.Lerp(currentOffset, nextOffset, transitionT);
    }


    private float GetConfiguredCrouchStateOffset(int stateShortNameHash)
    {
        CrouchStateOffsetCacheEntry[] cache = crouchStateOffsetCache;

        // Stop if no offsets are configured.
        if (cache == null || cache.Length == 0) return 0f;

        // Search for a configured state match.
        for (int i = 0; i < cache.Length; i++)
        {
            if (cache[i].stateShortNameHash == stateShortNameHash)
                return cache[i].verticalOffset;
        }

        // Unlisted states have no extra vertical offset.
        return 0f;
    }


    private void RebuildCrouchStateOffsetCache()
    {
        CrouchStateVerticalOffset[] configured = crouchStateVerticalOffsets;

        // Stop if no state offsets are configured.
        if (configured == null || configured.Length == 0)
        {
            crouchStateOffsetCache = new CrouchStateOffsetCacheEntry[0];
            return;
        }

        // Build worst-case sized cache, then trim to only valid configured entries.
        CrouchStateOffsetCacheEntry[] built = new CrouchStateOffsetCacheEntry[configured.Length];
        int count = 0;

        for (int i = 0; i < configured.Length; i++)
        {
            string stateName = configured[i].stateName;

            // Ignore empty state names.
            if (string.IsNullOrWhiteSpace(stateName))
                continue;

            built[count].stateShortNameHash = Animator.StringToHash(stateName);
            built[count].verticalOffset = configured[i].verticalOffset;
            count++;
        }

        if (count == 0)
        {
            crouchStateOffsetCache = new CrouchStateOffsetCacheEntry[0];
            return;
        }

        if (count == built.Length)
        {
            crouchStateOffsetCache = built;
            return;
        }

        CrouchStateOffsetCacheEntry[] trimmed = new CrouchStateOffsetCacheEntry[count];

        for (int i = 0; i < count; i++)
            trimmed[i] = built[i];

        crouchStateOffsetCache = trimmed;
    }


    private bool IsCombatModeActive()
    {
        if (!playerState) return false;

        return playerState.GetCombatMode();
    }


    private float GetObstructionLimitedDistance(float desiredDistance, out bool isLimited)
    {
        isLimited = false;

        if (!preventWallObstruction) return desiredDistance;

        if (!playerTarget) return desiredDistance;

        if (desiredDistance <= MinObstructionCastDistance) return desiredDistance;

        if (obstructionLayers.value == 0) return desiredDistance;

        if (!TryGetNearestObstructionDistance(desiredDistance, out float nearestWallDistance))
            return desiredDistance;

        float minimumSafeDistance = Mathf.Min(GetMinimumObstructionDistance(), desiredDistance);
        float safeDistance = nearestWallDistance - Mathf.Max(0f, obstructionWallBuffer);
        float largestDistanceBeforeWall = Mathf.Max(0f, nearestWallDistance - MinObstructionCastDistance);
        float minimumDistance = preventPlayerModelIntersection
            ? minimumSafeDistance
            : Mathf.Min(minimumSafeDistance, largestDistanceBeforeWall);
        safeDistance = Mathf.Clamp(safeDistance, minimumDistance, desiredDistance);

        isLimited = safeDistance < desiredDistance;

        return safeDistance;
    }


    private float GetMinimumObstructionDistance()
    {
        float minimumDistance = Mathf.Max(0f, obstructionMinimumDistance);

        if (preventPlayerModelIntersection)
            minimumDistance = Mathf.Max(minimumDistance, obstructionPlayerClearanceDistance);

        return minimumDistance;
    }


    private bool TryGetNearestObstructionDistance(float desiredDistance, out float nearestWallDistance)
    {
        nearestWallDistance = 0f;

        Vector3 origin = GetObstructionOrigin();

        if (!TryGetObstructionDirection(origin, out Vector3 direction))
            return false;

        float castDistance = Mathf.Max(
            MinObstructionCastDistance,
            desiredDistance + Mathf.Max(0f, obstructionWallBuffer)
        );

        float probeRadius = Mathf.Max(0f, obstructionProbeRadius);
        int hitCount;

        if (probeRadius > 0f)
        {
            hitCount = Physics.SphereCastNonAlloc(
                origin,
                probeRadius,
                direction,
                obstructionHits,
                castDistance,
                obstructionLayers,
                QueryTriggerInteraction.Ignore
            );

            if (hitCount >= obstructionHits.Length)
            {
                RaycastHit[] allHits = Physics.SphereCastAll(
                    origin,
                    probeRadius,
                    direction,
                    castDistance,
                    obstructionLayers,
                    QueryTriggerInteraction.Ignore
                );

                return TryGetNearestValidObstructionHit(allHits, allHits.Length, out nearestWallDistance);
            }
        }
        else
        {
            hitCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                obstructionHits,
                castDistance,
                obstructionLayers,
                QueryTriggerInteraction.Ignore
            );

            if (hitCount >= obstructionHits.Length)
            {
                RaycastHit[] allHits = Physics.RaycastAll(
                    origin,
                    direction,
                    castDistance,
                    obstructionLayers,
                    QueryTriggerInteraction.Ignore
                );

                return TryGetNearestValidObstructionHit(allHits, allHits.Length, out nearestWallDistance);
            }
        }

        return TryGetNearestValidObstructionHit(obstructionHits, hitCount, out nearestWallDistance);
    }


    private Vector3 GetObstructionOrigin()
    {
        CinemachinePositionComposer composer = positionComposer;

        if (useTrackedOffsetForObstructionOrigin && composer)
            return playerTarget.TransformPoint(composer.TargetOffset);

        return playerTarget.position + Vector3.up * Mathf.Max(0f, obstructionFallbackTargetHeight);
    }


    private bool TryGetObstructionDirection(Vector3 origin, out Vector3 direction)
    {
        Transform cameraTransform = virtualCamera ? virtualCamera.transform : null;

        if (cameraTransform)
        {
            Vector3 toCamera = cameraTransform.position - origin;

            if (toCamera.sqrMagnitude > MinObstructionCastDistance * MinObstructionCastDistance)
            {
                direction = toCamera.normalized;
                return true;
            }

            Vector3 cameraBack = -cameraTransform.forward;

            if (cameraBack.sqrMagnitude > MinObstructionCastDistance * MinObstructionCastDistance)
            {
                direction = cameraBack.normalized;
                return true;
            }
        }

        direction = Vector3.zero;
        return false;
    }


    private bool TryGetNearestValidObstructionHit(RaycastHit[] hits, int hitCount, out float nearestWallDistance)
    {
        nearestWallDistance = 0f;

        if (hits == null || hitCount <= 0) return false;

        float nearestDistance = float.PositiveInfinity;
        bool foundHit = false;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];

            if (ShouldIgnoreObstructionHit(hit))
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                foundHit = true;
            }
        }

        if (!foundHit) return false;

        nearestWallDistance = nearestDistance;

        return true;
    }


    private bool ShouldIgnoreObstructionHit(RaycastHit hit)
    {
        Collider hitCollider = hit.collider;

        if (!hitCollider) return true;

        Transform hitTransform = hitCollider.transform;
        Transform playerRoot = playerState ? playerState.transform : playerTarget.root;

        if (playerRoot && (hitTransform == playerRoot || hitTransform.IsChildOf(playerRoot)))
            return true;

        Transform cameraTransform = virtualCamera ? virtualCamera.transform : null;

        if (cameraTransform && (hitTransform == cameraTransform || hitTransform.IsChildOf(cameraTransform)))
            return true;

        return false;
    }
}
