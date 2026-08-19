// imports
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Cinemachine;
using UnityEngine.Serialization;



// methods
[DefaultExecutionOrder(-50)]
public class CameraRigOrbit : MonoBehaviour
{
    private const float MinLookDeltaSqr = 0.000001f;

    [Header("References")]
    // The player we orbit around.
    [SerializeField] private Transform playerTarget;

    // Player state that defines whether we are in combat mode.
    [SerializeField] private PlayerState playerState;

    // Your Cinemachine Camera (the same object that has CinemachinePositionComposer on it).
    [SerializeField] private CinemachineCamera virtualCamera;

    // The yaw pivot that will rotate around the player.
    [SerializeField] private Transform yawPivot;

    // The pitch pivot that holds pitch rotation.
    [SerializeField] private Transform pitchPivot;

    [Header("Input")]
    // Hold action (middle mouse) from your Input Actions.
    [SerializeField] private InputActionReference cameraRotateHold;

    // Mouse delta action from your Input Actions.
    [SerializeField] private InputActionReference cameraLookDelta;

    // ADS hold action (right mouse) so orbit is disabled while aiming.
    [SerializeField] private InputActionReference aDSHold;

    [Header("Orbit Settings")]
    // How many degrees of yaw we add per mouse-delta unit.
    [SerializeField] private float yawSpeed = 0.20f;

    // How many degrees of pitch we add per mouse-delta unit.
    [SerializeField] private float pitchSpeed = 0.20f;

    // If true, moving mouse up pitches the camera downward (typical “invert Y” toggle).
    [SerializeField] private bool invertPitch = true;

    // The pitch angle we start at when the game begins.
    [SerializeField] private float startPitch = 60.0f;

    // The minimum pitch angle (more top-down).
    [SerializeField] private float minPitch = 35.0f;

    // The maximum pitch angle (more side-on).
    [SerializeField] private float maxPitch = 75.0f;

    [Header("Orbit Behavior")]
    // In non-combat free-look, should we still block camera rotation while ADS is held?
    // (Default: false, to behave like a typical third-person camera.)
    [SerializeField] private bool blockFreeLookWhileAds = false;

    [Header("Combat Edge Panning")]
    // If true, combat mode can rotate the camera by pushing the mouse into screen edges while not ADS.
    [SerializeField] private bool enableEdgePanWhileCombatMode = true;

    // Pixel thickness of the edge zone that triggers combat-mode panning.
    [SerializeField] private float combatEdgePanPixels = 24.0f;

    // Degrees per second to rotate while pushing into a horizontal edge in combat mode.
    [SerializeField] private float combatEdgeYawDegreesPerSecond = 140.0f;

    // Extra yaw speed applied only near full-strength edge push.
    [SerializeField] private float combatEdgeYawHighPushSpeedMultiplier = 1.0f;

    // Push strength where the high-push yaw multiplier starts blending in.
    [SerializeField, Range(0.0f, 1.0f)] private float combatEdgeYawHighPushThreshold = 0.85f;

    // Higher values make the high-push yaw boost kick in later/sharper.
    [SerializeField] private float combatEdgeYawHighPushResponseExponent = 2.0f;

    // Degrees per second to rotate while pushing into a vertical edge in combat mode.
    [SerializeField] private float combatEdgePitchDegreesPerSecond = 110.0f;

    // How long combat edge panning takes to ramp in/out.
    [SerializeField] private float combatEdgePanSmoothingTime = 0.12f;

    // Mouse speed toward an edge that counts as a full-strength combat edge pan.
    [SerializeField] private float combatEdgePanFullPushMousePixelsPerSecond = 1200.0f;

    // Mouse speed below this does not add aggressive combat edge-pan intent.
    [SerializeField] private float combatEdgePanMinPushMousePixelsPerSecond = 450.0f;

    // Higher values make normal mouse movement contribute less while preserving hard whips.
    [SerializeField] private float combatEdgePanMousePushResponseExponent = 2.0f;

    // Position-based pan contribution when the mouse is resting on the screen edge.
    [SerializeField, Range(0.0f, 1.0f)] private float combatEdgePanPositionPushAtScreenEdge = 0.40f;

    // Pan contribution from whipping the mouse into the edge zone.
    [SerializeField, Range(0.0f, 1.0f)] private float combatEdgePanEntryPushWeight = 0.45f;

