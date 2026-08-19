// imports
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.InputSystem;



// requirements
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(PlayerControls))]
[RequireComponent(typeof(PlayerAim))]
[RequireComponent(typeof(PlayerState))]



// class
public class PlayerMovement : MonoBehaviour
{
    private const float MinInputSqr = 0.001f;
    private const float MinDirectionSqr = 0.0001f;
    private const float MinUpwardSurfaceNormalY = 0.55f;
    private const float InvalidTime = -999f;
    private const string CombatHitboxLayerName = "CombatHitbox";
    private static readonly QueryTriggerInteraction QueryIgnore = QueryTriggerInteraction.Ignore;
    private static readonly RaycastHit[] MovementCastHits = new RaycastHit[32];
    private static readonly Collider[] GroundCheckHits = new Collider[32];
    private static int combatHitboxLayer = int.MinValue;

    
	// variables
    [System.Serializable]
    private class ReferencesCategory
    {
        public PlayerWeaponController weaponController;
        public PipBoyController pipBoyController;
        public Animator animator;
        public CameraRigOrbit orbit;
    }

    [System.Serializable]
    private class MovementSpeedsCategory
    {
        [FormerlySerializedAs("moveSpeed")]
        public float walkSpeed = 1.2f;
        [Min(0f)] public float crippledLegWalkSpeedModifier = 0.5f;
        public float runSpeed = 3f;
        public float sprintSpeed = 5f;
        public float crouchSpeed = 1.2f;
        public float combatUnarmedWalkSpeed = 1f;
        public float combatUnarmedRunSpeed = 2.5f;
        public float combatKnifeWalkSpeed = 1f;
        public float combatKnifeRunSpeed = 2.5f;
        public float combatTwoHandedWalkSpeed = 1f;
        public float combatTwoHandedRunSpeed = 2.5f;
        public float combatPistolWalkSpeed = 1f;
        public float combatPistolRunSpeed = 2.5f;
        public float combatPistolCrouchSpeed = 1f;
        public float combatShotgunWalkSpeed = 1f;
        public float combatShotgunRunSpeed = 2.5f;
        public float combatSubmachineGunWalkSpeed = 1f;
        public float combatSubmachineGunRunSpeed = 2.5f;
        public float combatRifleWalkSpeed = 1f;
        public float combatRifleRunSpeed = 2.5f;
        public float combatBowWalkSpeed = 1f;
        public float combatBowRunSpeed = 2.5f;
        public float combatSpecialWalkSpeed = 1f;
        public float combatSpecialRunSpeed = 2.5f;
        public float combatExplosiveWalkSpeed = 1f;
        public float combatExplosiveRunSpeed = 2.5f;
    }

    [System.Serializable]
    private class SprintTuningCategory
    {
        [FormerlySerializedAs("sprintStaminaDrainPerSecond")]
        public float sprintActionPointsDrainPerSecond = 12f;
        public float sprintForwardDeadzone = 0.15f;
    }

    [System.Serializable]
    private class RotationAnimationCategory
    {
        public float turnSpeed = 720f;
        public float stationaryTurnSpeed = 240f;
        public float animDampTime = 0.08f;
    }

    [System.Serializable]
    private class CombatMovementLockCategory
    {
        public string[] movementLockStates =
        {
            "Left Punch",
            "Right Punch",
            "Unarmed Block",
            "Left Strike",
            "Right Strike",
            "Two Handed Block",
            "Slash",
            "Stab",
            "Knife Block",
            "Knife Equip",
            "Knife Unequip",
            "Two Handed Equip",
            "Two Handed Unequip",
            "Pistol Equip",
            "Pistol Unequip",
            "Pistol Reload",
            "Pistol Crouch Reload",
            "Longarm Equip",
            "Longarm Unequip",
            "Longarm Reload",
            "Longarm Crouch Reload"
        };
        public int movementLockLayer = 0;
    }

    [System.Serializable]
    private class AirControlCategory
    {
        public float airControlMultiplier = 1f;
    }

    [System.Serializable]
    private class GroundCheckCategory
    {
        public Transform groundCheck;
        public float groundCheckRadius = 0.22f;
    }

    [System.Serializable]
    private class CollisionCategory
    {
        public LayerMask collisionLayers = ~0;
        public float collisionSkin = 0.02f;
        public float upwardSurfaceIgnoreNormalY = MinUpwardSurfaceNormalY;
    }

    [System.Serializable]
    private class CrouchColliderCategory
    {
        public Vector3 crouchCenter = new Vector3(0f, 0.7f, 0f);
        public float crouchRadius = 0.16f;
        public float crouchHeight = 1.38f;
    }

    [System.Serializable]
    private class GroundingCategory
    {
        public LayerMask groundLayers = ~0;
        public float coyoteTime = 0.12f;
        public float jumpBufferTime = 0.12f;
    }

    [System.Serializable]
    private class JumpTuningCategory
    {
        public JumpProfile[] jumpProfiles =
        {
            new JumpProfile { direction = JumpDirection.Idle,          takeoffDelay = 0.65f, jumpImpulse = 4f },
            new JumpProfile { direction = JumpDirection.Forward,       takeoffDelay = 0.00f, jumpImpulse = 4f },
            new JumpProfile { direction = JumpDirection.Backward,      takeoffDelay = 0.00f, jumpImpulse = 4f },
            new JumpProfile { direction = JumpDirection.Left,          takeoffDelay = 0.00f, jumpImpulse = 4f },
            new JumpProfile { direction = JumpDirection.Right,         takeoffDelay = 0.00f, jumpImpulse = 4f },
            new JumpProfile { direction = JumpDirection.ForwardLeft,   takeoffDelay = 0.00f, jumpImpulse = 4f },
            new JumpProfile { direction = JumpDirection.ForwardRight,  takeoffDelay = 0.00f, jumpImpulse = 4f },
            new JumpProfile { direction = JumpDirection.BackwardLeft,  takeoffDelay = 0.00f, jumpImpulse = 4f },
            new JumpProfile { direction = JumpDirection.BackwardRight, takeoffDelay = 0.00f, jumpImpulse = 4f }
        };
    }

    [System.Serializable]
    private class FootstepNoiseCategory
    {
        public bool emitFootsteps = true;
        [Min(0f)] public float crouchHearingRadius = 3.5f;
        [Min(0f)] public float walkHearingRadius = 9f;
        [Min(0f)] public float runHearingRadius = 16f;
        [Min(0f)] public float sprintHearingRadius = 24f;
        [Min(0.1f)] public float crouchStepDistance = 0.85f;
        [Min(0.1f)] public float walkStepDistance = 0.75f;
        [Min(0.1f)] public float runStepDistance = 1f;
        [Min(0.1f)] public float sprintStepDistance = 1.25f;
        [Min(0f)] public float minHorizontalSpeed = 0.15f;
    }

    [SerializeField] private ReferencesCategory references = new ReferencesCategory();
    [SerializeField] private MovementSpeedsCategory movementSpeeds = new MovementSpeedsCategory();
    [SerializeField] private SprintTuningCategory sprintTuning = new SprintTuningCategory();
    [SerializeField] private RotationAnimationCategory rotationAnimation = new RotationAnimationCategory();
    [SerializeField] private CombatMovementLockCategory combatMovementLock = new CombatMovementLockCategory();
    [SerializeField] private AirControlCategory airControl = new AirControlCategory();
    [SerializeField] private GroundCheckCategory groundCheckCategory = new GroundCheckCategory();
    [SerializeField] private CollisionCategory collision = new CollisionCategory();
    [SerializeField] private CrouchColliderCategory crouchCollider = new CrouchColliderCategory();
    [SerializeField] private GroundingCategory grounding = new GroundingCategory();
    [SerializeField] private JumpTuningCategory jumpTuning = new JumpTuningCategory();
    [SerializeField] private FootstepNoiseCategory footstepNoise = new FootstepNoiseCategory();

    private PlayerWeaponController weaponController
    {
        get => references.weaponController;
        set => references.weaponController = value;
    }

    private PipBoyController pipBoyController
    {
        get => references.pipBoyController;
        set => references.pipBoyController = value;
    }

    private Animator animator
    {
        get => references.animator;
        set => references.animator = value;
    }

    private CameraRigOrbit orbit
    {
        get => references.orbit;
        set => references.orbit = value;
    }

    private float walkSpeed => movementSpeeds.walkSpeed;
    private float crippledLegWalkSpeedModifier => movementSpeeds.crippledLegWalkSpeedModifier;
    private float runSpeed => movementSpeeds.runSpeed;
    private float combatUnarmedWalkSpeed => movementSpeeds.combatUnarmedWalkSpeed;
    private float combatUnarmedRunSpeed => movementSpeeds.combatUnarmedRunSpeed;
    private float combatKnifeWalkSpeed => movementSpeeds.combatKnifeWalkSpeed;
    private float combatKnifeRunSpeed => movementSpeeds.combatKnifeRunSpeed;
    private float combatTwoHandedWalkSpeed => movementSpeeds.combatTwoHandedWalkSpeed;
    private float combatTwoHandedRunSpeed => movementSpeeds.combatTwoHandedRunSpeed;
    private float combatPistolWalkSpeed => movementSpeeds.combatPistolWalkSpeed;
    private float combatPistolRunSpeed => movementSpeeds.combatPistolRunSpeed;
    private float combatPistolCrouchSpeed => movementSpeeds.combatPistolCrouchSpeed;
    private float combatShotgunWalkSpeed => movementSpeeds.combatShotgunWalkSpeed;
    private float combatShotgunRunSpeed => movementSpeeds.combatShotgunRunSpeed;
    private float combatSubmachineGunWalkSpeed => movementSpeeds.combatSubmachineGunWalkSpeed;
    private float combatSubmachineGunRunSpeed => movementSpeeds.combatSubmachineGunRunSpeed;
    private float combatRifleWalkSpeed => movementSpeeds.combatRifleWalkSpeed;
    private float combatRifleRunSpeed => movementSpeeds.combatRifleRunSpeed;
    private float combatBowWalkSpeed => movementSpeeds.combatBowWalkSpeed;
    private float combatBowRunSpeed => movementSpeeds.combatBowRunSpeed;
    private float combatSpecialWalkSpeed => movementSpeeds.combatSpecialWalkSpeed;
    private float combatSpecialRunSpeed => movementSpeeds.combatSpecialRunSpeed;
    private float combatExplosiveWalkSpeed => movementSpeeds.combatExplosiveWalkSpeed;
    private float combatExplosiveRunSpeed => movementSpeeds.combatExplosiveRunSpeed;
    private float sprintSpeed => movementSpeeds.sprintSpeed;
    private float crouchSpeed => movementSpeeds.crouchSpeed;

