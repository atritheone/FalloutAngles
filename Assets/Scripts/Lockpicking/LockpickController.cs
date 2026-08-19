using System.Collections.Generic;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class LockpickController : MonoBehaviour
{
    private const float TorqueInputDeadzone = 0.5f;

    private enum EndReason
    {
        Success,
        Cancelled,
        OutOfPins
    }

    private struct BehaviourState
    {
        public Behaviour Behaviour;
        public bool WasEnabled;
    }

    private struct GameObjectState
    {
        public GameObject GameObject;
        public bool WasActiveSelf;
    }

    [Header("Behavior")]
    [SerializeField] private bool openContainerAfterUnlock = true;
    [SerializeField] private bool allowLockpickingOnKeyLocks = false;

    [Header("Input")]
    [SerializeField] private float mousePickSensitivity = 0.10f;
    [SerializeField] private float stickPickSpeed = 110.0f;

    [Header("Core")]
    [SerializeField] private LockDefinition defaultDefinition;
    [SerializeField] private LockDefinition veryEasyDefinition;
    [SerializeField] private LockDefinition easyDefinition;
    [FormerlySerializedAs("averageDefinition")]
    [SerializeField] private LockDefinition mediumDefinition;
    [SerializeField] private LockDefinition hardDefinition;
    [SerializeField] private LockDefinition veryHardDefinition;
    [FormerlySerializedAs("maxPickAngle")]
    [SerializeField] private float fallbackMaxPickAngle = 90.0f;
    [SerializeField] private float cylinderTurnSpeed = 180.0f;
    [SerializeField] private float cylinderReturnSpeed = 260.0f;
    [SerializeField] private int maxPins = 5;
    [SerializeField] private bool failWhenOutOfPins = true;
    [SerializeField] private bool randomizeTargetOnPinBreak = false;
    [SerializeField] private float successTolerance = 0.5f;

    [Header("Skill Requirements")]
    [SerializeField] private bool enforceLockpickSkillRequirement = true;
    [SerializeField, Range(0, 100)] private int easySkillRequirement = 25;
    [FormerlySerializedAs("averageSkillRequirement")]
    [SerializeField, Range(0, 100)] private int mediumSkillRequirement = 50;
    [SerializeField, Range(0, 100)] private int hardSkillRequirement = 75;
    [SerializeField, Range(0, 100)] private int veryHardSkillRequirement = 100;

    [Header("Skill Experience")]
    [SerializeField] private bool awardLockpickSkillExperience = true;
    [SerializeField, Min(0f)] private float veryEasyLockpickSkillExperience = 15f;
    [SerializeField, Min(0f)] private float easyLockpickSkillExperience = 25f;
    [SerializeField, Min(0f)] private float mediumLockpickSkillExperience = 50f;
    [SerializeField, Min(0f)] private float hardLockpickSkillExperience = 75f;
    [SerializeField, Min(0f)] private float veryHardLockpickSkillExperience = 100f;

    [Header("Bobby Pins")]
    [SerializeField] private bool useInventoryPinCount = true;
    [SerializeField] private MiscDefinition bobbyPinDefinition;
    [SerializeField] private bool requireBobbyPinToStart = true;
    [SerializeField] private bool consumeBobbyPinOnBreak = true;

    [Header("Control Locking")]
    [SerializeField] private bool disablePlayerActionMap = true;
    [SerializeField] private CameraRigOrbit cameraRigOrbit;
    [SerializeField] private List<Behaviour> additionalBehavioursToDisable = new List<Behaviour>();

    [Header("Camera")]
    [SerializeField] private Camera gameplayCamera;
    [SerializeField] private Camera lockpickCamera;
    [SerializeField] private Transform cameraTransformToSnap;
    [SerializeField] private Transform lockpickCameraAnchor;
    [SerializeField] private string containerCameraAnchorName = "LockpickCameraAnchor";
    [SerializeField] private GameObject lockpickModeRoot;
    [SerializeField] private Transform lockpickModeCameraAnchor;
    [SerializeField] private Vector3 lockpickModeWorldOffset = new Vector3(0.0f, 1000.0f, 0.0f);

    [Header("UI")]
    [SerializeField] private GameObject playerUiRoot;
    [SerializeField] private GameObject lockpickUiRoot;
    [SerializeField] private TMP_Text lockpickSkillNumberText;
    [SerializeField] private TMP_Text bobbyPinsNumberText;
    [SerializeField] private TMP_Text lockLevelActualText;

    [Header("Scene")]
    [SerializeField] private GameObject sceneSun;

    [Header("Visuals")]
    [SerializeField] private Transform bobbyPinPivot;
    [SerializeField] private Transform cylinderPivot;
    [SerializeField] private Vector3 bobbyPinAxis = Vector3.forward;
    [SerializeField] private Vector3 cylinderAxis = Vector3.forward;

    [Header("Debug")]
    [SerializeField] private bool showDebugOverlay = true;

    private static LockpickController activeController;

    private InputSystemActions lockpickInput;
    private readonly List<BehaviourState> disabledBehaviours = new List<BehaviourState>();
    private readonly List<GameObjectState> playerUiChildStates = new List<GameObjectState>();
    private readonly List<GameObjectState> lockpickUiSubtreeStates = new List<GameObjectState>();
    private readonly Dictionary<Container, float> persistentTargetAnglesByContainer = new Dictionary<Container, float>();
    private LockDefinition runtimeFallbackDefinition;

    private Container activeContainer;
    private LockDefinition activeDefinition;
    private Transform activeLockpickCameraAnchor;
    private GameObject activeInteractor;
    private PlayerInventory activePlayerInventory;
    private PlayerControls activePlayerControls;
    private Quaternion bobbyPinInitialRotation = Quaternion.identity;
    private Quaternion cylinderInitialRotation = Quaternion.identity;

    private bool activePlayerMapWasEnabled;
    private bool hasCachedLockpickModeRootPose;
    private bool hasCachedSceneSunState;
    private bool hasInitialVisualPose;
    private bool hasRuntimeInitialized;
    private bool isLockpickActive;
    private bool didHidePlayerUiForLockpick;
    private bool hasCachedLockpickUiSubtreeState;
    private bool isWaitingForInteractReleaseAfterEnter;
    private bool wasGameplayCameraEnabledBeforeMode;
    private bool wasPlayerUiActiveBeforeLockpick;
    private bool wasSceneSunActiveBeforeLockpick;
    private bool hadGameplayCameraPose;
    private Vector3 cachedGameplayCameraPosition;
    private Quaternion cachedGameplayCameraRotation;
    private Vector3 cachedLockpickModeRootPosition;
    private Quaternion cachedLockpickModeRootRotation = Quaternion.identity;

    private float targetAngle;
    private float currentPickAngle;
    private float cylinderRotation;
    private float pinStress;
    private int remainingPins;

    public bool IsLockpickActive => isLockpickActive;
    public float CurrentPickAngle => currentPickAngle;
    public float TargetAngle => targetAngle;
    public float CylinderRotation => cylinderRotation;
    public float PinStress => pinStress;

    public static LockpickController FindFirstInSceneIncludingInactive()
    {
        return FindFirstObjectInSceneIncludingInactive<LockpickController>();
    }

    private void OnValidate()
    {
        fallbackMaxPickAngle = Mathf.Max(1.0f, fallbackMaxPickAngle);
        cylinderTurnSpeed = Mathf.Max(1.0f, cylinderTurnSpeed);
        cylinderReturnSpeed = Mathf.Max(1.0f, cylinderReturnSpeed);
        maxPins = Mathf.Max(1, maxPins);
        successTolerance = Mathf.Max(0.0f, successTolerance);
        mousePickSensitivity = Mathf.Max(0.001f, mousePickSensitivity);
        stickPickSpeed = Mathf.Max(1.0f, stickPickSpeed);
        easySkillRequirement = Mathf.Clamp(easySkillRequirement, 0, 100);
        mediumSkillRequirement = Mathf.Clamp(mediumSkillRequirement, 0, 100);
        hardSkillRequirement = Mathf.Clamp(hardSkillRequirement, 0, 100);
        veryHardSkillRequirement = Mathf.Clamp(veryHardSkillRequirement, 0, 100);
        veryEasyLockpickSkillExperience = Mathf.Max(0f, veryEasyLockpickSkillExperience);
        easyLockpickSkillExperience = Mathf.Max(0f, easyLockpickSkillExperience);
        mediumLockpickSkillExperience = Mathf.Max(0f, mediumLockpickSkillExperience);
        hardLockpickSkillExperience = Mathf.Max(0f, hardLockpickSkillExperience);
        veryHardLockpickSkillExperience = Mathf.Max(0f, veryHardLockpickSkillExperience);
    }

    private void Awake()
    {
        EnsureRuntimeInitialized();
    }

    private void EnsureRuntimeInitialized()
    {
        if (hasRuntimeInitialized)
            return;

        lockpickInput = new InputSystemActions();

        if (!gameplayCamera)
            gameplayCamera = Camera.main;

        if (!cameraTransformToSnap && gameplayCamera)
            cameraTransformToSnap = gameplayCamera.transform;

        if (!cameraRigOrbit)
            cameraRigOrbit = FindAnyObjectByType<CameraRigOrbit>();

        if (lockpickCamera)
            lockpickCamera.enabled = false;

        ResolvePlayerUiRoot();
        ResolveLockpickUiRoot();
        ResolveLockpickUiTextReferences();
        ResolveSceneSun();
        CacheInitialVisualPoseIfNeeded();
        CacheLockpickModeRootPoseIfNeeded();
        ResolveLockpickModeCameraAnchor();
        SetLockpickModeVisible(false);
        hasRuntimeInitialized = true;
    }

    private void OnDisable()
    {
        if (isLockpickActive)
            EndLockpick(EndReason.Cancelled);
    }

    private void OnDestroy()
    {
        UnbindActivePlayerInventory();

        if (runtimeFallbackDefinition)
            Destroy(runtimeFallbackDefinition);

        if (activeController == this)
            activeController = null;
    }

    private void Update()
    {
        if (!isLockpickActive)
            return;

        if (WasCancelPressed())
        {
            EndLockpick(EndReason.Cancelled);
            return;
        }

        UpdatePickRotation();
        UpdateCylinderAndStress();
        UpdateVisuals();
    }

    public bool CanLockpick(Container targetContainer)
    {
        EnsureRuntimeInitialized();
        Container resolvedContainer = ResolveTargetContainer(targetContainer);
        return resolvedContainer && !IsNonPickableKeyLock(resolvedContainer);
    }

    public string GetInteractionText(Container targetContainer, GameObject interactor)
    {
        EnsureRuntimeInitialized();
        Container resolvedContainer = ResolveTargetContainer(targetContainer);
        if (!resolvedContainer)
            return "Lockpick";

        string basePrompt = $"Open {resolvedContainer.GetContainerName()}";

        if (!resolvedContainer.IsLocked())
            return basePrompt;

        if (IsNonPickableKeyLock(resolvedContainer))
            return $"{basePrompt}\n[Locked - Requires Key]";

        string lockLine = $"[Locked - {GetLockTypeLabel(resolvedContainer.GetLockType())}]";

        int requiredSkill = GetRequiredSkillForLock(resolvedContainer);
        int interactorSkill = GetInteractorSkill(interactor);
        if (enforceLockpickSkillRequirement && interactorSkill < requiredSkill)
            return $"{basePrompt}\n{lockLine}\n[Requires Lockpick {requiredSkill}]";

        if (requireBobbyPinToStart && ResolveStartingPinCount(GetInteractorInventory(interactor)) <= 0)
            return $"{basePrompt}\n{lockLine}\n[Need Bobby Pins]";

        return $"{basePrompt}\n{lockLine}";
    }

    public bool TryBegin(Container targetContainer, GameObject interactor)
    {
        EnsureRuntimeInitialized();
        Container resolvedContainer = ResolveTargetContainer(targetContainer);
        if (!resolvedContainer || !resolvedContainer.IsLocked())
            return false;

        if (activeController != null && activeController != this && activeController.isLockpickActive)
            return false;

        if (isLockpickActive)
            return false;

        if (IsNonPickableKeyLock(resolvedContainer))
            return false;

        PlayerState interactorPlayerState = GetInteractorPlayerState(interactor);
        PlayerInventory interactorInventory = GetInteractorInventory(interactor);

        int requiredSkill = GetRequiredSkillForLock(resolvedContainer);
        int interactorSkill = GetInteractorSkill(interactorPlayerState);
        if (enforceLockpickSkillRequirement && interactorSkill < requiredSkill)
            return false;

        int startingPins = ResolveStartingPinCount(interactorInventory);
        if (requireBobbyPinToStart && startingPins <= 0)
            return false;

        LockDefinition resolvedDefinition = ResolveDefinitionFromContainer(resolvedContainer);

        if (!resolvedDefinition)
        {
            Debug.LogWarning("LockpickController: no lock definition could be resolved.");
            return false;
        }

        activeController = this;
        activeContainer = resolvedContainer;
        activeInteractor = interactor;
        activePlayerInventory = interactorInventory;
        activeDefinition = resolvedDefinition;
        activeLockpickCameraAnchor = ResolveCameraAnchorForContainer(resolvedContainer);

        currentPickAngle = 0.0f;
        cylinderRotation = 0.0f;
        pinStress = 0.0f;
        remainingPins = startingPins;
        targetAngle = GetOrCreatePersistentTargetAngle(resolvedContainer);

        SetPickAngleVisual(0.0f);
        SetCylinderRotationVisual(0.0f);

        EnterLockpickMode();
        return true;
    }

    private void EnterLockpickMode()
    {
        isLockpickActive = true;
        isWaitingForInteractReleaseAfterEnter = true;

        BindActivePlayerInventory();
        RefreshLockpickUi();
        AlignLockpickModeRootToActiveAnchor();
        SetPlayerUiVisible(false);
        SetSceneSunVisible(false);
        SetLockpickInputEnabled(true);
        SetLockpickCameraEnabled(true);
        DisableInteractorControl();
        SetLockpickModeVisible(true);
    }

    private void EndLockpick(EndReason reason)
    {
        if (!isLockpickActive)
            return;

        isLockpickActive = false;

        SetLockpickInputEnabled(false);
        UnbindActivePlayerInventory();
        RestoreInteractorControl();
        SetLockpickCameraEnabled(false);
        SetPlayerUiVisible(true);
        SetSceneSunVisible(true);
        SetLockpickModeVisible(false);

        if (reason == EndReason.Success)
        {
            AwardLockpickSkillExperience();

            if (activeContainer)
                activeContainer.Unlock();

            if (openContainerAfterUnlock && activeContainer)
                activeContainer.Interact(activeInteractor);
        }

        activeContainer = null;
        activeDefinition = null;
        activeLockpickCameraAnchor = null;
        activeInteractor = null;
        activePlayerInventory = null;
        activePlayerControls = null;
        isWaitingForInteractReleaseAfterEnter = false;

        if (activeController == this)
            activeController = null;
    }

    private void UpdatePickRotation()
    {
        float angleDelta = GetPickAngleDelta();
        if (Mathf.Approximately(angleDelta, 0.0f))
            return;

        float maxPickAngle = GetActiveMaxPickAngle();
        currentPickAngle = Mathf.Clamp(currentPickAngle + angleDelta, -maxPickAngle, maxPickAngle);
    }

    private float GetPickAngleDelta()
    {
        Mouse mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            if (!Mathf.Approximately(mouseDelta.x, 0.0f))
                return mouseDelta.x * mousePickSensitivity;
        }

        InputAction lookAction = lockpickInput.Player.Look;
        if (lookAction == null)
            return 0.0f;

        bool usingGamepad =
            lookAction.activeControl != null &&
            lookAction.activeControl.device is Gamepad;

        if (!usingGamepad)
            return 0.0f;

        Vector2 lookValue = lookAction.ReadValue<Vector2>();
        return lookValue.x * stickPickSpeed * Time.unscaledDeltaTime;
    }

    private void UpdateCylinderAndStress()
    {
        if (!activeDefinition)
            return;

        float dt = Time.unscaledDeltaTime;
        float maxRotation = Mathf.Max(0.1f, activeDefinition.maxCylinderRotation);
        float allowedRotation = CalculateAllowedCylinderRotation();
        bool torqueHeld = IsTorqueHeld();

        if (torqueHeld)
        {
            cylinderRotation = Mathf.MoveTowards(cylinderRotation, allowedRotation, cylinderTurnSpeed * dt);

            float resistance = 1.0f - Mathf.Clamp01(allowedRotation / maxRotation);
            if (resistance > 0.0f)
                pinStress += resistance * activeDefinition.stressIncreaseRate * dt;
            else
                pinStress = Mathf.MoveTowards(pinStress, 0.0f, activeDefinition.stressRecoveryRate * dt);
        }
        else
        {
            cylinderRotation = Mathf.MoveTowards(cylinderRotation, 0.0f, cylinderReturnSpeed * dt);
            pinStress = Mathf.MoveTowards(pinStress, 0.0f, activeDefinition.stressRecoveryRate * dt);
        }

        if (pinStress >= activeDefinition.pinBreakThreshold)
        {
            HandlePinBreak();
            return;
        }

        if (cylinderRotation >= maxRotation - successTolerance)
            EndLockpick(EndReason.Success);
    }

    private float CalculateAllowedCylinderRotation()
    {
        if (!activeDefinition)
            return 0.0f;

        float maxRotation = Mathf.Max(0.1f, activeDefinition.maxCylinderRotation);
        float deltaToTarget = GetPickAngleDistanceToTarget();
        float sweetRange = GetActiveSweetSpotAngleRange();

        if (deltaToTarget <= sweetRange)
            return maxRotation;

        float falloffRange = Mathf.Max(0.1f, GetActiveMaxPickAngle() - sweetRange);
        float normalizedOvershoot = Mathf.Clamp01((deltaToTarget - sweetRange) / falloffRange);
        float quality = 1.0f - normalizedOvershoot;
        return quality * maxRotation;
    }

    private float GetPickAngleDistanceToTarget()
    {
        // The pick angle is clamped to a finite range, so there is no wraparound at +/-180.
        return Mathf.Abs(currentPickAngle - targetAngle);
    }

    private float GetActiveSweetSpotAngleRange()
    {
        if (!activeDefinition)
            return 0.1f;

        return Mathf.Max(0.1f, activeDefinition.sweetSpotAngleRange);
    }

    private void HandlePinBreak()
    {
        pinStress = 0.0f;
        cylinderRotation = 0.0f;
        ConsumeBobbyPin();

        if (randomizeTargetOnPinBreak)
        {
            float maxPickAngle = GetActiveMaxPickAngle();
            targetAngle = SetPersistentTargetAngle(activeContainer, Random.Range(-maxPickAngle, maxPickAngle));
        }

        if (failWhenOutOfPins && remainingPins <= 0)
            EndLockpick(EndReason.OutOfPins);
    }

    private bool IsTorqueHeld()
    {
        InputAction moveAction = lockpickInput.Player.Move;
        if (moveAction == null)
            return false;

        float horizontalInput = moveAction.ReadValue<Vector2>().x;
        return Mathf.Abs(horizontalInput) >= TorqueInputDeadzone;
    }

    private bool WasCancelPressed()
    {
        bool inputMapCancel = lockpickInput.UI.Cancel.WasPressedThisFrame();
        if (inputMapCancel)
            return true;

        InputAction interactAction = lockpickInput.Player.Interact;
        if (interactAction != null)
        {
            if (isWaitingForInteractReleaseAfterEnter)
            {
                if (!interactAction.IsPressed())
                    isWaitingForInteractReleaseAfterEnter = false;
            }
            else if (interactAction.WasPressedThisFrame())
            {
                return true;
            }
        }

        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
    }

    private void UpdateVisuals()
    {
        SetPickAngleVisual(currentPickAngle);
        SetCylinderRotationVisual(cylinderRotation);
    }

    private void SetLockpickInputEnabled(bool enabled)
    {
        if (lockpickInput == null)
            return;

        if (enabled)
        {
            lockpickInput.Player.Enable();
            lockpickInput.UI.Enable();
        }
        else
        {
            lockpickInput.Player.Disable();
            lockpickInput.UI.Disable();
        }
    }

    private void DisableInteractorControl()
    {
        if (activeInteractor)
        {
            activePlayerControls = activeInteractor.GetComponentInParent<PlayerControls>(true);

            if (disablePlayerActionMap && activePlayerControls && activePlayerControls.Controls != null)
            {
                activePlayerMapWasEnabled = activePlayerControls.Controls.Player.enabled;
                activePlayerControls.Controls.Player.Disable();
            }

            TryDisableBehaviour(activeInteractor.GetComponentInParent<PlayerMovement>(true));
            TryDisableBehaviour(activeInteractor.GetComponentInParent<PlayerInteraction>(true));
            TryDisableBehaviour(activeInteractor.GetComponentInParent<PlayerCombat>(true));
            TryDisableBehaviour(activeInteractor.GetComponentInParent<PlayerWeaponController>(true));
        }

        if (!cameraRigOrbit)
            cameraRigOrbit = FindAnyObjectByType<CameraRigOrbit>();

        if (cameraRigOrbit)
            cameraRigOrbit.SetInputEnabled(false);

        for (int i = 0; i < additionalBehavioursToDisable.Count; i++)
            TryDisableBehaviour(additionalBehavioursToDisable[i]);
    }

    private void RestoreInteractorControl()
    {
        for (int i = disabledBehaviours.Count - 1; i >= 0; i--)
        {
            BehaviourState state = disabledBehaviours[i];
            if (state.Behaviour)
                state.Behaviour.enabled = state.WasEnabled;
        }

        disabledBehaviours.Clear();

        if (disablePlayerActionMap && activePlayerControls && activePlayerControls.Controls != null && activePlayerMapWasEnabled)
            activePlayerControls.Controls.Player.Enable();

        if (!cameraRigOrbit)
            cameraRigOrbit = FindAnyObjectByType<CameraRigOrbit>();

        if (cameraRigOrbit)
            cameraRigOrbit.SetInputEnabled(true);
    }

    private void TryDisableBehaviour(Behaviour behaviour)
    {
        if (!behaviour)
            return;

        for (int i = 0; i < disabledBehaviours.Count; i++)
        {
            if (disabledBehaviours[i].Behaviour == behaviour)
                return;
        }

        BehaviourState state = new BehaviourState
        {
            Behaviour = behaviour,
            WasEnabled = behaviour.enabled
        };

        disabledBehaviours.Add(state);

        if (behaviour.enabled)
            behaviour.enabled = false;
    }

    private void SetLockpickCameraEnabled(bool enabled)
    {
        Transform cameraAnchor = activeLockpickCameraAnchor ? activeLockpickCameraAnchor : lockpickCameraAnchor;

        if (enabled)
        {
            if (!gameplayCamera)
                gameplayCamera = Camera.main;

            if (!cameraTransformToSnap && gameplayCamera)
                cameraTransformToSnap = gameplayCamera.transform;

            if (lockpickCamera)
            {
                if (!lockpickCamera.gameObject.activeSelf)
                    lockpickCamera.gameObject.SetActive(true);

                if (gameplayCamera)
                {
                    wasGameplayCameraEnabledBeforeMode = gameplayCamera.enabled;
                    gameplayCamera.enabled = false;
                }

                if (cameraAnchor)
                    lockpickCamera.transform.SetPositionAndRotation(GetActiveLockpickCameraPosition(), GetActiveLockpickCameraRotation());

                lockpickCamera.enabled = true;
                return;
            }

            if (!cameraTransformToSnap || !cameraAnchor)
                return;

            cachedGameplayCameraPosition = cameraTransformToSnap.position;
            cachedGameplayCameraRotation = cameraTransformToSnap.rotation;
            hadGameplayCameraPose = true;

            cameraTransformToSnap.SetPositionAndRotation(GetActiveLockpickCameraPosition(), GetActiveLockpickCameraRotation());
            return;
        }

        if (lockpickCamera)
        {
            lockpickCamera.enabled = false;

            if (gameplayCamera)
                gameplayCamera.enabled = wasGameplayCameraEnabledBeforeMode;
        }

        if (!hadGameplayCameraPose || !cameraTransformToSnap)
            return;

        cameraTransformToSnap.SetPositionAndRotation(cachedGameplayCameraPosition, cachedGameplayCameraRotation);
        hadGameplayCameraPose = false;
    }

    private bool IsNonPickableKeyLock(Container targetContainer)
    {
        if (!targetContainer)
            return false;

        return targetContainer.GetLockType() == Container.LockType.Key && !allowLockpickingOnKeyLocks;
    }

    private LockDefinition ResolveDefinitionFromContainer(Container targetContainer)
    {
        Container resolvedContainer = ResolveTargetContainer(targetContainer);
        if (!resolvedContainer)
            return defaultDefinition ? defaultDefinition : GetRuntimeFallbackDefinition(Container.LockType.Easy);

        LockDefinition resolved = resolvedContainer.GetLockType() switch
        {
            Container.LockType.VeryEasy => veryEasyDefinition ? veryEasyDefinition : defaultDefinition,
            Container.LockType.Easy => easyDefinition ? easyDefinition : defaultDefinition,
            Container.LockType.Medium => mediumDefinition ? mediumDefinition : defaultDefinition,
            Container.LockType.Hard => hardDefinition ? hardDefinition : defaultDefinition,
            Container.LockType.VeryHard => veryHardDefinition ? veryHardDefinition : defaultDefinition,
            _ => defaultDefinition
        };

        if (resolved)
            return resolved;

        return GetRuntimeFallbackDefinition(resolvedContainer.GetLockType());
    }

    private LockDefinition GetRuntimeFallbackDefinition(Container.LockType lockType)
    {
        if (!runtimeFallbackDefinition)
        {
            runtimeFallbackDefinition = ScriptableObject.CreateInstance<LockDefinition>();
            runtimeFallbackDefinition.name = "RuntimeFallbackLockDefinition";
            runtimeFallbackDefinition.maxBobbyPinAngle = fallbackMaxPickAngle;
        }

        switch (lockType)
        {
            case Container.LockType.VeryEasy:
                runtimeFallbackDefinition.difficulty = LockDefinition.Difficulty.VeryEasy;
                runtimeFallbackDefinition.maxBobbyPinAngle = fallbackMaxPickAngle;
                runtimeFallbackDefinition.sweetSpotAngleRange = 30.0f;
                runtimeFallbackDefinition.maxCylinderRotation = 90.0f;
                runtimeFallbackDefinition.pinBreakThreshold = 1.6f;
                runtimeFallbackDefinition.stressIncreaseRate = 0.8f;
                runtimeFallbackDefinition.stressRecoveryRate = 2.2f;
                break;

            case Container.LockType.Easy:
                runtimeFallbackDefinition.difficulty = LockDefinition.Difficulty.Easy;
                runtimeFallbackDefinition.maxBobbyPinAngle = fallbackMaxPickAngle;
                runtimeFallbackDefinition.sweetSpotAngleRange = 22.0f;
                runtimeFallbackDefinition.maxCylinderRotation = 90.0f;
                runtimeFallbackDefinition.pinBreakThreshold = 1.2f;
                runtimeFallbackDefinition.stressIncreaseRate = 1.0f;
                runtimeFallbackDefinition.stressRecoveryRate = 1.8f;
                break;

            case Container.LockType.Medium:
                runtimeFallbackDefinition.difficulty = LockDefinition.Difficulty.Medium;
                runtimeFallbackDefinition.maxBobbyPinAngle = fallbackMaxPickAngle;
                runtimeFallbackDefinition.sweetSpotAngleRange = 16.0f;
                runtimeFallbackDefinition.maxCylinderRotation = 90.0f;
                runtimeFallbackDefinition.pinBreakThreshold = 1.0f;
                runtimeFallbackDefinition.stressIncreaseRate = 1.25f;
                runtimeFallbackDefinition.stressRecoveryRate = 1.4f;
                break;

            case Container.LockType.Hard:
                runtimeFallbackDefinition.difficulty = LockDefinition.Difficulty.Hard;
                runtimeFallbackDefinition.maxBobbyPinAngle = fallbackMaxPickAngle;
                runtimeFallbackDefinition.sweetSpotAngleRange = 10.0f;
                runtimeFallbackDefinition.maxCylinderRotation = 90.0f;
                runtimeFallbackDefinition.pinBreakThreshold = 0.85f;
                runtimeFallbackDefinition.stressIncreaseRate = 1.55f;
                runtimeFallbackDefinition.stressRecoveryRate = 1.1f;
                break;

            case Container.LockType.VeryHard:
                runtimeFallbackDefinition.difficulty = LockDefinition.Difficulty.VeryHard;
                runtimeFallbackDefinition.maxBobbyPinAngle = fallbackMaxPickAngle;
                runtimeFallbackDefinition.sweetSpotAngleRange = 7.0f;
                runtimeFallbackDefinition.maxCylinderRotation = 90.0f;
                runtimeFallbackDefinition.pinBreakThreshold = 0.7f;
                runtimeFallbackDefinition.stressIncreaseRate = 1.9f;
                runtimeFallbackDefinition.stressRecoveryRate = 0.9f;
                break;

            default:
                runtimeFallbackDefinition.difficulty = LockDefinition.Difficulty.Easy;
                runtimeFallbackDefinition.maxBobbyPinAngle = fallbackMaxPickAngle;
                runtimeFallbackDefinition.sweetSpotAngleRange = 22.0f;
                runtimeFallbackDefinition.maxCylinderRotation = 90.0f;
                runtimeFallbackDefinition.pinBreakThreshold = 1.2f;
                runtimeFallbackDefinition.stressIncreaseRate = 1.0f;
                runtimeFallbackDefinition.stressRecoveryRate = 1.8f;
                break;
        }

        return runtimeFallbackDefinition;
    }

    private void ConsumeBobbyPin()
    {
        if (!consumeBobbyPinOnBreak)
            return;

        if (useInventoryPinCount && bobbyPinDefinition && activePlayerInventory)
        {
            activePlayerInventory.RemoveItem(bobbyPinDefinition, 1);
            remainingPins = Mathf.Max(0, activePlayerInventory.GetTotalCount(bobbyPinDefinition));
            RefreshLockpickUi();
            return;
        }

        remainingPins = Mathf.Max(0, remainingPins - 1);
        RefreshLockpickUi();
    }

    private int ResolveStartingPinCount(PlayerInventory interactorInventory)
    {
        if (useInventoryPinCount && bobbyPinDefinition && interactorInventory)
            return Mathf.Max(0, interactorInventory.GetTotalCount(bobbyPinDefinition));

        return Mathf.Max(1, maxPins);
    }

    private float GetOrCreatePersistentTargetAngle(Container targetContainer)
    {
        Container resolvedContainer = ResolveTargetContainer(targetContainer);
        if (!resolvedContainer)
            return 0.0f;

        if (!persistentTargetAnglesByContainer.TryGetValue(resolvedContainer, out float storedAngle))
        {
            float maxPickAngle = GetActiveMaxPickAngle();
            return SetPersistentTargetAngle(resolvedContainer, Random.Range(-maxPickAngle, maxPickAngle));
        }

        float clampedAngle = Mathf.Clamp(storedAngle, -GetActiveMaxPickAngle(), GetActiveMaxPickAngle());
        persistentTargetAnglesByContainer[resolvedContainer] = clampedAngle;
        return clampedAngle;
    }

    private float SetPersistentTargetAngle(Container targetContainer, float angle)
    {
        Container resolvedContainer = ResolveTargetContainer(targetContainer);
        if (!resolvedContainer)
            return 0.0f;

        float maxPickAngle = GetActiveMaxPickAngle();
        float clampedAngle = Mathf.Clamp(angle, -maxPickAngle, maxPickAngle);
        persistentTargetAnglesByContainer[resolvedContainer] = clampedAngle;
        return clampedAngle;
    }

    private float GetActiveMaxPickAngle()
    {
        if (activeDefinition)
            return Mathf.Max(0.1f, activeDefinition.maxBobbyPinAngle);

        return Mathf.Max(0.1f, fallbackMaxPickAngle);
    }

    private int GetRequiredSkillForLock(Container targetContainer)
    {
        Container resolvedContainer = ResolveTargetContainer(targetContainer);
        if (!resolvedContainer)
            return 0;

        return GetRequiredSkillForLockType(resolvedContainer.GetLockType());
    }

    private int GetRequiredSkillForLockType(Container.LockType lockType)
    {
        switch (lockType)
        {
            case Container.LockType.VeryEasy:
                return 0;
            case Container.LockType.Easy:
                return easySkillRequirement;
            case Container.LockType.Medium:
                return mediumSkillRequirement;
            case Container.LockType.Hard:
                return hardSkillRequirement;
            case Container.LockType.VeryHard:
                return veryHardSkillRequirement;
            default:
                return 0;
        }
    }

    private void AwardLockpickSkillExperience()
    {
        if (!awardLockpickSkillExperience || !activeContainer)
            return;

        PlayerState playerState = GetInteractorPlayerState(activeInteractor);
        if (!playerState)
            return;

        float experienceAmount = GetLockpickSkillExperienceForLockType(activeContainer.GetLockType());
        if (experienceAmount <= 0f)
            return;

        playerState.AddSkillExperience(PlayerSkill.Lockpick, experienceAmount);
    }

    private float GetLockpickSkillExperienceForLockType(Container.LockType lockType)
    {
        switch (lockType)
        {
            case Container.LockType.VeryEasy:
                return veryEasyLockpickSkillExperience;
            case Container.LockType.Easy:
                return easyLockpickSkillExperience;
            case Container.LockType.Medium:
                return mediumLockpickSkillExperience;
            case Container.LockType.Hard:
                return hardLockpickSkillExperience;
            case Container.LockType.VeryHard:
                return veryHardLockpickSkillExperience;
            default:
                return 0f;
        }
    }

    private int GetInteractorSkill(GameObject interactor)
    {
        return GetInteractorSkill(GetInteractorPlayerState(interactor));
    }

    private static int GetInteractorSkill(PlayerState playerState)
    {
        if (!playerState)
            return 0;

        return Mathf.Clamp(playerState.GetLockpick(), 0, 100);
    }

    private static PlayerState GetInteractorPlayerState(GameObject interactor)
    {
        if (!interactor)
            return null;

        return interactor.GetComponentInParent<PlayerState>(true);
    }

    private static PlayerInventory GetInteractorInventory(GameObject interactor)
    {
        if (!interactor)
            return null;

        return interactor.GetComponentInParent<PlayerInventory>(true);
    }

    private void SetLockpickModeVisible(bool visible)
    {
        if (lockpickModeRoot)
            lockpickModeRoot.SetActive(visible);

        if (!visible)
        {
            RestoreLockpickModeRootPose();
            ResetVisuals();
        }
    }

    private void CacheLockpickModeRootPoseIfNeeded()
    {
        if (hasCachedLockpickModeRootPose || !lockpickModeRoot)
            return;

        Transform rootTransform = lockpickModeRoot.transform;
        cachedLockpickModeRootPosition = rootTransform.position;
        cachedLockpickModeRootRotation = rootTransform.rotation;
        hasCachedLockpickModeRootPose = true;
    }

    private void SetPlayerUiVisible(bool visible)
    {
        GameObject resolvedPlayerUiRoot = ResolvePlayerUiRoot();
        if (!resolvedPlayerUiRoot)
            return;

        if (!visible)
        {
            wasPlayerUiActiveBeforeLockpick = resolvedPlayerUiRoot.activeSelf;
            didHidePlayerUiForLockpick = true;

            CacheAndDisableNonLockpickUi(resolvedPlayerUiRoot);
            ForceLockpickUiVisible(resolvedPlayerUiRoot);

            return;
        }

        if (!didHidePlayerUiForLockpick)
            return;

        RestoreLockpickUiState();
        RestorePlayerUiChildren();
        resolvedPlayerUiRoot.SetActive(wasPlayerUiActiveBeforeLockpick);
        didHidePlayerUiForLockpick = false;
    }

    private GameObject ResolvePlayerUiRoot()
    {
        if (playerUiRoot && playerUiRoot.scene.IsValid() && playerUiRoot.scene.isLoaded)
            return playerUiRoot;

        CrosshairController crosshairController = FindFirstObjectInSceneIncludingInactive<CrosshairController>();
        Canvas crosshairCanvas = FindCanvasInParents(crosshairController ? crosshairController.transform : null);
        if (crosshairCanvas)
        {
            playerUiRoot = crosshairCanvas.gameObject;
            return playerUiRoot;
        }

        FalloutHUDController hudController = FindFirstObjectInSceneIncludingInactive<FalloutHUDController>();
        Canvas hudCanvas = FindCanvasInParents(hudController ? hudController.transform : null);
        if (hudCanvas)
        {
            playerUiRoot = hudCanvas.gameObject;
            return playerUiRoot;
        }

        GameObject[] candidates = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < candidates.Length; i++)
        {
            GameObject candidate = candidates[i];
            if (!candidate || candidate.name != "PlayerUI")
                continue;

            if (!candidate.scene.IsValid() || !candidate.scene.isLoaded)
                continue;

            if (candidate.transform.parent != null)
                continue;

            playerUiRoot = candidate;
            return playerUiRoot;
        }

        return null;
    }

    private GameObject ResolveLockpickUiRoot()
    {
        GameObject resolvedPlayerUiRoot = ResolvePlayerUiRoot();

        if (lockpickUiRoot && lockpickUiRoot.scene.IsValid() && lockpickUiRoot.scene.isLoaded)
            return lockpickUiRoot;

        if (resolvedPlayerUiRoot)
        {
            Transform sharedUiRoot = resolvedPlayerUiRoot.transform.parent;
            Transform lockpickUiTransform = null;

            if (sharedUiRoot)
                lockpickUiTransform = FindChildTransformByName(sharedUiRoot, "LockpickUI");

            if (!lockpickUiTransform)
                lockpickUiTransform = FindChildTransformByName(resolvedPlayerUiRoot.transform, "LockpickUI");

            if (lockpickUiTransform)
            {
                lockpickUiRoot = lockpickUiTransform.gameObject;
                return lockpickUiRoot;
            }
        }

        GameObject[] candidates = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < candidates.Length; i++)
        {
            GameObject candidate = candidates[i];
            if (!candidate || candidate.name != "LockpickUI")
                continue;

            if (!candidate.scene.IsValid() || !candidate.scene.isLoaded)
                continue;

            lockpickUiRoot = candidate;
            return lockpickUiRoot;
        }

        lockpickUiRoot = null;
        return lockpickUiRoot;
    }

    private void ResolveLockpickUiTextReferences()
    {
        GameObject resolvedLockpickUiRoot = ResolveLockpickUiRoot();
        if (!resolvedLockpickUiRoot)
            return;

        if (!lockpickSkillNumberText)
            lockpickSkillNumberText = FindChildComponentByNameInRoot<TMP_Text>("LockpickSkillNumberText", resolvedLockpickUiRoot.transform);

        if (!bobbyPinsNumberText)
            bobbyPinsNumberText = FindChildComponentByNameInRoot<TMP_Text>("BobbyPinsNumberText", resolvedLockpickUiRoot.transform);

        if (!lockLevelActualText)
            lockLevelActualText = FindChildComponentByNameInRoot<TMP_Text>("LockLevelActualText", resolvedLockpickUiRoot.transform);
    }

    private void RefreshLockpickUi()
    {
        ResolveLockpickUiTextReferences();

        SetTextIfChanged(lockpickSkillNumberText, GetInteractorSkill(activeInteractor).ToString());
        SetTextIfChanged(bobbyPinsNumberText, Mathf.Max(0, remainingPins).ToString());
        SetTextIfChanged(lockLevelActualText, GetCurrentLockLevelText());
    }

    private string GetCurrentLockLevelText()
    {
        if (!activeContainer)
            return string.Empty;

        return GetLockTypeLabel(activeContainer.GetLockType());
    }

    private void CacheAndDisableNonLockpickUi(GameObject resolvedPlayerUiRoot)
    {
        playerUiChildStates.Clear();

        GameObject resolvedLockpickUiRoot = ResolveLockpickUiRoot();
        if (!resolvedPlayerUiRoot.activeSelf)
            resolvedPlayerUiRoot.SetActive(true);

        Transform playerUiTransform = resolvedPlayerUiRoot.transform;
        for (int i = 0; i < playerUiTransform.childCount; i++)
        {
            GameObject childObject = playerUiTransform.GetChild(i).gameObject;
            playerUiChildStates.Add(new GameObjectState
            {
                GameObject = childObject,
                WasActiveSelf = childObject.activeSelf
            });

            if (resolvedLockpickUiRoot && childObject == resolvedLockpickUiRoot)
                continue;

            if (childObject.activeSelf)
                childObject.SetActive(false);
        }
    }

    private void ForceLockpickUiVisible(GameObject resolvedPlayerUiRoot)
    {
        GameObject resolvedLockpickUiRoot = ResolveLockpickUiRoot();
        if (!resolvedLockpickUiRoot)
            return;

        if (!resolvedPlayerUiRoot.activeSelf)
            resolvedPlayerUiRoot.SetActive(true);

        lockpickUiSubtreeStates.Clear();
        CacheGameObjectSubtreeState(resolvedLockpickUiRoot.transform, lockpickUiSubtreeStates);
        hasCachedLockpickUiSubtreeState = true;

        SetGameObjectSubtreeActive(resolvedLockpickUiRoot.transform, true);
    }

    private void RestorePlayerUiChildren()
    {
        for (int i = 0; i < playerUiChildStates.Count; i++)
        {
            GameObjectState state = playerUiChildStates[i];
            if (state.GameObject)
                state.GameObject.SetActive(state.WasActiveSelf);
        }

        playerUiChildStates.Clear();
    }

    private void RestoreLockpickUiState()
    {
        if (!hasCachedLockpickUiSubtreeState)
            return;

        for (int i = 0; i < lockpickUiSubtreeStates.Count; i++)
        {
            GameObjectState state = lockpickUiSubtreeStates[i];
            if (state.GameObject)
                state.GameObject.SetActive(state.WasActiveSelf);
        }

        lockpickUiSubtreeStates.Clear();
        hasCachedLockpickUiSubtreeState = false;
    }

    private void BindActivePlayerInventory()
    {
        if (!activePlayerInventory)
            return;

        activePlayerInventory.OnInventoryChanged -= HandleActiveInventoryChanged;
        activePlayerInventory.OnInventoryChanged += HandleActiveInventoryChanged;
    }

    private void UnbindActivePlayerInventory()
    {
        if (!activePlayerInventory)
            return;

        activePlayerInventory.OnInventoryChanged -= HandleActiveInventoryChanged;
    }

    private void HandleActiveInventoryChanged()
    {
        if (useInventoryPinCount && bobbyPinDefinition && activePlayerInventory)
            remainingPins = Mathf.Max(0, activePlayerInventory.GetTotalCount(bobbyPinDefinition));

        RefreshLockpickUi();
    }

    private GameObject ResolveSceneSun()
    {
        if (sceneSun && sceneSun.scene.IsValid() && sceneSun.scene.isLoaded)
            return sceneSun;

        if (RenderSettings.sun && RenderSettings.sun.gameObject.scene.IsValid() && RenderSettings.sun.gameObject.scene.isLoaded)
        {
            sceneSun = RenderSettings.sun.gameObject;
            return sceneSun;
        }

        GameObject[] candidates = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < candidates.Length; i++)
        {
            GameObject candidate = candidates[i];
            if (!candidate || candidate.name != "Sun")
                continue;

            if (!candidate.scene.IsValid() || !candidate.scene.isLoaded)
                continue;

            sceneSun = candidate;
            return sceneSun;
        }

        return null;
    }

    private void SetSceneSunVisible(bool visible)
    {
        GameObject resolvedSceneSun = ResolveSceneSun();
        if (!resolvedSceneSun)
            return;

        if (!visible)
        {
            wasSceneSunActiveBeforeLockpick = resolvedSceneSun.activeSelf;
            hasCachedSceneSunState = true;

            if (resolvedSceneSun.activeSelf)
                resolvedSceneSun.SetActive(false);

            return;
        }

        if (!hasCachedSceneSunState)
            return;

        resolvedSceneSun.SetActive(wasSceneSunActiveBeforeLockpick);
        hasCachedSceneSunState = false;
    }

    private Transform ResolveLockpickModeCameraAnchor()
    {
        if (!lockpickModeRoot)
            return null;

        if (lockpickModeCameraAnchor && lockpickModeCameraAnchor.IsChildOf(lockpickModeRoot.transform))
            return lockpickModeCameraAnchor;

        lockpickModeCameraAnchor = FindChildTransformByName(lockpickModeRoot.transform, "LockpickCameraAnchor");
        return lockpickModeCameraAnchor ? lockpickModeCameraAnchor : lockpickModeRoot.transform;
    }

    private static T FindFirstObjectInSceneIncludingInactive<T>() where T : Component
    {
        T[] candidates = Resources.FindObjectsOfTypeAll<T>();

        for (int i = 0; i < candidates.Length; i++)
        {
            T candidate = candidates[i];
            if (!candidate)
                continue;

            GameObject candidateObject = candidate.gameObject;
            if (!candidateObject.scene.IsValid() || !candidateObject.scene.isLoaded)
                continue;

            return candidate;
        }

        return null;
    }

    private static Canvas FindCanvasInParents(Transform current)
    {
        while (current)
        {
            Canvas canvas = current.GetComponent<Canvas>();
            if (canvas)
                return canvas;

            current = current.parent;
        }

        return null;
    }

    private Vector3 GetActiveLockpickCameraPosition()
    {
        Transform cameraAnchor = activeLockpickCameraAnchor ? activeLockpickCameraAnchor : lockpickCameraAnchor;
        if (!cameraAnchor)
            return Vector3.zero;

        return cameraAnchor.position + lockpickModeWorldOffset;
    }

    private Quaternion GetActiveLockpickCameraRotation()
    {
        Transform cameraAnchor = activeLockpickCameraAnchor ? activeLockpickCameraAnchor : lockpickCameraAnchor;
        return cameraAnchor ? cameraAnchor.rotation : Quaternion.identity;
    }

    private void AlignLockpickModeRootToActiveAnchor()
    {
        if (!lockpickModeRoot || !activeLockpickCameraAnchor)
            return;

        Transform rootTransform = lockpickModeRoot.transform;
        Transform modeCameraAnchor = ResolveLockpickModeCameraAnchor();
        if (!modeCameraAnchor)
            return;

        CacheLockpickModeRootPoseIfNeeded();

        Vector3 anchorLocalPosition = rootTransform.InverseTransformPoint(modeCameraAnchor.position);
        Quaternion anchorLocalRotation = Quaternion.Inverse(rootTransform.rotation) * modeCameraAnchor.rotation;

        // Move the shared rig so its internal viewing anchor lands on the active container anchor.
        Quaternion targetRootRotation = GetActiveLockpickCameraRotation() * Quaternion.Inverse(anchorLocalRotation);
        Vector3 targetRootPosition = GetActiveLockpickCameraPosition() - (targetRootRotation * anchorLocalPosition);

        rootTransform.SetPositionAndRotation(targetRootPosition, targetRootRotation);
    }

    private Container ResolveTargetContainer(Container targetContainer)
    {
        return targetContainer;
    }

    private Transform ResolveCameraAnchorForContainer(Container targetContainer)
    {
        Container resolvedContainer = ResolveTargetContainer(targetContainer);
        if (resolvedContainer)
        {
            Transform containerAnchor = FindChildTransformByName(resolvedContainer.transform, containerCameraAnchorName);
            if (containerAnchor)
                return containerAnchor;
        }

        return lockpickCameraAnchor;
    }

    private void RestoreLockpickModeRootPose()
    {
        if (!hasCachedLockpickModeRootPose || !lockpickModeRoot)
            return;

        lockpickModeRoot.transform.SetPositionAndRotation(cachedLockpickModeRootPosition, cachedLockpickModeRootRotation);
    }

    private void CacheInitialVisualPoseIfNeeded()
    {
        if (hasInitialVisualPose)
            return;

        if (bobbyPinPivot)
            bobbyPinInitialRotation = bobbyPinPivot.localRotation;

        if (cylinderPivot)
            cylinderInitialRotation = cylinderPivot.localRotation;

        hasInitialVisualPose = true;
    }

    private void SetPickAngleVisual(float pickAngle)
    {
        CacheInitialVisualPoseIfNeeded();
        if (!bobbyPinPivot)
            return;

        Vector3 axis = GetSafeAxis(bobbyPinAxis);
        bobbyPinPivot.localRotation = bobbyPinInitialRotation * Quaternion.AngleAxis(pickAngle, axis);
    }

    private void SetCylinderRotationVisual(float rotation)
    {
        CacheInitialVisualPoseIfNeeded();
        if (!cylinderPivot)
            return;

        Vector3 axis = GetSafeAxis(cylinderAxis);
        cylinderPivot.localRotation = cylinderInitialRotation * Quaternion.AngleAxis(rotation, axis);
    }

    private void ResetVisuals()
    {
        CacheInitialVisualPoseIfNeeded();

        if (bobbyPinPivot)
            bobbyPinPivot.localRotation = bobbyPinInitialRotation;

        if (cylinderPivot)
            cylinderPivot.localRotation = cylinderInitialRotation;
    }

    private static Vector3 GetSafeAxis(Vector3 axis)
    {
        if (axis.sqrMagnitude < 0.0001f)
            return Vector3.forward;

        return axis.normalized;
    }

    private static void CacheGameObjectSubtreeState(Transform root, List<GameObjectState> states)
    {
        if (!root)
            return;

        states.Add(new GameObjectState
        {
            GameObject = root.gameObject,
            WasActiveSelf = root.gameObject.activeSelf
        });

        for (int i = 0; i < root.childCount; i++)
            CacheGameObjectSubtreeState(root.GetChild(i), states);
    }

    private static void SetGameObjectSubtreeActive(Transform root, bool active)
    {
        if (!root)
            return;

        root.gameObject.SetActive(active);

        for (int i = 0; i < root.childCount; i++)
            SetGameObjectSubtreeActive(root.GetChild(i), active);
    }

    private static T FindChildComponentByNameInRoot<T>(string childName, Transform root) where T : Component
    {
        Transform childTransform = FindChildTransformByName(root, childName);
        if (!childTransform)
            return null;

        T component = childTransform.GetComponent<T>();
        return component ? component : childTransform.GetComponentInChildren<T>(true);
    }

    private static Transform FindChildTransformByName(Transform root, string childName)
    {
        if (!root || string.IsNullOrEmpty(childName))
            return null;

        if (root.name == childName)
            return root;

        for (int i = 0; i < root.childCount; i++)
        {
            Transform match = FindChildTransformByName(root.GetChild(i), childName);
            if (match)
                return match;
        }

        return null;
    }

    private static string GetLockTypeLabel(Container.LockType lockType)
    {
        switch (lockType)
        {
            case Container.LockType.VeryEasy:
                return "Very Easy";
            case Container.LockType.Easy:
                return "Easy";
            case Container.LockType.Medium:
                return "Medium";
            case Container.LockType.Hard:
                return "Hard";
            case Container.LockType.VeryHard:
                return "Very Hard";
            case Container.LockType.Key:
                return "Key";
            default:
                return "Locked";
        }
    }

    private static void SetTextIfChanged(TMP_Text textComponent, string value)
    {
        if (!textComponent)
            return;

        string resolvedValue = value ?? string.Empty;
        if (textComponent.text == resolvedValue)
            return;

        textComponent.text = resolvedValue;
    }

    private void OnGUI()
    {
        if (!showDebugOverlay || !isLockpickActive)
            return;

        Rect area = new Rect(16.0f, 16.0f, 320.0f, 142.0f);
        GUI.Box(area, "Lockpick Debug");
        GUI.Label(new Rect(28.0f, 44.0f, 300.0f, 20.0f), $"currentPickAngle: {currentPickAngle:0.00}");
        GUI.Label(new Rect(28.0f, 64.0f, 300.0f, 20.0f), $"targetAngle: {targetAngle:0.00}");
        GUI.Label(new Rect(28.0f, 84.0f, 300.0f, 20.0f), $"pinStress: {pinStress:0.00}");
        GUI.Label(new Rect(28.0f, 104.0f, 300.0f, 20.0f), $"remainingPins: {remainingPins}");
    }
}
