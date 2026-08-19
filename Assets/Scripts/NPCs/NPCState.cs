using System;
using System.Collections.Generic;
using UnityEngine;

public class NPCState : CharacterState
{
    [Serializable]
    private class VitalsCategory
    {
        [SerializeField] public float healthPoints = 100f;
        [SerializeField] public float actionPoints = 100f;
        [SerializeField] public float maxHealthPoints = 100f;
        [SerializeField] public float maxActionPoints = 100f;
        [SerializeField] public float radiation = 0f;
    }

    [Serializable]
    private class RadiationPoisoningCategory
    {
        [SerializeField] public bool minorRadiationPosioning = false;
        [SerializeField] public bool advancedRadiationPosioning = false;
        [SerializeField] public bool criticalRadiationPosioning = false;
        [SerializeField] public bool deadlyRadiationPosioning = false;
        [SerializeField] public bool fatalRadiationPosioning = false;
    }

    [Serializable]
    private class CripplingCategory
    {
        [SerializeField] public bool leftArmCrippled = false;
        [SerializeField] public bool rightArmCrippled = false;
        [SerializeField] public bool chestCrippled = false;
        [SerializeField] public bool headCrippled = false;
        [SerializeField] public bool leftLegCrippled = false;
        [SerializeField] public bool rightLegCrippled = false;
    }

    [Serializable]
    private class BodyPartHealthCategory
    {
        [SerializeField] [Range(0f, 100f)] public float leftArmHealth = 100f;
        [SerializeField] [Range(0f, 100f)] public float rightArmHealth = 100f;
        [SerializeField] [Range(0f, 100f)] public float chestHealth = 100f;
        [SerializeField] [Range(0f, 100f)] public float headHealth = 100f;
        [SerializeField] [Range(0f, 100f)] public float leftLegHealth = 100f;
        [SerializeField] [Range(0f, 100f)] public float rightLegHealth = 100f;
    }

    [Serializable]
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

    [Serializable]
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

    private static readonly int CombatModeParam = Animator.StringToHash("CombatMode");
    private static readonly int WeaponInHandParam = Animator.StringToHash("WeaponInHand");

    [Header("NPC Identity")]
    [SerializeField] private string npcName = "";

    [Header("Vitals")]
    [SerializeField] private VitalsCategory vitals = new VitalsCategory();

    [Header("Radiation Poisoning")]
    [SerializeField] private RadiationPoisoningCategory radiationPoisoning = new RadiationPoisoningCategory();

    [Header("Body Part Health")]
    [SerializeField] private BodyPartHealthCategory bodyPartHealth = new BodyPartHealthCategory();

    [Header("Crippling")]
    [SerializeField] private CripplingCategory crippling = new CripplingCategory();

    [Header("Cripple Messages")]
    [SerializeField] private bool showCrippleMessages = true;

    [Header("S.P.E.C.I.A.L")]
    [SerializeField] private SpecialCategory special = new SpecialCategory();

    [Header("Skills")]
    [SerializeField] private SkillsCategory skills = new SkillsCategory();

    [Header("Perks")]
    [SerializeField] private List<PerkDefinition> perks = new List<PerkDefinition>();

    [Header("Combat")]
    [SerializeField] private bool combatMode = false;

    [Header("Weapon")]
    [SerializeField] private bool weaponInHand = false;

    [Header("Action Points Regen")]
    [SerializeField] private float actionPointsRegenPerSecond = 8f;

    [Header("Loot")]
    [SerializeField] private bool createLootContainerOnDeath = true;
    [SerializeField] private bool transferInventoryToLootContainerOnDeath = true;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    private bool isDead;
    private bool hasTransferredInventoryToDeathLootContainer;
    private Container deathLootContainer;
    private bool hasAnimatorStateCache;
    private bool lastCombatModeParam;
    private bool lastWeaponInHandParam;
    private bool lastLeftArmCrippled;
    private bool lastRightArmCrippled;
    private bool lastChestCrippled;
    private bool lastHeadCrippled;
    private bool lastLeftLegCrippled;
    private bool lastRightLegCrippled;

