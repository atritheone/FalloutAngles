﻿// imports
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;


public enum PlayerSkill
{
    None,
    Barter,
    BigGuns,
    EnergyWeapons,
    Explosives,
    Lockpick,
    Medicine,
    MeleeWeapons,
    Repair,
    Science,
    SmallGuns,
    Sneak,
    Speech,
    Unarmed
}

public enum PlayerCrippledBodyPart
{
    LeftArm,
    RightArm,
    Chest,
    Head,
    LeftLeg,
    RightLeg
}


// class
public class PlayerState : CharacterState
{
    private const int MinPlayerLevel = 1;
    private const int MinPlayerSkillValue = 0;
    private const int PlayerExperienceToNextLevelBase = 50;
    private const int PlayerExperienceToNextLevelPerCurrentLevel = 150;
    private const float HealthPointsPerLevel = 5f;
    private const float ActionPointsPerLevel = 5f;
    private const float SkillExperienceCurveExponent = 1.95f;
    public const int MaxPlayerLevel = 100;
    public const int MaxPlayerSkillValue = 100;

    [System.Serializable]
    private class VitalsCategory
    {
        [SerializeField] public float healthPoints = 100f;
        [SerializeField] public float actionPoints = 100f;
        [SerializeField] public float maxHealthPoints = 100f;
        [SerializeField] public float maxActionPoints = 100f;
        [SerializeField] public float radiation = 0f;
    }

    [System.Serializable]
    private class RadiationPoisoningCategory
    {
        [SerializeField] public bool minorRadiationPosioning = false;
        [SerializeField] public bool advancedRadiationPosioning = false;
        [SerializeField] public bool criticalRadiationPosioning = false;
        [SerializeField] public bool deadlyRadiationPosioning = false;
        [SerializeField] public bool fatalRadiationPosioning = false;
    }

    [System.Serializable]
    private class CripplingCategory
    {
        [SerializeField] public bool leftArmCrippled = false;
        [SerializeField] public bool rightArmCrippled = false;
        [SerializeField] public bool chestCrippled = false;
        [SerializeField] public bool headCrippled = false;
        [SerializeField] public bool leftLegCrippled = false;
        [SerializeField] public bool rightLegCrippled = false;
    }

    [System.Serializable]
    private class BodyPartHealthCategory
    {
        [SerializeField] [Range(0f, 100f)] public float leftArmHealth = 100f;
        [SerializeField] [Range(0f, 100f)] public float rightArmHealth = 100f;
        [SerializeField] [Range(0f, 100f)] public float chestHealth = 100f;
        [SerializeField] [Range(0f, 100f)] public float headHealth = 100f;
        [SerializeField] [Range(0f, 100f)] public float leftLegHealth = 100f;
        [SerializeField] [Range(0f, 100f)] public float rightLegHealth = 100f;
    }

    [System.Serializable]
    private class ProgressionCategory
    {
        [SerializeField] [Range(MinPlayerLevel, MaxPlayerLevel)] public int level = 1;
        [SerializeField] public int experience = 0;
        [SerializeField, HideInInspector] public int experienceToNextLevel = 200;
        [SerializeField] public int totalExperience = 0;
    }

    [System.Serializable]
    private class LevelingTuningCategory
    {
        [SerializeField] public int baseExperienceToNextLevel = 50;
        [SerializeField] public int experienceToNextLevelIncreasePerLevel = 150;
        [SerializeField] public int characterExperiencePerSkillLevelBase = 10;
        [SerializeField] public float characterExperiencePerSkillValue = 0.5f;
    }

    [System.Serializable]
    private class SpecialCategory
    {
        [SerializeField] public int strength = 5;
        [SerializeField] public int perception = 5;
        [SerializeField] public int endurance = 5;
        [SerializeField] public int charisma = 5;
        [SerializeField] public int intelligence = 5;
        [SerializeField] public int agility = 5;
        [SerializeField] public int luck = 5;
    }

    [System.Serializable]
    private class SkillsCategory
    {
        [SerializeField] [Range(0, 100)] public int barter = 0;
        [SerializeField] [Range(0, 100)] public int bigGuns = 0;
        [SerializeField] [Range(0, 100)] public int energyWeapons = 0;
        [SerializeField] [Range(0, 100)] public int explosives = 0;
        [SerializeField] [Range(0, 100)] public int lockpick = 0;
        [SerializeField] [Range(0, 100)] public int medicine = 0;
        [SerializeField] [Range(0, 100)] public int meleeWeapons = 0;
        [SerializeField] [Range(0, 100)] public int repair = 0;
        [SerializeField] [Range(0, 100)] public int science = 0;
        [SerializeField] [Range(0, 100)] public int smallGuns = 0;
        [SerializeField] [Range(0, 100)] public int sneak = 0;
        [SerializeField] [Range(0, 100)] public int speech = 0;
        [SerializeField] [Range(0, 100)] public int unarmed = 0;
    }

    [System.Serializable]
    private class SkillExperienceCategory
    {
        [SerializeField] public float barter = 0f;
        [SerializeField] public float bigGuns = 0f;
        [SerializeField] public float energyWeapons = 0f;
        [SerializeField] public float explosives = 0f;
        [SerializeField] public float lockpick = 0f;
        [SerializeField] public float medicine = 0f;
        [SerializeField] public float meleeWeapons = 0f;
        [SerializeField] public float repair = 0f;
        [SerializeField] public float science = 0f;
        [SerializeField] public float smallGuns = 0f;
        [SerializeField] public float sneak = 0f;
        [SerializeField] public float speech = 0f;
        [SerializeField] public float unarmed = 0f;
    }

    private const float MinorRadiationThreshold = 200f;
    private const float AdvancedRadiationThreshold = 400f;
    private const float CriticalRadiationThreshold = 600f;
    private const float DeadlyRadiationThreshold = 800f;
    private const float FatalRadiationThreshold = 1000f;
    private const float BodyPartHealthMin = 0f;
    private const float BodyPartHealthMax = 100f;
    private const float CrippledBodyPartHealthThreshold = 40f;
    private const float ManualCrippledBodyPartHealth = CrippledBodyPartHealthThreshold - 1f;
    private const string CrippledMessageSuffix = " Crippled";
    private const string RadiationPoisoningMessagePrefix = "You have ";
    private const string RadiationPoisoningMessageSuffix = " Radiation Poisoning.";
    private const string SkillIncreasedMessageSuffix = " increased to ";

    private static readonly int CombatModeParam = Animator.StringToHash("CombatMode");
    private static readonly int WeaponInHandParam = Animator.StringToHash("WeaponInHand");
    private bool hasAnimatorStateCache;
    private bool lastCombatModeParam;
    private bool lastWeaponInHandParam;
    private bool lastLeftArmCrippled;
    private bool lastRightArmCrippled;
    private bool lastChestCrippled;
    private bool lastHeadCrippled;
    private bool lastLeftLegCrippled;
    private bool lastRightLegCrippled;
    private bool lastMinorRadiationPosioning;
    private bool lastAdvancedRadiationPosioning;
    private bool lastCriticalRadiationPosioning;
    private bool lastDeadlyRadiationPosioning;
    private bool isDead;
    
    // variables
    [Header("Player Name")]
    // The player's display name.
    [SerializeField] private string playerName = "";

    [Header("Vitals")]
    [SerializeField] private VitalsCategory vitals = new VitalsCategory();

    [Header("Radiation Poisoning")]
    [SerializeField] private RadiationPoisoningCategory radiationPoisoning = new RadiationPoisoningCategory();

    [Header("Radiation Poisoning Messages")]
    [SerializeField] private bool showRadiationPoisoningMessages = true;

    [Header("Body Part Health")]
    [SerializeField] private BodyPartHealthCategory bodyPartHealth = new BodyPartHealthCategory();

    [Header("Crippling")]
    [SerializeField] private CripplingCategory crippling = new CripplingCategory();

    [Header("Cripple Messages")]
    [SerializeField] private bool showCrippleMessages = true;

    [Header("Progression")]
    [SerializeField] private ProgressionCategory progression = new ProgressionCategory();

    [Header("Leveling Tuning")]
    [SerializeField] private LevelingTuningCategory levelingTuning = new LevelingTuningCategory();

    [Header("S.P.E.C.I.A.L")]
    [SerializeField] private SpecialCategory special = new SpecialCategory();

    [Header("Skills")]
    [SerializeField] private SkillsCategory skills = new SkillsCategory();

    [Header("Skill Experience")]
    [SerializeField] private SkillExperienceCategory skillExperience = new SkillExperienceCategory();
    [SerializeField] private bool showSkillIncreaseMessages = true;

