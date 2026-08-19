// imports
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Serialization;



// class
[DefaultExecutionOrder(-200)]
public class FirearmConstraintAiming : MonoBehaviour
{
    [Header("References")]
    // Animator used to query active states.
    [SerializeField] private Animator animator;

    // Weapon controller used to verify selected weapon category.
    [FormerlySerializedAs("playerWeaponController")]
    [SerializeField] private WeaponController weaponController;

    // Character state used to verify selected weapon is in-hand.
    [FormerlySerializedAs("playerState")]
    [SerializeField] private CharacterState characterState;

    // Multi-aim constraints to drive on/off from animation state.
    [SerializeField] private MultiAimConstraint[] constraints;

    // Optional explicit aim target. When unset, the component resolves one from NPCAim/PlayerAim.
    [SerializeField] private Transform aimTarget;

    [Header("State Filter")]
    // Animator layer index to evaluate.
    [SerializeField] private int animatorLayer = 0;

    // State names that should enable the constraints (short names or full paths).
    [SerializeField] private string[] activeStateNames;

    // Keep constraints enabled while blending into an allowed state.
    [SerializeField] private bool includeNextStateDuringTransition = true;

    // NPCs need to track their aim before the actual shot animation starts.
    [SerializeField] private bool allowNpcCombatAimWithoutListedState = true;

    [Header("Constraint Weights")]
    // Applied to each constraint when an allowed state is active.
    [SerializeField] [Range(0f, 1f)] private float activeWeight = 1f;

    // Applied to each constraint when no allowed state is active.
    [SerializeField] [Range(0f, 1f)] private float inactiveWeight = 0f;

    [Header("Aim Target Stability")]
    // Distance used for runtime constraint targets so IK follows aim direction, not near wall hit points.
    [SerializeField] [Min(0.01f)] private float constraintAimTargetDistance = 500f;

    [Header("Player Constrained Look")]
    // Explicit player look constraints can follow the constrained player look direction instead of the frozen combat crosshair.
    [FormerlySerializedAs("useConstrainedPlayerHeadLookTarget")]
    [SerializeField] private bool useConstrainedPlayerLookTarget = true;

    // Explicit constraints that should use the constrained player look target.
    [FormerlySerializedAs("playerHeadLookConstraints")]
    [SerializeField] private MultiAimConstraint[] playerLookConstraints;

    // Keep the player constrained-look MultiAim limits in sync with PlayerAim's constrained look cone.
    [FormerlySerializedAs("drivePlayerHeadLookConstraintLimits")]
    [SerializeField] private bool drivePlayerLookConstraintLimits = true;

    // Enforce the final driven bone direction after the rig solves so parent/child constraints cannot compound past the cone.
    [SerializeField] private bool enforcePlayerLookConeAfterRig = true;

    [Header("Activation Filters")]
    // Require the selected weapon to be marked as in-hand before constraints can run.
    [SerializeField] private bool requireWeaponInHand = true;

    // Require the selected weapon category to be firearm-like before constraints can run.
    [SerializeField] private bool requireFirearmCategory = true;

    // Serialized version for migration of activation filter defaults on existing scene data.
    [SerializeField] [HideInInspector] private int activationFilterVersion = 1;

    private const int CurrentActivationFilterVersion = 1;

    private int[] activeStateHashes;
    private bool lastBaseActive;
    private bool lastPlayerLookActive;
    private bool hasLastWeightState;
    private NPCAim npcAim;
    private PlayerAim playerAim;
    private RigBuilder rigBuilder;
    private Transform runtimeNpcAimTarget;
    private Transform runtimePlayerAimTarget;
    private readonly List<RuntimeConstraintTarget> runtimePlayerLookTargets = new List<RuntimeConstraintTarget>();
    private bool hasResolvedInitialReferences;
    private bool rigRebuildQueued;
    private bool hasOrderedPlayerLookConstraints;

    private struct RuntimeConstraintTarget
    {
        public MultiAimConstraint constraint;
        public Transform target;
    }


