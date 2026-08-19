﻿﻿﻿﻿﻿// imports
using System;
using System.Collections.Generic;
using UnityEngine;



// class
public class PlayerWeaponController : WeaponController
{
    
    // variables
    // The weapon categories (these match your headings).
    public enum WeaponCategory
    {
        
        Unarmed,
        
        Knife,
        
        TwoHanded,
        
        Bow,
        
        Pistol,
        
        SubmachineGun,
        
        Rifle,
        
        Shotgun,
        
        Special,
        
        Explosive
    }
    

    // A single weapon entry in the selectable list.
    [Serializable]
    public class WeaponEntry
    {
        
        // The category this weapon belongs to.
        public WeaponCategory Category;
        
        // The weapon name (this is the second name after the hyphen).
        public string WeaponName;
    }

    [Serializable]
    public class WeaponAmmoEntry
    {
        
        // The category this ammo record belongs to.
        public WeaponCategory Category;
        
        // The weapon name this ammo record belongs to.
        public string WeaponName;
        
        // The rounds currently loaded in the active weapon.
        [Min(0)] public int CurrentAmmo;
        
        // The rounds currently stored in reserve for this weapon.
        [Min(0)] public int ReserveAmmo;
    }

    [Serializable]
    public class KnifeAnimatorSettings
    {
        [SerializeField] public string EquipStateName = "Knife Equip";
        [SerializeField] public string UnequipStateName = "Knife Unequip";
        [SerializeField, Range(0f, 1f)] public float EquipEnableTime = 0f;
        [SerializeField, Range(0f, 1f)] public float UnequipDisableTime = 0f;
        [SerializeField, Range(0f, 1f)] public float EquipHolsterDisableTime = 0.35f;
        [SerializeField, Range(0f, 1f)] public float EquipHolsterReEnableTime = 0f;
    }

    [Serializable]
    public class TwoHandedAnimatorSettings
    {
        [SerializeField] public string EquipStateName = "Two Handed Equip";
        [SerializeField] public string UnequipStateName = "Two Handed Unequip";
        [SerializeField, Range(0f, 1f)] public float EquipEnableTime = 0f;
        [SerializeField, Range(0f, 1f)] public float UnequipDisableTime = 0f;
        [SerializeField, Range(0f, 1f)] public float EquipHolsterDisableTime = 0.35f;
        [SerializeField, Range(0f, 1f)] public float EquipHolsterReEnableTime = 0f;
    }

    [Serializable]
    public class PistolAnimatorSettings
    {
        [SerializeField] public string EquipStateName = "Pistol Equip";
        [SerializeField] public string UnequipStateName = "Pistol Unequip";
        [SerializeField, Range(0f, 1f)] public float EquipEnableTime = 0f;
        [SerializeField, Range(0f, 1f)] public float UnequipDisableTime = 0f;
        [SerializeField, Range(0f, 1f)] public float EquipHolsterDisableTime = 0.35f;
        [SerializeField, Range(0f, 1f)] public float EquipHolsterReEnableTime = 0f;
    }

    [Serializable]
    public class LongarmAnimatorSettings
    {
        [SerializeField] public string EquipStateName = "Longarm Equip";
        [SerializeField] public string UnequipStateName = "Longarm Unequip";
        [SerializeField, Range(0f, 1f)] public float EquipEnableTime = 0f;
        [SerializeField, Range(0f, 1f)] public float UnequipDisableTime = 0f;
        [SerializeField, Range(0f, 1f)] public float EquipHolsterDisableTime = 0.35f;
        [SerializeField, Range(0f, 1f)] public float EquipHolsterReEnableTime = 0f;
    }

    [Serializable]
    public class WeaponPairReferences
    {
        [SerializeField] public GameObject Equipped;
        [SerializeField] public GameObject Holstered;
    }

    [Serializable]
    public class UnarmedWeaponReferences
    {
        [SerializeField] public GameObject KnuckleIdleLeft;
        [SerializeField] public GameObject KnuckleIdleRight;
        [SerializeField] public GameObject KnuckleReadyLeft;
        [SerializeField] public GameObject KnuckleReadyRight;
    }

    [Serializable]
    public class KnifeWeaponReferences
    {
        [SerializeField] public WeaponPairReferences CombatKnife = new WeaponPairReferences();
        [SerializeField] public WeaponPairReferences KitchenKnife = new WeaponPairReferences();
        [SerializeField] public WeaponPairReferences StraightRazor = new WeaponPairReferences();
    }

    [Serializable]
    public class TwoHandedWeaponReferences
    {
        [SerializeField] public WeaponPairReferences CricketBat = new WeaponPairReferences();
        [SerializeField] public WeaponPairReferences LeadPipe = new WeaponPairReferences();
        [SerializeField] public WeaponPairReferences Cane = new WeaponPairReferences();
        [SerializeField] public WeaponPairReferences FellingAxe = new WeaponPairReferences();
        [SerializeField] public WeaponPairReferences Shovel = new WeaponPairReferences();
    }

    [Serializable]
    public class PistolWeaponReferences
    {
        [SerializeField] public WeaponPairReferences SelfLoadingPistol = new WeaponPairReferences();
        [SerializeField] public WeaponPairReferences Revolver = new WeaponPairReferences();
        [SerializeField] public WeaponPairReferences LaserPistol = new WeaponPairReferences();
    }

    [Serializable]
    public class RifleWeaponReferences
    {
        [SerializeField] public WeaponPairReferences SubmachineGun = new WeaponPairReferences();
        [SerializeField] public WeaponPairReferences HuntingRifle = new WeaponPairReferences();
        [SerializeField] public WeaponPairReferences BattleRifle = new WeaponPairReferences();
        [SerializeField] public WeaponPairReferences LightMachineGun = new WeaponPairReferences();
        [SerializeField] public WeaponPairReferences LaserRifle = new WeaponPairReferences();
        [SerializeField] public WeaponPairReferences SniperRifle = new WeaponPairReferences();
        [SerializeField] public WeaponPairReferences DoubleBarrelShotgun = new WeaponPairReferences();
        [SerializeField] public WeaponPairReferences PumpActionShotgun = new WeaponPairReferences();
    }
    

    [Header("References")]
    // The PlayerState we will sync the equipped category into.
    [SerializeField] private PlayerState playerState;

    // Optional animator reference if you want this controller to drive category params directly.
    [SerializeField] private Animator animator;

    // The parent transform that holds weapon models.
    [SerializeField] private Transform weaponHolder;

    // The parent transform that holds holstered weapon models.
    [SerializeField] private Transform weaponHolster;

    [Header("Weapon References By Category")]
    [SerializeField] private UnarmedWeaponReferences unarmedWeaponReferences = new UnarmedWeaponReferences();
    [SerializeField] private KnifeWeaponReferences knifeWeaponReferences = new KnifeWeaponReferences();
    [SerializeField] private TwoHandedWeaponReferences twoHandedWeaponReferences = new TwoHandedWeaponReferences();
    [SerializeField] private PistolWeaponReferences pistolWeaponReferences = new PistolWeaponReferences();
    [SerializeField] private RifleWeaponReferences rifleWeaponReferences = new RifleWeaponReferences();

    // These properties preserve existing runtime logic while exposing grouped references in the inspector.
    private GameObject combatKnife { get => knifeWeaponReferences.CombatKnife.Equipped; set => knifeWeaponReferences.CombatKnife.Equipped = value; }
    private GameObject holsteredCombatKnife { get => knifeWeaponReferences.CombatKnife.Holstered; set => knifeWeaponReferences.CombatKnife.Holstered = value; }
    private GameObject kitchenKnife { get => knifeWeaponReferences.KitchenKnife.Equipped; set => knifeWeaponReferences.KitchenKnife.Equipped = value; }
    private GameObject holsteredKitchenKnife { get => knifeWeaponReferences.KitchenKnife.Holstered; set => knifeWeaponReferences.KitchenKnife.Holstered = value; }
    private GameObject straightRazor { get => knifeWeaponReferences.StraightRazor.Equipped; set => knifeWeaponReferences.StraightRazor.Equipped = value; }
    private GameObject holsteredStraightRazor { get => knifeWeaponReferences.StraightRazor.Holstered; set => knifeWeaponReferences.StraightRazor.Holstered = value; }
    private GameObject cricketBat { get => twoHandedWeaponReferences.CricketBat.Equipped; set => twoHandedWeaponReferences.CricketBat.Equipped = value; }
    private GameObject holsteredCricketBat { get => twoHandedWeaponReferences.CricketBat.Holstered; set => twoHandedWeaponReferences.CricketBat.Holstered = value; }
    private GameObject leadPipe { get => twoHandedWeaponReferences.LeadPipe.Equipped; set => twoHandedWeaponReferences.LeadPipe.Equipped = value; }
    private GameObject holsteredLeadPipe { get => twoHandedWeaponReferences.LeadPipe.Holstered; set => twoHandedWeaponReferences.LeadPipe.Holstered = value; }
    private GameObject cane { get => twoHandedWeaponReferences.Cane.Equipped; set => twoHandedWeaponReferences.Cane.Equipped = value; }
    private GameObject holsteredCane { get => twoHandedWeaponReferences.Cane.Holstered; set => twoHandedWeaponReferences.Cane.Holstered = value; }
    private GameObject fellingAxe { get => twoHandedWeaponReferences.FellingAxe.Equipped; set => twoHandedWeaponReferences.FellingAxe.Equipped = value; }
    private GameObject holsteredFellingAxe { get => twoHandedWeaponReferences.FellingAxe.Holstered; set => twoHandedWeaponReferences.FellingAxe.Holstered = value; }
    private GameObject shovel { get => twoHandedWeaponReferences.Shovel.Equipped; set => twoHandedWeaponReferences.Shovel.Equipped = value; }
    private GameObject holsteredShovel { get => twoHandedWeaponReferences.Shovel.Holstered; set => twoHandedWeaponReferences.Shovel.Holstered = value; }
    private GameObject selfLoadingPistol { get => pistolWeaponReferences.SelfLoadingPistol.Equipped; set => pistolWeaponReferences.SelfLoadingPistol.Equipped = value; }
    private GameObject holsteredSelfLoadingPistol { get => pistolWeaponReferences.SelfLoadingPistol.Holstered; set => pistolWeaponReferences.SelfLoadingPistol.Holstered = value; }
    private GameObject revolver { get => pistolWeaponReferences.Revolver.Equipped; set => pistolWeaponReferences.Revolver.Equipped = value; }
    private GameObject holsteredRevolver { get => pistolWeaponReferences.Revolver.Holstered; set => pistolWeaponReferences.Revolver.Holstered = value; }
    private GameObject laserPistol { get => pistolWeaponReferences.LaserPistol.Equipped; set => pistolWeaponReferences.LaserPistol.Equipped = value; }
    private GameObject holsteredLaserPistol { get => pistolWeaponReferences.LaserPistol.Holstered; set => pistolWeaponReferences.LaserPistol.Holstered = value; }
    private GameObject submachineGun { get => rifleWeaponReferences.SubmachineGun.Equipped; set => rifleWeaponReferences.SubmachineGun.Equipped = value; }
    private GameObject holsteredSubmachineGun { get => rifleWeaponReferences.SubmachineGun.Holstered; set => rifleWeaponReferences.SubmachineGun.Holstered = value; }
    private GameObject huntingRifle { get => rifleWeaponReferences.HuntingRifle.Equipped; set => rifleWeaponReferences.HuntingRifle.Equipped = value; }
    private GameObject holsteredHuntingRifle { get => rifleWeaponReferences.HuntingRifle.Holstered; set => rifleWeaponReferences.HuntingRifle.Holstered = value; }
    private GameObject battleRifle { get => rifleWeaponReferences.BattleRifle.Equipped; set => rifleWeaponReferences.BattleRifle.Equipped = value; }
    private GameObject holsteredBattleRifle { get => rifleWeaponReferences.BattleRifle.Holstered; set => rifleWeaponReferences.BattleRifle.Holstered = value; }
    private GameObject lightMachineGun { get => rifleWeaponReferences.LightMachineGun.Equipped; set => rifleWeaponReferences.LightMachineGun.Equipped = value; }
    private GameObject holsteredLightMachineGun { get => rifleWeaponReferences.LightMachineGun.Holstered; set => rifleWeaponReferences.LightMachineGun.Holstered = value; }
    private GameObject laserRifle { get => rifleWeaponReferences.LaserRifle.Equipped; set => rifleWeaponReferences.LaserRifle.Equipped = value; }
    private GameObject holsteredLaserRifle { get => rifleWeaponReferences.LaserRifle.Holstered; set => rifleWeaponReferences.LaserRifle.Holstered = value; }
    private GameObject sniperRifle { get => rifleWeaponReferences.SniperRifle.Equipped; set => rifleWeaponReferences.SniperRifle.Equipped = value; }
    private GameObject holsteredSniperRifle { get => rifleWeaponReferences.SniperRifle.Holstered; set => rifleWeaponReferences.SniperRifle.Holstered = value; }
    private GameObject doubleBarrelShotgun { get => rifleWeaponReferences.DoubleBarrelShotgun.Equipped; set => rifleWeaponReferences.DoubleBarrelShotgun.Equipped = value; }
    private GameObject holsteredDoubleBarrelShotgun { get => rifleWeaponReferences.DoubleBarrelShotgun.Holstered; set => rifleWeaponReferences.DoubleBarrelShotgun.Holstered = value; }
    private GameObject pumpActionShotgun { get => rifleWeaponReferences.PumpActionShotgun.Equipped; set => rifleWeaponReferences.PumpActionShotgun.Equipped = value; }
    private GameObject holsteredPumpActionShotgun { get => rifleWeaponReferences.PumpActionShotgun.Holstered; set => rifleWeaponReferences.PumpActionShotgun.Holstered = value; }
    private GameObject knuckleIdleLeft { get => unarmedWeaponReferences.KnuckleIdleLeft; set => unarmedWeaponReferences.KnuckleIdleLeft = value; }
    private GameObject knuckleIdleRight { get => unarmedWeaponReferences.KnuckleIdleRight; set => unarmedWeaponReferences.KnuckleIdleRight = value; }
    private GameObject knuckleReadyLeft { get => unarmedWeaponReferences.KnuckleReadyLeft; set => unarmedWeaponReferences.KnuckleReadyLeft = value; }
    private GameObject knuckleReadyRight { get => unarmedWeaponReferences.KnuckleReadyRight; set => unarmedWeaponReferences.KnuckleReadyRight = value; }

