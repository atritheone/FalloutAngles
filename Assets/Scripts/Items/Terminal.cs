// imports
using System;
using UnityEngine;
using UnityEngine.Events;



// class
public class Terminal : MonoBehaviour, IPlayerInteractTarget
{
    // variables
    public enum LockType
    {
        VeryEasy = 5,
        Easy = 0,
        Average = 1,
        Hard = 2,
        VeryHard = 3,
        Password = 4
    }

    [Serializable]
    private class LockSettingsGroup
    {
        // If true, the terminal starts locked.
        public bool startsLocked;

        // Which lock type this terminal uses when locked.
        public LockType lockType = LockType.Easy;

        // Optional password item definition used when lock type is Password.
        public ScriptableObject passwordItemDefinition;

        // If true, one password item is consumed when unlocking.
        public bool consumePasswordOnUnlock;
    }

    [Header("Terminal")]
    [SerializeField] private TerminalDefinition terminalDefinition;
    [SerializeField] private TerminalController terminalController;
    [SerializeField] private bool openTerminalUiOnInteract = true;

    [Header("Lock Settings")]
    [SerializeField] private LockSettingsGroup lockSettings = new LockSettingsGroup();

    [Header("Skill Experience")]
    [SerializeField] private bool awardScienceSkillExperience = true;
    [SerializeField, Min(0f)] private float veryEasyScienceSkillExperience = 15f;
    [SerializeField, Min(0f)] private float easyScienceSkillExperience = 25f;
    [SerializeField, Min(0f)] private float averageScienceSkillExperience = 50f;
    [SerializeField, Min(0f)] private float hardScienceSkillExperience = 75f;
    [SerializeField, Min(0f)] private float veryHardScienceSkillExperience = 100f;

    [Header("Events")]
    [SerializeField] private UnityEvent<GameObject> onUsed;
    [SerializeField] private UnityEvent<GameObject> onLocked;
    [SerializeField] private UnityEvent<GameObject> onUnlockedWithPassword;
    [SerializeField] private UnityEvent<GameObject> onUnlockedWithHacking;

    // Runtime lock state.
    [SerializeField, HideInInspector] private bool isLocked;



    // methods
    private void OnValidate()
    {
        EnsureLockSettings();
        veryEasyScienceSkillExperience = Mathf.Max(0f, veryEasyScienceSkillExperience);
        easyScienceSkillExperience = Mathf.Max(0f, easyScienceSkillExperience);
        averageScienceSkillExperience = Mathf.Max(0f, averageScienceSkillExperience);
        hardScienceSkillExperience = Mathf.Max(0f, hardScienceSkillExperience);
        veryHardScienceSkillExperience = Mathf.Max(0f, veryHardScienceSkillExperience);
    }


    private void Awake()
    {
        EnsureLockSettings();
        isLocked = lockSettings.startsLocked;
    }


    public string GetInteractionText(GameObject interactor)
    {
        EnsureLockSettings();

        string basePrompt = "Activate " + GetTerminalName();

        if (!IsCurrentlyLocked())
            return basePrompt;

        if (lockSettings.lockType == LockType.Password && CanInteractorUsePasswordLock(interactor))
            return basePrompt + "\n[Locked - Use Password]";

        if (lockSettings.lockType == LockType.Password)
            return basePrompt + "\n[Locked - Requires Password]";

        string lockLine = "[Locked - " + GetLockTypeLabel(lockSettings.lockType) + "]";
        int requiredScience = GetRequiredScienceSkill();
        int interactorScience = GetInteractorScience(interactor);
        if (IsHackingScienceRequirementEnforced() && interactorScience < requiredScience)
            return basePrompt + "\n" + lockLine + "\n[Requires Science " + requiredScience + "]";

        return basePrompt + "\n" + lockLine;
    }


    public void Interact(GameObject interactor)
    {
        EnsureLockSettings();

        if (IsCurrentlyLocked())
        {
            // Password locks can auto-unlock when the interactor has the referenced password item.
            if (lockSettings.lockType == LockType.Password && TryUnlockWithPassword(interactor))
            {
                Use(interactor);
                return;
            }

            if (lockSettings.lockType != LockType.Password)
            {
                TerminalController controller = ResolveTerminalUI();
                if (controller && controller.OpenHackingForTerminal(this, interactor))
                    return;
            }

            onLocked?.Invoke(interactor);
            return;
        }

        Use(interactor);
    }


    public void AddOnLockedListener(UnityAction<GameObject> listener)
    {
        if (listener == null)
            return;

        if (onLocked == null)
            onLocked = new UnityEvent<GameObject>();

        onLocked.AddListener(listener);
    }


    public void RemoveOnLockedListener(UnityAction<GameObject> listener)
    {
        if (listener == null || onLocked == null)
            return;

        onLocked.RemoveListener(listener);
    }


    public bool IsLocked()
    {
        return IsCurrentlyLocked();
    }


    public LockType GetLockType()
    {
        EnsureLockSettings();
        return lockSettings.lockType;
    }


    public ScriptableObject GetRequiredPasswordDefinition()
    {
        EnsureLockSettings();
        return lockSettings.passwordItemDefinition;
    }


    public int GetRequiredScienceSkill()
    {
        EnsureLockSettings();
        return GetRequiredScienceSkill(lockSettings.lockType);
    }


    public Vector2Int GetPasswordLengthRange()
    {
        EnsureLockSettings();
        return GetPasswordLengthRange(lockSettings.lockType);
    }


    public void Lock()
    {
        isLocked = true;
    }


