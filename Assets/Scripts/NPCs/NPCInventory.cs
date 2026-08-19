using System;
using System.Collections.Generic;
using UnityEngine;

public class NPCInventory : MonoBehaviour, WorldItem.IWorldItemReceiver
{
    private const float DefaultConditionPercent = 100f;

    public enum InventoryCategory
    {
        Weapons,
        Apparel,
        Aid,
        Misc,
        Ammo
    }

    [Serializable]
    public class ItemInstanceData
    {
        [SerializeField] private string instanceId;
        [SerializeField] private int quantity = 1;
        [Range(0f, 100f)] [SerializeField] private float conditionPercent = DefaultConditionPercent;
        [Min(0)] [SerializeField] private int loadedMagazineRounds;
        [SerializeField] private float value;

        public ItemInstanceData(ScriptableObject definition, int newQuantity, float newConditionPercent, int newLoadedMagazineRounds = 0)
        {
            instanceId = Guid.NewGuid().ToString("N");
            quantity = Mathf.Max(1, newQuantity);
            SetConditionPercent(definition, newConditionPercent);
            SetLoadedMagazineRounds(definition, newLoadedMagazineRounds);
        }

        public string GetInstanceId() => instanceId;
        public int GetQuantity() => quantity;
        public float GetConditionPercent() => conditionPercent;
        public float GetValue() => value;
        public int GetLoadedMagazineRounds() => Mathf.Max(0, loadedMagazineRounds);

        public void SetQuantity(int newQuantity)
        {
            quantity = Mathf.Max(1, newQuantity);
        }

        public void SetConditionPercent(ScriptableObject definition, float newConditionPercent)
        {
            conditionPercent = Mathf.Clamp(newConditionPercent, 0f, 100f);
            float conditionScale = DefinitionSupportsCondition(definition) ? conditionPercent / 100f : 1f;
            value = Mathf.Max(0f, GetBaseValue(definition) * conditionScale);
        }

        public void SetLoadedMagazineRounds(ScriptableObject definition, int newLoadedMagazineRounds)
        {
            if (!(definition is WeaponDefinition weaponDefinition))
            {
                loadedMagazineRounds = 0;
                return;
            }

            loadedMagazineRounds = Mathf.Clamp(newLoadedMagazineRounds, 0, Mathf.Max(0, weaponDefinition.GetMagazineSize()));
        }

        public void EnsureUniqueInstanceId(HashSet<string> usedIds)
        {
            if (string.IsNullOrWhiteSpace(instanceId) || usedIds.Contains(instanceId))
                instanceId = Guid.NewGuid().ToString("N");

            while (usedIds.Contains(instanceId))
                instanceId = Guid.NewGuid().ToString("N");

            usedIds.Add(instanceId);
        }
    }

    [Serializable]
    public class InventoryEntry
    {
        [SerializeField] private ScriptableObject itemDefinition;
        [SerializeField] private List<ItemInstanceData> itemInstances = new List<ItemInstanceData>();

        public InventoryEntry(ScriptableObject definition)
        {
            itemDefinition = definition;
        }

        public ScriptableObject GetItemDefinition() => itemDefinition;

        public IReadOnlyList<ItemInstanceData> GetItemInstances()
        {
            EnsureItemInstances();
            return itemInstances;
        }

        public int GetQuantity()
        {
            EnsureItemInstances();
            int total = 0;
            for (int i = 0; i < itemInstances.Count; i++)
                if (itemInstances[i] != null)
                    total += itemInstances[i].GetQuantity();
            return total;
        }

        public void AddStackQuantity(int amount)
        {
            if (amount <= 0) return;
            EnsureItemInstances();

            if (itemInstances.Count == 0 || itemInstances[0] == null)
            {
                if (itemInstances.Count == 0)
                    itemInstances.Add(new ItemInstanceData(itemDefinition, amount, DefaultConditionPercent));
                else
                    itemInstances[0] = new ItemInstanceData(itemDefinition, amount, DefaultConditionPercent);
                return;
            }

            itemInstances[0].SetQuantity(itemInstances[0].GetQuantity() + amount);
        }