    // Pan contribution from continuing to push the mouse toward the edge.
    [SerializeField, Range(0.0f, 1.0f)] private float combatEdgePanOngoingPushWeight = 0.55f;

    // How long the initial edge-entry push takes to fade.
    [SerializeField] private float combatEdgePanEntryPushDecayTime = 0.20f;

    [Header("Combat Edge Pitch Panning")]
    // Mouse speed toward the top/bottom edge that counts as a full-strength combat pitch pan.
    [SerializeField] private float combatEdgePitchPanFullPushMousePixelsPerSecond = 1200.0f;

    // Mouse speed below this does not add aggressive combat pitch-pan intent.
    [SerializeField] private float combatEdgePitchPanMinPushMousePixelsPerSecond = 450.0f;

    // Higher values make normal vertical mouse movement contribute less while preserving hard pitch pushes.
    [SerializeField] private float combatEdgePitchPanMousePushResponseExponent = 2.0f;

    // Position-based pitch contribution when the mouse is resting on the top/bottom screen edge.
    [SerializeField, Range(0.0f, 1.0f)] private float combatEdgePitchPanPositionPushAtScreenEdge = 0.40f;

    // Pitch contribution from whipping the mouse into the top/bottom edge zone.
    [SerializeField, Range(0.0f, 1.0f)] private float combatEdgePitchPanEntryPushWeight = 0.45f;

    // Pitch contribution from continuing to push the mouse toward the top/bottom edge.
    [SerializeField, Range(0.0f, 1.0f)] private float combatEdgePitchPanOngoingPushWeight = 0.55f;

    // How long the initial pitch edge-entry push takes to fade.
    [SerializeField] private float combatEdgePitchPanEntryPushDecayTime = 0.20f;


    [Header("Combat Crosshair Freeze")]
    // When combat MMB orbit ends, restore the hardware cursor to where the crosshair was frozen.
    [SerializeField] private bool restoreCursorPositionAfterCombatOrbit = true;

    [Header("Reset")]
    // Time window (seconds) allowed between clicks for a double-click.
    [SerializeField] private float doubleClickTime = 0.25f;

    // How long (seconds) the smooth return takes.
    [SerializeField] private float resetSmoothTime = 0.35f;

    [Header("Cursor")]
    // When combat mode is false, lock the cursor to the center (Fallout 3 style).
    [SerializeField] private bool lockCursorWhenCombatModeIsFalse = true;

    // When combat mode is true, hide the cursor (do not lock it).
    [FormerlySerializedAs("lockCursorWhenCombatModeIsTrue")]
    [SerializeField] private bool hideCursorWhenCombatModeIsTrue = true;

    // If true, unlock cursor when the application loses focus.
    [SerializeField] private bool unlockCursorWhenApplicationUnfocused = true;

    // Track what cursor state we last applied to avoid re-setting it every frame.
    private bool cursorLockedApplied;

    // Track what cursor visibility state we last applied to avoid re-setting it every frame.
    private bool cursorHiddenApplied;

    // Whether we have applied a tracked cursor state since input was enabled.
    private bool hasAppliedCursorState;

    // Time of the last middle mouse click.
    private float lastRotateClickTime;

    // Yaw we want to reset to (player facing direction at reset time).
    private float resetTargetYaw;

    // The yaw we have accumulated so far.
    private float currentYaw;

    // The pitch we have accumulated so far.
    private float currentPitch;

    // Whether we are currently returning to default orbit.
    private bool isResetting;

    // Whether orbit input should be processed.
    private bool inputEnabled = true;

    // Time spent resetting so far.
    private float resetTimer;

    // Yaw at the moment reset begins.
    private float resetStartYaw;

    // Pitch at the moment reset begins.
    private float resetStartPitch;

    // Pitch we want to reset to (startPitch clamped).
    private float resetTargetPitch;

    // Smoothed edge-pan input used only for non-ADS combat camera panning.
    private Vector2 smoothedCombatEdgePanPush;

    // SmoothDamp velocity for combat edge panning.
    private Vector2 combatEdgePanPushVelocity;

    // Signed impulse captured when the mouse first enters an edge pan zone.
    private Vector2 combatEdgePanEntryPush;

    // Last edge direction used to detect a fresh edge-zone entry.
    private Vector2 lastCombatEdgePanDirection;

