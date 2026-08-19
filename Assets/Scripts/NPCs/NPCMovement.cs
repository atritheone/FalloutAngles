using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;
using UnityEngine.Serialization;

[RequireComponent(typeof(Rigidbody))]
public class NPCMovement : MonoBehaviour
{
    private const float MinInputSqr = 0.001f;
    private const float MinDirectionSqr = 0.0001f;
    private const float MinUpwardSurfaceNormalY = 0.55f;
    private const string CombatHitboxLayerName = "CombatHitbox";
    private static readonly QueryTriggerInteraction QueryIgnore = QueryTriggerInteraction.Ignore;
    private static readonly RaycastHit[] MovementCastHits = new RaycastHit[32];
    private static readonly Collider[] GroundCheckHits = new Collider[32];
    private static int combatHitboxLayer = int.MinValue;

    [System.Serializable]
    private class ReferencesCategory
    {
        public NPCState npcState;
        public NPCWeaponController weaponController;
        public NPCAim aim;
        public Animator animator;
        public NavMeshAgent navMeshAgent;
        public NavMeshSurface navMeshSurface;
    }

    [System.Serializable]
    private class MovementSpeedsCategory
    {
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
    private class MovementStateCategory
    {
        public bool isRunning;
        public bool isSprinting;
        public bool isCrouching;
        public bool isPistolCrouching;
        public bool isLongarmCrouching;
    }

    [System.Serializable]
    private class SprintTuningCategory
    {
        public float sprintActionPointsDrainPerSecond = 12f;
        public float sprintForwardDeadzone = 0.15f;
        public bool suppressCombatModeWhileSprinting;
    }

    [System.Serializable]
    private class RotationAnimationCategory
    {
        public bool rotateToAimInCombat = true;
        public float turnSpeed = 720f;
        public float stationaryTurnSpeed = 240f;
        public float animDampTime = 0.08f;
    }

    [System.Serializable]
    private class DestinationCategory
    {
        public bool useNavMeshAgentWhenAvailable = true;
        public bool clearDestinationWhenReached = true;
        public bool buildSurfaceOnAwakeIfMissing = true;
        public float destinationStoppingDistance = 0.65f;
        public float destinationRepathInterval = 0.25f;
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

    [SerializeField] private ReferencesCategory references = new ReferencesCategory();
    [SerializeField] private MovementSpeedsCategory movementSpeeds = new MovementSpeedsCategory();
    [SerializeField] private MovementStateCategory movementState = new MovementStateCategory();
    [SerializeField] private SprintTuningCategory sprintTuning = new SprintTuningCategory();
    [SerializeField] private RotationAnimationCategory rotationAnimation = new RotationAnimationCategory();
    [SerializeField] private DestinationCategory destinationSettings = new DestinationCategory();
    [SerializeField] private CombatMovementLockCategory combatMovementLock = new CombatMovementLockCategory();
    [SerializeField] private AirControlCategory airControl = new AirControlCategory();
    [SerializeField] private GroundCheckCategory groundCheckCategory = new GroundCheckCategory();
    [SerializeField] private CollisionCategory collision = new CollisionCategory();
    [SerializeField] private CrouchColliderCategory crouchCollider = new CrouchColliderCategory();
    [SerializeField] private GroundingCategory grounding = new GroundingCategory();
    [SerializeField] private JumpTuningCategory jumpTuning = new JumpTuningCategory();

    [SerializeField, HideInInspector, FormerlySerializedAs("npcState")] private NPCState legacyNpcState;
    [SerializeField, HideInInspector, FormerlySerializedAs("weaponController")] private NPCWeaponController legacyWeaponController;
    [SerializeField, HideInInspector, FormerlySerializedAs("aim")] private NPCAim legacyAim;
    [SerializeField, HideInInspector, FormerlySerializedAs("animator")] private Animator legacyAnimator;
    [SerializeField, HideInInspector, FormerlySerializedAs("navMeshAgent")] private NavMeshAgent legacyNavMeshAgent;
    [SerializeField, HideInInspector, FormerlySerializedAs("groundCheck")] private Transform legacyGroundCheck;
    [SerializeField, HideInInspector, FormerlySerializedAs("speeds")] private MovementSpeedsCategory legacyMovementSpeeds = new MovementSpeedsCategory();
    [SerializeField, HideInInspector, FormerlySerializedAs("isRunning")] private bool legacyIsRunning;
    [SerializeField, HideInInspector, FormerlySerializedAs("isSprinting")] private bool legacyIsSprinting;
    [SerializeField, HideInInspector, FormerlySerializedAs("isCrouching")] private bool legacyIsCrouching;
    [SerializeField, HideInInspector, FormerlySerializedAs("isPistolCrouching")] private bool legacyIsPistolCrouching;
    [SerializeField, HideInInspector, FormerlySerializedAs("isLongarmCrouching")] private bool legacyIsLongarmCrouching;
    [SerializeField, HideInInspector, FormerlySerializedAs("sprintActionPointsDrainPerSecond")] private float legacySprintActionPointsDrainPerSecond = 12f;
    [SerializeField, HideInInspector, FormerlySerializedAs("rotateToAimInCombat")] private bool legacyRotateToAimInCombat = true;
    [SerializeField, HideInInspector, FormerlySerializedAs("suppressCombatModeWhileSprinting")] private bool legacySuppressCombatModeWhileSprinting;
    [SerializeField, HideInInspector, FormerlySerializedAs("turnSpeed")] private float legacyTurnSpeed = 720f;
    [SerializeField, HideInInspector, FormerlySerializedAs("stationaryTurnSpeed")] private float legacyStationaryTurnSpeed = 240f;
    [SerializeField, HideInInspector, FormerlySerializedAs("animDampTime")] private float legacyAnimDampTime = 0.08f;
    [SerializeField, HideInInspector, FormerlySerializedAs("useNavMeshAgentWhenAvailable")] private bool legacyUseNavMeshAgentWhenAvailable = true;
    [SerializeField, HideInInspector, FormerlySerializedAs("clearDestinationWhenReached")] private bool legacyClearDestinationWhenReached = true;
    [SerializeField, HideInInspector, FormerlySerializedAs("destinationStoppingDistance")] private float legacyDestinationStoppingDistance = 0.65f;
    [SerializeField, HideInInspector, FormerlySerializedAs("destinationRepathInterval")] private float legacyDestinationRepathInterval = 0.25f;
    [SerializeField, HideInInspector, FormerlySerializedAs("groundCheckRadius")] private float legacyGroundCheckRadius = 0.22f;
    [SerializeField, HideInInspector, FormerlySerializedAs("groundLayers")] private LayerMask legacyGroundLayers = ~0;
    [SerializeField, HideInInspector, FormerlySerializedAs("coyoteTime")] private float legacyCoyoteTime = 0.12f;
    [SerializeField, HideInInspector, FormerlySerializedAs("collisionLayers")] private LayerMask legacyCollisionLayers = ~0;
    [SerializeField, HideInInspector, FormerlySerializedAs("collisionSkin")] private float legacyCollisionSkin = 0.02f;
    [SerializeField, HideInInspector, FormerlySerializedAs("crouchCenter")] private Vector3 legacyCrouchCenter = new Vector3(0f, 0.7f, 0f);
    [SerializeField, HideInInspector, FormerlySerializedAs("crouchRadius")] private float legacyCrouchRadius = 0.16f;
    [SerializeField, HideInInspector, FormerlySerializedAs("crouchHeight")] private float legacyCrouchHeight = 1.38f;
    [SerializeField, HideInInspector, FormerlySerializedAs("jumpImpulse")] private float legacyJumpImpulse = 6.5f;

