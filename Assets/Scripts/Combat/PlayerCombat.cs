﻿﻿﻿﻿﻿﻿﻿// imports
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;



// class
public class PlayerCombat : MonoBehaviour
{
    private const float MinAimDirectionSqr = 0.001f;
    private const float MinRayDistance = 0.01f;
    private const int MaxDoubleBarrelRoundCount = 2;
    private const float AutomaticWeaponSpreadJitterFraction = 0.25f;
    private const float AdsAutomaticWeaponSpreadMultiplier = 0.5f;
    private const string DoubleBarrelShotgunWeaponName = "Double-Barrel Shotgun";
    private const string LeftDoubleBarrelGunpointName = "GunpointLeft";
    private const string RightDoubleBarrelGunpointName = "GunpointRight";

    private enum BodyDamageArea
    {
        Chest,
        Head,
        LeftArm,
        RightArm,
        LeftLeg,
        RightLeg
    }

    private const float ChestDamageMultiplier = 1.35f;
    private const float HeadDamageMultiplier = 2f;
    private const float ArmDamageMultiplier = 1.1f;
    private const float LegDamageMultiplier = 1f;
    private const float MinUnarmedDamage = 2f;
    private const float FirearmSneakAttackDamageMultiplier = 2f;
    private const float MeleeSneakAttackDamageMultiplier = 2f;
    private const float MeleeSneakAttackBonusDamageMultiplier = 4f;
    private const float CriticalHitChance = 0.05f;
    private const float CriticalDamageMultiplier = 1.5f;
    private const float CombatSkillExperiencePerNpcHit = 8f;
    private const float CombatSkillExperiencePerNpcDamage = 0.2f;
    private const string HeadshotMessageLabel = "Headshot";
    private const string SneakAttackMessageLabel = "Sneak Attack";
    private const string CriticalMessageLabel = "Critical";
    private const string HeadshotSneakAttackMessageLabel = "Headshot Sneak Attack";
    private const float DefaultGunshotProjectileDiameterMillimeters = 9f;

    private struct MeleeTargetHit
    {
        public BodyDamageArea bodyArea;
        public int specificityScore;
        public float sqrDistance;
    }

    public struct GunshotSignal
    {
        public PlayerCombat sourceCombat;
        public PlayerState sourceState;
        public Transform sourceTransform;
        public Vector3 position;
        public float hearingRadius;
        public float loudness;
        public AmmoDefinition ammoType;
        public WeaponDefinition weaponDefinition;
        public float roundDiameterMillimeters;
        public float muzzleVelocity;
        public int roundsFired;
    }

    public static event Action<GunshotSignal> GunshotEmitted;
    public static event Action<NPCState> PlayerDamagedNpc;

    [Serializable]
    private class SceneReferencesGroup
    {
        // The animator that will receive punch triggers.
        public Animator animator;

        // The PlayerState that tells us CombatMode.
        public PlayerState playerState;

        // The PlayerWeaponController that tells us which weapon category is equipped.
        public PlayerWeaponController playerWeaponController;

        // Inventory used to resolve the equipped weapon definition.
        public PlayerInventory playerInventory;

        // Aim data used to fire toward the crosshair world target.
        public PlayerAim playerAim;

        // Camera used to raycast through the current crosshair screen point.
        public Camera aimCamera;

        // Provides the muzzle spawn point under the weapon model.
        public GetGunPoint gunPointProvider;

        // Pip-Boy controller used to block combat input while UI is open.
        public PipBoyController pipBoyController;

        // Drives firearm model recoil and mirrored upper-body recoil when firing.
        public FirearmRecoilDriver firearmRecoilDriver;

        // Orbit controller used to keep combat crosshair/raycast stable during MMB orbit.
        public CameraRigOrbit cameraRigOrbit;

        // ADS controller used to reduce automatic firearm spread while aiming down sights.
        public CameraADSZoom cameraADSZoom;
    }

    [Serializable]
    private class InputActionsGroup
    {
        // The Input Action Reference for attacking (New Input System).
        public InputActionReference attackAction;

        // The Input Action Reference for blocking (New Input System).
        public InputActionReference blockAction;

        // The Input Action Reference for reloading (New Input System).
        public InputActionReference reloadAction;
    }

    [Serializable]
    private class CrosshairRaycastGroup
    {
        // Layers considered valid for crosshair world hits.
        public LayerMask crosshairHitLayers = ~0;

        // If true, raycast through crosshair uses infinite range.
        public bool useInfiniteCrosshairRayDistance = true;

        // Maximum crosshair ray distance when infinite mode is off.
        [Min(0.01f)] public float crosshairRayDistance = 500f;

        // Fallback world point distance when crosshair ray hits nothing.
        [Min(0.01f)] public float crosshairFallbackDistance = 500f;
    }

    [Serializable]
    private class FirearmAnimatorStatesGroup
    {
        // Animator short state names where firearm attack input is allowed.
        public List<string> firearmAttackStateNames = new List<string>
        {
            "Pistol Walk",
            "Pistol Run",
            "Longarm Walk",
            "Longarm Run"
        };

        // Animator short state names considered part of reload.
        public List<string> reloadStateNames = new List<string>
        {
            "Pistol Reload",
            "Longarm Reload"
        };
    }

    [Serializable]
    private class ShotgunPelletSimulationGroup
    {
        // Enables multi-pellet simulation when shotgun category is equipped.
        public bool enabled = true;

        // Number of pellets spawned from one shell.
        [Min(1)] public int pelletsPerShot = 9;

        // Very tight spread right at the muzzle.
        [Range(0f, 10f)] public float spreadAtMuzzleDegrees = 0.1f;

        // Maximum spread reached at long distance.
        [Range(0f, 10f)] public float spreadAtMaxDistanceDegrees = 2.2f;

        // Distance where spread reaches spreadAtMaxDistanceDegrees.
        [Min(0.01f)] public float maxSpreadDistance = 35f;

        // Fallback velocity if weapon muzzle velocity is missing/zero.
        [Min(0f)] public float fallbackPelletMuzzleVelocity = 380f;

        // Forward offset to reduce immediate overlap at muzzle.
        [Min(0f)] public float pelletSpawnForwardOffset = 0.02f;

        // If true, pellets ignore collisions with other pellets from the same shot.
        public bool ignorePelletSelfCollisions = true;

        // Optional override for pellet projectile prefab.
        public GameObject pelletPrefabOverride;
    }

    [Serializable]
    private class GunshotProjectionGroup
    {
        // Emits gameplay-only gunshot noise for NPC hearing. Audio playback can be added separately.
        public bool emitGunshotSignals = true;

        // Minimum radius before ammo/weapon contribution is applied.
        [Min(0f)] public float baseHearingRadius = 12f;

        // Added radius per millimeter of projectile diameter.
        [Min(0f)] public float roundDiameterRadiusScale = 1.35f;

        // Added radius per meter/second of muzzle velocity.
        [Min(0f)] public float muzzleVelocityRadiusScale = 0.04f;

        // Extra radius per additional round fired in the same shot event.
        [Min(0f)] public float additionalRoundRadiusScale = 0.15f;
    }

    // variables
    [Header("Scene References")]
    [SerializeField] private SceneReferencesGroup sceneReferences = new SceneReferencesGroup();

    [Header("Input Actions")]
    [SerializeField] private InputActionsGroup inputActions = new InputActionsGroup();

    [Header("Crosshair Raycast")]
    [SerializeField] private CrosshairRaycastGroup crosshairRaycast = new CrosshairRaycastGroup();

    [Header("Firearm Animator States")]
    [SerializeField] private FirearmAnimatorStatesGroup firearmAnimatorStates = new FirearmAnimatorStatesGroup();

    [Header("Shotgun Pellet Simulation")]
    [SerializeField] private ShotgunPelletSimulationGroup shotgunPelletSimulation = new ShotgunPelletSimulationGroup();

    [Header("Gunshot Projection")]
    [SerializeField] private GunshotProjectionGroup gunshotProjection = new GunshotProjectionGroup();

    [Header("Melee Damage")]
    [SerializeField] private bool applyMeleeDamageImmediately = true;
    [SerializeField] private Transform meleeOrigin;
    [SerializeField, Min(0f)] private float meleeRange = 1.35f;
    [SerializeField, Min(0f)] private float meleeRadius = 0.45f;
    [SerializeField] private bool useAssistedMeleeHits = true;
    [SerializeField, Min(0f)] private float knifeMeleeRange = 1.55f;
    [SerializeField, Min(0f)] private float knifeMeleeRadius = 0.55f;
    [SerializeField, Min(0f)] private float twoHandedMeleeRange = 1.9f;
    [SerializeField, Min(0f)] private float twoHandedMeleeRadius = 0.65f;
    [SerializeField, Range(1f, 180f)] private float assistedMeleeFacingAngleDegrees = 120f;
    [SerializeField] private bool useMeleeAnimationDamageFallback = true;
    [SerializeField, Range(0f, 1f)] private float meleeFallbackDamageNormalizedTime = 0.35f;
    [SerializeField, Min(0.01f)] private float meleeAttackInputLockSeconds = 0.55f;
    [SerializeField, Min(0f)] private float unarmedDamage = 8f;
    [SerializeField, Min(0.01f)] private float meleeDamageWindowSeconds = 0.35f;
    [SerializeField] private LayerMask meleeHitLayers = ~0;

    [SerializeField, HideInInspector] private bool hasMigratedLegacyInspectorFields;

    [FormerlySerializedAs("animator")] [SerializeField, HideInInspector] private Animator legacyAnimator;
    [FormerlySerializedAs("playerState")] [SerializeField, HideInInspector] private PlayerState legacyPlayerState;
    [FormerlySerializedAs("playerWeaponController")] [SerializeField, HideInInspector] private PlayerWeaponController legacyPlayerWeaponController;
    [FormerlySerializedAs("playerInventory")] [SerializeField, HideInInspector] private PlayerInventory legacyPlayerInventory;
    [FormerlySerializedAs("playerAim")] [SerializeField, HideInInspector] private PlayerAim legacyPlayerAim;
    [FormerlySerializedAs("aimCamera")] [SerializeField, HideInInspector] private Camera legacyAimCamera;
    [FormerlySerializedAs("gunPointProvider")] [SerializeField, HideInInspector] private GetGunPoint legacyGunPointProvider;
    [FormerlySerializedAs("pipBoyController")] [SerializeField, HideInInspector] private PipBoyController legacyPipBoyController;
    [FormerlySerializedAs("firearmRecoilDriver")] [SerializeField, HideInInspector] private FirearmRecoilDriver legacyFirearmRecoilDriver;
    [FormerlySerializedAs("attackAction")] [SerializeField, HideInInspector] private InputActionReference legacyAttackAction;
    [FormerlySerializedAs("blockAction")] [SerializeField, HideInInspector] private InputActionReference legacyBlockAction;
    [FormerlySerializedAs("reloadAction")] [SerializeField, HideInInspector] private InputActionReference legacyReloadAction;
    [FormerlySerializedAs("crosshairHitLayers")] [SerializeField, HideInInspector] private LayerMask legacyCrosshairHitLayers = ~0;
    [FormerlySerializedAs("useInfiniteCrosshairRayDistance")] [SerializeField, HideInInspector] private bool legacyUseInfiniteCrosshairRayDistance = true;
    [FormerlySerializedAs("crosshairRayDistance")] [Min(0.01f)] [SerializeField, HideInInspector] private float legacyCrosshairRayDistance = 500f;
    [FormerlySerializedAs("crosshairFallbackDistance")] [Min(0.01f)] [SerializeField, HideInInspector] private float legacyCrosshairFallbackDistance = 500f;

    private Animator animator
    {
        get => sceneReferences.animator;
        set => sceneReferences.animator = value;
    }

    private PlayerState playerState
    {
        get => sceneReferences.playerState;
        set => sceneReferences.playerState = value;
    }

    private PlayerWeaponController playerWeaponController
    {
        get => sceneReferences.playerWeaponController;
        set => sceneReferences.playerWeaponController = value;
    }

    private PlayerInventory playerInventory
    {
        get => sceneReferences.playerInventory;
        set => sceneReferences.playerInventory = value;
    }

    private PlayerAim playerAim
    {
        get => sceneReferences.playerAim;
        set => sceneReferences.playerAim = value;
    }

    private Camera aimCamera
    {
        get => sceneReferences.aimCamera;
        set => sceneReferences.aimCamera = value;
    }

    private GetGunPoint gunPointProvider
    {
        get => sceneReferences.gunPointProvider;
        set => sceneReferences.gunPointProvider = value;
    }

    private PipBoyController pipBoyController
    {
        get => sceneReferences.pipBoyController;
        set => sceneReferences.pipBoyController = value;
    }

    private FirearmRecoilDriver firearmRecoilDriver
    {
        get => sceneReferences.firearmRecoilDriver;
        set => sceneReferences.firearmRecoilDriver = value;
    }

    private CameraRigOrbit cameraRigOrbit
    {
        get => sceneReferences.cameraRigOrbit;
        set => sceneReferences.cameraRigOrbit = value;
    }

    private CameraADSZoom cameraADSZoom
    {
        get => sceneReferences.cameraADSZoom;
        set => sceneReferences.cameraADSZoom = value;
    }

    private InputActionReference attackAction
    {
        get => inputActions.attackAction;
        set => inputActions.attackAction = value;
    }

    private InputActionReference blockAction
    {
        get => inputActions.blockAction;
        set => inputActions.blockAction = value;
    }

    private InputActionReference reloadAction
    {
        get => inputActions.reloadAction;
        set => inputActions.reloadAction = value;
    }

    private LayerMask crosshairHitLayers
    {
        get => crosshairRaycast.crosshairHitLayers;
        set => crosshairRaycast.crosshairHitLayers = value;
    }

    private bool useInfiniteCrosshairRayDistance
    {
        get => crosshairRaycast.useInfiniteCrosshairRayDistance;
        set => crosshairRaycast.useInfiniteCrosshairRayDistance = value;
    }

    private float crosshairRayDistance
    {
        get => crosshairRaycast.crosshairRayDistance;
        set => crosshairRaycast.crosshairRayDistance = value;
    }

    private float crosshairFallbackDistance
    {
        get => crosshairRaycast.crosshairFallbackDistance;
        set => crosshairRaycast.crosshairFallbackDistance = value;
    }

    // Cached hash for the left punch trigger parameter.
    private static readonly int PunchLeftParam = Animator.StringToHash("PunchLeft");

    // Cached hash for the right punch trigger parameter.
    private static readonly int PunchRightParam = Animator.StringToHash("PunchRight");

    // Cached hash for the unarmed block trigger parameter.
    private static readonly int UnarmedBlockParam = Animator.StringToHash("UnarmedBlock");

    // Cached hash for the knife block trigger parameter.
    private static readonly int KnifeBlockParam = Animator.StringToHash("KnifeBlock");

    // Cached hash for the two-handed block trigger parameter.
    private static readonly int TwoHandedBlockParam = Animator.StringToHash("TwoHandedBlock");

    // Cached hash for the knife stab trigger parameter.
    private static readonly int StabParam = Animator.StringToHash("Stab");

    // Cached hash for the knife slash trigger parameter.
    private static readonly int SlashParam = Animator.StringToHash("Slash");

    // Cached hash for the two-handed left strike trigger parameter.
    private static readonly int LeftStrikeParam = Animator.StringToHash("StrikeLeft");

    // Cached hash for the two-handed right strike trigger parameter.
    private static readonly int RightStrikeParam = Animator.StringToHash("StrikeRight");

    private static readonly int LeftPunchState = Animator.StringToHash("Left Punch");
    private static readonly int RightPunchState = Animator.StringToHash("Right Punch");
    private static readonly int StabState = Animator.StringToHash("Stab");
    private static readonly int SlashState = Animator.StringToHash("Slash");
    private static readonly int LeftStrikeState = Animator.StringToHash("Left Strike");
    private static readonly int RightStrikeState = Animator.StringToHash("Right Strike");

    // Cached trigger hash for pistol reload animation.
    private static readonly int PistolReloadParam = Animator.StringToHash("PistolReload");

    // Cached trigger hash for longarm reload animation.
    private static readonly int LongarmReloadParam = Animator.StringToHash("LongarmReload");

    // Tracks which punch should fire next (true = left, false = right).
    private bool nextPunchIsLeft = true;

    // Tracks which knife attack should fire next (true = stab, false = slash).
    private bool nextKnifeIsStab = true;

    // Tracks which two-handed attack should fire next (true = left strike, false = right strike).
    private bool nextTwoHandedIsLeftStrike = true;

    // Tracks which barrel should fire next when a double-barrel shotgun has only one loaded round.
    private bool nextSingleRoundDoubleBarrelUsesLeft = true;

    // Absolute world time when pistol can fire again based on fire rate cooldown.
    private float nextPistolFireAllowedTime = 0f;

    // Reused non-alloc buffer for crosshair ray hits.
    private readonly RaycastHit[] crosshairRaycastHits = new RaycastHit[16];

    // Reused non-alloc buffer for melee overlap hits.
    private readonly Collider[] meleeHits = new Collider[64];

    // Reused non-alloc buffer for equipped weapon collider overlap hits.
    private readonly Collider[] meleeWeaponHits = new Collider[32];

    private readonly Dictionary<NPCState, MeleeTargetHit> meleeNpcHitSelections = new Dictionary<NPCState, MeleeTargetHit>();
    private readonly Dictionary<PlayerState, MeleeTargetHit> meleePlayerHitSelections = new Dictionary<PlayerState, MeleeTargetHit>();
    private readonly HashSet<int> meleeDamagedTargets = new HashSet<int>();
    private float meleeDamageWindowEndTime;
    private float nextMeleeAttackAllowedTime;
    private int trackedMeleeAttackStateHash;
    private int trackedMeleeAttackLoop;
    private bool openedMeleeDamageForTrackedAnimation;

