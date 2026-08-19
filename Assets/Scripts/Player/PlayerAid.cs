// imports
using System.Collections.Generic;
using UnityEngine;



// class
public class PlayerAid : MonoBehaviour
{

    // variables
    [Header("References")]
    [SerializeField] private PlayerState playerState;
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private PlayerWeaponController playerWeaponController;

    [Header("Debug")]
    [SerializeField] private bool logUnsupportedEffects = true;

    [Header("Skill Experience")]
    [SerializeField] private bool awardMedicineSkillExperience = true;
    [SerializeField, Min(0f)] private float medicineSkillExperiencePerAidUse = 10f;
    [SerializeField, Min(0f)] private float medicineSkillExperiencePerAppliedEffect = 2f;



    // methods
    private void Awake()
    {
        ResolveDependencies();
    }


    public bool TryConsumeInventoryEntry(PlayerInventory.InventoryEntry inventoryEntry)
    {
        // Stop if inventory entry is missing.
        if (inventoryEntry == null) return false;

        // Stop if entry is not an aid definition.
        if (!(inventoryEntry.GetItemDefinition() is AidDefinition aidDefinition)) return false;

        ResolveDependencies();

        // Stop if inventory cannot be resolved.
        if (!playerInventory) return false;

        // Apply all supported effects before consuming the item.
        int appliedEffectCount = ApplyAidEffects(aidDefinition);

        // Consume one item instance from this entry.
        bool consumed = playerInventory.RemoveInventoryEntry(inventoryEntry, 1);
        if (consumed && appliedEffectCount > 0)
            AwardMedicineSkillExperience(appliedEffectCount);

        return consumed;
    }


    private void ResolveDependencies()
    {
        if (!playerState)
            playerState = FindAnyObjectByType<PlayerState>();

        if (!playerInventory)
            playerInventory = FindAnyObjectByType<PlayerInventory>();

        if (!playerWeaponController)
            playerWeaponController = FindAnyObjectByType<PlayerWeaponController>();
    }


    private int ApplyAidEffects(AidDefinition aidDefinition)
    {
        // Stop if definition is missing.
        if (!aidDefinition) return 0;

        List<AidEffectDefinition> effects = aidDefinition.GetEffects();
        if (effects == null || effects.Count == 0)
            return 0;

        int appliedEffectCount = 0;

        for (int i = 0; i < effects.Count; i++)
        {
            AidEffectDefinition effect = effects[i];
            if (effect == null) continue;

            if (TryApplyEffect(effect))
                appliedEffectCount++;
        }

        if (aidDefinition.CanCauseAddiction() && logUnsupportedEffects)
        {
            Debug.Log(
                $"Aid '{aidDefinition.GetDisplayName()}' can cause addiction, but addiction systems are not implemented yet.",
                this);
        }

        return appliedEffectCount;
    }


    private void AwardMedicineSkillExperience(int appliedEffectCount)
    {
        if (!awardMedicineSkillExperience || !playerState)
            return;

        float experienceAmount = medicineSkillExperiencePerAidUse +
                                 Mathf.Max(0, appliedEffectCount) * medicineSkillExperiencePerAppliedEffect;

        if (experienceAmount <= 0f)
            return;

        playerState.AddSkillExperience(PlayerSkill.Medicine, experienceAmount);
    }


