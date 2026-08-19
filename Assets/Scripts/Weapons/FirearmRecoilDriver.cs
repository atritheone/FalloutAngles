// imports
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;



// class
[DefaultExecutionOrder(-220)]
public class FirearmRecoilDriver : MonoBehaviour
{
    public enum WeaponCategory
    {
        Unarmed,
        Knife,
        TwoHanded,
        Bow,
        Pistol,
        SubmachineGun,
        Rifle,
        Shotgun,
        Special,
        Explosive
    }

    [Serializable]
    public class FirearmRecoilProfile
    {
        // The category this profile applies to.
        [SerializeField] public WeaponCategory Category = WeaponCategory.Pistol;

        // The weapon name this profile applies to.
        [SerializeField] public string WeaponName;

        // The transform that receives model recoil for this firearm.
        [SerializeField] public Transform RecoilPivot;

        // How far back the weapon model kicks in local space.
        [Min(0f)] [SerializeField] public float KickBackDistance = 0.035f;

        // Percentage jitter applied around kick-back distance (0.10 = +/-10%).
        [Range(0.0f, 0.5f)] [SerializeField] public float KickBackJitterPercent = 0.08f;

        // How much the weapon model rotates upward in degrees.
        [SerializeField] public float KickUpDegrees = 3.0f;

        // Percentage jitter applied around kick-up degrees (0.12 = +/-12%).
        [Range(0.0f, 0.5f)] [SerializeField] public float KickUpJitterPercent = 0.12f;

        // If true, model kick axes are auto-resolved from recoil-pivot orientation.
        // Disabled by default to preserve legacy fixed-axis recoil behavior.
        [SerializeField] public bool UseAutomaticModelKickAxes = false;

        // If true, manual axes are used instead of automatic kick-axis detection.
        [SerializeField] public bool UseManualModelKickAxes = false;

        // Local axis used for model kick-back when manual mode is enabled.
        [SerializeField] public Vector3 ManualKickBackAxisLocal = Vector3.down;

        // Local axis used for model kick-up when manual mode is enabled.
        [SerializeField] public Vector3 ManualKickUpAxisLocal = Vector3.left;

        // If true, upper-body kick is mirrored from KickUpDegrees.
        [SerializeField] public bool MirrorKickIntoUpperBody = true;

        // Multiplier used when mirroring model kick into upper body kick.
        [Min(0f)] [SerializeField] public float UpperBodyMirrorMultiplier = 1.0f;

        // Explicit upper-body kick when mirroring is disabled.
        [SerializeField] public float UpperBodyKickDegrees = 4.0f;
    }


    private sealed class ModelRecoilState
    {
        public Vector3 DefaultLocalPos;
        public Quaternion DefaultLocalRot;
        public Vector3 CurrentPosOffset;
        public Vector3 CurrentRotOffset;
        public Vector3 PosVelocity;
        public Vector3 RotVelocity;
        public bool HasDefaultPose;
        public Vector3 ResolvedKickBackAxisLocal = Vector3.down;
        public Vector3 ResolvedKickUpAxisLocal = Vector3.left;
        public bool HasResolvedKickAxes;
    }

    [Serializable]
    private sealed class ModelRecoilSpringSettings
    {
        // How quickly model recoil snaps back.
        [FormerlySerializedAs("returnSpeed")]
        [SerializeField] public float ModelReturnSpeed = 25.0f;

        // How stiff model recoil feels.
        [FormerlySerializedAs("springStrength")]
        [SerializeField] public float ModelSpringStrength = 120.0f;
    }

    [Serializable]
    private sealed class UpperBodyBonesSettings
    {
        // The spine or chest bone that should take a small portion of recoil.
        [SerializeField] public Transform SpineBone;

        // The right clavicle bone that should absorb recoil.
        [SerializeField] public Transform RightClavicleBone;

        // The right upper arm bone that should absorb recoil.
        [SerializeField] public Transform RightUpperArmBone;

        // The right forearm bone that should absorb recoil.
        [SerializeField] public Transform RightForearmBone;

        // The right hand bone that should absorb recoil.
        [SerializeField] public Transform RightHandBone;

        // The left upper arm bone that should absorb a smaller portion of recoil.
        [SerializeField] public Transform LeftUpperArmBone;

        // The left forearm bone that should absorb a smaller portion of recoil.
        [SerializeField] public Transform LeftForearmBone;
    }

    [Serializable]
    private sealed class UpperBodyDistributionSettings
    {
        // How much kick goes into each upper-body bone (0..1).
        [SerializeField] public float SpineShare = 0.10f;

        // How much kick goes into each upper-body bone (0..1).
        [SerializeField] public float RightClavicleShare = 0.25f;

        // How much kick goes into each upper-body bone (0..1).
        [SerializeField] public float RightUpperArmShare = 0.40f;

        // How much kick goes into each upper-body bone (0..1).
        [SerializeField] public float RightForearmShare = 0.35f;

        // How much kick goes into each upper-body bone (0..1).
        [SerializeField] public float RightHandShare = 0.25f;

        // How much kick goes into each upper-body bone (0..1).
        [SerializeField] public float LeftUpperArmShare = 0.18f;

        // How much kick goes into each upper-body bone (0..1).
        [SerializeField] public float LeftForearmShare = 0.15f;

        // Scales support-hand recoil relative to main kick.
        [SerializeField] public float LeftKickMultiplier = 1.8f;