    // Reused list of active gun markers used to compute recoil-driven crosshair screen offset.
    private readonly List<Transform> recoilCrosshairGunMarkers = new List<Transform>(3);

    // Cached crosshair screen point so UI, interaction, and combat do not repeat the same work in one frame.
    private int cachedCrosshairScreenPointFrame = -1;
    private Vector2 cachedCrosshairScreenPoint;

    // Cached double-barrel gun markers. Marker discovery walks weapon model hierarchies, so do it only when needed.
    private Transform cachedDoubleBarrelReferenceGunMarker;
    private Transform cachedDoubleBarrelLeftGunMarker;
    private Transform cachedDoubleBarrelRightGunMarker;
    private bool hasCachedDoubleBarrelGunMarkers;

    // Cached muzzle-screen baseline while recoil is settled.
    private bool hasRecoilCrosshairBaseline;

    // Baseline screen point captured from active gun marker centroid.
    private Vector2 recoilCrosshairBaselineScreenPoint;

    // Signature of active gun markers currently driving recoil crosshair offset.
    private int recoilCrosshairMarkerSignature;

    // Runtime loaded rounds by weapon id/name (represents magazine contents).
    private readonly Dictionary<string, int> loadedRoundsByWeaponKey = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    // Tracks weapon definition queued for reload completion at animation end.
    private WeaponDefinition pendingPistolReloadWeaponDefinition;

    // True while waiting for pistol reload animation end event.
    private bool isPistolReloadPending;

    // Tracks whether current pending reload has entered reload state.
    private bool hasEnteredPistolReloadState;

    // Last equipped weapon key seen by ammo sync.
    private string syncedAmmoWeaponKey = string.Empty;

    // Last synced ammo values from PlayerWeaponController.
    private int lastSyncedControllerLoadedRounds = -1;
    private int lastSyncedControllerReserveRounds = -1;

    // Last synced runtime ammo values (magazine + inventory reserve).
    private int lastSyncedRuntimeLoadedRounds = -1;
    private int lastSyncedRuntimeReserveRounds = -1;

    // Resolved reload input action used for runtime subscribe/unsubscribe.
    private InputAction resolvedReloadInputAction;

    // Caches fallback definition lookups by equipped weapon name.
    private readonly Dictionary<string, WeaponDefinition> weaponDefinitionLookupCache =
        new Dictionary<string, WeaponDefinition>(StringComparer.OrdinalIgnoreCase);
    private string cachedResolvedWeaponKey = string.Empty;
    private WeaponDefinition cachedResolvedWeaponDefinition;
    private bool hasCachedResolvedWeaponDefinition;
    private PlayerInventory subscribedInventory;

    // methods
    private void OnValidate()
    {
        MigrateLegacyInspectorFields();
        meleeRange = Mathf.Max(0f, meleeRange);
        meleeRadius = Mathf.Max(0f, meleeRadius);
        knifeMeleeRange = Mathf.Max(0f, knifeMeleeRange);
        knifeMeleeRadius = Mathf.Max(0f, knifeMeleeRadius);
        twoHandedMeleeRange = Mathf.Max(0f, twoHandedMeleeRange);
        twoHandedMeleeRadius = Mathf.Max(0f, twoHandedMeleeRadius);
        assistedMeleeFacingAngleDegrees = Mathf.Clamp(assistedMeleeFacingAngleDegrees, 1f, 180f);
        meleeFallbackDamageNormalizedTime = Mathf.Clamp01(meleeFallbackDamageNormalizedTime);
        meleeAttackInputLockSeconds = Mathf.Max(0.01f, meleeAttackInputLockSeconds);
        unarmedDamage = Mathf.Max(MinUnarmedDamage, unarmedDamage);
        ClampGunshotProjectionSettings();
    }

    private void MigrateLegacyInspectorFields()
    {
        if (sceneReferences == null)
            sceneReferences = new SceneReferencesGroup();

        if (inputActions == null)
            inputActions = new InputActionsGroup();

        if (crosshairRaycast == null)
            crosshairRaycast = new CrosshairRaycastGroup();

        if (firearmAnimatorStates == null)
            firearmAnimatorStates = new FirearmAnimatorStatesGroup();

        if (shotgunPelletSimulation == null)
            shotgunPelletSimulation = new ShotgunPelletSimulationGroup();

        if (gunshotProjection == null)
            gunshotProjection = new GunshotProjectionGroup();

        if (hasMigratedLegacyInspectorFields)
            return;

        if (sceneReferences.animator == null && legacyAnimator != null)
            sceneReferences.animator = legacyAnimator;

        if (sceneReferences.playerState == null && legacyPlayerState != null)
            sceneReferences.playerState = legacyPlayerState;

        if (sceneReferences.playerWeaponController == null && legacyPlayerWeaponController != null)
            sceneReferences.playerWeaponController = legacyPlayerWeaponController;

        if (sceneReferences.playerInventory == null && legacyPlayerInventory != null)
            sceneReferences.playerInventory = legacyPlayerInventory;

        if (sceneReferences.playerAim == null && legacyPlayerAim != null)
            sceneReferences.playerAim = legacyPlayerAim;

        if (sceneReferences.aimCamera == null && legacyAimCamera != null)
            sceneReferences.aimCamera = legacyAimCamera;

        if (sceneReferences.gunPointProvider == null && legacyGunPointProvider != null)
            sceneReferences.gunPointProvider = legacyGunPointProvider;

        if (sceneReferences.pipBoyController == null && legacyPipBoyController != null)
            sceneReferences.pipBoyController = legacyPipBoyController;

        if (sceneReferences.firearmRecoilDriver == null && legacyFirearmRecoilDriver != null)
            sceneReferences.firearmRecoilDriver = legacyFirearmRecoilDriver;

        if (inputActions.attackAction == null && legacyAttackAction != null)
            inputActions.attackAction = legacyAttackAction;

        if (inputActions.blockAction == null && legacyBlockAction != null)
            inputActions.blockAction = legacyBlockAction;

        if (inputActions.reloadAction == null && legacyReloadAction != null)
            inputActions.reloadAction = legacyReloadAction;

        crosshairRaycast.crosshairHitLayers = legacyCrosshairHitLayers;
        crosshairRaycast.useInfiniteCrosshairRayDistance = legacyUseInfiniteCrosshairRayDistance;
        crosshairRaycast.crosshairRayDistance = legacyCrosshairRayDistance;
        crosshairRaycast.crosshairFallbackDistance = legacyCrosshairFallbackDistance;

        hasMigratedLegacyInspectorFields = true;
    }

    private void Awake()
    {
        MigrateLegacyInspectorFields();
        ClampGunshotProjectionSettings();

        // Auto-find the animator if not set.
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // Auto-find PlayerState if not set.
        if (playerState == null)
            playerState = GetComponentInParent<PlayerState>();

        // Auto-find PlayerWeaponController if not set.
        if (playerWeaponController == null)
            playerWeaponController = GetComponentInParent<PlayerWeaponController>();

        // Auto-find PlayerInventory if not set.
        if (playerInventory == null)
            playerInventory = GetComponentInParent<PlayerInventory>();

        if (playerInventory == null)
            playerInventory = FindAnyObjectByType<PlayerInventory>();

        // Auto-find PlayerAim if not set.
        if (playerAim == null)
            playerAim = GetComponentInParent<PlayerAim>();

        if (playerAim == null)
            playerAim = FindAnyObjectByType<PlayerAim>();

        // Auto-find aim camera if not set.
        if (aimCamera == null)
            aimCamera = Camera.main;

        // Auto-find gun point provider if not set.
        if (gunPointProvider == null)
            gunPointProvider = GetComponentInChildren<GetGunPoint>(true);

        if (gunPointProvider == null)
            gunPointProvider = GetComponentInParent<GetGunPoint>();

        // Auto-find PipBoyController if not set.
        if (pipBoyController == null)
            pipBoyController = FindAnyObjectByType<PipBoyController>();

        // Auto-find FirearmRecoilDriver if not set.
        if (firearmRecoilDriver == null)
            firearmRecoilDriver = GetComponentInChildren<FirearmRecoilDriver>(true);

        // Auto-find the orbit controller if not set.
        if (cameraRigOrbit == null)
            cameraRigOrbit = FindAnyObjectByType<CameraRigOrbit>();

        // Auto-find ADS state provider if not set.
        if (cameraADSZoom == null)
            cameraADSZoom = FindAnyObjectByType<CameraADSZoom>();

    }


    private void OnEnable()
    {
        RefreshInventorySubscription();

        InputAction attack = attackAction != null ? attackAction.action : null;

        // Stop if we have no attack action.
        if (attack != null)
        {
            // Enable the action so it can fire.
            attack.Enable();

            // Subscribe to performed so we punch on press.
            attack.performed += OnAttackPerformed;
        }

        InputAction block = blockAction != null ? blockAction.action : null;

        // Stop if we have no block action.
        if (block != null)
        {
            // Enable the action so it can fire.
            block.Enable();

            // Subscribe to performed so we block on press.
            block.performed += OnBlockPerformed;
        }

        InputAction reload = ResolveReloadInputAction();

        // Stop if we have no reload action.
        if (reload != null)
        {
            // Enable the action so it can fire.
            reload.Enable();

            // Subscribe to performed so we reload on press.
            reload.performed += OnReloadPerformed;

            resolvedReloadInputAction = reload;
        }
    }


    private void OnDisable()
    {
        UnsubscribeFromInventoryChanges();

        InputAction attack = attackAction != null ? attackAction.action : null;

        // Stop if we have no attack action.
        if (attack != null)
        {
            // Unsubscribe to avoid duplicate subscriptions.
            attack.performed -= OnAttackPerformed;

            // Disable the action when we’re not active.
            attack.Disable();
        }

        InputAction block = blockAction != null ? blockAction.action : null;

        // Stop if we have no block action.
        if (block != null)
        {
            // Unsubscribe to avoid duplicate subscriptions.
            block.performed -= OnBlockPerformed;

            // Disable the action when we’re not active.
            block.Disable();
        }

        InputAction reload = resolvedReloadInputAction != null
            ? resolvedReloadInputAction
            : ResolveReloadInputAction();

        // Stop if we have no reload action.
        if (reload != null)
        {
            // Unsubscribe to avoid duplicate subscriptions.
            reload.performed -= OnReloadPerformed;

            // Disable the action when we’re not active.
            reload.Disable();
        }

        resolvedReloadInputAction = null;

        isPistolReloadPending = false;
        ResetRecoilCrosshairTracking();
        ResetDoubleBarrelGunMarkerCache();
        hasEnteredPistolReloadState = false;
        pendingPistolReloadWeaponDefinition = null;
        ResetAmmoSyncTracking();
        weaponDefinitionLookupCache.Clear();
        InvalidateResolvedWeaponDefinitionCache();
    }


    private void Update()
    {
        SyncEquippedWeaponAmmoWithController();
        CompletePendingReloadFromAnimatorState();
        UpdateMeleeAnimationDamageFallback();
        ApplyMeleeWeaponColliderDamage();
        HandleHeldAutomaticFireInput();
    }


    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        // Stop if we don’t have an animator.
        if (animator == null) return;

        // Stop if we don’t have PlayerState.
        if (playerState == null) return;

        // Stop if we are not in combat mode.
        if (playerState.GetCombatMode() == false) return;

        // Stop if a gameplay-blocking UI is open.
        if (IsGameplayInputBlocked()) return;

        if (playerWeaponController == null) return;

        PlayerWeaponController.WeaponCategory equippedCategory = playerWeaponController.GetCurrentCategory();

        // If we are equipped with a knife, trigger the stab animation.
        if (equippedCategory == PlayerWeaponController.WeaponCategory.Knife)
        {
            if (!TryBeginMeleeSwing())
                return;

            animator.ResetTrigger(StabParam);
            animator.ResetTrigger(SlashParam);

            animator.SetTrigger(nextKnifeIsStab ? StabParam : SlashParam);

            nextKnifeIsStab = !nextKnifeIsStab;

            if (applyMeleeDamageImmediately)
                ApplyMeleeDamage();

            return;
        }

        // If we are equipped with a two-handed weapon, alternate left/right strike animations.
        if (equippedCategory == PlayerWeaponController.WeaponCategory.TwoHanded)
        {
            if (!TryBeginMeleeSwing())
                return;

            animator.ResetTrigger(LeftStrikeParam);
            animator.ResetTrigger(RightStrikeParam);

            animator.SetTrigger(nextTwoHandedIsLeftStrike ? LeftStrikeParam : RightStrikeParam);

            nextTwoHandedIsLeftStrike = !nextTwoHandedIsLeftStrike;

            if (applyMeleeDamageImmediately)
                ApplyMeleeDamage();

            return;
        }

        // If we are equipped with a firearm, fire a projectile and recoil.
        if (IsSupportedFirearmCategory(equippedCategory))
        {
            WeaponDefinition equippedWeaponDefinition = ResolveCurrentWeaponDefinition();
            if (equippedWeaponDefinition == null) return;

            TryFireEquippedFirearmShot(equippedWeaponDefinition);
            return;
        }

        // Stop if we are not unarmed.
        if (equippedCategory != PlayerWeaponController.WeaponCategory.Unarmed) return;

        if (!TryBeginMeleeSwing())
            return;

        // Clear both triggers to prevent stuck/queued wrong punches.
        animator.ResetTrigger(PunchLeftParam);

        // Clear both triggers to prevent stuck/queued wrong punches.
        animator.ResetTrigger(PunchRightParam);

        // If the next punch is left, fire left.
        animator.SetTrigger(nextPunchIsLeft ? PunchLeftParam : PunchRightParam);

        // Flip for next time so punches alternate.
        nextPunchIsLeft = !nextPunchIsLeft;