    private void Reset()
    {
        UpgradeActivationFilterDefaults();
        EnsureReferences();
        constraints = GetComponentsInChildren<MultiAimConstraint>(true);
        hasOrderedPlayerLookConstraints = false;
        OrderPlayerLookConstraintsForHierarchy();
        RebuildStateHashes();
    }


    private void Awake()
    {
        UpgradeActivationFilterDefaults();
        EnsureReferences();
        OrderPlayerLookConstraintsForHierarchy();
        RebuildStateHashes();
    }


    private void OnValidate()
    {
        UpgradeActivationFilterDefaults();
        EnsureReferences();
        hasOrderedPlayerLookConstraints = false;
        OrderPlayerLookConstraintsForHierarchy();
        RebuildStateHashes();
        hasLastWeightState = false;
    }


    private void OnDestroy()
    {
        DestroyRuntimeTarget(runtimeNpcAimTarget);
        DestroyRuntimeTarget(runtimePlayerAimTarget);
        DestroyRuntimeConstraintTargets(runtimePlayerLookTargets);
    }


    private void Update()
    {
        if (!hasResolvedInitialReferences || MissingRequiredReferences())
            EnsureReferences();

        EnsurePlayerLookConstraintsAreTracked();
        OrderPlayerLookConstraintsForHierarchy();
        SyncConstraintSources();
        bool shouldBaseBeActive = ShouldActivateConstraints();
        bool shouldPlayerLookBeActive = ShouldActivatePlayerLookConstraints();
        if (hasLastWeightState && shouldBaseBeActive == lastBaseActive && shouldPlayerLookBeActive == lastPlayerLookActive) return;

        ApplyConstraintWeights(shouldBaseBeActive, shouldPlayerLookBeActive);
        lastBaseActive = shouldBaseBeActive;
        lastPlayerLookActive = shouldPlayerLookBeActive;
        hasLastWeightState = true;
    }


    private void LateUpdate()
    {
        UpdateRuntimePlayerAimTarget();
        UpdateRuntimePlayerLookTargets();
        UpdateRuntimeNpcAimTarget();
        EnforcePlayerLookConeAfterRig();
    }


    private bool ShouldActivateConstraints()
    {
        if (!PassesWeaponFilters()) return false;
        if (!animator) return false;
        if (constraints == null || constraints.Length == 0) return false;
        if (!ResolveAimTarget()) return false;
        if (animatorLayer < 0 || animatorLayer >= animator.layerCount) return false;

        if (allowNpcCombatAimWithoutListedState && npcAim && npcAim.HasAimSolution && IsCharacterInCombatWithWeaponReady())
            return true;

        if (activeStateHashes == null || activeStateHashes.Length == 0) return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(animatorLayer);
        if (MatchesAllowedState(current))
            return true;

        if (!includeNextStateDuringTransition)
            return false;

        if (!animator.IsInTransition(animatorLayer))
            return false;

        AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(animatorLayer);
        return MatchesAllowedState(next);
    }


    private bool ShouldActivatePlayerLookConstraints()
    {
        if (!useConstrainedPlayerLookTarget) return false;
        if (!playerAim || !playerAim.IsCombatOrbitLookActive) return false;
        if (!HasPlayerLookConstraints()) return false;

        return true;
    }


    private void EnsureReferences()
    {
        if (!animator)
            animator = GetComponentInParent<Animator>();

        if (!weaponController)
            weaponController = GetComponentInParent<WeaponController>();

        if (!characterState)
            characterState = GetComponentInParent<CharacterState>();

        if (!npcAim)
            npcAim = GetComponentInParent<NPCAim>();

        if (!playerAim)
            playerAim = GetComponentInParent<PlayerAim>();

        if (!rigBuilder)
            rigBuilder = GetComponentInParent<RigBuilder>();

        hasResolvedInitialReferences = true;
    }


    private bool MissingRequiredReferences()
    {
        return !animator ||
               !weaponController ||
               !characterState ||
               (!npcAim && !playerAim && !aimTarget);
    }


    private void UpgradeActivationFilterDefaults()
    {
        if (activationFilterVersion >= CurrentActivationFilterVersion)
            return;

        requireWeaponInHand = true;
        requireFirearmCategory = true;
        activationFilterVersion = CurrentActivationFilterVersion;
    }