        // Local axis used to rotate right-side recoil bones.
        [SerializeField] public Vector3 RightKickAxisLocal = Vector3.right;

        // Local axis used to rotate left-side recoil bones.
        [SerializeField] public Vector3 LeftKickAxisLocal = Vector3.right;
    }

    [Serializable]
    private sealed class UpperBodySpringSettings
    {
        // Spring strength for returning upper body recoil to zero.
        [SerializeField] public float UpperBodySpringStrength = 140.0f;

        // Damping for upper body return motion.
        [SerializeField] public float UpperBodyDamping = 28.0f;

        // Delay for support-hand recoil response.
        [SerializeField] public float LeftHandLagSeconds = 0.03f;
    }

    [Serializable]
    private sealed class SettledThresholdSettings
    {
        // Position offset threshold for model recoil settle checks.
        [FormerlySerializedAs("settledPositionThreshold")]
        [Min(0f)] [SerializeField] public float SettledModelPositionThreshold = 0.0005f;

        // Rotation offset threshold for model recoil settle checks.
        [FormerlySerializedAs("settledRotationThreshold")]
        [Min(0f)] [SerializeField] public float SettledModelRotationThreshold = 0.05f;

        // Velocity threshold for model recoil settle checks.
        [FormerlySerializedAs("settledVelocityThreshold")]
        [Min(0f)] [SerializeField] public float SettledModelVelocityThreshold = 0.01f;

        // Angle threshold for upper-body settle checks.
        [Min(0f)] [SerializeField] public float SettledUpperBodyKickThreshold = 0.05f;

        // Velocity threshold for upper-body settle checks.
        [Min(0f)] [SerializeField] public float SettledUpperBodyVelocityThreshold = 0.05f;
    }

    [Serializable]
    private sealed class RuntimePoseEditingSettings
    {
        // If true, manual runtime edits to recoil pivots become the new default recoil pose once settled.
        [SerializeField] public bool CaptureRuntimePivotPoseEdits = true;

        // Minimum local-position delta needed to treat a runtime pivot change as intentional.
        [Min(0f)] [SerializeField] public float RuntimePivotPosePositionCaptureThreshold = 0.0001f;

        // Minimum local-rotation delta (degrees) needed to treat a runtime pivot change as intentional.
        [Min(0f)] [SerializeField] public float RuntimePivotPoseRotationCaptureThreshold = 0.1f;
    }

    [Serializable]
    private sealed class SafetyLimitSettings
    {
        // Safety cap for accumulated model position recoil.
        [FormerlySerializedAs("maxPositionOffset")]
        [Min(0f)] [SerializeField] public float MaxModelPositionOffset = 0.20f;

        // Safety cap for accumulated model rotation recoil.
        [FormerlySerializedAs("maxRotationOffset")]
        [Min(0f)] [SerializeField] public float MaxModelRotationOffset = 45.0f;

        // Safety cap for model spring velocity.
        [FormerlySerializedAs("maxSpringVelocity")]
        [Min(0f)] [SerializeField] public float MaxModelSpringVelocity = 20.0f;

        // Safety cap for main upper-body kick.
        [Min(0f)] [SerializeField] public float MaxMainKickDegrees = 35.0f;

        // Safety cap for support-hand upper-body kick.
        [Min(0f)] [SerializeField] public float MaxLeftKickDegrees = 25.0f;

        // Safety cap for upper-body kick velocity.
        [Min(0f)] [SerializeField] public float MaxKickVelocity = 360.0f;

        // Maximum simulation step for stable integration.
        [Min(0f)] [SerializeField] public float MaxSimulationDeltaTime = 0.0333f;
    }


    // variables
    [Header("References")]
    // Used to resolve the currently equipped weapon and category.
    [FormerlySerializedAs("playerWeaponController")]
    [Tooltip("Assign a PlayerWeaponController or NPCWeaponController.")]
    [SerializeField] private WeaponController weaponController;

    [Header("Per-Firearm Profiles")]
    // Recoil settings for each firearm.
    [SerializeField] private List<FirearmRecoilProfile> firearmProfiles = new List<FirearmRecoilProfile>();

    [SerializeField] private ModelRecoilSpringSettings modelRecoilSpring = new ModelRecoilSpringSettings();

    [SerializeField] private UpperBodyBonesSettings upperBodyBones = new UpperBodyBonesSettings();

    [SerializeField] private UpperBodyDistributionSettings upperBodyDistribution = new UpperBodyDistributionSettings();

    [SerializeField] private UpperBodySpringSettings upperBodySpring = new UpperBodySpringSettings();

    [SerializeField] private SettledThresholdSettings settledThresholds = new SettledThresholdSettings();

    [SerializeField] private RuntimePoseEditingSettings runtimePoseEditing = new RuntimePoseEditingSettings();

    [SerializeField] private SafetyLimitSettings safetyLimits = new SafetyLimitSettings();

    private float modelReturnSpeed => modelRecoilSpring.ModelReturnSpeed;
    private float modelSpringStrength => modelRecoilSpring.ModelSpringStrength;

