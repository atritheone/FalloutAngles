// imports
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;



// methods
public class CameraADSZoom : MonoBehaviour
{
    [Header("References")]
    // Reference to your existing distance controller (used only to freeze distance during ADS).
    [SerializeField] private CameraControlZoom cameraZoom;

    // Reference to the FOV controller (this is the real optical ADS).
    [SerializeField] private CameraControlFOV cameraFov;

    // Reference to the aim system that exposes the world-space aim target.
    [SerializeField] private PlayerAim playerAim;

    // ADS hold input action (right mouse button).
    [SerializeField] private InputActionReference ADSHold;

    // Cinemachine Camera we will control during ADS.
    [SerializeField] private CinemachineCamera virtualCamera;

    // The transform we want the camera to follow while ADS (optional).
    [SerializeField] private Transform aDSFollowTarget;

    // The driver that moves AdsFocus toward the aim target.
    [SerializeField] private ADSFocusDriver ADSFocusDriver;

    // Extension that hard-locks the camera aim onto the ADS target.
    [SerializeField] private ADSAimLockExtension aimLockExtension;

    // The orbit controller so ADS can rotate the camera at screen edges.
    [SerializeField] private CameraRigOrbit orbitController;
    
    // Player state used to check combat and weapon-in-hand conditions.
    [SerializeField] private PlayerState playerState;

    // Weapon controller used to check the currently equipped weapon.
    [SerializeField] private PlayerWeaponController playerWeaponController;

    [Header("ADS Settings")]
    // If true, we allow Follow swapping (NOT optical-only).
    [SerializeField] private bool swapFollowWhileAds = false;

    // If true, we freeze CameraDistance during ADS.
    [SerializeField] private bool freezeDistanceWhileAds = true;

    // The FOV we want during ADS.
    [SerializeField] private float aDSFOV = 35.0f;

    // Clamp for safety.
    [SerializeField] private float aDSMinFov = 20.0f;

    // Clamp for safety.
    [SerializeField] private float aDSMaxFov = 55.0f;

    // How fast the locked aim point follows the real mouse target.
    [SerializeField] private float aDSAimFollowSpeed = 6.0f;

    [Header("Sniper ADS Settings")]
    // The weapon name that should trigger stronger ADS zoom when equipped.
    [SerializeField] private string sniperWeaponName = "Sniper Rifle";

    // Stronger FOV used while ADS with sniper + weapon in hand + combat mode.
    [SerializeField] private float sniperADSFOV = 12.0f;

    // Clamp for sniper ADS safety.
    [SerializeField] private float sniperADSMinFov = 6.0f;

    // Clamp for sniper ADS safety.
    [SerializeField] private float sniperADSMaxFov = 35.0f;

    [Header("ADS Edge Panning")]
    // Pixel thickness of the edge zone that triggers panning.
    [SerializeField] private float edgePanPixels = 24.0f;

    // Degrees per second to rotate while pushing into an edge (yaw).
    [SerializeField] private float edgeYawDegreesPerSecond = 140.0f;

    // Extra yaw speed applied only near full-strength ADS edge push.
    [SerializeField] private float aDSEdgeYawHighPushSpeedMultiplier = 1.0f;

    // Push strength where the high-push ADS yaw multiplier starts blending in.
    [SerializeField, Range(0.0f, 1.0f)] private float aDSEdgeYawHighPushThreshold = 0.85f;

    // Higher values make the high-push ADS yaw boost kick in later/sharper.
    [SerializeField] private float aDSEdgeYawHighPushResponseExponent = 2.0f;

    // Degrees per second to rotate while pushing into an edge (pitch).
    [SerializeField] private float edgePitchDegreesPerSecond = 110.0f;

    // If true, edge panning is enabled during ADS.
    [SerializeField] private bool enableEdgePanWhileAds = true;

    // How long ADS edge panning takes to ramp in/out.
    [SerializeField] private float aDSEdgePanSmoothingTime = 0.12f;

    // Mouse speed toward an edge that counts as a full-strength ADS edge pan.
    [SerializeField] private float aDSEdgePanFullPushMousePixelsPerSecond = 1200.0f;