    private bool PassesWeaponFilters()
    {
        if (!weaponController)
            return !requireWeaponInHand && !requireFirearmCategory;

        if (string.IsNullOrWhiteSpace(weaponController.GetCurrentWeaponName()))
            return false;

        if (requireWeaponInHand)
        {
            if (!characterState)
                return false;

            if (!characterState.GetWeaponInHand())
                return false;
        }

        if (requireFirearmCategory && !IsCurrentWeaponFirearmCategory())
            return false;

        return true;
    }


    private bool IsCharacterInCombatWithWeaponReady()
    {
        if (!characterState)
            return !requireWeaponInHand;

        if (!IsCharacterInCombatMode())
            return false;

        return !requireWeaponInHand || characterState.GetWeaponInHand();
    }


    private bool IsCharacterInCombatMode()
    {
        if (characterState is NPCState npcState)
            return npcState.GetCombatMode();

        if (characterState is PlayerState playerState)
            return playerState.GetCombatMode();

        return false;
    }


    private void SyncConstraintSources()
    {
        if (constraints == null || constraints.Length == 0)
            return;

        Transform resolvedAimTarget = ResolveAimTarget();
        if (!resolvedAimTarget)
            return;

        for (int i = 0; i < constraints.Length; i++)
        {
            MultiAimConstraint constraint = constraints[i];
            if (!constraint)
                continue;

            Transform constraintTarget = ResolveAimTargetForConstraint(constraint);
            if (!constraintTarget)
                constraintTarget = resolvedAimTarget;

            MultiAimConstraintData data = constraint.data;
            SyncPlayerLookConstraintLimits(constraint, ref data);

            WeightedTransformArray sources = data.sourceObjects;
            if (sources.Count == 0)
            {
                sources.Add(new WeightedTransform(constraintTarget, 1f));
                rigRebuildQueued = true;
            }
            else
            {
                if (sources[0].transform == constraintTarget)
                {
                    constraint.data = data;
                    continue;
                }

                float existingWeight = sources[0].weight;
                sources[0] = new WeightedTransform(constraintTarget, existingWeight > 0f ? existingWeight : 1f);
                rigRebuildQueued = true;
            }

            data.sourceObjects = sources;
            constraint.data = data;
        }

        RebuildRigIfQueued();
    }


    private void RebuildRigIfQueued()
    {
        if (!rigRebuildQueued)
            return;

        rigRebuildQueued = false;

        if (!rigBuilder)
            rigBuilder = GetComponentInParent<RigBuilder>();

        if (!rigBuilder || !rigBuilder.isActiveAndEnabled)
            return;

        rigBuilder.Build();
    }


    private Transform ResolveAimTarget()
    {
        if (TryResolveNpcAimTarget(out Transform resolvedNpcAimTarget))
            return resolvedNpcAimTarget;

        if (aimTarget)
            return aimTarget;

        if (TryResolvePlayerAimTarget(out Transform resolvedPlayerAimTarget))
            return resolvedPlayerAimTarget;

        return null;
    }


    private Transform ResolveAimTargetForConstraint(MultiAimConstraint constraint)
    {
        if (ShouldUsePlayerLookTarget(constraint))
        {
            Transform lookTarget = ResolveRuntimePlayerLookTarget(constraint);
            UpdateRuntimePlayerLookTarget(constraint, lookTarget);
            return lookTarget;
        }

        return ResolveAimTarget();
    }


    private bool TryResolvePlayerAimTarget(out Transform resolvedAimTarget)
    {
        resolvedAimTarget = null;

        if (!playerAim || !playerAim.AimTarget)
            return false;

        resolvedAimTarget = ResolveRuntimePlayerAimTarget();
        UpdateRuntimePlayerAimTarget();
        return resolvedAimTarget;
    }


    private bool TryResolveNpcAimTarget(out Transform resolvedAimTarget)
    {
        resolvedAimTarget = null;

        if (!npcAim)
            return false;

        if (npcAim.HasAimSolution)
        {
            resolvedAimTarget = ResolveRuntimeNpcAimTarget();
            UpdateRuntimeNpcAimTarget();
            return resolvedAimTarget;
        }

        if (npcAim.AimTarget)
        {
            resolvedAimTarget = npcAim.AimTarget;
            return true;
        }

        return false;
    }


