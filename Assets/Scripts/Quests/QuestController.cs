using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-50)]
public class QuestController : MonoBehaviour
{
    private const int NpcDeathObjectiveBindingRetryAttempts = 12;
    private const float NpcDeathObjectiveBindingRetrySeconds = 0.25f;

    public static QuestController Instance { get; private set; }

    [Header("Quest Library")]
    [SerializeField] private List<QuestDefinition> questDefinitions = new List<QuestDefinition>();
    [SerializeField] private bool startGameStartQuestsOnAwake = true;

    [Header("Runtime State")]
    [SerializeField] private QuestDefinition currentQuestDefinition;
    [SerializeField] private List<QuestRuntimeState> runtimeStates = new List<QuestRuntimeState>();

    [Header("Rewards")]
    [SerializeField] private PlayerState playerState;

    [Header("Unity Events")]
    [SerializeField] private QuestRuntimeStateEvent onQuestStarted = new QuestRuntimeStateEvent();
    [SerializeField] private QuestRuntimeStateEvent onQuestUpdated = new QuestRuntimeStateEvent();
    [SerializeField] private QuestRuntimeStateEvent onQuestCompleted = new QuestRuntimeStateEvent();
    [SerializeField] private QuestRuntimeStateEvent onQuestFailed = new QuestRuntimeStateEvent();
    [SerializeField] private QuestRuntimeStateEvent onCurrentQuestChanged = new QuestRuntimeStateEvent();
    [SerializeField] private QuestObjectiveRuntimeStateEvent onCurrentObjectiveChanged = new QuestObjectiveRuntimeStateEvent();

    private readonly Dictionary<string, QuestDefinition> definitionsById = new Dictionary<string, QuestDefinition>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<QuestDefinition, QuestRuntimeState> statesByDefinition = new Dictionary<QuestDefinition, QuestRuntimeState>();
    private readonly List<NpcDeathObjectiveSubscription> npcDeathObjectiveSubscriptions = new List<NpcDeathObjectiveSubscription>();
    private readonly List<NpcDeathObjectiveBindingRetry> npcDeathObjectiveBindingRetries = new List<NpcDeathObjectiveBindingRetry>();
    private Coroutine npcDeathObjectiveBindingRetryRoutine;
    private bool hasStartedGameStartQuests;

    public event Action<QuestRuntimeState> QuestStarted;
    public event Action<QuestRuntimeState> QuestUpdated;
    public event Action<QuestRuntimeState> QuestCompleted;
    public event Action<QuestRuntimeState> QuestFailed;
    public event Action<QuestRuntimeState> CurrentQuestChanged;
    public event Action<QuestRuntimeState, int> QuestStageChanged;
    public event Action<QuestRuntimeState, QuestObjectiveRuntimeState> QuestObjectiveChanged;
    public event Action<QuestRuntimeState, QuestObjectiveRuntimeState> CurrentObjectiveChanged;

    private sealed class NpcDeathObjectiveSubscription
    {
        public readonly QuestDefinition Definition;
        public readonly int ObjectiveId;
        public readonly NPCState TargetState;

        public NpcDeathObjectiveSubscription(QuestDefinition definition, int objectiveId, NPCState targetState)
        {
            Definition = definition;
            ObjectiveId = objectiveId;
            TargetState = targetState;
        }
    }

    private sealed class NpcDeathObjectiveBindingRetry
    {
        public readonly QuestDefinition Definition;
        public readonly int ObjectiveId;
        public int RemainingAttempts;

        public NpcDeathObjectiveBindingRetry(QuestDefinition definition, int objectiveId)
        {
            Definition = definition;
            ObjectiveId = objectiveId;
            RemainingAttempts = NpcDeathObjectiveBindingRetryAttempts;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("Multiple QuestController instances exist. The first active instance will remain the global instance.", this);
        }
        else
        {
            Instance = this;
        }

        RebuildLookupTables();
    }

    private void Start()
    {
        if (startGameStartQuestsOnAwake)
            StartGameStartQuests();
    }

    private void OnDestroy()
    {
        ClearAllObjectiveRuntimeHandlers();
        npcDeathObjectiveBindingRetries.Clear();

        if (Instance == this)
            Instance = null;
    }

    private void OnValidate()
    {
        if (questDefinitions == null)
            questDefinitions = new List<QuestDefinition>();

        if (runtimeStates == null)
            runtimeStates = new List<QuestRuntimeState>();
    }

    public static QuestController FindOrCreate()
    {
        if (Instance)
            return Instance;

        QuestController existing = FindAnyObjectByType<QuestController>(FindObjectsInactive.Include);
        if (existing)
        {
            Instance = existing;
            existing.RebuildLookupTables();
            return existing;
        }

        GameObject controllerObject = new GameObject("QuestController");
        QuestController controller = controllerObject.AddComponent<QuestController>();
        Instance = controller;
        controller.RebuildLookupTables();
        return controller;
    }

    public void RegisterQuestDefinition(QuestDefinition definition)
    {
        if (!definition)
            return;

        if (!questDefinitions.Contains(definition))
            questDefinitions.Add(definition);

        string questId = definition.GetQuestId();
        if (!string.IsNullOrWhiteSpace(questId))
            definitionsById[questId] = definition;

        EnsureState(definition);
    }

    public bool TryGetQuestDefinition(string questId, out QuestDefinition definition)
    {
        RebuildLookupTablesIfNeeded();
        return definitionsById.TryGetValue(NormalizeQuestId(questId), out definition);
    }

    public bool TryFindQuestDefinition(string questName, out QuestDefinition definition)
    {
        definition = null;

        RebuildLookupTablesIfNeeded();

        string normalizedName = NormalizeQuestSearchName(questName);
        if (string.IsNullOrEmpty(normalizedName))
            return false;

        if (definitionsById.TryGetValue(NormalizeQuestId(questName), out definition) && definition)
            return true;

        for (int i = 0; i < questDefinitions.Count; i++)
        {
            QuestDefinition candidate = questDefinitions[i];
            if (!candidate)
                continue;

            if (NormalizeQuestSearchName(candidate.GetQuestId()) == normalizedName ||
                NormalizeQuestSearchName(candidate.GetDisplayName()) == normalizedName ||
                NormalizeQuestSearchName(candidate.name) == normalizedName)
            {
                definition = candidate;
                return true;
            }
        }

        return false;
    }