    private int knifeEquipStateHash;
    private int knifeUnequipStateHash;
    private int lastAnimatorStateHash = -1;
    private bool knifeEquipTriggered;
    private bool knifeUnequipTriggered;
    private bool knifeHolsterDisableTriggered;
    private bool knifeHolsterReEnableTriggered;
    private bool knifeHolsterReEnableScheduled;
    private float knifeHolsterReEnableTime;
    private GameObject holsterKnifePendingReEnable;
    private int twoHandedEquipStateHash;
    private int twoHandedUnequipStateHash;
    private int lastTwoHandedAnimatorStateHash = -1;
    private bool twoHandedEquipTriggered;
    private bool twoHandedUnequipTriggered;
    private bool twoHandedHolsterDisableTriggered;
    private bool twoHandedHolsterReEnableTriggered;
    private bool twoHandedHolsterReEnableScheduled;
    private float twoHandedHolsterReEnableTime;
    private GameObject holsterTwoHandedPendingReEnable;
    private int pistolEquipStateHash;
    private int pistolUnequipStateHash;
    private int longarmEquipStateHash;
    private int longarmUnequipStateHash;
    private int lastPistolAnimatorStateHash = -1;
    private bool pistolEquipTriggered;
    private bool pistolUnequipTriggered;
    private bool pistolHolsterDisableTriggered;
    private bool pistolHolsterReEnableTriggered;
    private bool pistolHolsterReEnableScheduled;
    private float pistolHolsterReEnableTime;
    private GameObject holsterPistolPendingReEnable;
    private int lastLongarmAnimatorStateHash = -1;
    private bool longarmEquipTriggered;
    private bool longarmUnequipTriggered;
    private bool longarmHolsterDisableTriggered;
    private bool longarmHolsterReEnableTriggered;
    private bool longarmHolsterReEnableScheduled;
    private float longarmHolsterReEnableTime;
    private GameObject holsterLongarmPendingReEnable;

    [Header("Weapon List")]
    // The full selectable weapon list (all items you specified).
    [SerializeField] private List<WeaponEntry> weapons = new List<WeaponEntry>();

    // The selected weapon index from the list.
    [SerializeField] private int equippedWeaponIndex = 0;

    [Header("Current Weapon Ammo")]
    // The rounds currently loaded in the equipped weapon.
    [SerializeField, Min(0)] private int currentWeaponAmmo = 0;

    // The rounds currently stored in reserve for the equipped weapon.
    [SerializeField, Min(0)] private int currentWeaponReserveAmmo = 0;

    // Per-weapon ammo records used to keep each weapon's ammo values.
    [SerializeField, HideInInspector] private List<WeaponAmmoEntry> weaponAmmo = new List<WeaponAmmoEntry>();

    // The weapon currently represented by currentWeaponAmmo/currentWeaponReserveAmmo.
    [SerializeField, HideInInspector] private WeaponCategory trackedAmmoCategory = WeaponCategory.Unarmed;

    // The weapon name currently represented by currentWeaponAmmo/currentWeaponReserveAmmo.
    [SerializeField, HideInInspector] private string trackedAmmoWeaponName = string.Empty;

    // Inventory instance id for the currently equipped weapon instance (empty when not bound).
    [SerializeField, HideInInspector] private string equippedInventoryWeaponInstanceId = string.Empty;

    // The currently equipped weapon entry (cached at runtime).
    private WeaponEntry currentWeapon;

    // The currently equipped category (cached at runtime).
    private WeaponCategory currentCategory;

    // Hash for EquippedUnarmed animator bool.
    private static readonly int EquippedUnarmedParam = Animator.StringToHash("EquippedUnarmed");

    // Hash for EquippedKnife animator bool.
    private static readonly int EquippedKnifeParam = Animator.StringToHash("EquippedKnife");

    // Hash for EquippedTwoHanded animator bool.
    private static readonly int EquippedTwoHandedParam = Animator.StringToHash("EquippedTwoHanded");

    // Hash for EquippedBow animator bool.
    private static readonly int EquippedBowParam = Animator.StringToHash("EquippedBow");

    // Hash for EquippedPistol animator bool.
    private static readonly int EquippedPistolParam = Animator.StringToHash("EquippedPistol");

    // Hash for EquippedSubmachineGun animator bool.
    private static readonly int EquippedSubmachineGunParam = Animator.StringToHash("EquippedSubmachineGun");

    // Hash for EquippedRifle animator bool.
    private static readonly int EquippedRifleParam = Animator.StringToHash("EquippedRifle");

    // Hash for EquippedShotgun animator bool.
    private static readonly int EquippedShotgunParam = Animator.StringToHash("EquippedShotgun");

    // Hash for EquippedLongarm animator bool.
    private static readonly int EquippedLongarmParam = Animator.StringToHash("EquippedLongarm");

    // Hash for EquippedSpecial animator bool.
    private static readonly int EquippedSpecialParam = Animator.StringToHash("EquippedSpecial");

    // Hash for EquippedExplosive animator bool.
    private static readonly int EquippedExplosiveParam = Animator.StringToHash("EquippedExplosive");

    [Header("Runtime Category Flags")]
    // Whether the player is currently in the Unarmed category.
    [SerializeField, HideInInspector] private bool EquippedUnarmed;

    // Whether the player is currently in the Knife category.
    [SerializeField, HideInInspector] private bool EquippedKnife;

    // Whether the player is currently in the TwoHanded category.
    [SerializeField, HideInInspector] private bool EquippedTwoHanded;

    // Whether the player is currently in the Bow category.
    [SerializeField, HideInInspector] private bool EquippedBow;

    // Whether the player is currently in the Pistol category.
    [SerializeField, HideInInspector] private bool EquippedPistol;

    // Whether the player is currently in the SubmachineGun category.
    [SerializeField, HideInInspector] private bool EquippedSubmachineGun;

    // Whether the player is currently in the Rifle category.
    [SerializeField, HideInInspector] private bool EquippedRifle;

    // Whether the player is currently in the Shotgun category.
    [SerializeField, HideInInspector] private bool EquippedShotgun;

    // Whether the player is currently in a longarm category (Rifle, Shotgun, or SubmachineGun).
    [SerializeField, HideInInspector] private bool EquippedLongarm;

    // Whether the player is currently in the Special category.
    [SerializeField, HideInInspector] private bool EquippedSpecial;

    // Whether the player is currently in the Explosive category.
    [SerializeField, HideInInspector] private bool EquippedExplosive;

    [Header("Knife Animator Settings")]
    // Unity renders serialized objects as a foldout in the inspector.
    [SerializeField] private KnifeAnimatorSettings knifeAnimatorSettings = new KnifeAnimatorSettings();

    [Header("Two Handed Animator Settings")]
    // Unity renders serialized objects as a foldout in the inspector.
    [SerializeField] private TwoHandedAnimatorSettings twoHandedAnimatorSettings = new TwoHandedAnimatorSettings();

    [Header("Pistol Animator Settings")]
    // Unity renders serialized objects as a foldout in the inspector.
    [SerializeField] private PistolAnimatorSettings pistolAnimatorSettings = new PistolAnimatorSettings();

    [Header("Longarm Animator Settings")]
    // Unity renders serialized objects as a foldout in the inspector.
    [SerializeField] private LongarmAnimatorSettings longarmAnimatorSettings = new LongarmAnimatorSettings();

    private const string CombatKnifeWeaponName = "Combat Knife";
    
    private const string KitchenKnifeWeaponName = "Kitchen Knife";
    
    private const string StraightRazorWeaponName = "Straight Razor";

    private const string KnuckleDustersWeaponName = "Knuckle Dusters";

    private const string CricketBatWeaponName = "Cricket Bat";

    private const string LeadPipeWeaponName = "Lead Pipe";

    private const string CaneWeaponName = "Cane";

    private const string FellingAxeWeaponName = "Felling Axe";

    private const string ShovelWeaponName = "Shovel";

    private const string SelfLoadingPistolWeaponName = "Self-Loading Pistol";

    private const string RevolverWeaponName = "Revolver";

    private const string LaserPistolWeaponName = "Laser Pistol";

    private const string SubmachineGunWeaponName = "Submachine Gun";

    private const string HuntingRifleWeaponName = "Hunting Rifle";

    private const string BattleRifleWeaponName = "Battle Rifle";

    private const string LightMachineGunWeaponName = "Light Machine Gun";

    private const string LaserRifleWeaponName = "Laser Rifle";

    private const string SniperRifleWeaponName = "Sniper Rifle";

    private const string DoubleBarrelShotgunWeaponName = "Double-Barrel Shotgun";

    private const string PumpActionShotgunWeaponName = "Pump-Action Shotgun";

    
	
    // methods
    private void EnsureWeaponReferenceGroups()
    {
        if (unarmedWeaponReferences == null) unarmedWeaponReferences = new UnarmedWeaponReferences();
        if (knifeWeaponReferences == null) knifeWeaponReferences = new KnifeWeaponReferences();
        if (twoHandedWeaponReferences == null) twoHandedWeaponReferences = new TwoHandedWeaponReferences();
        if (pistolWeaponReferences == null) pistolWeaponReferences = new PistolWeaponReferences();
        if (rifleWeaponReferences == null) rifleWeaponReferences = new RifleWeaponReferences();

        EnsureWeaponPair(ref knifeWeaponReferences.CombatKnife);
        EnsureWeaponPair(ref knifeWeaponReferences.KitchenKnife);
        EnsureWeaponPair(ref knifeWeaponReferences.StraightRazor);
        EnsureWeaponPair(ref twoHandedWeaponReferences.CricketBat);
        EnsureWeaponPair(ref twoHandedWeaponReferences.LeadPipe);
        EnsureWeaponPair(ref twoHandedWeaponReferences.Cane);
        EnsureWeaponPair(ref twoHandedWeaponReferences.FellingAxe);
        EnsureWeaponPair(ref twoHandedWeaponReferences.Shovel);
        EnsureWeaponPair(ref pistolWeaponReferences.SelfLoadingPistol);
        EnsureWeaponPair(ref pistolWeaponReferences.Revolver);
        EnsureWeaponPair(ref pistolWeaponReferences.LaserPistol);
        EnsureWeaponPair(ref rifleWeaponReferences.SubmachineGun);
        EnsureWeaponPair(ref rifleWeaponReferences.HuntingRifle);
        EnsureWeaponPair(ref rifleWeaponReferences.BattleRifle);
        EnsureWeaponPair(ref rifleWeaponReferences.LightMachineGun);
        EnsureWeaponPair(ref rifleWeaponReferences.LaserRifle);
        EnsureWeaponPair(ref rifleWeaponReferences.SniperRifle);
        EnsureWeaponPair(ref rifleWeaponReferences.DoubleBarrelShotgun);
        EnsureWeaponPair(ref rifleWeaponReferences.PumpActionShotgun);
    }

    private void EnsureWeaponPair(ref WeaponPairReferences weaponPairReferences)
    {
        if (weaponPairReferences == null) weaponPairReferences = new WeaponPairReferences();
    }