    private NPCState npcState
    {
        get => references.npcState;
        set => references.npcState = value;
    }

    private NPCWeaponController weaponController
    {
        get => references.weaponController;
        set => references.weaponController = value;
    }

    private NPCAim aim
    {
        get => references.aim;
        set => references.aim = value;
    }

    private Animator animator
    {
        get => references.animator;
        set => references.animator = value;
    }

    private NavMeshAgent navMeshAgent
    {
        get => references.navMeshAgent;
        set => references.navMeshAgent = value;
    }

    private NavMeshSurface navMeshSurface
    {
        get => references.navMeshSurface;
        set => references.navMeshSurface = value;
    }

    private Transform groundCheck
    {
        get => groundCheckCategory.groundCheck;
        set => groundCheckCategory.groundCheck = value;
    }

    private bool isRunning
    {
        get => movementState.isRunning;
        set => movementState.isRunning = value;
    }

    private bool isSprinting
    {
        get => movementState.isSprinting;
        set => movementState.isSprinting = value;
    }

    private bool isCrouching
    {
        get => movementState.isCrouching;
        set => movementState.isCrouching = value;
    }

    private bool isPistolCrouching
    {
        get => movementState.isPistolCrouching;
        set => movementState.isPistolCrouching = value;
    }

    private bool isLongarmCrouching
    {
        get => movementState.isLongarmCrouching;
        set => movementState.isLongarmCrouching = value;
    }

    private float sprintActionPointsDrainPerSecond => sprintTuning.sprintActionPointsDrainPerSecond;
    private float sprintForwardDeadzone => sprintTuning.sprintForwardDeadzone;
    private bool suppressCombatModeWhileSprinting => sprintTuning.suppressCombatModeWhileSprinting;
    private bool rotateToAimInCombat => rotationAnimation.rotateToAimInCombat;
    private float turnSpeed => rotationAnimation.turnSpeed;
    private float stationaryTurnSpeed => rotationAnimation.stationaryTurnSpeed;
    private float animDampTime => rotationAnimation.animDampTime;
    private bool useNavMeshAgentWhenAvailable => destinationSettings.useNavMeshAgentWhenAvailable;
    private bool clearDestinationWhenReached => destinationSettings.clearDestinationWhenReached;
    private bool buildSurfaceOnAwakeIfMissing => destinationSettings.buildSurfaceOnAwakeIfMissing;
    private float destinationStoppingDistance => destinationSettings.destinationStoppingDistance;
    private float destinationRepathInterval => destinationSettings.destinationRepathInterval;
    private string[] movementLockStates => combatMovementLock.movementLockStates;
    private int movementLockLayer => combatMovementLock.movementLockLayer;
    private float airControlMultiplier => airControl.airControlMultiplier;
    private float groundCheckRadius => groundCheckCategory.groundCheckRadius;
    private LayerMask groundLayers => grounding.groundLayers;
    private float coyoteTime => grounding.coyoteTime;
    private float jumpBufferTime => grounding.jumpBufferTime;
    private LayerMask collisionLayers => collision.collisionLayers;
    private float collisionSkin => collision.collisionSkin;
    private float upwardSurfaceIgnoreNormalY => collision.upwardSurfaceIgnoreNormalY;
    private JumpProfile[] jumpProfiles => jumpTuning.jumpProfiles;

    private Rigidbody rb;
    private CapsuleCollider capsule;
    private Vector3 manualMoveDirection;
    private Vector3 currentWorldMoveDirection;
    private Vector3 destination;
    private bool hasDestination;
    private bool jumpQueued;
    private bool isGrounded;
    private bool wasGrounded;
    private bool hasLeftGroundSinceJump;
    private float lastGroundedTime;
    private float lastJumpQueuedTime = -999f;
    private float lastRepathTime = -999f;
    private Vector3 standingCapsuleCenter;
    private float standingCapsuleRadius;
    private float standingCapsuleHeight;
    private bool usingCrouchCollider;
    private bool combatModeSuppressedBySprint;

    private bool lastAnimIsRunning;
    private bool lastAnimIsSprinting;
    private bool lastAnimIsCrouching;
    private bool lastAnimIsPistolCrouching;
    private bool lastAnimIsLongarmCrouching;
    private bool lastAnimIsGrounded;
    private bool lastAnimIsFalling;
    private int[] movementLockStateHashes;

    private static readonly int XVel = Animator.StringToHash("xVelocity");
    private static readonly int ZVel = Animator.StringToHash("zVelocity");
    private static readonly int IsRunning = Animator.StringToHash("IsRunning");
    private static readonly int IsSprinting = Animator.StringToHash("IsSprinting");
    private static readonly int JumpTrig = Animator.StringToHash("Jump");
    private static readonly int IsGrounded = Animator.StringToHash("IsGrounded");
    private static readonly int IsCrouching = Animator.StringToHash("IsCrouching");
    private static readonly int IsPistolCrouching = Animator.StringToHash("IsPistolCrouching");
    private static readonly int IsLongarmCrouching = Animator.StringToHash("IsLongarmCrouching");
    private static readonly int YVel = Animator.StringToHash("yVelocity");
    private static readonly int IsFalling = Animator.StringToHash("IsFalling");
    private static readonly int LandTrig = Animator.StringToHash("Land");

    public bool HasMovementInput => hasDestination || manualMoveDirection.sqrMagnitude > MinInputSqr;
    public bool HasDestination => hasDestination;
    public Vector3 Destination => destination;
    public Vector3 CurrentWorldMoveDirection => currentWorldMoveDirection;
    public bool IsGroundedNow => isGrounded;
    public bool IsRunningNow => isRunning;
    public bool IsSprintingNow => isSprinting;
    public bool IsCrouchingNow => isCrouching || isPistolCrouching || isLongarmCrouching;
    public bool HasNavMeshAgentComponent => navMeshAgent;
    public bool HasNavMeshSurfaceComponent => navMeshSurface;
    public NavMeshSurface NavigationSurface => navMeshSurface;

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