        public void AddSingleInstance(float conditionPercent, int loadedMagazineRounds = 0)
        {
            EnsureItemInstances();
            itemInstances.Add(new ItemInstanceData(itemDefinition, 1, conditionPercent, loadedMagazineRounds));
        }

        public int RemoveQuantity(int amount)
        {
            if (amount <= 0) return 0;
            EnsureItemInstances();

            int remaining = amount;
            for (int i = itemInstances.Count - 1; i >= 0 && remaining > 0; i--)
            {
                ItemInstanceData instance = itemInstances[i];
                if (instance == null)
                {
                    itemInstances.RemoveAt(i);
                    continue;
                }

                int quantity = instance.GetQuantity();
                if (quantity <= remaining)
                {
                    remaining -= quantity;
                    itemInstances.RemoveAt(i);
                    continue;
                }

                instance.SetQuantity(quantity - remaining);
                remaining = 0;
            }

            return remaining;
        }

        public bool IsEmpty() => GetQuantity() <= 0;
        public string GetInstanceId(int instanceIndex) => GetInstance(instanceIndex)?.GetInstanceId() ?? string.Empty;
        public int GetInstanceLoadedMagazineRounds(int instanceIndex) => GetInstance(instanceIndex)?.GetLoadedMagazineRounds() ?? 0;

        public bool SetInstanceLoadedMagazineRounds(int instanceIndex, int loadedRounds)
        {
            ItemInstanceData instance = GetInstance(instanceIndex);
            if (instance == null) return false;
            instance.SetLoadedMagazineRounds(itemDefinition, loadedRounds);
            return true;
        }

        public void Normalize()
        {
            EnsureItemInstances();
            for (int i = itemInstances.Count - 1; i >= 0; i--)
                if (itemInstances[i] == null)
                    itemInstances.RemoveAt(i);

            if (!itemDefinition) return;
            if (itemInstances.Count == 0)
                itemInstances.Add(new ItemInstanceData(itemDefinition, 1, DefaultConditionPercent));

            bool supportsCondition = DefinitionSupportsCondition(itemDefinition);
            bool supportsLoadedRounds = DefinitionSupportsLoadedMagazineRounds(itemDefinition);
            for (int i = 0; i < itemInstances.Count; i++)
            {
                ItemInstanceData instance = itemInstances[i];
                float condition = supportsCondition ? instance.GetConditionPercent() : DefaultConditionPercent;
                int loadedRounds = supportsLoadedRounds ? instance.GetLoadedMagazineRounds() : 0;
                instance.SetConditionPercent(itemDefinition, condition);
                instance.SetLoadedMagazineRounds(itemDefinition, loadedRounds);
            }
        }

        public void EnsureUniqueInstanceIds(HashSet<string> usedIds)
        {
            EnsureItemInstances();
            for (int i = 0; i < itemInstances.Count; i++)
                itemInstances[i]?.EnsureUniqueInstanceId(usedIds);
        }

        public void RecalculateInstanceValues()
        {
            EnsureItemInstances();
            for (int i = 0; i < itemInstances.Count; i++)
                itemInstances[i]?.SetConditionPercent(itemDefinition, itemInstances[i].GetConditionPercent());
        }

        private ItemInstanceData GetInstance(int index)
        {
            EnsureItemInstances();
            return index >= 0 && index < itemInstances.Count ? itemInstances[index] : null;
        }

        private void EnsureItemInstances()
        {
            if (itemInstances == null)
                itemInstances = new List<ItemInstanceData>();
        }
    }

    [Header("Inventory")]
    [SerializeField] private List<InventoryEntry> weapons = new List<InventoryEntry>();
    [SerializeField] private List<InventoryEntry> apparel = new List<InventoryEntry>();
    [SerializeField] private List<InventoryEntry> aid = new List<InventoryEntry>();
    [SerializeField] private List<InventoryEntry> misc = new List<InventoryEntry>();
    [SerializeField] private List<InventoryEntry> ammo = new List<InventoryEntry>();