    // True while combat MMB orbit has captured the crosshair screen point.
    private bool isCombatOrbitCrosshairFrozen;

    // Screen point captured when combat MMB orbit started.
    private Vector2 combatOrbitFrozenCrosshairScreenPoint;

    // Expose the yaw pivot so movement can use it as a stable yaw-only reference.
    public Transform YawPivot => yawPivot;

    // Expose current yaw so movement can build a yaw-only basis even if pivots are missing.
    public float CurrentYaw => currentYaw;

    // Expose combat mode state for other scripts (optional).
    public bool CombatMode => GetCombatMode();

    // Expose whether combat MMB orbit is currently hiding/freezing the crosshair.
    public bool IsCombatOrbitCrosshairFrozen => isCombatOrbitCrosshairFrozen;

    // Expose the actual manual combat orbit hold state so aim/body systems can lock immediately.
    public bool IsCombatManualOrbitHeld => inputEnabled && !UI.ConsoleController.IsOpen && GetCombatMode() && !IsAdsHeld() && IsRotateHeld();


    public bool TryGetCombatOrbitFrozenCrosshairScreenPoint(out Vector2 screenPoint)
    {
        if (isCombatOrbitCrosshairFrozen && GetCombatMode())
        {
            screenPoint = combatOrbitFrozenCrosshairScreenPoint;
            return true;
        }

        screenPoint = Vector2.zero;
        return false;
    }


    public bool TryGetCameraForward(out Vector3 direction)
    {
        if (pitchPivot)
        {
            Vector3 pivotForward = pitchPivot.forward;
            if (pivotForward.sqrMagnitude > MinLookDeltaSqr)
            {
                direction = pivotForward.normalized;
                return true;
            }
        }

        Transform cameraTransform = virtualCamera ? virtualCamera.transform : null;
        if (cameraTransform)
        {
            Vector3 cameraForward = cameraTransform.forward;
            if (cameraForward.sqrMagnitude > MinLookDeltaSqr)
            {
                direction = cameraForward.normalized;
                return true;
            }
        }

        direction = Vector3.zero;
        return false;
    }



    public bool IsOrbitBusy()
    {
        // Return true while combat orbit is finishing cursor/crosshair restoration.
        if (isCombatOrbitCrosshairFrozen) return true;

        // Return true if we are resetting.
        if (isResetting) return true;

        // Return true if the rotate button is held.
        if (IsRotateHeld()) return true;

        // Return true if combat edge panning is currently driving orbit.
        if (IsCombatEdgePanActive()) return true;

        // Otherwise we are not busy.
        return false;
    }



    private void Awake()
    {
        // Stop if we don't have a player target.
        if (!playerTarget) return;

        // Auto-wire player state if not assigned.
        if (!playerState)
        {
            playerState = playerTarget.GetComponentInParent<PlayerState>();
        }

        // If no yaw pivot was assigned, create one.
        if (!yawPivot)
        {
            // Create a new GameObject to act as the yaw pivot.
            GameObject yawObj = new GameObject("CameraYawPivot");

            // Cache its transform.
            yawPivot = yawObj.transform;

            // Put it at the player's position.
            yawPivot.position = playerTarget.position;
        }

        // If no pitch pivot was assigned, create one as a child of yaw pivot.
        if (!pitchPivot)
        {
            // Create a new GameObject to act as the pitch pivot.
            GameObject pitchObj = new GameObject("CameraPitchPivot");

            // Cache its transform.
            pitchPivot = pitchObj.transform;

            // Parent it to the yaw pivot so it inherits yaw.
            pitchPivot.SetParent(yawPivot, false);

            // Zero it out locally.
            pitchPivot.localPosition = Vector3.zero;
        }

        // If we have a Cinemachine camera, parent it under the pitch pivot.
        if (virtualCamera)
        {
            // Parent the virtual camera under the pitch pivot so it inherits yaw + pitch.
            virtualCamera.transform.SetParent(pitchPivot, true);

            // Force the vcam to have no local rotation so Cinemachine stays "no rotation component".
            virtualCamera.transform.localRotation = Quaternion.identity;
        }

        // Start yaw from current pivot rotation.
        currentYaw = yawPivot.eulerAngles.y;

        // Start pitch from an explicit value (matches your intended default).
        currentPitch = startPitch;

        // Clamp it so start respects limits.
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

        // Apply the initial pitch.
        pitchPivot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
    }