    [FormerlySerializedAs("healthPoints")]
    [FormerlySerializedAs("health")]
    [SerializeField] [HideInInspector] private float legacyHealthPoints = 100f;
    [FormerlySerializedAs("actionPoints")]
    [FormerlySerializedAs("stamina")]
    [SerializeField] [HideInInspector] private float legacyActionPoints = 100f;
    [FormerlySerializedAs("maxHealthPoints")]
    [FormerlySerializedAs("maxHealth")]
    [SerializeField] [HideInInspector] private float legacyMaxHealthPoints = 100f;
    [FormerlySerializedAs("maxActionPoints")]
    [FormerlySerializedAs("maxStamina")]
    [SerializeField] [HideInInspector] private float legacyMaxActionPoints = 100f;
    [FormerlySerializedAs("radiation")]
    [SerializeField] [HideInInspector] private float legacyRadiation = 0f;
    [FormerlySerializedAs("minorRadiationPosioning")]
    [SerializeField] [HideInInspector] private bool legacyMinorRadiationPosioning = false;
    [FormerlySerializedAs("advancedRadiationPosioning")]
    [SerializeField] [HideInInspector] private bool legacyAdvancedRadiationPosioning = false;
    [FormerlySerializedAs("criticalRadiationPosioning")]
    [SerializeField] [HideInInspector] private bool legacyCriticalRadiationPosioning = false;
    [FormerlySerializedAs("deadlyRadiationPosioning")]
    [SerializeField] [HideInInspector] private bool legacyDeadlyRadiationPosioning = false;
    [FormerlySerializedAs("fatalRadiationPosioning")]
    [SerializeField] [HideInInspector] private bool legacyFatalRadiationPosioning = false;
    [FormerlySerializedAs("leftArmCrippled")]
    [SerializeField] [HideInInspector] private bool legacyLeftArmCrippled = false;
    [FormerlySerializedAs("rightArmCrippled")]
    [SerializeField] [HideInInspector] private bool legacyRightArmCrippled = false;
    [FormerlySerializedAs("chestCrippled")]
    [SerializeField] [HideInInspector] private bool legacyChestCrippled = false;
    [FormerlySerializedAs("headCrippled")]
    [SerializeField] [HideInInspector] private bool legacyHeadCrippled = false;
    [FormerlySerializedAs("leftLegCrippled")]
    [SerializeField] [HideInInspector] private bool legacyLeftLegCrippled = false;
    [FormerlySerializedAs("rightLegCrippled")]
    [SerializeField] [HideInInspector] private bool legacyRightLegCrippled = false;
    [FormerlySerializedAs("level")]
    [SerializeField] [HideInInspector] private int legacyLevel = 1;
    [FormerlySerializedAs("experience")]
    [SerializeField] [HideInInspector] private int legacyExperience = 0;
    [FormerlySerializedAs("experienceToNextLevel")]
    [SerializeField] [HideInInspector] private int legacyExperienceToNextLevel = 100;
    [FormerlySerializedAs("totalExperience")]
    [SerializeField] [HideInInspector] private int legacyTotalExperience = 0;
    [FormerlySerializedAs("strength")]
    [SerializeField] [HideInInspector] private int legacyStrength = 5;
    [FormerlySerializedAs("perception")]
    [SerializeField] [HideInInspector] private int legacyPerception = 5;
    [FormerlySerializedAs("endurance")]
    [SerializeField] [HideInInspector] private int legacyEndurance = 5;
    [FormerlySerializedAs("charisma")]
    [SerializeField] [HideInInspector] private int legacyCharisma = 5;
    [FormerlySerializedAs("intelligence")]
    [SerializeField] [HideInInspector] private int legacyIntelligence = 5;
    [FormerlySerializedAs("agility")]
    [SerializeField] [HideInInspector] private int legacyAgility = 5;
    [FormerlySerializedAs("luck")]
    [SerializeField] [HideInInspector] private int legacyLuck = 5;
    [FormerlySerializedAs("barter")]
    [SerializeField] [HideInInspector] private int legacyBarter = 0;
    [FormerlySerializedAs("bigGuns")]
    [SerializeField] [HideInInspector] private int legacyBigGuns = 0;
    [FormerlySerializedAs("energyWeapons")]
    [SerializeField] [HideInInspector] private int legacyEnergyWeapons = 0;
    [FormerlySerializedAs("explosives")]
    [SerializeField] [HideInInspector] private int legacyExplosives = 0;
    [FormerlySerializedAs("lockpick")]
    [SerializeField] [HideInInspector] private int legacyLockpick = 0;
    [FormerlySerializedAs("medicine")]
    [SerializeField] [HideInInspector] private int legacyMedicine = 0;
    [FormerlySerializedAs("meleeWeapons")]
    [SerializeField] [HideInInspector] private int legacyMeleeWeapons = 0;
    [FormerlySerializedAs("repair")]
    [SerializeField] [HideInInspector] private int legacyRepair = 0;
    [FormerlySerializedAs("science")]
    [SerializeField] [HideInInspector] private int legacyScience = 0;
    [FormerlySerializedAs("smallGuns")]
    [SerializeField] [HideInInspector] private int legacySmallGuns = 0;
    [FormerlySerializedAs("sneak")]
    [SerializeField] [HideInInspector] private int legacySneak = 0;
    [FormerlySerializedAs("speech")]
    [SerializeField] [HideInInspector] private int legacySpeech = 0;
    [FormerlySerializedAs("unarmed")]
    [SerializeField] [HideInInspector] private int legacyUnarmed = 0;
    [SerializeField] [HideInInspector] private bool legacyFieldsMigrated = false;
    
    [Header("Perks")]
    [SerializeField] private List<PerkDefinition> perks = new List<PerkDefinition>();

    [Header("Combat")]
    // Whether the player is currently in combat mode.
    [SerializeField] private bool combatMode = false;

    [Header("Debug")]
    // Fallout-style god mode toggle. When enabled, incoming damage is ignored.
    [SerializeField] private bool godMode = false;

    [Header("Interaction")]
    // Whether the player is currently holding a grabbed physics item.
    [SerializeField] private bool hasGrabbedItem = false;

    [Header("Weapon")]
    // Whether the currently selected weapon is in the player's hands.
    [SerializeField] private bool weaponInHand = false;
    
    [Header("Action Points Regen")]
    // Action points regained per second.
    [FormerlySerializedAs("staminaRegenPerSecond")]
    [SerializeField] private float actionPointsRegenPerSecond = 8f;

    [Header("Animation")]
    // Animator that receives player state parameters.
    [SerializeField] private Animator animator;

    public event Action<PlayerState> OnDied;
    public event Action<PlayerState, PlayerCrippledBodyPart> OnBodyPartCrippled;
    public event Action<PlayerState, ExperienceChange> OnExperienceChanged;
    public event Action<PlayerState, int> OnLevelChanged;
    public event Action<PlayerState, PlayerSkill, int> OnSkillLevelChanged;

    public readonly struct ExperienceChange
    {
        public readonly int Amount;
        public readonly int PreviousLevel;
        public readonly int PreviousExperience;
        public readonly int PreviousExperienceToNextLevel;
        public readonly int CurrentLevel;
        public readonly int CurrentExperience;
        public readonly int CurrentExperienceToNextLevel;
        public readonly PlayerSkill SkillExperienceSource;

        public ExperienceChange(
            int amount,
            int previousLevel,
            int previousExperience,
            int previousExperienceToNextLevel,
            int currentLevel,
            int currentExperience,
            int currentExperienceToNextLevel,
            PlayerSkill skillExperienceSource = PlayerSkill.None)
        {
            Amount = amount;
            PreviousLevel = previousLevel;
            PreviousExperience = previousExperience;
            PreviousExperienceToNextLevel = previousExperienceToNextLevel;
            CurrentLevel = currentLevel;
            CurrentExperience = currentExperience;
            CurrentExperienceToNextLevel = currentExperienceToNextLevel;
            SkillExperienceSource = skillExperienceSource;
        }

        public bool LeveledUp => CurrentLevel > PreviousLevel;
        public bool IsSkillLevelExperience => SkillExperienceSource != PlayerSkill.None;
    }

    private readonly struct SkillExperienceCurve
    {
        public readonly float ImproveMult;
        public readonly float ImproveOffset;

        public SkillExperienceCurve(float improveMult, float improveOffset)
        {
            ImproveMult = improveMult;
            ImproveOffset = improveOffset;
        }
    }


    // methods
    private void Awake()
    {
        MigrateLegacyFieldsIfNeeded();
        EnsureLevelingTuning();
        SyncExperienceToNextLevel();
        NormalizeTotalExperience();
        ClampSkillExperience();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        UpdateRadiationPoisoningFlags(false);
        CacheRadiationPoisoningStates();
        ClampBodyPartHealth();
        SyncCrippledStatesFromBodyPartHealth(false);
        CacheCrippledStates();
        UpdateAnimatorParameters();
        isDead = vitals.healthPoints <= 0f;
    }

    private void Start()
    {
        UpdateAnimatorParameters();
    }