    private float sprintActionPointsDrainPerSecond => sprintTuning.sprintActionPointsDrainPerSecond;
    private float sprintForwardDeadzone => sprintTuning.sprintForwardDeadzone;

    private float turnSpeed => rotationAnimation.turnSpeed;
    private float stationaryTurnSpeed => rotationAnimation.stationaryTurnSpeed;
    private float animDampTime => rotationAnimation.animDampTime;

    private string[] movementLockStates => combatMovementLock.movementLockStates;
    private int movementLockLayer => combatMovementLock.movementLockLayer;

    private float airControlMultiplier => airControl.airControlMultiplier;

    private Transform groundCheck => groundCheckCategory.groundCheck;
    private float groundCheckRadius => groundCheckCategory.groundCheckRadius;

    private LayerMask collisionLayers => collision.collisionLayers;
    private float collisionSkin => collision.collisionSkin;
    private float upwardSurfaceIgnoreNormalY => collision.upwardSurfaceIgnoreNormalY;

    private LayerMask groundLayers => grounding.groundLayers;
    private float coyoteTime => grounding.coyoteTime;
    private float jumpBufferTime => grounding.jumpBufferTime;

    private JumpProfile[] jumpProfiles => jumpTuning.jumpProfiles;

    // Cache the player component.
    private PlayerControls p;

    // Cache the player state (action points/health points).
    private PlayerState playerState;

    // Cache the player inventory for over-encumbered movement gating.
    private PlayerInventory playerInventory;

    // Cache the input actions.
    private InputSystemActions controls;

    // Cache the Rigidbody for physics-safe movement and rotation.
    private Rigidbody rb;

    // Cache the capsule collider for movement gating.
    private CapsuleCollider capsule;

    // Cache the aim solver (used ONLY in combat mode).
    private PlayerAim aim;

    // Cache main camera transform for cheaper yaw lookups.
    private Transform mainCamTransform;

    // Store movement input.
    private Vector2 moveInput;

    // Track run state.
    private bool isRunning;

    // Track sprint held state (Left Shift hold).
    private bool sprintHeld;

    // Track sprint lock state (we are currently sprinting).
    private bool isSprinting;

    // Track whether sprint temporarily disabled combat mode.
    private bool combatModeSuppressedBySprint;

    // Track crouch state.
    private bool isCrouching;

    // Track pistol crouch state for animation.
    private bool isPistolCrouching;

    // Track longarm crouch state for animation.
    private bool isLongarmCrouching;

    // Track grounded state.
    private bool isGrounded;

    // Tracks whether we were grounded in the previous FixedUpdate.
    private bool wasGrounded;

    // Tracks whether we have left the ground since the current jump started.
    private bool hasLeftGroundSinceJump;

    // Time we were last grounded (for coyote time).
    private float lastGroundedTime;

    // Time jump was last pressed (for buffering).
    private float lastJumpPressedTime;

    // True when jump input has been buffered and is eligible to start a jump.
    private bool jumpStartQueued;

    // True when we have started a jump and are waiting to apply physics takeoff.
    private bool isWaitingForTakeoff;

    // The time (Time.time) at which we should apply the takeoff impulse.
    private float scheduledTakeoffTime;

    // True when we lock the blend tree direction for the whole jump.
    private bool lockJumpBlend;

    // Locked blend value for xVelocity during the jump.
    private float lockedJumpX;

    // Locked blend value for zVelocity during the jump.
    private float lockedJumpZ;

    // The impulse we will apply for the currently scheduled jump.
    private float scheduledJumpImpulse;

    // Cached horizontal velocity from the last grounded frame (used to preserve sprint momentum in air).
    private Vector3 lastGroundedHorizontalVel;

    // Animator hash for xVelocity.
    private static readonly int XVel = Animator.StringToHash("xVelocity");

    // Animator hash for zVelocity.
    private static readonly int ZVel = Animator.StringToHash("zVelocity");

    // Animator hash for IsRunning.
    private static readonly int IsRunning = Animator.StringToHash("IsRunning");

    // Animator hash for IsSprinting.
    private static readonly int IsSprinting = Animator.StringToHash("IsSprinting");

    // Animator hash for Jump trigger.
    private static readonly int JumpTrig = Animator.StringToHash("Jump");

    // Animator hash for IsGrounded.
    private static readonly int IsGrounded = Animator.StringToHash("IsGrounded");

    // Animator hash for IsCrouching.
    private static readonly int IsCrouching = Animator.StringToHash("IsCrouching");

    // Animator hash for IsPistolCrouching.
    private static readonly int IsPistolCrouching = Animator.StringToHash("IsPistolCrouching");

    // Animator hash for IsLongarmCrouching.
    private static readonly int IsLongarmCrouching = Animator.StringToHash("IsLongarmCrouching");

    // Animator hash for yVelocity.
    private static readonly int YVel = Animator.StringToHash("yVelocity");

    // Animator hash for IsFalling.
    private static readonly int IsFalling = Animator.StringToHash("IsFalling");

    // Animator hash for Land trigger.
    private static readonly int LandTrig = Animator.StringToHash("Land");

    // Animator hash for IsEquipping.
    private static readonly int IsEquippingParam = Animator.StringToHash("IsEquipping");

    // Keep delegates so we can unsubscribe cleanly.
    private System.Action<InputAction.CallbackContext> onMovePerformed;

    // Keep delegates so we can unsubscribe cleanly.
    private System.Action<InputAction.CallbackContext> onMoveCanceled;

    // Keep delegates so we can unsubscribe cleanly.
    private System.Action<InputAction.CallbackContext> onRunStarted;

    // Keep delegates so we can unsubscribe cleanly.
    private System.Action<InputAction.CallbackContext> onSprintStarted;

    // Keep delegates so we can unsubscribe cleanly.
    private System.Action<InputAction.CallbackContext> onSprintCanceled;

    // Keep delegates so we can unsubscribe cleanly.
    private System.Action<InputAction.CallbackContext> onJumpStarted;

    // Keep delegates so we can unsubscribe cleanly.
    private System.Action<InputAction.CallbackContext> onCrouchStarted;

    // Keep delegates so we can unsubscribe cleanly.
    private System.Action<InputAction.CallbackContext> onAttackStarted;

    // Keep delegates so we can unsubscribe cleanly.
    private System.Action<InputAction.CallbackContext> onHolsterPerformed;

    // Expose whether the player currently has movement input (intent, not physics velocity).
    public bool HasMovementInput => moveInput.sqrMagnitude > MinInputSqr;

    // Expose any crouch variant for systems that do not care which weapon-specific crouch is active.
    public bool IsAnyCrouching => isCrouching || isPistolCrouching || isLongarmCrouching;

    public bool IsNoClipEnabled => noClipEnabled;

    // The logical jump directions that match your 2D blend tree points.
    private enum JumpDirection
    {
        Idle,

        Forward,

        Backward,

        Left,

        Right,

        ForwardLeft,

        ForwardRight,

        BackwardLeft,

        BackwardRight
    }

    // Per-direction tuning so each animation can lift off at the right moment and reach the right height.
    [System.Serializable]
    private struct JumpProfile
    {
        // Which direction this profile applies to.
        public JumpDirection direction;

        // The delay (seconds) from Jump trigger to physics takeoff.
        public float takeoffDelay;

        // The upward impulse applied at takeoff.
        public float jumpImpulse;
    }

    // Cached “unit” blend points that match your tree (0,0), (0,1), (0,-1), (-1,0), (1,0), (+/-0.707, +/-0.707).
    private static readonly Vector2[] jumpBlendPoints =
    {
        new Vector2(0f, 0f),                      // Idle

        new Vector2(0f, 1f),                      // Forward

        new Vector2(0f, -1f),                     // Backward

        new Vector2(-1f, 0f),                     // Left

        new Vector2(1f, 0f),                      // Right

        new Vector2(-0.70710678f, 0.70710678f),   // ForwardLeft

        new Vector2(0.70710678f, 0.70710678f),    // ForwardRight

        new Vector2(-0.70710678f, -0.70710678f),  // BackwardLeft

        new Vector2(0.70710678f, -0.70710678f)    // BackwardRight
    };

    // Cached animator bools to avoid redundant SetBool calls.
    private bool lastAnimIsRunning;
    private bool lastAnimIsSprinting;
    private bool lastAnimIsCrouching;
    private bool lastAnimIsPistolCrouching;
    private bool lastAnimIsLongarmCrouching;
    private bool lastAnimIsGrounded;
    private bool lastAnimIsFalling;
    private bool lastAnimIsEquipping;

    // Cached hashes for movement lock state names.
    private int[] movementLockStateHashes;

    // Stores default standing capsule center so crouch can restore it.
    private Vector3 standingCapsuleCenter;

    // Stores default standing capsule radius so crouch can restore it.
    private float standingCapsuleRadius;

    // Stores default standing capsule height so crouch can restore it.
    private float standingCapsuleHeight;

    // Tracks whether crouch collider values are currently active.
    private bool usingCrouchCollider;

    // Tracks whether console no-clip is active.
    private bool noClipEnabled;

    // Cached physics settings restored when no-clip is disabled.
    private bool cachedNoClipUseGravity;
    private bool cachedNoClipDetectCollisions;
    private bool cachedNoClipCapsuleEnabled;
    private bool hasNoClipPhysicsCache;

    // Distance walked since the last emitted footstep.
    private float footstepDistanceAccumulator;

    // Tracks movement transitions so the first audible step does not fire immediately.
    private bool wasEmittingFootstepMovement;

