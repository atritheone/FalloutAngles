using System;
using System.Collections.Generic;
using UnityEngine;

public class NPCWeaponController : WeaponController
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
    public class WeaponEntry
    {
        public WeaponCategory Category;
        public string WeaponName;
    }

    [Serializable]
    public class WeaponAmmoEntry
    {
        public WeaponCategory Category;
        public string WeaponName;
        [Min(0)] public int CurrentAmmo;
        [Min(0)] public int ReserveAmmo;
    }

    [Serializable]
    public class WeaponModelBinding
    {
        public WeaponCategory Category;
        public string WeaponName;
        public GameObject EquippedModel;
        public GameObject HolsteredModel;
    }

    [Serializable]
    private class EquipAnimationSettings
    {
        public string EquipStateName;
        public string UnequipStateName;
        [Range(0f, 1f)] public float EquipEnableTime = 0f;
        [Range(0f, 1f)] public float UnequipDisableTime = 0f;
        [Range(0f, 1f)] public float EquipHolsterDisableTime = 0.35f;
        [Range(0f, 1f)] public float UnequipHolsterReEnableTime = 0f;
        [NonSerialized] public int EquipStateHash;
        [NonSerialized] public int UnequipStateHash;
    }

    private static readonly int EquippedUnarmedParam = Animator.StringToHash("EquippedUnarmed");
    private static readonly int EquippedKnifeParam = Animator.StringToHash("EquippedKnife");
    private static readonly int EquippedTwoHandedParam = Animator.StringToHash("EquippedTwoHanded");
    private static readonly int EquippedBowParam = Animator.StringToHash("EquippedBow");
    private static readonly int EquippedPistolParam = Animator.StringToHash("EquippedPistol");
    private static readonly int EquippedSubmachineGunParam = Animator.StringToHash("EquippedSubmachineGun");
    private static readonly int EquippedRifleParam = Animator.StringToHash("EquippedRifle");
    private static readonly int EquippedShotgunParam = Animator.StringToHash("EquippedShotgun");
    private static readonly int EquippedLongarmParam = Animator.StringToHash("EquippedLongarm");
    private static readonly int EquippedSpecialParam = Animator.StringToHash("EquippedSpecial");
    private static readonly int EquippedExplosiveParam = Animator.StringToHash("EquippedExplosive");

    [Header("References")]
    [SerializeField] private NPCState npcState;
    [SerializeField] private NPCInventory npcInventory;
    [SerializeField] private Animator animator;
    [SerializeField] private Transform weaponHolder;
    [SerializeField] private Transform weaponHolster;

    [Header("Weapon List")]
    [SerializeField] private List<WeaponEntry> weapons = new List<WeaponEntry>();
    [SerializeField] private int equippedWeaponIndex;

    [Header("Weapon Models")]
    [SerializeField] private List<WeaponModelBinding> weaponModelBindings = new List<WeaponModelBinding>();
    [SerializeField] private bool autoResolveMissingModelBindings = true;

    [Header("NPC Equip Behaviour")]
    [SerializeField] private bool syncWeaponInHandToCombatMode = false;

    [Header("Animation Timing")]
    [SerializeField] private EquipAnimationSettings knifeAnimation = new EquipAnimationSettings
    {
        EquipStateName = "Knife Equip",
        UnequipStateName = "Knife Unequip"
    };
    [SerializeField] private EquipAnimationSettings twoHandedAnimation = new EquipAnimationSettings
    {
        EquipStateName = "Two Handed Equip",
        UnequipStateName = "Two Handed Unequip"
    };
    [SerializeField] private EquipAnimationSettings pistolAnimation = new EquipAnimationSettings
    {
        EquipStateName = "Pistol Equip",
        UnequipStateName = "Pistol Unequip"
    };
    [SerializeField] private EquipAnimationSettings longarmAnimation = new EquipAnimationSettings
    {
        EquipStateName = "Longarm Equip",
        UnequipStateName = "Longarm Unequip"
    };

    [Header("Current Weapon Ammo")]
    [SerializeField, Min(0)] private int currentWeaponAmmo;
    [SerializeField, Min(0)] private int currentWeaponReserveAmmo;
    [SerializeField] private List<WeaponAmmoEntry> weaponAmmo = new List<WeaponAmmoEntry>();
    [SerializeField] private WeaponCategory trackedAmmoCategory = WeaponCategory.Unarmed;
    [SerializeField] private string trackedAmmoWeaponName = string.Empty;
    [SerializeField] private string equippedInventoryWeaponInstanceId = string.Empty;

    private WeaponEntry currentWeapon;
    private WeaponCategory currentCategory;
    private int knifeEquipStateHash;
    private int knifeUnequipStateHash;
    private int twoHandedEquipStateHash;
    private int twoHandedUnequipStateHash;
    private int pistolEquipStateHash;
    private int pistolUnequipStateHash;
    private int longarmEquipStateHash;
    private int longarmUnequipStateHash;
    private int lastAnimatorStateHash = -1;
    private bool equipHandOffTriggered;
    private bool unequipHandOffTriggered;
    private bool holsterDisableTriggered;
    private bool holsterReEnableScheduled;
    private bool holsterReEnableTriggered;
    private float holsterReEnableTime;
    private GameObject holsterPendingReEnable;
    private WeaponModelBinding currentWeaponBinding;
    private bool weaponVisibilityDirty = true;
    private bool animatorCategoryParametersDirty = true;
    private bool hasLastWeaponInHand;
    private bool lastWeaponInHand;

    private void Awake()
    {
        ResolveReferences();
        EnsureAnimationSettings();
        CacheStateHashes();
        if (npcState)
            npcState.SetWeaponInHand(false);

        EnsureDefaultWeaponList();
        EnsureWeaponAmmoRecords();
        equippedWeaponIndex = Mathf.Clamp(equippedWeaponIndex, 0, Mathf.Max(0, weapons.Count - 1));
        EquipByIndex(equippedWeaponIndex);
        UpdateWeaponVisibility();
    }

    private void OnValidate()
    {
        EnsureAnimationSettings();
        CacheStateHashes();
        EnsureDefaultWeaponList();
        SaveTrackedAmmoValues();
        EnsureWeaponAmmoRecords();

        if (weapons == null || weapons.Count == 0)
            return;

        equippedWeaponIndex = Mathf.Clamp(equippedWeaponIndex, 0, weapons.Count - 1);
        currentWeapon = weapons[equippedWeaponIndex];
        currentCategory = currentWeapon != null ? currentWeapon.Category : WeaponCategory.Unarmed;
        currentWeaponBinding = null;
        LoadTrackedAmmoValuesForCurrentWeapon();
        MarkAnimatorCategoryParametersDirty();
        MarkWeaponVisibilityDirty();
    }

    private void Update()
    {
        if (syncWeaponInHandToCombatMode && npcState)
            SetNpcWeaponInHand(npcState.GetCombatMode() && currentCategory != WeaponCategory.Unarmed);

        UpdateWeaponVisibilityFromAnimator();

        bool weaponInHand = npcState && npcState.GetWeaponInHand();
        if (!hasLastWeaponInHand || weaponInHand != lastWeaponInHand)
        {
            hasLastWeaponInHand = true;
            lastWeaponInHand = weaponInHand;
            MarkWeaponVisibilityDirty();
        }

        if (weaponVisibilityDirty)
            UpdateWeaponVisibility();

        if (animatorCategoryParametersDirty)
            UpdateAnimatorCategoryParameters();
    }

    public void EquipByIndex(int index)
    {
        if (weapons == null || weapons.Count == 0)
            return;

        if (index < 0 || index >= weapons.Count)
            return;

        SaveTrackedAmmoValues();

        if (index == equippedWeaponIndex && currentWeapon != null)
        {
            currentWeapon = weapons[equippedWeaponIndex];
            currentCategory = currentWeapon != null ? currentWeapon.Category : WeaponCategory.Unarmed;
            LoadTrackedAmmoValuesForCurrentWeapon();
            RefreshCurrentWeaponBinding();
            MarkWeaponVisibilityDirty();
            MarkAnimatorCategoryParametersDirty();
            return;
        }

        equippedInventoryWeaponInstanceId = string.Empty;
        equippedWeaponIndex = index;
        currentWeapon = weapons[equippedWeaponIndex];
        currentCategory = currentWeapon != null ? currentWeapon.Category : WeaponCategory.Unarmed;
        LoadTrackedAmmoValuesForCurrentWeapon();
        RefreshCurrentWeaponBinding();

        if (npcState)
            SetNpcWeaponInHand(false);

        ResetAnimationVisibilityState();
        MarkWeaponVisibilityDirty();
        MarkAnimatorCategoryParametersDirty();
    }

    public bool TryEquipWeapon(WeaponCategory category, string weaponName)
    {
        if (weapons == null)
            return false;

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponEntry entry = weapons[i];
            if (entry == null) continue;
            if (entry.Category != category) continue;
            if (!string.Equals(entry.WeaponName, weaponName, StringComparison.OrdinalIgnoreCase)) continue;

            EquipByIndex(i);
            return true;
        }

        return false;
    }

    public bool TryEquipWeaponByName(string weaponName)
    {
        if (string.IsNullOrWhiteSpace(weaponName) || weapons == null)
            return false;

        int index = FindWeaponIndexByName(weaponName);
        if (index >= 0)
        {
            EquipByIndex(index);
            return true;
        }

        return false;
    }

    public bool TryEquipWeaponDefinition(WeaponDefinition weaponDefinition)
    {
        if (!weaponDefinition)
            return false;

        if (weapons == null)
            weapons = new List<WeaponEntry>();

        string weaponName = ResolveWeaponDefinitionName(weaponDefinition);
        if (string.IsNullOrWhiteSpace(weaponName))
            return false;

        int existingIndex = FindWeaponIndexByName(weaponName);
        if (existingIndex >= 0)
        {
            EquipByIndex(existingIndex);
            return true;
        }

        WeaponCategory category = ResolveWeaponDefinitionCategory(weaponDefinition);
        weapons.Add(new WeaponEntry
        {
            Category = category,
            WeaponName = weaponName
        });

        EnsureWeaponAmmoRecords();
        EquipByIndex(weapons.Count - 1);
        return true;
    }

    private int FindWeaponIndexByName(string weaponName)
    {
        if (string.IsNullOrWhiteSpace(weaponName) || weapons == null)
            return -1;

        string compactWeaponName = CompactName(weaponName);
        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponEntry entry = weapons[i];
            if (entry == null) continue;
            if (string.Equals(entry.WeaponName, weaponName, StringComparison.OrdinalIgnoreCase))
                return i;

            if (!string.IsNullOrWhiteSpace(compactWeaponName) &&
                string.Equals(CompactName(entry.WeaponName), compactWeaponName, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    public bool TryEquipUnarmed()
    {
        return TryEquipWeapon(WeaponCategory.Unarmed, "Unarmed");
    }

    public void EquipNext()
    {
        if (weapons == null || weapons.Count == 0)
            return;

        EquipByIndex((equippedWeaponIndex + 1) % weapons.Count);
    }

    public void EquipPrevious()
    {
        if (weapons == null || weapons.Count == 0)
            return;

        int previousIndex = equippedWeaponIndex - 1;
        if (previousIndex < 0)
            previousIndex = weapons.Count - 1;

        EquipByIndex(previousIndex);
    }

    public WeaponEntry GetCurrentWeapon()
    {
        return currentWeapon;
    }

    public WeaponCategory GetCurrentCategory()
    {
        return currentCategory;
    }

    public override string GetCurrentCategoryName()
    {
        return currentCategory.ToString();
    }

    public override string GetCurrentWeaponName()
    {
        return currentWeapon != null ? currentWeapon.WeaponName : string.Empty;
    }

    public int GetEquippedWeaponIndex()
    {
        return equippedWeaponIndex;
    }

    public int GetWeaponCount()
    {
        return weapons != null ? weapons.Count : 0;
    }

    public bool GetSyncWeaponInHandToCombatMode()
    {
        return syncWeaponInHandToCombatMode;
    }

    public void SetSyncWeaponInHandToCombatMode(bool sync)
    {
        syncWeaponInHandToCombatMode = sync;
    }

    public int GetCurrentWeaponAmmo()
    {
        return currentWeaponAmmo;
    }

    public int GetCurrentWeaponReserveAmmo()
    {
        return currentWeaponReserveAmmo;
    }

    public void SetCurrentWeaponAmmo(int ammo)
    {
        currentWeaponAmmo = Mathf.Max(0, ammo);
        SaveTrackedAmmoValues();
    }

    public void SetCurrentWeaponReserveAmmo(int ammo)
    {
        currentWeaponReserveAmmo = Mathf.Max(0, ammo);
        SaveTrackedAmmoValues();
    }

    public void SetEquippedInventoryWeaponInstanceId(string instanceId)
    {
        equippedInventoryWeaponInstanceId = string.IsNullOrWhiteSpace(instanceId) ? string.Empty : instanceId.Trim();
    }

    public string GetEquippedInventoryWeaponInstanceId()
    {
        return equippedInventoryWeaponInstanceId;
    }

    public bool IsWeaponInHand()
    {
        return npcState && npcState.GetWeaponInHand();
    }

    public bool IsFirearmEquipped()
    {
        return IsFirearmCategory(currentCategory);
    }

    public bool IsLongarmEquipped()
    {
        return IsLongarmCategory(currentCategory);
    }

    public bool IsEquipAnimationPlaying()
    {
        if (!animator || 0 >= animator.layerCount)
            return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (IsEquipStateInProgress(current))
            return true;

        if (!animator.IsInTransition(0))
            return false;

        AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
        return IsEquipStateInProgress(next);
    }

    private void UpdateWeaponVisibilityFromAnimator()
    {
        if (!animator || 0 >= animator.layerCount)
            return;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        int stateHash = stateInfo.shortNameHash;
        if (stateHash != lastAnimatorStateHash)
        {
            lastAnimatorStateHash = stateHash;
            equipHandOffTriggered = false;
            unequipHandOffTriggered = false;
            holsterDisableTriggered = false;
            MarkWeaponVisibilityDirty();
        }

        if (!TryGetAnimationSettingsForState(stateHash, out EquipAnimationSettings settings))
            return;

        WeaponModelBinding binding = RefreshCurrentWeaponBinding();
        if (binding == null)
            return;

        if (!holsterReEnableScheduled && stateHash == settings.UnequipStateHash)
        {
            float stateDuration = Mathf.Max(0f, stateInfo.length);
            holsterReEnableScheduled = true;
            holsterReEnableTriggered = false;
            holsterReEnableTime = Time.time + stateDuration * settings.UnequipHolsterReEnableTime;
            holsterPendingReEnable = binding.HolsteredModel;
        }

        float normalizedTime = stateInfo.normalizedTime;
        if (!equipHandOffTriggered &&
            stateHash == settings.EquipStateHash &&
            normalizedTime >= settings.EquipEnableTime)
        {
            equipHandOffTriggered = true;
            SetActive(binding.EquippedModel, true);
            SetNpcWeaponInHand(true);
        }

        if (!holsterDisableTriggered &&
            stateHash == settings.EquipStateHash &&
            normalizedTime >= settings.EquipHolsterDisableTime)
        {
            holsterDisableTriggered = true;
            holsterPendingReEnable = binding.HolsteredModel;
            SetActive(binding.HolsteredModel, false);
        }

        if (!unequipHandOffTriggered &&
            stateHash == settings.UnequipStateHash &&
            normalizedTime >= settings.UnequipDisableTime)
        {
            unequipHandOffTriggered = true;
            SetActive(binding.EquippedModel, false);
            SetNpcWeaponInHand(false);
        }

        if (holsterReEnableScheduled &&
            !holsterReEnableTriggered &&
            Time.time >= holsterReEnableTime)
        {
            holsterReEnableTriggered = true;
            holsterReEnableScheduled = false;
            SetActive(holsterPendingReEnable, true);
            holsterPendingReEnable = null;
        }
    }

    private bool IsCurrentAnimatorStateTimedEquipState()
    {
        if (!animator || 0 >= animator.layerCount)
            return false;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        return IsEquipOrUnequipState(stateInfo.shortNameHash);
    }

    private bool IsEquipStateInProgress(AnimatorStateInfo stateInfo)
    {
        return IsEquipOrUnequipState(stateInfo.shortNameHash) && stateInfo.normalizedTime < 1f;
    }

    private bool IsEquipOrUnequipState(int stateHash)
    {
        return stateHash == knifeEquipStateHash ||
               stateHash == knifeUnequipStateHash ||
               stateHash == twoHandedEquipStateHash ||
               stateHash == twoHandedUnequipStateHash ||
               stateHash == pistolEquipStateHash ||
               stateHash == pistolUnequipStateHash ||
               stateHash == longarmEquipStateHash ||
               stateHash == longarmUnequipStateHash;
    }

    private bool TryGetAnimationSettingsForState(int stateHash, out EquipAnimationSettings settings)
    {
        settings = null;
        if (stateHash == knifeEquipStateHash || stateHash == knifeUnequipStateHash)
            settings = knifeAnimation;
        else if (stateHash == twoHandedEquipStateHash || stateHash == twoHandedUnequipStateHash)
            settings = twoHandedAnimation;
        else if (stateHash == pistolEquipStateHash || stateHash == pistolUnequipStateHash)
            settings = pistolAnimation;
        else if (stateHash == longarmEquipStateHash || stateHash == longarmUnequipStateHash)
            settings = longarmAnimation;

        return settings != null;
    }

    private void EnsureAnimationSettings()
    {
        if (knifeAnimation == null)
            knifeAnimation = new EquipAnimationSettings();
        if (twoHandedAnimation == null)
            twoHandedAnimation = new EquipAnimationSettings();
        if (pistolAnimation == null)
            pistolAnimation = new EquipAnimationSettings();
        if (longarmAnimation == null)
            longarmAnimation = new EquipAnimationSettings();

        if (string.IsNullOrWhiteSpace(knifeAnimation.EquipStateName))
            knifeAnimation.EquipStateName = "Knife Equip";
        if (string.IsNullOrWhiteSpace(knifeAnimation.UnequipStateName))
            knifeAnimation.UnequipStateName = "Knife Unequip";
        if (string.IsNullOrWhiteSpace(twoHandedAnimation.EquipStateName))
            twoHandedAnimation.EquipStateName = "Two Handed Equip";
        if (string.IsNullOrWhiteSpace(twoHandedAnimation.UnequipStateName))
            twoHandedAnimation.UnequipStateName = "Two Handed Unequip";
        if (string.IsNullOrWhiteSpace(pistolAnimation.EquipStateName))
            pistolAnimation.EquipStateName = "Pistol Equip";
        if (string.IsNullOrWhiteSpace(pistolAnimation.UnequipStateName))
            pistolAnimation.UnequipStateName = "Pistol Unequip";
        if (string.IsNullOrWhiteSpace(longarmAnimation.EquipStateName))
            longarmAnimation.EquipStateName = "Longarm Equip";
        if (string.IsNullOrWhiteSpace(longarmAnimation.UnequipStateName))
            longarmAnimation.UnequipStateName = "Longarm Unequip";
    }

    private void CacheStateHashes()
    {
        knifeEquipStateHash = Animator.StringToHash(knifeAnimation.EquipStateName);
        knifeUnequipStateHash = Animator.StringToHash(knifeAnimation.UnequipStateName);
        twoHandedEquipStateHash = Animator.StringToHash(twoHandedAnimation.EquipStateName);
        twoHandedUnequipStateHash = Animator.StringToHash(twoHandedAnimation.UnequipStateName);
        pistolEquipStateHash = Animator.StringToHash(pistolAnimation.EquipStateName);
        pistolUnequipStateHash = Animator.StringToHash(pistolAnimation.UnequipStateName);
        longarmEquipStateHash = Animator.StringToHash(longarmAnimation.EquipStateName);
        longarmUnequipStateHash = Animator.StringToHash(longarmAnimation.UnequipStateName);

        knifeAnimation.EquipStateHash = knifeEquipStateHash;
        knifeAnimation.UnequipStateHash = knifeUnequipStateHash;
        twoHandedAnimation.EquipStateHash = twoHandedEquipStateHash;
        twoHandedAnimation.UnequipStateHash = twoHandedUnequipStateHash;
        pistolAnimation.EquipStateHash = pistolEquipStateHash;
        pistolAnimation.UnequipStateHash = pistolUnequipStateHash;
        longarmAnimation.EquipStateHash = longarmEquipStateHash;
        longarmAnimation.UnequipStateHash = longarmUnequipStateHash;
    }

    private void ResetAnimationVisibilityState()
    {
        lastAnimatorStateHash = -1;
        equipHandOffTriggered = false;
        unequipHandOffTriggered = false;
        holsterDisableTriggered = false;
        holsterReEnableScheduled = false;
        holsterReEnableTriggered = false;
        holsterPendingReEnable = null;
    }

    public void SetWeaponInHandImmediate(bool inHand)
    {
        SetNpcWeaponInHand(inHand && currentCategory != WeaponCategory.Unarmed);

        MarkWeaponVisibilityDirty();
    }

    public void HideEquippedWeaponInHandImmediate()
    {
        SetActive(currentWeaponBinding != null ? currentWeaponBinding.EquippedModel : GetBindingForCurrentWeapon()?.EquippedModel, false);
        SetNpcWeaponInHand(false);
        MarkWeaponVisibilityDirty();
    }

    private void ResolveReferences()
    {
        if (!npcState)
            npcState = GetComponentInParent<NPCState>();

        if (!npcInventory)
            npcInventory = GetComponentInParent<NPCInventory>();

        if (!animator)
            animator = GetComponentInChildren<Animator>(true);

        if (!weaponHolder)
            weaponHolder = transform.Find("WeaponHolder");

        if (!weaponHolster)
            weaponHolster = transform.Find("WeaponHolster");
    }

    private void UpdateWeaponVisibility()
    {
        weaponVisibilityDirty = false;

        bool isTimedState = IsCurrentAnimatorStateTimedEquipState();
        bool weaponInHand = npcState && npcState.GetWeaponInHand();
        lastWeaponInHand = weaponInHand;
        hasLastWeaponInHand = true;
        string currentWeaponName = currentWeapon != null ? currentWeapon.WeaponName : null;

        for (int i = 0; i < weaponModelBindings.Count; i++)
        {
            WeaponModelBinding binding = weaponModelBindings[i];
            if (binding == null) continue;

            bool isCurrentWeapon = IsBindingForWeapon(binding, currentCategory, currentWeaponName);
            if (isCurrentWeapon && isTimedState)
                continue;

            SetActive(binding.EquippedModel, isCurrentWeapon && weaponInHand);
            SetActive(binding.HolsteredModel, isCurrentWeapon && !weaponInHand);
        }

        if (!autoResolveMissingModelBindings)
            return;

        WeaponModelBinding currentBinding = RefreshCurrentWeaponBinding();
        if (currentBinding == null)
            return;

        bool currentWeaponInHand = npcState && npcState.GetWeaponInHand();
        if (isTimedState)
            return;

        SetActive(currentBinding.EquippedModel, currentWeaponInHand);
        SetActive(currentBinding.HolsteredModel, !currentWeaponInHand);
    }

    private void MarkWeaponVisibilityDirty()
    {
        weaponVisibilityDirty = true;
    }

    private void MarkAnimatorCategoryParametersDirty()
    {
        animatorCategoryParametersDirty = true;
    }

    private void SetNpcWeaponInHand(bool inHand)
    {
        if (!npcState)
            return;

        if (npcState.GetWeaponInHand() == inHand)
            return;

        npcState.SetWeaponInHand(inHand);
        lastWeaponInHand = inHand;
        hasLastWeaponInHand = true;
        MarkWeaponVisibilityDirty();
    }

    private WeaponModelBinding RefreshCurrentWeaponBinding()
    {
        if (currentWeaponBinding != null &&
            currentWeapon != null &&
            IsBindingForWeapon(currentWeaponBinding, currentCategory, currentWeapon.WeaponName))
        {
            return currentWeaponBinding;
        }

        currentWeaponBinding = autoResolveMissingModelBindings
            ? GetOrCreateBindingForCurrentWeapon()
            : GetBindingForCurrentWeapon();

        return currentWeaponBinding;
    }

    private WeaponModelBinding GetOrCreateBindingForCurrentWeapon()
    {
        if (currentWeapon == null || string.IsNullOrWhiteSpace(currentWeapon.WeaponName))
            return null;

        WeaponModelBinding binding = GetBindingForCurrentWeapon();
        if (binding == null)
        {
            binding = new WeaponModelBinding
            {
                Category = currentCategory,
                WeaponName = currentWeapon.WeaponName
            };
            weaponModelBindings.Add(binding);
        }

        if (!binding.EquippedModel)
            binding.EquippedModel = FindWeaponModel(weaponHolder, currentWeapon.WeaponName, true);

        if (!binding.HolsteredModel)
            binding.HolsteredModel = FindWeaponModel(weaponHolster, currentWeapon.WeaponName, false);

        currentWeaponBinding = binding;
        return binding;
    }

    private WeaponModelBinding GetBindingForCurrentWeapon()
    {
        if (currentWeapon == null)
            return null;

        for (int i = 0; i < weaponModelBindings.Count; i++)
        {
            WeaponModelBinding binding = weaponModelBindings[i];
            if (binding == null) continue;
            if (IsBindingForWeapon(binding, currentCategory, currentWeapon.WeaponName))
                return binding;
        }

        return null;
    }

    private static bool IsBindingForWeapon(WeaponModelBinding binding, WeaponCategory category, string weaponName)
    {
        return binding.Category == category
               && string.Equals(binding.WeaponName, weaponName, StringComparison.OrdinalIgnoreCase);
    }

    private static GameObject FindWeaponModel(Transform root, string weaponName, bool equipped)
    {
        if (!root || string.IsNullOrWhiteSpace(weaponName))
            return null;

        string compactName = CompactName(weaponName);
        string suffix = equipped ? "Equipped" : "Holstered";
        string compactWithSuffix = compactName + suffix.ToLowerInvariant();

        Transform direct = root.Find(compactName);
        if (direct) return direct.gameObject;

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            Transform child = children[i];
            if (!child) continue;

            string childCompactName = CompactName(child.name);
            if (childCompactName == compactName || childCompactName == compactWithSuffix)
                return child.gameObject;
        }

        return null;
    }

    private void UpdateAnimatorCategoryParameters()
    {
        if (!CanUpdateAnimatorCategoryParameters())
            return;

        bool isLongarm = IsLongarmCategory(currentCategory);
        animator.SetBool(EquippedUnarmedParam, currentCategory == WeaponCategory.Unarmed);
        animator.SetBool(EquippedKnifeParam, currentCategory == WeaponCategory.Knife);
        animator.SetBool(EquippedTwoHandedParam, currentCategory == WeaponCategory.TwoHanded);
        animator.SetBool(EquippedBowParam, currentCategory == WeaponCategory.Bow);
        animator.SetBool(EquippedPistolParam, currentCategory == WeaponCategory.Pistol);
        animator.SetBool(EquippedSubmachineGunParam, currentCategory == WeaponCategory.SubmachineGun);
        animator.SetBool(EquippedRifleParam, currentCategory == WeaponCategory.Rifle);
        animator.SetBool(EquippedShotgunParam, currentCategory == WeaponCategory.Shotgun);
        animator.SetBool(EquippedLongarmParam, isLongarm);
        animator.SetBool(EquippedSpecialParam, currentCategory == WeaponCategory.Special);
        animator.SetBool(EquippedExplosiveParam, currentCategory == WeaponCategory.Explosive);
        animatorCategoryParametersDirty = false;
    }

    private bool CanUpdateAnimatorCategoryParameters()
    {
        if (!animator)
            return false;

        if (!animator.runtimeAnimatorController)
            return false;

        // OnValidate can run before the Animator has actually bound its controller.
        return animator.isInitialized;
    }

    private void EnsureWeaponAmmoRecords()
    {
        if (weaponAmmo == null)
            weaponAmmo = new List<WeaponAmmoEntry>();

        if (weapons == null)
            return;

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponEntry weaponEntry = weapons[i];
            if (weaponEntry == null || string.IsNullOrWhiteSpace(weaponEntry.WeaponName)) continue;
            if (FindAmmoRecord(weaponEntry.Category, weaponEntry.WeaponName) != null) continue;

            weaponAmmo.Add(new WeaponAmmoEntry
            {
                Category = weaponEntry.Category,
                WeaponName = weaponEntry.WeaponName
            });
        }
    }

    private WeaponAmmoEntry FindAmmoRecord(WeaponCategory category, string weaponName)
    {
        if (weaponAmmo == null)
            return null;

        for (int i = 0; i < weaponAmmo.Count; i++)
        {
            WeaponAmmoEntry ammoEntry = weaponAmmo[i];
            if (ammoEntry == null) continue;
            if (ammoEntry.Category != category) continue;
            if (string.Equals(ammoEntry.WeaponName, weaponName, StringComparison.OrdinalIgnoreCase))
                return ammoEntry;
        }

        return null;
    }

    private WeaponAmmoEntry GetOrCreateAmmoRecord(WeaponCategory category, string weaponName)
    {
        WeaponAmmoEntry ammoEntry = FindAmmoRecord(category, weaponName);
        if (ammoEntry != null)
            return ammoEntry;

        ammoEntry = new WeaponAmmoEntry
        {
            Category = category,
            WeaponName = weaponName
        };

        weaponAmmo.Add(ammoEntry);
        return ammoEntry;
    }

    private void SaveTrackedAmmoValues()
    {
        if (string.IsNullOrWhiteSpace(trackedAmmoWeaponName))
            return;

        WeaponAmmoEntry ammoEntry = GetOrCreateAmmoRecord(trackedAmmoCategory, trackedAmmoWeaponName);
        ammoEntry.CurrentAmmo = Mathf.Max(0, currentWeaponAmmo);
        ammoEntry.ReserveAmmo = Mathf.Max(0, currentWeaponReserveAmmo);
    }

    private void LoadTrackedAmmoValuesForCurrentWeapon()
    {
        if (currentWeapon == null || string.IsNullOrWhiteSpace(currentWeapon.WeaponName))
        {
            currentWeaponAmmo = 0;
            currentWeaponReserveAmmo = 0;
            trackedAmmoWeaponName = string.Empty;
            return;
        }

        WeaponAmmoEntry ammoEntry = GetOrCreateAmmoRecord(currentWeapon.Category, currentWeapon.WeaponName);
        currentWeaponAmmo = Mathf.Max(0, ammoEntry.CurrentAmmo);
        currentWeaponReserveAmmo = Mathf.Max(0, ammoEntry.ReserveAmmo);
        trackedAmmoCategory = currentWeapon.Category;
        trackedAmmoWeaponName = currentWeapon.WeaponName;
    }

    private void EnsureDefaultWeaponList()
    {
        if (weapons == null)
            weapons = new List<WeaponEntry>();

        if (weapons.Count > 0)
            return;

        weapons.Add(new WeaponEntry { Category = WeaponCategory.Unarmed, WeaponName = "Unarmed" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Unarmed, WeaponName = "Knuckle Dusters" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Knife, WeaponName = "Combat Knife" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Knife, WeaponName = "Straight Razor" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Knife, WeaponName = "Kitchen Knife" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.TwoHanded, WeaponName = "Lead Pipe" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.TwoHanded, WeaponName = "Cricket Bat" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.TwoHanded, WeaponName = "Shovel" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.TwoHanded, WeaponName = "Felling Axe" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.TwoHanded, WeaponName = "Cane" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Bow, WeaponName = "Bow & Arrow" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Pistol, WeaponName = "Self-Loading Pistol" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Pistol, WeaponName = "Revolver" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Pistol, WeaponName = "Laser Pistol" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.SubmachineGun, WeaponName = "Submachine Gun" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Rifle, WeaponName = "Hunting Rifle" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Rifle, WeaponName = "Battle Rifle" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Rifle, WeaponName = "Light Machine Gun" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Rifle, WeaponName = "Laser Rifle" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Rifle, WeaponName = "Sniper Rifle" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Shotgun, WeaponName = "Double-Barrel Shotgun" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Shotgun, WeaponName = "Pump-Action Shotgun" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Special, WeaponName = "Bazooka" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Special, WeaponName = "Gatling Gun" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Explosive, WeaponName = "Hand Grenade" });
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Explosive, WeaponName = "Land Mine" });
    }

    private static bool IsFirearmCategory(WeaponCategory category)
    {
        return category == WeaponCategory.Pistol
               || category == WeaponCategory.SubmachineGun
               || category == WeaponCategory.Rifle
               || category == WeaponCategory.Shotgun
               || category == WeaponCategory.Special;
    }

    private static bool IsLongarmCategory(WeaponCategory category)
    {
        return category == WeaponCategory.SubmachineGun
               || category == WeaponCategory.Rifle
               || category == WeaponCategory.Shotgun;
    }

    private static string ResolveWeaponDefinitionName(WeaponDefinition weaponDefinition)
    {
        if (!weaponDefinition)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(weaponDefinition.GetDisplayName()))
            return weaponDefinition.GetDisplayName().Trim();

        if (!string.IsNullOrWhiteSpace(weaponDefinition.name))
            return weaponDefinition.name.Trim();

        return !string.IsNullOrWhiteSpace(weaponDefinition.GetItemId())
            ? weaponDefinition.GetItemId().Trim()
            : string.Empty;
    }

    private static WeaponCategory ResolveWeaponDefinitionCategory(WeaponDefinition weaponDefinition)
    {
        if (!weaponDefinition)
            return WeaponCategory.Unarmed;

        string compactName = CompactName(ResolveWeaponDefinitionName(weaponDefinition));
        if (compactName.Contains("shotgun"))
            return WeaponCategory.Shotgun;

        if (compactName.Contains("submachine"))
            return WeaponCategory.SubmachineGun;

        if (compactName.Contains("rifle") || compactName.Contains("machinegun"))
            return WeaponCategory.Rifle;

        if (compactName.Contains("pistol") || compactName.Contains("revolver"))
            return WeaponCategory.Pistol;

        if (compactName.Contains("knife") || compactName.Contains("razor"))
            return WeaponCategory.Knife;

        if (compactName.Contains("grenade") || compactName.Contains("mine"))
            return WeaponCategory.Explosive;

        if (weaponDefinition.GetAmmoType())
            return WeaponCategory.Pistol;

        return WeaponCategory.TwoHanded;
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target && target.activeSelf != active)
            target.SetActive(active);
    }

    private static string CompactName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] chars = new char[value.Length];
        int count = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = char.ToLowerInvariant(value[i]);
            if (!char.IsLetterOrDigit(c)) continue;

            chars[count] = c;
            count++;
        }

        return new string(chars, 0, count);
    }
}
