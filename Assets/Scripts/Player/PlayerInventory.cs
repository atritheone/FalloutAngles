// imports
using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif



// class
public class PlayerInventory : MonoBehaviour, WorldItem.IWorldItemReceiver
{
    private const float DefaultConditionPercent = 100.0f;
    private const string BottleCapDefinitionAssetPath = "Assets/Definitions/Misc/BottleCap.asset";
    private const string BottleCapAssetName = "BottleCap";
    private const string BottleCapDisplayName = "Bottle Cap";
    private const string OverEncumberedMessage = "You are over-encumbered.";

    // variables
    // The five categories the player inventory supports.
    public enum InventoryCategory
    {
        Weapons,

        Apparel,

        Aid,

        Misc,

        Ammo
    }


    // A single item instance with its own mutable state.
    [Serializable]
    public class ItemInstanceData
    {

        // Unique id for this runtime/save instance.
        [SerializeField] private string instanceId;

        // Quantity represented by this instance (usually 1).
        [SerializeField] private int quantity = 1;

        // Condition for this specific instance.
        [Range(0.0f, 100.0f)] [SerializeField] private float conditionPercent = 100.0f;

        // Loaded rounds currently in this instance's weapon magazine (weapons only).
        [Min(0)] [SerializeField] private int loadedMagazineRounds = 0;

        // Instance value derived from definition base value and condition.
        [SerializeField] private float value;


        // Build a new item instance.
        public ItemInstanceData(ScriptableObject sourceDefinition, int newQuantity, float newConditionPercent, int newLoadedMagazineRounds = 0)
        {
            // Generate a stable instance id.
            instanceId = Guid.NewGuid().ToString("N");

            // Store quantity with a minimum of one instance unit.
            quantity = Mathf.Max(1, newQuantity);

            // Store condition/value.
            SetConditionPercent(sourceDefinition, newConditionPercent);

            // Store weapon magazine rounds.
            SetLoadedMagazineRounds(sourceDefinition, newLoadedMagazineRounds);
        }


        public string GetInstanceId()
        {
            // Return unique instance id.
            return instanceId;
        }


        public void EnsureUniqueInstanceId(HashSet<string> usedInstanceIds)
        {
            // Track whether this instance needs a replacement id.
            bool needsNewId = string.IsNullOrWhiteSpace(instanceId);

            // Existing duplicate ids must be replaced.
            if (!needsNewId && usedInstanceIds != null && usedInstanceIds.Contains(instanceId))
                needsNewId = true;

            // Generate a new id if needed.
            if (needsNewId)
                instanceId = Guid.NewGuid().ToString("N");

            // Register this id and guarantee uniqueness when a set is provided.
            if (usedInstanceIds != null)
            {
                while (usedInstanceIds.Contains(instanceId))
                    instanceId = Guid.NewGuid().ToString("N");

                usedInstanceIds.Add(instanceId);
            }
        }


        public int GetQuantity()
        {
            // Return quantity represented by this instance.
            return quantity;
        }


        public void SetQuantity(int newQuantity)
        {
            // Store quantity with a minimum of one instance unit.
            quantity = Mathf.Max(1, newQuantity);
        }


        public float GetConditionPercent()
        {
            // Return per-instance condition.
            return conditionPercent;
        }


        public void SetConditionPercent(ScriptableObject sourceDefinition, float newConditionPercent)
        {
            // Clamp condition to 0..100.
            conditionPercent = Mathf.Clamp(newConditionPercent, 0.0f, 100.0f);

            // Recalculate value from definition base value and condition.
            RecalculateValue(sourceDefinition);
        }


        public float GetValue()
        {
            // Return per-instance value.
            return value;
        }


        public int GetLoadedMagazineRounds()
        {
            // Return per-instance loaded magazine rounds.
            return Mathf.Max(0, loadedMagazineRounds);
        }


        public void SetLoadedMagazineRounds(ScriptableObject sourceDefinition, int newLoadedMagazineRounds)
        {
            // Clamp loaded rounds to this definition's magazine size.
            loadedMagazineRounds = ClampLoadedMagazineRoundsForDefinition(sourceDefinition, newLoadedMagazineRounds);
        }


        private void RecalculateValue(ScriptableObject sourceDefinition)
        {
            // Value is base value, optionally scaled by condition for degradable items.
            float baseValue = GetBaseValueForDefinition(sourceDefinition);
            float conditionMultiplier = DefinitionSupportsCondition(sourceDefinition)
                ? Mathf.Clamp01(conditionPercent / 100.0f)
                : 1.0f;

            value = Mathf.Max(0.0f, baseValue * conditionMultiplier);
        }


        private float GetBaseValueForDefinition(ScriptableObject sourceDefinition)
        {
            // Weapon instances use weapon base value.
            if (sourceDefinition is WeaponDefinition weaponDefinition)
                return Mathf.Max(0.0f, weaponDefinition.GetBaseValue());

            // Apparel instances use apparel base value.
            if (sourceDefinition is ApparelDefinition apparelDefinition)
                return Mathf.Max(0.0f, apparelDefinition.GetBaseValue());

            // Aid instances use aid value.
            if (sourceDefinition is AidDefinition aidDefinition)
                return Mathf.Max(0.0f, aidDefinition.GetValue());

            // Misc instances use misc value.
            if (sourceDefinition is MiscDefinition miscDefinition)
                return Mathf.Max(0.0f, miscDefinition.GetValue());

            // Ammo instances use ammo value.
            if (sourceDefinition is AmmoDefinition ammoDefinition)
                return Mathf.Max(0.0f, ammoDefinition.GetValue());

            // Other item categories do not currently expose a value field.
            return 0.0f;
        }


        private int ClampLoadedMagazineRoundsForDefinition(ScriptableObject sourceDefinition, int rounds)
        {
            // Non-weapon items do not track loaded magazine rounds.
            if (!(sourceDefinition is WeaponDefinition weaponDefinition))
                return 0;

            int magazineSize = Mathf.Max(0, weaponDefinition.GetMagazineSize());
            return Mathf.Clamp(rounds, 0, magazineSize);
        }
    }


