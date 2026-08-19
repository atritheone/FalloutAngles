using System;
using UnityEngine;

[CreateAssetMenu(fileName = "NPCDefinition", menuName = "Fallout Angles/NPC Definition")]
public class NPCDefinition : ScriptableObject
{
    [Serializable]
    public class NPCVitalsDefinition
    {
        [Min(0f)] [SerializeField] private float startingHealthPoints = 100f;
        [Min(0f)] [SerializeField] private float maxHealthPoints = 100f;
        [Min(0f)] [SerializeField] private float startingActionPoints = 100f;
        [Min(0f)] [SerializeField] private float maxActionPoints = 100f;
        [Min(0f)] [SerializeField] private float actionPointsRegenPerSecond = 8f;

        public void Clamp()
        {
            maxHealthPoints = Mathf.Max(0f, maxHealthPoints);
            maxActionPoints = Mathf.Max(0f, maxActionPoints);
            startingHealthPoints = Mathf.Clamp(startingHealthPoints, 0f, maxHealthPoints);
            startingActionPoints = Mathf.Clamp(startingActionPoints, 0f, maxActionPoints);
            actionPointsRegenPerSecond = Mathf.Max(0f, actionPointsRegenPerSecond);
        }

        public float GetStartingHealthPoints()
        {
            return startingHealthPoints;
        }

        public float GetMaxHealthPoints()
        {
            return maxHealthPoints;
        }

        public float GetStartingActionPoints()
        {
            return startingActionPoints;
        }

        public float GetMaxActionPoints()
        {
            return maxActionPoints;
        }

        public float GetActionPointsRegenPerSecond()
        {
            return actionPointsRegenPerSecond;
        }
    }

    [SerializeField] private string npcId = "";
    [SerializeField] private string npcName = "";
    [SerializeField] private bool essential;
    [SerializeField] private bool hasDialogue;
    [SerializeField] private bool trader;
    [SerializeField] private string factionName = "";
    [SerializeField] private NPCVitalsDefinition vitals = new NPCVitalsDefinition();
    [SerializeField] private DialogueDefinition dialogueDefinition;

    private void OnValidate()
    {
        npcId = string.IsNullOrWhiteSpace(npcId) ? string.Empty : npcId.Trim();
        npcName = string.IsNullOrWhiteSpace(npcName) ? string.Empty : npcName.Trim();
        factionName = string.IsNullOrWhiteSpace(factionName) ? string.Empty : factionName.Trim();

        if (vitals == null)
            vitals = new NPCVitalsDefinition();

        vitals.Clamp();
    }

    public string GetNPCId()
    {
        return npcId;
    }

    public string GetNPCName()
    {
        return npcName;
    }

    public bool IsEssential()
    {
        return essential;
    }

    public bool HasDialogue()
    {
        return hasDialogue;
    }

    public bool IsTrader()
    {
        return trader;
    }

    public string GetFactionName()
    {
        return factionName;
    }

    public NPCVitalsDefinition GetVitals()
    {
        return vitals;
    }

    public DialogueDefinition GetDialogueDefinition()
    {
        return hasDialogue ? dialogueDefinition : null;
    }
}
