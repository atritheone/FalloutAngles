using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class QuestUIController : MonoBehaviour
{
    private const string QuestUiObjectName = "QuestUI";
    private const string QuestNameTextObjectName = "QuestNameText";
    private const string ObjectiveTextObjectName = "ObjectiveText";
    private const string CompleteBoxObjectName = "CompleteBox";
    private const string CompletedPrefix = "COMPLETED: ";

    [Header("References")]
    [SerializeField] private QuestController questController;
    [SerializeField] private CanvasGroup questCanvasGroup;
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private TMP_Text objectiveTextTemplate;
    [SerializeField] private GameObject completeBoxTemplate;

    [Header("Timing")]
    [SerializeField, Min(0.0f)] private float questNameCharactersPerSecond = 35.0f;
    [SerializeField, Min(0.0f)] private float questNameHoldSeconds = 2.0f;
    [SerializeField, Min(0.0f)] private float questNameFadeOutSeconds = 0.35f;
    [SerializeField, Min(0.0f)] private float entryFadeInSeconds = 0.25f;
    [SerializeField, Min(0.0f)] private float entryHoldSeconds = 4.0f;
    [SerializeField, Min(0.0f)] private float entryFadeOutSeconds = 0.45f;

    [Header("Layout")]
    [SerializeField, Min(0.0f)] private float entryVerticalSpacing = 10.0f;
    [SerializeField, Min(0.0f)] private float boxTextGap = 10.0f;
    [SerializeField, Min(0.0f)] private float objectiveTextLineVerticalOffset = 1.0f;
    [SerializeField, Min(1.0f)] private float fallbackTextWidth = 700.0f;
    [SerializeField, Min(1.0f)] private float fallbackBoxSize = 12.0f;
    [SerializeField, Min(1.0f)] private float outlineThickness = 2.0f;

    private readonly List<QuestUiEntry> activeEntries = new List<QuestUiEntry>();
    private readonly Queue<string> questNameQueue = new Queue<string>();
    private readonly HashSet<string> visibleEntryKeys = new HashSet<string>();
    private readonly HashSet<string> shownQuestNameKeys = new HashSet<string>();

    private RectTransform rootRectTransform;
    private RectTransform questNameRectTransform;
    private RectTransform objectiveTemplateRectTransform;
    private RectTransform completeBoxTemplateRectTransform;
    private Coroutine questNameRoutine;
    private bool isSubscribed;
    private bool hasInitialized;
    private bool hasProcessedInitialQuestState;
    private float objectiveTextStartY;
    private float objectiveTextRelativeY;
    private float completeBoxRelativeY;

    private sealed class QuestUiEntry
    {
        public string Key;
        public RectTransform Root;
        public CanvasGroup CanvasGroup;
        public TMP_Text Text;
        public float Height;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureQuestUiControllerExists()
    {
        GameObject questUiObject = FindSceneGameObjectByName(QuestUiObjectName);
        if (!questUiObject)
            return;

        if (!questUiObject.GetComponent<QuestUIController>())
            questUiObject.AddComponent<QuestUIController>();
    }

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        EnsureInitialized();
        SubscribeToQuestController();
    }

    private void Start()
    {
        EnsureInitialized();
        SubscribeToQuestController();
        ProcessInitialQuestState();
    }

    private void OnDisable()
    {
        UnsubscribeFromQuestController();
    }

    private void OnDestroy()
    {
        UnsubscribeFromQuestController();
    }

    private void OnValidate()
    {
        questNameCharactersPerSecond = Mathf.Max(0.0f, questNameCharactersPerSecond);
        questNameHoldSeconds = Mathf.Max(0.0f, questNameHoldSeconds);
        questNameFadeOutSeconds = Mathf.Max(0.0f, questNameFadeOutSeconds);
        entryFadeInSeconds = Mathf.Max(0.0f, entryFadeInSeconds);
        entryHoldSeconds = Mathf.Max(0.0f, entryHoldSeconds);
        entryFadeOutSeconds = Mathf.Max(0.0f, entryFadeOutSeconds);
        entryVerticalSpacing = Mathf.Max(0.0f, entryVerticalSpacing);
        boxTextGap = Mathf.Max(0.0f, boxTextGap);
        objectiveTextLineVerticalOffset = Mathf.Max(0.0f, objectiveTextLineVerticalOffset);
        fallbackTextWidth = Mathf.Max(1.0f, fallbackTextWidth);
        fallbackBoxSize = Mathf.Max(1.0f, fallbackBoxSize);
        outlineThickness = Mathf.Max(1.0f, outlineThickness);
    }

    private void EnsureInitialized()
    {
        if (hasInitialized)
            return;

        rootRectTransform = transform as RectTransform;
        if (!rootRectTransform)
            rootRectTransform = gameObject.AddComponent<RectTransform>();

        if (rootRectTransform.localScale.sqrMagnitude <= 0.001f)
            rootRectTransform.localScale = Vector3.one;

        if (!questCanvasGroup)
            questCanvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        if (!questController)
            questController = GetComponent<QuestController>() ?? FindAnyObjectByType<QuestController>(FindObjectsInactive.Include);

        if (!questNameText)
            questNameText = FindChildComponentByName<TMP_Text>(QuestNameTextObjectName);

        if (!objectiveTextTemplate)
            objectiveTextTemplate = FindChildComponentByName<TMP_Text>(ObjectiveTextObjectName);

        if (!completeBoxTemplate)
            completeBoxTemplate = FindChildTransformByName(CompleteBoxObjectName)?.gameObject;

        questNameRectTransform = questNameText ? questNameText.rectTransform : null;
        objectiveTemplateRectTransform = objectiveTextTemplate ? objectiveTextTemplate.rectTransform : null;
        completeBoxTemplateRectTransform = completeBoxTemplate ? completeBoxTemplate.transform as RectTransform : null;

        float objectiveTemplateStartY = objectiveTemplateRectTransform ? objectiveTemplateRectTransform.anchoredPosition.y : -240.0f;
        float completeBoxStartY = completeBoxTemplateRectTransform ? completeBoxTemplateRectTransform.anchoredPosition.y : objectiveTemplateStartY;
        objectiveTextStartY = completeBoxTemplateRectTransform ? completeBoxStartY : objectiveTemplateStartY;
        objectiveTextRelativeY = completeBoxTemplateRectTransform ? 0.0f : objectiveTemplateStartY - objectiveTextStartY;
        completeBoxRelativeY = completeBoxTemplateRectTransform ? 0.0f : completeBoxStartY - objectiveTextStartY;

        ConfigureTemplateObject(questNameText ? questNameText.gameObject : null);
        ConfigureTemplateObject(objectiveTextTemplate ? objectiveTextTemplate.gameObject : null);
        ConfigureTemplateObject(completeBoxTemplate);

        SetTemplateVisible(questNameText ? questNameText.gameObject : null, false);
        SetTemplateVisible(objectiveTextTemplate ? objectiveTextTemplate.gameObject : null, false);
        SetTemplateVisible(completeBoxTemplate, false);
        RefreshRootVisibility();

        hasInitialized = true;
    }

    private void SubscribeToQuestController()
    {
        if (isSubscribed)
            return;

        if (!questController)
            questController = GetComponent<QuestController>() ?? FindAnyObjectByType<QuestController>(FindObjectsInactive.Include);

        if (!questController)
            return;

        questController.QuestStarted += OnQuestStarted;
        questController.QuestObjectiveChanged += OnQuestObjectiveChanged;
        questController.CurrentObjectiveChanged += OnCurrentObjectiveChanged;
        questController.QuestCompleted += OnQuestCompleted;
        questController.QuestFailed += OnQuestFailed;
        isSubscribed = true;
    }

    private void UnsubscribeFromQuestController()
    {
        if (!isSubscribed || !questController)
            return;

        questController.QuestStarted -= OnQuestStarted;
        questController.QuestObjectiveChanged -= OnQuestObjectiveChanged;
        questController.CurrentObjectiveChanged -= OnCurrentObjectiveChanged;
        questController.QuestCompleted -= OnQuestCompleted;
        questController.QuestFailed -= OnQuestFailed;
        isSubscribed = false;
    }

    private void ProcessInitialQuestState()
    {
        if (hasProcessedInitialQuestState || !questController)
            return;

        hasProcessedInitialQuestState = true;
        IReadOnlyList<QuestRuntimeState> states = questController.GetQuestStates();
        for (int i = 0; i < states.Count; i++)
        {
            QuestRuntimeState state = states[i];
            if (state == null || state.GetStatus() != QuestStatus.Active)
                continue;

            ShowQuestName(state);
            QuestObjectiveRuntimeState currentObjective = state.GetObjectiveState(state.GetCurrentObjectiveId());
            if (currentObjective != null && currentObjective.GetState() == QuestObjectiveState.Displayed)
                ShowObjectiveEntry(state, currentObjective, false);
        }
    }

    private void OnQuestStarted(QuestRuntimeState state)
    {
        ShowQuestName(state);
    }

    private void OnQuestObjectiveChanged(QuestRuntimeState state, QuestObjectiveRuntimeState objective)
    {
        if (state == null || objective == null)
            return;

        if (objective.GetState() == QuestObjectiveState.Displayed)
            ShowObjectiveEntry(state, objective, false);
        else if (objective.GetState() == QuestObjectiveState.Completed)
            ShowObjectiveEntry(state, objective, true);
    }

    private void OnCurrentObjectiveChanged(QuestRuntimeState state, QuestObjectiveRuntimeState objective)
    {
        if (state == null || objective == null || objective.GetState() != QuestObjectiveState.Displayed)
            return;

        ShowObjectiveEntry(state, objective, false);
    }

    private void OnQuestCompleted(QuestRuntimeState state)
    {
        if (state == null || !state.GetDefinition())
            return;

        shownQuestNameKeys.Remove(state.GetDefinition().GetQuestId());

        string questName = state.GetDefinition().GetDisplayName();
        ShowListEntry(
            "quest-completed:" + state.GetDefinition().GetQuestId(),
            CompletedPrefix + questName,
            true);
    }

    private void OnQuestFailed(QuestRuntimeState state)
    {
        if (state == null || !state.GetDefinition())
            return;

        shownQuestNameKeys.Remove(state.GetDefinition().GetQuestId());
    }

    private void ShowQuestName(QuestRuntimeState state)
    {
        if (state == null || !state.GetDefinition() || !questNameText)
            return;

        string questName = state.GetDefinition().GetDisplayName();
        if (string.IsNullOrWhiteSpace(questName))
            return;

        string questId = state.GetDefinition().GetQuestId();
        if (!string.IsNullOrWhiteSpace(questId) && shownQuestNameKeys.Contains(questId))
            return;

        if (!string.IsNullOrWhiteSpace(questId))
            shownQuestNameKeys.Add(questId);

        questNameQueue.Enqueue(questName);
        if (questNameRoutine == null)
            questNameRoutine = StartCoroutine(QuestNameRoutine());
    }

    private IEnumerator QuestNameRoutine()
    {
        while (questNameQueue.Count > 0)
        {
            string questName = questNameQueue.Dequeue();
            SetTemplateVisible(questNameText ? questNameText.gameObject : null, true);
            SetGraphicAlpha(questNameText, 1.0f);
            RefreshRootVisibility();

            questNameText.text = questName;
            questNameText.maxVisibleCharacters = 0;

            int visibleCount = 0;
            int characterCount = questName.Length;
            if (questNameCharactersPerSecond <= 0.0f)
            {
                questNameText.maxVisibleCharacters = characterCount;
            }
            else
            {
                float secondsPerCharacter = 1.0f / questNameCharactersPerSecond;
                while (visibleCount < characterCount)
                {
                    visibleCount++;
                    questNameText.maxVisibleCharacters = visibleCount;
                    yield return new WaitForSeconds(secondsPerCharacter);
                }
            }

            yield return new WaitForSeconds(questNameHoldSeconds);
            yield return FadeGraphic(questNameText, 1.0f, 0.0f, questNameFadeOutSeconds);

            questNameText.text = string.Empty;
            questNameText.maxVisibleCharacters = int.MaxValue;
            SetTemplateVisible(questNameText.gameObject, false);
            RefreshRootVisibility();
        }

        questNameRoutine = null;
        RefreshRootVisibility();
    }

    private void ShowObjectiveEntry(QuestRuntimeState state, QuestObjectiveRuntimeState objective, bool completed)
    {
        if (state == null || objective == null || !state.GetDefinition())
            return;

        QuestDefinition definition = state.GetDefinition();
        if (!definition.TryGetObjective(objective.GetObjectiveId(), out QuestObjectiveDefinition objectiveDefinition))
            return;

        string objectiveText = objectiveDefinition.GetDisplayText();
        if (string.IsNullOrWhiteSpace(objectiveText))
            return;

        string key = (completed ? "objective-completed:" : "objective-active:") +
                     definition.GetQuestId() + ":" + objective.GetObjectiveId();
        ShowListEntry(key, completed ? CompletedPrefix + objectiveText : objectiveText, completed);
    }

    private void ShowListEntry(string key, string text, bool completed)
    {
        if (string.IsNullOrWhiteSpace(text))
            return;

        if (!completed && !string.IsNullOrWhiteSpace(key) && visibleEntryKeys.Contains(key))
            return;

        QuestUiEntry entry = CreateEntry(key, text, completed);
        if (entry == null)
            return;

        activeEntries.Add(entry);
        if (!string.IsNullOrWhiteSpace(key))
            visibleEntryKeys.Add(key);

        RelayoutEntries();
        RefreshRootVisibility();
        StartCoroutine(EntryRoutine(entry));
    }

    private QuestUiEntry CreateEntry(string key, string text, bool completed)
    {
        if (!objectiveTextTemplate)
            return null;

        GameObject rowObject = new GameObject("QuestObjectiveEntry", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.SetParent(rootRectTransform, false);
        rowRect.anchorMin = new Vector2(0.0f, 1.0f);
        rowRect.anchorMax = new Vector2(0.0f, 1.0f);
        rowRect.pivot = new Vector2(0.0f, 1.0f);
        rowRect.localScale = Vector3.one;

        CanvasGroup rowCanvasGroup = rowObject.GetComponent<CanvasGroup>();
        rowCanvasGroup.alpha = 0.0f;
        rowCanvasGroup.interactable = false;
        rowCanvasGroup.blocksRaycasts = false;

        GameObject textObject = Instantiate(objectiveTextTemplate.gameObject, rowRect);
        textObject.name = "ObjectiveTextRuntime";
        textObject.SetActive(true);

        TMP_Text textComponent = textObject.GetComponent<TMP_Text>();
        textComponent.text = text;
        textComponent.raycastTarget = false;
        textComponent.textWrappingMode = TextWrappingModes.Normal;
        textComponent.overflowMode = TextOverflowModes.Overflow;
        SetGraphicAlpha(textComponent, 1.0f);

        RectTransform textRect = textComponent.rectTransform;
        textRect.anchorMin = new Vector2(0.0f, 1.0f);
        textRect.anchorMax = new Vector2(0.0f, 1.0f);
        textRect.pivot = new Vector2(0.0f, 1.0f);
        textRect.anchoredPosition = new Vector2(GetObjectiveTextX(), objectiveTextRelativeY + GetObjectiveTextFirstLineYOffset(textComponent));
        textRect.sizeDelta = new Vector2(GetObjectiveTextWidth(), objectiveTemplateRectTransform ? objectiveTemplateRectTransform.sizeDelta.y : 50.0f);
        textRect.localScale = Vector3.one;

        GameObject boxObject = completed ? CreateCompletedBox(rowRect) : CreateIncompleteBox(rowRect);
        RectTransform boxRect = boxObject.transform as RectTransform;
        boxRect.anchorMin = new Vector2(0.0f, 1.0f);
        boxRect.anchorMax = new Vector2(0.0f, 1.0f);
        boxRect.pivot = completeBoxTemplateRectTransform ? completeBoxTemplateRectTransform.pivot : new Vector2(0.5f, 0.5f);
        boxRect.anchoredPosition = new Vector2(GetCompleteBoxX(), completeBoxRelativeY);
        boxRect.sizeDelta = GetCompleteBoxSize();
        boxRect.localScale = Vector3.one;

        float entryHeight = CalculateTextHeight(textComponent, GetObjectiveTextWidth());
        entryHeight = Mathf.Max(entryHeight, GetCompleteBoxSize().y);
        rowRect.sizeDelta = new Vector2(GetEntryWidth(), entryHeight);

        return new QuestUiEntry
        {
            Key = key,
            Root = rowRect,
            CanvasGroup = rowCanvasGroup,
            Text = textComponent,
            Height = entryHeight
        };
    }

    private GameObject CreateCompletedBox(RectTransform parent)
    {
        if (completeBoxTemplate)
        {
            GameObject box = Instantiate(completeBoxTemplate, parent);
            box.name = "CompleteBoxRuntime";
            box.SetActive(true);
            SetGraphicAlpha(box.GetComponent<Graphic>(), 1.0f);
            return box;
        }

        GameObject fallbackBox = new GameObject("CompleteBoxRuntime", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        fallbackBox.transform.SetParent(parent, false);
        Image image = fallbackBox.GetComponent<Image>();
        image.color = GetQuestUIColor();
        image.raycastTarget = false;
        return fallbackBox;
    }

    private GameObject CreateIncompleteBox(RectTransform parent)
    {
        GameObject outlineRoot = new GameObject("IncompleteBoxRuntime", typeof(RectTransform));
        outlineRoot.transform.SetParent(parent, false);

        CreateOutlineSegment("Top", outlineRoot.transform, new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(0.0f, 1.0f), new Vector2(0.0f, outlineThickness));
        CreateOutlineSegment("Bottom", outlineRoot.transform, new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(0.0f, 0.0f), new Vector2(0.0f, outlineThickness));
        CreateOutlineSegment("Left", outlineRoot.transform, new Vector2(0.0f, 0.0f), new Vector2(0.0f, 1.0f), new Vector2(0.0f, 1.0f), new Vector2(outlineThickness, 0.0f));
        CreateOutlineSegment("Right", outlineRoot.transform, new Vector2(1.0f, 0.0f), new Vector2(1.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(outlineThickness, 0.0f));

        return outlineRoot;
    }

    private void CreateOutlineSegment(
        string segmentName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 sizeDelta)
    {
        GameObject segmentObject = new GameObject(segmentName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rectTransform = segmentObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.localScale = Vector3.one;

        Image image = segmentObject.GetComponent<Image>();
        image.color = GetQuestUIColor();
        image.raycastTarget = false;
    }

    private IEnumerator EntryRoutine(QuestUiEntry entry)
    {
        yield return FadeCanvasGroup(entry.CanvasGroup, 0.0f, 1.0f, entryFadeInSeconds);
        yield return new WaitForSeconds(entryHoldSeconds);
        yield return FadeCanvasGroup(entry.CanvasGroup, 1.0f, 0.0f, entryFadeOutSeconds);

        RemoveEntry(entry);
    }

    private void RemoveEntry(QuestUiEntry entry)
    {
        if (entry == null)
            return;

        activeEntries.Remove(entry);
        if (!string.IsNullOrWhiteSpace(entry.Key))
            visibleEntryKeys.Remove(entry.Key);

        if (entry.Root)
            Destroy(entry.Root.gameObject);

        RelayoutEntries();
        RefreshRootVisibility();
    }

    private void RelayoutEntries()
    {
        float y = objectiveTextStartY;
        for (int i = 0; i < activeEntries.Count; i++)
        {
            QuestUiEntry entry = activeEntries[i];
            if (entry == null || !entry.Root)
                continue;

            entry.Root.anchoredPosition = new Vector2(0.0f, y);
            y -= entry.Height + entryVerticalSpacing;
        }
    }

    private float CalculateTextHeight(TMP_Text textComponent, float width)
    {
        if (!textComponent)
            return 24.0f;

        Vector2 preferredValues = textComponent.GetPreferredValues(textComponent.text, width, 0.0f);
        return Mathf.Max(textComponent.fontSize, preferredValues.y);
    }

    private float GetObjectiveTextFirstLineYOffset(TMP_Text textComponent)
    {
        if (!textComponent || objectiveTextLineVerticalOffset <= 0.0f)
            return 0.0f;

        return textComponent.fontSize * objectiveTextLineVerticalOffset;
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup canvasGroup, float fromAlpha, float toAlpha, float duration)
    {
        if (!canvasGroup)
            yield break;

        if (duration <= 0.0f)
        {
            canvasGroup.alpha = toAlpha;
            yield break;
        }

        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(fromAlpha, toAlpha, percent);
            yield return null;
        }

        canvasGroup.alpha = toAlpha;
    }

    private IEnumerator FadeGraphic(Graphic graphic, float fromAlpha, float toAlpha, float duration)
    {
        if (!graphic)
            yield break;

        if (duration <= 0.0f)
        {
            SetGraphicAlpha(graphic, toAlpha);
            yield break;
        }

        float elapsed = 0.0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float percent = Mathf.Clamp01(elapsed / duration);
            SetGraphicAlpha(graphic, Mathf.Lerp(fromAlpha, toAlpha, percent));
            yield return null;
        }

        SetGraphicAlpha(graphic, toAlpha);
    }

    private void RefreshRootVisibility()
    {
        if (!questCanvasGroup)
            return;

        bool visible = activeEntries.Count > 0 ||
                       questNameRoutine != null ||
                       questNameQueue.Count > 0 ||
                       (questNameText && questNameText.gameObject.activeSelf);
        questCanvasGroup.alpha = visible ? 1.0f : 0.0f;
        questCanvasGroup.interactable = false;
        questCanvasGroup.blocksRaycasts = false;
    }

    private float GetObjectiveTextX()
    {
        if (completeBoxTemplateRectTransform)
            return GetCompleteBoxRightEdgeX() + boxTextGap;

        return GetTemplateObjectiveTextX();
    }

    private float GetObjectiveTextWidth()
    {
        if (!objectiveTemplateRectTransform)
            return fallbackTextWidth;

        return Mathf.Max(1.0f, objectiveTemplateRectTransform.sizeDelta.x);
    }

    private float GetCompleteBoxX()
    {
        if (completeBoxTemplateRectTransform)
            return completeBoxTemplateRectTransform.anchoredPosition.x;

        return GetTemplateObjectiveTextX() - fallbackBoxSize - boxTextGap;
    }

    private Vector2 GetCompleteBoxSize()
    {
        if (!completeBoxTemplateRectTransform)
            return new Vector2(fallbackBoxSize, fallbackBoxSize);

        Vector2 size = completeBoxTemplateRectTransform.sizeDelta;
        if (size.x <= 0.0f || size.y <= 0.0f)
            return new Vector2(fallbackBoxSize, fallbackBoxSize);

        return size;
    }

    private float GetEntryWidth()
    {
        return Mathf.Max(GetObjectiveTextX() + GetObjectiveTextWidth(), GetCompleteBoxX() + GetCompleteBoxSize().x);
    }

    private float GetTemplateObjectiveTextX()
    {
        return objectiveTemplateRectTransform ? objectiveTemplateRectTransform.anchoredPosition.x : 48.0f;
    }

    private float GetCompleteBoxRightEdgeX()
    {
        Vector2 boxSize = GetCompleteBoxSize();
        Vector2 boxPivot = completeBoxTemplateRectTransform ? completeBoxTemplateRectTransform.pivot : new Vector2(0.5f, 0.5f);
        return GetCompleteBoxX() + boxSize.x * (1.0f - boxPivot.x);
    }

    private Color GetQuestUIColor()
    {
        if (completeBoxTemplate)
        {
            Graphic graphic = completeBoxTemplate.GetComponent<Graphic>();
            if (graphic)
                return graphic.color;
        }

        if (objectiveTextTemplate)
            return objectiveTextTemplate.color;

        return new Color32(0x00, 0xBB, 0xFF, 0xFF);
    }

    private void ConfigureTemplateObject(GameObject templateObject)
    {
        if (!templateObject)
            return;

        Graphic[] graphics = templateObject.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i])
                graphics[i].raycastTarget = false;
        }
    }

    private static void SetTemplateVisible(GameObject target, bool visible)
    {
        if (target && target.activeSelf != visible)
            target.SetActive(visible);
    }

    private static void SetGraphicAlpha(Graphic graphic, float alpha)
    {
        if (!graphic)
            return;

        Color color = graphic.color;
        color.a = Mathf.Clamp01(alpha);
        graphic.color = color;
    }

    private T FindChildComponentByName<T>(string childName) where T : Component
    {
        T[] components = GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component && component.name == childName)
                return component;
        }

        return null;
    }

    private Transform FindChildTransformByName(string childName)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform candidate = transforms[i];
            if (candidate && candidate.name == childName)
                return candidate;
        }

        return null;
    }

    private static GameObject FindSceneGameObjectByName(string targetName)
    {
        if (string.IsNullOrWhiteSpace(targetName))
            return null;

        for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount; sceneIndex++)
        {
            Scene scene = SceneManager.GetSceneAt(sceneIndex);
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            GameObject[] roots = scene.GetRootGameObjects();
            for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                GameObject match = FindChildGameObjectByName(roots[rootIndex].transform, targetName);
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

        if (root.name == targetName)
            return root.gameObject;

        for (int i = 0; i < root.childCount; i++)
        {
            GameObject match = FindChildGameObjectByName(root.GetChild(i), targetName);
            if (match)
                return match;
        }

        return null;
    }
}
