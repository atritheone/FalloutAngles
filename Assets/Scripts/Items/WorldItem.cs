// imports
using UnityEngine;



// class
public class WorldItem : MonoBehaviour
{
    
    // variables
    // The item definition that describes what this world pickup is.
    [SerializeField] private ScriptableObject itemDefinition;

    // How many units of this item exist in the world stack.
    [SerializeField] private int quantity = 1;

    // Item condition from 0 (broken) to 100 (perfect).
    [Range(0, 100)] [SerializeField] private int condition = 100;

    // If true, the GameObject will be destroyed after a successful pickup.
    [SerializeField] private bool destroyOnPickup = true;

    // If true (and destroyOnPickup is false), the GameObject will be disabled after a successful pickup.
    [SerializeField] private bool disableOnPickup = false;

    // Optional pickup sound played at this item’s position.
    [SerializeField] private AudioClip pickupSfx;

    // Optional volume for the pickup sound.
    [Range(0f, 1f)] [SerializeField] private float pickupSfxVolume = 0.8f;

    // If true, walking into the trigger will attempt to pick this up automatically.
    [SerializeField] private bool autoPickupOnTrigger = false;

    // Optional prompt override (leave empty to use default “Pick Up”).
    [SerializeField] private string promptVerbOverride = "";



    // methods
    public ScriptableObject GetItemDefinition()
    {
        // Return the definition.
        return itemDefinition;
    }


    public int GetQuantity()
    {
        // Return effective quantity (AmmoItem rounds override when present).
        return ResolvePickupQuantity();
    }


    public float GetConditionPercent()
    {
        // Return condition clamped to 0..100.
        return Mathf.Clamp(condition, 0, 100);
    }


    public void SetItemDefinition(ScriptableObject newItemDefinition)
    {
        // Stop if definition is missing.
        if (!newItemDefinition) return;

        // Store the provided definition.
        itemDefinition = newItemDefinition;

    }


    public void SetConditionPercent(float newConditionPercent)
    {
        // Clamp condition to 0..100 and store as int.
        condition = Mathf.RoundToInt(Mathf.Clamp(newConditionPercent, 0.0f, 100.0f));
    }


    public void SetQuantity(int newQuantity)
    {
        // Clamp quantity to never be below 1.
        quantity = Mathf.Max(1, newQuantity);
    }


    public string GetItemId()
    {
        // Return item id from the definition.
        return GetItemIdFromDefinition(itemDefinition, "");
    }


    public string GetDisplayName()
    {
        // Resolve base display name from the definition.
        string baseDisplayName = GetDisplayNameFromDefinition(itemDefinition, "Unknown");

        // Ammo pickups include round count in the label.
        if (IsAmmoWorldDefinition(itemDefinition))
        {
            int ammoRounds = ResolvePickupQuantity();
            return baseDisplayName + " (" + ammoRounds + ")";
        }

        return baseDisplayName;
    }


    public string GetDescription()
    {
        // Return description from the definition.
        return GetDescriptionFromDefinition(itemDefinition, "");
    }


    public Sprite GetIcon()
    {
        // Return icon from the definition.
        return GetIconFromDefinition(itemDefinition);
    }


    public float GetWeight()
    {
        // Return weight from the definition.
        return GetWeightFromDefinition(itemDefinition, 0f);
    }


    public bool IsStackable()
    {
        // Return stackable flag from the definition.
        return GetStackableFromDefinition(itemDefinition, true);
    }


    public int GetMaxStackSize()
    {
        // Return max stack size from the definition.
        return GetMaxStackSizeFromDefinition(itemDefinition, 0);
    }


    public PlayerInventory.InventoryCategory GetInventoryCategory()
    {
        // Return category from the definition.
        return GetInventoryCategoryFromDefinition(itemDefinition, PlayerInventory.InventoryCategory.Misc);
    }


    public string GetInteractionText()
    {
        // Choose the verb for the prompt.
        string verb = string.IsNullOrWhiteSpace(promptVerbOverride) ? "Pick Up" : promptVerbOverride;
        ScriptableObject definition = itemDefinition;

        // Stop if no definition.
        if (!definition)
            return verb + " (Unknown)";

        string displayName = GetDisplayNameFromDefinition(definition, "Unknown");

        int itemQuantity = ResolvePickupQuantity();

        // Format with quantity if greater than 1.
        if (itemQuantity > 1)
            return verb + " " + displayName + " x" + itemQuantity;

        // Format without quantity for single items.
        return verb + " " + displayName;
    }