    private Transform spineBone => upperBodyBones.SpineBone;
    private Transform rightClavicleBone => upperBodyBones.RightClavicleBone;
    private Transform rightUpperArmBone => upperBodyBones.RightUpperArmBone;
    private Transform rightForearmBone => upperBodyBones.RightForearmBone;
    private Transform rightHandBone => upperBodyBones.RightHandBone;
    private Transform leftUpperArmBone => upperBodyBones.LeftUpperArmBone;
    private Transform leftForearmBone => upperBodyBones.LeftForearmBone;

    private float spineShare => upperBodyDistribution.SpineShare;
    private float rightClavicleShare => upperBodyDistribution.RightClavicleShare;
    private float rightUpperArmShare => upperBodyDistribution.RightUpperArmShare;
    private float rightForearmShare => upperBodyDistribution.RightForearmShare;
    private float rightHandShare => upperBodyDistribution.RightHandShare;
    private float leftUpperArmShare => upperBodyDistribution.LeftUpperArmShare;
    private float leftForearmShare => upperBodyDistribution.LeftForearmShare;
    private float leftKickMultiplier => upperBodyDistribution.LeftKickMultiplier;
    private Vector3 rightKickAxisLocal => upperBodyDistribution.RightKickAxisLocal;
    private Vector3 leftKickAxisLocal => upperBodyDistribution.LeftKickAxisLocal;

    private float upperBodySpringStrength => upperBodySpring.UpperBodySpringStrength;
    private float upperBodyDamping => upperBodySpring.UpperBodyDamping;
    private float leftHandLagSeconds => upperBodySpring.LeftHandLagSeconds;

    private float settledModelPositionThreshold => settledThresholds.SettledModelPositionThreshold;
    private float settledModelRotationThreshold => settledThresholds.SettledModelRotationThreshold;
    private float settledModelVelocityThreshold => settledThresholds.SettledModelVelocityThreshold;
    private float settledUpperBodyKickThreshold => settledThresholds.SettledUpperBodyKickThreshold;
    private float settledUpperBodyVelocityThreshold => settledThresholds.SettledUpperBodyVelocityThreshold;

    private bool captureRuntimePivotPoseEdits => runtimePoseEditing.CaptureRuntimePivotPoseEdits;
    private float runtimePivotPosePositionCaptureThreshold => runtimePoseEditing.RuntimePivotPosePositionCaptureThreshold;
    private float runtimePivotPoseRotationCaptureThreshold => runtimePoseEditing.RuntimePivotPoseRotationCaptureThreshold;

    private float maxModelPositionOffset => safetyLimits.MaxModelPositionOffset;
    private float maxModelRotationOffset => safetyLimits.MaxModelRotationOffset;
    private float maxModelSpringVelocity => safetyLimits.MaxModelSpringVelocity;
    private float maxMainKickDegrees => safetyLimits.MaxMainKickDegrees;
    private float maxLeftKickDegrees => safetyLimits.MaxLeftKickDegrees;
    private float maxKickVelocity => safetyLimits.MaxKickVelocity;
    private float maxSimulationDeltaTime => safetyLimits.MaxSimulationDeltaTime;

    // Runtime model recoil state per recoil pivot transform.
    private readonly Dictionary<Transform, ModelRecoilState> modelRecoilStates = new Dictionary<Transform, ModelRecoilState>();
    private static readonly Vector3[] localCardinalAxes =
    {
        Vector3.right,
        Vector3.left,
        Vector3.up,
        Vector3.down,
        Vector3.forward,
        Vector3.back
    };
    private const float minAxisAlignmentForAutoDetection = 0.20f;

    // Cached animated local rotations (we add recoil on top each frame).
    private Quaternion spineAnimatedLocalRot;

    private Quaternion rightClavicleAnimatedLocalRot;

    private Quaternion rightUpperArmAnimatedLocalRot;

    private Quaternion rightForearmAnimatedLocalRot;

    private Quaternion rightHandAnimatedLocalRot;

    private Quaternion leftUpperArmAnimatedLocalRot;

    private Quaternion leftForearmAnimatedLocalRot;

    // Current upper-body recoil offset (degrees) that springs to zero.
    private float currentKick;

    // Current upper-body recoil spring velocity.
    private float kickVelocity;

    // Current support-hand recoil offset (degrees).
    private float currentLeftKick;

    // Current support-hand recoil velocity estimate.
    private float leftKickVelocity;




    // methods
    private void Awake()
    {
        ResolveWeaponController();

        CacheProfileDefaultPoses();
    }


    private void OnValidate()
    {
        ResolveWeaponController();

        CacheProfileDefaultPoses();
        InvalidateResolvedModelKickAxes();
    }


    private void Update()
    {
        float deltaTime = Mathf.Clamp(Time.deltaTime, 0.0f, Mathf.Max(0.0001f, maxSimulationDeltaTime));
        // Run model recoil before Animation Rigging sync so IK sees the kicked firearm this frame.
        UpdateModelRecoil(deltaTime);
    }


    private void LateUpdate()
    {
        float deltaTime = Mathf.Clamp(Time.deltaTime, 0.0f, Mathf.Max(0.0001f, maxSimulationDeltaTime));
        UpdateUpperBodyRecoil(deltaTime);
    }