    private void OnEnable()
    {
        // Sync input actions to current state.
        SetInputEnabled(inputEnabled);

        // Apply cursor state on enable.
        ApplyDesiredCursorState();
    }


    private void OnDisable()
    {
        // Disable input when this component goes inactive.
        DisableInputActions();

        // Force a fresh cursor-state apply the next time input is enabled.
        hasAppliedCursorState = false;

        // Unlock cursor when this component disables.
        SetCursorState(false, false);

        // Clear transient combat orbit crosshair state.
        ClearCombatOrbitCrosshairFreeze(false);
    }


    private void OnApplicationFocus(bool hasFocus)
    {
        // If we want to unlock when unfocused, release the cursor on focus loss.
        if (unlockCursorWhenApplicationUnfocused && !hasFocus)
        {
            // Unlock cursor when focus is lost.
            SetCursorState(false, false);
        }

        // Re-apply desired state when focus returns.
        if (hasFocus)
        {
            // Apply desired cursor state again after refocus.
            ApplyDesiredCursorState();
        }
    }


    private void LateUpdate()
    {
        // Stop if we don't have a player target.
        if (!playerTarget) return;

        if (UI.ConsoleController.IsOpen)
        {
            ResetCombatEdgePanSmoothing();
            ClearCombatOrbitCrosshairFreeze(false);
            hasAppliedCursorState = false;
            return;
        }

        // Stop processing input while disabled (e.g., UI open).
        if (!inputEnabled) return;

        bool combatMode = GetCombatMode();

        // Apply cursor lock based on combat mode.
        ApplyDesiredCursorState(combatMode);

        // Capture/release the combat crosshair point independently of camera rotation.
        UpdateCombatOrbitCrosshairFreeze(combatMode);

        // Keep the yaw pivot centered on the player every frame.
        yawPivot.position = playerTarget.position;

        // Handle smooth reset if active.
        if (isResetting)
        {
            // Clear edge-pan smoothing so it cannot resume after the reset completes.
            ResetCombatEdgePanSmoothing();

            // Step the reset.
            UpdateSmoothReset();

            // Stop here so manual orbit doesn't fight the reset.
            return;
        }

        // Combat mode keeps the existing hybrid orbit flow (MMB hold + double-click reset).
        if (combatMode)
        {
            // Stop if we are ADSing (we do not want orbit while aiming).
            if (IsAdsHeld())
            {
                ResetCombatEdgePanSmoothing();
                return;
            }

            // Handle double-click reset on rotate button.
            HandleDoubleClickReset();

            // Apply edge panning while not ADS so combat mode can orbit without middle mouse.
            ApplyCombatEdgePan();

            // Stop if we are not holding the rotate button.
            if (!IsRotateHeld()) return;

            // Apply orbit input.
            ApplyLookDelta();

            // Done.
            return;
        }

        // Combat-only edge pan should not carry smoothed velocity into normal free-look.
        ResetCombatEdgePanSmoothing();

        // Non-combat is Fallout 3 style: free-look camera (mouse always rotates the camera).
        // Optionally block rotation while ADS is held if you want that behaviour.
        if (blockFreeLookWhileAds && IsAdsHeld()) return;

        // Apply free-look input (even if rotate hold is not pressed).
        ApplyLookDelta();
    }



    private void ApplyDesiredCursorState()
    {
        if (!inputEnabled) return;

        ApplyDesiredCursorState(GetCombatMode());
    }


    private void ApplyDesiredCursorState(bool combatMode)
    {
        if (!inputEnabled) return;

        // Lock only in non-combat, but still hide in combat when configured.
        bool shouldLock = combatMode ? false : lockCursorWhenCombatModeIsFalse;
        bool shouldHide = combatMode ? hideCursorWhenCombatModeIsTrue : shouldLock;
        bool isCurrentlyLocked = Cursor.lockState == CursorLockMode.Locked;
        bool isCurrentlyHidden = !Cursor.visible;

        // Avoid hammering the Cursor API every frame.
        if (hasAppliedCursorState
            && cursorLockedApplied == shouldLock
            && cursorHiddenApplied == shouldHide
            && isCurrentlyLocked == shouldLock
            && isCurrentlyHidden == shouldHide)
            return;

        // Apply the state.
        SetCursorState(shouldLock, shouldHide);

        // Cache the applied state.
        cursorLockedApplied = shouldLock;
        cursorHiddenApplied = shouldHide;
        hasAppliedCursorState = true;
    }