    public bool TryPickup(GameObject picker)
    {
        ScriptableObject definition = itemDefinition;
        int itemQuantity = ResolvePickupQuantity();

        // Stop if picker is missing.
        if (!picker)
            return false;

        // Stop if no definition.
        if (!definition)
            return false;

        // Stop if quantity is invalid.
        if (itemQuantity <= 0)
            return false;

        // Try to find a world item receiver on the picker.
        IWorldItemReceiver receiver = picker.GetComponentInParent<IWorldItemReceiver>(true);

        // Stop if no receiver found.
        if (receiver == null)
            return false;

        // Attempt to give the item to the receiver.
        bool accepted = receiver.TryReceiveWorldItem(definition, itemQuantity, this);

        // If accepted, finalize pickup.
        if (accepted)
        {
            FinalizePickup();
            return true;
        }

        // Pickup rejected (inventory full etc).
        return false;
    }

    
    private void OnTriggerEnter(Collider other)
    {
        // Stop if auto pickup is disabled.
        if (!autoPickupOnTrigger)
            return;

        // Stop if collider is missing.
        if (!other)
            return;

        // Attempt pickup using the collider’s GameObject.
        TryPickup(other.gameObject);
    }


    private void OnValidate()
    {
        // Keep serialized values in valid runtime ranges.
        quantity = Mathf.Max(1, quantity);
        condition = Mathf.Clamp(condition, 0, 100);
    }


    private void FinalizePickup()
    {
        Transform selfTransform = transform;
        GameObject self = gameObject;

        // Play pickup sound if set.
        if (pickupSfx)
            AudioSource.PlayClipAtPoint(pickupSfx, selfTransform.position, pickupSfxVolume);

        // Destroy if configured.
        if (destroyOnPickup)
        {
            // Destroy this GameObject.
            Destroy(self);

            // Stop further execution.
            return;
        }

        // Disable if configured.
        if (disableOnPickup)
        {
            // Disable this GameObject.
            self.SetActive(false);

            // Stop further execution.
            return;
        }
    }


    private string GetItemIdFromDefinition(ScriptableObject definition, string fallback)
    {
        if (!definition) return fallback;

        if (definition is WeaponDefinition weaponDefinition) return weaponDefinition.GetItemId();
        if (definition is ApparelDefinition apparelDefinition) return apparelDefinition.GetItemId();
        if (definition is AidDefinition aidDefinition) return aidDefinition.GetItemId();
        if (definition is MiscDefinition miscDefinition) return miscDefinition.GetItemId();
        if (definition is AmmoDefinition ammoDefinition) return ammoDefinition.GetItemId();
        if (definition is AmmoItemDefinition ammoItemDefinition) return ammoItemDefinition.GetItemId();
        return fallback;
    }


    private string GetDisplayNameFromDefinition(ScriptableObject definition, string fallback)
    {
        if (!definition) return fallback;

        if (definition is WeaponDefinition weaponDefinition) return weaponDefinition.GetDisplayName();
        if (definition is ApparelDefinition apparelDefinition) return apparelDefinition.GetDisplayName();
        if (definition is AidDefinition aidDefinition) return aidDefinition.GetDisplayName();
        if (definition is MiscDefinition miscDefinition) return miscDefinition.GetDisplayName();
        if (definition is AmmoDefinition ammoDefinition) return ammoDefinition.GetDisplayName();
        if (definition is AmmoItemDefinition ammoItemDefinition) return ammoItemDefinition.GetDisplayName();
        return fallback;
    }


    private string GetDescriptionFromDefinition(ScriptableObject definition, string fallback)
    {
        if (!definition) return fallback;

        if (definition is WeaponDefinition weaponDefinition) return weaponDefinition.GetDescription();
        if (definition is ApparelDefinition apparelDefinition) return apparelDefinition.GetDescription();
        if (definition is AidDefinition aidDefinition) return aidDefinition.GetDescription();
        if (definition is MiscDefinition miscDefinition) return miscDefinition.GetDescription();
        if (definition is AmmoDefinition ammoDefinition) return ammoDefinition.GetDescription();
        if (definition is AmmoItemDefinition ammoItemDefinition) return ammoItemDefinition.GetDescription();
        return fallback;
    }


    private Sprite GetIconFromDefinition(ScriptableObject definition)
    {
        if (!definition) return null;

        if (definition is WeaponDefinition weaponDefinition) return weaponDefinition.GetIcon();
        if (definition is ApparelDefinition apparelDefinition) return apparelDefinition.GetIcon();
        if (definition is AidDefinition aidDefinition) return aidDefinition.GetIcon();
        if (definition is MiscDefinition miscDefinition) return miscDefinition.GetIcon();
        if (definition is AmmoDefinition ammoDefinition) return ammoDefinition.GetIcon();
        if (definition is AmmoItemDefinition ammoItemDefinition) return ammoItemDefinition.GetIcon();
        return null;
    }


    private float GetWeightFromDefinition(ScriptableObject definition, float fallback)
    {
        if (!definition) return fallback;

        if (definition is WeaponDefinition weaponDefinition) return weaponDefinition.GetWeight();
        if (definition is ApparelDefinition apparelDefinition) return apparelDefinition.GetWeight();
        if (definition is AidDefinition aidDefinition) return aidDefinition.GetWeight();
        if (definition is MiscDefinition miscDefinition) return miscDefinition.GetWeight();
        if (definition is AmmoDefinition ammoDefinition) return ammoDefinition.GetWeight();
        if (definition is AmmoItemDefinition ammoItemDefinition) return ammoItemDefinition.GetWeight();
        return fallback;
    }