    // Mouse speed below this does not add aggressive ADS edge-pan intent.
    [SerializeField] private float aDSEdgePanMinPushMousePixelsPerSecond = 450.0f;

    // Higher values make normal mouse movement contribute less while preserving hard whips.
    [SerializeField] private float aDSEdgePanMousePushResponseExponent = 2.0f;

    // Position-based pan contribution when the mouse is resting on the screen edge.
    [SerializeField, Range(0.0f, 1.0f)] private float aDSEdgePanPositionPushAtScreenEdge = 0.40f;

    // Pan contribution from whipping the mouse into the edge zone.
    [SerializeField, Range(0.0f, 1.0f)] private float aDSEdgePanEntryPushWeight = 0.45f;

    // Pan contribution from continuing to push the mouse toward the edge.
    [SerializeField, Range(0.0f, 1.0f)] private float aDSEdgePanOngoingPushWeight = 0.55f;

    // How long the initial edge-entry push takes to fade.
    [SerializeField] private float aDSEdgePanEntryPushDecayTime = 0.20f;

    [Header("ADS Edge Pitch Panning")]
    // Mouse speed toward the top/bottom edge that counts as a full-strength ADS pitch pan.
    [SerializeField] private float aDSEdgePitchPanFullPushMousePixelsPerSecond = 1200.0f;

    // Mouse speed below this does not add aggressive ADS pitch-pan intent.
    [SerializeField] private float aDSEdgePitchPanMinPushMousePixelsPerSecond = 450.0f;

    // Higher values make normal vertical mouse movement contribute less while preserving hard pitch pushes.
    [SerializeField] private float aDSEdgePitchPanMousePushResponseExponent = 2.0f;

    // Position-based pitch contribution when the mouse is resting on the top/bottom screen edge.
    [SerializeField, Range(0.0f, 1.0f)] private float aDSEdgePitchPanPositionPushAtScreenEdge = 0.40f;

    // Pitch contribution from whipping the mouse into the top/bottom edge zone.
    [SerializeField, Range(0.0f, 1.0f)] private float aDSEdgePitchPanEntryPushWeight = 0.45f;

    // Pitch contribution from continuing to push the mouse toward the top/bottom edge.
    [SerializeField, Range(0.0f, 1.0f)] private float aDSEdgePitchPanOngoingPushWeight = 0.55f;

    // How long the initial pitch edge-entry push takes to fade.
    [SerializeField] private float aDSEdgePitchPanEntryPushDecayTime = 0.20f;


    [Header("Sniper ADS Edge Panning")]
    // If true, sniper ADS uses dedicated edge panning settings.
    [SerializeField] private bool enableSniperEdgePanOverride = true;

    // Pixel thickness of the sniper edge zone that triggers panning.
    [SerializeField] private float sniperEdgePanPixels = 32.0f;

    // Degrees per second to rotate while pushing into an edge (yaw) during sniper ADS.
    [SerializeField] private float sniperEdgeYawDegreesPerSecond = 90.0f;

    // Degrees per second to rotate while pushing into an edge (pitch) during sniper ADS.
    [SerializeField] private float sniperEdgePitchDegreesPerSecond = 70.0f;

    // Tracks whether ADS is active.
    private bool isAdsActive;

    // Tracks whether the hold interaction has completed.
    private bool isAdsHoldActive;

    // Cached original Follow.
    private Transform originalFollow;

    // Cached original LookAt.
    private Transform originalLookAt;

    // The locked aim transform.
    private Transform adsLockedAim;

    // Smoothed edge-pan input used while ADS is active.
    private Vector2 smoothedADSEdgePanPush;

    // SmoothDamp velocity for ADS edge panning.
    private Vector2 aDSEdgePanPushVelocity;

    // Signed impulse captured when the mouse first enters an edge pan zone.
    private Vector2 aDSEdgePanEntryPush;

    // Last edge direction used to detect a fresh edge-zone entry.
    private Vector2 lastADSEdgePanDirection;

