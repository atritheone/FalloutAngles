using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class NPCCombat : MonoBehaviour
{
    public struct KillhouseCombatTestSettings
    {
        public bool killhouseOnPlay;
        public bool showKillhouseNotes;
        public PlayerState playerState;
        public Transform playerTarget;
        public Transform playerAimTarget;
        public Transform playerKillhouseExit;
        public bool resetKillhouseOnResult;
        public bool autoFindPlayer;
        public bool usePlayerAimTarget;
        public bool huntPlayerWithoutSight;
        public bool preferFirearms;
        public bool useSpecificKillhouseInventoryWeapon;
        public string selectedKillhouseInventoryWeaponInstanceId;
        public bool crouchWhenShooting;
        public bool useCoverWhenDamaged;
        public bool jumpWhenStuck;
        public bool avoidSameFactionFriendlyFire;
        public LayerMask friendlyFireAvoidanceLayers;
        public float friendlyFireAvoidanceRadius;
        public LayerMask lineOfSightLayers;
        public LayerMask coverProbeLayers;
        public float sightRange;
        public float sightConeAngleDegrees;
        public float targetAcquisitionSeconds;
        public float meleeEngageRange;
        public float closeCombatDrawRange;
        public float closeCombatUndrawDelay;
        public float preferredFirearmRange;
        public float maxFirearmRange;
        public float closeRange;
        public float killhouseReachDistance;
        public float killhouseDecisionInterval;
        public float killhouseRepathInterval;
        public float killhouseActionInterval;
        public float reloadResumeDelay;
        public float coverSearchRadius;
        public int coverSearchSamples;
        public float coverMinPlayerDistance;
        public float retreatHealthPercent;
        public float damageReactionSeconds;
        public float damageHealthDelta;
        public float coverDamageHealthPercent;
        public float coverDamageReactionSeconds;
        public float stuckCheckInterval;
        public float stuckDistance;
        public float stuckJumpInterval;
        public int stuckChecksBeforeJump;
        public float searchAreaRadius;
        public int searchAreaSamples;
        public float broadSearchChance;
        public float searchMinPointSpacing;
        public float searchPointReachDistance;
        public float lostTargetEngageSeconds;
        public float lostTargetCombatEngageSeconds;
        public float lostTargetSearchSeconds;
        public float coverDestinationHoldSeconds;
        public float coverRetryDelay;
        public float aimLookAheadDistance;
        public float aimFollowSpeed;
        public float firearmShootAimToleranceDegrees;
        public float aimNotReadyRetryDelay;
        public float searchAimSweepAngle;
        public float searchAimSweepSpeed;
        public float killhouseResultBannerSeconds;
        public bool showKillhouseResultBanner;
        public bool alsoShowKillhouseResultOnHud;
        public float visibleTargetTurnSpeed;
        public float visibleTargetSnapAngle;
        public float lastKnownFacingHoldSeconds;
        public float weaponEquipSettleSeconds;
        public float weaponEquipActionLockSeconds;
        public float weaponUnequipActionLockSeconds;
        public float reloadActionLockSeconds;
        public float busyPollSeconds;
        public int animatorLayer;
        public string[] busyAnimatorStateNames;
    }

    private const string DoubleBarrelShotgunWeaponName = "Double-Barrel Shotgun";
    private const string WeaponHolderName = "WeaponHolder";
    private const string WeaponInHandName = "WeaponInHand";
    private const string LeftDoubleBarrelGunpointName = "GunpointLeft";
    private const string RightDoubleBarrelGunpointName = "GunpointRight";
    private const int MaxDoubleBarrelRoundCount = 2;
    private const float AutomaticWeaponSpreadJitterFraction = 0.25f;
    private const float MinAimDirectionSqr = 0.001f;
    private const float MinRayDistance = 0.01f;
    private const string KillhouseUnarmedWeaponSelectionId = "__KILLHOUSE_UNARMED__";
    private const float MinDirectionSqr = 0.0001f;
    private const float KillhouseStartupNavMeshSampleDistance = 2f;
    private const float DefaultLostTargetCombatEngageSeconds = 6f;
    private const float PlayerGunshotSneakAttackGraceSeconds = 1.25f;
    private const float PlayerGunshotSneakAttackFlightPaddingSeconds = 0.25f;
    private const float MaxPlayerGunshotSneakAttackGraceSeconds = 3f;
    private static readonly RaycastHit[] LineOfSightHits = new RaycastHit[16];
    private static readonly RaycastHit[] FriendlyFireHits = new RaycastHit[32];

    private enum KillhouseTactic
    {
        Idle,
        Chase,
        Attack,
        Flank,
        TakeCover,
        Search,
        Reload
    }

    public enum PlayerStealthThreatLevel
    {
        Hidden,
        Caution,
        Danger
    }


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
    private const float CriticalHitChance = 0.05f;
    private const float CriticalDamageMultiplier = 1.5f;

    private struct MeleeTargetHit
    {
        public BodyDamageArea bodyArea;
        public int specificityScore;
        public float sqrDistance;
    }

    [Serializable]
    private class FirearmAnimatorStates
    {
        public List<string> firearmAttackStateNames = new List<string>
        {
            "Pistol Walk",
            "Pistol Run",
            "Pistol Crouch",
            "Longarm Walk",
            "Longarm Run",
            "Longarm Crouch"
        };

        public List<string> reloadStateNames = new List<string>
        {
            "Pistol Reload",
            "Longarm Reload",
            "Pistol Crouch Reload",
            "Longarm Crouch Reload"
        };
    }

    [Serializable]
    private class MeleeAnimatorStates
    {
        public List<string> unarmedAttackStateNames = new List<string>
        {
            "Unarmed Walk",
            "Unarmed Run"
        };

        public List<string> knifeAttackStateNames = new List<string>
        {
            "Knife Walk",
            "Knife Run"
        };

        public List<string> twoHandedAttackStateNames = new List<string>
        {
            "Two Handed Walk",
            "Two Handed Run"
        };
    }

    [Serializable]
    private class ShotgunPellets
    {
        public bool enabled = true;
        [Min(1)] public int pelletsPerShot = 9;
        [Range(0f, 10f)] public float spreadAtMuzzleDegrees = 0.1f;
        [Range(0f, 10f)] public float spreadAtMaxDistanceDegrees = 2.2f;
        [Min(0.01f)] public float maxSpreadDistance = 35f;
        [Min(0f)] public float fallbackPelletMuzzleVelocity = 380f;
        [Min(0f)] public float pelletSpawnForwardOffset = 0.02f;
        public bool ignorePelletSelfCollisions = true;
        public GameObject pelletPrefabOverride;
    }

    private static readonly int PunchLeftParam = Animator.StringToHash("PunchLeft");
    private static readonly int PunchRightParam = Animator.StringToHash("PunchRight");
    private static readonly int UnarmedBlockParam = Animator.StringToHash("UnarmedBlock");
    private static readonly int KnifeBlockParam = Animator.StringToHash("KnifeBlock");
    private static readonly int TwoHandedBlockParam = Animator.StringToHash("TwoHandedBlock");
    private static readonly int StabParam = Animator.StringToHash("Stab");
    private static readonly int SlashParam = Animator.StringToHash("Slash");
    private static readonly int LeftStrikeParam = Animator.StringToHash("StrikeLeft");
    private static readonly int RightStrikeParam = Animator.StringToHash("StrikeRight");
    private static readonly int LeftPunchState = Animator.StringToHash("Left Punch");
    private static readonly int RightPunchState = Animator.StringToHash("Right Punch");
    private static readonly int StabState = Animator.StringToHash("Stab");
    private static readonly int SlashState = Animator.StringToHash("Slash");
    private static readonly int LeftStrikeState = Animator.StringToHash("Left Strike");
    private static readonly int RightStrikeState = Animator.StringToHash("Right Strike");
    private static readonly int PistolReloadState = Animator.StringToHash("Pistol Reload");
    private static readonly int LongarmReloadState = Animator.StringToHash("Longarm Reload");
    private static readonly int PistolCrouchReloadState = Animator.StringToHash("Pistol Crouch Reload");
    private static readonly int LongarmCrouchReloadState = Animator.StringToHash("Longarm Crouch Reload");
    private static readonly int PistolReloadParam = Animator.StringToHash("PistolReload");
    private static readonly int LongarmReloadParam = Animator.StringToHash("LongarmReload");

    [Header("References")]
    [SerializeField] private NPCMovement movement;
    [SerializeField] private NPCState npcState;
    [SerializeField] private NPCInventory npcInventory;
    [SerializeField] private NPCWeaponController weaponController;
    [SerializeField] private NPCAim aim;
    [SerializeField] private Animator animator;
    [SerializeField] private GetGunPoint gunPointProvider;
    [SerializeField] private Transform weaponHolder;
    [SerializeField] private Transform gunPoint;
    [SerializeField] private Transform secondaryGunPoint;

    private bool killhouseOnPlay = false;
    private bool showKillhouseNotes = true;
    private PlayerState playerState;
    private Transform playerTarget;
    private Transform playerAimTarget;
    private Transform playerKillhouseExit;
    private bool resetKillhouseOnResult = true;
    private bool autoFindPlayer = true;
    private bool usePlayerAimTarget = false;
    private bool huntPlayerWithoutSight = true;
    private bool preferFirearms = true;
    private bool useSpecificKillhouseInventoryWeapon = false;
    private string selectedKillhouseInventoryWeaponInstanceId = string.Empty;
    private bool crouchWhenShooting = false;
    private bool useCoverWhenDamaged = true;
    private bool jumpWhenStuck = true;
    private LayerMask lineOfSightLayers = ~0;
    private LayerMask coverProbeLayers = ~0;
    private float sightRange = 35f;
    private float sightConeAngleDegrees = 100f;
    private float targetAcquisitionSeconds = 0.2f;
    private float meleeEngageRange = 1.65f;
    private float closeCombatDrawRange = 3.25f;
    private float closeCombatUndrawDelay = 1.25f;
    private float preferredFirearmRange = 9f;
    private float maxFirearmRange = 24f;
    private float closeRange = 4f;
    private float killhouseReachDistance = 0.9f;
    private float killhouseDecisionInterval = 0.18f;
    private float killhouseRepathInterval = 0.35f;
    private float killhouseActionInterval = 0.35f;
    private float reloadResumeDelay = 0.75f;
    private float coverSearchRadius = 8f;
    private int coverSearchSamples = 18;
    private float coverMinPlayerDistance = 5f;
    private float retreatHealthPercent = 0.35f;
    private float damageReactionSeconds = 3f;
    private float damageHealthDelta = 0.5f;
    private float coverDamageHealthPercent = 0.18f;
    private float coverDamageReactionSeconds = 4f;
    private float stuckCheckInterval = 0.75f;
    private float stuckDistance = 0.15f;
    private float stuckJumpInterval = 1.2f;
    private int stuckChecksBeforeJump = 3;
    private float searchAreaRadius = 7f;
    private int searchAreaSamples = 16;
    private float broadSearchChance = 0.35f;
    private float searchMinPointSpacing = 3f;
    private float searchPointReachDistance = 1f;
    private float lostTargetEngageSeconds = 2.5f;
    private float lostTargetCombatEngageSeconds = DefaultLostTargetCombatEngageSeconds;
    private float lostTargetSearchSeconds = 8f;
    private float coverDestinationHoldSeconds = 1.25f;
    private float coverRetryDelay = 0.75f;
    private float aimLookAheadDistance = 12f;
    private float aimFollowSpeed = 9f;
    private float firearmShootAimToleranceDegrees = 10f;
    private float aimNotReadyRetryDelay = 0.08f;
    private float searchAimSweepAngle = 65f;
    private float searchAimSweepSpeed = 1.25f;
    private float killhouseResultBannerSeconds = 3f;
    private bool showKillhouseResultBanner = true;
    private bool alsoShowKillhouseResultOnHud = true;

    private float visibleTargetTurnSpeed = 1080f;
    private float visibleTargetSnapAngle = 0.5f;
    private float lastKnownFacingHoldSeconds = 2.5f;

    private float weaponEquipSettleSeconds = 0.35f;
    private float weaponEquipActionLockSeconds = 1.25f;
    private float weaponUnequipActionLockSeconds = 1.25f;
    private float reloadActionLockSeconds = 2.5f;
    private float busyPollSeconds = 0.1f;
    private int animatorLayer = 0;
    private string[] busyAnimatorStateNames =
    {
        "Left Punch",
        "Right Punch",
        "Unarmed Block",
        "Stab",
        "Slash",
        "Knife Block",
        "Left Strike",
        "Right Strike",
        "Two Handed Block",
        "Pistol Reload",
        "Longarm Reload",
        "Pistol Crouch Reload",
        "Longarm Crouch Reload",
        "Knife Equip",
        "Knife Unequip",
        "Two Handed Equip",
        "Two Handed Unequip",
        "Pistol Equip",
        "Pistol Unequip",
        "Longarm Equip",
        "Longarm Unequip"
    };

    [Header("Combat Rules")]
    [SerializeField] private bool requireCombatMode = true;
    [SerializeField] private bool requireWeaponInHand = true;
    [SerializeField] private bool requireFirearmAttackState = true;
    [SerializeField] private bool requireMeleeAttackState = true;
    [SerializeField] private bool reloadCompletesFromAnimationEvent = true;
    [SerializeField, Min(0.25f)] private float reloadFallbackCompleteSeconds = 2.5f;

    [Header("Friendly Fire Avoidance")]
    [SerializeField] private bool avoidSameFactionFriendlyFire = true;
    [SerializeField] private LayerMask friendlyFireAvoidanceLayers = ~0;
    [SerializeField, Min(0f)] private float friendlyFireAvoidanceRadius = 0.3f;

    [Header("Hearing")]
    [SerializeField] private bool hearPlayerFootsteps = true;
    [SerializeField, Min(0f)] private float footstepHearingMultiplier = 1f;
    [SerializeField, Range(0f, 1f)] private float occludedFootstepHearingMultiplier = 0.65f;
    [SerializeField, Min(0f)] private float footstepHearCooldown = 0.15f;
    [SerializeField] private bool hearPlayerGunshots = true;
    [SerializeField, Min(0f)] private float gunshotHearingMultiplier = 1f;
    [SerializeField, Range(0f, 1f)] private float occludedGunshotHearingMultiplier = 0.85f;
    [SerializeField, Min(0f)] private float gunshotHearCooldown = 0.05f;

    [Header("Melee Damage")]
    [SerializeField] private bool applyMeleeDamageImmediately = false;
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

    [Header("Projectile Aim")]
    [SerializeField, Min(0.01f)] private float fallbackAimDistance = 75f;
    [SerializeField, Min(0.01f)] private float shotgunFallbackDistance = 35f;
    [SerializeField] private bool requireFirearmFacingAim = true;
    [SerializeField, Range(1f, 90f)] private float firearmFacingToleranceDegrees = 12f;
    [SerializeField, Range(1f, 90f)] private float firearmMuzzleToleranceDegrees = 8f;

    [Header("Firearm Animator States")]
    [SerializeField] private FirearmAnimatorStates firearmAnimatorStates = new FirearmAnimatorStates();

    [Header("Melee Animator States")]
    [SerializeField] private MeleeAnimatorStates meleeAnimatorStates = new MeleeAnimatorStates();

    [Header("Shotgun Pellet Simulation")]
    [SerializeField] private ShotgunPellets shotgunPellets = new ShotgunPellets();

    private readonly Dictionary<string, int> loadedRoundsByWeaponKey =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, WeaponDefinition> weaponDefinitionLookupCache =
        new Dictionary<string, WeaponDefinition>(StringComparer.OrdinalIgnoreCase);

    private readonly Collider[] meleeHits = new Collider[64];
    private readonly Collider[] meleeWeaponHits = new Collider[32];
    private readonly Collider[] friendlyFireOverlapHits = new Collider[32];
    private readonly Dictionary<NPCState, MeleeTargetHit> meleeNpcHitSelections = new Dictionary<NPCState, MeleeTargetHit>();
    private readonly Dictionary<PlayerState, MeleeTargetHit> meleePlayerHitSelections = new Dictionary<PlayerState, MeleeTargetHit>();
    private readonly HashSet<int> meleeDamagedTargets = new HashSet<int>();
    private float nextFireAllowedTime;
    private float meleeDamageWindowEndTime;
    private float nextMeleeAttackAllowedTime;
    private int trackedMeleeAttackStateHash;
    private int trackedMeleeAttackLoop;
    private bool openedMeleeDamageForTrackedAnimation;
    private bool nextPunchIsLeft = true;
    private bool nextKnifeIsStab = true;
    private bool nextTwoHandedIsLeftStrike = true;
    private WeaponDefinition pendingReloadWeaponDefinition;
    private bool isReloadPending;
    private bool hasEnteredReloadState;
    private float reloadStartedTime;
    private string cachedResolvedWeaponKey = string.Empty;
    private WeaponDefinition cachedResolvedWeaponDefinition;
    private bool hasCachedResolvedWeaponDefinition;
    private Transform cachedGunPoint;
    private string cachedGunPointWeaponName = string.Empty;
    private Vector3 startPosition;
    private Vector3 killhouseStartPosition;
    private Quaternion killhouseStartRotation;
    private bool killhouseActive;
    private bool hasLastKnownPlayerPosition;
    private bool hasConfirmedThreatPosition;
    private bool hasKillhouseDestination;
    private bool hasCoverDestination;
    private bool hasPreviousCoverDestination;
    private bool hasSearchDestination;
    private Vector3 lastKnownPlayerPosition;
    private Vector3 confirmedThreatPosition;
    private Vector3 killhouseDestination;
    private Vector3 coverDestination;
    private Vector3 previousCoverDestination;
    private Vector3 searchDestination;
    private Vector3 previousSearchDestination;
    private Vector3 killhouseAimPoint;
    private Vector3 lastStuckCheckPosition;
    private float nextKillhouseDecisionTime;
    private float nextKillhouseRepathTime;
    private float nextKillhouseActionTime;
    private float nextKillhouseWeaponSwitchTime;
    private float nextStuckCheckTime;
    private float nextStuckJumpTime;
    private float coverDestinationHoldUntil;
    private float nextCoverSearchTime;
    private float lastObservedHealth;
    private float recentDamageUntil = -999f;
    private float recentCoverDamageUntil = -999f;
    private float killhouseReloadResumeTime;
    private float killhouseActionLockUntil;
    private float closeCombatDrawHoldUntil;
    private float nextFootstepHearTime = -999f;
    private float nextGunshotHearTime = -999f;
    private Transform recentPlayerGunshotSneakAttackRoot;
    private float recentPlayerGunshotSneakAttackUntil = -999f;
    private int searchProbeIndex;
    private int consecutiveStuckChecks;
    private int[] busyAnimatorStateHashes;
    private bool killhouseReloadInProgress;
    private bool killhouseActionLockActive;
    private bool killhouseAwaitingWeaponDrawCompletion;
    private bool hasKillhouseAimPoint;
    private bool coverHoldStarted;
    private bool lostTargetSearchActive;
    private bool returnToKillhousePatrolAfterLostTargetSearch;
    private bool resumeKillhousePatrolAfterLostTargetSearch;
    private bool inspectLastKnownBeforeSearch;
    private bool hasPreviousSearchDestination;
    private bool killhouseStartPending;
    private bool restartKillhouseWhenPlayerReturns;
    private bool hasKillhouseStartTransform;
    private bool killhouseResultResolving;
    private bool hasNoticedPlayer;
    private bool hasAcquiredVisiblePlayer;
    private string killhouseResultBannerText;
    private float killhouseResultBannerUntil;
    private float visiblePlayerAcquisitionStartedTime = -1f;
    private float lostTargetSearchUntil;
    private float lostSightStartedTime = -1f;
    private float lastDirectCombatContactTime = -999f;
    private float lastKnownFacingHoldUntil;
    private KillhouseTactic killhouseTactic = KillhouseTactic.Idle;


    private NPCState state
    {
        get => npcState;
        set => npcState = value;
    }

    public bool KillhouseOnPlay => killhouseOnPlay;
    public bool IsKillhouseCombatRunning => killhouseActive || killhouseStartPending;
    public bool IsReloadPending => isReloadPending;
    public WeaponDefinition CurrentWeaponDefinition => ResolveCurrentWeaponDefinition();

    public bool IsSearchingForPlayer()
    {
        if (state && state.IsDead())
            return false;

        return killhouseActive && (lostTargetSearchActive || killhouseTactic == KillhouseTactic.Search);
    }

    public bool IsAggroedOrSearchingForPlayer()
    {
        ResolveReferences();
        if (state && state.IsDead())
            return false;

        if (IsSearchingForPlayer())
            return true;

        return GetPlayerStealthThreatLevel() == PlayerStealthThreatLevel.Danger;
    }

    public bool ConsumeKillhousePatrolResumeRequest()
    {
        if (!resumeKillhousePatrolAfterLostTargetSearch)
            return false;

        resumeKillhousePatrolAfterLostTargetSearch = false;
        return true;
    }

    public PlayerStealthThreatLevel GetPlayerStealthThreatLevel()
    {
        ResolveReferences();
        if (!ResolvePlayerTarget())
            return PlayerStealthThreatLevel.Hidden;

        bool hasVisualContact = CanSeePlayer(ResolvePlayerAimPosition());
        bool canSeePlayer = hasAcquiredVisiblePlayer && hasVisualContact;
        if (IsActivelyEngagingKillhousePlayer(canSeePlayer))
            return PlayerStealthThreatLevel.Danger;

        return hasNoticedPlayer || canSeePlayer
            ? PlayerStealthThreatLevel.Caution
            : PlayerStealthThreatLevel.Hidden;
    }

    public bool CanReceivePlayerSneakAttack(Transform attackerRoot)
    {
        return CanReceivePlayerSneakAttack(attackerRoot, true);
    }

    private bool CanReceivePlayerSneakAttack(Transform attackerRoot, bool allowRecentGunshotGrace)
    {
        if (!attackerRoot)
            return false;

        PlayerState attackerState = attackerRoot.GetComponentInParent<PlayerState>();
        if (!attackerState)
            return false;

        ResolveReferences();
        if (state && state.IsDead())
            return false;

        if (allowRecentGunshotGrace && IsRecentPlayerGunshotSneakAttack(attackerRoot))
            return true;

        bool hasTrackedPlayer = ResolvePlayerTarget();
        if (hasTrackedPlayer && !IsTrackedPlayer(attackerState, attackerRoot))
            return true;

        if (lostTargetSearchActive || killhouseTactic == KillhouseTactic.Search)
            return false;

        if (hasNoticedPlayer)
            return false;

        if (!hasTrackedPlayer)
            return true;

        bool hasVisualContact = CanSeePlayer(ResolvePlayerAimPosition());
        bool canSeePlayer = hasAcquiredVisiblePlayer && hasVisualContact;
        return !IsActivelyEngagingKillhousePlayer(canSeePlayer);
    }

    private bool IsRecentPlayerGunshotSneakAttack(Transform attackerRoot)
    {
        if (!attackerRoot || !recentPlayerGunshotSneakAttackRoot)
            return false;

        if (Time.time > recentPlayerGunshotSneakAttackUntil)
            return false;

        return IsInstigatorTransform(attackerRoot, recentPlayerGunshotSneakAttackRoot);
    }

    private void CachePlayerGunshotSneakAttackGrace(PlayerCombat.GunshotSignal signal, bool wasSneakAttackEligible)
    {
        Transform attackerRoot = signal.sourceTransform;
        if (!attackerRoot)
            return;

        if (!wasSneakAttackEligible)
        {
            if (IsInstigatorTransform(attackerRoot, recentPlayerGunshotSneakAttackRoot))
                ClearPlayerGunshotSneakAttackGrace();

            return;
        }

        recentPlayerGunshotSneakAttackRoot = attackerRoot;
        recentPlayerGunshotSneakAttackUntil = Time.time + ResolvePlayerGunshotSneakAttackGraceSeconds(signal);
    }

    private float ResolvePlayerGunshotSneakAttackGraceSeconds(PlayerCombat.GunshotSignal signal)
    {
        float graceSeconds = PlayerGunshotSneakAttackGraceSeconds;
        float muzzleVelocity = Mathf.Max(0f, signal.muzzleVelocity);
        if (muzzleVelocity > MinDirectionSqr)
        {
            float distance = Vector3.Distance(ResolveEyePosition(), signal.position);
            graceSeconds = Mathf.Max(
                graceSeconds,
                distance / muzzleVelocity + PlayerGunshotSneakAttackFlightPaddingSeconds);
        }

        return Mathf.Clamp(
            graceSeconds,
            PlayerGunshotSneakAttackFlightPaddingSeconds,
            MaxPlayerGunshotSneakAttackGraceSeconds);
    }

    private void ClearPlayerGunshotSneakAttackGrace()
    {
        recentPlayerGunshotSneakAttackRoot = null;
        recentPlayerGunshotSneakAttackUntil = -999f;
    }

    private void HandlePlayerFootstepEmitted(PlayerMovement.FootstepSignal signal)
    {
        if (!hearPlayerFootsteps || !isActiveAndEnabled)
            return;

        if (!signal.sourceTransform)
            return;

        if (signal.sourceState && signal.sourceState.GetHealthPoints() <= 0f)
            return;

        ResolveReferences();
        if (state && state.IsDead())
            return;

        if (IsOwnTransform(signal.sourceTransform))
            return;

        if (Time.time < nextFootstepHearTime)
            return;

        float hearingRadius = Mathf.Max(0f, signal.hearingRadius) * Mathf.Max(0f, footstepHearingMultiplier);
        if (hearingRadius <= 0f)
            return;

        Vector3 listenerPosition = ResolveEyePosition();
        Vector3 sourcePosition = signal.position + Vector3.up * 0.5f;
        if (IsSoundOccluded(listenerPosition, sourcePosition, signal.sourceTransform, occludedFootstepHearingMultiplier))
            hearingRadius *= occludedFootstepHearingMultiplier;

        float distance = Vector3.Distance(listenerPosition, sourcePosition);
        if (distance > hearingRadius)
            return;

        HearPlayerFootstep(signal);
    }

    private void HearPlayerFootstep(PlayerMovement.FootstepSignal signal)
    {
        PlayerState heardPlayerState = signal.sourceState;
        if (!heardPlayerState && signal.sourceTransform)
            heardPlayerState = signal.sourceTransform.GetComponentInParent<PlayerState>();

        if (heardPlayerState)
        {
            playerState = heardPlayerState;
            playerTarget = heardPlayerState.transform;
            playerAimTarget = null;
        }

        bool needsCombatStart = !killhouseActive || killhouseStartPending;
        if (needsCombatStart)
        {
            StopExternalPatrolForSoundSearch();
            StartKillhouseCombat(true);
        }

        MarkPlayerNoticed();
        BeginLostTargetSearch(signal.position);
        nextFootstepHearTime = Time.time + footstepHearCooldown;
    }

    private void HandlePlayerGunshotEmitted(PlayerCombat.GunshotSignal signal)
    {
        if (!hearPlayerGunshots || !isActiveAndEnabled)
            return;

        if (!signal.sourceTransform)
            return;

        if (signal.sourceState && signal.sourceState.GetHealthPoints() <= 0f)
            return;

        ResolveReferences();
        if (state && state.IsDead())
            return;

        if (IsOwnTransform(signal.sourceTransform))
            return;

        if (Time.time < nextGunshotHearTime)
            return;

        float hearingRadius = Mathf.Max(0f, signal.hearingRadius) * Mathf.Max(0f, gunshotHearingMultiplier);
        if (hearingRadius <= 0f)
            return;

        Vector3 listenerPosition = ResolveEyePosition();
        Vector3 sourcePosition = signal.position + Vector3.up * 0.5f;
        if (IsSoundOccluded(listenerPosition, sourcePosition, signal.sourceTransform, occludedGunshotHearingMultiplier))
            hearingRadius *= occludedGunshotHearingMultiplier;

        float distance = Vector3.Distance(listenerPosition, sourcePosition);
        if (distance > hearingRadius)
            return;

        HearPlayerGunshot(signal);
    }

    private void HearPlayerGunshot(PlayerCombat.GunshotSignal signal)
    {
        bool wasSneakAttackEligible = CanReceivePlayerSneakAttack(signal.sourceTransform, false);

        PlayerState heardPlayerState = signal.sourceState;
        if (!heardPlayerState && signal.sourceTransform)
            heardPlayerState = signal.sourceTransform.GetComponentInParent<PlayerState>();

        if (heardPlayerState)
        {
            playerState = heardPlayerState;
            playerTarget = heardPlayerState.transform;
            playerAimTarget = null;
        }

        bool needsCombatStart = !killhouseActive || killhouseStartPending;
        if (needsCombatStart)
        {
            StopExternalPatrolForSoundSearch();
            StartKillhouseCombat(true);
        }

        CachePlayerGunshotSneakAttackGrace(signal, wasSneakAttackEligible);
        MarkPlayerNoticed();
        BeginLostTargetSearch(signal.position);
        nextGunshotHearTime = Time.time + gunshotHearCooldown;
    }

    private bool IsSoundOccluded(
        Vector3 listenerPosition,
        Vector3 sourcePosition,
        Transform sourceTransform,
        float occludedHearingMultiplier)
    {
        if (occludedHearingMultiplier >= 1f || coverProbeLayers.value == 0)
            return false;

        Vector3 toSource = sourcePosition - listenerPosition;
        float distance = toSource.magnitude;
        if (distance <= MinDirectionSqr)
            return false;

        Vector3 direction = toSource / distance;
        if (!Physics.Raycast(listenerPosition, direction, out RaycastHit hit, distance, coverProbeLayers, QueryTriggerInteraction.Ignore))
            return false;

        Transform hitTransform = hit.transform;
        if (!hitTransform || IsOwnTransform(hitTransform))
            return false;

        return !IsSourceTransform(hitTransform, sourceTransform);
    }

    private static bool IsSourceTransform(Transform hitTransform, Transform sourceTransform)
    {
        if (!hitTransform || !sourceTransform)
            return false;

        return hitTransform == sourceTransform ||
               hitTransform.IsChildOf(sourceTransform) ||
               sourceTransform.IsChildOf(hitTransform);
    }

    private void StopExternalPatrolForSoundSearch()
    {
        NPCTestDriver testDriver = GetComponent<NPCTestDriver>();
        if (!testDriver)
            testDriver = GetComponentInParent<NPCTestDriver>();

        if (!testDriver)
            return;

        testDriver.StopPatrol();
        testDriver.StopSweep(false);
        testDriver.StopKillhousePatrol(true);
    }

    public void PrepareKillhousePatrolLoadout()
    {
        ResolveReferences();
        TryEquipBestInventoryWeapon(true);
        ApplyKillhouseCombatState(false, false);

        if (state)
        {
            state.SetCombatMode(false);
            state.SetWeaponInHand(false);
        }

        if (movement)
        {
            movement.SetCrouching(false);
            movement.SetSprinting(false);
        }

        if (aim)
            aim.ClearAim();
    }

    public bool CanSeeKillhousePlayer()
    {
        ResolveReferences();
        if (!ResolvePlayerTarget())
        {
            ResetPlayerTargetAcquisition();
            return false;
        }

        bool hasVisualContact = CanSeePlayer(ResolvePlayerAimPosition());
        return UpdatePlayerTargetAcquisition(hasVisualContact);
    }

    public void ApplyKillhouseCombatTestSettings(KillhouseCombatTestSettings settings)
    {
        killhouseOnPlay = settings.killhouseOnPlay;
        showKillhouseNotes = settings.showKillhouseNotes;
        playerState = settings.playerState;
        playerTarget = settings.playerTarget;
        playerAimTarget = settings.playerAimTarget;
        playerKillhouseExit = settings.playerKillhouseExit;
        resetKillhouseOnResult = settings.resetKillhouseOnResult;
        autoFindPlayer = settings.autoFindPlayer;
        usePlayerAimTarget = settings.usePlayerAimTarget;
        huntPlayerWithoutSight = settings.huntPlayerWithoutSight;
        preferFirearms = settings.preferFirearms;
        useSpecificKillhouseInventoryWeapon = settings.useSpecificKillhouseInventoryWeapon;
        selectedKillhouseInventoryWeaponInstanceId = settings.selectedKillhouseInventoryWeaponInstanceId ?? string.Empty;
        crouchWhenShooting = settings.crouchWhenShooting;
        useCoverWhenDamaged = settings.useCoverWhenDamaged;
        jumpWhenStuck = settings.jumpWhenStuck;
        avoidSameFactionFriendlyFire = settings.avoidSameFactionFriendlyFire;
        friendlyFireAvoidanceLayers = settings.friendlyFireAvoidanceLayers;
        friendlyFireAvoidanceRadius = settings.friendlyFireAvoidanceRadius;
        lineOfSightLayers = settings.lineOfSightLayers;
        coverProbeLayers = settings.coverProbeLayers;
        sightRange = settings.sightRange;
        sightConeAngleDegrees = settings.sightConeAngleDegrees;
        targetAcquisitionSeconds = settings.targetAcquisitionSeconds;
        meleeEngageRange = settings.meleeEngageRange;
        closeCombatDrawRange = settings.closeCombatDrawRange;
        closeCombatUndrawDelay = settings.closeCombatUndrawDelay;
        preferredFirearmRange = settings.preferredFirearmRange;
        maxFirearmRange = settings.maxFirearmRange;
        closeRange = settings.closeRange;
        killhouseReachDistance = settings.killhouseReachDistance;
        killhouseDecisionInterval = settings.killhouseDecisionInterval;
        killhouseRepathInterval = settings.killhouseRepathInterval;
        killhouseActionInterval = settings.killhouseActionInterval;
        reloadResumeDelay = settings.reloadResumeDelay;
        coverSearchRadius = settings.coverSearchRadius;
        coverSearchSamples = settings.coverSearchSamples;
        coverMinPlayerDistance = settings.coverMinPlayerDistance;
        retreatHealthPercent = settings.retreatHealthPercent;
        damageReactionSeconds = settings.damageReactionSeconds;
        damageHealthDelta = settings.damageHealthDelta;
        coverDamageHealthPercent = settings.coverDamageHealthPercent;
        coverDamageReactionSeconds = settings.coverDamageReactionSeconds;
        stuckCheckInterval = settings.stuckCheckInterval;
        stuckDistance = settings.stuckDistance;
        stuckJumpInterval = settings.stuckJumpInterval;
        stuckChecksBeforeJump = settings.stuckChecksBeforeJump;
        searchAreaRadius = settings.searchAreaRadius;
        searchAreaSamples = settings.searchAreaSamples;
        broadSearchChance = settings.broadSearchChance;
        searchMinPointSpacing = settings.searchMinPointSpacing;
        searchPointReachDistance = settings.searchPointReachDistance;
        lostTargetEngageSeconds = settings.lostTargetEngageSeconds;
        lostTargetCombatEngageSeconds = settings.lostTargetCombatEngageSeconds;
        lostTargetSearchSeconds = settings.lostTargetSearchSeconds;
        coverDestinationHoldSeconds = settings.coverDestinationHoldSeconds;
        coverRetryDelay = settings.coverRetryDelay;
        aimLookAheadDistance = settings.aimLookAheadDistance;
        aimFollowSpeed = settings.aimFollowSpeed;
        firearmShootAimToleranceDegrees = settings.firearmShootAimToleranceDegrees;
        aimNotReadyRetryDelay = settings.aimNotReadyRetryDelay;
        searchAimSweepAngle = settings.searchAimSweepAngle;
        searchAimSweepSpeed = settings.searchAimSweepSpeed;
        killhouseResultBannerSeconds = settings.killhouseResultBannerSeconds;
        showKillhouseResultBanner = settings.showKillhouseResultBanner;
        alsoShowKillhouseResultOnHud = settings.alsoShowKillhouseResultOnHud;
        visibleTargetTurnSpeed = settings.visibleTargetTurnSpeed;
        visibleTargetSnapAngle = settings.visibleTargetSnapAngle;
        lastKnownFacingHoldSeconds = settings.lastKnownFacingHoldSeconds;
        weaponEquipSettleSeconds = settings.weaponEquipSettleSeconds;
        weaponEquipActionLockSeconds = settings.weaponEquipActionLockSeconds;
        weaponUnequipActionLockSeconds = settings.weaponUnequipActionLockSeconds;
        reloadActionLockSeconds = settings.reloadActionLockSeconds;
        busyPollSeconds = settings.busyPollSeconds;
        animatorLayer = settings.animatorLayer;
        busyAnimatorStateNames = settings.busyAnimatorStateNames;

        ClampKillhouseCombatTestSettings();
        CacheBusyAnimatorStateHashes();
    }

    private void ClampKillhouseCombatTestSettings()
    {
        closeCombatDrawRange = Mathf.Max(meleeEngageRange, closeCombatDrawRange);
        closeCombatUndrawDelay = Mathf.Max(0f, closeCombatUndrawDelay);
        sightRange = Mathf.Max(0.5f, sightRange);
        sightConeAngleDegrees = Mathf.Clamp(sightConeAngleDegrees, 1f, 179f);
        targetAcquisitionSeconds = Mathf.Max(0f, targetAcquisitionSeconds);
        lostTargetEngageSeconds = Mathf.Max(0.1f, lostTargetEngageSeconds);
        if (lostTargetCombatEngageSeconds <= 0f)
            lostTargetCombatEngageSeconds = DefaultLostTargetCombatEngageSeconds;
        lostTargetCombatEngageSeconds = Mathf.Max(lostTargetEngageSeconds, lostTargetCombatEngageSeconds);
        lostTargetSearchSeconds = Mathf.Max(0.5f, lostTargetSearchSeconds);
        visibleTargetTurnSpeed = Mathf.Max(0f, visibleTargetTurnSpeed);
        visibleTargetSnapAngle = Mathf.Max(0f, visibleTargetSnapAngle);
        lastKnownFacingHoldSeconds = Mathf.Max(0f, lastKnownFacingHoldSeconds);
        friendlyFireAvoidanceRadius = Mathf.Max(0f, friendlyFireAvoidanceRadius);
    }

    public bool CanReloadCurrentWeapon()
    {
        SyncEquippedWeaponAmmoWithController();
        return CanReload(ResolveCurrentWeaponDefinition());
    }

    private void Awake()
    {
        ResolveReferences();
        CacheBusyAnimatorStateHashes();
        startPosition = transform.position;
        lastObservedHealth = state ? state.GetHealthPoints() : 0f;
        EnsureGroups();
        requireFirearmAttackState = true;
        requireMeleeAttackState = true;
        SyncEquippedWeaponAmmoWithController();
    }

    private void OnEnable()
    {
        PlayerMovement.FootstepEmitted += HandlePlayerFootstepEmitted;
        PlayerCombat.GunshotEmitted += HandlePlayerGunshotEmitted;
    }

    private void OnDisable()
    {
        PlayerMovement.FootstepEmitted -= HandlePlayerFootstepEmitted;
        PlayerCombat.GunshotEmitted -= HandlePlayerGunshotEmitted;
        ClearPlayerGunshotSneakAttackGrace();
    }

    private void OnValidate()
    {
        EnsureGroups();
        requireFirearmAttackState = true;
        requireMeleeAttackState = true;
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
        fallbackAimDistance = Mathf.Max(MinRayDistance, fallbackAimDistance);
        shotgunFallbackDistance = Mathf.Max(MinRayDistance, shotgunFallbackDistance);
        firearmFacingToleranceDegrees = Mathf.Clamp(firearmFacingToleranceDegrees, 1f, 90f);
        firearmMuzzleToleranceDegrees = Mathf.Clamp(firearmMuzzleToleranceDegrees, 1f, 90f);
        friendlyFireAvoidanceRadius = Mathf.Max(0f, friendlyFireAvoidanceRadius);
        footstepHearingMultiplier = Mathf.Max(0f, footstepHearingMultiplier);
        occludedFootstepHearingMultiplier = Mathf.Clamp01(occludedFootstepHearingMultiplier);
        footstepHearCooldown = Mathf.Max(0f, footstepHearCooldown);
        gunshotHearingMultiplier = Mathf.Max(0f, gunshotHearingMultiplier);
        occludedGunshotHearingMultiplier = Mathf.Clamp01(occludedGunshotHearingMultiplier);
        gunshotHearCooldown = Mathf.Max(0f, gunshotHearCooldown);
        ClampKillhouseCombatTestSettings();
        CacheBusyAnimatorStateHashes();
    }

    private void Update()
    {
        ResolveReferences();
        SyncEquippedWeaponAmmoWithController();
        CompletePendingReloadFromAnimatorState();
        UpdateMeleeAnimationDamageFallback();
        ApplyMeleeWeaponColliderDamage();

        if (killhouseActive)
            UpdateKillhouseCombat();
        else if (killhouseStartPending)
            UpdateKillhouseStartupGate();
    }

    public void StartKillhouseCombat()
    {
        StartKillhouseCombat(false);
    }

    public void StartKillhouseCombat(bool returnToPatrolAfterLostTargetSearch)
    {
        killhouseStartPending = false;
        returnToKillhousePatrolAfterLostTargetSearch = returnToPatrolAfterLostTargetSearch;
        resumeKillhousePatrolAfterLostTargetSearch = false;
        BeginKillhouseMode();
    }

    public void StopKillhouseCombat(bool stopMovement)
    {
        killhouseStartPending = false;
        if (!killhouseResultResolving)
            restartKillhouseWhenPlayerReturns = false;

        killhouseActive = false;
        killhouseTactic = KillhouseTactic.Idle;
        hasLastKnownPlayerPosition = false;
        hasKillhouseDestination = false;
        hasCoverDestination = false;
        hasPreviousCoverDestination = false;
        hasSearchDestination = false;
        hasKillhouseAimPoint = false;
        hasConfirmedThreatPosition = false;
        coverHoldStarted = false;
        recentDamageUntil = -999f;
        recentCoverDamageUntil = -999f;
        coverDestinationHoldUntil = 0f;
        nextCoverSearchTime = 0f;
        killhouseReloadInProgress = false;
        killhouseActionLockActive = false;
        killhouseAwaitingWeaponDrawCompletion = false;
        lostTargetSearchActive = false;
        returnToKillhousePatrolAfterLostTargetSearch = false;
        inspectLastKnownBeforeSearch = false;
        hasPreviousSearchDestination = false;
        hasNoticedPlayer = false;
        ResetPlayerTargetAcquisition();
        lostSightStartedTime = -1f;
        lastDirectCombatContactTime = -999f;
        lastKnownFacingHoldUntil = 0f;
        closeCombatDrawHoldUntil = 0f;
        ClearPlayerGunshotSneakAttackGrace();

        if (aim)
            aim.ClearAim();

        if (state)
        {
            state.SetCombatMode(false);
            state.SetWeaponInHand(false);
        }

        if (movement)
        {
            movement.SetCrouching(false);
            movement.SetSprinting(false);
            if (stopMovement)
                movement.StopMovement(true);
        }
    }

    private void BeginKillhouseMode()
    {
        ResolveReferences();
        killhouseStartPending = false;
        CaptureKillhouseStartTransform();
        restartKillhouseWhenPlayerReturns = true;
        killhouseActive = true;
        killhouseTactic = KillhouseTactic.Idle;
        hasLastKnownPlayerPosition = false;
        hasKillhouseDestination = false;
        hasCoverDestination = false;
        hasPreviousCoverDestination = false;
        hasSearchDestination = false;
        hasKillhouseAimPoint = false;
        hasConfirmedThreatPosition = false;
        coverHoldStarted = false;
        recentDamageUntil = -999f;
        recentCoverDamageUntil = -999f;
        killhouseReloadInProgress = false;
        killhouseActionLockActive = false;
        killhouseAwaitingWeaponDrawCompletion = false;
        lostTargetSearchActive = false;
        inspectLastKnownBeforeSearch = false;
        hasPreviousSearchDestination = false;
        hasNoticedPlayer = false;
        ResetPlayerTargetAcquisition();
        lostSightStartedTime = -1f;
        lastDirectCombatContactTime = -999f;
        lastKnownFacingHoldUntil = 0f;
        nextKillhouseDecisionTime = 0f;
        nextKillhouseRepathTime = 0f;
        nextKillhouseActionTime = 0f;
        nextKillhouseWeaponSwitchTime = 0f;
        nextStuckCheckTime = 0f;
        nextStuckJumpTime = 0f;
        coverDestinationHoldUntil = 0f;
        nextCoverSearchTime = 0f;
        killhouseReloadResumeTime = 0f;
        killhouseActionLockUntil = 0f;
        ClearPlayerGunshotSneakAttackGrace();
        closeCombatDrawHoldUntil = 0f;
        consecutiveStuckChecks = 0;
        searchProbeIndex = 0;
        lastObservedHealth = state ? state.GetHealthPoints() : 0f;
        lastStuckCheckPosition = transform.position;

        ApplyKillhouseCombatState(false, false);

        TryEquipBestInventoryWeapon(true);
        SeedInitialKillhouseSearch();
    }

    public void QueueKillhouseOnPlayStart()
    {
        killhouseActive = false;
        killhouseStartPending = true;
        restartKillhouseWhenPlayerReturns = true;
        killhouseTactic = KillhouseTactic.Idle;
        returnToKillhousePatrolAfterLostTargetSearch = false;
        resumeKillhousePatrolAfterLostTargetSearch = false;
        ResetPlayerTargetAcquisition();
        lostSightStartedTime = -1f;
        lastDirectCombatContactTime = -999f;
    }

    private void UpdateKillhouseStartupGate()
    {
        ResolveReferences();

        if (!AreKillhouseParticipantsOnNavMesh(out _, out _))
            return;

        BeginKillhouseMode();
    }

    private void UpdateKillhouseCombat()
    {
        ResolveReferences();

        if (TryHandleKillhouseResult())
            return;

        TrackRecentDamage();

        if (!movement || !state)
        {
            StopKillhouseCombat(true);
            return;
        }

        if (!ResolvePlayerTarget())
        {
            ResetPlayerTargetAcquisition();

            if (lostTargetSearchActive && Time.time >= lostTargetSearchUntil)
            {
                FinishLostTargetSearch();
                return;
            }

            if (!lostTargetSearchActive)
                SeedInitialKillhouseSearch();

            if (hasLastKnownPlayerPosition)
            {
                killhouseTactic = KillhouseTactic.Search;
                UpdateKillhouseAim(false, transform.position + transform.forward * aimLookAheadDistance);
                UpdateKillhouseFacing(false, transform.position + transform.forward * aimLookAheadDistance);
                ApplyKillhouseCombatState(true, true);
                ExecuteKillhouseMovement(false);
                HandleKillhouseStuckMovement();
            }
            else
            {
                SetKillhouseIdle();
            }

            return;
        }

        Vector3 targetPosition = ResolvePlayerAimPosition();
        bool hasVisualContact = CanSeePlayer(targetPosition);
        bool canSeePlayer = UpdatePlayerTargetAcquisition(hasVisualContact);
        if (canSeePlayer)
        {
            lostSightStartedTime = -1f;
            lostTargetSearchActive = false;
            MarkPlayerNoticed();
            MarkDirectCombatContact();
            RememberPlayerPosition(playerTarget.position, false, true);
            hasSearchDestination = false;
        }
        else if (hasVisualContact)
        {
            lostSightStartedTime = -1f;
        }
        else if (hasNoticedPlayer)
        {
            UpdateLostSightSearchState();
            if (lostTargetSearchActive && Time.time >= lostTargetSearchUntil)
            {
                FinishLostTargetSearch();
                return;
            }
        }
        else if (huntPlayerWithoutSight && !hasLastKnownPlayerPosition && playerTarget)
        {
            RememberPlayerPosition(playerTarget.position);
        }

        UpdateKillhouseAim(canSeePlayer, targetPosition);
        UpdateKillhouseFacing(canSeePlayer, targetPosition);

        if (killhouseReloadInProgress || IsReloadPending)
        {
            ContinueKillhouseReload();
            if (killhouseReloadInProgress || IsReloadPending)
                return;
        }

        if (ContinueKillhouseActionLock())
            return;

        if (canSeePlayer && ShouldReloadCurrentWeapon())
        {
            BeginKillhouseReload();
            return;
        }

        if (Time.time >= nextKillhouseDecisionTime)
        {
            ChooseKillhouseTactic(canSeePlayer);
            nextKillhouseDecisionTime = Time.time + killhouseDecisionInterval;
        }

        if (PrepareKillhouseWeaponForCurrentTactic(canSeePlayer))
            return;

        ExecuteKillhouseMovement(canSeePlayer);
        UpdateKillhouseFacing(canSeePlayer, targetPosition);
        ExecuteKillhouseCombat(canSeePlayer);
        HandleKillhouseStuckMovement();
    }

    private bool TryHandleKillhouseResult()
    {
        if (killhouseResultResolving)
            return true;

        if (state && (state.IsDead() || state.GetHealthPoints() <= 0f))
        {
            CompleteKillhouseResult("Player Wins");
            return true;
        }

        if (playerState && playerState.GetHealthPoints() <= 0f)
        {
            CompleteKillhouseResult(ResolveKillhouseNPCName() + " Wins");
            return true;
        }

        return false;
    }

    private void CompleteKillhouseResult(string message)
    {
        killhouseResultResolving = true;
        ShowKillhouseResult(message);
        if (resetKillhouseOnResult)
            ResetKillhouseAfterResult();
        else
            FinishKillhouseResultWithoutReset();
        killhouseResultResolving = false;
    }

    private void ShowKillhouseResult(string message)
    {
        string result = string.IsNullOrWhiteSpace(message) ? "Killhouse Complete" : message.Trim();
        killhouseResultBannerText = result;
        killhouseResultBannerUntil = Time.time + killhouseResultBannerSeconds;

        if (alsoShowKillhouseResultOnHud)
            UI.HUDMessagePanelController.Show(result, killhouseResultBannerSeconds);
    }

    private void ResetKillhouseAfterResult()
    {
        StopKillhouseCombat(true);
        RestoreKillhouseParticipants();
        TeleportNPCToKillhouseStart();
        TeleportPlayerToKillhouseExit();
        lastObservedHealth = state ? state.GetHealthPoints() : 0f;
        RearmKillhouseAfterResult();
    }

    private void FinishKillhouseResultWithoutReset()
    {
        StopKillhouseCombat(true);
        restartKillhouseWhenPlayerReturns = false;
        lastObservedHealth = state ? state.GetHealthPoints() : 0f;
    }

    private void RearmKillhouseAfterResult()
    {
        if (!restartKillhouseWhenPlayerReturns)
            return;

        killhouseActive = false;
        killhouseStartPending = true;
        killhouseTactic = KillhouseTactic.Idle;
        nextKillhouseDecisionTime = 0f;
        nextKillhouseActionTime = 0f;
        nextKillhouseRepathTime = 0f;
    }

    private void RestoreKillhouseParticipants()
    {
        RestoreNPCForKillhouseReset();
        RestorePlayerForKillhouseReset();
    }

    private void RestoreNPCForKillhouseReset()
    {
        if (!state)
            return;

        state.SetHealthPoints(state.GetMaxHealthPoints());
        state.SetActionPoints(state.GetMaxActionPoints());
        state.SetLeftArmHealth(100f);
        state.SetRightArmHealth(100f);
        state.SetChestHealth(100f);
        state.SetHeadHealth(100f);
        state.SetLeftLegHealth(100f);
        state.SetRightLegHealth(100f);
        state.SetCombatMode(false);
        state.SetWeaponInHand(false);
    }

    private void RestorePlayerForKillhouseReset()
    {
        if (!playerState)
            return;

        playerState.SetHealthPoints(playerState.GetMaxHealthPoints());
        playerState.SetActionPoints(playerState.GetMaxActionPoints());
        playerState.SetLeftArmHealth(100f);
        playerState.SetRightArmHealth(100f);
        playerState.SetChestHealth(100f);
        playerState.SetHeadHealth(100f);
        playerState.SetLeftLegHealth(100f);
        playerState.SetRightLegHealth(100f);
        playerState.SetCombatMode(false);
        playerState.SetWeaponInHand(false);
    }

    private void TeleportNPCToKillhouseStart()
    {
        if (!hasKillhouseStartTransform)
            return;

        TeleportTransform(transform, killhouseStartPosition, killhouseStartRotation);
        lastStuckCheckPosition = killhouseStartPosition;
    }

    private void TeleportPlayerToKillhouseExit()
    {
        if (!playerKillhouseExit || !playerState)
            return;

        TeleportTransform(playerState.transform, playerKillhouseExit.position, playerKillhouseExit.rotation);
    }

    private void CaptureKillhouseStartTransform()
    {
        killhouseStartPosition = transform.position;
        killhouseStartRotation = transform.rotation;
        hasKillhouseStartTransform = true;
    }

    private string ResolveKillhouseNPCName()
    {
        if (state)
        {
            string npcName = state.GetNPCName();
            if (!string.IsNullOrWhiteSpace(npcName))
                return npcName;
        }

        return string.IsNullOrWhiteSpace(gameObject.name) ? "NPC" : gameObject.name;
    }

    private static void TeleportTransform(Transform target, Vector3 position, Quaternion rotation)
    {
        if (!target)
            return;

        Transform moveTransform = target;
        Rigidbody body = target.GetComponent<Rigidbody>();
        if (!body)
            body = target.GetComponentInParent<Rigidbody>();

        if (body)
        {
            moveTransform = body.transform;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = position;
            body.rotation = rotation;
        }

        NavMeshAgent agent = moveTransform.GetComponent<NavMeshAgent>();
        if (agent && agent.enabled && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.Warp(position);
        }

        moveTransform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();
    }

    private bool ResolvePlayerTarget()
    {
        if (!playerState && playerTarget)
            playerState = playerTarget.GetComponentInParent<PlayerState>();

        if (playerState && playerState.GetHealthPoints() <= 0f)
        {
            BeginLostTargetSearch(playerTarget ? playerTarget.position : playerState.transform.position);
            ClearPlayerTargetReferences();
        }

        if ((!playerState || !playerTarget) && autoFindPlayer)
            TryAcquireLivePlayerTarget();

        if (!playerTarget && playerState)
            playerTarget = playerState.transform;

        if (!playerAimTarget && playerTarget)
        {
            PlayerAim playerAim = playerTarget.GetComponentInChildren<PlayerAim>(true);
            if (!playerAim)
                playerAim = playerTarget.GetComponentInParent<PlayerAim>();

            playerAimTarget = playerAim && playerAim.AimTarget ? playerAim.AimTarget : playerTarget;
        }

        if (!playerTarget)
            return false;

        if (IsSameFactionNpcTransform(playerTarget))
        {
            ClearPlayerTargetReferences();
            return false;
        }

        if (playerState && playerState.GetHealthPoints() <= 0f)
            return false;

        return true;
    }

    private bool TryAcquireLivePlayerTarget()
    {
        PlayerState best = null;
        float bestSqrDistance = float.PositiveInfinity;
        PlayerState[] candidates = FindObjectsByType<PlayerState>();
        for (int i = 0; i < candidates.Length; i++)
        {
            PlayerState candidate = candidates[i];
            if (!candidate || candidate.GetHealthPoints() <= 0f)
                continue;

            float sqrDistance = (candidate.transform.position - transform.position).sqrMagnitude;
            if (sqrDistance >= bestSqrDistance)
                continue;

            best = candidate;
            bestSqrDistance = sqrDistance;
        }

        if (!best)
            return false;

        playerState = best;
        playerTarget = best.transform;
        playerAimTarget = null;
        lostTargetSearchActive = false;
        return true;
    }

    private void ClearPlayerTargetReferences()
    {
        playerState = null;
        playerTarget = null;
        playerAimTarget = null;
        hasKillhouseAimPoint = false;
    }

    private void UpdateLostSightSearchState()
    {
        if (lostTargetSearchActive)
            return;

        if (lostSightStartedTime < 0f)
            lostSightStartedTime = Time.time;

        float engageSeconds = ResolveLostTargetEngageSeconds();
        if (Time.time - lostSightStartedTime < engageSeconds)
            return;

        Vector3 searchOrigin = hasLastKnownPlayerPosition
            ? lastKnownPlayerPosition
            : playerTarget
                ? playerTarget.position
                : transform.position;
        BeginLostTargetSearch(searchOrigin);
    }

    private void BeginLostTargetSearch(Vector3 origin)
    {
        if (TrySampleNavMesh(origin, searchAreaRadius, out Vector3 sampledOrigin))
            origin = sampledOrigin;

        RememberPlayerPosition(origin, true);
        hasSearchDestination = false;
        hasKillhouseDestination = false;
        searchProbeIndex = 0;
        consecutiveStuckChecks = 0;
        lostTargetSearchActive = true;
        lostTargetSearchUntil = Time.time + Mathf.Max(0.5f, lostTargetSearchSeconds);
        killhouseTactic = KillhouseTactic.Search;
        nextKillhouseDecisionTime = Time.time + killhouseDecisionInterval;
    }

    private void FinishLostTargetSearch()
    {
        lostTargetSearchActive = false;
        hasLastKnownPlayerPosition = false;
        hasConfirmedThreatPosition = false;
        hasSearchDestination = false;
        hasKillhouseDestination = false;
        hasCoverDestination = false;
        coverHoldStarted = false;
        inspectLastKnownBeforeSearch = false;
        consecutiveStuckChecks = 0;
        closeCombatDrawHoldUntil = 0f;
        killhouseAwaitingWeaponDrawCompletion = false;

        if (returnToKillhousePatrolAfterLostTargetSearch)
        {
            resumeKillhousePatrolAfterLostTargetSearch = true;
            StopKillhouseCombat(true);
            return;
        }

        SetKillhouseIdle();
    }

    private void SeedInitialKillhouseSearch()
    {
        if (!huntPlayerWithoutSight || hasLastKnownPlayerPosition)
            return;

        ResolvePlayerTarget();

        Vector3 searchOrigin = playerTarget ? playerTarget.position : startPosition;
        if (TrySampleNavMesh(searchOrigin, searchAreaRadius, out Vector3 sampledSearchOrigin))
            searchOrigin = sampledSearchOrigin;

        RememberPlayerPosition(searchOrigin, false);
        killhouseTactic = KillhouseTactic.Search;
        nextKillhouseDecisionTime = 0f;
    }

    private Vector3 ResolvePlayerAimPosition()
    {
        Transform assignedAimTarget = ResolveAssignedAimTarget();
        if (assignedAimTarget && IsPlayerTransform(assignedAimTarget))
            return assignedAimTarget.position;

        if (usePlayerAimTarget && playerAimTarget)
            return playerAimTarget.position;

        if (playerTarget)
            return playerTarget.position + Vector3.up * 1.2f;

        return transform.position + transform.forward;
    }

    private void UpdateKillhouseAim(bool canSeePlayer, Vector3 visibleTargetPosition)
    {
        if (!aim)
            return;

        Vector3 desiredAimPoint = canSeePlayer
            ? visibleTargetPosition
            : hasNoticedPlayer
                ? ResolvePostNoticeSearchAimPoint()
                : ResolveUnnoticedLookPoint();

        if (!hasKillhouseAimPoint)
        {
            killhouseAimPoint = desiredAimPoint;
            hasKillhouseAimPoint = true;
        }
        else if (canSeePlayer)
        {
            killhouseAimPoint = desiredAimPoint;
        }
        else
        {
            float followSpeed = Mathf.Max(0.1f, aimFollowSpeed);
            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            killhouseAimPoint = Vector3.Lerp(killhouseAimPoint, desiredAimPoint, t);
        }

        aim.SetAimPoint(killhouseAimPoint);
    }

    private void UpdateKillhouseFacing(bool canSeePlayer, Vector3 visibleTargetPosition)
    {
        if (!hasNoticedPlayer)
            return;

        if (canSeePlayer)
        {
            FaceVisibleTarget(visibleTargetPosition);
            return;
        }

        if (TryResolveKillhouseFacingPoint(out Vector3 facingPoint))
            FaceVisibleTarget(facingPoint);
    }

    private void MarkPlayerNoticed()
    {
        hasNoticedPlayer = true;
        lastKnownFacingHoldUntil = Time.time + Mathf.Max(0f, lastKnownFacingHoldSeconds);
    }

    private void MarkDirectCombatContact()
    {
        lastDirectCombatContactTime = Time.time;
    }

    private float ResolveLostTargetEngageSeconds()
    {
        float baseEngageSeconds = Mathf.Max(0.1f, lostTargetEngageSeconds);
        float combatEngageSeconds = Mathf.Max(baseEngageSeconds, lostTargetCombatEngageSeconds);
        if (Time.time - lastDirectCombatContactTime <= combatEngageSeconds)
            return combatEngageSeconds;

        return baseEngageSeconds;
    }

    private bool TryResolveKillhouseFacingPoint(out Vector3 facingPoint)
    {
        if (Time.time <= lastKnownFacingHoldUntil)
        {
            if (hasLastKnownPlayerPosition)
            {
                facingPoint = lastKnownPlayerPosition;
                return true;
            }

            if (hasConfirmedThreatPosition)
            {
                facingPoint = confirmedThreatPosition;
                return true;
            }
        }

        if (killhouseTactic == KillhouseTactic.Search || lostTargetSearchActive)
        {
            facingPoint = ResolveSearchAimPoint();
            return true;
        }

        if (hasSearchDestination)
        {
            facingPoint = searchDestination;
            return true;
        }

        if (hasKillhouseDestination)
        {
            facingPoint = killhouseDestination;
            return true;
        }

        facingPoint = Vector3.zero;
        return false;
    }

    private void FaceVisibleTarget(Vector3 targetPosition)
    {
        Vector3 toTarget = targetPosition - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude <= MinDirectionSqr)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        Rigidbody body = GetComponent<Rigidbody>();
        if (!body)
            body = GetComponentInParent<Rigidbody>();

        if (body)
        {
            Quaternion nextRotation = GetSmoothedVisibleTargetRotation(body.rotation, targetRotation);
            body.angularVelocity = Vector3.zero;
            body.rotation = nextRotation;
            body.transform.rotation = nextRotation;
            return;
        }

        transform.rotation = GetSmoothedVisibleTargetRotation(transform.rotation, targetRotation);
    }

    private Quaternion GetSmoothedVisibleTargetRotation(Quaternion currentRotation, Quaternion targetRotation)
    {
        float snapAngle = Mathf.Max(0f, visibleTargetSnapAngle);
        if (Quaternion.Angle(currentRotation, targetRotation) <= snapAngle)
            return targetRotation;

        float turnSpeed = Mathf.Max(0f, visibleTargetTurnSpeed);
        if (turnSpeed <= 0f)
            return targetRotation;

        return Quaternion.RotateTowards(currentRotation, targetRotation, turnSpeed * Time.deltaTime);
    }

    private Transform ResolveAssignedAimTarget()
    {
        return aim ? aim.AssignedAimTarget : null;
    }

    private Vector3 ResolveSearchAimPoint()
    {
        Vector3 origin = ResolveEyePosition();
        Vector3 direction = ResolveSearchAimDirection(origin);
        return origin + direction * Mathf.Max(1f, aimLookAheadDistance);
    }

    private Vector3 ResolvePostNoticeSearchAimPoint()
    {
        if (Time.time <= lastKnownFacingHoldUntil && hasLastKnownPlayerPosition)
            return lastKnownPlayerPosition + Vector3.up * 1.2f;

        return ResolveSearchAimPoint();
    }

    private Vector3 ResolveUnnoticedLookPoint()
    {
        Vector3 origin = ResolveEyePosition();
        Vector3 direction = ResolveKillhouseLookDirection();
        return origin + direction * Mathf.Max(1f, aimLookAheadDistance);
    }

    private Vector3 ResolveSearchAimDirection(Vector3 origin)
    {
        if (TryResolveSearchFocusPoint(out Vector3 focusPoint))
        {
            Vector3 toFocus = focusPoint - origin;
            toFocus.y = 0f;
            if (toFocus.sqrMagnitude > MinDirectionSqr)
            {
                float sweepAngle = Mathf.Sin(Time.time * Mathf.Max(0.1f, searchAimSweepSpeed) * Mathf.PI * 2f) *
                                   Mathf.Max(0f, searchAimSweepAngle);
                Vector3 sweptDirection = Quaternion.Euler(0f, sweepAngle, 0f) * toFocus.normalized;
                if (sweptDirection.sqrMagnitude > MinDirectionSqr)
                    return sweptDirection.normalized;
            }
        }

        return ResolveKillhouseLookDirection();
    }

    private bool TryResolveSearchFocusPoint(out Vector3 focusPoint)
    {
        if (hasNoticedPlayer && hasLastKnownPlayerPosition)
        {
            focusPoint = lastKnownPlayerPosition + Vector3.up * 1.2f;
            return true;
        }

        if (hasSearchDestination)
        {
            focusPoint = searchDestination + Vector3.up * 1.2f;
            return true;
        }

        if (hasKillhouseDestination)
        {
            focusPoint = killhouseDestination + Vector3.up * 1.2f;
            return true;
        }

        focusPoint = Vector3.zero;
        return false;
    }

    private Vector3 ResolveKillhouseLookDirection()
    {
        if (movement && movement.CurrentWorldMoveDirection.sqrMagnitude > MinDirectionSqr)
            return movement.CurrentWorldMoveDirection.normalized;

        Vector3 toDestination = Vector3.zero;
        if (hasKillhouseDestination)
            toDestination = killhouseDestination - transform.position;
        else if (hasSearchDestination)
            toDestination = searchDestination - transform.position;
        else if (hasLastKnownPlayerPosition)
            toDestination = lastKnownPlayerPosition - transform.position;

        toDestination.y = 0f;
        if (toDestination.sqrMagnitude > MinDirectionSqr)
            return toDestination.normalized;

        Vector3 forward = transform.forward;
        forward.y = 0f;
        return forward.sqrMagnitude > MinDirectionSqr ? forward.normalized : Vector3.forward;
    }

    private void TrackRecentDamage()
    {
        if (!state)
            return;

        float health = state.GetHealthPoints();
        if (lastObservedHealth <= 0f)
        {
            lastObservedHealth = health;
            return;
        }

        float damageTaken = lastObservedHealth - health;
        if (damageTaken >= damageHealthDelta)
        {
            recentDamageUntil = Time.time + damageReactionSeconds;
            MarkDirectCombatContact();

            float maxHealth = state ? Mathf.Max(1f, state.GetMaxHealthPoints()) : 1f;
            if (damageTaken / maxHealth >= coverDamageHealthPercent)
                recentCoverDamageUntil = Time.time + coverDamageReactionSeconds;

            if (!hasLastKnownPlayerPosition)
                RememberPlayerPosition(transform.position);
        }

        lastObservedHealth = health;
    }

    private bool CanSeePlayer(Vector3 targetPosition)
    {
        if (CanSeePlayerPoint(targetPosition))
            return true;

        if (playerTarget)
        {
            Vector3 basePosition = playerTarget.position;
            if (CanSeePlayerPoint(basePosition + Vector3.up * 1.45f))
                return true;

            if (CanSeePlayerPoint(basePosition + Vector3.up * 0.8f))
                return true;
        }

        return false;
    }

    private bool CanSeePlayerPoint(Vector3 targetPosition)
    {
        Vector3 origin = ResolveEyePosition();
        Vector3 toTarget = targetPosition - origin;
        float distance = toTarget.magnitude;
        if (distance <= MinDirectionSqr || distance > sightRange)
            return false;

        Vector3 direction = toTarget / distance;
        if (!IsDirectionInsideSightCone(direction))
            return false;

        Transform blockingHit = GetNearestLineOfSightHit(origin, direction, distance);
        if (!blockingHit)
            return true;

        return IsPlayerTransform(blockingHit);
    }

    private bool UpdatePlayerTargetAcquisition(bool hasVisualContact)
    {
        if (!hasVisualContact)
        {
            ResetPlayerTargetAcquisition();
            return false;
        }

        if (targetAcquisitionSeconds <= 0f)
        {
            hasAcquiredVisiblePlayer = true;
            visiblePlayerAcquisitionStartedTime = Time.time;
            return true;
        }

        if (hasAcquiredVisiblePlayer)
            return true;

        if (visiblePlayerAcquisitionStartedTime < 0f)
            visiblePlayerAcquisitionStartedTime = Time.time;

        if (Time.time - visiblePlayerAcquisitionStartedTime < targetAcquisitionSeconds)
            return false;

        hasAcquiredVisiblePlayer = true;
        return true;
    }

    private void ResetPlayerTargetAcquisition()
    {
        hasAcquiredVisiblePlayer = false;
        visiblePlayerAcquisitionStartedTime = -1f;
    }

    private bool IsDirectionInsideSightCone(Vector3 directionToTarget)
    {
        Vector3 sightForward = ResolveSightForwardDirection();
        float halfConeAngle = sightConeAngleDegrees * 0.5f;
        return Vector3.Angle(sightForward, directionToTarget) <= halfConeAngle;
    }

    private Vector3 ResolveSightForwardDirection()
    {
        Vector3 forward = transform.forward;
        if (forward.sqrMagnitude > MinDirectionSqr)
            return forward.normalized;

        if (movement && movement.CurrentWorldMoveDirection.sqrMagnitude > MinDirectionSqr)
            return movement.CurrentWorldMoveDirection.normalized;

        return Vector3.forward;
    }

    private void RememberPlayerPosition(Vector3 position, bool inspectFirst = false, bool confirmedThreat = false)
    {
        lastKnownPlayerPosition = position;
        hasLastKnownPlayerPosition = true;
        if (confirmedThreat)
        {
            confirmedThreatPosition = position;
            hasConfirmedThreatPosition = true;
        }

        if (inspectFirst)
            inspectLastKnownBeforeSearch = true;
    }

    private Transform GetNearestLineOfSightHit(Vector3 origin, Vector3 direction, float distance)
    {
        int hitCount = Physics.RaycastNonAlloc(
            origin,
            direction,
            LineOfSightHits,
            distance,
            lineOfSightLayers,
            QueryTriggerInteraction.Ignore);

        Transform nearest = null;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = LineOfSightHits[i];
            Transform hitTransform = hit.transform;
            if (!hitTransform || ShouldIgnoreLineOfSightHit(hitTransform))
                continue;

            if (hit.distance >= nearestDistance)
                continue;

            nearest = hitTransform;
            nearestDistance = hit.distance;
        }

        return nearest;
    }

    private void ChooseKillhouseTactic(bool canSeePlayer)
    {
        float distanceToPlayer = GetFlatDistanceToPlayer();
        bool lowHealth = IsLowHealth();
        bool recentlyDamaged = Time.time < recentDamageUntil;
        bool recentlyTookCoverDamage = Time.time < recentCoverDamageUntil;
        bool needsReload = ShouldReloadCurrentWeapon();
        bool hasCoverThreat = TryResolveCoverThreatPosition(canSeePlayer, out Vector3 coverThreatPosition);

        if (canSeePlayer && needsReload)
        {
            killhouseTactic = KillhouseTactic.Reload;
            return;
        }

        if ((lowHealth || recentlyTookCoverDamage) && useCoverWhenDamaged && hasCoverThreat)
        {
            if (ShouldKeepCurrentCoverDestination())
            {
                killhouseTactic = KillhouseTactic.TakeCover;
                return;
            }

            if (Time.time >= nextCoverSearchTime && TryFindCoverPosition(coverThreatPosition, out Vector3 cover))
            {
                SetCoverDestination(cover);
                nextCoverSearchTime = Time.time + coverRetryDelay;
                killhouseTactic = KillhouseTactic.TakeCover;
                return;
            }
        }

        StorePreviousCoverDestination();
        hasCoverDestination = false;
        coverHoldStarted = false;

        if (!canSeePlayer)
        {
            if (lostTargetSearchActive)
                killhouseTactic = KillhouseTactic.Search;
            else if (hasNoticedPlayer && HasRecentPlayerMemory())
                killhouseTactic = KillhouseTactic.Chase;
            else
            {
                killhouseTactic = (huntPlayerWithoutSight || recentlyDamaged) && HasRecentPlayerMemory()
                    ? KillhouseTactic.Search
                    : KillhouseTactic.Idle;
            }

            return;
        }

        if (IsCurrentWeaponFirearm())
        {
            if (distanceToPlayer <= closeRange)
            {
                killhouseTactic = KillhouseTactic.Flank;
                return;
            }

            if (distanceToPlayer <= maxFirearmRange)
            {
                killhouseTactic = KillhouseTactic.Attack;
                return;
            }
        }

        killhouseTactic = distanceToPlayer <= meleeEngageRange ? KillhouseTactic.Attack : KillhouseTactic.Chase;
    }

    private bool ShouldKeepCurrentCoverDestination()
    {
        if (!hasCoverDestination)
            return false;

        if (ReachedFlatPosition(coverDestination, killhouseReachDistance))
        {
            if (!coverHoldStarted)
            {
                coverHoldStarted = true;
                coverDestinationHoldUntil = Time.time + coverDestinationHoldSeconds;
            }

            return Time.time < coverDestinationHoldUntil;
        }

        return IsReachableNavMeshDestination(coverDestination);
    }

    private bool TryResolveCoverThreatPosition(bool canSeePlayer, out Vector3 threatPosition)
    {
        if (canSeePlayer && playerTarget)
        {
            threatPosition = ResolvePlayerAimPosition();
            return true;
        }

        if (hasConfirmedThreatPosition)
        {
            threatPosition = confirmedThreatPosition + Vector3.up * 1.2f;
            return true;
        }

        threatPosition = Vector3.zero;
        return false;
    }

    private void SetCoverDestination(Vector3 destination)
    {
        StorePreviousCoverDestination();
        coverDestination = destination;
        hasCoverDestination = true;
        coverHoldStarted = false;
        coverDestinationHoldUntil = 0f;
    }

    private void StorePreviousCoverDestination()
    {
        if (!hasCoverDestination)
            return;

        previousCoverDestination = coverDestination;
        hasPreviousCoverDestination = true;
    }

    private void ExecuteKillhouseMovement(bool canSeePlayer)
    {
        if (!movement)
            return;

        bool shouldRun = killhouseTactic == KillhouseTactic.Chase ||
                         killhouseTactic == KillhouseTactic.Search ||
                         killhouseTactic == KillhouseTactic.TakeCover;

        movement.SetSprinting(false);

        switch (killhouseTactic)
        {
            case KillhouseTactic.Idle:
                SetKillhouseIdle();
                break;

            case KillhouseTactic.Chase:
                MoveToKillhouseDestination(playerTarget ? playerTarget.position : lastKnownPlayerPosition, shouldRun);
                break;

            case KillhouseTactic.Search:
                MoveToSearchDestination(shouldRun);
                break;

            case KillhouseTactic.Reload:
                StopKillhouseMovement();
                break;

            case KillhouseTactic.TakeCover:
                if (hasCoverDestination && !ReachedFlatPosition(coverDestination, killhouseReachDistance))
                    MoveToKillhouseDestination(coverDestination, shouldRun);
                else
                    StopKillhouseMovement();
                break;

            case KillhouseTactic.Flank:
                if (!TryMoveToFlankPoint(shouldRun))
                    MoveAwayFromPlayer(shouldRun);
                break;

            case KillhouseTactic.Attack:
                HoldCombatRange(canSeePlayer);
                break;
        }
    }

    private void ExecuteKillhouseCombat(bool canSeePlayer)
    {
        if (!state)
            return;

        if (!canSeePlayer && !IsReloadPending && !killhouseReloadInProgress)
        {
            if (movement)
                movement.SetCrouching(false);

            return;
        }

        bool shouldCrouch = crouchWhenShooting &&
                            canSeePlayer &&
                            IsCurrentWeaponFirearm() &&
                            killhouseTactic == KillhouseTactic.Attack &&
                            movement &&
                            !movement.HasMovementInput &&
                            !IsReloadPending;
        movement.SetCrouching(shouldCrouch);

        if (Time.time < nextKillhouseActionTime || IsAnimatorBusy())
            return;

        if (!canSeePlayer)
            return;

        if (killhouseTactic != KillhouseTactic.Attack)
            return;

        if (!EnsureKillhouseAttackReady())
            return;

        if (!IsAimReadyToFireAtVisibleTarget(ResolvePlayerAimPosition()))
        {
            nextKillhouseActionTime = Time.time + aimNotReadyRetryDelay;
            return;
        }

        float distanceToPlayer = GetFlatDistanceToPlayer();
        bool inWeaponRange = IsCurrentWeaponFirearm()
            ? distanceToPlayer <= maxFirearmRange
            : distanceToPlayer <= meleeEngageRange;

        if (inWeaponRange && TryAttackSafe())
        {
            MarkDirectCombatContact();
            nextKillhouseActionTime = Time.time + killhouseActionInterval;
            return;
        }

        if (IsCurrentWeaponFirearm() && ShouldReloadCurrentWeapon())
            nextKillhouseActionTime = Time.time + killhouseActionInterval;
    }

    private bool EnsureKillhouseAttackReady()
    {
        if (!state)
            return false;

        bool needsWeaponInHand = ShouldCurrentWeaponUseHand();
        if (state.GetCombatMode() && (!needsWeaponInHand || state.GetWeaponInHand()))
            return true;

        ApplyKillhouseCombatState(true, true);
        BeginKillhouseActionLock(needsWeaponInHand ? weaponEquipActionLockSeconds : busyPollSeconds, needsWeaponInHand);
        return false;
    }

    private bool IsAimReadyToFireAtVisibleTarget(Vector3 visibleTargetPosition)
    {
        if (!IsCurrentWeaponFirearm())
            return true;

        if (!aim || !aim.HasAimSolution)
            return false;

        Vector3 origin = aim.AimOrigin ? aim.AimOrigin.position : ResolveEyePosition();
        Vector3 desiredDirection = visibleTargetPosition - origin;
        if (desiredDirection.sqrMagnitude <= MinDirectionSqr)
            return true;

        Vector3 currentDirection = aim.FullAimDirection;
        if (currentDirection.sqrMagnitude <= MinDirectionSqr)
            return false;

        float aimAngle = Vector3.Angle(currentDirection.normalized, desiredDirection.normalized);
        return aimAngle <= firearmShootAimToleranceDegrees;
    }

    private bool ShouldDrawWeaponForKillhouse(bool canSeePlayer)
    {
        if (killhouseReloadInProgress || IsReloadPending)
            return true;

        if (IsCurrentWeaponCloseCombat())
            return ShouldDrawCloseCombatWeaponForKillhouse();

        return ShouldUseCombatLocomotionForKillhouseTactic();
    }

    private bool ShouldDrawCloseCombatWeaponForKillhouse()
    {
        if (!hasNoticedPlayer || !ShouldUseCombatLocomotionForKillhouseTactic())
            return false;

        if (lostTargetSearchActive)
            return true;

        float drawRange = Mathf.Max(meleeEngageRange, closeCombatDrawRange);
        if (GetFlatDistanceToPlayer() <= drawRange)
            closeCombatDrawHoldUntil = Time.time + closeCombatUndrawDelay;

        return Time.time <= closeCombatDrawHoldUntil;
    }

    private bool ShouldUseCombatLocomotionForKillhouseTactic()
    {
        return killhouseTactic == KillhouseTactic.Search ||
               killhouseTactic == KillhouseTactic.Chase ||
               killhouseTactic == KillhouseTactic.Attack ||
               killhouseTactic == KillhouseTactic.Flank ||
               killhouseTactic == KillhouseTactic.TakeCover;
    }

    private bool IsActivelyEngagingKillhousePlayer(bool canSeePlayer)
    {
        if (!killhouseActive || !hasNoticedPlayer)
            return false;

        if (killhouseTactic == KillhouseTactic.Attack ||
            killhouseTactic == KillhouseTactic.Chase ||
            killhouseTactic == KillhouseTactic.Flank ||
            killhouseTactic == KillhouseTactic.TakeCover ||
            killhouseTactic == KillhouseTactic.Reload)
        {
            return true;
        }

        return canSeePlayer && state && state.GetCombatMode();
    }

    private bool PrepareKillhouseWeaponForCurrentTactic(bool canSeePlayer)
    {
        if (!state)
            return false;

        bool shouldDrawWeapon = ShouldDrawWeaponForKillhouse(canSeePlayer);
        bool changedEquippedWeapon = ShouldUseCombatLocomotionForKillhouseTactic() && EquipCombatWeapon();
        if (changedEquippedWeapon)
            shouldDrawWeapon = ShouldDrawWeaponForKillhouse(canSeePlayer);

        if (shouldDrawWeapon && changedEquippedWeapon)
        {
            ApplyKillhouseCombatState(true, true);
            BeginKillhouseActionLock(weaponEquipActionLockSeconds, ShouldCurrentWeaponUseHand());
            return true;
        }

        bool stateChanged = ApplyKillhouseCombatState(shouldDrawWeapon, shouldDrawWeapon);
        if (!stateChanged)
            return false;

        BeginKillhouseActionLock(
            shouldDrawWeapon ? weaponEquipActionLockSeconds : weaponUnequipActionLockSeconds,
            shouldDrawWeapon && ShouldCurrentWeaponUseHand());
        return true;
    }

    private void BeginKillhouseActionLock(float duration)
    {
        BeginKillhouseActionLock(duration, false);
    }

    private void BeginKillhouseActionLock(float duration, bool awaitingWeaponDrawCompletion)
    {
        killhouseActionLockActive = true;
        killhouseAwaitingWeaponDrawCompletion |= awaitingWeaponDrawCompletion;
        killhouseActionLockUntil = Mathf.Max(killhouseActionLockUntil, Time.time + Mathf.Max(0f, duration));
        StopKillhouseMovement();

        if (movement)
        {
            movement.SetCrouching(false);
            movement.SetSprinting(false);
        }

        nextKillhouseActionTime = Mathf.Max(nextKillhouseActionTime, Time.time + busyPollSeconds);
        nextKillhouseDecisionTime = Mathf.Max(nextKillhouseDecisionTime, Time.time + busyPollSeconds);
    }

    private bool ContinueKillhouseActionLock()
    {
        if (!killhouseActionLockActive)
            return false;

        StopKillhouseMovement();

        if (movement)
        {
            movement.SetCrouching(false);
            movement.SetSprinting(false);
        }

        if (Time.time < killhouseActionLockUntil || IsAnimatorBusy())
        {
            nextKillhouseActionTime = Time.time + busyPollSeconds;
            nextKillhouseDecisionTime = Time.time + busyPollSeconds;
            return true;
        }

        CompletePendingKillhouseWeaponDraw();
        killhouseActionLockActive = false;
        nextKillhouseDecisionTime = 0f;
        return false;
    }

    private void CompletePendingKillhouseWeaponDraw()
    {
        if (!killhouseAwaitingWeaponDrawCompletion)
            return;

        killhouseAwaitingWeaponDrawCompletion = false;

        if (!state || !weaponController || !state.GetCombatMode() || !ShouldCurrentWeaponUseHand())
            return;

        if (state.GetWeaponInHand() || weaponController.IsEquipAnimationPlaying())
            return;

        weaponController.SetWeaponInHandImmediate(true);
    }

    private void BeginKillhouseReload()
    {
        killhouseTactic = KillhouseTactic.Reload;
        StopKillhouseMovement();

        if (movement)
        {
            movement.SetCrouching(false);
            movement.SetSprinting(false);
        }

        ApplyKillhouseCombatState(true, true);
        UpdateKillhouseAim(false, ResolveSearchAimPoint());

        nextKillhouseDecisionTime = Time.time + killhouseDecisionInterval;

        if (Time.time < nextKillhouseActionTime)
            return;

        if (IsAnimatorBusy())
        {
            nextKillhouseActionTime = Time.time + busyPollSeconds;
            return;
        }

        if (TryReloadSafe())
        {
            killhouseReloadInProgress = true;
            killhouseReloadResumeTime = Time.time + reloadResumeDelay;
            BeginKillhouseActionLock(reloadActionLockSeconds);
        }

        nextKillhouseActionTime = Time.time + killhouseActionInterval;
    }

    private void ContinueKillhouseReload()
    {
        killhouseTactic = KillhouseTactic.Reload;
        StopKillhouseMovement();

        if (movement)
        {
            movement.SetCrouching(false);
            movement.SetSprinting(false);
        }

        if (state)
            ApplyKillhouseCombatState(true, true);

        nextKillhouseActionTime = Time.time + killhouseActionInterval;
        nextKillhouseDecisionTime = Time.time + killhouseDecisionInterval;

        if (IsReloadPending)
        {
            killhouseReloadInProgress = true;
            killhouseReloadResumeTime = Time.time + reloadResumeDelay;
            return;
        }

        if (killhouseReloadInProgress && Time.time < killhouseReloadResumeTime)
            return;

        killhouseReloadInProgress = false;
        nextKillhouseDecisionTime = 0f;
    }

    private bool EquipCombatWeapon()
    {
        if (!weaponController || Time.time < nextKillhouseWeaponSwitchTime || IsAnimatorBusy())
            return false;

        bool currentFirearmEmpty = IsCurrentWeaponFirearm() && CurrentFirearmOutOfAmmo();
        bool currentUnarmed = weaponController.GetCurrentCategory() == NPCWeaponController.WeaponCategory.Unarmed;
        bool currentCloseCombatWeapon = IsCurrentWeaponCloseCombat() && !currentUnarmed;
        bool forceSelection = currentUnarmed || currentFirearmEmpty || (!IsCurrentWeaponFirearm() && !currentCloseCombatWeapon);
        if (TryEquipBestInventoryWeapon(forceSelection))
        {
            nextKillhouseWeaponSwitchTime = Time.time + weaponEquipSettleSeconds;
            return true;
        }

        if (IsSelectedKillhouseUnarmedWeapon())
            return false;

        if (preferFirearms)
        {
            if (currentUnarmed || currentFirearmEmpty)
            {
                if (TryEquipNextSafe())
                {
                    nextKillhouseWeaponSwitchTime = Time.time + weaponEquipSettleSeconds;
                    return true;
                }
            }

            return false;
        }

        if (weaponController.GetCurrentCategory() == NPCWeaponController.WeaponCategory.Unarmed &&
            weaponController.GetWeaponCount() > 1)
        {
            if (TryEquipNextSafe())
            {
                nextKillhouseWeaponSwitchTime = Time.time + weaponEquipSettleSeconds;
                return true;
            }
        }

        return false;
    }

    private void HoldCombatRange(bool canSeePlayer)
    {
        if (!movement || !playerTarget)
            return;

        float distance = GetFlatDistanceToPlayer();
        if (!IsCurrentWeaponFirearm())
        {
            MoveToKillhouseDestination(playerTarget.position, true);
            return;
        }

        if (distance > preferredFirearmRange * 1.25f)
        {
            MoveToKillhouseDestination(playerTarget.position, true);
            return;
        }

        if (distance < preferredFirearmRange * 0.65f)
        {
            MoveAwayFromPlayer(true);
            return;
        }

        if (!canSeePlayer)
        {
            MoveToKillhouseDestination(lastKnownPlayerPosition, true);
            return;
        }

        StopKillhouseMovement();
    }

    private bool TryMoveToFlankPoint(bool run)
    {
        if (!playerTarget)
            return false;

        Vector3 toPlayer = GetFlatDirectionToPlayer();
        Vector3 side = Vector3.Cross(Vector3.up, toPlayer).normalized;
        if (Mathf.Sin(Time.time * 0.7f) < 0f)
            side = -side;

        Vector3 rawPoint = playerTarget.position + side * preferredFirearmRange - toPlayer * (preferredFirearmRange * 0.35f);
        if (!TrySampleNavMesh(rawPoint, 2.5f, out Vector3 sampledPoint))
            return false;

        MoveToKillhouseDestination(sampledPoint, run);
        return true;
    }

    private void MoveAwayFromPlayer(bool run)
    {
        if (!movement || !playerTarget)
            return;

        Vector3 away = transform.position - playerTarget.position;
        away.y = 0f;
        if (away.sqrMagnitude <= MinDirectionSqr)
            away = -transform.forward;

        Vector3 rawDestination = transform.position + away.normalized * Mathf.Max(2f, preferredFirearmRange * 0.5f);
        if (TrySampleNavMesh(rawDestination, 2.5f, out Vector3 sampledDestination))
            MoveToKillhouseDestination(sampledDestination, run);
        else
        {
            movement.SetMoveDirection(away, run);
            hasKillhouseDestination = false;
        }
    }

    private void MoveToSearchDestination(bool run)
    {
        if (!movement || !hasLastKnownPlayerPosition)
            return;

        if (inspectLastKnownBeforeSearch &&
            !ReachedFlatPosition(lastKnownPlayerPosition, searchPointReachDistance * 1.5f))
        {
            MoveToKillhouseDestination(lastKnownPlayerPosition, run);
            return;
        }

        inspectLastKnownBeforeSearch = false;

        if (!hasSearchDestination || ReachedFlatPosition(searchDestination, searchPointReachDistance))
        {
            hasSearchDestination = TryPickSearchDestination(out searchDestination);
            if (!hasSearchDestination)
            {
                StopKillhouseMovement();
                return;
            }
        }

        MoveToKillhouseDestination(searchDestination, run);
    }

    private bool TryPickSearchDestination(out Vector3 destination)
    {
        destination = lastKnownPlayerPosition;
        int sampleCount = Mathf.Max(8, searchAreaSamples);
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < sampleCount * 2; i++)
        {
            bool useBroadSearch = UnityEngine.Random.value < broadSearchChance;
            Vector3 rawPoint;
            bool pickedCandidate = useBroadSearch
                ? TryPickBroadSearchCandidate(out rawPoint)
                : TryPickLocalSearchCandidate(out rawPoint);

            if (!pickedCandidate)
                continue;

            if (!TryScoreSearchCandidate(rawPoint, out Vector3 candidate, out float score))
                continue;

            if (score <= bestScore)
                continue;

            destination = candidate;
            bestScore = score;
        }

        if (bestScore > float.NegativeInfinity)
        {
            previousSearchDestination = destination;
            hasPreviousSearchDestination = true;
            searchProbeIndex++;
            return true;
        }

        if (ReachedFlatPosition(lastKnownPlayerPosition, searchPointReachDistance) ||
            !IsReachableNavMeshDestination(lastKnownPlayerPosition, out destination))
        {
            return false;
        }

        previousSearchDestination = destination;
        hasPreviousSearchDestination = true;
        return true;
    }

    private bool TryPickLocalSearchCandidate(out Vector3 candidate)
    {
        float radius = Mathf.Max(1.5f, searchAreaRadius);
        float minRadius = Mathf.Min(radius, Mathf.Max(searchPointReachDistance * 1.5f, searchMinPointSpacing));
        Vector3 center = hasLastKnownPlayerPosition && UnityEngine.Random.value < 0.65f
            ? lastKnownPlayerPosition
            : transform.position;

        float angle = UnityEngine.Random.Range(0f, 360f);
        float probeRadius = UnityEngine.Random.Range(minRadius, radius);
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * probeRadius;
        candidate = center + offset;
        return true;
    }

    private bool TryPickBroadSearchCandidate(out Vector3 candidate)
    {
        candidate = Vector3.zero;
        NavMeshTriangulation triangulation = NavMesh.CalculateTriangulation();
        if (triangulation.indices == null ||
            triangulation.vertices == null ||
            triangulation.indices.Length < 3 ||
            triangulation.vertices.Length == 0)
        {
            return false;
        }

        int triangleCount = triangulation.indices.Length / 3;
        int triangleStart = UnityEngine.Random.Range(0, triangleCount) * 3;
        Vector3 a = triangulation.vertices[triangulation.indices[triangleStart]];
        Vector3 b = triangulation.vertices[triangulation.indices[triangleStart + 1]];
        Vector3 c = triangulation.vertices[triangulation.indices[triangleStart + 2]];

        float r1 = Mathf.Sqrt(UnityEngine.Random.value);
        float r2 = UnityEngine.Random.value;
        candidate = (1f - r1) * a + (r1 * (1f - r2)) * b + (r1 * r2) * c;
        return true;
    }

    private bool TryScoreSearchCandidate(Vector3 rawPoint, out Vector3 candidate, out float score)
    {
        candidate = rawPoint;
        score = float.NegativeInfinity;

        if (!IsReachableNavMeshDestination(rawPoint, out candidate))
            return false;

        float currentDistance = FlatDistance(transform.position, candidate);
        if (currentDistance < Mathf.Max(searchPointReachDistance, searchMinPointSpacing))
            return false;

        if (hasPreviousSearchDestination &&
            FlatDistance(previousSearchDestination, candidate) < searchMinPointSpacing)
        {
            return false;
        }

        float lastKnownDistance = hasLastKnownPlayerPosition
            ? FlatDistance(lastKnownPlayerPosition, candidate)
            : 0f;
        float previousDistance = hasPreviousSearchDestination
            ? FlatDistance(previousSearchDestination, candidate)
            : searchMinPointSpacing;

        score = UnityEngine.Random.value * 2f;
        score += Mathf.Clamp(currentDistance / Mathf.Max(1f, searchAreaRadius), 0f, 2.5f);
        score += Mathf.Clamp(previousDistance / Mathf.Max(1f, searchAreaRadius), 0f, 1.5f);
        score += Mathf.Clamp(lastKnownDistance / Mathf.Max(1f, searchAreaRadius), 0f, 1.25f) * broadSearchChance;

        if (hasLastKnownPlayerPosition &&
            HasLineOfSightFrom(candidate + Vector3.up * 1.2f, lastKnownPlayerPosition + Vector3.up * 1.2f))
        {
            score += 0.75f;
        }

        return true;
    }

    private void MoveToKillhouseDestination(Vector3 destination, bool run)
    {
        if (!movement)
            return;

        Vector3 flatToDestination = destination - transform.position;
        flatToDestination.y = 0f;
        if (flatToDestination.sqrMagnitude <= killhouseReachDistance * killhouseReachDistance)
        {
            hasKillhouseDestination = false;
            movement.StopMovement(true);
            return;
        }

        if (hasKillhouseDestination &&
            Time.time < nextKillhouseRepathTime &&
            (killhouseDestination - destination).sqrMagnitude < 0.5f)
        {
            return;
        }

        if (TrySampleNavMesh(destination, 2f, out Vector3 sampledDestination))
            destination = sampledDestination;

        killhouseDestination = destination;
        hasKillhouseDestination = true;
        nextKillhouseRepathTime = Time.time + killhouseRepathInterval;
        movement.SetDestination(destination, run);
    }

    private void SetKillhouseIdle()
    {
        hasKillhouseDestination = false;
        hasSearchDestination = false;
        inspectLastKnownBeforeSearch = false;
        killhouseAwaitingWeaponDrawCompletion = false;

        if (movement)
        {
            movement.StopMovement(true);
            movement.SetCrouching(false);
            movement.SetSprinting(false);
        }

        if (state)
            ApplyKillhouseCombatState(false, false);
    }

    private void HandleKillhouseStuckMovement()
    {
        if (!movement || !jumpWhenStuck || Time.time < nextStuckCheckTime)
            return;

        bool checksDestinationProgress =
            hasKillhouseDestination &&
            movement.HasDestination &&
            movement.CurrentWorldMoveDirection.sqrMagnitude > MinDirectionSqr &&
            killhouseTactic != KillhouseTactic.Attack &&
            killhouseTactic != KillhouseTactic.Reload &&
            !IsReloadPending &&
            !IsAnimatorBusy();
        float movedSqr = (transform.position - lastStuckCheckPosition).sqrMagnitude;

        if (checksDestinationProgress && movedSqr < stuckDistance * stuckDistance)
            consecutiveStuckChecks++;
        else
            consecutiveStuckChecks = 0;

        if (checksDestinationProgress &&
            consecutiveStuckChecks >= Mathf.Max(1, stuckChecksBeforeJump) &&
            movement.IsGroundedNow &&
            Time.time >= nextStuckJumpTime)
        {
            if (killhouseTactic == KillhouseTactic.Search)
            {
                RecoverKillhouseSearchDestination();
                lastStuckCheckPosition = transform.position;
                nextStuckCheckTime = Time.time + stuckCheckInterval;
                return;
            }

            if (killhouseTactic == KillhouseTactic.TakeCover)
            {
                RecoverKillhouseCoverDestination();
                lastStuckCheckPosition = transform.position;
                nextStuckCheckTime = Time.time + stuckCheckInterval;
                return;
            }

            movement.RequestJump();
            nextStuckJumpTime = Time.time + stuckJumpInterval;
            consecutiveStuckChecks = 0;
        }

        lastStuckCheckPosition = transform.position;
        nextStuckCheckTime = Time.time + stuckCheckInterval;
    }

    private void RecoverKillhouseSearchDestination()
    {
        hasKillhouseDestination = false;
        hasSearchDestination = false;
        consecutiveStuckChecks = 0;
        nextKillhouseRepathTime = 0f;
        searchProbeIndex += Mathf.Max(1, searchAreaSamples / 2);

        if (movement)
            movement.StopMovement(true);
    }

    private void RecoverKillhouseCoverDestination()
    {
        hasKillhouseDestination = false;
        StorePreviousCoverDestination();
        hasCoverDestination = false;
        coverHoldStarted = false;
        consecutiveStuckChecks = 0;
        nextKillhouseRepathTime = 0f;
        coverDestinationHoldUntil = 0f;
        nextCoverSearchTime = Time.time + coverRetryDelay;

        if (movement)
            movement.StopMovement(true);
    }

    private bool TryFindCoverPosition(Vector3 threatPosition, out Vector3 bestCoverPosition)
    {
        bestCoverPosition = Vector3.zero;
        Vector3 flatThreatPosition = threatPosition;
        flatThreatPosition.y = transform.position.y;
        Vector3 awayFromThreat = transform.position - flatThreatPosition;
        awayFromThreat.y = 0f;
        if (awayFromThreat.sqrMagnitude <= MinDirectionSqr)
            return false;

        awayFromThreat.Normalize();
        float bestScore = float.NegativeInfinity;
        int sampleCount = Mathf.Max(4, coverSearchSamples);
        float radius = Mathf.Max(0.5f, coverSearchRadius);
        float desiredThreatDistance = Mathf.Max(
            coverMinPlayerDistance + 1f,
            Mathf.Min(maxFirearmRange * 0.85f, coverMinPlayerDistance + radius));

        for (int i = 0; i < sampleCount; i++)
        {
            float angle = i * 137.508f + Mathf.Repeat(Time.time * 57f, 360f);
            float sampleRadius = Mathf.Lerp(radius * 0.35f, radius, ((i * 7) % sampleCount) / (float)Mathf.Max(1, sampleCount - 1));
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * sampleRadius;
            Vector3 rawPoint = transform.position + offset;

            if (!TrySampleNavMesh(rawPoint, 2.5f, out Vector3 candidate))
                continue;

            if (!IsReachableNavMeshDestination(candidate, out candidate))
                continue;

            float threatDistance = FlatDistance(candidate, flatThreatPosition);
            if (threatDistance < coverMinPlayerDistance)
                continue;

            if (HasLineOfSightFrom(candidate + Vector3.up * 1.2f, threatPosition))
                continue;

            float selfDistance = Vector3.Distance(transform.position, candidate);
            Vector3 toCandidate = candidate - transform.position;
            toCandidate.y = 0f;
            float awayAlignment = toCandidate.sqrMagnitude > MinDirectionSqr
                ? Vector3.Dot(toCandidate.normalized, awayFromThreat)
                : 0f;

            float threatDistanceError = Mathf.Abs(threatDistance - desiredThreatDistance) / desiredThreatDistance;
            float score = 0f;
            score += awayAlignment * 1.25f;
            score -= threatDistanceError * 1.5f;
            score -= Mathf.Clamp01(selfDistance / radius) * 0.75f;
            score += UnityEngine.Random.value * 0.35f;

            if (hasPreviousCoverDestination)
            {
                float previousDistance = FlatDistance(previousCoverDestination, candidate);
                if (previousDistance < Mathf.Max(killhouseReachDistance * 2f, searchMinPointSpacing))
                    score -= 2f;
                else
                    score += Mathf.Clamp01(previousDistance / radius) * 0.35f;
            }

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestCoverPosition = candidate;
        }

        if (bestScore > float.NegativeInfinity)
            return true;

        return TryFallbackCoverBehindObstacle(threatPosition, out bestCoverPosition);
    }

    private bool TryFallbackCoverBehindObstacle(Vector3 threatPosition, out Vector3 cover)
    {
        cover = Vector3.zero;
        Vector3 flatThreatPosition = threatPosition;
        flatThreatPosition.y = transform.position.y;
        Vector3 fromThreat = transform.position - flatThreatPosition;
        fromThreat.y = 0f;
        if (fromThreat.sqrMagnitude <= MinDirectionSqr)
            fromThreat = -transform.forward;

        Vector3 direction = fromThreat.normalized;
        float baseDistance = Mathf.Max(2f, coverSearchRadius * 0.5f);

        for (int i = 0; i < 3; i++)
        {
            Vector3 candidate = transform.position + direction * (baseDistance + i * 1.5f);
            if (!IsReachableNavMeshDestination(candidate, out cover))
                continue;

            if (FlatDistance(cover, flatThreatPosition) < coverMinPlayerDistance)
                continue;

            if (!HasLineOfSightFrom(cover + Vector3.up * 1.2f, threatPosition))
                return true;
        }

        cover = Vector3.zero;
        return false;
    }

    private bool HasLineOfSightFrom(Vector3 origin, Vector3 target)
    {
        Vector3 toTarget = target - origin;
        float distance = toTarget.magnitude;
        if (distance <= MinDirectionSqr)
            return true;

        Vector3 direction = toTarget / distance;
        if (!Physics.Raycast(origin, direction, out RaycastHit hit, distance, coverProbeLayers, QueryTriggerInteraction.Ignore))
            return true;

        return IsPlayerTransform(hit.transform);
    }

    private bool TrySampleNavMesh(Vector3 position, float maxDistance, out Vector3 sampledPosition)
    {
        if (NavMesh.SamplePosition(position, out NavMeshHit hit, maxDistance, NavMesh.AllAreas))
        {
            sampledPosition = hit.position;
            return true;
        }

        sampledPosition = position;
        return false;
    }

    private bool IsReachableNavMeshDestination(Vector3 position)
    {
        return IsReachableNavMeshDestination(position, out _);
    }

    private bool IsReachableNavMeshDestination(Vector3 position, out Vector3 sampledPosition)
    {
        if (!TrySampleNavMesh(position, 2.5f, out sampledPosition))
            return false;

        NavMeshPath path = new NavMeshPath();
        return NavMesh.CalculatePath(transform.position, sampledPosition, NavMesh.AllAreas, path) &&
               path.status == NavMeshPathStatus.PathComplete;
    }

    private bool AreKillhouseParticipantsOnNavMesh(out bool npcOnNavMesh, out bool playerOnNavMesh)
    {
        npcOnNavMesh = IsTransformOnNavMeshSurface(transform);
        playerOnNavMesh = TryResolveKillhouseStartupPlayerTransform(out Transform playerTransform) &&
                          IsTransformOnNavMeshSurface(playerTransform);

        return npcOnNavMesh && playerOnNavMesh;
    }

    private bool TryResolveKillhouseStartupPlayerTransform(out Transform playerTransform)
    {
        if (!playerState && playerTarget)
            playerState = playerTarget.GetComponentInParent<PlayerState>();

        if ((!playerState || !playerTarget) && autoFindPlayer)
            TryAcquireLivePlayerTarget();

        if (!playerTarget && playerState)
            playerTarget = playerState.transform;

        if (!killhouseResultResolving && playerState && playerState.GetHealthPoints() <= 0f)
        {
            playerTransform = null;
            return false;
        }

        playerTransform = playerState ? playerState.transform : playerTarget;
        return playerTransform != null;
    }

    private static bool IsTransformOnNavMeshSurface(Transform target)
    {
        if (!target)
            return false;

        Transform sampleTransform = target;
        Rigidbody body = target.GetComponent<Rigidbody>();
        if (!body)
            body = target.GetComponentInParent<Rigidbody>();

        if (body)
            sampleTransform = body.transform;

        NavMeshAgent agent = sampleTransform.GetComponent<NavMeshAgent>();
        if (!agent)
            agent = sampleTransform.GetComponentInParent<NavMeshAgent>();

        if (agent && agent.enabled)
            return agent.isOnNavMesh;

        return NavMesh.SamplePosition(
            sampleTransform.position,
            out _,
            KillhouseStartupNavMeshSampleDistance,
            NavMesh.AllAreas);
    }

    private bool HasRecentPlayerMemory()
    {
        return hasLastKnownPlayerPosition;
    }

    private bool IsLowHealth()
    {
        if (!state)
            return false;

        float maxHealth = Mathf.Max(1f, state.GetMaxHealthPoints());
        return state.GetHealthPoints() / maxHealth <= retreatHealthPercent;
    }

    private bool ShouldReloadCurrentWeapon()
    {
        if (!IsCurrentWeaponFirearm() || !weaponController)
            return false;

        if (weaponController.GetCurrentWeaponAmmo() > 0)
            return false;

        if (weaponController.GetCurrentWeaponReserveAmmo() <= 0)
            return false;

        return CanReloadCurrentWeapon();
    }

    private bool CurrentFirearmOutOfAmmo()
    {
        return IsCurrentWeaponFirearm() &&
               weaponController &&
               weaponController.GetCurrentWeaponAmmo() <= 0 &&
               weaponController.GetCurrentWeaponReserveAmmo() <= 0;
    }

    private bool IsCurrentWeaponFirearm()
    {
        if (!weaponController)
            return false;

        NPCWeaponController.WeaponCategory category = weaponController.GetCurrentCategory();
        return category == NPCWeaponController.WeaponCategory.Pistol ||
               category == NPCWeaponController.WeaponCategory.SubmachineGun ||
               category == NPCWeaponController.WeaponCategory.Rifle ||
               category == NPCWeaponController.WeaponCategory.Shotgun;
    }

    private bool IsCurrentWeaponCloseCombat()
    {
        if (!weaponController)
            return false;

        NPCWeaponController.WeaponCategory category = weaponController.GetCurrentCategory();
        return category == NPCWeaponController.WeaponCategory.Unarmed ||
               category == NPCWeaponController.WeaponCategory.Knife ||
               category == NPCWeaponController.WeaponCategory.TwoHanded;
    }

    private void StopKillhouseMovement()
    {
        hasKillhouseDestination = false;
        if (movement)
            movement.StopMovement(true);
    }

    private bool ApplyKillhouseCombatState(bool combatMode, bool weaponInHand)
    {
        if (!state)
            return false;

        bool shouldHoldWeapon = combatMode && weaponInHand && ShouldCurrentWeaponUseHand();
        bool changed = false;

        if (state.GetCombatMode() != combatMode)
        {
            state.SetCombatMode(combatMode);
            changed = true;
        }

        if (!combatMode)
        {
            if (state.GetWeaponInHand())
            {
                state.SetWeaponInHand(false);
                changed = true;
            }

            return changed;
        }

        if (!shouldHoldWeapon && state.GetWeaponInHand())
        {
            state.SetWeaponInHand(false);
            changed = true;
        }

        return changed;
    }

    private bool ReachedFlatPosition(Vector3 position, float reachDistance)
    {
        Vector3 toPosition = position - transform.position;
        toPosition.y = 0f;
        return toPosition.sqrMagnitude <= reachDistance * reachDistance;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        Vector3 offset = b - a;
        offset.y = 0f;
        return offset.magnitude;
    }

    private bool TryEquipBestInventoryWeapon(bool forceSelection)
    {
        if (!weaponController)
            return false;

        if (useSpecificKillhouseInventoryWeapon)
            return TryEquipSelectedKillhouseInventoryWeapon();

        if (!npcInventory)
            return false;

        if (!forceSelection &&
            !useSpecificKillhouseInventoryWeapon &&
            weaponController.GetCurrentCategory() != NPCWeaponController.WeaponCategory.Unarmed &&
            (!IsCurrentWeaponFirearm() || !CurrentFirearmOutOfAmmo()))
        {
            return false;
        }

        IReadOnlyList<NPCInventory.InventoryEntry> weaponEntries = npcInventory.GetCategoryItems(NPCInventory.InventoryCategory.Weapons);
        if (weaponEntries == null || weaponEntries.Count == 0)
            return false;

        WeaponDefinition bestWeapon = null;
        NPCInventory.InventoryEntry bestEntry = null;
        int bestInstanceIndex = -1;
        int bestLoadedRounds = 0;
        int bestReserveRounds = 0;
        float bestScore = float.NegativeInfinity;

        for (int entryIndex = 0; entryIndex < weaponEntries.Count; entryIndex++)
        {
            NPCInventory.InventoryEntry entry = weaponEntries[entryIndex];
            if (entry == null || !(entry.GetItemDefinition() is WeaponDefinition weaponDefinition))
                continue;

            bool firearm = IsFirearmDefinition(weaponDefinition);
            int reserveRounds = firearm && weaponDefinition.GetAmmoType()
                ? npcInventory.GetAmmoCount(weaponDefinition.GetAmmoType())
                : 0;

            IReadOnlyList<NPCInventory.ItemInstanceData> instances = entry.GetItemInstances();
            int instanceCount = instances != null ? instances.Count : 0;
            for (int instanceIndex = 0; instanceIndex < Mathf.Max(1, instanceCount); instanceIndex++)
            {
                int loadedRounds = instanceIndex < instanceCount && instances[instanceIndex] != null
                    ? instances[instanceIndex].GetLoadedMagazineRounds()
                    : 0;

                if (firearm && loadedRounds <= 0 && reserveRounds <= 0)
                    continue;

                float score = ScoreInventoryWeapon(weaponDefinition, firearm, loadedRounds, reserveRounds);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                bestWeapon = weaponDefinition;
                bestEntry = entry;
                bestInstanceIndex = instanceIndex;
                bestLoadedRounds = loadedRounds;
                bestReserveRounds = reserveRounds;
            }
        }

        if (!bestWeapon || bestEntry == null)
            return false;

        string instanceId = bestInstanceIndex >= 0 ? npcInventory.GetInstanceId(bestEntry, bestInstanceIndex) : string.Empty;
        if (IsCurrentWeaponDefinition(bestWeapon) &&
            (string.IsNullOrWhiteSpace(instanceId) ||
             string.Equals(weaponController.GetEquippedInventoryWeaponInstanceId(), instanceId, StringComparison.Ordinal)))
        {
            weaponController.SetEquippedInventoryWeaponInstanceId(instanceId);
            weaponController.SetCurrentWeaponAmmo(bestLoadedRounds);
            weaponController.SetCurrentWeaponReserveAmmo(bestReserveRounds);
            return false;
        }

        if (!TryEquipWeaponDefinition(bestWeapon))
            return false;

        weaponController.SetEquippedInventoryWeaponInstanceId(instanceId);
        weaponController.SetCurrentWeaponAmmo(bestLoadedRounds);
        weaponController.SetCurrentWeaponReserveAmmo(bestReserveRounds);
        return true;
    }

    private bool TryEquipSelectedKillhouseInventoryWeapon()
    {
        if (string.IsNullOrWhiteSpace(selectedKillhouseInventoryWeaponInstanceId))
            return false;

        if (IsSelectedKillhouseUnarmedWeapon())
        {
            if (weaponController.GetCurrentCategory() == NPCWeaponController.WeaponCategory.Unarmed)
                return false;

            if (!weaponController.TryEquipUnarmed())
                return false;

            weaponController.SetEquippedInventoryWeaponInstanceId(string.Empty);
            weaponController.SetCurrentWeaponAmmo(0);
            weaponController.SetCurrentWeaponReserveAmmo(0);
            return true;
        }

        if (!TryResolveSelectedKillhouseInventoryWeapon(
                out WeaponDefinition selectedWeapon,
                out _,
                out _,
                out int loadedRounds,
                out int reserveRounds,
                out string instanceId))
        {
            return false;
        }

        bool alreadyEquipped = IsCurrentWeaponDefinition(selectedWeapon) &&
                               string.Equals(
                                   weaponController.GetEquippedInventoryWeaponInstanceId(),
                                   instanceId,
                                   StringComparison.Ordinal);

        if (alreadyEquipped)
        {
            SyncSelectedKillhouseWeaponAmmo(instanceId, loadedRounds, reserveRounds);
            return false;
        }

        if (!TryEquipWeaponDefinition(selectedWeapon))
            return false;

        SyncSelectedKillhouseWeaponAmmo(instanceId, loadedRounds, reserveRounds);
        return true;
    }

    private bool IsSelectedKillhouseUnarmedWeapon()
    {
        return useSpecificKillhouseInventoryWeapon &&
               string.Equals(
                   selectedKillhouseInventoryWeaponInstanceId,
                   KillhouseUnarmedWeaponSelectionId,
                   StringComparison.Ordinal);
    }

    private bool TryResolveSelectedKillhouseInventoryWeapon(
        out WeaponDefinition weaponDefinition,
        out NPCInventory.InventoryEntry inventoryEntry,
        out int instanceIndex,
        out int loadedRounds,
        out int reserveRounds,
        out string instanceId)
    {
        weaponDefinition = null;
        inventoryEntry = null;
        instanceIndex = -1;
        loadedRounds = 0;
        reserveRounds = 0;
        instanceId = string.Empty;

        if (!npcInventory || string.IsNullOrWhiteSpace(selectedKillhouseInventoryWeaponInstanceId))
            return false;

        string selectedInstanceId = selectedKillhouseInventoryWeaponInstanceId.Trim();
        IReadOnlyList<NPCInventory.InventoryEntry> weaponEntries = npcInventory.GetCategoryItems(NPCInventory.InventoryCategory.Weapons);
        if (weaponEntries == null)
            return false;

        for (int entryIndex = 0; entryIndex < weaponEntries.Count; entryIndex++)
        {
            NPCInventory.InventoryEntry entry = weaponEntries[entryIndex];
            if (entry == null || !(entry.GetItemDefinition() is WeaponDefinition candidateWeapon))
                continue;

            IReadOnlyList<NPCInventory.ItemInstanceData> instances = entry.GetItemInstances();
            int instanceCount = instances != null ? instances.Count : 0;
            for (int i = 0; i < instanceCount; i++)
            {
                NPCInventory.ItemInstanceData instance = instances[i];
                if (instance == null)
                    continue;

                string candidateInstanceId = instance.GetInstanceId();
                if (!string.Equals(candidateInstanceId, selectedInstanceId, StringComparison.Ordinal))
                    continue;

                bool firearm = IsFirearmDefinition(candidateWeapon);
                weaponDefinition = candidateWeapon;
                inventoryEntry = entry;
                instanceIndex = i;
                loadedRounds = instance.GetLoadedMagazineRounds();
                reserveRounds = firearm && candidateWeapon.GetAmmoType()
                    ? npcInventory.GetAmmoCount(candidateWeapon.GetAmmoType())
                    : 0;
                instanceId = candidateInstanceId;
                return true;
            }
        }

        return false;
    }

    private void SyncSelectedKillhouseWeaponAmmo(string instanceId, int loadedRounds, int reserveRounds)
    {
        weaponController.SetEquippedInventoryWeaponInstanceId(instanceId);
        weaponController.SetCurrentWeaponAmmo(loadedRounds);
        weaponController.SetCurrentWeaponReserveAmmo(reserveRounds);
    }

    private bool IsCurrentWeaponDefinition(WeaponDefinition weaponDefinition)
    {
        if (!weaponDefinition || !weaponController)
            return false;

        return DoesWeaponNameMatchDefinition(weaponDefinition, weaponController.GetCurrentWeaponName());
    }

    private static bool IsFirearmDefinition(WeaponDefinition weaponDefinition)
    {
        return weaponDefinition && weaponDefinition.GetAmmoType();
    }

    private float ScoreInventoryWeapon(WeaponDefinition weaponDefinition, bool firearm, int loadedRounds, int reserveRounds)
    {
        float score = Mathf.Max(0, weaponDefinition.GetDamage());

        if (firearm)
        {
            score += preferFirearms ? 1000f : 250f;
            score += loadedRounds > 0 ? 120f : 40f;
            score += Mathf.Min(weaponDefinition.GetRange(), maxFirearmRange) * 2f;
            score += Mathf.Min(reserveRounds, 50);
        }
        else
        {
            score += preferFirearms ? 300f : 800f;
        }

        return score;
    }

    private bool TryEquipWeaponDefinition(WeaponDefinition weaponDefinition)
    {
        if (!weaponDefinition || !weaponController)
            return false;

        return weaponController.TryEquipWeaponDefinition(weaponDefinition) ||
               TryEquipWeaponByName(weaponDefinition.GetDisplayName()) ||
               TryEquipWeaponByName(weaponDefinition.name) ||
               TryEquipWeaponByName(weaponDefinition.GetItemId());
    }

    private bool TryEquipWeaponByName(string weaponName)
    {
        return !string.IsNullOrWhiteSpace(weaponName) && weaponController.TryEquipWeaponByName(weaponName);
    }

    private static bool DoesWeaponNameMatchDefinition(WeaponDefinition weaponDefinition, string weaponName)
    {
        if (!weaponDefinition || string.IsNullOrWhiteSpace(weaponName))
            return false;

        if (string.Equals(weaponDefinition.GetDisplayName(), weaponName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(weaponDefinition.name, weaponName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(weaponDefinition.GetItemId(), weaponName, StringComparison.OrdinalIgnoreCase))
            return true;

        string normalizedWeaponName = NormalizeWeaponName(weaponName);
        if (string.IsNullOrWhiteSpace(normalizedWeaponName))
            return false;

        return string.Equals(NormalizeWeaponName(weaponDefinition.GetDisplayName()), normalizedWeaponName, StringComparison.Ordinal)
               || string.Equals(NormalizeWeaponName(weaponDefinition.name), normalizedWeaponName, StringComparison.Ordinal)
               || string.Equals(NormalizeWeaponName(weaponDefinition.GetItemId()), normalizedWeaponName, StringComparison.Ordinal);
    }

    private static string NormalizeWeaponName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] chars = new char[value.Length];
        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char current = char.ToLowerInvariant(value[i]);
            if (!char.IsLetterOrDigit(current)) continue;

            chars[count] = current;
            count++;
        }

        return new string(chars, 0, count);
    }

    private bool IsOwnTransform(Transform hitTransform)
    {
        if (!hitTransform)
            return false;

        if (hitTransform.IsChildOf(transform))
            return true;

        if (state && hitTransform.IsChildOf(state.transform))
            return true;

        return false;
    }

    private bool ShouldIgnoreLineOfSightHit(Transform hitTransform)
    {
        if (!hitTransform)
            return true;

        if (IsOwnTransform(hitTransform))
            return true;

        return IsSameFactionNpcTransform(hitTransform);
    }

    private bool IsPlayerTransform(Transform hitTransform)
    {
        if (!hitTransform)
            return false;

        if (playerTarget && hitTransform.IsChildOf(playerTarget))
        {
            if (IsSameFactionNpcTransform(hitTransform))
                return false;

            return true;
        }

        PlayerState hitPlayerState = hitTransform.GetComponentInParent<PlayerState>();
        return hitPlayerState && (!playerState || hitPlayerState == playerState);
    }

    private bool IsSameFactionNpcTransform(Transform hitTransform)
    {
        if (!hitTransform)
            return false;

        NPCState hitNpcState = hitTransform.GetComponentInParent<NPCState>();
        return hitNpcState && hitNpcState != state && NPC.HasSameFaction(transform, hitNpcState.transform);
    }

    private bool IsTrackedPlayer(PlayerState candidateState, Transform candidateRoot)
    {
        if (candidateState && playerState)
            return candidateState == playerState;

        if (!candidateRoot || !playerTarget)
            return false;

        return candidateRoot == playerTarget ||
               candidateRoot.IsChildOf(playerTarget) ||
               playerTarget.IsChildOf(candidateRoot);
    }

    private float GetFlatDistanceToPlayer()
    {
        if (!playerTarget)
            return float.PositiveInfinity;

        Vector3 toPlayer = playerTarget.position - transform.position;
        toPlayer.y = 0f;
        return toPlayer.magnitude;
    }

    private Vector3 GetFlatDirectionToPlayer()
    {
        if (!playerTarget)
            return transform.forward;

        Vector3 direction = playerTarget.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= MinDirectionSqr)
            return transform.forward;

        return direction.normalized;
    }

    private Vector3 ResolveEyePosition()
    {
        if (aim && aim.AimOrigin)
            return aim.AimOrigin.position;

        return transform.position + Vector3.up * 1.45f;
    }

    public bool TryAttack()
    {
        ResolveReferences();

        if (TryEnterCombatFromAttackRequest())
            return false;

        if (!CanUseCombat())
            return false;

        NPCWeaponController.WeaponCategory equippedCategory = weaponController.GetCurrentCategory();

        if (equippedCategory == NPCWeaponController.WeaponCategory.Knife)
        {
            if (requireMeleeAttackState && !IsMeleeAttackStateActive(equippedCategory))
                return false;

            return TriggerKnifeAttack();
        }

        if (equippedCategory == NPCWeaponController.WeaponCategory.TwoHanded)
        {
            if (requireMeleeAttackState && !IsMeleeAttackStateActive(equippedCategory))
                return false;

            return TriggerTwoHandedAttack();
        }

        if (IsSupportedFirearmCategory(equippedCategory))
        {
            WeaponDefinition weaponDefinition = ResolveCurrentWeaponDefinition();
            return TryFireEquippedFirearmShot(weaponDefinition);
        }

        if (equippedCategory != NPCWeaponController.WeaponCategory.Unarmed)
            return false;

        if (requireMeleeAttackState && !IsMeleeAttackStateActive(equippedCategory))
            return false;

        return TriggerUnarmedAttack();
    }

    public bool TryBlock()
    {
        if (!CanUseCombat())
            return false;

        NPCWeaponController.WeaponCategory equippedCategory = weaponController.GetCurrentCategory();

        if (equippedCategory == NPCWeaponController.WeaponCategory.Knife)
            return SetAnimatorTrigger(KnifeBlockParam);

        if (equippedCategory == NPCWeaponController.WeaponCategory.TwoHanded)
            return SetAnimatorTrigger(TwoHandedBlockParam);

        if (equippedCategory == NPCWeaponController.WeaponCategory.Unarmed)
            return SetAnimatorTrigger(UnarmedBlockParam);

        return false;
    }

    public bool TryReload()
    {
        ResolveReferences();

        if (!CanUseCombat())
            return false;

        SyncEquippedWeaponAmmoWithController();

        NPCWeaponController.WeaponCategory equippedCategory = weaponController.GetCurrentCategory();
        bool isPistol = equippedCategory == NPCWeaponController.WeaponCategory.Pistol;
        bool isLongarm = IsLongarmCategory(equippedCategory);
        if (!isPistol && !isLongarm)
            return false;

        if (isReloadPending)
            return false;

        WeaponDefinition weaponDefinition = ResolveCurrentWeaponDefinition();
        if (!CanReload(weaponDefinition))
            return false;

        pendingReloadWeaponDefinition = weaponDefinition;
        isReloadPending = true;
        hasEnteredReloadState = false;
        reloadStartedTime = Time.time;

        if (!animator || !reloadCompletesFromAnimationEvent)
        {
            CompleteReload();
            return true;
        }

        animator.ResetTrigger(PistolReloadParam);
        animator.ResetTrigger(LongarmReloadParam);
        animator.SetTrigger(isLongarm ? LongarmReloadParam : PistolReloadParam);
        return true;
    }

    public void OnPistolReloadAnimationFinished()
    {
        CompleteReload();
    }

    public void OnLongarmReloadAnimationFinished()
    {
        CompleteReload();
    }

    public void OnMeleeHitAnimationEvent()
    {
        ApplyMeleeDamage();
    }

    private bool TriggerUnarmedAttack()
    {
        if (!animator)
            return false;

        if (WouldMeleeSwingThreatenSameFactionNpc())
            return false;

        if (!TryBeginMeleeSwing())
            return false;

        animator.ResetTrigger(PunchLeftParam);
        animator.ResetTrigger(PunchRightParam);
        animator.SetTrigger(nextPunchIsLeft ? PunchLeftParam : PunchRightParam);
        nextPunchIsLeft = !nextPunchIsLeft;

        if (applyMeleeDamageImmediately)
            ApplyMeleeDamage();

        return true;
    }

    private bool TriggerKnifeAttack()
    {
        if (!animator)
            return false;

        if (WouldMeleeSwingThreatenSameFactionNpc())
            return false;

        if (!TryBeginMeleeSwing())
            return false;

        animator.ResetTrigger(StabParam);
        animator.ResetTrigger(SlashParam);
        animator.SetTrigger(nextKnifeIsStab ? StabParam : SlashParam);
        nextKnifeIsStab = !nextKnifeIsStab;

        if (applyMeleeDamageImmediately)
            ApplyMeleeDamage();

        return true;
    }

    private bool TriggerTwoHandedAttack()
    {
        if (!animator)
            return false;

        if (WouldMeleeSwingThreatenSameFactionNpc())
            return false;

        if (!TryBeginMeleeSwing())
            return false;

        animator.ResetTrigger(LeftStrikeParam);
        animator.ResetTrigger(RightStrikeParam);
        animator.SetTrigger(nextTwoHandedIsLeftStrike ? LeftStrikeParam : RightStrikeParam);
        nextTwoHandedIsLeftStrike = !nextTwoHandedIsLeftStrike;

        if (applyMeleeDamageImmediately)
            ApplyMeleeDamage();

        return true;
    }

    private bool TryFireEquippedFirearmShot(WeaponDefinition weaponDefinition)
    {
        if (weaponDefinition == null)
            return false;

        NPCWeaponController.WeaponCategory equippedCategory = weaponController.GetCurrentCategory();
        if (requireFirearmAttackState && !IsFirearmAttackStateActive(equippedCategory))
            return false;

        if (!CanFire(weaponDefinition))
            return false;

        if (!IsFirearmAlignedForShot(ResolveSingleGunPoint()))
            return false;

        int roundsToFire = ResolveFirearmRoundCountForCurrentShot(weaponDefinition);
        if (roundsToFire <= 0)
            return false;

        if (WouldFirearmShotThreatenSameFactionNpc(weaponDefinition, roundsToFire))
            return false;

        if (!TryConsumeFirearmRounds(weaponDefinition, roundsToFire))
            return false;

        int firedRounds = FireProjectiles(weaponDefinition, roundsToFire);
        if (firedRounds <= 0)
        {
            RestoreConsumedFirearmRounds(weaponDefinition, roundsToFire);
            return false;
        }

        if (firedRounds < roundsToFire)
            RestoreConsumedFirearmRounds(weaponDefinition, roundsToFire - firedRounds);

        SetNextFireTime(weaponDefinition);
        ApplyRuntimeAmmoToController(weaponDefinition);
        return true;
    }

    private bool WouldFirearmShotThreatenSameFactionNpc(WeaponDefinition weaponDefinition, int roundsToFire)
    {
        if (!avoidSameFactionFriendlyFire || weaponDefinition == null)
            return false;

        int clampedRoundsToFire = Mathf.Max(1, roundsToFire);
        if (clampedRoundsToFire >= MaxDoubleBarrelRoundCount &&
            TryResolveDoubleBarrelGunPoints(out Transform leftGunPoint, out Transform rightGunPoint))
        {
            return WouldFirearmShotFromMuzzleThreatenSameFactionNpc(weaponDefinition, leftGunPoint) ||
                   WouldFirearmShotFromMuzzleThreatenSameFactionNpc(weaponDefinition, rightGunPoint);
        }

        return WouldFirearmShotFromMuzzleThreatenSameFactionNpc(weaponDefinition, ResolveSingleGunPoint());
    }

    private bool WouldFirearmShotFromMuzzleThreatenSameFactionNpc(WeaponDefinition weaponDefinition, Transform muzzle)
    {
        Vector3 spawnPosition = muzzle ? muzzle.position : transform.position;
        Vector3 shotDirection = ResolveShotDirection(spawnPosition, muzzle);
        if (shotDirection.sqrMagnitude <= MinAimDirectionSqr)
            return false;

        shotDirection.Normalize();
        float checkDistance = ResolveFriendlyFireCheckDistance(spawnPosition, weaponDefinition);
        if (checkDistance <= MinRayDistance)
            return false;

        if (HasSameFactionNpcOverlappingShotStart(spawnPosition))
            return true;

        float checkRadius = ResolveFriendlyFireCheckRadius(weaponDefinition, checkDistance);
        int hitCount = checkRadius > 0f
            ? Physics.SphereCastNonAlloc(
                spawnPosition,
                checkRadius,
                shotDirection,
                FriendlyFireHits,
                checkDistance,
                friendlyFireAvoidanceLayers,
                QueryTriggerInteraction.Collide)
            : Physics.RaycastNonAlloc(
                spawnPosition,
                shotDirection,
                FriendlyFireHits,
                checkDistance,
                friendlyFireAvoidanceLayers,
                QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = FriendlyFireHits[i].collider;
            FriendlyFireHits[i] = new RaycastHit();
            if (IsSameFactionNpcFriendlyFireCollider(hitCollider))
                return true;
        }

        return false;
    }

    private float ResolveFriendlyFireCheckDistance(Vector3 spawnPosition, WeaponDefinition weaponDefinition)
    {
        if (playerTarget)
            return Mathf.Max(MinRayDistance, Vector3.Distance(spawnPosition, ResolvePlayerAimPosition()));

        float weaponRange = weaponDefinition ? weaponDefinition.GetRange() : 0f;
        if (weaponRange > 0f)
            return weaponRange;

        return fallbackAimDistance;
    }

    private float ResolveFriendlyFireCheckRadius(WeaponDefinition weaponDefinition, float checkDistance)
    {
        float radius = Mathf.Max(0f, friendlyFireAvoidanceRadius);
        float spreadDegrees = 0f;

        if (IsShotgunPelletSimulationActive() && shotgunPellets != null)
        {
            float maxSpreadDistance = Mathf.Max(MinRayDistance, shotgunPellets.maxSpreadDistance);
            float spreadDistanceT = Mathf.Clamp01(checkDistance / maxSpreadDistance);
            float minSpread = Mathf.Max(0f, shotgunPellets.spreadAtMuzzleDegrees);
            float maxSpread = Mathf.Max(minSpread, shotgunPellets.spreadAtMaxDistanceDegrees);
            spreadDegrees = Mathf.Lerp(minSpread, maxSpread, spreadDistanceT);
        }
        else if (weaponDefinition != null && weaponDefinition.IsAutomatic())
        {
            spreadDegrees = weaponDefinition.GetSpread();
        }

        if (spreadDegrees > 0f)
            radius += Mathf.Tan(Mathf.Deg2Rad * spreadDegrees) * Mathf.Max(0f, checkDistance);

        return radius;
    }

    private bool HasSameFactionNpcOverlappingShotStart(Vector3 spawnPosition)
    {
        float radius = Mathf.Max(0.01f, friendlyFireAvoidanceRadius);
        int hitCount = Physics.OverlapSphereNonAlloc(
            spawnPosition,
            radius,
            friendlyFireOverlapHits,
            friendlyFireAvoidanceLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = friendlyFireOverlapHits[i];
            friendlyFireOverlapHits[i] = null;
            if (IsSameFactionNpcFriendlyFireCollider(hitCollider))
                return true;
        }

        return false;
    }

    private bool WouldMeleeSwingThreatenSameFactionNpc()
    {
        if (!avoidSameFactionFriendlyFire)
            return false;

        Transform origin = meleeOrigin ? meleeOrigin : transform;
        if (!origin)
            return false;

        Vector3 overlapStart = origin.position;
        Vector3 forward = origin.forward.sqrMagnitude > MinAimDirectionSqr
            ? origin.forward.normalized
            : transform.forward.normalized;
        ResolveAssistedMeleeHitVolume(out float range, out float radius);
        Vector3 overlapEnd = overlapStart + forward * range;

        int hitCount = Physics.OverlapCapsuleNonAlloc(
            overlapStart,
            overlapEnd,
            radius,
            friendlyFireOverlapHits,
            meleeHitLayers,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            Collider hitCollider = friendlyFireOverlapHits[i];
            friendlyFireOverlapHits[i] = null;
            if (!IsSameFactionNpcFriendlyFireCollider(hitCollider))
                continue;

            if (IsInsideAssistedMeleeFacing(hitCollider, overlapStart, forward))
                return true;
        }

        return false;
    }

    private bool IsSameFactionNpcFriendlyFireCollider(Collider hitCollider)
    {
        if (!hitCollider)
            return false;

        Transform hitTransform = hitCollider.transform;
        if (IsOwnTransform(hitTransform) || IsUnderWeaponInHand(hitTransform))
            return false;

        return IsSameFactionNpcTransform(hitTransform);
    }

    private int FireProjectiles(WeaponDefinition weaponDefinition, int roundsToFire)
    {
        if (weaponDefinition == null || roundsToFire <= 0)
            return 0;

        AmmoDefinition ammoType = weaponDefinition.GetAmmoType();
        if (ammoType == null)
            return 0;

        GameObject roundPrefab = ammoType.GetRoundPrefab();
        int clampedRoundsToFire = Mathf.Max(1, roundsToFire);
        int requestedMuzzleShots = Mathf.Min(MaxDoubleBarrelRoundCount, clampedRoundsToFire);
        int spawnedShots = 0;

        if (requestedMuzzleShots == MaxDoubleBarrelRoundCount &&
            TryResolveDoubleBarrelGunPoints(out Transform leftGunPoint, out Transform rightGunPoint))
        {
            if (TryFireProjectileFromGunPoint(weaponDefinition, ammoType, roundPrefab, leftGunPoint))
                spawnedShots++;

            if (TryFireProjectileFromGunPoint(weaponDefinition, ammoType, roundPrefab, rightGunPoint))
                spawnedShots++;

            return spawnedShots;
        }

        return TryFireProjectileFromGunPoint(weaponDefinition, ammoType, roundPrefab, ResolveSingleGunPoint()) ? 1 : 0;
    }

    private bool TryFireProjectileFromGunPoint(
        WeaponDefinition weaponDefinition,
        AmmoDefinition ammoType,
        GameObject roundPrefab,
        Transform muzzle)
    {
        Vector3 spawnPosition = muzzle ? muzzle.position : transform.position;
        Vector3 baseShotDirection = ResolveShotDirection(spawnPosition, muzzle);
        Vector3 shotDirection = ApplyAutomaticWeaponSpread(weaponDefinition, baseShotDirection);
        float weaponDamage = ResolveFirearmProjectileDamage(weaponDefinition);
        Transform instigatorRoot = ResolveProjectileInstigatorRoot();

        if (IsShotgunPelletSimulationActive())
            return FireShotgunPellets(weaponDefinition, ammoType, spawnPosition, shotDirection, weaponDamage, instigatorRoot);

        if (!roundPrefab)
            return false;

        return SpawnProjectileRound(roundPrefab, spawnPosition, shotDirection, weaponDefinition.GetMuzzleVelocity(), ammoType, weaponDamage, instigatorRoot);
    }

    private bool FireShotgunPellets(
        WeaponDefinition weaponDefinition,
        AmmoDefinition ammoType,
        Vector3 spawnPosition,
        Vector3 baseShotDirection,
        float totalShotDamage,
        Transform instigatorRoot)
    {
        ShotgunPellets settings = shotgunPellets;
        GameObject pelletPrefab = settings.pelletPrefabOverride ? settings.pelletPrefabOverride : ammoType.GetRoundPrefab();
        if (!pelletPrefab)
            return false;

        int pelletCount = Mathf.Max(1, settings.pelletsPerShot);
        float pelletDamage = pelletCount > 0 ? Mathf.Max(0f, totalShotDamage) / pelletCount : Mathf.Max(0f, totalShotDamage);
        float shotDistance = ResolveShotDistanceEstimate(spawnPosition);
        float maxSpreadDistance = Mathf.Max(MinRayDistance, settings.maxSpreadDistance);
        float spreadDistanceT = Mathf.Clamp01(shotDistance / maxSpreadDistance);
        float minSpread = Mathf.Max(0f, settings.spreadAtMuzzleDegrees);
        float maxSpread = Mathf.Max(minSpread, settings.spreadAtMaxDistanceDegrees);
        float spreadDegrees = Mathf.Lerp(minSpread, maxSpread, spreadDistanceT);
        float weaponMuzzleVelocity = weaponDefinition ? Mathf.Max(0f, weaponDefinition.GetMuzzleVelocity()) : 0f;
        float muzzleVelocity = weaponMuzzleVelocity > 0f ? weaponMuzzleVelocity : Mathf.Max(0f, settings.fallbackPelletMuzzleVelocity);
        float pelletSpawnForwardOffset = Mathf.Max(0f, settings.pelletSpawnForwardOffset);
        List<Collider> spawnedPelletColliders = settings.ignorePelletSelfCollisions ? new List<Collider>(pelletCount) : null;

        bool spawnedAnyPellet = false;
        for (int i = 0; i < pelletCount; i++)
        {
            Vector3 pelletDirection = GetDirectionInsideSpreadCone(baseShotDirection, spreadDegrees);
            Vector3 pelletSpawnPosition = spawnPosition + pelletDirection * pelletSpawnForwardOffset;

            if (SpawnProjectileRound(pelletPrefab, pelletSpawnPosition, pelletDirection, muzzleVelocity, ammoType, pelletDamage, instigatorRoot, out GameObject spawnedPellet))
            {
                spawnedAnyPellet = true;
                if (settings.ignorePelletSelfCollisions)
                    IgnorePelletCollisions(spawnedPellet, spawnedPelletColliders);
            }
        }

        return spawnedAnyPellet;
    }

    private bool SpawnProjectileRound(
        GameObject roundPrefab,
        Vector3 spawnPosition,
        Vector3 shotDirection,
        float muzzleVelocity,
        AmmoDefinition ammoType,
        float projectileDamage,
        Transform instigatorRoot)
    {
        return SpawnProjectileRound(roundPrefab, spawnPosition, shotDirection, muzzleVelocity, ammoType, projectileDamage, instigatorRoot, out _);
    }

    private bool SpawnProjectileRound(
        GameObject roundPrefab,
        Vector3 spawnPosition,
        Vector3 shotDirection,
        float muzzleVelocity,
        AmmoDefinition ammoType,
        float projectileDamage,
        Transform instigatorRoot,
        out GameObject spawnedRound)
    {
        spawnedRound = null;
        if (!roundPrefab)
            return false;

        Vector3 normalizedDirection = shotDirection.sqrMagnitude > MinAimDirectionSqr
            ? shotDirection.normalized
            : transform.forward;

        Quaternion spawnRotation = Quaternion.LookRotation(normalizedDirection, Vector3.up) * roundPrefab.transform.rotation;
        spawnedRound = Bullet.SpawnProjectile(roundPrefab, spawnPosition, spawnRotation);
        if (!spawnedRound)
            return false;

        Vector3 launchVelocity = normalizedDirection * Mathf.Max(0f, muzzleVelocity);

        Bullet bullet = spawnedRound.GetComponent<Bullet>();
        if (!bullet)
            bullet = spawnedRound.GetComponentInChildren<Bullet>(true);

        if (bullet)
        {
            bullet.ConfigureBallisticsFromAmmoDefinition(ammoType);
            bullet.ConfigureDamage(projectileDamage, instigatorRoot);
            bullet.Launch(launchVelocity);
            return true;
        }

        Rigidbody roundRigidbody = spawnedRound.GetComponent<Rigidbody>();
        if (!roundRigidbody)
            roundRigidbody = spawnedRound.GetComponentInChildren<Rigidbody>(true);

        if (roundRigidbody)
            roundRigidbody.linearVelocity = launchVelocity;

        return true;
    }

    private float ResolveFirearmProjectileDamage(WeaponDefinition weaponDefinition)
    {
        if (!weaponDefinition)
            return 0f;

        if (npcInventory)
            return Mathf.Max(0f, npcInventory.GetWeaponDamage(weaponDefinition));

        return Mathf.Max(0f, weaponDefinition.GetDamage());
    }

    private Transform ResolveProjectileInstigatorRoot()
    {
        if (npcState)
            return npcState.transform;

        return transform.root ? transform.root : transform;
    }

    internal static bool TryApplyProjectileDamage(
        Collider hitCollider,
        float rawDamage,
        Transform instigatorRoot,
        bool respectTargetDamageResistance)
    {
        if (instigatorRoot && instigatorRoot.GetComponentInParent<PlayerState>())
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
            return ApplyDamageToNpcTarget(targetNpc, resistedDamage, bodyArea);
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

        NPCState targetNpc = hitCollider.GetComponentInParent<NPCState>();
        return targetNpc && IsRootCombatCollider(hitCollider, targetNpc.transform);
    }

    private bool CanFire(WeaponDefinition weaponDefinition)
    {
        if (weaponDefinition == null)
            return false;

        if (Time.time < nextFireAllowedTime)
            return false;

        return GetAvailableFirearmRounds(weaponDefinition) > 0;
    }

    private bool CanReload(WeaponDefinition weaponDefinition)
    {
        if (weaponDefinition == null)
            return false;

        int magazineSize = Mathf.Max(0, weaponDefinition.GetMagazineSize());
        if (magazineSize <= 0)
            return false;

        EnsureMagazineInitialized(weaponDefinition);

        int loadedRounds = GetLoadedMagazineRounds(weaponDefinition);
        if (loadedRounds >= magazineSize)
            return false;

        return GetReserveAmmoCount(weaponDefinition) > 0;
    }

    private void CompleteReload()
    {
        if (!isReloadPending)
            return;

        WeaponDefinition weaponDefinition = pendingReloadWeaponDefinition;
        pendingReloadWeaponDefinition = null;
        isReloadPending = false;
        hasEnteredReloadState = false;

        ReloadMagazine(weaponDefinition);
    }

    private void CompletePendingReloadFromAnimatorState()
    {
        if (!isReloadPending)
        {
            hasEnteredReloadState = false;
            return;
        }

        if (!reloadCompletesFromAnimationEvent)
            return;

        if (!animator)
            return;

        if (IsReloadStateActive())
        {
            hasEnteredReloadState = true;
            if (Time.time - reloadStartedTime >= reloadFallbackCompleteSeconds)
                CompleteReload();

            return;
        }

        if (!hasEnteredReloadState)
        {
            if (Time.time - reloadStartedTime >= reloadFallbackCompleteSeconds)
                CompleteReload();

            return;
        }

        CompleteReload();
    }

    private void ReloadMagazine(WeaponDefinition weaponDefinition)
    {
        if (weaponDefinition == null)
            return;

        int magazineSize = Mathf.Max(0, weaponDefinition.GetMagazineSize());
        if (magazineSize <= 0)
            return;

        EnsureMagazineInitialized(weaponDefinition);

        int loadedRounds = GetLoadedMagazineRounds(weaponDefinition);
        int roundsNeeded = magazineSize - loadedRounds;
        if (roundsNeeded <= 0)
            return;

        int reserveAmmo = GetReserveAmmoCount(weaponDefinition);
        int roundsToLoad = Mathf.Min(roundsNeeded, reserveAmmo);
        if (roundsToLoad <= 0)
            return;

        if (!TryConsumeReserveAmmo(weaponDefinition, roundsToLoad))
            return;

        SetLoadedMagazineRounds(weaponDefinition, loadedRounds + roundsToLoad);
        ApplyRuntimeAmmoToController(weaponDefinition);
    }

    private bool TryConsumeFirearmRounds(WeaponDefinition weaponDefinition, int roundCount)
    {
        if (weaponDefinition == null || roundCount <= 0)
            return false;

        int consumedRounds = 0;
        for (int i = 0; i < roundCount; i++)
        {
            if (!ConsumeFirearmRound(weaponDefinition))
            {
                if (consumedRounds > 0)
                    RestoreConsumedFirearmRounds(weaponDefinition, consumedRounds);

                return false;
            }

            consumedRounds++;
        }

        return true;
    }

    private bool ConsumeFirearmRound(WeaponDefinition weaponDefinition)
    {
        if (weaponDefinition == null)
            return false;

        int magazineSize = Mathf.Max(0, weaponDefinition.GetMagazineSize());
        if (magazineSize <= 0)
            return TryConsumeReserveAmmo(weaponDefinition, 1);

        EnsureMagazineInitialized(weaponDefinition);
        int loadedRounds = GetLoadedMagazineRounds(weaponDefinition);
        if (loadedRounds <= 0)
            return false;

        SetLoadedMagazineRounds(weaponDefinition, loadedRounds - 1);
        return true;
    }

    private void RestoreConsumedFirearmRounds(WeaponDefinition weaponDefinition, int roundCount)
    {
        if (weaponDefinition == null || roundCount <= 0)
            return;

        int magazineSize = Mathf.Max(0, weaponDefinition.GetMagazineSize());
        if (magazineSize <= 0)
        {
            AmmoDefinition ammoType = weaponDefinition.GetAmmoType();
            if (ammoType && npcInventory)
                npcInventory.AddItem(ammoType, roundCount);

            return;
        }

        EnsureMagazineInitialized(weaponDefinition);
        int loadedRounds = GetLoadedMagazineRounds(weaponDefinition);
        SetLoadedMagazineRounds(weaponDefinition, loadedRounds + roundCount);
    }

    private bool TryConsumeReserveAmmo(WeaponDefinition weaponDefinition, int amount)
    {
        if (weaponDefinition == null)
            return false;

        if (amount <= 0)
            return true;

        AmmoDefinition ammoType = weaponDefinition.GetAmmoType();
        if (!ammoType || !npcInventory)
            return false;

        return npcInventory.RemoveItem(ammoType, amount);
    }

    private int GetReserveAmmoCount(WeaponDefinition weaponDefinition)
    {
        if (weaponDefinition == null)
            return 0;

        AmmoDefinition ammoType = weaponDefinition.GetAmmoType();
        if (!ammoType || !npcInventory)
            return 0;

        return Mathf.Max(0, npcInventory.GetAmmoCount(ammoType));
    }

    private int GetAvailableFirearmRounds(WeaponDefinition weaponDefinition)
    {
        if (weaponDefinition == null)
            return 0;

        int magazineSize = Mathf.Max(0, weaponDefinition.GetMagazineSize());
        if (magazineSize <= 0)
            return GetReserveAmmoCount(weaponDefinition);

        EnsureMagazineInitialized(weaponDefinition);
        return Mathf.Max(0, GetLoadedMagazineRounds(weaponDefinition));
    }

    private void EnsureMagazineInitialized(WeaponDefinition weaponDefinition)
    {
        if (weaponDefinition == null)
            return;

        int magazineSize = Mathf.Max(0, weaponDefinition.GetMagazineSize());
        if (magazineSize <= 0)
            return;

        if (!TryGetWeaponAmmoKey(weaponDefinition, out string weaponAmmoKey))
            return;

        if (loadedRoundsByWeaponKey.ContainsKey(weaponAmmoKey))
            return;

        int initialLoadedRounds = ResolveInitialLoadedMagazineRounds(weaponDefinition);
        SetLoadedMagazineRounds(weaponDefinition, Mathf.Clamp(initialLoadedRounds, 0, magazineSize));
    }

    private int ResolveInitialLoadedMagazineRounds(WeaponDefinition weaponDefinition)
    {
        int loadedRounds = weaponController ? Mathf.Max(0, weaponController.GetCurrentWeaponAmmo()) : 0;

        if (!weaponDefinition || !npcInventory)
            return loadedRounds;

        if (weaponController)
        {
            string instanceId = weaponController.GetEquippedInventoryWeaponInstanceId();
            if (!string.IsNullOrWhiteSpace(instanceId) &&
                npcInventory.TryGetWeaponMagazineRoundsByInstanceId(instanceId, out int instanceLoadedRounds))
            {
                loadedRounds = Mathf.Max(loadedRounds, instanceLoadedRounds);
            }
        }

        IReadOnlyList<NPCInventory.InventoryEntry> weaponEntries = npcInventory.GetCategoryItems(NPCInventory.InventoryCategory.Weapons);
        if (weaponEntries == null)
            return loadedRounds;

        for (int entryIndex = 0; entryIndex < weaponEntries.Count; entryIndex++)
        {
            NPCInventory.InventoryEntry entry = weaponEntries[entryIndex];
            if (entry == null)
                continue;

            if (!(entry.GetItemDefinition() is WeaponDefinition inventoryWeapon))
                continue;

            if (inventoryWeapon != weaponDefinition && !DoesWeaponNameMatch(inventoryWeapon, GetCurrentWeaponName()))
                continue;

            IReadOnlyList<NPCInventory.ItemInstanceData> instances = entry.GetItemInstances();
            if (instances == null)
                continue;

            for (int instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
            {
                NPCInventory.ItemInstanceData instance = instances[instanceIndex];
                if (instance == null)
                    continue;

                loadedRounds = Mathf.Max(loadedRounds, instance.GetLoadedMagazineRounds());
            }
        }

        return loadedRounds;
    }

    private int GetLoadedMagazineRounds(WeaponDefinition weaponDefinition)
    {
        if (weaponDefinition == null)
            return 0;

        if (!TryGetWeaponAmmoKey(weaponDefinition, out string weaponAmmoKey))
            return 0;

        return loadedRoundsByWeaponKey.TryGetValue(weaponAmmoKey, out int loadedRounds)
            ? Mathf.Max(0, loadedRounds)
            : 0;
    }

    private void SetLoadedMagazineRounds(WeaponDefinition weaponDefinition, int loadedRounds)
    {
        if (weaponDefinition == null)
            return;

        if (!TryGetWeaponAmmoKey(weaponDefinition, out string weaponAmmoKey))
            return;

        int magazineSize = Mathf.Max(0, weaponDefinition.GetMagazineSize());
        int clampedRounds = magazineSize > 0
            ? Mathf.Clamp(loadedRounds, 0, magazineSize)
            : Mathf.Max(0, loadedRounds);

        loadedRoundsByWeaponKey[weaponAmmoKey] = clampedRounds;
        PersistLoadedRoundsToBoundWeaponInstance(clampedRounds);
    }

    private void PersistLoadedRoundsToBoundWeaponInstance(int loadedRounds)
    {
        if (!weaponController || !npcInventory)
            return;

        string instanceId = weaponController.GetEquippedInventoryWeaponInstanceId();
        if (string.IsNullOrWhiteSpace(instanceId))
            return;

        npcInventory.TrySetWeaponMagazineRoundsByInstanceId(instanceId, loadedRounds);
    }

    private void SyncEquippedWeaponAmmoWithController()
    {
        WeaponDefinition weaponDefinition = ResolveCurrentWeaponDefinition();
        if (weaponDefinition == null || !weaponController)
            return;

        AmmoDefinition ammoType = weaponDefinition.GetAmmoType();
        if (!ammoType)
            return;

        int magazineSize = Mathf.Max(0, weaponDefinition.GetMagazineSize());
        if (magazineSize > 0)
            EnsureMagazineInitialized(weaponDefinition);

        ApplyRuntimeAmmoToController(weaponDefinition);
    }

    private void ApplyRuntimeAmmoToController(WeaponDefinition weaponDefinition)
    {
        if (weaponDefinition == null || !weaponController)
            return;

        int magazineSize = Mathf.Max(0, weaponDefinition.GetMagazineSize());
        int loadedRounds = magazineSize > 0 ? GetLoadedMagazineRounds(weaponDefinition) : 0;
        int reserveRounds = GetReserveAmmoCount(weaponDefinition);

        if (weaponController.GetCurrentWeaponAmmo() != loadedRounds)
            weaponController.SetCurrentWeaponAmmo(loadedRounds);

        if (weaponController.GetCurrentWeaponReserveAmmo() != reserveRounds)
            weaponController.SetCurrentWeaponReserveAmmo(reserveRounds);
    }

    private int ResolveFirearmRoundCountForCurrentShot(WeaponDefinition weaponDefinition)
    {
        if (weaponDefinition == null)
            return 0;

        int availableRounds = GetAvailableFirearmRounds(weaponDefinition);
        if (availableRounds <= 0)
            return 0;

        if (IsDoubleBarrelShotgunEquipped() &&
            TryResolveDoubleBarrelGunPoints(out _, out _))
        {
            return Mathf.Clamp(availableRounds, 1, MaxDoubleBarrelRoundCount);
        }

        return 1;
    }

    private void SetNextFireTime(WeaponDefinition weaponDefinition)
    {
        float now = Time.time;
        if (weaponDefinition == null)
        {
            nextFireAllowedTime = now;
            return;
        }

        float fireRate = weaponDefinition.GetFireRate();
        if (fireRate <= 0f)
        {
            nextFireAllowedTime = now;
            return;
        }

        nextFireAllowedTime = now + 1f / fireRate;
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
        if (!weaponController)
            return false;

        NPCWeaponController.WeaponCategory equippedCategory = weaponController.GetCurrentCategory();
        return equippedCategory == NPCWeaponController.WeaponCategory.Unarmed ||
               equippedCategory == NPCWeaponController.WeaponCategory.Knife ||
               equippedCategory == NPCWeaponController.WeaponCategory.TwoHanded;
    }

    private bool ShouldUseAssistedMeleeDamage()
    {
        return useAssistedMeleeHits || IsUnarmedMeleeDamageCategory();
    }

    private bool IsUnarmedMeleeDamageCategory()
    {
        return weaponController &&
               weaponController.GetCurrentCategory() == NPCWeaponController.WeaponCategory.Unarmed;
    }

    private void ApplyAssistedMeleeDamage(float damage)
    {
        Transform origin = meleeOrigin ? meleeOrigin : transform;
        if (!origin)
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

        Transform instigatorRoot = npcState ? npcState.transform : transform;
        for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
        {
            Collider hit = meleeHits[hitIndex];
            meleeHits[hitIndex] = null;
            RegisterAssistedMeleeHit(hit, selectionCenter, instigatorRoot, overlapStart, forward);
        }

        foreach (KeyValuePair<NPCState, MeleeTargetHit> hitSelection in meleeNpcHitSelections)
        {
            NPCState targetNpc = hitSelection.Key;
            if (!targetNpc || !meleeDamagedTargets.Add(targetNpc.GetHashCode()))
                continue;

            ApplyDamageToNpcTarget(targetNpc, damage, hitSelection.Value.bodyArea);
        }

        foreach (KeyValuePair<PlayerState, MeleeTargetHit> hitSelection in meleePlayerHitSelections)
        {
            PlayerState targetPlayer = hitSelection.Key;
            if (!targetPlayer || !meleeDamagedTargets.Add(targetPlayer.GetHashCode()))
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
        if (targetNpc && targetNpc != npcState)
        {
            RegisterMeleeHitSelection(meleeNpcHitSelections, targetNpc, hitCollider, selectionCenter);
            return;
        }

        PlayerState targetPlayer = hitCollider.GetComponentInParent<PlayerState>();
        if (targetPlayer)
            RegisterMeleeHitSelection(meleePlayerHitSelections, targetPlayer, hitCollider, selectionCenter);
    }

    private void ResolveAssistedMeleeHitVolume(out float range, out float radius)
    {
        range = meleeRange;
        radius = meleeRadius;

        if (weaponController)
        {
            NPCWeaponController.WeaponCategory equippedCategory = weaponController.GetCurrentCategory();
            if (equippedCategory == NPCWeaponController.WeaponCategory.Knife)
            {
                range = knifeMeleeRange;
                radius = knifeMeleeRadius;
            }
            else if (equippedCategory == NPCWeaponController.WeaponCategory.TwoHanded)
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
        if (!root && weaponController)
            root = FindDescendantByName(weaponController.transform, "WeaponInHand");

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
        if (!animator)
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

        if (!animator.IsInTransition(BaseLayer))
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

        Transform instigatorRoot = npcState ? npcState.transform : transform;
        if (instigatorRoot && hitCollider.transform.IsChildOf(instigatorRoot))
            return;

        if (IsUnderWeaponInHand(hitCollider.transform))
            return;

        NPCState targetNpc = hitCollider.GetComponentInParent<NPCState>();
        if (targetNpc && targetNpc != npcState)
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
        if (targetPlayer)
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
        WeaponDefinition weaponDefinition = ResolveCurrentWeaponDefinition();
        if (weaponDefinition)
        {
            if (npcInventory)
                return isUnarmed
                    ? Mathf.Max(MinUnarmedDamage, npcInventory.GetWeaponDamage(weaponDefinition))
                    : Mathf.Max(0f, npcInventory.GetWeaponDamage(weaponDefinition));

            return isUnarmed
                ? Mathf.Max(MinUnarmedDamage, weaponDefinition.GetDamage())
                : Mathf.Max(0f, weaponDefinition.GetDamage());
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

    private static bool ApplyDamageToNpcTarget(NPCState targetNpc, float damage, BodyDamageArea bodyArea)
    {
        if (!targetNpc || damage <= 0f)
            return false;

        bool isCriticalHit = RollCriticalHit();
        float modifiedDamage = ApplyBodyAreaDamageModifier(damage * GetCriticalDamageMultiplier(isCriticalHit), bodyArea);
        if (modifiedDamage <= 0f)
            return false;

        targetNpc.ApplyDamage(modifiedDamage);
        ApplyDamageToNpcBodyArea(targetNpc, modifiedDamage, bodyArea);
        return true;
    }

    private static bool ApplyDamageToPlayerTarget(PlayerState targetPlayer, float damage, BodyDamageArea bodyArea)
    {
        if (!targetPlayer || damage <= 0f)
            return false;

        bool isCriticalHit = RollCriticalHit();
        float modifiedDamage = ApplyBodyAreaDamageModifier(damage * GetCriticalDamageMultiplier(isCriticalHit), bodyArea);
        if (modifiedDamage <= 0f)
            return false;

        targetPlayer.ApplyDamage(modifiedDamage);
        ApplyDamageToPlayerBodyArea(targetPlayer, modifiedDamage, bodyArea);
        return true;
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
        if (!hitCollider)
            return BodyDamageArea.Chest;

        bool foundChestMatch = false;
        for (Transform current = hitCollider.transform; current; current = current.parent)
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
        return hitCollider && combatRoot && hitCollider.transform == combatRoot;
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

    private Vector3 ResolveShotDirection(Vector3 spawnPosition, Transform muzzle)
    {
        if (aim)
        {
            Vector3 aimDirection = aim.GetFullAimDirection(spawnPosition);
            if (aimDirection.sqrMagnitude > MinAimDirectionSqr)
                return aimDirection.normalized;
        }

        if (muzzle && muzzle.forward.sqrMagnitude > MinAimDirectionSqr)
            return muzzle.forward.normalized;

        Vector3 fallbackPoint = spawnPosition + transform.forward * fallbackAimDistance;
        return (fallbackPoint - spawnPosition).normalized;
    }

    private bool IsFirearmAlignedForShot(Transform muzzle)
    {
        if (!requireFirearmFacingAim)
            return true;

        Vector3 spawnPosition = muzzle ? muzzle.position : transform.position;
        Vector3 shotDirection = ResolveShotDirection(spawnPosition, muzzle);
        if (shotDirection.sqrMagnitude <= MinAimDirectionSqr)
            return false;

        shotDirection.Normalize();
        Vector3 flatShotDirection = shotDirection;
        flatShotDirection.y = 0f;
        if (flatShotDirection.sqrMagnitude <= MinAimDirectionSqr)
            return true;

        flatShotDirection.Normalize();

        Transform facingTransform = npcState ? npcState.transform : transform;
        Vector3 flatFacingDirection = facingTransform.forward;
        flatFacingDirection.y = 0f;
        if (flatFacingDirection.sqrMagnitude <= MinAimDirectionSqr)
            return false;

        float facingAngle = Vector3.Angle(flatFacingDirection.normalized, flatShotDirection);
        if (facingAngle > firearmFacingToleranceDegrees)
            return false;

        return true;
    }

    private float ResolveShotDistanceEstimate(Vector3 spawnPosition)
    {
        if (aim && aim.HasAimSolution)
            return Mathf.Max(MinRayDistance, Vector3.Distance(spawnPosition, aim.GetAimPoint(spawnPosition, shotgunFallbackDistance)));

        return shotgunFallbackDistance;
    }

    private Transform ResolveSingleGunPoint()
    {
        if (gunPoint)
            return gunPoint;

        Transform providerGunPoint = ResolveGunPointFromProvider();
        if (providerGunPoint)
            return providerGunPoint;

        string weaponName = GetCurrentWeaponName();
        if (cachedGunPoint &&
            string.Equals(cachedGunPointWeaponName, weaponName, StringComparison.OrdinalIgnoreCase) &&
            cachedGunPoint.gameObject.activeInHierarchy)
        {
            return cachedGunPoint;
        }

        cachedGunPoint = FindBestGunPoint(weaponName);
        cachedGunPointWeaponName = weaponName;
        return cachedGunPoint;
    }

    private Transform ResolveGunPointFromProvider()
    {
        ResolveGunPointProvider();

        return gunPointProvider ? gunPointProvider.GetGunMarker() : null;
    }

    private void ResolveGunPointProvider()
    {
        if (gunPointProvider)
            return;

        GetGunPoint[] providers = GetComponentsInChildren<GetGunPoint>(true);
        for (int i = 0; i < providers.Length; i++)
        {
            GetGunPoint provider = providers[i];
            if (!provider) continue;
            if (!string.Equals(provider.transform.name, WeaponInHandName, StringComparison.OrdinalIgnoreCase)) continue;

            gunPointProvider = provider;
            return;
        }

        if (!gunPointProvider)
            gunPointProvider = GetComponentInChildren<GetGunPoint>(true);

        if (!gunPointProvider)
            gunPointProvider = GetComponentInParent<GetGunPoint>();
    }

    private bool TryResolveDoubleBarrelGunPoints(out Transform leftGunPoint, out Transform rightGunPoint)
    {
        leftGunPoint = null;
        rightGunPoint = null;

        if (!IsDoubleBarrelShotgunEquipped())
            return false;

        if (secondaryGunPoint)
        {
            leftGunPoint = ResolveSingleGunPoint();
            rightGunPoint = secondaryGunPoint;
            return leftGunPoint && rightGunPoint;
        }

        Transform referenceGunPoint = ResolveSingleGunPoint();
        for (Transform searchRoot = referenceGunPoint; searchRoot; searchRoot = searchRoot.parent)
        {
            leftGunPoint = FindNamedGunPoint(searchRoot, LeftDoubleBarrelGunpointName);
            rightGunPoint = FindNamedGunPoint(searchRoot, RightDoubleBarrelGunpointName);
            if (leftGunPoint && rightGunPoint)
                return true;
        }

        Transform providerRoot = gunPointProvider ? gunPointProvider.transform : transform.root;
        leftGunPoint = FindNamedGunPoint(providerRoot, LeftDoubleBarrelGunpointName);
        rightGunPoint = FindNamedGunPoint(providerRoot, RightDoubleBarrelGunpointName);
        return leftGunPoint && rightGunPoint;
    }

    private static Transform FindNamedGunPoint(Transform searchRoot, string gunPointName)
    {
        if (!searchRoot || string.IsNullOrWhiteSpace(gunPointName))
            return null;

        Transform[] transforms = searchRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (!candidate) continue;
            if (!string.Equals(candidate.name, gunPointName, StringComparison.OrdinalIgnoreCase)) continue;
            if (!candidate.gameObject.activeInHierarchy) continue;

            return candidate;
        }

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (!candidate) continue;
            if (string.Equals(candidate.name, gunPointName, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
    }

    private Transform FindBestGunPoint(string weaponName)
    {
        Transform holder = weaponHolder;
        if (!holder && weaponController)
            holder = weaponController.transform.Find(WeaponHolderName);

        if (!holder)
            holder = transform.Find(WeaponHolderName);

        if (!holder)
            return null;

        string normalizedWeaponName = NormalizeName(weaponName);
        Transform[] transforms = holder.GetComponentsInChildren<Transform>(true);
        Transform bestMatch = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (!candidate) continue;
            if (!string.Equals(candidate.name, "Gunpoint", StringComparison.OrdinalIgnoreCase)) continue;

            int score = candidate.gameObject.activeInHierarchy ? 1000 : 0;
            if (!string.IsNullOrWhiteSpace(normalizedWeaponName))
            {
                for (Transform parent = candidate.parent; parent; parent = parent.parent)
                {
                    string normalizedParent = NormalizeName(parent.name);
                    if (normalizedParent.Contains(normalizedWeaponName) || normalizedWeaponName.Contains(normalizedParent))
                    {
                        score += 100;
                        break;
                    }
                }
            }

            if (score <= bestScore)
                continue;

            bestScore = score;
            bestMatch = candidate;
        }

        return bestMatch;
    }

    private WeaponDefinition ResolveCurrentWeaponDefinition()
    {
        if (!weaponController)
            return null;

        NPCWeaponController.WeaponEntry equippedWeapon = weaponController.GetCurrentWeapon();
        if (equippedWeapon == null || string.IsNullOrWhiteSpace(equippedWeapon.WeaponName))
        {
            InvalidateResolvedWeaponDefinitionCache();
            return null;
        }

        string cacheKey = BuildEquippedWeaponCacheKey(equippedWeapon);
        if (hasCachedResolvedWeaponDefinition &&
            string.Equals(cachedResolvedWeaponKey, cacheKey, StringComparison.Ordinal))
        {
            return cachedResolvedWeaponDefinition;
        }

        if (npcInventory)
        {
            IReadOnlyList<NPCInventory.InventoryEntry> weaponEntries = npcInventory.GetCategoryItems(NPCInventory.InventoryCategory.Weapons);
            if (weaponEntries != null)
            {
                for (int i = 0; i < weaponEntries.Count; i++)
                {
                    NPCInventory.InventoryEntry entry = weaponEntries[i];
                    if (entry == null) continue;

                    if (!(entry.GetItemDefinition() is WeaponDefinition weaponDefinition))
                        continue;

                    if (DoesWeaponNameMatch(weaponDefinition, equippedWeapon.WeaponName))
                    {
                        CacheResolvedWeaponDefinition(cacheKey, weaponDefinition);
                        return weaponDefinition;
                    }
                }
            }
        }

        if (weaponDefinitionLookupCache.TryGetValue(cacheKey, out WeaponDefinition cachedWeapon) && cachedWeapon)
        {
            CacheResolvedWeaponDefinition(cacheKey, cachedWeapon);
            return cachedWeapon;
        }

        WeaponDefinition[] loadedWeaponDefinitions = Resources.FindObjectsOfTypeAll<WeaponDefinition>();
        for (int i = 0; i < loadedWeaponDefinitions.Length; i++)
        {
            WeaponDefinition weaponDefinition = loadedWeaponDefinitions[i];
            if (!weaponDefinition) continue;
            if (!DoesWeaponNameMatch(weaponDefinition, equippedWeapon.WeaponName)) continue;

            weaponDefinitionLookupCache[cacheKey] = weaponDefinition;
            CacheResolvedWeaponDefinition(cacheKey, weaponDefinition);
            return weaponDefinition;
        }

        CacheResolvedWeaponDefinition(cacheKey, null);
        return null;
    }

    private bool TryGetWeaponAmmoKey(WeaponDefinition weaponDefinition, out string weaponAmmoKey)
    {
        weaponAmmoKey = null;
        if (!weaponDefinition)
            return false;

        if (weaponController)
        {
            string instanceId = weaponController.GetEquippedInventoryWeaponInstanceId();
            if (!string.IsNullOrWhiteSpace(instanceId))
            {
                weaponAmmoKey = "instance:" + instanceId.Trim();
                return true;
            }
        }

        string itemId = weaponDefinition.GetItemId();
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            weaponAmmoKey = itemId;
            return true;
        }

        string displayName = weaponDefinition.GetDisplayName();
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            weaponAmmoKey = displayName;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(weaponDefinition.name))
        {
            weaponAmmoKey = weaponDefinition.name;
            return true;
        }

        return false;
    }

    private bool CanUseCombat()
    {
        if (!npcState || npcState.IsDead())
            return false;

        if (!weaponController)
            return false;

        if (requireCombatMode && !npcState.GetCombatMode())
            return false;

        if (requireWeaponInHand && weaponController.GetCurrentCategory() != NPCWeaponController.WeaponCategory.Unarmed && !npcState.GetWeaponInHand())
            return false;

        return true;
    }

    private bool TryEnterCombatFromAttackRequest()
    {
        if (!npcState || npcState.IsDead())
            return false;

        if (!weaponController)
            return false;

        NPCWeaponController.WeaponCategory category = weaponController.GetCurrentCategory();
        bool needsWeaponInHand = category != NPCWeaponController.WeaponCategory.Unarmed;
        bool changed = false;

        if (requireCombatMode && !npcState.GetCombatMode())
        {
            npcState.SetCombatMode(true);
            changed = true;
        }

        if (!needsWeaponInHand && npcState.GetWeaponInHand())
        {
            npcState.SetWeaponInHand(false);
            changed = true;
        }

        return changed;
    }

    private bool SetAnimatorTrigger(int triggerHash)
    {
        if (!animator)
            return false;

        animator.ResetTrigger(triggerHash);
        animator.SetTrigger(triggerHash);
        return true;
    }

    private bool IsFirearmAttackStateActive(NPCWeaponController.WeaponCategory category)
    {
        return IsAnimatorInAnyConfiguredStateForFirearmCategory(firearmAnimatorStates.firearmAttackStateNames, category);
    }

    private bool IsMeleeAttackStateActive(NPCWeaponController.WeaponCategory category)
    {
        return TryGetMeleeAttackStateNames(category, out List<string> stateNames) &&
               IsAnimatorInAnyConfiguredState(stateNames);
    }

    private bool TryGetMeleeAttackStateNames(
        NPCWeaponController.WeaponCategory category,
        out List<string> stateNames)
    {
        stateNames = null;
        if (meleeAnimatorStates == null)
            return false;

        switch (category)
        {
            case NPCWeaponController.WeaponCategory.Unarmed:
                stateNames = meleeAnimatorStates.unarmedAttackStateNames;
                return true;
            case NPCWeaponController.WeaponCategory.Knife:
                stateNames = meleeAnimatorStates.knifeAttackStateNames;
                return true;
            case NPCWeaponController.WeaponCategory.TwoHanded:
                stateNames = meleeAnimatorStates.twoHandedAttackStateNames;
                return true;
            default:
                return false;
        }
    }

    private bool IsReloadStateActive()
    {
        if (!animator)
            return false;

        const int BaseLayer = 0;
        if (BaseLayer < 0 || BaseLayer >= animator.layerCount)
            return false;

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(BaseLayer);
        if (IsBuiltInReloadState(currentState) ||
            MatchesAnyAnimatorStateByName(currentState, firearmAnimatorStates.reloadStateNames))
        {
            return true;
        }

        if (!animator.IsInTransition(BaseLayer))
            return false;

        AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(BaseLayer);
        return IsBuiltInReloadState(nextState) ||
               MatchesAnyAnimatorStateByName(nextState, firearmAnimatorStates.reloadStateNames);
    }

    private bool IsAnimatorInAnyConfiguredState(List<string> configuredStateNames)
    {
        if (!animator || configuredStateNames == null || configuredStateNames.Count == 0)
            return false;

        const int BaseLayer = 0;
        if (BaseLayer < 0 || BaseLayer >= animator.layerCount)
            return false;

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(BaseLayer);
        if (MatchesAnyAnimatorStateByName(currentState, configuredStateNames))
            return true;

        if (!animator.IsInTransition(BaseLayer))
            return false;

        AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(BaseLayer);
        return MatchesAnyAnimatorStateByName(nextState, configuredStateNames);
    }

    private bool IsAnimatorInAnyConfiguredStateForFirearmCategory(
        List<string> configuredStateNames,
        NPCWeaponController.WeaponCategory category)
    {
        if (!animator || configuredStateNames == null || configuredStateNames.Count == 0)
            return false;

        const int BaseLayer = 0;
        if (BaseLayer < 0 || BaseLayer >= animator.layerCount)
            return false;

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(BaseLayer);
        if (MatchesAnyAnimatorStateByNameForFirearmCategory(currentState, configuredStateNames, category))
            return true;

        if (!animator.IsInTransition(BaseLayer))
            return false;

        AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(BaseLayer);
        return MatchesAnyAnimatorStateByNameForFirearmCategory(nextState, configuredStateNames, category);
    }

    private static bool MatchesAnyAnimatorStateByName(AnimatorStateInfo stateInfo, List<string> configuredStateNames)
    {
        if (configuredStateNames == null || configuredStateNames.Count == 0)
            return false;

        for (int i = 0; i < configuredStateNames.Count; i++)
        {
            string stateName = configuredStateNames[i];
            if (string.IsNullOrWhiteSpace(stateName)) continue;

            int hash = Animator.StringToHash(stateName.Trim());
            if (stateInfo.shortNameHash == hash || stateInfo.fullPathHash == hash)
                return true;
        }

        return false;
    }

    private static bool MatchesAnyAnimatorStateByNameForFirearmCategory(
        AnimatorStateInfo stateInfo,
        List<string> configuredStateNames,
        NPCWeaponController.WeaponCategory category)
    {
        if (configuredStateNames == null || configuredStateNames.Count == 0)
            return false;

        for (int i = 0; i < configuredStateNames.Count; i++)
        {
            string stateName = configuredStateNames[i];
            if (string.IsNullOrWhiteSpace(stateName)) continue;
            if (!IsFirearmStateNameAllowedForCategory(stateName, category)) continue;

            int hash = Animator.StringToHash(stateName.Trim());
            if (stateInfo.shortNameHash == hash || stateInfo.fullPathHash == hash)
                return true;
        }

        return false;
    }

    private static bool IsFirearmStateNameAllowedForCategory(
        string stateName,
        NPCWeaponController.WeaponCategory category)
    {
        string trimmedStateName = stateName.Trim();
        if (category == NPCWeaponController.WeaponCategory.Pistol)
            return trimmedStateName.StartsWith("Pistol ", StringComparison.Ordinal);

        return IsLongarmCategory(category) &&
               trimmedStateName.StartsWith("Longarm ", StringComparison.Ordinal);
    }

    private static bool IsBuiltInReloadState(AnimatorStateInfo stateInfo)
    {
        int shortHash = stateInfo.shortNameHash;
        return shortHash == PistolReloadState ||
               shortHash == LongarmReloadState ||
               shortHash == PistolCrouchReloadState ||
               shortHash == LongarmCrouchReloadState ||
               stateInfo.fullPathHash == PistolReloadState ||
               stateInfo.fullPathHash == LongarmReloadState ||
               stateInfo.fullPathHash == PistolCrouchReloadState ||
               stateInfo.fullPathHash == LongarmCrouchReloadState;
    }

    public void AddKillhouseSetupNotes(List<string> lines)
    {
        if (!showKillhouseNotes)
            return;
        if (!killhouseActive && !killhouseOnPlay)
            return;

        if (!movement)
            lines.Add("Killhouse note: add NPCMovement.");

        if (movement && !movement.HasNavMeshAgentComponent)
            lines.Add("Killhouse note: assign a NavMeshAgent in NPCMovement.");

        if (movement && !movement.HasNavMeshSurfaceComponent)
            lines.Add("Killhouse note: assign or enable a NavMeshSurface from AI Navigation 2.0.");

        if (movement && movement.NavigationSurface && !movement.NavigationSurface.navMeshData)
            lines.Add("Killhouse note: build the assigned NavMeshSurface.");

        if (!weaponController)
            lines.Add("Killhouse note: add NPCWeaponController.");

        if (!aim)
            lines.Add("Killhouse note: add NPCAim for aimed shots.");

        if (!playerTarget && !playerState)
            lines.Add("Killhouse note: assign Player Target or enable Auto Find Player.");

        if (resetKillhouseOnResult && !playerKillhouseExit)
            lines.Add("Killhouse note: assign Player Killhouse Exit for result resets.");

        if (Application.isPlaying && !NavMesh.SamplePosition(transform.position, out _, 2f, NavMesh.AllAreas))
            lines.Add("Killhouse note: bake a NavMeshSurface under the maze/NPC.");

        if (Application.isPlaying && killhouseStartPending)
        {
            AreKillhouseParticipantsOnNavMesh(out bool npcOnNavMesh, out bool playerOnNavMesh);
            if (!npcOnNavMesh)
                lines.Add("Killhouse note: waiting for NPC to reach the NavMeshSurface.");
            if (!playerOnNavMesh)
                lines.Add("Killhouse note: waiting for player to reach the NavMeshSurface.");
        }

        if (useSpecificKillhouseInventoryWeapon)
            AddSelectedKillhouseWeaponNotes(lines);

        if (weaponController && preferFirearms && !useSpecificKillhouseInventoryWeapon && !IsCurrentWeaponFirearm())
            lines.Add("Killhouse note: NPC is cycling to find a firearm.");

        if (weaponController &&
            IsCurrentWeaponFirearm() &&
            weaponController.GetCurrentWeaponAmmo() <= 0 &&
            weaponController.GetCurrentWeaponReserveAmmo() <= 0)
        {
            lines.Add("Killhouse note: add reserve ammo for current firearm.");
        }
    }

    private void AddSelectedKillhouseWeaponNotes(List<string> lines)
    {
        if (string.IsNullOrWhiteSpace(selectedKillhouseInventoryWeaponInstanceId))
        {
            lines.Add("Killhouse note: choose a specific inventory weapon.");
            return;
        }

        if (IsSelectedKillhouseUnarmedWeapon())
            return;

        if (!TryResolveSelectedKillhouseInventoryWeapon(
                out WeaponDefinition weaponDefinition,
                out _,
                out _,
                out int loadedRounds,
                out int reserveRounds,
                out _))
        {
            lines.Add("Killhouse note: selected inventory weapon was not found.");
            return;
        }

        if (IsFirearmDefinition(weaponDefinition) && loadedRounds <= 0 && reserveRounds <= 0)
            lines.Add("Killhouse note: selected firearm has no loaded or reserve ammo.");
    }

    public string GetKillhouseStatusText()
    {
        if (killhouseActive)
            return killhouseTactic.ToString();

        return killhouseStartPending ? "Waiting for NavMesh" : "Off";
    }

    public void DrawKillhouseResultBanner()
    {
        if (!showKillhouseResultBanner ||
            string.IsNullOrWhiteSpace(killhouseResultBannerText) ||
            Time.time >= killhouseResultBannerUntil)
        {
            return;
        }

        GUIStyle bannerStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold,
            fontSize = Mathf.RoundToInt(Mathf.Clamp(Screen.height * 0.045f, 24f, 48f))
        };
        bannerStyle.normal.textColor = Color.white;

        float width = Mathf.Max(220f, Mathf.Min(560f, Screen.width - 48f));
        float height = Mathf.Max(64f, bannerStyle.fontSize + 32f);
        Rect bannerRect = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.16f, width, height);
        GUI.Box(bannerRect, killhouseResultBannerText, bannerStyle);
    }



    private bool TryAttackSafe()
    {
        if (IsAnimatorBusy())
            return false;

        return TryAttack();
    }

    private bool TryReloadSafe()
    {
        if (IsAnimatorBusy())
            return false;

        return TryReload();
    }

    private bool TryEquipNextSafe()
    {
        if (!weaponController || IsAnimatorBusy())
            return false;

        weaponController.EquipNext();
        return true;
    }

    private bool ShouldCurrentWeaponUseHand()
    {
        return weaponController &&
               weaponController.GetCurrentCategory() != NPCWeaponController.WeaponCategory.Unarmed;
    }

    private bool IsAnimatorBusy()
    {
        if (!animator)
            return false;

        if (animatorLayer < 0 || animatorLayer >= animator.layerCount)
            return false;

        if (animator.IsInTransition(animatorLayer))
            return true;

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(animatorLayer);
        return IsBusyAnimatorState(currentState);
    }

    private bool IsBusyAnimatorState(AnimatorStateInfo stateInfo)
    {
        if (busyAnimatorStateHashes == null || busyAnimatorStateHashes.Length == 0)
            return false;

        for (int i = 0; i < busyAnimatorStateHashes.Length; i++)
        {
            int hash = busyAnimatorStateHashes[i];
            if (hash == 0)
                continue;

            if (stateInfo.shortNameHash == hash || stateInfo.fullPathHash == hash)
                return true;
        }

        return false;
    }

    private void CacheBusyAnimatorStateHashes()
    {
        if (busyAnimatorStateNames == null || busyAnimatorStateNames.Length == 0)
        {
            busyAnimatorStateHashes = null;
            return;
        }

        busyAnimatorStateHashes = new int[busyAnimatorStateNames.Length];
        for (int i = 0; i < busyAnimatorStateNames.Length; i++)
        {
            string stateName = busyAnimatorStateNames[i];
            busyAnimatorStateHashes[i] = string.IsNullOrWhiteSpace(stateName)
                ? 0
                : Animator.StringToHash(stateName.Trim());
        }
    }

    private void ResolveReferences()
    {
        if (!movement)
            movement = GetComponent<NPCMovement>();

        if (!movement)
            movement = GetComponentInParent<NPCMovement>();

        if (!npcState)
            npcState = GetComponent<NPCState>();

        if (!npcState)
            npcState = GetComponentInParent<NPCState>();

        if (!npcInventory)
            npcInventory = GetComponent<NPCInventory>();

        if (!npcInventory)
            npcInventory = GetComponentInParent<NPCInventory>();

        if (!weaponController)
            weaponController = GetComponentInChildren<NPCWeaponController>(true);

        if (!weaponController)
            weaponController = GetComponentInParent<NPCWeaponController>();

        if (!aim)
            aim = GetComponent<NPCAim>();

        if (!aim)
            aim = GetComponentInChildren<NPCAim>(true);

        if (!aim)
            aim = GetComponentInParent<NPCAim>();

        if (!animator)
            animator = GetComponentInChildren<Animator>(true);

        if (!animator)
            animator = GetComponentInParent<Animator>();

        if (!playerState && playerTarget)
            playerState = playerTarget.GetComponentInParent<PlayerState>();

        if (!playerState && autoFindPlayer)
            TryAcquireLivePlayerTarget();

        if (!playerTarget && playerState)
            playerTarget = playerState.transform;

        ResolveGunPointProvider();

        if (!weaponHolder && weaponController)
            weaponHolder = weaponController.transform.Find(WeaponHolderName);

        if (!meleeOrigin)
            meleeOrigin = transform;
    }

    private void EnsureGroups()
    {
        if (firearmAnimatorStates == null)
            firearmAnimatorStates = new FirearmAnimatorStates();

        if (meleeAnimatorStates == null)
            meleeAnimatorStates = new MeleeAnimatorStates();

        if (shotgunPellets == null)
            shotgunPellets = new ShotgunPellets();
    }

    private string GetCurrentWeaponName()
    {
        if (!weaponController)
            return string.Empty;

        NPCWeaponController.WeaponEntry weapon = weaponController.GetCurrentWeapon();
        return weapon != null ? weapon.WeaponName ?? string.Empty : string.Empty;
    }

    private bool IsDoubleBarrelShotgunEquipped()
    {
        if (!weaponController || weaponController.GetCurrentCategory() != NPCWeaponController.WeaponCategory.Shotgun)
            return false;

        return string.Equals(GetCurrentWeaponName(), DoubleBarrelShotgunWeaponName, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsShotgunPelletSimulationActive()
    {
        return shotgunPellets != null
               && shotgunPellets.enabled
               && weaponController
               && weaponController.GetCurrentCategory() == NPCWeaponController.WeaponCategory.Shotgun;
    }

    private static bool IsSupportedFirearmCategory(NPCWeaponController.WeaponCategory category)
    {
        return category == NPCWeaponController.WeaponCategory.Pistol
            || category == NPCWeaponController.WeaponCategory.SubmachineGun
            || category == NPCWeaponController.WeaponCategory.Rifle
            || category == NPCWeaponController.WeaponCategory.Shotgun;
    }

    private static bool IsLongarmCategory(NPCWeaponController.WeaponCategory category)
    {
        return category == NPCWeaponController.WeaponCategory.SubmachineGun
            || category == NPCWeaponController.WeaponCategory.Rifle
            || category == NPCWeaponController.WeaponCategory.Shotgun;
    }

    private static void IgnorePelletCollisions(GameObject spawnedPellet, List<Collider> spawnedPelletColliders)
    {
        if (!spawnedPellet || spawnedPelletColliders == null)
            return;

        Collider[] pelletColliders = spawnedPellet.GetComponentsInChildren<Collider>(false);
        for (int i = 0; i < pelletColliders.Length; i++)
        {
            Collider pelletCollider = pelletColliders[i];
            if (!pelletCollider || !pelletCollider.enabled)
                continue;

            for (int j = 0; j < spawnedPelletColliders.Count; j++)
            {
                Collider existingCollider = spawnedPelletColliders[j];
                if (existingCollider)
                    Physics.IgnoreCollision(pelletCollider, existingCollider, true);
            }

            spawnedPelletColliders.Add(pelletCollider);
        }
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

    private static Vector3 ApplyAutomaticWeaponSpread(WeaponDefinition weaponDefinition, Vector3 shotDirection)
    {
        if (weaponDefinition == null || !weaponDefinition.IsAutomatic())
            return shotDirection;

        float spreadDegrees = weaponDefinition.GetSpread();
        if (spreadDegrees <= 0f)
            return shotDirection;

        float jitteredSpreadDegrees = spreadDegrees * UnityEngine.Random.Range(
            1f - AutomaticWeaponSpreadJitterFraction,
            1f + AutomaticWeaponSpreadJitterFraction);

        return GetDirectionInsideSpreadCone(shotDirection, jitteredSpreadDegrees);
    }

    private static string BuildEquippedWeaponCacheKey(NPCWeaponController.WeaponEntry weapon)
    {
        if (weapon == null)
            return string.Empty;

        return ((int)weapon.Category) + "|" + (weapon.WeaponName ?? string.Empty);
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

    private static bool DoesWeaponNameMatch(WeaponDefinition weaponDefinition, string equippedWeaponName)
    {
        if (!weaponDefinition || string.IsNullOrWhiteSpace(equippedWeaponName))
            return false;

        if (string.Equals(weaponDefinition.GetItemId(), equippedWeaponName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(weaponDefinition.GetDisplayName(), equippedWeaponName, StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(weaponDefinition.name, equippedWeaponName, StringComparison.OrdinalIgnoreCase))
            return true;

        string normalizedEquipped = NormalizeName(equippedWeaponName);
        if (string.IsNullOrWhiteSpace(normalizedEquipped))
            return false;

        return string.Equals(NormalizeName(weaponDefinition.GetDisplayName()), normalizedEquipped, StringComparison.Ordinal)
            || string.Equals(NormalizeName(weaponDefinition.name), normalizedEquipped, StringComparison.Ordinal)
            || string.Equals(NormalizeName(weaponDefinition.GetItemId()), normalizedEquipped, StringComparison.Ordinal);
    }

    private static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] chars = new char[value.Length];
        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char current = char.ToLowerInvariant(value[i]);
            if (!char.IsLetterOrDigit(current)) continue;
            chars[count] = current;
            count++;
        }

        return new string(chars, 0, count);
    }
}