    public void FireRecoil()
    {
        if (IsCurrentWeaponFirearm() == false)
            return;

        if (TryGetActiveFirearmProfile(out FirearmRecoilProfile profile) == false)
            return;

        float randomizedKickBack = ApplySymmetricJitter(
            profile.KickBackDistance,
            profile.KickBackJitterPercent,
            keepPositive: true);

        float randomizedKickUp = ApplySymmetricJitter(
            profile.KickUpDegrees,
            profile.KickUpJitterPercent,
            keepPositive: false);

        ApplyModelKick(profile, randomizedKickBack, randomizedKickUp);
        ApplyUpperBodyKick(
            randomizedKickUp,
            profile.MirrorKickIntoUpperBody,
            profile.UpperBodyMirrorMultiplier,
            profile.UpperBodyKickDegrees);
    }


    public bool IsRecoilSettled()
    {
        float modelPositionThresholdSqr = settledModelPositionThreshold * settledModelPositionThreshold;
        float modelRotationThresholdSqr = settledModelRotationThreshold * settledModelRotationThreshold;
        float modelVelocityThresholdSqr = settledModelVelocityThreshold * settledModelVelocityThreshold;

        foreach (var pair in modelRecoilStates)
        {
            Transform recoilPivot = pair.Key;
            if (!recoilPivot) continue;

            ModelRecoilState state = pair.Value;
            if (state == null) continue;

            bool positionSettled = state.CurrentPosOffset.sqrMagnitude <= modelPositionThresholdSqr;
            bool rotationSettled = state.CurrentRotOffset.sqrMagnitude <= modelRotationThresholdSqr;
            bool positionVelocitySettled = state.PosVelocity.sqrMagnitude <= modelVelocityThresholdSqr;
            bool rotationVelocitySettled = state.RotVelocity.sqrMagnitude <= modelVelocityThresholdSqr;

            if (!positionSettled || !rotationSettled || !positionVelocitySettled || !rotationVelocitySettled)
                return false;
        }

        bool mainKickSettled = Mathf.Abs(currentKick) <= settledUpperBodyKickThreshold;
        bool leftKickSettled = Mathf.Abs(currentLeftKick) <= settledUpperBodyKickThreshold;
        bool mainVelocitySettled = Mathf.Abs(kickVelocity) <= settledUpperBodyVelocityThreshold;
        bool leftVelocitySettled = Mathf.Abs(leftKickVelocity) <= settledUpperBodyVelocityThreshold;

        return mainKickSettled
               && leftKickSettled
               && mainVelocitySettled
               && leftVelocitySettled;
    }


    private void UpdateModelRecoil(float deltaTime)
    {
        CacheProfileDefaultPoses();

        foreach (var pair in modelRecoilStates)
        {
            Transform recoilPivot = pair.Key;
            if (!recoilPivot) continue;

            ModelRecoilState state = pair.Value;
            if (state == null) continue;

            if (!state.HasDefaultPose)
            {
                state.DefaultLocalPos = recoilPivot.localPosition;
                state.DefaultLocalRot = recoilPivot.localRotation;
                state.HasDefaultPose = true;
            }

            if (!IsFiniteVector3(state.CurrentPosOffset)
                || !IsFiniteVector3(state.CurrentRotOffset)
                || !IsFiniteVector3(state.PosVelocity)
                || !IsFiniteVector3(state.RotVelocity))
            {
                state.CurrentPosOffset = Vector3.zero;
                state.CurrentRotOffset = Vector3.zero;
                state.PosVelocity = Vector3.zero;
                state.RotVelocity = Vector3.zero;
            }

            TryCaptureRuntimePivotPoseEdit(recoilPivot, state);

            state.CurrentPosOffset = SpringVectorToZero(
                state.CurrentPosOffset,
                ref state.PosVelocity,
                modelSpringStrength,
                modelReturnSpeed,
                deltaTime);

            state.CurrentRotOffset = SpringVectorToZero(
                state.CurrentRotOffset,
                ref state.RotVelocity,
                modelSpringStrength,
                modelReturnSpeed,
                deltaTime);

            state.CurrentPosOffset = ClampMagnitudeSafe(state.CurrentPosOffset, maxModelPositionOffset);
            state.CurrentRotOffset = ClampMagnitudeSafe(state.CurrentRotOffset, maxModelRotationOffset);
            state.PosVelocity = ClampMagnitudeSafe(state.PosVelocity, maxModelSpringVelocity);
            state.RotVelocity = ClampMagnitudeSafe(state.RotVelocity, maxModelSpringVelocity);

            Vector3 targetLocalPos = state.DefaultLocalPos + state.CurrentPosOffset;
            Vector3 safeTargetLocalPos = IsFiniteVector3(targetLocalPos) ? targetLocalPos : state.DefaultLocalPos;
            if (recoilPivot.localPosition != safeTargetLocalPos)
                recoilPivot.localPosition = safeTargetLocalPos;

            Quaternion rotKick = Quaternion.Euler(state.CurrentRotOffset);
            Quaternion targetRot = state.DefaultLocalRot * rotKick;
            Quaternion safeTargetRot = IsFiniteQuaternion(targetRot) ? NormalizeSafe(targetRot) : state.DefaultLocalRot;
            if (recoilPivot.localRotation != safeTargetRot)
                recoilPivot.localRotation = safeTargetRot;
        }
    }