    private void Awake()
    {
        EnsureWeaponReferenceGroups();
        
        // Auto-find PlayerState if not set.
        if (!playerState)
            playerState = GetComponentInParent<PlayerState>();

        // Auto-find Animator if not set.
        if (!animator)
            animator = GetComponentInChildren<Animator>();

        // Auto-find WeaponHolder if not set.
        if (!weaponHolder)
            weaponHolder = transform.Find("WeaponHolder");

        // Auto-find WeaponHolster if not set.
        if (!weaponHolster)
            weaponHolster = transform.Find("WeaponHolster");

        // Auto-find CombatKnife if not set.
        if (!combatKnife)
            combatKnife = FindWeaponModel("CombatKnife");

        // Auto-find KitchenKnife if not set.
        if (!kitchenKnife)
            kitchenKnife = FindWeaponModel("KitchenKnife");

        // Auto-find StraightRazor if not set.
        if (!straightRazor)
            straightRazor = FindWeaponModel("StraightRazor");

        // Auto-find CricketBat if not set.
        if (!cricketBat)
            cricketBat = FindWeaponModel("CricketBat");

        // Auto-find LeadPipe if not set.
        if (!leadPipe)
            leadPipe = FindWeaponModel("LeadPipe");

        if (!leadPipe)
            leadPipe = FindModelAnywhere("LeadPipeEquipped");

        // Auto-find Cane if not set.
        if (!cane)
            cane = FindWeaponModel("Cane");

        if (!cane)
            cane = FindModelAnywhere("CaneEquipped");

        // Auto-find FellingAxe if not set.
        if (!fellingAxe)
            fellingAxe = FindWeaponModel("FellingAxe");

        if (!fellingAxe)
            fellingAxe = FindModelAnywhere("FellingAxeEquipped");

        // Auto-find Shovel if not set.
        if (!shovel)
            shovel = FindWeaponModel("Shovel");

        if (!shovel)
            shovel = FindModelAnywhere("ShovelEquipped");

        // Auto-find SelfLoadingPistol if not set.
        if (!selfLoadingPistol)
            selfLoadingPistol = FindWeaponModel("SelfLoadingPistol");

        if (!selfLoadingPistol)
            selfLoadingPistol = FindModelAnywhere("SelfLoadingPistolEquipped");

        // Auto-find Revolver if not set.
        if (!revolver)
            revolver = FindWeaponModel("Revolver");

        if (!revolver)
            revolver = FindModelAnywhere("RevolverEquipped");

        // Auto-find LaserPistol if not set.
        if (!laserPistol)
            laserPistol = FindWeaponModel("LaserPistol");

        if (!laserPistol)
            laserPistol = FindModelAnywhere("LaserPistolEquipped");

        if (!laserPistol)
            laserPistol = FindModelAnywhere("Laser Pistol Equipped");

        // Auto-find SubmachineGun if not set.
        if (!submachineGun)
            submachineGun = FindWeaponModel("SubmachineGun");

        if (!submachineGun)
            submachineGun = FindModelAnywhere("SubmachineGunEquipped");

        if (!submachineGun)
            submachineGun = FindModelAnywhere("Submachine Gun Equipped");

        // Auto-find HuntingRifle if not set.
        if (!huntingRifle)
            huntingRifle = FindWeaponModel("HuntingRifle");

        if (!huntingRifle)
            huntingRifle = FindModelAnywhere("HuntingRifleEquipped");

        if (!huntingRifle)
            huntingRifle = FindModelAnywhere("Hunting Rifle Equipped");

        // Auto-find BattleRifle if not set.
        if (!battleRifle)
            battleRifle = FindWeaponModel("BattleRifle");

        if (!battleRifle)
            battleRifle = FindModelAnywhere("BattleRifleEquipped");

        if (!battleRifle)
            battleRifle = FindModelAnywhere("Battle Rifle Equipped");

        // Auto-find LightMachineGun if not set.
        if (!lightMachineGun)
            lightMachineGun = FindWeaponModel("LightMachineGun");

        if (!lightMachineGun)
            lightMachineGun = FindModelAnywhere("LightMachineGunEquipped");

        if (!lightMachineGun)
            lightMachineGun = FindModelAnywhere("Light Machine Gun Equipped");

        // Auto-find LaserRifle if not set.
        if (!laserRifle)
            laserRifle = FindWeaponModel("LaserRifle");

        if (!laserRifle)
            laserRifle = FindModelAnywhere("LaserRifleEquipped");

        if (!laserRifle)
            laserRifle = FindModelAnywhere("Laser Rifle Equipped");

        // Auto-find SniperRifle if not set.
        if (!sniperRifle)
            sniperRifle = FindWeaponModel("SniperRifle");

        if (!sniperRifle)
            sniperRifle = FindModelAnywhere("SniperRifleEquipped");

        if (!sniperRifle)
            sniperRifle = FindModelAnywhere("Sniper Rifle Equipped");

        // Auto-find DoubleBarrelShotgun if not set.
        if (!doubleBarrelShotgun)
            doubleBarrelShotgun = FindWeaponModel("DoubleBarrelShotgun");

        if (!doubleBarrelShotgun)
            doubleBarrelShotgun = FindModelAnywhere("DoubleBarrelShotgunEquipped");

        if (!doubleBarrelShotgun)
            doubleBarrelShotgun = FindModelAnywhere("Double Barrel Shotgun Equipped");

        // Auto-find PumpActionShotgun if not set.
        if (!pumpActionShotgun)
            pumpActionShotgun = FindWeaponModel("PumpActionShotgun");

        if (!pumpActionShotgun)
            pumpActionShotgun = FindModelAnywhere("PumpActionShotgunEquipped");

        if (!pumpActionShotgun)
            pumpActionShotgun = FindModelAnywhere("Pump Action Shotgun Equipped");

        // Auto-find CombatKnife under WeaponHolster if not set.
        if (!holsteredCombatKnife)
            holsteredCombatKnife = FindHolsteredWeaponModel("CombatKnife");

        // Auto-find KitchenKnife under WeaponHolster if not set.
        if (!holsteredKitchenKnife)
            holsteredKitchenKnife = FindHolsteredWeaponModel("KitchenKnife");

        // Auto-find StraightRazor under WeaponHolster if not set.
        if (!holsteredStraightRazor)
            holsteredStraightRazor = FindHolsteredWeaponModel("StraightRazor");

        // Auto-find CricketBat under WeaponHolster if not set.
        if (!holsteredCricketBat)
            holsteredCricketBat = FindHolsteredWeaponModel("CricketBat");

        // Auto-find LeadPipe under WeaponHolster if not set.
        if (!holsteredLeadPipe)
            holsteredLeadPipe = FindHolsteredWeaponModel("LeadPipe");

        if (!holsteredLeadPipe)
            holsteredLeadPipe = FindModelAnywhere("LeadPipeHolstered");

        // Auto-find Cane under WeaponHolster if not set.
        if (!holsteredCane)
            holsteredCane = FindHolsteredWeaponModel("Cane");

        if (!holsteredCane)
            holsteredCane = FindModelAnywhere("CaneHolstered");

        // Auto-find FellingAxe under WeaponHolster if not set.
        if (!holsteredFellingAxe)
            holsteredFellingAxe = FindHolsteredWeaponModel("FellingAxe");

        if (!holsteredFellingAxe)
            holsteredFellingAxe = FindModelAnywhere("FellingAxeHolstered");

        // Auto-find Shovel under WeaponHolster if not set.
        if (!holsteredShovel)
            holsteredShovel = FindHolsteredWeaponModel("Shovel");

        if (!holsteredShovel)
            holsteredShovel = FindModelAnywhere("ShovelHolstered");

        // Auto-find SelfLoadingPistol under WeaponHolster if not set.
        if (!holsteredSelfLoadingPistol)
            holsteredSelfLoadingPistol = FindHolsteredWeaponModel("SelfLoadingPistol");

        if (!holsteredSelfLoadingPistol)
            holsteredSelfLoadingPistol = FindModelAnywhere("SelfLoadingPistolHolstered");

        // Auto-find Revolver under WeaponHolster if not set.
        if (!holsteredRevolver)
            holsteredRevolver = FindHolsteredWeaponModel("Revolver");

        if (!holsteredRevolver)
            holsteredRevolver = FindModelAnywhere("RevolverHolstered");

        // Auto-find LaserPistol under WeaponHolster if not set.
        if (!holsteredLaserPistol)
            holsteredLaserPistol = FindHolsteredWeaponModel("LaserPistol");

        if (!holsteredLaserPistol)
            holsteredLaserPistol = FindModelAnywhere("LaserPistolHolstered");

        if (!holsteredLaserPistol)
            holsteredLaserPistol = FindModelAnywhere("Laser Pistol Holstered");

        // Auto-find SubmachineGun under WeaponHolster if not set.
        if (!holsteredSubmachineGun)
            holsteredSubmachineGun = FindHolsteredWeaponModel("SubmachineGun");

        if (!holsteredSubmachineGun)
            holsteredSubmachineGun = FindModelAnywhere("SubmachineGunHolstered");

        if (!holsteredSubmachineGun)
            holsteredSubmachineGun = FindModelAnywhere("Submachine Gun Holstered");

        // Auto-find HuntingRifle under WeaponHolster if not set.
        if (!holsteredHuntingRifle)
            holsteredHuntingRifle = FindHolsteredWeaponModel("HuntingRifle");

        if (!holsteredHuntingRifle)
            holsteredHuntingRifle = FindModelAnywhere("HuntingRifleHolstered");

        if (!holsteredHuntingRifle)
            holsteredHuntingRifle = FindModelAnywhere("Hunting Rifle Holstered");

        // Auto-find BattleRifle under WeaponHolster if not set.
        if (!holsteredBattleRifle)
            holsteredBattleRifle = FindHolsteredWeaponModel("BattleRifle");

        if (!holsteredBattleRifle)
            holsteredBattleRifle = FindModelAnywhere("BattleRifleHolstered");

        if (!holsteredBattleRifle)
            holsteredBattleRifle = FindModelAnywhere("Battle Rifle Holstered");

        // Auto-find LightMachineGun under WeaponHolster if not set.
        if (!holsteredLightMachineGun)
            holsteredLightMachineGun = FindHolsteredWeaponModel("LightMachineGun");

        if (!holsteredLightMachineGun)
            holsteredLightMachineGun = FindModelAnywhere("LightMachineGunHolstered");

        if (!holsteredLightMachineGun)
            holsteredLightMachineGun = FindModelAnywhere("Light Machine Gun Holstered");

        // Auto-find LaserRifle under WeaponHolster if not set.
        if (!holsteredLaserRifle)
            holsteredLaserRifle = FindHolsteredWeaponModel("LaserRifle");

        if (!holsteredLaserRifle)
            holsteredLaserRifle = FindModelAnywhere("LaserRifleHolstered");

        if (!holsteredLaserRifle)
            holsteredLaserRifle = FindModelAnywhere("Laser Rifle Holstered");

        // Auto-find SniperRifle under WeaponHolster if not set.
        if (!holsteredSniperRifle)
            holsteredSniperRifle = FindHolsteredWeaponModel("SniperRifle");

        if (!holsteredSniperRifle)
            holsteredSniperRifle = FindModelAnywhere("SniperRifleHolstered");

        if (!holsteredSniperRifle)
            holsteredSniperRifle = FindModelAnywhere("Sniper Rifle Holstered");

        // Auto-find DoubleBarrelShotgun under WeaponHolster if not set.
        if (!holsteredDoubleBarrelShotgun)
            holsteredDoubleBarrelShotgun = FindHolsteredWeaponModel("DoubleBarrelShotgun");

        if (!holsteredDoubleBarrelShotgun)
            holsteredDoubleBarrelShotgun = FindModelAnywhere("DoubleBarrelShotgunHolstered");

        if (!holsteredDoubleBarrelShotgun)
            holsteredDoubleBarrelShotgun = FindModelAnywhere("Double Barrel Shotgun Holstered");

        // Auto-find PumpActionShotgun under WeaponHolster if not set.
        if (!holsteredPumpActionShotgun)
            holsteredPumpActionShotgun = FindHolsteredWeaponModel("PumpActionShotgun");

        if (!holsteredPumpActionShotgun)
            holsteredPumpActionShotgun = FindModelAnywhere("PumpActionShotgunHolstered");

        if (!holsteredPumpActionShotgun)
            holsteredPumpActionShotgun = FindModelAnywhere("Pump Action Shotgun Holstered");

        // Auto-find knuckle models if not set.
        if (!knuckleIdleLeft)
            knuckleIdleLeft = FindModelAnywhere("KnuckleIdleLeft");

        if (!knuckleIdleRight)
            knuckleIdleRight = FindModelAnywhere("KnuckleIdleRight");

        if (!knuckleReadyLeft)
            knuckleReadyLeft = FindModelAnywhere("KnuckleReadyLeft");

        if (!knuckleReadyRight)
            knuckleReadyRight = FindModelAnywhere("KnuckleReadyRight");

        // Cache state hashes for fast lookup.
        CacheStateHashes();

        // Default to not in-hand until an equip animation reaches its enable timing.
        if (playerState)
            playerState.SetWeaponInHand(false);

        // Ensure the weapon list contains your full default set.
        EnsureDefaultWeaponList();

        // Ensure we have ammo records for all configured weapons.
        EnsureWeaponAmmoRecords();

        // Clamp the equipped index so it is always valid.
        equippedWeaponIndex = Mathf.Clamp(equippedWeaponIndex, 0, weapons.Count - 1);

        // Equip the selected index at startup.
        EquipByIndex(equippedWeaponIndex);
    }
    

