using UnityEngine;

public class NPCAim : MonoBehaviour
{
    private const float MinAimDirectionSqr = 0.001f;

    public enum AimMode
    {
        None,
        TransformTarget,
        WorldPoint,
        WorldDirection
    }

    [Header("References")]
    [SerializeField] private NPCState npcState;
    [SerializeField] private Transform aimOrigin;
    [SerializeField] private Transform aimTarget;
    [SerializeField] private Transform bodyToRotate;
    [SerializeField] private Rigidbody bodyRigidbody;

    [Header("Aim")]
    [SerializeField] private AimMode aimMode = AimMode.None;
    [SerializeField] private Vector3 aimPoint;
    [SerializeField] private Vector3 aimDirection = Vector3.forward;
    [SerializeField] private bool computeEveryUpdate = true;

    [Header("Body Rotation")]
    [SerializeField] private bool rotateBodyTowardsAim = false;
    [SerializeField] private bool rotateOnlyInCombatMode = true;
    [SerializeField, Min(0f)] private float rotationSpeed = 540f;
    [SerializeField] private bool useRigidbodyRotation = true;

    private Quaternion desiredRotation;
    private bool hasAimSolution;
    private Vector3 fullAimDirection;
    private Vector3 lastResolvedAimPoint;

    public Quaternion DesiredRotation => desiredRotation;
    public bool HasAimSolution => hasAimSolution;
    public Transform AimTarget => aimMode == AimMode.TransformTarget ? aimTarget : null;
    public Transform AssignedAimTarget => aimTarget;
    public Transform AimOrigin => aimOrigin;
    public AimMode CurrentAimMode => aimMode;
    public Vector3 AimPoint => lastResolvedAimPoint;
    public Vector3 FullAimDirection => fullAimDirection;

    private void Awake()
    {
        ResolveReferences();
        ApplyDefaultAimModeFromAssignedTarget();
        desiredRotation = ResolveBodyTransform().rotation;
        hasAimSolution = false;
        fullAimDirection = ResolveBodyTransform().forward;
        lastResolvedAimPoint = ResolveOriginPosition() + fullAimDirection;
    }

    private void Reset()
    {
        ResolveReferences();
        ApplyDefaultAimModeFromAssignedTarget();
    }

    private void OnValidate()
    {
        rotationSpeed = Mathf.Max(0f, rotationSpeed);

        if (aimDirection.sqrMagnitude <= MinAimDirectionSqr)
            aimDirection = Vector3.forward;
        else
            aimDirection.Normalize();

        ApplyDefaultAimModeFromAssignedTarget();
    }

    private void Update()
    {
        if (computeEveryUpdate)
            ComputeDesiredRotation();

        if (rotateBodyTowardsAim)
            RotateBodyTowardsDesiredRotation(Time.deltaTime);
    }

    public void SetAimTarget(Transform target)
    {
        aimTarget = target;
        aimMode = target ? AimMode.TransformTarget : AimMode.None;
        ComputeDesiredRotation();
    }

    public void SetAimPoint(Vector3 worldPoint)
    {
        aimPoint = worldPoint;
        aimMode = AimMode.WorldPoint;
        ComputeDesiredRotation();
    }

    public void SetAimDirection(Vector3 worldDirection)
    {
        if (worldDirection.sqrMagnitude <= MinAimDirectionSqr)
        {
            ClearAim();
            return;
        }

        aimDirection = worldDirection.normalized;
        aimMode = AimMode.WorldDirection;
        ComputeDesiredRotation();
    }

    public void ClearAim()
    {
        aimMode = aimTarget ? AimMode.TransformTarget : AimMode.None;
        hasAimSolution = false;
    }

    public void ClearAimTarget()
    {
        aimTarget = null;
        aimMode = AimMode.None;
        hasAimSolution = false;
    }

    public void ComputeDesiredRotation()
    {
        ComputeDesiredRotationFromAimTarget(ResolveOriginPosition());
    }