    private void OnValidate()
    {
        MigrateLegacyFieldsIfNeeded();

        progression.level = Mathf.Clamp(progression.level, MinPlayerLevel, MaxPlayerLevel);
        progression.experience = Mathf.Max(0, progression.experience);
        progression.experienceToNextLevel = Mathf.Max(0, progression.experienceToNextLevel);
        progression.totalExperience = Mathf.Max(0, progression.totalExperience);
        EnsureLevelingTuning();
        SyncExperienceToNextLevel();
        vitals.radiation = Mathf.Max(0f, vitals.radiation);
        special.strength = Mathf.Max(0, special.strength);
        special.perception = Mathf.Max(0, special.perception);
        special.endurance = Mathf.Max(0, special.endurance);
        special.charisma = Mathf.Max(0, special.charisma);
        special.intelligence = Mathf.Max(0, special.intelligence);
        special.agility = Mathf.Max(0, special.agility);
        special.luck = Mathf.Max(0, special.luck);
        skills.barter = Mathf.Clamp(skills.barter, 0, 100);
        skills.bigGuns = Mathf.Clamp(skills.bigGuns, 0, 100);
        skills.energyWeapons = Mathf.Clamp(skills.energyWeapons, 0, 100);
        skills.explosives = Mathf.Clamp(skills.explosives, 0, 100);
        skills.lockpick = Mathf.Clamp(skills.lockpick, 0, 100);
        skills.medicine = Mathf.Clamp(skills.medicine, 0, 100);
        skills.meleeWeapons = Mathf.Clamp(skills.meleeWeapons, 0, 100);
        skills.repair = Mathf.Clamp(skills.repair, 0, 100);
        skills.science = Mathf.Clamp(skills.science, 0, 100);
        skills.smallGuns = Mathf.Clamp(skills.smallGuns, 0, 100);
        skills.sneak = Mathf.Clamp(skills.sneak, 0, 100);
        skills.speech = Mathf.Clamp(skills.speech, 0, 100);
        skills.unarmed = Mathf.Clamp(skills.unarmed, 0, 100);
        ClampSkillExperience();
        ClampBodyPartHealth();
        SyncCrippledStatesFromBodyPartHealth(false);

        if (progression.experienceToNextLevel > 0 && progression.experience > progression.experienceToNextLevel)
        {
            progression.experience = progression.experienceToNextLevel;
        }
        NormalizeTotalExperience();

        UpdateRadiationPoisoningFlags(false);
        hasAnimatorStateCache = false;
    }

    private void MigrateLegacyFieldsIfNeeded()
    {
        if (legacyFieldsMigrated) return;

        vitals.healthPoints = legacyHealthPoints;
        vitals.actionPoints = legacyActionPoints;
        vitals.maxHealthPoints = legacyMaxHealthPoints;
        vitals.maxActionPoints = legacyMaxActionPoints;
        vitals.radiation = legacyRadiation;

        radiationPoisoning.minorRadiationPosioning = legacyMinorRadiationPosioning;
        radiationPoisoning.advancedRadiationPosioning = legacyAdvancedRadiationPosioning;
        radiationPoisoning.criticalRadiationPosioning = legacyCriticalRadiationPosioning;
        radiationPoisoning.deadlyRadiationPosioning = legacyDeadlyRadiationPosioning;
        radiationPoisoning.fatalRadiationPosioning = legacyFatalRadiationPosioning;

        crippling.leftArmCrippled = legacyLeftArmCrippled;
        crippling.rightArmCrippled = legacyRightArmCrippled;
        crippling.chestCrippled = legacyChestCrippled;
        crippling.headCrippled = legacyHeadCrippled;
        crippling.leftLegCrippled = legacyLeftLegCrippled;
        crippling.rightLegCrippled = legacyRightLegCrippled;

        progression.level = legacyLevel;
        progression.experience = legacyExperience;
        progression.experienceToNextLevel = legacyExperienceToNextLevel;
        progression.totalExperience = legacyTotalExperience;

        special.strength = legacyStrength;
        special.perception = legacyPerception;
        special.endurance = legacyEndurance;
        special.charisma = legacyCharisma;
        special.intelligence = legacyIntelligence;
        special.agility = legacyAgility;
        special.luck = legacyLuck;

        skills.barter = legacyBarter;
        skills.bigGuns = legacyBigGuns;
        skills.energyWeapons = legacyEnergyWeapons;
        skills.explosives = legacyExplosives;
        skills.lockpick = legacyLockpick;
        skills.medicine = legacyMedicine;
        skills.meleeWeapons = legacyMeleeWeapons;
        skills.repair = legacyRepair;
        skills.science = legacyScience;
        skills.smallGuns = legacySmallGuns;
        skills.sneak = legacySneak;
        skills.speech = legacySpeech;
        skills.unarmed = legacyUnarmed;

        legacyFieldsMigrated = true;
    }

    private void Update()
    {
        UpdateRadiationPoisoningFlags(true);
        SyncCrippledStatesFromBodyPartHealth(true);
        DetectCrippledStateChanges();

        if (isDead)
            return;

        if (vitals.actionPoints < vitals.maxActionPoints)
        {
            // Inline the simple clamp math used by RestoreActionPoints to avoid method-call overhead every frame.
            vitals.actionPoints = Mathf.Clamp(vitals.actionPoints + (actionPointsRegenPerSecond * Time.deltaTime), 0f, vitals.maxActionPoints);
        }
    }

    public float GetHealthPoints()
    {
        // Return the current health points value.
        return vitals.healthPoints;
    }

    public string GetPlayerName()
    {
        // Return the player's display name.
        return playerName;
    }

    public void SetPlayerName(string value)
    {
        // Store the player's display name.
        playerName = value ?? string.Empty;
    }


    public float GetActionPoints()
    {
        // Return the current action points value.
        return vitals.actionPoints;
    }

    public float GetMaxHealthPoints()
    {
        // Return the maximum health points value.
        return vitals.maxHealthPoints;
    }

    public float GetMaxActionPoints()
    {
        // Return the maximum action points value.
        return vitals.maxActionPoints;
    }

    public float GetRadiation()
    {
        // Return the current radiation value.
        return vitals.radiation;
    }

    public bool GetMinorRadiationPosioning()
    {
        // Return whether minor radiation poisoning is active.
        return radiationPoisoning.minorRadiationPosioning;
    }

    public bool GetAdvancedRadiationPosioning()
    {
        // Return whether advanced radiation poisoning is active.
        return radiationPoisoning.advancedRadiationPosioning;
    }

    public bool GetCriticalRadiationPosioning()
    {
        // Return whether critical radiation poisoning is active.
        return radiationPoisoning.criticalRadiationPosioning;
    }

    public bool GetDeadlyRadiationPosioning()
    {
        // Return whether deadly radiation poisoning is active.
        return radiationPoisoning.deadlyRadiationPosioning;
    }

    public bool GetFatalRadiationPosioning()
    {
        // Return whether fatal radiation poisoning is active.
        return radiationPoisoning.fatalRadiationPosioning;
    }

    public bool GetLeftArmCrippled()
    {
        return crippling.leftArmCrippled;
    }

    public bool GetRightArmCrippled()
    {
        return crippling.rightArmCrippled;
    }

    public bool GetChestCrippled()
    {
        return crippling.chestCrippled;
    }

    public bool GetHeadCrippled()
    {
        return crippling.headCrippled;
    }

    public bool GetLeftLegCrippled()
    {
        return crippling.leftLegCrippled;
    }

    public bool GetRightLegCrippled()
    {
        return crippling.rightLegCrippled;
    }

    public float GetLeftArmHealth()
    {
        return bodyPartHealth.leftArmHealth;
    }

    public float GetRightArmHealth()
    {
        return bodyPartHealth.rightArmHealth;
    }

    public float GetChestHealth()
    {
        return bodyPartHealth.chestHealth;
    }

    public float GetHeadHealth()
    {
        return bodyPartHealth.headHealth;
    }

    public float GetLeftLegHealth()
    {
        return bodyPartHealth.leftLegHealth;
    }

    public float GetRightLegHealth()
    {
        return bodyPartHealth.rightLegHealth;
    }


    public bool GetCombatMode()
    {
        // Return whether combat mode is active.
        return combatMode;
    }

    public bool IsDead()
    {
        return isDead;
    }

    public bool GetGodMode()
    {
        return godMode;
    }

    public bool GetHasGrabbedItem()
    {
        // Return whether the player is currently holding a grabbed item.
        return hasGrabbedItem;
    }

    public override bool GetWeaponInHand()
    {
        // Return whether the currently selected weapon is in-hand.
        return weaponInHand;
    }

    public int GetLevel()
    {
        // Return the player's current level.
        return progression.level;
    }

    public int SetLevel(int level)
    {
        int previousLevel = progression.level;
        progression.level = Mathf.Clamp(level, MinPlayerLevel, MaxPlayerLevel);
        progression.experience = 0;
        progression.experienceToNextLevel = progression.level >= MaxPlayerLevel
            ? 0
            : CalculateExperienceToNextLevel(progression.level);
        ApplyLevelUpVitalsBonus(progression.level - previousLevel);
        OnLevelChanged?.Invoke(this, progression.level);
        return progression.level;
    }