    private void TryCaptureRuntimePivotPoseEdit(Transform recoilPivot, ModelRecoilState state)
    {
        if (!Application.isPlaying) return;
        if (!captureRuntimePivotPoseEdits) return;
        if (!recoilPivot || state == null) return;

        if (!IsModelStateSettled(state)) return;

        Vector3 expectedLocalPos = state.DefaultLocalPos + state.CurrentPosOffset;
        Quaternion expectedLocalRot = state.DefaultLocalRot * Quaternion.Euler(state.CurrentRotOffset);

        Vector3 currentLocalPos = recoilPivot.localPosition;
        Quaternion currentLocalRot = recoilPivot.localRotation;

        float positionDeltaSqr = (currentLocalPos - expectedLocalPos).sqrMagnitude;
        float requiredPositionDeltaSqr = runtimePivotPosePositionCaptureThreshold * runtimePivotPosePositionCaptureThreshold;
        float rotationDelta = Quaternion.Angle(currentLocalRot, expectedLocalRot);

        bool hasMeaningfulPositionChange = positionDeltaSqr > requiredPositionDeltaSqr;
        bool hasMeaningfulRotationChange = rotationDelta > runtimePivotPoseRotationCaptureThreshold;
        if (!hasMeaningfulPositionChange && !hasMeaningfulRotationChange)
            return;

        state.DefaultLocalPos = currentLocalPos;
        state.DefaultLocalRot = currentLocalRot;
        state.CurrentPosOffset = Vector3.zero;
        state.CurrentRotOffset = Vector3.zero;
        state.PosVelocity = Vector3.zero;
        state.RotVelocity = Vector3.zero;
        state.HasDefaultPose = true;
        state.HasResolvedKickAxes = false;
    }


    private void UpdateUpperBodyRecoil(float deltaTime)
    {
        if (!IsFiniteFloat(currentKick)
            || !IsFiniteFloat(currentLeftKick)
            || !IsFiniteFloat(kickVelocity)
            || !IsFiniteFloat(leftKickVelocity))
        {
            currentKick = 0.0f;
            currentLeftKick = 0.0f;
            kickVelocity = 0.0f;
            leftKickVelocity = 0.0f;
        }

        CacheAnimatedPoseRotations();

        currentKick = SpringFloatToZero(currentKick, ref kickVelocity, upperBodySpringStrength, upperBodyDamping, deltaTime);

        float previousLeftKick = currentLeftKick;
        currentLeftKick = GetLaggedLeftKick(deltaTime);
        leftKickVelocity = (deltaTime > 0.000001f) ? ((currentLeftKick - previousLeftKick) / deltaTime) : 0.0f;

        currentKick = ClampFloatMagnitude(currentKick, maxMainKickDegrees);
        currentLeftKick = ClampFloatMagnitude(currentLeftKick, maxLeftKickDegrees);
        kickVelocity = ClampFloatMagnitude(kickVelocity, maxKickVelocity);
        leftKickVelocity = ClampFloatMagnitude(leftKickVelocity, maxKickVelocity);

        ApplyUpperBodyRecoilToBones();
    }


    private void ApplyModelKick(FirearmRecoilProfile profile, float kickBackDistance, float kickUpDegrees)
    {
        if (profile == null) return;

        Transform recoilPivot = profile.RecoilPivot;
        if (!recoilPivot) return;

        ModelRecoilState state = GetOrCreateModelState(recoilPivot);
        ResolveModelKickAxes(profile, recoilPivot, state, out Vector3 kickBackAxisLocal, out Vector3 kickUpAxisLocal);

        float safeKickBackDistance = Mathf.Max(0.0f, kickBackDistance);
        state.CurrentPosOffset += kickBackAxisLocal * safeKickBackDistance;

        state.CurrentRotOffset += kickUpAxisLocal * kickUpDegrees;

        state.CurrentPosOffset = ClampMagnitudeSafe(state.CurrentPosOffset, maxModelPositionOffset);
        state.CurrentRotOffset = ClampMagnitudeSafe(state.CurrentRotOffset, maxModelRotationOffset);
    }


    private void ResolveModelKickAxes(
        FirearmRecoilProfile profile,
        Transform recoilPivot,
        ModelRecoilState state,
        out Vector3 kickBackAxisLocal,
        out Vector3 kickUpAxisLocal)
    {
        if (profile == null || !recoilPivot || state == null)
        {
            kickBackAxisLocal = Vector3.down;
            kickUpAxisLocal = Vector3.left;
            return;
        }

        if (profile.UseManualModelKickAxes)
        {
            kickBackAxisLocal = GetSafeAxis(profile.ManualKickBackAxisLocal, Vector3.down);
            kickUpAxisLocal = GetSafeAxis(profile.ManualKickUpAxisLocal, Vector3.left);
            return;
        }

        if (!profile.UseAutomaticModelKickAxes)
        {
            // Legacy behavior: fixed local axes for all weapons.
            kickBackAxisLocal = Vector3.down;
            kickUpAxisLocal = Vector3.left;
            return;
        }

        if (state.HasResolvedKickAxes)
        {
            kickBackAxisLocal = GetSafeAxis(state.ResolvedKickBackAxisLocal, Vector3.down);
            kickUpAxisLocal = GetSafeAxis(state.ResolvedKickUpAxisLocal, Vector3.left);
            return;
        }

        Transform reference = recoilPivot.parent ? recoilPivot.parent : transform;
        Vector3 targetBackWorldDirection = reference ? -reference.forward : Vector3.back;
        Vector3 targetPitchWorldAxis = reference ? reference.right : Vector3.right;
        Vector3 targetUpWorldDirection = reference ? reference.up : Vector3.up;

        Vector3 autoBackAxisLocal = GetBestLocalCardinalAxisForWorldDirection(
            recoilPivot,
            targetBackWorldDirection,
            Vector3.down);

        Vector3 autoKickUpAxisLocal = GetBestLocalCardinalAxisForWorldDirection(
            recoilPivot,
            targetPitchWorldAxis,
            Vector3.left);

        autoKickUpAxisLocal = EnsurePositiveAngleMovesMuzzleUp(
            recoilPivot,
            autoKickUpAxisLocal,
            targetUpWorldDirection);

        state.ResolvedKickBackAxisLocal = GetSafeAxis(autoBackAxisLocal, Vector3.down);
        state.ResolvedKickUpAxisLocal = GetSafeAxis(autoKickUpAxisLocal, Vector3.left);
        state.HasResolvedKickAxes = true;

        kickBackAxisLocal = state.ResolvedKickBackAxisLocal;
        kickUpAxisLocal = state.ResolvedKickUpAxisLocal;
    }