    private void OnValidate()
    {
        EnsureWeaponReferenceGroups();
        
        // Ensure the weapon list contains your full default set.
        EnsureDefaultWeaponList();

        // Persist inspector ammo edits before any weapon list/category changes are applied.
        SaveTrackedAmmoValues();

        // Ensure we have ammo records for all configured weapons.
        EnsureWeaponAmmoRecords();

        // Stop if we have no weapons.
        if (weapons == null || weapons.Count == 0) return;

        // Clamp the equipped index so it is always valid.
        equippedWeaponIndex = Mathf.Clamp(equippedWeaponIndex, 0, weapons.Count - 1);

        // Refresh cached state hashes for inspector edits.
        CacheStateHashes();

        // Auto-find WeaponHolder if not set.
        if (!weaponHolder)
            weaponHolder = transform.Find("WeaponHolder");

        // Auto-find CombatKnife if not set.
        if (!combatKnife)
            combatKnife = FindWeaponModel("CombatKnife");

        // Auto-find KitchenKnife if not set.
        if (!kitchenKnife)
            kitchenKnife = FindWeaponModel("KitchenKnife");

        // Auto-find StraightRazor if not set.
        if (!straightRazor)
            straightRazor = FindWeaponModel("StraightRazor");

        // Auto-find CricketBat if not set.
        if (!cricketBat)
            cricketBat = FindWeaponModel("CricketBat");

        // Auto-find LeadPipe if not set.
        if (!leadPipe)
            leadPipe = FindWeaponModel("LeadPipe");

        if (!leadPipe)
            leadPipe = FindModelAnywhere("LeadPipeEquipped");

        // Auto-find Cane if not set.
        if (!cane)
            cane = FindWeaponModel("Cane");

        if (!cane)
            cane = FindModelAnywhere("CaneEquipped");

        // Auto-find FellingAxe if not set.
        if (!fellingAxe)
            fellingAxe = FindWeaponModel("FellingAxe");

        if (!fellingAxe)
            fellingAxe = FindModelAnywhere("FellingAxeEquipped");

        // Auto-find Shovel if not set.
        if (!shovel)
            shovel = FindWeaponModel("Shovel");

        if (!shovel)
            shovel = FindModelAnywhere("ShovelEquipped");

        // Auto-find SelfLoadingPistol if not set.
        if (!selfLoadingPistol)
            selfLoadingPistol = FindWeaponModel("SelfLoadingPistol");

        if (!selfLoadingPistol)
            selfLoadingPistol = FindModelAnywhere("SelfLoadingPistolEquipped");

        // Auto-find Revolver if not set.
        if (!revolver)
            revolver = FindWeaponModel("Revolver");

        if (!revolver)
            revolver = FindModelAnywhere("RevolverEquipped");

        // Auto-find LaserPistol if not set.
        if (!laserPistol)
            laserPistol = FindWeaponModel("LaserPistol");

        if (!laserPistol)
            laserPistol = FindModelAnywhere("LaserPistolEquipped");

        if (!laserPistol)
            laserPistol = FindModelAnywhere("Laser Pistol Equipped");

        // Auto-find SubmachineGun if not set.
        if (!submachineGun)
            submachineGun = FindWeaponModel("SubmachineGun");

        if (!submachineGun)
            submachineGun = FindModelAnywhere("SubmachineGunEquipped");

        if (!submachineGun)
            submachineGun = FindModelAnywhere("Submachine Gun Equipped");

        // Auto-find HuntingRifle if not set.
        if (!huntingRifle)
            huntingRifle = FindWeaponModel("HuntingRifle");

        if (!huntingRifle)
            huntingRifle = FindModelAnywhere("HuntingRifleEquipped");

        if (!huntingRifle)
            huntingRifle = FindModelAnywhere("Hunting Rifle Equipped");

        // Auto-find BattleRifle if not set.
        if (!battleRifle)
            battleRifle = FindWeaponModel("BattleRifle");

        if (!battleRifle)
            battleRifle = FindModelAnywhere("BattleRifleEquipped");

        if (!battleRifle)
            battleRifle = FindModelAnywhere("Battle Rifle Equipped");

        // Auto-find LightMachineGun if not set.
        if (!lightMachineGun)
            lightMachineGun = FindWeaponModel("LightMachineGun");

        if (!lightMachineGun)
            lightMachineGun = FindModelAnywhere("LightMachineGunEquipped");

        if (!lightMachineGun)
            lightMachineGun = FindModelAnywhere("Light Machine Gun Equipped");

        // Auto-find LaserRifle if not set.
        if (!laserRifle)
            laserRifle = FindWeaponModel("LaserRifle");

        if (!laserRifle)
            laserRifle = FindModelAnywhere("LaserRifleEquipped");

        if (!laserRifle)
            laserRifle = FindModelAnywhere("Laser Rifle Equipped");

        // Auto-find SniperRifle if not set.
        if (!sniperRifle)
            sniperRifle = FindWeaponModel("SniperRifle");

        if (!sniperRifle)
            sniperRifle = FindModelAnywhere("SniperRifleEquipped");

        if (!sniperRifle)
            sniperRifle = FindModelAnywhere("Sniper Rifle Equipped");

        // Auto-find DoubleBarrelShotgun if not set.
        if (!doubleBarrelShotgun)
            doubleBarrelShotgun = FindWeaponModel("DoubleBarrelShotgun");

        if (!doubleBarrelShotgun)
            doubleBarrelShotgun = FindModelAnywhere("DoubleBarrelShotgunEquipped");

        if (!doubleBarrelShotgun)
            doubleBarrelShotgun = FindModelAnywhere("Double Barrel Shotgun Equipped");

        // Auto-find PumpActionShotgun if not set.
        if (!pumpActionShotgun)
            pumpActionShotgun = FindWeaponModel("PumpActionShotgun");

        if (!pumpActionShotgun)
            pumpActionShotgun = FindModelAnywhere("PumpActionShotgunEquipped");

        if (!pumpActionShotgun)
            pumpActionShotgun = FindModelAnywhere("Pump Action Shotgun Equipped");

        // Auto-find WeaponHolster if not set.
        if (!weaponHolster)
            weaponHolster = transform.Find("WeaponHolster");

        // Auto-find CombatKnife under WeaponHolster if not set.
        if (!holsteredCombatKnife)
            holsteredCombatKnife = FindHolsteredWeaponModel("CombatKnife");

        // Auto-find KitchenKnife under WeaponHolster if not set.
        if (!holsteredKitchenKnife)
            holsteredKitchenKnife = FindHolsteredWeaponModel("KitchenKnife");

        // Auto-find StraightRazor under WeaponHolster if not set.
        if (!holsteredStraightRazor)
            holsteredStraightRazor = FindHolsteredWeaponModel("StraightRazor");

        // Auto-find CricketBat under WeaponHolster if not set.
        if (!holsteredCricketBat)
            holsteredCricketBat = FindHolsteredWeaponModel("CricketBat");

        // Auto-find LeadPipe under WeaponHolster if not set.
        if (!holsteredLeadPipe)
            holsteredLeadPipe = FindHolsteredWeaponModel("LeadPipe");

        if (!holsteredLeadPipe)
            holsteredLeadPipe = FindModelAnywhere("LeadPipeHolstered");

        // Auto-find Cane under WeaponHolster if not set.
        if (!holsteredCane)
            holsteredCane = FindHolsteredWeaponModel("Cane");

        if (!holsteredCane)
            holsteredCane = FindModelAnywhere("CaneHolstered");

        // Auto-find FellingAxe under WeaponHolster if not set.
        if (!holsteredFellingAxe)
            holsteredFellingAxe = FindHolsteredWeaponModel("FellingAxe");

        if (!holsteredFellingAxe)
            holsteredFellingAxe = FindModelAnywhere("FellingAxeHolstered");

        // Auto-find Shovel under WeaponHolster if not set.
        if (!holsteredShovel)
            holsteredShovel = FindHolsteredWeaponModel("Shovel");

        if (!holsteredShovel)
            holsteredShovel = FindModelAnywhere("ShovelHolstered");

        // Auto-find SelfLoadingPistol under WeaponHolster if not set.
        if (!holsteredSelfLoadingPistol)
            holsteredSelfLoadingPistol = FindHolsteredWeaponModel("SelfLoadingPistol");

        if (!holsteredSelfLoadingPistol)
            holsteredSelfLoadingPistol = FindModelAnywhere("SelfLoadingPistolHolstered");

        // Auto-find Revolver under WeaponHolster if not set.
        if (!holsteredRevolver)
            holsteredRevolver = FindHolsteredWeaponModel("Revolver");

        if (!holsteredRevolver)
            holsteredRevolver = FindModelAnywhere("RevolverHolstered");

        // Auto-find LaserPistol under WeaponHolster if not set.
        if (!holsteredLaserPistol)
            holsteredLaserPistol = FindHolsteredWeaponModel("LaserPistol");

        if (!holsteredLaserPistol)
            holsteredLaserPistol = FindModelAnywhere("LaserPistolHolstered");

        if (!holsteredLaserPistol)
            holsteredLaserPistol = FindModelAnywhere("Laser Pistol Holstered");

        // Auto-find SubmachineGun under WeaponHolster if not set.
        if (!holsteredSubmachineGun)
            holsteredSubmachineGun = FindHolsteredWeaponModel("SubmachineGun");

        if (!holsteredSubmachineGun)
            holsteredSubmachineGun = FindModelAnywhere("SubmachineGunHolstered");

        if (!holsteredSubmachineGun)
            holsteredSubmachineGun = FindModelAnywhere("Submachine Gun Holstered");

        // Auto-find HuntingRifle under WeaponHolster if not set.
        if (!holsteredHuntingRifle)
            holsteredHuntingRifle = FindHolsteredWeaponModel("HuntingRifle");

        if (!holsteredHuntingRifle)
            holsteredHuntingRifle = FindModelAnywhere("HuntingRifleHolstered");

        if (!holsteredHuntingRifle)
            holsteredHuntingRifle = FindModelAnywhere("Hunting Rifle Holstered");

        // Auto-find BattleRifle under WeaponHolster if not set.
        if (!holsteredBattleRifle)
            holsteredBattleRifle = FindHolsteredWeaponModel("BattleRifle");

        if (!holsteredBattleRifle)
            holsteredBattleRifle = FindModelAnywhere("BattleRifleHolstered");

        if (!holsteredBattleRifle)
            holsteredBattleRifle = FindModelAnywhere("Battle Rifle Holstered");

        // Auto-find LightMachineGun under WeaponHolster if not set.
        if (!holsteredLightMachineGun)
            holsteredLightMachineGun = FindHolsteredWeaponModel("LightMachineGun");

        if (!holsteredLightMachineGun)
            holsteredLightMachineGun = FindModelAnywhere("LightMachineGunHolstered");

        if (!holsteredLightMachineGun)
            holsteredLightMachineGun = FindModelAnywhere("Light Machine Gun Holstered");

        // Auto-find LaserRifle under WeaponHolster if not set.
        if (!holsteredLaserRifle)
            holsteredLaserRifle = FindHolsteredWeaponModel("LaserRifle");

        if (!holsteredLaserRifle)
            holsteredLaserRifle = FindModelAnywhere("LaserRifleHolstered");

        if (!holsteredLaserRifle)
            holsteredLaserRifle = FindModelAnywhere("Laser Rifle Holstered");

        // Auto-find SniperRifle under WeaponHolster if not set.
        if (!holsteredSniperRifle)
            holsteredSniperRifle = FindHolsteredWeaponModel("SniperRifle");

        if (!holsteredSniperRifle)
            holsteredSniperRifle = FindModelAnywhere("SniperRifleHolstered");

        if (!holsteredSniperRifle)
            holsteredSniperRifle = FindModelAnywhere("Sniper Rifle Holstered");

        // Auto-find DoubleBarrelShotgun under WeaponHolster if not set.
        if (!holsteredDoubleBarrelShotgun)
            holsteredDoubleBarrelShotgun = FindHolsteredWeaponModel("DoubleBarrelShotgun");

        if (!holsteredDoubleBarrelShotgun)
            holsteredDoubleBarrelShotgun = FindModelAnywhere("DoubleBarrelShotgunHolstered");

        if (!holsteredDoubleBarrelShotgun)
            holsteredDoubleBarrelShotgun = FindModelAnywhere("Double Barrel Shotgun Holstered");

        // Auto-find PumpActionShotgun under WeaponHolster if not set.
        if (!holsteredPumpActionShotgun)
            holsteredPumpActionShotgun = FindHolsteredWeaponModel("PumpActionShotgun");

        if (!holsteredPumpActionShotgun)
            holsteredPumpActionShotgun = FindModelAnywhere("PumpActionShotgunHolstered");

        if (!holsteredPumpActionShotgun)
            holsteredPumpActionShotgun = FindModelAnywhere("Pump Action Shotgun Holstered");

        // Auto-find knuckle models if not set.
        if (!knuckleIdleLeft)
            knuckleIdleLeft = FindModelAnywhere("KnuckleIdleLeft");

        if (!knuckleIdleRight)
            knuckleIdleRight = FindModelAnywhere("KnuckleIdleRight");

        if (!knuckleReadyLeft)
            knuckleReadyLeft = FindModelAnywhere("KnuckleReadyLeft");

        if (!knuckleReadyRight)
            knuckleReadyRight = FindModelAnywhere("KnuckleReadyRight");

        // Cache selection/category in editor without toggling GameObject active state.
        currentWeapon = weapons[equippedWeaponIndex];
        currentCategory = currentWeapon != null ? currentWeapon.Category : WeaponCategory.Unarmed;
        UpdateCategoryBooleans(currentCategory);

        // Show ammo values for the currently selected weapon.
        LoadTrackedAmmoValuesForCurrentWeapon();
    }


    private void Update()
    {

        // Stop if we do not have an animator.
        if (!animator) return;

        UpdateKnifeVisibilityFromAnimator();
        UpdateTwoHandedVisibilityFromAnimator();
        UpdatePistolVisibilityFromAnimator();
        UpdateLongarmVisibilityFromAnimator();
        UpdateWeaponInHandFromEquipCompletion();
        UpdateKnuckleVisibility();
        UpdateCricketBatVisibility();
        UpdatePistolVisibility();
        UpdateRifleVisibility();
    }
    

    public void EquipByIndex(int index)
    {
        
        // Stop if the weapons list is missing.
        if (weapons == null) return;

        // Stop if there are no weapons.
        if (weapons.Count == 0) return;

        // Stop if the index is invalid.
        if (index < 0 || index >= weapons.Count) return;

        // Persist current inspector ammo before changing equipped weapon.
        SaveTrackedAmmoValues();

        // New equip selection starts unbound until inventory UI binds a specific weapon instance.
        equippedInventoryWeaponInstanceId = string.Empty;

        // Store the new equipped index.
        equippedWeaponIndex = index;

        // Cache the equipped weapon entry.
        currentWeapon = weapons[equippedWeaponIndex];

        // Cache the equipped category.
        currentCategory = currentWeapon.Category;

        // Show ammo values for this equipped weapon.
        LoadTrackedAmmoValuesForCurrentWeapon();

        // Update all hidden category booleans.
        UpdateCategoryBooleans(currentCategory);

        // Sync the category name into PlayerState (keeps your existing state logic working).
        SyncCategoryToPlayerState(currentCategory);

        // Push category booleans into animator parameters (if present).
        UpdateAnimatorCategoryParameters();

        // Toggle holstered weapon visuals to match the equipped weapon.
        UpdateHolsterVisibility();

        // Toggle knuckle visuals to match equipped/holstered state.
        UpdateKnuckleVisibility();

        // Toggle cricket bat visuals to match equipped/holstered state.
        UpdateCricketBatVisibility();

        // Toggle pistol visuals to match equipped/holstered state.
        UpdatePistolVisibility();

        // Toggle rifle visuals to match equipped/holstered state.
        UpdateRifleVisibility();
    }