    public struct FootstepSignal
    {
        public PlayerMovement sourceMovement;
        public PlayerState sourceState;
        public Transform sourceTransform;
        public Vector3 position;
        public float hearingRadius;
        public float loudness;
        public bool isCrouching;
        public bool isRunning;
        public bool isSprinting;
    }

    public static event System.Action<FootstepSignal> FootstepEmitted;



    // methods
    private void OnValidate()
    {
        EnsureInspectorCategories();
        ClampFootstepNoiseSettings();
    }


    private void EnsureInspectorCategories()
    {
        if (references == null) references = new ReferencesCategory();
        if (movementSpeeds == null) movementSpeeds = new MovementSpeedsCategory();
        if (sprintTuning == null) sprintTuning = new SprintTuningCategory();
        if (rotationAnimation == null) rotationAnimation = new RotationAnimationCategory();
        if (combatMovementLock == null) combatMovementLock = new CombatMovementLockCategory();
        if (airControl == null) airControl = new AirControlCategory();
        if (groundCheckCategory == null) groundCheckCategory = new GroundCheckCategory();
        if (collision == null) collision = new CollisionCategory();
        if (crouchCollider == null) crouchCollider = new CrouchColliderCategory();
        if (grounding == null) grounding = new GroundingCategory();
        if (jumpTuning == null) jumpTuning = new JumpTuningCategory();
        if (footstepNoise == null) footstepNoise = new FootstepNoiseCategory();
    }


    private void Awake()
    {
        EnsureInspectorCategories();
        ClampFootstepNoiseSettings();

        // Get the player component.
        p = GetComponent<PlayerControls>();

        // Get the controls reference.
        controls = p.Controls;

        // Get the player state.
        playerState = GetComponent<PlayerState>();

        // Get the player inventory.
        playerInventory = GetComponent<PlayerInventory>();
        if (!playerInventory)
            playerInventory = GetComponentInParent<PlayerInventory>();

        // Get the weapon controller.
        if (!weaponController)
            weaponController = GetComponentInChildren<PlayerWeaponController>(true);
        if (!weaponController)
            weaponController = GetComponentInParent<PlayerWeaponController>();

        // Auto-find PipBoyController if not set.
        if (!pipBoyController)
            pipBoyController = FindAnyObjectByType<PipBoyController>();

        // Get the Rigidbody.
        rb = GetComponent<Rigidbody>();

        // Get the capsule collider.
        capsule = GetComponent<CapsuleCollider>();

        CacheStandingColliderProfile();
        IgnoreOwnColliderContacts();

        // Get the aim solver.
        aim = GetComponent<PlayerAim>();

        // Cache main camera transform if available.
        Camera mainCam = Camera.main;
        if (mainCam)
            mainCamTransform = mainCam.transform;

        // Find animator if not assigned.
        if (!animator)
            animator = GetComponentInChildren<Animator>(true);

        // If orbit wasn't assigned, try to find one in the scene.
        if (!orbit)
            orbit = FindAnyObjectByType<CameraRigOrbit>();

        // Improve visual smoothness when using Rigidbody movement.
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        // Prevent physics from ever tilting the character on X/Z.
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        // Store movement input on performed.
        onMovePerformed = ctx =>
        {
            if (IsInputBlockedByPipBoy())
            {
                moveInput = Vector2.zero;
                return;
            }

            moveInput = ctx.ReadValue<Vector2>();
        };

        // Clear movement input on cancel.
        onMoveCanceled = _ => moveInput = Vector2.zero;

        // Toggle running state each time ToggleRun is pressed.
        onRunStarted = _ =>
        {
            // Stop while Pip-Boy is open.
            if (IsInputBlockedByPipBoy())
                return;

            // Stop if carrying a grabbed item.
            if (playerState && playerState.GetHasGrabbedItem())
                return;

            // Stop if over-encumbered.
            if (IsPlayerOverEncumbered())
                return;

            // Stop if either leg is crippled.
            if (HasCrippledLeg())
            {
                isRunning = false;
                return;
            }

            isRunning = !isRunning;
        };

        // Mark sprint held when Sprint is pressed (Left Shift hold).
        onSprintStarted = _ =>
        {
            if (IsInputBlockedByPipBoy())
            {
                sprintHeld = false;
                return;
            }

            if (IsPlayerOverEncumbered())
            {
                sprintHeld = false;
                return;
            }

            if (HasCrippledLeg())
            {
                sprintHeld = false;
                isSprinting = false;
                return;
            }

            sprintHeld = true;
        };

        // Clear sprint held when Sprint is released.
        onSprintCanceled = _ => sprintHeld = false;

        // Toggle crouch state each time Crouch is pressed.
        onCrouchStarted = _ =>
        {
            // Stop while Pip-Boy is open.
            if (IsInputBlockedByPipBoy())
                return;

            // Stop if carrying a grabbed item.
            if (playerState && playerState.GetHasGrabbedItem())
                return;

            bool canUsePistolCrouch = CanUsePistolCrouch();
            bool canUseLongarmCrouch = CanUseLongarmCrouch();
            if (canUsePistolCrouch)
            {
                // In pistol combat, C toggles pistol crouch only.
                isPistolCrouching = !isPistolCrouching;
                isLongarmCrouching = false;
                isCrouching = false;
            }
            else if (canUseLongarmCrouch)
            {
                // In longarm combat, C toggles longarm crouch only.
                isLongarmCrouching = !isLongarmCrouching;
                isPistolCrouching = false;
                isCrouching = false;
            }
            else
            {
                // Outside pistol/longarm combat, C toggles normal crouch.
                isCrouching = !isCrouching;

                // Entering normal crouch exits weapon-specific crouches.
                if (isCrouching)
                {
                    isPistolCrouching = false;
                    isLongarmCrouching = false;
                }
            }

            // Push crouch states to animator.
            if (animator)
            {
                animator.SetBool(IsCrouching, isCrouching);
                animator.SetBool(IsPistolCrouching, isPistolCrouching);
                animator.SetBool(IsLongarmCrouching, isLongarmCrouching);
            }

            ApplyCapsuleColliderForCrouchState();
        };

        // Buffer jump press time when Jump is pressed.
        onJumpStarted = _ =>
        {
            // Stop while Pip-Boy is open.
            if (IsInputBlockedByPipBoy())
                return;

            // Store the time for buffering.
            lastJumpPressedTime = Time.time;

            // Mark that we want to attempt a jump start.
            jumpStartQueued = true;
        };

        // Flip into combat mode when Attack is used while not in combat.
        onAttackStarted = _ =>
        {
            // Stop while Pip-Boy is open.
            if (IsInputBlockedByPipBoy())
                return;

            if (playerState && !playerState.GetCombatMode() && !playerState.GetHasGrabbedItem())
                playerState.SetCombatMode(true);
        };

        // Flip out of combat mode when Holster is performed while in combat.
        onHolsterPerformed = _ =>
        {
            // Stop while Pip-Boy is open.
            if (IsInputBlockedByPipBoy())
                return;

            if (playerState && playerState.GetCombatMode())
                playerState.SetCombatMode(false);
        };

        // Initialize timers to safe values.
        lastGroundedTime = InvalidTime;

        // Initialize timers to safe values.
        lastJumpPressedTime = InvalidTime;

        // Initialize jump state.
        isWaitingForTakeoff = false;

        // Initialize jump queue.
        jumpStartQueued = false;

        // Initialize blend lock.
        lockJumpBlend = false;

        // Initialize scheduled values.
        scheduledTakeoffTime = InvalidTime;

        // Initialize scheduled impulse.
        scheduledJumpImpulse = 0f;

        // Assume we start grounded until proven otherwise.
        wasGrounded = true;

        // We have not left the ground at start.
        hasLeftGroundSinceJump = false;

        // Ensure sprint defaults off.
        sprintHeld = false;

        // Ensure sprint state defaults off.
        isSprinting = false;

        // Ensure crouch defaults off.
        isCrouching = false;
        isPistolCrouching = false;
        isLongarmCrouching = false;

        // Ensure standing collider profile is active at startup.
        ApplyCapsuleColliderForCrouchState();

        // Push crouch state to animator at start.
        if (animator)
        {
            animator.SetBool(IsCrouching, isCrouching);
            animator.SetBool(IsPistolCrouching, isPistolCrouching);
            animator.SetBool(IsLongarmCrouching, isLongarmCrouching);
            animator.SetBool(IsFalling, false);
            animator.SetBool(IsEquippingParam, false);
            animator.SetFloat(YVel, 0f);
        }

        // Initialize animator bool cache.
        lastAnimIsCrouching = isCrouching;
        lastAnimIsPistolCrouching = isPistolCrouching;
        lastAnimIsLongarmCrouching = isLongarmCrouching;
        lastAnimIsRunning = false;
        lastAnimIsSprinting = false;
        lastAnimIsGrounded = false;
        lastAnimIsFalling = false;
        lastAnimIsEquipping = false;

        CacheMovementLockStateHashes();
    }


    private void OnEnable()
    {
        var playerControls = controls.Player;

        // Subscribe to movement input.
        playerControls.Move.performed += onMovePerformed;

        // Subscribe to movement cancel.
        playerControls.Move.canceled += onMoveCanceled;

        // Subscribe to run toggle press.
        playerControls.ToggleRun.performed += onRunStarted;

        // Subscribe to sprint press.
        playerControls.Sprint.performed += onSprintStarted;

        // Subscribe to sprint release.
        playerControls.Sprint.canceled += onSprintCanceled;

        // Subscribe to crouch press.
        playerControls.Crouch.started += onCrouchStarted;

        // Subscribe to jump press.
        playerControls.Jump.started += onJumpStarted;

        // Subscribe to attack press.
        playerControls.Attack.started += onAttackStarted;

        // Subscribe to holster performed (hold action).
        playerControls.Holster.performed += onHolsterPerformed;
    }