    [System.Serializable]
    private struct JumpProfile
    {
        public JumpDirection direction;
        public float takeoffDelay;
        public float jumpImpulse;
    }

    private static readonly Vector2[] jumpBlendPoints =
    {
        new Vector2(0f, 0f),
        new Vector2(0f, 1f),
        new Vector2(0f, -1f),
        new Vector2(-1f, 0f),
        new Vector2(1f, 0f),
        new Vector2(-0.70710678f, 0.70710678f),
        new Vector2(0.70710678f, 0.70710678f),
        new Vector2(-0.70710678f, -0.70710678f),
        new Vector2(0.70710678f, -0.70710678f)
    };

    private void Awake()
    {
        EnsureInspectorCategories();
        MigrateLegacyInspectorData();
        BuildMovementLockStateHashes();
        ResolveReferences();
        EnsureNavMeshSurfaceReady();
        ConfigureNavMeshAgent();
        CacheStandingColliderProfile();
        IgnoreOwnColliderContacts();
        UpdateGroundedState();
    }

    private void Reset()
    {
        EnsureInspectorCategories();
        ResolveReferences();
    }

    private void OnValidate()
    {
        EnsureInspectorCategories();
        MigrateLegacyInspectorData();
        BuildMovementLockStateHashes();
    }

    private void EnsureInspectorCategories()
    {
        if (references == null) references = new ReferencesCategory();
        if (movementSpeeds == null) movementSpeeds = new MovementSpeedsCategory();
        if (movementState == null) movementState = new MovementStateCategory();
        if (sprintTuning == null) sprintTuning = new SprintTuningCategory();
        if (rotationAnimation == null) rotationAnimation = new RotationAnimationCategory();
        if (destinationSettings == null) destinationSettings = new DestinationCategory();
        if (combatMovementLock == null) combatMovementLock = new CombatMovementLockCategory();
        if (airControl == null) airControl = new AirControlCategory();
        if (groundCheckCategory == null) groundCheckCategory = new GroundCheckCategory();
        if (collision == null) collision = new CollisionCategory();
        if (crouchCollider == null) crouchCollider = new CrouchColliderCategory();
        if (grounding == null) grounding = new GroundingCategory();
        if (jumpTuning == null) jumpTuning = new JumpTuningCategory();
        if (legacyMovementSpeeds == null) legacyMovementSpeeds = new MovementSpeedsCategory();
    }

    private void MigrateLegacyInspectorData()
    {
        if (!HasLegacyInspectorData())
            return;

        references.npcState = legacyNpcState;
        references.weaponController = legacyWeaponController;
        references.aim = legacyAim;
        references.animator = legacyAnimator;
        references.navMeshAgent = legacyNavMeshAgent;

        movementSpeeds.walkSpeed = legacyMovementSpeeds.walkSpeed;
        movementSpeeds.runSpeed = legacyMovementSpeeds.runSpeed;
        movementSpeeds.sprintSpeed = legacyMovementSpeeds.sprintSpeed;
        movementSpeeds.crouchSpeed = legacyMovementSpeeds.crouchSpeed;
        movementSpeeds.combatUnarmedWalkSpeed = legacyMovementSpeeds.combatUnarmedWalkSpeed;
        movementSpeeds.combatUnarmedRunSpeed = legacyMovementSpeeds.combatUnarmedRunSpeed;
        movementSpeeds.combatKnifeWalkSpeed = legacyMovementSpeeds.combatKnifeWalkSpeed;
        movementSpeeds.combatKnifeRunSpeed = legacyMovementSpeeds.combatKnifeRunSpeed;
        movementSpeeds.combatTwoHandedWalkSpeed = legacyMovementSpeeds.combatTwoHandedWalkSpeed;
        movementSpeeds.combatTwoHandedRunSpeed = legacyMovementSpeeds.combatTwoHandedRunSpeed;
        movementSpeeds.combatPistolWalkSpeed = legacyMovementSpeeds.combatPistolWalkSpeed;
        movementSpeeds.combatPistolRunSpeed = legacyMovementSpeeds.combatPistolRunSpeed;
        movementSpeeds.combatPistolCrouchSpeed = legacyMovementSpeeds.combatPistolCrouchSpeed;
        movementSpeeds.combatShotgunWalkSpeed = legacyMovementSpeeds.combatShotgunWalkSpeed;
        movementSpeeds.combatShotgunRunSpeed = legacyMovementSpeeds.combatShotgunRunSpeed;
        movementSpeeds.combatSubmachineGunWalkSpeed = legacyMovementSpeeds.combatSubmachineGunWalkSpeed;
        movementSpeeds.combatSubmachineGunRunSpeed = legacyMovementSpeeds.combatSubmachineGunRunSpeed;
        movementSpeeds.combatRifleWalkSpeed = legacyMovementSpeeds.combatRifleWalkSpeed;
        movementSpeeds.combatRifleRunSpeed = legacyMovementSpeeds.combatRifleRunSpeed;
        movementSpeeds.combatBowWalkSpeed = legacyMovementSpeeds.combatBowWalkSpeed;
        movementSpeeds.combatBowRunSpeed = legacyMovementSpeeds.combatBowRunSpeed;
        movementSpeeds.combatSpecialWalkSpeed = legacyMovementSpeeds.combatSpecialWalkSpeed;
        movementSpeeds.combatSpecialRunSpeed = legacyMovementSpeeds.combatSpecialRunSpeed;
        movementSpeeds.combatExplosiveWalkSpeed = legacyMovementSpeeds.combatExplosiveWalkSpeed;
        movementSpeeds.combatExplosiveRunSpeed = legacyMovementSpeeds.combatExplosiveRunSpeed;

        movementState.isRunning = legacyIsRunning;
        movementState.isSprinting = legacyIsSprinting;
        movementState.isCrouching = legacyIsCrouching;
        movementState.isPistolCrouching = legacyIsPistolCrouching;
        movementState.isLongarmCrouching = legacyIsLongarmCrouching;

        sprintTuning.sprintActionPointsDrainPerSecond = legacySprintActionPointsDrainPerSecond;
        sprintTuning.suppressCombatModeWhileSprinting = legacySuppressCombatModeWhileSprinting;

        rotationAnimation.rotateToAimInCombat = legacyRotateToAimInCombat;
        rotationAnimation.turnSpeed = legacyTurnSpeed;
        rotationAnimation.stationaryTurnSpeed = legacyStationaryTurnSpeed;
        rotationAnimation.animDampTime = legacyAnimDampTime;

        destinationSettings.useNavMeshAgentWhenAvailable = legacyUseNavMeshAgentWhenAvailable;
        destinationSettings.clearDestinationWhenReached = legacyClearDestinationWhenReached;
        destinationSettings.destinationStoppingDistance = legacyDestinationStoppingDistance;
        destinationSettings.destinationRepathInterval = legacyDestinationRepathInterval;

        groundCheckCategory.groundCheck = legacyGroundCheck;
        groundCheckCategory.groundCheckRadius = legacyGroundCheckRadius;

        collision.collisionLayers = legacyCollisionLayers;
        collision.collisionSkin = legacyCollisionSkin;

        crouchCollider.crouchCenter = legacyCrouchCenter;
        crouchCollider.crouchRadius = legacyCrouchRadius;
        crouchCollider.crouchHeight = legacyCrouchHeight;

        grounding.groundLayers = legacyGroundLayers;
        grounding.coyoteTime = legacyCoyoteTime;

        if (jumpTuning.jumpProfiles == null || jumpTuning.jumpProfiles.Length != jumpBlendPoints.Length)
            jumpTuning.jumpProfiles = new JumpTuningCategory().jumpProfiles;

        for (int i = 0; i < jumpTuning.jumpProfiles.Length; i++)
        {
            JumpProfile profile = jumpTuning.jumpProfiles[i];
            profile.takeoffDelay = 0f;
            profile.jumpImpulse = legacyJumpImpulse;
            jumpTuning.jumpProfiles[i] = profile;
        }
    }