    // A single entry in the inventory for one item definition.
    [Serializable]
    public class InventoryEntry
    {

        // The ScriptableObject definition for this item.
        [SerializeField] private ScriptableObject itemDefinition;

        // Concrete item instances represented by this entry.
        [SerializeField] private List<ItemInstanceData> itemInstances = new List<ItemInstanceData>();


        // Build a new inventory entry.
        public InventoryEntry(ScriptableObject newItemDefinition)
        {
            // Store the provided item definition.
            itemDefinition = newItemDefinition;
        }


        // Build a new inventory entry with stack quantity.
        public InventoryEntry(ScriptableObject newItemDefinition, int newQuantity)
        {
            // Store the provided item definition.
            itemDefinition = newItemDefinition;

            // Add quantity into this entry.
            AddStackQuantity(newQuantity);
        }


        public ScriptableObject GetItemDefinition()
        {
            // Return the stored item definition.
            return itemDefinition;
        }


        public IReadOnlyList<ItemInstanceData> GetItemInstances()
        {
            // Ensure instance list exists.
            EnsureItemInstancesInitialized();

            // Return instance list.
            return itemInstances;
        }


        public int GetQuantity()
        {
            // Ensure instance list exists.
            EnsureItemInstancesInitialized();

            // Track total quantity.
            int total = 0;

            // Sum all quantities in this entry.
            for (int i = 0; i < itemInstances.Count; i++)
                total += itemInstances[i].GetQuantity();

            // Return total quantity.
            return total;
        }


        public void RecalculateInstanceValues()
        {
            // Ensure instance list exists.
            EnsureItemInstancesInitialized();

            // Recalculate value for every instance from definition + condition.
            for (int i = 0; i < itemInstances.Count; i++)
            {
                ItemInstanceData instance = itemInstances[i];
                if (instance == null) continue;

                instance.SetConditionPercent(itemDefinition, instance.GetConditionPercent());
            }
        }


        public void EnsureUniqueInstanceIds(HashSet<string> usedInstanceIds)
        {
            // Ensure instance list exists.
            EnsureItemInstancesInitialized();

            // Ensure every instance has a unique non-empty id.
            for (int i = 0; i < itemInstances.Count; i++)
            {
                ItemInstanceData instance = itemInstances[i];
                if (instance == null) continue;

                instance.EnsureUniqueInstanceId(usedInstanceIds);
            }
        }


        public void SetQuantity(int newQuantity)
        {
            // Ensure instance list exists.
            EnsureItemInstancesInitialized();

            // Reset entry quantity to exactly one stack instance.
            itemInstances.Clear();

            // If new quantity is positive, add a stack instance.
            if (newQuantity > 0)
                AddStackQuantity(newQuantity);
        }


        public void AddStackQuantity(int amount)
        {
            // Stop if amount is invalid.
            if (amount <= 0) return;

            // Ensure instance list exists.
            EnsureItemInstancesInitialized();

            // Create a stack instance if this entry is empty.
            if (itemInstances.Count == 0)
            {
                itemInstances.Add(new ItemInstanceData(itemDefinition, amount, 100.0f));
                return;
            }

            // Add amount to the first stack instance.
            ItemInstanceData stackInstance = itemInstances[0];
            stackInstance.SetQuantity(stackInstance.GetQuantity() + amount);
        }


        public void AddSingleInstance(float conditionPercent, int loadedMagazineRounds = 0)
        {
            // Ensure instance list exists.
            EnsureItemInstancesInitialized();

            // Add one uniquely tracked instance.
            itemInstances.Add(new ItemInstanceData(itemDefinition, 1, conditionPercent, loadedMagazineRounds));
        }


        public string GetInstanceId(int instanceIndex)
        {
            // Ensure instance list exists.
            EnsureItemInstancesInitialized();

            // Stop if index is invalid.
            if (instanceIndex < 0 || instanceIndex >= itemInstances.Count) return string.Empty;

            // Return selected instance id.
            ItemInstanceData selectedInstance = itemInstances[instanceIndex];
            return selectedInstance != null ? selectedInstance.GetInstanceId() : string.Empty;
        }


        public int GetInstanceLoadedMagazineRounds(int instanceIndex)
        {
            // Ensure instance list exists.
            EnsureItemInstancesInitialized();

            // Stop if index is invalid.
            if (instanceIndex < 0 || instanceIndex >= itemInstances.Count) return 0;

            // Return selected instance magazine rounds.
            ItemInstanceData selectedInstance = itemInstances[instanceIndex];
            return selectedInstance != null ? selectedInstance.GetLoadedMagazineRounds() : 0;
        }


        public bool SetInstanceLoadedMagazineRounds(int instanceIndex, int loadedRounds)
        {
            // Ensure instance list exists.
            EnsureItemInstancesInitialized();

            // Stop if index is invalid.
            if (instanceIndex < 0 || instanceIndex >= itemInstances.Count) return false;

            // Stop if selected instance is missing.
            ItemInstanceData selectedInstance = itemInstances[instanceIndex];
            if (selectedInstance == null) return false;

            // Store clamped rounds for this entry definition.
            selectedInstance.SetLoadedMagazineRounds(itemDefinition, loadedRounds);
            return true;
        }


        public int RemoveQuantity(int amount)
        {
            // Stop if amount is invalid.
            if (amount <= 0) return 0;

            // Ensure instance list exists.
            EnsureItemInstancesInitialized();

            // Track amount still needing removal.
            int remaining = amount;

            // Remove from the end so per-instance differences remain stable.
            for (int i = itemInstances.Count - 1; i >= 0 && remaining > 0; i--)
            {
                // Cache instance.
                ItemInstanceData instance = itemInstances[i];

                // Read quantity in this instance.
                int instanceQuantity = instance.GetQuantity();

                // If this instance is fully consumed, remove it.
                if (instanceQuantity <= remaining)
                {
                    // Consume this instance.
                    remaining -= instanceQuantity;

                    // Remove this instance.
                    itemInstances.RemoveAt(i);
                    continue;
                }

                // Partially consume this instance.
                instance.SetQuantity(instanceQuantity - remaining);
                remaining = 0;
            }

            // Return amount that could not be removed.
            return remaining;
        }