    public event Action<NPCState> OnDied;
    public event Action<NPCState> OnResurrected;
    public event Action<NPCState, float, float> OnHealthChanged;
    public event Action<NPCState, float, float> OnActionPointsChanged;

    private void Awake()
    {
        if (!animator)
            animator = GetComponentInChildren<Animator>();

        ClampState();
        CacheCrippledStates();
        UpdateRadiationPoisoningFlags();
        UpdateAnimatorParameters();
        isDead = vitals.healthPoints <= 0f;

        if (isDead)
            EnsureDeathLootContainer();
    }

    private void Start()
    {
        UpdateAnimatorParameters();
    }

    private void OnValidate()
    {
        ClampState();
        CacheCrippledStates();
        UpdateRadiationPoisoningFlags();
        hasAnimatorStateCache = false;
    }

    private void Update()
    {
        SyncCrippledStatesFromBodyPartHealth(true);

        if (isDead)
            return;

        if (vitals.actionPoints >= vitals.maxActionPoints)
            return;

        SetActionPoints(vitals.actionPoints + actionPointsRegenPerSecond * Time.deltaTime);
    }

    public string GetNPCName()
    {
        return npcName;
    }

    public float GetHealthPoints()
    {
        return vitals.healthPoints;
    }

    public float GetMaxHealthPoints()
    {
        return vitals.maxHealthPoints;
    }

    public float GetActionPoints()
    {
        return vitals.actionPoints;
    }

    public float GetMaxActionPoints()
    {
        return vitals.maxActionPoints;
    }

    public float GetRadiation()
    {
        return vitals.radiation;
    }

    public bool IsDead()
    {
        return isDead;
    }

    public Container GetDeathLootContainer()
    {
        return deathLootContainer;
    }

    public bool GetCombatMode()
    {
        return combatMode;
    }

    public override bool GetWeaponInHand()
    {
        return weaponInHand;
    }

    public bool GetMinorRadiationPosioning()
    {
        return radiationPoisoning.minorRadiationPosioning;
    }

    public bool GetAdvancedRadiationPosioning()
    {
        return radiationPoisoning.advancedRadiationPosioning;
    }

    public bool GetCriticalRadiationPosioning()
    {
        return radiationPoisoning.criticalRadiationPosioning;
    }

    public bool GetDeadlyRadiationPosioning()
    {
        return radiationPoisoning.deadlyRadiationPosioning;
    }

    public bool GetFatalRadiationPosioning()
    {
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
        return perk && perks.Contains(perk);
    }

    public bool AddPerk(PerkDefinition perk)
    {
        if (!perk || perks.Contains(perk))
            return false;

        perks.Add(perk);
        return true;
    }

    public bool RemovePerk(PerkDefinition perk)
    {
        return perk && perks.Remove(perk);
    }

    public void SetCombatMode(bool value)
    {
        if (isDead)
            value = false;

        if (combatMode == value)
            return;

        combatMode = value;
        UpdateAnimatorParameters();
    }

    public void SetWeaponInHand(bool value)
    {
        if (isDead)
            value = false;

        if (weaponInHand == value)
            return;

        weaponInHand = value;
        UpdateAnimatorParameters();
    }