    private void InvalidateResolvedModelKickAxes()
    {
        foreach (var pair in modelRecoilStates)
        {
            ModelRecoilState state = pair.Value;
            if (state == null) continue;
            state.HasResolvedKickAxes = false;
        }
    }


    private static Vector3 GetBestLocalCardinalAxisForWorldDirection(
        Transform recoilPivot,
        Vector3 targetWorldDirection,
        Vector3 fallbackLocalAxis)
    {
        Vector3 safeFallbackLocalAxis = GetSafeAxis(fallbackLocalAxis, Vector3.right);
        if (!recoilPivot) return safeFallbackLocalAxis;
        if (!IsFiniteVector3(targetWorldDirection) || targetWorldDirection.sqrMagnitude <= 0.000001f) return safeFallbackLocalAxis;

        Vector3 safeTargetWorldDirection = targetWorldDirection.normalized;
        float bestAlignment = -2.0f;
        Vector3 bestLocalAxis = safeFallbackLocalAxis;

        for (int i = 0; i < localCardinalAxes.Length; i++)
        {
            Vector3 localAxis = localCardinalAxes[i];
            Vector3 worldAxis = recoilPivot.TransformDirection(localAxis);
            if (!IsFiniteVector3(worldAxis) || worldAxis.sqrMagnitude <= 0.000001f) continue;

            float alignment = Vector3.Dot(worldAxis.normalized, safeTargetWorldDirection);
            if (alignment <= bestAlignment) continue;

            bestAlignment = alignment;
            bestLocalAxis = localAxis;
        }

        if (bestAlignment < minAxisAlignmentForAutoDetection)
            return safeFallbackLocalAxis;

        return bestLocalAxis;
    }


    private static Vector3 EnsurePositiveAngleMovesMuzzleUp(
        Transform recoilPivot,
        Vector3 pitchAxisLocal,
        Vector3 desiredUpWorldDirection)
    {
        Vector3 safePitchAxisLocal = GetSafeAxis(pitchAxisLocal, Vector3.left);
        if (!recoilPivot) return safePitchAxisLocal;

        Vector3 safeUpWorldDirection = (IsFiniteVector3(desiredUpWorldDirection) && desiredUpWorldDirection.sqrMagnitude > 0.000001f)
            ? desiredUpWorldDirection.normalized
            : Vector3.up;

        Vector3 worldPitchAxis = recoilPivot.TransformDirection(safePitchAxisLocal);
        if (!IsFiniteVector3(worldPitchAxis) || worldPitchAxis.sqrMagnitude <= 0.000001f)
            return safePitchAxisLocal;

        Vector3 forwardBefore = recoilPivot.forward;
        Vector3 forwardAfter = Quaternion.AngleAxis(1.0f, worldPitchAxis.normalized) * forwardBefore;
        float upwardDelta = Vector3.Dot(forwardAfter - forwardBefore, safeUpWorldDirection);
        return upwardDelta >= 0.0f ? safePitchAxisLocal : -safePitchAxisLocal;
    }


    private void ApplyUpperBodyKick(
        float kickUpDegrees,
        bool mirrorModelKick,
        float mirrorMultiplier,
        float explicitUpperBodyKick)
    {
        float requestedKick = mirrorModelKick
            ? kickUpDegrees * Mathf.Max(0.0f, mirrorMultiplier)
            : explicitUpperBodyKick;

        currentKick = ClampFloatMagnitude(currentKick + requestedKick, maxMainKickDegrees);
    }


    private static float ApplySymmetricJitter(float baseValue, float jitterPercent, bool keepPositive)
    {
        float safeJitterPercent = Mathf.Clamp(jitterPercent, 0.0f, 0.5f);
        float jitterMultiplier = UnityEngine.Random.Range(1.0f - safeJitterPercent, 1.0f + safeJitterPercent);
        float jitteredValue = baseValue * jitterMultiplier;
        return keepPositive ? Mathf.Max(0.0f, jitteredValue) : jitteredValue;
    }