    private Transform ResolveRuntimeNpcAimTarget()
    {
        if (runtimeNpcAimTarget)
            return runtimeNpcAimTarget;

        GameObject targetObject = new GameObject($"{name}_NPCAimTarget");
        targetObject.hideFlags = HideFlags.HideAndDontSave;
        runtimeNpcAimTarget = targetObject.transform;
        runtimeNpcAimTarget.SetParent(transform, false);
        return runtimeNpcAimTarget;
    }


    private Transform ResolveRuntimePlayerAimTarget()
    {
        if (runtimePlayerAimTarget)
            return runtimePlayerAimTarget;

        GameObject targetObject = new GameObject($"{name}_PlayerConstraintAimTarget");
        targetObject.hideFlags = HideFlags.HideAndDontSave;
        runtimePlayerAimTarget = targetObject.transform;
        runtimePlayerAimTarget.SetParent(transform, false);
        return runtimePlayerAimTarget;
    }


    private Transform ResolveRuntimePlayerLookTarget(MultiAimConstraint constraint)
    {
        for (int i = 0; i < runtimePlayerLookTargets.Count; i++)
        {
            RuntimeConstraintTarget entry = runtimePlayerLookTargets[i];
            if (entry.constraint == constraint && entry.target)
                return entry.target;
        }

        GameObject targetObject = new GameObject($"{name}_{constraint.name}_PlayerLookTarget");
        targetObject.hideFlags = HideFlags.HideAndDontSave;
        Transform target = targetObject.transform;
        target.SetParent(transform, false);

        runtimePlayerLookTargets.Add(new RuntimeConstraintTarget
        {
            constraint = constraint,
            target = target
        });

        return target;
    }


    private void UpdateRuntimePlayerAimTarget()
    {
        if (!runtimePlayerAimTarget || !playerAim || !playerAim.AimTarget)
            return;

        if (playerAim.TryGetStableAimPoint(constraintAimTargetDistance, out Vector3 stableAimPoint))
        {
            runtimePlayerAimTarget.position = stableAimPoint;
            return;
        }

        Vector3 originPosition = ResolveConstraintOriginPosition();
        Vector3 aimDirection = playerAim.GetFullAimDirection(originPosition);
        if (aimDirection.sqrMagnitude <= 0.0001f)
            aimDirection = transform.forward;

        runtimePlayerAimTarget.position =
            originPosition + aimDirection.normalized * Mathf.Max(0.01f, constraintAimTargetDistance);
    }


    private void UpdateRuntimePlayerLookTargets()
    {
        for (int i = runtimePlayerLookTargets.Count - 1; i >= 0; i--)
        {
            RuntimeConstraintTarget entry = runtimePlayerLookTargets[i];
            if (!entry.constraint || !entry.target)
            {
                if (entry.target)
                    DestroyRuntimeTarget(entry.target);

                runtimePlayerLookTargets.RemoveAt(i);
                continue;
            }

            UpdateRuntimePlayerLookTarget(entry.constraint, entry.target);
        }
    }


    private void UpdateRuntimePlayerLookTarget(MultiAimConstraint constraint, Transform target)
    {
        if (!constraint || !target || !playerAim)
            return;

        Vector3 originPosition = ResolveConstraintOriginPosition(constraint);
        if (playerAim.TryGetConstrainedLookPoint(originPosition, constraintAimTargetDistance, out Vector3 lookPoint))
        {
            target.position = lookPoint;
            return;
        }

        Vector3 fallbackDirection = playerAim.GetFullAimDirection(originPosition);
        if (fallbackDirection.sqrMagnitude <= 0.0001f)
            fallbackDirection = transform.forward;

        target.position =
            originPosition + fallbackDirection.normalized * Mathf.Max(0.01f, constraintAimTargetDistance);
    }