    private bool HasLegacyInspectorData()
    {
        return legacyNpcState
            || legacyWeaponController
            || legacyAim
            || legacyAnimator
            || legacyNavMeshAgent
            || legacyGroundCheck;
    }

    private void BuildMovementLockStateHashes()
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

    private void FixedUpdate()
    {
        ResolveReferences();

        if (npcState && npcState.IsDead())
        {
            StopMovement(false);
            ApplyHorizontalVelocity(Vector3.zero);
            UpdateAnimator(Vector3.zero, false);
            return;
        }

        UpdateGroundedState();
        HandleLandingReset();
        SyncNavMeshAgentPosition();

        bool combatMode = npcState && npcState.GetCombatMode();
        currentWorldMoveDirection = ResolveMoveDirection();

        if (IsMovementLockedByAnimation())
            currentWorldMoveDirection = Vector3.zero;

        bool hasMovement = currentWorldMoveDirection.sqrMagnitude > MinInputSqr;
        NPCWeaponController.WeaponCategory category = GetCurrentWeaponCategory();

        UpdateCrouchModeForWeapon(combatMode, category);
        ApplyCapsuleColliderForCrouchState();
        UpdateSprintState(combatMode, hasMovement);

        if (npcState)
            combatMode = npcState.GetCombatMode();

        float speed = ResolveActiveSpeed(combatMode, category, hasMovement);
        Vector3 desiredHorizontalVelocity = ConstrainHorizontalVelocity(currentWorldMoveDirection * speed);
        Vector3 nextHorizontalVelocity = ResolveHorizontalVelocity(desiredHorizontalVelocity);
        ApplyHorizontalVelocity(nextHorizontalVelocity);
        TryConsumeJump();
        RotateBody(nextHorizontalVelocity, combatMode, hasMovement);
        UpdateAnimator(nextHorizontalVelocity, combatMode);
        SyncNavMeshAgentPosition();
    }

    public void SetMoveDirection(Vector3 worldDirection, bool run)
    {
        ClearDestination();
        manualMoveDirection = FlattenDirection(worldDirection);
        isRunning = run && !HasCrippledLeg();
    }

    public void SetMoveDirection(Vector3 worldDirection)
    {
        SetMoveDirection(worldDirection, isRunning);
    }

    public void SetMoveInput(Vector2 input, Quaternion referenceRotation, bool run)
    {
        Vector2 clampedInput = Vector2.ClampMagnitude(input, 1f);
        Vector3 forward = FlattenDirection(referenceRotation * Vector3.forward);
        Vector3 right = FlattenDirection(referenceRotation * Vector3.right);
        SetMoveDirection((right * clampedInput.x) + (forward * clampedInput.y), run);
    }

    public void SetDestination(Vector3 worldDestination, bool run)
    {
        destination = worldDestination;
        hasDestination = true;
        manualMoveDirection = Vector3.zero;
        isRunning = run && !HasCrippledLeg();
        lastRepathTime = -999f;
        TryUpdateNavMeshDestination(true);
    }

    public void SetDestination(Vector3 worldDestination)
    {
        SetDestination(worldDestination, true);
    }

    public void FaceDestinationDirectionImmediately()
    {
        if (!hasDestination)
            return;

        Vector3 currentPosition = rb ? rb.position : transform.position;
        FaceDirectionImmediately(ResolveImmediateDestinationFacingDirection(currentPosition));
    }

