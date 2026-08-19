// imports
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;



// class
public class Container : MonoBehaviour, IPlayerInteractTarget
{

    // variables
    public enum LockType
    {
        VeryEasy = 5,
        Easy = 0,
        Medium = 1,
        Hard = 2,
        VeryHard = 3,
        Key = 4
    }

    [Serializable]
    private class LockSettingsGroup
    {
        // If true, the container starts locked.
        public bool startsLocked;

        // Which lock type this container uses when locked.
        public LockType lockType = LockType.Easy;

        // Optional key item definition used when lock type is Key.
        public ScriptableObject keyItemDefinition;

        // If true, one key item is consumed when unlocking.
        public bool consumeKeyOnUnlock;
    }

    [Serializable]
    private class EventsGroup
    {
        // Fired when the container opens.
        public UnityEvent<GameObject> onOpened;

        // Fired when interaction fails due to lock.
        public UnityEvent<GameObject> onLocked;

        // Fired when a key lock is successfully unlocked.
        public UnityEvent<GameObject> onUnlockedWithKey;
    }

    [Header("Container")]
    [SerializeField] private string containerName = "Container";
    [SerializeField] private bool useLootInteractionPrompt;

    [Header("Inventory")]
    [SerializeField] private List<PlayerInventory.InventoryEntry> weapons = new List<PlayerInventory.InventoryEntry>();
    [SerializeField] private List<PlayerInventory.InventoryEntry> apparel = new List<PlayerInventory.InventoryEntry>();
    [SerializeField] private List<PlayerInventory.InventoryEntry> aid = new List<PlayerInventory.InventoryEntry>();
    [SerializeField] private List<PlayerInventory.InventoryEntry> misc = new List<PlayerInventory.InventoryEntry>();
    [SerializeField] private List<PlayerInventory.InventoryEntry> ammo = new List<PlayerInventory.InventoryEntry>();
    [SerializeField] private float weight;

    [Header("Lock Settings")]
    [SerializeField] private LockSettingsGroup lockSettings = new LockSettingsGroup();

    [Header("Events")]
    [SerializeField] private EventsGroup eventsGroup = new EventsGroup();

    [Header("UI")]
    [SerializeField] private ContainerController containerController;

    [Header("Lockpicking")]
    [SerializeField] private LockpickController lockpickController;

    // Runtime lock state.
    [SerializeField, HideInInspector] private bool isLocked;

    private bool inventoryDerivedDataDirty = true;

    // Fires whenever this container inventory changes (UI can subscribe).
    public event Action OnInventoryChanged;



    // methods
    private void OnValidate()
    {
        if (lockSettings == null)
            lockSettings = new LockSettingsGroup();

        if (eventsGroup == null)
            eventsGroup = new EventsGroup();

        if (string.IsNullOrWhiteSpace(containerName))
            containerName = "Container";

        ProcessInventoryMutation(false);
    }


    private void Awake()
    {
        isLocked = lockSettings.startsLocked;
        ProcessInventoryMutation(false);

        if (!containerController)
            containerController = ContainerController.FindFirstInSceneIncludingInactive();

        if (!lockpickController)
            lockpickController = LockpickController.FindFirstInSceneIncludingInactive();
    }


    public string GetInteractionText(GameObject interactor)
    {
        string safeName = string.IsNullOrWhiteSpace(containerName) ? "Container" : containerName.Trim();
        string basePrompt = useLootInteractionPrompt ? "Loot" : "Open " + safeName;

        if (!IsCurrentlyLocked())
            return basePrompt;

        if (lockSettings.lockType == LockType.Key && CanInteractorUseKeyLock(interactor))
            return basePrompt + "\n[Locked - Use Key]";

        LockpickController resolvedLockpickController = ResolveLockpickController();
        if (resolvedLockpickController && resolvedLockpickController.CanLockpick(this))
            return resolvedLockpickController.GetInteractionText(this, interactor);

        if (lockSettings.lockType == LockType.Key)
            return basePrompt + "\n[Locked - Requires Key]";

        return basePrompt + "\n[Locked - " + GetLockTypeLabel(lockSettings.lockType) + "]";
    }


