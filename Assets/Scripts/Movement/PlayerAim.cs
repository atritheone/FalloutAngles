// imports
using UnityEngine;



// methods
public class PlayerAim : MonoBehaviour
{
    private const float MinAimDirectionSqr = 0.001f;

    // The world-space aim target (the object driven by MouseTargetFollower).
    [SerializeField] private Transform aimTarget;

    [Header("Constrained Look")]
    // Camera orbit used to let head-look and sight follow combat MMB orbit without moving the body.
    [SerializeField] private CameraRigOrbit cameraRigOrbit;

    // Maximum horizontal yaw away from body forward used for constrained head/look IK and player sight.
    [SerializeField] [Range(1f, 179f)] private float lookConeAngleDegrees = 120f;

    // Maximum upward pitch for constrained look directions.
    [SerializeField] [Range(0f, 89f)] private float maxLookPitchUp = 45f;

    // Maximum downward pitch for constrained look directions.
    [SerializeField] [Range(0f, 89f)] private float maxLookPitchDown = 35f;

    // Combat MMB orbit is primarily a yaw look. Keep pitch neutral so the head does not stare down with the camera rig.
    [SerializeField] private bool includeCombatOrbitCameraPitch = false;

    [Header("Constrained Look Smoothing")]
    // Smooth the clamped yaw so crossing behind the body does not snap from one side of the cone to the other.
    [SerializeField] private bool smoothConstrainedLookYaw = true;

    // Time for the constrained yaw to settle toward the new cone edge.
    [SerializeField] [Min(0f)] private float constrainedLookYawSmoothTime = 0.12f;

    // Maximum yaw speed while smoothing across the cone.
    [SerializeField] [Min(1f)] private float constrainedLookYawMaxSpeed = 540f;

    // The latest desired facing rotation derived from aim target (BODY YAW ONLY).
    private Quaternion desiredRotation;

    // True once we have a valid aim solution this frame.
    private bool hasAimSolution;

    // Cached full 3D aim direction (includes pitch).
    private Vector3 fullAimDirection;

    // Cached target follower used to derive stable yaw from the mouse ray instead of surface hit depth.
    private MouseTargetFollower aimTargetFollower;

    // Target transform used when the follower cache was resolved.
    private Transform cachedAimTargetFollowerTransform;

    // Last non-zero yaw side used when the desired direction is exactly behind the body.
    private float lastLookYawSign = 1f;

    // Smoothed yaw inside the constrained look cone.
    private float smoothedConstrainedLookYaw;

    // SmoothDamp velocity for constrained look yaw.
    private float constrainedLookYawVelocity;

    // True once the smoothed constrained yaw has been initialized.
    private bool hasSmoothedConstrainedLookYaw;

    // Frame guard so multiple IK/sight queries do not advance smoothing multiple times.
    private int lastConstrainedLookYawSmoothFrame = -1;

    // Expose the desired rotation to other systems (movement, weapon, etc.).
    public Quaternion DesiredRotation => desiredRotation;

    // Expose whether the aim solution is valid.
    public bool HasAimSolution => hasAimSolution;

    // Expose the assigned aim target (read-only).
    public Transform AimTarget => aimTarget;

    // Expose the full 3D aim direction (pitch preserved).
    public Vector3 FullAimDirection => fullAimDirection;

    // Maximum horizontal yaw away from body forward used by head-look IK and player sight.
    public float LookConeAngleDegrees => Mathf.Clamp(lookConeAngleDegrees, 1f, 179f);

    // Maximum horizontal turn away from body forward.
    public float MaxLookYawFromBody => LookConeAngleDegrees;

    // True while constrained look should follow combat orbit instead of the frozen crosshair target.
    public bool IsCombatOrbitLookActive
    {
        get
        {
            EnsureReferences();
            return cameraRigOrbit && cameraRigOrbit.IsCombatManualOrbitHeld;
        }
    }



    private void Awake()
    {
        EnsureReferences();

        // Default to current rotation so we never output identity by accident.
        desiredRotation = transform.rotation;

        // Start with no confirmed aim solution.
        hasAimSolution = false;

        // Default full aim direction to forward so it's always sane.
        fullAimDirection = transform.forward;
    }