        public bool IsEmpty()
        {
            // Entry is empty when quantity reaches zero.
            return GetQuantity() <= 0;
        }


        public void NormalizeSerializedData()
        {
            // Make sure the list exists.
            EnsureItemInstancesInitialized();

            // Strip null list entries created by manual inspector edits.
            for (int i = itemInstances.Count - 1; i >= 0; i--)
            {
                if (itemInstances[i] == null)
                    itemInstances.RemoveAt(i);
            }

            // Entries without a definition cannot create valid instances.
            if (!itemDefinition)
                return;

            // Ensure manually added entries always have at least one instance.
            if (itemInstances.Count == 0)
                itemInstances.Add(new ItemInstanceData(itemDefinition, 1, DefaultConditionPercent));

            bool supportsCondition = DefinitionSupportsCondition(itemDefinition);
            bool supportsLoadedRounds = DefinitionSupportsLoadedMagazineRounds(itemDefinition);

            // Clamp all instance data to the valid range for this definition.
            for (int i = 0; i < itemInstances.Count; i++)
            {
                ItemInstanceData instance = itemInstances[i];
                if (instance == null) continue;

                // Inventory instances cannot represent zero quantity.
                if (instance.GetQuantity() <= 0)
                    instance.SetQuantity(1);

                // Non-weapon/apparel items keep condition fixed at 100.
                float normalizedCondition = supportsCondition
                    ? Mathf.Clamp(instance.GetConditionPercent(), 0.0f, 100.0f)
                    : DefaultConditionPercent;
                instance.SetConditionPercent(itemDefinition, normalizedCondition);

                // Only firearm-style weapons may keep loaded rounds.
                int normalizedLoadedRounds = supportsLoadedRounds
                    ? instance.GetLoadedMagazineRounds()
                    : 0;
                instance.SetLoadedMagazineRounds(itemDefinition, normalizedLoadedRounds);
            }
        }


        private void EnsureItemInstancesInitialized()
        {
            // Make sure the list exists.
            if (itemInstances == null)
                itemInstances = new List<ItemInstanceData>();
        }
    }


    // Weapons category entries.
    [SerializeField] private List<InventoryEntry> weapons = new List<InventoryEntry>();

    // Apparel category entries.
    [SerializeField] private List<InventoryEntry> apparel = new List<InventoryEntry>();

    // Aid category entries.
    [SerializeField] private List<InventoryEntry> aid = new List<InventoryEntry>();

    // Misc category entries.
    [SerializeField] private List<InventoryEntry> misc = new List<InventoryEntry>();

    // Ammo category entries.
    [SerializeField] private List<InventoryEntry> ammo = new List<InventoryEntry>();

    // Current carried weight (derived from all inventory items).
    [SerializeField] private float weight;

    // Maximum carry weight (independent value).
    [SerializeField] private float maxWeight;

    // True when current carried weight exceeds maximum carry weight.
    [SerializeField] private bool IsOverencumbered;

    [Header("Weight Messages")]
    [SerializeField] private bool showOverEncumberedMessage = true;

    // Total damage resistance (independent value).
    [SerializeField] private int totalDamageResistance;

    // Current caps mirrored from the Bottle Cap item count in misc inventory.
    [SerializeField] private int caps;

    // Total caps tracked (independent value).
    [SerializeField] private int totalCaps;

    // Definition used for inventory-backed caps.
    [SerializeField] private MiscDefinition bottleCapDefinition;

    // Last inspector caps value used to detect direct inspector edits.
    [HideInInspector] [SerializeField] private int lastValidatedCaps = -1;

    // Fires whenever the inventory changes (UI can subscribe).
    public event Action OnInventoryChanged;
    private bool inventoryDerivedDataDirty = true;
    private bool hasOverEncumberedStateCache;
    private bool lastOverEncumbered;



    // methods
    private void Awake()
    {
        EnsureBottleCapDefinitionReference();
        NormalizeSerializedInventoryEntries();
        ApplySerializedCapsToBottleCapInventory();
        ProcessInventoryMutation(false);
        CacheOverEncumberedState();
    }


    private void OnValidate()
    {
        EnsureBottleCapDefinitionReference();
        NormalizeSerializedInventoryEntries();

        // Inspector edits clamp to non-negative values.
        caps = Mathf.Max(0, caps);

        // Apply direct inspector changes on the Caps field back into inventory.
        if (caps != lastValidatedCaps)
            ApplySerializedCapsToBottleCapInventory();

        ProcessInventoryMutation(false);

        if (Application.isPlaying && hasOverEncumberedStateCache)
            DetectOverEncumberedStateChange();
        else
            CacheOverEncumberedState();
    }