    public void Interact(GameObject interactor)
    {
        if (!IsCurrentlyLocked())
        {
            Open(interactor);
            return;
        }

        // Key locks can auto-unlock when the interactor has the referenced key item.
        if (lockSettings.lockType == LockType.Key && TryUnlockWithKey(interactor))
        {
            Open(interactor);
            return;
        }

        LockpickController resolvedLockpickController = ResolveLockpickController();
        if (resolvedLockpickController && resolvedLockpickController.TryBegin(this, interactor))
            return;

        eventsGroup.onLocked?.Invoke(interactor);
    }


    public void AddOnLockedListener(UnityAction<GameObject> listener)
    {
        if (listener == null)
            return;

        if (eventsGroup == null)
            eventsGroup = new EventsGroup();

        if (eventsGroup.onLocked == null)
            eventsGroup.onLocked = new UnityEvent<GameObject>();

        eventsGroup.onLocked.AddListener(listener);
    }


    public void RemoveOnLockedListener(UnityAction<GameObject> listener)
    {
        if (listener == null || eventsGroup == null || eventsGroup.onLocked == null)
            return;

        eventsGroup.onLocked.RemoveListener(listener);
    }


    public string GetContainerName()
    {
        return containerName;
    }


    public void SetContainerName(string newContainerName)
    {
        containerName = string.IsNullOrWhiteSpace(newContainerName) ? "Container" : newContainerName.Trim();
    }


    public void ConfigureAsLootContainer(string lootContainerName)
    {
        SetContainerName(lootContainerName);
        useLootInteractionPrompt = true;
        Unlock();
    }


    public bool UsesLootInteractionPrompt()
    {
        return useLootInteractionPrompt;
    }


    public bool IsLocked()
    {
        return IsCurrentlyLocked();
    }


    public LockType GetLockType()
    {
        return lockSettings.lockType;
    }


    public ScriptableObject GetRequiredKeyDefinition()
    {
        return lockSettings.keyItemDefinition;
    }


    public void Lock()
    {
        isLocked = true;
    }


    public void Unlock()
    {
        isLocked = false;
    }


    public bool AddItem(ScriptableObject itemToAdd, int amount)
    {
        if (!itemToAdd) return false;
        if (amount <= 0) return false;
        if (!IsItemTypeValidForCategory(itemToAdd)) return false;

        PlayerInventory.InventoryCategory category = GetInventoryCategoryOrDefault(itemToAdd);
        List<PlayerInventory.InventoryEntry> targetList = GetListForCategory(category);
        if (targetList == null) return false;

        bool canStack = CanItemStack(itemToAdd);
        int maxStackSize = canStack ? GetMaxStackSizeOrDefault(itemToAdd) : 0;

        if (canStack)
        {
            PlayerInventory.InventoryEntry existingEntry = FindFirstStackEntryWithSpace(targetList, itemToAdd, maxStackSize);
            if (existingEntry != null)
            {
                if (maxStackSize <= 0)
                {
                    existingEntry.AddStackQuantity(amount);
                    NotifyInventoryChanged();
                    return true;
                }

                int spaceLeft = maxStackSize - existingEntry.GetQuantity();
                if (spaceLeft >= amount)
                {
                    existingEntry.AddStackQuantity(amount);
                    NotifyInventoryChanged();
                    return true;
                }

                if (spaceLeft > 0)
                {
                    existingEntry.AddStackQuantity(spaceLeft);
                    amount -= spaceLeft;
                }
            }
        }

        while (amount > 0)
        {
            if (canStack)
            {
                int stackAmount = amount;
                if (maxStackSize > 0)
                    stackAmount = Mathf.Min(amount, maxStackSize);

                PlayerInventory.InventoryEntry newEntry = new PlayerInventory.InventoryEntry(itemToAdd);
                newEntry.AddStackQuantity(stackAmount);
                targetList.Add(newEntry);
                amount -= stackAmount;
                continue;
            }

            PlayerInventory.InventoryEntry uniqueEntry = new PlayerInventory.InventoryEntry(itemToAdd);
            uniqueEntry.AddSingleInstance(100.0f);
            targetList.Add(uniqueEntry);
            amount -= 1;
        }

        NotifyInventoryChanged();
        return true;
    }