    private void OnValidate()
    {
        lookConeAngleDegrees = Mathf.Clamp(lookConeAngleDegrees, 1f, 179f);
        maxLookPitchUp = Mathf.Clamp(maxLookPitchUp, 0f, 89f);
        maxLookPitchDown = Mathf.Clamp(maxLookPitchDown, 0f, 89f);
        constrainedLookYawSmoothTime = Mathf.Max(0f, constrainedLookYawSmoothTime);
        constrainedLookYawMaxSpeed = Mathf.Max(1f, constrainedLookYawMaxSpeed);
    }


    // Compute and cache desiredRotation using the provided origin position.
    // NOTE: desiredRotation is intentionally BODY YAW ONLY. Pitch is preserved in FullAimDirection.
    public void ComputeDesiredRotationFromAimTarget(Vector3 originPosition)
    {
        Transform target = aimTarget;

        // Assume no valid solution until proven otherwise.
        hasAimSolution = false;

        // Stop if we don't have an aim target assigned.
        if (!target) return;

        // Build a full 3D direction from the origin position to the aim target (PITCH PRESERVED).
        Vector3 aimDirection = target.position - originPosition;

        // Stop if the direction is too small to be meaningful.
        if (aimDirection.sqrMagnitude <= MinAimDirectionSqr) return;

        // Cache the full 3D direction so other systems can pitch (weapon/upper body/IK).
        fullAimDirection = aimDirection.normalized;

        // Build a horizontal-only direction for BODY rotation (YAW ONLY).
        Vector3 bodyDirection;
        if (!TryGetStableFlatAimDirection(originPosition, target, out bodyDirection))
        {
            bodyDirection = aimDirection;
            bodyDirection.y = 0f;
        }

        // Stop if the flat direction is too small to be meaningful (e.g., target straight up/down).
        if (bodyDirection.sqrMagnitude <= MinAimDirectionSqr)
        {
            // Keep the last desiredRotation, but still report a valid aim solution (pitch-only aim).
            hasAimSolution = true;

            return;
        }

        // Convert the flat direction into a world-space facing rotation (BODY YAW).
        desiredRotation = Quaternion.LookRotation(bodyDirection);

        // Mark that we have a valid aim solution.
        hasAimSolution = true;
    }


    // Get a flattened direction to the aim target from a given origin (YAW ONLY).
    public Vector3 GetFlatAimDirection(Vector3 originPosition)
    {
        Transform self = transform;
        Transform target = aimTarget;

        // Return forward if we cannot compute aim.
        if (!target) return self.forward;

        if (TryGetStableFlatAimDirection(originPosition, target, out Vector3 stableDirection))
            return stableDirection;

        // Build direction from origin to target.
        Vector3 direction = target.position - originPosition;

        // Stop if it's basically zero.
        if (direction.sqrMagnitude <= MinAimDirectionSqr) return self.forward;

        // Flatten pitch for a BODY / movement-friendly direction.
        direction.y = 0f;

        // If it's basically zero after flattening, fall back to current forward.
        if (direction.sqrMagnitude <= MinAimDirectionSqr) return self.forward;

        // Return the normalized flat direction.
        return direction.normalized;
    }


    // Get the full 3D direction to the aim target from a given origin (PITCH PRESERVED).
    public Vector3 GetFullAimDirection(Vector3 originPosition)
    {
        Transform self = transform;
        Transform target = aimTarget;

        // Return forward if we cannot compute aim.
        if (!target) return self.forward;

        // Build direction from origin to target.
        Vector3 direction = target.position - originPosition;

        // Stop if it's basically zero.
        if (direction.sqrMagnitude <= MinAimDirectionSqr) return self.forward;

        // Return the normalized full direction (includes pitch).
        return direction.normalized;
    }


    public bool TryGetConstrainedLookDirection(Vector3 originPosition, out Vector3 direction)
    {
        EnsureReferences();

        direction = Vector3.zero;
        if (!TryGetUnconstrainedLookDirection(originPosition, out Vector3 desiredDirection))
            return false;

        return TryConstrainLookDirection(desiredDirection, out direction);
    }