    public bool TryGetQuestState(string questId, out QuestRuntimeState state)
    {
        state = null;
        return TryGetQuestDefinition(questId, out QuestDefinition definition)
               && TryGetQuestState(definition, out state);
    }

    public bool TryGetQuestState(QuestDefinition definition, out QuestRuntimeState state)
    {
        state = null;

        if (!definition)
            return false;

        RebuildLookupTablesIfNeeded();
        return statesByDefinition.TryGetValue(definition, out state);
    }

    public QuestRuntimeState GetQuestState(QuestDefinition definition)
    {
        return definition ? EnsureState(definition) : null;
    }

    public QuestRuntimeState GetCurrentQuest()
    {
        return currentQuestDefinition ? EnsureState(currentQuestDefinition) : null;
    }

    public IReadOnlyList<QuestRuntimeState> GetQuestStates()
    {
        RebuildLookupTablesIfNeeded();
        return runtimeStates;
    }

    public List<QuestRuntimeState> GetQuestsByStatus(QuestStatus status)
    {
        RebuildLookupTablesIfNeeded();

        List<QuestRuntimeState> quests = new List<QuestRuntimeState>();
        for (int i = 0; i < runtimeStates.Count; i++)
        {
            QuestRuntimeState state = runtimeStates[i];
            if (state != null && state.GetStatus() == status)
                quests.Add(state);
        }

        return quests;
    }

    public bool StartQuest(string questId)
    {
        return TryGetQuestDefinition(questId, out QuestDefinition definition) && StartQuest(definition);
    }

    public bool StartQuest(QuestDefinition definition)
    {
        if (!definition)
            return false;

        RegisterQuestDefinition(definition);
        QuestRuntimeState state = EnsureState(definition);

        if (state.GetStatus() == QuestStatus.Active)
        {
            SetCurrentQuest(definition);
            return true;
        }

        if ((state.GetStatus() == QuestStatus.Completed || state.GetStatus() == QuestStatus.Failed) && !definition.IsRepeatable())
            return false;

        state.ResetForStart();
        state.SetStatus(QuestStatus.Active);
        state.SetCurrentStage(definition.GetInitialStage());
        state.MarkStageCompleted(definition.GetInitialStage());
        ClearObjectiveRuntimeHandlers(state);
        InitializeObjectives(state);
        SetupObjectiveRuntimeHandlers(state);

        NotifyQuestStarted(state);

        QuestStageDefinition initialStage = definition.GetInitialStageDefinition();
        if (initialStage != null)
            ApplyStageDefinition(state, initialStage);

        if (state.GetStatus() == QuestStatus.Active)
            SetCurrentQuest(definition);

        EvaluateQuestCompletionRules(state);
        return true;
    }

    public bool SetQuestStage(string questId, int stageValue, bool startQuestIfNeeded = true)
    {
        return TryGetQuestDefinition(questId, out QuestDefinition definition)
               && SetQuestStage(definition, stageValue, startQuestIfNeeded);
    }

    public bool SetQuestStage(QuestDefinition definition, int stageValue, bool startQuestIfNeeded = true)
    {
        if (!definition)
            return false;

        RegisterQuestDefinition(definition);
        QuestRuntimeState state = EnsureState(definition);

        if (state.GetStatus() == QuestStatus.Inactive)
        {
            if (!startQuestIfNeeded || !StartQuest(definition))
                return false;

            state = EnsureState(definition);
        }

        if (state.GetStatus() != QuestStatus.Active)
            return false;

        int clampedStage = Mathf.Max(0, stageValue);
        state.SetCurrentStage(clampedStage);
        state.MarkStageCompleted(clampedStage);

        if (definition.TryGetStage(clampedStage, out QuestStageDefinition stage))
            ApplyStageDefinition(state, stage);
        else
            NotifyQuestUpdated(state);

        QuestStageChanged?.Invoke(state, clampedStage);

        if (definition.GetCompletionMode() == QuestCompletionMode.CompleteWhenFinalStageSet &&
            clampedStage >= definition.GetHighestStageValue())
        {
            CompleteQuest(definition);
        }
        else
        {
            EvaluateQuestCompletionRules(state);
        }

        return true;
    }

    public bool CompleteQuest(string questId)
    {
        return TryGetQuestDefinition(questId, out QuestDefinition definition) && CompleteQuest(definition);
    }

    public bool CompleteQuest(QuestDefinition definition)
    {
        QuestRuntimeState state = GetActiveState(definition);
        if (state == null)
            return false;

        state.SetStatus(QuestStatus.Completed);
        CompleteVisibleObjectives(state, true);
        ClearObjectiveRuntimeHandlers(state);
        ClearCurrentQuestIfNeeded(state);
        AwardCompletionExperience(definition);

        NotifyQuestCompleted(state);
        return true;
    }

    public bool CompleteAllObjectives(string questId)
    {
        return TryFindQuestDefinition(questId, out QuestDefinition definition) && CompleteAllObjectives(definition);
    }

    public bool CompleteAllObjectives(QuestDefinition definition)
    {
        QuestRuntimeState state = GetActiveState(definition);
        if (state == null)
            return false;

        List<QuestObjectiveDefinition> objectiveDefinitions = definition.GetObjectives();
        if (objectiveDefinitions == null || objectiveDefinitions.Count == 0)
            return false;

        bool shouldCompleteQuest = false;

        for (int i = 0; i < objectiveDefinitions.Count; i++)
        {
            QuestObjectiveDefinition objectiveDefinition = objectiveDefinitions[i];
            if (objectiveDefinition == null)
                continue;

            QuestObjectiveRuntimeState runtimeObjective = state.GetObjectiveState(objectiveDefinition.GetObjectiveId());
            if (runtimeObjective == null)
            {
                runtimeObjective = new QuestObjectiveRuntimeState(objectiveDefinition.GetObjectiveId(), QuestObjectiveState.Hidden);
                state.AddObjective(runtimeObjective);
            }

            if (runtimeObjective.GetState() != QuestObjectiveState.Completed)
            {
                runtimeObjective.SetState(QuestObjectiveState.Completed);
                QuestObjectiveChanged?.Invoke(state, runtimeObjective);
            }

            ClearObjectiveRuntimeHandlers(state, objectiveDefinition.GetObjectiveId());

            if (objectiveDefinition.CompletesQuestWhenFinished())
                shouldCompleteQuest = true;
        }

        EnsureCurrentObjective(state);

        if (shouldCompleteQuest)
        {
            CompleteQuest(definition);
            return true;
        }

        EvaluateQuestCompletionRules(state);
        if (state.GetStatus() == QuestStatus.Active)
            NotifyQuestUpdated(state);

        return true;
    }

