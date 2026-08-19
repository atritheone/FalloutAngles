// imports
using UnityEngine;



// class
[CreateAssetMenu(menuName = "Fallout Angles/Items/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
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

    // Weapon damage per hit/shot.
    [SerializeField] private int damage;

    // Sell/buy value in caps.
    [SerializeField] private float baseValue;

    // Shots per second for automatic weapons.
    [SerializeField] private float fireRate;

    // If true, holding fire will continuously shoot using fireRate.
    [SerializeField] private bool automatic;

    // Random spread cone in degrees for automatic fire.
    [Min(0f)] [SerializeField, HideInInspector] private float spread;

    // Weapon effective range.
    [SerializeField] private float range;

    // Projectile launch speed used when firing this weapon.
    [Min(0f)] [SerializeField] private float muzzleVelocity = 250f;

    // Ammo type consumed (optional for melee).
    [SerializeField] private AmmoDefinition ammoType;

    // Magazine size (0 means no magazine system).
    [SerializeField] private int magazineSize;

    // Optional world pickup prefab used when dropping this weapon.
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


    public int GetDamage()
    {
        // Return damage.
        return damage;
    }


    public float GetBaseValue()
    {
        // Return value.
        return baseValue;
    }


    public float GetFireRate()
    {
        // Return fire rate.
        return fireRate;
    }

    public bool IsAutomatic()
    {
        // Return automatic fire mode flag.
        return automatic;
    }


    public float GetSpread()
    {
        // Return automatic fire spread in degrees.
        return Mathf.Max(0f, spread);
    }


    public float GetRange()
    {
        // Return range.
        return range;
    }


    public float GetMuzzleVelocity()
    {
        // Return muzzle velocity.
        return muzzleVelocity;
    }


    public AmmoDefinition GetAmmoType()
    {
        // Return ammo type.
        return ammoType;
    }


    public int GetMagazineSize()
    {
        // Return magazine size.
        return magazineSize;
    }


    public GameObject GetWorldPrefab()
    {
        // Return world pickup prefab.
        return worldPrefab;
    }


    public PlayerInventory.InventoryCategory GetInventoryCategory()
    {
        // Weapons always belong to Weapons category.
        return PlayerInventory.InventoryCategory.Weapons;
    }
}
