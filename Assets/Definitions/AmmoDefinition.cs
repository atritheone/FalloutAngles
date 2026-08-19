// imports
using UnityEngine;



// class
[CreateAssetMenu(menuName = "Fallout Angles/Items/Ammo Definition")]
public class AmmoDefinition : ScriptableObject
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
    [SerializeField] private int maxStackSize = 10000;

    // Value in caps.
    [SerializeField] private float value;

    // Optional calibre label like ".308" or "5.56".
    [SerializeField] private string calibreLabel;

    // Projectile prefab spawned when this ammo type is fired.
    [SerializeField] private GameObject roundPrefab;

    [Header("Impact Behaviour")]
    // If true, this projectile keeps normal physics collision response instead of forcing a stop at first impact.
    [SerializeField] private bool allowRicochet;

    [Header("Ballistics Overrides")]
    // Enables per-ammo ballistic tuning values for projectile flight/impact.
    [SerializeField] private bool overrideProjectileBallistics;

    // Optional projectile mass override in kilograms (0 = keep prefab rigidbody mass).
    [Min(0f)] [SerializeField] private float projectileMassKilograms = 0f;

    // Optional projectile diameter override in millimeters (0 = estimate from collider).
    [Min(0f)] [SerializeField] private float projectileDiameterMillimeters = 0f;

    // Aerodynamic drag coefficient used by quadratic drag simulation.
    [Min(0f)] [SerializeField] private float dragCoefficient = 0.295f;

    // Gravity scale applied during projectile flight.
    [Min(0f)] [SerializeField] private float gravityScale = 1f;

    // Fraction of projectile momentum transferred to hit rigidbodies on impact.
    [Range(0f, 1f)] [SerializeField] private float impactMomentumTransfer = 0.35f;

    // Extra multiplier for impact impulse after momentum transfer.
    [Min(0f)] [SerializeField] private float impactImpulseScale = 1f;

    // If true, projectile uses quadratic aerodynamic drag.
    [SerializeField] private bool useQuadraticDrag = true;



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


    public string GetCalibreLabel()
    {
        // Return calibre label.
        return calibreLabel;
    }


    public GameObject GetRoundPrefab()
    {
        // Return projectile prefab for this ammo type.
        return roundPrefab;
    }

    public bool AllowsRicochet()
    {
        // Return whether this projectile should keep its authored ricochet behaviour.
        return allowRicochet;
    }


    public bool HasProjectileBallisticsOverrides()
    {
        // Return whether this ammo should override projectile ballistics at runtime.
        return overrideProjectileBallistics;
    }


    public float GetProjectileMassKilograms()
    {
        // Return optional projectile mass override in kilograms.
        return projectileMassKilograms;
    }


    public float GetProjectileDiameterMillimeters()
    {
        // Return optional projectile diameter override in millimeters.
        return projectileDiameterMillimeters;
    }


    public float GetDragCoefficient()
    {
        // Return aerodynamic drag coefficient for this ammo.
        return dragCoefficient;
    }


    public float GetGravityScale()
    {
        // Return projectile gravity scale.
        return gravityScale;
    }


    public float GetImpactMomentumTransfer()
    {
        // Return fraction of momentum transferred to hit rigidbodies.
        return impactMomentumTransfer;
    }


    public float GetImpactImpulseScale()
    {
        // Return extra multiplier for projectile impact impulse.
        return impactImpulseScale;
    }


    public bool UseQuadraticDrag()
    {
        // Return whether projectile should use quadratic aerodynamic drag.
        return useQuadraticDrag;
    }


    public PlayerInventory.InventoryCategory GetInventoryCategory()
    {
        // Ammo always belongs to Ammo category.
        return PlayerInventory.InventoryCategory.Ammo;
    }
}