    public bool FailQuest(string questId)
    {
        return TryGetQuestDefinition(questId, out QuestDefinition definition) && FailQuest(definition);
    }

    public bool FailQuest(QuestDefinition definition)
    {
        QuestRuntimeState state = GetActiveState(definition);
        if (state == null)
            return false;

        state.SetStatus(QuestStatus.Failed);
        ClearObjectiveRuntimeHandlers(state);
        ClearCurrentQuestIfNeeded(state);

        NotifyQuestFailed(state);
        return true;
    }

    public bool SetCurrentQuest(string questId)
    {
        return TryGetQuestDefinition(questId, out QuestDefinition definition) && SetCurrentQuest(definition);
    }

    public bool SetCurrentQuest(QuestDefinition definition)
    {
        QuestRuntimeState state = GetActiveState(definition);
        if (state == null)
            return false;

        for (int i = 0; i < runtimeStates.Count; i++)
        {
            QuestRuntimeState candidate = runtimeStates[i];
            if (candidate != null)
                candidate.SetCurrentQuest(candidate == state);
        }

        currentQuestDefinition = definition;
        EnsureCurrentObjective(state);

        CurrentQuestChanged?.Invoke(state);
        onCurrentQuestChanged.Invoke(state);
        return true;
    }

    public bool ClearCurrentQuest()
    {
        if (!currentQuestDefinition)
            return false;

        QuestRuntimeState previousState = GetCurrentQuest();
        currentQuestDefinition = null;

        if (previousState != null)
        {
            previousState.SetCurrentQuest(false);
            CurrentQuestChanged?.Invoke(previousState);
            onCurrentQuestChanged.Invoke(previousState);
        }

        return true;
    }

    public bool SetCurrentObjective(string questId, int objectiveId)
    {
        return TryGetQuestDefinition(questId, out QuestDefinition definition)
               && SetCurrentObjective(definition, objectiveId);
    }

    public bool SetCurrentObjective(QuestDefinition definition, int objectiveId)
    {
        QuestRuntimeState state = GetActiveState(definition);
        if (state == null)
            return false;

        QuestObjectiveRuntimeState objectiveState = state.GetObjectiveState(objectiveId);
        if (objectiveState == null || !objectiveState.CanBeTracked())
            return false;

        state.SetCurrentObjectiveId(objectiveId);
        NotifyCurrentObjectiveChanged(state, objectiveState);
        return true;
    }

    public bool DisplayObjective(string questId, int objectiveId)
    {
        return TryGetQuestDefinition(questId, out QuestDefinition definition)
               && SetObjectiveState(definition, objectiveId, QuestObjectiveState.Displayed);
    }

    public bool CompleteObjective(string questId, int objectiveId)
    {
        return TryGetQuestDefinition(questId, out QuestDefinition definition)
               && SetObjectiveState(definition, objectiveId, QuestObjectiveState.Completed);
    }

    public bool FailObjective(string questId, int objectiveId)
    {
        return TryGetQuestDefinition(questId, out QuestDefinition definition)
               && SetObjectiveState(definition, objectiveId, QuestObjectiveState.Failed);
    }

    public bool HideObjective(string questId, int objectiveId)
    {
        return TryGetQuestDefinition(questId, out QuestDefinition definition)
               && SetObjectiveState(definition, objectiveId, QuestObjectiveState.Hidden);
    }

    public bool SetObjectiveState(QuestDefinition definition, int objectiveId, QuestObjectiveState objectiveState)
    {
        QuestRuntimeState state = GetActiveState(definition);
        if (state == null)
            return false;

        QuestObjectiveRuntimeState runtimeObjective = state.GetObjectiveState(objectiveId);
        if (runtimeObjective == null)
            return false;

        if (runtimeObjective.GetState() == objectiveState)
            return true;

        runtimeObjective.SetState(objectiveState);
        QuestObjectiveChanged?.Invoke(state, runtimeObjective);

        if (objectiveState == QuestObjectiveState.Displayed &&
            definition.TryGetObjective(objectiveId, out QuestObjectiveDefinition displayedObjective) &&
            displayedObjective.GetObjectiveType() == QuestObjectiveType.KillSpecificNpcs)
        {
            SetupNpcDeathObjectiveHandler(state, displayedObjective);
        }

        if (objectiveState == QuestObjectiveState.Hidden ||
            objectiveState == QuestObjectiveState.Completed ||
            objectiveState == QuestObjectiveState.Failed)
        {
            ClearObjectiveRuntimeHandlers(state, objectiveId);
        }

        if (state.GetCurrentObjectiveId() == objectiveId && !runtimeObjective.CanBeTracked())
            EnsureCurrentObjective(state);

        if (objectiveState == QuestObjectiveState.Completed &&
            definition.TryGetObjective(objectiveId, out QuestObjectiveDefinition definitionObjective) &&
            definitionObjective.CompletesQuestWhenFinished())
        {
            CompleteQuest(definition);
            return true;
        }

        EvaluateQuestCompletionRules(state);
        if (state.GetStatus() == QuestStatus.Active)
            NotifyQuestUpdated(state);

        return true;
    }

    public bool IsQuestStarted(string questId)
    {
        return TryGetQuestState(questId, out QuestRuntimeState state)
               && state.GetStatus() != QuestStatus.Inactive;
    }

    public bool IsQuestActive(string questId)
    {
        return TryGetQuestState(questId, out QuestRuntimeState state)
               && state.GetStatus() == QuestStatus.Active;
    }

    public bool IsQuestCompleted(string questId)
    {
        return TryGetQuestState(questId, out QuestRuntimeState state)
               && state.GetStatus() == QuestStatus.Completed;
    }

    public bool IsStageDone(string questId, int stageValue)
    {
        return TryGetQuestState(questId, out QuestRuntimeState state)
               && state.IsStageCompleted(stageValue);
    }

    public List<QuestSaveData> CaptureSaveData()
    {
        RebuildLookupTablesIfNeeded();

        List<QuestSaveData> saveData = new List<QuestSaveData>();
        for (int i = 0; i < runtimeStates.Count; i++)
        {
            QuestRuntimeState state = runtimeStates[i];
            if (state == null || !state.GetDefinition())
                continue;

            saveData.Add(QuestSaveData.FromRuntimeState(state));
        }

        return saveData;
    }