    public int GetExperience()
    {
        // Return experience earned toward the current level.
        return progression.experience;
    }

    public int GetExperienceToNextLevel()
    {
        // Return the experience required for the next level.
        return progression.experienceToNextLevel;
    }

    public int GetExperienceToNextLevelForLevel(int level)
    {
        EnsureLevelingTuning();

        int clampedLevel = Mathf.Clamp(level, MinPlayerLevel, MaxPlayerLevel);
        return clampedLevel >= MaxPlayerLevel ? 0 : CalculateExperienceToNextLevel(clampedLevel);
    }

    public int GetTotalExperience()
    {
        // Return total experience earned over the lifetime.
        return progression.totalExperience;
    }

    public int AddExperience(int amount)
    {
        return AddExperience(amount, PlayerSkill.None);
    }

    private int AddExperience(int amount, PlayerSkill skillExperienceSource)
    {
        if (amount == 0)
            return progression.experience;

        if (amount > 0)
            amount = ApplyExperienceGainPerks(amount);

        int previousLevel = progression.level;
        int previousExperience = progression.experience;
        int previousExperienceToNextLevel = progression.experienceToNextLevel;

        if (amount < 0)
        {
            progression.experience = ClampCurrentExperience((long)progression.experience + amount);
            return progression.experience;
        }

        if (progression.level >= MaxPlayerLevel)
        {
            progression.experience = 0;
            progression.experienceToNextLevel = 0;
            progression.totalExperience = ClampInt((long)progression.totalExperience + amount);
            NotifyExperienceChanged(amount, previousLevel, previousExperience, previousExperienceToNextLevel, skillExperienceSource);
            return progression.experience;
        }

        progression.totalExperience = ClampInt((long)progression.totalExperience + amount);
        progression.experience = ClampInt((long)progression.experience + amount);
        if (progression.experienceToNextLevel <= 0)
            progression.experienceToNextLevel = CalculateExperienceToNextLevel(progression.level);

        while (progression.level < MaxPlayerLevel)
        {
            int requiredExperience = Mathf.Max(1, progression.experienceToNextLevel);
            if (progression.experience < requiredExperience)
                break;

            progression.experience -= requiredExperience;
            progression.level = Mathf.Clamp(progression.level + 1, MinPlayerLevel, MaxPlayerLevel);
            progression.experienceToNextLevel = progression.level >= MaxPlayerLevel
                ? 0
                : CalculateExperienceToNextLevel(progression.level);

            ApplyLevelUpVitalsBonus(1);
            OnLevelChanged?.Invoke(this, progression.level);
        }

        if (progression.level >= MaxPlayerLevel)
        {
            progression.experience = 0;
            progression.experienceToNextLevel = 0;
        }

        NotifyExperienceChanged(amount, previousLevel, previousExperience, previousExperienceToNextLevel, skillExperienceSource);
        return progression.experience;
    }

    private void ApplyLevelUpVitalsBonus(int levelsGained)
    {
        if (levelsGained <= 0)
            return;

        SetMaxHealthPoints(vitals.maxHealthPoints + (HealthPointsPerLevel * levelsGained));
        SetMaxActionPoints(vitals.maxActionPoints + (ActionPointsPerLevel * levelsGained));
    }

    private void NotifyExperienceChanged(
        int amount,
        int previousLevel,
        int previousExperience,
        int previousExperienceToNextLevel,
        PlayerSkill skillExperienceSource)
    {
        ExperienceChange change = new ExperienceChange(
            amount,
            previousLevel,
            previousExperience,
            previousExperienceToNextLevel,
            progression.level,
            progression.experience,
            progression.experienceToNextLevel,
            skillExperienceSource);

        OnExperienceChanged?.Invoke(this, change);
        UI.ExperienceUIController.QueueExperienceChange(this, change);
    }

    public float GetSkillExperience(PlayerSkill skill)
    {
        EnsureLevelingTuning();
        return GetSkillExperienceValue(skill);
    }

    public float GetSkillExperienceToNextLevel(PlayerSkill skill)
    {
        EnsureLevelingTuning();
        int skillValue = GetSkillValue(skill);
        if (skillValue >= MaxPlayerSkillValue)
            return 0f;

        return CalculateSkillExperienceToNextLevel(skill, skillValue);
    }

    public int AddSkillExperience(PlayerSkill skill, float amount)
    {
        EnsureLevelingTuning();

        if (skill == PlayerSkill.None)
            return 0;

        int currentSkillValue = GetSkillValue(skill);
        if (amount <= 0f || currentSkillValue >= MaxPlayerSkillValue)
            return currentSkillValue;

        float currentSkillExperience = Mathf.Max(0f, GetSkillExperienceValue(skill) + amount);
        int pendingCharacterExperience = 0;

        while (currentSkillValue < MaxPlayerSkillValue)
        {
            float requiredExperience = CalculateSkillExperienceToNextLevel(skill, currentSkillValue);
            if (currentSkillExperience < requiredExperience)
                break;

            currentSkillExperience -= requiredExperience;
            currentSkillValue = Mathf.Clamp(currentSkillValue + 1, MinPlayerSkillValue, MaxPlayerSkillValue);
            SetSkillValue(skill, currentSkillValue);

            int characterExperience = CalculateCharacterExperienceForSkillLevel(currentSkillValue);
            if (characterExperience > 0)
                pendingCharacterExperience = ClampInt((long)pendingCharacterExperience + characterExperience);

            if (showSkillIncreaseMessages)
                UI.HUDMessagePanelController.Queue(GetSkillDisplayName(skill) + SkillIncreasedMessageSuffix + currentSkillValue);

            OnSkillLevelChanged?.Invoke(this, skill, currentSkillValue);
        }

        if (pendingCharacterExperience > 0)
            AddExperience(pendingCharacterExperience, skill);

        if (currentSkillValue >= MaxPlayerSkillValue)
            currentSkillExperience = 0f;

        SetSkillExperienceValue(skill, currentSkillExperience);
        return currentSkillValue;
    }

    public int GetStrength()
    {
        return special.strength;
    }

    public int GetPerception()
    {
        return special.perception;
    }

    public int GetEndurance()
    {
        return special.endurance;
    }

    public int GetCharisma()
    {
        return special.charisma;
    }

    public int GetIntelligence()
    {
        return special.intelligence;
    }

    public int GetAgility()
    {
        return special.agility;
    }

    public int GetLuck()
    {
        return special.luck;
    }

    public int GetBarter()
    {
        return skills.barter;
    }

    public int GetBigGuns()
    {
        return skills.bigGuns;
    }

    public int GetEnergyWeapons()
    {
        return skills.energyWeapons;
    }

    public int GetExplosives()
    {
        return skills.explosives;
    }

    public int GetLockpick()
    {
        return skills.lockpick;
    }

    public int GetMedicine()
    {
        return skills.medicine;
    }

    public int GetMeleeWeapons()
    {
        return skills.meleeWeapons;
    }

    public int GetRepair()
    {
        return skills.repair;
    }

    public int GetScience()
    {
        return skills.science;
    }

    public int GetSmallGuns()
    {
        return skills.smallGuns;
    }

    public int GetSneak()
    {
        return skills.sneak;
    }

    public int GetSpeech()
    {
        return skills.speech;
    }

    public int GetUnarmed()
    {
        return skills.unarmed;
    }
    
    public List<PerkDefinition> GetPerks()
    {
        return perks;
    }

    public bool HasPerk(PerkDefinition perk)
    {
        if (perk == null) return false;
        return perks.Contains(perk);
    }

    public bool AddPerk(PerkDefinition perk)
    {
        if (perk == null) return false;
        if (perks.Contains(perk)) return false;

        perks.Add(perk);
        ApplyPerkEffects(perk, 1);
        return true;
    }

    public bool RemovePerk(PerkDefinition perk)
    {
        if (perk == null) return false;
        if (!perks.Remove(perk)) return false;

        ApplyPerkEffects(perk, -1);
        return true;
    }

    private void ApplyPerkEffects(PerkDefinition perk, int direction)
    {
        if (!perk || direction == 0)
            return;

        List<PerkEffectDefinition> effects = perk.GetEffects();
        if (effects == null)
            return;

        for (int i = 0; i < effects.Count; i++)
            ApplyPerkEffect(effects[i], direction);
    }

    private void ApplyPerkEffect(PerkEffectDefinition effect, int direction)
    {
        if (effect == null)
            return;

        switch (effect.GetTarget())
        {
            case PerkEffectTarget.Skill:
                ApplySkillPerkEffect(effect, direction);
                break;
            case PerkEffectTarget.CarryWeight:
                ApplyCarryWeightPerkEffect(effect, direction);
                break;
        }
    }