    private bool TryApplyEffect(AidEffectDefinition effect)
    {
        // Stop if effect is missing.
        if (effect == null) return false;

        ResolveDependencies();

        AidEffectTarget target = effect.GetTarget();
        float magnitude = ResolveScaledMagnitude(effect);
        bool isTimedEffect = effect.GetDurationSeconds() > 0f;

        // Skip timed effects for targets that do not yet support expiration/reversion.
        if (isTimedEffect && !CanApplyTimedEffectNow(target))
        {
            LogUnsupported(
                $"Timed aid effect on target '{target}' is not implemented yet and was skipped.",
                effect);
            return false;
        }

        // Stop if this effect needs player stats but no player state exists.
        if (IsPlayerStateIntegerTarget(target) && !playerState)
            return false;

        switch (target)
        {
            case AidEffectTarget.Health:
                return ApplyHealthEffect(effect, magnitude);

            case AidEffectTarget.Radiation:
                return ApplyRadiationEffect(effect, magnitude);

            case AidEffectTarget.ActionPoints:
                return ApplyActionPointsEffect(effect, magnitude, effect.ModifiesMaximumValue());

            case AidEffectTarget.MaxActionPoints:
                return ApplyActionPointsEffect(effect, magnitude, true);

            case AidEffectTarget.Strength:
                return ApplyIntegerPlayerStateEffect(playerState.GetStrength, playerState.SetStrength, effect, magnitude);

            case AidEffectTarget.Perception:
                return ApplyIntegerPlayerStateEffect(playerState.GetPerception, playerState.SetPerception, effect, magnitude);

            case AidEffectTarget.Endurance:
                return ApplyIntegerPlayerStateEffect(playerState.GetEndurance, playerState.SetEndurance, effect, magnitude);

            case AidEffectTarget.Charisma:
                return ApplyIntegerPlayerStateEffect(playerState.GetCharisma, playerState.SetCharisma, effect, magnitude);

            case AidEffectTarget.Intelligence:
                return ApplyIntegerPlayerStateEffect(playerState.GetIntelligence, playerState.SetIntelligence, effect, magnitude);

            case AidEffectTarget.Agility:
                return ApplyIntegerPlayerStateEffect(playerState.GetAgility, playerState.SetAgility, effect, magnitude);

            case AidEffectTarget.Luck:
                return ApplyIntegerPlayerStateEffect(playerState.GetLuck, playerState.SetLuck, effect, magnitude);

            case AidEffectTarget.SneakSkill:
                return ApplyIntegerPlayerStateEffect(playerState.GetSneak, playerState.SetSneak, effect, magnitude);

            case AidEffectTarget.DamageResistance:
                return ApplyDamageResistanceEffect(effect, magnitude);

            case AidEffectTarget.BottleCaps:
                return ApplyBottleCapsEffect(effect, magnitude);

            case AidEffectTarget.EquippedWeaponCondition:
                return ApplyEquippedWeaponConditionEffect(effect, magnitude);

            case AidEffectTarget.DamagePercent:
            case AidEffectTarget.RadiationResistance:
            case AidEffectTarget.FireResistance:
            case AidEffectTarget.StealthField:
            case AidEffectTarget.RandomEffectBundle:
                LogUnsupported($"Aid effect target '{target}' is not implemented yet.", effect);
                return false;

            default:
                LogUnsupported($"Unknown aid effect target '{target}'.", effect);
                return false;
        }
    }


    private float ResolveScaledMagnitude(AidEffectDefinition effect)
    {
        if (effect == null) return 0f;

        float scaleMultiplier = 1f;

        if (effect.ScalesWithMedicineSkill() && playerState)
            scaleMultiplier += Mathf.Max(0, playerState.GetMedicine()) / 100f;

        if (effect.ScalesWithRepairSkill() && playerState)
            scaleMultiplier += Mathf.Max(0, playerState.GetRepair()) / 100f;

        return effect.GetMagnitude() * Mathf.Max(0f, scaleMultiplier);
    }


    private bool ApplyHealthEffect(AidEffectDefinition effect, float magnitude)
    {
        // Stop if player state is missing.
        if (!playerState) return false;

        if (effect.ModifiesMaximumValue())
        {
            float currentMaxHealth = playerState.GetMaxHealthPoints();
            float delta = ResolveDelta(currentMaxHealth, magnitude, effect.GetOperation());

            if (Mathf.Approximately(delta, 0f))
                return false;

            playerState.SetMaxHealthPoints(currentMaxHealth + delta);
            return true;
        }

        float currentHealth = playerState.GetHealthPoints();
        float healthPercentBase = effect.GetOperation() == AidEffectOperation.AddPercent
            ? playerState.GetMaxHealthPoints()
            : currentHealth;
        float healthDelta = ResolveDelta(healthPercentBase, magnitude, effect.GetOperation());
        if (Mathf.Approximately(healthDelta, 0f))
            return false;

        if (healthDelta >= 0f)
            playerState.RestoreHealth(healthDelta);
        else
            playerState.ApplyDamage(-healthDelta);

        return true;
    }


    private bool ApplyRadiationEffect(AidEffectDefinition effect, float magnitude)
    {
        // Stop if player state is missing.
        if (!playerState) return false;

        float currentRadiation = playerState.GetRadiation();
        float radiationDelta = ResolveDelta(currentRadiation, magnitude, effect.GetOperation());
        if (Mathf.Approximately(radiationDelta, 0f))
            return false;

        if (radiationDelta >= 0f)
            playerState.AddRadiation(radiationDelta);
        else
            playerState.RemoveRadiation(-radiationDelta);

        return true;
    }


