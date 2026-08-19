// imports
using System;
using System.Collections.Generic;
using UnityEngine;


public enum AidConsumableType
{
    Food,
    Drink,
    Chem,
    Miscellaneous
}


public enum AidAddictionType
{
    None,
    Alcohol,
    AntNectar,
    Buffout,
    Jet,
    MedX,
    Mentats,
    NukaColaQuantum,
    Psycho,
    Ultrajet,
    Custom
}


public enum AidEffectTarget
{
    Health,
    Radiation,
    ActionPoints,
    MaxActionPoints,
    Strength,
    Perception,
    Endurance,
    Charisma,
    Intelligence,
    Agility,
    Luck,
    SneakSkill,
    DamagePercent,
    DamageResistance,
    RadiationResistance,
    FireResistance,
    BottleCaps,
    EquippedWeaponCondition,
    StealthField,
    RandomEffectBundle
}


public enum AidEffectOperation
{
    AddFlat,
    AddPercent
}


[Serializable]
public class AidEffectDefinition
{

    // variables
    // What this effect changes.
    [SerializeField] private AidEffectTarget target = AidEffectTarget.Health;

    // How the value is interpreted.
    [SerializeField] private AidEffectOperation operation = AidEffectOperation.AddFlat;

    // Signed amount for this effect.
    [SerializeField] private float magnitude;

    // If true, applies to the target's maximum value instead of current value.
    [SerializeField] private bool modifiesMaximumValue;

    // 0 means instant/permanent (not timed). Positive values are timed effects.
    [Min(0f)] [SerializeField] private float durationSeconds;

    // True when Medicine skill should scale this effect.
    [SerializeField] private bool scalesWithMedicineSkill;

    // True when Repair skill should scale this effect (used by alien epoxy-like effects).
    [SerializeField] private bool scalesWithRepairSkill;

    // Optional effect id used for custom effect tables (e.g. random biogel outcomes).
    [SerializeField] private string effectId;

    // Optional designer note for non-standard behaviors.
    [TextArea] [SerializeField] private string notes;



    // methods
    public AidEffectTarget GetTarget()
    {
        // Return effect target.
        return target;
    }


    public AidEffectOperation GetOperation()
    {
        // Return operation mode.
        return operation;
    }


    public float GetMagnitude()
    {
        // Return magnitude.
        return magnitude;
    }


    public bool ModifiesMaximumValue()
    {
        // Return max value modifier flag.
        return modifiesMaximumValue;
    }


    public float GetDurationSeconds()
    {
        // Return duration seconds.
        return durationSeconds;
    }


    public bool ScalesWithMedicineSkill()
    {
        // Return Medicine scaling flag.
        return scalesWithMedicineSkill;
    }


    public bool ScalesWithRepairSkill()
    {
        // Return Repair scaling flag.
        return scalesWithRepairSkill;
    }


    public string GetEffectId()
    {
        // Return optional effect id.
        return effectId;
    }


    public string GetNotes()
    {
        // Return optional notes.
        return notes;
    }
}


// class
[CreateAssetMenu(menuName = "Fallout Angles/Items/Aid Definition")]
public class AidDefinition : ScriptableObject
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
    [SerializeField] private int maxStackSize = 99;

    // Sell/buy value in caps.
    [SerializeField] private float value;

    // Consumable class for Fallout-like categorization.
    [SerializeField] private AidConsumableType consumableType = AidConsumableType.Food;

    // Full effect list for this consumable.
    [SerializeField] private List<AidEffectDefinition> effects = new List<AidEffectDefinition>();

    // True when consuming this item can cause addiction.
    [SerializeField] private bool canCauseAddiction;

    // Addiction type applied by this consumable.
    [SerializeField] private AidAddictionType addictionType = AidAddictionType.None;

    // Used when addiction type is Custom.
    [SerializeField] private string customAddictionId;

    // Optional chance (0-100) to apply addiction.
    [Range(0f, 100f)] [SerializeField] private float addictionChancePercent;



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


    public AidConsumableType GetConsumableType()
    {
        // Return consumable type.
        return consumableType;
    }


    public List<AidEffectDefinition> GetEffects()
    {
        // Return full effect list.
        return effects;
    }


    public bool CanCauseAddiction()
    {
        // Return addiction flag.
        return canCauseAddiction;
    }


    public AidAddictionType GetAddictionType()
    {
        // Return addiction type.
        return addictionType;
    }


    public string GetCustomAddictionId()
    {
        // Return custom addiction id.
        return customAddictionId;
    }


    public float GetAddictionChancePercent()
    {
        // Return addiction chance.
        return addictionChancePercent;
    }


    public PlayerInventory.InventoryCategory GetInventoryCategory()
    {
        // Aid always belongs to Aid category.
        return PlayerInventory.InventoryCategory.Aid;
    }
}