    private void UpdateRuntimeNpcAimTarget()
    {
        if (!runtimeNpcAimTarget || !npcAim || !npcAim.HasAimSolution)
            return;

        Vector3 originPosition = ResolveConstraintOriginPosition();
        Vector3 aimDirection = npcAim.FullAimDirection;
        if (aimDirection.sqrMagnitude <= 0.0001f)
            aimDirection = npcAim.GetFullAimDirection(originPosition);
        if (aimDirection.sqrMagnitude <= 0.0001f)
            aimDirection = transform.forward;

        runtimeNpcAimTarget.position =
            originPosition + aimDirection.normalized * Mathf.Max(0.01f, constraintAimTargetDistance);
    }


    private Vector3 ResolveConstraintOriginPosition()
    {
        if (constraints != null)
        {
            for (int i = 0; i < constraints.Length; i++)
            {
                MultiAimConstraint constraint = constraints[i];
                if (!constraint)
                    continue;

                Transform constrainedObject = constraint.data.constrainedObject;
                if (constrainedObject)
                    return constrainedObject.position;
            }
        }

        if (playerAim)
            return playerAim.transform.position;

        if (npcAim && npcAim.AimOrigin)
            return npcAim.AimOrigin.position;

        return transform.position;
    }


    private static Vector3 ResolveConstraintOriginPosition(MultiAimConstraint constraint)
    {
        if (!constraint)
            return Vector3.zero;

        Transform constrainedObject = constraint.data.constrainedObject;
        return constrainedObject ? constrainedObject.position : constraint.transform.position;
    }


    private void SyncPlayerLookConstraintLimits(MultiAimConstraint constraint, ref MultiAimConstraintData data)
    {
        if (!drivePlayerLookConstraintLimits || !playerAim || !IsPlayerLookConstraint(constraint))
            return;

        float maxYaw = playerAim.MaxLookYawFromBody;
        Vector2 limits = data.limits;
        if (Mathf.Approximately(limits.x, -maxYaw) && Mathf.Approximately(limits.y, maxYaw))
            return;

        data.limits = new Vector2(-maxYaw, maxYaw);
        rigRebuildQueued = true;
    }


    private void EnforcePlayerLookConeAfterRig()
    {
        if (!enforcePlayerLookConeAfterRig || !playerAim || !HasPlayerLookConstraints())
            return;

        Vector3 bodyForward = playerAim.transform.forward;
        bodyForward.y = 0f;
        if (bodyForward.sqrMagnitude <= 0.0001f)
            return;

        bodyForward.Normalize();
        float maxYaw = playerAim.MaxLookYawFromBody;

        for (int i = 0; i < playerLookConstraints.Length; i++)
        {
            MultiAimConstraint constraint = playerLookConstraints[i];
            if (!constraint || constraint.weight <= 0f)
                continue;

            MultiAimConstraintData data = constraint.data;
            Transform constrainedObject = data.constrainedObject;
            if (!constrainedObject)
                continue;

            Vector3 aimDirection = constrainedObject.TransformDirection(GetAimAxis(data.aimAxis));
            aimDirection.y = 0f;
            if (aimDirection.sqrMagnitude <= 0.0001f)
                continue;

            aimDirection.Normalize();
            float currentYaw = Vector3.SignedAngle(bodyForward, aimDirection, Vector3.up);
            float clampedYaw = Mathf.Clamp(currentYaw, -maxYaw, maxYaw);
            if (Mathf.Abs(currentYaw - clampedYaw) <= 0.01f)
                continue;

            Quaternion correction = Quaternion.AngleAxis(clampedYaw - currentYaw, Vector3.up);
            constrainedObject.rotation = correction * constrainedObject.rotation;
        }
    }


    private static Vector3 GetAimAxis(MultiAimConstraintData.Axis axis)
    {
        switch (axis)
        {
            case MultiAimConstraintData.Axis.X:
                return Vector3.right;
            case MultiAimConstraintData.Axis.X_NEG:
                return Vector3.left;
            case MultiAimConstraintData.Axis.Y:
                return Vector3.up;
            case MultiAimConstraintData.Axis.Y_NEG:
                return Vector3.down;
            case MultiAimConstraintData.Axis.Z:
                return Vector3.forward;
            case MultiAimConstraintData.Axis.Z_NEG:
                return Vector3.back;
            default:
                return Vector3.forward;
        }
    }