    private void ApplySkillPerkEffect(PerkEffectDefinition effect, int direction)
    {
        if (effect.GetOperation() != PerkEffectOperation.AddFlat)
            return;

        PlayerSkill skill = effect.GetSkillTarget();
        if (skill == PlayerSkill.None)
            return;

        int amount = Mathf.RoundToInt(effect.GetMagnitude() * direction);
        SetSkillValue(skill, GetSkillValue(skill) + amount);
    }

    private void ApplyCarryWeightPerkEffect(PerkEffectDefinition effect, int direction)
    {
        if (effect.GetOperation() != PerkEffectOperation.AddFlat)
            return;

        PlayerInventory inventory = GetComponent<PlayerInventory>();
        if (!inventory)
            return;

        inventory.SetMaxWeight(inventory.GetMaxWeight() + effect.GetMagnitude() * direction);
    }

    private int ApplyExperienceGainPerks(int amount)
    {
        if (amount <= 0 || perks == null || perks.Count == 0)
            return amount;

        float percentBonus = 0f;
        for (int i = 0; i < perks.Count; i++)
        {
            PerkDefinition perk = perks[i];
            if (!perk)
                continue;

            List<PerkEffectDefinition> effects = perk.GetEffects();
            if (effects == null)
                continue;

            for (int effectIndex = 0; effectIndex < effects.Count; effectIndex++)
            {
                PerkEffectDefinition effect = effects[effectIndex];
                if (effect == null)
                    continue;

                if (effect.GetTarget() == PerkEffectTarget.ExperienceGain &&
                    effect.GetOperation() == PerkEffectOperation.AddPercent)
                {
                    percentBonus += effect.GetMagnitude();
                }
            }
        }

        if (percentBonus <= 0f)
            return amount;

        return Mathf.Max(0, Mathf.RoundToInt(amount * (1f + percentBonus / 100f)));
    }


    public void SetCombatMode(bool value)
    {
        if (isDead)
            value = false;

        // Never allow entering combat mode while carrying a grabbed item.
        if (value && hasGrabbedItem)
            value = false;

        if (combatMode == value)
            return;

        // Set the combat mode flag.
        combatMode = value;
        UpdateAnimatorParameters();
    }

    public void SetGodMode(bool value)
    {
        if (godMode == value)
            return;

        godMode = value;
        if (godMode)
            ApplyGodModeProtectionState();
    }

    public bool ToggleGodMode()
    {
        SetGodMode(!godMode);
        return godMode;
    }

    public void SetHasGrabbedItem(bool value)
    {
        if (hasGrabbedItem == value)
        {
            if (value && combatMode)
                SetCombatMode(false);

            return;
        }

        // Store current grabbed-item state.
        hasGrabbedItem = value;

        // Force combat mode off while an item is being carried.
        if (hasGrabbedItem && combatMode)
            SetCombatMode(false);
    }

    public void SetWeaponInHand(bool value)
    {
        if (isDead)
            value = false;

        if (weaponInHand == value)
            return;

        // Set the weapon in-hand state.
        weaponInHand = value;
        UpdateAnimatorParameters();
    }

    public void SetRadiation(float value)
    {
        if (godMode && value > vitals.radiation)
            value = vitals.radiation;

        // Clamp radiation so it never goes below zero.
        vitals.radiation = Mathf.Max(0f, value);
        UpdateRadiationPoisoningFlags(true);
    }

    public void SetHealthPoints(float value)
    {
        // Clamp health into the current max-health range.
        vitals.healthPoints = Mathf.Clamp(value, 0f, vitals.maxHealthPoints);

        if (vitals.healthPoints <= 0f)
            Die();
        else
            isDead = false;
    }

    public void RestoreHealth(float amount)
    {
        // Ignore non-positive restore values.
        if (amount <= 0f) return;

        // Add health and clamp to max.
        SetHealthPoints(vitals.healthPoints + amount);
    }

    public void SetMaxHealthPoints(float value)
    {
        // Clamp max health to non-negative values.
        vitals.maxHealthPoints = Mathf.Max(0f, value);

        // Keep current health within the new range.
        SetHealthPoints(vitals.healthPoints);
    }

    public void SetActionPoints(float value)
    {
        if (godMode && value < vitals.actionPoints)
            return;

        // Clamp action points into the current max-action range.
        vitals.actionPoints = Mathf.Clamp(value, 0f, vitals.maxActionPoints);
    }

    public void SetMaxActionPoints(float value)
    {
        // Clamp max action points to non-negative values.
        vitals.maxActionPoints = Mathf.Max(0f, value);

        // Keep current action points within the new range.
        vitals.actionPoints = Mathf.Clamp(vitals.actionPoints, 0f, vitals.maxActionPoints);

        if (godMode)
            vitals.actionPoints = vitals.maxActionPoints;
    }

    public void AddRadiation(float amount)
    {
        if (godMode && amount > 0f)
            return;

        // Increase radiation and refresh poisoning flags.
        SetRadiation(vitals.radiation + amount);
    }

    public void RemoveRadiation(float amount)
    {
        // Decrease radiation and refresh poisoning flags.
        SetRadiation(vitals.radiation - amount);
    }

    public void SetLeftArmCrippled(bool value)
    {
        SetBodyPartCrippledState(ref bodyPartHealth.leftArmHealth, ref crippling.leftArmCrippled, ref lastLeftArmCrippled, value, "Left Arm", PlayerCrippledBodyPart.LeftArm);
    }

    public void SetRightArmCrippled(bool value)
    {
        SetBodyPartCrippledState(ref bodyPartHealth.rightArmHealth, ref crippling.rightArmCrippled, ref lastRightArmCrippled, value, "Right Arm", PlayerCrippledBodyPart.RightArm);
    }

    public void SetChestCrippled(bool value)
    {
        SetBodyPartCrippledState(ref bodyPartHealth.chestHealth, ref crippling.chestCrippled, ref lastChestCrippled, value, "Chest", PlayerCrippledBodyPart.Chest);
    }

    public void SetHeadCrippled(bool value)
    {
        SetBodyPartCrippledState(ref bodyPartHealth.headHealth, ref crippling.headCrippled, ref lastHeadCrippled, value, "Head", PlayerCrippledBodyPart.Head);
    }

    public void SetLeftLegCrippled(bool value)
    {
        SetBodyPartCrippledState(ref bodyPartHealth.leftLegHealth, ref crippling.leftLegCrippled, ref lastLeftLegCrippled, value, "Left Leg", PlayerCrippledBodyPart.LeftLeg);
    }

    public void SetRightLegCrippled(bool value)
    {
        SetBodyPartCrippledState(ref bodyPartHealth.rightLegHealth, ref crippling.rightLegCrippled, ref lastRightLegCrippled, value, "Right Leg", PlayerCrippledBodyPart.RightLeg);
    }

    public void SetLeftArmHealth(float value)
    {
        SetBodyPartHealth(ref bodyPartHealth.leftArmHealth, ref crippling.leftArmCrippled, ref lastLeftArmCrippled, value, "Left Arm", PlayerCrippledBodyPart.LeftArm);
    }

    public void SetRightArmHealth(float value)
    {
        SetBodyPartHealth(ref bodyPartHealth.rightArmHealth, ref crippling.rightArmCrippled, ref lastRightArmCrippled, value, "Right Arm", PlayerCrippledBodyPart.RightArm);
    }

    public void SetChestHealth(float value)
    {
        SetBodyPartHealth(ref bodyPartHealth.chestHealth, ref crippling.chestCrippled, ref lastChestCrippled, value, "Chest", PlayerCrippledBodyPart.Chest);
    }

    public void SetHeadHealth(float value)
    {
        SetBodyPartHealth(ref bodyPartHealth.headHealth, ref crippling.headCrippled, ref lastHeadCrippled, value, "Head", PlayerCrippledBodyPart.Head);
    }

    public void SetLeftLegHealth(float value)
    {
        SetBodyPartHealth(ref bodyPartHealth.leftLegHealth, ref crippling.leftLegCrippled, ref lastLeftLegCrippled, value, "Left Leg", PlayerCrippledBodyPart.LeftLeg);
    }

    public void SetRightLegHealth(float value)
    {
        SetBodyPartHealth(ref bodyPartHealth.rightLegHealth, ref crippling.rightLegCrippled, ref lastRightLegCrippled, value, "Right Leg", PlayerCrippledBodyPart.RightLeg);
    }

    public void SetStrength(int value)
    {
        special.strength = Mathf.Max(0, value);
    }

    public void SetPerception(int value)
    {
        special.perception = Mathf.Max(0, value);
    }

    public void SetEndurance(int value)
    {
        special.endurance = Mathf.Max(0, value);
    }

    public void SetCharisma(int value)
    {
        special.charisma = Mathf.Max(0, value);
    }

    public void SetIntelligence(int value)
    {
        special.intelligence = Mathf.Max(0, value);
    }

    public void SetAgility(int value)
    {
        special.agility = Mathf.Max(0, value);
    }

    public void SetLuck(int value)
    {
        special.luck = Mathf.Max(0, value);
    }

