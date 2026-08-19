using UnityEngine;

[CreateAssetMenu(menuName = "Fallout Angles/Items/Ammo Item Definition")]
public class AmmoItemDefinition : ScriptableObject
{
    // Unique id for save systems and comparisons.
    [SerializeField] private string itemId;

    // Display name for UI.
    [SerializeField] private string displayName;

    // Description for UI.
    [TextArea] [SerializeField] private string description;

    // Icon for UI.
    [SerializeField] private Sprite icon;

    // Weight for encumbrance (optional).
    [SerializeField] private float weight;

    // Whether this item stacks.
    [SerializeField] private bool isStackable = true;

    // Max stack size (0 or less means unlimited).
    [SerializeField] private int maxStackSize = 10000;

    // Value in caps.
    [SerializeField] private float value;

    // The ammo definition this world ammo item contains.
    [SerializeField] private AmmoDefinition ammoDefinition;

    // Default rounds in this item when no AmmoItem override is set.
    [Min(1)] [SerializeField] private int defaultRounds = 1;

    // Optional world pickup prefab used for this ammo item container.
    [SerializeField] private GameObject worldPrefab;

    public string GetItemId()
    {
        if (string.IsNullOrWhiteSpace(itemId) == false)
            return itemId;

        return ammoDefinition ? ammoDefinition.GetItemId() : string.Empty;
    }

    public string GetDisplayName()
    {
        if (string.IsNullOrWhiteSpace(displayName) == false)
            return displayName;

        return ammoDefinition ? ammoDefinition.GetDisplayName() : string.Empty;
    }

    public string GetDescription()
    {
        if (string.IsNullOrWhiteSpace(description) == false)
            return description;

        return ammoDefinition ? ammoDefinition.GetDescription() : string.Empty;
    }

    public Sprite GetIcon()
    {
        if (icon)
            return icon;

        return ammoDefinition ? ammoDefinition.GetIcon() : null;
    }

    public float GetWeight()
    {
        if (weight > 0.0f)
            return weight;

        return ammoDefinition ? Mathf.Max(0.0f, ammoDefinition.GetWeight()) : 0.0f;
    }

    public bool IsStackable()
    {
        return isStackable;
    }

    public int GetMaxStackSize()
    {
        if (maxStackSize > 0)
            return maxStackSize;

        return ammoDefinition ? ammoDefinition.GetMaxStackSize() : maxStackSize;
    }

    public float GetValue()
    {
        if (value > 0.0f)
            return value;

        return ammoDefinition ? Mathf.Max(0.0f, ammoDefinition.GetValue()) : 0.0f;
    }

    public AmmoDefinition GetAmmoDefinition()
    {
        return ammoDefinition;
    }

    public int GetDefaultRounds()
    {
        return Mathf.Max(1, defaultRounds);
    }

    public GameObject GetWorldPrefab()
    {
        return worldPrefab;
    }

    public PlayerInventory.InventoryCategory GetInventoryCategory()
    {
        return PlayerInventory.InventoryCategory.Ammo;
    }
}