    public bool TryGetConstrainedFlatLookDirection(Vector3 originPosition, out Vector3 direction)
    {
        EnsureReferences();

        direction = Vector3.zero;
        if (TryGetCombatOrbitLookDirection(out Vector3 combatOrbitDirection))
            return TryConstrainAndFlattenLookDirection(combatOrbitDirection, out direction);

        Transform target = aimTarget;
        if (target && TryGetStableFlatAimDirection(originPosition, target, out Vector3 stableFlatDirection))
            return TryConstrainAndFlattenLookDirection(stableFlatDirection, out direction);

        if (!TryGetConstrainedLookDirection(originPosition, out Vector3 constrainedDirection))
            return false;

        return TryFlattenLookDirection(constrainedDirection, out direction);
    }


    public bool TryGetConstrainedLookPoint(Vector3 originPosition, float lookDistance, out Vector3 point)
    {
        point = Vector3.zero;
        if (!TryGetConstrainedLookDirection(originPosition, out Vector3 direction))
            return false;

        point = originPosition + direction * Mathf.Max(0.01f, lookDistance);
        return true;
    }


    public bool TryGetStableAimPoint(float aimDistance, out Vector3 point)
    {
        point = Vector3.zero;

        Transform target = aimTarget;
        if (!target)
            return false;

        MouseTargetFollower follower = ResolveAimTargetFollower(target);
        if (!follower)
            return false;

        return follower.TryGetStableAimPoint(aimDistance, out point);
    }


    private void EnsureReferences()
    {
        if (!cameraRigOrbit)
            cameraRigOrbit = FindAnyObjectByType<CameraRigOrbit>();
    }


    private bool TryGetUnconstrainedLookDirection(Vector3 originPosition, out Vector3 direction)
    {
        direction = Vector3.zero;

        if (TryGetCombatOrbitLookDirection(out direction))
            return true;

        Transform target = aimTarget;
        if (target)
        {
            direction = target.position - originPosition;
            if (direction.sqrMagnitude > MinAimDirectionSqr)
            {
                direction.Normalize();
                return true;
            }
        }

        if (fullAimDirection.sqrMagnitude > MinAimDirectionSqr)
        {
            direction = fullAimDirection.normalized;
            return true;
        }

        direction = transform.forward;
        return direction.sqrMagnitude > MinAimDirectionSqr;
    }


    private bool TryGetCombatOrbitLookDirection(out Vector3 direction)
    {
        direction = Vector3.zero;

        CameraRigOrbit orbit = cameraRigOrbit;
        if (!orbit || !orbit.IsCombatManualOrbitHeld)
            return false;

        if (includeCombatOrbitCameraPitch && orbit.TryGetCameraForward(out Vector3 cameraForward))
        {
            direction = cameraForward;
        }
        else if (orbit.YawPivot)
        {
            direction = orbit.YawPivot.forward;
        }
        else if (orbit.TryGetCameraForward(out Vector3 fallbackCameraForward))
        {
            direction = fallbackCameraForward;
        }

        if (!includeCombatOrbitCameraPitch)
            direction.y = 0f;

        if (direction.sqrMagnitude <= MinAimDirectionSqr)
            return false;

        direction.Normalize();
        return true;
    }