    public bool AddItemInstance(ScriptableObject itemToAdd, float conditionPercent, int loadedMagazineRounds = 0)
    {
        if (!itemToAdd) return false;
        if (!IsItemTypeValidForCategory(itemToAdd)) return false;

        if (CanItemStack(itemToAdd))
            return AddItem(itemToAdd, 1);

        PlayerInventory.InventoryCategory category = GetInventoryCategoryOrDefault(itemToAdd);
        List<PlayerInventory.InventoryEntry> targetList = GetListForCategory(category);
        if (targetList == null) return false;

        PlayerInventory.InventoryEntry uniqueEntry = new PlayerInventory.InventoryEntry(itemToAdd);
        uniqueEntry.AddSingleInstance(conditionPercent, loadedMagazineRounds);
        targetList.Add(uniqueEntry);

        NotifyInventoryChanged();
        return true;
    }


    public bool RemoveItem(ScriptableObject itemToRemove, int amount)
    {
        if (!itemToRemove) return false;
        if (amount <= 0) return false;
        if (!IsItemTypeValidForCategory(itemToRemove)) return false;

        PlayerInventory.InventoryCategory category = GetInventoryCategoryOrDefault(itemToRemove);
        List<PlayerInventory.InventoryEntry> targetList = GetListForCategory(category);
        if (targetList == null) return false;

        bool inventoryChanged = false;

        for (int i = targetList.Count - 1; i >= 0 && amount > 0; i--)
        {
            PlayerInventory.InventoryEntry entry = targetList[i];
            if (entry.GetItemDefinition() != itemToRemove) continue;

            int remainingAfterEntry = entry.RemoveQuantity(amount);
            if (remainingAfterEntry != amount)
                inventoryChanged = true;

            if (entry.IsEmpty())
                targetList.RemoveAt(i);

            amount = remainingAfterEntry;
        }

        if (inventoryChanged)
            NotifyInventoryChanged();

        return amount == 0;
    }


    public bool RemoveInventoryEntry(PlayerInventory.InventoryEntry entryToRemove, int amount = 1)
    {
        if (entryToRemove == null) return false;
        if (amount <= 0) return false;

        ScriptableObject itemDefinition = entryToRemove.GetItemDefinition();
        if (!itemDefinition) return false;

        PlayerInventory.InventoryCategory category = GetInventoryCategoryOrDefault(itemDefinition);
        List<PlayerInventory.InventoryEntry> targetList = GetListForCategory(category);
        if (targetList == null) return false;

        int targetIndex = targetList.IndexOf(entryToRemove);
        if (targetIndex < 0) return false;

        PlayerInventory.InventoryEntry targetEntry = targetList[targetIndex];
        int remainingAfterEntry = targetEntry.RemoveQuantity(amount);
        if (remainingAfterEntry == amount) return false;

        if (targetEntry.IsEmpty())
            targetList.RemoveAt(targetIndex);

        NotifyInventoryChanged();
        return remainingAfterEntry == 0;
    }


    public IReadOnlyList<PlayerInventory.InventoryEntry> GetCategoryItems(PlayerInventory.InventoryCategory category)
    {
        return GetListForCategory(category);
    }


    public int GetTotalCount(ScriptableObject item)
    {
        if (!item) return 0;
        if (!IsItemTypeValidForCategory(item)) return 0;

        PlayerInventory.InventoryCategory category = GetInventoryCategoryOrDefault(item);
        List<PlayerInventory.InventoryEntry> targetList = GetListForCategory(category);
        if (targetList == null) return 0;

        int total = 0;
        for (int i = 0; i < targetList.Count; i++)
        {
            if (targetList[i].GetItemDefinition() != item) continue;
            total += targetList[i].GetQuantity();
        }

        return total;
    }


    public int GetAmmoCount(AmmoDefinition ammoType)
    {
        if (!ammoType) return 0;
        return GetTotalCount(ammoType);
    }


    public string GetInstanceId(PlayerInventory.InventoryEntry entry, int instanceIndex)
    {
        if (entry == null) return string.Empty;
        return entry.GetInstanceId(instanceIndex);
    }


    public int GetInstanceLoadedMagazineRounds(PlayerInventory.InventoryEntry entry, int instanceIndex)
    {
        if (entry == null) return 0;
        return entry.GetInstanceLoadedMagazineRounds(instanceIndex);
    }