    private void OnDisable()
    {
        if (noClipEnabled)
            SetNoClipEnabled(false);

        var playerControls = controls.Player;

        // Unsubscribe to avoid duplicate callbacks.
        playerControls.Move.performed -= onMovePerformed;

        // Unsubscribe movement cancel.
        playerControls.Move.canceled -= onMoveCanceled;

        // Unsubscribe run toggle press.
        playerControls.ToggleRun.performed -= onRunStarted;

        // Unsubscribe sprint press.
        playerControls.Sprint.performed -= onSprintStarted;

        // Unsubscribe sprint release.
        playerControls.Sprint.canceled -= onSprintCanceled;

        // Unsubscribe crouch press.
        playerControls.Crouch.started -= onCrouchStarted;

        // Unsubscribe jump press.
        playerControls.Jump.started -= onJumpStarted;

        // Unsubscribe attack press.
        playerControls.Attack.started -= onAttackStarted;

        // Unsubscribe holster performed (hold action).
        playerControls.Holster.performed -= onHolsterPerformed;

        if (animator && lastAnimIsEquipping)
            animator.SetBool(IsEquippingParam, false);

        lastAnimIsEquipping = false;
    }


 private void FixedUpdate()
    {
        Rigidbody body = rb;
        PlayerState state = playerState;
        float dt = Time.fixedDeltaTime;

        // Block movement while Pip-Boy is open.
        if (IsInputBlockedByPipBoy())
        {
            moveInput = Vector2.zero;
            sprintHeld = false;
            isRunning = false;
            isSprinting = false;
            isPistolCrouching = false;
            isLongarmCrouching = false;
            jumpStartQueued = false;
            isWaitingForTakeoff = false;
            ResetFootstepEmission();

            body.linearVelocity = new Vector3(0f, body.linearVelocity.y, 0f);
            body.angularVelocity = Vector3.zero;
            ApplyCapsuleColliderForCrouchState();
            UpdateAnimator(Vector3.zero, state ? state.GetCombatMode() : false);
            return;
        }

        if (noClipEnabled)
        {
            HandleNoClipMovement(body, state, dt);
            return;
        }

        // Update grounded state before movement logic.
        UpdateGroundedState();

        // If we landed, clear any jump blend locks and pending takeoff.
        HandleLandingReset();

        // Clamp input so diagonals aren’t faster.
        Vector2 input = Vector2.ClampMagnitude(moveInput, 1f);

        // Check if we have movement input.
        bool hasMovement = input.sqrMagnitude > MinInputSqr;

        // Read combat mode from player state (not camera rig).
        bool isCombatMode = state ? state.GetCombatMode() : false;

        // Read grabbed-item state from player state.
        bool hasGrabbedItem = state && state.GetHasGrabbedItem();

        // Read inventory over-encumbered state.
        bool isOverEncumbered = IsPlayerOverEncumbered();

        // Read crippled leg state.
        bool hasCrippledLeg = HasCrippledLeg();

        // Force-disable run/sprint/crouch while carrying a grabbed item.
        if (hasGrabbedItem)
        {
            isRunning = false;
            isSprinting = false;
            isCrouching = false;
            isPistolCrouching = false;
            isLongarmCrouching = false;
        }

        // Force-disable all running and sprinting while over-encumbered.
        if (isOverEncumbered)
        {
            isRunning = false;
            isSprinting = false;
            sprintHeld = false;
        }

        // Force-disable all running and sprinting while one or both legs are crippled.
        if (hasCrippledLeg)
        {
            isRunning = false;
            isSprinting = false;
            sprintHeld = false;
        }

        // Stop movement input while combat lock animations are playing.
        if (IsMovementLockedByAnimation())
        {
            input = Vector2.zero;
            hasMovement = false;
        }

        // Determine if input is forward-only FOR START (positive forward, no strafe).
        bool forwardOnlyForStart =
            input.y > sprintForwardDeadzone &&
            Mathf.Abs(input.x) < sprintForwardDeadzone;

        // Determine if input is forward-enough FOR CONTINUE (allow strafe, but must still be pressing forward).
        bool forwardEnoughToContinue =
            input.y > sprintForwardDeadzone;

        // Determine if we have enough action points to sprint.
        bool hasActionPoints = state && state.GetActionPoints() > 0f;

        // Determine if sprint can START right now.
        bool canStartSprint =
            sprintHeld &&
            hasMovement &&
            forwardOnlyForStart &&
            isGrounded &&
            !hasGrabbedItem &&
            !isOverEncumbered &&
            !hasCrippledLeg &&
            hasActionPoints;

        // Determine if sprint can CONTINUE right now.
        bool canContinueSprint =
            sprintHeld &&
            hasMovement &&
            forwardEnoughToContinue &&
            isGrounded &&
            !hasGrabbedItem &&
            !isOverEncumbered &&
            !hasCrippledLeg &&
            hasActionPoints;

        // Start sprint when allowed and not already sprinting.
        if (canStartSprint && !isSprinting)
        {
            // Enter sprint state.
            isSprinting = true;
        }

        // Stop sprint if conditions are no longer valid for continuing.
        if (isSprinting && !canContinueSprint)
        {
            // Exit sprint state.
            isSprinting = false;
        }

        // Drain stamina while sprinting and stop if it runs out.
        if (isSprinting && state)
        {
            float actionPointsCost = sprintActionPointsDrainPerSecond * dt;
            if (!state.TrySpendActionPoints(actionPointsCost) || state.GetActionPoints() <= 0f)
                isSprinting = false;
        }

        // Suppress combat mode while sprinting, and restore after sprint ends if it was active.
        if (state)
        {
            bool isAnyCrouching = isCrouching || isPistolCrouching || isLongarmCrouching;
            if (isSprinting && !isAnyCrouching)
            {
                if (isCombatMode)
                {
                    state.SetCombatMode(false);
                    combatModeSuppressedBySprint = true;
                    isCombatMode = false;
                }
            }
            else if (combatModeSuppressedBySprint)
            {
                if (!hasGrabbedItem)
                    state.SetCombatMode(true);

                combatModeSuppressedBySprint = false;
                isCombatMode = state.GetCombatMode();
            }
        }

        // While sprinting, force forward-only input (no strafe) but allow mouse yaw to steer.
        if (isSprinting)
        {
            input = new Vector2(0f, Mathf.Max(0f, input.y));
            hasMovement = input.sqrMagnitude > MinInputSqr;
        }

        // Build a movement direction (NORMAL: camera yaw, COMBAT: orbit yaw).
        // This updates every FixedUpdate, so turning the camera (mouse) changes direction live.
        Vector3 moveDir = GetMoveDirection(input, isCombatMode);

        // Choose category-specific walk/run speeds in combat mode.
        float walkBaseSpeed = GetModifiedWalkSpeed(walkSpeed);
        float runBaseSpeed = runSpeed;
        PlayerWeaponController.WeaponCategory currentCategory = PlayerWeaponController.WeaponCategory.Unarmed;
        if (weaponController)
            currentCategory = weaponController.GetCurrentCategory();

        // While in combat, remap pistol crouch when the equipped category changes away from pistol:
        // - pistol -> longarm: keep crouched via longarm crouch
        // - pistol -> any non-longarm: fall back to normal crouch
        if (isCombatMode &&
            isPistolCrouching &&
            currentCategory != PlayerWeaponController.WeaponCategory.Pistol &&
            !hasGrabbedItem)
        {
            isPistolCrouching = false;

            if (IsLongarmCategory(currentCategory))
            {
                isLongarmCrouching = true;
                isCrouching = false;
            }
            else
            {
                isLongarmCrouching = false;
                isCrouching = true;
            }
        }

        // If combat mode is already active, convert normal crouch into longarm crouch for longarms.
        if (isCombatMode && isCrouching && IsLongarmCategory(currentCategory) && !hasGrabbedItem)
        {
            isCrouching = false;
            isPistolCrouching = false;
            isLongarmCrouching = true;
        }

        // If combat mode is already active, convert normal crouch into pistol crouch for pistols.
        if (isCombatMode &&
            isCrouching &&
            currentCategory == PlayerWeaponController.WeaponCategory.Pistol &&
            !hasGrabbedItem)
        {
            isCrouching = false;
            isLongarmCrouching = false;
            isPistolCrouching = true;
        }

        // If combat mode is no longer active while longarm crouching with a longarm equipped,
        // fall back to normal crouch.
        if (!isCombatMode &&
            isLongarmCrouching &&
            IsLongarmCategory(currentCategory) &&
            !hasGrabbedItem)
        {
            isCrouching = true;
            isLongarmCrouching = false;
            isPistolCrouching = false;
        }

        // If combat mode is no longer active while pistol crouching with a pistol equipped,
        // fall back to normal crouch.
        if (!isCombatMode &&
            isPistolCrouching &&
            currentCategory == PlayerWeaponController.WeaponCategory.Pistol &&
            !hasGrabbedItem)
        {
            isCrouching = true;
            isPistolCrouching = false;
            isLongarmCrouching = false;
        }

        if (isCombatMode && weaponController)
        {
            walkBaseSpeed = GetCombatWalkSpeed(currentCategory);
            runBaseSpeed = GetCombatRunSpeed(currentCategory);
        }

        bool pistolCrouchEligible =
            isCombatMode &&
            currentCategory == PlayerWeaponController.WeaponCategory.Pistol;

        bool longarmCrouchEligible =
            isCombatMode &&
            IsLongarmCategory(currentCategory);

        // Pistol crouch is only valid while pistol+combat conditions are true.
        if (!pistolCrouchEligible)
            isPistolCrouching = false;

        // Longarm crouch is only valid while longarm+combat conditions are true.
        if (!longarmCrouchEligible)
            isLongarmCrouching = false;

        // Keep crouch modes mutually exclusive.
        if (isPistolCrouching)
        {
            isCrouching = false;
            isLongarmCrouching = false;
        }

        // Keep crouch modes mutually exclusive.
        if (isLongarmCrouching)
        {
            isCrouching = false;
            isPistolCrouching = false;
        }

        ApplyCapsuleColliderForCrouchState();

        // Choose walk/run/sprint speed (sprint overrides run/crouch).
        float baseSpeed =
            isSprinting ? sprintSpeed :
            isPistolCrouching ? combatPistolCrouchSpeed :
            isLongarmCrouching ? crouchSpeed :
            isCrouching ? crouchSpeed :
            (isRunning && hasMovement) ? runBaseSpeed :
            walkBaseSpeed;

        // Build desired horizontal velocity (ground target).
        Vector3 desiredHorizontalVel = moveDir * baseSpeed;

        // Cache grounded horizontal velocity so air keeps sprint momentum.
        if (isGrounded)
            lastGroundedHorizontalVel = desiredHorizontalVel;

        // Preserve existing horizontal momentum when airborne, blending only if there is input.
        Vector3 currentHorizontalVel = new Vector3(body.linearVelocity.x, 0f, body.linearVelocity.z);
        
        Vector3 nextHorizontalVel = desiredHorizontalVel;

        if (!isGrounded)
        {
            if (hasMovement)
            {
                float takeoffSpeed = lastGroundedHorizontalVel.magnitude;
                Vector3 airborneTarget = moveDir * takeoffSpeed;
                nextHorizontalVel = Vector3.Lerp(currentHorizontalVel, airborneTarget, airControlMultiplier);
            }
            else
                nextHorizontalVel = currentHorizontalVel;
        }

        // Gate horizontal velocity against collisions to avoid flicker/jitter.
        if (!noClipEnabled)
            nextHorizontalVel = ConstrainHorizontalVelocity(nextHorizontalVel);

        // Apply horizontal velocity while preserving current Y velocity (gravity/jump).
        float nextVerticalVelocity = noClipEnabled ? 0f : body.linearVelocity.y;
        body.linearVelocity = new Vector3(nextHorizontalVel.x, nextVerticalVelocity, nextHorizontalVel.z);

        // Try to start jump animation if conditions allow.
        TryStartJumpAnimation(input);

        // If a takeoff is scheduled, apply it when its time arrives (deterministic, not event-driven).
        TryConsumeScheduledTakeoff();

        // Predict a near-future position for aim solving (horizontal only).
        Vector3 predictedPos = body.position + new Vector3(nextHorizontalVel.x, 0f, nextHorizontalVel.z) * dt;

        // Default to keeping current rotation.
        Quaternion desiredRot = body.rotation;

        // NORMAL MODE: rotate only while moving, and always to camera yaw (including while sprinting).
        if (!isCombatMode)
        {
            // Only rotate if we have movement input.
            if (hasMovement)
            {
                // Get yaw-only forward from the camera/orbit.
                Vector3 camForward = GetNormalModeYawForward();

                // If we have a valid forward, face it (mouse steers sprint direction).
                if (camForward.sqrMagnitude > MinDirectionSqr)
                    desiredRot = Quaternion.LookRotation(camForward, Vector3.up);
            }
        }

        // COMBAT MODE: rotate to aim solution unless manual MMB orbit is locking facing.
        if (isCombatMode)
        {
            // Determine if we should lock rotation during manual combat orbit.
            bool lockRotation = orbit && orbit.IsCombatManualOrbitHeld;

            // Ask the aim solver to compute facing from predicted position.
            if (!lockRotation && aim)
                aim.ComputeDesiredRotationFromAimTarget(predictedPos);

            // Use aim rotation when not locked and when aim has a solution.
            if (!lockRotation && aim && aim.HasAimSolution)
                desiredRot = aim.DesiredRotation;
        }

        // Rotate toward target with a capped angular speed to prevent snap-rotations.
        float activeTurnSpeed = hasMovement ? turnSpeed : stationaryTurnSpeed;
        Quaternion nextRot = Quaternion.RotateTowards(body.rotation, desiredRot, activeTurnSpeed * dt);

        // Stop any physics-added spin from collisions.
        body.angularVelocity = Vector3.zero;
        
        // Apply rotation through Rigidbody (no transform writes).
        body.MoveRotation(nextRot);

        // Update animator using horizontal velocity (for locomotion blend).
        UpdateAnimator(nextHorizontalVel, isCombatMode);

        UpdateFootstepEmission(nextHorizontalVel, dt);
    }