    private bool ShouldUsePlayerLookTarget(MultiAimConstraint constraint)
    {
        if (!useConstrainedPlayerLookTarget || !playerAim || !constraint)
            return false;

        return IsPlayerLookConstraint(constraint);
    }


    private bool HasPlayerLookConstraints()
    {
        if (playerLookConstraints == null || playerLookConstraints.Length == 0)
            return false;

        for (int i = 0; i < playerLookConstraints.Length; i++)
        {
            if (playerLookConstraints[i])
                return true;
        }

        return false;
    }


    private void EnsurePlayerLookConstraintsAreTracked()
    {
        if (playerLookConstraints == null || playerLookConstraints.Length == 0)
            return;

        for (int i = 0; i < playerLookConstraints.Length; i++)
        {
            MultiAimConstraint lookConstraint = playerLookConstraints[i];
            if (!lookConstraint || IsTrackedConstraint(lookConstraint))
                continue;

            int oldLength = constraints != null ? constraints.Length : 0;
            MultiAimConstraint[] expandedConstraints = new MultiAimConstraint[oldLength + 1];
            for (int j = 0; j < oldLength; j++)
            {
                expandedConstraints[j] = constraints[j];
            }

            expandedConstraints[oldLength] = lookConstraint;
            constraints = expandedConstraints;
            hasLastWeightState = false;
            rigRebuildQueued = true;
        }
    }


    private bool IsTrackedConstraint(MultiAimConstraint constraint)
    {
        if (!constraint || constraints == null)
            return false;

        for (int i = 0; i < constraints.Length; i++)
        {
            if (constraints[i] == constraint)
                return true;
        }

        return false;
    }


    private void OrderPlayerLookConstraintsForHierarchy()
    {
        if (hasOrderedPlayerLookConstraints)
            return;

        if (playerLookConstraints == null || playerLookConstraints.Length < 2)
        {
            hasOrderedPlayerLookConstraints = true;
            return;
        }

        bool changedArrayOrder = false;
        for (int i = 1; i < playerLookConstraints.Length; i++)
        {
            MultiAimConstraint current = playerLookConstraints[i];
            int currentDepth = GetConstrainedObjectHierarchyDepth(current);
            int j = i - 1;

            while (j >= 0 && GetConstrainedObjectHierarchyDepth(playerLookConstraints[j]) > currentDepth)
            {
                playerLookConstraints[j + 1] = playerLookConstraints[j];
                j--;
                changedArrayOrder = true;
            }

            playerLookConstraints[j + 1] = current;
        }

        bool changedSiblingOrder = OrderConstraintSiblingTransforms(playerLookConstraints);
        hasOrderedPlayerLookConstraints = true;

        if (changedArrayOrder || changedSiblingOrder)
            rigRebuildQueued = true;
    }


    private static int GetConstrainedObjectHierarchyDepth(MultiAimConstraint constraint)
    {
        if (!constraint)
            return int.MaxValue;

        Transform constrainedObject = constraint.data.constrainedObject;
        if (!constrainedObject)
            return int.MaxValue;

        int depth = 0;
        Transform current = constrainedObject;
        while (current.parent)
        {
            depth++;
            current = current.parent;
        }

        return depth;
    }


    private static bool OrderConstraintSiblingTransforms(MultiAimConstraint[] orderedConstraints)
    {
        bool changed = false;
        for (int i = 0; i < orderedConstraints.Length; i++)
        {
            MultiAimConstraint constraint = orderedConstraints[i];
            if (!constraint)
                continue;

            Transform constraintTransform = constraint.transform;
            Transform constraintParent = constraintTransform.parent;
            if (!constraintParent)
                continue;

            int desiredIndex = constraintTransform.GetSiblingIndex();
            for (int j = 0; j < i; j++)
            {
                MultiAimConstraint earlierConstraint = orderedConstraints[j];
                if (!earlierConstraint || earlierConstraint.transform.parent != constraintParent)
                    continue;

                desiredIndex = Mathf.Max(desiredIndex, earlierConstraint.transform.GetSiblingIndex() + 1);
            }

            if (constraintTransform.GetSiblingIndex() == desiredIndex)
                continue;

            constraintTransform.SetSiblingIndex(desiredIndex);
            changed = true;
        }

        return changed;
    }