    public bool SetInstanceLoadedMagazineRounds(PlayerInventory.InventoryEntry entry, int instanceIndex, int loadedRounds, bool notifyChange = false)
    {
        if (entry == null) return false;

        bool updated = entry.SetInstanceLoadedMagazineRounds(instanceIndex, loadedRounds);
        if (!updated) return false;

        if (notifyChange)
            NotifyInventoryChanged();

        return true;
    }


    public float GetInstanceConditionPercent(PlayerInventory.InventoryEntry entry, int instanceIndex)
    {
        if (entry == null) return 0.0f;

        IReadOnlyList<PlayerInventory.ItemInstanceData> itemInstances = entry.GetItemInstances();
        if (instanceIndex < 0 || instanceIndex >= itemInstances.Count) return 0.0f;

        return itemInstances[instanceIndex].GetConditionPercent();
    }


    public bool SetInstanceConditionPercent(PlayerInventory.InventoryEntry entry, int instanceIndex, float conditionPercent, bool notifyChange = false)
    {
        if (entry == null) return false;

        IReadOnlyList<PlayerInventory.ItemInstanceData> itemInstances = entry.GetItemInstances();
        if (instanceIndex < 0 || instanceIndex >= itemInstances.Count) return false;

        PlayerInventory.ItemInstanceData selectedInstance = itemInstances[instanceIndex];
        if (selectedInstance == null) return false;

        selectedInstance.SetConditionPercent(entry.GetItemDefinition(), conditionPercent);

        if (notifyChange)
            NotifyInventoryChanged();

        return true;
    }


    public float GetInstanceValue(PlayerInventory.InventoryEntry entry, int instanceIndex)
    {
        if (entry == null) return 0.0f;

        IReadOnlyList<PlayerInventory.ItemInstanceData> itemInstances = entry.GetItemInstances();
        if (instanceIndex < 0 || instanceIndex >= itemInstances.Count) return 0.0f;

        PlayerInventory.ItemInstanceData selectedInstance = itemInstances[instanceIndex];
        selectedInstance.SetConditionPercent(entry.GetItemDefinition(), selectedInstance.GetConditionPercent());
        return selectedInstance.GetValue();
    }


    public float GetWeight()
    {
        RefreshInventoryDerivedDataIfDirty();
        return weight;
    }


    public bool TryTransferFromPlayer(PlayerInventory playerInventory, PlayerInventory.InventoryEntry playerEntry, int amount = 1)
    {
        if (!playerInventory) return false;

        return TryTransferEntryCore(
            playerEntry,
            amount,
            playerInventory.RemoveInventoryEntry,
            playerInventory.AddItem,
            playerInventory.AddItemInstance,
            playerInventory.GetInstanceConditionPercent,
            playerInventory.GetInstanceLoadedMagazineRounds,
            AddItem,
            AddItemInstance
        );
    }


    public bool TryTransferToPlayer(PlayerInventory playerInventory, PlayerInventory.InventoryEntry containerEntry, int amount = 1)
    {
        if (!playerInventory) return false;

        return TryTransferEntryCore(
            containerEntry,
            amount,
            RemoveInventoryEntry,
            AddItem,
            AddItemInstance,
            GetInstanceConditionPercent,
            GetInstanceLoadedMagazineRounds,
            playerInventory.AddItem,
            playerInventory.AddItemInstance
        );
    }


    private void Open(GameObject interactor)
    {
        eventsGroup.onOpened?.Invoke(interactor);

        ContainerController openedContainerController = ResolveContainerUI();
        if (openedContainerController)
            openedContainerController.OpenForContainer(this, interactor);
    }


    private ContainerController ResolveContainerUI()
    {
        if (containerController)
            return containerController;

        containerController = ContainerController.FindFirstInSceneIncludingInactive();
        return containerController;
    }


    private LockpickController ResolveLockpickController()
    {
        if (lockpickController && lockpickController.gameObject.scene.IsValid() && lockpickController.gameObject.scene.isLoaded)
            return lockpickController;

        lockpickController = LockpickController.FindFirstInSceneIncludingInactive();
        return lockpickController;
    }