    private bool TryConstrainLookDirection(Vector3 desiredDirection, out Vector3 constrainedDirection)
    {
        constrainedDirection = Vector3.zero;

        if (desiredDirection.sqrMagnitude <= MinAimDirectionSqr)
            return false;

        Vector3 bodyForward = transform.forward;
        bodyForward.y = 0f;
        if (bodyForward.sqrMagnitude <= MinAimDirectionSqr)
            bodyForward = Vector3.forward;
        else
            bodyForward.Normalize();

        Vector3 desiredFlat = desiredDirection;
        desiredFlat.y = 0f;
        if (desiredFlat.sqrMagnitude <= MinAimDirectionSqr)
            desiredFlat = bodyForward;
        else
            desiredFlat.Normalize();

        float yaw = Vector3.SignedAngle(bodyForward, desiredFlat, Vector3.up);
        if (Mathf.Abs(yaw) >= 179.9f)
            yaw = 180f * lastLookYawSign;
        else if (Mathf.Abs(yaw) > 0.01f)
            lastLookYawSign = Mathf.Sign(yaw);

        float maxLookYaw = MaxLookYawFromBody;
        float clampedYaw = Mathf.Clamp(yaw, -maxLookYaw, maxLookYaw);
        clampedYaw = GetSmoothedConstrainedLookYaw(clampedYaw, maxLookYaw);
        Vector3 clampedFlat = Quaternion.AngleAxis(clampedYaw, Vector3.up) * bodyForward;

        float horizontalMagnitude = new Vector2(desiredDirection.x, desiredDirection.z).magnitude;
        float pitch = Mathf.Atan2(desiredDirection.y, horizontalMagnitude) * Mathf.Rad2Deg;
        float clampedPitch = Mathf.Clamp(pitch, -maxLookPitchDown, maxLookPitchUp);
        float pitchRadians = clampedPitch * Mathf.Deg2Rad;

        constrainedDirection = clampedFlat * Mathf.Cos(pitchRadians) + Vector3.up * Mathf.Sin(pitchRadians);
        if (constrainedDirection.sqrMagnitude <= MinAimDirectionSqr)
            return false;

        constrainedDirection.Normalize();
        return true;
    }


    private bool TryConstrainAndFlattenLookDirection(Vector3 desiredDirection, out Vector3 direction)
    {
        direction = Vector3.zero;
        if (!TryConstrainLookDirection(desiredDirection, out Vector3 constrainedDirection))
            return false;

        return TryFlattenLookDirection(constrainedDirection, out direction);
    }


    private static bool TryFlattenLookDirection(Vector3 lookDirection, out Vector3 direction)
    {
        direction = Vector3.zero;

        lookDirection.y = 0f;
        if (lookDirection.sqrMagnitude <= MinAimDirectionSqr)
            return false;

        direction = lookDirection.normalized;
        return true;
    }


    private float GetSmoothedConstrainedLookYaw(float targetYaw, float maxLookYaw)
    {
        if (!Application.isPlaying || !smoothConstrainedLookYaw || constrainedLookYawSmoothTime <= 0f)
        {
            ResetSmoothedConstrainedLookYaw(targetYaw);
            return targetYaw;
        }

        if (!hasSmoothedConstrainedLookYaw)
            ResetSmoothedConstrainedLookYaw(targetYaw);

        int frame = Time.frameCount;
        if (lastConstrainedLookYawSmoothFrame != frame)
        {
            lastConstrainedLookYawSmoothFrame = frame;
            smoothedConstrainedLookYaw = Mathf.SmoothDamp(
                smoothedConstrainedLookYaw,
                targetYaw,
                ref constrainedLookYawVelocity,
                constrainedLookYawSmoothTime,
                constrainedLookYawMaxSpeed,
                Time.deltaTime
            );

            smoothedConstrainedLookYaw = Mathf.Clamp(smoothedConstrainedLookYaw, -maxLookYaw, maxLookYaw);
        }

        return smoothedConstrainedLookYaw;
    }


    private void ResetSmoothedConstrainedLookYaw(float yaw)
    {
        smoothedConstrainedLookYaw = yaw;
        constrainedLookYawVelocity = 0f;
        hasSmoothedConstrainedLookYaw = true;
        lastConstrainedLookYawSmoothFrame = Time.frameCount;
    }


    private bool TryGetStableFlatAimDirection(Vector3 originPosition, Transform target, out Vector3 direction)
    {
        direction = Vector3.zero;

        MouseTargetFollower follower = ResolveAimTargetFollower(target);
        if (!follower)
            return false;

        return follower.TryGetStableFlatAimDirection(originPosition, out direction);
    }


    private MouseTargetFollower ResolveAimTargetFollower(Transform target)
    {
        if (target != cachedAimTargetFollowerTransform)
        {
            cachedAimTargetFollowerTransform = target;
            aimTargetFollower = target ? target.GetComponent<MouseTargetFollower>() : null;
        }

        return aimTargetFollower;
    }
}