    public void RestoreFromSaveData(List<QuestSaveData> saveData)
    {
        if (saveData == null)
            return;

        ClearAllObjectiveRuntimeHandlers();
        RebuildLookupTables();
        currentQuestDefinition = null;

        for (int i = 0; i < saveData.Count; i++)
        {
            QuestSaveData savedQuest = saveData[i];
            if (savedQuest == null || !TryGetQuestDefinition(savedQuest.questId, out QuestDefinition definition))
                continue;

            QuestRuntimeState state = EnsureState(definition);
            state.RestoreFromSaveData(savedQuest);

            if (state.GetStatus() == QuestStatus.Active)
                SetupObjectiveRuntimeHandlers(state);

            if (savedQuest.isCurrentQuest && state.GetStatus() == QuestStatus.Active)
                currentQuestDefinition = definition;
        }

        if (currentQuestDefinition)
            SetCurrentQuest(currentQuestDefinition);
    }

    private void StartGameStartQuests()
    {
        if (hasStartedGameStartQuests)
            return;

        hasStartedGameStartQuests = true;
        for (int i = 0; i < questDefinitions.Count; i++)
        {
            QuestDefinition definition = questDefinitions[i];
            if (definition && definition.ShouldStartOnGameStart())
                StartQuest(definition);
        }
    }

    private QuestRuntimeState GetActiveState(QuestDefinition definition)
    {
        if (!definition)
            return null;

        RegisterQuestDefinition(definition);
        QuestRuntimeState state = EnsureState(definition);
        return state.GetStatus() == QuestStatus.Active ? state : null;
    }

    private QuestRuntimeState EnsureState(QuestDefinition definition)
    {
        RebuildLookupTablesIfNeeded();

        if (statesByDefinition.TryGetValue(definition, out QuestRuntimeState state) && state != null)
            return state;

        state = new QuestRuntimeState(definition);
        runtimeStates.Add(state);
        statesByDefinition[definition] = state;
        return state;
    }

    private void InitializeObjectives(QuestRuntimeState state)
    {
        state.ClearObjectives();

        QuestDefinition definition = state.GetDefinition();
        if (!definition)
            return;

        List<QuestObjectiveDefinition> objectives = definition.GetObjectives();
        if (objectives == null)
            return;

        for (int i = 0; i < objectives.Count; i++)
        {
            QuestObjectiveDefinition objective = objectives[i];
            if (objective == null)
                continue;

            QuestObjectiveState initialState = objective.IsVisibleOnQuestStart()
                ? QuestObjectiveState.Displayed
                : QuestObjectiveState.Hidden;
            state.AddObjective(new QuestObjectiveRuntimeState(objective.GetObjectiveId(), initialState));
        }
    }

    private void SetupObjectiveRuntimeHandlers(QuestRuntimeState state)
    {
        if (state == null || state.GetStatus() != QuestStatus.Active)
            return;

        QuestDefinition definition = state.GetDefinition();
        if (!definition)
            return;

        List<QuestObjectiveDefinition> objectives = definition.GetObjectives();
        if (objectives == null)
            return;

        for (int i = 0; i < objectives.Count; i++)
        {
            QuestObjectiveDefinition objective = objectives[i];
            if (objective == null || objective.GetObjectiveType() != QuestObjectiveType.KillSpecificNpcs)
                continue;

            QuestObjectiveRuntimeState objectiveState = state.GetObjectiveState(objective.GetObjectiveId());
            if (objectiveState == null ||
                objectiveState.GetState() != QuestObjectiveState.Displayed ||
                objectiveState.GetState() == QuestObjectiveState.Completed ||
                objectiveState.GetState() == QuestObjectiveState.Failed)
            {
                continue;
            }

            SetupNpcDeathObjectiveHandler(state, objective);
        }
    }

    private void SetupNpcDeathObjectiveHandler(QuestRuntimeState state, QuestObjectiveDefinition objective)
    {
        if (state == null || objective == null)
            return;

        bool resolvedAllTargets = TrySubscribeNpcDeathObjectiveTargets(state, objective);
        if (!resolvedAllTargets)
            QueueNpcDeathObjectiveBindingRetry(state.GetDefinition(), objective.GetObjectiveId());

        EvaluateNpcDeathObjective(state, objective);
    }

    private bool TrySubscribeNpcDeathObjectiveTargets(QuestRuntimeState state, QuestObjectiveDefinition objective)
    {
        List<string> targetNames = objective.GetRequiredNpcSceneObjectNames();
        if (targetNames == null || targetNames.Count == 0)
            return true;

        bool resolvedAllTargets = true;
        for (int i = 0; i < targetNames.Count; i++)
        {
            if (!TryResolveNpcStateBySceneObjectName(targetNames[i], out NPCState targetState))
            {
                resolvedAllTargets = false;
                continue;
            }

            if (HasNpcDeathObjectiveSubscription(state.GetDefinition(), objective.GetObjectiveId(), targetState))
                continue;

            targetState.OnDied += OnTrackedNpcDied;
            npcDeathObjectiveSubscriptions.Add(new NpcDeathObjectiveSubscription(state.GetDefinition(), objective.GetObjectiveId(), targetState));
        }

        return resolvedAllTargets;
    }

    private void ApplyStageDefinition(QuestRuntimeState state, QuestStageDefinition stage)
    {
        if (state == null || stage == null)
            return;

        ApplyObjectiveStateChanges(state, stage.GetObjectivesToDisplay(), QuestObjectiveState.Displayed);
        if (state.GetStatus() != QuestStatus.Active)
            return;

        ApplyObjectiveStateChanges(state, stage.GetObjectivesToComplete(), QuestObjectiveState.Completed);
        if (state.GetStatus() != QuestStatus.Active)
            return;

        ApplyObjectiveStateChanges(state, stage.GetObjectivesToFail(), QuestObjectiveState.Failed);
        if (state.GetStatus() != QuestStatus.Active)
            return;

        if (stage.ShouldSetCurrentObjective())
            SetCurrentObjective(state.GetDefinition(), stage.GetCurrentObjectiveId());
        else
            EnsureCurrentObjective(state);

        if (stage.FailsQuest())
        {
            FailQuest(state.GetDefinition());
            return;
        }

        if (stage.CompletesQuest())
        {
            CompleteQuest(state.GetDefinition());
            return;
        }

        if (state.GetStatus() != QuestStatus.Active)
            return;

        NotifyQuestUpdated(state);

    }