    public void ClearDestination()
    {
        hasDestination = false;

        if (navMeshAgent && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
            navMeshAgent.ResetPath();
    }

    public void StopMovement(bool clearDestination)
    {
        manualMoveDirection = Vector3.zero;
        isRunning = false;
        isSprinting = false;

        if (clearDestination)
            ClearDestination();
    }

    public void RecoverAfterResurrection()
    {
        ResolveReferences();
        StopMovement(true);
        SetCrouching(false);

        jumpQueued = false;
        hasLeftGroundSinceJump = false;
        combatModeSuppressedBySprint = false;

        Vector3 position = rb ? rb.position : transform.position;
        Quaternion rotation = rb ? rb.rotation : transform.rotation;

        if (TryResolveGroundedRecoveryPosition(position, out Vector3 recoveryPosition))
            position = recoveryPosition;

        if (rb)
        {
            if (!rb.isKinematic)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            rb.position = position;
            rb.rotation = rotation;
        }

        transform.SetPositionAndRotation(position, rotation);

        if (navMeshAgent && navMeshAgent.enabled)
        {
            if (navMeshAgent.isOnNavMesh)
                navMeshAgent.ResetPath();

            navMeshAgent.Warp(position);
            if (navMeshAgent.isOnNavMesh)
                navMeshAgent.nextPosition = position;
        }

        wasGrounded = true;
        isGrounded = true;
        lastGroundedTime = Time.time;
        ForceAnimatorGroundedState();
        Physics.SyncTransforms();
    }

    public void SetRunning(bool value)
    {
        isRunning = value && !HasCrippledLeg();
    }

    public void SetSprinting(bool value)
    {
        isSprinting = value && !HasCrippledLeg();
    }

    public void SetCrouching(bool value)
    {
        isCrouching = value;
        if (!value)
        {
            isPistolCrouching = false;
            isLongarmCrouching = false;
        }
    }

    public void RequestJump()
    {
        jumpQueued = true;
        lastJumpQueuedTime = Time.time;
    }

    private void ResolveReferences()
    {
        if (!rb)
            rb = GetComponent<Rigidbody>();

        if (!capsule)
            capsule = GetComponent<CapsuleCollider>();

        if (!npcState)
            npcState = GetComponent<NPCState>();

        if (!npcState)
            npcState = GetComponentInParent<NPCState>();

        if (!weaponController)
            weaponController = GetComponentInChildren<NPCWeaponController>(true);

        if (!weaponController)
            weaponController = GetComponentInParent<NPCWeaponController>();

        if (!aim)
            aim = GetComponent<NPCAim>();

        if (!aim)
            aim = GetComponentInChildren<NPCAim>(true);

        if (!animator)
            animator = GetComponentInChildren<Animator>(true);

        if (!navMeshAgent)
            navMeshAgent = GetComponent<NavMeshAgent>();

        if (!navMeshSurface)
            navMeshSurface = GetComponentInParent<NavMeshSurface>();

        if (!navMeshSurface)
            navMeshSurface = GetComponentInChildren<NavMeshSurface>(true);

        if (!navMeshSurface)
            navMeshSurface = FindCompatibleActiveNavMeshSurface();
    }

    private void ConfigureNavMeshAgent()
    {
        if (!navMeshAgent)
            return;

        navMeshAgent.updatePosition = false;
        navMeshAgent.updateRotation = false;
        navMeshAgent.updateUpAxis = true;
        navMeshAgent.stoppingDistance = Mathf.Max(navMeshAgent.stoppingDistance, destinationStoppingDistance);
    }

    private void EnsureNavMeshSurfaceReady()
    {
        if (!navMeshSurface || !buildSurfaceOnAwakeIfMissing)
            return;

        if (navMeshAgent && navMeshSurface.agentTypeID != navMeshAgent.agentTypeID)
        {
            Debug.LogWarning(
                $"NavMeshSurface agent type ({navMeshSurface.agentTypeID}) does not match NavMeshAgent agent type ({navMeshAgent.agentTypeID}).",
                this);
        }

        if (!navMeshSurface.navMeshData)
            navMeshSurface.BuildNavMesh();
    }

    private Vector3 ResolveMoveDirection()
    {
        if (hasDestination)
            return ResolveDestinationMoveDirection();

        return manualMoveDirection;
    }

    private Vector3 ResolveDestinationMoveDirection()
    {
        Vector3 currentPosition = rb ? rb.position : transform.position;
        Vector3 toDestination = destination - currentPosition;
        toDestination.y = 0f;

        if (toDestination.sqrMagnitude <= destinationStoppingDistance * destinationStoppingDistance)
        {
            if (clearDestinationWhenReached)
                ClearDestination();

            return Vector3.zero;
        }

        TryUpdateNavMeshDestination(false);

        if (useNavMeshAgentWhenAvailable && navMeshAgent && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            Vector3 desired = navMeshAgent.desiredVelocity;
            desired.y = 0f;
            if (desired.sqrMagnitude > MinDirectionSqr)
                return desired.normalized;
        }

        return toDestination.normalized;
    }

    private void TryUpdateNavMeshDestination(bool force)
    {
        if (!hasDestination || !useNavMeshAgentWhenAvailable)
            return;

        if (!navMeshAgent || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
            return;

        float now = Time.time;
        if (!force && now - lastRepathTime < destinationRepathInterval)
            return;

        navMeshAgent.speed = ResolveActiveSpeed(npcState && npcState.GetCombatMode(), GetCurrentWeaponCategory(), true);
        navMeshAgent.stoppingDistance = destinationStoppingDistance;
        navMeshAgent.SetDestination(destination);
        lastRepathTime = now;
    }

    private NavMeshSurface FindCompatibleActiveNavMeshSurface()
    {
        if (NavMeshSurface.activeSurfaces == null || NavMeshSurface.activeSurfaces.Count == 0)
            return null;

        NavMeshSurface fallback = null;
        for (int i = 0; i < NavMeshSurface.activeSurfaces.Count; i++)
        {
            NavMeshSurface surface = NavMeshSurface.activeSurfaces[i];
            if (!surface)
                continue;

            if (!fallback)
                fallback = surface;

            if (!navMeshAgent || surface.agentTypeID == navMeshAgent.agentTypeID)
                return surface;
        }

        return fallback;
    }

    private void SyncNavMeshAgentPosition()
    {
        if (!navMeshAgent || !navMeshAgent.enabled || !navMeshAgent.isOnNavMesh)
            return;

        navMeshAgent.nextPosition = rb ? rb.position : transform.position;
    }

    private bool TryResolveGroundedRecoveryPosition(Vector3 currentPosition, out Vector3 recoveryPosition)
    {
        if (navMeshAgent && NavMesh.SamplePosition(currentPosition, out NavMeshHit navMeshHit, 2f, navMeshAgent.areaMask))
        {
            recoveryPosition = navMeshHit.position;
            return true;
        }

        if (NavMesh.SamplePosition(currentPosition, out navMeshHit, 2f, NavMesh.AllAreas))
        {
            recoveryPosition = navMeshHit.position;
            return true;
        }

        Vector3 rayOrigin = currentPosition + Vector3.up;
        int filteredGroundLayers = FilterMovementLayerMask(groundLayers);
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit, 4f, filteredGroundLayers, QueryIgnore) &&
            groundHit.normal.y >= MinUpwardSurfaceNormalY)
        {
            recoveryPosition = groundHit.point;
            return true;
        }

        recoveryPosition = currentPosition;
        return false;
    }

    private void ForceAnimatorGroundedState()
    {
        if (!animator)
            return;

        animator.SetFloat(YVel, 0f);
        animator.SetBool(IsGrounded, true);
        animator.SetBool(IsFalling, false);
        lastAnimIsGrounded = true;
        lastAnimIsFalling = false;
    }

    private void UpdateSprintState(bool combatMode, bool hasMovement)
    {
        if (HasCrippledLeg())
        {
            isRunning = false;
            isSprinting = false;
        }

        if (!hasMovement || !isGrounded)
            isSprinting = false;

        if (isSprinting && !IsMoveDirectionForwardEnoughForSprint())
            isSprinting = false;

        if (isSprinting && npcState && sprintActionPointsDrainPerSecond > 0f)
        {
            float cost = sprintActionPointsDrainPerSecond * Time.fixedDeltaTime;
            if (!npcState.TrySpendActionPoints(cost) || npcState.GetActionPoints() <= 0f)
                isSprinting = false;
        }

        if (!suppressCombatModeWhileSprinting || !npcState)
            return;

        if (isSprinting && combatMode && !IsCrouchingNow)
        {
            npcState.SetCombatMode(false);
            combatModeSuppressedBySprint = true;
        }
        else if (!isSprinting && combatModeSuppressedBySprint)
        {
            npcState.SetCombatMode(true);
            combatModeSuppressedBySprint = false;
        }
    }

