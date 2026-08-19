using System;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class NPC : MonoBehaviour
{
    [SerializeField] private NPCDefinition definition;
    [SerializeField] private NPCState npcState;
    [SerializeField] private bool applyDefinitionToStateOnAwake = true;

    [Header("Rendering")]
    [SerializeField] private NPCLineOfSightRenderer lineOfSightRenderer;

    private void Reset()
    {
        ResolveReferences();
        ApplyDefinitionToState();
    }

    private void Awake()
    {
        ResolveReferences();

        if (applyDefinitionToStateOnAwake)
            ApplyDefinitionToState();

        EnsureLineOfSightRenderer();
    }

    private void OnValidate()
    {
        ResolveReferences();
        ApplyDefinitionToState();
    }

    public NPCDefinition GetDefinition()
    {
        return definition;
    }

    public NPCState GetState()
    {
        return npcState;
    }

    public string GetNPCName()
    {
        if (npcState)
        {
            string stateName = npcState.GetNPCName();
            if (!string.IsNullOrWhiteSpace(stateName))
                return stateName;
        }

        return definition ? definition.GetNPCName() : string.Empty;
    }

    public void SetNPCName(string value)
    {
        ResolveReferences();

        if (npcState)
            npcState.SetNPCName(value);
    }

    public bool IsEssential()
    {
        return definition && definition.IsEssential();
    }

    public bool HasDialogue()
    {
        return definition && definition.HasDialogue();
    }

    public bool IsTrader()
    {
        return definition && definition.IsTrader();
    }

    public string GetFactionName()
    {
        return definition ? definition.GetFactionName() : string.Empty;
    }

    public static bool TryGetFactionName(Transform source, out string factionName)
    {
        factionName = string.Empty;
        if (!source)
            return false;

        NPC npc = source.GetComponentInParent<NPC>();
        if (!npc)
            npc = source.GetComponentInChildren<NPC>(true);

        if (!npc)
            return false;

        factionName = npc.GetFactionName();
        return !string.IsNullOrWhiteSpace(factionName);
    }

    public static bool HasSameFaction(Transform first, Transform second)
    {
        if (!TryGetFactionName(first, out string firstFaction) ||
            !TryGetFactionName(second, out string secondFaction))
        {
            return false;
        }

        return string.Equals(firstFaction, secondFaction, StringComparison.OrdinalIgnoreCase);
    }

    public DialogueDefinition GetDialogueDefinition()
    {
        return definition ? definition.GetDialogueDefinition() : null;
    }

    public void ApplyDefinitionToState()
    {
        if (!definition || !npcState)
            return;

        npcState.ApplyDefinition(definition);
    }

    private void ResolveReferences()
    {
        if (!npcState)
            npcState = GetComponent<NPCState>();

        if (!npcState)
            npcState = GetComponentInParent<NPCState>();

        if (!npcState)
            npcState = GetComponentInChildren<NPCState>(true);

        if (!lineOfSightRenderer)
            lineOfSightRenderer = GetComponent<NPCLineOfSightRenderer>();
    }

    private void EnsureLineOfSightRenderer()
    {
        if (!lineOfSightRenderer)
            lineOfSightRenderer = gameObject.AddComponent<NPCLineOfSightRenderer>();
    }
}