    [Header("Carry")]
    [SerializeField] private float weight;
    [SerializeField] private float maxWeight = 200f;

    [Header("Combat Stats")]
    [SerializeField] private int totalDamageResistance;

    [Header("Currency")]
    [SerializeField] private int caps;
    [SerializeField] private int totalCaps;

    private bool dirty = true;

    public event Action OnInventoryChanged;

    private void Awake()
    {
        Normalize();
        RefreshDerivedData();
    }

    private void OnValidate()
    {
        maxWeight = Mathf.Max(0f, maxWeight);
        totalDamageResistance = Mathf.Max(0, totalDamageResistance);
        caps = Mathf.Max(0, caps);
        totalCaps = Mathf.Max(0, totalCaps);
        Normalize();
        RefreshDerivedData();
    }

    public bool AddItem(ScriptableObject itemToAdd, int amount)
    {
        if (!itemToAdd || amount <= 0 || !IsSupportedItemDefinition(itemToAdd)) return false;

        if (itemToAdd is AmmoItemDefinition ammoItemDefinition)
            itemToAdd = ammoItemDefinition.GetAmmoDefinition();
        if (!itemToAdd) return false;

        List<InventoryEntry> targetList = GetListForCategory(GetInventoryCategoryOrDefault(itemToAdd));
        if (targetList == null) return false;

        if (CanItemStack(itemToAdd))
        {
            InventoryEntry stack = FindStackEntry(targetList, itemToAdd, GetMaxStackSizeOrDefault(itemToAdd));
            if (stack != null)
            {
                stack.AddStackQuantity(amount);
                NotifyInventoryChanged();
                return true;
            }
        }

        if (CanItemStack(itemToAdd))
        {
            InventoryEntry entry = new InventoryEntry(itemToAdd);
            entry.AddStackQuantity(amount);
            targetList.Add(entry);
        }
        else
        {
            for (int i = 0; i < amount; i++)
            {
                InventoryEntry entry = new InventoryEntry(itemToAdd);
                entry.AddSingleInstance(DefaultConditionPercent);
                targetList.Add(entry);
            }
        }

        NotifyInventoryChanged();
        return true;
    }

    public bool AddItemInstance(ScriptableObject itemToAdd, float conditionPercent, int loadedMagazineRounds = 0)
    {
        if (!itemToAdd || !IsSupportedItemDefinition(itemToAdd)) return false;
        if (CanItemStack(itemToAdd)) return AddItem(itemToAdd, 1);

        List<InventoryEntry> targetList = GetListForCategory(GetInventoryCategoryOrDefault(itemToAdd));
        if (targetList == null) return false;

        InventoryEntry entry = new InventoryEntry(itemToAdd);
        entry.AddSingleInstance(conditionPercent, loadedMagazineRounds);
        targetList.Add(entry);
        NotifyInventoryChanged();
        return true;
    }

    public bool RemoveItem(ScriptableObject itemToRemove, int amount)
    {
        if (!itemToRemove || amount <= 0 || !IsSupportedItemDefinition(itemToRemove)) return false;
        List<InventoryEntry> targetList = GetListForCategory(GetInventoryCategoryOrDefault(itemToRemove));
        if (targetList == null) return false;

        bool changed = false;
        for (int i = targetList.Count - 1; i >= 0 && amount > 0; i--)
        {
            InventoryEntry entry = targetList[i];
            if (entry == null || entry.GetItemDefinition() != itemToRemove) continue;

            int remaining = entry.RemoveQuantity(amount);
            changed |= remaining != amount;
            amount = remaining;
            if (entry.IsEmpty()) targetList.RemoveAt(i);
        }

        if (changed) NotifyInventoryChanged();
        return amount == 0;
    }

    public bool RemoveInventoryEntry(InventoryEntry entryToRemove, int amount = 1)
    {
        if (entryToRemove == null || amount <= 0) return false;
        List<InventoryEntry> targetList = GetListForCategory(GetInventoryCategoryOrDefault(entryToRemove.GetItemDefinition()));
        if (targetList == null) return false;

        int index = targetList.IndexOf(entryToRemove);
        if (index < 0) return false;

        int remaining = targetList[index].RemoveQuantity(amount);
        if (remaining == amount) return false;
        if (targetList[index].IsEmpty()) targetList.RemoveAt(index);

        NotifyInventoryChanged();
        return remaining == 0;
    }