    private void HandleNoClipMovement(Rigidbody body, PlayerState state, float dt)
    {
        if (!body)
            return;

        Vector2 input = Vector2.ClampMagnitude(moveInput, 1f);
        bool hasMovement = input.sqrMagnitude > MinInputSqr;
        bool hasCrippledLeg = HasCrippledLeg();
        if (hasCrippledLeg)
        {
            isRunning = false;
            isSprinting = false;
            sprintHeld = false;
        }

        bool sprintingInNoClip = sprintHeld && hasMovement && !hasCrippledLeg;
        bool runningInNoClip = isRunning && hasMovement && !hasCrippledLeg;
        float speed = sprintingInNoClip ? sprintSpeed : runningInNoClip ? runSpeed : GetModifiedWalkSpeed(walkSpeed);
        Vector3 velocity = GetNoClipMoveDirection(input) * speed;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.MovePosition(body.position + velocity * dt);

        Vector3 facing = ResolveNoClipFacingForward();
        if (facing.sqrMagnitude > MinDirectionSqr)
        {
            Quaternion desiredRot = Quaternion.LookRotation(facing, Vector3.up);
            body.MoveRotation(Quaternion.RotateTowards(body.rotation, desiredRot, turnSpeed * dt));
        }

        isGrounded = true;
        wasGrounded = true;
        hasLeftGroundSinceJump = false;
        jumpStartQueued = false;
        isWaitingForTakeoff = false;
        scheduledTakeoffTime = InvalidTime;
        scheduledJumpImpulse = 0f;
        lockJumpBlend = false;
        isSprinting = sprintingInNoClip;

        bool isCombatMode = state ? state.GetCombatMode() : false;
        UpdateAnimator(new Vector3(velocity.x, 0f, velocity.z), isCombatMode);
        ResetFootstepEmission();
    }

    private Vector3 GetNoClipMoveDirection(Vector2 input)
    {
        if (input.sqrMagnitude <= MinInputSqr)
            return Vector3.zero;

        ResolveNoClipCameraBasis(out Vector3 forward, out Vector3 right);
        Vector3 direction = (right * input.x) + (forward * input.y);

        if (direction.sqrMagnitude <= MinDirectionSqr)
            return Vector3.zero;

        return Vector3.ClampMagnitude(direction, 1f);
    }

    private void ResolveNoClipCameraBasis(out Vector3 forward, out Vector3 right)
    {
        CameraRigOrbit orbitController = orbit;
        if (orbitController && orbitController.TryGetCameraForward(out Vector3 orbitForward))
        {
            forward = orbitForward.normalized;
            right = orbitController.YawPivot ? orbitController.YawPivot.right : Vector3.Cross(Vector3.up, forward);
            if (right.sqrMagnitude > MinDirectionSqr)
                right.Normalize();
            else
                right = transform.right;
            return;
        }

        if (!mainCamTransform && Camera.main)
            mainCamTransform = Camera.main.transform;

        if (mainCamTransform)
        {
            forward = mainCamTransform.forward.normalized;
            right = mainCamTransform.right.normalized;
            return;
        }

        forward = transform.forward;
        right = transform.right;
    }

    private Vector3 ResolveNoClipFacingForward()
    {
        ResolveNoClipCameraBasis(out Vector3 forward, out _);
        forward.y = 0f;

        if (forward.sqrMagnitude > MinDirectionSqr)
            return forward.normalized;

        return transform.forward;
    }


    private void UpdateFootstepEmission(Vector3 horizontalVelocity, float deltaTime)
    {
        if (footstepNoise == null || !footstepNoise.emitFootsteps || !isGrounded)
        {
            ResetFootstepEmission();
            return;
        }

        float speed = new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z).magnitude;
        bool movingAudibly = speed >= footstepNoise.minHorizontalSpeed;
        if (!movingAudibly)
        {
            ResetFootstepEmission();
            return;
        }

        float stepDistance = ResolveFootstepStepDistance();
        if (!wasEmittingFootstepMovement)
        {
            footstepDistanceAccumulator = stepDistance * 0.5f;
            wasEmittingFootstepMovement = true;
        }

        footstepDistanceAccumulator += speed * deltaTime;
        if (footstepDistanceAccumulator < stepDistance)
            return;