    public bool TryEquipWeapon(WeaponCategory category, string weaponName)
    {

        // Stop if the weapon name is missing.
        if (string.IsNullOrWhiteSpace(weaponName)) return false;

        // Stop if we have no weapons.
        if (weapons == null || weapons.Count == 0) return false;

        // Find a matching category/name entry.
        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponEntry weaponEntry = weapons[i];
            if (weaponEntry == null) continue;

            if (weaponEntry.Category != category) continue;

            if (!string.Equals(weaponEntry.WeaponName, weaponName, StringComparison.OrdinalIgnoreCase)) continue;

            EquipByIndex(i);
            return true;
        }

        return false;
    }


    public bool TryEquipWeaponByName(string weaponName)
    {

        // Stop if the weapon name is missing.
        if (string.IsNullOrWhiteSpace(weaponName)) return false;

        // Stop if we have no weapons.
        if (weapons == null || weapons.Count == 0) return false;

        // Find the first matching weapon name.
        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponEntry weaponEntry = weapons[i];
            if (weaponEntry == null) continue;

            if (!string.Equals(weaponEntry.WeaponName, weaponName, StringComparison.OrdinalIgnoreCase)) continue;

            EquipByIndex(i);
            return true;
        }

        return false;
    }


    public bool TryEquipUnarmed()
    {

        // Stop if we have no weapons.
        if (weapons == null || weapons.Count == 0) return false;

        // Find the first unarmed entry in the configured weapon list.
        int unarmedIndex = GetFirstWeaponIndexByCategory(WeaponCategory.Unarmed);
        if (unarmedIndex < 0) return false;

        EquipByIndex(unarmedIndex);
        return true;
    }
    

    public void EquipNext()
    {
        
        // Stop if we have no weapons.
        if (weapons == null || weapons.Count == 0) return;

        // Calculate the next index.
        int nextIndex = equippedWeaponIndex + 1;

        // Wrap to the start if needed.
        if (nextIndex >= weapons.Count) nextIndex = 0;

        // Equip the next weapon.
        EquipByIndex(nextIndex);
    }
    

    public void EquipPrevious()
    {
        
        // Stop if we have no weapons.
        if (weapons == null || weapons.Count == 0) return;

        // Calculate the previous index.
        int prevIndex = equippedWeaponIndex - 1;

        // Wrap to the end if needed.
        if (prevIndex < 0) prevIndex = weapons.Count - 1;

        // Equip the previous weapon.
        EquipByIndex(prevIndex);
    }
    

    public WeaponEntry GetCurrentWeapon()
    {
        
        // Return the currently equipped weapon entry.
        return currentWeapon;
    }
    

    public WeaponCategory GetCurrentCategory()
    {
        
        // Return the currently equipped category.
        return currentCategory;
    }


    public override string GetCurrentCategoryName()
    {
        return currentCategory.ToString();
    }


    public override string GetCurrentWeaponName()
    {
        return currentWeapon != null ? currentWeapon.WeaponName : string.Empty;
    }


    public bool IsEquipAnimationPlaying()
    {

        // Stop if the animator is missing.
        if (!animator) return false;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        if (IsEquipStateInProgress(current))
            return true;

        if (!animator.IsInTransition(0))
            return false;

        AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
        return IsEquipStateInProgress(next);
    }
    

    public int GetEquippedWeaponIndex()
    {
        
        // Return the currently equipped weapon index.
        return equippedWeaponIndex;
    }

    
    public int GetCurrentWeaponAmmo()
    {
        
        // Return the rounds currently loaded in the equipped weapon.
        return currentWeaponAmmo;
    }


    public int GetCurrentWeaponReserveAmmo()
    {
        
        // Return the reserve rounds for the equipped weapon.
        return currentWeaponReserveAmmo;
    }


    public void SetEquippedInventoryWeaponInstanceId(string instanceId)
    {
        // Bind this equipped weapon to a concrete inventory instance id.
        equippedInventoryWeaponInstanceId = string.IsNullOrWhiteSpace(instanceId) ? string.Empty : instanceId.Trim();
    }


    public string GetEquippedInventoryWeaponInstanceId()
    {
        // Return the bound inventory instance id (empty when not bound).
        return equippedInventoryWeaponInstanceId;
    }


    private bool IsEquipStateInProgress(AnimatorStateInfo stateInfo)
    {
        int stateHash = stateInfo.shortNameHash;
        bool isEquipOrUnequipState =
            stateHash == knifeEquipStateHash ||
            stateHash == knifeUnequipStateHash ||
            stateHash == twoHandedEquipStateHash ||
            stateHash == twoHandedUnequipStateHash ||
            stateHash == pistolEquipStateHash ||
            stateHash == pistolUnequipStateHash ||
            stateHash == longarmEquipStateHash ||
            stateHash == longarmUnequipStateHash;

        // Equip/unequip is considered active while the clip is still in progress.
        return isEquipOrUnequipState && stateInfo.normalizedTime < 1f;
    }


    public void SetCurrentWeaponAmmo(int ammo)
    {
        
        // Clamp to zero or above.
        currentWeaponAmmo = Mathf.Max(0, ammo);

        // Save back into the tracked weapon ammo record.
        SaveTrackedAmmoValues();
    }


    public void SetCurrentWeaponReserveAmmo(int ammo)
    {
        
        // Clamp to zero or above.
        currentWeaponReserveAmmo = Mathf.Max(0, ammo);

        // Save back into the tracked weapon ammo record.
        SaveTrackedAmmoValues();
    }


    public void HideEquippedWeaponInHandImmediate()
    {
        // Force all known in-hand weapon visuals off before a weapon swap.
        SetWeaponActive(combatKnife, false);
        SetWeaponActive(kitchenKnife, false);
        SetWeaponActive(straightRazor, false);
        SetWeaponActive(cricketBat, false);
        SetWeaponActive(leadPipe, false);
        SetWeaponActive(cane, false);
        SetWeaponActive(fellingAxe, false);
        SetWeaponActive(shovel, false);
        SetWeaponActive(selfLoadingPistol, false);
        SetWeaponActive(revolver, false);
        SetWeaponActive(laserPistol, false);
        SetWeaponActive(submachineGun, false);
        SetWeaponActive(huntingRifle, false);
        SetWeaponActive(battleRifle, false);
        SetWeaponActive(lightMachineGun, false);
        SetWeaponActive(laserRifle, false);
        SetWeaponActive(sniperRifle, false);
        SetWeaponActive(doubleBarrelShotgun, false);
        SetWeaponActive(pumpActionShotgun, false);
        SetWeaponActive(knuckleReadyLeft, false);
        SetWeaponActive(knuckleReadyRight, false);
    }
    

    private void SyncCategoryToPlayerState(WeaponCategory category)
    {
        
        // Stop if we do not have a PlayerState reference.
        if (!playerState) return;
    }
    

    private void UpdateCategoryBooleans(WeaponCategory activeCategory)
    {
        
        // Reset Unarmed tracking.
        EquippedUnarmed = false;

        // Reset Knife tracking.
        EquippedKnife = false;

        // Reset TwoHanded tracking.
        EquippedTwoHanded = false;

        // Reset Bow tracking.
        EquippedBow = false;

        // Reset Pistol tracking.
        EquippedPistol = false;

        // Reset SubmachineGun tracking.
        EquippedSubmachineGun = false;

        // Reset Rifle tracking.
        EquippedRifle = false;

        // Reset Shotgun tracking.
        EquippedShotgun = false;

        // Reset Longarm tracking.
        EquippedLongarm = false;

        // Reset Special tracking.
        EquippedSpecial = false;

        // Reset Explosive tracking.
        EquippedExplosive = false;

        // Enable only the active category.
        switch (activeCategory)
        {
            
            // Set Unarmed active.
            case WeaponCategory.Unarmed:
                EquippedUnarmed = true;
                break;

            // Set Knife active.
            case WeaponCategory.Knife:
                EquippedKnife = true;
                break;

            // Set TwoHanded active.
            case WeaponCategory.TwoHanded:
                EquippedTwoHanded = true;
                break;

            // Set Bow active.
            case WeaponCategory.Bow:
                EquippedBow = true;
                break;

            // Set Pistol active.
            case WeaponCategory.Pistol:
                EquippedPistol = true;
                break;

            // Set SubmachineGun active.
            case WeaponCategory.SubmachineGun:
                EquippedSubmachineGun = true;
                break;

            // Set Rifle active.
            case WeaponCategory.Rifle:
                EquippedRifle = true;
                break;

            // Set Shotgun active.
            case WeaponCategory.Shotgun:
                EquippedShotgun = true;
                break;

            // Set Special active.
            case WeaponCategory.Special:
                EquippedSpecial = true;
                break;

            // Set Explosive active.
            case WeaponCategory.Explosive:
                EquippedExplosive = true;
                break;
        }

        EquippedLongarm = EquippedSubmachineGun || EquippedRifle || EquippedShotgun;
    }
    

    private void UpdateAnimatorCategoryParameters()
    {
        
        // Stop if we do not have an animator.
        if (!animator) return;

        // Set EquippedUnarmed parameter.
        animator.SetBool(EquippedUnarmedParam, EquippedUnarmed);

        // Set EquippedKnife parameter.
        animator.SetBool(EquippedKnifeParam, EquippedKnife);

        // Set EquippedTwoHanded parameter.
        animator.SetBool(EquippedTwoHandedParam, EquippedTwoHanded);

        // Set EquippedBow parameter.
        animator.SetBool(EquippedBowParam, EquippedBow);

        // Set EquippedPistol parameter.
        animator.SetBool(EquippedPistolParam, EquippedPistol);

        // Set EquippedSubmachineGun parameter.
        animator.SetBool(EquippedSubmachineGunParam, EquippedSubmachineGun);

        // Set EquippedRifle parameter.
        animator.SetBool(EquippedRifleParam, EquippedRifle);

        // Set EquippedShotgun parameter.
        animator.SetBool(EquippedShotgunParam, EquippedShotgun);

        // Set EquippedLongarm parameter.
        animator.SetBool(EquippedLongarmParam, EquippedLongarm);

        // Set EquippedSpecial parameter.
        animator.SetBool(EquippedSpecialParam, EquippedSpecial);

        // Set EquippedExplosive parameter.
        animator.SetBool(EquippedExplosiveParam, EquippedExplosive);
    }

    
    private GameObject FindWeaponModel(string weaponName)
    {

        // Stop if we do not have a weapon holder.
        if (!weaponHolder) return null;

        // Search for a direct child first.
        Transform directChild = weaponHolder.Find(weaponName);
        if (directChild) return directChild.gameObject;

        // Search nested children for the named weapon.
        Transform[] children = weaponHolder.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == weaponName)
                return children[i].gameObject;
        }

        return null;
    }


    private GameObject FindHolsteredWeaponModel(string weaponName)
    {

        // Stop if we do not have a weapon holster.
        if (!weaponHolster) return null;

        // Search for a direct child first.
        Transform directChild = weaponHolster.Find(weaponName);
        if (directChild) return directChild.gameObject;

        // Search nested children for the named weapon.
        Transform[] children = weaponHolster.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == weaponName)
                return children[i].gameObject;
        }

        return null;
    }


    private GameObject FindModelAnywhere(string modelName)
    {

        Transform[] children = transform.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == modelName)
                return children[i].gameObject;
        }

        return null;
    }

    
    private void SetWeaponActive(GameObject weaponModel, bool isActive)
    {

        // Stop if the weapon model is missing.
        if (!weaponModel) return;

        // Only toggle when a change is needed.
        if (weaponModel.activeSelf == isActive) return;

        weaponModel.SetActive(isActive);
    }

    
    private void CacheStateHashes()
    {

        // Cache hashes for quick comparison.
        knifeEquipStateHash = Animator.StringToHash(knifeAnimatorSettings.EquipStateName);
        knifeUnequipStateHash = Animator.StringToHash(knifeAnimatorSettings.UnequipStateName);
        twoHandedEquipStateHash = Animator.StringToHash(twoHandedAnimatorSettings.EquipStateName);
        twoHandedUnequipStateHash = Animator.StringToHash(twoHandedAnimatorSettings.UnequipStateName);
        pistolEquipStateHash = Animator.StringToHash(pistolAnimatorSettings.EquipStateName);
        pistolUnequipStateHash = Animator.StringToHash(pistolAnimatorSettings.UnequipStateName);
        longarmEquipStateHash = Animator.StringToHash(longarmAnimatorSettings.EquipStateName);
        longarmUnequipStateHash = Animator.StringToHash(longarmAnimatorSettings.UnequipStateName);
    }

    
    private void UpdateKnifeVisibilityFromAnimator()
    {

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        int currentStateHash = stateInfo.shortNameHash;

        if (currentStateHash != lastAnimatorStateHash)
        {
            lastAnimatorStateHash = currentStateHash;
            knifeEquipTriggered = false;
            knifeUnequipTriggered = false;
            knifeHolsterDisableTriggered = false;
        }

        if (!knifeHolsterReEnableScheduled && currentStateHash == knifeUnequipStateHash)
        {
            // Schedule re-enable from unequip start using normalized 0..1 timing.
            float stateDuration = Mathf.Max(0f, stateInfo.length);
            knifeHolsterReEnableScheduled = true;
            knifeHolsterReEnableTriggered = false;
            knifeHolsterReEnableTime = Time.time + (stateDuration * knifeAnimatorSettings.EquipHolsterReEnableTime);
        }

        float normalizedTime = stateInfo.normalizedTime;

        if (!knifeEquipTriggered && currentStateHash == knifeEquipStateHash
            && normalizedTime >= knifeAnimatorSettings.EquipEnableTime)
        {
            knifeEquipTriggered = true;
            SetWeaponActive(GetEquippedKnifeModel(), true);

            // Equip animation reached hand-off point.
            if (playerState)
                playerState.SetWeaponInHand(true);
        }

        if (!knifeHolsterDisableTriggered && currentStateHash == knifeEquipStateHash
            && normalizedTime >= knifeAnimatorSettings.EquipHolsterDisableTime)
        {
            knifeHolsterDisableTriggered = true;
            DisableEquippedHolsterKnife();
        }

        if (!knifeUnequipTriggered && currentStateHash == knifeUnequipStateHash
            && normalizedTime >= knifeAnimatorSettings.UnequipDisableTime)
        {
            knifeUnequipTriggered = true;
            SetWeaponActive(GetEquippedKnifeModel(), false);

            // Unequip animation reached holster-off point.
            if (playerState)
                playerState.SetWeaponInHand(false);
        }

        if (knifeHolsterReEnableScheduled && !knifeHolsterReEnableTriggered
            && Time.time >= knifeHolsterReEnableTime)
        {
            knifeHolsterReEnableTriggered = true;
            knifeHolsterReEnableScheduled = false;
            ReEnablePendingHolsterKnife();
        }
    }


    private void UpdateHolsterVisibility()
    {

        // Keep only the currently selected supported weapon visible in the holster.
        bool canShowHolsteredByCombatState = !playerState
            || !playerState.GetCombatMode()
            || !playerState.GetWeaponInHand();

        bool showHolsteredCombatKnife = IsCurrentWeaponName(CombatKnifeWeaponName);
        bool showHolsteredKitchenKnife = IsCurrentWeaponName(KitchenKnifeWeaponName);
        bool showHolsteredStraightRazor = IsCurrentWeaponName(StraightRazorWeaponName);
        bool showHolsteredSelfLoadingPistol = IsCurrentWeaponName(SelfLoadingPistolWeaponName)
            && canShowHolsteredByCombatState;
        bool showHolsteredRevolver = IsCurrentWeaponName(RevolverWeaponName)
            && canShowHolsteredByCombatState;
        bool showHolsteredLaserPistol = IsCurrentWeaponName(LaserPistolWeaponName)
            && canShowHolsteredByCombatState;
        bool showHolsteredSubmachineGun = IsCurrentWeaponName(SubmachineGunWeaponName)
            && canShowHolsteredByCombatState;
        bool showHolsteredCricketBat = IsCurrentWeaponName(CricketBatWeaponName)
            && canShowHolsteredByCombatState;
        bool showHolsteredLeadPipe = IsCurrentWeaponName(LeadPipeWeaponName)
            && canShowHolsteredByCombatState;
        bool showHolsteredCane = IsCurrentWeaponName(CaneWeaponName)
            && canShowHolsteredByCombatState;
        bool showHolsteredFellingAxe = IsCurrentWeaponName(FellingAxeWeaponName)
            && canShowHolsteredByCombatState;
        bool showHolsteredShovel = IsCurrentWeaponName(ShovelWeaponName)
            && canShowHolsteredByCombatState;
        bool showHolsteredHuntingRifle = IsCurrentWeaponName(HuntingRifleWeaponName)
            && canShowHolsteredByCombatState;
        bool showHolsteredBattleRifle = IsCurrentWeaponName(BattleRifleWeaponName)
            && canShowHolsteredByCombatState;
        bool showHolsteredLightMachineGun = IsCurrentWeaponName(LightMachineGunWeaponName)
            && canShowHolsteredByCombatState;
        bool showHolsteredLaserRifle = IsCurrentWeaponName(LaserRifleWeaponName)
            && canShowHolsteredByCombatState;
        bool showHolsteredSniperRifle = IsCurrentWeaponName(SniperRifleWeaponName)
            && canShowHolsteredByCombatState;
        bool showHolsteredDoubleBarrelShotgun = IsCurrentWeaponName(DoubleBarrelShotgunWeaponName)
            && canShowHolsteredByCombatState;
        bool showHolsteredPumpActionShotgun = IsCurrentWeaponName(PumpActionShotgunWeaponName)
            && canShowHolsteredByCombatState;
        SetWeaponActive(holsteredCombatKnife, showHolsteredCombatKnife);
        SetWeaponActive(holsteredKitchenKnife, showHolsteredKitchenKnife);
        SetWeaponActive(holsteredStraightRazor, showHolsteredStraightRazor);
        SetWeaponActive(holsteredSelfLoadingPistol, showHolsteredSelfLoadingPistol);
        SetWeaponActive(holsteredRevolver, showHolsteredRevolver);
        SetWeaponActive(holsteredLaserPistol, showHolsteredLaserPistol);
        SetWeaponActive(holsteredSubmachineGun, showHolsteredSubmachineGun);
        SetWeaponActive(holsteredHuntingRifle, showHolsteredHuntingRifle);
        SetWeaponActive(holsteredBattleRifle, showHolsteredBattleRifle);
        SetWeaponActive(holsteredLightMachineGun, showHolsteredLightMachineGun);
        SetWeaponActive(holsteredLaserRifle, showHolsteredLaserRifle);
        SetWeaponActive(holsteredSniperRifle, showHolsteredSniperRifle);
        SetWeaponActive(holsteredDoubleBarrelShotgun, showHolsteredDoubleBarrelShotgun);
        SetWeaponActive(holsteredPumpActionShotgun, showHolsteredPumpActionShotgun);
        SetWeaponActive(holsteredCricketBat, showHolsteredCricketBat);
        SetWeaponActive(holsteredLeadPipe, showHolsteredLeadPipe);
        SetWeaponActive(holsteredCane, showHolsteredCane);
        SetWeaponActive(holsteredFellingAxe, showHolsteredFellingAxe);
        SetWeaponActive(holsteredShovel, showHolsteredShovel);
    }


    private void UpdateKnuckleVisibility()
    {

        bool isInCombat = playerState && playerState.GetCombatMode();
        bool isKnuckleDustersSelected = IsCurrentWeaponName(KnuckleDustersWeaponName);
        bool isKnuckleDustersEquipped = isKnuckleDustersSelected && isInCombat;
        bool showIdleKnuckles = isKnuckleDustersSelected && !isKnuckleDustersEquipped;
        bool showReadyKnuckles = isKnuckleDustersEquipped;

        SetWeaponActive(knuckleIdleLeft, showIdleKnuckles);
        SetWeaponActive(knuckleIdleRight, showIdleKnuckles);
        SetWeaponActive(knuckleReadyLeft, showReadyKnuckles);
        SetWeaponActive(knuckleReadyRight, showReadyKnuckles);
    }


    private void UpdateTwoHandedVisibilityFromAnimator()
    {

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        int currentStateHash = stateInfo.shortNameHash;

        if (currentStateHash != lastTwoHandedAnimatorStateHash)
        {
            lastTwoHandedAnimatorStateHash = currentStateHash;
            twoHandedEquipTriggered = false;
            twoHandedUnequipTriggered = false;
            twoHandedHolsterDisableTriggered = false;
        }

        if (!twoHandedHolsterReEnableScheduled && currentStateHash == twoHandedUnequipStateHash)
        {
            // Schedule re-enable from unequip start using normalized 0..1 timing.
            float stateDuration = Mathf.Max(0f, stateInfo.length);
            twoHandedHolsterReEnableScheduled = true;
            twoHandedHolsterReEnableTriggered = false;
            twoHandedHolsterReEnableTime = Time.time + (stateDuration * twoHandedAnimatorSettings.EquipHolsterReEnableTime);
        }

        float normalizedTime = stateInfo.normalizedTime;

        if (!twoHandedEquipTriggered && currentStateHash == twoHandedEquipStateHash
            && normalizedTime >= twoHandedAnimatorSettings.EquipEnableTime)
        {
            twoHandedEquipTriggered = true;
            SetWeaponActive(GetEquippedTwoHandedModel(), true);

            // Equip animation reached hand-off point.
            if (playerState)
                playerState.SetWeaponInHand(true);
        }

        if (!twoHandedHolsterDisableTriggered && currentStateHash == twoHandedEquipStateHash
            && normalizedTime >= twoHandedAnimatorSettings.EquipHolsterDisableTime)
        {
            twoHandedHolsterDisableTriggered = true;
            DisableEquippedHolsterTwoHanded();
        }

        if (!twoHandedUnequipTriggered && currentStateHash == twoHandedUnequipStateHash
            && normalizedTime >= twoHandedAnimatorSettings.UnequipDisableTime)
        {
            twoHandedUnequipTriggered = true;
            SetWeaponActive(GetEquippedTwoHandedModel(), false);

            // Unequip animation reached holster-off point.
            if (playerState)
                playerState.SetWeaponInHand(false);
        }

        if (twoHandedHolsterReEnableScheduled && !twoHandedHolsterReEnableTriggered
            && Time.time >= twoHandedHolsterReEnableTime)
        {
            twoHandedHolsterReEnableTriggered = true;
            twoHandedHolsterReEnableScheduled = false;
            ReEnablePendingHolsterTwoHanded();
        }
    }


    private void UpdatePistolVisibilityFromAnimator()
    {

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        int currentStateHash = stateInfo.shortNameHash;

        if (currentStateHash != lastPistolAnimatorStateHash)
        {
            lastPistolAnimatorStateHash = currentStateHash;
            pistolEquipTriggered = false;
            pistolUnequipTriggered = false;
            pistolHolsterDisableTriggered = false;
        }

        if (!pistolHolsterReEnableScheduled && currentStateHash == pistolUnequipStateHash)
        {
            // Schedule re-enable from unequip start using normalized 0..1 timing.
            float stateDuration = Mathf.Max(0f, stateInfo.length);
            pistolHolsterReEnableScheduled = true;
            pistolHolsterReEnableTriggered = false;
            pistolHolsterReEnableTime = Time.time + (stateDuration * pistolAnimatorSettings.EquipHolsterReEnableTime);
        }

        float normalizedTime = stateInfo.normalizedTime;

        if (!pistolEquipTriggered && currentStateHash == pistolEquipStateHash
            && normalizedTime >= pistolAnimatorSettings.EquipEnableTime)
        {
            pistolEquipTriggered = true;
            SetWeaponActive(GetEquippedPistolModel(), true);

            // Equip animation reached hand-off point.
            if (playerState)
                playerState.SetWeaponInHand(true);
        }

        if (!pistolHolsterDisableTriggered && currentStateHash == pistolEquipStateHash
            && normalizedTime >= pistolAnimatorSettings.EquipHolsterDisableTime)
        {
            pistolHolsterDisableTriggered = true;
            DisableEquippedHolsterPistol();
        }

        if (!pistolUnequipTriggered && currentStateHash == pistolUnequipStateHash
            && normalizedTime >= pistolAnimatorSettings.UnequipDisableTime)
        {
            pistolUnequipTriggered = true;
            SetWeaponActive(GetEquippedPistolModel(), false);

            // Unequip animation reached holster-off point.
            if (playerState)
                playerState.SetWeaponInHand(false);
        }

        if (pistolHolsterReEnableScheduled && !pistolHolsterReEnableTriggered
            && Time.time >= pistolHolsterReEnableTime)
        {
            pistolHolsterReEnableTriggered = true;
            pistolHolsterReEnableScheduled = false;
            ReEnablePendingHolsterPistol();
        }
    }


    private void UpdateLongarmVisibilityFromAnimator()
    {

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        int currentStateHash = stateInfo.shortNameHash;

        if (currentStateHash != lastLongarmAnimatorStateHash)
        {
            lastLongarmAnimatorStateHash = currentStateHash;
            longarmEquipTriggered = false;
            longarmUnequipTriggered = false;
            longarmHolsterDisableTriggered = false;
        }

        if (!longarmHolsterReEnableScheduled && currentStateHash == longarmUnequipStateHash)
        {
            // Schedule re-enable from unequip start using normalized 0..1 timing.
            float stateDuration = Mathf.Max(0f, stateInfo.length);
            longarmHolsterReEnableScheduled = true;
            longarmHolsterReEnableTriggered = false;
            longarmHolsterReEnableTime = Time.time + (stateDuration * longarmAnimatorSettings.EquipHolsterReEnableTime);
        }

        float normalizedTime = stateInfo.normalizedTime;

        if (!longarmEquipTriggered && currentStateHash == longarmEquipStateHash
            && normalizedTime >= longarmAnimatorSettings.EquipEnableTime)
        {
            longarmEquipTriggered = true;
            SetWeaponActive(GetEquippedLongarmModel(), true);

            // Equip animation reached hand-off point.
            if (playerState)
                playerState.SetWeaponInHand(true);
        }

        if (!longarmHolsterDisableTriggered && currentStateHash == longarmEquipStateHash
            && normalizedTime >= longarmAnimatorSettings.EquipHolsterDisableTime)
        {
            longarmHolsterDisableTriggered = true;
            DisableEquippedHolsterLongarm();
        }

        if (!longarmUnequipTriggered && currentStateHash == longarmUnequipStateHash
            && normalizedTime >= longarmAnimatorSettings.UnequipDisableTime)
        {
            longarmUnequipTriggered = true;
            SetWeaponActive(GetEquippedLongarmModel(), false);

            // Unequip animation reached holster-off point.
            if (playerState)
                playerState.SetWeaponInHand(false);
        }

        if (longarmHolsterReEnableScheduled && !longarmHolsterReEnableTriggered
            && Time.time >= longarmHolsterReEnableTime)
        {
            longarmHolsterReEnableTriggered = true;
            longarmHolsterReEnableScheduled = false;
            ReEnablePendingHolsterLongarm();
        }
    }


    private void UpdateWeaponInHandFromEquipCompletion()
    {

        // Stop if we do not have a player state target.
        if (!playerState) return;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        int currentStateHash = stateInfo.shortNameHash;
        float normalizedTime = stateInfo.normalizedTime;
        bool isEquipCompleteState = normalizedTime >= 1f
            && (currentStateHash == knifeEquipStateHash
                || currentStateHash == twoHandedEquipStateHash
                || currentStateHash == pistolEquipStateHash);
        bool isLongarmEquipCompleteState = normalizedTime >= longarmAnimatorSettings.EquipEnableTime
            && currentStateHash == longarmEquipStateHash;
        bool isLongarmUnequipCompleteState = normalizedTime >= longarmAnimatorSettings.UnequipDisableTime
            && currentStateHash == longarmUnequipStateHash;

        if (isEquipCompleteState || isLongarmEquipCompleteState)
        {
            // Equip animation finished, so weapon should be considered in hand.
            playerState.SetWeaponInHand(true);
            return;
        }

        if (!isLongarmUnequipCompleteState) return;

        // Longarm unequip animation finished, so weapon is no longer in hand.
        playerState.SetWeaponInHand(false);
    }


    private void UpdateCricketBatVisibility()
    {

        bool isInCombat = playerState && playerState.GetCombatMode();
        bool isCricketBatSelected = IsCurrentWeaponName(CricketBatWeaponName);
        bool isCricketBatEquipped = isCricketBatSelected && isInCombat;
        bool isLeadPipeSelected = IsCurrentWeaponName(LeadPipeWeaponName);
        bool isLeadPipeEquipped = isLeadPipeSelected && isInCombat;
        bool isCaneSelected = IsCurrentWeaponName(CaneWeaponName);
        bool isCaneEquipped = isCaneSelected && isInCombat;
        bool isFellingAxeSelected = IsCurrentWeaponName(FellingAxeWeaponName);
        bool isFellingAxeEquipped = isFellingAxeSelected && isInCombat;
        bool isShovelSelected = IsCurrentWeaponName(ShovelWeaponName);
        bool isShovelEquipped = isShovelSelected && isInCombat;
        bool showHolsteredCricketBat = isCricketBatSelected && !isCricketBatEquipped;
        bool showHolsteredLeadPipe = isLeadPipeSelected && !isLeadPipeEquipped;
        bool showHolsteredCane = isCaneSelected && !isCaneEquipped;
        bool showHolsteredFellingAxe = isFellingAxeSelected && !isFellingAxeEquipped;
        bool showHolsteredShovel = isShovelSelected && !isShovelEquipped;
        int currentAnimatorStateHash = animator ? animator.GetCurrentAnimatorStateInfo(0).shortNameHash : 0;
        bool isTwoHandedAnimatorState = animator
            && (currentAnimatorStateHash == twoHandedEquipStateHash
                || currentAnimatorStateHash == twoHandedUnequipStateHash);

        // Let timed two-handed equip/unequip logic control visible swaps while selected.
        if (isCricketBatSelected || isLeadPipeSelected || isCaneSelected || isFellingAxeSelected || isShovelSelected)
        {
            // During dedicated equip/unequip states, UpdateTwoHandedVisibilityFromAnimator handles timing.
            if (isTwoHandedAnimatorState) return;

            // While selected, keep cricket bat visibility fully animation-driven for both
            // equip (combat on) and unequip (combat off) transitions.
            return;
        }

        // Outside the timed path (or when no longer selected), use immediate visibility.
        SetWeaponActive(cricketBat, isCricketBatEquipped);
        SetWeaponActive(holsteredCricketBat, showHolsteredCricketBat);
        SetWeaponActive(leadPipe, isLeadPipeEquipped);
        SetWeaponActive(holsteredLeadPipe, showHolsteredLeadPipe);
        SetWeaponActive(cane, isCaneEquipped);
        SetWeaponActive(holsteredCane, showHolsteredCane);
        SetWeaponActive(fellingAxe, isFellingAxeEquipped);
        SetWeaponActive(holsteredFellingAxe, showHolsteredFellingAxe);
        SetWeaponActive(shovel, isShovelEquipped);
        SetWeaponActive(holsteredShovel, showHolsteredShovel);
    }


    private void UpdatePistolVisibility()
    {

        bool isInCombat = playerState && playerState.GetCombatMode();
        bool isSelfLoadingPistolSelected = IsCurrentWeaponName(SelfLoadingPistolWeaponName);
        bool isRevolverSelected = IsCurrentWeaponName(RevolverWeaponName);
        bool isLaserPistolSelected = IsCurrentWeaponName(LaserPistolWeaponName);
        bool isSelfLoadingPistolEquipped = isSelfLoadingPistolSelected && isInCombat;
        bool isRevolverEquipped = isRevolverSelected && isInCombat;
        bool isLaserPistolEquipped = isLaserPistolSelected && isInCombat;
        bool showHolsteredSelfLoadingPistol = isSelfLoadingPistolSelected && !isSelfLoadingPistolEquipped;
        bool showHolsteredRevolver = isRevolverSelected && !isRevolverEquipped;
        bool showHolsteredLaserPistol = isLaserPistolSelected && !isLaserPistolEquipped;
        int currentAnimatorStateHash = animator ? animator.GetCurrentAnimatorStateInfo(0).shortNameHash : 0;
        bool isPistolAnimatorState = animator
            && (currentAnimatorStateHash == pistolEquipStateHash
                || currentAnimatorStateHash == pistolUnequipStateHash);

        // Let timed pistol equip/unequip logic control visible swaps while selected.
        if (isSelfLoadingPistolSelected || isRevolverSelected || isLaserPistolSelected)
        {
            // During dedicated equip/unequip states, UpdatePistolVisibilityFromAnimator handles timing.
            if (isPistolAnimatorState) return;

            // While selected, keep pistol visibility fully animation-driven for both
            // equip (combat on) and unequip (combat off) transitions.
            return;
        }

        // Outside the timed path (or when no longer selected), use immediate visibility.
        SetWeaponActive(selfLoadingPistol, isSelfLoadingPistolEquipped);
        SetWeaponActive(holsteredSelfLoadingPistol, showHolsteredSelfLoadingPistol);
        SetWeaponActive(revolver, isRevolverEquipped);
        SetWeaponActive(holsteredRevolver, showHolsteredRevolver);
        SetWeaponActive(laserPistol, isLaserPistolEquipped);
        SetWeaponActive(holsteredLaserPistol, showHolsteredLaserPistol);
    }


    private void UpdateRifleVisibility()
    {

        bool isInCombat = playerState && playerState.GetCombatMode();
        bool isSubmachineGunSelected = IsCurrentWeaponName(SubmachineGunWeaponName);
        bool isSubmachineGunEquipped = isSubmachineGunSelected && isInCombat;
        bool showHolsteredSubmachineGun = isSubmachineGunSelected && !isSubmachineGunEquipped;
        bool isHuntingRifleSelected = IsCurrentWeaponName(HuntingRifleWeaponName);
        bool isHuntingRifleEquipped = isHuntingRifleSelected && isInCombat;
        bool showHolsteredHuntingRifle = isHuntingRifleSelected && !isHuntingRifleEquipped;
        bool isBattleRifleSelected = IsCurrentWeaponName(BattleRifleWeaponName);
        bool isBattleRifleEquipped = isBattleRifleSelected && isInCombat;
        bool showHolsteredBattleRifle = isBattleRifleSelected && !isBattleRifleEquipped;
        bool isLightMachineGunSelected = IsCurrentWeaponName(LightMachineGunWeaponName);
        bool isLightMachineGunEquipped = isLightMachineGunSelected && isInCombat;
        bool showHolsteredLightMachineGun = isLightMachineGunSelected && !isLightMachineGunEquipped;
        bool isLaserRifleSelected = IsCurrentWeaponName(LaserRifleWeaponName);
        bool isLaserRifleEquipped = isLaserRifleSelected && isInCombat;
        bool showHolsteredLaserRifle = isLaserRifleSelected && !isLaserRifleEquipped;
        bool isSniperRifleSelected = IsCurrentWeaponName(SniperRifleWeaponName);
        bool isSniperRifleEquipped = isSniperRifleSelected && isInCombat;
        bool showHolsteredSniperRifle = isSniperRifleSelected && !isSniperRifleEquipped;
        bool isDoubleBarrelShotgunSelected = IsCurrentWeaponName(DoubleBarrelShotgunWeaponName);
        bool isDoubleBarrelShotgunEquipped = isDoubleBarrelShotgunSelected && isInCombat;
        bool showHolsteredDoubleBarrelShotgun = isDoubleBarrelShotgunSelected && !isDoubleBarrelShotgunEquipped;
        bool isPumpActionShotgunSelected = IsCurrentWeaponName(PumpActionShotgunWeaponName);
        bool isPumpActionShotgunEquipped = isPumpActionShotgunSelected && isInCombat;
        bool showHolsteredPumpActionShotgun = isPumpActionShotgunSelected && !isPumpActionShotgunEquipped;
        int currentAnimatorStateHash = animator ? animator.GetCurrentAnimatorStateInfo(0).shortNameHash : 0;
        bool isLongarmAnimatorState = animator
            && (currentAnimatorStateHash == longarmEquipStateHash
                || currentAnimatorStateHash == longarmUnequipStateHash);

        // Let timed longarm equip/unequip logic control visible swaps while selected.
        if (isSubmachineGunSelected || isHuntingRifleSelected || isBattleRifleSelected || isLightMachineGunSelected || isLaserRifleSelected || isSniperRifleSelected || isDoubleBarrelShotgunSelected || isPumpActionShotgunSelected)
        {
            // During dedicated equip/unequip states, UpdateLongarmVisibilityFromAnimator handles timing.
            if (isLongarmAnimatorState) return;

            // While selected, keep longarm visibility fully animation-driven for both
            // equip (combat on) and unequip (combat off) transitions.
            return;
        }

        SetWeaponActive(submachineGun, isSubmachineGunEquipped);
        SetWeaponActive(holsteredSubmachineGun, showHolsteredSubmachineGun);
        SetWeaponActive(huntingRifle, isHuntingRifleEquipped);
        SetWeaponActive(holsteredHuntingRifle, showHolsteredHuntingRifle);
        SetWeaponActive(battleRifle, isBattleRifleEquipped);
        SetWeaponActive(holsteredBattleRifle, showHolsteredBattleRifle);
        SetWeaponActive(lightMachineGun, isLightMachineGunEquipped);
        SetWeaponActive(holsteredLightMachineGun, showHolsteredLightMachineGun);
        SetWeaponActive(laserRifle, isLaserRifleEquipped);
        SetWeaponActive(holsteredLaserRifle, showHolsteredLaserRifle);
        SetWeaponActive(sniperRifle, isSniperRifleEquipped);
        SetWeaponActive(holsteredSniperRifle, showHolsteredSniperRifle);
        SetWeaponActive(doubleBarrelShotgun, isDoubleBarrelShotgunEquipped);
        SetWeaponActive(holsteredDoubleBarrelShotgun, showHolsteredDoubleBarrelShotgun);
        SetWeaponActive(pumpActionShotgun, isPumpActionShotgunEquipped);
        SetWeaponActive(holsteredPumpActionShotgun, showHolsteredPumpActionShotgun);
    }


    private GameObject GetEquippedKnifeModel()
    {

        // Return the active in-hand knife model matching the equipped weapon.
        if (IsCurrentWeaponName(CombatKnifeWeaponName))
            return combatKnife;

        if (IsCurrentWeaponName(KitchenKnifeWeaponName))
            return kitchenKnife;

        if (IsCurrentWeaponName(StraightRazorWeaponName))
            return straightRazor;

        return null;
    }


    private GameObject GetEquippedPistolModel()
    {

        // Return the active in-hand pistol model matching the equipped weapon.
        if (IsCurrentWeaponName(SelfLoadingPistolWeaponName))
            return selfLoadingPistol;

        if (IsCurrentWeaponName(RevolverWeaponName))
            return revolver;

        if (IsCurrentWeaponName(LaserPistolWeaponName))
            return laserPistol ? laserPistol : selfLoadingPistol;

        return null;
    }


    private GameObject GetEquippedLongarmModel()
    {

        // Return the active in-hand longarm model matching the equipped weapon.
        if (IsCurrentWeaponName(SubmachineGunWeaponName))
            return submachineGun;

        if (IsCurrentWeaponName(HuntingRifleWeaponName))
            return huntingRifle;

        if (IsCurrentWeaponName(BattleRifleWeaponName))
            return battleRifle;

        if (IsCurrentWeaponName(LightMachineGunWeaponName))
            return lightMachineGun;

        if (IsCurrentWeaponName(LaserRifleWeaponName))
            return laserRifle;

        if (IsCurrentWeaponName(SniperRifleWeaponName))
            return sniperRifle;

        if (IsCurrentWeaponName(DoubleBarrelShotgunWeaponName))
            return doubleBarrelShotgun;

        if (IsCurrentWeaponName(PumpActionShotgunWeaponName))
            return pumpActionShotgun;

        return null;
    }


    private void DisableEquippedHolsterKnife()
    {

        // Disable only the holstered knife that matches the current equipped weapon.
        if (IsCurrentWeaponName(CombatKnifeWeaponName))
        {
            holsterKnifePendingReEnable = holsteredCombatKnife;
            SetWeaponActive(holsteredCombatKnife, false);
            return;
        }

        if (IsCurrentWeaponName(KitchenKnifeWeaponName))
        {
            holsterKnifePendingReEnable = holsteredKitchenKnife;
            SetWeaponActive(holsteredKitchenKnife, false);
            return;
        }

        if (IsCurrentWeaponName(StraightRazorWeaponName))
        {
            holsterKnifePendingReEnable = holsteredStraightRazor;
            SetWeaponActive(holsteredStraightRazor, false);
        }
    }


    private void ReEnablePendingHolsterKnife()
    {

        // Re-enable the same holstered knife that was disabled during equip.
        if (!holsterKnifePendingReEnable) return;

        SetWeaponActive(holsterKnifePendingReEnable, true);
        holsterKnifePendingReEnable = null;
    }


    private void DisableEquippedHolsterPistol()
    {

        // Disable only the holstered pistol that matches the current equipped weapon.
        if (IsCurrentWeaponName(SelfLoadingPistolWeaponName))
        {
            holsterPistolPendingReEnable = holsteredSelfLoadingPistol;
            SetWeaponActive(holsteredSelfLoadingPistol, false);
            return;
        }

        if (IsCurrentWeaponName(RevolverWeaponName))
        {
            holsterPistolPendingReEnable = holsteredRevolver;
            SetWeaponActive(holsteredRevolver, false);
            return;
        }

        if (IsCurrentWeaponName(LaserPistolWeaponName))
        {
            holsterPistolPendingReEnable = holsteredLaserPistol ? holsteredLaserPistol : holsteredSelfLoadingPistol;
            SetWeaponActive(holsterPistolPendingReEnable, false);
        }
    }


    private void ReEnablePendingHolsterPistol()
    {

        // Re-enable the same holstered pistol that was disabled during equip.
        if (!holsterPistolPendingReEnable) return;

        SetWeaponActive(holsterPistolPendingReEnable, true);
        holsterPistolPendingReEnable = null;
    }


    private void DisableEquippedHolsterLongarm()
    {

        // Disable only the holstered longarm weapon that matches the current equipped weapon.
        if (IsCurrentWeaponName(SubmachineGunWeaponName))
        {
            holsterLongarmPendingReEnable = holsteredSubmachineGun;
            SetWeaponActive(holsteredSubmachineGun, false);
            return;
        }

        if (IsCurrentWeaponName(HuntingRifleWeaponName))
        {
            holsterLongarmPendingReEnable = holsteredHuntingRifle;
            SetWeaponActive(holsteredHuntingRifle, false);
            return;
        }

        if (IsCurrentWeaponName(BattleRifleWeaponName))
        {
            holsterLongarmPendingReEnable = holsteredBattleRifle;
            SetWeaponActive(holsteredBattleRifle, false);
            return;
        }

        if (IsCurrentWeaponName(LightMachineGunWeaponName))
        {
            holsterLongarmPendingReEnable = holsteredLightMachineGun;
            SetWeaponActive(holsteredLightMachineGun, false);
            return;
        }

        if (IsCurrentWeaponName(LaserRifleWeaponName))
        {
            holsterLongarmPendingReEnable = holsteredLaserRifle;
            SetWeaponActive(holsteredLaserRifle, false);
            return;
        }

        if (IsCurrentWeaponName(SniperRifleWeaponName))
        {
            holsterLongarmPendingReEnable = holsteredSniperRifle;
            SetWeaponActive(holsteredSniperRifle, false);
            return;
        }

        if (IsCurrentWeaponName(DoubleBarrelShotgunWeaponName))
        {
            holsterLongarmPendingReEnable = holsteredDoubleBarrelShotgun;
            SetWeaponActive(holsteredDoubleBarrelShotgun, false);
            return;
        }

        if (IsCurrentWeaponName(PumpActionShotgunWeaponName))
        {
            holsterLongarmPendingReEnable = holsteredPumpActionShotgun;
            SetWeaponActive(holsteredPumpActionShotgun, false);
        }
    }


    private void ReEnablePendingHolsterLongarm()
    {

        // Re-enable the same holstered longarm weapon that was disabled during equip.
        if (!holsterLongarmPendingReEnable) return;

        SetWeaponActive(holsterLongarmPendingReEnable, true);
        holsterLongarmPendingReEnable = null;
    }


    private GameObject GetEquippedTwoHandedModel()
    {

        // Return the active in-hand two-handed model matching the equipped weapon.
        if (IsCurrentWeaponName(ShovelWeaponName))
            return shovel;

        if (IsCurrentWeaponName(FellingAxeWeaponName))
            return fellingAxe;

        if (IsCurrentWeaponName(CaneWeaponName))
            return cane;

        if (IsCurrentWeaponName(LeadPipeWeaponName))
            return leadPipe;

        if (IsCurrentWeaponName(CricketBatWeaponName))
            return cricketBat;

        return null;
    }


    private void DisableEquippedHolsterTwoHanded()
    {

        // Disable only the holstered two-handed weapon that matches the current equipped weapon.
        if (IsCurrentWeaponName(ShovelWeaponName))
        {
            holsterTwoHandedPendingReEnable = holsteredShovel;
            SetWeaponActive(holsteredShovel, false);
            return;
        }

        if (IsCurrentWeaponName(FellingAxeWeaponName))
        {
            holsterTwoHandedPendingReEnable = holsteredFellingAxe;
            SetWeaponActive(holsteredFellingAxe, false);
            return;
        }

        if (IsCurrentWeaponName(CaneWeaponName))
        {
            holsterTwoHandedPendingReEnable = holsteredCane;
            SetWeaponActive(holsteredCane, false);
            return;
        }

        if (IsCurrentWeaponName(LeadPipeWeaponName))
        {
            holsterTwoHandedPendingReEnable = holsteredLeadPipe;
            SetWeaponActive(holsteredLeadPipe, false);
            return;
        }

        if (IsCurrentWeaponName(CricketBatWeaponName))
        {
            holsterTwoHandedPendingReEnable = holsteredCricketBat;
            SetWeaponActive(holsteredCricketBat, false);
        }
    }


    private void ReEnablePendingHolsterTwoHanded()
    {

        // Re-enable the same holstered two-handed weapon that was disabled during equip.
        if (!holsterTwoHandedPendingReEnable) return;

        SetWeaponActive(holsterTwoHandedPendingReEnable, true);
        holsterTwoHandedPendingReEnable = null;
    }


    private bool IsCurrentWeaponName(string weaponName)
    {

        // Compare current weapon name safely.
        return currentWeapon != null
            && string.Equals(currentWeapon.WeaponName, weaponName, StringComparison.OrdinalIgnoreCase);
    }

    
    private void EnsureWeaponAmmoRecords()
    {
        
        // Create the list if missing.
        if (weaponAmmo == null) weaponAmmo = new List<WeaponAmmoEntry>();

        // Stop if we have no configured weapons.
        if (weapons == null || weapons.Count == 0) return;

        // Add missing records.
        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponEntry weaponEntry = weapons[i];
            if (weaponEntry == null) continue;
            if (string.IsNullOrWhiteSpace(weaponEntry.WeaponName)) continue;
            if (FindAmmoRecord(weaponEntry.Category, weaponEntry.WeaponName) != null) continue;

            weaponAmmo.Add(new WeaponAmmoEntry
            {
                Category = weaponEntry.Category,
                WeaponName = weaponEntry.WeaponName,
                CurrentAmmo = 0,
                ReserveAmmo = 0
            });
        }

        // Remove stale records that no longer map to a configured weapon.
        for (int i = weaponAmmo.Count - 1; i >= 0; i--)
        {
            WeaponAmmoEntry ammoEntry = weaponAmmo[i];
            if (ammoEntry == null)
            {
                weaponAmmo.RemoveAt(i);
                continue;
            }

            bool hasMatchingWeapon = false;
            for (int weaponIndex = 0; weaponIndex < weapons.Count; weaponIndex++)
            {
                WeaponEntry weaponEntry = weapons[weaponIndex];
                if (weaponEntry == null) continue;
                if (weaponEntry.Category != ammoEntry.Category) continue;
                if (!string.Equals(weaponEntry.WeaponName, ammoEntry.WeaponName, StringComparison.OrdinalIgnoreCase))
                    continue;

                hasMatchingWeapon = true;
                break;
            }

            if (!hasMatchingWeapon)
                weaponAmmo.RemoveAt(i);
        }
    }


    private WeaponAmmoEntry FindAmmoRecord(WeaponCategory category, string weaponName)
    {
        
        // Stop if the ammo list is missing.
        if (weaponAmmo == null) return null;

        // Stop if the weapon name is missing.
        if (string.IsNullOrWhiteSpace(weaponName)) return null;

        for (int i = 0; i < weaponAmmo.Count; i++)
        {
            WeaponAmmoEntry ammoEntry = weaponAmmo[i];
            if (ammoEntry == null) continue;
            if (ammoEntry.Category != category) continue;
            if (!string.Equals(ammoEntry.WeaponName, weaponName, StringComparison.OrdinalIgnoreCase)) continue;
            return ammoEntry;
        }

        return null;
    }


    private WeaponAmmoEntry GetOrCreateAmmoRecord(WeaponCategory category, string weaponName)
    {
        
        // Stop if the weapon name is missing.
        if (string.IsNullOrWhiteSpace(weaponName)) return null;

        // Create the list if missing.
        if (weaponAmmo == null) weaponAmmo = new List<WeaponAmmoEntry>();

        WeaponAmmoEntry ammoEntry = FindAmmoRecord(category, weaponName);
        if (ammoEntry != null) return ammoEntry;

        ammoEntry = new WeaponAmmoEntry
        {
            Category = category,
            WeaponName = weaponName,
            CurrentAmmo = 0,
            ReserveAmmo = 0
        };

        weaponAmmo.Add(ammoEntry);
        return ammoEntry;
    }


    private void SaveTrackedAmmoValues()
    {
        
        // Stop if no weapon is currently tracked by the inspector ammo fields.
        if (string.IsNullOrWhiteSpace(trackedAmmoWeaponName)) return;

        WeaponAmmoEntry ammoEntry = GetOrCreateAmmoRecord(trackedAmmoCategory, trackedAmmoWeaponName);
        if (ammoEntry == null) return;

        ammoEntry.CurrentAmmo = Mathf.Max(0, currentWeaponAmmo);
        ammoEntry.ReserveAmmo = Mathf.Max(0, currentWeaponReserveAmmo);
    }


    private void LoadTrackedAmmoValuesForCurrentWeapon()
    {
        
        // Stop if we do not have a current weapon.
        if (currentWeapon == null)
        {
            currentWeaponAmmo = 0;
            currentWeaponReserveAmmo = 0;
            trackedAmmoWeaponName = string.Empty;
            return;
        }

        WeaponAmmoEntry ammoEntry = GetOrCreateAmmoRecord(currentWeapon.Category, currentWeapon.WeaponName);
        if (ammoEntry == null)
        {
            currentWeaponAmmo = 0;
            currentWeaponReserveAmmo = 0;
            trackedAmmoWeaponName = string.Empty;
            return;
        }

        currentWeaponAmmo = Mathf.Max(0, ammoEntry.CurrentAmmo);
        currentWeaponReserveAmmo = Mathf.Max(0, ammoEntry.ReserveAmmo);
        trackedAmmoCategory = currentWeapon.Category;
        trackedAmmoWeaponName = currentWeapon.WeaponName;
    }


    private void EnsureDefaultWeaponList()
    {
        
        // Create the list if missing.
        if (weapons == null) weapons = new List<WeaponEntry>();

        // Stop if the list already contains entries (so we do not overwrite your setup).
        if (weapons.Count > 0) return;

        // Unarmed
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Unarmed, WeaponName = "Knuckle Dusters" });

        // Knife
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Knife, WeaponName = "Combat Knife" });

        weapons.Add(new WeaponEntry { Category = WeaponCategory.Knife, WeaponName = "Straight Razor" });

        weapons.Add(new WeaponEntry { Category = WeaponCategory.Knife, WeaponName = "Kitchen Knife" });

        // TwoHanded
        weapons.Add(new WeaponEntry { Category = WeaponCategory.TwoHanded, WeaponName = "Lead Pipe" });

        weapons.Add(new WeaponEntry { Category = WeaponCategory.TwoHanded, WeaponName = "Cricket Bat" });

        weapons.Add(new WeaponEntry { Category = WeaponCategory.TwoHanded, WeaponName = "Shovel" });

        weapons.Add(new WeaponEntry { Category = WeaponCategory.TwoHanded, WeaponName = "Felling Axe" });

        weapons.Add(new WeaponEntry { Category = WeaponCategory.TwoHanded, WeaponName = "Cane" });

        // Bow
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Bow, WeaponName = "Bow & Arrow" });

        // Pistol
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Pistol, WeaponName = "Self-Loading Pistol" });

        weapons.Add(new WeaponEntry { Category = WeaponCategory.Pistol, WeaponName = "Revolver" });

        weapons.Add(new WeaponEntry { Category = WeaponCategory.Pistol, WeaponName = "Laser Pistol" });

        // SubmachineGun
        weapons.Add(new WeaponEntry { Category = WeaponCategory.SubmachineGun, WeaponName = "Submachine Gun" });

        // Rifle
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Rifle, WeaponName = "Hunting Rifle" });

        weapons.Add(new WeaponEntry { Category = WeaponCategory.Rifle, WeaponName = "Battle Rifle" });

        weapons.Add(new WeaponEntry { Category = WeaponCategory.Rifle, WeaponName = "Light Machine Gun" });

        weapons.Add(new WeaponEntry { Category = WeaponCategory.Rifle, WeaponName = "Laser Rifle" });

        weapons.Add(new WeaponEntry { Category = WeaponCategory.Rifle, WeaponName = "Sniper Rifle" });

        // Shotgun
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Shotgun, WeaponName = "Double-Barrel Shotgun" });

        weapons.Add(new WeaponEntry { Category = WeaponCategory.Shotgun, WeaponName = "Pump-Action Shotgun" });

        // Special
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Special, WeaponName = "Bazooka" });

        weapons.Add(new WeaponEntry { Category = WeaponCategory.Special, WeaponName = "Gatling Gun" });

        // Explosive
        weapons.Add(new WeaponEntry { Category = WeaponCategory.Explosive, WeaponName = "Hand Grenade" });

        weapons.Add(new WeaponEntry { Category = WeaponCategory.Explosive, WeaponName = "Land Mine" });
    }


    private int GetFirstWeaponIndexByCategory(WeaponCategory category)
    {

        // Stop if we have no weapons.
        if (weapons == null || weapons.Count == 0) return -1;

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponEntry weaponEntry = weapons[i];
            if (weaponEntry == null) continue;
            if (weaponEntry.Category != category) continue;
            return i;
        }

        return -1;
    }
}