    public void SetNPCName(string value)
    {
        npcName = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    public void SetHealthPoints(float value)
    {
        bool wasDead = isDead;
        float previous = vitals.healthPoints;
        vitals.healthPoints = Mathf.Clamp(value, 0f, vitals.maxHealthPoints);

        if (!Mathf.Approximately(previous, vitals.healthPoints))
            OnHealthChanged?.Invoke(this, previous, vitals.healthPoints);

        if (vitals.healthPoints <= 0f)
        {
            Die();
        }
        else
        {
            isDead = false;

            if (wasDead)
            {
                UpdateAnimatorParameters();
                OnResurrected?.Invoke(this);
            }
        }
    }

    public void SetMaxHealthPoints(float value)
    {
        vitals.maxHealthPoints = Mathf.Max(0f, value);
        SetHealthPoints(vitals.healthPoints);
    }

    public void RestoreHealth(float amount)
    {
        if (amount <= 0f)
            return;

        SetHealthPoints(vitals.healthPoints + amount);
    }

    public void ApplyDamage(float amount)
    {
        if (amount <= 0f || isDead)
            return;

        SetHealthPoints(vitals.healthPoints - amount);
    }

    public void SetActionPoints(float value)
    {
        float previous = vitals.actionPoints;
        vitals.actionPoints = Mathf.Clamp(value, 0f, vitals.maxActionPoints);

        if (!Mathf.Approximately(previous, vitals.actionPoints))
            OnActionPointsChanged?.Invoke(this, previous, vitals.actionPoints);
    }

    public void SetMaxActionPoints(float value)
    {
        vitals.maxActionPoints = Mathf.Max(0f, value);
        SetActionPoints(vitals.actionPoints);
    }

    public float GetActionPointsRegenPerSecond()
    {
        return actionPointsRegenPerSecond;
    }

    public void SetActionPointsRegenPerSecond(float value)
    {
        actionPointsRegenPerSecond = Mathf.Max(0f, value);
    }

    public bool TrySpendActionPoints(float amount)
    {
        if (amount <= 0f)
            return true;

        if (vitals.actionPoints < amount)
            return false;

        SetActionPoints(vitals.actionPoints - amount);
        return true;
    }

    public void RestoreActionPoints(float amount)
    {
        if (amount <= 0f)
            return;

        SetActionPoints(vitals.actionPoints + amount);
    }

    public void SetRadiation(float value)
    {
        vitals.radiation = Mathf.Max(0f, value);
        UpdateRadiationPoisoningFlags();
    }

    public void AddRadiation(float amount)
    {
        SetRadiation(vitals.radiation + amount);
    }

    public void RemoveRadiation(float amount)
    {
        SetRadiation(vitals.radiation - amount);
    }

    public void SetLeftArmCrippled(bool value)
    {
        SetBodyPartCrippledState(ref bodyPartHealth.leftArmHealth, ref crippling.leftArmCrippled, ref lastLeftArmCrippled, value, "Left Arm");
    }

    public void SetRightArmCrippled(bool value)
    {
        SetBodyPartCrippledState(ref bodyPartHealth.rightArmHealth, ref crippling.rightArmCrippled, ref lastRightArmCrippled, value, "Right Arm");
    }

    public void SetChestCrippled(bool value)
    {
        SetBodyPartCrippledState(ref bodyPartHealth.chestHealth, ref crippling.chestCrippled, ref lastChestCrippled, value, "Chest");
    }

    public void SetHeadCrippled(bool value)
    {
        SetBodyPartCrippledState(ref bodyPartHealth.headHealth, ref crippling.headCrippled, ref lastHeadCrippled, value, "Head");
    }

    public void SetLeftLegCrippled(bool value)
    {
        SetBodyPartCrippledState(ref bodyPartHealth.leftLegHealth, ref crippling.leftLegCrippled, ref lastLeftLegCrippled, value, "Left Leg");
    }

    public void SetRightLegCrippled(bool value)
    {
        SetBodyPartCrippledState(ref bodyPartHealth.rightLegHealth, ref crippling.rightLegCrippled, ref lastRightLegCrippled, value, "Right Leg");
    }

    public void SetLeftArmHealth(float value)
    {
        SetBodyPartHealth(ref bodyPartHealth.leftArmHealth, ref crippling.leftArmCrippled, ref lastLeftArmCrippled, value, "Left Arm");
    }

    public void SetRightArmHealth(float value)
    {
        SetBodyPartHealth(ref bodyPartHealth.rightArmHealth, ref crippling.rightArmCrippled, ref lastRightArmCrippled, value, "Right Arm");
    }

    public void SetChestHealth(float value)
    {
        SetBodyPartHealth(ref bodyPartHealth.chestHealth, ref crippling.chestCrippled, ref lastChestCrippled, value, "Chest");
    }

    public void SetHeadHealth(float value)
    {
        SetBodyPartHealth(ref bodyPartHealth.headHealth, ref crippling.headCrippled, ref lastHeadCrippled, value, "Head");
    }

    public void SetLeftLegHealth(float value)
    {
        SetBodyPartHealth(ref bodyPartHealth.leftLegHealth, ref crippling.leftLegCrippled, ref lastLeftLegCrippled, value, "Left Leg");
    }

    public void SetRightLegHealth(float value)
    {
        SetBodyPartHealth(ref bodyPartHealth.rightLegHealth, ref crippling.rightLegCrippled, ref lastRightLegCrippled, value, "Right Leg");
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

    public void SetSneak(int value)
    {
        skills.sneak = Mathf.Clamp(value, 0, 100);
    }

    public void ApplyDefinition(NPCDefinition definition)
    {
        if (!definition)
            return;

        SetNPCName(definition.GetNPCName());

        NPCDefinition.NPCVitalsDefinition definitionVitals = definition.GetVitals();
        if (definitionVitals == null)
            return;

        SetMaxHealthPoints(definitionVitals.GetMaxHealthPoints());
        SetMaxActionPoints(definitionVitals.GetMaxActionPoints());
        SetHealthPoints(definitionVitals.GetStartingHealthPoints());
        SetActionPoints(definitionVitals.GetStartingActionPoints());
        SetActionPointsRegenPerSecond(definitionVitals.GetActionPointsRegenPerSecond());
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;
        combatMode = false;
        weaponInHand = false;
        UpdateAnimatorParameters();
        EnsureDeathLootContainer();
        OnDied?.Invoke(this);
    }

    private void EnsureDeathLootContainer()
    {
        if (!Application.isPlaying || !createLootContainerOnDeath)
            return;

        GameObject lootContainerHost = ResolveLootContainerHost();
        if (!lootContainerHost)
            return;

        if (!deathLootContainer)
            deathLootContainer = lootContainerHost.GetComponent<Container>();

        if (!deathLootContainer)
            deathLootContainer = lootContainerHost.AddComponent<Container>();

        deathLootContainer.ConfigureAsLootContainer(GetDeathLootContainerName());
        TryTransferInventoryToDeathLootContainer(lootContainerHost);
    }

    private void TryTransferInventoryToDeathLootContainer(GameObject lootContainerHost)
    {
        if (!transferInventoryToLootContainerOnDeath ||
            hasTransferredInventoryToDeathLootContainer ||
            !deathLootContainer)
        {
            return;
        }

        NPCInventory npcInventory = ResolveNpcInventory(lootContainerHost);
        if (!npcInventory)
        {
            hasTransferredInventoryToDeathLootContainer = true;
            return;
        }

        npcInventory.TransferAllItemsToContainer(deathLootContainer);
        hasTransferredInventoryToDeathLootContainer = true;
    }

    private GameObject ResolveLootContainerHost()
    {
        NPC npc = GetComponent<NPC>();
        if (!npc)
            npc = GetComponentInParent<NPC>();
        if (!npc)
            npc = GetComponentInChildren<NPC>(true);

        return npc ? npc.gameObject : gameObject;
    }

    private NPCInventory ResolveNpcInventory(GameObject lootContainerHost)
    {
        NPCInventory inventory = null;

        if (lootContainerHost)
            inventory = lootContainerHost.GetComponent<NPCInventory>();

        if (!inventory)
            inventory = GetComponent<NPCInventory>();

        if (!inventory)
            inventory = GetComponentInParent<NPCInventory>();

        if (!inventory)
            inventory = GetComponentInChildren<NPCInventory>(true);

        return inventory;
    }

    private string GetDeathLootContainerName()
    {
        NPC npc = GetComponent<NPC>();
        if (!npc)
            npc = GetComponentInParent<NPC>();
        if (!npc)
            npc = GetComponentInChildren<NPC>(true);

        string resolvedName = npc ? npc.GetNPCName() : npcName;
        return string.IsNullOrWhiteSpace(resolvedName) ? "NPC" : resolvedName.Trim();
    }

    private void ClampState()
    {
        npcName = string.IsNullOrWhiteSpace(npcName) ? string.Empty : npcName.Trim();

        if (vitals == null)
            vitals = new VitalsCategory();

        if (radiationPoisoning == null)
            radiationPoisoning = new RadiationPoisoningCategory();

        if (crippling == null)
            crippling = new CripplingCategory();

        if (bodyPartHealth == null)
            bodyPartHealth = new BodyPartHealthCategory();

        if (special == null)
            special = new SpecialCategory();

        if (skills == null)
            skills = new SkillsCategory();

        if (perks == null)
            perks = new List<PerkDefinition>();

        vitals.maxHealthPoints = Mathf.Max(0f, vitals.maxHealthPoints);
        vitals.maxActionPoints = Mathf.Max(0f, vitals.maxActionPoints);
        vitals.healthPoints = Mathf.Clamp(vitals.healthPoints, 0f, vitals.maxHealthPoints);
        vitals.actionPoints = Mathf.Clamp(vitals.actionPoints, 0f, vitals.maxActionPoints);
        vitals.radiation = Mathf.Max(0f, vitals.radiation);
        actionPointsRegenPerSecond = Mathf.Max(0f, actionPointsRegenPerSecond);
        ClampBodyPartHealth();
        SyncCrippledStatesFromBodyPartHealth(false);

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
    }

    private void ClampBodyPartHealth()
    {
        bodyPartHealth.leftArmHealth = ClampBodyPartHealthValue(bodyPartHealth.leftArmHealth);
        bodyPartHealth.rightArmHealth = ClampBodyPartHealthValue(bodyPartHealth.rightArmHealth);
        bodyPartHealth.chestHealth = ClampBodyPartHealthValue(bodyPartHealth.chestHealth);
        bodyPartHealth.headHealth = ClampBodyPartHealthValue(bodyPartHealth.headHealth);
        bodyPartHealth.leftLegHealth = ClampBodyPartHealthValue(bodyPartHealth.leftLegHealth);
        bodyPartHealth.rightLegHealth = ClampBodyPartHealthValue(bodyPartHealth.rightLegHealth);
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

    private void SyncCrippledStatesFromBodyPartHealth(bool notify)
    {
        SyncCrippledStateFromBodyPartHealth(ref crippling.leftArmCrippled, ref lastLeftArmCrippled, bodyPartHealth.leftArmHealth, "Left Arm", notify);
        SyncCrippledStateFromBodyPartHealth(ref crippling.rightArmCrippled, ref lastRightArmCrippled, bodyPartHealth.rightArmHealth, "Right Arm", notify);
        SyncCrippledStateFromBodyPartHealth(ref crippling.chestCrippled, ref lastChestCrippled, bodyPartHealth.chestHealth, "Chest", notify);
        SyncCrippledStateFromBodyPartHealth(ref crippling.headCrippled, ref lastHeadCrippled, bodyPartHealth.headHealth, "Head", notify);
        SyncCrippledStateFromBodyPartHealth(ref crippling.leftLegCrippled, ref lastLeftLegCrippled, bodyPartHealth.leftLegHealth, "Left Leg", notify);
        SyncCrippledStateFromBodyPartHealth(ref crippling.rightLegCrippled, ref lastRightLegCrippled, bodyPartHealth.rightLegHealth, "Right Leg", notify);
    }

    private void SyncCrippledStateFromBodyPartHealth(ref bool currentValue, ref bool lastValue, float healthValue, string areaName, bool notify)
    {
        bool nextValue = IsCrippledBodyPartHealth(healthValue);

        if (notify)
            SetCrippledState(ref currentValue, ref lastValue, nextValue, areaName);
        else
            currentValue = nextValue;
    }

    private void SetBodyPartHealth(ref float healthValue, ref bool crippledValue, ref bool lastCrippledValue, float nextHealthValue, string areaName)
    {
        healthValue = ClampBodyPartHealthValue(nextHealthValue);
        SetCrippledState(ref crippledValue, ref lastCrippledValue, IsCrippledBodyPartHealth(healthValue), areaName);
    }

    private void SetBodyPartCrippledState(ref float healthValue, ref bool crippledValue, ref bool lastCrippledValue, bool nextCrippledValue, string areaName)
    {
        healthValue = ClampBodyPartHealthValue(healthValue);

        if (nextCrippledValue && healthValue >= CrippledBodyPartHealthThreshold)
            healthValue = ManualCrippledBodyPartHealth;
        else if (!nextCrippledValue && healthValue < CrippledBodyPartHealthThreshold)
            healthValue = CrippledBodyPartHealthThreshold;

        SetCrippledState(ref crippledValue, ref lastCrippledValue, nextCrippledValue, areaName);
    }

    private static float ClampBodyPartHealthValue(float value)
    {
        return Mathf.Clamp(value, BodyPartHealthMin, BodyPartHealthMax);
    }

    private static bool IsCrippledBodyPartHealth(float healthValue)
    {
        return healthValue < CrippledBodyPartHealthThreshold;
    }

    private void SetCrippledState(ref bool currentValue, ref bool lastValue, bool nextValue, string areaName)
    {
        bool becameCrippled = !lastValue && nextValue;
        currentValue = nextValue;
        lastValue = nextValue;

        if (becameCrippled)
            ShowCrippledMessage(areaName);
    }

    private void ShowCrippledMessage(string areaName)
    {
        if (!showCrippleMessages || !Application.isPlaying)
            return;

        UI.HUDMessagePanelController.Queue(GetCrippleMessageNpcName() + " " + areaName + CrippledMessageSuffix);
    }

    private string GetCrippleMessageNpcName()
    {
        NPC npc = GetComponent<NPC>();
        if (!npc)
            npc = GetComponentInParent<NPC>();
        if (!npc)
            npc = GetComponentInChildren<NPC>(true);

        string resolvedName = npc ? npc.GetNPCName() : npcName;
        return string.IsNullOrWhiteSpace(resolvedName) ? "NPC" : resolvedName.Trim();
    }

    private void UpdateRadiationPoisoningFlags()
    {
        float currentRadiation = vitals.radiation;
        radiationPoisoning.fatalRadiationPosioning = currentRadiation >= FatalRadiationThreshold;
        radiationPoisoning.deadlyRadiationPosioning = !radiationPoisoning.fatalRadiationPosioning && currentRadiation >= DeadlyRadiationThreshold;
        radiationPoisoning.criticalRadiationPosioning = !radiationPoisoning.fatalRadiationPosioning && !radiationPoisoning.deadlyRadiationPosioning && currentRadiation >= CriticalRadiationThreshold;
        radiationPoisoning.advancedRadiationPosioning = !radiationPoisoning.fatalRadiationPosioning && !radiationPoisoning.deadlyRadiationPosioning && !radiationPoisoning.criticalRadiationPosioning && currentRadiation >= AdvancedRadiationThreshold;
        radiationPoisoning.minorRadiationPosioning = !radiationPoisoning.fatalRadiationPosioning && !radiationPoisoning.deadlyRadiationPosioning && !radiationPoisoning.criticalRadiationPosioning && !radiationPoisoning.advancedRadiationPosioning && currentRadiation >= MinorRadiationThreshold;
    }

    private void UpdateAnimatorParameters()
    {
        if (!CanUpdateAnimatorParameters())
            return;

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
        if (!animator)
        {
            hasAnimatorStateCache = false;
            return false;
        }

        if (!animator.isActiveAndEnabled ||
            !animator.runtimeAnimatorController ||
            !animator.isInitialized)
        {
            hasAnimatorStateCache = false;
            return false;
        }

        return true;
    }
}