    private void ApplyObjectiveStateChanges(QuestRuntimeState state, List<int> objectiveIds, QuestObjectiveState objectiveState)
    {
        if (objectiveIds == null)
            return;

        for (int i = 0; i < objectiveIds.Count; i++)
            SetObjectiveState(state.GetDefinition(), objectiveIds[i], objectiveState);
    }

    private void OnTrackedNpcDied(NPCState _)
    {
        EvaluateNpcDeathObjectives();
    }

    private void EvaluateNpcDeathObjectives()
    {
        for (int i = 0; i < runtimeStates.Count; i++)
        {
            QuestRuntimeState state = runtimeStates[i];
            if (state == null || state.GetStatus() != QuestStatus.Active)
                continue;

            QuestDefinition definition = state.GetDefinition();
            if (!definition)
                continue;

            List<QuestObjectiveDefinition> objectives = definition.GetObjectives();
            if (objectives == null)
                continue;

            for (int objectiveIndex = 0; objectiveIndex < objectives.Count; objectiveIndex++)
            {
                QuestObjectiveDefinition objective = objectives[objectiveIndex];
                if (objective != null && objective.GetObjectiveType() == QuestObjectiveType.KillSpecificNpcs)
                    EvaluateNpcDeathObjective(state, objective);
            }
        }
    }

    private void EvaluateNpcDeathObjective(QuestRuntimeState state, QuestObjectiveDefinition objective)
    {
        if (state == null || state.GetStatus() != QuestStatus.Active || objective == null)
            return;

        QuestObjectiveRuntimeState objectiveState = state.GetObjectiveState(objective.GetObjectiveId());
        if (objectiveState == null ||
            objectiveState.GetState() != QuestObjectiveState.Displayed ||
            objectiveState.GetState() == QuestObjectiveState.Completed ||
            objectiveState.GetState() == QuestObjectiveState.Failed)
        {
            return;
        }

        List<string> targetNames = objective.GetRequiredNpcSceneObjectNames();
        if (targetNames == null || targetNames.Count == 0)
            return;

        for (int i = 0; i < targetNames.Count; i++)
        {
            if (!TryResolveNpcStateBySceneObjectName(targetNames[i], out NPCState targetState) || !targetState.IsDead())
                return;
        }

        SetObjectiveState(state.GetDefinition(), objective.GetObjectiveId(), QuestObjectiveState.Completed);
    }

    private void ClearAllObjectiveRuntimeHandlers()
    {
        for (int i = npcDeathObjectiveSubscriptions.Count - 1; i >= 0; i--)
            UnsubscribeNpcDeathObjectiveSubscription(npcDeathObjectiveSubscriptions[i]);

        npcDeathObjectiveSubscriptions.Clear();
        npcDeathObjectiveBindingRetries.Clear();
    }

    private void ClearObjectiveRuntimeHandlers(QuestRuntimeState state)
    {
        if (state == null)
            return;

        ClearObjectiveRuntimeHandlers(state, -1);
    }

    private void ClearObjectiveRuntimeHandlers(QuestRuntimeState state, int objectiveId)
    {
        if (state == null)
            return;

        QuestDefinition definition = state.GetDefinition();
        for (int i = npcDeathObjectiveSubscriptions.Count - 1; i >= 0; i--)
        {
            NpcDeathObjectiveSubscription subscription = npcDeathObjectiveSubscriptions[i];
            if (subscription == null || subscription.Definition != definition)
                continue;

            if (objectiveId >= 0 && subscription.ObjectiveId != objectiveId)
                continue;

            UnsubscribeNpcDeathObjectiveSubscription(subscription);
            npcDeathObjectiveSubscriptions.RemoveAt(i);
        }

        ClearNpcDeathObjectiveBindingRetries(definition, objectiveId);
    }

    private void UnsubscribeNpcDeathObjectiveSubscription(NpcDeathObjectiveSubscription subscription)
    {
        if (subscription != null && subscription.TargetState)
            subscription.TargetState.OnDied -= OnTrackedNpcDied;
    }

    private bool HasNpcDeathObjectiveSubscription(QuestDefinition definition, int objectiveId, NPCState targetState)
    {
        if (!definition || !targetState)
            return false;

        for (int i = 0; i < npcDeathObjectiveSubscriptions.Count; i++)
        {
            NpcDeathObjectiveSubscription subscription = npcDeathObjectiveSubscriptions[i];
            if (subscription != null &&
                subscription.Definition == definition &&
                subscription.ObjectiveId == objectiveId &&
                subscription.TargetState == targetState)
            {
                return true;
            }
        }

        return false;
    }

    private void QueueNpcDeathObjectiveBindingRetry(QuestDefinition definition, int objectiveId)
    {
        if (!definition)
            return;

        for (int i = 0; i < npcDeathObjectiveBindingRetries.Count; i++)
        {
            NpcDeathObjectiveBindingRetry retry = npcDeathObjectiveBindingRetries[i];
            if (retry != null && retry.Definition == definition && retry.ObjectiveId == objectiveId)
                return;
        }

        npcDeathObjectiveBindingRetries.Add(new NpcDeathObjectiveBindingRetry(definition, objectiveId));
        if (npcDeathObjectiveBindingRetryRoutine == null && isActiveAndEnabled)
            npcDeathObjectiveBindingRetryRoutine = StartCoroutine(NpcDeathObjectiveBindingRetryRoutine());
    }

    private IEnumerator NpcDeathObjectiveBindingRetryRoutine()
    {
        while (npcDeathObjectiveBindingRetries.Count > 0)
        {
            yield return new WaitForSeconds(NpcDeathObjectiveBindingRetrySeconds);

            for (int i = npcDeathObjectiveBindingRetries.Count - 1; i >= 0; i--)
            {
                NpcDeathObjectiveBindingRetry retry = npcDeathObjectiveBindingRetries[i];
                if (retry == null ||
                    !retry.Definition ||
                    !TryGetQuestState(retry.Definition, out QuestRuntimeState state) ||
                    state == null ||
                    state.GetStatus() != QuestStatus.Active ||
                    !retry.Definition.TryGetObjective(retry.ObjectiveId, out QuestObjectiveDefinition objective))
                {
                    npcDeathObjectiveBindingRetries.RemoveAt(i);
                    continue;
                }

                bool resolvedAllTargets = TrySubscribeNpcDeathObjectiveTargets(state, objective);
                EvaluateNpcDeathObjective(state, objective);

                if (resolvedAllTargets)
                {
                    npcDeathObjectiveBindingRetries.RemoveAt(i);
                    continue;
                }

                retry.RemainingAttempts--;
                if (retry.RemainingAttempts <= 0)
                {
                    LogMissingNpcDeathObjectiveTargets(state, objective);
                    npcDeathObjectiveBindingRetries.RemoveAt(i);
                }
            }
        }

        npcDeathObjectiveBindingRetryRoutine = null;
    }