    public bool IsAdsActive => isAdsActive;


    private void Awake()
    {
        AutoWireCombatReferences();
    }



    private void OnEnable()
    {
        // Enable the ADS input action.
        if (ADSHold && ADSHold.action != null)
        {
            ADSHold.action.Enable();

            ADSHold.action.performed += OnAdsHoldPerformed;
            ADSHold.action.canceled += OnAdsHoldCanceled;
        }
    }


    private void OnDisable()
    {
        // Disable the ADS input action.
        if (ADSHold && ADSHold.action != null)
        {
            ADSHold.action.performed -= OnAdsHoldPerformed;
            ADSHold.action.canceled -= OnAdsHoldCanceled;

            ADSHold.action.Disable();
        }
    }


    private void Update()
    {
        InputAction holdAction = ADSHold ? ADSHold.action : null;

        // Stop if required references are missing.
        if (!playerAim || holdAction == null)
            return;

        // Stop if no virtual camera.
        if (!virtualCamera)
            return;

        // ADS held (after the hold interaction completes).
        if (isAdsHoldActive)
        {
            if (!isAdsActive)
                BeginAds();

            UpdateAds();

            return;
        }

        // ADS released.
        if (isAdsActive)
            EndAds();
    }

    
    private void OnAdsHoldPerformed(InputAction.CallbackContext context)
    {
        isAdsHoldActive = true;
    }


    private void OnAdsHoldCanceled(InputAction.CallbackContext context)
    {
        isAdsHoldActive = false;
    }



    private void BeginAds()
    {
        // Mark ADS active.
        isAdsActive = true;

        // Cache Follow.
        originalFollow = virtualCamera.Follow;

        // Cache LookAt.
        originalLookAt = virtualCamera.LookAt;

        // Create locked aim if missing.
        if (!adsLockedAim)
        {
            GameObject lockObj = new GameObject("AdsLockedAimPoint");

            adsLockedAim = lockObj.transform;
        }

        // Snapshot current aim target.
        Transform aimTarget = playerAim ? playerAim.AimTarget : null;
        if (aimTarget)
            adsLockedAim.position = aimTarget.position;

        // Feed and enable ADS mode on focus driver.
        if (ADSFocusDriver)
        {
            ADSFocusDriver.SetRuntimeAimTarget(adsLockedAim);
            ADSFocusDriver.SetAdsActive(true);
        }

        // Optional Follow swap.
        if (swapFollowWhileAds && aDSFollowTarget)
            virtualCamera.Follow = aDSFollowTarget;

        // LookAt locked point.
        virtualCamera.LookAt = adsLockedAim;

        // Auto-find aim lock extension if not assigned.
        if (!aimLockExtension && virtualCamera)
            aimLockExtension = virtualCamera.GetComponent<ADSAimLockExtension>();
        
        // Auto-find orbit controller if not assigned.
        if (!orbitController)
            orbitController = FindAnyObjectByType<CameraRigOrbit>();

        // Enable hard aim lock.
        if (aimLockExtension)
        {
            aimLockExtension.SetAimTarget(adsLockedAim);

            aimLockExtension.SetActive(true);
        }

        // Freeze distance.
        if (freezeDistanceWhileAds && cameraZoom)
            cameraZoom.BeginZoomOverride(cameraZoom.GetCurrentDistance());

        // Apply optical zoom.
        float clampedFov = GetCurrentAdsFov();

        if (cameraFov)
            cameraFov.BeginFovOverride(clampedFov);
    }


    private void EndAds()
    {
        // Mark ADS inactive.
        isAdsActive = false;

        // Clear edge-pan smoothing so ADS cannot resume with stale pan velocity.
        ResetADSEdgePanSmoothing();

        // Clear runtime aim override.
        if (ADSFocusDriver)
            ADSFocusDriver.SetRuntimeAimTarget(null);

        // Disable ADS mode on focus driver.
        if (ADSFocusDriver)
            ADSFocusDriver.SetAdsActive(false);

        // Disable hard aim lock.
        if (aimLockExtension)
        {
            aimLockExtension.SetActive(false);

            aimLockExtension.SetAimTarget(null);
        }

        // Restore Follow.
        virtualCamera.Follow = originalFollow;

        // Restore LookAt.
        virtualCamera.LookAt = originalLookAt;

        // Unfreeze distance.
        if (freezeDistanceWhileAds && cameraZoom)
            cameraZoom.EndZoomOverride();

        // Restore FOV.
        if (cameraFov)
            cameraFov.EndFovOverride();
    }


