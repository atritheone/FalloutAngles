using System;
using System.Collections.Generic;
using UnityEngine;

public enum PerkCategory
{
    Regular,
    Special,
    Quest,
    Cut
}

public enum PerkContentSource
{
    BaseGame,
    OperationAnchorage,
    ThePitt,
    BrokenSteel,
    PointLookout,
    MothershipZeta,
    Custom
}

public enum PerkSpecialStat
{
    Strength,
    Perception,
    Endurance,
    Charisma,
    Intelligence,
    Agility,
    Luck
}

public enum PerkGender
{
    Any,
    Male,
    Female
}

public enum PerkKarmaRequirement
{
    Any,
    VeryEvil,
    Evil,
    Neutral,
    Good,
    VeryGood,
    NotNeutral,
    HigherThanVeryEvil,
    LowerThanVeryGood,
    Custom
}

public enum PerkEffectTarget
{
    None,
    SpecialStat,
    Skill,
    HealthPoints,
    ActionPoints,
    CarryWeight,
    Damage,
    DamageResistance,
    RadiationResistance,
    PoisonResistance,
    FireResistance,
    CriticalChance,
    CriticalDamage,
    VatsAccuracy,
    VatsHeadAccuracy,
    ExperienceGain,
    SkillBookPoints,
    SkillPointsPerLevel,
    UnarmedDamage,
    DialogueOptions,
    ContainerCaps,
    ContainerAmmo,
    VendorPrices,
    VendorDiscount,
    AddictionChance,
    ChemDuration,
    AidHealing,
    LimbDamageTaken,
    TrapTriggering,
    TerminalRetry,
    LockRetry,
    ArmorUsage,
    MapLocations,
    Karma,
    SchematicKnowledge,
    RestedEffect,
    CompanionRespawn,
    ItemConversion,
    RadiationDecay,
    LimbRegeneration,
    Custom
}

public enum PerkEffectOperation
{
    AddFlat,
    AddPercent,
    Multiply,
    SetValue,
    Unlock,
    Disable,
    Restore,
    Reveal,
    Spawn,
    Convert,
    Custom
}

public enum PerkConditionType
{
    Always,
    Gender,
    Karma,
    TimeOfDay,
    HealthAtOrBelowPercent,
    Outdoors,
    InVats,
    SameQueuedBodyPartInVats,
    WeaponCategory,
    TargetType,
    TargetSleeping,
    TargetOppositeSex,
    Sneaking,
    StandingStill,
    RadiationPoisoning,
    HasItem,
    QuestState,
    Custom
}

public enum PerkWeaponCategory
{
    Any,
    OneHanded,
    TwoHanded,
    Melee,
    Unarmed,
    Explosive,
    FireBased,
    Alien,
    AutoAxe,
    SmallGuns,
    EnergyWeapons,
    BigGuns,
    Custom
}

public enum PerkTargetType
{
    Any,
    OppositeSexCharacter,
    Insect,
    Animal,
    Robot,
    Ghoul,
    Mirelurk,
    SleepingNpc,
    ChildDialogue,
    Custom
}

[Serializable]
public class PerkSpecialRequirement
{
    [SerializeField] private PerkSpecialStat stat = PerkSpecialStat.Strength;
    [SerializeField, Min(1)] private int minimumValue = 1;
    [SerializeField] private bool ignoreTemporaryModifiers = true;
    [SerializeField] private bool allowLucky8BallException = true;

    public void Sanitize()
    {
        minimumValue = Mathf.Max(1, minimumValue);
    }

    public PerkSpecialStat GetStat()
    {
        return stat;
    }

    public int GetMinimumValue()
    {
        return Mathf.Max(1, minimumValue);
    }

    public bool IgnoresTemporaryModifiers()
    {
        return ignoreTemporaryModifiers;
    }

    public bool AllowsLucky8BallException()
    {
        return allowLucky8BallException;
    }
}

[Serializable]
public class PerkSkillRequirement
{
    [SerializeField] private PlayerSkill skill = PlayerSkill.None;
    [SerializeField, Range(0, 100)] private int minimumValue;

    public void Sanitize()
    {
        minimumValue = Mathf.Clamp(minimumValue, 0, PlayerState.MaxPlayerSkillValue);
    }

    public PlayerSkill GetSkill()
    {
        return skill;
    }

    public int GetMinimumValue()
    {
        return Mathf.Clamp(minimumValue, 0, PlayerState.MaxPlayerSkillValue);
    }
}

[Serializable]
public class PerkQuestRequirement
{
    [SerializeField] private QuestDefinition quest;
    [SerializeField] private string questId = "";
    [SerializeField, TextArea(2, 5)] private string requiredOutcome = "";

    public void Sanitize()
    {
        questId = string.IsNullOrWhiteSpace(questId) ? "" : questId.Trim();
    }

