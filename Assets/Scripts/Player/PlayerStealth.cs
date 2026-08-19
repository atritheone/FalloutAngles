using UnityEngine;

public class PlayerStealth : MonoBehaviour
{
    private const float NpcRefreshInterval = 0.5f;
    private const float MinSneakExperienceIntervalSeconds = 0.1f;

    public enum StealthState
    {
        Hidden,
        Caution,
        Danger
    }

    [SerializeField] private PlayerMovement playerMovement;
    [SerializeField] private PlayerState playerState;

    [Header("Skill Experience")]
    [SerializeField] private bool awardSneakSkillExperience = true;
    [SerializeField, Min(0f)] private float sneakExperienceRadius = 14f;
    [SerializeField, Min(MinSneakExperienceIntervalSeconds)] private float sneakExperienceIntervalSeconds = 1f;
    [SerializeField, Min(0f)] private float sneakExperiencePerInterval = 1.5f;
    [SerializeField, Min(0f)] private float cautionExperienceMultiplier = 1.5f;

    private NPCCombat[] npcCombats = new NPCCombat[0];
    private float nextNpcRefreshTime;
    private float nextSneakExperienceTime;
    private int lastRefreshFrame = -1;

    public bool IsStealthActive => playerMovement != null && playerMovement.IsAnyCrouching;
    public StealthState CurrentState { get; private set; } = StealthState.Hidden;

    private void Awake()
    {
        ResolveReferences();
        RefreshNpcCache();
        RefreshStealthState();
    }

    private void Update()
    {
        ResolveReferences();
        RefreshStealthState();
        UpdateSneakSkillExperience();
    }

    public StealthState RefreshStealthState()
    {
        if (lastRefreshFrame == Time.frameCount)
            return CurrentState;

        lastRefreshFrame = Time.frameCount;

        bool isCrouching = playerMovement != null && playerMovement.IsAnyCrouching;
        StealthState resolvedState = ResolveStealthState();
        if (!isCrouching && resolvedState == StealthState.Hidden)
        {
            CurrentState = StealthState.Hidden;
            return CurrentState;
        }

        CurrentState = resolvedState;
        return CurrentState;
    }

    private void ResolveReferences()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerMovement == null)
            playerMovement = GetComponentInParent<PlayerMovement>();

        if (playerMovement == null)
            playerMovement = FindAnyObjectByType<PlayerMovement>();

        if (playerState == null)
            playerState = GetComponent<PlayerState>();

        if (playerState == null)
            playerState = GetComponentInParent<PlayerState>();
    }

    private void RefreshNpcCache()
    {
        npcCombats = FindObjectsByType<NPCCombat>(FindObjectsInactive.Exclude);
        nextNpcRefreshTime = Time.time + NpcRefreshInterval;
    }

    private StealthState ResolveStealthState()
    {
        if (Time.time >= nextNpcRefreshTime || npcCombats == null)
            RefreshNpcCache();

        StealthState resolved = StealthState.Hidden;
        for (int i = 0; i < npcCombats.Length; i++)
        {
            NPCCombat npcCombat = npcCombats[i];
            if (npcCombat == null || !npcCombat.isActiveAndEnabled)
                continue;

            NPCCombat.PlayerStealthThreatLevel threatLevel = npcCombat.GetPlayerStealthThreatLevel();
            if (threatLevel == NPCCombat.PlayerStealthThreatLevel.Danger)
                return StealthState.Danger;

            if (threatLevel == NPCCombat.PlayerStealthThreatLevel.Caution)
                resolved = StealthState.Caution;
        }

        return resolved;
    }

    private void UpdateSneakSkillExperience()
    {
        if (!awardSneakSkillExperience || playerState == null)
            return;

        if (!IsStealthActive || CurrentState == StealthState.Danger)
            return;

        float interval = Mathf.Max(MinSneakExperienceIntervalSeconds, sneakExperienceIntervalSeconds);
        if (Time.time < nextSneakExperienceTime)
            return;

        if (!HasNearbyUndiscoveredNpc(out StealthState highestNearbyState))
            return;

        float multiplier = highestNearbyState == StealthState.Caution
            ? Mathf.Max(0f, cautionExperienceMultiplier)
            : 1f;
        float experienceAmount = Mathf.Max(0f, sneakExperiencePerInterval) * multiplier;
        if (experienceAmount > 0f)
            playerState.AddSkillExperience(PlayerSkill.Sneak, experienceAmount);

        nextSneakExperienceTime = Time.time + interval;
    }

    private bool HasNearbyUndiscoveredNpc(out StealthState highestNearbyState)
    {
        highestNearbyState = StealthState.Hidden;

        if (Time.time >= nextNpcRefreshTime || npcCombats == null)
            RefreshNpcCache();

        float radius = Mathf.Max(0f, sneakExperienceRadius);
        if (radius <= 0f)
            return false;

        float radiusSqr = radius * radius;
        Vector3 playerPosition = transform.position;
        bool foundNearbyNpc = false;

        for (int i = 0; i < npcCombats.Length; i++)
        {
            NPCCombat npcCombat = npcCombats[i];
            if (npcCombat == null || !npcCombat.isActiveAndEnabled)
                continue;

            NPCState npcState = npcCombat.GetComponent<NPCState>();
            if (npcState != null && npcState.IsDead())
                continue;

            Vector3 toNpc = npcCombat.transform.position - playerPosition;
            toNpc.y = 0f;
            if (toNpc.sqrMagnitude > radiusSqr)
                continue;

            NPCCombat.PlayerStealthThreatLevel threatLevel = npcCombat.GetPlayerStealthThreatLevel();
            if (threatLevel == NPCCombat.PlayerStealthThreatLevel.Danger)
                continue;

            foundNearbyNpc = true;
            if (threatLevel == NPCCombat.PlayerStealthThreatLevel.Caution)
                highestNearbyState = StealthState.Caution;
        }

        return foundNearbyNpc;
    }
}