    private void UpdateAds()
    {
        Transform aimTarget = playerAim ? playerAim.AimTarget : null;

        // Stop if no live aim target.
        if (!aimTarget) return;

        // Stop if no locked transform.
        if (!adsLockedAim) return;

        float dt = Time.deltaTime;

        // Move locked aim toward real aim.
        adsLockedAim.position = Vector3.Lerp(
            adsLockedAim.position,
            aimTarget.position,
            dt * aDSAimFollowSpeed
        );
        
        // Apply edge panning while ADS so we can acquire targets outside the zoomed window.
        ApplyEdgePanWhileAds();

        // Maintain optical zoom.
        float clampedFov = GetCurrentAdsFov();

        if (cameraFov)
            cameraFov.SetOverrideFov(clampedFov);
    }
    
    
    private void ApplyEdgePanWhileAds()
    {
        // Stop if edge pan is disabled.
        if (!enableEdgePanWhileAds)
        {
            ResetADSEdgePanSmoothing();
            return;
        }

        // Stop if we don't have an orbit controller to rotate.
        if (!orbitController)
        {
            ResetADSEdgePanSmoothing();
            return;
        }

        // Stop if there is no mouse device detected.
        if (Mouse.current == null)
        {
            ResetADSEdgePanSmoothing();
            return;
        }

        // Read the mouse position in screen pixels.
        Vector2 mousePos = Mouse.current.position.ReadValue();

        // Read physical mouse movement so pan strength can respond to how hard the player pushes.
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();
        
        // Pick edge-pan settings, with optional sniper override.
        bool useSniperEdgePan = enableSniperEdgePanOverride && ShouldUseSniperAdsFov();

        float activeEdgePanPixels = useSniperEdgePan ? sniperEdgePanPixels : edgePanPixels;

        activeEdgePanPixels = Mathf.Max(0f, activeEdgePanPixels);

        float activeEdgeYawDegreesPerSecond = useSniperEdgePan
            ? sniperEdgeYawDegreesPerSecond
            : edgeYawDegreesPerSecond;

        float activeEdgePitchDegreesPerSecond = useSniperEdgePan
            ? sniperEdgePitchDegreesPerSecond
            : edgePitchDegreesPerSecond;

        // Cache screen size.
        float w = Screen.width;

        float h = Screen.height;

        // Stop if screen size is invalid.
        if (w <= 1f || h <= 1f)
        {
            ResetADSEdgePanSmoothing();
            return;
        }

        // Stop if the edge zone is disabled by size.
        if (activeEdgePanPixels <= 0f)
        {
            ResetADSEdgePanSmoothing();
            return;
        }

        // Compute how hard we are pushing into each edge (-1 .. 0 .. +1).
        Vector2 targetPush = Vector2.zero;

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float fullPushPixelsPerSecond = Mathf.Max(1f, aDSEdgePanFullPushMousePixelsPerSecond);
        float minPushPixelsPerSecond = Mathf.Max(0f, aDSEdgePanMinPushMousePixelsPerSecond);
        float mousePushResponseExponent = Mathf.Max(0.01f, aDSEdgePanMousePushResponseExponent);
        float positionPushAtScreenEdge = Mathf.Clamp01(aDSEdgePanPositionPushAtScreenEdge);
        float entryPushWeight = Mathf.Clamp01(aDSEdgePanEntryPushWeight);
        float ongoingPushWeight = Mathf.Clamp01(aDSEdgePanOngoingPushWeight);
        float pitchFullPushPixelsPerSecond = Mathf.Max(1f, aDSEdgePitchPanFullPushMousePixelsPerSecond);
        float pitchMinPushPixelsPerSecond = Mathf.Max(0f, aDSEdgePitchPanMinPushMousePixelsPerSecond);
        float pitchMousePushResponseExponent = Mathf.Max(0.01f, aDSEdgePitchPanMousePushResponseExponent);
        float pitchPositionPushAtScreenEdge = Mathf.Clamp01(aDSEdgePitchPanPositionPushAtScreenEdge);
        float pitchEntryPushWeight = Mathf.Clamp01(aDSEdgePitchPanEntryPushWeight);
        float pitchOngoingPushWeight = Mathf.Clamp01(aDSEdgePitchPanOngoingPushWeight);

        Vector2 edgeDirection = Vector2.zero;
        Vector2 positionPush = Vector2.zero;
        Vector2 ongoingMousePush = Vector2.zero;

        // Left edge.
        if (mousePos.x <= activeEdgePanPixels)
        {
            float edgeDepth = Mathf.Clamp01((activeEdgePanPixels - mousePos.x) / activeEdgePanPixels);
            float mouseSpeedTowardEdge = Mathf.Max(0f, -mouseDelta.x / dt);
            edgeDirection.x = -1f;
            positionPush.x = edgeDepth;
            ongoingMousePush.x = GetMousePushStrength(mouseSpeedTowardEdge, minPushPixelsPerSecond, fullPushPixelsPerSecond, mousePushResponseExponent);
        }

        // Right edge.
        else if (mousePos.x >= w - activeEdgePanPixels)
        {
            float rightEdgeStart = w - activeEdgePanPixels;
            float rightEdgeSpan = Mathf.Max(1f, (w - 1f) - rightEdgeStart);
            float edgeDepth = Mathf.Clamp01((mousePos.x - rightEdgeStart) / rightEdgeSpan);
            float mouseSpeedTowardEdge = Mathf.Max(0f, mouseDelta.x / dt);
            edgeDirection.x = 1f;
            positionPush.x = edgeDepth;
            ongoingMousePush.x = GetMousePushStrength(mouseSpeedTowardEdge, minPushPixelsPerSecond, fullPushPixelsPerSecond, mousePushResponseExponent);
        }

        // Bottom edge.
        if (mousePos.y <= activeEdgePanPixels)
        {
            float edgeDepth = Mathf.Clamp01((activeEdgePanPixels - mousePos.y) / activeEdgePanPixels);
            float mouseSpeedTowardEdge = Mathf.Max(0f, -mouseDelta.y / dt);
            edgeDirection.y = -1f;
            positionPush.y = edgeDepth;
            ongoingMousePush.y = GetMousePushStrength(mouseSpeedTowardEdge, pitchMinPushPixelsPerSecond, pitchFullPushPixelsPerSecond, pitchMousePushResponseExponent);
        }

        // Top edge.
        else if (mousePos.y >= h - activeEdgePanPixels)
        {
            float topEdgeStart = h - activeEdgePanPixels;
            float topEdgeSpan = Mathf.Max(1f, (h - 1f) - topEdgeStart);
            float edgeDepth = Mathf.Clamp01((mousePos.y - topEdgeStart) / topEdgeSpan);
            float mouseSpeedTowardEdge = Mathf.Max(0f, mouseDelta.y / dt);
            edgeDirection.y = 1f;
            positionPush.y = edgeDepth;
            ongoingMousePush.y = GetMousePushStrength(mouseSpeedTowardEdge, pitchMinPushPixelsPerSecond, pitchFullPushPixelsPerSecond, pitchMousePushResponseExponent);
        }

        UpdateEdgePanEntryPush(
            edgeDirection,
            ongoingMousePush,
            ref aDSEdgePanEntryPush,
            ref lastADSEdgePanDirection,
            aDSEdgePanEntryPushDecayTime,
            aDSEdgePitchPanEntryPushDecayTime,
            dt
        );

        targetPush.x = GetSignedEdgePanPush(
            edgeDirection.x,
            positionPush.x,
            Mathf.Abs(aDSEdgePanEntryPush.x),
            ongoingMousePush.x,
            positionPushAtScreenEdge,
            entryPushWeight,
            ongoingPushWeight
        );

        targetPush.y = GetSignedEdgePanPush(
            edgeDirection.y,
            positionPush.y,
            Mathf.Abs(aDSEdgePanEntryPush.y),
            ongoingMousePush.y,
            pitchPositionPushAtScreenEdge,
            pitchEntryPushWeight,
            pitchOngoingPushWeight
        );

        // Smooth the raw edge input so ADS panning eases in/out.
        float smoothTime = Mathf.Max(0f, aDSEdgePanSmoothingTime);

        smoothedADSEdgePanPush = Vector2.SmoothDamp(
            smoothedADSEdgePanPush,
            targetPush,
            ref aDSEdgePanPushVelocity,
            smoothTime,
            Mathf.Infinity,
            dt
        );

        // Stop once the smoothed input has settled back to zero.
        if (smoothedADSEdgePanPush.sqrMagnitude < 0.000001f)
        {
            smoothedADSEdgePanPush = Vector2.zero;
            aDSEdgePanPushVelocity = Vector2.zero;
            return;
        }

        // Convert push into degrees this frame (time-based, not input-delta based).
        float yawHighPushMultiplier = GetHighPushSpeedMultiplier(
            Mathf.Abs(smoothedADSEdgePanPush.x),
            aDSEdgeYawHighPushThreshold,
            aDSEdgeYawHighPushSpeedMultiplier,
            aDSEdgeYawHighPushResponseExponent
        );

        float yawDegrees = smoothedADSEdgePanPush.x * activeEdgeYawDegreesPerSecond * yawHighPushMultiplier * dt;

        float pitchDegrees = smoothedADSEdgePanPush.y * activeEdgePitchDegreesPerSecond * dt;

        // Apply forced orbit rotation (works even while ADS is held).
        orbitController.AddOrbitDegrees(yawDegrees, pitchDegrees);
    }