    public QuestDefinition GetQuest()
    {
        return quest;
    }

    public string GetQuestId()
    {
        return quest ? quest.GetQuestId() : questId;
    }

    public string GetRequiredOutcome()
    {
        return requiredOutcome;
    }
}

[Serializable]
public class PerkEffectCondition
{
    [SerializeField] private PerkConditionType conditionType = PerkConditionType.Always;
    [SerializeField] private PerkGender gender = PerkGender.Any;
    [SerializeField] private PerkKarmaRequirement karmaRequirement = PerkKarmaRequirement.Any;
    [SerializeField] private PerkWeaponCategory weaponCategory = PerkWeaponCategory.Any;
    [SerializeField] private PerkTargetType targetType = PerkTargetType.Any;
    [SerializeField, Range(0f, 24f)] private float startHour;
    [SerializeField, Range(0f, 24f)] private float endHour;
    [SerializeField, Range(0f, 100f)] private float thresholdPercent;
    [SerializeField] private string requiredItemId = "";
    [SerializeField] private QuestDefinition quest;
    [SerializeField] private string questId = "";
    [SerializeField, TextArea(2, 5)] private string conditionDetails = "";

    public void Sanitize()
    {
        startHour = Mathf.Clamp(startHour, 0f, 24f);
        endHour = Mathf.Clamp(endHour, 0f, 24f);
        thresholdPercent = Mathf.Clamp(thresholdPercent, 0f, 100f);
        requiredItemId = string.IsNullOrWhiteSpace(requiredItemId) ? "" : requiredItemId.Trim();
        questId = string.IsNullOrWhiteSpace(questId) ? "" : questId.Trim();
    }

    public PerkConditionType GetConditionType()
    {
        return conditionType;
    }

    public PerkGender GetGender()
    {
        return gender;
    }

    public PerkKarmaRequirement GetKarmaRequirement()
    {
        return karmaRequirement;
    }

    public PerkWeaponCategory GetWeaponCategory()
    {
        return weaponCategory;
    }

    public PerkTargetType GetTargetType()
    {
        return targetType;
    }

    public float GetStartHour()
    {
        return startHour;
    }

    public float GetEndHour()
    {
        return endHour;
    }

    public float GetThresholdPercent()
    {
        return thresholdPercent;
    }

    public string GetRequiredItemId()
    {
        return requiredItemId;
    }

    public QuestDefinition GetQuest()
    {
        return quest;
    }

    public string GetQuestId()
    {
        return quest ? quest.GetQuestId() : questId;
    }

    public string GetConditionDetails()
    {
        return conditionDetails;
    }
}

[Serializable]
public class PerkEffectDefinition
{
    [SerializeField] private PerkEffectTarget target = PerkEffectTarget.None;
    [SerializeField] private PerkSpecialStat specialStatTarget = PerkSpecialStat.Strength;
    [SerializeField] private PlayerSkill skillTarget = PlayerSkill.None;
    [SerializeField] private PerkEffectOperation operation = PerkEffectOperation.AddFlat;
    [SerializeField] private float magnitude;
    [SerializeField, Min(0f)] private float durationSeconds;
    [SerializeField, Range(0f, 100f)] private float chancePercent;
    [SerializeField] private bool scalesWithRank;
    [SerializeField] private List<PerkEffectCondition> conditions = new List<PerkEffectCondition>();
    [SerializeField] private string effectId = "";
    [SerializeField, TextArea(2, 6)] private string notes = "";

    public void Sanitize()
    {
        durationSeconds = Mathf.Max(0f, durationSeconds);
        chancePercent = Mathf.Clamp(chancePercent, 0f, 100f);
        effectId = string.IsNullOrWhiteSpace(effectId) ? "" : effectId.Trim();

        if (conditions == null)
            conditions = new List<PerkEffectCondition>();

        for (int i = 0; i < conditions.Count; i++)
        {
            if (conditions[i] == null)
                conditions[i] = new PerkEffectCondition();

            conditions[i].Sanitize();
        }
    }

    public PerkEffectTarget GetTarget()
    {
        return target;
    }

    public PerkSpecialStat GetSpecialStatTarget()
    {
        return specialStatTarget;
    }

    public PlayerSkill GetSkillTarget()
    {
        return skillTarget;
    }

    public PerkEffectOperation GetOperation()
    {
        return operation;
    }

    public float GetMagnitude()
    {
        return magnitude;
    }

    public float GetDurationSeconds()
    {
        return durationSeconds;
    }

    public float GetChancePercent()
    {
        return chancePercent;
    }

    public bool ScalesWithRank()
    {
        return scalesWithRank;
    }

    public List<PerkEffectCondition> GetConditions()
    {
        return conditions;
    }

