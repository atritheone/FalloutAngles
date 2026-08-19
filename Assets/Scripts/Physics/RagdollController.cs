using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class RagdollController : MonoBehaviour
{
    [Serializable]
    private class RagdollBone
    {
        public string boneName;
        public string connectedBoneName;
        public float mass = 5f;
        public float lowTwistLimit = -25f;
        public float highTwistLimit = 25f;
        public float swing1Limit = 35f;
        public float swing2Limit = 35f;
    }

    private class RuntimeBone
    {
        public RagdollBone setup;
        public Transform transform;
        public Rigidbody body;
        public Collider[] colliders;
        public CharacterJoint joint;
        public RuntimeBone connectedBone;
        public Quaternion targetRotationFromConnected;
        public Quaternion crippledLimbVisualRotation;
        public bool hasCrippledLimbVisualRotation;
    }

    private struct AttachedTransformState
    {
        public Transform Transform;
        public Transform Parent;
        public int SiblingIndex;
        public Vector3 LocalPosition;
        public Quaternion LocalRotation;
        public Vector3 LocalScale;

        public AttachedTransformState(Transform target)
        {
            Transform = target;
            Parent = target ? target.parent : null;
            SiblingIndex = target ? target.GetSiblingIndex() : 0;
            LocalPosition = target ? target.localPosition : Vector3.zero;
            LocalRotation = target ? target.localRotation : Quaternion.identity;
            LocalScale = target ? target.localScale : Vector3.one;
        }
    }

    [Header("Activation")]
    [SerializeField] private bool ragdollWhenHealthDepleted = true;
    [SerializeField] private bool buildOnAwake = false;

    [Header("Test Input")]
    [SerializeField] private bool enableTestRagdollInput = false;
    [SerializeField] private Key testRagdollKey = Key.None;

    [Header("Root")]
    [SerializeField] private Animator animator;
    [SerializeField] private Rigidbody rootRigidbody;
    [SerializeField] private Collider rootCollider;
    [SerializeField] private bool disableRootRigidbodyOnRagdoll = true;
    [SerializeField] private bool disableRootColliderOnRagdoll = true;

    [Header("Generated Bone Colliders")]
    [SerializeField] private bool createMissingBoneColliders = true;
    [SerializeField] private float defaultBoneColliderRadius = 0.08f;

    [Header("Joint Stability")]
    [SerializeField] private bool alignJointAxesToBindPose = true;
    [SerializeField] private float boneLinearDamping = 0.02f;
    [SerializeField] private float boneAngularDamping = 3f;
    [SerializeField] private float maxBoneAngularVelocity = 7f;
    [SerializeField] private int boneSolverIterations = 24;
    [SerializeField] private int boneSolverVelocityIterations = 10;
    [SerializeField] private float jointLimitSpring = 2500f;
    [SerializeField] private float jointLimitDamper = 250f;
    [SerializeField] private float jointLimitContactDistance = 3f;
    [SerializeField] private bool enableJointProjection = true;
    [SerializeField] private float jointProjectionDistance = 0.01f;
    [SerializeField] private float jointProjectionAngle = 3f;
    [SerializeField] private bool preserveRagdollActivationPose = true;
    [SerializeField] private float poseSpring = 260f;
    [SerializeField] private float poseDamper = 34f;
    [SerializeField] private float torsoPoseMultiplier = 2.25f;
    [SerializeField] private float headPoseMultiplier = 1.5f;
    [SerializeField] private float limbPoseMultiplier = 1f;
    [SerializeField] private float handFootPoseMultiplier = 0.45f;
    [SerializeField] private float maxPoseCorrectionAcceleration = 420f;

    [Header("Corpse Settling")]
    [SerializeField] private bool freezeWhenSettled = true;
    [SerializeField] private float minimumSettleTime = 0.75f;
    [SerializeField] private float requiredStillTime = 0.2f;
    [SerializeField] private float maximumSettleTime = 3f;
    [SerializeField] private float settledLinearVelocity = 0.12f;
    [SerializeField] private float settledAngularVelocity = 0.8f;

    [Header("Crippled Limb Ragdoll")]
    [SerializeField] private bool enableCrippledLimbRagdoll = true;
    [SerializeField] private float crippledLimbInheritedVelocityMultiplier = 0.35f;
    [SerializeField] private float crippledLimbLinearDamping = 1.2f;
    [SerializeField] private float crippledLimbAngularDamping = 8f;
    [SerializeField] private float crippledLimbMaxAngularVelocity = 3.5f;
    [SerializeField] private float crippledLimbPoseSpringMultiplier = 0.22f;
    [SerializeField] private float crippledLimbPoseDamperMultiplier = 1.5f;
    [SerializeField] private float crippledLimbDownwardVelocityKick = 0.7f;
    [SerializeField] private float crippledLimbBackwardVelocityKick = 0.25f;
    [SerializeField] [Range(0f, 1f)] private float crippledLimbVisualWeight = 0.75f;
    [SerializeField] private float crippledLimbVisualFollowSpeed = 5f;
    [SerializeField] private float crippledLegThighDragDegrees = 12f;
    [SerializeField] private float crippledLegShinBendDegrees = 30f;
    [SerializeField] private float crippledLegFootDropDegrees = 26f;

    [Header("Head Attachments")]
    [SerializeField] private string headRagdollBoneName = "DEF-head";
    [SerializeField] private string[] transformsToAttachToHeadOnRagdoll = { "ORG-head" };

    [Header("Chest Attachments")]
    [SerializeField] private string chestRagdollBoneName = "DEF-spine.003";
    [SerializeField] private string[] transformsToAttachToChestOnRagdoll =
    {
        "ORG-shoulder.L",
        "ORG-shoulder.R",
        "ORG-breast.L",
        "ORG-breast.R"
    };

    [Header("Systems Disabled On Ragdoll")]
    [SerializeField] private MonoBehaviour[] behavioursToDisable;
    [SerializeField]
    private string[] behaviourTypeNamesToDisable =
    {
        "PlayerMovement",
        "playeraim",
        "PlayerCombat",
        "PlayerInteraction",
        "PlayerWeaponController",
        "FirearmRecoilDriver",
        "MouseTargetFollower",
        "MultiAimConstraintStateGate",
        "FirearmIKConstraint",
        "RigBuilder",
        "NPCAim",
        "NPCMovement",
        "NPCCombat",
        "NPCWeaponController",
        "NPCTestDriver",
        "NPCLineOfSightRenderer",
        "NavMeshAgent"
    };

    [Header("Bones")]
    [SerializeField]
    private RagdollBone[] bones =
    {
        new RagdollBone { boneName = "DEF-spine", mass = 10f, lowTwistLimit = -18f, highTwistLimit = 18f, swing1Limit = 25f, swing2Limit = 25f },
        new RagdollBone { boneName = "DEF-spine.001", connectedBoneName = "DEF-spine", mass = 8f, lowTwistLimit = -18f, highTwistLimit = 18f, swing1Limit = 20f, swing2Limit = 20f },
        new RagdollBone { boneName = "DEF-spine.002", connectedBoneName = "DEF-spine.001", mass = 8f, lowTwistLimit = -18f, highTwistLimit = 18f, swing1Limit = 20f, swing2Limit = 20f },
        new RagdollBone { boneName = "DEF-spine.003", connectedBoneName = "DEF-spine.002", mass = 12f, lowTwistLimit = -18f, highTwistLimit = 18f, swing1Limit = 25f, swing2Limit = 25f },
        new RagdollBone { boneName = "DEF-neck", connectedBoneName = "DEF-spine.003", mass = 2f, lowTwistLimit = -25f, highTwistLimit = 25f, swing1Limit = 20f, swing2Limit = 20f },
        new RagdollBone { boneName = "DEF-head", connectedBoneName = "DEF-neck", mass = 5f, lowTwistLimit = -35f, highTwistLimit = 35f, swing1Limit = 30f, swing2Limit = 20f },

        new RagdollBone { boneName = "DEF-upper_arm.L", connectedBoneName = "DEF-spine.003", mass = 4f, lowTwistLimit = -50f, highTwistLimit = 35f, swing1Limit = 55f, swing2Limit = 35f },
        new RagdollBone { boneName = "DEF-forearm.L", connectedBoneName = "DEF-upper_arm.L", mass = 3f, lowTwistLimit = -10f, highTwistLimit = 85f, swing1Limit = 12f, swing2Limit = 12f },
        new RagdollBone { boneName = "DEF-hand.L", connectedBoneName = "DEF-forearm.L", mass = 1f, lowTwistLimit = -35f, highTwistLimit = 35f, swing1Limit = 25f, swing2Limit = 25f },

        new RagdollBone { boneName = "DEF-upper_arm.R", connectedBoneName = "DEF-spine.003", mass = 4f, lowTwistLimit = -50f, highTwistLimit = 35f, swing1Limit = 55f, swing2Limit = 35f },
        new RagdollBone { boneName = "DEF-forearm.R", connectedBoneName = "DEF-upper_arm.R", mass = 3f, lowTwistLimit = -10f, highTwistLimit = 85f, swing1Limit = 12f, swing2Limit = 12f },
        new RagdollBone { boneName = "DEF-hand.R", connectedBoneName = "DEF-forearm.R", mass = 1f, lowTwistLimit = -35f, highTwistLimit = 35f, swing1Limit = 25f, swing2Limit = 25f },

        new RagdollBone { boneName = "DEF-thigh.L", connectedBoneName = "DEF-spine", mass = 7f, lowTwistLimit = -25f, highTwistLimit = 45f, swing1Limit = 45f, swing2Limit = 25f },
        new RagdollBone { boneName = "DEF-shin.L", connectedBoneName = "DEF-thigh.L", mass = 5f, lowTwistLimit = -5f, highTwistLimit = 95f, swing1Limit = 10f, swing2Limit = 10f },
        new RagdollBone { boneName = "DEF-foot.L", connectedBoneName = "DEF-shin.L", mass = 2f, lowTwistLimit = -25f, highTwistLimit = 35f, swing1Limit = 20f, swing2Limit = 20f },

        new RagdollBone { boneName = "DEF-thigh.R", connectedBoneName = "DEF-spine", mass = 7f, lowTwistLimit = -25f, highTwistLimit = 45f, swing1Limit = 45f, swing2Limit = 25f },
        new RagdollBone { boneName = "DEF-shin.R", connectedBoneName = "DEF-thigh.R", mass = 5f, lowTwistLimit = -5f, highTwistLimit = 95f, swing1Limit = 10f, swing2Limit = 10f },
        new RagdollBone { boneName = "DEF-foot.R", connectedBoneName = "DEF-shin.R", mass = 2f, lowTwistLimit = -25f, highTwistLimit = 35f, swing1Limit = 20f, swing2Limit = 20f }
    };

    private readonly Dictionary<string, RuntimeBone> runtimeBonesByName = new Dictionary<string, RuntimeBone>(StringComparer.Ordinal);
    private readonly List<Behaviour> behavioursDisabledForRagdoll = new List<Behaviour>();
    private readonly List<AttachedTransformState> attachedTransformsForRagdoll = new List<AttachedTransformState>();
    private NPCState npcState;
    private PlayerState playerState;
    private NPCState subscribedNpcState;
    private PlayerState subscribedPlayerState;
    private bool built;
    private bool ragdolled;
    private bool ragdollSettled;
    private float ragdollActivationTime;
    private float stillTime;
    private bool hasRagdollStateCache;
    private bool cachedAnimatorEnabled;
    private bool cachedRootColliderEnabled;
    private bool cachedRootRigidbodyIsKinematic;
    private bool cachedRootRigidbodyUseGravity;
    private bool cachedRootRigidbodyDetectCollisions;
    private const float MinimumBodyMass = 0.01f;
    private static readonly string[] LeftLegCrippledLimbBones = { "DEF-thigh.L", "DEF-shin.L", "DEF-foot.L" };
    private static readonly string[] RightLegCrippledLimbBones = { "DEF-thigh.R", "DEF-shin.R", "DEF-foot.R" };
    private readonly List<RuntimeBone> activeCrippledLimbBones = new List<RuntimeBone>();
    private readonly List<Collider> rootIgnoredCrippledLimbColliders = new List<Collider>();
    private bool crippledLimbRagdollActive;
    private bool hasPlayerCrippledStateCache;
    private bool cachedPlayerLeftLegCrippled;
    private bool cachedPlayerRightLegCrippled;

    public bool IsRagdolled => ragdolled;
    public bool IsCrippledLimbRagdolled => crippledLimbRagdollActive;

    private void Awake()
    {
        CacheRootReferences();
        CacheStateReferences();

        if (buildOnAwake)
            BuildRagdoll();
    }

    private void OnEnable()
    {
        CacheStateReferences();
        SubscribeNpcDeathEvent();
        SubscribePlayerDeathEvent();
        CachePlayerCrippledStates();
        ActivateRagdollIfHealthDepleted();
    }

    private void OnDisable()
    {
        DeactivateCrippledLimbRagdoll(false);
        UnsubscribeNpcDeathEvent();
        UnsubscribePlayerDeathEvent();
    }

    private void Update()
    {
        if (!ragdolled && WasTestRagdollPressed())
            ActivateRagdoll();

        if (!ragdolled)
        {
            SyncCrippledLimbRagdollToPlayerState();
        }

        ActivateRagdollIfHealthDepleted();
    }

    private void LateUpdate()
    {
        if (!ragdolled && crippledLimbRagdollActive)
            ApplyCrippledLimbAnimationFailure();
    }

    private void FixedUpdate()
    {
        if (ragdolled)
            StabilizeActiveRagdoll();
        else if (crippledLimbRagdollActive)
            StabilizeCrippledLimbRagdoll();
    }

    [ContextMenu("Build Ragdoll Runtime Components")]
    public void BuildRagdoll()
    {
        if (built)
            return;

        CacheRootReferences();
        runtimeBonesByName.Clear();

        Dictionary<string, Transform> transformsByName = BuildTransformLookup();
        for (int i = 0; i < bones.Length; i++)
        {
            RagdollBone setup = bones[i];
            if (setup == null || string.IsNullOrWhiteSpace(setup.boneName))
                continue;

            if (!transformsByName.TryGetValue(setup.boneName, out Transform boneTransform) || !boneTransform)
            {
                Debug.LogWarning($"{name}: ragdoll bone '{setup.boneName}' was not found.", this);
                continue;
            }

            Collider[] boneColliders = boneTransform.GetComponents<Collider>();
            if (boneColliders == null || boneColliders.Length == 0)
            {
                Collider generatedCollider = CreateDefaultBoneCollider(boneTransform);
                if (!generatedCollider)
                {
                    Debug.LogWarning($"{name}: ragdoll bone '{setup.boneName}' has no collider.", boneTransform);
                    continue;
                }

                boneColliders = new[] { generatedCollider };
            }

            Rigidbody body = boneTransform.GetComponent<Rigidbody>();
            if (!body)
                body = boneTransform.gameObject.AddComponent<Rigidbody>();

            ConfigureBody(body, Mathf.Max(MinimumBodyMass, setup.mass));
            runtimeBonesByName[setup.boneName] = new RuntimeBone
            {
                setup = setup,
                transform = boneTransform,
                body = body,
                colliders = boneColliders
            };
        }

        DistributeRootMassAcrossRagdollBodies();

        foreach (RuntimeBone runtimeBone in runtimeBonesByName.Values)
            ConfigureJoint(runtimeBone);

        SetRagdollBodiesActive(false, Vector3.zero);
        built = true;
    }

    [ContextMenu("Activate Ragdoll")]
    public void ActivateRagdoll()
    {
        ActivateRagdoll(Vector3.zero, transform.position, null);
    }

    public void ActivateRagdoll(Vector3 impulse, Vector3 point, Collider hitCollider)
    {
        if (ragdolled)
            return;

        DeactivateCrippledLimbRagdoll(false);

        if (!built)
            BuildRagdoll();

        DistributeRootMassAcrossRagdollBodies();
        AttachConfiguredTransformsToRagdollBones();
        CaptureCurrentPoseAsRagdollTargets();
        CachePreRagdollState();

        Vector3 inheritedVelocity = rootRigidbody ? rootRigidbody.linearVelocity : Vector3.zero;
        DisableAnimatedSystems();
        SetRootPhysicsActive(false);
        SetRagdollBodiesActive(true, inheritedVelocity);

        if (impulse.sqrMagnitude > 0f)
            ApplyImpulse(impulse, point, hitCollider);

        ragdollActivationTime = Time.time;
        stillTime = 0f;
        ragdollSettled = false;
        ragdolled = true;
    }

    [ContextMenu("Deactivate Ragdoll")]
    public void DeactivateRagdoll()
    {
        if (!ragdolled)
            return;

        SetRagdollBodiesActive(false, Vector3.zero);
        RestoreAttachedTransforms();
        RestoreAnimatedSystems();
        RestoreRootPhysicsState();
        ragdollSettled = false;
        stillTime = 0f;
        ragdolled = false;
    }

    public void ActivateCrippledLimbRagdoll(PlayerCrippledBodyPart bodyPart)
    {
        if (!ShouldActivateCrippledLimbRagdoll(bodyPart))
            return;

        if (IsCrippledLimbBodyPartActive(bodyPart))
            return;

        if (!built)
            BuildRagdoll();

        string[] boneNames = ResolveCrippledLimbBoneNames(bodyPart);
        if (boneNames == null || boneNames.Length == 0)
            return;

        bool wasActive = crippledLimbRagdollActive;
        if (!wasActive)
            CaptureCurrentPoseAsRagdollTargets();

        Vector3 inheritedVelocity = rootRigidbody
            ? rootRigidbody.linearVelocity * Mathf.Max(0f, crippledLimbInheritedVelocityMultiplier)
            : Vector3.zero;

        for (int i = 0; i < boneNames.Length; i++)
        {
            if (!runtimeBonesByName.TryGetValue(boneNames[i], out RuntimeBone runtimeBone) || runtimeBone == null)
                continue;

            ActivateCrippledLimbBone(runtimeBone, inheritedVelocity);
        }

        if (activeCrippledLimbBones.Count == 0)
            return;

        crippledLimbRagdollActive = true;
        ApplyCrippledLimbActivationKick(bodyPart);
    }

    public void DeactivateCrippledLimbRagdoll()
    {
        DeactivateCrippledLimbRagdoll(true);
    }

    private bool WasTestRagdollPressed()
    {
        if (!enableTestRagdollInput || testRagdollKey == Key.None)
            return false;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        return keyboard[testRagdollKey].wasPressedThisFrame;
    }

    private void CacheRootReferences()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>(true);

        if (!rootRigidbody)
            rootRigidbody = GetComponent<Rigidbody>();

        if (!rootCollider)
            rootCollider = GetComponent<Collider>();
    }

    private void CacheStateReferences()
    {
        if (!npcState)
            npcState = GetComponent<NPCState>();

        if (!npcState)
            npcState = GetComponentInParent<NPCState>();

        if (!npcState)
            npcState = GetComponentInChildren<NPCState>(true);

        if (!playerState)
            playerState = GetComponent<PlayerState>();

        if (!playerState)
            playerState = GetComponentInParent<PlayerState>();

        if (!playerState)
            playerState = GetComponentInChildren<PlayerState>(true);
    }

    private void SubscribeNpcDeathEvent()
    {
        if (!npcState || subscribedNpcState == npcState)
            return;

        UnsubscribeNpcDeathEvent();
        npcState.OnDied += HandleNpcDied;
        npcState.OnResurrected += HandleNpcResurrected;
        subscribedNpcState = npcState;
    }

    private void UnsubscribeNpcDeathEvent()
    {
        if (!subscribedNpcState)
            return;

        subscribedNpcState.OnDied -= HandleNpcDied;
        subscribedNpcState.OnResurrected -= HandleNpcResurrected;
        subscribedNpcState = null;
    }

    private void SubscribePlayerDeathEvent()
    {
        if (!playerState || subscribedPlayerState == playerState)
            return;

        UnsubscribePlayerDeathEvent();
        playerState.OnDied += HandlePlayerDied;
        playerState.OnBodyPartCrippled += HandlePlayerBodyPartCrippled;
        subscribedPlayerState = playerState;
    }

    private void UnsubscribePlayerDeathEvent()
    {
        if (!subscribedPlayerState)
            return;

        subscribedPlayerState.OnDied -= HandlePlayerDied;
        subscribedPlayerState.OnBodyPartCrippled -= HandlePlayerBodyPartCrippled;
        subscribedPlayerState = null;
    }

    private void HandleNpcDied(NPCState deadNpcState)
    {
        if (!ragdollWhenHealthDepleted || deadNpcState != npcState)
            return;

        ActivateRagdoll();
    }

    private void HandleNpcResurrected(NPCState resurrectedNpcState)
    {
        if (resurrectedNpcState != npcState)
            return;

        DeactivateRagdoll();
    }

    private void HandlePlayerDied(PlayerState deadPlayerState)
    {
        if (!ragdollWhenHealthDepleted || deadPlayerState != playerState)
            return;

        ActivateRagdoll();
    }

    private void HandlePlayerBodyPartCrippled(PlayerState crippledPlayerState, PlayerCrippledBodyPart bodyPart)
    {
        if (crippledPlayerState != playerState)
            return;

        SyncCrippledLimbRagdollToPlayerState();
    }

    private void ActivateRagdollIfHealthDepleted()
    {
        if (ragdolled || !ragdollWhenHealthDepleted)
            return;

        if (TryResolveHealth(out float health) && health <= 0f)
            ActivateRagdoll();
    }

    private void SyncCrippledLimbRagdollToPlayerState()
    {
        if (!enableCrippledLimbRagdoll || !playerState)
        {
            DeactivateCrippledLimbRagdoll(false);
            hasPlayerCrippledStateCache = false;
            return;
        }

        bool leftLegCrippled = playerState.GetLeftLegCrippled();
        bool rightLegCrippled = playerState.GetRightLegCrippled();
        bool hasCrippledLimb = leftLegCrippled || rightLegCrippled;

        if (TryResolveHealth(out float health) && health <= 0f)
        {
            DeactivateCrippledLimbRagdoll(false);
            return;
        }

        if (!hasCrippledLimb)
        {
            DeactivateCrippledLimbRagdoll(false);
            CachePlayerCrippledStates(leftLegCrippled, rightLegCrippled);
            return;
        }

        bool changed = !hasPlayerCrippledStateCache
            || cachedPlayerLeftLegCrippled != leftLegCrippled
            || cachedPlayerRightLegCrippled != rightLegCrippled;

        if (!changed && crippledLimbRagdollActive)
            return;

        RebuildCrippledLimbRagdoll(leftLegCrippled, rightLegCrippled);
        CachePlayerCrippledStates(leftLegCrippled, rightLegCrippled);
    }

    private void RebuildCrippledLimbRagdoll(bool leftLegCrippled, bool rightLegCrippled)
    {
        DeactivateCrippledLimbRagdoll(false);

        if (!built)
            BuildRagdoll();

        CaptureCurrentPoseAsRagdollTargets();

        if (leftLegCrippled)
            ActivateCrippledLimbRagdoll(PlayerCrippledBodyPart.LeftLeg);

        if (rightLegCrippled)
            ActivateCrippledLimbRagdoll(PlayerCrippledBodyPart.RightLeg);
    }

    private void CachePlayerCrippledStates()
    {
        if (!playerState)
        {
            hasPlayerCrippledStateCache = false;
            return;
        }

        CachePlayerCrippledStates(
            playerState.GetLeftLegCrippled(),
            playerState.GetRightLegCrippled());
    }

    private void CachePlayerCrippledStates(bool leftLegCrippled, bool rightLegCrippled)
    {
        cachedPlayerLeftLegCrippled = leftLegCrippled;
        cachedPlayerRightLegCrippled = rightLegCrippled;
        hasPlayerCrippledStateCache = true;
    }

    private bool ShouldActivateCrippledLimbRagdoll(PlayerCrippledBodyPart bodyPart)
    {
        if (!enableCrippledLimbRagdoll || ragdolled || !IsCrippledLimbRagdollBodyPart(bodyPart))
            return false;

        if (TryResolveHealth(out float health) && health <= 0f)
            return false;

        return true;
    }

    private static bool IsCrippledLimbRagdollBodyPart(PlayerCrippledBodyPart bodyPart)
    {
        return bodyPart == PlayerCrippledBodyPart.LeftLeg
            || bodyPart == PlayerCrippledBodyPart.RightLeg;
    }

    private static string[] ResolveCrippledLimbBoneNames(PlayerCrippledBodyPart bodyPart)
    {
        switch (bodyPart)
        {
            case PlayerCrippledBodyPart.LeftLeg:
                return LeftLegCrippledLimbBones;
            case PlayerCrippledBodyPart.RightLeg:
                return RightLegCrippledLimbBones;
            default:
                return null;
        }
    }

    private bool IsCrippledLimbBodyPartActive(PlayerCrippledBodyPart bodyPart)
    {
        string[] boneNames = ResolveCrippledLimbBoneNames(bodyPart);
        if (boneNames == null || boneNames.Length == 0)
            return false;

        for (int i = 0; i < boneNames.Length; i++)
        {
            if (!runtimeBonesByName.TryGetValue(boneNames[i], out RuntimeBone runtimeBone) || runtimeBone == null)
                continue;

            if (activeCrippledLimbBones.Contains(runtimeBone))
                return true;
        }

        return false;
    }

    private void ActivateCrippledLimbBone(RuntimeBone runtimeBone, Vector3 inheritedVelocity)
    {
        Rigidbody body = runtimeBone.body;
        if (!body)
            return;

        if (activeCrippledLimbBones.Contains(runtimeBone))
            return;

        body.isKinematic = false;
        body.useGravity = true;
        body.detectCollisions = true;
        body.linearDamping = Mathf.Max(0f, crippledLimbLinearDamping);
        body.angularDamping = Mathf.Max(0f, crippledLimbAngularDamping);
        body.maxAngularVelocity = Mathf.Max(0.1f, crippledLimbMaxAngularVelocity);
        body.linearVelocity = inheritedVelocity;
        body.angularVelocity = Vector3.zero;
        runtimeBone.hasCrippledLimbVisualRotation = false;

        IgnoreRootCollisionForCrippledLimb(runtimeBone, true);
        activeCrippledLimbBones.Add(runtimeBone);
    }

    private void DeactivateCrippledLimbRagdoll(bool cacheCrippledStates)
    {
        if (!crippledLimbRagdollActive && activeCrippledLimbBones.Count == 0 && rootIgnoredCrippledLimbColliders.Count == 0)
        {
            if (cacheCrippledStates)
                CachePlayerCrippledStates();

            return;
        }

        RestoreRootCollisionForCrippledLimb();

        for (int i = 0; i < activeCrippledLimbBones.Count; i++)
        {
            RuntimeBone runtimeBone = activeCrippledLimbBones[i];
            Rigidbody body = runtimeBone != null ? runtimeBone.body : null;
            if (!body)
                continue;

            ClearVelocityIfDynamic(body);
            body.linearDamping = Mathf.Max(0f, boneLinearDamping);
            body.angularDamping = Mathf.Max(0f, boneAngularDamping);
            body.maxAngularVelocity = Mathf.Max(0.1f, maxBoneAngularVelocity);
            body.isKinematic = true;
            body.useGravity = false;
            body.detectCollisions = false;
            runtimeBone.hasCrippledLimbVisualRotation = false;
        }

        activeCrippledLimbBones.Clear();
        crippledLimbRagdollActive = false;

        if (cacheCrippledStates)
            CachePlayerCrippledStates();
    }

    private void ApplyCrippledLimbAnimationFailure()
    {
        if (activeCrippledLimbBones.Count == 0)
            return;

        UpdateCrippledLimbAnimatedPoseTargets();

        float followStep = 1f - Mathf.Exp(-Mathf.Max(0f, crippledLimbVisualFollowSpeed) * Time.deltaTime);
        float visualWeight = Mathf.Clamp01(crippledLimbVisualWeight);
        for (int i = 0; i < activeCrippledLimbBones.Count; i++)
        {
            RuntimeBone runtimeBone = activeCrippledLimbBones[i];
            if (runtimeBone == null || !runtimeBone.transform)
                continue;

            Quaternion animatedRotation = runtimeBone.transform.rotation;
            Quaternion failedRotation = ResolveCrippledLimbFailedRotation(runtimeBone, animatedRotation);

            if (!runtimeBone.hasCrippledLimbVisualRotation)
            {
                runtimeBone.crippledLimbVisualRotation = animatedRotation;
                runtimeBone.hasCrippledLimbVisualRotation = true;
            }

            runtimeBone.crippledLimbVisualRotation = Quaternion.Slerp(
                runtimeBone.crippledLimbVisualRotation,
                failedRotation,
                followStep);

            Quaternion visualRotation = Quaternion.Slerp(animatedRotation, runtimeBone.crippledLimbVisualRotation, visualWeight);
            runtimeBone.transform.rotation = visualRotation;

            Rigidbody body = runtimeBone.body;
            if (body && !body.isKinematic)
            {
                body.rotation = visualRotation;
                body.angularVelocity *= 0.92f;
            }
        }
    }

    private void UpdateCrippledLimbAnimatedPoseTargets()
    {
        for (int i = 0; i < activeCrippledLimbBones.Count; i++)
        {
            RuntimeBone runtimeBone = activeCrippledLimbBones[i];
            RuntimeBone connectedBone = runtimeBone != null ? runtimeBone.connectedBone : null;
            if (runtimeBone == null || connectedBone == null || !runtimeBone.transform || !connectedBone.transform)
                continue;

            runtimeBone.targetRotationFromConnected = ResolveRotationFromConnected(runtimeBone, connectedBone);
        }
    }

    private Quaternion ResolveCrippledLimbFailedRotation(RuntimeBone runtimeBone, Quaternion animatedRotation)
    {
        if (runtimeBone == null || runtimeBone.setup == null)
            return animatedRotation;

        string boneName = runtimeBone.setup.boneName;
        if (string.IsNullOrWhiteSpace(boneName))
            return animatedRotation;

        Vector3 forwardAxis = transform.forward.sqrMagnitude > 0.0001f ? transform.forward.normalized : Vector3.forward;
        Vector3 rightAxis = transform.right.sqrMagnitude > 0.0001f ? transform.right.normalized : Vector3.right;

        if (boneName.IndexOf("thigh.L", StringComparison.OrdinalIgnoreCase) >= 0)
            return Quaternion.AngleAxis(-crippledLegThighDragDegrees, rightAxis) * animatedRotation;

        if (boneName.IndexOf("shin.L", StringComparison.OrdinalIgnoreCase) >= 0)
            return Quaternion.AngleAxis(crippledLegShinBendDegrees, rightAxis) * animatedRotation;

        if (boneName.IndexOf("foot.L", StringComparison.OrdinalIgnoreCase) >= 0)
            return Quaternion.AngleAxis(-crippledLegFootDropDegrees, rightAxis) * animatedRotation;

        if (boneName.IndexOf("thigh.R", StringComparison.OrdinalIgnoreCase) >= 0)
            return Quaternion.AngleAxis(-crippledLegThighDragDegrees, rightAxis) * animatedRotation;

        if (boneName.IndexOf("shin.R", StringComparison.OrdinalIgnoreCase) >= 0)
            return Quaternion.AngleAxis(crippledLegShinBendDegrees, rightAxis) * animatedRotation;

        if (boneName.IndexOf("foot.R", StringComparison.OrdinalIgnoreCase) >= 0)
            return Quaternion.AngleAxis(-crippledLegFootDropDegrees, rightAxis) * animatedRotation;

        return animatedRotation;
    }

    private void IgnoreRootCollisionForCrippledLimb(RuntimeBone runtimeBone, bool ignore)
    {
        if (!rootCollider || runtimeBone?.colliders == null)
            return;

        for (int i = 0; i < runtimeBone.colliders.Length; i++)
        {
            Collider boneCollider = runtimeBone.colliders[i];
            if (!boneCollider || boneCollider == rootCollider)
                continue;

            Physics.IgnoreCollision(rootCollider, boneCollider, ignore);
            if (ignore && !rootIgnoredCrippledLimbColliders.Contains(boneCollider))
                rootIgnoredCrippledLimbColliders.Add(boneCollider);
        }
    }

    private void RestoreRootCollisionForCrippledLimb()
    {
        if (rootCollider)
        {
            for (int i = 0; i < rootIgnoredCrippledLimbColliders.Count; i++)
            {
                Collider boneCollider = rootIgnoredCrippledLimbColliders[i];
                if (boneCollider)
                    Physics.IgnoreCollision(rootCollider, boneCollider, false);
            }
        }

        rootIgnoredCrippledLimbColliders.Clear();
    }

    private void ApplyCrippledLimbActivationKick(PlayerCrippledBodyPart bodyPart)
    {
        Rigidbody targetBody = ResolveCrippledLimbKickBody(bodyPart);
        if (!targetBody)
            return;

        Vector3 kick = (-transform.up * Mathf.Max(0f, crippledLimbDownwardVelocityKick))
            + (-transform.forward * Mathf.Max(0f, crippledLimbBackwardVelocityKick));

        if (kick.sqrMagnitude > 0f)
            targetBody.AddForce(kick, ForceMode.VelocityChange);
    }

    private Rigidbody ResolveCrippledLimbKickBody(PlayerCrippledBodyPart bodyPart)
    {
        string boneName;
        switch (bodyPart)
        {
            case PlayerCrippledBodyPart.LeftLeg:
                boneName = "DEF-foot.L";
                break;
            case PlayerCrippledBodyPart.RightLeg:
                boneName = "DEF-foot.R";
                break;
            default:
                return null;
        }

        return runtimeBonesByName.TryGetValue(boneName, out RuntimeBone runtimeBone) && runtimeBone != null
            ? runtimeBone.body
            : null;
    }

    private Dictionary<string, Transform> BuildTransformLookup()
    {
        Dictionary<string, Transform> transformsByName = new Dictionary<string, Transform>(StringComparer.Ordinal);
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (!candidate || transformsByName.ContainsKey(candidate.name))
                continue;

            transformsByName.Add(candidate.name, candidate);
        }

        return transformsByName;
    }

    private void ConfigureBody(Rigidbody body, float mass)
    {
        body.mass = mass;
        body.linearDamping = Mathf.Max(0f, boneLinearDamping);
        body.angularDamping = Mathf.Max(0f, boneAngularDamping);
        body.maxAngularVelocity = Mathf.Max(0.1f, maxBoneAngularVelocity);
        body.solverIterations = Mathf.Max(6, boneSolverIterations);
        body.solverVelocityIterations = Mathf.Max(1, boneSolverVelocityIterations);
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        body.useGravity = false;
        body.isKinematic = true;
    }

    private void DistributeRootMassAcrossRagdollBodies()
    {
        if (!rootRigidbody)
            return;

        float totalWeight = 0f;
        foreach (RuntimeBone runtimeBone in runtimeBonesByName.Values)
        {
            if (runtimeBone?.setup == null || !runtimeBone.body)
                continue;

            totalWeight += Mathf.Max(0f, runtimeBone.setup.mass);
        }

        if (totalWeight <= 0f)
            return;

        float rootMass = Mathf.Max(MinimumBodyMass, rootRigidbody.mass);
        foreach (RuntimeBone runtimeBone in runtimeBonesByName.Values)
        {
            if (runtimeBone?.setup == null || !runtimeBone.body)
                continue;

            float weight = Mathf.Max(0f, runtimeBone.setup.mass);
            float distributedMass = rootMass * (weight / totalWeight);
            runtimeBone.body.mass = Mathf.Max(MinimumBodyMass, distributedMass);
        }
    }

    private Collider CreateDefaultBoneCollider(Transform boneTransform)
    {
        if (!createMissingBoneColliders)
            return null;

        SphereCollider collider = boneTransform.gameObject.AddComponent<SphereCollider>();
        collider.radius = Mathf.Max(0.01f, defaultBoneColliderRadius);
        return collider;
    }

    private void AttachConfiguredTransformsToRagdollBones()
    {
        Dictionary<string, Transform> transformsByName = BuildTransformLookup();
        AttachTransformsToBone(transformsByName, headRagdollBoneName, transformsToAttachToHeadOnRagdoll);
        AttachTransformsToBone(transformsByName, chestRagdollBoneName, transformsToAttachToChestOnRagdoll);
    }

    private void AttachTransformsToBone(Dictionary<string, Transform> transformsByName, string ragdollBoneName, string[] transformNames)
    {
        if (transformsByName == null || string.IsNullOrWhiteSpace(ragdollBoneName) || transformNames == null)
            return;

        if (!transformsByName.TryGetValue(ragdollBoneName, out Transform parentTransform) || !parentTransform)
            return;

        for (int i = 0; i < transformNames.Length; i++)
        {
            string transformName = transformNames[i];
            if (string.IsNullOrWhiteSpace(transformName))
                continue;

            if (!transformsByName.TryGetValue(transformName, out Transform target) || !target || target == parentTransform)
                continue;

            if (target.IsChildOf(parentTransform))
                continue;

            CacheAttachedTransformState(target);
            target.SetParent(parentTransform, true);
        }
    }

    private void CacheAttachedTransformState(Transform target)
    {
        if (!target)
            return;

        for (int i = 0; i < attachedTransformsForRagdoll.Count; i++)
        {
            if (attachedTransformsForRagdoll[i].Transform == target)
                return;
        }

        attachedTransformsForRagdoll.Add(new AttachedTransformState(target));
    }

    private void RestoreAttachedTransforms()
    {
        for (int i = attachedTransformsForRagdoll.Count - 1; i >= 0; i--)
        {
            AttachedTransformState state = attachedTransformsForRagdoll[i];
            Transform target = state.Transform;
            if (!target)
                continue;

            target.SetParent(state.Parent, false);
            target.SetSiblingIndex(Mathf.Clamp(state.SiblingIndex, 0, target.parent ? target.parent.childCount - 1 : state.SiblingIndex));
            target.localPosition = state.LocalPosition;
            target.localRotation = state.LocalRotation;
            target.localScale = state.LocalScale;
        }

        attachedTransformsForRagdoll.Clear();
    }

    private void ConfigureJoint(RuntimeBone runtimeBone)
    {
        RagdollBone setup = runtimeBone.setup;
        if (string.IsNullOrWhiteSpace(setup.connectedBoneName))
            return;

        if (!runtimeBonesByName.TryGetValue(setup.connectedBoneName, out RuntimeBone connectedBone) || connectedBone.body == null)
        {
            Debug.LogWarning($"{name}: ragdoll bone '{setup.boneName}' could not connect to '{setup.connectedBoneName}'.", runtimeBone.transform);
            return;
        }

        CharacterJoint joint = runtimeBone.transform.GetComponent<CharacterJoint>();
        if (!joint)
            joint = runtimeBone.transform.gameObject.AddComponent<CharacterJoint>();

        joint.connectedBody = connectedBone.body;
        joint.autoConfigureConnectedAnchor = false;
        joint.anchor = Vector3.zero;
        joint.connectedAnchor = connectedBone.transform.InverseTransformPoint(runtimeBone.transform.position);
        joint.enableCollision = false;
        joint.enablePreprocessing = true;
        joint.enableProjection = enableJointProjection;
        joint.projectionDistance = Mathf.Max(0.001f, jointProjectionDistance);
        joint.projectionAngle = Mathf.Max(1f, jointProjectionAngle);

        ResolveJointAxes(runtimeBone, connectedBone, out Vector3 axis, out Vector3 swingAxis);
        joint.axis = axis;
        joint.swingAxis = swingAxis;
        SoftJointLimitSpring limitSpring = CreateLimitSpring();
        joint.twistLimitSpring = limitSpring;
        joint.swingLimitSpring = limitSpring;
        joint.lowTwistLimit = CreateLimit(setup.lowTwistLimit);
        joint.highTwistLimit = CreateLimit(setup.highTwistLimit);
        joint.swing1Limit = CreateLimit(setup.swing1Limit);
        joint.swing2Limit = CreateLimit(setup.swing2Limit);
        runtimeBone.connectedBone = connectedBone;
        runtimeBone.targetRotationFromConnected = ResolveRotationFromConnected(runtimeBone, connectedBone);
        runtimeBone.joint = joint;
    }

    private void ResolveJointAxes(RuntimeBone runtimeBone, RuntimeBone connectedBone, out Vector3 axis, out Vector3 swingAxis)
    {
        if (!alignJointAxesToBindPose)
        {
            axis = Vector3.right;
            swingAxis = Vector3.up;
            return;
        }

        Transform boneTransform = runtimeBone.transform;
        Vector3 boneDirection = ResolveBoneDirection(runtimeBone, connectedBone);
        if (IsHingeLikeBone(runtimeBone.setup.boneName))
        {
            Vector3 hingeAxis = Vector3.ProjectOnPlane(transform.right, boneDirection);
            if (hingeAxis.sqrMagnitude < 0.0001f)
                hingeAxis = Vector3.ProjectOnPlane(transform.forward, boneDirection);

            if (hingeAxis.sqrMagnitude < 0.0001f)
                hingeAxis = Vector3.Cross(boneDirection, Vector3.up);

            hingeAxis.Normalize();
            axis = ToLocalNormalizedDirection(boneTransform, hingeAxis, Vector3.right);
            swingAxis = ToLocalNormalizedDirection(boneTransform, boneDirection, Vector3.up);
            return;
        }

        Vector3 bindSwingAxis = Vector3.ProjectOnPlane(transform.up, boneDirection);
        if (bindSwingAxis.sqrMagnitude < 0.0001f)
            bindSwingAxis = Vector3.ProjectOnPlane(transform.forward, boneDirection);

        axis = ToLocalNormalizedDirection(boneTransform, boneDirection, Vector3.right);
        swingAxis = ToLocalNormalizedDirection(boneTransform, bindSwingAxis, Vector3.up);
    }

    private Vector3 ResolveBoneDirection(RuntimeBone runtimeBone, RuntimeBone connectedBone)
    {
        Vector3 direction = runtimeBone.transform.position - connectedBone.transform.position;
        if (direction.sqrMagnitude > 0.0001f)
            return direction.normalized;

        Vector3 childDirection = ResolveFirstChildDirection(runtimeBone.transform);
        if (childDirection.sqrMagnitude > 0.0001f)
            return childDirection.normalized;

        return runtimeBone.transform.forward;
    }

    private static Vector3 ResolveFirstChildDirection(Transform transform)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);
            if (!child)
                continue;

            Vector3 direction = child.position - transform.position;
            if (direction.sqrMagnitude > 0.0001f)
                return direction;
        }

        return Vector3.zero;
    }

    private static Vector3 ToLocalNormalizedDirection(Transform transform, Vector3 worldDirection, Vector3 fallback)
    {
        if (worldDirection.sqrMagnitude < 0.0001f)
            return fallback;

        Vector3 localDirection = transform.InverseTransformDirection(worldDirection.normalized);
        if (localDirection.sqrMagnitude < 0.0001f)
            return fallback;

        return localDirection.normalized;
    }

    private static bool IsHingeLikeBone(string boneName)
    {
        if (string.IsNullOrWhiteSpace(boneName))
            return false;

        return boneName.IndexOf("shin", StringComparison.OrdinalIgnoreCase) >= 0
            || boneName.IndexOf("forearm", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private SoftJointLimit CreateLimit(float value)
    {
        SoftJointLimit limit = new SoftJointLimit();
        limit.limit = value;
        limit.bounciness = 0f;
        limit.contactDistance = Mathf.Max(0f, jointLimitContactDistance);
        return limit;
    }

    private SoftJointLimitSpring CreateLimitSpring()
    {
        SoftJointLimitSpring spring = new SoftJointLimitSpring();
        spring.spring = Mathf.Max(0f, jointLimitSpring);
        spring.damper = Mathf.Max(0f, jointLimitDamper);
        return spring;
    }

    private void CaptureCurrentPoseAsRagdollTargets()
    {
        if (!preserveRagdollActivationPose)
            return;

        foreach (RuntimeBone runtimeBone in runtimeBonesByName.Values)
        {
            RuntimeBone connectedBone = runtimeBone.connectedBone;
            if (connectedBone == null || !connectedBone.transform || !runtimeBone.transform)
                continue;

            runtimeBone.targetRotationFromConnected = ResolveRotationFromConnected(runtimeBone, connectedBone);
        }
    }

    private static Quaternion ResolveRotationFromConnected(RuntimeBone runtimeBone, RuntimeBone connectedBone)
    {
        return Quaternion.Inverse(connectedBone.transform.rotation) * runtimeBone.transform.rotation;
    }

    private void CachePreRagdollState()
    {
        behavioursDisabledForRagdoll.Clear();
        cachedAnimatorEnabled = animator && animator.enabled;
        cachedRootColliderEnabled = rootCollider && rootCollider.enabled;

        if (rootRigidbody)
        {
            cachedRootRigidbodyIsKinematic = rootRigidbody.isKinematic;
            cachedRootRigidbodyUseGravity = rootRigidbody.useGravity;
            cachedRootRigidbodyDetectCollisions = rootRigidbody.detectCollisions;
        }

        hasRagdollStateCache = true;
    }

    private void DisableAnimatedSystems()
    {
        if (animator)
            animator.enabled = false;

        DisableConfiguredBehaviours();
        DisableBehavioursByTypeName();
    }

    private void DisableConfiguredBehaviours()
    {
        if (behavioursToDisable == null)
            return;

        for (int i = 0; i < behavioursToDisable.Length; i++)
        {
            MonoBehaviour behaviour = behavioursToDisable[i];
            if (!behaviour || behaviour == this)
                continue;

            DisableBehaviourForRagdoll(behaviour);
        }
    }

    private void DisableBehavioursByTypeName()
    {
        if (behaviourTypeNamesToDisable == null || behaviourTypeNamesToDisable.Length == 0)
            return;

        Behaviour[] behaviours = GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour behaviour = behaviours[i];
            if (!behaviour || behaviour == this)
                continue;

            Type type = behaviour.GetType();
            if (ShouldDisableType(type))
                DisableBehaviourForRagdoll(behaviour);
        }
    }

    private void DisableBehaviourForRagdoll(Behaviour behaviour)
    {
        if (!behaviour || !behaviour.enabled)
            return;

        if (!behavioursDisabledForRagdoll.Contains(behaviour))
            behavioursDisabledForRagdoll.Add(behaviour);

        behaviour.enabled = false;
    }

    private void RestoreAnimatedSystems()
    {
        if (!hasRagdollStateCache)
            return;

        if (animator && cachedAnimatorEnabled)
        {
            animator.enabled = true;
            animator.Rebind();
            animator.Update(0f);
        }

        for (int i = behavioursDisabledForRagdoll.Count - 1; i >= 0; i--)
        {
            Behaviour behaviour = behavioursDisabledForRagdoll[i];
            if (behaviour)
                behaviour.enabled = true;
        }

        behavioursDisabledForRagdoll.Clear();
    }

    private bool ShouldDisableType(Type type)
    {
        if (type == null)
            return false;

        for (int i = 0; i < behaviourTypeNamesToDisable.Length; i++)
        {
            string typeName = behaviourTypeNamesToDisable[i];
            if (string.IsNullOrWhiteSpace(typeName))
                continue;

            if (type.Name == typeName || type.FullName == typeName)
                return true;
        }

        return false;
    }

    private void SetRootPhysicsActive(bool active)
    {
        if (rootCollider && disableRootColliderOnRagdoll)
            rootCollider.enabled = active;

        if (rootRigidbody && disableRootRigidbodyOnRagdoll)
        {
            if (!active)
                ClearVelocityIfDynamic(rootRigidbody);

            rootRigidbody.isKinematic = !active;
            rootRigidbody.useGravity = active;
            rootRigidbody.detectCollisions = active;
        }
    }

    private void RestoreRootPhysicsState()
    {
        if (!hasRagdollStateCache)
            return;

        if (rootCollider && disableRootColliderOnRagdoll)
            rootCollider.enabled = cachedRootColliderEnabled;

        if (rootRigidbody && disableRootRigidbodyOnRagdoll)
        {
            rootRigidbody.isKinematic = cachedRootRigidbodyIsKinematic;
            rootRigidbody.useGravity = cachedRootRigidbodyUseGravity;
            rootRigidbody.detectCollisions = cachedRootRigidbodyDetectCollisions;

            if (!rootRigidbody.isKinematic)
            {
                rootRigidbody.linearVelocity = Vector3.zero;
                rootRigidbody.angularVelocity = Vector3.zero;
            }
        }

        hasRagdollStateCache = false;
    }

    private void SetRagdollBodiesActive(bool active, Vector3 inheritedVelocity)
    {
        foreach (RuntimeBone runtimeBone in runtimeBonesByName.Values)
        {
            Rigidbody body = runtimeBone.body;
            if (!body)
                continue;

            if (active)
            {
                body.isKinematic = false;
                body.useGravity = true;
                body.detectCollisions = true;
                body.linearVelocity = inheritedVelocity;
            }
            else
            {
                ClearVelocityIfDynamic(body);
                body.isKinematic = true;
                body.useGravity = false;
                body.detectCollisions = false;
            }
        }
    }

    private void StabilizeActiveRagdoll()
    {
        UpdateCorpseSettling();
        if (ragdollSettled)
            return;

        ApplyPosePreservationTorques();
        ClampBoneAngularVelocities();
    }

    private void StabilizeCrippledLimbRagdoll()
    {
        ApplyPosePreservationTorques(
            activeCrippledLimbBones,
            Mathf.Max(0f, crippledLimbPoseSpringMultiplier),
            Mathf.Max(0f, crippledLimbPoseDamperMultiplier));
        ClampBoneAngularVelocities(activeCrippledLimbBones, Mathf.Max(0.1f, crippledLimbMaxAngularVelocity));
    }

    private void UpdateCorpseSettling()
    {
        if (!freezeWhenSettled || ragdollSettled)
            return;

        float activeTime = Time.time - ragdollActivationTime;
        if (activeTime < Mathf.Max(0f, minimumSettleTime))
        {
            stillTime = 0f;
            return;
        }

        if (AreRagdollBodiesStill())
            stillTime += Time.fixedDeltaTime;
        else
            stillTime = 0f;

        float maxSettleTime = Mathf.Max(0f, maximumSettleTime);
        if (stillTime >= Mathf.Max(0f, requiredStillTime)
            || (maxSettleTime > 0f && activeTime >= maxSettleTime))
            SetCorpseSettled();
    }

    private bool AreRagdollBodiesStill()
    {
        float linearThreshold = Mathf.Max(0f, settledLinearVelocity);
        float angularThreshold = Mathf.Max(0f, settledAngularVelocity);
        float linearThresholdSqr = linearThreshold * linearThreshold;
        float angularThresholdSqr = angularThreshold * angularThreshold;

        foreach (RuntimeBone runtimeBone in runtimeBonesByName.Values)
        {
            Rigidbody body = runtimeBone.body;
            if (!body || body.isKinematic)
                continue;

            if (body.linearVelocity.sqrMagnitude > linearThresholdSqr)
                return false;

            if (body.angularVelocity.sqrMagnitude > angularThresholdSqr)
                return false;
        }

        return true;
    }

    private void SetCorpseSettled()
    {
        foreach (RuntimeBone runtimeBone in runtimeBonesByName.Values)
        {
            Rigidbody body = runtimeBone.body;
            if (!body || body.isKinematic)
                continue;

            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.detectCollisions = true;
            body.isKinematic = true;
            body.Sleep();
        }

        ragdollSettled = true;
    }

    private void ApplyPosePreservationTorques()
    {
        ApplyPosePreservationTorques(null, 1f, 1f);
    }

    private void ApplyPosePreservationTorques(IReadOnlyList<RuntimeBone> bonesToStabilize, float springScale, float damperScale)
    {
        if (!preserveRagdollActivationPose || poseSpring <= 0f)
            return;

        IEnumerable<RuntimeBone> targetBones = bonesToStabilize != null
            ? (IEnumerable<RuntimeBone>)bonesToStabilize
            : runtimeBonesByName.Values;
        foreach (RuntimeBone runtimeBone in targetBones)
        {
            if (runtimeBone == null)
                continue;

            RuntimeBone connectedBone = runtimeBone.connectedBone;
            Rigidbody body = runtimeBone.body;
            Rigidbody connectedBody = connectedBone != null ? connectedBone.body : null;
            if (!body || body.isKinematic || !connectedBody)
                continue;

            Quaternion targetWorldRotation = connectedBone.transform.rotation * runtimeBone.targetRotationFromConnected;
            Quaternion rotationError = targetWorldRotation * Quaternion.Inverse(runtimeBone.transform.rotation);
            rotationError.ToAngleAxis(out float angle, out Vector3 axis);
            if (angle > 180f)
                angle -= 360f;

            if (Mathf.Abs(angle) < 0.05f || axis.sqrMagnitude < 0.0001f)
                continue;

            float multiplier = ResolvePoseMultiplier(runtimeBone.setup.boneName);
            Vector3 relativeAngularVelocity = body.angularVelocity - connectedBody.angularVelocity;
            Vector3 correction = axis.normalized * (angle * Mathf.Deg2Rad * poseSpring * multiplier * springScale)
                - relativeAngularVelocity * (Mathf.Max(0f, poseDamper) * multiplier * damperScale);

            float maxCorrection = Mathf.Max(0f, maxPoseCorrectionAcceleration);
            if (maxCorrection > 0f && correction.sqrMagnitude > maxCorrection * maxCorrection)
                correction = correction.normalized * maxCorrection;

            body.AddTorque(correction, ForceMode.Acceleration);
            if (!connectedBody.isKinematic)
                connectedBody.AddTorque(-correction * 0.5f, ForceMode.Acceleration);
        }
    }

    private float ResolvePoseMultiplier(string boneName)
    {
        if (string.IsNullOrWhiteSpace(boneName))
            return Mathf.Max(0f, limbPoseMultiplier);

        if (boneName.IndexOf("spine", StringComparison.OrdinalIgnoreCase) >= 0)
            return Mathf.Max(0f, torsoPoseMultiplier);

        if (boneName.IndexOf("neck", StringComparison.OrdinalIgnoreCase) >= 0
            || boneName.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0)
            return Mathf.Max(0f, headPoseMultiplier);

        if (boneName.IndexOf("hand", StringComparison.OrdinalIgnoreCase) >= 0
            || boneName.IndexOf("foot", StringComparison.OrdinalIgnoreCase) >= 0)
            return Mathf.Max(0f, handFootPoseMultiplier);

        return Mathf.Max(0f, limbPoseMultiplier);
    }

    private void ClampBoneAngularVelocities()
    {
        ClampBoneAngularVelocities(null, Mathf.Max(0.1f, maxBoneAngularVelocity));
    }

    private void ClampBoneAngularVelocities(IReadOnlyList<RuntimeBone> bonesToClamp, float maxAngularVelocity)
    {
        float clampedMaxAngularVelocity = Mathf.Max(0.1f, maxAngularVelocity);
        float maxAngularVelocitySqr = clampedMaxAngularVelocity * clampedMaxAngularVelocity;

        IEnumerable<RuntimeBone> targetBones = bonesToClamp != null
            ? (IEnumerable<RuntimeBone>)bonesToClamp
            : runtimeBonesByName.Values;
        foreach (RuntimeBone runtimeBone in targetBones)
        {
            if (runtimeBone == null)
                continue;

            Rigidbody body = runtimeBone.body;
            if (!body || body.isKinematic)
                continue;

            Vector3 angularVelocity = body.angularVelocity;
            if (angularVelocity.sqrMagnitude <= maxAngularVelocitySqr)
                continue;

            body.angularVelocity = angularVelocity.normalized * clampedMaxAngularVelocity;
        }
    }

    private static void ClearVelocityIfDynamic(Rigidbody body)
    {
        if (!body || body.isKinematic)
            return;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private void ApplyImpulse(Vector3 impulse, Vector3 point, Collider hitCollider)
    {
        Rigidbody targetBody = hitCollider ? hitCollider.GetComponentInParent<Rigidbody>() : null;
        if (!targetBody || !IsRagdollBody(targetBody))
            targetBody = FindClosestRagdollBody(point);

        if (targetBody)
            targetBody.AddForceAtPosition(impulse, point, ForceMode.Impulse);
    }

    private bool IsRagdollBody(Rigidbody body)
    {
        if (!body)
            return false;

        foreach (RuntimeBone runtimeBone in runtimeBonesByName.Values)
        {
            if (runtimeBone.body == body)
                return true;
        }

        return false;
    }

    private Rigidbody FindClosestRagdollBody(Vector3 point)
    {
        float bestDistance = float.PositiveInfinity;
        Rigidbody bestBody = null;

        foreach (RuntimeBone runtimeBone in runtimeBonesByName.Values)
        {
            Rigidbody body = runtimeBone.body;
            if (!body)
                continue;

            float distance = (body.worldCenterOfMass - point).sqrMagnitude;
            if (distance >= bestDistance)
                continue;

            bestDistance = distance;
            bestBody = body;
        }

        return bestBody;
    }

    private bool TryResolveHealth(out float health)
    {
        health = 0f;

        if (playerState)
        {
            health = playerState.GetHealthPoints();
            return true;
        }

        if (npcState)
        {
            health = npcState.GetHealthPoints();
            return true;
        }

        return false;
    }
}