    public bool AddItem(ScriptableObject itemToAdd, int amount)
    {
        // Stop if the item is missing.
        if (!itemToAdd) return false;

        // Stop if the amount is invalid.
        if (amount <= 0) return false;

        // Validate that the item definition type matches its enforced category.
        if (!IsItemTypeValidForCategory(itemToAdd)) return false;

        // Get the correct category list.
        InventoryCategory category = GetInventoryCategoryOrDefault(itemToAdd);
        List<InventoryEntry> targetList = GetListForCategory(category);

        // Stop if the category is unknown.
        if (targetList == null) return false;

        // Read stack behaviour for this item type.
        bool canStack = CanItemStack(itemToAdd);
        int maxStackSize = canStack ? GetMaxStackSizeOrDefault(itemToAdd) : 0;

        // If stacking is allowed, try to add into an existing stack first.
        if (canStack)
        {
            // Find the first entry with available stack space.
            InventoryEntry existingEntry = FindFirstStackEntryWithSpace(targetList, itemToAdd, maxStackSize);

            // If we found one, try to fill it.
            if (existingEntry != null)
            {
                // Unlimited stack size can take all quantity.
                if (maxStackSize <= 0)
                {
                    existingEntry.AddStackQuantity(amount);
                    NotifyInventoryChanged();
                    return true;
                }

                // Work out free capacity in this stack.
                int spaceLeft = maxStackSize - existingEntry.GetQuantity();

                // If it can hold all quantity, add and finish.
                if (spaceLeft >= amount)
                {
                    existingEntry.AddStackQuantity(amount);
                    NotifyInventoryChanged();
                    return true;
                }

                // Fill this stack and continue with leftovers.
                if (spaceLeft > 0)
                {
                    existingEntry.AddStackQuantity(spaceLeft);
                    amount -= spaceLeft;
                }
            }
        }

        // Create new entries for remaining amount.
        while (amount > 0)
        {
            // If this item stacks, create a stack entry.
            if (canStack)
            {
                // Default this stack to all remaining quantity.
                int stackAmount = amount;

                // Clamp to max stack if there is a limit.
                if (maxStackSize > 0)
                    stackAmount = Mathf.Min(amount, maxStackSize);

                // Create the new stack entry.
                InventoryEntry newEntry = new InventoryEntry(itemToAdd);
                newEntry.AddStackQuantity(stackAmount);
                targetList.Add(newEntry);

                // Reduce remaining amount.
                amount -= stackAmount;
                continue;
            }

            // Non-stackable items are one instance per entry.
            InventoryEntry uniqueEntry = new InventoryEntry(itemToAdd);
            uniqueEntry.AddSingleInstance(100.0f);
            targetList.Add(uniqueEntry);
            amount -= 1;
        }

        // Notify listeners.
        NotifyInventoryChanged();

        // Confirm success.
        return true;
    }


    public bool AddItemInstance(ScriptableObject itemToAdd, float conditionPercent, int loadedMagazineRounds = 0)
    {
        // Stop if the item is missing.
        if (!itemToAdd) return false;

        // Validate that the item definition type matches its enforced category.
        if (!IsItemTypeValidForCategory(itemToAdd)) return false;

        // Stacking categories should use AddItem quantity flow.
        if (CanItemStack(itemToAdd))
            return AddItem(itemToAdd, 1);

        // Get the correct category list.
        InventoryCategory category = GetInventoryCategoryOrDefault(itemToAdd);
        List<InventoryEntry> targetList = GetListForCategory(category);

        // Stop if the category is unknown.
        if (targetList == null) return false;

        // Create one uniquely tracked item instance.
        InventoryEntry uniqueEntry = new InventoryEntry(itemToAdd);
        uniqueEntry.AddSingleInstance(conditionPercent, loadedMagazineRounds);
        targetList.Add(uniqueEntry);

        // Notify listeners.
        NotifyInventoryChanged();

        // Confirm success.
        return true;
    }


    public bool RemoveItem(ScriptableObject itemToRemove, int amount)
    {
        // Stop if the item is missing.
        if (!itemToRemove) return false;

        // Stop if the amount is invalid.
        if (amount <= 0) return false;

        // Validate that the item definition type matches its enforced category.
        if (!IsItemTypeValidForCategory(itemToRemove)) return false;

        // Get the correct category list.
        InventoryCategory category = GetInventoryCategoryOrDefault(itemToRemove);
        List<InventoryEntry> targetList = GetListForCategory(category);

        // Stop if the category is unknown.
        if (targetList == null) return false;

        // Track whether anything changed.
        bool inventoryChanged = false;

        // Walk backwards so removal is safe.
        for (int i = targetList.Count - 1; i >= 0 && amount > 0; i--)
        {
            // Cache the entry.
            InventoryEntry entry = targetList[i];

            // Skip non-matching items.
            if (entry.GetItemDefinition() != itemToRemove) continue;

            // Remove quantity from this entry.
            int remainingAfterEntry = entry.RemoveQuantity(amount);

            // Mark that data changed when any amount was removed.
            if (remainingAfterEntry != amount)
                inventoryChanged = true;

            // Keep only entries with remaining quantity.
            if (entry.IsEmpty())
                targetList.RemoveAt(i);

            // Continue with remaining amount.
            amount = remainingAfterEntry;
        }

        // Notify listeners only if inventory changed.
        if (inventoryChanged)
        {
            NotifyInventoryChanged();
        }

        // Return success only when all requested quantity was removed.
        return amount == 0;
    }


    public bool RemoveInventoryEntry(InventoryEntry entryToRemove, int amount = 1)
    {
        // Stop if entry is missing.
        if (entryToRemove == null) return false;

        // Stop if amount is invalid.
        if (amount <= 0) return false;

        // Stop if definition is missing.
        ScriptableObject itemDefinition = entryToRemove.GetItemDefinition();
        if (!itemDefinition) return false;

        // Get the correct category list.
        InventoryCategory category = GetInventoryCategoryOrDefault(itemDefinition);
        List<InventoryEntry> targetList = GetListForCategory(category);

        // Stop if the category is unknown.
        if (targetList == null) return false;

        // Find the exact entry reference.
        int targetIndex = targetList.IndexOf(entryToRemove);
        if (targetIndex < 0) return false;

        // Remove quantity from this exact entry.
        InventoryEntry targetEntry = targetList[targetIndex];
        int remainingAfterEntry = targetEntry.RemoveQuantity(amount);

        // Stop if no quantity was removed.
        if (remainingAfterEntry == amount) return false;

        // Remove empty entries.
        if (targetEntry.IsEmpty())
            targetList.RemoveAt(targetIndex);

        // Notify listeners.
        NotifyInventoryChanged();

        // Return success only when all requested quantity was removed.
        return remainingAfterEntry == 0;
    }


    public IReadOnlyList<InventoryEntry> GetCategoryItems(InventoryCategory category)
    {
        // Return the list for this category.
        return GetListForCategory(category);
    }