    private bool IsPlayerLookConstraint(MultiAimConstraint constraint)
    {
        if (!constraint || playerLookConstraints == null)
            return false;

        for (int i = 0; i < playerLookConstraints.Length; i++)
        {
            if (playerLookConstraints[i] == constraint)
                return true;
        }

        return false;
    }


    private static void DestroyRuntimeTarget(Transform runtimeTarget)
    {
        if (!runtimeTarget)
            return;

        if (Application.isPlaying)
            Destroy(runtimeTarget.gameObject);
        else
            DestroyImmediate(runtimeTarget.gameObject);
    }


    private static void DestroyRuntimeConstraintTargets(List<RuntimeConstraintTarget> runtimeTargets)
    {
        for (int i = 0; i < runtimeTargets.Count; i++)
        {
            DestroyRuntimeTarget(runtimeTargets[i].target);
        }

        runtimeTargets.Clear();
    }


    private bool IsFirearmCategory(string categoryName)
    {
        return string.Equals(categoryName, "Pistol", System.StringComparison.Ordinal)
            || string.Equals(categoryName, "SubmachineGun", System.StringComparison.Ordinal)
            || string.Equals(categoryName, "Rifle", System.StringComparison.Ordinal)
            || string.Equals(categoryName, "Shotgun", System.StringComparison.Ordinal)
            || string.Equals(categoryName, "Special", System.StringComparison.Ordinal);
    }


    private bool IsCurrentWeaponFirearmCategory()
    {
        if (weaponController is PlayerWeaponController playerWeaponController)
        {
            PlayerWeaponController.WeaponCategory category = playerWeaponController.GetCurrentCategory();
            return category == PlayerWeaponController.WeaponCategory.Pistol ||
                   category == PlayerWeaponController.WeaponCategory.SubmachineGun ||
                   category == PlayerWeaponController.WeaponCategory.Rifle ||
                   category == PlayerWeaponController.WeaponCategory.Shotgun ||
                   category == PlayerWeaponController.WeaponCategory.Special;
        }

        if (weaponController is NPCWeaponController npcWeaponController)
        {
            NPCWeaponController.WeaponCategory category = npcWeaponController.GetCurrentCategory();
            return category == NPCWeaponController.WeaponCategory.Pistol ||
                   category == NPCWeaponController.WeaponCategory.SubmachineGun ||
                   category == NPCWeaponController.WeaponCategory.Rifle ||
                   category == NPCWeaponController.WeaponCategory.Shotgun ||
                   category == NPCWeaponController.WeaponCategory.Special;
        }

        return IsFirearmCategory(weaponController.GetCurrentCategoryName());
    }


    private bool MatchesAllowedState(AnimatorStateInfo stateInfo)
    {
        for (int i = 0; i < activeStateHashes.Length; i++)
        {
            int hash = activeStateHashes[i];
            if (stateInfo.shortNameHash == hash || stateInfo.fullPathHash == hash)
                return true;
        }

        return false;
    }


    private void ApplyConstraintWeights(bool baseActive, bool playerLookActive)
    {
        if (constraints == null) return;

        for (int i = 0; i < constraints.Length; i++)
        {
            MultiAimConstraint constraint = constraints[i];
            if (!constraint) continue;

            bool constraintActive = baseActive || (playerLookActive && IsPlayerLookConstraint(constraint));
            constraint.weight = constraintActive ? activeWeight : inactiveWeight;
        }
    }


    private void RebuildStateHashes()
    {
        if (activeStateNames == null || activeStateNames.Length == 0)
        {
            activeStateHashes = null;
            return;
        }

        List<int> hashes = new List<int>(activeStateNames.Length);
        for (int i = 0; i < activeStateNames.Length; i++)
        {
            string stateName = activeStateNames[i];
            if (string.IsNullOrWhiteSpace(stateName)) continue;

            hashes.Add(Animator.StringToHash(stateName));
        }

        activeStateHashes = hashes.Count > 0 ? hashes.ToArray() : null;
    }
}