    private bool TryGetActiveFirearmProfile(out FirearmRecoilProfile profile)
    {
        profile = null;

        if (TryGetCurrentWeaponInfo(out WeaponCategory currentCategory, out string activeWeaponName) == false)
            return false;

        if (!IsFirearmCategory(currentCategory))
            return false;

        // 1) Exact match on category + name.
        for (int i = 0; i < firearmProfiles.Count; i++)
        {
            FirearmRecoilProfile candidate = firearmProfiles[i];
            if (candidate == null) continue;
            if (candidate.Category != currentCategory) continue;
            if (string.IsNullOrWhiteSpace(candidate.WeaponName)) continue;

            if (string.Equals(candidate.WeaponName, activeWeaponName, StringComparison.OrdinalIgnoreCase))
            {
                profile = candidate;
                return true;
            }
        }

        return false;
    }


    private bool IsCurrentWeaponFirearm()
    {
        if (TryGetCurrentWeaponInfo(out WeaponCategory currentCategory, out _) == false)
            return false;

        return IsFirearmCategory(currentCategory);
    }


    private bool TryGetCurrentWeaponInfo(
        out WeaponCategory category,
        out string weaponName)
    {
        category = WeaponCategory.Unarmed;
        weaponName = string.Empty;

        ResolveWeaponController();

        if (!weaponController)
            return false;

        if (Enum.TryParse(weaponController.GetCurrentCategoryName(), out WeaponCategory parsedCategory))
            category = parsedCategory;

        weaponName = weaponController.GetCurrentWeaponName();
        return true;
    }

    private void ResolveWeaponController()
    {
        if (weaponController)
            return;

        weaponController = GetComponentInParent<WeaponController>();
    }


    private void CacheProfileDefaultPoses()
    {
        if (firearmProfiles == null) return;

        for (int i = 0; i < firearmProfiles.Count; i++)
        {
            FirearmRecoilProfile profile = firearmProfiles[i];
            if (profile == null) continue;
            if (!profile.RecoilPivot) continue;

            ModelRecoilState state = GetOrCreateModelState(profile.RecoilPivot);
            if (state.HasDefaultPose) continue;

            state.DefaultLocalPos = profile.RecoilPivot.localPosition;
            state.DefaultLocalRot = profile.RecoilPivot.localRotation;
            state.HasDefaultPose = true;
        }
    }


    private ModelRecoilState GetOrCreateModelState(Transform recoilPivot)
    {
        if (modelRecoilStates.TryGetValue(recoilPivot, out ModelRecoilState state))
            return state;

        state = new ModelRecoilState();
        modelRecoilStates[recoilPivot] = state;
        return state;
    }


    private bool IsModelStateSettled(ModelRecoilState state)
    {
        if (state == null) return true;

        float positionThresholdSqr = settledModelPositionThreshold * settledModelPositionThreshold;
        float rotationThresholdSqr = settledModelRotationThreshold * settledModelRotationThreshold;
        float velocityThresholdSqr = settledModelVelocityThreshold * settledModelVelocityThreshold;

        bool positionSettled = state.CurrentPosOffset.sqrMagnitude <= positionThresholdSqr;
        bool rotationSettled = state.CurrentRotOffset.sqrMagnitude <= rotationThresholdSqr;
        bool positionVelocitySettled = state.PosVelocity.sqrMagnitude <= velocityThresholdSqr;
        bool rotationVelocitySettled = state.RotVelocity.sqrMagnitude <= velocityThresholdSqr;

        return positionSettled && rotationSettled && positionVelocitySettled && rotationVelocitySettled;
    }


    private void CacheAnimatedPoseRotations()
    {
        if (spineBone) spineAnimatedLocalRot = GetSafeLocalRotation(spineBone);
        if (rightClavicleBone) rightClavicleAnimatedLocalRot = GetSafeLocalRotation(rightClavicleBone);
        if (rightUpperArmBone) rightUpperArmAnimatedLocalRot = GetSafeLocalRotation(rightUpperArmBone);
        if (rightForearmBone) rightForearmAnimatedLocalRot = GetSafeLocalRotation(rightForearmBone);
        if (rightHandBone) rightHandAnimatedLocalRot = GetSafeLocalRotation(rightHandBone);
        if (leftUpperArmBone) leftUpperArmAnimatedLocalRot = GetSafeLocalRotation(leftUpperArmBone);
        if (leftForearmBone) leftForearmAnimatedLocalRot = GetSafeLocalRotation(leftForearmBone);
    }