    public int GetTotalCount(ScriptableObject item)
    {
        // Stop if item is missing.
        if (!item) return 0;

        // Validate that the item definition type matches its enforced category.
        if (!IsItemTypeValidForCategory(item)) return 0;

        // Get the correct category list.
        InventoryCategory category = GetInventoryCategoryOrDefault(item);
        List<InventoryEntry> targetList = GetListForCategory(category);

        // Stop if category is unknown.
        if (targetList == null) return 0;

        // Track total quantity.
        int total = 0;

        // Sum across all entries.
        for (int i = 0; i < targetList.Count; i++)
        {
            // Skip non-matching items.
            if (targetList[i].GetItemDefinition() != item) continue;

            // Add entry quantity.
            total += targetList[i].GetQuantity();
        }

        // Return total.
        return total;
    }


    public int GetAmmoCount(AmmoDefinition ammoType)
    {
        // Stop if ammo type is missing.
        if (!ammoType) return 0;

        // Return the total count for this ammo type.
        return GetTotalCount(ammoType);
    }


    public int GetWeaponDamage(WeaponDefinition weapon)
    {
        // Stop if weapon is missing.
        if (!weapon) return 0;

        // Return damage from the weapon ScriptableObject.
        return weapon.GetDamage();
    }


    public string GetInstanceId(InventoryEntry entry, int instanceIndex)
    {
        // Stop if entry is missing.
        if (entry == null) return string.Empty;

        // Return selected instance id.
        return entry.GetInstanceId(instanceIndex);
    }


    public int GetInstanceLoadedMagazineRounds(InventoryEntry entry, int instanceIndex)
    {
        // Stop if entry is missing.
        if (entry == null) return 0;

        // Return selected instance loaded rounds.
        return entry.GetInstanceLoadedMagazineRounds(instanceIndex);
    }


    public bool SetInstanceLoadedMagazineRounds(InventoryEntry entry, int instanceIndex, int loadedRounds, bool notifyChange = false)
    {
        // Stop if entry is missing.
        if (entry == null) return false;

        // Apply new loaded rounds on this specific instance.
        bool updated = entry.SetInstanceLoadedMagazineRounds(instanceIndex, loadedRounds);
        if (!updated) return false;

        // Optional inventory change event for UI flows that need immediate refresh.
        if (notifyChange)
            NotifyInventoryChanged();

        return true;
    }


    public bool TryGetWeaponMagazineRoundsByInstanceId(string instanceId, out int loadedRounds)
    {
        loadedRounds = 0;

        // Stop if id is missing.
        if (string.IsNullOrWhiteSpace(instanceId)) return false;

        if (TryFindWeaponInstanceById(instanceId, out InventoryEntry entry, out int instanceIndex) == false)
            return false;

        loadedRounds = entry.GetInstanceLoadedMagazineRounds(instanceIndex);
        return true;
    }


    public bool TrySetWeaponMagazineRoundsByInstanceId(string instanceId, int loadedRounds, bool notifyChange = false)
    {
        // Stop if id is missing.
        if (string.IsNullOrWhiteSpace(instanceId)) return false;

        if (TryFindWeaponInstanceById(instanceId, out InventoryEntry entry, out int instanceIndex) == false)
            return false;

        bool updated = entry.SetInstanceLoadedMagazineRounds(instanceIndex, loadedRounds);
        if (!updated) return false;

        // Optional inventory change event for UI flows that need immediate refresh.
        if (notifyChange)
            NotifyInventoryChanged();

        return true;
    }


    public float GetInstanceConditionPercent(InventoryEntry entry, int instanceIndex)
    {
        // Stop if entry is missing.
        if (entry == null) return 0.0f;

        // Read item instances.
        IReadOnlyList<ItemInstanceData> itemInstances = entry.GetItemInstances();

        // Stop if index is invalid.
        if (instanceIndex < 0 || instanceIndex >= itemInstances.Count) return 0.0f;

        // Return selected instance condition.
        return itemInstances[instanceIndex].GetConditionPercent();
    }


    public bool SetInstanceConditionPercent(InventoryEntry entry, int instanceIndex, float conditionPercent, bool notifyChange = false)
    {
        // Stop if entry is missing.
        if (entry == null) return false;

        // Read item instances.
        IReadOnlyList<ItemInstanceData> itemInstances = entry.GetItemInstances();

        // Stop if index is invalid.
        if (instanceIndex < 0 || instanceIndex >= itemInstances.Count) return false;

        // Stop if selected instance is missing.
        ItemInstanceData selectedInstance = itemInstances[instanceIndex];
        if (selectedInstance == null) return false;

        // Clamp and apply condition using definition-aware value recalculation.
        selectedInstance.SetConditionPercent(entry.GetItemDefinition(), conditionPercent);

        // Optional inventory change event for UI flows that need immediate refresh.
        if (notifyChange)
            NotifyInventoryChanged();

        return true;
    }


    public bool TryGetWeaponConditionPercentByInstanceId(string instanceId, out float conditionPercent)
    {
        conditionPercent = 0.0f;

        // Stop if id is missing.
        if (string.IsNullOrWhiteSpace(instanceId)) return false;

        if (TryFindWeaponInstanceById(instanceId, out InventoryEntry entry, out int instanceIndex) == false)
            return false;

        conditionPercent = GetInstanceConditionPercent(entry, instanceIndex);
        return true;
    }


    public bool TrySetWeaponConditionPercentByInstanceId(string instanceId, float conditionPercent, bool notifyChange = false)
    {
        // Stop if id is missing.
        if (string.IsNullOrWhiteSpace(instanceId)) return false;

        if (TryFindWeaponInstanceById(instanceId, out InventoryEntry entry, out int instanceIndex) == false)
            return false;

        return SetInstanceConditionPercent(entry, instanceIndex, conditionPercent, notifyChange);
    }


    public float GetInstanceValue(InventoryEntry entry, int instanceIndex)
    {
        // Stop if entry is missing.
        if (entry == null) return 0.0f;

        // Read item instances.
        IReadOnlyList<ItemInstanceData> itemInstances = entry.GetItemInstances();

        // Stop if index is invalid.
        if (instanceIndex < 0 || instanceIndex >= itemInstances.Count) return 0.0f;

        // Keep this instance value synced with current condition/definition.
        ItemInstanceData selectedInstance = itemInstances[instanceIndex];
        selectedInstance.SetConditionPercent(entry.GetItemDefinition(), selectedInstance.GetConditionPercent());

        // Return selected instance value.
        return selectedInstance.GetValue();
    }