    public IReadOnlyList<InventoryEntry> GetCategoryItems(InventoryCategory category) => GetListForCategory(category);
    public int GetAmmoCount(AmmoDefinition ammoType) => ammoType ? GetTotalCount(ammoType) : 0;
    public int GetWeaponDamage(WeaponDefinition weapon) => weapon ? weapon.GetDamage() : 0;
    public string GetInstanceId(InventoryEntry entry, int instanceIndex) => entry != null ? entry.GetInstanceId(instanceIndex) : string.Empty;
    public int GetInstanceLoadedMagazineRounds(InventoryEntry entry, int instanceIndex) => entry != null ? entry.GetInstanceLoadedMagazineRounds(instanceIndex) : 0;
    public float GetWeight() { RefreshDerivedData(); return weight; }
    public float GetMaxWeight() => maxWeight;
    public int GetTotalDamageResistance() => totalDamageResistance;
    public int GetCaps() => caps;
    public int GetTotalCaps() => totalCaps;

    public void SetMaxWeight(float value) { maxWeight = Mathf.Max(0f, value); OnInventoryChanged?.Invoke(); }
    public void SetTotalDamageResistance(int value) { totalDamageResistance = Mathf.Max(0, value); OnInventoryChanged?.Invoke(); }
    public void SetCaps(int value) { caps = Mathf.Max(0, value); OnInventoryChanged?.Invoke(); }
    public void SetTotalCaps(int value) { totalCaps = Mathf.Max(0, value); OnInventoryChanged?.Invoke(); }

    public int GetTotalCount(ScriptableObject item)
    {
        if (!item || !IsSupportedItemDefinition(item)) return 0;
        if (item is AmmoItemDefinition ammoItemDefinition)
            item = ammoItemDefinition.GetAmmoDefinition();
        if (!item) return 0;

        List<InventoryEntry> targetList = GetListForCategory(GetInventoryCategoryOrDefault(item));
        if (targetList == null) return 0;

        int total = 0;
        for (int i = 0; i < targetList.Count; i++)
        {
            InventoryEntry entry = targetList[i];
            if (entry != null && entry.GetItemDefinition() == item)
                total += entry.GetQuantity();
        }

        return total;
    }

    public bool SetInstanceLoadedMagazineRounds(InventoryEntry entry, int instanceIndex, int loadedRounds, bool notifyChange = false)
    {
        if (entry == null) return false;
        if (!entry.SetInstanceLoadedMagazineRounds(instanceIndex, loadedRounds)) return false;
        if (notifyChange) NotifyInventoryChanged();
        else dirty = true;
        return true;
    }

    public bool TryGetWeaponMagazineRoundsByInstanceId(string instanceId, out int loadedRounds)
    {
        loadedRounds = 0;
        if (!TryFindWeaponInstanceById(instanceId, out InventoryEntry entry, out int instanceIndex)) return false;
        loadedRounds = entry.GetInstanceLoadedMagazineRounds(instanceIndex);
        return true;
    }

    public bool TrySetWeaponMagazineRoundsByInstanceId(string instanceId, int loadedRounds, bool notifyChange = false)
    {
        if (!TryFindWeaponInstanceById(instanceId, out InventoryEntry entry, out int instanceIndex)) return false;
        return SetInstanceLoadedMagazineRounds(entry, instanceIndex, loadedRounds, notifyChange);
    }

    public float GetInstanceConditionPercent(InventoryEntry entry, int instanceIndex)
    {
        if (entry == null) return 0f;
        IReadOnlyList<ItemInstanceData> instances = entry.GetItemInstances();
        if (instanceIndex < 0 || instanceIndex >= instances.Count) return 0f;

        ItemInstanceData instance = instances[instanceIndex];
        return instance != null ? instance.GetConditionPercent() : 0f;
    }

