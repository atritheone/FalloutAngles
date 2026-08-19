using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "QuestDefinition", menuName = "Fallout Angles/Quest Definition")]
public class QuestDefinition : ScriptableObject
{
    [SerializeField] private string questId = "";
    [SerializeField] private string displayName = "Quest";
    [TextArea(2, 8)] [SerializeField] private string description = "";
    [SerializeField] private QuestType questType = QuestType.Side;
    [SerializeField] private int priority;
    [SerializeField] private bool startOnGameStart;
    [SerializeField] private bool repeatable;
    [SerializeField] private QuestCompletionMode completionMode = QuestCompletionMode.Manual;
    [SerializeField, Min(0)] private int completionExperienceReward;
    [SerializeField] private int initialStage = 10;
    [SerializeField] private List<QuestStageDefinition> stages = new List<QuestStageDefinition>
    {
        new QuestStageDefinition()
    };
    [SerializeField] private List<QuestObjectiveDefinition> objectives = new List<QuestObjectiveDefinition>
    {
        new QuestObjectiveDefinition()
    };

    private void OnValidate()
    {
        questId = string.IsNullOrWhiteSpace(questId) ? name : questId.Trim();
        displayName = string.IsNullOrWhiteSpace(displayName) ? "Quest" : displayName.Trim();
        initialStage = Mathf.Max(0, initialStage);
        completionExperienceReward = Mathf.Max(0, completionExperienceReward);

        if (stages == null)
            stages = new List<QuestStageDefinition>();

        for (int i = 0; i < stages.Count; i++)
        {
            if (stages[i] == null)
                stages[i] = new QuestStageDefinition();

            stages[i].Sanitize(i);

            for (int previousIndex = 0; previousIndex < i; previousIndex++)
            {
                QuestStageDefinition previousStage = stages[previousIndex];
                if (previousStage != null && previousStage.GetStageValue() == stages[i].GetStageValue())
                    stages[i].SetStageValue((i + 1) * 10);
            }
        }

        if (objectives == null)
            objectives = new List<QuestObjectiveDefinition>();

        for (int i = 0; i < objectives.Count; i++)
        {
            if (objectives[i] == null)
                objectives[i] = new QuestObjectiveDefinition();

            objectives[i].Sanitize(i);

            for (int previousIndex = 0; previousIndex < i; previousIndex++)
            {
                QuestObjectiveDefinition previousObjective = objectives[previousIndex];
                if (previousObjective != null && previousObjective.GetObjectiveId() == objectives[i].GetObjectiveId())
                    objectives[i].SetObjectiveId((i + 1) * 10);
            }
        }
    }

    public string GetQuestId()
    {
        return string.IsNullOrWhiteSpace(questId) ? name : questId.Trim();
    }

    public string GetDisplayName()
    {
        return string.IsNullOrWhiteSpace(displayName) ? "Quest" : displayName.Trim();
    }

    public string GetDescription()
    {
        return description;
    }

    public QuestType GetQuestType()
    {
        return questType;
    }

    public int GetPriority()
    {
        return priority;
    }

    public bool ShouldStartOnGameStart()
    {
        return startOnGameStart;
    }

    public bool IsRepeatable()
    {
        return repeatable;
    }

    public QuestCompletionMode GetCompletionMode()
    {
        return completionMode;
    }

    public int GetCompletionExperienceReward()
    {
        return Mathf.Max(0, completionExperienceReward);
    }

    public int GetInitialStage()
    {
        return initialStage;
    }

    public List<QuestStageDefinition> GetStages()
    {
        return stages;
    }

    public List<QuestObjectiveDefinition> GetObjectives()
    {
        return objectives;
    }

    public bool TryGetStage(int stageValue, out QuestStageDefinition stage)
    {
        stage = null;

        if (stages == null)
            return false;

        for (int i = 0; i < stages.Count; i++)
        {
            QuestStageDefinition candidate = stages[i];
            if (candidate == null || candidate.GetStageValue() != stageValue)
                continue;

            stage = candidate;
            return true;
        }

        return false;
    }

    public QuestStageDefinition GetInitialStageDefinition()
    {
        if (TryGetStage(initialStage, out QuestStageDefinition stage))
            return stage;

        return stages != null && stages.Count > 0 ? stages[0] : null;
    }

    public bool TryGetObjective(int objectiveId, out QuestObjectiveDefinition objective)
    {
        objective = null;

        if (objectives == null)
            return false;

        for (int i = 0; i < objectives.Count; i++)
        {
            QuestObjectiveDefinition candidate = objectives[i];
            if (candidate == null || candidate.GetObjectiveId() != objectiveId)
                continue;

            objective = candidate;
            return true;
        }

        return false;
    }

    public int GetHighestStageValue()
    {
        int highestStage = initialStage;

        if (stages == null)
            return highestStage;

        for (int i = 0; i < stages.Count; i++)
        {
            QuestStageDefinition stage = stages[i];
            if (stage != null)
                highestStage = Mathf.Max(highestStage, stage.GetStageValue());
        }

        return highestStage;
    }
}

