// imports
using UnityEngine;



// class
[CreateAssetMenu(menuName = "Fallout Angles/Items/Misc Definition")]
public class MiscDefinition : ScriptableObject
{

    // variables
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

    // Whether the item stacks.
    [SerializeField] private bool isStackable = true;

    // Max stack size (0 or less means unlimited).
    [SerializeField] private int maxStackSize = 1000;

    // Value in caps (optional, if you use trading).
    [SerializeField] private float value;

    // Optional world pickup prefab used when dropping this misc item.
    [SerializeField] private GameObject worldPrefab;



    // methods
    public string GetItemId()
    {
        // Return item id.
        return itemId;
    }


    public string GetDisplayName()
    {
        // Return display name.
        return displayName;
    }


    public string GetDescription()
    {
        // Return description.
        return description;
    }


    public Sprite GetIcon()
    {
        // Return icon.
        return icon;
    }


    public float GetWeight()
    {
        // Return weight.
        return weight;
    }


    public bool IsStackable()
    {
        // Return stackable flag.
        return isStackable;
    }


    public int GetMaxStackSize()
    {
        // Return max stack size.
        return maxStackSize;
    }


    public float GetValue()
    {
        // Return value.
        return value;
    }


    public GameObject GetWorldPrefab()
    {
        // Return world pickup prefab.
        return worldPrefab;
    }


    public PlayerInventory.InventoryCategory GetInventoryCategory()
    {
        // Misc always belongs to Misc category.
        return PlayerInventory.InventoryCategory.Misc;
    }
}