    public void SetBarter(int value)
    {
        skills.barter = ClampSkillValue(value);
    }

    public void SetBigGuns(int value)
    {
        skills.bigGuns = ClampSkillValue(value);
    }

    public void SetEnergyWeapons(int value)
    {
        skills.energyWeapons = ClampSkillValue(value);
    }

    public void SetExplosives(int value)
    {
        skills.explosives = ClampSkillValue(value);
    }

    public void SetLockpick(int value)
    {
        skills.lockpick = ClampSkillValue(value);
    }

    public void SetMedicine(int value)
    {
        skills.medicine = ClampSkillValue(value);
    }

    public void SetMeleeWeapons(int value)
    {
        skills.meleeWeapons = ClampSkillValue(value);
    }

    public void SetRepair(int value)
    {
        skills.repair = ClampSkillValue(value);
    }

    public void SetScience(int value)
    {
        skills.science = ClampSkillValue(value);
    }

    public void SetSmallGuns(int value)
    {
        skills.smallGuns = ClampSkillValue(value);
    }

    public void SetSneak(int value)
    {
        skills.sneak = ClampSkillValue(value);
    }

    public void SetSpeech(int value)
    {
        skills.speech = ClampSkillValue(value);
    }

    public void SetUnarmed(int value)
    {
        skills.unarmed = ClampSkillValue(value);
    }

    private void EnsureLevelingTuning()
    {
        if (levelingTuning == null)
            levelingTuning = new LevelingTuningCategory();

        if (progression == null)
            progression = new ProgressionCategory();

        if (skills == null)
            skills = new SkillsCategory();

        if (skillExperience == null)
            skillExperience = new SkillExperienceCategory();

        levelingTuning.baseExperienceToNextLevel = PlayerExperienceToNextLevelBase;
        levelingTuning.experienceToNextLevelIncreasePerLevel = PlayerExperienceToNextLevelPerCurrentLevel;
        levelingTuning.characterExperiencePerSkillLevelBase = Mathf.Max(0, levelingTuning.characterExperiencePerSkillLevelBase);
        levelingTuning.characterExperiencePerSkillValue = Mathf.Max(0f, levelingTuning.characterExperiencePerSkillValue);
    }

    private void SyncExperienceToNextLevel()
    {
        progression.experienceToNextLevel = progression.level >= MaxPlayerLevel
            ? 0
            : CalculateExperienceToNextLevel(progression.level);
    }

    private int CalculateExperienceToNextLevel(int level)
    {
        int clampedLevel = Mathf.Clamp(level, MinPlayerLevel, MaxPlayerLevel);
        long requiredExperience = (long)PlayerExperienceToNextLevelBase +
                                  (long)clampedLevel * PlayerExperienceToNextLevelPerCurrentLevel;

        return ClampInt(requiredExperience);
    }

    private void NormalizeTotalExperience()
    {
        long minimumTotalExperience = CalculateTotalExperienceForLevel(progression.level) + progression.experience;
        if (progression.totalExperience < minimumTotalExperience)
            progression.totalExperience = ClampInt(minimumTotalExperience);
    }

    private int CalculateTotalExperienceForLevel(int level)
    {
        int clampedLevel = Mathf.Clamp(level, MinPlayerLevel, MaxPlayerLevel);
        long totalExperienceForLevel = 0L;

        for (int currentLevel = MinPlayerLevel; currentLevel < clampedLevel; currentLevel++)
            totalExperienceForLevel += CalculateExperienceToNextLevel(currentLevel);

        return ClampInt(totalExperienceForLevel);
    }

    private float CalculateSkillExperienceToNextLevel(PlayerSkill skill, int currentSkillValue)
    {
        SkillExperienceCurve curve = GetSkillExperienceCurve(skill);
        int clampedSkillValue = Mathf.Clamp(currentSkillValue, MinPlayerSkillValue, MaxPlayerSkillValue);
        float requiredExperience = curve.ImproveMult * Mathf.Pow(clampedSkillValue, SkillExperienceCurveExponent) +
                                   curve.ImproveOffset;

        return Mathf.Max(1f, requiredExperience);
    }

    private static SkillExperienceCurve GetSkillExperienceCurve(PlayerSkill skill)
    {
        switch (skill)
        {
            case PlayerSkill.Lockpick:
            case PlayerSkill.Repair:
            case PlayerSkill.Science:
                return new SkillExperienceCurve(0.25f, 300f);
            case PlayerSkill.Sneak:
                return new SkillExperienceCurve(0.5f, 120f);
            case PlayerSkill.Barter:
            case PlayerSkill.Medicine:
                return new SkillExperienceCurve(1.6f, 65f);
            default:
                return new SkillExperienceCurve(2f, 0f);
        }
    }

    private int CalculateCharacterExperienceForSkillLevel(int newSkillValue)
    {
        EnsureLevelingTuning();
        float experience = levelingTuning.characterExperiencePerSkillLevelBase +
                           Mathf.Max(0, newSkillValue) * levelingTuning.characterExperiencePerSkillValue;

        return Mathf.Max(0, Mathf.RoundToInt(experience));
    }

    private int GetSkillValue(PlayerSkill skill)
    {
        switch (skill)
        {
            case PlayerSkill.Barter:
                return skills.barter;
            case PlayerSkill.BigGuns:
                return skills.bigGuns;
            case PlayerSkill.EnergyWeapons:
                return skills.energyWeapons;
            case PlayerSkill.Explosives:
                return skills.explosives;
            case PlayerSkill.Lockpick:
                return skills.lockpick;
            case PlayerSkill.Medicine:
                return skills.medicine;
            case PlayerSkill.MeleeWeapons:
                return skills.meleeWeapons;
            case PlayerSkill.Repair:
                return skills.repair;
            case PlayerSkill.Science:
                return skills.science;
            case PlayerSkill.SmallGuns:
                return skills.smallGuns;
            case PlayerSkill.Sneak:
                return skills.sneak;
            case PlayerSkill.Speech:
                return skills.speech;
            case PlayerSkill.Unarmed:
                return skills.unarmed;
            default:
                return 0;
        }
    }

    private void SetSkillValue(PlayerSkill skill, int value)
    {
        int clampedValue = ClampSkillValue(value);
        switch (skill)
        {
            case PlayerSkill.Barter:
                skills.barter = clampedValue;
                break;
            case PlayerSkill.BigGuns:
                skills.bigGuns = clampedValue;
                break;
            case PlayerSkill.EnergyWeapons:
                skills.energyWeapons = clampedValue;
                break;
            case PlayerSkill.Explosives:
                skills.explosives = clampedValue;
                break;
            case PlayerSkill.Lockpick:
                skills.lockpick = clampedValue;
                break;
            case PlayerSkill.Medicine:
                skills.medicine = clampedValue;
                break;
            case PlayerSkill.MeleeWeapons:
                skills.meleeWeapons = clampedValue;
                break;
            case PlayerSkill.Repair:
                skills.repair = clampedValue;
                break;
            case PlayerSkill.Science:
                skills.science = clampedValue;
                break;
            case PlayerSkill.SmallGuns:
                skills.smallGuns = clampedValue;
                break;
            case PlayerSkill.Sneak:
                skills.sneak = clampedValue;
                break;
            case PlayerSkill.Speech:
                skills.speech = clampedValue;
                break;
            case PlayerSkill.Unarmed:
                skills.unarmed = clampedValue;
                break;
        }
    }

    private float GetSkillExperienceValue(PlayerSkill skill)
    {
        switch (skill)
        {
            case PlayerSkill.Barter:
                return skillExperience.barter;
            case PlayerSkill.BigGuns:
                return skillExperience.bigGuns;
            case PlayerSkill.EnergyWeapons:
                return skillExperience.energyWeapons;
            case PlayerSkill.Explosives:
                return skillExperience.explosives;
            case PlayerSkill.Lockpick:
                return skillExperience.lockpick;
            case PlayerSkill.Medicine:
                return skillExperience.medicine;
            case PlayerSkill.MeleeWeapons:
                return skillExperience.meleeWeapons;
            case PlayerSkill.Repair:
                return skillExperience.repair;
            case PlayerSkill.Science:
                return skillExperience.science;
            case PlayerSkill.SmallGuns:
                return skillExperience.smallGuns;
            case PlayerSkill.Sneak:
                return skillExperience.sneak;
            case PlayerSkill.Speech:
                return skillExperience.speech;
            case PlayerSkill.Unarmed:
                return skillExperience.unarmed;
            default:
                return 0f;
        }
    }