    private bool ApplyActionPointsEffect(AidEffectDefinition effect, float magnitude, bool modifiesMaximum)
    {
        // Stop if player state is missing.
        if (!playerState) return false;

        if (modifiesMaximum)
        {
            float currentMaxActionPoints = playerState.GetMaxActionPoints();
            float delta = ResolveDelta(currentMaxActionPoints, magnitude, effect.GetOperation());
            if (Mathf.Approximately(delta, 0f))
                return false;

            playerState.SetMaxActionPoints(currentMaxActionPoints + delta);
            return true;
        }

        float currentActionPoints = playerState.GetActionPoints();
        float actionPointsPercentBase = effect.GetOperation() == AidEffectOperation.AddPercent
            ? playerState.GetMaxActionPoints()
            : currentActionPoints;
        float actionPointsDelta = ResolveDelta(actionPointsPercentBase, magnitude, effect.GetOperation());
        if (Mathf.Approximately(actionPointsDelta, 0f))
            return false;

        playerState.SetActionPoints(currentActionPoints + actionPointsDelta);
        return true;
    }


    private bool ApplyIntegerPlayerStateEffect(
        System.Func<int> getter,
        System.Action<int> setter,
        AidEffectDefinition effect,
        float magnitude)
    {
        // Stop if getter/setter is missing.
        if (getter == null || setter == null) return false;

        int currentValue = getter();
        float delta = ResolveDelta(currentValue, magnitude, effect.GetOperation());
        if (Mathf.Approximately(delta, 0f))
            return false;

        setter(Mathf.RoundToInt(currentValue + delta));
        return true;
    }


    private bool ApplyDamageResistanceEffect(AidEffectDefinition effect, float magnitude)
    {
        // Stop if inventory is missing.
        if (!playerInventory) return false;

        int currentDamageResistance = playerInventory.GetTotalDamageResistance();
        float delta = ResolveDelta(currentDamageResistance, magnitude, effect.GetOperation());
        if (Mathf.Approximately(delta, 0f))
            return false;

        playerInventory.SetTotalDamageResistance(Mathf.RoundToInt(currentDamageResistance + delta));
        return true;
    }


    private bool ApplyBottleCapsEffect(AidEffectDefinition effect, float magnitude)
    {
        // Stop if inventory is missing.
        if (!playerInventory) return false;

        int currentCaps = playerInventory.GetCaps();
        float delta = ResolveDelta(currentCaps, magnitude, effect.GetOperation());
        if (Mathf.Approximately(delta, 0f))
            return false;

        playerInventory.SetCaps(Mathf.RoundToInt(currentCaps + delta));
        return true;
    }


    private bool ApplyEquippedWeaponConditionEffect(AidEffectDefinition effect, float magnitude)
    {
        // Stop if dependencies are missing.
        if (!playerInventory || !playerWeaponController) return false;

        string instanceId = playerWeaponController.GetEquippedInventoryWeaponInstanceId();
        if (string.IsNullOrWhiteSpace(instanceId))
            return false;

        if (playerInventory.TryGetWeaponConditionPercentByInstanceId(instanceId, out float currentCondition) == false)
            return false;

        float delta = ResolveDelta(currentCondition, magnitude, effect.GetOperation());
        if (Mathf.Approximately(delta, 0f))
            return false;

        return playerInventory.TrySetWeaponConditionPercentByInstanceId(
            instanceId,
            currentCondition + delta,
            true);
    }


    private static float ResolveDelta(float currentValue, float magnitude, AidEffectOperation operation)
    {
        if (operation == AidEffectOperation.AddPercent)
            return currentValue * (magnitude / 100f);

        return magnitude;
    }


    private static bool CanApplyTimedEffectNow(AidEffectTarget target)
    {
        return target == AidEffectTarget.Health
               || target == AidEffectTarget.Radiation
               || target == AidEffectTarget.ActionPoints
               || target == AidEffectTarget.MaxActionPoints
               || target == AidEffectTarget.BottleCaps
               || target == AidEffectTarget.EquippedWeaponCondition;
    }


    private static bool IsPlayerStateIntegerTarget(AidEffectTarget target)
    {
        return target == AidEffectTarget.Strength
               || target == AidEffectTarget.Perception
               || target == AidEffectTarget.Endurance
               || target == AidEffectTarget.Charisma
               || target == AidEffectTarget.Intelligence
               || target == AidEffectTarget.Agility
               || target == AidEffectTarget.Luck
               || target == AidEffectTarget.SneakSkill;
    }


    private void LogUnsupported(string message, AidEffectDefinition effect)
    {
        if (!logUnsupportedEffects) return;

        string effectId = effect != null ? effect.GetEffectId() : string.Empty;
        if (!string.IsNullOrWhiteSpace(effectId))
            message = $"{message} EffectId: {effectId}.";

        Debug.Log(message, this);
    }
}