    private static float GetMousePushStrength(
        float mouseSpeedTowardEdge,
        float minPushPixelsPerSecond,
        float fullPushPixelsPerSecond,
        float responseExponent)
    {
        float fullSpeed = Mathf.Max(minPushPixelsPerSecond + 1f, fullPushPixelsPerSecond);
        float normalizedPush = Mathf.InverseLerp(minPushPixelsPerSecond, fullSpeed, mouseSpeedTowardEdge);

        return Mathf.Pow(normalizedPush, Mathf.Max(0.01f, responseExponent));
    }


    private static float GetHighPushSpeedMultiplier(
        float pushStrength,
        float boostThreshold,
        float maxMultiplier,
        float responseExponent)
    {
        float multiplier = Mathf.Max(1f, maxMultiplier);
        float threshold = Mathf.Clamp01(boostThreshold);
        float boostT = Mathf.InverseLerp(threshold, 1f, Mathf.Clamp01(pushStrength));
        boostT = Mathf.Pow(boostT, Mathf.Max(0.01f, responseExponent));

        return Mathf.Lerp(1f, multiplier, boostT);
    }


    private static float GetSignedEdgePanPush(
        float direction,
        float edgeDepth,
        float entryPush,
        float ongoingMousePush,
        float positionPushAtScreenEdge,
        float entryPushWeight,
        float ongoingPushWeight)
    {
        if (Mathf.Approximately(direction, 0f))
            return 0f;

        float positionPush = Mathf.Clamp01(edgeDepth) * Mathf.Clamp01(positionPushAtScreenEdge);
        float weightedEntryPush = Mathf.Clamp01(entryPush) * Mathf.Clamp01(entryPushWeight);
        float weightedOngoingPush = Mathf.Clamp01(ongoingMousePush) * Mathf.Clamp01(ongoingPushWeight);
        float pushStrength = Mathf.Clamp01(positionPush + weightedEntryPush + weightedOngoingPush);

        return direction * pushStrength;
    }