        footstepDistanceAccumulator = Mathf.Repeat(footstepDistanceAccumulator, stepDistance);
        EmitFootstepSignal(ResolveFootstepHearingRadius());
    }


    private void EmitFootstepSignal(float hearingRadius)
    {
        if (hearingRadius <= 0f)
            return;

        FootstepSignal signal = new FootstepSignal
        {
            sourceMovement = this,
            sourceState = playerState,
            sourceTransform = transform,
            position = rb ? rb.position : transform.position,
            hearingRadius = hearingRadius,
            loudness = Mathf.InverseLerp(footstepNoise.crouchHearingRadius, footstepNoise.sprintHearingRadius, hearingRadius),
            isCrouching = isCrouching || isPistolCrouching || isLongarmCrouching,
            isRunning = isRunning,
            isSprinting = isSprinting
        };

        FootstepEmitted?.Invoke(signal);
    }


    private void ResetFootstepEmission()
    {
        wasEmittingFootstepMovement = false;
        footstepDistanceAccumulator = 0f;
    }


    private float ResolveFootstepHearingRadius()
    {
        if (isSprinting)
            return footstepNoise.sprintHearingRadius;

        if (isCrouching || isPistolCrouching || isLongarmCrouching)
            return footstepNoise.crouchHearingRadius;

        return isRunning ? footstepNoise.runHearingRadius : footstepNoise.walkHearingRadius;
    }


    private float ResolveFootstepStepDistance()
    {
        if (isSprinting)
            return Mathf.Max(0.1f, footstepNoise.sprintStepDistance);

        if (isCrouching || isPistolCrouching || isLongarmCrouching)
            return Mathf.Max(0.1f, footstepNoise.crouchStepDistance);

        return Mathf.Max(0.1f, isRunning ? footstepNoise.runStepDistance : footstepNoise.walkStepDistance);
    }


    private void ClampFootstepNoiseSettings()
    {
        if (footstepNoise == null)
            return;

        footstepNoise.crouchHearingRadius = Mathf.Max(0f, footstepNoise.crouchHearingRadius);
        footstepNoise.walkHearingRadius = Mathf.Max(0f, footstepNoise.walkHearingRadius);
        footstepNoise.runHearingRadius = Mathf.Max(0f, footstepNoise.runHearingRadius);
        footstepNoise.sprintHearingRadius = Mathf.Max(0f, footstepNoise.sprintHearingRadius);
        footstepNoise.crouchStepDistance = Mathf.Max(0.1f, footstepNoise.crouchStepDistance);
        footstepNoise.walkStepDistance = Mathf.Max(0.1f, footstepNoise.walkStepDistance);
        footstepNoise.runStepDistance = Mathf.Max(0.1f, footstepNoise.runStepDistance);
        footstepNoise.sprintStepDistance = Mathf.Max(0.1f, footstepNoise.sprintStepDistance);
        footstepNoise.minHorizontalSpeed = Mathf.Max(0f, footstepNoise.minHorizontalSpeed);
    }


    private void UpdateGroundedState()
    {
        float now = Time.time;

        // Store previous grounded state.
        wasGrounded = isGrounded;

        // Choose a ground check position.
        Vector3 checkPos = groundCheck ? groundCheck.position : (rb.position + Vector3.up * 0.1f);

        // Perform a sphere check against ground layers.
        bool hit = CheckMovementSphere(checkPos, groundCheckRadius, groundLayers);

        // Store grounded state.
        isGrounded = hit;

        // If we are not grounded now but we were grounded last frame, we just left the ground.
        if (!isGrounded && wasGrounded)
            hasLeftGroundSinceJump = true;

        // If grounded, refresh coyote timer.
        if (isGrounded)
            lastGroundedTime = now;

        // Push grounded bool into animator only when it changes.
        if (animator && isGrounded != lastAnimIsGrounded)
        {
            animator.SetBool(IsGrounded, isGrounded);
            lastAnimIsGrounded = isGrounded;
        }
    }

    private void CacheMovementLockStateHashes()
    {
        if (movementLockStates == null || movementLockStates.Length == 0)
        {
            movementLockStateHashes = null;
            return;
        }

        movementLockStateHashes = new int[movementLockStates.Length];
        for (int i = 0; i < movementLockStates.Length; i++)
            movementLockStateHashes[i] = Animator.StringToHash(movementLockStates[i]);
    }

    private bool IsMovementLockedByAnimation()
    {
        if (!animator || movementLockStateHashes == null || movementLockStateHashes.Length == 0)
            return false;

        if (movementLockLayer < 0 || movementLockLayer >= animator.layerCount)
            return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(movementLockLayer);
        if (IsStateLocked(current))
            return true;

        if (animator.IsInTransition(movementLockLayer))
        {
            AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(movementLockLayer);
            if (IsStateLocked(next))
                return true;
        }

        return false;
    }

    private bool IsStateLocked(AnimatorStateInfo info)
    {
        for (int i = 0; i < movementLockStateHashes.Length; i++)
        {
            int hash = movementLockStateHashes[i];
            if (info.shortNameHash == hash || info.fullPathHash == hash)
                return true;
        }

        return false;
    }


    private void HandleLandingReset()
    {
        // Detect a landing transition: was not grounded, now grounded.
        bool landedThisFrame = !wasGrounded && isGrounded;

        // Stop if we didn't land or never actually left the ground.
        if (!landedThisFrame || !hasLeftGroundSinceJump) return;

        // Trigger landing animation when we touch down after being airborne.
        if (animator)
            animator.SetTrigger(LandTrig);

        // Clear the jump blend lock when grounded so locomotion can resume normally.
        lockJumpBlend = false;

        // Clear any pending takeoff (prevents “late” takeoffs after landing).
        isWaitingForTakeoff = false;

        // Clear the scheduled time.
        scheduledTakeoffTime = InvalidTime;

        // Clear the scheduled impulse.
        scheduledJumpImpulse = 0f;

        // Reset leave-ground flag for the next jump cycle.
        hasLeftGroundSinceJump = false;
    }


    private void TryStartJumpAnimation(Vector2 inputAtThisFrame)
    {
        float now = Time.time;

        // Stop if we already started a jump and are waiting for takeoff.
        if (isWaitingForTakeoff) return;

        // Stop if we don’t have a queued jump attempt.
        if (!jumpStartQueued) return;

        // Check if jump was pressed recently (buffer).
        bool hasBufferedJump = (now - lastJumpPressedTime) <= jumpBufferTime;

        // Check if we were grounded recently (coyote time).
        bool hasCoyote = (now - lastGroundedTime) <= coyoteTime;

        // Stop if the buffer expired.
        if (!hasBufferedJump) return;

        // Stop if we’re not allowed to jump yet.
        if (!hasCoyote) return;

        // We are consuming the jump request now.
        jumpStartQueued = false;

        // Reset per-jump leave-ground tracking.
        hasLeftGroundSinceJump = false;

        // Clear buffered time so we don’t double fire.
        lastJumpPressedTime = InvalidTime;

        // Pick which jump direction we will commit to (based on input at the moment we jump).
        JumpDirection dir = ResolveJumpDirection(inputAtThisFrame);

        // Get the blend point that matches that direction.
        Vector2 blend = GetBlendPointForDirection(dir);

        // Lock the blend tree direction so it can’t “slide” into another jump clip mid-air.
        lockJumpBlend = true;

        // Store the locked x.
        lockedJumpX = blend.x;

        // Store the locked z.
        lockedJumpZ = blend.y;

        // Fetch the per-direction timing/impulse.
        JumpProfile profile = GetProfile(dir);

        // Store the impulse we will use for this jump.
        scheduledJumpImpulse = profile.jumpImpulse;

        // Schedule takeoff deterministically (no animation events).
        scheduledTakeoffTime = now + Mathf.Max(0f, profile.takeoffDelay);

        // Tell the animator to enter Jump immediately.
        if (animator)
            animator.SetTrigger(JumpTrig);

        // Now wait for the scheduled takeoff moment to apply physics.
        isWaitingForTakeoff = true;
    }


    private void TryConsumeScheduledTakeoff()
    {
        float now = Time.time;

        // Stop if no takeoff is pending.
        if (!isWaitingForTakeoff) return;

        // Stop if it is not time yet.
        if (now < scheduledTakeoffTime) return;

        // Consume the takeoff now.
        isWaitingForTakeoff = false;

        // Zero current vertical velocity so jump height is consistent.
        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);

        // Apply an instant upward impulse using the per-direction value.
        rb.AddForce(Vector3.up * scheduledJumpImpulse, ForceMode.VelocityChange);
    }


    public void JumpTakeoff()
    {
        // Allow the event to still work if you forget to remove it, but do not depend on it.
        TryConsumeScheduledTakeoff();
    }


    private JumpDirection ResolveJumpDirection(Vector2 input)
    {
        // If input is basically zero, this is the idle jump.
        if (input.sqrMagnitude <= MinInputSqr)
            return JumpDirection.Idle;

        // Normalize input so direction selection is purely angular.
        Vector2 n = input.normalized;

        // Start with the best match as forward.
        JumpDirection bestDir = JumpDirection.Forward;

        // Start with a very low score.
        float bestScore = -999f;

        // Check forward.
        bestScore = Vector2.Dot(n, new Vector2(0f, 1f));

        // Check backward.
        TryUpdateBestDirection(n, new Vector2(0f, -1f), JumpDirection.Backward, ref bestDir, ref bestScore);

        // Check left.
        TryUpdateBestDirection(n, new Vector2(-1f, 0f), JumpDirection.Left, ref bestDir, ref bestScore);

        // Check right.
        TryUpdateBestDirection(n, new Vector2(1f, 0f), JumpDirection.Right, ref bestDir, ref bestScore);

        // Check forward-left.
        TryUpdateBestDirection(n, new Vector2(-0.70710678f, 0.70710678f), JumpDirection.ForwardLeft, ref bestDir, ref bestScore);

        // Check forward-right.
        TryUpdateBestDirection(n, new Vector2(0.70710678f, 0.70710678f), JumpDirection.ForwardRight, ref bestDir, ref bestScore);

        // Check backward-left.
        TryUpdateBestDirection(n, new Vector2(-0.70710678f, -0.70710678f), JumpDirection.BackwardLeft, ref bestDir, ref bestScore);

        // Check backward-right.
        TryUpdateBestDirection(n, new Vector2(0.70710678f, -0.70710678f), JumpDirection.BackwardRight, ref bestDir, ref bestScore);

        // Return the direction with the highest dot product.
        return bestDir;
    }


    private void TryUpdateBestDirection(Vector2 n, Vector2 candidate, JumpDirection candidateDir, ref JumpDirection bestDir, ref float bestScore)
    {
        // Score the candidate direction by dot product.
        float score = Vector2.Dot(n, candidate);

        // If this candidate is better, take it.
        if (score > bestScore)
        {
            // Store best direction.
            bestDir = candidateDir;

            // Store best score.
            bestScore = score;
        }
    }


    private Vector2 GetBlendPointForDirection(JumpDirection dir)
    {
        // Convert the enum into an index that matches our array ordering.
        int index = (int)dir;

        // Clamp the index for safety.
        index = Mathf.Clamp(index, 0, jumpBlendPoints.Length - 1);

        // Return the correct blend point.
        return jumpBlendPoints[index];
    }


    private JumpProfile GetProfile(JumpDirection dir)
    {
        // If profiles are missing, fall back to a safe default.
        if (jumpProfiles == null || jumpProfiles.Length == 0)
            return new JumpProfile { direction = dir, takeoffDelay = 0f, jumpImpulse = 6.5f };

        // Search for the matching profile.
        for (int i = 0; i < jumpProfiles.Length; i++)
        {
            // If this profile matches the direction, return it.
            if (jumpProfiles[i].direction == dir)
                return jumpProfiles[i];
        }

        // If no match, return a safe default.
        return new JumpProfile { direction = dir, takeoffDelay = 0f, jumpImpulse = 6.5f };
    }


    private Vector3 GetMoveDirection(Vector2 input, bool isCombatMode)
    {
        // COMBAT MODE: use orbit yaw (orbit may be mouse-driven).
        if (isCombatMode && orbit)
        {
            // Build a yaw-only rotation from the orbit's yaw degrees.
            Quaternion yawRotation = Quaternion.Euler(0f, orbit.CurrentYaw, 0f);

            // Build forward from yaw (ground-plane).
            Vector3 forward = yawRotation * Vector3.forward;

            // Build right from yaw (ground-plane).
            Vector3 right = yawRotation * Vector3.right;

            // Combine input into a world direction (W/S = forward/back, A/D = left/right).
            Vector3 dir = (right * input.x) + (forward * input.y);

            // Clamp to prevent diagonals exceeding length 1.
            dir = Vector3.ClampMagnitude(dir, 1f);

            // Return the final movement direction.
            return dir;
        }

        // NORMAL MODE: always use CAMERA YAW so W/S/A/D never depend on character facing.
        Vector3 camForward = GetNormalModeYawForward();

        // If we cannot resolve a camera forward, fall back to world axes.
        if (camForward.sqrMagnitude <= MinDirectionSqr)
            return Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);

        // Build the camera-right direction from the flattened camera-forward.
        Vector3 camRight = Vector3.Cross(Vector3.up, camForward).normalized;

        // Combine input into a world direction (W/S = cam forward/back, A/D = cam left/right).
        Vector3 normalDir = (camRight * input.x) + (camForward * input.y);

        // Clamp to prevent diagonals exceeding length 1.
        normalDir = Vector3.ClampMagnitude(normalDir, 1f);

        // Return the final movement direction.
        return normalDir;
    }


    private Vector3 GetNormalModeYawForward()
    {
        CameraRigOrbit orbitController = orbit;

        // Prefer orbit yaw pivot if available (this is your real camera yaw driver).
        if (orbitController && orbitController.YawPivot)
        {
            // Read the pivot forward direction.
            Vector3 f = orbitController.YawPivot.forward;

            // Flatten to ground plane.
            f.y = 0f;

            // If valid, return normalized.
            if (f.sqrMagnitude > MinDirectionSqr)
                return f.normalized;
        }

        // Fall back to main camera forward if orbit pivot is missing.
        if (!mainCamTransform && Camera.main)
            mainCamTransform = Camera.main.transform;

        if (mainCamTransform)
        {
            // Read the camera forward direction.
            Vector3 f = mainCamTransform.forward;

            // Flatten to ground plane.
            f.y = 0f;

            // If valid, return normalized.
            if (f.sqrMagnitude > MinDirectionSqr)
                return f.normalized;
        }

        // If everything fails, return zero.
        return Vector3.zero;
    }


    private void UpdateAnimator(Vector3 worldHorizontalVel, bool isCombatMode)
    {
        Animator anim = animator;
        Rigidbody body = rb;

        // Stop if no animator.
        if (!anim) return;

        // Feed vertical velocity into animator so jump/fall state machines can blend by Y speed.
        float yVelocity = body ? body.linearVelocity.y : 0f;
        anim.SetFloat(YVel, yVelocity);

        // Falling means airborne and moving downward.
        bool animFalling = !isGrounded && yVelocity < -0.01f;
        if (animFalling != lastAnimIsFalling)
        {
            anim.SetBool(IsFalling, animFalling);
            lastAnimIsFalling = animFalling;
        }

        // Choose which rotation defines "forward" for animation.
        Quaternion referenceRotation = transform.rotation;

        // In combat mode, prefer aim rotation for strafe blend.
        if (isCombatMode && aim && aim.HasAimSolution)
            referenceRotation = aim.DesiredRotation;

        // Convert world horizontal velocity into the chosen local space.
        Vector3 localVel = Quaternion.Inverse(referenceRotation) * worldHorizontalVel;

        // Choose category-specific walk/run speeds in combat mode.
        float walkBaseSpeed = GetModifiedWalkSpeed(walkSpeed);
        float runBaseSpeed = runSpeed;
        PlayerWeaponController.WeaponCategory currentCategory = PlayerWeaponController.WeaponCategory.Unarmed;

        if (isCombatMode && weaponController)
        {
            currentCategory = weaponController.GetCurrentCategory();
            walkBaseSpeed = GetCombatWalkSpeed(currentCategory);
            runBaseSpeed = GetCombatRunSpeed(currentCategory);
        }

        float normalizer =
            isSprinting ? sprintSpeed :
            isPistolCrouching ? combatPistolCrouchSpeed :
            isLongarmCrouching ? crouchSpeed :
            isCrouching ? crouchSpeed :
            isRunning ? runBaseSpeed :
            walkBaseSpeed;

        // Prevent divide-by-zero.
        if (normalizer <= MinDirectionSqr) normalizer = 1f;

        // Normalize x for strafe.
        float x = Mathf.Clamp(localVel.x / normalizer, -1f, 1f);

        // Normalize z for forward.
        float z = Mathf.Clamp(localVel.z / normalizer, -1f, 1f);

        // If sprinting, force “forward only” in the blend (this is your positive zVelocity rule).
        if (isSprinting)
        {
            // Remove strafe while sprinting.
            x = 0f;

            // Force forward.
            z = 1f;
        }

        // If we are mid-jump, force the blend tree to stay on the chosen jump clip.
        if (lockJumpBlend)
        {
            // Override x with the locked jump direction.
            x = lockedJumpX;

            // Override z with the locked jump direction.
            z = lockedJumpZ;

            // Push x into blend tree without damping (keeps the clip stable).
            anim.SetFloat(XVel, x);

            // Push z into blend tree without damping (keeps the clip stable).
            anim.SetFloat(ZVel, z);
        }
        else
        {
            // Push x into blend tree.
            anim.SetFloat(XVel, x, animDampTime, Time.fixedDeltaTime);

            // Push z into blend tree.
            anim.SetFloat(ZVel, z, animDampTime, Time.fixedDeltaTime);
        }

        bool hasMovement = worldHorizontalVel.sqrMagnitude > MinInputSqr;
        bool animRunning = isRunning && !isSprinting && hasMovement;
        bool animSprinting = isSprinting && hasMovement;

        if (animRunning != lastAnimIsRunning)
        {
            anim.SetBool(IsRunning, animRunning);
            lastAnimIsRunning = animRunning;
        }

        if (animSprinting != lastAnimIsSprinting)
        {
            anim.SetBool(IsSprinting, animSprinting);
            lastAnimIsSprinting = animSprinting;
        }

        if (isCrouching != lastAnimIsCrouching)
        {
            anim.SetBool(IsCrouching, isCrouching);
            lastAnimIsCrouching = isCrouching;
        }

        if (isPistolCrouching != lastAnimIsPistolCrouching)
        {
            anim.SetBool(IsPistolCrouching, isPistolCrouching);
            lastAnimIsPistolCrouching = isPistolCrouching;
        }

        if (isLongarmCrouching != lastAnimIsLongarmCrouching)
        {
            anim.SetBool(IsLongarmCrouching, isLongarmCrouching);
            lastAnimIsLongarmCrouching = isLongarmCrouching;
        }

        bool animEquipping = weaponController && weaponController.IsEquipAnimationPlaying();
        if (animEquipping != lastAnimIsEquipping)
        {
            anim.SetBool(IsEquippingParam, animEquipping);
            lastAnimIsEquipping = animEquipping;
        }
    }


    private Vector3 ConstrainHorizontalVelocity(Vector3 desiredHorizontalVel)
    {
        CapsuleCollider capsuleRef = capsule;
        if (!capsuleRef) return desiredHorizontalVel;

        Transform self = transform;
        float dt = Time.fixedDeltaTime;
        float invDt = 1f / dt;

        Vector3 horizontal = new Vector3(desiredHorizontalVel.x, 0f, desiredHorizontalVel.z);
        float speed = horizontal.magnitude;
        if (speed <= MinDirectionSqr) return desiredHorizontalVel;

        float distance = speed * dt;
        Vector3 center = self.TransformPoint(capsuleRef.center);
        Vector3 lossy = self.lossyScale;

        float radiusScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z));
        float heightScale = Mathf.Abs(lossy.y);
        float radius = Mathf.Max(MinInputSqr, capsuleRef.radius * radiusScale);
        float height = Mathf.Max(radius * 2f, capsuleRef.height * heightScale);
        float half = Mathf.Max(0f, (height * 0.5f) - radius);

        Vector3 up = self.up;
        Vector3 p1 = center + up * half;
        Vector3 p2 = center - up * half;
        Vector3 dir = horizontal.normalized;

        if (CapsuleCastMovement(p1, p2, radius, dir, distance + collisionSkin, collisionLayers, out RaycastHit hit))
        {
            float allowed = Mathf.Max(0f, hit.distance - collisionSkin);
            Vector3 constrained = dir * (allowed * invDt);

            Vector3 slide = Vector3.ProjectOnPlane(horizontal, hit.normal);
            if (slide.sqrMagnitude > MinDirectionSqr)
            {
                Vector3 slideDir = slide.normalized;
                float slideDist = slide.magnitude * dt;
                if (CapsuleCastMovement(p1, p2, radius, slideDir, slideDist + collisionSkin, collisionLayers, out RaycastHit slideHit))
                {
                    float slideAllowed = Mathf.Max(0f, slideHit.distance - collisionSkin);
                    constrained = slideDir * (slideAllowed * invDt);
                }
                else
                    constrained = slide;
            }

            return constrained;
        }

        return desiredHorizontalVel;
    }

    private void IgnoreOwnColliderContacts()
    {
        Collider[] ownColliders = GetComponentsInChildren<Collider>(true);
        if (ownColliders == null || ownColliders.Length < 2)
            return;

        for (int i = 0; i < ownColliders.Length; i++)
        {
            Collider first = ownColliders[i];
            if (!first)
                continue;

            for (int j = i + 1; j < ownColliders.Length; j++)
            {
                Collider second = ownColliders[j];
                if (second && second != first)
                    Physics.IgnoreCollision(first, second, true);
            }
        }
    }

    private bool CheckMovementSphere(Vector3 position, float radius, LayerMask layerMask)
    {
        int filteredLayerMask = FilterMovementLayerMask(layerMask);
        int hitCount = Physics.OverlapSphereNonAlloc(position, radius, GroundCheckHits, filteredLayerMask, QueryIgnore);

        if (hitCount >= GroundCheckHits.Length)
        {
            Collider[] allHits = Physics.OverlapSphere(position, radius, filteredLayerMask, QueryIgnore);
            return ContainsMovementBlockingCollider(allHits, allHits.Length);
        }

        return ContainsMovementBlockingCollider(GroundCheckHits, hitCount);
    }

    private bool CapsuleCastMovement(
        Vector3 point1,
        Vector3 point2,
        float radius,
        Vector3 direction,
        float maxDistance,
        LayerMask layerMask,
        out RaycastHit nearestHit)
    {
        int filteredLayerMask = FilterMovementLayerMask(layerMask);
        int hitCount = Physics.CapsuleCastNonAlloc(
            point1,
            point2,
            radius,
            direction,
            MovementCastHits,
            maxDistance,
            filteredLayerMask,
            QueryIgnore);

        if (hitCount >= MovementCastHits.Length)
        {
            RaycastHit[] allHits = Physics.CapsuleCastAll(
                point1,
                point2,
                radius,
                direction,
                maxDistance,
                filteredLayerMask,
                QueryIgnore);

            return TryGetNearestMovementHit(allHits, allHits.Length, out nearestHit);
        }

        return TryGetNearestMovementHit(MovementCastHits, hitCount, out nearestHit);
    }

    private bool ContainsMovementBlockingCollider(Collider[] hits, int hitCount)
    {
        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = hits[i];
            hits[i] = null;

            if (!ShouldIgnoreMovementCollider(hit))
                return true;
        }

        return false;
    }

    private bool TryGetNearestMovementHit(RaycastHit[] hits, int hitCount, out RaycastHit nearestHit)
    {
        nearestHit = new RaycastHit();
        bool found = false;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hits[i];
            hits[i] = new RaycastHit();

            if (ShouldIgnoreMovementHit(hit) || hit.distance >= nearestDistance)
                continue;

            nearestHit = hit;
            nearestDistance = hit.distance;
            found = true;
        }

        return found;
    }

    private bool ShouldIgnoreMovementHit(RaycastHit hit)
    {
        if (ShouldIgnoreMovementCollider(hit.collider))
            return true;

        return hit.normal.y >= upwardSurfaceIgnoreNormalY;
    }

    private bool ShouldIgnoreMovementCollider(Collider hit)
    {
        if (!hit)
            return true;

        if (hit.transform.IsChildOf(transform))
            return true;

        int layer = ResolveCombatHitboxLayer();
        return layer >= 0 && hit.gameObject.layer == layer;
    }

    private static int FilterMovementLayerMask(LayerMask layerMask)
    {
        int mask = layerMask;
        int layer = ResolveCombatHitboxLayer();
        if (layer >= 0)
            mask &= ~(1 << layer);

        return mask;
    }

    private static int ResolveCombatHitboxLayer()
    {
        if (combatHitboxLayer == int.MinValue)
            combatHitboxLayer = LayerMask.NameToLayer(CombatHitboxLayerName);

        return combatHitboxLayer;
    }

    private void CacheStandingColliderProfile()
    {
        CapsuleCollider capsuleRef = capsule;
        if (!capsuleRef)
            return;

        standingCapsuleCenter = capsuleRef.center;
        standingCapsuleRadius = capsuleRef.radius;
        standingCapsuleHeight = capsuleRef.height;
        usingCrouchCollider = false;
    }

    private void ApplyCapsuleColliderForCrouchState()
    {
        if (noClipEnabled)
            return;

        usingCrouchCollider =
            isCrouching ||
            isPistolCrouching ||
            isLongarmCrouching;
    }

    public void SetNoClipEnabled(bool value)
    {
        if (noClipEnabled == value)
            return;

        noClipEnabled = value;

        Rigidbody body = rb ? rb : GetComponent<Rigidbody>();
        CapsuleCollider capsuleRef = capsule ? capsule : GetComponent<CapsuleCollider>();

        if (value)
        {
            if (body)
            {
                cachedNoClipUseGravity = body.useGravity;
                cachedNoClipDetectCollisions = body.detectCollisions;
                body.useGravity = false;
                body.detectCollisions = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            if (capsuleRef)
            {
                cachedNoClipCapsuleEnabled = capsuleRef.enabled;
                capsuleRef.enabled = false;
            }

            hasNoClipPhysicsCache = true;
            isGrounded = true;
            wasGrounded = true;
            hasLeftGroundSinceJump = false;
            jumpStartQueued = false;
            isWaitingForTakeoff = false;
            scheduledTakeoffTime = InvalidTime;
            scheduledJumpImpulse = 0f;
            lockJumpBlend = false;
            isSprinting = false;
            ResetFootstepEmission();
            return;
        }

        if (body && hasNoClipPhysicsCache)
        {
            body.useGravity = cachedNoClipUseGravity;
            body.detectCollisions = cachedNoClipDetectCollisions;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
        }

        if (capsuleRef && hasNoClipPhysicsCache)
            capsuleRef.enabled = cachedNoClipCapsuleEnabled;

        hasNoClipPhysicsCache = false;
        ApplyCapsuleColliderForCrouchState();
    }

    public bool ToggleNoClip()
    {
        SetNoClipEnabled(!noClipEnabled);
        return noClipEnabled;
    }

    private bool CanUsePistolCrouch()
    {
        PlayerState state = playerState;
        if (!state || !state.GetCombatMode())
            return false;

        PlayerWeaponController controller = weaponController;
        if (!controller)
            return false;

        return controller.GetCurrentCategory() == PlayerWeaponController.WeaponCategory.Pistol;
    }

    private bool CanUseLongarmCrouch()
    {
        PlayerState state = playerState;
        if (!state || !state.GetCombatMode())
            return false;

        PlayerWeaponController controller = weaponController;
        if (!controller)
            return false;

        return IsLongarmCategory(controller.GetCurrentCategory());
    }

    private bool IsPlayerOverEncumbered()
    {
        PlayerInventory inventory = playerInventory;
        if (!inventory)
        {
            inventory = GetComponent<PlayerInventory>();
            if (!inventory)
                inventory = GetComponentInParent<PlayerInventory>();
            if (!inventory)
                inventory = FindAnyObjectByType<PlayerInventory>();

            playerInventory = inventory;
        }

        return inventory && inventory.IsOverEncumbered();
    }

    private float GetModifiedWalkSpeed(float baseWalkSpeed)
    {
        if (!HasCrippledLeg())
            return baseWalkSpeed;

        return baseWalkSpeed * Mathf.Max(0f, crippledLegWalkSpeedModifier);
    }

    private bool HasCrippledLeg()
    {
        PlayerState state = playerState;
        return state && (state.GetLeftLegCrippled() || state.GetRightLegCrippled());
    }

    private static bool IsLongarmCategory(PlayerWeaponController.WeaponCategory category)
    {
        return
            category == PlayerWeaponController.WeaponCategory.Shotgun ||
            category == PlayerWeaponController.WeaponCategory.SubmachineGun ||
            category == PlayerWeaponController.WeaponCategory.Rifle;
    }

    private bool IsInputBlockedByPipBoy()
    {
        if (UI.ConsoleController.IsOpen)
            return true;

        if (UI.LevelUpUIController.IsInputBlockActive())
            return true;

        PipBoyController controller = pipBoyController;
        if (!controller)
        {
            controller = FindAnyObjectByType<PipBoyController>();
            pipBoyController = controller;
        }

        return controller && controller.IsOpen();
    }

    private float GetCombatWalkSpeed(PlayerWeaponController.WeaponCategory category)
    {
        switch (category)
        {
            case PlayerWeaponController.WeaponCategory.Unarmed:
                return GetModifiedWalkSpeed(combatUnarmedWalkSpeed);
            case PlayerWeaponController.WeaponCategory.Knife:
                return GetModifiedWalkSpeed(combatKnifeWalkSpeed);
            case PlayerWeaponController.WeaponCategory.TwoHanded:
                return GetModifiedWalkSpeed(combatTwoHandedWalkSpeed);
            case PlayerWeaponController.WeaponCategory.Pistol:
                return GetModifiedWalkSpeed(combatPistolWalkSpeed);
            case PlayerWeaponController.WeaponCategory.Shotgun:
                return GetModifiedWalkSpeed(combatShotgunWalkSpeed);
            case PlayerWeaponController.WeaponCategory.SubmachineGun:
                return GetModifiedWalkSpeed(combatSubmachineGunWalkSpeed);
            case PlayerWeaponController.WeaponCategory.Rifle:
                return GetModifiedWalkSpeed(combatRifleWalkSpeed);
            case PlayerWeaponController.WeaponCategory.Bow:
                return GetModifiedWalkSpeed(combatBowWalkSpeed);
            case PlayerWeaponController.WeaponCategory.Special:
                return GetModifiedWalkSpeed(combatSpecialWalkSpeed);
            case PlayerWeaponController.WeaponCategory.Explosive:
                return GetModifiedWalkSpeed(combatExplosiveWalkSpeed);
            default:
                return GetModifiedWalkSpeed(walkSpeed);
        }
    }

    private float GetCombatRunSpeed(PlayerWeaponController.WeaponCategory category)
    {
        switch (category)
        {
            case PlayerWeaponController.WeaponCategory.Unarmed:
                return combatUnarmedRunSpeed;
            case PlayerWeaponController.WeaponCategory.Knife:
                return combatKnifeRunSpeed;
            case PlayerWeaponController.WeaponCategory.TwoHanded:
                return combatTwoHandedRunSpeed;
            case PlayerWeaponController.WeaponCategory.Pistol:
                return combatPistolRunSpeed;
            case PlayerWeaponController.WeaponCategory.Shotgun:
                return combatShotgunRunSpeed;
            case PlayerWeaponController.WeaponCategory.SubmachineGun:
                return combatSubmachineGunRunSpeed;
            case PlayerWeaponController.WeaponCategory.Rifle:
                return combatRifleRunSpeed;
            case PlayerWeaponController.WeaponCategory.Bow:
                return combatBowRunSpeed;
            case PlayerWeaponController.WeaponCategory.Special:
                return combatSpecialRunSpeed;
            case PlayerWeaponController.WeaponCategory.Explosive:
                return combatExplosiveRunSpeed;
            default:
                return runSpeed;
        }
    }
}