    private void ClearNpcDeathObjectiveBindingRetries(QuestDefinition definition, int objectiveId)
    {
        if (!definition)
            return;

        for (int i = npcDeathObjectiveBindingRetries.Count - 1; i >= 0; i--)
        {
            NpcDeathObjectiveBindingRetry retry = npcDeathObjectiveBindingRetries[i];
            if (retry == null || retry.Definition != definition)
                continue;

            if (objectiveId >= 0 && retry.ObjectiveId != objectiveId)
                continue;

            npcDeathObjectiveBindingRetries.RemoveAt(i);
        }
    }

    private void LogMissingNpcDeathObjectiveTargets(QuestRuntimeState state, QuestObjectiveDefinition objective)
    {
        if (state == null || objective == null || !state.GetDefinition())
            return;

        List<string> targetNames = objective.GetRequiredNpcSceneObjectNames();
        if (targetNames == null)
            return;

        for (int i = 0; i < targetNames.Count; i++)
        {
            if (TryResolveNpcStateBySceneObjectName(targetNames[i], out _))
                continue;

            Debug.LogWarning(
                $"QuestController could not find NPC target '{targetNames[i]}' for quest '{state.GetDefinition().GetQuestId()}' objective {objective.GetObjectiveId()}. Available NPC scene objects: {BuildAvailableNpcSceneObjectList()}",
                this);
        }
    }

    private static bool TryResolveNpcStateBySceneObjectName(string sceneObjectName, out NPCState npcState)
    {
        npcState = null;

        if (string.IsNullOrWhiteSpace(sceneObjectName))
            return false;

        string safeSceneObjectName = sceneObjectName.Trim();
        GameObject sceneObject = FindSceneGameObjectByName(safeSceneObjectName);
        if (sceneObject && TryGetNpcState(sceneObject, out npcState))
            return true;

        if (TryFindNpcStateByComponentSceneObjectName(safeSceneObjectName, out npcState))
            return true;

        return false;
    }

    private static bool TryGetNpcState(GameObject sceneObject, out NPCState npcState)
    {
        npcState = null;
        if (!sceneObject)
            return false;

        npcState = sceneObject.GetComponent<NPCState>();
        if (!npcState)
            npcState = sceneObject.GetComponentInParent<NPCState>();
        if (!npcState)
            npcState = sceneObject.GetComponentInChildren<NPCState>(true);

        if (npcState)
            return true;

        NPC npc = sceneObject.GetComponent<NPC>();
        if (!npc)
            npc = sceneObject.GetComponentInParent<NPC>();
        if (!npc)
            npc = sceneObject.GetComponentInChildren<NPC>(true);

        npcState = npc ? npc.GetState() : null;
        return npcState;
    }

    private static bool TryFindNpcStateByComponentSceneObjectName(string sceneObjectName, out NPCState npcState)
    {
        npcState = null;

        NPCState[] npcStates = UnityEngine.Object.FindObjectsByType<NPCState>(FindObjectsInactive.Include);
        for (int i = 0; i < npcStates.Length; i++)
        {
            NPCState candidate = npcStates[i];
            if (!IsLoadedSceneObject(candidate) || !DoesNpcStateMatchSceneObjectName(candidate, sceneObjectName))
                continue;

            npcState = candidate;
            return true;
        }

        NPC[] npcs = UnityEngine.Object.FindObjectsByType<NPC>(FindObjectsInactive.Include);
        for (int i = 0; i < npcs.Length; i++)
        {
            NPC npc = npcs[i];
            if (!IsLoadedSceneObject(npc) || !DoesObjectNameMatch(npc.gameObject.name, sceneObjectName))
                continue;

            npcState = npc.GetState();
            if (npcState)
                return true;
        }

        return false;
    }

    private static bool DoesNpcStateMatchSceneObjectName(NPCState npcState, string sceneObjectName)
    {
        if (!npcState)
            return false;

        Transform current = npcState.transform;
        while (current)
        {
            if (DoesObjectNameMatch(current.name, sceneObjectName))
                return true;

            current = current.parent;
        }

        NPC parentNpc = npcState.GetComponentInParent<NPC>();
        if (parentNpc && DoesObjectNameMatch(parentNpc.gameObject.name, sceneObjectName))
            return true;

        NPC childNpc = npcState.GetComponentInChildren<NPC>(true);
        return childNpc && DoesObjectNameMatch(childNpc.gameObject.name, sceneObjectName);
    }