[Serializable]
public class QuestStageDefinition
{
    [SerializeField] private int stageValue = 10;
    [TextArea(2, 8)] [SerializeField] private string journalEntry = "";
    [SerializeField] private bool setCurrentObjective;
    [SerializeField] private int currentObjectiveId = -1;
    [SerializeField] private List<int> objectivesToDisplay = new List<int>();
    [SerializeField] private List<int> objectivesToComplete = new List<int>();
    [SerializeField] private List<int> objectivesToFail = new List<int>();
    [SerializeField] private bool completesQuest;
    [SerializeField] private bool failsQuest;

    public void Sanitize(int stageIndex)
    {
        stageValue = Mathf.Max(0, stageValue);
        if (stageIndex == 0 && stageValue == 0)
            stageValue = 10;

        if (objectivesToDisplay == null)
            objectivesToDisplay = new List<int>();

        if (objectivesToComplete == null)
            objectivesToComplete = new List<int>();

        if (objectivesToFail == null)
            objectivesToFail = new List<int>();
    }

    public int GetStageValue()
    {
        return stageValue;
    }

    public void SetStageValue(int value)
    {
        stageValue = Mathf.Max(0, value);
    }

    public string GetJournalEntry()
    {
        return journalEntry;
    }

    public bool ShouldSetCurrentObjective()
    {
        return setCurrentObjective;
    }

    public int GetCurrentObjectiveId()
    {
        return currentObjectiveId;
    }

    public List<int> GetObjectivesToDisplay()
    {
        return objectivesToDisplay;
    }

    public List<int> GetObjectivesToComplete()
    {
        return objectivesToComplete;
    }

    public List<int> GetObjectivesToFail()
    {
        return objectivesToFail;
    }

    public bool CompletesQuest()
    {
        return completesQuest;
    }

    public bool FailsQuest()
    {
        return failsQuest;
    }

}

[Serializable]
public class QuestObjectiveDefinition
{
    [SerializeField] private int objectiveId = 10;
    [TextArea(1, 4)] [SerializeField] private string displayText = "Objective";
    [SerializeField] private QuestObjectiveType objectiveType = QuestObjectiveType.Manual;
    [SerializeField] private bool visibleOnQuestStart;
    [SerializeField] private bool requiredToCompleteQuest = true;
    [SerializeField] private bool completesQuestWhenFinished;
    [SerializeField] private string targetSceneObjectName = "";
    [SerializeField] private List<string> requiredNpcSceneObjectNames = new List<string>();
    [SerializeField] private bool hasWorldPosition;
    [SerializeField] private Vector3 worldPosition;

    public void Sanitize(int objectiveIndex)
    {
        objectiveId = objectiveId < 0 ? (objectiveIndex + 1) * 10 : objectiveId;
        displayText = string.IsNullOrWhiteSpace(displayText) ? "Objective" : displayText.Trim();
        targetSceneObjectName = string.IsNullOrWhiteSpace(targetSceneObjectName) ? string.Empty : targetSceneObjectName.Trim();

        if (requiredNpcSceneObjectNames == null)
            requiredNpcSceneObjectNames = new List<string>();

        for (int i = 0; i < requiredNpcSceneObjectNames.Count; i++)
            requiredNpcSceneObjectNames[i] = string.IsNullOrWhiteSpace(requiredNpcSceneObjectNames[i])
                ? string.Empty
                : requiredNpcSceneObjectNames[i].Trim();
    }

    public int GetObjectiveId()
    {
        return objectiveId;
    }

    public void SetObjectiveId(int value)
    {
        objectiveId = Mathf.Max(0, value);
    }

    public string GetDisplayText()
    {
        return string.IsNullOrWhiteSpace(displayText) ? "Objective" : displayText.Trim();
    }

    public bool IsVisibleOnQuestStart()
    {
        return visibleOnQuestStart;
    }

    public QuestObjectiveType GetObjectiveType()
    {
        return objectiveType;
    }

    public bool IsRequiredToCompleteQuest()
    {
        return requiredToCompleteQuest;
    }

    public bool CompletesQuestWhenFinished()
    {
        return completesQuestWhenFinished;
    }

    public string GetTargetSceneObjectName()
    {
        return targetSceneObjectName;
    }

    public List<string> GetRequiredNpcSceneObjectNames()
    {
        return requiredNpcSceneObjectNames;
    }

    public bool HasWorldPosition()
    {
        return hasWorldPosition;
    }

    public Vector3 GetWorldPosition()
    {
        return worldPosition;
    }
}

public enum QuestType
{
    Main,
    Side,
    Miscellaneous
}

public enum QuestCompletionMode
{
    Manual,
    CompleteWhenFinalStageSet,
    CompleteWhenAllRequiredObjectivesComplete
}

public enum QuestObjectiveType
{
    Manual,
    KillSpecificNpcs
}