    private void SetSkillExperienceValue(PlayerSkill skill, float value)
    {
        float clampedValue = Mathf.Max(0f, value);
        switch (skill)
        {
            case PlayerSkill.Barter:
                skillExperience.barter = clampedValue;
                break;
            case PlayerSkill.BigGuns:
                skillExperience.bigGuns = clampedValue;
                break;
            case PlayerSkill.EnergyWeapons:
                skillExperience.energyWeapons = clampedValue;
                break;
            case PlayerSkill.Explosives:
                skillExperience.explosives = clampedValue;
                break;
            case PlayerSkill.Lockpick:
                skillExperience.lockpick = clampedValue;
                break;
            case PlayerSkill.Medicine:
                skillExperience.medicine = clampedValue;
                break;
            case PlayerSkill.MeleeWeapons:
                skillExperience.meleeWeapons = clampedValue;
                break;
            case PlayerSkill.Repair:
                skillExperience.repair = clampedValue;
                break;
            case PlayerSkill.Science:
                skillExperience.science = clampedValue;
                break;
            case PlayerSkill.SmallGuns:
                skillExperience.smallGuns = clampedValue;
                break;
            case PlayerSkill.Sneak:
                skillExperience.sneak = clampedValue;
                break;
            case PlayerSkill.Speech:
                skillExperience.speech = clampedValue;
                break;
            case PlayerSkill.Unarmed:
                skillExperience.unarmed = clampedValue;
                break;
        }
    }

    private void ClampSkillExperience()
    {
        if (skillExperience == null)
            skillExperience = new SkillExperienceCategory();

        skillExperience.barter = Mathf.Max(0f, skillExperience.barter);
        skillExperience.bigGuns = Mathf.Max(0f, skillExperience.bigGuns);
        skillExperience.energyWeapons = Mathf.Max(0f, skillExperience.energyWeapons);
        skillExperience.explosives = Mathf.Max(0f, skillExperience.explosives);
        skillExperience.lockpick = Mathf.Max(0f, skillExperience.lockpick);
        skillExperience.medicine = Mathf.Max(0f, skillExperience.medicine);
        skillExperience.meleeWeapons = Mathf.Max(0f, skillExperience.meleeWeapons);
        skillExperience.repair = Mathf.Max(0f, skillExperience.repair);
        skillExperience.science = Mathf.Max(0f, skillExperience.science);
        skillExperience.smallGuns = Mathf.Max(0f, skillExperience.smallGuns);
        skillExperience.sneak = Mathf.Max(0f, skillExperience.sneak);
        skillExperience.speech = Mathf.Max(0f, skillExperience.speech);
        skillExperience.unarmed = Mathf.Max(0f, skillExperience.unarmed);
    }

    private static string GetSkillDisplayName(PlayerSkill skill)
    {
        switch (skill)
        {
            case PlayerSkill.Barter:
                return "Barter";
            case PlayerSkill.BigGuns:
                return "Big Guns";
            case PlayerSkill.EnergyWeapons:
                return "Energy Weapons";
            case PlayerSkill.Explosives:
                return "Explosives";
            case PlayerSkill.Lockpick:
                return "Lockpick";
            case PlayerSkill.Medicine:
                return "Medicine";
            case PlayerSkill.MeleeWeapons:
                return "Melee Weapons";
            case PlayerSkill.Repair:
                return "Repair";
            case PlayerSkill.Science:
                return "Science";
            case PlayerSkill.SmallGuns:
                return "Small Guns";
            case PlayerSkill.Sneak:
                return "Sneak";
            case PlayerSkill.Speech:
                return "Speech";
            case PlayerSkill.Unarmed:
                return "Unarmed";
            default:
                return string.Empty;
        }
    }

    private int ClampCurrentExperience(long value)
    {
        long clampedValue = Math.Max(0L, value);

        if (progression.experienceToNextLevel > 0)
            clampedValue = Math.Min(clampedValue, progression.experienceToNextLevel);

        return clampedValue > int.MaxValue ? int.MaxValue : (int)clampedValue;
    }

    private static int ClampInt(long value)
    {
        if (value <= 0L)
            return 0;

        return value > int.MaxValue ? int.MaxValue : (int)value;
    }

    private static int ClampSkillValue(int value)
    {
        return Mathf.Clamp(value, MinPlayerSkillValue, MaxPlayerSkillValue);
    }


    public void ApplyDamage(float amount)
    {
        if (godMode)
            return;

        if (amount <= 0f || isDead)
            return;

        // Subtract damage and clamp into valid health range.
        SetHealthPoints(vitals.healthPoints - amount);
    }


    public bool TrySpendActionPoints(float amount)
    {
        if (amount <= 0f)
            return true;

        if (godMode)
        {
            vitals.actionPoints = vitals.maxActionPoints;
            return true;
        }

        // Stop if there isn't enough action points to spend.
        if (vitals.actionPoints < amount) return false;

        // Subtract the cost from action points.
        vitals.actionPoints = Mathf.Max(0f, vitals.actionPoints - amount);

        // Return success.
        return true;
    }


    public void RestoreActionPoints(float amount)
    {
        // Add action points back.
        vitals.actionPoints = Mathf.Clamp(vitals.actionPoints + amount, 0f, vitals.maxActionPoints);
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        combatMode = false;
        weaponInHand = false;
        UpdateAnimatorParameters();
        OnDied?.Invoke(this);
    }

    private void CacheCrippledStates()
    {
        lastLeftArmCrippled = crippling.leftArmCrippled;
        lastRightArmCrippled = crippling.rightArmCrippled;
        lastChestCrippled = crippling.chestCrippled;
        lastHeadCrippled = crippling.headCrippled;
        lastLeftLegCrippled = crippling.leftLegCrippled;
        lastRightLegCrippled = crippling.rightLegCrippled;
    }

    private void CacheRadiationPoisoningStates()
    {
        lastMinorRadiationPosioning = radiationPoisoning.minorRadiationPosioning;
        lastAdvancedRadiationPosioning = radiationPoisoning.advancedRadiationPosioning;
        lastCriticalRadiationPosioning = radiationPoisoning.criticalRadiationPosioning;
        lastDeadlyRadiationPosioning = radiationPoisoning.deadlyRadiationPosioning;
    }

    private void ClampBodyPartHealth()
    {
        if (bodyPartHealth == null)
            bodyPartHealth = new BodyPartHealthCategory();

        bodyPartHealth.leftArmHealth = ClampBodyPartHealthValue(bodyPartHealth.leftArmHealth);
        bodyPartHealth.rightArmHealth = ClampBodyPartHealthValue(bodyPartHealth.rightArmHealth);
        bodyPartHealth.chestHealth = ClampBodyPartHealthValue(bodyPartHealth.chestHealth);
        bodyPartHealth.headHealth = ClampBodyPartHealthValue(bodyPartHealth.headHealth);
        bodyPartHealth.leftLegHealth = ClampBodyPartHealthValue(bodyPartHealth.leftLegHealth);
        bodyPartHealth.rightLegHealth = ClampBodyPartHealthValue(bodyPartHealth.rightLegHealth);
    }

    private void SyncCrippledStatesFromBodyPartHealth(bool notify)
    {
        SyncCrippledStateFromBodyPartHealth(ref crippling.leftArmCrippled, ref lastLeftArmCrippled, bodyPartHealth.leftArmHealth, "Left Arm", PlayerCrippledBodyPart.LeftArm, notify);
        SyncCrippledStateFromBodyPartHealth(ref crippling.rightArmCrippled, ref lastRightArmCrippled, bodyPartHealth.rightArmHealth, "Right Arm", PlayerCrippledBodyPart.RightArm, notify);
        SyncCrippledStateFromBodyPartHealth(ref crippling.chestCrippled, ref lastChestCrippled, bodyPartHealth.chestHealth, "Chest", PlayerCrippledBodyPart.Chest, notify);
        SyncCrippledStateFromBodyPartHealth(ref crippling.headCrippled, ref lastHeadCrippled, bodyPartHealth.headHealth, "Head", PlayerCrippledBodyPart.Head, notify);
        SyncCrippledStateFromBodyPartHealth(ref crippling.leftLegCrippled, ref lastLeftLegCrippled, bodyPartHealth.leftLegHealth, "Left Leg", PlayerCrippledBodyPart.LeftLeg, notify);
        SyncCrippledStateFromBodyPartHealth(ref crippling.rightLegCrippled, ref lastRightLegCrippled, bodyPartHealth.rightLegHealth, "Right Leg", PlayerCrippledBodyPart.RightLeg, notify);
    }

    private void SyncCrippledStateFromBodyPartHealth(ref bool currentValue, ref bool lastValue, float healthValue, string areaName, PlayerCrippledBodyPart bodyPart, bool notify)
    {
        bool nextValue = !godMode && IsCrippledBodyPartHealth(healthValue);

        if (notify)
        {
            SetCrippledState(ref currentValue, ref lastValue, nextValue, areaName, bodyPart);
        }
        else
        {
            currentValue = nextValue;
        }
    }

    private void SetBodyPartHealth(ref float healthValue, ref bool crippledValue, ref bool lastCrippledValue, float nextHealthValue, string areaName, PlayerCrippledBodyPart bodyPart)
    {
        healthValue = ClampBodyPartHealthValue(nextHealthValue);
        bool nextCrippledValue = !godMode && IsCrippledBodyPartHealth(healthValue);
        SetCrippledState(ref crippledValue, ref lastCrippledValue, nextCrippledValue, areaName, bodyPart);
    }