    private bool IsMoveDirectionForwardEnoughForSprint()
    {
        if (currentWorldMoveDirection.sqrMagnitude <= MinInputSqr)
            return false;

        Vector3 localDirection = transform.InverseTransformDirection(currentWorldMoveDirection);
        return localDirection.z > sprintForwardDeadzone;
    }

    private Vector3 ResolveHorizontalVelocity(Vector3 desiredHorizontalVelocity)
    {
        if (isGrounded || !rb)
            return desiredHorizontalVelocity;

        Vector3 currentHorizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        return Vector3.Lerp(currentHorizontalVelocity, desiredHorizontalVelocity, Mathf.Clamp01(airControlMultiplier));
    }

    private void ApplyHorizontalVelocity(Vector3 horizontalVelocity)
    {
        if (!rb)
            return;

        rb.linearVelocity = new Vector3(horizontalVelocity.x, rb.linearVelocity.y, horizontalVelocity.z);
    }

    private void RotateBody(Vector3 horizontalVelocity, bool combatMode, bool hasMovement)
    {
        if (!rb)
            return;

        Quaternion desiredRotation = rb.rotation;
        Vector3 facingDirection = ResolveFacingDirection(horizontalVelocity, hasMovement);
        bool hasFacingDirection = facingDirection.sqrMagnitude > MinDirectionSqr;

        if (combatMode && rotateToAimInCombat && aim)
        {
            Vector3 predictedPosition = rb.position + horizontalVelocity * Time.fixedDeltaTime;
            aim.ComputeDesiredRotationFromAimTarget(predictedPosition);

            if (aim.HasAimSolution)
                desiredRotation = aim.DesiredRotation;
            else if (hasFacingDirection)
                desiredRotation = Quaternion.LookRotation(facingDirection, Vector3.up);
        }
        else if (hasFacingDirection)
        {
            desiredRotation = Quaternion.LookRotation(facingDirection, Vector3.up);
        }

        float activeTurnSpeed = hasMovement ? turnSpeed : stationaryTurnSpeed;
        Quaternion nextRotation = activeTurnSpeed <= 0f
            ? desiredRotation
            : Quaternion.RotateTowards(rb.rotation, desiredRotation, activeTurnSpeed * Time.fixedDeltaTime);

        rb.angularVelocity = Vector3.zero;
        rb.MoveRotation(nextRotation);
    }

    private void FaceDirectionImmediately(Vector3 direction)
    {
        if (!rb)
            return;

        Vector3 facingDirection = FlattenDirection(direction);
        if (facingDirection.sqrMagnitude <= MinDirectionSqr)
            return;

        Quaternion desiredRotation = Quaternion.LookRotation(facingDirection, Vector3.up);
        rb.angularVelocity = Vector3.zero;
        rb.rotation = desiredRotation;
        transform.rotation = desiredRotation;
    }

    private Vector3 ResolveImmediateDestinationFacingDirection(Vector3 currentPosition)
    {
        if (useNavMeshAgentWhenAvailable && navMeshAgent && navMeshAgent.enabled && navMeshAgent.isOnNavMesh)
        {
            NavMeshPath path = new NavMeshPath();
            int areaMask = navMeshAgent.areaMask;
            if (NavMesh.CalculatePath(currentPosition, destination, areaMask, path) &&
                path.corners != null &&
                path.corners.Length > 1)
            {
                for (int i = 1; i < path.corners.Length; i++)
                {
                    Vector3 toCorner = path.corners[i] - currentPosition;
                    toCorner.y = 0f;
                    if (toCorner.sqrMagnitude > MinDirectionSqr)
                        return toCorner;
                }
            }
        }

        Vector3 toDestination = destination - currentPosition;
        toDestination.y = 0f;
        return toDestination;
    }

    private Vector3 ResolveFacingDirection(Vector3 horizontalVelocity, bool hasMovement)
    {
        Vector3 velocityDirection = FlattenDirection(horizontalVelocity);
        if (velocityDirection.sqrMagnitude > MinDirectionSqr)
            return velocityDirection;

        return hasMovement ? currentWorldMoveDirection : Vector3.zero;
    }

    private void UpdateGroundedState()
    {
        wasGrounded = isGrounded;

        Vector3 checkPosition = groundCheck
            ? groundCheck.position
            : (rb ? rb.position + Vector3.up * 0.1f : transform.position + Vector3.up * 0.1f);

        isGrounded = CheckMovementSphere(checkPosition, groundCheckRadius, groundLayers);

        if (!isGrounded && wasGrounded)
            hasLeftGroundSinceJump = true;

        if (isGrounded)
            lastGroundedTime = Time.time;

        if (animator && isGrounded != lastAnimIsGrounded)
        {
            animator.SetBool(IsGrounded, isGrounded);
            lastAnimIsGrounded = isGrounded;
        }
    }

    private void TryConsumeJump()
    {
        if (!jumpQueued)
            return;

        if (Time.time - lastJumpQueuedTime > jumpBufferTime)
        {
            jumpQueued = false;
            return;
        }

        if (!rb)
            return;

        bool canJump = isGrounded || Time.time - lastGroundedTime <= coyoteTime;
        if (!canJump)
            return;

        jumpQueued = false;
        JumpProfile profile = GetProfile(ResolveJumpDirection(currentWorldMoveDirection));

        rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
        rb.AddForce(Vector3.up * profile.jumpImpulse, ForceMode.VelocityChange);
        isGrounded = false;
        hasLeftGroundSinceJump = true;

        if (animator)
            animator.SetTrigger(JumpTrig);
    }

    private void HandleLandingReset()
    {
        bool landedThisFrame = !wasGrounded && isGrounded;
        if (!landedThisFrame || !hasLeftGroundSinceJump)
            return;

        if (animator)
            animator.SetTrigger(LandTrig);

        hasLeftGroundSinceJump = false;
    }

