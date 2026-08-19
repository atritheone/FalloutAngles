// imports
using System;
using UnityEngine;
using UnityEngine.Serialization;



// methods
public class GetGunPoint : MonoBehaviour
{
    private const string WeaponHolderName = "WeaponHolder";
    private const string WeaponInHandName = "WeaponInHand";

    // Optional direct override for the muzzle spawn point.
    [SerializeField] private Transform gunPoint;

    // Weapon controller used to resolve the currently equipped weapon.
    [FormerlySerializedAs("playerWeaponController")]
    [SerializeField] private WeaponController weaponController;

    // Root that contains equipped weapon models.
    [SerializeField] private Transform weaponHolder;

    // Child transform name to search for on the equipped weapon.
    [SerializeField] private string gunPointName = "Gunpoint";

    // Last resolved gun point for quick reuse.
    private Transform cachedGunPoint;

    // Weapon name associated with the cached gun point.
    private string cachedWeaponName;


    private void Awake()
    {
        EnsureReferences();
    }


    public Transform GetGunMarker()
    {
        // Prefer explicit manual override if set.
        if (gunPoint)
            return gunPoint;

        // Ensure references are initialized.
        EnsureReferences();

        // Resolve currently equipped weapon name.
        string equippedWeaponName = GetEquippedWeaponName();

        // Reuse cache only when still valid for the current weapon.
        if (cachedGunPoint
            && string.Equals(cachedWeaponName, equippedWeaponName, StringComparison.OrdinalIgnoreCase)
            && cachedGunPoint.gameObject.activeInHierarchy)
        {
            return cachedGunPoint;
        }

        // Search weapon holder for the best matching gun point.
        cachedGunPoint = FindBestGunPoint(equippedWeaponName);
        cachedWeaponName = equippedWeaponName;
        return cachedGunPoint;
    }

    
    private void EnsureReferences()
    {
        // Auto-find weapon controller if not set.
        if (!weaponController)
            weaponController = GetComponentInParent<WeaponController>();

        if (!weaponController)
            weaponController = FindAnyObjectByType<WeaponController>();

        // Auto-find weapon holder if not set.
        if (!weaponHolder)
            weaponHolder = ResolveWeaponHolder();
    }


    private Transform ResolveWeaponHolder()
    {
        WeaponController controller = weaponController;

        // NPCs keep equipped models directly under WeaponInHand, where this provider lives.
        if (string.Equals(transform.name, WeaponInHandName, StringComparison.OrdinalIgnoreCase))
            return transform;

        Transform localWeaponInHand = FindDescendantByName(transform, WeaponInHandName);
        if (localWeaponInHand) return localWeaponInHand;

        // Prefer the child under the weapon controller.
        if (controller)
        {
            Transform weaponInHand = controller.transform.Find(WeaponInHandName);
            if (weaponInHand) return weaponInHand;

            Transform holder = controller.transform.Find(WeaponHolderName);
            if (holder) return holder;
        }

        // Fallback to this object hierarchy.
        Transform localHolder = transform.Find(WeaponHolderName);
        if (localHolder) return localHolder;

        return null;
    }

    private static Transform FindDescendantByName(Transform root, string targetName)
    {
        if (!root || string.IsNullOrWhiteSpace(targetName))
            return null;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (!candidate) continue;
            if (string.Equals(candidate.name, targetName, StringComparison.OrdinalIgnoreCase))
                return candidate;
        }