    public void ComputeDesiredRotationFromAimTarget(Vector3 originPosition)
    {
        hasAimSolution = false;

        Vector3 direction;
        if (!TryResolveAimDirection(originPosition, out direction))
            return;

        fullAimDirection = direction;

        Vector3 bodyDirection = direction;
        bodyDirection.y = 0f;

        if (bodyDirection.sqrMagnitude <= MinAimDirectionSqr)
        {
            hasAimSolution = true;
            return;
        }

        desiredRotation = Quaternion.LookRotation(bodyDirection.normalized);
        hasAimSolution = true;
    }

    public Vector3 GetFlatAimDirection(Vector3 originPosition)
    {
        Transform body = ResolveBodyTransform();

        Vector3 direction;
        if (!TryResolveAimDirection(originPosition, out direction))
            return body.forward;

        direction.y = 0f;
        if (direction.sqrMagnitude <= MinAimDirectionSqr)
            return body.forward;

        return direction.normalized;
    }

    public Vector3 GetFullAimDirection(Vector3 originPosition)
    {
        Transform body = ResolveBodyTransform();

        Vector3 direction;
        if (!TryResolveAimDirection(originPosition, out direction))
            return body.forward;

        return direction;
    }

    public Vector3 GetAimPoint(Vector3 fallbackOrigin, float fallbackDistance = 25f)
    {
        if (hasAimSolution)
            return lastResolvedAimPoint;

        return fallbackOrigin + ResolveBodyTransform().forward * Mathf.Max(0f, fallbackDistance);
    }

    public void RotateBodyTowardsDesiredRotation(float deltaTime)
    {
        if (!hasAimSolution)
            return;

        if (rotateOnlyInCombatMode && npcState && !npcState.GetCombatMode())
            return;

        Transform body = ResolveBodyTransform();
        Quaternion currentRotation = body.rotation;
        Quaternion nextRotation = rotationSpeed <= 0f
            ? desiredRotation
            : Quaternion.RotateTowards(currentRotation, desiredRotation, rotationSpeed * deltaTime);

        if (useRigidbodyRotation && bodyRigidbody)
            bodyRigidbody.MoveRotation(nextRotation);
        else
            body.rotation = nextRotation;
    }

    private void ResolveReferences()
    {
        if (!npcState)
            npcState = GetComponentInParent<NPCState>();

        if (!aimOrigin)
            aimOrigin = transform;

        if (!bodyToRotate)
            bodyToRotate = transform;

        if (!bodyRigidbody)
            bodyRigidbody = GetComponentInParent<Rigidbody>();
    }

    private void ApplyDefaultAimModeFromAssignedTarget()
    {
        if (aimMode == AimMode.None && aimTarget)
            aimMode = AimMode.TransformTarget;
    }

    private Vector3 ResolveOriginPosition()
    {
        return aimOrigin ? aimOrigin.position : transform.position;
    }

    private Transform ResolveBodyTransform()
    {
        return bodyToRotate ? bodyToRotate : transform;
    }

    private bool TryResolveAimDirection(Vector3 originPosition, out Vector3 direction)
    {
        direction = Vector3.zero;

        switch (aimMode)
        {
            case AimMode.TransformTarget:
                if (!aimTarget)
                    return false;

                lastResolvedAimPoint = aimTarget.position;
                direction = lastResolvedAimPoint - originPosition;
                break;

            case AimMode.WorldPoint:
                lastResolvedAimPoint = aimPoint;
                direction = lastResolvedAimPoint - originPosition;
                break;

            case AimMode.WorldDirection:
                if (aimDirection.sqrMagnitude <= MinAimDirectionSqr)
                    return false;

                direction = aimDirection.normalized;
                lastResolvedAimPoint = originPosition + direction;
                break;

            default:
                return false;
        }

        if (direction.sqrMagnitude <= MinAimDirectionSqr)
            return false;

        direction.Normalize();
        return true;
    }
}