    public void Unlock()
    {
        isLocked = false;
    }


    public void UnlockWithHacking(GameObject interactor)
    {
        bool wasLocked = isLocked;
        isLocked = false;
        if (wasLocked)
            AwardScienceSkillExperience(interactor);

        onUnlockedWithHacking?.Invoke(interactor);
    }


    private void Use(GameObject interactor)
    {
        if (openTerminalUiOnInteract && !TerminalController.IsInteractCloseCooldownActive())
            OpenTerminalUi(interactor);

        onUsed?.Invoke(interactor);
    }


    public string GetTerminalName()
    {
        return terminalDefinition ? terminalDefinition.GetTerminalName() : "Terminal";
    }


    public TerminalDefinition GetTerminalDefinition()
    {
        return terminalDefinition;
    }


    public void SetTerminalName(string newTerminalName)
    {
        if (terminalDefinition)
            terminalDefinition.SetTerminalName(newTerminalName);
    }


    public void SetTerminalDefinition(TerminalDefinition newTerminalDefinition)
    {
        terminalDefinition = newTerminalDefinition;
    }


    private void OpenTerminalUi(GameObject interactor)
    {
        TerminalController controller = ResolveTerminalUI();

        if (!controller)
            return;

        controller.OpenForTerminal(this, interactor);
    }


    private TerminalController ResolveTerminalUI()
    {
        TerminalController controller = terminalController;

        if (!controller)
        {
            controller = TerminalController.FindFirstInSceneIncludingInactive();
            terminalController = controller;
        }

        return controller;
    }


    private bool IsHackingScienceRequirementEnforced()
    {
        TerminalController controller = ResolveTerminalUI();
        return !controller || controller.ShouldEnforceHackingScienceRequirement();
    }


    private bool TryUnlockWithPassword(GameObject interactor)
    {
        EnsureLockSettings();

        ScriptableObject passwordDefinition = lockSettings.passwordItemDefinition;
        if (!passwordDefinition || !CanInteractorUsePasswordLock(interactor))
            return false;

        PlayerInventory inventory = interactor.GetComponentInParent<PlayerInventory>(true);
        if (!inventory)
            return false;

        if (inventory.GetTotalCount(passwordDefinition) <= 0)
            return false;

        if (lockSettings.consumePasswordOnUnlock && !inventory.RemoveItem(passwordDefinition, 1))
            return false;

        isLocked = false;
        onUnlockedWithPassword?.Invoke(interactor);
        return true;
    }


    private bool CanInteractorUsePasswordLock(GameObject interactor)
    {
        EnsureLockSettings();

        ScriptableObject passwordDefinition = lockSettings.passwordItemDefinition;
        if (!passwordDefinition || !interactor)
            return false;

        PlayerInventory inventory = interactor.GetComponentInParent<PlayerInventory>(true);
        if (!inventory)
            return false;

        return inventory.GetTotalCount(passwordDefinition) > 0;
    }


    private bool IsCurrentlyLocked()
    {
        return isLocked;
    }


    private void EnsureLockSettings()
    {
        if (lockSettings == null)
            lockSettings = new LockSettingsGroup();
    }


    private static int GetInteractorScience(GameObject interactor)
    {
        if (!interactor)
            return 0;

        PlayerState playerState = interactor.GetComponentInParent<PlayerState>(true);
        return playerState ? Mathf.Clamp(playerState.GetScience(), 0, 100) : 0;
    }


    private void AwardScienceSkillExperience(GameObject interactor)
    {
        if (!awardScienceSkillExperience || !interactor)
            return;

        PlayerState playerState = interactor.GetComponentInParent<PlayerState>(true);
        if (!playerState)
            return;

        float experienceAmount = GetScienceSkillExperience(lockSettings.lockType);
        if (experienceAmount <= 0f)
            return;

        playerState.AddSkillExperience(PlayerSkill.Science, experienceAmount);
    }


    private float GetScienceSkillExperience(LockType type)
    {
        if (type == LockType.VeryEasy) return veryEasyScienceSkillExperience;
        if (type == LockType.Easy) return easyScienceSkillExperience;
        if (type == LockType.Average) return averageScienceSkillExperience;
        if (type == LockType.Hard) return hardScienceSkillExperience;
        if (type == LockType.VeryHard) return veryHardScienceSkillExperience;
        return 0f;
    }


    private static int GetRequiredScienceSkill(LockType type)
    {
        if (type == LockType.VeryEasy) return 15;
        if (type == LockType.Easy) return 25;
        if (type == LockType.Average) return 50;
        if (type == LockType.Hard) return 75;
        if (type == LockType.VeryHard) return 100;
        return 0;
    }


    private static Vector2Int GetPasswordLengthRange(LockType type)
    {
        if (type == LockType.VeryEasy) return new Vector2Int(4, 5);
        if (type == LockType.Easy) return new Vector2Int(6, 8);
        if (type == LockType.Average) return new Vector2Int(9, 10);
        if (type == LockType.Hard) return new Vector2Int(11, 12);
        if (type == LockType.VeryHard) return new Vector2Int(13, 15);
        return new Vector2Int(4, 5);
    }


    public static string GetLockTypeLabel(LockType type)
    {
        if (type == LockType.VeryEasy) return "Very Easy";
        if (type == LockType.Easy) return "Easy";
        if (type == LockType.Average) return "Average";
        if (type == LockType.Hard) return "Hard";
        if (type == LockType.VeryHard) return "Very Hard";
        return "Locked";
    }
}