    public bool SetInstanceConditionPercent(InventoryEntry entry, int instanceIndex, float conditionPercent, bool notifyChange = false)
    {
        if (entry == null) return false;
        IReadOnlyList<ItemInstanceData> instances = entry.GetItemInstances();
        if (instanceIndex < 0 || instanceIndex >= instances.Count) return false;

        ItemInstanceData instance = instances[instanceIndex];
        if (instance == null) return false;

        instance.SetConditionPercent(entry.GetItemDefinition(), conditionPercent);
        if (notifyChange) NotifyInventoryChanged();
        else dirty = true;
        return true;
    }

    public bool TryGetWeaponConditionPercentByInstanceId(string instanceId, out float conditionPercent)
    {
        conditionPercent = 0f;
        if (!TryFindWeaponInstanceById(instanceId, out InventoryEntry entry, out int instanceIndex)) return false;
        conditionPercent = GetInstanceConditionPercent(entry, instanceIndex);
        return true;
    }

    public bool TrySetWeaponConditionPercentByInstanceId(string instanceId, float conditionPercent, bool notifyChange = false)
    {
        if (!TryFindWeaponInstanceById(instanceId, out InventoryEntry entry, out int instanceIndex)) return false;
        return SetInstanceConditionPercent(entry, instanceIndex, conditionPercent, notifyChange);
    }

    public float GetInstanceValue(InventoryEntry entry, int instanceIndex)
    {
        if (entry == null) return 0f;
        IReadOnlyList<ItemInstanceData> instances = entry.GetItemInstances();
        if (instanceIndex < 0 || instanceIndex >= instances.Count) return 0f;

        ItemInstanceData instance = instances[instanceIndex];
        if (instance == null) return 0f;

        instance.SetConditionPercent(entry.GetItemDefinition(), instance.GetConditionPercent());
        return instance.GetValue();
    }

    public bool TryReceiveWorldItem(ScriptableObject definition, int quantity, WorldItem worldItem)
    {
        if (!definition || quantity <= 0) return false;

        if (definition is AmmoItemDefinition ammoItemDefinition)
        {
            definition = ammoItemDefinition.GetAmmoDefinition();
            if (!definition) return false;
        }

        if (definition is AmmoDefinition && worldItem != null)
        {
            AmmoItem ammoItem = worldItem.GetComponent<AmmoItem>();
            if (ammoItem != null)
                quantity = Mathf.Max(1, ammoItem.GetRounds());
        }

        if (CanItemStack(definition))
            return AddItem(definition, quantity);

        float conditionPercent = worldItem != null ? worldItem.GetConditionPercent() : DefaultConditionPercent;
        WeaponItem weaponItem = worldItem != null ? worldItem.GetComponent<WeaponItem>() : null;
        int loadedRounds = weaponItem != null ? weaponItem.GetLoadedMagazineRounds() : 0;

        bool addedAny = false;
        for (int i = 0; i < quantity; i++)
        {
            if (!AddItemInstance(definition, conditionPercent, loadedRounds))
                return false;
            addedAny = true;
        }

        return addedAny;
    }

    public bool TransferAllItemsToContainer(Container container)
    {
        if (!container) return false;

        bool transferredAny = false;
        transferredAny |= TransferEntriesToContainer(weapons, container);
        transferredAny |= TransferEntriesToContainer(apparel, container);
        transferredAny |= TransferEntriesToContainer(aid, container);
        transferredAny |= TransferEntriesToContainer(misc, container);
        transferredAny |= TransferEntriesToContainer(ammo, container);

        if (transferredAny)
            NotifyInventoryChanged();

        return transferredAny;
    }

    private void NotifyInventoryChanged()
    {
        dirty = true;
        RefreshDerivedData();
        OnInventoryChanged?.Invoke();
    }