    private static bool DoesObjectNameMatch(string objectName, string targetName)
    {
        if (string.IsNullOrWhiteSpace(objectName) || string.IsNullOrWhiteSpace(targetName))
            return false;

        string safeObjectName = objectName.Trim();
        string safeTargetName = targetName.Trim();
        if (string.Equals(safeObjectName, safeTargetName, StringComparison.OrdinalIgnoreCase))
            return true;

        const string cloneSuffix = "(Clone)";
        if (safeObjectName.EndsWith(cloneSuffix, StringComparison.OrdinalIgnoreCase))
        {
            string withoutCloneSuffix = safeObjectName.Substring(0, safeObjectName.Length - cloneSuffix.Length).TrimEnd();
            return string.Equals(withoutCloneSuffix, safeTargetName, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsLoadedSceneObject(Component component)
    {
        return component && component.gameObject.scene.IsValid() && component.gameObject.scene.isLoaded;
    }

    private static string BuildAvailableNpcSceneObjectList()
    {
        NPCState[] npcStates = UnityEngine.Object.FindObjectsByType<NPCState>(FindObjectsInactive.Include);
        List<string> names = new List<string>();
        for (int i = 0; i < npcStates.Length; i++)
        {
            NPCState npcState = npcStates[i];
            if (!IsLoadedSceneObject(npcState))
                continue;

            string name = npcState.gameObject.name;
            NPC npc = npcState.GetComponentInParent<NPC>();
            if (npc)
                name = npc.gameObject.name;

            if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name))
                names.Add(name);
        }

        return names.Count > 0 ? string.Join(", ", names) : "none";
    }

    private static GameObject FindSceneGameObjectByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        string safeTargetName = targetName.Trim();
        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject match = FindChildGameObjectByName(roots[rootIndex].transform, safeTargetName);
                if (match)
                    return match;
            }
        }

        return null;
    }

    private static GameObject FindChildGameObjectByName(Transform root, string targetName)
    {
        if (!root)
            return null;

        if (string.Equals(root.name, targetName, StringComparison.OrdinalIgnoreCase))
            return root.gameObject;

        for (int i = 0; i < root.childCount; i++)
        {
            GameObject match = FindChildGameObjectByName(root.GetChild(i), targetName);
            if (match)
                return match;
        }

        return null;
    }

    private void CompleteVisibleObjectives(QuestRuntimeState state, bool notifyObjectiveChanges)
    {
        IReadOnlyList<QuestObjectiveRuntimeState> objectives = state.GetObjectives();
        for (int i = 0; i < objectives.Count; i++)
        {
            QuestObjectiveRuntimeState objective = objectives[i];
            if (objective == null || objective.GetState() != QuestObjectiveState.Displayed)
                continue;

            objective.SetState(QuestObjectiveState.Completed);
            if (notifyObjectiveChanges)
                QuestObjectiveChanged?.Invoke(state, objective);
        }
    }

    private void ClearCurrentQuestIfNeeded(QuestRuntimeState state)
    {
        if (state == null)
            return;

        state.SetCurrentQuest(false);

        if (currentQuestDefinition == state.GetDefinition())
            currentQuestDefinition = null;
    }

    private void EnsureCurrentObjective(QuestRuntimeState state)
    {
        if (state == null)
            return;

        QuestObjectiveRuntimeState current = state.GetObjectiveState(state.GetCurrentObjectiveId());
        if (current != null && current.CanBeTracked())
            return;

        IReadOnlyList<QuestObjectiveRuntimeState> objectives = state.GetObjectives();
        for (int i = 0; i < objectives.Count; i++)
        {
            QuestObjectiveRuntimeState candidate = objectives[i];
            if (candidate == null || !candidate.CanBeTracked())
                continue;

            state.SetCurrentObjectiveId(candidate.GetObjectiveId());
            NotifyCurrentObjectiveChanged(state, candidate);
            return;
        }

        state.SetCurrentObjectiveId(-1);
        NotifyCurrentObjectiveChanged(state, null);
    }

    private void EvaluateQuestCompletionRules(QuestRuntimeState state)
    {
        if (state == null || state.GetStatus() != QuestStatus.Active)
            return;

        QuestDefinition definition = state.GetDefinition();
        if (!definition || definition.GetCompletionMode() != QuestCompletionMode.CompleteWhenAllRequiredObjectivesComplete)
            return;

        List<QuestObjectiveDefinition> objectives = definition.GetObjectives();
        if (objectives == null || objectives.Count == 0)
            return;

        bool foundRequiredObjective = false;
        for (int i = 0; i < objectives.Count; i++)
        {
            QuestObjectiveDefinition objective = objectives[i];
            if (objective == null || !objective.IsRequiredToCompleteQuest())
                continue;

            foundRequiredObjective = true;
            QuestObjectiveRuntimeState objectiveState = state.GetObjectiveState(objective.GetObjectiveId());
            if (objectiveState == null || objectiveState.GetState() != QuestObjectiveState.Completed)
                return;
        }

        if (foundRequiredObjective)
            CompleteQuest(definition);
    }

    private void AwardCompletionExperience(QuestDefinition definition)
    {
        if (!definition)
            return;

        int experienceReward = definition.GetCompletionExperienceReward();
        if (experienceReward <= 0)
            return;

        PlayerState rewardTarget = ResolvePlayerState();
        if (!rewardTarget)
            return;

        rewardTarget.AddExperience(experienceReward);
    }

    private PlayerState ResolvePlayerState()
    {
        if (!playerState)
            playerState = FindAnyObjectByType<PlayerState>();

        return playerState;
    }

    private void NotifyQuestStarted(QuestRuntimeState state)
    {
        QuestStarted?.Invoke(state);
        onQuestStarted.Invoke(state);
    }

    private void NotifyQuestUpdated(QuestRuntimeState state)
    {
        QuestUpdated?.Invoke(state);
        onQuestUpdated.Invoke(state);
    }

    private void NotifyQuestCompleted(QuestRuntimeState state)
    {
        QuestCompleted?.Invoke(state);
        onQuestCompleted.Invoke(state);
    }

    private void NotifyQuestFailed(QuestRuntimeState state)
    {
        QuestFailed?.Invoke(state);
        onQuestFailed.Invoke(state);
    }

    private void NotifyCurrentObjectiveChanged(QuestRuntimeState state, QuestObjectiveRuntimeState objective)
    {
        CurrentObjectiveChanged?.Invoke(state, objective);
        onCurrentObjectiveChanged.Invoke(state, objective);
    }

    private void RebuildLookupTablesIfNeeded()
    {
        if (definitionsById.Count == 0 && questDefinitions.Count > 0)
            RebuildLookupTables();
    }

    private void RebuildLookupTables()
    {
        definitionsById.Clear();
        statesByDefinition.Clear();

        if (questDefinitions == null)
            questDefinitions = new List<QuestDefinition>();

        for (int i = questDefinitions.Count - 1; i >= 0; i--)
        {
            QuestDefinition definition = questDefinitions[i];
            if (!definition)
            {
                questDefinitions.RemoveAt(i);
                continue;
            }

            string questId = definition.GetQuestId();
            if (!string.IsNullOrWhiteSpace(questId))
                definitionsById[questId] = definition;
        }

        if (runtimeStates == null)
            runtimeStates = new List<QuestRuntimeState>();

        for (int i = runtimeStates.Count - 1; i >= 0; i--)
        {
            QuestRuntimeState state = runtimeStates[i];
            if (state == null || !state.GetDefinition())
            {
                runtimeStates.RemoveAt(i);
                continue;
            }

            QuestDefinition definition = state.GetDefinition();
            RegisterDefinitionLookupOnly(definition);
            statesByDefinition[definition] = state;
        }
    }

    private void RegisterDefinitionLookupOnly(QuestDefinition definition)
    {
        if (!definition)
            return;

        if (!questDefinitions.Contains(definition))
            questDefinitions.Add(definition);

        string questId = definition.GetQuestId();
        if (!string.IsNullOrWhiteSpace(questId))
            definitionsById[questId] = definition;
    }

    private static string NormalizeQuestId(string questId)
    {
        return string.IsNullOrWhiteSpace(questId) ? string.Empty : questId.Trim();
    }

    private static string NormalizeQuestSearchName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        char[] buffer = new char[value.Length];
        int count = 0;

        for (int i = 0; i < value.Length; i++)
        {
            char current = value[i];
            if (char.IsWhiteSpace(current) || current == '_' || current == '-')
                continue;

            buffer[count] = char.ToLowerInvariant(current);
            count++;
        }

        return new string(buffer, 0, count);
    }
}

