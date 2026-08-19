// imports
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.Serialization;



// class
[DefaultExecutionOrder(-210)]
public class WeaponIKConstraint : MonoBehaviour
{
    private const string WeaponHolderName = "WeaponHolder";

    [Header("References")]
    // Animator used to query active states.
    [SerializeField] private Animator animator;

    // Weapon controller used to resolve the equipped weapon.
    [FormerlySerializedAs("playerWeaponController")]
    [SerializeField] private WeaponController weaponController;

    // Character state used to check whether the selected weapon is currently in-hand.
    [FormerlySerializedAs("playerState")]
    [SerializeField] private CharacterState characterState;

    // Root that contains equipped weapon models.
    [SerializeField] private Transform weaponHolder;

    // Left-hand Two Bone IK constraint to drive.
    [SerializeField] private TwoBoneIKConstraint leftHandConstraint;

    // Transform moved to the weapon's left-hand target.
    [SerializeField] private Transform leftHandIKTarget;

    // Transform moved to the weapon's left-hand hint target.
    [SerializeField] private Transform leftHandIKHint;

    [Header("Target")]
    // Prefix used to resolve left-hand weapon-specific targets.
    [FormerlySerializedAs("barrelTargetPrefix")]
    [FormerlySerializedAs("targetPrefix")]
    [SerializeField] private string leftTargetPrefix = "LeftHandTarget";

    // Prefix used to resolve left-hand weapon-specific hint targets.
    [FormerlySerializedAs("hintPrefix")]
    [SerializeField] private string leftHintPrefix = "LeftHandHint";

    // Also copy rotation from target when true.
    [SerializeField] private bool matchRotation;

    [Header("State Filter")]
    // Animator layer index to evaluate.
    [SerializeField] private int animatorLayer = 0;

    // State names that should enable the IK constraint (short names or full paths).
    [SerializeField] private string[] activeStateNames;

    // Keep IK enabled while blending into an allowed state.
    [SerializeField] private bool includeNextStateDuringTransition = true;

    [Header("Constraint Weights")]
    // Applied when an allowed state is active and a target is found.
    [SerializeField] [Range(0f, 1f)] private float activeWeight = 1f;

    // Applied when no allowed state is active or no target is found.
    [SerializeField] [Range(0f, 1f)] private float inactiveWeight = 0f;

    [Header("Activation Filters")]
    // Require the selected weapon to be marked as in-hand before IK can run.
    [SerializeField] private bool requireWeaponInHand = true;

    // Require the selected weapon category to be firearm-like before IK can run.
    [SerializeField] private bool requireFirearmCategory = true;

    // Serialized version for migration of activation filter defaults on existing scene data.
    [SerializeField] [HideInInspector] private int activationFilterVersion = 1;

    private const int CurrentActivationFilterVersion = 1;

    private int[] activeStateHashes;
    private bool hasLastActive;
    private bool lastActive;
    private string cachedWeaponName;
    private Transform cachedLeftTarget;
    private Transform cachedLeftHintTarget;
    private bool hasResolvedInitialReferences;


    private void Reset()
    {
        UpgradeActivationFilterDefaults();
        EnsureReferences();
        RebuildStateHashes();
    }


    private void Awake()
    {
        UpgradeActivationFilterDefaults();
        EnsureReferences();
        RebuildStateHashes();
    }


    private void OnValidate()
    {
        UpgradeActivationFilterDefaults();
        EnsureReferences();
        RebuildStateHashes();
        hasLastActive = false;
        cachedWeaponName = string.Empty;
        ClearCachedWeaponTargets();
    }