    private void SetCursorState(bool locked, bool hidden)
    {
        // Lock or unlock the cursor.
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;

        // Hide cursor when requested, show it when not hidden.
        Cursor.visible = !hidden;
    }


    private void UpdateCombatOrbitCrosshairFreeze(bool combatMode)
    {
        bool shouldFreeze = combatMode && IsCombatManualOrbitHeld;

        if (shouldFreeze)
        {
            if (!isCombatOrbitCrosshairFrozen)
            {
                combatOrbitFrozenCrosshairScreenPoint = GetMouseScreenPositionOrCenter();
                isCombatOrbitCrosshairFrozen = true;
            }

            return;
        }

        ClearCombatOrbitCrosshairFreeze(restoreCursorPositionAfterCombatOrbit);
    }


    private void ClearCombatOrbitCrosshairFreeze(bool restoreCursorPosition)
    {
        if (!isCombatOrbitCrosshairFrozen)
            return;

        Vector2 restorePoint = ClampScreenPoint(combatOrbitFrozenCrosshairScreenPoint);
        isCombatOrbitCrosshairFrozen = false;

        if (!restoreCursorPosition)
            return;

        Mouse mouse = Mouse.current;
        if (mouse == null)
            return;

        mouse.WarpCursorPosition(restorePoint);
    }