    private static void UpdateEdgePanEntryPush(
        Vector2 edgeDirection,
        Vector2 ongoingMousePush,
        ref Vector2 entryPush,
        ref Vector2 lastEdgeDirection,
        float decayTimeX,
        float decayTimeY,
        float dt)
    {
        float entryX = entryPush.x;
        float entryY = entryPush.y;
        float lastDirectionX = lastEdgeDirection.x;
        float lastDirectionY = lastEdgeDirection.y;

        UpdateEdgePanEntryAxis(
            edgeDirection.x,
            ongoingMousePush.x,
            ref entryX,
            ref lastDirectionX,
            decayTimeX,
            dt
        );

        UpdateEdgePanEntryAxis(
            edgeDirection.y,
            ongoingMousePush.y,
            ref entryY,
            ref lastDirectionY,
            decayTimeY,
            dt
        );

        entryPush = new Vector2(entryX, entryY);
        lastEdgeDirection = new Vector2(lastDirectionX, lastDirectionY);
    }


    private static void UpdateEdgePanEntryAxis(
        float direction,
        float ongoingMousePush,
        ref float entryPush,
        ref float lastDirection,
        float decayTime,
        float dt)
    {
        if (Mathf.Approximately(direction, 0f))
        {
            entryPush = 0f;
            lastDirection = 0f;
            return;
        }

        if (!Mathf.Approximately(direction, lastDirection))
        {
            entryPush = direction * ongoingMousePush;
            lastDirection = direction;
            return;
        }

        float decayRate = 1f / Mathf.Max(0.0001f, decayTime);
        entryPush = Mathf.MoveTowards(entryPush, 0f, decayRate * dt);
    }