    public string GetEffectId()
    {
        return effectId;
    }

    public string GetNotes()
    {
        return notes;
    }
}

[Serializable]
public class PerkRankDefinition
{
    [SerializeField, Min(1)] private int rank = 1;
    [SerializeField, TextArea(2, 6)] private string benefit = "";
    [SerializeField] private List<PerkEffectDefinition> effects = new List<PerkEffectDefinition>();

    public void Sanitize(int index)
    {
        rank = Mathf.Max(1, rank);

        if (index >= 0 && rank == 1)
            rank = index + 1;

        if (effects == null)
            effects = new List<PerkEffectDefinition>();

        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] == null)
                effects[i] = new PerkEffectDefinition();

            effects[i].Sanitize();
        }
    }

    public int GetRank()
    {
        return Mathf.Max(1, rank);
    }

    public string GetBenefit()
    {
        return benefit;
    }

    public List<PerkEffectDefinition> GetEffects()
    {
        return effects;
    }
}

[CreateAssetMenu(fileName = "Perk", menuName = "Fallout Angles/Perk", order = 10)]
public class PerkDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string perkId = "";
    [SerializeField] private string perkName = "";
    [SerializeField] [TextArea(2, 6)] private string description = "";
    [SerializeField] private List<string> formCodes = new List<string>();
    [SerializeField] private string editorId = "";
    [SerializeField] private string referenceUrl = "";

    [Header("Presentation")]
    [SerializeField] private Sprite icon;

    [Header("Source")]
    [SerializeField] private PerkCategory category = PerkCategory.Regular;
    [SerializeField] private PerkContentSource contentSource = PerkContentSource.BaseGame;
    [SerializeField] private string customContentSource = "";
    [SerializeField] private bool selectableAtLevelUp = true;
    [SerializeField] private bool cutContent;

    [Header("Progression")]
    [SerializeField, Min(1)] private int maxRank = 1;
    [SerializeField, Min(0)] private int requiredLevel;
    [SerializeField] private PerkGender genderRequirement = PerkGender.Any;
    [SerializeField] private PerkKarmaRequirement karmaRequirement = PerkKarmaRequirement.Any;
    [SerializeField] private List<PerkSpecialRequirement> specialRequirements = new List<PerkSpecialRequirement>();
    [SerializeField] private List<PerkSkillRequirement> skillRequirements = new List<PerkSkillRequirement>();
    [SerializeField] private List<PerkDefinition> prerequisitePerks = new List<PerkDefinition>();
    [SerializeField] private List<PerkDefinition> mutuallyExclusivePerks = new List<PerkDefinition>();
    [SerializeField] private List<string> customRequirements = new List<string>();

    [Header("Acquisition")]
    [SerializeField] private QuestDefinition foundInQuest;
    [SerializeField] private List<QuestDefinition> relatedQuests = new List<QuestDefinition>();
    [SerializeField] private List<PerkQuestRequirement> questRequirements = new List<PerkQuestRequirement>();
    [SerializeField] [TextArea(2, 6)] private string acquisitionDescription = "";
    [SerializeField] private bool unsafeToGrantDirectly;

    [Header("Benefits")]
    [SerializeField] [TextArea(2, 8)] private string benefitSummary = "";
    [SerializeField] private List<PerkRankDefinition> rankBenefits = new List<PerkRankDefinition>();
    [SerializeField] private List<PerkEffectDefinition> effects = new List<PerkEffectDefinition>();

    [Header("Notes")]
    [SerializeField] [TextArea(2, 8)] private string designerNotes = "";

    private void OnValidate()
    {
        perkId = string.IsNullOrWhiteSpace(perkId) ? name : perkId.Trim();
        perkName = string.IsNullOrWhiteSpace(perkName) ? name : perkName.Trim();
        editorId = string.IsNullOrWhiteSpace(editorId) ? "" : editorId.Trim();
        referenceUrl = string.IsNullOrWhiteSpace(referenceUrl) ? "" : referenceUrl.Trim();
        customContentSource = string.IsNullOrWhiteSpace(customContentSource) ? "" : customContentSource.Trim();
        maxRank = Mathf.Max(1, maxRank);
        requiredLevel = Mathf.Max(0, requiredLevel);

        if (category == PerkCategory.Cut)
            cutContent = true;

        if (category != PerkCategory.Regular || cutContent)
            selectableAtLevelUp = false;

        SanitizeStringList(formCodes);
        SanitizeStringList(customRequirements);
        SanitizeObjectList(prerequisitePerks);
        SanitizeObjectList(mutuallyExclusivePerks);
        SanitizeObjectList(relatedQuests);

        if (specialRequirements == null)
            specialRequirements = new List<PerkSpecialRequirement>();

        for (int i = 0; i < specialRequirements.Count; i++)
        {
            if (specialRequirements[i] == null)
                specialRequirements[i] = new PerkSpecialRequirement();

            specialRequirements[i].Sanitize();
        }

        if (skillRequirements == null)
            skillRequirements = new List<PerkSkillRequirement>();

        for (int i = 0; i < skillRequirements.Count; i++)
        {
            if (skillRequirements[i] == null)
                skillRequirements[i] = new PerkSkillRequirement();

            skillRequirements[i].Sanitize();
        }

        if (questRequirements == null)
            questRequirements = new List<PerkQuestRequirement>();

        for (int i = 0; i < questRequirements.Count; i++)
        {
            if (questRequirements[i] == null)
                questRequirements[i] = new PerkQuestRequirement();

            questRequirements[i].Sanitize();
        }

        if (rankBenefits == null)
            rankBenefits = new List<PerkRankDefinition>();

        for (int i = 0; i < rankBenefits.Count; i++)
        {
            if (rankBenefits[i] == null)
                rankBenefits[i] = new PerkRankDefinition();

            rankBenefits[i].Sanitize(i);
            maxRank = Mathf.Max(maxRank, rankBenefits[i].GetRank());
        }

        if (effects == null)
            effects = new List<PerkEffectDefinition>();

        for (int i = 0; i < effects.Count; i++)
        {
            if (effects[i] == null)
                effects[i] = new PerkEffectDefinition();

            effects[i].Sanitize();
        }
    }

    public string GetPerkId()
    {
        return string.IsNullOrWhiteSpace(perkId) ? name : perkId;
    }

    public string GetPerkName()
    {
        return string.IsNullOrWhiteSpace(perkName) ? name : perkName;
    }

    public string GetDescription()
    {
        return description;
    }

    public List<string> GetFormCodes()
    {
        return formCodes;
    }

    public string GetEditorId()
    {
        return editorId;
    }

    public string GetReferenceUrl()
    {
        return referenceUrl;
    }

    public Sprite GetIcon()
    {
        return icon;
    }

    public PerkCategory GetCategory()
    {
        return category;
    }

    public PerkContentSource GetContentSource()
    {
        return contentSource;
    }

    public string GetCustomContentSource()
    {
        return customContentSource;
    }

    public bool IsSelectableAtLevelUp()
    {
        return selectableAtLevelUp;
    }

    public bool IsCutContent()
    {
        return cutContent;
    }

    public int GetMaxRank()
    {
        return Mathf.Max(1, maxRank);
    }

    public int GetRequiredLevel()
    {
        return Mathf.Max(0, requiredLevel);
    }

    public PerkGender GetGenderRequirement()
    {
        return genderRequirement;
    }

    public PerkKarmaRequirement GetKarmaRequirement()
    {
        return karmaRequirement;
    }

    public List<PerkSpecialRequirement> GetSpecialRequirements()
    {
        return specialRequirements;
    }

    public List<PerkSkillRequirement> GetSkillRequirements()
    {
        return skillRequirements;
    }

    public List<PerkDefinition> GetPrerequisitePerks()
    {
        return prerequisitePerks;
    }

    public List<PerkDefinition> GetMutuallyExclusivePerks()
    {
        return mutuallyExclusivePerks;
    }

    public List<string> GetCustomRequirements()
    {
        return customRequirements;
    }

    public QuestDefinition GetFoundInQuest()
    {
        return foundInQuest;
    }

    public List<QuestDefinition> GetRelatedQuests()
    {
        return relatedQuests;
    }

    public List<PerkQuestRequirement> GetQuestRequirements()
    {
        return questRequirements;
    }

    public string GetAcquisitionDescription()
    {
        return acquisitionDescription;
    }

    public bool IsUnsafeToGrantDirectly()
    {
        return unsafeToGrantDirectly;
    }

    public string GetBenefitSummary()
    {
        return string.IsNullOrWhiteSpace(benefitSummary) ? description : benefitSummary;
    }

    public List<PerkRankDefinition> GetRankBenefits()
    {
        return rankBenefits;
    }

    public List<PerkEffectDefinition> GetEffects()
    {
        return effects;
    }

    public string GetDesignerNotes()
    {
        return designerNotes;
    }

    private static void SanitizeStringList(List<string> values)
    {
        if (values == null)
            return;

        for (int i = values.Count - 1; i >= 0; i--)
        {
            string value = values[i];
            if (string.IsNullOrWhiteSpace(value))
            {
                values.RemoveAt(i);
                continue;
            }

            values[i] = value.Trim();
        }
    }

    private static void SanitizeObjectList<T>(List<T> values) where T : UnityEngine.Object
    {
        if (values == null)
            return;

        for (int i = values.Count - 1; i >= 0; i--)
        {
            if (!values[i])
                values.RemoveAt(i);
        }
    }
}