    private bool GetStackableFromDefinition(ScriptableObject definition, bool fallback)
    {
        if (!definition) return fallback;

        if (definition is WeaponDefinition weaponDefinition) return weaponDefinition.IsStackable();
        if (definition is ApparelDefinition apparelDefinition) return apparelDefinition.IsStackable();
        if (definition is AidDefinition aidDefinition) return aidDefinition.IsStackable();
        if (definition is MiscDefinition miscDefinition) return miscDefinition.IsStackable();
        if (definition is AmmoDefinition ammoDefinition) return ammoDefinition.IsStackable();
        if (definition is AmmoItemDefinition ammoItemDefinition) return ammoItemDefinition.IsStackable();
        return fallback;
    }


    private int GetMaxStackSizeFromDefinition(ScriptableObject definition, int fallback)
    {
        if (!definition) return fallback;

        if (definition is WeaponDefinition weaponDefinition) return weaponDefinition.GetMaxStackSize();
        if (definition is ApparelDefinition apparelDefinition) return apparelDefinition.GetMaxStackSize();
        if (definition is AidDefinition aidDefinition) return aidDefinition.GetMaxStackSize();
        if (definition is MiscDefinition miscDefinition) return miscDefinition.GetMaxStackSize();
        if (definition is AmmoDefinition ammoDefinition) return ammoDefinition.GetMaxStackSize();
        if (definition is AmmoItemDefinition ammoItemDefinition) return ammoItemDefinition.GetMaxStackSize();
        return fallback;
    }


    private PlayerInventory.InventoryCategory GetInventoryCategoryFromDefinition(ScriptableObject definition, PlayerInventory.InventoryCategory fallback)
    {
        if (!definition) return fallback;

        if (definition is WeaponDefinition weaponDefinition) return weaponDefinition.GetInventoryCategory();
        if (definition is ApparelDefinition apparelDefinition) return apparelDefinition.GetInventoryCategory();
        if (definition is AidDefinition aidDefinition) return aidDefinition.GetInventoryCategory();
        if (definition is MiscDefinition miscDefinition) return miscDefinition.GetInventoryCategory();
        if (definition is AmmoDefinition ammoDefinition) return ammoDefinition.GetInventoryCategory();
        if (definition is AmmoItemDefinition ammoItemDefinition) return ammoItemDefinition.GetInventoryCategory();
        return fallback;
    }


    private int ResolvePickupQuantity()
    {
        int resolvedQuantity = Mathf.Max(1, quantity);

        if (IsAmmoWorldDefinition(itemDefinition))
        {
            AmmoItem ammoItem = GetComponent<AmmoItem>();
            if (ammoItem)
                resolvedQuantity = Mathf.Max(1, ammoItem.GetRounds());
            else if (itemDefinition is AmmoItemDefinition ammoItemDefinition)
                resolvedQuantity = Mathf.Max(1, ammoItemDefinition.GetDefaultRounds());
        }

        return resolvedQuantity;
    }


    private static bool IsAmmoWorldDefinition(ScriptableObject definition)
    {
        return definition is AmmoDefinition || definition is AmmoItemDefinition;
    }


    // nested types
    public interface IWorldItemReceiver
    {
        // Attempt to receive a world item and return true if accepted.
        bool TryReceiveWorldItem(ScriptableObject definition, int quantity, WorldItem worldItem);
    }


    public struct WorldItemPickupPayload
    {
        // The definition being offered.
        public ScriptableObject Definition;

        // The quantity being offered.
        public int Quantity;

        // The world item instance being picked up.
        public WorldItem Source;

        // Loaded rounds currently in this world weapon's magazine.
        public int LoadedMagazineRounds;

        // Number of rounds represented by this pickup when it is an ammo item.
        public int AmmoRounds;

        public WorldItemPickupPayload(ScriptableObject definition, int quantity, WorldItem source)
        {
            // Store definition.
            Definition = definition;

            // Store quantity.
            Quantity = quantity;

            // Store source.
            Source = source;

            // Snapshot loaded rounds from source weapon component (if available).
            WeaponItem sourceWeaponItem = source != null ? source.GetComponent<WeaponItem>() : null;
            LoadedMagazineRounds = sourceWeaponItem != null ? sourceWeaponItem.GetLoadedMagazineRounds() : 0;

            // Snapshot ammo rounds from source ammo component (if available).
            AmmoItem sourceAmmoItem = source != null ? source.GetComponent<AmmoItem>() : null;
            AmmoRounds = sourceAmmoItem != null ? sourceAmmoItem.GetRounds() : Mathf.Max(1, quantity);
        }
    }
}