    private void SetBodyPartCrippledState(ref float healthValue, ref bool crippledValue, ref bool lastCrippledValue, bool nextCrippledValue, string areaName, PlayerCrippledBodyPart bodyPart)
    {
        healthValue = ClampBodyPartHealthValue(healthValue);
        if (godMode && nextCrippledValue)
            nextCrippledValue = false;

        if (nextCrippledValue && healthValue >= CrippledBodyPartHealthThreshold)
            healthValue = ManualCrippledBodyPartHealth;
        else if (!nextCrippledValue && healthValue < CrippledBodyPartHealthThreshold)
            healthValue = CrippledBodyPartHealthThreshold;

        SetCrippledState(ref crippledValue, ref lastCrippledValue, nextCrippledValue, areaName, bodyPart);
    }

    private static float ClampBodyPartHealthValue(float value)
    {
        return Mathf.Clamp(value, BodyPartHealthMin, BodyPartHealthMax);
    }

    private static bool IsCrippledBodyPartHealth(float healthValue)
    {
        return healthValue < CrippledBodyPartHealthThreshold;
    }

    private void DetectCrippledStateChanges()
    {
        DetectCrippledStateChange(crippling.leftArmCrippled, ref lastLeftArmCrippled, "Left Arm", PlayerCrippledBodyPart.LeftArm);
        DetectCrippledStateChange(crippling.rightArmCrippled, ref lastRightArmCrippled, "Right Arm", PlayerCrippledBodyPart.RightArm);
        DetectCrippledStateChange(crippling.chestCrippled, ref lastChestCrippled, "Chest", PlayerCrippledBodyPart.Chest);
        DetectCrippledStateChange(crippling.headCrippled, ref lastHeadCrippled, "Head", PlayerCrippledBodyPart.Head);
        DetectCrippledStateChange(crippling.leftLegCrippled, ref lastLeftLegCrippled, "Left Leg", PlayerCrippledBodyPart.LeftLeg);
        DetectCrippledStateChange(crippling.rightLegCrippled, ref lastRightLegCrippled, "Right Leg", PlayerCrippledBodyPart.RightLeg);
    }

    private void DetectCrippledStateChange(bool currentValue, ref bool lastValue, string areaName, PlayerCrippledBodyPart bodyPart)
    {
        bool becameCrippled = !lastValue && currentValue;
        lastValue = currentValue;

        if (becameCrippled)
            NotifyBodyPartCrippled(areaName, bodyPart);
    }

    private void SetCrippledState(ref bool currentValue, ref bool lastValue, bool nextValue, string areaName, PlayerCrippledBodyPart bodyPart)
    {
        bool becameCrippled = !lastValue && nextValue;
        currentValue = nextValue;
        lastValue = nextValue;

        if (becameCrippled)
            NotifyBodyPartCrippled(areaName, bodyPart);
    }

    private void NotifyBodyPartCrippled(string areaName, PlayerCrippledBodyPart bodyPart)
    {
        ShowCrippledMessage(areaName);
        OnBodyPartCrippled?.Invoke(this, bodyPart);
    }

    private void ShowCrippledMessage(string areaName)
    {
        if (showCrippleMessages)
            UI.HUDMessagePanelController.Queue(areaName + CrippledMessageSuffix);
    }

    private void UpdateRadiationPoisoningFlags(bool notify)
    {
        if (godMode)
        {
            ClearRadiationPoisoningFlags();
            if (notify)
                CacheRadiationPoisoningStates();
            return;
        }

        float currentRadiation = vitals.radiation;
        radiationPoisoning.fatalRadiationPosioning = currentRadiation >= FatalRadiationThreshold;
        radiationPoisoning.deadlyRadiationPosioning = !radiationPoisoning.fatalRadiationPosioning && currentRadiation >= DeadlyRadiationThreshold;
        radiationPoisoning.criticalRadiationPosioning = !radiationPoisoning.fatalRadiationPosioning && !radiationPoisoning.deadlyRadiationPosioning && currentRadiation >= CriticalRadiationThreshold;
        radiationPoisoning.advancedRadiationPosioning = !radiationPoisoning.fatalRadiationPosioning && !radiationPoisoning.deadlyRadiationPosioning && !radiationPoisoning.criticalRadiationPosioning && currentRadiation >= AdvancedRadiationThreshold;
        radiationPoisoning.minorRadiationPosioning = !radiationPoisoning.fatalRadiationPosioning && !radiationPoisoning.deadlyRadiationPosioning && !radiationPoisoning.criticalRadiationPosioning && !radiationPoisoning.advancedRadiationPosioning && currentRadiation >= MinorRadiationThreshold;

        if (notify)
            DetectRadiationPoisoningStateChanges();
    }

    private void DetectRadiationPoisoningStateChanges()
    {
        DetectRadiationPoisoningStateChange(radiationPoisoning.minorRadiationPosioning, ref lastMinorRadiationPosioning, "Minor");
        DetectRadiationPoisoningStateChange(radiationPoisoning.advancedRadiationPosioning, ref lastAdvancedRadiationPosioning, "Advanced");
        DetectRadiationPoisoningStateChange(radiationPoisoning.criticalRadiationPosioning, ref lastCriticalRadiationPosioning, "Critical");
        DetectRadiationPoisoningStateChange(radiationPoisoning.deadlyRadiationPosioning, ref lastDeadlyRadiationPosioning, "Deadly");
    }

    private void DetectRadiationPoisoningStateChange(bool currentValue, ref bool lastValue, string poisoningName)
    {
        bool becamePoisoned = !lastValue && currentValue;
        lastValue = currentValue;

        if (becamePoisoned)
            ShowRadiationPoisoningMessage(poisoningName);
    }

    private void ShowRadiationPoisoningMessage(string poisoningName)
    {
        if (showRadiationPoisoningMessages)
            UI.HUDMessagePanelController.Queue(RadiationPoisoningMessagePrefix + poisoningName + RadiationPoisoningMessageSuffix);
    }

    private void ApplyGodModeProtectionState()
    {
        vitals.actionPoints = vitals.maxActionPoints;

        ClearRadiationPoisoningFlags();
        CacheRadiationPoisoningStates();

        SetBodyPartNotCrippled(ref bodyPartHealth.leftArmHealth, ref crippling.leftArmCrippled, ref lastLeftArmCrippled);
        SetBodyPartNotCrippled(ref bodyPartHealth.rightArmHealth, ref crippling.rightArmCrippled, ref lastRightArmCrippled);
        SetBodyPartNotCrippled(ref bodyPartHealth.chestHealth, ref crippling.chestCrippled, ref lastChestCrippled);
        SetBodyPartNotCrippled(ref bodyPartHealth.headHealth, ref crippling.headCrippled, ref lastHeadCrippled);
        SetBodyPartNotCrippled(ref bodyPartHealth.leftLegHealth, ref crippling.leftLegCrippled, ref lastLeftLegCrippled);
        SetBodyPartNotCrippled(ref bodyPartHealth.rightLegHealth, ref crippling.rightLegCrippled, ref lastRightLegCrippled);
    }

    private void SetBodyPartNotCrippled(ref float healthValue, ref bool crippledValue, ref bool lastCrippledValue)
    {
        healthValue = Mathf.Max(ClampBodyPartHealthValue(healthValue), CrippledBodyPartHealthThreshold);
        crippledValue = false;
        lastCrippledValue = false;
    }

    private void ClearRadiationPoisoningFlags()
    {
        radiationPoisoning.minorRadiationPosioning = false;
        radiationPoisoning.advancedRadiationPosioning = false;
        radiationPoisoning.criticalRadiationPosioning = false;
        radiationPoisoning.deadlyRadiationPosioning = false;
        radiationPoisoning.fatalRadiationPosioning = false;
    }

    private void UpdateAnimatorParameters()
    {
        if (!CanUpdateAnimatorParameters()) return;

        if (hasAnimatorStateCache &&
            lastCombatModeParam == combatMode &&
            lastWeaponInHandParam == weaponInHand)
        {
            return;
        }

        if (!hasAnimatorStateCache || lastCombatModeParam != combatMode)
        {
            animator.SetBool(CombatModeParam, combatMode);
            lastCombatModeParam = combatMode;
        }

        if (!hasAnimatorStateCache || lastWeaponInHandParam != weaponInHand)
        {
            animator.SetBool(WeaponInHandParam, weaponInHand);
            lastWeaponInHandParam = weaponInHand;
        }

        hasAnimatorStateCache = true;

    }

    private bool CanUpdateAnimatorParameters()
    {
        if (animator == null)
        {
            hasAnimatorStateCache = false;
            return false;
        }

        if (animator.isActiveAndEnabled &&
            animator.runtimeAnimatorController != null &&
            animator.isInitialized)
        {
            return true;
        }

        hasAnimatorStateCache = false;
        return false;
    }
}