    private bool TryUnlockWithKey(GameObject interactor)
    {
        ScriptableObject keyDefinition = lockSettings.keyItemDefinition;
        if (!keyDefinition || !CanInteractorUseKeyLock(interactor))
            return false;

        PlayerInventory inventory = interactor.GetComponentInParent<PlayerInventory>(true);
        if (!inventory)
            return false;

        if (inventory.GetTotalCount(keyDefinition) <= 0)
            return false;

        if (lockSettings.consumeKeyOnUnlock && !inventory.RemoveItem(keyDefinition, 1))
            return false;

        isLocked = false;
        eventsGroup.onUnlockedWithKey?.Invoke(interactor);
        return true;
    }


    private bool CanInteractorUseKeyLock(GameObject interactor)
    {
        ScriptableObject keyDefinition = lockSettings.keyItemDefinition;
        if (!keyDefinition || !interactor)
            return false;

        PlayerInventory inventory = interactor.GetComponentInParent<PlayerInventory>(true);
        if (!inventory)
            return false;

        return inventory.GetTotalCount(keyDefinition) > 0;
    }


    private bool IsCurrentlyLocked()
    {
        return isLocked;
    }


    private static string GetLockTypeLabel(LockType type)
    {
        if (type == LockType.VeryEasy) return "Very Easy";
        if (type == LockType.Easy) return "Easy";
        if (type == LockType.Medium) return "Medium";
        if (type == LockType.Hard) return "Hard";
        if (type == LockType.VeryHard) return "Very Hard";
        return "Locked";
    }


    private bool TryTransferEntryCore(
        PlayerInventory.InventoryEntry sourceEntry,
        int amount,
        Func<PlayerInventory.InventoryEntry, int, bool> sourceRemoveEntry,
        Func<ScriptableObject, int, bool> sourceAddItem,
        Func<ScriptableObject, float, int, bool> sourceAddItemInstance,
        Func<PlayerInventory.InventoryEntry, int, float> sourceGetCondition,
        Func<PlayerInventory.InventoryEntry, int, int> sourceGetLoadedRounds,
        Func<ScriptableObject, int, bool> targetAddItem,
        Func<ScriptableObject, float, int, bool> targetAddItemInstance)
    {
        if (sourceEntry == null) return false;
        if (amount <= 0) return false;
        if (sourceRemoveEntry == null) return false;
        if (sourceAddItem == null) return false;
        if (sourceAddItemInstance == null) return false;
        if (sourceGetCondition == null) return false;
        if (sourceGetLoadedRounds == null) return false;
        if (targetAddItem == null) return false;
        if (targetAddItemInstance == null) return false;

        ScriptableObject itemDefinition = sourceEntry.GetItemDefinition();
        if (!itemDefinition) return false;

        int transferAmount = Mathf.Min(amount, sourceEntry.GetQuantity());
        if (transferAmount <= 0) return false;

        if (CanItemStack(itemDefinition))
        {
            if (!sourceRemoveEntry(sourceEntry, transferAmount))
                return false;

            if (targetAddItem(itemDefinition, transferAmount))
                return true;

            sourceAddItem(itemDefinition, transferAmount);
            return false;
        }

        int transferredCount = 0;
        for (int i = 0; i < transferAmount; i++)
        {
            IReadOnlyList<PlayerInventory.ItemInstanceData> instances = sourceEntry.GetItemInstances();
            int instanceIndex = instances.Count - 1;
            if (instanceIndex < 0)
                break;

            float conditionPercent = sourceGetCondition(sourceEntry, instanceIndex);
            int loadedMagazineRounds = sourceGetLoadedRounds(sourceEntry, instanceIndex);

            if (!sourceRemoveEntry(sourceEntry, 1))
                break;

            if (!targetAddItemInstance(itemDefinition, conditionPercent, loadedMagazineRounds))
            {
                sourceAddItemInstance(itemDefinition, conditionPercent, loadedMagazineRounds);
                break;
            }

            transferredCount += 1;
        }

        return transferredCount == transferAmount;
    }