    private void Update()
    {
        if (!hasResolvedInitialReferences || MissingRequiredReferences())
            EnsureReferences();

        string equippedWeaponName;
        bool canUseWeaponTargets = TryGetActiveWeaponName(out equippedWeaponName);
        SyncWeaponCacheKey(equippedWeaponName);

        Transform leftTarget;
        bool hasLeftTarget = false;
        if (canUseWeaponTargets)
            hasLeftTarget = TryResolveWeaponTarget(equippedWeaponName, leftTargetPrefix, ref cachedLeftTarget, out leftTarget);
        else
            leftTarget = null;

        if (hasLeftTarget && leftHandIKTarget)
        {
            leftHandIKTarget.position = leftTarget.position;
            if (matchRotation)
                leftHandIKTarget.rotation = leftTarget.rotation;
        }

        Transform leftHintTarget;
        bool hasLeftHintTarget = false;
        if (canUseWeaponTargets)
            hasLeftHintTarget = TryResolveWeaponTarget(equippedWeaponName, leftHintPrefix, ref cachedLeftHintTarget, out leftHintTarget);
        else
            leftHintTarget = null;

        if (hasLeftHintTarget && leftHandIKHint)
        {
            leftHandIKHint.position = leftHintTarget.position;
            leftHandIKHint.rotation = leftHintTarget.rotation;
        }

        bool hasAnyDrivenTarget = hasLeftTarget && leftHandIKTarget;
        bool shouldBeActive = hasAnyDrivenTarget && ShouldActivateConstraints();
        if (hasLastActive && shouldBeActive == lastActive) return;

        ApplyConstraintWeight(shouldBeActive ? activeWeight : inactiveWeight);
        lastActive = shouldBeActive;
        hasLastActive = true;
    }


    private void EnsureReferences()
    {
        if (!animator)
            animator = GetComponentInParent<Animator>();

        if (!weaponController)
            weaponController = GetComponentInParent<WeaponController>();

        if (!characterState)
            characterState = GetComponentInParent<CharacterState>();

        if (!weaponHolder)
            weaponHolder = ResolveWeaponHolder();

        if (!leftHandConstraint)
        {
            TwoBoneIKConstraint[] constraints = GetComponentsInChildren<TwoBoneIKConstraint>(true);
            if (constraints.Length > 0)
                leftHandConstraint = constraints[0];
        }

        if (!leftHandIKTarget && leftHandConstraint)
            leftHandIKTarget = leftHandConstraint.data.target;

        if (!leftHandIKHint && leftHandConstraint)
            leftHandIKHint = leftHandConstraint.data.hint;

        hasResolvedInitialReferences = true;
    }


    private bool MissingRequiredReferences()
    {
        return !animator ||
               !weaponController ||
               !characterState ||
               !weaponHolder ||
               !leftHandConstraint ||
               !leftHandIKTarget;
    }


    private void UpgradeActivationFilterDefaults()
    {
        if (activationFilterVersion >= CurrentActivationFilterVersion)
            return;

        requireWeaponInHand = true;
        requireFirearmCategory = true;
        activationFilterVersion = CurrentActivationFilterVersion;
    }


    private Transform ResolveWeaponHolder()
    {
        if (weaponController)
        {
            Transform holder = weaponController.transform.Find(WeaponHolderName);
            if (holder) return holder;
        }

        Transform localHolder = transform.Find(WeaponHolderName);
        if (localHolder) return localHolder;

        return null;
    }