    public float GetWeight()
    {
        // Keep serialized weight in sync with inventory contents.
        RefreshInventoryDerivedDataIfDirty();

        // Return current carried weight.
        return weight;
    }


    public void SetWeight(float _)
    {
        // Weight is derived from inventory contents.
        RefreshInventoryDerivedDataIfDirty();

        DetectOverEncumberedStateChange();

        // Notify listeners.
        OnInventoryChanged?.Invoke();
    }


    public float GetMaxWeight()
    {
        // Return maximum carry weight.
        return maxWeight;
    }


    public void SetMaxWeight(float newMaxWeight)
    {
        // Store max weight as a non-negative value.
        maxWeight = Mathf.Max(0.0f, newMaxWeight);
        RefreshOverEncumberedFlag();

        DetectOverEncumberedStateChange();

        // Notify listeners.
        OnInventoryChanged?.Invoke();
    }


    public int GetTotalDamageResistance()
    {
        // Return total damage resistance.
        return totalDamageResistance;
    }


    public void SetTotalDamageResistance(int newTotalDamageResistance)
    {
        // Store damage resistance as a non-negative value.
        totalDamageResistance = Mathf.Max(0, newTotalDamageResistance);

        // Notify listeners.
        OnInventoryChanged?.Invoke();
    }


    public int GetCaps()
    {
        // Return current caps.
        return caps;
    }


    public void SetCaps(int newCaps)
    {
        // Store caps as a non-negative value.
        caps = Mathf.Max(0, newCaps);

        // Keep Bottle Cap item quantity synced with caps.
        ApplySerializedCapsToBottleCapInventory();

        // Notify listeners.
        ProcessInventoryMutation(true);
    }


    public int GetTotalCaps()
    {
        // Return total tracked caps.
        return totalCaps;
    }


    public void SetTotalCaps(int newTotalCaps)
    {
        // Store total caps as a non-negative value.
        totalCaps = Mathf.Max(0, newTotalCaps);

        // Notify listeners.
        OnInventoryChanged?.Invoke();
    }



    private List<InventoryEntry> GetListForCategory(InventoryCategory category)
    {
        // Return weapons list.
        if (category == InventoryCategory.Weapons) return weapons;

        // Return apparel list.
        if (category == InventoryCategory.Apparel) return apparel;

        // Return aid list.
        if (category == InventoryCategory.Aid) return aid;

        // Return misc list.
        if (category == InventoryCategory.Misc) return misc;

        // Return ammo list.
        if (category == InventoryCategory.Ammo) return ammo;

        // Unknown category.
        return null;
    }


    private static bool DefinitionSupportsCondition(ScriptableObject definition)
    {
        if (!definition) return false;
        return definition is WeaponDefinition || definition is ApparelDefinition;
    }


    private static bool DefinitionSupportsLoadedMagazineRounds(ScriptableObject definition)
    {
        if (!(definition is WeaponDefinition weaponDefinition))
            return false;

        // Firearm-like weapons have a magazine to track.
        return weaponDefinition.GetMagazineSize() > 0;
    }


    private void NormalizeSerializedInventoryEntries()
    {
        NormalizeCategoryEntries(weapons);
        NormalizeCategoryEntries(apparel);
        NormalizeCategoryEntries(aid);
        NormalizeCategoryEntries(misc);
        NormalizeCategoryEntries(ammo);
    }


    private static void NormalizeCategoryEntries(List<InventoryEntry> entries)
    {
        if (entries == null) return;

        for (int i = entries.Count - 1; i >= 0; i--)
        {
            InventoryEntry entry = entries[i];
            if (entry == null)
            {
                entries.RemoveAt(i);
                continue;
            }

            entry.NormalizeSerializedData();
        }
    }


    private InventoryEntry FindFirstStackEntryWithSpace(List<InventoryEntry> list, ScriptableObject item, int maxStackSize)
    {
        // Stop if list is missing.
        if (list == null) return null;

        // Search for the first matching entry that has capacity.
        for (int i = 0; i < list.Count; i++)
        {
            // Skip non-matching definitions.
            if (list[i].GetItemDefinition() != item) continue;

            // Unlimited stacks always have capacity.
            if (maxStackSize <= 0) return list[i];

            // Return first entry that is not full.
            if (list[i].GetQuantity() < maxStackSize) return list[i];
        }

        // Not found.
        return null;
    }


    private bool CanItemStack(ScriptableObject item)
    {
        // Stop if item is missing.
        if (!item) return false;

        // Weapons and apparel are always unique instances.
        InventoryCategory category = GetInventoryCategoryOrDefault(item);
        if (category == InventoryCategory.Weapons) return false;
        if (category == InventoryCategory.Apparel) return false;

        // Other categories follow definition stack settings.
        return IsStackableOrDefault(item, true);
    }


    private float CalculateCurrentWeight()
    {
        // Track full inventory weight.
        float totalWeight = 0.0f;

        // Add all item category lists.
        totalWeight += CalculateCategoryWeight(weapons);
        totalWeight += CalculateCategoryWeight(apparel);
        totalWeight += CalculateCategoryWeight(aid);
        totalWeight += CalculateCategoryWeight(misc);
        totalWeight += CalculateCategoryWeight(ammo);

        // Clamp for safety.
        return Mathf.Max(0.0f, totalWeight);
    }


    private float CalculateCategoryWeight(List<InventoryEntry> entries)
    {
        // Stop if the list is missing.
        if (entries == null) return 0.0f;

        // Track category weight.
        float categoryWeight = 0.0f;

        // Sum item weight * quantity for every entry.
        for (int i = 0; i < entries.Count; i++)
        {
            InventoryEntry entry = entries[i];
            if (entry == null) continue;

            float entryWeight = Mathf.Max(0.0f, GetDefinitionWeightOrDefault(entry.GetItemDefinition()));
            categoryWeight += entryWeight * Mathf.Max(0, entry.GetQuantity());
        }

        // Return this category total.
        return categoryWeight;
    }