    private static Vector2 GetMouseScreenPositionOrCenter()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null)
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        return ClampScreenPoint(mouse.position.ReadValue());
    }


    private static Vector2 ClampScreenPoint(Vector2 screenPoint)
    {
        float maxX = Mathf.Max(0f, Screen.width - 1f);
        float maxY = Mathf.Max(0f, Screen.height - 1f);

        return new Vector2(
            Mathf.Clamp(screenPoint.x, 0f, maxX),
            Mathf.Clamp(screenPoint.y, 0f, maxY)
        );
    }


    private bool GetCombatMode()
    {
        if (!playerState) return false;

        return playerState.GetCombatMode();
    }



    private bool IsRotateHeld()
    {
        if (UI.ConsoleController.IsOpen) return false;

        if (!inputEnabled) return false;

        InputAction action = cameraRotateHold ? cameraRotateHold.action : null;

        // Stop if we have no rotate hold action.
        if (action == null) return false;

        // Return whether the button is currently pressed.
        return action.IsPressed();
    }


    private bool IsAdsHeld()
    {
        if (UI.ConsoleController.IsOpen) return false;

        if (!inputEnabled) return false;

        InputAction action = aDSHold ? aDSHold.action : null;

        // Stop if we have no ADS action assigned.
        if (action == null) return false;

        // Return whether ADS is currently pressed.
        return action.IsPressed();
    }


    private void ApplyLookDelta()
    {
        if (UI.ConsoleController.IsOpen) return;

        if (!inputEnabled) return;

        InputAction action = cameraLookDelta ? cameraLookDelta.action : null;

        // Stop if we don't have a look delta action.
        if (action == null) return;

        // Read the mouse delta.
        Vector2 delta = action.ReadValue<Vector2>();

        // Stop if there is no meaningful delta (prevents tiny noise causing drift).
        if (delta.sqrMagnitude < MinLookDeltaSqr) return;

        // Add yaw from horizontal mouse delta.
        currentYaw += delta.x * yawSpeed;

        // Choose the pitch sign based on invert setting.
        float pitchSign = invertPitch ? 1.0f : -1.0f;

        // Add pitch from vertical mouse delta.
        currentPitch += delta.y * pitchSpeed * pitchSign;

        // Clamp pitch so we never flip.
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

        // Apply yaw to the yaw pivot (orbit around the player).
        yawPivot.rotation = Quaternion.Euler(0f, currentYaw, 0f);

        // Apply pitch to the pitch pivot (tilt the orbit).
        pitchPivot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
    }


    private bool IsCombatEdgePanActive()
    {
        if (!inputEnabled) return false;

        if (!enableEdgePanWhileCombatMode) return false;

        if (!GetCombatMode()) return false;

        if (IsAdsHeld()) return false;

        if (TryGetCombatEdgePanTargetPush(out _)) return true;

        return smoothedCombatEdgePanPush.sqrMagnitude > MinLookDeltaSqr;
    }


    private void ApplyCombatEdgePan()
    {
        // Read the graded edge input, then smooth it so edge panning eases in/out.
        TryGetCombatEdgePanTargetPush(out Vector2 targetPush, true);

        float dt = Time.deltaTime;

        float smoothTime = Mathf.Max(0f, combatEdgePanSmoothingTime);

        smoothedCombatEdgePanPush = Vector2.SmoothDamp(
            smoothedCombatEdgePanPush,
            targetPush,
            ref combatEdgePanPushVelocity,
            smoothTime,
            Mathf.Infinity,
            dt
        );

        // Stop once the smoothed input has settled back to zero.
        if (smoothedCombatEdgePanPush.sqrMagnitude < MinLookDeltaSqr)
        {
            smoothedCombatEdgePanPush = Vector2.zero;
            combatEdgePanPushVelocity = Vector2.zero;
            return;
        }

        // Convert push into degrees this frame (time-based, not input-delta based).
        float yawHighPushMultiplier = GetHighPushSpeedMultiplier(
            Mathf.Abs(smoothedCombatEdgePanPush.x),
            combatEdgeYawHighPushThreshold,
            combatEdgeYawHighPushSpeedMultiplier,
            combatEdgeYawHighPushResponseExponent
        );

        float yawDegrees = smoothedCombatEdgePanPush.x * combatEdgeYawDegreesPerSecond * yawHighPushMultiplier * dt;

        float pitchDegrees = smoothedCombatEdgePanPush.y * combatEdgePitchDegreesPerSecond * dt;

        // Apply forced orbit rotation using the same pitch inversion/clamping path as ADS edge pan.
        AddOrbitDegrees(yawDegrees, pitchDegrees);
    }


    private void ResetCombatEdgePanSmoothing()
    {
        smoothedCombatEdgePanPush = Vector2.zero;

        combatEdgePanPushVelocity = Vector2.zero;

        combatEdgePanEntryPush = Vector2.zero;

        lastCombatEdgePanDirection = Vector2.zero;
    }


    private bool TryGetCombatEdgePanTargetPush(out Vector2 push, bool updateEntryState = false)
    {
        push = Vector2.zero;

        if (UI.ConsoleController.IsOpen) return false;

        // Stop if this input path is disabled.
        if (!inputEnabled) return false;

        // Stop if combat edge pan is disabled.
        if (!enableEdgePanWhileCombatMode) return false;

        // Stop unless we are in combat mode.
        if (!GetCombatMode()) return false;

        // ADS has its own edge-pan path in CameraADSZoom.
        if (IsAdsHeld()) return false;

        // Stop if there is no mouse device detected.
        if (Mouse.current == null) return false;

        // Cache screen size.
        float w = Screen.width;

        float h = Screen.height;

        // Stop if screen size is invalid.
        if (w <= 1f || h <= 1f) return false;

        float edgePixels = Mathf.Max(0f, combatEdgePanPixels);

        // Stop if the edge zone is disabled by size.
        if (edgePixels <= 0f) return false;

        // Read the mouse position in screen pixels.
        Vector2 mousePos = Mouse.current.position.ReadValue();

        // Read physical mouse movement so pan strength can respond to how hard the player pushes.
        Vector2 mouseDelta = Mouse.current.delta.ReadValue();

        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float fullPushPixelsPerSecond = Mathf.Max(1f, combatEdgePanFullPushMousePixelsPerSecond);
        float minPushPixelsPerSecond = Mathf.Max(0f, combatEdgePanMinPushMousePixelsPerSecond);
        float mousePushResponseExponent = Mathf.Max(0.01f, combatEdgePanMousePushResponseExponent);
        float positionPushAtScreenEdge = Mathf.Clamp01(combatEdgePanPositionPushAtScreenEdge);
        float entryPushWeight = Mathf.Clamp01(combatEdgePanEntryPushWeight);
        float ongoingPushWeight = Mathf.Clamp01(combatEdgePanOngoingPushWeight);
        float pitchFullPushPixelsPerSecond = Mathf.Max(1f, combatEdgePitchPanFullPushMousePixelsPerSecond);
        float pitchMinPushPixelsPerSecond = Mathf.Max(0f, combatEdgePitchPanMinPushMousePixelsPerSecond);
        float pitchMousePushResponseExponent = Mathf.Max(0.01f, combatEdgePitchPanMousePushResponseExponent);
        float pitchPositionPushAtScreenEdge = Mathf.Clamp01(combatEdgePitchPanPositionPushAtScreenEdge);
        float pitchEntryPushWeight = Mathf.Clamp01(combatEdgePitchPanEntryPushWeight);
        float pitchOngoingPushWeight = Mathf.Clamp01(combatEdgePitchPanOngoingPushWeight);

        Vector2 edgeDirection = Vector2.zero;
        Vector2 positionPush = Vector2.zero;
        Vector2 ongoingMousePush = Vector2.zero;

        // Left edge.
        if (mousePos.x <= edgePixels)
        {
            float edgeDepth = Mathf.Clamp01((edgePixels - mousePos.x) / edgePixels);
            float mouseSpeedTowardEdge = Mathf.Max(0f, -mouseDelta.x / dt);
            edgeDirection.x = -1f;
            positionPush.x = edgeDepth;
            ongoingMousePush.x = GetMousePushStrength(mouseSpeedTowardEdge, minPushPixelsPerSecond, fullPushPixelsPerSecond, mousePushResponseExponent);
        }

        // Right edge.
        else if (mousePos.x >= w - edgePixels)
        {
            float rightEdgeStart = w - edgePixels;
            float rightEdgeSpan = Mathf.Max(1f, (w - 1f) - rightEdgeStart);
            float edgeDepth = Mathf.Clamp01((mousePos.x - rightEdgeStart) / rightEdgeSpan);
            float mouseSpeedTowardEdge = Mathf.Max(0f, mouseDelta.x / dt);
            edgeDirection.x = 1f;
            positionPush.x = edgeDepth;
            ongoingMousePush.x = GetMousePushStrength(mouseSpeedTowardEdge, minPushPixelsPerSecond, fullPushPixelsPerSecond, mousePushResponseExponent);
        }

        // Bottom edge.
        if (mousePos.y <= edgePixels)
        {
            float edgeDepth = Mathf.Clamp01((edgePixels - mousePos.y) / edgePixels);
            float mouseSpeedTowardEdge = Mathf.Max(0f, -mouseDelta.y / dt);
            edgeDirection.y = -1f;
            positionPush.y = edgeDepth;
            ongoingMousePush.y = GetMousePushStrength(mouseSpeedTowardEdge, pitchMinPushPixelsPerSecond, pitchFullPushPixelsPerSecond, pitchMousePushResponseExponent);
        }

        // Top edge.
        else if (mousePos.y >= h - edgePixels)
        {
            float topEdgeStart = h - edgePixels;
            float topEdgeSpan = Mathf.Max(1f, (h - 1f) - topEdgeStart);
            float edgeDepth = Mathf.Clamp01((mousePos.y - topEdgeStart) / topEdgeSpan);
            float mouseSpeedTowardEdge = Mathf.Max(0f, mouseDelta.y / dt);
            edgeDirection.y = 1f;
            positionPush.y = edgeDepth;
            ongoingMousePush.y = GetMousePushStrength(mouseSpeedTowardEdge, pitchMinPushPixelsPerSecond, pitchFullPushPixelsPerSecond, pitchMousePushResponseExponent);
        }

        if (updateEntryState)
        {
            UpdateEdgePanEntryPush(
                edgeDirection,
                ongoingMousePush,
                ref combatEdgePanEntryPush,
                ref lastCombatEdgePanDirection,
                combatEdgePanEntryPushDecayTime,
                combatEdgePitchPanEntryPushDecayTime,
                dt
            );
        }

        push.x = GetSignedEdgePanPush(
            edgeDirection.x,
            positionPush.x,
            Mathf.Abs(combatEdgePanEntryPush.x),
            ongoingMousePush.x,
            positionPushAtScreenEdge,
            entryPushWeight,
            ongoingPushWeight
        );

        push.y = GetSignedEdgePanPush(
            edgeDirection.y,
            positionPush.y,
            Mathf.Abs(combatEdgePanEntryPush.y),
            ongoingMousePush.y,
            pitchPositionPushAtScreenEdge,
            pitchEntryPushWeight,
            pitchOngoingPushWeight
        );

        return push != Vector2.zero;
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


    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;

        if (inputEnabled)
        {
            EnableInputActions();
            ApplyDesiredCursorState();
        }
        else
        {
            DisableInputActions();
            ResetCombatEdgePanSmoothing();
            hasAppliedCursorState = false;
        }
    }

    private void EnableInputActions()
    {
        InputAction rotateAction = cameraRotateHold ? cameraRotateHold.action : null;
        InputAction lookAction = cameraLookDelta ? cameraLookDelta.action : null;
        InputAction adsAction = aDSHold ? aDSHold.action : null;

        // Enable rotate hold input if assigned.
        if (rotateAction != null) rotateAction.Enable();

        // Enable look delta input if assigned.
        if (lookAction != null) lookAction.Enable();

        // Enable ADS hold input if assigned.
        if (adsAction != null) adsAction.Enable();
    }

    private void DisableInputActions()
    {
        InputAction rotateAction = cameraRotateHold ? cameraRotateHold.action : null;
        InputAction lookAction = cameraLookDelta ? cameraLookDelta.action : null;
        InputAction adsAction = aDSHold ? aDSHold.action : null;

        // Disable rotate hold input if assigned.
        if (rotateAction != null) rotateAction.Disable();

        // Disable look delta input if assigned.
        if (lookAction != null) lookAction.Disable();

        // Disable ADS hold input if assigned.
        if (adsAction != null) adsAction.Disable();
    }


    private void HandleDoubleClickReset()
    {
        InputAction rotateAction = cameraRotateHold ? cameraRotateHold.action : null;

        // Stop if rotate action is missing.
        if (rotateAction == null) return;

        // Detect button down this frame.
        if (!rotateAction.WasPressedThisFrame()) return;

        // If the time since last click is within threshold, reset camera.
        if (Time.time - lastRotateClickTime <= doubleClickTime)
        {
            // Begin the reset.
            ResetOrbit();
        }

        // Record click time.
        lastRotateClickTime = Time.time;
    }


    private void ResetOrbit()
    {
        // Capture where we are starting from.
        resetStartYaw = currentYaw;

        // Capture where we are starting from.
        resetStartPitch = currentPitch;

        // Target yaw is always behind the player (match player facing).
        resetTargetYaw = playerTarget.eulerAngles.y;

        // Target pitch is the default pitch (clamped).
        resetTargetPitch = Mathf.Clamp(startPitch, minPitch, maxPitch);

        // Reset timer.
        resetTimer = 0f;

        // Enter reset mode.
        isResetting = true;
    }


    private void UpdateSmoothReset()
    {
        // Advance timer.
        resetTimer += Time.deltaTime;

        // Normalized progress (0 → 1).
        float t = Mathf.Clamp01(resetTimer / resetSmoothTime);

        // Smooth easing.
        t = Mathf.SmoothStep(0f, 1f, t);

        // Interpolate yaw toward player-facing yaw.
        currentYaw = Mathf.LerpAngle(resetStartYaw, resetTargetYaw, t);

        // Interpolate pitch toward default pitch.
        currentPitch = Mathf.Lerp(resetStartPitch, resetTargetPitch, t);

        // Apply yaw.
        yawPivot.rotation = Quaternion.Euler(0f, currentYaw, 0f);

        // Apply pitch.
        pitchPivot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);

        // Finish reset.
        if (t >= 1f)
        {
            // Exit reset mode.
            isResetting = false;
        }
    }
    
    
    public void AddOrbitDegrees(float yawDegrees, float pitchDegrees)
    {
        // Stop if we don't have pivots.
        if (!yawPivot) return;

        if (!pitchPivot) return;

        // Stop if a smooth reset is running (do not fight it).
        if (isResetting) return;

        // Add yaw directly in degrees.
        currentYaw += yawDegrees;

        // Choose pitch sign based on invert setting (match manual orbit behavior).
        float pitchSign = invertPitch ? 1.0f : -1.0f;

        // Add pitch directly in degrees (respect invert).
        currentPitch += pitchDegrees * pitchSign;

        // Clamp pitch so we never flip.
        currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

        // Apply yaw to the yaw pivot (orbit around the player).
        yawPivot.rotation = Quaternion.Euler(0f, currentYaw, 0f);

        // Apply pitch to the pitch pivot (tilt the orbit).
        pitchPivot.localRotation = Quaternion.Euler(currentPitch, 0f, 0f);
    }
}
