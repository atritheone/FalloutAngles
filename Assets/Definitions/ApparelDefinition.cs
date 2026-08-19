// imports
using UnityEngine;



// class
[CreateAssetMenu(menuName = "Fallout Angles/Items/Apparel Definition")]
public class ApparelDefinition : ScriptableObject
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
    [SerializeField] private bool isStackable = false;

    // Max stack size (0 or less means unlimited).
    [SerializeField] private int maxStackSize = 0;

    // Sell/buy value in caps.
    [SerializeField] private float baseValue;

    // Damage resistance provided by this apparel.
    [SerializeField] private int damageResistance;

    // Radiation resistance provided by this apparel.
    [SerializeField] private int radiationResistance;



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


    public float GetBaseValue()
    {
        // Return value.
        return baseValue;
    }


    public int GetDamageResistance()
    {
        // Return damage resistance.
        return damageResistance;
    }


    public int GetRadiationResistance()
    {
        // Return radiation resistance.
        return radiationResistance;
    }


    public PlayerInventory.InventoryCategory GetInventoryCategory()
    {
        // Apparel always belongs to Apparel category.
        return PlayerInventory.InventoryCategory.Apparel;
    }
}