        if (applyMeleeDamageImmediately)
            ApplyMeleeDamage();
    }

    public void OnMeleeHitAnimationEvent()
    {
        ApplyMeleeDamage();
    }

    private void HandleHeldAutomaticFireInput()
    {
        InputAction attack = attackAction != null ? attackAction.action : null;
        if (attack == null || attack.IsPressed() == false) return;

        // Stop if we don’t have an animator.
        if (animator == null) return;

        // Stop if we don’t have PlayerState.
        if (playerState == null) return;

        // Stop if we are not in combat mode.
        if (playerState.GetCombatMode() == false) return;

        // Stop if a gameplay-blocking UI is open.
        if (IsGameplayInputBlocked()) return;

        if (playerWeaponController == null) return;

        PlayerWeaponController.WeaponCategory equippedCategory = playerWeaponController.GetCurrentCategory();
        if (IsSupportedFirearmCategory(equippedCategory) == false) return;

        WeaponDefinition equippedWeaponDefinition = ResolveCurrentWeaponDefinition();
        if (equippedWeaponDefinition == null) return;

        // Hold-to-fire is only enabled for automatic weapons.
        if (equippedWeaponDefinition.IsAutomatic() == false) return;

        // Held automatic fire requires a positive fire rate to enforce cadence.
        if (equippedWeaponDefinition.GetFireRate() <= 0f) return;

        TryFireEquippedFirearmShot(equippedWeaponDefinition);
    }

    private bool TryFireEquippedFirearmShot(WeaponDefinition equippedWeaponDefinition)
    {
        if (equippedWeaponDefinition == null)
            return false;

        // Only allow firearm attack while configured animator states are active.
        if (IsFirearmAttackStateActive() == false)
            return false;

        if (CanFirePistolShot(equippedWeaponDefinition) == false)
            return false;

        int roundsToFire = ResolveFirearmRoundCountForCurrentShot(equippedWeaponDefinition);
        if (roundsToFire <= 0)
            return false;

        if (TryConsumeFirearmRounds(equippedWeaponDefinition, roundsToFire) == false)
            return false;

        int firedRounds = FirePistolProjectile(equippedWeaponDefinition, roundsToFire);
        if (firedRounds <= 0)
        {
            RestoreConsumedFirearmRounds(equippedWeaponDefinition, roundsToFire);
            return false;
        }

        if (firedRounds < roundsToFire)
            RestoreConsumedFirearmRounds(equippedWeaponDefinition, roundsToFire - firedRounds);

        if (firearmRecoilDriver != null)
            firearmRecoilDriver.FireRecoil();

        EmitGunshotSignal(equippedWeaponDefinition, firedRounds);
        SetNextPistolFireTime(equippedWeaponDefinition);
        return true;
    }

    private static bool IsSupportedFirearmCategory(PlayerWeaponController.WeaponCategory weaponCategory)
    {
        return weaponCategory == PlayerWeaponController.WeaponCategory.Pistol
               || weaponCategory == PlayerWeaponController.WeaponCategory.SubmachineGun
               || weaponCategory == PlayerWeaponController.WeaponCategory.Rifle
               || weaponCategory == PlayerWeaponController.WeaponCategory.Shotgun;
    }

    private bool IsGameplayInputBlocked()
    {
        if (UI.ConsoleController.IsOpen)
            return true;

        if (UI.LevelUpUIController.IsInputBlockActive())
            return true;

        return pipBoyController != null && pipBoyController.IsOpen();
    }

    private void OnBlockPerformed(InputAction.CallbackContext context)
    {
        // Stop if we don’t have an animator.
        if (animator == null) return;

        // Stop if we don’t have PlayerState.
        if (playerState == null) return;

        // Stop if we are not in combat mode.
        if (playerState.GetCombatMode() == false) return;

        // Stop if a gameplay-blocking UI is open.
        if (IsGameplayInputBlocked()) return;

        if (playerWeaponController == null) return;

        PlayerWeaponController.WeaponCategory equippedCategory = playerWeaponController.GetCurrentCategory();

        // If we are equipped with a knife, trigger the knife block.
        if (equippedCategory == PlayerWeaponController.WeaponCategory.Knife)
        {
            // Clear to prevent stuck/queued block triggers.
            animator.ResetTrigger(KnifeBlockParam);

            // Fire the knife block trigger.
            animator.SetTrigger(KnifeBlockParam);
            return;
        }

        // If we are equipped with a two-handed weapon, trigger the two-handed block.
        if (equippedCategory == PlayerWeaponController.WeaponCategory.TwoHanded)
        {
            // Clear to prevent stuck/queued block triggers.
            animator.ResetTrigger(TwoHandedBlockParam);

            // Fire the two-handed block trigger.
            animator.SetTrigger(TwoHandedBlockParam);
            return;
        }

        // Stop if we are not unarmed.
        if (equippedCategory != PlayerWeaponController.WeaponCategory.Unarmed) return;

        // Clear to prevent stuck/queued block triggers.
        animator.ResetTrigger(UnarmedBlockParam);

        // Fire the unarmed block trigger.
        animator.SetTrigger(UnarmedBlockParam);
    }

    private void OnReloadPerformed(InputAction.CallbackContext context)
    {
        // Stop if we don’t have an animator.
        if (animator == null) return;

        // Stop if we don’t have PlayerState.
        if (playerState == null) return;

        // Stop if we are not in combat mode.
        if (playerState.GetCombatMode() == false) return;

        // Stop if a gameplay-blocking UI is open.
        if (IsGameplayInputBlocked()) return;

        if (playerWeaponController == null) return;

        // Reload input only applies to firearm categories currently using these reload triggers.
        PlayerWeaponController.WeaponCategory equippedCategory = playerWeaponController.GetCurrentCategory();
        bool isPistolCategory = equippedCategory == PlayerWeaponController.WeaponCategory.Pistol;
        bool isLongarmCategory =
            equippedCategory == PlayerWeaponController.WeaponCategory.Rifle ||
            equippedCategory == PlayerWeaponController.WeaponCategory.Shotgun ||
            equippedCategory == PlayerWeaponController.WeaponCategory.SubmachineGun;

        if (isPistolCategory == false && isLongarmCategory == false)
            return;

        if (isPistolReloadPending == true)
            return;

        WeaponDefinition equippedWeaponDefinition = ResolveCurrentWeaponDefinition();
        if (equippedWeaponDefinition == null) return;

        if (CanReloadPistol(equippedWeaponDefinition) == false)
            return;

        pendingPistolReloadWeaponDefinition = equippedWeaponDefinition;
        isPistolReloadPending = true;
        hasEnteredPistolReloadState = false;

        // Trigger category-appropriate reload animation.
        int reloadTriggerParam = isLongarmCategory ? LongarmReloadParam : PistolReloadParam;
        animator.ResetTrigger(PistolReloadParam);
        animator.ResetTrigger(LongarmReloadParam);
        animator.SetTrigger(reloadTriggerParam);
    }

    private InputAction ResolveReloadInputAction()
    {
        InputAction directReload = reloadAction != null ? reloadAction.action : null;
        if (directReload != null)
            return directReload;

        InputAction fromAttackMap = FindActionInReferenceMap(attackAction, "Reload");
        if (fromAttackMap != null)
            return fromAttackMap;

        return FindActionInReferenceMap(blockAction, "Reload");
    }

    private InputAction FindActionInReferenceMap(InputActionReference actionReference, string actionName)
    {
        if (actionReference == null)
            return null;

        InputAction referencedAction = actionReference.action;
        if (referencedAction == null)
            return null;

        InputActionMap actionMap = referencedAction.actionMap;
        if (actionMap == null)
            return null;

        return actionMap.FindAction(actionName, false);
    }

    // Animation Event hook for end of "Pistol Reload" animation.
    public void OnPistolReloadAnimationFinished()
    {
        if (isPistolReloadPending == false)
            return;

        WeaponDefinition reloadWeaponDefinition = pendingPistolReloadWeaponDefinition;
        pendingPistolReloadWeaponDefinition = null;
        isPistolReloadPending = false;
        hasEnteredPistolReloadState = false;

        if (reloadWeaponDefinition == null)
            return;

        ReloadPistolMagazine(reloadWeaponDefinition);
    }

    private void CompletePendingReloadFromAnimatorState()
    {
        if (isPistolReloadPending == false)
        {
            hasEnteredPistolReloadState = false;
            return;
        }

        if (animator == null)
        {
            OnPistolReloadAnimationFinished();
            return;
        }

        if (IsReloadStateActive() == true)
        {
            hasEnteredPistolReloadState = true;
            return;
        }

        // Wait until reload state was observed, then complete when it exits.
        if (hasEnteredPistolReloadState == false)
            return;

        OnPistolReloadAnimationFinished();
    }

    private int FirePistolProjectile(WeaponDefinition equippedWeaponDefinition, int roundsToFire)
    {
        if (equippedWeaponDefinition == null)
            return 0;

        if (roundsToFire <= 0)
            return 0;

        AmmoDefinition ammoType = equippedWeaponDefinition.GetAmmoType();
        if (ammoType == null)
            return 0;

        GameObject roundPrefab = ammoType.GetRoundPrefab();

        int clampedRoundsToFire = Mathf.Max(1, roundsToFire);
        int requestedMuzzleShots = Mathf.Min(MaxDoubleBarrelRoundCount, clampedRoundsToFire);
        int spawnedMuzzleShots = 0;

        if (requestedMuzzleShots == MaxDoubleBarrelRoundCount &&
            TryResolveDoubleBarrelGunMarkers(out Transform leftGunMarker, out Transform rightGunMarker) == true)
        {
            if (TryFireProjectileFromGunMarker(equippedWeaponDefinition, ammoType, roundPrefab, leftGunMarker))
                spawnedMuzzleShots++;

            if (TryFireProjectileFromGunMarker(equippedWeaponDefinition, ammoType, roundPrefab, rightGunMarker))
                spawnedMuzzleShots++;

            return spawnedMuzzleShots;
        }

        Transform singleGunMarker = ResolveSingleRoundGunMarkerForCurrentShot();
        return TryFireProjectileFromGunMarker(equippedWeaponDefinition, ammoType, roundPrefab, singleGunMarker)
            ? 1
            : 0;
    }

    private bool TryFireProjectileFromGunMarker(
        WeaponDefinition equippedWeaponDefinition,
        AmmoDefinition ammoType,
        GameObject roundPrefab,
        Transform gunMarker)
    {
        Vector3 spawnPosition = gunMarker != null ? gunMarker.position : transform.position;
        Vector3 baseShotDirection = ResolveShotDirection(spawnPosition, gunMarker);
        Vector3 shotDirection = ApplyAutomaticWeaponSpread(equippedWeaponDefinition, baseShotDirection);
        float weaponDamage = ResolveFirearmProjectileDamage(equippedWeaponDefinition);
        Transform instigatorRoot = ResolveProjectileInstigatorRoot();
        PlayerSkill damageExperienceSkill = ResolveCombatSkillForCurrentWeapon();

        if (IsShotgunPelletSimulationActive() == true)
            return FireShotgunPellets(equippedWeaponDefinition, ammoType, spawnPosition, shotDirection, weaponDamage, instigatorRoot, damageExperienceSkill);

        if (roundPrefab == null)
            return false;

        return SpawnProjectileRound(
            roundPrefab,
            spawnPosition,
            shotDirection,
            equippedWeaponDefinition.GetMuzzleVelocity(),
            ammoType,
            weaponDamage,
            instigatorRoot,
            damageExperienceSkill);
    }

    private bool IsShotgunPelletSimulationActive()
    {
        if (playerWeaponController == null)
            return false;

        if (playerWeaponController.GetCurrentCategory() != PlayerWeaponController.WeaponCategory.Shotgun)
            return false;

        ShotgunPelletSimulationGroup settings = ResolveShotgunPelletSimulation();
        return settings.enabled;
    }

    private bool FireShotgunPellets(
        WeaponDefinition equippedWeaponDefinition,
        AmmoDefinition ammoType,
        Vector3 spawnPosition,
        Vector3 baseShotDirection,
        float totalShotDamage,
        Transform instigatorRoot,
        PlayerSkill damageExperienceSkill)
    {
        ShotgunPelletSimulationGroup settings = ResolveShotgunPelletSimulation();
        GameObject pelletPrefab = settings.pelletPrefabOverride != null
            ? settings.pelletPrefabOverride
            : ammoType.GetRoundPrefab();

        if (pelletPrefab == null)
            return false;

        int pelletCount = Mathf.Max(1, settings.pelletsPerShot);
        float pelletDamage = pelletCount > 0 ? Mathf.Max(0f, totalShotDamage) / pelletCount : Mathf.Max(0f, totalShotDamage);
        float shotDistance = ResolveShotDistanceEstimate(spawnPosition);
        float maxSpreadDistance = Mathf.Max(MinRayDistance, settings.maxSpreadDistance);
        float spreadDistanceT = Mathf.Clamp01(shotDistance / maxSpreadDistance);
        float minSpread = Mathf.Max(0f, settings.spreadAtMuzzleDegrees);
        float maxSpread = Mathf.Max(minSpread, settings.spreadAtMaxDistanceDegrees);
        float spreadDegrees = Mathf.Lerp(minSpread, maxSpread, spreadDistanceT);

        float weaponMuzzleVelocity = equippedWeaponDefinition != null
            ? Mathf.Max(0f, equippedWeaponDefinition.GetMuzzleVelocity())
            : 0f;
        float muzzleVelocity = weaponMuzzleVelocity > 0f
            ? weaponMuzzleVelocity
            : Mathf.Max(0f, settings.fallbackPelletMuzzleVelocity);
        float pelletSpawnForwardOffset = Mathf.Max(0f, settings.pelletSpawnForwardOffset);
        bool ignorePelletSelfCollisions = settings.ignorePelletSelfCollisions;
        List<Collider> spawnedPelletColliders = ignorePelletSelfCollisions
            ? new List<Collider>(pelletCount)
            : null;

        bool spawnedAnyPellet = false;
        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 pelletDirection = GetDirectionInsideSpreadCone(baseShotDirection, spreadDegrees);
            Vector3 pelletSpawnPosition = spawnPosition + (pelletDirection * pelletSpawnForwardOffset);

            if (SpawnProjectileRound(pelletPrefab, pelletSpawnPosition, pelletDirection, muzzleVelocity, ammoType, pelletDamage, instigatorRoot, damageExperienceSkill, out GameObject spawnedPellet))
            {
                spawnedAnyPellet = true;
                if (ignorePelletSelfCollisions)
                    IgnoreShotgunPelletCollisions(spawnedPellet, spawnedPelletColliders);
            }
        }

        return spawnedAnyPellet;
    }

    private static void IgnoreShotgunPelletCollisions(GameObject spawnedPellet, List<Collider> spawnedPelletColliders)
    {
        if (spawnedPellet == null || spawnedPelletColliders == null)
            return;

        Collider[] pelletColliders = spawnedPellet.GetComponentsInChildren<Collider>(false);
        if (pelletColliders == null || pelletColliders.Length == 0)
            return;

        for (int i = 0; i < pelletColliders.Length; i++)
        {
            Collider pelletCollider = pelletColliders[i];
            if (pelletCollider == null || pelletCollider.enabled == false)
                continue;

            for (int j = 0; j < spawnedPelletColliders.Count; j++)
            {
                Collider existingCollider = spawnedPelletColliders[j];
                if (existingCollider == null)
                    continue;

                Physics.IgnoreCollision(pelletCollider, existingCollider, true);
            }

            spawnedPelletColliders.Add(pelletCollider);
        }
    }

    private float ResolveShotDistanceEstimate(Vector3 spawnPosition)
    {
        if (TryGetCrosshairWorldPoint(out Vector3 crosshairWorldPoint) == true)
            return Mathf.Max(0f, Vector3.Distance(spawnPosition, crosshairWorldPoint));

        return Mathf.Max(MinRayDistance, crosshairFallbackDistance);
    }

    private static Vector3 GetDirectionInsideSpreadCone(Vector3 forwardDirection, float spreadDegrees)
    {
        Vector3 normalizedForward = forwardDirection.sqrMagnitude > MinAimDirectionSqr
            ? forwardDirection.normalized
            : Vector3.forward;

        if (spreadDegrees <= 0f)
            return normalizedForward;

        Vector3 right = Vector3.Cross(normalizedForward, Vector3.up);
        if (right.sqrMagnitude <= MinAimDirectionSqr)
            right = Vector3.Cross(normalizedForward, Vector3.right);

        right.Normalize();
        Vector3 up = Vector3.Cross(right, normalizedForward).normalized;

        float spreadRadius = Mathf.Tan(Mathf.Deg2Rad * spreadDegrees);
        Vector2 randomOffset = UnityEngine.Random.insideUnitCircle * spreadRadius;

        Vector3 spreadDirection = normalizedForward + right * randomOffset.x + up * randomOffset.y;
        return spreadDirection.sqrMagnitude > MinAimDirectionSqr
            ? spreadDirection.normalized
            : normalizedForward;
    }

    private Vector3 ApplyAutomaticWeaponSpread(WeaponDefinition weaponDefinition, Vector3 shotDirection)
    {
        if (weaponDefinition == null || weaponDefinition.IsAutomatic() == false)
            return shotDirection;

        float spreadDegrees = weaponDefinition.GetSpread();
        if (spreadDegrees <= 0f)
            return shotDirection;

        if (IsAdsSpreadReductionActive())
            spreadDegrees *= AdsAutomaticWeaponSpreadMultiplier;

        float jitteredSpreadDegrees = spreadDegrees * UnityEngine.Random.Range(
            1f - AutomaticWeaponSpreadJitterFraction,
            1f + AutomaticWeaponSpreadJitterFraction);

        return GetDirectionInsideSpreadCone(shotDirection, jitteredSpreadDegrees);
    }

    private bool IsAdsSpreadReductionActive()
    {
        if (cameraADSZoom == null)
            cameraADSZoom = FindAnyObjectByType<CameraADSZoom>();

        return cameraADSZoom != null && cameraADSZoom.IsAdsActive;
    }

    private bool SpawnProjectileRound(
        GameObject roundPrefab,
        Vector3 spawnPosition,
        Vector3 shotDirection,
        float muzzleVelocity,
        AmmoDefinition ammoType,
        float projectileDamage,
        Transform instigatorRoot,
        PlayerSkill damageExperienceSkill)
    {
        return SpawnProjectileRound(roundPrefab, spawnPosition, shotDirection, muzzleVelocity, ammoType, projectileDamage, instigatorRoot, damageExperienceSkill, out _);
    }

    private bool SpawnProjectileRound(
        GameObject roundPrefab,
        Vector3 spawnPosition,
        Vector3 shotDirection,
        float muzzleVelocity,
        AmmoDefinition ammoType,
        float projectileDamage,
        Transform instigatorRoot,
        PlayerSkill damageExperienceSkill,
        out GameObject spawnedRound)
    {
        spawnedRound = null;

        if (roundPrefab == null)
            return false;

        Vector3 normalizedDirection = shotDirection.sqrMagnitude > MinAimDirectionSqr
            ? shotDirection.normalized
            : transform.forward;

        // Preserve prefab-authored root orientation so projectile colliders/meshes stay aligned per ammo type.
        Quaternion spawnRotation =
            Quaternion.LookRotation(normalizedDirection, Vector3.up) * roundPrefab.transform.rotation;
        spawnedRound = Bullet.SpawnProjectile(roundPrefab, spawnPosition, spawnRotation);
        if (spawnedRound == null)
            return false;

        Vector3 launchVelocity = normalizedDirection * Mathf.Max(0f, muzzleVelocity);

        Bullet spawnedBullet = spawnedRound.GetComponent<Bullet>();
        if (spawnedBullet == null)
            spawnedBullet = spawnedRound.GetComponentInChildren<Bullet>(true);

        if (spawnedBullet != null)
        {
            spawnedBullet.ConfigureBallisticsFromAmmoDefinition(ammoType);
            spawnedBullet.ConfigureDamage(projectileDamage, instigatorRoot, damageExperienceSkill);
            spawnedBullet.Launch(launchVelocity);
            return true;
        }

        Rigidbody roundRigidbody = spawnedRound.GetComponent<Rigidbody>();
        if (roundRigidbody == null)
            roundRigidbody = spawnedRound.GetComponentInChildren<Rigidbody>(true);

        if (roundRigidbody != null)
            roundRigidbody.linearVelocity = launchVelocity;

        return true;
    }

    private float ResolveFirearmProjectileDamage(WeaponDefinition equippedWeaponDefinition)
    {
        if (equippedWeaponDefinition == null)
            return 0f;

        PlayerInventory inventory = ResolvePlayerInventory();
        if (inventory != null)
            return Mathf.Max(0f, inventory.GetWeaponDamage(equippedWeaponDefinition));

        return Mathf.Max(0f, equippedWeaponDefinition.GetDamage());
    }

    private void EmitGunshotSignal(WeaponDefinition equippedWeaponDefinition, int roundsFired)
    {
        GunshotProjectionGroup settings = ResolveGunshotProjection();
        if (settings.emitGunshotSignals == false)
            return;

        if (equippedWeaponDefinition == null || roundsFired <= 0)
            return;

        AmmoDefinition ammoType = equippedWeaponDefinition.GetAmmoType();
        if (ammoType == null)
            return;

        float roundDiameterMillimeters = ResolveGunshotRoundDiameterMillimeters(ammoType);
        float muzzleVelocity = Mathf.Max(0f, equippedWeaponDefinition.GetMuzzleVelocity());
        float hearingRadius = ResolveGunshotHearingRadius(roundDiameterMillimeters, muzzleVelocity, roundsFired);
        if (hearingRadius <= 0f)
            return;

        GunshotSignal signal = new GunshotSignal
        {
            sourceCombat = this,
            sourceState = playerState,
            sourceTransform = ResolveProjectileInstigatorRoot(),
            position = ResolveGunshotPosition(roundsFired),
            hearingRadius = hearingRadius,
            loudness = Mathf.InverseLerp(settings.baseHearingRadius, settings.baseHearingRadius * 5f, hearingRadius),
            ammoType = ammoType,
            weaponDefinition = equippedWeaponDefinition,
            roundDiameterMillimeters = roundDiameterMillimeters,
            muzzleVelocity = muzzleVelocity,
            roundsFired = roundsFired
        };

        GunshotEmitted?.Invoke(signal);
    }

    private float ResolveGunshotHearingRadius(float roundDiameterMillimeters, float muzzleVelocity, int roundsFired)
    {
        GunshotProjectionGroup settings = ResolveGunshotProjection();
        float radius =
            Mathf.Max(0f, settings.baseHearingRadius) +
            Mathf.Max(0f, roundDiameterMillimeters) * Mathf.Max(0f, settings.roundDiameterRadiusScale) +
            Mathf.Max(0f, muzzleVelocity) * Mathf.Max(0f, settings.muzzleVelocityRadiusScale);

        int additionalRounds = Mathf.Max(0, roundsFired - 1);
        if (additionalRounds > 0)
            radius *= 1f + additionalRounds * Mathf.Max(0f, settings.additionalRoundRadiusScale);

        return Mathf.Max(0f, radius);
    }

    private static float ResolveGunshotRoundDiameterMillimeters(AmmoDefinition ammoType)
    {
        if (ammoType == null)
            return DefaultGunshotProjectileDiameterMillimeters;

        float projectileDiameter = ammoType.GetProjectileDiameterMillimeters();
        if (projectileDiameter > 0f)
            return projectileDiameter;

        return DefaultGunshotProjectileDiameterMillimeters;
    }

    private Vector3 ResolveGunshotPosition(int roundsFired)
    {
        if (roundsFired >= MaxDoubleBarrelRoundCount &&
            TryResolveDoubleBarrelGunMarkers(out Transform leftGunMarker, out Transform rightGunMarker) == true)
        {
            if (leftGunMarker != null && rightGunMarker != null)
                return (leftGunMarker.position + rightGunMarker.position) * 0.5f;

            if (leftGunMarker != null)
                return leftGunMarker.position;

            if (rightGunMarker != null)
                return rightGunMarker.position;
        }

        Transform gunMarker = ResolveGunMarker();
        return gunMarker != null ? gunMarker.position : transform.position;
    }

    private Transform ResolveProjectileInstigatorRoot()
    {
        if (playerState != null)
            return playerState.transform;

        return transform.root != null ? transform.root : transform;
    }

    internal static bool TryApplyProjectileDamage(
        Collider hitCollider,
        float rawDamage,
        Transform instigatorRoot,
        bool respectTargetDamageResistance,
        PlayerSkill damageExperienceSkill = PlayerSkill.None)
    {
        PlayerState sourcePlayerState = instigatorRoot ? instigatorRoot.GetComponentInParent<PlayerState>() : null;
        if (!sourcePlayerState)
            return false;

        rawDamage = Mathf.Max(0f, rawDamage);
        if (!hitCollider || rawDamage <= 0f)
            return false;

        NPCState targetNpc = hitCollider.GetComponentInParent<NPCState>();
        if (targetNpc)
        {
            if (IsInstigatorTransform(instigatorRoot, targetNpc.transform))
                return false;

            BodyDamageArea bodyArea = ResolveHitBodyArea(hitCollider, out _);
            float resistedDamage = ResolveProjectileDamageAfterResistance(rawDamage, hitCollider, respectTargetDamageResistance);
            float sneakAttackMultiplier = ResolveFirearmSneakAttackMultiplier(targetNpc, instigatorRoot);
            bool damageApplied = ApplyProjectileDamageToNpcTarget(targetNpc, resistedDamage, bodyArea, sneakAttackMultiplier);
            if (damageApplied)
            {
                AwardCombatSkillExperience(sourcePlayerState, damageExperienceSkill, resistedDamage);
                NotifyPlayerDamagedNpc(targetNpc);
            }

            return damageApplied;
        }

        PlayerState targetPlayer = hitCollider.GetComponentInParent<PlayerState>();
        if (targetPlayer)
        {
            if (IsInstigatorTransform(instigatorRoot, targetPlayer.transform))
                return false;

            BodyDamageArea bodyArea = ResolveHitBodyArea(hitCollider, out _);
            float resistedDamage = ResolveProjectileDamageAfterResistance(rawDamage, hitCollider, respectTargetDamageResistance);
            return ApplyDamageToPlayerTarget(targetPlayer, resistedDamage, bodyArea);
        }

        return false;
    }

    internal static bool IsRootCombatCollider(Collider hitCollider)
    {
        if (!hitCollider)
            return false;

        PlayerState targetPlayer = hitCollider.GetComponentInParent<PlayerState>();
        return targetPlayer && IsRootCombatCollider(hitCollider, targetPlayer.transform);
    }

    private void ApplyMeleeDamage()
    {
        BeginMeleeDamageWindow();
    }

    private bool TryBeginMeleeSwing()
    {
        if (!CanStartMeleeSwing())
            return false;

        ResetMeleeSwingDamageTracking();
        nextMeleeAttackAllowedTime = Time.time + meleeAttackInputLockSeconds;
        return true;
    }

    private bool CanStartMeleeSwing()
    {
        if (Time.time < nextMeleeAttackAllowedTime)
            return false;

        return !IsMeleeAttackAnimationActive();
    }

    private void ResetMeleeSwingDamageTracking()
    {
        meleeDamagedTargets.Clear();
        meleeDamageWindowEndTime = 0f;
        trackedMeleeAttackStateHash = 0;
        trackedMeleeAttackLoop = 0;
        openedMeleeDamageForTrackedAnimation = false;
    }

    private void UpdateMeleeAnimationDamageFallback()
    {
        if (!useMeleeAnimationDamageFallback || applyMeleeDamageImmediately)
            return;

        if (!TryGetCurrentMeleeAttackState(out AnimatorStateInfo meleeStateInfo))
        {
            trackedMeleeAttackStateHash = 0;
            trackedMeleeAttackLoop = 0;
            openedMeleeDamageForTrackedAnimation = false;
            return;
        }

        int stateHash = meleeStateInfo.fullPathHash != 0
            ? meleeStateInfo.fullPathHash
            : meleeStateInfo.shortNameHash;
        int stateLoop = meleeStateInfo.loop ? Mathf.FloorToInt(Mathf.Max(0f, meleeStateInfo.normalizedTime)) : 0;
        if (stateHash != trackedMeleeAttackStateHash || stateLoop != trackedMeleeAttackLoop)
        {
            trackedMeleeAttackStateHash = stateHash;
            trackedMeleeAttackLoop = stateLoop;
            openedMeleeDamageForTrackedAnimation = false;
            meleeDamagedTargets.Clear();
            meleeDamageWindowEndTime = 0f;
        }

        float normalizedTime = meleeStateInfo.loop
            ? Mathf.Repeat(meleeStateInfo.normalizedTime, 1f)
            : Mathf.Clamp01(meleeStateInfo.normalizedTime);
        if (openedMeleeDamageForTrackedAnimation || normalizedTime < meleeFallbackDamageNormalizedTime)
            return;

        openedMeleeDamageForTrackedAnimation = true;
        BeginMeleeDamageWindow();
    }

    private void BeginMeleeDamageWindow()
    {
        if (!IsMeleeDamageWindowActive())
            meleeDamagedTargets.Clear();

        meleeDamageWindowEndTime = Mathf.Max(meleeDamageWindowEndTime, Time.time + meleeDamageWindowSeconds);
        ApplyMeleeWeaponColliderDamage();
    }

    private bool IsMeleeDamageWindowActive()
    {
        return IsTimedMeleeDamageWindowActive() || IsMeleeAttackAnimationActive();
    }

    private bool IsTimedMeleeDamageWindowActive()
    {
        return meleeDamageWindowEndTime > 0f && Time.time <= meleeDamageWindowEndTime;
    }

    private void ApplyMeleeWeaponColliderDamage()
    {
        if (!IsMeleeWeaponDamageCategory())
            return;

        float damage = ResolveMeleeDamage();
        if (damage <= 0f)
            return;

        if (ShouldUseAssistedMeleeDamage())
        {
            if (!IsTimedMeleeDamageWindowActive())
                return;

            ApplyAssistedMeleeDamage(damage);
            return;
        }

        if (!IsMeleeDamageWindowActive())
            return;

        Transform weaponRoot = ResolveMeleeWeaponRoot();
        if (!weaponRoot)
            return;

        Collider[] weaponColliders = weaponRoot.GetComponentsInChildren<Collider>(false);
        for (int i = 0; i < weaponColliders.Length; i++)
        {
            Collider weaponCollider = weaponColliders[i];
            if (!IsUsableMeleeWeaponCollider(weaponCollider))
                continue;

            int hitCount = OverlapColliderNonAlloc(weaponCollider, meleeWeaponHits, meleeHitLayers);
            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider hit = meleeWeaponHits[hitIndex];
                meleeWeaponHits[hitIndex] = null;
                TryApplyMeleeWeaponColliderHit(hit, damage);
            }
        }
    }

    private bool IsMeleeWeaponDamageCategory()
    {
        if (playerWeaponController == null)
            return false;

        PlayerWeaponController.WeaponCategory equippedCategory = playerWeaponController.GetCurrentCategory();
        return equippedCategory == PlayerWeaponController.WeaponCategory.Unarmed ||
               equippedCategory == PlayerWeaponController.WeaponCategory.Knife ||
               equippedCategory == PlayerWeaponController.WeaponCategory.TwoHanded;
    }

    private PlayerSkill ResolveCombatSkillForCurrentWeapon()
    {
        if (playerWeaponController == null)
            return PlayerSkill.Unarmed;

        PlayerWeaponController.WeaponCategory equippedCategory = playerWeaponController.GetCurrentCategory();
        WeaponDefinition equippedWeaponDefinition = ResolveCurrentWeaponDefinition();
        if (IsEnergyWeaponDefinition(equippedWeaponDefinition))
            return PlayerSkill.EnergyWeapons;

        switch (equippedCategory)
        {
            case PlayerWeaponController.WeaponCategory.Unarmed:
                return PlayerSkill.Unarmed;
            case PlayerWeaponController.WeaponCategory.Knife:
            case PlayerWeaponController.WeaponCategory.TwoHanded:
                return PlayerSkill.MeleeWeapons;
            case PlayerWeaponController.WeaponCategory.Special:
                return PlayerSkill.BigGuns;
            case PlayerWeaponController.WeaponCategory.Explosive:
                return PlayerSkill.Explosives;
            case PlayerWeaponController.WeaponCategory.Pistol:
            case PlayerWeaponController.WeaponCategory.SubmachineGun:
            case PlayerWeaponController.WeaponCategory.Rifle:
            case PlayerWeaponController.WeaponCategory.Shotgun:
            case PlayerWeaponController.WeaponCategory.Bow:
                return PlayerSkill.SmallGuns;
            default:
                return PlayerSkill.None;
        }
    }

    private static bool IsEnergyWeaponDefinition(WeaponDefinition weaponDefinition)
    {
        if (weaponDefinition == null)
            return false;

        return IsEnergyWeaponText(weaponDefinition.GetItemId()) ||
               IsEnergyWeaponText(weaponDefinition.GetDisplayName()) ||
               IsEnergyWeaponText(weaponDefinition.name);
    }

    private static bool IsEnergyWeaponText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string lowerValue = value.ToLowerInvariant();
        return lowerValue.Contains("laser") ||
               lowerValue.Contains("plasma") ||
               lowerValue.Contains("energy");
    }

    private void AwardCombatSkillExperience(PlayerSkill skill, float damage)
    {
        PlayerState sourcePlayerState = playerState;
        if (!sourcePlayerState)
        {
            Transform instigatorRoot = ResolveProjectileInstigatorRoot();
            sourcePlayerState = instigatorRoot ? instigatorRoot.GetComponentInParent<PlayerState>() : null;
        }

        AwardCombatSkillExperience(sourcePlayerState, skill, damage);
    }

    private static void AwardCombatSkillExperience(PlayerState sourcePlayerState, PlayerSkill skill, float damage)
    {
        if (!sourcePlayerState || skill == PlayerSkill.None || damage <= 0f)
            return;

        float experienceAmount = CombatSkillExperiencePerNpcHit +
                                 Mathf.Max(0f, damage) * CombatSkillExperiencePerNpcDamage;

        sourcePlayerState.AddSkillExperience(skill, experienceAmount);
    }

    private bool ShouldUseAssistedMeleeDamage()
    {
        return useAssistedMeleeHits || IsUnarmedMeleeDamageCategory();
    }

    private bool IsUnarmedMeleeDamageCategory()
    {
        return playerWeaponController != null &&
               playerWeaponController.GetCurrentCategory() == PlayerWeaponController.WeaponCategory.Unarmed;
    }

    private void ApplyAssistedMeleeDamage(float damage)
    {
        Transform origin = meleeOrigin != null ? meleeOrigin : transform;
        if (origin == null)
            return;

        Vector3 overlapStart = origin.position;
        Vector3 forward = origin.forward.sqrMagnitude > MinAimDirectionSqr
            ? origin.forward.normalized
            : transform.forward.normalized;
        ResolveAssistedMeleeHitVolume(out float range, out float radius);
        Vector3 overlapEnd = overlapStart + forward * range;
        Vector3 selectionCenter = (overlapStart + overlapEnd) * 0.5f;

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            overlapStart,
            overlapEnd,
            radius,
            meleeHits,
            meleeHitLayers,
            QueryTriggerInteraction.Collide);

        meleeNpcHitSelections.Clear();
        meleePlayerHitSelections.Clear();

        Transform instigatorRoot = ResolveProjectileInstigatorRoot();
        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider hit = meleeHits[hitIndex];
            meleeHits[hitIndex] = null;
            RegisterAssistedMeleeHit(hit, selectionCenter, instigatorRoot, overlapStart, forward);
        }

        foreach (KeyValuePair<NPCState, MeleeTargetHit> hitSelection in meleeNpcHitSelections)
        {
            NPCState targetNpc = hitSelection.Key;
            if (targetNpc == null || !meleeDamagedTargets.Add(targetNpc.GetHashCode()))
                continue;

            ApplyDamageToNpcTarget(targetNpc, damage, hitSelection.Value.bodyArea);
        }

        foreach (KeyValuePair<PlayerState, MeleeTargetHit> hitSelection in meleePlayerHitSelections)
        {
            PlayerState targetPlayer = hitSelection.Key;
            if (targetPlayer == null || !meleeDamagedTargets.Add(targetPlayer.GetHashCode()))
                continue;

            ApplyDamageToPlayerTarget(targetPlayer, damage, hitSelection.Value.bodyArea);
        }
    }

    private void RegisterAssistedMeleeHit(
        Collider hitCollider,
        Vector3 selectionCenter,
        Transform instigatorRoot,
        Vector3 originPosition,
        Vector3 forward)
    {
        if (!hitCollider)
            return;

        if (instigatorRoot && hitCollider.transform.IsChildOf(instigatorRoot))
            return;

        if (IsUnderWeaponInHand(hitCollider.transform))
            return;

        if (!IsInsideAssistedMeleeFacing(hitCollider, originPosition, forward))
            return;

        NPCState targetNpc = hitCollider.GetComponentInParent<NPCState>();
        if (targetNpc)
        {
            RegisterMeleeHitSelection(meleeNpcHitSelections, targetNpc, hitCollider, selectionCenter);
            return;
        }

        PlayerState targetPlayer = hitCollider.GetComponentInParent<PlayerState>();
        if (targetPlayer && targetPlayer != playerState)
            RegisterMeleeHitSelection(meleePlayerHitSelections, targetPlayer, hitCollider, selectionCenter);
    }

    private void ResolveAssistedMeleeHitVolume(out float range, out float radius)
    {
        range = meleeRange;
        radius = meleeRadius;

        if (playerWeaponController != null)
        {
            PlayerWeaponController.WeaponCategory equippedCategory = playerWeaponController.GetCurrentCategory();
            if (equippedCategory == PlayerWeaponController.WeaponCategory.Knife)
            {
                range = knifeMeleeRange;
                radius = knifeMeleeRadius;
            }
            else if (equippedCategory == PlayerWeaponController.WeaponCategory.TwoHanded)
            {
                range = twoHandedMeleeRange;
                radius = twoHandedMeleeRadius;
            }
        }

        range = Mathf.Max(0f, range);
        radius = Mathf.Max(MinRayDistance, radius);
    }

    private bool IsInsideAssistedMeleeFacing(Collider hitCollider, Vector3 originPosition, Vector3 forward)
    {
        if (!hitCollider)
            return false;

        if (assistedMeleeFacingAngleDegrees >= 179.9f)
            return true;

        Vector3 normalizedForward = forward.sqrMagnitude > MinAimDirectionSqr
            ? forward.normalized
            : transform.forward.normalized;
        Vector3 closestPoint = GetSafeColliderPoint(hitCollider, originPosition);
        Vector3 toHit = closestPoint - originPosition;
        if (toHit.sqrMagnitude <= MinAimDirectionSqr)
            toHit = hitCollider.bounds.center - originPosition;

        if (toHit.sqrMagnitude <= MinAimDirectionSqr)
            return true;

        float minDot = Mathf.Cos(assistedMeleeFacingAngleDegrees * 0.5f * Mathf.Deg2Rad);
        return Vector3.Dot(normalizedForward, toHit.normalized) >= minDot;
    }

    private Transform ResolveMeleeWeaponRoot()
    {
        Transform root = FindDescendantByName(transform, "WeaponInHand");
        if (!root && playerWeaponController)
            root = FindDescendantByName(playerWeaponController.transform, "WeaponInHand");

        return root;
    }

    private bool IsUsableMeleeWeaponCollider(Collider weaponCollider)
    {
        return weaponCollider &&
               weaponCollider.enabled &&
               weaponCollider.gameObject.activeInHierarchy &&
               weaponCollider.transform.IsChildOf(ResolveMeleeWeaponRoot());
    }

    private bool IsMeleeAttackAnimationActive()
    {
        return TryGetCurrentMeleeAttackState(out _);
    }

    private bool TryGetCurrentMeleeAttackState(out AnimatorStateInfo meleeStateInfo)
    {
        meleeStateInfo = default;
        if (animator == null)
            return false;

        const int BaseLayer = 0;
        if (BaseLayer < 0 || BaseLayer >= animator.layerCount)
            return false;

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(BaseLayer);
        if (IsMeleeAttackState(currentState))
        {
            meleeStateInfo = currentState;
            return true;
        }

        if (animator.IsInTransition(BaseLayer) == false)
            return false;

        AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(BaseLayer);
        if (!IsMeleeAttackState(nextState))
            return false;

        meleeStateInfo = nextState;
        return true;
    }

    private static bool IsMeleeAttackState(AnimatorStateInfo stateInfo)
    {
        int stateHash = stateInfo.shortNameHash;
        return stateHash == LeftPunchState ||
               stateHash == RightPunchState ||
               stateHash == StabState ||
               stateHash == SlashState ||
               stateHash == LeftStrikeState ||
               stateHash == RightStrikeState;
    }

    private void TryApplyMeleeWeaponColliderHit(Collider hitCollider, float damage)
    {
        if (!hitCollider || damage <= 0f)
            return;

        Transform instigatorRoot = ResolveProjectileInstigatorRoot();
        if (instigatorRoot && hitCollider.transform.IsChildOf(instigatorRoot))
            return;

        if (IsUnderWeaponInHand(hitCollider.transform))
            return;

        NPCState targetNpc = hitCollider.GetComponentInParent<NPCState>();
        if (targetNpc)
        {
            if (IsRootCombatCollider(hitCollider, targetNpc.transform))
                return;

            if (!meleeDamagedTargets.Add(targetNpc.GetHashCode()))
                return;

            BodyDamageArea bodyArea = ResolveHitBodyArea(hitCollider, out _);
            ApplyDamageToNpcTarget(targetNpc, damage, bodyArea);
            return;
        }

        PlayerState targetPlayer = hitCollider.GetComponentInParent<PlayerState>();
        if (targetPlayer && targetPlayer != playerState)
        {
            if (IsRootCombatCollider(hitCollider, targetPlayer.transform))
                return;

            if (!meleeDamagedTargets.Add(targetPlayer.GetHashCode()))
                return;

            BodyDamageArea bodyArea = ResolveHitBodyArea(hitCollider, out _);
            ApplyDamageToPlayerTarget(targetPlayer, damage, bodyArea);
        }
    }

    private float ResolveMeleeDamage()
    {
        bool isUnarmed = IsUnarmedMeleeDamageCategory();
        WeaponDefinition equippedWeaponDefinition = ResolveCurrentWeaponDefinition();
        if (equippedWeaponDefinition != null)
        {
            PlayerInventory inventory = ResolvePlayerInventory();
            if (inventory != null)
                return isUnarmed
                    ? Mathf.Max(MinUnarmedDamage, inventory.GetWeaponDamage(equippedWeaponDefinition))
                    : Mathf.Max(0f, inventory.GetWeaponDamage(equippedWeaponDefinition));

            return isUnarmed
                ? Mathf.Max(MinUnarmedDamage, equippedWeaponDefinition.GetDamage())
                : Mathf.Max(0f, equippedWeaponDefinition.GetDamage());
        }

        return isUnarmed ? Mathf.Max(MinUnarmedDamage, unarmedDamage) : Mathf.Max(0f, unarmedDamage);
    }

    private static void RegisterMeleeHitSelection<TTarget>(
        Dictionary<TTarget, MeleeTargetHit> hitSelections,
        TTarget target,
        Collider hitCollider,
        Vector3 overlapCenter)
        where TTarget : Component
    {
        if (hitSelections == null || target == null || hitCollider == null)
            return;

        BodyDamageArea hitBodyArea = ResolveHitBodyArea(hitCollider, out bool matchedExplicitBodyArea);
        int specificityScore = GetHitSpecificityScore(hitBodyArea, matchedExplicitBodyArea);
        Vector3 closestPoint = GetSafeColliderPoint(hitCollider, overlapCenter);
        float sqrDistance = (closestPoint - overlapCenter).sqrMagnitude;

        MeleeTargetHit candidate = new MeleeTargetHit
        {
            bodyArea = hitBodyArea,
            specificityScore = specificityScore,
            sqrDistance = sqrDistance
        };

        if (hitSelections.TryGetValue(target, out MeleeTargetHit currentSelection) == false ||
            ShouldReplaceMeleeHitSelection(candidate, currentSelection))
        {
            hitSelections[target] = candidate;
        }
    }

    private static bool ShouldReplaceMeleeHitSelection(MeleeTargetHit candidate, MeleeTargetHit currentSelection)
    {
        if (candidate.specificityScore != currentSelection.specificityScore)
            return candidate.specificityScore > currentSelection.specificityScore;

        return candidate.sqrDistance < currentSelection.sqrDistance;
    }

    private static int GetHitSpecificityScore(BodyDamageArea bodyArea, bool matchedExplicitBodyArea)
    {
        if (!matchedExplicitBodyArea)
            return 1;

        return bodyArea == BodyDamageArea.Chest ? 2 : 3;
    }

    private void ApplyDamageToNpcTarget(NPCState targetNpc, float damage, BodyDamageArea bodyArea)
    {
        if (targetNpc == null || damage <= 0f)
            return;

        float sneakAttackMultiplier = ResolveMeleeSneakAttackMultiplier(targetNpc);
        float meleeSneakAttackBonusMultiplier = ResolveMeleeSneakAttackBonusMultiplier(sneakAttackMultiplier);
        bool isCriticalHit = RollCriticalHit();
        float modifiedDamage = ApplyBodyAreaDamageModifier(
            damage * sneakAttackMultiplier * meleeSneakAttackBonusMultiplier * GetCriticalDamageMultiplier(isCriticalHit),
            bodyArea);
        if (modifiedDamage <= 0f)
            return;

        ShowNpcDamageMessage(sneakAttackMultiplier * meleeSneakAttackBonusMultiplier, targetNpc, bodyArea, isCriticalHit);
        targetNpc.ApplyDamage(modifiedDamage);
        ApplyDamageToNpcBodyArea(targetNpc, modifiedDamage, bodyArea);
        NotifyPlayerDamagedNpc(targetNpc);
        AwardCombatSkillExperience(ResolveCombatSkillForCurrentWeapon(), modifiedDamage);
    }

    private static bool ApplyProjectileDamageToNpcTarget(
        NPCState targetNpc,
        float damage,
        BodyDamageArea bodyArea,
        float sneakAttackMultiplier)
    {
        if (targetNpc == null || damage <= 0f)
            return false;

        bool isCriticalHit = RollCriticalHit();
        float modifiedDamage = ApplyBodyAreaDamageModifier(
            damage * Mathf.Max(1f, sneakAttackMultiplier) * GetCriticalDamageMultiplier(isCriticalHit),
            bodyArea);
        if (modifiedDamage <= 0f)
            return false;

        ShowNpcDamageMessage(sneakAttackMultiplier, targetNpc, bodyArea, isCriticalHit);
        targetNpc.ApplyDamage(modifiedDamage);
        ApplyDamageToNpcBodyArea(targetNpc, modifiedDamage, bodyArea);
        return true;
    }

    private float ResolveMeleeSneakAttackMultiplier(NPCState targetNpc)
    {
        if (targetNpc == null)
            return 1f;

        Transform instigatorRoot = ResolveProjectileInstigatorRoot();
        if (instigatorRoot == null || instigatorRoot.GetComponentInParent<PlayerState>() == null)
            return 1f;

        NPCCombat targetCombat = targetNpc.GetComponentInParent<NPCCombat>();
        if (targetCombat == null)
            targetCombat = targetNpc.GetComponentInChildren<NPCCombat>(true);

        if (targetCombat == null)
            return 1f;

        return targetCombat.CanReceivePlayerSneakAttack(instigatorRoot)
            ? MeleeSneakAttackDamageMultiplier
            : 1f;
    }

    private static float ResolveMeleeSneakAttackBonusMultiplier(float sneakAttackMultiplier)
    {
        return sneakAttackMultiplier > 1f ? MeleeSneakAttackBonusDamageMultiplier : 1f;
    }

    private static float ResolveFirearmSneakAttackMultiplier(NPCState targetNpc, Transform instigatorRoot)
    {
        if (targetNpc == null || instigatorRoot == null || instigatorRoot.GetComponentInParent<PlayerState>() == null)
            return 1f;

        NPCCombat targetCombat = targetNpc.GetComponentInParent<NPCCombat>();
        if (targetCombat == null)
            targetCombat = targetNpc.GetComponentInChildren<NPCCombat>(true);

        if (targetCombat == null)
            return 1f;

        return targetCombat.CanReceivePlayerSneakAttack(instigatorRoot)
            ? FirearmSneakAttackDamageMultiplier
            : 1f;
    }

    private static float ResolveProjectileDamageAfterResistance(
        float rawDamage,
        Collider hitCollider,
        bool respectTargetDamageResistance)
    {
        if (!respectTargetDamageResistance || !hitCollider)
            return rawDamage;

        NPCInventory npcInventory = hitCollider.GetComponentInParent<NPCInventory>();
        if (npcInventory)
            return Mathf.Max(0f, rawDamage - Mathf.Max(0, npcInventory.GetTotalDamageResistance()));

        PlayerInventory playerInventory = hitCollider.GetComponentInParent<PlayerInventory>();
        if (playerInventory)
            return Mathf.Max(0f, rawDamage - Mathf.Max(0, playerInventory.GetTotalDamageResistance()));

        return rawDamage;
    }

    private static bool IsInstigatorTransform(Transform instigatorRoot, Transform targetTransform)
    {
        if (!instigatorRoot || !targetTransform)
            return false;

        return targetTransform == instigatorRoot ||
               targetTransform.IsChildOf(instigatorRoot) ||
               instigatorRoot.IsChildOf(targetTransform);
    }

    private static void ShowNpcDamageMessage(
        float sneakAttackMultiplier,
        NPCState targetNpc,
        BodyDamageArea bodyArea,
        bool isCriticalHit)
    {
        bool isHeadshot = bodyArea == BodyDamageArea.Head;
        bool isSneakAttack = sneakAttackMultiplier > 1f;
        if (!isHeadshot && !isSneakAttack && !isCriticalHit)
            return;

        string messageLabel = ResolveNpcDamageMessageLabel(isHeadshot, isSneakAttack, isCriticalHit);
        float messageMultiplier = ResolveNpcDamageMessageMultiplier(sneakAttackMultiplier, bodyArea, isCriticalHit);
        string targetName = ResolveNpcDamageMessageTargetName(targetNpc);
        string targetSuffix = string.IsNullOrWhiteSpace(targetName) ? string.Empty : " on " + targetName;

        UI.HUDMessagePanelController.Queue(messageLabel + targetSuffix + " for " + FormatMultiplier(messageMultiplier) + "x");
    }

    private static string ResolveNpcDamageMessageLabel(bool isHeadshot, bool isSneakAttack, bool isCriticalHit)
    {
        if (isSneakAttack && isHeadshot && isCriticalHit)
            return SneakAttackMessageLabel + " " + HeadshotMessageLabel + " " + CriticalMessageLabel;

        if (isSneakAttack && isCriticalHit)
            return SneakAttackMessageLabel + " " + CriticalMessageLabel;

        if (isHeadshot && isCriticalHit)
            return HeadshotMessageLabel + " " + CriticalMessageLabel;

        if (isCriticalHit)
            return CriticalMessageLabel;

        if (isHeadshot && isSneakAttack)
            return HeadshotSneakAttackMessageLabel;

        return isHeadshot ? HeadshotMessageLabel : SneakAttackMessageLabel;
    }

    private static float ResolveNpcDamageMessageMultiplier(
        float sneakAttackMultiplier,
        BodyDamageArea bodyArea,
        bool isCriticalHit)
    {
        float messageMultiplier = Mathf.Max(1f, sneakAttackMultiplier);
        if (bodyArea == BodyDamageArea.Head)
            messageMultiplier *= GetBodyAreaDamageMultiplier(bodyArea);

        messageMultiplier *= GetCriticalDamageMultiplier(isCriticalHit);

        return messageMultiplier;
    }

    private static string ResolveNpcDamageMessageTargetName(NPCState targetNpc)
    {
        if (targetNpc == null)
            return string.Empty;

        NPC npc = targetNpc.GetComponentInParent<NPC>();
        if (!npc)
            npc = targetNpc.GetComponentInChildren<NPC>(true);

        string targetName = npc ? npc.GetNPCName() : targetNpc.GetNPCName();
        return string.IsNullOrWhiteSpace(targetName) ? string.Empty : targetName.Trim();
    }

    private static string FormatMultiplier(float multiplier)
    {
        int roundedMultiplier = Mathf.RoundToInt(multiplier);
        if (Mathf.Approximately(multiplier, roundedMultiplier))
            return roundedMultiplier.ToString();

        return multiplier.ToString("0.##");
    }

    private static bool ApplyDamageToPlayerTarget(PlayerState targetPlayer, float damage, BodyDamageArea bodyArea)
    {
        if (targetPlayer == null || damage <= 0f)
            return false;

        bool isCriticalHit = RollCriticalHit();
        float modifiedDamage = ApplyBodyAreaDamageModifier(damage * GetCriticalDamageMultiplier(isCriticalHit), bodyArea);
        if (modifiedDamage <= 0f)
            return false;

        targetPlayer.ApplyDamage(modifiedDamage);
        ApplyDamageToPlayerBodyArea(targetPlayer, modifiedDamage, bodyArea);
        return true;
    }

    private static void NotifyPlayerDamagedNpc(NPCState targetNpc)
    {
        if (targetNpc == null)
            return;

        PlayerDamagedNpc?.Invoke(targetNpc);
    }

    private static float ApplyBodyAreaDamageModifier(float damage, BodyDamageArea bodyArea)
    {
        return Mathf.Max(0f, damage) * GetBodyAreaDamageMultiplier(bodyArea);
    }

    private static bool RollCriticalHit()
    {
        return UnityEngine.Random.value < CriticalHitChance;
    }

    private static float GetCriticalDamageMultiplier(bool isCriticalHit)
    {
        return isCriticalHit ? CriticalDamageMultiplier : 1f;
    }

    private static float GetBodyAreaDamageMultiplier(BodyDamageArea bodyArea)
    {
        float multiplier;
        switch (bodyArea)
        {
            case BodyDamageArea.Head:
                multiplier = HeadDamageMultiplier;
                break;
            case BodyDamageArea.LeftArm:
            case BodyDamageArea.RightArm:
                multiplier = ArmDamageMultiplier;
                break;
            case BodyDamageArea.LeftLeg:
            case BodyDamageArea.RightLeg:
                multiplier = LegDamageMultiplier;
                break;
            default:
                multiplier = ChestDamageMultiplier;
                break;
        }

        return Mathf.Max(1f, multiplier);
    }

    private static void ApplyDamageToNpcBodyArea(NPCState targetNpc, float damage, BodyDamageArea bodyArea)
    {
        switch (bodyArea)
        {
            case BodyDamageArea.Head:
                targetNpc.SetHeadHealth(targetNpc.GetHeadHealth() - damage);
                break;
            case BodyDamageArea.LeftArm:
                targetNpc.SetLeftArmHealth(targetNpc.GetLeftArmHealth() - damage);
                break;
            case BodyDamageArea.RightArm:
                targetNpc.SetRightArmHealth(targetNpc.GetRightArmHealth() - damage);
                break;
            case BodyDamageArea.LeftLeg:
                targetNpc.SetLeftLegHealth(targetNpc.GetLeftLegHealth() - damage);
                break;
            case BodyDamageArea.RightLeg:
                targetNpc.SetRightLegHealth(targetNpc.GetRightLegHealth() - damage);
                break;
            default:
                targetNpc.SetChestHealth(targetNpc.GetChestHealth() - damage);
                break;
        }
    }

    private static void ApplyDamageToPlayerBodyArea(PlayerState targetPlayer, float damage, BodyDamageArea bodyArea)
    {
        switch (bodyArea)
        {
            case BodyDamageArea.Head:
                targetPlayer.SetHeadHealth(targetPlayer.GetHeadHealth() - damage);
                break;
            case BodyDamageArea.LeftArm:
                targetPlayer.SetLeftArmHealth(targetPlayer.GetLeftArmHealth() - damage);
                break;
            case BodyDamageArea.RightArm:
                targetPlayer.SetRightArmHealth(targetPlayer.GetRightArmHealth() - damage);
                break;
            case BodyDamageArea.LeftLeg:
                targetPlayer.SetLeftLegHealth(targetPlayer.GetLeftLegHealth() - damage);
                break;
            case BodyDamageArea.RightLeg:
                targetPlayer.SetRightLegHealth(targetPlayer.GetRightLegHealth() - damage);
                break;
            default:
                targetPlayer.SetChestHealth(targetPlayer.GetChestHealth() - damage);
                break;
        }
    }

    private static BodyDamageArea ResolveHitBodyArea(Collider hitCollider, out bool matchedExplicitBodyArea)
    {
        matchedExplicitBodyArea = false;
        if (hitCollider == null)
            return BodyDamageArea.Chest;

        bool foundChestMatch = false;
        for (Transform current = hitCollider.transform; current != null; current = current.parent)
        {
            string normalizedNodeName = NormalizeBodyAreaToken(current.name);
            if (string.IsNullOrEmpty(normalizedNodeName))
                continue;

            if (ContainsAnyBodyAreaToken(normalizedNodeName, "head", "defhead", "neck", "skull", "face", "jaw"))
            {
                matchedExplicitBodyArea = true;
                return BodyDamageArea.Head;
            }

            if (ContainsAnyBodyAreaToken(normalizedNodeName, "upperarml", "forearml", "handl", "shoulderl"))
            {
                matchedExplicitBodyArea = true;
                return BodyDamageArea.LeftArm;
            }

            if (ContainsAnyBodyAreaToken(normalizedNodeName, "upperarmr", "forearmr", "handr", "shoulderr"))
            {
                matchedExplicitBodyArea = true;
                return BodyDamageArea.RightArm;
            }

            if (ContainsAnyBodyAreaToken(normalizedNodeName, "thighl", "shinl", "footl", "toel", "calfl"))
            {
                matchedExplicitBodyArea = true;
                return BodyDamageArea.LeftLeg;
            }

            if (ContainsAnyBodyAreaToken(normalizedNodeName, "thighr", "shinr", "footr", "toer", "calfr"))
            {
                matchedExplicitBodyArea = true;
                return BodyDamageArea.RightLeg;
            }

            if (ContainsAnyBodyAreaToken(normalizedNodeName, "spine", "torso", "chest", "pelvis", "hips"))
            {
                foundChestMatch = true;
            }
        }

        if (foundChestMatch)
            matchedExplicitBodyArea = true;

        return BodyDamageArea.Chest;
    }

    private static bool ContainsAnyBodyAreaToken(string normalizedNodeName, params string[] tokens)
    {
        if (string.IsNullOrEmpty(normalizedNodeName) || tokens == null || tokens.Length == 0)
            return false;

        for (int i = 0; i < tokens.Length; i++)
        {
            string token = tokens[i];
            if (string.IsNullOrEmpty(token))
                continue;

            if (normalizedNodeName.Contains(token))
                return true;
        }

        return false;
    }

    private static bool IsRootCombatCollider(Collider hitCollider, Transform combatRoot)
    {
        return hitCollider != null && combatRoot != null && hitCollider.transform == combatRoot;
    }

    private static Vector3 GetSafeColliderPoint(Collider collider, Vector3 position)
    {
        if (!collider)
            return position;

        if (CanUsePreciseClosestPoint(collider))
            return collider.ClosestPoint(position);

        Bounds bounds = collider.bounds;
        return bounds.size.sqrMagnitude > MinAimDirectionSqr
            ? bounds.ClosestPoint(position)
            : bounds.center;
    }

    private static bool CanUsePreciseClosestPoint(Collider collider)
    {
        if (!collider)
            return false;

        if (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider)
            return true;

        return collider is MeshCollider meshCollider && meshCollider.convex;
    }

    private static int OverlapColliderNonAlloc(Collider collider, Collider[] results, LayerMask layers)
    {
        if (!collider || results == null || results.Length == 0)
            return 0;

        if (collider is BoxCollider boxCollider)
        {
            Vector3 center = boxCollider.transform.TransformPoint(boxCollider.center);
            Vector3 halfExtents = Vector3.Scale(boxCollider.size * 0.5f, Abs(boxCollider.transform.lossyScale));
            return Physics.OverlapBoxNonAlloc(center, halfExtents, results, boxCollider.transform.rotation, layers, QueryTriggerInteraction.Collide);
        }

        if (collider is SphereCollider sphereCollider)
        {
            Vector3 center = sphereCollider.transform.TransformPoint(sphereCollider.center);
            float radius = sphereCollider.radius * MaxAbsAxis(sphereCollider.transform.lossyScale);
            return Physics.OverlapSphereNonAlloc(center, radius, results, layers, QueryTriggerInteraction.Collide);
        }

        if (collider is CapsuleCollider capsuleCollider)
        {
            GetWorldCapsule(capsuleCollider, out Vector3 point0, out Vector3 point1, out float radius);
            return Physics.OverlapCapsuleNonAlloc(point0, point1, radius, results, layers, QueryTriggerInteraction.Collide);
        }

        Bounds bounds = collider.bounds;
        return Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents, results, Quaternion.identity, layers, QueryTriggerInteraction.Collide);
    }

    private static void GetWorldCapsule(CapsuleCollider capsuleCollider, out Vector3 point0, out Vector3 point1, out float radius)
    {
        Transform capsuleTransform = capsuleCollider.transform;
        Vector3 scale = Abs(capsuleTransform.lossyScale);
        Vector3 axis = GetCapsuleAxis(capsuleTransform, capsuleCollider.direction);
        float axisScale = GetAxisScale(scale, capsuleCollider.direction);
        float perpendicularScale = GetCapsulePerpendicularScale(scale, capsuleCollider.direction);
        radius = capsuleCollider.radius * perpendicularScale;

        Vector3 center = capsuleTransform.TransformPoint(capsuleCollider.center);
        float height = Mathf.Max(capsuleCollider.height * axisScale, radius * 2f);
        float segmentHalfLength = Mathf.Max(0f, height * 0.5f - radius);
        point0 = center + axis * segmentHalfLength;
        point1 = center - axis * segmentHalfLength;
    }

    private static Vector3 GetCapsuleAxis(Transform capsuleTransform, int direction)
    {
        switch (direction)
        {
            case 0:
                return capsuleTransform.right;
            case 2:
                return capsuleTransform.forward;
            default:
                return capsuleTransform.up;
        }
    }

    private static float GetAxisScale(Vector3 scale, int direction)
    {
        switch (direction)
        {
            case 0:
                return scale.x;
            case 2:
                return scale.z;
            default:
                return scale.y;
        }
    }

    private static float GetCapsulePerpendicularScale(Vector3 scale, int direction)
    {
        switch (direction)
        {
            case 0:
                return Mathf.Max(scale.y, scale.z);
            case 2:
                return Mathf.Max(scale.x, scale.y);
            default:
                return Mathf.Max(scale.x, scale.z);
        }
    }

    private static Vector3 Abs(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private static float MaxAbsAxis(Vector3 value)
    {
        return Mathf.Max(Mathf.Abs(value.x), Mathf.Max(Mathf.Abs(value.y), Mathf.Abs(value.z)));
    }

    private static Transform FindDescendantByName(Transform root, string name)
    {
        if (!root || string.IsNullOrWhiteSpace(name))
            return null;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate && string.Equals(candidate.name, name, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
    }

    private static bool IsUnderWeaponInHand(Transform candidate)
    {
        for (Transform current = candidate; current; current = current.parent)
        {
            if (string.Equals(current.name, "WeaponInHand", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static string NormalizeBodyAreaToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] chars = new char[value.Length];
        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char current = char.ToLowerInvariant(value[i]);
            if (!char.IsLetterOrDigit(current))
                continue;

            chars[count] = current;
            count++;
        }

        return new string(chars, 0, count);
    }

    private bool CanFirePistolShot(WeaponDefinition equippedWeaponDefinition)
    {
        float now = Time.time;

        // Enforce per-weapon fire-rate cooldown.
        if (now < nextPistolFireAllowedTime)
            return false;

        // Firing requires an available round in the current weapon/ammo system.
        return HasPistolRoundAvailable(equippedWeaponDefinition);
    }

    private bool HasPistolRoundAvailable(WeaponDefinition equippedWeaponDefinition)
    {
        if (equippedWeaponDefinition == null) return false;

        if (IsGodModeEnabled())
            return equippedWeaponDefinition.GetAmmoType() != null;

        int magazineSize = Mathf.Max(0, equippedWeaponDefinition.GetMagazineSize());
        if (magazineSize <= 0)
            return GetReserveAmmoCount(equippedWeaponDefinition) > 0;

        EnsureMagazineInitialized(equippedWeaponDefinition);
        return GetLoadedMagazineRounds(equippedWeaponDefinition) > 0;
    }

    private bool ConsumePistolRound(WeaponDefinition equippedWeaponDefinition)
    {
        if (equippedWeaponDefinition == null) return false;

        if (IsGodModeEnabled())
            return equippedWeaponDefinition.GetAmmoType() != null;

        int magazineSize = Mathf.Max(0, equippedWeaponDefinition.GetMagazineSize());
        if (magazineSize <= 0)
            return TryConsumeReserveAmmo(equippedWeaponDefinition, 1);

        EnsureMagazineInitialized(equippedWeaponDefinition);
        int loadedRounds = GetLoadedMagazineRounds(equippedWeaponDefinition);
        if (loadedRounds <= 0) return false;

        SetLoadedMagazineRounds(equippedWeaponDefinition, loadedRounds - 1);
        return true;
    }

    private bool TryConsumeFirearmRounds(WeaponDefinition equippedWeaponDefinition, int roundCount)
    {
        if (roundCount <= 0)
            return false;

        if (IsGodModeEnabled())
            return equippedWeaponDefinition != null && equippedWeaponDefinition.GetAmmoType() != null;

        int consumedRounds = 0;
        for (int i = 0; i < roundCount; i++)
        {
            if (ConsumePistolRound(equippedWeaponDefinition) == false)
            {
                if (consumedRounds > 0)
                    RestoreConsumedFirearmRounds(equippedWeaponDefinition, consumedRounds);

                return false;
            }

            consumedRounds++;
        }

        return true;
    }

    private void RestoreConsumedPistolRound(WeaponDefinition equippedWeaponDefinition)
    {
        if (equippedWeaponDefinition == null) return;

        int magazineSize = Mathf.Max(0, equippedWeaponDefinition.GetMagazineSize());
        if (magazineSize <= 0)
        {
            AmmoDefinition ammoType = equippedWeaponDefinition.GetAmmoType();
            PlayerInventory inventory = ResolvePlayerInventory();
            if (ammoType != null && inventory != null)
                inventory.AddItem(ammoType, 1);

            return;
        }

        EnsureMagazineInitialized(equippedWeaponDefinition);
        int loadedRounds = GetLoadedMagazineRounds(equippedWeaponDefinition);
        int clampedRounds = Mathf.Clamp(loadedRounds + 1, 0, magazineSize);
        SetLoadedMagazineRounds(equippedWeaponDefinition, clampedRounds);
    }

    private void RestoreConsumedFirearmRounds(WeaponDefinition equippedWeaponDefinition, int roundCount)
    {
        if (roundCount <= 0)
            return;

        if (IsGodModeEnabled())
            return;

        for (int i = 0; i < roundCount; i++)
            RestoreConsumedPistolRound(equippedWeaponDefinition);
    }

    private int ResolveFirearmRoundCountForCurrentShot(WeaponDefinition equippedWeaponDefinition)
    {
        if (equippedWeaponDefinition == null)
            return 0;

        int availableRounds = GetAvailableFirearmRounds(equippedWeaponDefinition);
        if (availableRounds <= 0)
            return 0;

        if (IsDoubleBarrelShotgunEquipped())
            return 1;

        if (TryResolveDoubleBarrelGunMarkers(out _, out _) == false)
            return 1;

        return Mathf.Clamp(availableRounds, 1, MaxDoubleBarrelRoundCount);
    }

    private bool IsDoubleBarrelShotgunEquipped()
    {
        if (playerWeaponController == null)
            return false;

        if (playerWeaponController.GetCurrentCategory() != PlayerWeaponController.WeaponCategory.Shotgun)
            return false;

        PlayerWeaponController.WeaponEntry equippedWeapon = playerWeaponController.GetCurrentWeapon();
        if (equippedWeapon == null)
            return false;

        return string.Equals(
            equippedWeapon.WeaponName,
            DoubleBarrelShotgunWeaponName,
            StringComparison.OrdinalIgnoreCase);
    }

    private int GetAvailableFirearmRounds(WeaponDefinition equippedWeaponDefinition)
    {
        if (equippedWeaponDefinition == null)
            return 0;

        int magazineSize = Mathf.Max(0, equippedWeaponDefinition.GetMagazineSize());

        if (IsGodModeEnabled())
            return Mathf.Max(1, magazineSize);

        if (magazineSize <= 0)
            return Mathf.Max(0, GetReserveAmmoCount(equippedWeaponDefinition));

        EnsureMagazineInitialized(equippedWeaponDefinition);
        return Mathf.Max(0, GetLoadedMagazineRounds(equippedWeaponDefinition));
    }

    private bool CanReloadPistol(WeaponDefinition equippedWeaponDefinition)
    {
        if (equippedWeaponDefinition == null) return false;

        int magazineSize = Mathf.Max(0, equippedWeaponDefinition.GetMagazineSize());
        if (magazineSize <= 0) return false;

        EnsureMagazineInitialized(equippedWeaponDefinition);

        int loadedRounds = GetLoadedMagazineRounds(equippedWeaponDefinition);
        if (loadedRounds >= magazineSize) return false;

        if (IsGodModeEnabled())
            return true;

        return GetReserveAmmoCount(equippedWeaponDefinition) > 0;
    }

    private void ReloadPistolMagazine(WeaponDefinition equippedWeaponDefinition)
    {
        if (equippedWeaponDefinition == null) return;

        int magazineSize = Mathf.Max(0, equippedWeaponDefinition.GetMagazineSize());
        if (magazineSize <= 0) return;

        EnsureMagazineInitialized(equippedWeaponDefinition);

        int loadedRounds = GetLoadedMagazineRounds(equippedWeaponDefinition);
        int roundsNeeded = magazineSize - loadedRounds;
        if (roundsNeeded <= 0) return;

        if (IsGodModeEnabled())
        {
            SetLoadedMagazineRounds(equippedWeaponDefinition, magazineSize);
            return;
        }

        int reserveAmmo = GetReserveAmmoCount(equippedWeaponDefinition);
        int roundsToLoad = Mathf.Min(roundsNeeded, reserveAmmo);
        if (roundsToLoad <= 0) return;

        if (TryConsumeReserveAmmo(equippedWeaponDefinition, roundsToLoad) == false)
            return;

        SetLoadedMagazineRounds(equippedWeaponDefinition, loadedRounds + roundsToLoad);
    }

    private void EnsureMagazineInitialized(WeaponDefinition equippedWeaponDefinition)
    {
        if (equippedWeaponDefinition == null) return;

        int magazineSize = Mathf.Max(0, equippedWeaponDefinition.GetMagazineSize());
        if (magazineSize <= 0) return;

        if (TryGetWeaponAmmoKey(equippedWeaponDefinition, out string weaponAmmoKey) == false)
            return;

        if (loadedRoundsByWeaponKey.ContainsKey(weaponAmmoKey) == true)
            return;

        // First-time initialization should never auto-fill from reserve.
        int initialLoadedRounds = 0;
        if (IsGodModeEnabled())
            initialLoadedRounds = magazineSize;
        else if (playerWeaponController != null)
            initialLoadedRounds = Mathf.Max(0, playerWeaponController.GetCurrentWeaponAmmo());

        SetLoadedMagazineRounds(equippedWeaponDefinition, Mathf.Clamp(initialLoadedRounds, 0, magazineSize));
    }

    private int GetLoadedMagazineRounds(WeaponDefinition equippedWeaponDefinition)
    {
        if (equippedWeaponDefinition == null) return 0;

        if (TryGetWeaponAmmoKey(equippedWeaponDefinition, out string weaponAmmoKey) == false)
            return 0;

        if (loadedRoundsByWeaponKey.TryGetValue(weaponAmmoKey, out int loadedRounds) == false)
            return 0;

        return Mathf.Max(0, loadedRounds);
    }

    private void SetLoadedMagazineRounds(WeaponDefinition equippedWeaponDefinition, int loadedRounds)
    {
        if (equippedWeaponDefinition == null) return;

        if (TryGetWeaponAmmoKey(equippedWeaponDefinition, out string weaponAmmoKey) == false)
            return;

        int magazineSize = Mathf.Max(0, equippedWeaponDefinition.GetMagazineSize());
        int clampedRounds = magazineSize > 0
            ? Mathf.Clamp(loadedRounds, 0, magazineSize)
            : Mathf.Max(0, loadedRounds);
        loadedRoundsByWeaponKey[weaponAmmoKey] = clampedRounds;
        PersistLoadedRoundsToBoundWeaponInstance(equippedWeaponDefinition, clampedRounds);
    }

    private void PersistLoadedRoundsToBoundWeaponInstance(WeaponDefinition equippedWeaponDefinition, int loadedRounds)
    {
        if (equippedWeaponDefinition == null) return;
        if (playerWeaponController == null) return;

        string equippedInstanceId = playerWeaponController.GetEquippedInventoryWeaponInstanceId();
        if (string.IsNullOrWhiteSpace(equippedInstanceId)) return;

        PlayerInventory inventory = ResolvePlayerInventory();
        if (inventory == null) return;

        inventory.TrySetWeaponMagazineRoundsByInstanceId(equippedInstanceId, loadedRounds);
    }

    private bool TryConsumeReserveAmmo(WeaponDefinition equippedWeaponDefinition, int amount)
    {
        if (equippedWeaponDefinition == null) return false;
        if (amount <= 0) return true;

        if (IsGodModeEnabled())
            return true;

        AmmoDefinition ammoType = equippedWeaponDefinition.GetAmmoType();
        if (ammoType == null) return false;

        PlayerInventory inventory = ResolvePlayerInventory();
        if (inventory == null) return false;

        return inventory.RemoveItem(ammoType, amount);
    }

    private int GetReserveAmmoCount(WeaponDefinition equippedWeaponDefinition)
    {
        if (equippedWeaponDefinition == null) return 0;

        AmmoDefinition ammoType = equippedWeaponDefinition.GetAmmoType();
        if (ammoType == null) return 0;

        PlayerInventory inventory = ResolvePlayerInventory();
        if (inventory == null) return 0;

        return Mathf.Max(0, inventory.GetAmmoCount(ammoType));
    }

    private bool IsGodModeEnabled()
    {
        return playerState != null && playerState.GetGodMode();
    }

    private PlayerInventory ResolvePlayerInventory()
    {
        PlayerInventory inventory = playerInventory;
        if (inventory == null)
        {
            inventory = GetComponentInParent<PlayerInventory>();
            playerInventory = inventory;
        }

        if (inventory == null)
        {
            inventory = FindAnyObjectByType<PlayerInventory>();
            playerInventory = inventory;
        }

        if (inventory != subscribedInventory)
            RefreshInventorySubscription();

        return inventory;
    }

    private void RefreshInventorySubscription()
    {
        UnsubscribeFromInventoryChanges();

        PlayerInventory inventory = playerInventory;
        if (inventory == null)
            inventory = ResolvePlayerInventoryWithoutSubscription();

        if (inventory == null)
            return;

        subscribedInventory = inventory;
        subscribedInventory.OnInventoryChanged += OnPlayerInventoryChanged;
    }

    private PlayerInventory ResolvePlayerInventoryWithoutSubscription()
    {
        PlayerInventory inventory = playerInventory;
        if (inventory == null)
        {
            inventory = GetComponentInParent<PlayerInventory>();
            playerInventory = inventory;
        }

        if (inventory == null)
        {
            inventory = FindAnyObjectByType<PlayerInventory>();
            playerInventory = inventory;
        }

        return inventory;
    }

    private void UnsubscribeFromInventoryChanges()
    {
        if (subscribedInventory == null)
            return;

        subscribedInventory.OnInventoryChanged -= OnPlayerInventoryChanged;
        subscribedInventory = null;
    }

    private void OnPlayerInventoryChanged()
    {
        weaponDefinitionLookupCache.Clear();
        InvalidateResolvedWeaponDefinitionCache();
        ResetAmmoSyncTracking();
    }

    private bool TryGetWeaponAmmoKey(WeaponDefinition equippedWeaponDefinition, out string weaponAmmoKey)
    {
        weaponAmmoKey = null;
        if (equippedWeaponDefinition == null) return false;

        // Prefer equipped inventory instance id so duplicate weapons track separate magazines.
        if (playerWeaponController != null)
        {
            string equippedInstanceId = playerWeaponController.GetEquippedInventoryWeaponInstanceId();
            if (string.IsNullOrWhiteSpace(equippedInstanceId) == false)
            {
                weaponAmmoKey = "instance:" + equippedInstanceId.Trim();
                return true;
            }
        }

        string itemId = equippedWeaponDefinition.GetItemId();
        if (string.IsNullOrWhiteSpace(itemId) == false)
        {
            weaponAmmoKey = itemId;
            return true;
        }

        string displayName = equippedWeaponDefinition.GetDisplayName();
        if (string.IsNullOrWhiteSpace(displayName) == false)
        {
            weaponAmmoKey = displayName;
            return true;
        }

        if (string.IsNullOrWhiteSpace(equippedWeaponDefinition.name) == false)
        {
            weaponAmmoKey = equippedWeaponDefinition.name;
            return true;
        }

        return false;
    }

    private void SetNextPistolFireTime(WeaponDefinition weaponDefinition)
    {
        float now = Time.time;

        // Missing or invalid weapon data means no cooldown lockout.
        if (weaponDefinition == null)
        {
            nextPistolFireAllowedTime = now;
            return;
        }

        float fireRate = weaponDefinition.GetFireRate();
        if (fireRate <= 0f)
        {
            nextPistolFireAllowedTime = now;
            return;
        }

        float secondsPerShot = 1f / fireRate;
        nextPistolFireAllowedTime = now + secondsPerShot;
    }

    private Transform ResolveGunMarker()
    {
        if (gunPointProvider == null)
            gunPointProvider = GetComponentInChildren<GetGunPoint>(true);

        if (gunPointProvider == null)
            gunPointProvider = GetComponentInParent<GetGunPoint>();

        if (gunPointProvider == null) return null;
        return gunPointProvider.GetGunMarker();
    }

    private Transform ResolveSingleRoundGunMarkerForCurrentShot()
    {
        if (TryResolveDoubleBarrelGunMarkers(out Transform leftGunMarker, out Transform rightGunMarker) == false)
            return ResolveGunMarker();

        Transform selectedGunMarker = nextSingleRoundDoubleBarrelUsesLeft ? leftGunMarker : rightGunMarker;
        if (selectedGunMarker == null)
            selectedGunMarker = leftGunMarker != null ? leftGunMarker : rightGunMarker;

        nextSingleRoundDoubleBarrelUsesLeft = !nextSingleRoundDoubleBarrelUsesLeft;
        return selectedGunMarker;
    }

    private bool TryResolveDoubleBarrelGunMarkers(out Transform leftGunMarker, out Transform rightGunMarker)
    {
        leftGunMarker = null;
        rightGunMarker = null;

        if (playerWeaponController == null)
            return false;

        if (playerWeaponController.GetCurrentCategory() != PlayerWeaponController.WeaponCategory.Shotgun)
        {
            ResetDoubleBarrelGunMarkerCache();
            return false;
        }

        Transform referenceGunMarker = ResolveGunMarker();
        if (TryGetCachedDoubleBarrelGunMarkers(referenceGunMarker, out leftGunMarker, out rightGunMarker))
            return true;

        for (Transform searchRoot = referenceGunMarker; searchRoot != null; searchRoot = searchRoot.parent)
        {
            leftGunMarker = FindNamedGunMarker(searchRoot, LeftDoubleBarrelGunpointName);
            rightGunMarker = FindNamedGunMarker(searchRoot, RightDoubleBarrelGunpointName);
            if (leftGunMarker != null && rightGunMarker != null)
            {
                CacheDoubleBarrelGunMarkers(referenceGunMarker, leftGunMarker, rightGunMarker);
                return true;
            }
        }

        if (leftGunMarker == null || rightGunMarker == null)
        {
            Transform providerRoot = gunPointProvider != null
                ? gunPointProvider.transform
                : transform.root;

            if (leftGunMarker == null)
                leftGunMarker = FindNamedGunMarker(providerRoot, LeftDoubleBarrelGunpointName);

            if (rightGunMarker == null)
                rightGunMarker = FindNamedGunMarker(providerRoot, RightDoubleBarrelGunpointName);
        }

        bool foundBothMarkers = leftGunMarker != null && rightGunMarker != null;
        if (foundBothMarkers)
            CacheDoubleBarrelGunMarkers(referenceGunMarker, leftGunMarker, rightGunMarker);

        return foundBothMarkers;
    }

    private Transform FindNamedGunMarker(Transform searchRoot, string gunMarkerName)
    {
        if (searchRoot == null)
            return null;

        if (string.IsNullOrWhiteSpace(gunMarkerName))
            return null;

        if (string.Equals(searchRoot.name, gunMarkerName, StringComparison.OrdinalIgnoreCase) &&
            searchRoot.gameObject.activeInHierarchy)
        {
            return searchRoot;
        }

        for (int i = 0; i < searchRoot.childCount; i++)
        {
            Transform candidate = FindNamedGunMarker(searchRoot.GetChild(i), gunMarkerName);
            if (candidate != null)
                return candidate;
        }

        return null;
    }

    private bool TryGetCachedDoubleBarrelGunMarkers(
        Transform referenceGunMarker,
        out Transform leftGunMarker,
        out Transform rightGunMarker)
    {
        leftGunMarker = null;
        rightGunMarker = null;

        if (!hasCachedDoubleBarrelGunMarkers)
            return false;

        if (cachedDoubleBarrelReferenceGunMarker != referenceGunMarker)
        {
            ResetDoubleBarrelGunMarkerCache();
            return false;
        }

        if (!cachedDoubleBarrelLeftGunMarker ||
            !cachedDoubleBarrelRightGunMarker ||
            cachedDoubleBarrelLeftGunMarker.gameObject.activeInHierarchy == false ||
            cachedDoubleBarrelRightGunMarker.gameObject.activeInHierarchy == false)
        {
            ResetDoubleBarrelGunMarkerCache();
            return false;
        }

        leftGunMarker = cachedDoubleBarrelLeftGunMarker;
        rightGunMarker = cachedDoubleBarrelRightGunMarker;
        return true;
    }

    private void CacheDoubleBarrelGunMarkers(
        Transform referenceGunMarker,
        Transform leftGunMarker,
        Transform rightGunMarker)
    {
        cachedDoubleBarrelReferenceGunMarker = referenceGunMarker;
        cachedDoubleBarrelLeftGunMarker = leftGunMarker;
        cachedDoubleBarrelRightGunMarker = rightGunMarker;
        hasCachedDoubleBarrelGunMarkers = true;
    }

    private void ResetDoubleBarrelGunMarkerCache()
    {
        cachedDoubleBarrelReferenceGunMarker = null;
        cachedDoubleBarrelLeftGunMarker = null;
        cachedDoubleBarrelRightGunMarker = null;
        hasCachedDoubleBarrelGunMarkers = false;
    }

    private Vector3 ResolveShotDirection(Vector3 spawnPosition, Transform gunMarker)
    {
        // Primary path: always shoot from muzzle toward the current crosshair ray solution.
        if (TryGetCrosshairRay(out Ray crosshairRay) == true)
        {
            if (TryGetCrosshairWorldPoint(crosshairRay, out Vector3 crosshairWorldPoint) == true)
            {
                Vector3 toCrosshairPoint = crosshairWorldPoint - spawnPosition;
                if (toCrosshairPoint.sqrMagnitude > MinAimDirectionSqr)
                    return toCrosshairPoint.normalized;
            }

            Vector3 crosshairRayDirection = crosshairRay.direction;
            if (crosshairRayDirection.sqrMagnitude > MinAimDirectionSqr)
                return crosshairRayDirection.normalized;
        }

        if (gunMarker != null)
            return gunMarker.forward;

        return transform.forward;
    }

    private bool TryGetCrosshairWorldPoint(out Vector3 worldPoint)
    {
        if (TryGetCrosshairRay(out Ray crosshairRay) == false)
        {
            worldPoint = Vector3.zero;
            return false;
        }

        return TryGetCrosshairWorldPoint(crosshairRay, out worldPoint);
    }

    private bool TryGetCrosshairRay(out Ray crosshairRay)
    {
        Camera camera = ResolveAimCamera();

        if (camera == null)
        {
            crosshairRay = default;
            return false;
        }

        Vector2 screenPoint = GetCurrentCrosshairScreenPoint();
        crosshairRay = camera.ScreenPointToRay(screenPoint);
        return true;
    }

    private bool TryGetCrosshairWorldPoint(Ray crosshairRay, out Vector3 worldPoint)
    {
        float rayDistance = useInfiniteCrosshairRayDistance == true
            ? Mathf.Infinity
            : Mathf.Max(MinRayDistance, crosshairRayDistance);

        int hitCount = Physics.RaycastNonAlloc(
            crosshairRay,
            crosshairRaycastHits,
            rayDistance,
            crosshairHitLayers,
            QueryTriggerInteraction.Ignore);

        // If the non-alloc buffer was saturated, rerun once with RaycastAll so nearest-hit selection is complete.
        if (hitCount >= crosshairRaycastHits.Length)
        {
            RaycastHit[] allHits = Physics.RaycastAll(
                crosshairRay,
                rayDistance,
                crosshairHitLayers,
                QueryTriggerInteraction.Ignore);

            if (TryGetNearestValidCrosshairHitPoint(allHits, allHits.Length, out Vector3 saturatedNearestPoint) == true)
            {
                worldPoint = saturatedNearestPoint;
                return true;
            }
        }

        if (TryGetNearestValidCrosshairHitPoint(crosshairRaycastHits, hitCount, out Vector3 nearestPoint) == true)
        {
            worldPoint = nearestPoint;
            return true;
        }

        worldPoint = crosshairRay.GetPoint(Mathf.Max(MinRayDistance, crosshairFallbackDistance));
        return true;
    }

    private bool TryGetNearestValidCrosshairHitPoint(RaycastHit[] hits, int hitCount, out Vector3 nearestPoint)
    {
        nearestPoint = Vector3.zero;
        if (hits == null || hitCount <= 0)
            return false;

        float nearestDistance = float.PositiveInfinity;
        bool foundValidHit = false;
        Transform playerRoot = transform.root;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit currentHit = hits[i];
            Collider hitCollider = currentHit.collider;
            if (hitCollider == null)
                continue;

            if (playerRoot != null && hitCollider.transform.IsChildOf(playerRoot))
                continue;

            if (currentHit.distance < nearestDistance)
            {
                nearestDistance = currentHit.distance;
                nearestPoint = currentHit.point;
                foundValidHit = true;
            }
        }

        return foundValidHit;
    }

    public Vector2 GetCurrentCrosshairScreenPoint()
    {
        int frame = Time.frameCount;
        if (cachedCrosshairScreenPointFrame == frame)
            return cachedCrosshairScreenPoint;

        cachedCrosshairScreenPoint = GetBaseCrosshairScreenPoint() + GetGunPointRecoilCrosshairOffset();
        cachedCrosshairScreenPointFrame = frame;
        return cachedCrosshairScreenPoint;
    }

    private Vector2 GetBaseCrosshairScreenPoint()
    {
        PlayerState state = playerState;

        // Combat mode crosshair follows mouse position.
        if (state != null && state.GetCombatMode() == true)
        {
            if (TryGetFrozenCombatOrbitCrosshairScreenPoint(out Vector2 frozenScreenPoint))
                return frozenScreenPoint;

            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();
        }

        // Non-combat or no mouse defaults to center.
        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }

    private bool TryGetFrozenCombatOrbitCrosshairScreenPoint(out Vector2 screenPoint)
    {
        if (cameraRigOrbit == null)
            cameraRigOrbit = FindAnyObjectByType<CameraRigOrbit>();

        if (cameraRigOrbit != null &&
            cameraRigOrbit.TryGetCombatOrbitFrozenCrosshairScreenPoint(out screenPoint))
        {
            return true;
        }

        screenPoint = Vector2.zero;
        return false;
    }

    private Vector2 GetGunPointRecoilCrosshairOffset()
    {
        if (!ShouldApplyGunPointRecoilCrosshairOffset())
        {
            ResetRecoilCrosshairTracking();
            return Vector2.zero;
        }

        if (TryCollectActiveRecoilCrosshairGunMarkers(out int markerCount) == false)
        {
            ResetRecoilCrosshairTracking();
            return Vector2.zero;
        }

        Camera camera = ResolveAimCamera();
        if (camera == null)
        {
            ResetRecoilCrosshairTracking();
            return Vector2.zero;
        }

        if (TryGetGunMarkerCentroidScreenPoint(camera, markerCount, out Vector2 currentMarkerScreenPoint) == false)
        {
            ResetRecoilCrosshairTracking();
            return Vector2.zero;
        }

        int markerSignature = BuildRecoilCrosshairMarkerSignature(markerCount);
        if (markerSignature != recoilCrosshairMarkerSignature)
        {
            recoilCrosshairMarkerSignature = markerSignature;
            hasRecoilCrosshairBaseline = false;
        }

        bool recoilSettled = firearmRecoilDriver == null || firearmRecoilDriver.IsRecoilSettled();
        if (!hasRecoilCrosshairBaseline || recoilSettled)
        {
            recoilCrosshairBaselineScreenPoint = currentMarkerScreenPoint;
            hasRecoilCrosshairBaseline = true;
            return Vector2.zero;
        }

        // Follow only vertical muzzle movement so kick-up pulls the crosshair with it.
        float verticalScreenDelta = currentMarkerScreenPoint.y - recoilCrosshairBaselineScreenPoint.y;
        return new Vector2(0f, verticalScreenDelta);
    }

    private bool ShouldApplyGunPointRecoilCrosshairOffset()
    {
        if (playerState == null || playerState.GetCombatMode() == false)
            return false;

        if (playerWeaponController == null)
            return false;

        return IsSupportedFirearmCategory(playerWeaponController.GetCurrentCategory());
    }

    private bool TryCollectActiveRecoilCrosshairGunMarkers(out int markerCount)
    {
        recoilCrosshairGunMarkers.Clear();

        if (TryResolveDoubleBarrelGunMarkers(out Transform leftGunMarker, out Transform rightGunMarker))
        {
            AddRecoilCrosshairGunMarker(leftGunMarker);
            AddRecoilCrosshairGunMarker(rightGunMarker);
        }

        AddRecoilCrosshairGunMarker(ResolveGunMarker());

        markerCount = recoilCrosshairGunMarkers.Count;
        return markerCount > 0;
    }

    private void AddRecoilCrosshairGunMarker(Transform marker)
    {
        if (marker == null || marker.gameObject.activeInHierarchy == false)
            return;

        for (int i = 0; i < recoilCrosshairGunMarkers.Count; i++)
        {
            if (recoilCrosshairGunMarkers[i] == marker)
                return;
        }

        recoilCrosshairGunMarkers.Add(marker);
    }

    private bool TryGetGunMarkerCentroidScreenPoint(Camera camera, int markerCount, out Vector2 screenPoint)
    {
        screenPoint = Vector2.zero;
        if (camera == null || markerCount <= 0)
            return false;

        Vector3 worldPositionSum = Vector3.zero;
        int validMarkerCount = 0;

        for (int i = 0; i < markerCount; i++)
        {
            Transform marker = recoilCrosshairGunMarkers[i];
            if (marker == null)
                continue;

            worldPositionSum += marker.position;
            validMarkerCount++;
        }

        if (validMarkerCount <= 0)
            return false;

        Vector3 worldCentroid = worldPositionSum / validMarkerCount;
        Vector3 projected = camera.WorldToScreenPoint(worldCentroid);
        if (projected.z <= 0f)
            return false;

        if (float.IsNaN(projected.x) || float.IsNaN(projected.y))
            return false;

        screenPoint = new Vector2(projected.x, projected.y);
        return true;
    }

    private int BuildRecoilCrosshairMarkerSignature(int markerCount)
    {
        unchecked
        {
            int hash = 17;

            for (int i = 0; i < markerCount; i++)
            {
                Transform marker = recoilCrosshairGunMarkers[i];
                int markerId = marker != null ? marker.GetHashCode() : 0;
                hash = (hash * 31) + markerId;
            }

            return hash;
        }
    }

    private void ResetRecoilCrosshairTracking()
    {
        hasRecoilCrosshairBaseline = false;
        recoilCrosshairMarkerSignature = 0;
        recoilCrosshairGunMarkers.Clear();
    }

    private Camera ResolveAimCamera()
    {
        Camera camera = aimCamera;
        if (camera == null || camera.isActiveAndEnabled == false)
        {
            camera = Camera.main;
            if (camera != null)
                aimCamera = camera;
        }

        return camera;
    }

    private WeaponDefinition ResolveCurrentWeaponDefinition()
    {
        PlayerWeaponController weaponController = playerWeaponController;
        if (weaponController == null) return null;

        PlayerWeaponController.WeaponEntry equippedWeapon = weaponController.GetCurrentWeapon();
        if (equippedWeapon == null)
        {
            InvalidateResolvedWeaponDefinitionCache();
            return null;
        }

        string equippedWeaponName = equippedWeapon.WeaponName;
        if (string.IsNullOrWhiteSpace(equippedWeaponName))
        {
            InvalidateResolvedWeaponDefinitionCache();
            return null;
        }

        string equippedWeaponCacheKey = BuildEquippedWeaponCacheKey(equippedWeapon);
        if (hasCachedResolvedWeaponDefinition &&
            string.Equals(cachedResolvedWeaponKey, equippedWeaponCacheKey, StringComparison.Ordinal))
        {
            return cachedResolvedWeaponDefinition;
        }

        PlayerInventory inventory = ResolvePlayerInventory();
        if (inventory == null)
        {
            CacheResolvedWeaponDefinition(equippedWeaponCacheKey, null);
            return null;
        }

        var weaponEntries = inventory.GetCategoryItems(PlayerInventory.InventoryCategory.Weapons);
        if (weaponEntries == null)
        {
            CacheResolvedWeaponDefinition(equippedWeaponCacheKey, null);
            return null;
        }

        for (int i = 0; i < weaponEntries.Count; i++)
        {
            PlayerInventory.InventoryEntry inventoryEntry = weaponEntries[i];
            if (inventoryEntry == null) continue;

            if (!(inventoryEntry.GetItemDefinition() is WeaponDefinition weaponDefinition))
                continue;

            if (DoesWeaponNameMatch(weaponDefinition, equippedWeaponName) == true)
            {
                CacheResolvedWeaponDefinition(equippedWeaponCacheKey, weaponDefinition);
                return weaponDefinition;
            }
        }

        if (weaponDefinitionLookupCache.TryGetValue(equippedWeaponCacheKey, out WeaponDefinition cachedWeaponDefinition)
            && cachedWeaponDefinition != null)
        {
            CacheResolvedWeaponDefinition(equippedWeaponCacheKey, cachedWeaponDefinition);
            return cachedWeaponDefinition;
        }

        WeaponDefinition[] loadedWeaponDefinitions = Resources.FindObjectsOfTypeAll<WeaponDefinition>();
        for (int i = 0; i < loadedWeaponDefinitions.Length; i++)
        {
            WeaponDefinition weaponDefinition = loadedWeaponDefinitions[i];
            if (weaponDefinition == null) continue;

            if (DoesWeaponNameMatch(weaponDefinition, equippedWeaponName) == false)
                continue;

            weaponDefinitionLookupCache[equippedWeaponCacheKey] = weaponDefinition;
            CacheResolvedWeaponDefinition(equippedWeaponCacheKey, weaponDefinition);
            return weaponDefinition;
        }

        CacheResolvedWeaponDefinition(equippedWeaponCacheKey, null);
        return null;
    }

    private static string BuildEquippedWeaponCacheKey(PlayerWeaponController.WeaponEntry equippedWeapon)
    {
        if (equippedWeapon == null) return string.Empty;
        return ((int)equippedWeapon.Category) + "|" + (equippedWeapon.WeaponName ?? string.Empty);
    }

    private void CacheResolvedWeaponDefinition(string weaponKey, WeaponDefinition weaponDefinition)
    {
        cachedResolvedWeaponKey = weaponKey ?? string.Empty;
        cachedResolvedWeaponDefinition = weaponDefinition;
        hasCachedResolvedWeaponDefinition = true;
    }

    private void InvalidateResolvedWeaponDefinitionCache()
    {
        cachedResolvedWeaponKey = string.Empty;
        cachedResolvedWeaponDefinition = null;
        hasCachedResolvedWeaponDefinition = false;
    }

    private bool DoesWeaponNameMatch(WeaponDefinition weaponDefinition, string equippedWeaponName)
    {
        if (weaponDefinition == null) return false;
        if (string.IsNullOrWhiteSpace(equippedWeaponName)) return false;

        if (string.Equals(weaponDefinition.GetItemId(), equippedWeaponName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(weaponDefinition.GetDisplayName(), equippedWeaponName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(weaponDefinition.name, equippedWeaponName, StringComparison.OrdinalIgnoreCase))
            return true;

        // Final fallback ignores punctuation/spacing differences (e.g. "Self-Loading Pistol" vs "SelfLoadingPistol").
        string normalizedDefinitionDisplayName = NormalizeWeaponName(weaponDefinition.GetDisplayName());
        string normalizedDefinitionAssetName = NormalizeWeaponName(weaponDefinition.name);
        string normalizedDefinitionItemId = NormalizeWeaponName(weaponDefinition.GetItemId());
        string normalizedEquippedWeaponName = NormalizeWeaponName(equippedWeaponName);

        if (string.IsNullOrWhiteSpace(normalizedEquippedWeaponName))
            return false;

        if (string.Equals(normalizedDefinitionDisplayName, normalizedEquippedWeaponName, StringComparison.Ordinal))
            return true;

        if (string.Equals(normalizedDefinitionAssetName, normalizedEquippedWeaponName, StringComparison.Ordinal))
            return true;

        return string.Equals(normalizedDefinitionItemId, normalizedEquippedWeaponName, StringComparison.Ordinal);
    }

    private string NormalizeWeaponName(string weaponName)
    {
        if (string.IsNullOrWhiteSpace(weaponName)) return string.Empty;

        int inputLength = weaponName.Length;
        char[] normalizedChars = new char[inputLength];
        int normalizedCount = 0;

        for (int i = 0; i < inputLength; i++)
        {
            char currentChar = char.ToLowerInvariant(weaponName[i]);
            if (char.IsLetterOrDigit(currentChar) == false)
                continue;

            normalizedChars[normalizedCount] = currentChar;
            normalizedCount++;
        }

        return new string(normalizedChars, 0, normalizedCount);
    }

    private bool IsFirearmAttackStateActive()
    {
        FirearmAnimatorStatesGroup configuredStates = ResolveFirearmAnimatorStates();
        return IsAnimatorInAnyConfiguredState(configuredStates.firearmAttackStateNames);
    }

    private bool IsReloadStateActive()
    {
        FirearmAnimatorStatesGroup configuredStates = ResolveFirearmAnimatorStates();
        return IsAnimatorInAnyConfiguredState(configuredStates.reloadStateNames);
    }

    private bool IsAnimatorInAnyConfiguredState(List<string> configuredStateNames)
    {
        if (configuredStateNames == null || configuredStateNames.Count == 0)
            return false;

        // Uses layer 0 for movement/combat states.
        const int BaseLayer = 0;

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(BaseLayer);
        if (MatchesAnyAnimatorStateByName(currentState, configuredStateNames))
        {
            return true;
        }

        if (animator.IsInTransition(BaseLayer) == false) return false;

        AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(BaseLayer);
        return MatchesAnyAnimatorStateByName(nextState, configuredStateNames);
    }

    private static int ToAnimatorStateShortNameHash(string stateName)
    {
        if (string.IsNullOrWhiteSpace(stateName))
            return 0;

        return Animator.StringToHash(stateName.Trim());
    }

    private static bool MatchesAnyAnimatorStateByName(AnimatorStateInfo stateInfo, List<string> configuredStateNames)
    {
        if (configuredStateNames == null || configuredStateNames.Count == 0)
            return false;

        for (int i = 0; i < configuredStateNames.Count; i++)
        {
            int stateHash = ToAnimatorStateShortNameHash(configuredStateNames[i]);
            if (stateHash != 0 && stateInfo.shortNameHash == stateHash)
                return true;
        }

        return false;
    }

    private FirearmAnimatorStatesGroup ResolveFirearmAnimatorStates()
    {
        if (firearmAnimatorStates == null)
            firearmAnimatorStates = new FirearmAnimatorStatesGroup();

        return firearmAnimatorStates;
    }

    private ShotgunPelletSimulationGroup ResolveShotgunPelletSimulation()
    {
        if (shotgunPelletSimulation == null)
            shotgunPelletSimulation = new ShotgunPelletSimulationGroup();

        return shotgunPelletSimulation;
    }

    private GunshotProjectionGroup ResolveGunshotProjection()
    {
        if (gunshotProjection == null)
            gunshotProjection = new GunshotProjectionGroup();

        return gunshotProjection;
    }

    private void ClampGunshotProjectionSettings()
    {
        GunshotProjectionGroup settings = ResolveGunshotProjection();
        settings.baseHearingRadius = Mathf.Max(0f, settings.baseHearingRadius);
        settings.roundDiameterRadiusScale = Mathf.Max(0f, settings.roundDiameterRadiusScale);
        settings.muzzleVelocityRadiusScale = Mathf.Max(0f, settings.muzzleVelocityRadiusScale);
        settings.additionalRoundRadiusScale = Mathf.Max(0f, settings.additionalRoundRadiusScale);
    }

    private void SyncEquippedWeaponAmmoWithController()
    {
        if (playerWeaponController == null) return;

        WeaponDefinition equippedWeaponDefinition = ResolveCurrentWeaponDefinition();
        if (equippedWeaponDefinition == null)
        {
            ResetAmmoSyncTracking();
            return;
        }

        AmmoDefinition ammoType = equippedWeaponDefinition.GetAmmoType();
        if (ammoType == null)
        {
            ResetAmmoSyncTracking();
            return;
        }

        if (TryGetWeaponAmmoKey(equippedWeaponDefinition, out string weaponAmmoKey) == false)
        {
            ResetAmmoSyncTracking();
            return;
        }

        int magazineSize = Mathf.Max(0, equippedWeaponDefinition.GetMagazineSize());
        int runtimeLoadedRounds = magazineSize > 0 ? GetLoadedMagazineRounds(equippedWeaponDefinition) : 0;
        int runtimeReserveRounds = GetReserveAmmoCount(equippedWeaponDefinition);
        int controllerLoadedRounds = Mathf.Max(0, playerWeaponController.GetCurrentWeaponAmmo());
        int controllerReserveRounds = Mathf.Max(0, playerWeaponController.GetCurrentWeaponReserveAmmo());
        bool hasRuntimeMagazineState = magazineSize > 0 && loadedRoundsByWeaponKey.ContainsKey(weaponAmmoKey);

        bool weaponChanged = string.Equals(syncedAmmoWeaponKey, weaponAmmoKey, StringComparison.OrdinalIgnoreCase) == false;
        if (weaponChanged)
        {
            // If this weapon instance has no runtime magazine entry yet, initialize from controller loaded rounds only.
            if (magazineSize > 0 && hasRuntimeMagazineState == false)
            {
                int desiredLoadedRounds = Mathf.Clamp(controllerLoadedRounds, 0, magazineSize);
                SetLoadedMagazineRounds(equippedWeaponDefinition, desiredLoadedRounds);
                ApplyRuntimeAmmoToController(equippedWeaponDefinition);
                UpdateAmmoSyncSnapshot(equippedWeaponDefinition, weaponAmmoKey);
                return;
            }

            // Bootstrap from inspector ammo only when runtime ammo is empty.
            bool runtimeIsEmpty = runtimeLoadedRounds <= 0 && runtimeReserveRounds <= 0;
            bool controllerHasAmmo = controllerLoadedRounds > 0 || controllerReserveRounds > 0;
            if (runtimeIsEmpty && controllerHasAmmo)
            {
                ApplyControllerAmmoToRuntime(equippedWeaponDefinition, controllerLoadedRounds, controllerReserveRounds);
            }
            else
            {
                ApplyRuntimeAmmoToController(equippedWeaponDefinition);
            }

            UpdateAmmoSyncSnapshot(equippedWeaponDefinition, weaponAmmoKey);
            return;
        }

        bool controllerChanged =
            controllerLoadedRounds != lastSyncedControllerLoadedRounds ||
            controllerReserveRounds != lastSyncedControllerReserveRounds;

        bool runtimeChanged =
            runtimeLoadedRounds != lastSyncedRuntimeLoadedRounds ||
            runtimeReserveRounds != lastSyncedRuntimeReserveRounds;

        // If both changed in the same frame, prefer explicit controller edits.
        if (controllerChanged)
        {
            ApplyControllerAmmoToRuntime(equippedWeaponDefinition, controllerLoadedRounds, controllerReserveRounds);
            UpdateAmmoSyncSnapshot(equippedWeaponDefinition, weaponAmmoKey);
            return;
        }

        if (runtimeChanged)
        {
            ApplyRuntimeAmmoToController(equippedWeaponDefinition);
            UpdateAmmoSyncSnapshot(equippedWeaponDefinition, weaponAmmoKey);
            return;
        }
    }

    private void ApplyControllerAmmoToRuntime(WeaponDefinition equippedWeaponDefinition, int controllerLoadedRounds, int controllerReserveRounds)
    {
        if (equippedWeaponDefinition == null) return;

        AmmoDefinition ammoType = equippedWeaponDefinition.GetAmmoType();
        if (ammoType == null) return;

        PlayerInventory inventory = ResolvePlayerInventory();
        if (inventory == null) return;

        int magazineSize = Mathf.Max(0, equippedWeaponDefinition.GetMagazineSize());
        int desiredLoadedRounds = magazineSize > 0
            ? Mathf.Clamp(controllerLoadedRounds, 0, magazineSize)
            : 0;

        int desiredReserveRounds = Mathf.Max(0, controllerReserveRounds);
        if (magazineSize <= 0)
            desiredReserveRounds = Mathf.Max(0, controllerReserveRounds + controllerLoadedRounds);

        if (magazineSize > 0)
            SetLoadedMagazineRounds(equippedWeaponDefinition, desiredLoadedRounds);

        int currentReserveRounds = Mathf.Max(0, inventory.GetAmmoCount(ammoType));
        int reserveDelta = desiredReserveRounds - currentReserveRounds;

        if (reserveDelta > 0)
            inventory.AddItem(ammoType, reserveDelta);
        else if (reserveDelta < 0)
            inventory.RemoveItem(ammoType, -reserveDelta);

        // Mirror clamped values back so inspector and runtime stay identical.
        ApplyRuntimeAmmoToController(equippedWeaponDefinition);
    }

    private void ApplyRuntimeAmmoToController(WeaponDefinition equippedWeaponDefinition)
    {
        if (equippedWeaponDefinition == null) return;
        if (playerWeaponController == null) return;

        int magazineSize = Mathf.Max(0, equippedWeaponDefinition.GetMagazineSize());
        int runtimeLoadedRounds = magazineSize > 0 ? GetLoadedMagazineRounds(equippedWeaponDefinition) : 0;
        int runtimeReserveRounds = GetReserveAmmoCount(equippedWeaponDefinition);

        if (playerWeaponController.GetCurrentWeaponAmmo() != runtimeLoadedRounds)
            playerWeaponController.SetCurrentWeaponAmmo(runtimeLoadedRounds);

        if (playerWeaponController.GetCurrentWeaponReserveAmmo() != runtimeReserveRounds)
            playerWeaponController.SetCurrentWeaponReserveAmmo(runtimeReserveRounds);
    }

    private void UpdateAmmoSyncSnapshot(WeaponDefinition equippedWeaponDefinition, string weaponAmmoKey)
    {
        if (equippedWeaponDefinition == null)
        {
            ResetAmmoSyncTracking();
            return;
        }

        syncedAmmoWeaponKey = string.IsNullOrWhiteSpace(weaponAmmoKey) ? string.Empty : weaponAmmoKey;

        int magazineSize = Mathf.Max(0, equippedWeaponDefinition.GetMagazineSize());
        lastSyncedRuntimeLoadedRounds = magazineSize > 0 ? GetLoadedMagazineRounds(equippedWeaponDefinition) : 0;
        lastSyncedRuntimeReserveRounds = GetReserveAmmoCount(equippedWeaponDefinition);

        if (playerWeaponController != null)
        {
            lastSyncedControllerLoadedRounds = Mathf.Max(0, playerWeaponController.GetCurrentWeaponAmmo());
            lastSyncedControllerReserveRounds = Mathf.Max(0, playerWeaponController.GetCurrentWeaponReserveAmmo());
            return;
        }

        lastSyncedControllerLoadedRounds = -1;
        lastSyncedControllerReserveRounds = -1;
    }

    private void ResetAmmoSyncTracking()
    {
        syncedAmmoWeaponKey = string.Empty;
        lastSyncedControllerLoadedRounds = -1;
        lastSyncedControllerReserveRounds = -1;
        lastSyncedRuntimeLoadedRounds = -1;
        lastSyncedRuntimeReserveRounds = -1;
    }
}