    private bool ShouldActivateConstraints()
    {
        if (!animator) return false;
        if (!leftHandConstraint) return false;
        if (activeStateHashes == null || activeStateHashes.Length == 0) return false;
        if (animatorLayer < 0 || animatorLayer >= animator.layerCount) return false;

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


    private void SyncWeaponCacheKey(string equippedWeaponName)
    {
        if (string.IsNullOrWhiteSpace(equippedWeaponName))
        {
            cachedWeaponName = string.Empty;
            ClearCachedWeaponTargets();
            return;
        }

        if (string.Equals(cachedWeaponName, equippedWeaponName, StringComparison.OrdinalIgnoreCase))
            return;

        cachedWeaponName = equippedWeaponName;
        ClearCachedWeaponTargets();
    }


    private void ClearCachedWeaponTargets()
    {
        cachedLeftTarget = null;
        cachedLeftHintTarget = null;
    }


    private bool TryResolveWeaponTarget(
        string equippedWeaponName,
        string targetPrefix,
        ref Transform cachedTarget,
        out Transform target)
    {
        target = null;

        if (string.IsNullOrWhiteSpace(equippedWeaponName))
        {
            cachedTarget = null;
            return false;
        }

        if (string.IsNullOrWhiteSpace(targetPrefix))
        {
            cachedTarget = null;
            return false;
        }

        if (cachedTarget && cachedTarget.gameObject.activeInHierarchy)
        {
            target = cachedTarget;
            return true;
        }

        cachedTarget = FindBestWeaponTarget(equippedWeaponName, targetPrefix);
        target = cachedTarget;
        return target;
    }


    private bool TryGetActiveWeaponName(out string weaponName)
    {
        weaponName = string.Empty;

        if (!weaponController)
            return false;

        string currentWeaponName = weaponController.GetCurrentWeaponName();
        if (string.IsNullOrWhiteSpace(currentWeaponName))
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

        weaponName = currentWeaponName;
        return true;
    }


    private bool IsFirearmCategory(string categoryName)
    {
        return string.Equals(categoryName, "Pistol", StringComparison.Ordinal)
            || string.Equals(categoryName, "SubmachineGun", StringComparison.Ordinal)
            || string.Equals(categoryName, "Rifle", StringComparison.Ordinal)
            || string.Equals(categoryName, "Shotgun", StringComparison.Ordinal)
            || string.Equals(categoryName, "Special", StringComparison.Ordinal);
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


    private Transform FindBestWeaponTarget(string equippedWeaponName, string targetPrefix)
    {
        if (!weaponHolder) return null;
        if (string.IsNullOrWhiteSpace(targetPrefix)) return null;

        string compactWeaponName = BuildCompactName(equippedWeaponName);
        string expectedTargetName = targetPrefix + compactWeaponName;
        string normalizedExpectedTargetName = NormalizeName(expectedTargetName);
        string normalizedPrefix = NormalizeName(targetPrefix);
        string normalizedWeaponName = NormalizeName(equippedWeaponName);
        bool hasWeaponName = string.IsNullOrWhiteSpace(normalizedWeaponName) == false;

        Transform[] transforms = weaponHolder.GetComponentsInChildren<Transform>(true);
        Transform bestMatch = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (!candidate) continue;

            string candidateName = candidate.name;
            if (string.IsNullOrWhiteSpace(candidateName))
                continue;

            string normalizedCandidateName = NormalizeName(candidateName);
            if (string.IsNullOrWhiteSpace(normalizedCandidateName))
                continue;

            if (!candidate.gameObject.activeInHierarchy)
                continue;

            int score;

            if (string.Equals(candidateName, expectedTargetName, StringComparison.OrdinalIgnoreCase))
            {
                score = 5000;
            }
            else if (string.Equals(normalizedCandidateName, normalizedExpectedTargetName, StringComparison.Ordinal))
            {
                score = 4500;
            }
            else
            {
                if (normalizedCandidateName.StartsWith(normalizedPrefix, StringComparison.Ordinal) == false)
                    continue;

                if (hasWeaponName && normalizedCandidateName.Contains(normalizedWeaponName) == false)
                    continue;

                score = 0;
            }

            if (hasWeaponName)
            {
                for (Transform parent = candidate.parent; parent; parent = parent.parent)
                {
                    string normalizedParentName = NormalizeName(parent.name);
                    if (string.IsNullOrWhiteSpace(normalizedParentName))
                        continue;

                    if (normalizedParentName.Contains(normalizedWeaponName)
                        || normalizedWeaponName.Contains(normalizedParentName))
                    {
                        score += 250;
                        break;
                    }
                }
            }

            score -= GetDepthFromRoot(candidate, weaponHolder);

            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = candidate;
            }
        }

        return bestMatch;
    }


    private int GetDepthFromRoot(Transform candidate, Transform root)
    {
        if (!candidate || !root) return int.MaxValue;

        int depth = 0;
        for (Transform current = candidate; current; current = current.parent)
        {
            if (current == root)
                return depth;

            depth++;
        }

        return int.MaxValue;
    }


    private void ApplyConstraintWeight(float weight)
    {
        if (leftHandConstraint)
            leftHandConstraint.weight = weight;
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


    private string BuildCompactName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        int length = value.Length;
        char[] chars = new char[length];
        int count = 0;

        for (int i = 0; i < length; i++)
        {
            char current = value[i];
            if (char.IsLetterOrDigit(current) == false) continue;

            chars[count] = current;
            count++;
        }

        return new string(chars, 0, count);
    }


    private string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        int length = value.Length;
        char[] chars = new char[length];
        int count = 0;

        for (int i = 0; i < length; i++)
        {
            char current = char.ToLowerInvariant(value[i]);
            if (char.IsLetterOrDigit(current) == false) continue;

            chars[count] = current;
            count++;
        }

        return new string(chars, 0, count);
    }
}