    private List<PlayerInventory.InventoryEntry> GetListForCategory(PlayerInventory.InventoryCategory category)
    {
        if (category == PlayerInventory.InventoryCategory.Weapons) return weapons;
        if (category == PlayerInventory.InventoryCategory.Apparel) return apparel;
        if (category == PlayerInventory.InventoryCategory.Aid) return aid;
        if (category == PlayerInventory.InventoryCategory.Misc) return misc;
        if (category == PlayerInventory.InventoryCategory.Ammo) return ammo;
        return null;
    }


    private PlayerInventory.InventoryEntry FindFirstStackEntryWithSpace(List<PlayerInventory.InventoryEntry> list, ScriptableObject item, int maxStackSize)
    {
        if (list == null) return null;

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].GetItemDefinition() != item) continue;
            if (maxStackSize <= 0) return list[i];
            if (list[i].GetQuantity() < maxStackSize) return list[i];
        }

        return null;
    }


    private bool CanItemStack(ScriptableObject item)
    {
        if (!item) return false;

        PlayerInventory.InventoryCategory category = GetInventoryCategoryOrDefault(item);
        if (category == PlayerInventory.InventoryCategory.Weapons) return false;
        if (category == PlayerInventory.InventoryCategory.Apparel) return false;

        return IsStackableOrDefault(item, true);
    }


    private float CalculateCurrentWeight()
    {
        float totalWeight = 0.0f;
        totalWeight += CalculateCategoryWeight(weapons);
        totalWeight += CalculateCategoryWeight(apparel);
        totalWeight += CalculateCategoryWeight(aid);
        totalWeight += CalculateCategoryWeight(misc);
        totalWeight += CalculateCategoryWeight(ammo);
        return Mathf.Max(0.0f, totalWeight);
    }


    private float CalculateCategoryWeight(List<PlayerInventory.InventoryEntry> entries)
    {
        if (entries == null) return 0.0f;

        float categoryWeight = 0.0f;
        for (int i = 0; i < entries.Count; i++)
        {
            PlayerInventory.InventoryEntry entry = entries[i];
            if (entry == null) continue;

            float entryWeight = Mathf.Max(0.0f, GetDefinitionWeightOrDefault(entry.GetItemDefinition()));
            categoryWeight += entryWeight * Mathf.Max(0, entry.GetQuantity());
        }

        return categoryWeight;
    }


    private void RecalculateWeight()
    {
        weight = CalculateCurrentWeight();
    }


    private void MarkInventoryDerivedDataDirty()
    {
        inventoryDerivedDataDirty = true;
    }


    private void RefreshInventoryDerivedDataIfDirty()
    {
        if (!inventoryDerivedDataDirty)
            return;

        EnsureAllInstanceIds();
        RecalculateAllInstanceValues();
        RecalculateWeight();
        inventoryDerivedDataDirty = false;
    }


    private void NotifyInventoryChanged()
    {
        ProcessInventoryMutation(true);
    }


    private void ProcessInventoryMutation(bool notifyChange)
    {
        MarkInventoryDerivedDataDirty();
        RefreshInventoryDerivedDataIfDirty();

        if (notifyChange)
            OnInventoryChanged?.Invoke();
    }


    private void RecalculateAllInstanceValues()
    {
        RecalculateCategoryInstanceValues(weapons);
        RecalculateCategoryInstanceValues(apparel);
        RecalculateCategoryInstanceValues(aid);
        RecalculateCategoryInstanceValues(misc);
        RecalculateCategoryInstanceValues(ammo);
    }


    private void EnsureAllInstanceIds()
    {
        HashSet<string> usedInstanceIds = new HashSet<string>();
        EnsureCategoryInstanceIds(weapons, usedInstanceIds);
        EnsureCategoryInstanceIds(apparel, usedInstanceIds);
        EnsureCategoryInstanceIds(aid, usedInstanceIds);
        EnsureCategoryInstanceIds(misc, usedInstanceIds);
        EnsureCategoryInstanceIds(ammo, usedInstanceIds);
    }


    private void EnsureCategoryInstanceIds(List<PlayerInventory.InventoryEntry> entries, HashSet<string> usedInstanceIds)
    {
        if (entries == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            PlayerInventory.InventoryEntry entry = entries[i];
            if (entry == null) continue;

            entry.EnsureUniqueInstanceIds(usedInstanceIds);
        }
    }


    private void RecalculateCategoryInstanceValues(List<PlayerInventory.InventoryEntry> entries)
    {
        if (entries == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            PlayerInventory.InventoryEntry entry = entries[i];
            if (entry == null) continue;

            entry.RecalculateInstanceValues();
        }
    }


    private bool IsItemTypeValidForCategory(ScriptableObject item)
    {
        if (!item) return false;

        if (!TryGetInventoryCategory(item, out PlayerInventory.InventoryCategory category))
            return false;

        if (category == PlayerInventory.InventoryCategory.Weapons)
            return item is WeaponDefinition;

        if (category == PlayerInventory.InventoryCategory.Apparel)
            return item is ApparelDefinition;

        if (category == PlayerInventory.InventoryCategory.Aid)
            return item is AidDefinition;

        if (category == PlayerInventory.InventoryCategory.Misc)
            return item is MiscDefinition;

        if (category == PlayerInventory.InventoryCategory.Ammo)
            return item is AmmoDefinition;

        return false;
    }


    private bool TryGetInventoryCategory(ScriptableObject definition, out PlayerInventory.InventoryCategory category)
    {
        if (!definition)
        {
            category = PlayerInventory.InventoryCategory.Misc;
            return false;
        }

        if (definition is WeaponDefinition weaponDefinition)
        {
            category = weaponDefinition.GetInventoryCategory();
            return true;
        }

        if (definition is ApparelDefinition apparelDefinition)
        {
            category = apparelDefinition.GetInventoryCategory();
            return true;
        }

        if (definition is AidDefinition aidDefinition)
        {
            category = aidDefinition.GetInventoryCategory();
            return true;
        }

        if (definition is MiscDefinition miscDefinition)
        {
            category = miscDefinition.GetInventoryCategory();
            return true;
        }

        if (definition is AmmoDefinition ammoDefinition)
        {
            category = ammoDefinition.GetInventoryCategory();
            return true;
        }

        category = PlayerInventory.InventoryCategory.Misc;
        return false;
    }


    private PlayerInventory.InventoryCategory GetInventoryCategoryOrDefault(
        ScriptableObject definition,
        PlayerInventory.InventoryCategory fallback = PlayerInventory.InventoryCategory.Misc)
    {
        if (!TryGetInventoryCategory(definition, out PlayerInventory.InventoryCategory category))
            return fallback;

        return category;
    }


    private bool IsStackableOrDefault(ScriptableObject definition, bool fallback)
    {
        if (!definition) return fallback;

        if (definition is WeaponDefinition weaponDefinition) return weaponDefinition.IsStackable();
        if (definition is ApparelDefinition apparelDefinition) return apparelDefinition.IsStackable();
        if (definition is AidDefinition aidDefinition) return aidDefinition.IsStackable();
        if (definition is MiscDefinition miscDefinition) return miscDefinition.IsStackable();
        if (definition is AmmoDefinition ammoDefinition) return ammoDefinition.IsStackable();
        return fallback;
    }


    private int GetMaxStackSizeOrDefault(ScriptableObject definition, int fallback = 0)
    {
        if (!definition) return fallback;

        if (definition is WeaponDefinition weaponDefinition) return weaponDefinition.GetMaxStackSize();
        if (definition is ApparelDefinition apparelDefinition) return apparelDefinition.GetMaxStackSize();
        if (definition is AidDefinition aidDefinition) return aidDefinition.GetMaxStackSize();
        if (definition is MiscDefinition miscDefinition) return miscDefinition.GetMaxStackSize();
        if (definition is AmmoDefinition ammoDefinition) return ammoDefinition.GetMaxStackSize();
        return fallback;
    }


    private float GetDefinitionWeightOrDefault(ScriptableObject definition, float fallback = 0.0f)
    {
        if (!definition) return fallback;

        if (definition is WeaponDefinition weaponDefinition) return weaponDefinition.GetWeight();
        if (definition is ApparelDefinition apparelDefinition) return apparelDefinition.GetWeight();
        if (definition is AidDefinition aidDefinition) return aidDefinition.GetWeight();
        if (definition is MiscDefinition miscDefinition) return miscDefinition.GetWeight();
        if (definition is AmmoDefinition ammoDefinition) return ammoDefinition.GetWeight();
        return fallback;
    }
}