    private void ApplyUpperBodyRecoilToBones()
    {
        Vector3 safeRightAxis = GetSafeAxis(rightKickAxisLocal, Vector3.right);
        Vector3 safeLeftAxis = GetSafeAxis(leftKickAxisLocal, Vector3.right);

        float clampedSpineShare = Mathf.Clamp01(spineShare);
        float clampedRightClavicleShare = Mathf.Clamp01(rightClavicleShare);
        float clampedRightUpperArmShare = Mathf.Clamp01(rightUpperArmShare);
        float clampedRightForearmShare = Mathf.Clamp01(rightForearmShare);
        float clampedRightHandShare = Mathf.Clamp01(rightHandShare);
        float clampedLeftUpperArmShare = Mathf.Clamp01(leftUpperArmShare);
        float clampedLeftForearmShare = Mathf.Clamp01(leftForearmShare);

        Quaternion kickRot = Quaternion.AngleAxis(currentKick, safeRightAxis);
        Quaternion leftKickRot = Quaternion.AngleAxis(currentLeftKick, safeLeftAxis);

        if (spineBone)
            SetSafeLocalRotation(spineBone, spineAnimatedLocalRot * Quaternion.Slerp(Quaternion.identity, kickRot, clampedSpineShare));

        if (rightClavicleBone)
            SetSafeLocalRotation(rightClavicleBone, rightClavicleAnimatedLocalRot * Quaternion.Slerp(Quaternion.identity, kickRot, clampedRightClavicleShare));

        if (rightUpperArmBone)
            SetSafeLocalRotation(rightUpperArmBone, rightUpperArmAnimatedLocalRot * Quaternion.Slerp(Quaternion.identity, kickRot, clampedRightUpperArmShare));

        if (rightForearmBone)
            SetSafeLocalRotation(rightForearmBone, rightForearmAnimatedLocalRot * Quaternion.Slerp(Quaternion.identity, kickRot, clampedRightForearmShare));

        if (rightHandBone)
            SetSafeLocalRotation(rightHandBone, rightHandAnimatedLocalRot * Quaternion.Slerp(Quaternion.identity, kickRot, clampedRightHandShare));

        if (leftUpperArmBone)
            SetSafeLocalRotation(leftUpperArmBone, leftUpperArmAnimatedLocalRot * Quaternion.Slerp(Quaternion.identity, leftKickRot, clampedLeftUpperArmShare));

        if (leftForearmBone)
            SetSafeLocalRotation(leftForearmBone, leftForearmAnimatedLocalRot * Quaternion.Slerp(Quaternion.identity, leftKickRot, clampedLeftForearmShare));
    }


    private float GetLaggedLeftKick(float deltaTime)
    {
        float safeLeftKickMultiplier = Mathf.Max(0.0f, leftKickMultiplier);
        float targetLeftKick = ClampFloatMagnitude(currentKick * safeLeftKickMultiplier, maxLeftKickDegrees);

        if (leftHandLagSeconds <= 0.0f) return targetLeftKick;

        float t = 1.0f - Mathf.Exp(-deltaTime / leftHandLagSeconds);
        return Mathf.Lerp(currentLeftKick, targetLeftKick, t);
    }


    private static bool IsFirearmCategory(WeaponCategory category)
    {
        return category != WeaponCategory.Unarmed
               && category != WeaponCategory.Knife
               && category != WeaponCategory.TwoHanded
               && category != WeaponCategory.Explosive;
    }


    private static Vector3 SpringVectorToZero(Vector3 value, ref Vector3 velocity, float strength, float damping, float deltaTime)
    {
        Vector3 acceleration = (-value * strength) - (velocity * damping);
        velocity += acceleration * deltaTime;
        value += velocity * deltaTime;
        return value;
    }


    private static float SpringFloatToZero(float value, ref float velocity, float strength, float damping, float deltaTime)
    {
        float acceleration = (-value * strength) - (velocity * damping);
        velocity += acceleration * deltaTime;
        value += velocity * deltaTime;
        return value;
    }


    private static Vector3 ClampMagnitudeSafe(Vector3 value, float maxMagnitude)
    {
        if (maxMagnitude <= 0.0f) return Vector3.zero;
        if (!IsFiniteVector3(value)) return Vector3.zero;
        return Vector3.ClampMagnitude(value, maxMagnitude);
    }


    private static float ClampFloatMagnitude(float value, float maxMagnitude)
    {
        if (maxMagnitude <= 0.0f) return 0.0f;
        if (!IsFiniteFloat(value)) return 0.0f;
        return Mathf.Clamp(value, -maxMagnitude, maxMagnitude);
    }


    private static Vector3 GetSafeAxis(Vector3 axis, Vector3 fallbackAxis)
    {
        if (!IsFiniteFloat(axis.x) || !IsFiniteFloat(axis.y) || !IsFiniteFloat(axis.z)) return fallbackAxis;
        if (axis.sqrMagnitude <= 0.000001f) return fallbackAxis;
        return axis.normalized;
    }


    private static Quaternion GetSafeLocalRotation(Transform bone)
    {
        Quaternion localRot = bone.localRotation;
        if (IsFiniteQuaternion(localRot)) return NormalizeSafe(localRot);
        bone.localRotation = Quaternion.identity;
        return Quaternion.identity;
    }


    private static void SetSafeLocalRotation(Transform bone, Quaternion localRotation)
    {
        Quaternion safeLocalRotation = IsFiniteQuaternion(localRotation) ? NormalizeSafe(localRotation) : Quaternion.identity;
        if (bone.localRotation != safeLocalRotation)
            bone.localRotation = safeLocalRotation;
    }


    private static Quaternion NormalizeSafe(Quaternion q)
    {
        if (!IsFiniteQuaternion(q)) return Quaternion.identity;

        float mag = Mathf.Sqrt((q.x * q.x) + (q.y * q.y) + (q.z * q.z) + (q.w * q.w));
        if (mag <= 0.000001f || float.IsNaN(mag) || float.IsInfinity(mag)) return Quaternion.identity;

        return new Quaternion(q.x / mag, q.y / mag, q.z / mag, q.w / mag);
    }


    private static bool IsFiniteVector3(Vector3 value)
    {
        return IsFiniteFloat(value.x) && IsFiniteFloat(value.y) && IsFiniteFloat(value.z);
    }


    private static bool IsFiniteQuaternion(Quaternion value)
    {
        return IsFiniteFloat(value.x) && IsFiniteFloat(value.y) && IsFiniteFloat(value.z) && IsFiniteFloat(value.w);
    }


    private static bool IsFiniteFloat(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