    private void RecalculateWeight()
    {
        // Weight is always derived from inventory contents.
        weight = CalculateCurrentWeight();
        RefreshOverEncumberedFlag();
    }

    private void MarkInventoryDerivedDataDirty()
    {
        inventoryDerivedDataDirty = true;
    }

    private void RefreshInventoryDerivedDataIfDirty()
    {
        if (!inventoryDerivedDataDirty)
            return;

        // Keep ids, per-instance values, and aggregate weight internally consistent.
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
        SyncCapsFromBottleCapInventory();
        lastValidatedCaps = caps;

        if (notifyChange)
        {
            DetectOverEncumberedStateChange();
            OnInventoryChanged?.Invoke();
        }
    }


    private void CacheOverEncumberedState()
    {
        lastOverEncumbered = IsOverEncumbered();
        hasOverEncumberedStateCache = true;
    }


    private void DetectOverEncumberedStateChange()
    {
        bool isOverEncumbered = IsOverEncumbered();
        bool becameOverEncumbered = !lastOverEncumbered && isOverEncumbered;
        lastOverEncumbered = isOverEncumbered;
        hasOverEncumberedStateCache = true;

        if (becameOverEncumbered)
            ShowOverEncumberedMessage();
    }


    public bool IsOverEncumbered()
    {
        RefreshInventoryDerivedDataIfDirty();
        RefreshOverEncumberedFlag();
        return IsOverencumbered;
    }


    public bool GetIsOverencumbered()
    {
        return IsOverEncumbered();
    }


    private void RefreshOverEncumberedFlag()
    {
        IsOverencumbered = weight > maxWeight;
    }


    private void ShowOverEncumberedMessage()
    {
        if (showOverEncumberedMessage)
            UI.HUDMessagePanelController.Queue(OverEncumberedMessage);
    }


    private void EnsureBottleCapDefinitionReference()
    {
        // Keep existing inspector assignment when available.
        if (bottleCapDefinition)
            return;

        // Try to infer from existing misc entries first.
        bottleCapDefinition = FindBottleCapDefinitionInMiscEntries();

#if UNITY_EDITOR
        // Fallback to known project asset path.
        if (!bottleCapDefinition)
            bottleCapDefinition = AssetDatabase.LoadAssetAtPath<MiscDefinition>(BottleCapDefinitionAssetPath);
#endif
    }


    private MiscDefinition FindBottleCapDefinitionInMiscEntries()
    {
        if (misc == null) return null;

        for (int i = 0; i < misc.Count; i++)
        {
            InventoryEntry entry = misc[i];
            if (entry == null) continue;

            if (!(entry.GetItemDefinition() is MiscDefinition miscDefinition))
                continue;

            if (IsBottleCapDefinition(miscDefinition))
                return miscDefinition;
        }

        return null;
    }


    private bool IsBottleCapDefinition(MiscDefinition definition)
    {
        if (!definition) return false;

        if (string.Equals(definition.name, BottleCapAssetName, StringComparison.OrdinalIgnoreCase))
            return true;

        return string.Equals(definition.GetDisplayName(), BottleCapDisplayName, StringComparison.OrdinalIgnoreCase);
    }


    private void ApplySerializedCapsToBottleCapInventory()
    {
        EnsureBottleCapDefinitionReference();

        if (!bottleCapDefinition)
            return;

        List<InventoryEntry> miscEntries = GetListForCategory(InventoryCategory.Misc);
        if (miscEntries == null)
            return;

        SetInventoryItemCount(miscEntries, bottleCapDefinition, caps);
    }


    private void SyncCapsFromBottleCapInventory()
    {
        EnsureBottleCapDefinitionReference();

        // Keep field clamped even when no bottle cap definition is available.
        if (!bottleCapDefinition)
        {
            caps = Mathf.Max(0, caps);
            return;
        }

        caps = Mathf.Max(0, GetTotalCount(bottleCapDefinition));
    }


    private void SetInventoryItemCount(List<InventoryEntry> targetList, ScriptableObject itemDefinition, int quantity)
    {
        if (targetList == null) return;
        if (!itemDefinition) return;

        int clampedQuantity = Mathf.Max(0, quantity);

        // Remove all existing entries for this item so quantity can be rebuilt exactly.
        for (int i = targetList.Count - 1; i >= 0; i--)
        {
            InventoryEntry existingEntry = targetList[i];
            if (existingEntry == null) continue;

            if (existingEntry.GetItemDefinition() == itemDefinition)
                targetList.RemoveAt(i);
        }

        if (clampedQuantity <= 0)
            return;

        bool canStack = CanItemStack(itemDefinition);
        int maxStackSize = canStack ? GetMaxStackSizeOrDefault(itemDefinition) : 0;

        if (canStack)
        {
            int remaining = clampedQuantity;
            while (remaining > 0)
            {
                int stackAmount = maxStackSize > 0 ? Mathf.Min(remaining, maxStackSize) : remaining;

                InventoryEntry stackedEntry = new InventoryEntry(itemDefinition);
                stackedEntry.AddStackQuantity(stackAmount);
                targetList.Add(stackedEntry);

                remaining -= stackAmount;
            }

            return;
        }

        for (int i = 0; i < clampedQuantity; i++)
        {
            InventoryEntry uniqueEntry = new InventoryEntry(itemDefinition);
            uniqueEntry.AddSingleInstance(100.0f);
            targetList.Add(uniqueEntry);
        }
    }


    private void RecalculateAllInstanceValues()
    {
        // Recalculate all categories.
        RecalculateCategoryInstanceValues(weapons);
        RecalculateCategoryInstanceValues(apparel);
        RecalculateCategoryInstanceValues(aid);
        RecalculateCategoryInstanceValues(misc);
        RecalculateCategoryInstanceValues(ammo);
    }