    private void ResetADSEdgePanSmoothing()
    {
        smoothedADSEdgePanPush = Vector2.zero;

        aDSEdgePanPushVelocity = Vector2.zero;

        aDSEdgePanEntryPush = Vector2.zero;

        lastADSEdgePanDirection = Vector2.zero;
    }


    private float GetCurrentAdsFov()
    {
        // Use stronger sniper zoom only when all required gameplay conditions are true.
        if (ShouldUseSniperAdsFov())
            return Mathf.Clamp(sniperADSFOV, sniperADSMinFov, sniperADSMaxFov);

        // Fall back to the standard ADS zoom profile.
        return Mathf.Clamp(aDSFOV, aDSMinFov, aDSMaxFov);
    }


    private bool ShouldUseSniperAdsFov()
    {
        // Ensure references are available even when not wired in inspector.
        AutoWireCombatReferences();

        // Combat mode and weapon-in-hand are required for sniper zoom.
        if (!playerState || !playerState.GetCombatMode() || !playerState.GetWeaponInHand())
            return false;

        // We must know the currently equipped weapon.
        if (!playerWeaponController) return false;

        PlayerWeaponController.WeaponEntry currentWeapon = playerWeaponController.GetCurrentWeapon();

        // Stop if weapon data is unavailable.
        if (currentWeapon == null || string.IsNullOrWhiteSpace(currentWeapon.WeaponName))
            return false;

        // Match by configured sniper weapon name.
        return string.Equals(currentWeapon.WeaponName, sniperWeaponName, StringComparison.OrdinalIgnoreCase);
    }


    private void AutoWireCombatReferences()
    {
        // Auto-find player state from known references when not assigned.
        if (!playerState)
        {
            if (playerAim)
                playerState = playerAim.GetComponentInParent<PlayerState>();

            if (!playerState && virtualCamera)
                playerState = virtualCamera.GetComponentInParent<PlayerState>();

            if (!playerState)
                playerState = FindAnyObjectByType<PlayerState>();
        }

        // Auto-find weapon controller when not assigned.
        if (!playerWeaponController)
        {
            if (playerAim)
                playerWeaponController = playerAim.GetComponentInParent<PlayerWeaponController>();

            if (!playerWeaponController && playerState)
                playerWeaponController = playerState.GetComponentInParent<PlayerWeaponController>();

            if (!playerWeaponController)
                playerWeaponController = FindAnyObjectByType<PlayerWeaponController>();
        }
    }
}