        return null;
    }


    private string GetEquippedWeaponName()
    {
        return weaponController ? weaponController.GetCurrentWeaponName() : string.Empty;
    }


    private Transform FindBestGunPoint(string equippedWeaponName)
    {
        Transform holder = weaponHolder;
        if (!holder) return null;

        // Prefer the gun point under the currently equipped weapon model first.
        if (TryFindGunPointOnEquippedWeapon(holder, equippedWeaponName, out Transform equippedGunPoint))
            return equippedGunPoint;

        string normalizedEquippedName = NormalizeName(equippedWeaponName);
        bool hasNormalizedEquippedName = !string.IsNullOrWhiteSpace(normalizedEquippedName);
        string targetPointName = gunPointName;
        Transform[] transforms = holder.GetComponentsInChildren<Transform>(true);

        Transform bestMatch = null;
        int bestScore = int.MinValue;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (!candidate) continue;
            if (!string.Equals(candidate.name, targetPointName, StringComparison.OrdinalIgnoreCase)) continue;

            int score = 0;

            // Prefer active equipped model gun points.
            if (candidate.gameObject.activeInHierarchy)
                score += 1000;

            // Prefer candidates whose parent chain resembles the equipped weapon name.
            if (hasNormalizedEquippedName)
            {
                for (Transform parent = candidate.parent; parent; parent = parent.parent)
                {
                    string normalizedParentName = NormalizeName(parent.name);
                    if (normalizedParentName.Contains(normalizedEquippedName)
                        || normalizedEquippedName.Contains(normalizedParentName))
                    {
                        score += 100;
                        break;
                    }

                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestMatch = candidate;
            }
        }

        return bestMatch;
    }

    private bool TryFindGunPointOnEquippedWeapon(Transform holder, string equippedWeaponName, out Transform gunPointTransform)
    {
        gunPointTransform = null;
        if (!holder) return false;

        string normalizedEquippedName = NormalizeName(equippedWeaponName);
        if (string.IsNullOrWhiteSpace(normalizedEquippedName))
            return false;

        Transform[] transforms = holder.GetComponentsInChildren<Transform>(true);
        Transform bestWeaponRoot = null;
        int bestWeaponRootScore = int.MinValue;

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (!candidate) continue;
            if (candidate == holder) continue;

            string normalizedCandidateName = NormalizeName(candidate.name);
            if (string.IsNullOrWhiteSpace(normalizedCandidateName))
                continue;

            bool nameLooksLikeEquippedWeapon =
                normalizedCandidateName.Contains(normalizedEquippedName)
                || normalizedEquippedName.Contains(normalizedCandidateName);

            if (!nameLooksLikeEquippedWeapon)
                continue;

            int score = 0;

            // Prefer direct weapon-holder children (actual weapon roots) over deep descendants.
            if (candidate.parent == holder)
                score += 1000;

            // Prefer currently active model roots.
            if (candidate.gameObject.activeInHierarchy)
                score += 100;

            // Prefer exact normalized name matches when available.
            if (string.Equals(normalizedCandidateName, normalizedEquippedName, StringComparison.Ordinal))
                score += 50;

            // Prefer shallower transforms to avoid picking random internal nodes.
            score -= GetDepthFromRoot(candidate, holder);

            if (score <= bestWeaponRootScore)
                continue;

            bestWeaponRootScore = score;
            bestWeaponRoot = candidate;
        }

        if (!bestWeaponRoot)
            return false;

        string targetPointName = gunPointName;
        Transform[] weaponTransforms = bestWeaponRoot.GetComponentsInChildren<Transform>(true);
        Transform activeMatch = null;
        Transform inactiveMatch = null;

        for (int i = 0; i < weaponTransforms.Length; i++)
        {
            Transform candidate = weaponTransforms[i];
            if (!candidate) continue;
            if (!string.Equals(candidate.name, targetPointName, StringComparison.OrdinalIgnoreCase)) continue;

            if (candidate.gameObject.activeInHierarchy)
            {
                activeMatch = candidate;
                break;
            }

            if (!inactiveMatch)
                inactiveMatch = candidate;
        }

        gunPointTransform = activeMatch ? activeMatch : inactiveMatch;
        return gunPointTransform;
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


    private string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        int rawLength = value.Length;
        char[] cleanChars = new char[rawLength];
        int count = 0;

        for (int i = 0; i < rawLength; i++)
        {
            char current = char.ToLowerInvariant(value[i]);
            if (char.IsLetterOrDigit(current) == false) continue;

            cleanChars[count] = current;
            count++;
        }

        return new string(cleanChars, 0, count);
    }
}