    private void EnsureAllInstanceIds()
    {
        // Track all ids across the full inventory so duplicates can be resolved.
        HashSet<string> usedInstanceIds = new HashSet<string>();

        // Normalize ids in every category.
        EnsureCategoryInstanceIds(weapons, usedInstanceIds);
        EnsureCategoryInstanceIds(apparel, usedInstanceIds);
        EnsureCategoryInstanceIds(aid, usedInstanceIds);
        EnsureCategoryInstanceIds(misc, usedInstanceIds);
        EnsureCategoryInstanceIds(ammo, usedInstanceIds);
    }


    private void EnsureCategoryInstanceIds(List<InventoryEntry> entries, HashSet<string> usedInstanceIds)
    {
        // Stop if the list is missing.
        if (entries == null) return;

        // Ensure each entry's instances have unique ids.
        for (int i = 0; i < entries.Count; i++)
        {
            InventoryEntry entry = entries[i];
            if (entry == null) continue;

            entry.EnsureUniqueInstanceIds(usedInstanceIds);
        }
    }


    private void RecalculateCategoryInstanceValues(List<InventoryEntry> entries)
    {
        // Stop if the list is missing.
        if (entries == null) return;

        // Recalculate each entry.
        for (int i = 0; i < entries.Count; i++)
        {
            InventoryEntry entry = entries[i];
            if (entry == null) continue;

            entry.RecalculateInstanceValues();
        }
    }


    private bool TryFindWeaponInstanceById(string instanceId, out InventoryEntry weaponEntry, out int instanceIndex)
    {
        weaponEntry = null;
        instanceIndex = -1;

        // Stop if id is missing.
        if (string.IsNullOrWhiteSpace(instanceId)) return false;

        // Stop if weapon list is missing.
        if (weapons == null) return false;

        for (int entryIndex = 0; entryIndex < weapons.Count; entryIndex++)
        {
            InventoryEntry entry = weapons[entryIndex];
            if (entry == null) continue;

            IReadOnlyList<ItemInstanceData> instances = entry.GetItemInstances();
            for (int currentInstanceIndex = 0; currentInstanceIndex < instances.Count; currentInstanceIndex++)
            {
                ItemInstanceData instance = instances[currentInstanceIndex];
                if (instance == null) continue;

                if (!string.Equals(instance.GetInstanceId(), instanceId, StringComparison.Ordinal))
                    continue;

                weaponEntry = entry;
                instanceIndex = currentInstanceIndex;
                return true;
            }
        }

        return false;
    }


    private bool IsItemTypeValidForCategory(ScriptableObject item)
    {
        // Stop if item is missing.
        if (!item) return false;

        // Stop if the category cannot be resolved.
        if (!TryGetInventoryCategory(item, out InventoryCategory category))
            return false;

        // Weapons category must be WeaponDefinition.
        if (category == InventoryCategory.Weapons)
            return item is WeaponDefinition;

        // Apparel category must be ApparelDefinition.
        if (category == InventoryCategory.Apparel)
            return item is ApparelDefinition;

        // Aid category must be AidDefinition.
        if (category == InventoryCategory.Aid)
            return item is AidDefinition;

        // Misc category must be MiscDefinition.
        if (category == InventoryCategory.Misc)
            return item is MiscDefinition;

        // Ammo category must be AmmoDefinition.
        if (category == InventoryCategory.Ammo)
            return item is AmmoDefinition;

        // Unknown category.
        return false;
    }

    
    public bool TryReceiveWorldItem(ScriptableObject definition, int quantity, WorldItem worldItem)
    {
        // Stop If The Definition Is Missing.
        if (!definition) return false;

        // Ammo item container definitions resolve to their contained ammo type.
        if (definition is AmmoItemDefinition ammoItemDefinition)
        {
            AmmoDefinition containedAmmoDefinition = ammoItemDefinition.GetAmmoDefinition();
            if (!containedAmmoDefinition)
                return false;

            definition = containedAmmoDefinition;
        }

        // Stop If The Quantity Is Invalid.
        if (quantity <= 0) return false;

        // Read source condition (defaults to perfect if no source is provided).
        float conditionPercent = worldItem != null ? worldItem.GetConditionPercent() : 100.0f;

        // Ammo world items can provide an explicit rounds value via AmmoItem.
        if (definition is AmmoDefinition && worldItem != null)
        {
            AmmoItem sourceAmmoItem = worldItem.GetComponent<AmmoItem>();
            if (sourceAmmoItem != null)
                quantity = Mathf.Max(1, sourceAmmoItem.GetRounds());
        }

        // Read source loaded magazine rounds from a weapon world component (if present).
        WeaponItem sourceWeaponItem = worldItem != null ? worldItem.GetComponent<WeaponItem>() : null;
        int loadedMagazineRounds = sourceWeaponItem != null ? sourceWeaponItem.GetLoadedMagazineRounds() : 0;

        // Stackable items still use stack quantity flow.
        if (CanItemStack(definition))
            return AddItem(definition, quantity);

        // Non-stackable pickups must preserve source condition per instance.
        bool addedAny = false;
        for (int i = 0; i < quantity; i++)
        {
            int instanceLoadedRounds = definition is WeaponDefinition ? loadedMagazineRounds : 0;
            if (!AddItemInstance(definition, conditionPercent, instanceLoadedRounds))
                return false;

            addedAny = true;
        }

        // Confirm at least one instance was created.
        if (addedAny)
            return true;

        // Add The Item Into The Inventory Using Existing Logic.
        return AddItem(definition, quantity);
    }


    private bool TryGetInventoryCategory(ScriptableObject definition, out InventoryCategory category)
    {
        if (!definition)
        {
            category = InventoryCategory.Misc;
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

        category = InventoryCategory.Misc;
        return false;
    }


    private InventoryCategory GetInventoryCategoryOrDefault(ScriptableObject definition, InventoryCategory fallback = InventoryCategory.Misc)
    {
        if (!TryGetInventoryCategory(definition, out InventoryCategory category))
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