[Serializable]
public class QuestRuntimeState
{
    [SerializeField] private QuestDefinition definition;
    [SerializeField] private QuestStatus status = QuestStatus.Inactive;
    [SerializeField] private int currentStage;
    [SerializeField] private int currentObjectiveId = -1;
    [SerializeField] private bool isCurrentQuest;
    [SerializeField] private List<int> completedStages = new List<int>();
    [SerializeField] private List<QuestObjectiveRuntimeState> objectives = new List<QuestObjectiveRuntimeState>();

    public QuestRuntimeState(QuestDefinition definition)
    {
        this.definition = definition;
    }

    public QuestDefinition GetDefinition()
    {
        return definition;
    }

    public QuestStatus GetStatus()
    {
        return status;
    }

    public int GetCurrentStage()
    {
        return currentStage;
    }

    public int GetCurrentObjectiveId()
    {
        return currentObjectiveId;
    }

    public bool IsCurrentQuest()
    {
        return isCurrentQuest;
    }

    public IReadOnlyList<int> GetCompletedStages()
    {
        return completedStages;
    }

    public IReadOnlyList<QuestObjectiveRuntimeState> GetObjectives()
    {
        return objectives;
    }

    public QuestObjectiveRuntimeState GetObjectiveState(int objectiveId)
    {
        for (int i = 0; i < objectives.Count; i++)
        {
            QuestObjectiveRuntimeState objective = objectives[i];
            if (objective != null && objective.GetObjectiveId() == objectiveId)
                return objective;
        }

        return null;
    }

    public bool IsStageCompleted(int stageValue)
    {
        return completedStages.Contains(stageValue);
    }

    internal void ResetForStart()
    {
        status = QuestStatus.Inactive;
        currentStage = 0;
        currentObjectiveId = -1;
        isCurrentQuest = false;
        completedStages.Clear();
        objectives.Clear();
    }

    internal void SetStatus(QuestStatus value)
    {
        status = value;
    }

    internal void SetCurrentStage(int value)
    {
        currentStage = Mathf.Max(0, value);
    }

    internal void SetCurrentObjectiveId(int value)
    {
        currentObjectiveId = value;
    }

    internal void SetCurrentQuest(bool value)
    {
        isCurrentQuest = value;
    }

    internal void MarkStageCompleted(int stageValue)
    {
        if (!completedStages.Contains(stageValue))
            completedStages.Add(stageValue);
    }

    internal void ClearObjectives()
    {
        objectives.Clear();
    }

    internal void AddObjective(QuestObjectiveRuntimeState objective)
    {
        if (objective != null)
            objectives.Add(objective);
    }

    internal void RestoreFromSaveData(QuestSaveData saveData)
    {
        status = saveData.status;
        currentStage = Mathf.Max(0, saveData.currentStage);
        currentObjectiveId = saveData.currentObjectiveId;
        isCurrentQuest = saveData.isCurrentQuest;

        completedStages.Clear();
        if (saveData.completedStages != null)
            completedStages.AddRange(saveData.completedStages);

        objectives.Clear();
        if (saveData.objectives == null)
            return;

        for (int i = 0; i < saveData.objectives.Count; i++)
        {
            QuestObjectiveSaveData savedObjective = saveData.objectives[i];
            if (savedObjective != null)
                objectives.Add(new QuestObjectiveRuntimeState(savedObjective.objectiveId, savedObjective.state));
        }
    }
}

[Serializable]
public class QuestObjectiveRuntimeState
{
    [SerializeField] private int objectiveId;
    [SerializeField] private QuestObjectiveState state = QuestObjectiveState.Hidden;

    public QuestObjectiveRuntimeState(int objectiveId, QuestObjectiveState state)
    {
        this.objectiveId = objectiveId;
        this.state = state;
    }

    public int GetObjectiveId()
    {
        return objectiveId;
    }

    public QuestObjectiveState GetState()
    {
        return state;
    }

    public bool CanBeTracked()
    {
        return state == QuestObjectiveState.Displayed;
    }

    internal void SetState(QuestObjectiveState value)
    {
        state = value;
    }
}

[Serializable]
public class QuestSaveData
{
    public string questId;
    public QuestStatus status;
    public int currentStage;
    public int currentObjectiveId;
    public bool isCurrentQuest;
    public List<int> completedStages = new List<int>();
    public List<QuestObjectiveSaveData> objectives = new List<QuestObjectiveSaveData>();

    public static QuestSaveData FromRuntimeState(QuestRuntimeState state)
    {
        QuestSaveData saveData = new QuestSaveData
        {
            questId = state.GetDefinition() ? state.GetDefinition().GetQuestId() : string.Empty,
            status = state.GetStatus(),
            currentStage = state.GetCurrentStage(),
            currentObjectiveId = state.GetCurrentObjectiveId(),
            isCurrentQuest = state.IsCurrentQuest(),
            completedStages = new List<int>(state.GetCompletedStages())
        };

        IReadOnlyList<QuestObjectiveRuntimeState> runtimeObjectives = state.GetObjectives();
        for (int i = 0; i < runtimeObjectives.Count; i++)
        {
            QuestObjectiveRuntimeState objective = runtimeObjectives[i];
            if (objective == null)
                continue;

            saveData.objectives.Add(new QuestObjectiveSaveData
            {
                objectiveId = objective.GetObjectiveId(),
                state = objective.GetState()
            });
        }

        return saveData;
    }
}

[Serializable]
public class QuestObjectiveSaveData
{
    public int objectiveId;
    public QuestObjectiveState state;
}

[Serializable]
public class QuestRuntimeStateEvent : UnityEvent<QuestRuntimeState>
{
}

[Serializable]
public class QuestObjectiveRuntimeStateEvent : UnityEvent<QuestRuntimeState, QuestObjectiveRuntimeState>
{
}

public enum QuestStatus
{
    Inactive,
    Active,
    Completed,
    Failed
}

public enum QuestObjectiveState
{
    Hidden,
    Displayed,
    Completed,
    Failed
}