    private Vector3 ConstrainHorizontalVelocity(Vector3 desiredHorizontalVelocity)
    {
        if (!capsule)
            return desiredHorizontalVelocity;

        float dt = Time.fixedDeltaTime;
        float invDt = 1f / dt;
        Vector3 horizontal = new Vector3(desiredHorizontalVelocity.x, 0f, desiredHorizontalVelocity.z);
        float speed = horizontal.magnitude;
        if (speed <= MinDirectionSqr)
            return desiredHorizontalVelocity;

        Transform self = transform;
        Vector3 center = self.TransformPoint(capsule.center);
        Vector3 lossy = self.lossyScale;
        float radiusScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z));
        float heightScale = Mathf.Abs(lossy.y);
        float radius = Mathf.Max(MinInputSqr, capsule.radius * radiusScale);
        float height = Mathf.Max(radius * 2f, capsule.height * heightScale);
        float half = Mathf.Max(0f, height * 0.5f - radius);
        Vector3 up = self.up;
        Vector3 p1 = center + up * half;
        Vector3 p2 = center - up * half;
        Vector3 direction = horizontal.normalized;
        float distance = speed * dt;

        if (!CapsuleCastMovement(p1, p2, radius, direction, distance + collisionSkin, collisionLayers, out RaycastHit hit))
            return desiredHorizontalVelocity;

        float allowed = Mathf.Max(0f, hit.distance - collisionSkin);
        Vector3 constrained = direction * (allowed * invDt);
        Vector3 slide = Vector3.ProjectOnPlane(horizontal, hit.normal);

        if (slide.sqrMagnitude <= MinDirectionSqr)
            return constrained;

        Vector3 slideDirection = slide.normalized;
        float slideDistance = slide.magnitude * dt;
        if (CapsuleCastMovement(p1, p2, radius, slideDirection, slideDistance + collisionSkin, collisionLayers, out RaycastHit slideHit))
        {
            float slideAllowed = Mathf.Max(0f, slideHit.distance - collisionSkin);
            constrained = slideDirection * (slideAllowed * invDt);
        }
        else
        {
            constrained = slide;
        }

        return constrained;
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
        if (!capsule)
            return;

        standingCapsuleCenter = capsule.center;
        standingCapsuleRadius = capsule.radius;
        standingCapsuleHeight = capsule.height;
        usingCrouchCollider = false;
    }

    private void ApplyCapsuleColliderForCrouchState()
    {
        usingCrouchCollider = isCrouching || isPistolCrouching || isLongarmCrouching;
    }

    private void UpdateCrouchModeForWeapon(bool combatMode, NPCWeaponController.WeaponCategory category)
    {
        if (!combatMode)
        {
            if (isPistolCrouching || isLongarmCrouching)
            {
                isCrouching = true;
                isPistolCrouching = false;
                isLongarmCrouching = false;
            }

            return;
        }

        if (!isCrouching && !isPistolCrouching && !isLongarmCrouching)
            return;

        if (category == NPCWeaponController.WeaponCategory.Pistol)
        {
            isCrouching = false;
            isPistolCrouching = true;
            isLongarmCrouching = false;
            return;
        }

        if (IsLongarmCategory(category))
        {
            isCrouching = false;
            isPistolCrouching = false;
            isLongarmCrouching = true;
            return;
        }

        isCrouching = true;
        isPistolCrouching = false;
        isLongarmCrouching = false;
    }

    private void UpdateAnimator(Vector3 worldHorizontalVelocity, bool combatMode)
    {
        if (!animator)
            return;

        float yVelocity = rb ? rb.linearVelocity.y : 0f;
        animator.SetFloat(YVel, yVelocity);

        bool animFalling = !isGrounded && yVelocity < -0.01f;
        if (animFalling != lastAnimIsFalling)
        {
            animator.SetBool(IsFalling, animFalling);
            lastAnimIsFalling = animFalling;
        }

        Quaternion referenceRotation = transform.rotation;
        if (combatMode && aim && aim.HasAimSolution)
            referenceRotation = aim.DesiredRotation;

        Vector3 localVelocity = Quaternion.Inverse(referenceRotation) * worldHorizontalVelocity;
        NPCWeaponController.WeaponCategory category = GetCurrentWeaponCategory();
        float normalizer = ResolveActiveSpeed(combatMode, category, worldHorizontalVelocity.sqrMagnitude > MinInputSqr);
        if (normalizer <= MinDirectionSqr)
            normalizer = 1f;

        float x = Mathf.Clamp(localVelocity.x / normalizer, -1f, 1f);
        float z = Mathf.Clamp(localVelocity.z / normalizer, -1f, 1f);

        if (isSprinting)
        {
            x = 0f;
            z = 1f;
        }

        animator.SetFloat(XVel, x, animDampTime, Time.fixedDeltaTime);
        animator.SetFloat(ZVel, z, animDampTime, Time.fixedDeltaTime);

        bool hasMovement = worldHorizontalVelocity.sqrMagnitude > MinInputSqr;
        bool animRunning = isRunning && !isSprinting && hasMovement;
        bool animSprinting = isSprinting && hasMovement;

        if (animRunning != lastAnimIsRunning)
        {
            animator.SetBool(IsRunning, animRunning);
            lastAnimIsRunning = animRunning;
        }

        if (animSprinting != lastAnimIsSprinting)
        {
            animator.SetBool(IsSprinting, animSprinting);
            lastAnimIsSprinting = animSprinting;
        }

        if (isCrouching != lastAnimIsCrouching)
        {
            animator.SetBool(IsCrouching, isCrouching);
            lastAnimIsCrouching = isCrouching;
        }

        if (isPistolCrouching != lastAnimIsPistolCrouching)
        {
            animator.SetBool(IsPistolCrouching, isPistolCrouching);
            lastAnimIsPistolCrouching = isPistolCrouching;
        }

        if (isLongarmCrouching != lastAnimIsLongarmCrouching)
        {
            animator.SetBool(IsLongarmCrouching, isLongarmCrouching);
            lastAnimIsLongarmCrouching = isLongarmCrouching;
        }
    }

    private float ResolveActiveSpeed(
        bool combatMode,
        NPCWeaponController.WeaponCategory category,
        bool hasMovement)
    {
        bool hasCrippledLeg = HasCrippledLeg();
        float walkBaseSpeed = GetModifiedWalkSpeed(movementSpeeds.walkSpeed);
        float runBaseSpeed = movementSpeeds.runSpeed;

        if (combatMode)
        {
            walkBaseSpeed = GetCombatWalkSpeed(category);
            runBaseSpeed = GetCombatRunSpeed(category);
        }

        if (isSprinting && hasMovement && !hasCrippledLeg)
            return movementSpeeds.sprintSpeed;

        if (isPistolCrouching)
            return movementSpeeds.combatPistolCrouchSpeed;

        if (isLongarmCrouching || isCrouching)
            return movementSpeeds.crouchSpeed;

        return isRunning && hasMovement && !hasCrippledLeg ? runBaseSpeed : walkBaseSpeed;
    }

    private float GetModifiedWalkSpeed(float baseWalkSpeed)
    {
        if (!HasCrippledLeg())
            return baseWalkSpeed;

        return baseWalkSpeed * Mathf.Max(0f, movementSpeeds.crippledLegWalkSpeedModifier);
    }