    private void RefreshDerivedData()
    {
        if (!dirty) return;

        HashSet<string> usedIds = new HashSet<string>();
        NormalizeIdsAndValues(weapons, usedIds);
        NormalizeIdsAndValues(apparel, usedIds);
        NormalizeIdsAndValues(aid, usedIds);
        NormalizeIdsAndValues(misc, usedIds);
        NormalizeIdsAndValues(ammo, usedIds);

        weight = GetCategoryWeight(weapons)
                 + GetCategoryWeight(apparel)
                 + GetCategoryWeight(aid)
                 + GetCategoryWeight(misc)
                 + GetCategoryWeight(ammo);

        weight = Mathf.Max(0f, weight);
        dirty = false;
    }

    private void Normalize()
    {
        if (weapons == null) weapons = new List<InventoryEntry>();
        if (apparel == null) apparel = new List<InventoryEntry>();
        if (aid == null) aid = new List<InventoryEntry>();
        if (misc == null) misc = new List<InventoryEntry>();
        if (ammo == null) ammo = new List<InventoryEntry>();

        NormalizeCategory(weapons);
        NormalizeCategory(apparel);
        NormalizeCategory(aid);
        NormalizeCategory(misc);
        NormalizeCategory(ammo);
        dirty = true;
    }

    private static void NormalizeCategory(List<InventoryEntry> entries)
    {
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            InventoryEntry entry = entries[i];
            if (entry == null)
            {
                entries.RemoveAt(i);
                continue;
            }

            entry.Normalize();
        }
    }

    private static void NormalizeIdsAndValues(List<InventoryEntry> entries, HashSet<string> usedIds)
    {
        if (entries == null) return;

        for (int i = 0; i < entries.Count; i++)
        {
            InventoryEntry entry = entries[i];
            if (entry == null) continue;
            entry.EnsureUniqueInstanceIds(usedIds);
            entry.RecalculateInstanceValues();
        }
    }

    private float GetCategoryWeight(List<InventoryEntry> entries)
    {
        if (entries == null) return 0f;

        float total = 0f;
        for (int i = 0; i < entries.Count; i++)
        {
            InventoryEntry entry = entries[i];
            if (entry == null) continue;
            total += GetDefinitionWeight(entry.GetItemDefinition()) * entry.GetQuantity();
        }

        return total;
    }

    private InventoryEntry FindStackEntry(List<InventoryEntry> entries, ScriptableObject item, int maxStackSize)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            InventoryEntry entry = entries[i];
            if (entry == null || entry.GetItemDefinition() != item) continue;
            if (maxStackSize <= 0 || entry.GetQuantity() < maxStackSize) return entry;
        }

        return null;
    }

    private bool TryFindWeaponInstanceById(string instanceId, out InventoryEntry entry, out int instanceIndex)
    {
        entry = null;
        instanceIndex = -1;
        if (string.IsNullOrWhiteSpace(instanceId) || weapons == null) return false;

        for (int entryIndex = 0; entryIndex < weapons.Count; entryIndex++)
        {
            InventoryEntry candidateEntry = weapons[entryIndex];
            if (candidateEntry == null) continue;

            IReadOnlyList<ItemInstanceData> instances = candidateEntry.GetItemInstances();
            for (int i = 0; i < instances.Count; i++)
            {
                ItemInstanceData instance = instances[i];
                if (instance == null) continue;
                if (!string.Equals(instance.GetInstanceId(), instanceId, StringComparison.Ordinal)) continue;

                entry = candidateEntry;
                instanceIndex = i;
                return true;
            }
        }

        return false;
    }

    private bool TransferEntriesToContainer(List<InventoryEntry> entries, Container container)
    {
        if (entries == null || !container)
            return false;

        bool transferredAny = false;
        for (int entryIndex = entries.Count - 1; entryIndex >= 0; entryIndex--)
        {
            InventoryEntry entry = entries[entryIndex];
            if (entry == null)
            {
                entries.RemoveAt(entryIndex);
                continue;
            }

            ScriptableObject itemDefinition = entry.GetItemDefinition();
            if (!itemDefinition)
                continue;

            if (CanItemStack(itemDefinition))
            {
                int quantity = entry.GetQuantity();
                if (quantity > 0 && container.AddItem(itemDefinition, quantity))
                {
                    entries.RemoveAt(entryIndex);
                    transferredAny = true;
                }

                continue;
            }

            IReadOnlyList<ItemInstanceData> itemInstances = entry.GetItemInstances();
            for (int instanceIndex = itemInstances.Count - 1; instanceIndex >= 0; instanceIndex--)
            {
                ItemInstanceData instance = itemInstances[instanceIndex];
                if (instance == null)
                    continue;

                if (!container.AddItemInstance(
                        itemDefinition,
                        instance.GetConditionPercent(),
                        instance.GetLoadedMagazineRounds()))
                {
                    continue;
                }

                entry.RemoveQuantity(1);
                transferredAny = true;
            }

            if (entry.IsEmpty())
                entries.RemoveAt(entryIndex);
        }

        return transferredAny;
    }

    private List<InventoryEntry> GetListForCategory(InventoryCategory category)
    {
        switch (category)
        {
            case InventoryCategory.Weapons: return weapons;
            case InventoryCategory.Apparel: return apparel;
            case InventoryCategory.Aid: return aid;
            case InventoryCategory.Misc: return misc;
            case InventoryCategory.Ammo: return ammo;
            default: return null;
        }
    }

    private static InventoryCategory GetInventoryCategoryOrDefault(ScriptableObject definition)
    {
        if (definition is WeaponDefinition) return InventoryCategory.Weapons;
        if (definition is ApparelDefinition) return InventoryCategory.Apparel;
        if (definition is AidDefinition) return InventoryCategory.Aid;
        if (definition is AmmoDefinition) return InventoryCategory.Ammo;
        return InventoryCategory.Misc;
    }

    private static bool IsSupportedItemDefinition(ScriptableObject definition)
    {
        return definition is WeaponDefinition
               || definition is ApparelDefinition
               || definition is AidDefinition
               || definition is MiscDefinition
               || definition is AmmoDefinition
               || definition is AmmoItemDefinition;
    }

    private static bool CanItemStack(ScriptableObject definition)
    {
        return definition is AidDefinition
               || definition is MiscDefinition
               || definition is AmmoDefinition
               || definition is AmmoItemDefinition;
    }

    private static int GetMaxStackSizeOrDefault(ScriptableObject definition)
    {
        if (definition is AidDefinition aidDefinition) return aidDefinition.GetMaxStackSize();
        if (definition is MiscDefinition miscDefinition) return miscDefinition.GetMaxStackSize();
        if (definition is AmmoDefinition || definition is AmmoItemDefinition) return 0;
        return 1;
    }

    private static bool DefinitionSupportsCondition(ScriptableObject definition)
    {
        return definition is WeaponDefinition || definition is ApparelDefinition;
    }

    private static bool DefinitionSupportsLoadedMagazineRounds(ScriptableObject definition)
    {
        return definition is WeaponDefinition weaponDefinition && weaponDefinition.GetMagazineSize() > 0;
    }

    private static float GetBaseValue(ScriptableObject definition)
    {
        if (definition is WeaponDefinition weaponDefinition) return weaponDefinition.GetBaseValue();
        if (definition is ApparelDefinition apparelDefinition) return apparelDefinition.GetBaseValue();
        if (definition is AidDefinition aidDefinition) return aidDefinition.GetValue();
        if (definition is MiscDefinition miscDefinition) return miscDefinition.GetValue();
        if (definition is AmmoDefinition ammoDefinition) return ammoDefinition.GetValue();
        return 0f;
    }

    private static float GetDefinitionWeight(ScriptableObject definition)
    {
        if (definition is WeaponDefinition weaponDefinition) return weaponDefinition.GetWeight();
        if (definition is ApparelDefinition apparelDefinition) return apparelDefinition.GetWeight();
        if (definition is AidDefinition aidDefinition) return aidDefinition.GetWeight();
        if (definition is MiscDefinition miscDefinition) return miscDefinition.GetWeight();
        if (definition is AmmoDefinition ammoDefinition) return ammoDefinition.GetWeight();
        return 0f;
    }
}
