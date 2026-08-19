using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class NPCTestDriver : MonoBehaviour
{
    private const int BaseAnimatorLayer = 0;
    private static readonly int CombatModeParam = Animator.StringToHash("CombatMode");
    private static readonly int WeaponInHandParam = Animator.StringToHash("WeaponInHand");
    private static readonly int WalkState = Animator.StringToHash("Walk");
    private static readonly int RunState = Animator.StringToHash("Run");
    private static readonly int WalkStateFullPath = Animator.StringToHash("Base Layer.Walk");
    private static readonly int RunStateFullPath = Animator.StringToHash("Base Layer.Run");
    private static readonly List<NPCTestDriver> ActiveKillhousePatrolDrivers = new List<NPCTestDriver>();

    [Serializable]
    private class ReferencesSettings
    {
        public NPCMovement movement;
        public NPCState state;
        public NPCWeaponController weaponController;
        public NPCCombat combat;
        public NPCAim aim;
        public PlayerState playerState;
        public Animator animator;
    }

    [Serializable]
    private class RuntimeControlsSettings
    {
        public bool enableKeyboardControls = true;
        public bool enableRuntimeDebugOverlay = false;
        public bool showOnScreenHelp = true;
    }

    [Serializable]
    private class KillhouseParticipantsSettings
    {
        public bool killhouseOnPlay = false;
        public bool showKillhouseNotes = true;
        public Transform playerTarget;
        public Transform playerAimTarget;
        public Transform playerKillhouseExit;
        public bool resetKillhouseOnResult = true;
        public bool autoFindPlayer = true;
        public bool usePlayerAimTarget = false;
        public bool huntPlayerWithoutSight = true;
    }

    [Serializable]
    private class KillhouseLoadoutSettings
    {
        public bool preferFirearms = true;
        public bool useSpecificKillhouseInventoryWeapon = false;
        [HideInInspector] public string selectedKillhouseInventoryWeaponInstanceId = string.Empty;
        public bool crouchWhenShooting = false;
        public bool useCoverWhenDamaged = true;
        public bool jumpWhenStuck = true;
    }

    [Serializable]
    private class KillhousePerceptionSettings
    {
        public LayerMask lineOfSightLayers = ~0;
        public LayerMask coverProbeLayers = ~0;
        [Min(0.5f)] public float sightRange = 35f;
        [Range(1f, 179f)] public float sightConeAngleDegrees = 100f;
        [Min(0f)] public float targetAcquisitionSeconds = 0.2f;
    }

    [Serializable]
    private class KillhouseFriendlyFireSettings
    {
        public bool avoidSameFactionFriendlyFire = true;
        public LayerMask friendlyFireAvoidanceLayers = ~0;
        [Min(0f)] public float friendlyFireAvoidanceRadius = 0.3f;
    }

    [Serializable]
    private class KillhouseRangeSettings
    {
        [Min(0.1f)] public float meleeEngageRange = 1.65f;
        [Min(0.1f)] public float closeCombatDrawRange = 3.25f;
        [Min(0f)] public float closeCombatUndrawDelay = 1.25f;
        [Min(1f)] public float preferredFirearmRange = 9f;
        [Min(1f)] public float maxFirearmRange = 24f;
        [Min(0.25f)] public float closeRange = 4f;
    }

    [Serializable]
    private class KillhouseTimingSettings
    {
        [Min(0.1f)] public float killhouseReachDistance = 0.9f;
        [Min(0.05f)] public float killhouseDecisionInterval = 0.18f;
        [Min(0.05f)] public float killhouseRepathInterval = 0.35f;
        [Min(0.05f)] public float killhouseActionInterval = 0.35f;
        [Min(0f)] public float reloadResumeDelay = 0.75f;
    }

    [Serializable]
    private class KillhouseCoverSettings
    {
        [Min(0.5f)] public float coverSearchRadius = 8f;
        [Min(4)] public int coverSearchSamples = 18;
        [Min(0.5f)] public float coverMinPlayerDistance = 5f;
        [Range(0.05f, 1f)] public float retreatHealthPercent = 0.35f;
        [Min(0.1f)] public float damageReactionSeconds = 3f;
        [Min(0f)] public float damageHealthDelta = 0.5f;
        [Range(0.01f, 1f)] public float coverDamageHealthPercent = 0.18f;
        [Min(0.1f)] public float coverDamageReactionSeconds = 4f;
        [Min(0.1f)] public float coverDestinationHoldSeconds = 1.25f;
        [Min(0.1f)] public float coverRetryDelay = 0.75f;
    }

    [Serializable]
    private class KillhouseMovementRecoverySettings
    {
        [Min(0.2f)] public float stuckCheckInterval = 0.75f;
        [Min(0.01f)] public float stuckDistance = 0.15f;
        [Min(0.2f)] public float stuckJumpInterval = 1.2f;
        [Min(1)] public int stuckChecksBeforeJump = 3;
    }

    [Serializable]
    private class KillhouseSearchSettings
    {
        [Min(1.5f)] public float searchAreaRadius = 7f;
        [Min(6)] public int searchAreaSamples = 16;
        [Range(0f, 1f)] public float broadSearchChance = 0.35f;
        [Min(0.5f)] public float searchMinPointSpacing = 3f;
        [Min(0.3f)] public float searchPointReachDistance = 1f;
        [Min(0.1f)] public float lostTargetEngageSeconds = 2.5f;
        [Min(0.1f)] public float lostTargetCombatEngageSeconds = 6f;
        [Min(0.5f)] public float lostTargetSearchSeconds = 8f;
    }

    [Serializable]
    private class KillhouseAimSettings
    {
        [Min(1f)] public float aimLookAheadDistance = 12f;
        [Min(0.1f)] public float aimFollowSpeed = 9f;
        [Range(1f, 45f)] public float firearmShootAimToleranceDegrees = 10f;
        [Min(0.02f)] public float aimNotReadyRetryDelay = 0.08f;
        [Range(0f, 120f)] public float searchAimSweepAngle = 65f;
        [Min(0.1f)] public float searchAimSweepSpeed = 1.25f;
    }

    [Serializable]
    private class KillhouseResultSettings
    {
        [Min(0.1f)] public float killhouseResultBannerSeconds = 3f;
        public bool showKillhouseResultBanner = true;
        public bool alsoShowKillhouseResultOnHud = true;
    }

    [Serializable]
    private class CombatFacingSettings
    {
        [Min(0f)] public float visibleTargetTurnSpeed = 1080f;
        [Min(0f)] public float visibleTargetSnapAngle = 0.5f;
        [Min(0f)] public float lastKnownFacingHoldSeconds = 2.5f;
    }

    [Serializable]
    private class PatrolSettings
    {
        public bool patrolOnPlay = false;
        public bool patrolRuns = true;
        [Min(0.05f)] public float waypointReachDistance = 0.8f;
        public Transform[] patrolWaypoints;
        public Vector3[] fallbackLocalPatrolOffsets =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, 5f),
            new Vector3(5f, 0f, 5f),
            new Vector3(5f, 0f, 0f)
        };
    }

    [Serializable]
    private class KillhousePatrolSettings
    {
        public bool killhousePatrolOnPlay = false;
        public bool usePatrolWaypointsForKillhousePatrol = false;
        public Transform killhousePatrolCenter;
        [Min(2f)] public float killhousePatrolRadius = 12f;
        [Min(1f)] public float killhousePatrolMinPointSpacing = 4f;
        [Min(4)] public int killhousePatrolNavMeshSamples = 16;
        [Min(0.05f)] public float killhousePatrolSightInterval = 0.15f;
        [Min(0.1f)] public float killhousePatrolRepathInterval = 0.75f;
        public bool avoidOtherKillhousePatrolAreas = true;
        [Min(0f)] public float killhousePatrolSeparation = 6f;
        [Min(0f)] public float killhousePatrolSeparationScoreWeight = 0.75f;
    }

    [Serializable]
    private class AnimationSweepSettings
    {
        public bool sweepOnPlay = false;
        public bool loopSweep = false;
        [Min(0.1f)] public float idleSeconds = 1.5f;
        [Min(0.1f)] public float moveSeconds = 2.5f;
        [Min(0.1f)] public float combatMoveSeconds = 2.5f;
        [Min(0.1f)] public float weaponStepSeconds = 1.25f;
        public bool sweepAllWeapons = true;
        [Min(1)] public int weaponStepsPerSweep = 12;
    }

    [Serializable]
    private class AnimationGatingSettings
    {
        [Min(0f)] public float weaponEquipSettleSeconds = 0.35f;
        [Min(0f)] public float weaponEquipActionLockSeconds = 1.25f;
        [Min(0f)] public float weaponUnequipActionLockSeconds = 1.25f;
        [Min(0f)] public float reloadActionLockSeconds = 2.5f;
        [Min(0.01f)] public float busyPollSeconds = 0.1f;
        public int animatorLayer = 0;
        public string[] busyAnimatorStateNames =
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
    }

    [Header("Core")]
    [SerializeField] private ReferencesSettings references = new ReferencesSettings();
    [SerializeField] private RuntimeControlsSettings runtimeControls = new RuntimeControlsSettings();

    [Header("Killhouse Combat")]
    [SerializeField] private KillhouseParticipantsSettings killhouseParticipants = new KillhouseParticipantsSettings();
    [SerializeField] private KillhouseLoadoutSettings killhouseLoadout = new KillhouseLoadoutSettings();
    [SerializeField] private KillhousePerceptionSettings killhousePerception = new KillhousePerceptionSettings();
    [SerializeField] private KillhouseFriendlyFireSettings killhouseFriendlyFire = new KillhouseFriendlyFireSettings();
    [SerializeField] private KillhouseRangeSettings killhouseRanges = new KillhouseRangeSettings();
    [SerializeField] private KillhouseTimingSettings killhouseTiming = new KillhouseTimingSettings();
    [SerializeField] private KillhouseCoverSettings killhouseCover = new KillhouseCoverSettings();
    [SerializeField] private KillhouseMovementRecoverySettings killhouseMovementRecovery = new KillhouseMovementRecoverySettings();
    [SerializeField] private KillhouseSearchSettings killhouseSearch = new KillhouseSearchSettings();
    [SerializeField] private KillhouseAimSettings killhouseAim = new KillhouseAimSettings();
    [SerializeField] private KillhouseResultSettings killhouseResult = new KillhouseResultSettings();
    [SerializeField] private CombatFacingSettings combatFacing = new CombatFacingSettings();

    [Header("Patrol")]
    [SerializeField] private PatrolSettings patrol = new PatrolSettings();
    [SerializeField] private KillhousePatrolSettings killhousePatrol = new KillhousePatrolSettings();

    [Header("Animation Tests")]
    [SerializeField] private AnimationSweepSettings animationSweep = new AnimationSweepSettings();
    [SerializeField] private AnimationGatingSettings animationGating = new AnimationGatingSettings();
    [SerializeField, HideInInspector] private bool inspectorGroupsMigrated = false;

    [SerializeField, HideInInspector, FormerlySerializedAs("movement")] private NPCMovement legacyMovement;
    [SerializeField, HideInInspector, FormerlySerializedAs("state")] private NPCState legacyState;
    [SerializeField, HideInInspector, FormerlySerializedAs("weaponController")] private NPCWeaponController legacyWeaponController;
    [SerializeField, HideInInspector, FormerlySerializedAs("combat")] private NPCCombat legacyCombat;
    [SerializeField, HideInInspector, FormerlySerializedAs("aim")] private NPCAim legacyAim;
    [SerializeField, HideInInspector, FormerlySerializedAs("playerState")] private PlayerState legacyPlayerState;
    [SerializeField, HideInInspector, FormerlySerializedAs("animator")] private Animator legacyAnimator;
    [SerializeField, HideInInspector, FormerlySerializedAs("enableKeyboardControls")] private bool legacyEnableKeyboardControls = true;
    [SerializeField, HideInInspector, FormerlySerializedAs("enableRuntimeDebugOverlay")] private bool legacyEnableRuntimeDebugOverlay = false;
    [SerializeField, HideInInspector, FormerlySerializedAs("showOnScreenHelp")] private bool legacyShowOnScreenHelp = true;
    [SerializeField, HideInInspector, FormerlySerializedAs("killhouseOnPlay")] private bool legacyKillhouseOnPlay = false;
    [SerializeField, HideInInspector, FormerlySerializedAs("showKillhouseNotes")] private bool legacyShowKillhouseNotes = true;
    [SerializeField, HideInInspector, FormerlySerializedAs("playerTarget")] private Transform legacyPlayerTarget;
    [SerializeField, HideInInspector, FormerlySerializedAs("playerAimTarget")] private Transform legacyPlayerAimTarget;
    [SerializeField, HideInInspector, FormerlySerializedAs("playerKillhouseExit")] private Transform legacyPlayerKillhouseExit;
    [SerializeField, HideInInspector, FormerlySerializedAs("resetKillhouseOnResult")] private bool legacyResetKillhouseOnResult = true;
    [SerializeField, HideInInspector, FormerlySerializedAs("autoFindPlayer")] private bool legacyAutoFindPlayer = true;
    [SerializeField, HideInInspector, FormerlySerializedAs("usePlayerAimTarget")] private bool legacyUsePlayerAimTarget = false;
    [SerializeField, HideInInspector, FormerlySerializedAs("huntPlayerWithoutSight")] private bool legacyHuntPlayerWithoutSight = true;
    [SerializeField, HideInInspector, FormerlySerializedAs("preferFirearms")] private bool legacyPreferFirearms = true;
    [SerializeField, HideInInspector, FormerlySerializedAs("useSpecificKillhouseInventoryWeapon")] private bool legacyUseSpecificKillhouseInventoryWeapon = false;
    [SerializeField, HideInInspector, FormerlySerializedAs("selectedKillhouseInventoryWeaponInstanceId")] private string legacySelectedKillhouseInventoryWeaponInstanceId = string.Empty;
    [SerializeField, HideInInspector, FormerlySerializedAs("crouchWhenShooting")] private bool legacyCrouchWhenShooting = false;
    [SerializeField, HideInInspector, FormerlySerializedAs("useCoverWhenDamaged")] private bool legacyUseCoverWhenDamaged = true;
    [SerializeField, HideInInspector, FormerlySerializedAs("jumpWhenStuck")] private bool legacyJumpWhenStuck = true;
    [SerializeField, HideInInspector, FormerlySerializedAs("lineOfSightLayers")] private LayerMask legacyLineOfSightLayers = ~0;
    [SerializeField, HideInInspector, FormerlySerializedAs("coverProbeLayers")] private LayerMask legacyCoverProbeLayers = ~0;
    [SerializeField, HideInInspector, FormerlySerializedAs("sightRange")] private float legacySightRange = 35f;
    [SerializeField, HideInInspector, FormerlySerializedAs("meleeEngageRange")] private float legacyMeleeEngageRange = 1.65f;
    [SerializeField, HideInInspector, FormerlySerializedAs("closeCombatDrawRange")] private float legacyCloseCombatDrawRange = 3.25f;
    [SerializeField, HideInInspector, FormerlySerializedAs("closeCombatUndrawDelay")] private float legacyCloseCombatUndrawDelay = 1.25f;
    [SerializeField, HideInInspector, FormerlySerializedAs("preferredFirearmRange")] private float legacyPreferredFirearmRange = 9f;
    [SerializeField, HideInInspector, FormerlySerializedAs("maxFirearmRange")] private float legacyMaxFirearmRange = 24f;
    [SerializeField, HideInInspector, FormerlySerializedAs("closeRange")] private float legacyCloseRange = 4f;
    [SerializeField, HideInInspector, FormerlySerializedAs("killhouseReachDistance")] private float legacyKillhouseReachDistance = 0.9f;
    [SerializeField, HideInInspector, FormerlySerializedAs("killhouseDecisionInterval")] private float legacyKillhouseDecisionInterval = 0.18f;
    [SerializeField, HideInInspector, FormerlySerializedAs("killhouseRepathInterval")] private float legacyKillhouseRepathInterval = 0.35f;
    [SerializeField, HideInInspector, FormerlySerializedAs("killhouseActionInterval")] private float legacyKillhouseActionInterval = 0.35f;
    [SerializeField, HideInInspector, FormerlySerializedAs("reloadResumeDelay")] private float legacyReloadResumeDelay = 0.75f;
    [SerializeField, HideInInspector, FormerlySerializedAs("coverSearchRadius")] private float legacyCoverSearchRadius = 8f;
    [SerializeField, HideInInspector, FormerlySerializedAs("coverSearchSamples")] private int legacyCoverSearchSamples = 18;
    [SerializeField, HideInInspector, FormerlySerializedAs("coverMinPlayerDistance")] private float legacyCoverMinPlayerDistance = 5f;
    [SerializeField, HideInInspector, FormerlySerializedAs("retreatHealthPercent")] private float legacyRetreatHealthPercent = 0.35f;
    [SerializeField, HideInInspector, FormerlySerializedAs("damageReactionSeconds")] private float legacyDamageReactionSeconds = 3f;
    [SerializeField, HideInInspector, FormerlySerializedAs("damageHealthDelta")] private float legacyDamageHealthDelta = 0.5f;
    [SerializeField, HideInInspector, FormerlySerializedAs("coverDamageHealthPercent")] private float legacyCoverDamageHealthPercent = 0.18f;
    [SerializeField, HideInInspector, FormerlySerializedAs("coverDamageReactionSeconds")] private float legacyCoverDamageReactionSeconds = 4f;
    [SerializeField, HideInInspector, FormerlySerializedAs("stuckCheckInterval")] private float legacyStuckCheckInterval = 0.75f;
    [SerializeField, HideInInspector, FormerlySerializedAs("stuckDistance")] private float legacyStuckDistance = 0.15f;
    [SerializeField, HideInInspector, FormerlySerializedAs("stuckJumpInterval")] private float legacyStuckJumpInterval = 1.2f;
    [SerializeField, HideInInspector, FormerlySerializedAs("stuckChecksBeforeJump")] private int legacyStuckChecksBeforeJump = 3;
    [SerializeField, HideInInspector, FormerlySerializedAs("searchAreaRadius")] private float legacySearchAreaRadius = 7f;
    [SerializeField, HideInInspector, FormerlySerializedAs("searchAreaSamples")] private int legacySearchAreaSamples = 16;
    [SerializeField, HideInInspector, FormerlySerializedAs("broadSearchChance")] private float legacyBroadSearchChance = 0.35f;
    [SerializeField, HideInInspector, FormerlySerializedAs("searchMinPointSpacing")] private float legacySearchMinPointSpacing = 3f;
    [SerializeField, HideInInspector, FormerlySerializedAs("searchPointReachDistance")] private float legacySearchPointReachDistance = 1f;
    [SerializeField, HideInInspector, FormerlySerializedAs("lostTargetSearchSeconds")] private float legacyLostTargetSearchSeconds = 8f;
    [SerializeField, HideInInspector, FormerlySerializedAs("coverDestinationHoldSeconds")] private float legacyCoverDestinationHoldSeconds = 1.25f;
    [SerializeField, HideInInspector, FormerlySerializedAs("coverRetryDelay")] private float legacyCoverRetryDelay = 0.75f;
    [SerializeField, HideInInspector, FormerlySerializedAs("aimLookAheadDistance")] private float legacyAimLookAheadDistance = 12f;
    [SerializeField, HideInInspector, FormerlySerializedAs("aimFollowSpeed")] private float legacyAimFollowSpeed = 9f;
    [SerializeField, HideInInspector, FormerlySerializedAs("firearmShootAimToleranceDegrees")] private float legacyFirearmShootAimToleranceDegrees = 10f;
    [SerializeField, HideInInspector, FormerlySerializedAs("aimNotReadyRetryDelay")] private float legacyAimNotReadyRetryDelay = 0.08f;
    [SerializeField, HideInInspector, FormerlySerializedAs("searchAimSweepAngle")] private float legacySearchAimSweepAngle = 65f;
    [SerializeField, HideInInspector, FormerlySerializedAs("searchAimSweepSpeed")] private float legacySearchAimSweepSpeed = 1.25f;
    [SerializeField, HideInInspector, FormerlySerializedAs("killhouseResultBannerSeconds")] private float legacyKillhouseResultBannerSeconds = 3f;
    [SerializeField, HideInInspector, FormerlySerializedAs("showKillhouseResultBanner")] private bool legacyShowKillhouseResultBanner = true;
    [SerializeField, HideInInspector, FormerlySerializedAs("alsoShowKillhouseResultOnHud")] private bool legacyAlsoShowKillhouseResultOnHud = true;
    [SerializeField, HideInInspector, FormerlySerializedAs("visibleTargetTurnSpeed")] private float legacyVisibleTargetTurnSpeed = 1080f;
    [SerializeField, HideInInspector, FormerlySerializedAs("visibleTargetSnapAngle")] private float legacyVisibleTargetSnapAngle = 0.5f;
    [SerializeField, HideInInspector, FormerlySerializedAs("lastKnownFacingHoldSeconds")] private float legacyLastKnownFacingHoldSeconds = 2.5f;
    [SerializeField, HideInInspector, FormerlySerializedAs("patrolOnPlay")] private bool legacyPatrolOnPlay = false;
    [SerializeField, HideInInspector, FormerlySerializedAs("patrolRuns")] private bool legacyPatrolRuns = true;
    [SerializeField, HideInInspector, FormerlySerializedAs("waypointReachDistance")] private float legacyWaypointReachDistance = 0.8f;
    [SerializeField, HideInInspector, FormerlySerializedAs("patrolWaypoints")] private Transform[] legacyPatrolWaypoints;
    [SerializeField, HideInInspector, FormerlySerializedAs("fallbackLocalPatrolOffsets")] private Vector3[] legacyFallbackLocalPatrolOffsets =
    {
        new Vector3(0f, 0f, 0f),
        new Vector3(0f, 0f, 5f),
        new Vector3(5f, 0f, 5f),
        new Vector3(5f, 0f, 0f)
    };
    [SerializeField, HideInInspector, FormerlySerializedAs("killhousePatrolOnPlay")] private bool legacyKillhousePatrolOnPlay = false;
    [SerializeField, HideInInspector, FormerlySerializedAs("usePatrolWaypointsForKillhousePatrol")] private bool legacyUsePatrolWaypointsForKillhousePatrol = false;
    [SerializeField, HideInInspector, FormerlySerializedAs("killhousePatrolCenter")] private Transform legacyKillhousePatrolCenter;
    [SerializeField, HideInInspector, FormerlySerializedAs("killhousePatrolRadius")] private float legacyKillhousePatrolRadius = 12f;
    [SerializeField, HideInInspector, FormerlySerializedAs("killhousePatrolMinPointSpacing")] private float legacyKillhousePatrolMinPointSpacing = 4f;
    [SerializeField, HideInInspector, FormerlySerializedAs("killhousePatrolNavMeshSamples")] private int legacyKillhousePatrolNavMeshSamples = 16;
    [SerializeField, HideInInspector, FormerlySerializedAs("killhousePatrolSightInterval")] private float legacyKillhousePatrolSightInterval = 0.15f;
    [SerializeField, HideInInspector, FormerlySerializedAs("killhousePatrolRepathInterval")] private float legacyKillhousePatrolRepathInterval = 0.75f;
    [SerializeField, HideInInspector, FormerlySerializedAs("sweepOnPlay")] private bool legacySweepOnPlay = false;
    [SerializeField, HideInInspector, FormerlySerializedAs("loopSweep")] private bool legacyLoopSweep = false;
    [SerializeField, HideInInspector, FormerlySerializedAs("idleSeconds")] private float legacyIdleSeconds = 1.5f;
    [SerializeField, HideInInspector, FormerlySerializedAs("moveSeconds")] private float legacyMoveSeconds = 2.5f;
    [SerializeField, HideInInspector, FormerlySerializedAs("combatMoveSeconds")] private float legacyCombatMoveSeconds = 2.5f;
    [SerializeField, HideInInspector, FormerlySerializedAs("weaponStepSeconds")] private float legacyWeaponStepSeconds = 1.25f;
    [SerializeField, HideInInspector, FormerlySerializedAs("sweepAllWeapons")] private bool legacySweepAllWeapons = true;
    [SerializeField, HideInInspector, FormerlySerializedAs("weaponStepsPerSweep")] private int legacyWeaponStepsPerSweep = 12;
    [SerializeField, HideInInspector, FormerlySerializedAs("weaponEquipSettleSeconds")] private float legacyWeaponEquipSettleSeconds = 0.35f;
    [SerializeField, HideInInspector, FormerlySerializedAs("weaponEquipActionLockSeconds")] private float legacyWeaponEquipActionLockSeconds = 1.25f;
    [SerializeField, HideInInspector, FormerlySerializedAs("weaponUnequipActionLockSeconds")] private float legacyWeaponUnequipActionLockSeconds = 1.25f;
    [SerializeField, HideInInspector, FormerlySerializedAs("reloadActionLockSeconds")] private float legacyReloadActionLockSeconds = 2.5f;
    [SerializeField, HideInInspector, FormerlySerializedAs("busyPollSeconds")] private float legacyBusyPollSeconds = 0.1f;
    [SerializeField, HideInInspector, FormerlySerializedAs("animatorLayer")] private int legacyAnimatorLayer = 0;
    [SerializeField, HideInInspector, FormerlySerializedAs("busyAnimatorStateNames")] private string[] legacyBusyAnimatorStateNames =
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

    private NPCMovement movement { get => references.movement; set => references.movement = value; }
    private NPCState state { get => references.state; set => references.state = value; }
    private NPCWeaponController weaponController { get => references.weaponController; set => references.weaponController = value; }
    private NPCCombat combat { get => references.combat; set => references.combat = value; }
    private NPCAim aim { get => references.aim; set => references.aim = value; }
    private PlayerState playerState { get => references.playerState; set => references.playerState = value; }
    private Animator animator { get => references.animator; set => references.animator = value; }
    private bool enableKeyboardControls => runtimeControls.enableKeyboardControls;
    private bool enableRuntimeDebugOverlay { get => runtimeControls.enableRuntimeDebugOverlay; set => runtimeControls.enableRuntimeDebugOverlay = value; }
    private bool showOnScreenHelp { get => runtimeControls.showOnScreenHelp; set => runtimeControls.showOnScreenHelp = value; }
    private bool killhouseOnPlay => killhouseParticipants.killhouseOnPlay;
    private bool showKillhouseNotes => killhouseParticipants.showKillhouseNotes;
    private Transform playerTarget => killhouseParticipants.playerTarget;
    private Transform playerAimTarget => killhouseParticipants.playerAimTarget;
    private Transform playerKillhouseExit => killhouseParticipants.playerKillhouseExit;
    private bool resetKillhouseOnResult => killhouseParticipants.resetKillhouseOnResult;
    private bool autoFindPlayer => killhouseParticipants.autoFindPlayer;
    private bool usePlayerAimTarget => killhouseParticipants.usePlayerAimTarget;
    private bool huntPlayerWithoutSight => killhouseParticipants.huntPlayerWithoutSight;
    private bool preferFirearms => killhouseLoadout.preferFirearms;
    private bool useSpecificKillhouseInventoryWeapon => killhouseLoadout.useSpecificKillhouseInventoryWeapon;
    private string selectedKillhouseInventoryWeaponInstanceId => killhouseLoadout.selectedKillhouseInventoryWeaponInstanceId;
    private bool crouchWhenShooting => killhouseLoadout.crouchWhenShooting;
    private bool useCoverWhenDamaged => killhouseLoadout.useCoverWhenDamaged;
    private bool jumpWhenStuck => killhouseLoadout.jumpWhenStuck;
    private LayerMask lineOfSightLayers => killhousePerception.lineOfSightLayers;
    private LayerMask coverProbeLayers => killhousePerception.coverProbeLayers;
    private bool avoidSameFactionFriendlyFire => killhouseFriendlyFire.avoidSameFactionFriendlyFire;
    private LayerMask friendlyFireAvoidanceLayers => killhouseFriendlyFire.friendlyFireAvoidanceLayers;
    private float friendlyFireAvoidanceRadius => killhouseFriendlyFire.friendlyFireAvoidanceRadius;
    private float sightRange => killhousePerception.sightRange;
    private float sightConeAngleDegrees => killhousePerception.sightConeAngleDegrees;
    private float targetAcquisitionSeconds => killhousePerception.targetAcquisitionSeconds;
    private float meleeEngageRange => killhouseRanges.meleeEngageRange;
    private float closeCombatDrawRange { get => killhouseRanges.closeCombatDrawRange; set => killhouseRanges.closeCombatDrawRange = value; }
    private float closeCombatUndrawDelay { get => killhouseRanges.closeCombatUndrawDelay; set => killhouseRanges.closeCombatUndrawDelay = value; }
    private float preferredFirearmRange => killhouseRanges.preferredFirearmRange;
    private float maxFirearmRange => killhouseRanges.maxFirearmRange;
    private float closeRange => killhouseRanges.closeRange;
    private float killhouseReachDistance => killhouseTiming.killhouseReachDistance;
    private float killhouseDecisionInterval => killhouseTiming.killhouseDecisionInterval;
    private float killhouseRepathInterval => killhouseTiming.killhouseRepathInterval;
    private float killhouseActionInterval => killhouseTiming.killhouseActionInterval;
    private float reloadResumeDelay => killhouseTiming.reloadResumeDelay;
    private float coverSearchRadius => killhouseCover.coverSearchRadius;
    private int coverSearchSamples => killhouseCover.coverSearchSamples;
    private float coverMinPlayerDistance => killhouseCover.coverMinPlayerDistance;
    private float retreatHealthPercent => killhouseCover.retreatHealthPercent;
    private float damageReactionSeconds => killhouseCover.damageReactionSeconds;
    private float damageHealthDelta => killhouseCover.damageHealthDelta;
    private float coverDamageHealthPercent => killhouseCover.coverDamageHealthPercent;
    private float coverDamageReactionSeconds => killhouseCover.coverDamageReactionSeconds;
    private float stuckCheckInterval => killhouseMovementRecovery.stuckCheckInterval;
    private float stuckDistance => killhouseMovementRecovery.stuckDistance;
    private float stuckJumpInterval => killhouseMovementRecovery.stuckJumpInterval;
    private int stuckChecksBeforeJump => killhouseMovementRecovery.stuckChecksBeforeJump;
    private float searchAreaRadius => killhouseSearch.searchAreaRadius;
    private int searchAreaSamples => killhouseSearch.searchAreaSamples;
    private float broadSearchChance => killhouseSearch.broadSearchChance;
    private float searchMinPointSpacing => killhouseSearch.searchMinPointSpacing;
    private float searchPointReachDistance => killhouseSearch.searchPointReachDistance;
    private float lostTargetEngageSeconds => killhouseSearch.lostTargetEngageSeconds;
    private float lostTargetSearchSeconds => killhouseSearch.lostTargetSearchSeconds;
    private float coverDestinationHoldSeconds => killhouseCover.coverDestinationHoldSeconds;
    private float coverRetryDelay => killhouseCover.coverRetryDelay;
    private float aimLookAheadDistance => killhouseAim.aimLookAheadDistance;
    private float aimFollowSpeed => killhouseAim.aimFollowSpeed;
    private float firearmShootAimToleranceDegrees => killhouseAim.firearmShootAimToleranceDegrees;
    private float aimNotReadyRetryDelay => killhouseAim.aimNotReadyRetryDelay;
    private float searchAimSweepAngle => killhouseAim.searchAimSweepAngle;
    private float searchAimSweepSpeed => killhouseAim.searchAimSweepSpeed;
    private float killhouseResultBannerSeconds => killhouseResult.killhouseResultBannerSeconds;
    private bool showKillhouseResultBanner => killhouseResult.showKillhouseResultBanner;
    private bool alsoShowKillhouseResultOnHud => killhouseResult.alsoShowKillhouseResultOnHud;
    private float visibleTargetTurnSpeed { get => combatFacing.visibleTargetTurnSpeed; set => combatFacing.visibleTargetTurnSpeed = value; }
    private float visibleTargetSnapAngle { get => combatFacing.visibleTargetSnapAngle; set => combatFacing.visibleTargetSnapAngle = value; }
    private float lastKnownFacingHoldSeconds { get => combatFacing.lastKnownFacingHoldSeconds; set => combatFacing.lastKnownFacingHoldSeconds = value; }
    private bool patrolOnPlay => patrol.patrolOnPlay;
    private bool patrolRuns => patrol.patrolRuns;
    private float waypointReachDistance => patrol.waypointReachDistance;
    private Transform[] patrolWaypoints => patrol.patrolWaypoints;
    private Vector3[] fallbackLocalPatrolOffsets => patrol.fallbackLocalPatrolOffsets;
    private bool killhousePatrolOnPlay => killhousePatrol.killhousePatrolOnPlay;
    private bool usePatrolWaypointsForKillhousePatrol => killhousePatrol.usePatrolWaypointsForKillhousePatrol;
    private Transform killhousePatrolCenter => killhousePatrol.killhousePatrolCenter;
    private float killhousePatrolRadius => killhousePatrol.killhousePatrolRadius;
    private float killhousePatrolMinPointSpacing => killhousePatrol.killhousePatrolMinPointSpacing;
    private int killhousePatrolNavMeshSamples => killhousePatrol.killhousePatrolNavMeshSamples;
    private float killhousePatrolSightInterval => killhousePatrol.killhousePatrolSightInterval;
    private float killhousePatrolRepathInterval => killhousePatrol.killhousePatrolRepathInterval;
    private bool avoidOtherKillhousePatrolAreas => killhousePatrol.avoidOtherKillhousePatrolAreas;
    private float killhousePatrolSeparation => killhousePatrol.killhousePatrolSeparation;
    private float killhousePatrolSeparationScoreWeight => killhousePatrol.killhousePatrolSeparationScoreWeight;
    private bool sweepOnPlay => animationSweep.sweepOnPlay;
    private bool loopSweep => animationSweep.loopSweep;
    private float idleSeconds => animationSweep.idleSeconds;
    private float moveSeconds => animationSweep.moveSeconds;
    private float combatMoveSeconds => animationSweep.combatMoveSeconds;
    private float weaponStepSeconds => animationSweep.weaponStepSeconds;
    private bool sweepAllWeapons => animationSweep.sweepAllWeapons;
    private int weaponStepsPerSweep => animationSweep.weaponStepsPerSweep;
    private float weaponEquipSettleSeconds => animationGating.weaponEquipSettleSeconds;
    private float weaponEquipActionLockSeconds => animationGating.weaponEquipActionLockSeconds;
    private float weaponUnequipActionLockSeconds => animationGating.weaponUnequipActionLockSeconds;
    private float reloadActionLockSeconds => animationGating.reloadActionLockSeconds;
    private float busyPollSeconds => animationGating.busyPollSeconds;
    private int animatorLayer => animationGating.animatorLayer;
    private string[] busyAnimatorStateNames => animationGating.busyAnimatorStateNames;

    private const int SweepIdle = 0;
    private const int SweepWalk = 1;
    private const int SweepRun = 2;
    private const int SweepSprint = 3;
    private const int SweepJump = 4;
    private const int SweepCrouchWalk = 5;
    private const int SweepCombatIdle = 6;
    private const int SweepCombatWalk = 7;
    private const int SweepCombatRun = 8;
    private const int SweepWeapons = 9;
    private const int SweepDone = 10;
    private const int WeaponSweepBeginUnequip = 0;
    private const int WeaponSweepFinishUnequip = 1;
    private const int WeaponSweepSelect = 2;
    private const int WeaponSweepBeginEquip = 3;
    private const int WeaponSweepFinishEquip = 4;
    private const int WeaponSweepAttack = 5;
    private const int WeaponSweepReload = 6;
    private const int WeaponSweepCompleteStep = 7;
    private Vector3 startPosition;
    private int currentWaypointIndex;
    private bool patrolActive;
    private bool killhousePatrolActive;
    private int killhousePatrolWaypointIndex;
    private Vector3 killhousePatrolDestination;
    private bool hasKillhousePatrolDestination;
    private float nextKillhousePatrolSightTime;
    private float nextKillhousePatrolRepathTime;
    private bool sweepActive;
    private int sweepStep;
    private int weaponSweepStep;
    private int weaponSweepTargetSteps;
    private int weaponSweepPhase;
    private int[] busyAnimatorStateHashes;
    private float nextSweepStepTime;
    private float nextWeaponActionTime;
    private bool crouchToggle;
    private bool preparingFirstWeaponSweepStep;
    private bool storedWeaponInHandSync;
    private bool hasStoredWeaponInHandSync;

    private void Awake()
    {
        EnsureInspectorGroups();
        MigrateLegacyInspectorFields();
        ResolveReferences();
        SyncKillhouseCombatSettings();
        CacheBusyAnimatorStateHashes();
        startPosition = transform.position;
        patrolActive = patrolOnPlay;

        if (sweepOnPlay)
            StartSweep();
        else if (killhouseOnPlay && combat)
            combat.QueueKillhouseOnPlayStart();
        else if (killhousePatrolOnPlay)
            StartKillhousePatrol();
        else if (patrolActive)
            SetCurrentPatrolDestination();
    }

    private void Reset()
    {
        EnsureInspectorGroups();
        MigrateLegacyInspectorFields();
        ResolveReferences();
    }

    private void OnValidate()
    {
        EnsureInspectorGroups();
        MigrateLegacyInspectorFields();
        ClampKillhouseCombatSettings();
        SyncKillhouseCombatSettings();
        CacheBusyAnimatorStateHashes();
    }

    private void OnDisable()
    {
        UnregisterActiveKillhousePatrol();
    }

    private void EnsureInspectorGroups()
    {
        if (references == null) references = new ReferencesSettings();
        if (runtimeControls == null) runtimeControls = new RuntimeControlsSettings();
        if (killhouseParticipants == null) killhouseParticipants = new KillhouseParticipantsSettings();
        if (killhouseLoadout == null) killhouseLoadout = new KillhouseLoadoutSettings();
        if (killhousePerception == null) killhousePerception = new KillhousePerceptionSettings();
        if (killhouseFriendlyFire == null) killhouseFriendlyFire = new KillhouseFriendlyFireSettings();
        if (killhouseRanges == null) killhouseRanges = new KillhouseRangeSettings();
        if (killhouseTiming == null) killhouseTiming = new KillhouseTimingSettings();
        if (killhouseCover == null) killhouseCover = new KillhouseCoverSettings();
        if (killhouseMovementRecovery == null) killhouseMovementRecovery = new KillhouseMovementRecoverySettings();
        if (killhouseSearch == null) killhouseSearch = new KillhouseSearchSettings();
        if (killhouseAim == null) killhouseAim = new KillhouseAimSettings();
        if (killhouseResult == null) killhouseResult = new KillhouseResultSettings();
        if (combatFacing == null) combatFacing = new CombatFacingSettings();
        if (patrol == null) patrol = new PatrolSettings();
        if (killhousePatrol == null) killhousePatrol = new KillhousePatrolSettings();
        if (animationSweep == null) animationSweep = new AnimationSweepSettings();
        if (animationGating == null) animationGating = new AnimationGatingSettings();
    }

    private void MigrateLegacyInspectorFields()
    {
        if (inspectorGroupsMigrated)
            return;

        references.movement = legacyMovement;
        references.state = legacyState;
        references.weaponController = legacyWeaponController;
        references.combat = legacyCombat;
        references.aim = legacyAim;
        references.playerState = legacyPlayerState;
        references.animator = legacyAnimator;

        runtimeControls.enableKeyboardControls = legacyEnableKeyboardControls;
        runtimeControls.enableRuntimeDebugOverlay = legacyEnableRuntimeDebugOverlay;
        runtimeControls.showOnScreenHelp = legacyShowOnScreenHelp;

        killhouseParticipants.killhouseOnPlay = legacyKillhouseOnPlay;
        killhouseParticipants.showKillhouseNotes = legacyShowKillhouseNotes;
        killhouseParticipants.playerTarget = legacyPlayerTarget;
        killhouseParticipants.playerAimTarget = legacyPlayerAimTarget;
        killhouseParticipants.playerKillhouseExit = legacyPlayerKillhouseExit;
        killhouseParticipants.resetKillhouseOnResult = legacyResetKillhouseOnResult;
        killhouseParticipants.autoFindPlayer = legacyAutoFindPlayer;
        killhouseParticipants.usePlayerAimTarget = legacyUsePlayerAimTarget;
        killhouseParticipants.huntPlayerWithoutSight = legacyHuntPlayerWithoutSight;

        killhouseLoadout.preferFirearms = legacyPreferFirearms;
        killhouseLoadout.useSpecificKillhouseInventoryWeapon = legacyUseSpecificKillhouseInventoryWeapon;
        killhouseLoadout.selectedKillhouseInventoryWeaponInstanceId = legacySelectedKillhouseInventoryWeaponInstanceId ?? string.Empty;
        killhouseLoadout.crouchWhenShooting = legacyCrouchWhenShooting;
        killhouseLoadout.useCoverWhenDamaged = legacyUseCoverWhenDamaged;
        killhouseLoadout.jumpWhenStuck = legacyJumpWhenStuck;

        killhousePerception.lineOfSightLayers = legacyLineOfSightLayers;
        killhousePerception.coverProbeLayers = legacyCoverProbeLayers;
        killhousePerception.sightRange = legacySightRange;

        killhouseRanges.meleeEngageRange = legacyMeleeEngageRange;
        killhouseRanges.closeCombatDrawRange = legacyCloseCombatDrawRange;
        killhouseRanges.closeCombatUndrawDelay = legacyCloseCombatUndrawDelay;
        killhouseRanges.preferredFirearmRange = legacyPreferredFirearmRange;
        killhouseRanges.maxFirearmRange = legacyMaxFirearmRange;
        killhouseRanges.closeRange = legacyCloseRange;

        killhouseTiming.killhouseReachDistance = legacyKillhouseReachDistance;
        killhouseTiming.killhouseDecisionInterval = legacyKillhouseDecisionInterval;
        killhouseTiming.killhouseRepathInterval = legacyKillhouseRepathInterval;
        killhouseTiming.killhouseActionInterval = legacyKillhouseActionInterval;
        killhouseTiming.reloadResumeDelay = legacyReloadResumeDelay;

        killhouseCover.coverSearchRadius = legacyCoverSearchRadius;
        killhouseCover.coverSearchSamples = legacyCoverSearchSamples;
        killhouseCover.coverMinPlayerDistance = legacyCoverMinPlayerDistance;
        killhouseCover.retreatHealthPercent = legacyRetreatHealthPercent;
        killhouseCover.damageReactionSeconds = legacyDamageReactionSeconds;
        killhouseCover.damageHealthDelta = legacyDamageHealthDelta;
        killhouseCover.coverDamageHealthPercent = legacyCoverDamageHealthPercent;
        killhouseCover.coverDamageReactionSeconds = legacyCoverDamageReactionSeconds;
        killhouseCover.coverDestinationHoldSeconds = legacyCoverDestinationHoldSeconds;
        killhouseCover.coverRetryDelay = legacyCoverRetryDelay;

        killhouseMovementRecovery.stuckCheckInterval = legacyStuckCheckInterval;
        killhouseMovementRecovery.stuckDistance = legacyStuckDistance;
        killhouseMovementRecovery.stuckJumpInterval = legacyStuckJumpInterval;
        killhouseMovementRecovery.stuckChecksBeforeJump = legacyStuckChecksBeforeJump;

        killhouseSearch.searchAreaRadius = legacySearchAreaRadius;
        killhouseSearch.searchAreaSamples = legacySearchAreaSamples;
        killhouseSearch.broadSearchChance = legacyBroadSearchChance;
        killhouseSearch.searchMinPointSpacing = legacySearchMinPointSpacing;
        killhouseSearch.searchPointReachDistance = legacySearchPointReachDistance;
        killhouseSearch.lostTargetSearchSeconds = legacyLostTargetSearchSeconds;

        killhouseAim.aimLookAheadDistance = legacyAimLookAheadDistance;
        killhouseAim.aimFollowSpeed = legacyAimFollowSpeed;
        killhouseAim.firearmShootAimToleranceDegrees = legacyFirearmShootAimToleranceDegrees;
        killhouseAim.aimNotReadyRetryDelay = legacyAimNotReadyRetryDelay;
        killhouseAim.searchAimSweepAngle = legacySearchAimSweepAngle;
        killhouseAim.searchAimSweepSpeed = legacySearchAimSweepSpeed;

        killhouseResult.killhouseResultBannerSeconds = legacyKillhouseResultBannerSeconds;
        killhouseResult.showKillhouseResultBanner = legacyShowKillhouseResultBanner;
        killhouseResult.alsoShowKillhouseResultOnHud = legacyAlsoShowKillhouseResultOnHud;

        combatFacing.visibleTargetTurnSpeed = legacyVisibleTargetTurnSpeed;
        combatFacing.visibleTargetSnapAngle = legacyVisibleTargetSnapAngle;
        combatFacing.lastKnownFacingHoldSeconds = legacyLastKnownFacingHoldSeconds;

        patrol.patrolOnPlay = legacyPatrolOnPlay;
        patrol.patrolRuns = legacyPatrolRuns;
        patrol.waypointReachDistance = legacyWaypointReachDistance;
        patrol.patrolWaypoints = legacyPatrolWaypoints;
        patrol.fallbackLocalPatrolOffsets = legacyFallbackLocalPatrolOffsets;

        killhousePatrol.killhousePatrolOnPlay = legacyKillhousePatrolOnPlay;
        killhousePatrol.usePatrolWaypointsForKillhousePatrol = legacyUsePatrolWaypointsForKillhousePatrol;
        killhousePatrol.killhousePatrolCenter = legacyKillhousePatrolCenter;
        killhousePatrol.killhousePatrolRadius = legacyKillhousePatrolRadius;
        killhousePatrol.killhousePatrolMinPointSpacing = legacyKillhousePatrolMinPointSpacing;
        killhousePatrol.killhousePatrolNavMeshSamples = legacyKillhousePatrolNavMeshSamples;
        killhousePatrol.killhousePatrolSightInterval = legacyKillhousePatrolSightInterval;
        killhousePatrol.killhousePatrolRepathInterval = legacyKillhousePatrolRepathInterval;

        animationSweep.sweepOnPlay = legacySweepOnPlay;
        animationSweep.loopSweep = legacyLoopSweep;
        animationSweep.idleSeconds = legacyIdleSeconds;
        animationSweep.moveSeconds = legacyMoveSeconds;
        animationSweep.combatMoveSeconds = legacyCombatMoveSeconds;
        animationSweep.weaponStepSeconds = legacyWeaponStepSeconds;
        animationSweep.sweepAllWeapons = legacySweepAllWeapons;
        animationSweep.weaponStepsPerSweep = legacyWeaponStepsPerSweep;

        animationGating.weaponEquipSettleSeconds = legacyWeaponEquipSettleSeconds;
        animationGating.weaponEquipActionLockSeconds = legacyWeaponEquipActionLockSeconds;
        animationGating.weaponUnequipActionLockSeconds = legacyWeaponUnequipActionLockSeconds;
        animationGating.reloadActionLockSeconds = legacyReloadActionLockSeconds;
        animationGating.busyPollSeconds = legacyBusyPollSeconds;
        animationGating.animatorLayer = legacyAnimatorLayer;
        animationGating.busyAnimatorStateNames = legacyBusyAnimatorStateNames;

        inspectorGroupsMigrated = true;
    }

    private void Update()
    {
        ResolveReferences();
        SyncKillhouseCombatSettings();

        if (combat && combat.ConsumeKillhousePatrolResumeRequest())
        {
            StartKillhousePatrol();
            return;
        }

        if (enableKeyboardControls)
            HandleKeyboard();

        if (sweepActive)
            UpdateSweep();
        else if (killhousePatrolActive)
            UpdateKillhousePatrol();
        else if (patrolActive)
            UpdatePatrol();
    }

    public void StartPatrol()
    {
        StopSweep(false);
        StopKillhouseCombat(false);
        StopKillhousePatrol(false);
        patrolActive = true;
        currentWaypointIndex = GetDistributedWaypointStartIndex();
        SetCurrentPatrolDestination();
        ForceNonCombatPatrolAnimation(patrolRuns);
    }

    public void StopPatrol()
    {
        patrolActive = false;
        if (movement)
            movement.StopMovement(true);
    }

    public void StartSweep()
    {
        StopPatrol();
        StopKillhouseCombat(false);
        StopKillhousePatrol(false);
        EndManualWeaponInHandControl();
        sweepActive = true;
        sweepStep = -1;
        weaponSweepStep = 0;
        weaponSweepTargetSteps = 0;
        preparingFirstWeaponSweepStep = false;
        AdvanceSweep();
    }

    public void StopSweep(bool stopMovement)
    {
        sweepActive = false;
        EndManualWeaponInHandControl();
        if (stopMovement && movement)
            movement.StopMovement(true);
    }

    private void HandleKeyboard()
    {
        if (WasPressedThisFrame(Key.H))
        {
            enableRuntimeDebugOverlay = !enableRuntimeDebugOverlay;
            showOnScreenHelp = enableRuntimeDebugOverlay;
        }

        if (WasPressedThisFrame(Key.P))
        {
            if (patrolActive)
                StopPatrol();
            else
                StartPatrol();
        }

        if (WasPressedThisFrame(Key.T))
        {
            if (sweepActive)
                StopSweep(true);
            else
                StartSweep();
        }

        if (WasPressedThisFrame(Key.Y))
        {
            if (combat && combat.IsKillhouseCombatRunning)
                StopKillhouseCombat(true);
            else
                StartKillhouseCombat();
        }

        if (WasPressedThisFrame(Key.U))
        {
            if (killhousePatrolActive)
                StopKillhousePatrol(true);
            else
                StartKillhousePatrol();
        }

        if (WasPressedThisFrame(Key.Digit1))
            SetIdle();

        if (WasPressedThisFrame(Key.Digit2))
            SetMove(transform.forward, false, false);

        if (WasPressedThisFrame(Key.Digit3))
            SetMove(transform.forward, true, false);

        if (WasPressedThisFrame(Key.Digit4))
            SetMove(transform.forward, true, true);

        if (WasPressedThisFrame(Key.Digit5))
        {
            crouchToggle = !crouchToggle;
            if (movement)
                movement.SetCrouching(crouchToggle);
        }

        if (WasPressedThisFrame(Key.Space) && movement)
            movement.RequestJump();

        if (WasPressedThisFrame(Key.C) && state)
            state.SetCombatMode(!state.GetCombatMode());

        if (WasPressedThisFrame(Key.V) && state)
            state.SetWeaponInHand(!state.GetWeaponInHand());

        if (WasPressedThisFrame(Key.N) && weaponController)
            TryEquipNextSafe();

        if (WasPressedThisFrame(Key.B) && weaponController)
            TryEquipPreviousSafe();

        if (WasPressedThisFrame(Key.F) && combat)
            TryAttackSafe();

        if (WasPressedThisFrame(Key.G) && combat)
            TryBlockSafe();

        if (WasPressedThisFrame(Key.R) && combat)
            TryReloadSafe();
    }

    private void UpdatePatrol()
    {
        if (!movement)
            return;

        Vector3 target = GetPatrolPoint(currentWaypointIndex);
        Vector3 toTarget = target - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude > waypointReachDistance * waypointReachDistance)
            return;

        currentWaypointIndex = GetNextWaypointIndex();
        SetCurrentPatrolDestination();
    }

    private void SetCurrentPatrolDestination()
    {
        if (!movement)
            return;

        movement.SetDestination(GetPatrolPoint(currentWaypointIndex), patrolRuns);
    }

    public void StartKillhousePatrol()
    {
        StopPatrol();
        StopSweep(false);
        StopKillhouseCombat(false);
        EndManualWeaponInHandControl();

        killhousePatrolActive = true;
        RegisterActiveKillhousePatrol();
        killhousePatrolWaypointIndex = GetDistributedWaypointStartIndex();
        hasKillhousePatrolDestination = false;
        nextKillhousePatrolSightTime = 0f;
        nextKillhousePatrolRepathTime = 0f;
        PrepareKillhousePatrolState(true);
        SetNextKillhousePatrolDestination();
        ForceNonCombatPatrolAnimation(false);
    }

    public void StopKillhousePatrol(bool stopMovement)
    {
        killhousePatrolActive = false;
        hasKillhousePatrolDestination = false;
        UnregisterActiveKillhousePatrol();
        if (stopMovement && movement)
            movement.StopMovement(true);
    }

    private void UpdateKillhousePatrol()
    {
        if (!movement)
            return;

        PrepareKillhousePatrolState(false);

        if (Time.time >= nextKillhousePatrolSightTime)
        {
            nextKillhousePatrolSightTime = Time.time + killhousePatrolSightInterval;
            if (combat && combat.CanSeeKillhousePlayer())
            {
                AggroFromKillhousePatrol();
                return;
            }
        }

        if (!hasKillhousePatrolDestination || HasReachedKillhousePatrolDestination())
            SetNextKillhousePatrolDestination();
        else if (Time.time >= nextKillhousePatrolRepathTime)
            SetKillhousePatrolDestination(killhousePatrolDestination, false);
    }

    private void PrepareKillhousePatrolState(bool refreshLoadout)
    {
        if (refreshLoadout && combat)
            combat.PrepareKillhousePatrolLoadout();

        if (state)
        {
            state.SetCombatMode(false);
            state.SetWeaponInHand(false);
        }

        if (!movement)
            return;

        movement.SetRunning(false);
        movement.SetSprinting(false);
        movement.SetCrouching(false);
    }

    private void ForceNonCombatPatrolAnimation(bool run)
    {
        if (!animator || BaseAnimatorLayer >= animator.layerCount)
            return;

        animator.SetBool(CombatModeParam, false);
        animator.SetBool(WeaponInHandParam, false);

        int targetState = run ? RunState : WalkState;
        int targetFullPath = run ? RunStateFullPath : WalkStateFullPath;
        bool hasShortState = animator.HasState(BaseAnimatorLayer, targetState);
        bool hasFullPathState = animator.HasState(BaseAnimatorLayer, targetFullPath);
        if (!hasShortState && !hasFullPathState)
            return;

        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(BaseAnimatorLayer);
        if (currentState.shortNameHash == targetState || currentState.fullPathHash == targetFullPath)
            return;

        animator.CrossFadeInFixedTime(hasFullPathState ? targetFullPath : targetState, 0.1f, BaseAnimatorLayer);
    }

    private void AggroFromKillhousePatrol()
    {
        StopKillhousePatrol(true);
        SyncKillhouseCombatSettings();
        if (combat)
            combat.StartKillhouseCombat(true);
    }

    private bool HasReachedKillhousePatrolDestination()
    {
        Vector3 toDestination = killhousePatrolDestination - transform.position;
        toDestination.y = 0f;
        return toDestination.sqrMagnitude <= waypointReachDistance * waypointReachDistance;
    }

    private void SetNextKillhousePatrolDestination()
    {
        if (!movement)
            return;

        if (ShouldUsePatrolWaypointsForKillhousePatrol())
        {
            killhousePatrolWaypointIndex = GetNextKillhousePatrolWaypointIndex();
            SetKillhousePatrolDestination(GetPatrolPoint(killhousePatrolWaypointIndex), true);
            return;
        }

        if (TryFindRandomKillhousePatrolDestination(out Vector3 destination))
        {
            SetKillhousePatrolDestination(destination, true);
            return;
        }

        killhousePatrolWaypointIndex = GetNextKillhousePatrolWaypointIndex();
        SetKillhousePatrolDestination(GetPatrolPoint(killhousePatrolWaypointIndex), true);
    }

    private void SetKillhousePatrolDestination(Vector3 destination, bool faceImmediately)
    {
        killhousePatrolDestination = destination;
        hasKillhousePatrolDestination = true;
        nextKillhousePatrolRepathTime = Time.time + killhousePatrolRepathInterval;
        movement.SetDestination(destination, false);
        if (faceImmediately)
            movement.FaceDestinationDirectionImmediately();
        movement.SetSprinting(false);
    }

    private bool ShouldUsePatrolWaypointsForKillhousePatrol()
    {
        return usePatrolWaypointsForKillhousePatrol && GetPatrolPointCount() > 0;
    }

    private int GetNextKillhousePatrolWaypointIndex()
    {
        int count = GetPatrolPointCount();
        if (count <= 0)
            return 0;

        if (!hasKillhousePatrolDestination)
            return Mathf.Clamp(killhousePatrolWaypointIndex, 0, count - 1);

        return (killhousePatrolWaypointIndex + 1) % count;
    }

    private bool TryFindRandomKillhousePatrolDestination(out Vector3 destination)
    {
        Vector3 center = killhousePatrolCenter ? killhousePatrolCenter.position : startPosition;
        float bestAllowedScore = float.NegativeInfinity;
        float bestFallbackScore = float.NegativeInfinity;
        Vector3 bestAllowedDestination = Vector3.zero;
        Vector3 bestFallbackDestination = Vector3.zero;
        bool foundAllowed = false;
        bool foundFallback = false;
        destination = Vector3.zero;

        for (int i = 0; i < killhousePatrolNavMeshSamples; i++)
        {
            Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * killhousePatrolRadius;
            Vector3 candidate = center + new Vector3(randomCircle.x, 0f, randomCircle.y);
            if (!NavMesh.SamplePosition(candidate, out NavMeshHit hit, 2.5f, NavMesh.AllAreas))
                continue;

            if (!IsReachableKillhousePatrolDestination(hit.position))
                continue;

            float distanceFromNpc = FlatDistance(transform.position, hit.position);
            if (distanceFromNpc < killhousePatrolMinPointSpacing)
                continue;

            float distanceFromPrevious = hasKillhousePatrolDestination
                ? FlatDistance(killhousePatrolDestination, hit.position)
                : killhousePatrolMinPointSpacing;
            float separationScore = GetKillhousePatrolSeparationScore(hit.position, out bool isTooCloseToOtherPatrol);
            float score = distanceFromNpc + distanceFromPrevious * 0.5f + separationScore;

            if (score > bestFallbackScore)
            {
                bestFallbackScore = score;
                bestFallbackDestination = hit.position;
                foundFallback = true;
            }

            if (isTooCloseToOtherPatrol || score <= bestAllowedScore)
                continue;

            bestAllowedScore = score;
            bestAllowedDestination = hit.position;
            foundAllowed = true;
        }

        if (foundAllowed)
        {
            destination = bestAllowedDestination;
            return true;
        }

        if (foundFallback)
        {
            destination = bestFallbackDestination;
            return true;
        }

        return false;
    }

    private float GetKillhousePatrolSeparationScore(Vector3 candidate, out bool isTooClose)
    {
        isTooClose = false;
        if (!avoidOtherKillhousePatrolAreas || ActiveKillhousePatrolDrivers.Count <= 1)
            return 0f;

        float minimumSqrDistance = float.PositiveInfinity;
        for (int i = ActiveKillhousePatrolDrivers.Count - 1; i >= 0; i--)
        {
            NPCTestDriver other = ActiveKillhousePatrolDrivers[i];
            if (!other)
            {
                ActiveKillhousePatrolDrivers.RemoveAt(i);
                continue;
            }

            if (!ShouldAvoidKillhousePatrolDriver(other))
                continue;

            minimumSqrDistance = Mathf.Min(minimumSqrDistance, FlatSqrDistance(candidate, other.transform.position));
            if (other.hasKillhousePatrolDestination)
                minimumSqrDistance = Mathf.Min(minimumSqrDistance, FlatSqrDistance(candidate, other.killhousePatrolDestination));
        }

        if (float.IsPositiveInfinity(minimumSqrDistance))
            return 0f;

        float minimumDistance = Mathf.Sqrt(minimumSqrDistance);
        float separation = Mathf.Max(killhousePatrolMinPointSpacing, killhousePatrolSeparation);
        isTooClose = separation > 0f && minimumDistance < separation;
        return minimumDistance * killhousePatrolSeparationScoreWeight;
    }

    private bool ShouldAvoidKillhousePatrolDriver(NPCTestDriver other)
    {
        return other &&
               other != this &&
               other.isActiveAndEnabled &&
               other.killhousePatrolActive;
    }

    private void RegisterActiveKillhousePatrol()
    {
        if (!ActiveKillhousePatrolDrivers.Contains(this))
            ActiveKillhousePatrolDrivers.Add(this);
    }

    private void UnregisterActiveKillhousePatrol()
    {
        ActiveKillhousePatrolDrivers.Remove(this);
    }

    private bool IsReachableKillhousePatrolDestination(Vector3 destination)
    {
        NavMeshPath path = new NavMeshPath();
        return NavMesh.CalculatePath(transform.position, destination, NavMesh.AllAreas, path) &&
               path.status == NavMeshPathStatus.PathComplete;
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        Vector3 offset = b - a;
        offset.y = 0f;
        return offset.magnitude;
    }

    private static float FlatSqrDistance(Vector3 a, Vector3 b)
    {
        Vector3 offset = b - a;
        offset.y = 0f;
        return offset.sqrMagnitude;
    }

    public void StartKillhouseCombat()
    {
        StopPatrol();
        StopKillhousePatrol(false);
        StopSweep(false);
        SyncKillhouseCombatSettings();
        if (combat)
            combat.StartKillhouseCombat();
    }

    public void StopKillhouseCombat(bool stopMovement)
    {
        SyncKillhouseCombatSettings();
        if (combat)
            combat.StopKillhouseCombat(stopMovement);
    }

    private int GetNextWaypointIndex()
    {
        int count = GetPatrolPointCount();
        if (count <= 0)
            return 0;

        return (currentWaypointIndex + 1) % count;
    }

    private int GetDistributedWaypointStartIndex()
    {
        int count = GetPatrolPointCount();
        if (count <= 0)
            return 0;

        return (GetEntityId().GetHashCode() & int.MaxValue) % count;
    }

    private int GetPatrolPointCount()
    {
        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
            return patrolWaypoints.Length;

        return fallbackLocalPatrolOffsets != null ? fallbackLocalPatrolOffsets.Length : 0;
    }

    private Vector3 GetPatrolPoint(int index)
    {
        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            Transform waypoint = patrolWaypoints[Mathf.Clamp(index, 0, patrolWaypoints.Length - 1)];
            if (waypoint)
                return waypoint.position;
        }

        if (fallbackLocalPatrolOffsets == null || fallbackLocalPatrolOffsets.Length == 0)
            return startPosition;

        Vector3 offset = fallbackLocalPatrolOffsets[Mathf.Clamp(index, 0, fallbackLocalPatrolOffsets.Length - 1)];
        return startPosition + offset;
    }

    private void UpdateSweep()
    {
        if (sweepStep == SweepWeapons)
        {
            UpdateWeaponSweep();
            return;
        }

        if (Time.time >= nextSweepStepTime)
            AdvanceSweep();
    }

    private void AdvanceSweep()
    {
        if (sweepStep == SweepWeapons)
            EndManualWeaponInHandControl();

        sweepStep++;

        if (sweepStep >= SweepDone)
        {
            if (loopSweep)
            {
                sweepStep = SweepIdle;
            }
            else
            {
                sweepActive = false;
                SetIdle();
                return;
            }
        }

        switch (sweepStep)
        {
            case SweepIdle:
                SetIdle();
                SetCombat(false, false);
                nextSweepStepTime = Time.time + idleSeconds;
                break;

            case SweepWalk:
                SetMove(transform.forward, false, false);
                nextSweepStepTime = Time.time + moveSeconds;
                break;

            case SweepRun:
                SetMove(transform.forward, true, false);
                nextSweepStepTime = Time.time + moveSeconds;
                break;

            case SweepSprint:
                SetMove(transform.forward, true, true);
                nextSweepStepTime = Time.time + moveSeconds;
                break;

            case SweepJump:
                SetIdle();
                if (movement)
                    movement.RequestJump();
                nextSweepStepTime = Time.time + idleSeconds;
                break;

            case SweepCrouchWalk:
                if (movement)
                    movement.SetCrouching(true);
                SetMove(transform.forward, false, false, true);
                nextSweepStepTime = Time.time + moveSeconds;
                break;

            case SweepCombatIdle:
                SetIdle();
                SetCombat(true, true);
                nextSweepStepTime = Time.time + idleSeconds;
                break;

            case SweepCombatWalk:
                SetCombat(true, true);
                SetMove(transform.right, false, false);
                nextSweepStepTime = Time.time + combatMoveSeconds;
                break;

            case SweepCombatRun:
                SetCombat(true, true);
                SetMove(-transform.right, true, false);
                nextSweepStepTime = Time.time + combatMoveSeconds;
                break;

            case SweepWeapons:
                SetIdle();
                BeginManualWeaponInHandControl();
                weaponSweepStep = 0;
                weaponSweepTargetSteps = GetWeaponSweepTargetSteps();
                preparingFirstWeaponSweepStep = true;
                weaponSweepPhase = WeaponSweepBeginUnequip;
                nextWeaponActionTime = 0f;
                break;
        }
    }

    private void UpdateWeaponSweep()
    {
        if (!preparingFirstWeaponSweepStep && weaponSweepStep >= weaponSweepTargetSteps)
        {
            AdvanceSweep();
            return;
        }

        if (Time.time < nextWeaponActionTime)
            return;

        if (ShouldWaitForWeaponSweepBusyState())
        {
            nextWeaponActionTime = Time.time + busyPollSeconds;
            return;
        }

        switch (weaponSweepPhase)
        {
            case WeaponSweepBeginUnequip:
                BeginWeaponSweepUnequip();
                weaponSweepPhase = WeaponSweepFinishUnequip;
                nextWeaponActionTime = Time.time + weaponEquipSettleSeconds;
                break;

            case WeaponSweepFinishUnequip:
                FinishWeaponSweepUnequip();
                weaponSweepPhase = preparingFirstWeaponSweepStep
                    ? WeaponSweepSelect
                    : WeaponSweepCompleteStep;
                nextWeaponActionTime = Time.time + weaponEquipSettleSeconds;
                break;

            case WeaponSweepSelect:
                if (weaponController)
                    weaponController.EquipNext();

                SetWeaponSweepHolsteredState();
                weaponSweepPhase = WeaponSweepBeginEquip;
                nextWeaponActionTime = Time.time + busyPollSeconds;
                break;

            case WeaponSweepBeginEquip:
                BeginWeaponSweepEquip();
                weaponSweepPhase = WeaponSweepFinishEquip;
                nextWeaponActionTime = Time.time + weaponEquipSettleSeconds;
                break;

            case WeaponSweepFinishEquip:
                FinishWeaponSweepEquip();
                weaponSweepPhase = WeaponSweepAttack;
                nextWeaponActionTime = Time.time + busyPollSeconds;
                break;

            case WeaponSweepAttack:
                SetWeaponSweepReadyState();
                TryAttackSafe();
                weaponSweepPhase = WeaponSweepReload;
                nextWeaponActionTime = Time.time + weaponStepSeconds;
                break;

            case WeaponSweepReload:
                SetWeaponSweepReadyState();
                if (ShouldTryReloadForCurrentWeapon())
                    TryReloadSafe();

                weaponSweepPhase = WeaponSweepBeginUnequip;
                nextWeaponActionTime = Time.time + weaponStepSeconds;
                break;

            case WeaponSweepCompleteStep:
                preparingFirstWeaponSweepStep = false;
                weaponSweepStep++;
                weaponSweepPhase = WeaponSweepSelect;
                nextWeaponActionTime = Time.time + weaponStepSeconds;
                break;
        }
    }

    private bool ShouldWaitForWeaponSweepBusyState()
    {
        if (weaponSweepPhase == WeaponSweepFinishEquip ||
            weaponSweepPhase == WeaponSweepFinishUnequip)
        {
            return false;
        }

        return IsAnimatorBusy() || (combat && combat.IsReloadPending);
    }

    private void BeginWeaponSweepEquip()
    {
        if (!state)
            return;

        state.SetCombatMode(true);
        state.SetWeaponInHand(false);
    }

    private void FinishWeaponSweepEquip()
    {
        if (!state)
            return;

        state.SetCombatMode(true);
        state.SetWeaponInHand(ShouldCurrentWeaponUseHand());
    }

    private void BeginWeaponSweepUnequip()
    {
        if (!state)
            return;

        if (!state.GetWeaponInHand() || !ShouldCurrentWeaponUseHand())
        {
            state.SetCombatMode(false);
            state.SetWeaponInHand(false);
            return;
        }

        state.SetCombatMode(false);
    }

    private void FinishWeaponSweepUnequip()
    {
        SetWeaponSweepHolsteredState();
    }

    private void SetWeaponSweepHolsteredState()
    {
        if (!state)
            return;

        state.SetCombatMode(false);
        state.SetWeaponInHand(false);
    }

    private void SetWeaponSweepReadyState()
    {
        if (!state)
            return;

        state.SetCombatMode(true);
        state.SetWeaponInHand(ShouldCurrentWeaponUseHand());
    }

    private bool ShouldCurrentWeaponUseHand()
    {
        return weaponController &&
               weaponController.GetCurrentCategory() != NPCWeaponController.WeaponCategory.Unarmed;
    }

    private bool ShouldTryReloadForCurrentWeapon()
    {
        if (!weaponController)
            return false;

        NPCWeaponController.WeaponCategory category = weaponController.GetCurrentCategory();
        return category == NPCWeaponController.WeaponCategory.Pistol ||
               category == NPCWeaponController.WeaponCategory.SubmachineGun ||
               category == NPCWeaponController.WeaponCategory.Rifle ||
               category == NPCWeaponController.WeaponCategory.Shotgun;
    }

    private int GetWeaponSweepTargetSteps()
    {
        int configuredSteps = Mathf.Max(1, weaponStepsPerSweep);
        if (!sweepAllWeapons || !weaponController)
            return configuredSteps;

        int weaponCount = weaponController.GetWeaponCount();
        return weaponCount > 0 ? weaponCount : configuredSteps;
    }

    private void BeginManualWeaponInHandControl()
    {
        if (!weaponController || hasStoredWeaponInHandSync)
            return;

        storedWeaponInHandSync = weaponController.GetSyncWeaponInHandToCombatMode();
        hasStoredWeaponInHandSync = true;
        weaponController.SetSyncWeaponInHandToCombatMode(false);
    }

    private void EndManualWeaponInHandControl()
    {
        if (!weaponController || !hasStoredWeaponInHandSync)
            return;

        weaponController.SetSyncWeaponInHandToCombatMode(storedWeaponInHandSync);
        hasStoredWeaponInHandSync = false;
    }

    private void SetIdle()
    {
        patrolActive = false;
        StopKillhousePatrol(false);
        StopKillhouseCombat(false);
        crouchToggle = false;

        if (!movement)
            return;

        movement.StopMovement(true);
        movement.SetRunning(false);
        movement.SetSprinting(false);
        movement.SetCrouching(false);
    }

    private void SetMove(Vector3 direction, bool run, bool sprint)
    {
        SetMove(direction, run, sprint, false);
    }

    private void SetMove(Vector3 direction, bool run, bool sprint, bool keepCrouching)
    {
        patrolActive = false;
        StopKillhousePatrol(false);
        StopKillhouseCombat(false);

        if (!movement)
            return;

        if (!keepCrouching)
            movement.SetCrouching(false);

        movement.SetMoveDirection(direction, run);
        movement.SetSprinting(sprint);
    }

    private void SetCombat(bool combatMode, bool weaponInHand)
    {
        if (!state)
            return;

        state.SetCombatMode(combatMode);
        state.SetWeaponInHand(weaponInHand);
    }


    private void SyncKillhouseCombatSettings()
    {
        if (!combat)
            return;

        combat.ApplyKillhouseCombatTestSettings(new NPCCombat.KillhouseCombatTestSettings
        {
            killhouseOnPlay = killhouseOnPlay,
            showKillhouseNotes = showKillhouseNotes,
            playerState = playerState,
            playerTarget = playerTarget,
            playerAimTarget = playerAimTarget,
            playerKillhouseExit = playerKillhouseExit,
            resetKillhouseOnResult = resetKillhouseOnResult,
            autoFindPlayer = autoFindPlayer,
            usePlayerAimTarget = usePlayerAimTarget,
            huntPlayerWithoutSight = huntPlayerWithoutSight,
            preferFirearms = preferFirearms,
            useSpecificKillhouseInventoryWeapon = useSpecificKillhouseInventoryWeapon,
            selectedKillhouseInventoryWeaponInstanceId = selectedKillhouseInventoryWeaponInstanceId,
            crouchWhenShooting = crouchWhenShooting,
            useCoverWhenDamaged = useCoverWhenDamaged,
            jumpWhenStuck = jumpWhenStuck,
            avoidSameFactionFriendlyFire = avoidSameFactionFriendlyFire,
            friendlyFireAvoidanceLayers = friendlyFireAvoidanceLayers,
            friendlyFireAvoidanceRadius = friendlyFireAvoidanceRadius,
            lineOfSightLayers = lineOfSightLayers,
            coverProbeLayers = coverProbeLayers,
            sightRange = sightRange,
            sightConeAngleDegrees = sightConeAngleDegrees,
            targetAcquisitionSeconds = targetAcquisitionSeconds,
            meleeEngageRange = meleeEngageRange,
            closeCombatDrawRange = closeCombatDrawRange,
            closeCombatUndrawDelay = closeCombatUndrawDelay,
            preferredFirearmRange = preferredFirearmRange,
            maxFirearmRange = maxFirearmRange,
            closeRange = closeRange,
            killhouseReachDistance = killhouseReachDistance,
            killhouseDecisionInterval = killhouseDecisionInterval,
            killhouseRepathInterval = killhouseRepathInterval,
            killhouseActionInterval = killhouseActionInterval,
            reloadResumeDelay = reloadResumeDelay,
            coverSearchRadius = coverSearchRadius,
            coverSearchSamples = coverSearchSamples,
            coverMinPlayerDistance = coverMinPlayerDistance,
            retreatHealthPercent = retreatHealthPercent,
            damageReactionSeconds = damageReactionSeconds,
            damageHealthDelta = damageHealthDelta,
            coverDamageHealthPercent = coverDamageHealthPercent,
            coverDamageReactionSeconds = coverDamageReactionSeconds,
            stuckCheckInterval = stuckCheckInterval,
            stuckDistance = stuckDistance,
            stuckJumpInterval = stuckJumpInterval,
            stuckChecksBeforeJump = stuckChecksBeforeJump,
            searchAreaRadius = searchAreaRadius,
            searchAreaSamples = searchAreaSamples,
            broadSearchChance = broadSearchChance,
            searchMinPointSpacing = searchMinPointSpacing,
            searchPointReachDistance = searchPointReachDistance,
            lostTargetEngageSeconds = lostTargetEngageSeconds,
            lostTargetCombatEngageSeconds = killhouseSearch.lostTargetCombatEngageSeconds,
            lostTargetSearchSeconds = lostTargetSearchSeconds,
            coverDestinationHoldSeconds = coverDestinationHoldSeconds,
            coverRetryDelay = coverRetryDelay,
            aimLookAheadDistance = aimLookAheadDistance,
            aimFollowSpeed = aimFollowSpeed,
            firearmShootAimToleranceDegrees = firearmShootAimToleranceDegrees,
            aimNotReadyRetryDelay = aimNotReadyRetryDelay,
            searchAimSweepAngle = searchAimSweepAngle,
            searchAimSweepSpeed = searchAimSweepSpeed,
            killhouseResultBannerSeconds = killhouseResultBannerSeconds,
            showKillhouseResultBanner = showKillhouseResultBanner,
            alsoShowKillhouseResultOnHud = alsoShowKillhouseResultOnHud,
            visibleTargetTurnSpeed = visibleTargetTurnSpeed,
            visibleTargetSnapAngle = visibleTargetSnapAngle,
            lastKnownFacingHoldSeconds = lastKnownFacingHoldSeconds,
            weaponEquipSettleSeconds = weaponEquipSettleSeconds,
            weaponEquipActionLockSeconds = weaponEquipActionLockSeconds,
            weaponUnequipActionLockSeconds = weaponUnequipActionLockSeconds,
            reloadActionLockSeconds = reloadActionLockSeconds,
            busyPollSeconds = busyPollSeconds,
            animatorLayer = animatorLayer,
            busyAnimatorStateNames = busyAnimatorStateNames
        });
    }

    private void ClampKillhouseCombatSettings()
    {
        closeCombatDrawRange = Mathf.Max(meleeEngageRange, closeCombatDrawRange);
        closeCombatUndrawDelay = Mathf.Max(0f, closeCombatUndrawDelay);
        killhousePerception.sightRange = Mathf.Max(0.5f, killhousePerception.sightRange);
        killhousePerception.sightConeAngleDegrees = Mathf.Clamp(killhousePerception.sightConeAngleDegrees, 1f, 179f);
        killhousePerception.targetAcquisitionSeconds = Mathf.Max(0f, killhousePerception.targetAcquisitionSeconds);
        killhouseSearch.lostTargetEngageSeconds = Mathf.Max(0.1f, killhouseSearch.lostTargetEngageSeconds);
        if (killhouseSearch.lostTargetCombatEngageSeconds <= 0f)
            killhouseSearch.lostTargetCombatEngageSeconds = 6f;
        killhouseSearch.lostTargetCombatEngageSeconds = Mathf.Max(killhouseSearch.lostTargetEngageSeconds, killhouseSearch.lostTargetCombatEngageSeconds);
        killhouseSearch.lostTargetSearchSeconds = Mathf.Max(0.5f, killhouseSearch.lostTargetSearchSeconds);
        visibleTargetTurnSpeed = Mathf.Max(0f, visibleTargetTurnSpeed);
        visibleTargetSnapAngle = Mathf.Max(0f, visibleTargetSnapAngle);
        lastKnownFacingHoldSeconds = Mathf.Max(0f, lastKnownFacingHoldSeconds);
        killhouseFriendlyFire.friendlyFireAvoidanceRadius = Mathf.Max(0f, killhouseFriendlyFire.friendlyFireAvoidanceRadius);
        killhousePatrol.killhousePatrolSeparation = Mathf.Max(0f, killhousePatrol.killhousePatrolSeparation);
        killhousePatrol.killhousePatrolSeparationScoreWeight = Mathf.Max(0f, killhousePatrol.killhousePatrolSeparationScoreWeight);
    }

    private void ResolveReferences()
    {
        if (!movement)
            movement = GetComponent<NPCMovement>();

        if (!movement)
            movement = GetComponentInParent<NPCMovement>();

        if (!state)
            state = GetComponent<NPCState>();

        if (!state)
            state = GetComponentInParent<NPCState>();

        if (!weaponController)
            weaponController = GetComponentInChildren<NPCWeaponController>(true);

        if (!weaponController)
            weaponController = GetComponentInParent<NPCWeaponController>();

        if (!combat)
            combat = GetComponent<NPCCombat>();

        if (!combat)
            combat = GetComponentInParent<NPCCombat>();

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
    }

    private static bool WasPressedThisFrame(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        return keyboard[key].wasPressedThisFrame;
    }

    private bool TryEquipNextSafe()
    {
        if (!weaponController || IsAnimatorBusy())
            return false;

        weaponController.EquipNext();
        return true;
    }

    private bool TryEquipPreviousSafe()
    {
        if (!weaponController || IsAnimatorBusy())
            return false;

        weaponController.EquipPrevious();
        return true;
    }

    private bool TryAttackSafe()
    {
        if (!combat || IsAnimatorBusy())
            return false;

        return combat.TryAttack();
    }

    private bool TryBlockSafe()
    {
        if (!combat || IsAnimatorBusy())
            return false;

        return combat.TryBlock();
    }

    private bool TryReloadSafe()
    {
        if (!combat || IsAnimatorBusy())
            return false;

        return combat.TryReload();
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

    private void OnGUI()
    {
        if (!enableRuntimeDebugOverlay)
            return;

        SyncKillhouseCombatSettings();
        if (combat)
            combat.DrawKillhouseResultBanner();

        if (!showOnScreenHelp)
            return;

        List<string> lines = new List<string>
        {
            "NPC Test Driver",
            "1 Idle | 2 Walk | 3 Run | 4 Sprint | 5 Crouch",
            "Space Jump | P Patrol | U Killhouse patrol | T Auto sweep | Y Killhouse | H Hide",
            "C Combat | V Weapon hand | N/B Weapon next/prev",
            "F Attack | G Block | R Reload",
            "Patrol: " + (patrolActive ? "On" : "Off") +
            " | Killhouse Patrol: " + (killhousePatrolActive ? "On" : "Off") +
            " | Sweep: " + (sweepActive ? "On" : "Off") +
            " | Killhouse: " + (combat ? combat.GetKillhouseStatusText() : "Off")
        };

        SyncKillhouseCombatSettings();
        if (combat)
            combat.AddKillhouseSetupNotes(lines);

        if (state)
        {
            int currentHp = Mathf.RoundToInt(Mathf.Max(0f, state.GetHealthPoints()));
            int maxHp = Mathf.RoundToInt(Mathf.Max(0f, state.GetMaxHealthPoints()));
            int currentAp = Mathf.RoundToInt(Mathf.Max(0f, state.GetActionPoints()));
            int maxAp = Mathf.RoundToInt(Mathf.Max(0f, state.GetMaxActionPoints()));

            lines.Add($"NPC HP: {currentHp}/{maxHp}");
            lines.Add($"NPC AP: {currentAp}/{maxAp}");
            lines.Add($"LA: {Mathf.RoundToInt(state.GetLeftArmHealth())}/100 {(state.GetLeftArmCrippled() ? "CRIP" : "OK")}");
            lines.Add($"RA: {Mathf.RoundToInt(state.GetRightArmHealth())}/100 {(state.GetRightArmCrippled() ? "CRIP" : "OK")}");
            lines.Add($"CH: {Mathf.RoundToInt(state.GetChestHealth())}/100 {(state.GetChestCrippled() ? "CRIP" : "OK")}");
            lines.Add($"HD: {Mathf.RoundToInt(state.GetHeadHealth())}/100 {(state.GetHeadCrippled() ? "CRIP" : "OK")}");
            lines.Add($"LL: {Mathf.RoundToInt(state.GetLeftLegHealth())}/100 {(state.GetLeftLegCrippled() ? "CRIP" : "OK")}");
            lines.Add($"RL: {Mathf.RoundToInt(state.GetRightLegHealth())}/100 {(state.GetRightLegCrippled() ? "CRIP" : "OK")}");
        }
        else
        {
            lines.Add("NPC HP: -/-");
            lines.Add("NPC AP: -/-");
            lines.Add("LA: -/100 -");
            lines.Add("RA: -/100 -");
            lines.Add("CH: -/100 -");
            lines.Add("HD: -/100 -");
            lines.Add("LL: -/100 -");
            lines.Add("RL: -/100 -");
        }

        GUIStyle labelStyle = GUI.skin.label;
        GUIStyle boxStyle = GUI.skin.box;
        GUIContent content = new GUIContent();

        float maxLineWidth = 0f;
        float lineHeight = 0f;
        for (int i = 0; i < lines.Count; i++)
        {
            content.text = lines[i];
            Vector2 size = labelStyle.CalcSize(content);
            if (size.x > maxLineWidth)
                maxLineWidth = size.x;
            if (size.y > lineHeight)
                lineHeight = size.y;
        }

        float contentHeight = lineHeight * lines.Count;
        float width = maxLineWidth + boxStyle.padding.left + boxStyle.padding.right;
        float height = contentHeight + boxStyle.padding.top + boxStyle.padding.bottom;
        Rect panelRect = new Rect(12f, 12f, width, height);
        GUI.Box(panelRect, GUIContent.none, boxStyle);

        float x = panelRect.x + boxStyle.padding.left;
        float y = panelRect.y + boxStyle.padding.top;
        float labelWidth = maxLineWidth;
        for (int i = 0; i < lines.Count; i++)
        {
            GUI.Label(new Rect(x, y, labelWidth, lineHeight), lines[i], labelStyle);
            y += lineHeight;
        }
    }

    private void OnDrawGizmosSelected()
    {
        int count = GetPatrolPointCount();
        if (count <= 0)
            return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < count; i++)
        {
            Vector3 point = Application.isPlaying ? GetPatrolPoint(i) : GetEditorPatrolPoint(i);
            Gizmos.DrawSphere(point, 0.2f);

            Vector3 nextPoint = Application.isPlaying ? GetPatrolPoint((i + 1) % count) : GetEditorPatrolPoint((i + 1) % count);
            Gizmos.DrawLine(point, nextPoint);
        }

        Gizmos.color = Color.yellow;
        Vector3 center = killhousePatrolCenter ? killhousePatrolCenter.position : transform.position;
        Gizmos.DrawWireSphere(center, killhousePatrolRadius);
    }

    private Vector3 GetEditorPatrolPoint(int index)
    {
        if (patrolWaypoints != null && patrolWaypoints.Length > 0)
        {
            Transform waypoint = patrolWaypoints[Mathf.Clamp(index, 0, patrolWaypoints.Length - 1)];
            if (waypoint)
                return waypoint.position;
        }

        if (fallbackLocalPatrolOffsets == null || fallbackLocalPatrolOffsets.Length == 0)
            return transform.position;

        return transform.position + fallbackLocalPatrolOffsets[Mathf.Clamp(index, 0, fallbackLocalPatrolOffsets.Length - 1)];
    }
}