    private bool HasCrippledLeg()
    {
        NPCState state = npcState;
        return state && (state.GetLeftLegCrippled() || state.GetRightLegCrippled());
    }

    private NPCWeaponController.WeaponCategory GetCurrentWeaponCategory()
    {
        return weaponController ? weaponController.GetCurrentCategory() : NPCWeaponController.WeaponCategory.Unarmed;
    }

    private static Vector3 FlattenDirection(Vector3 direction)
    {
        direction.y = 0f;
        if (direction.sqrMagnitude <= MinDirectionSqr)
            return Vector3.zero;

        return direction.normalized;
    }

    private static bool IsLongarmCategory(NPCWeaponController.WeaponCategory category)
    {
        return category == NPCWeaponController.WeaponCategory.Shotgun
            || category == NPCWeaponController.WeaponCategory.SubmachineGun
            || category == NPCWeaponController.WeaponCategory.Rifle;
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

    private float GetCombatWalkSpeed(NPCWeaponController.WeaponCategory category)
    {
        switch (category)
        {
            case NPCWeaponController.WeaponCategory.Unarmed:
                return GetModifiedWalkSpeed(movementSpeeds.combatUnarmedWalkSpeed);
            case NPCWeaponController.WeaponCategory.Knife:
                return GetModifiedWalkSpeed(movementSpeeds.combatKnifeWalkSpeed);
            case NPCWeaponController.WeaponCategory.TwoHanded:
                return GetModifiedWalkSpeed(movementSpeeds.combatTwoHandedWalkSpeed);
            case NPCWeaponController.WeaponCategory.Pistol:
                return GetModifiedWalkSpeed(movementSpeeds.combatPistolWalkSpeed);
            case NPCWeaponController.WeaponCategory.Shotgun:
                return GetModifiedWalkSpeed(movementSpeeds.combatShotgunWalkSpeed);
            case NPCWeaponController.WeaponCategory.SubmachineGun:
                return GetModifiedWalkSpeed(movementSpeeds.combatSubmachineGunWalkSpeed);
            case NPCWeaponController.WeaponCategory.Rifle:
                return GetModifiedWalkSpeed(movementSpeeds.combatRifleWalkSpeed);
            case NPCWeaponController.WeaponCategory.Bow:
                return GetModifiedWalkSpeed(movementSpeeds.combatBowWalkSpeed);
            case NPCWeaponController.WeaponCategory.Special:
                return GetModifiedWalkSpeed(movementSpeeds.combatSpecialWalkSpeed);
            case NPCWeaponController.WeaponCategory.Explosive:
                return GetModifiedWalkSpeed(movementSpeeds.combatExplosiveWalkSpeed);
            default:
                return GetModifiedWalkSpeed(movementSpeeds.walkSpeed);
        }
    }

    private float GetCombatRunSpeed(NPCWeaponController.WeaponCategory category)
    {
        switch (category)
        {
            case NPCWeaponController.WeaponCategory.Unarmed:
                return movementSpeeds.combatUnarmedRunSpeed;
            case NPCWeaponController.WeaponCategory.Knife:
                return movementSpeeds.combatKnifeRunSpeed;
            case NPCWeaponController.WeaponCategory.TwoHanded:
                return movementSpeeds.combatTwoHandedRunSpeed;
            case NPCWeaponController.WeaponCategory.Pistol:
                return movementSpeeds.combatPistolRunSpeed;
            case NPCWeaponController.WeaponCategory.Shotgun:
                return movementSpeeds.combatShotgunRunSpeed;
            case NPCWeaponController.WeaponCategory.SubmachineGun:
                return movementSpeeds.combatSubmachineGunRunSpeed;
            case NPCWeaponController.WeaponCategory.Rifle:
                return movementSpeeds.combatRifleRunSpeed;
            case NPCWeaponController.WeaponCategory.Bow:
                return movementSpeeds.combatBowRunSpeed;
            case NPCWeaponController.WeaponCategory.Special:
                return movementSpeeds.combatSpecialRunSpeed;
            case NPCWeaponController.WeaponCategory.Explosive:
                return movementSpeeds.combatExplosiveRunSpeed;
            default:
                return movementSpeeds.runSpeed;
        }
    }

    private JumpDirection ResolveJumpDirection(Vector3 worldDirection)
    {
        if (worldDirection.sqrMagnitude <= MinInputSqr)
            return JumpDirection.Idle;

        Vector3 localDirection = transform.InverseTransformDirection(worldDirection);
        Vector2 input = new Vector2(localDirection.x, localDirection.z);
        if (input.sqrMagnitude <= MinInputSqr)
            return JumpDirection.Idle;

        Vector2 normalizedInput = input.normalized;
        JumpDirection bestDirection = JumpDirection.Forward;
        float bestScore = Vector2.Dot(normalizedInput, jumpBlendPoints[(int)JumpDirection.Forward]);

        TryUpdateBestDirection(normalizedInput, JumpDirection.Backward, ref bestDirection, ref bestScore);
        TryUpdateBestDirection(normalizedInput, JumpDirection.Left, ref bestDirection, ref bestScore);
        TryUpdateBestDirection(normalizedInput, JumpDirection.Right, ref bestDirection, ref bestScore);
        TryUpdateBestDirection(normalizedInput, JumpDirection.ForwardLeft, ref bestDirection, ref bestScore);
        TryUpdateBestDirection(normalizedInput, JumpDirection.ForwardRight, ref bestDirection, ref bestScore);
        TryUpdateBestDirection(normalizedInput, JumpDirection.BackwardLeft, ref bestDirection, ref bestScore);
        TryUpdateBestDirection(normalizedInput, JumpDirection.BackwardRight, ref bestDirection, ref bestScore);

        return bestDirection;
    }

    private void TryUpdateBestDirection(Vector2 normalizedInput, JumpDirection candidateDirection, ref JumpDirection bestDirection, ref float bestScore)
    {
        float score = Vector2.Dot(normalizedInput, jumpBlendPoints[(int)candidateDirection]);
        if (score > bestScore)
        {
            bestDirection = candidateDirection;
            bestScore = score;
        }
    }

    private JumpProfile GetProfile(JumpDirection direction)
    {
        if (jumpProfiles == null || jumpProfiles.Length == 0)
            return new JumpProfile { direction = direction, takeoffDelay = 0f, jumpImpulse = 4f };

        for (int i = 0; i < jumpProfiles.Length; i++)
        {
            if (jumpProfiles[i].direction == direction)
                return jumpProfiles[i];
        }

        return new JumpProfile { direction = direction, takeoffDelay = 0f, jumpImpulse = 4f };
    }
}
