using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UI
{
    public class LevelUpUIController : MonoBehaviour
    {
        private const string LevelUpRootName = "LevelUpUI";
        private const string WelcomeToTextName = "WelcomeToText";
        private const string PerkScrollName = "PerkScroll";
        private const string PerkDescriptionTextName = "PerkDescriptionText";
        private const string PerkRequirementsTextName = "PerkRequirementsText";
        private const string PerkRanksTextName = "PerkRanksText";
        private const string ContinueTextName = "ContinueText";
        private const string ResetTextName = "ResetText";
        private const string SelectedBoxName = "SelectedBox";
        private const string SelectedBoxPersistentName = "SelectedBoxPersistent";
        private const string PlayerSelectedBoxName = "PlayerSelectedBox";
        private const string RuntimeThemeBackgroundExclusionObjectName = "Background";
        private const float InteractCloseReopenCooldownSeconds = 0.15f;
        private const float PipBoyPaletteColorTolerance = 0.01f;
        private const float SelectionOutlineThickness = 2.0f;
        private const float PerkHoverOutlineThickness = 1.0f;
        private const float PerkHoverOutlineLeftExtension = 18.0f;
        private const float PerkHoverOutlineTopInset = 3.0f;
        private const float PerkHoverOutlineBottomInset = 1.0f;
        private const float RuntimeRowHeight = 28.0f;
        private const float FallbackPerkRowWidth = 460.0f;
        private const float FallbackPerkRowPrefabWidth = 140.0f;

        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text welcomeToText;
        [SerializeField] private ScrollRect perkScroll;
        [SerializeField] private Transform perkContentRoot;
        [SerializeField] private TMP_Text perkDescriptionText;
        [SerializeField] private TMP_Text perkRequirementsText;
        [SerializeField] private TMP_Text perkRanksText;
        [SerializeField] private TMP_Text continueText;
        [SerializeField] private TMP_Text resetText;
        [SerializeField] private GameObject selectedBox;
        [SerializeField] private GameObject perkRowPrefab;
        [SerializeField] private PlayerControls playerControls;
        [SerializeField] private CameraRigOrbit cameraRigOrbit;
        [SerializeField] private CameraControlZoom cameraControlZoom;

        [Header("Perks")]
        [SerializeField] private List<PerkDefinition> availablePerks = new List<PerkDefinition>();
        [SerializeField] private bool includeLoadedPerkAssets = true;
        [SerializeField] private bool onlySelectableLevelUpPerks = true;

        [Header("Runtime Theme")]
        [SerializeField] private Color pipBoyLightColor = new Color32(0x55, 0xD3, 0xF2, 0xFF);
        [SerializeField] private Color pipBoyDarkColor = new Color32(0x00, 0x0E, 0x93, 0xFF);
        [SerializeField] private Color unmetRequirementColor = new Color32(0x55, 0xD3, 0xF2, 0x88);
        [SerializeField] private Color hoverBackgroundColor = new Color32(0x55, 0xD3, 0xF2, 0x22);

        [Header("Behavior")]
        [SerializeField] private bool pauseGameWhenOpen = true;
        [SerializeField] private bool disableGameplayActionsWhenOpen = true;
        [SerializeField] private bool waitForCombatToEnd = true;
        [SerializeField, Min(0.05f)] private float combatPollInterval = 0.2f;
        [SerializeField, Min(0f)] private float queuedLevelUpDelaySeconds = 2f;

        private readonly List<GameObject> spawnedRows = new List<GameObject>();
        private readonly Dictionary<GameObject, PerkDefinition> perkByRowObject = new Dictionary<GameObject, PerkDefinition>();
        private readonly Dictionary<GameObject, bool> rowMeetsRequirements = new Dictionary<GameObject, bool>();
        private readonly Queue<LevelUpRequest> queuedRequests = new Queue<LevelUpRequest>();
        private Coroutine queueRoutine;
        private Coroutine gameplayInputRestoreCoroutine;
        private MonoBehaviour gameplayInputRestoreCoroutineHost;
        private InputSystemActions controls;
        private GameObject hoveredRowObject;
        private GameObject selectedRowObject;
        private PerkDefinition selectedPerk;
        private PlayerState activePlayerState;
        private int activeLevel;
        private bool isOpen;
        private bool isWaitingForSelection;
        private bool hasInitialized;
        private float cachedTimeScale = 1f;
        private static float lastCloseUnscaledTime = float.NegativeInfinity;
        private static LevelUpUIController activeOpenController;

        public static bool QueueLevelUp(PlayerState playerState, int level)
        {
            if (!playerState || level < 1)
                return false;

            LevelUpUIController controller = FindOrCreateActiveController();
            if (!controller)
                return false;

            controller.EnqueueLevelUp(playerState, level);
            return true;
        }

        public static IEnumerator PlayQueuedLevelUpsWhenReady()
        {
            LevelUpUIController controller = FindOrCreateActiveController();
            if (!controller)
                yield break;

            yield return controller.PlayQueue();
        }

        public bool IsOpen()
        {
            return isOpen;
        }

        public static bool IsInputBlockActive()
        {
            if (Time.unscaledTime - lastCloseUnscaledTime <= InteractCloseReopenCooldownSeconds)
                return true;

            if (activeOpenController && activeOpenController.IsOpen())
                return true;

            LevelUpUIController controller = FindFirstInSceneIncludingInactive();
            return controller && controller.IsOpen();
        }

        private void Awake()
        {
            EnsureInitialized();
            SetOpenState(false, true);
        }

        private void OnEnable()
        {
            EnsureInitialized();
        }

        private void OnDisable()
        {
            CancelPendingGameplayInputRestore();
            if (isOpen)
                SetOpenState(false, true);
        }

        private void Update()
        {
            if (!isOpen)
                return;

            RefreshSelectedBoxIndicator();

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.rKey.wasPressedThisFrame)
            {
                ClearSelection();
                return;
            }

            if (keyboard.eKey.wasPressedThisFrame)
                TryContinueWithSelectedPerk();
        }

        private void EnqueueLevelUp(PlayerState playerState, int level)
        {
            queuedRequests.Enqueue(new LevelUpRequest(playerState, Mathf.Clamp(level, 1, PlayerState.MaxPlayerLevel)));
        }

        private IEnumerator PlayQueue()
        {
            if (queueRoutine != null)
            {
                while (queueRoutine != null)
                    yield return null;
                yield break;
            }

            queueRoutine = StartCoroutine(PlayQueueRoutine());
            while (queueRoutine != null)
                yield return null;
        }

        private IEnumerator PlayQueueRoutine()
        {
            while (queuedRequests.Count > 0)
            {
                LevelUpRequest request = queuedRequests.Dequeue();
                if (!request.PlayerState)
                    continue;

                if (waitForCombatToEnd)
                    yield return WaitForCombatToEnd(request.PlayerState);

                Open(request.PlayerState, request.Level);
                while (isWaitingForSelection)
                    yield return null;

                if (queuedRequests.Count > 0)
                    yield return WaitBetweenQueuedLevelUps();
            }

            queueRoutine = null;
            RestoreGameplayActionsAfterLevelUpQueue();
        }

        private IEnumerator WaitBetweenQueuedLevelUps()
        {
            float delaySeconds = Mathf.Max(0f, queuedLevelUpDelaySeconds);
            if (delaySeconds <= 0f)
                yield break;

            float elapsed = 0f;
            while (elapsed < delaySeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private IEnumerator WaitForCombatToEnd(PlayerState playerState)
        {
            while (IsCombatBlockingLevelUp(playerState))
            {
                float elapsed = 0f;
                while (elapsed < combatPollInterval)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        private void Open(PlayerState playerState, int level)
        {
            EnsureInitialized();
            activePlayerState = playerState ? playerState : FindAnyObjectByType<PlayerState>();
            activeLevel = Mathf.Clamp(level, 1, PlayerState.MaxPlayerLevel);
            ClearSelection();
            SetOpenState(true, false);
            PopulatePerkList();
            UpdateDetails(null);
        }

        private void Close()
        {
            SetOpenState(false, false);
        }

        private void TryContinueWithSelectedPerk()
        {
            if (!selectedPerk || !activePlayerState)
                return;

            if (!DoesPlayerMeetRequirements(activePlayerState, selectedPerk, activeLevel))
                return;

            activePlayerState.AddPerk(selectedPerk);
            isWaitingForSelection = false;
            Close();
        }

        private void ClearSelection()
        {
            selectedPerk = null;
            selectedRowObject = null;
            RefreshSelectedBoxIndicator();
            UpdateDetails(hoveredRowObject ? ResolvePerkForRow(hoveredRowObject) : null);
        }

        private void PopulatePerkList()
        {
            List<PerkDefinition> perks = BuildAvailablePerkList();
            int targetCount = perks.Count;
            ConfigurePerkScrollContent();
            EnsureRows(targetCount);

            perkByRowObject.Clear();
            rowMeetsRequirements.Clear();
            Transform listParent = ResolvePerkContentRoot();

            for (int i = 0; i < targetCount; i++)
            {
                GameObject rowObject = spawnedRows[i];
                if (!rowObject)
                    continue;

                PerkDefinition perk = perks[i];
                if (listParent && rowObject.transform.parent != listParent)
                    rowObject.transform.SetParent(listParent, false);

                rowObject.transform.SetSiblingIndex(i);
                SetActiveSafe(rowObject, true);

                TMP_Text rowText = rowObject.GetComponentInChildren<TMP_Text>(true);
                SetTextIfChanged(rowText, perk ? perk.GetPerkName() : string.Empty);

                bool meetsRequirements = activePlayerState && perk && DoesPlayerMeetRequirements(activePlayerState, perk, activeLevel);
                if (rowText)
                    rowText.color = meetsRequirements ? pipBoyLightColor : unmetRequirementColor;

                perkByRowObject[rowObject] = perk;
                rowMeetsRequirements[rowObject] = meetsRequirements;
                BindRowEvents(rowObject);
                ConfigureRowVisualState(rowObject, false);
            }

            for (int i = targetCount; i < spawnedRows.Count; i++)
                SetActiveSafe(spawnedRows[i], false);

            RebuildPerkScrollLayout();
            RefreshSelectedBoxIndicator();
        }

        private void ConfigurePerkScrollContent()
        {
            RectTransform contentRect = ResolvePerkContentRoot() as RectTransform;
            if (!contentRect)
                return;

            VerticalLayoutGroup verticalLayoutGroup = contentRect.GetComponent<VerticalLayoutGroup>();
            if (verticalLayoutGroup)
                verticalLayoutGroup.enabled = true;

            ContentSizeFitter contentSizeFitter = contentRect.GetComponent<ContentSizeFitter>();
            if (contentSizeFitter)
                contentSizeFitter.enabled = true;

            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0f, 1f);
        }

        private float ResolvePerkRowWidth()
        {
            float width = 0f;

            if (perkScroll && perkScroll.viewport)
                width = perkScroll.viewport.rect.width;

            if (width <= 1f && perkScroll && perkScroll.transform is RectTransform scrollRectTransform)
                width = scrollRectTransform.rect.width;

            if (width <= 1f && perkContentRoot is RectTransform contentRect)
                width = contentRect.rect.width;

            return width > 1f ? width : FallbackPerkRowWidth;
        }

        private List<PerkDefinition> BuildAvailablePerkList()
        {
            List<PerkDefinition> perks = new List<PerkDefinition>();
            AddUniquePerks(perks, availablePerks);

#if UNITY_EDITOR
            AddUniquePerks(perks, LoadEditorPerkAssets());
#endif

            if (includeLoadedPerkAssets)
                AddUniquePerks(perks, Resources.FindObjectsOfTypeAll<PerkDefinition>());

            for (int i = perks.Count - 1; i >= 0; i--)
            {
                PerkDefinition perk = perks[i];
                if (!perk)
                {
                    perks.RemoveAt(i);
                    continue;
                }

                if (onlySelectableLevelUpPerks && !perk.IsSelectableAtLevelUp())
                {
                    perks.RemoveAt(i);
                    continue;
                }

                if (activePlayerState && activePlayerState.HasPerk(perk))
                    perks.RemoveAt(i);
            }

            perks.Sort((left, right) =>
            {
                bool leftMeetsRequirements = activePlayerState && left && DoesPlayerMeetRequirements(activePlayerState, left, activeLevel);
                bool rightMeetsRequirements = activePlayerState && right && DoesPlayerMeetRequirements(activePlayerState, right, activeLevel);
                if (leftMeetsRequirements != rightMeetsRequirements)
                    return leftMeetsRequirements ? -1 : 1;

                string leftName = left ? left.GetPerkName() : string.Empty;
                string rightName = right ? right.GetPerkName() : string.Empty;
                int nameComparison = string.Compare(leftName, rightName, System.StringComparison.OrdinalIgnoreCase);
                if (nameComparison != 0)
                    return nameComparison;

                return string.Compare(leftName, rightName, System.StringComparison.Ordinal);
            });

            return perks;
        }

        private static void AddUniquePerks(ICollection<PerkDefinition> target, IEnumerable<PerkDefinition> source)
        {
            if (target == null || source == null)
                return;

            foreach (PerkDefinition perk in source)
            {
                if (!perk || target.Contains(perk))
                    continue;

                target.Add(perk);
            }
        }

#if UNITY_EDITOR
        private static IEnumerable<PerkDefinition> LoadEditorPerkAssets()
        {
            string[] perkAssetGuids = AssetDatabase.FindAssets("t:PerkDefinition", new[] { "Assets/Definitions/Perks" });
            for (int i = 0; i < perkAssetGuids.Length; i++)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(perkAssetGuids[i]);
                if (string.IsNullOrWhiteSpace(assetPath))
                    continue;

                PerkDefinition perk = AssetDatabase.LoadAssetAtPath<PerkDefinition>(assetPath);
                if (perk)
                    yield return perk;
            }
        }
#endif

        private void EnsureRows(int targetCount)
        {
            Transform parent = ResolvePerkContentRoot();
            if (!parent)
                return;

            for (int i = spawnedRows.Count - 1; i >= 0; i--)
            {
                if (spawnedRows[i])
                    continue;

                spawnedRows.RemoveAt(i);
            }

            for (int i = spawnedRows.Count; i < targetCount; i++)
            {
                GameObject rowObject = perkRowPrefab
                    ? Instantiate(perkRowPrefab, parent, false)
                    : CreateRuntimeRow(parent);

                ApplyPipBoyPaletteColorOverrides(rowObject);
                spawnedRows.Add(rowObject);
            }
        }

        private GameObject CreateRuntimeRow(Transform parent)
        {
            GameObject rowObject = new GameObject("PerkRow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.SetParent(parent, false);
            ConfigureRuntimePerkRowRect(rowObject);

            Image rowImage = rowObject.GetComponent<Image>();
            rowImage.color = Color.clear;
            rowImage.raycastTarget = true;

            GameObject textObject = new GameObject("PerkNameText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            RectTransform textRect = textObject.GetComponent<RectTransform>();
            textRect.SetParent(rowRect, false);
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(40f, 0f);
            textRect.offsetMax = new Vector2(-8f, 0f);

            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.fontSize = 24f;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.color = pipBoyLightColor;
            text.raycastTarget = false;

            EnsureOutlineSegments(rowRect, pipBoyLightColor);
            return rowObject;
        }

        private void ConfigureRuntimePerkRowRect(GameObject rowObject)
        {
            if (!rowObject)
                return;

            RectTransform rowRect = rowObject.transform as RectTransform;
            if (!rowRect)
                return;

            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(0f, 1f);
            rowRect.pivot = new Vector2(0f, 1f);
            rowRect.anchoredPosition = Vector2.zero;

            float rowWidth = ResolvePerkRowWidth();
            if (rowWidth <= 1f)
                rowWidth = FallbackPerkRowPrefabWidth;

            rowRect.sizeDelta = new Vector2(Mathf.Max(1f, rowWidth), RuntimeRowHeight);

            LayoutElement layoutElement = rowObject.GetComponent<LayoutElement>();
            if (!layoutElement)
                layoutElement = rowObject.AddComponent<LayoutElement>();

            layoutElement.minWidth = Mathf.Max(1f, rowWidth);
            layoutElement.preferredWidth = Mathf.Max(1f, rowWidth);
            layoutElement.flexibleWidth = 0f;
            layoutElement.minHeight = RuntimeRowHeight;
            layoutElement.preferredHeight = RuntimeRowHeight;
            layoutElement.flexibleHeight = 0f;
        }

        private void RebuildPerkScrollLayout()
        {
            RectTransform contentRect = ResolvePerkContentRoot() as RectTransform;
            if (contentRect)
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);

            if (perkScroll)
            {
                perkScroll.normalizedPosition = new Vector2(perkScroll.normalizedPosition.x, 1f);
                perkScroll.StopMovement();
            }
        }

        private void ConfigureActionTextButtons()
        {
            ConfigureActionTextButton(continueText, TryContinueWithSelectedPerk);
            ConfigureActionTextButton(resetText, ClearSelection);
        }

        private void ConfigureActionTextButton(TMP_Text text, System.Action action)
        {
            if (!text || action == null)
                return;

            text.raycastTarget = true;
            text.color = pipBoyLightColor;

            Button button = text.GetComponent<Button>();
            if (button)
            {
                button.onClick.RemoveAllListeners();
                button.targetGraphic = null;
                button.transition = Selectable.Transition.None;
                button.interactable = false;
                button.enabled = false;
            }

            EventTrigger eventTrigger = text.GetComponent<EventTrigger>();
            if (!eventTrigger)
                eventTrigger = text.gameObject.AddComponent<EventTrigger>();

            if (eventTrigger.triggers == null)
                eventTrigger.triggers = new List<EventTrigger.Entry>();

            eventTrigger.triggers.Clear();

            EventTrigger.Entry pointerClickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            pointerClickEntry.callback.AddListener(eventData => OnActionTextPointerClick(eventData, action));
            eventTrigger.triggers.Add(pointerClickEntry);
        }

        private void OnActionTextPointerClick(BaseEventData eventData, System.Action action)
        {
            if (!isOpen || action == null)
                return;

            if (eventData is PointerEventData pointerEventData &&
                pointerEventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            action();
        }

        private void BindRowEvents(GameObject rowObject)
        {
            if (!rowObject)
                return;

            TMP_Text rowText = rowObject.GetComponentInChildren<TMP_Text>(true);
            if (rowText)
                rowText.raycastTarget = false;

            Image rowImage = rowObject.GetComponent<Image>();
            if (!rowImage)
                rowImage = rowObject.AddComponent<Image>();

            rowImage.raycastTarget = true;
            if (rowImage.color.a > 0.001f && rowImage.gameObject.name != RuntimeThemeBackgroundExclusionObjectName)
                rowImage.color = new Color(rowImage.color.r, rowImage.color.g, rowImage.color.b, 0f);

            RectTransform rowRect = rowObject.transform as RectTransform;
            EnsureOutlineSegments(rowRect, pipBoyLightColor);
            SetOutlineSegmentsActive(rowRect, false);

            Button rowButton = rowObject.GetComponent<Button>();
            if (rowButton)
            {
                rowButton.transition = Selectable.Transition.None;
                rowButton.interactable = true;
                rowButton.onClick.RemoveAllListeners();
                rowButton.onClick.AddListener(() => SelectRow(rowObject));
            }

            EventTrigger eventTrigger = rowObject.GetComponent<EventTrigger>();
            if (!eventTrigger)
                eventTrigger = rowObject.AddComponent<EventTrigger>();

            if (eventTrigger.triggers == null)
                eventTrigger.triggers = new List<EventTrigger.Entry>();

            eventTrigger.triggers.Clear();

            EventTrigger.Entry pointerEnterEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            pointerEnterEntry.callback.AddListener(_ => OnRowPointerEnter(rowObject));
            eventTrigger.triggers.Add(pointerEnterEntry);

            EventTrigger.Entry pointerExitEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            pointerExitEntry.callback.AddListener(_ => OnRowPointerExit(rowObject));
            eventTrigger.triggers.Add(pointerExitEntry);

            EventTrigger.Entry pointerClickEntry = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            pointerClickEntry.callback.AddListener(eventData => OnRowPointerClick(rowObject, eventData));
            eventTrigger.triggers.Add(pointerClickEntry);

            if (perkScroll)
            {
                EventTrigger.Entry scrollEntry = new EventTrigger.Entry { eventID = EventTriggerType.Scroll };
                scrollEntry.callback.AddListener(eventData => ForwardScrollToPerkScroll(eventData));
                eventTrigger.triggers.Add(scrollEntry);
            }
        }

        private void OnRowPointerEnter(GameObject rowObject)
        {
            hoveredRowObject = rowObject;
            ConfigureRowVisualState(rowObject, true);
            UpdateDetails(ResolvePerkForRow(rowObject));
        }

        private void OnRowPointerExit(GameObject rowObject)
        {
            if (hoveredRowObject == rowObject)
                hoveredRowObject = null;

            ConfigureRowVisualState(rowObject, false);
            UpdateDetails(selectedPerk);
        }

        private void OnRowPointerClick(GameObject rowObject, BaseEventData eventData)
        {
            if (eventData is PointerEventData pointerEventData &&
                pointerEventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            SelectRow(rowObject);
        }

        private void SelectRow(GameObject rowObject)
        {
            PerkDefinition perk = ResolvePerkForRow(rowObject);
            if (!perk)
                return;

            if (!DoesRowMeetRequirements(rowObject))
            {
                ClearSelection();
                UpdateDetails(perk);
                return;
            }

            selectedPerk = perk;
            selectedRowObject = rowObject;
            RefreshSelectedBoxIndicator();
            UpdateDetails(perk);
        }

        private void ConfigureRowVisualState(GameObject rowObject, bool hovered)
        {
            if (!rowObject)
                return;

            Image rowImage = rowObject.GetComponent<Image>();
            if (rowImage)
                rowImage.color = Color.clear;

            SetOutlineSegmentsActive(rowObject.transform as RectTransform, hovered);
        }

        private void UpdateDetails(PerkDefinition perk)
        {
            SetTextIfChanged(perkDescriptionText, perk ? perk.GetDescription() : string.Empty);
            SetTextIfChanged(perkRequirementsText, perk ? BuildRequirementsText(perk) : string.Empty);
            SetTextIfChanged(perkRanksText, perk ? BuildRanksText(perk) : string.Empty);
        }

        private string BuildRequirementsText(PerkDefinition perk)
        {
            if (!perk)
                return string.Empty;

            List<string> requirements = new List<string>();
            int requiredLevel = perk.GetRequiredLevel();
            if (requiredLevel > 0)
                requirements.Add("Level " + requiredLevel);

            List<PerkSpecialRequirement> specialRequirements = perk.GetSpecialRequirements();
            if (specialRequirements != null)
            {
                for (int i = 0; i < specialRequirements.Count; i++)
                {
                    PerkSpecialRequirement requirement = specialRequirements[i];
                    if (requirement == null)
                        continue;

                    requirements.Add(GetSpecialRequirementDisplayName(requirement.GetStat()) + " " + requirement.GetMinimumValue());
                }
            }

            List<PerkSkillRequirement> skillRequirements = perk.GetSkillRequirements();
            if (skillRequirements != null)
            {
                for (int i = 0; i < skillRequirements.Count; i++)
                {
                    PerkSkillRequirement requirement = skillRequirements[i];
                    if (requirement == null || requirement.GetSkill() == PlayerSkill.None)
                        continue;

                    requirements.Add(GetSkillDisplayName(requirement.GetSkill()) + " " + requirement.GetMinimumValue());
                }
            }

            List<PerkDefinition> prerequisitePerks = perk.GetPrerequisitePerks();
            if (prerequisitePerks != null)
            {
                for (int i = 0; i < prerequisitePerks.Count; i++)
                {
                    if (prerequisitePerks[i])
                        requirements.Add(prerequisitePerks[i].GetPerkName());
                }
            }

            List<string> customRequirements = perk.GetCustomRequirements();
            if (customRequirements != null)
            {
                for (int i = 0; i < customRequirements.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(customRequirements[i]))
                        requirements.Add(customRequirements[i].Trim());
                }
            }

            return "Req: " + (requirements.Count > 0 ? string.Join(", ", requirements) : "None");
        }

        private string BuildRanksText(PerkDefinition perk)
        {
            if (!perk)
                return string.Empty;

            return "Ranks: " + perk.GetMaxRank();
        }

        private bool DoesPlayerMeetRequirements(PlayerState playerState, PerkDefinition perk, int level)
        {
            if (!playerState || !perk)
                return false;

            if (level < perk.GetRequiredLevel())
                return false;

            if (!MeetsSpecialRequirements(playerState, perk))
                return false;

            if (!MeetsSkillRequirements(playerState, perk))
                return false;

            if (!MeetsPrerequisitePerks(playerState, perk))
                return false;

            if (HasMutuallyExclusivePerk(playerState, perk))
                return false;

            if (perk.GetGenderRequirement() != PerkGender.Any)
                return false;

            if (perk.GetKarmaRequirement() != PerkKarmaRequirement.Any)
                return false;

            List<PerkQuestRequirement> questRequirements = perk.GetQuestRequirements();
            if (questRequirements != null && questRequirements.Count > 0)
                return false;

            List<string> customRequirements = perk.GetCustomRequirements();
            if (customRequirements != null && customRequirements.Count > 0)
                return false;

            return true;
        }

        private static bool MeetsSpecialRequirements(PlayerState playerState, PerkDefinition perk)
        {
            List<PerkSpecialRequirement> requirements = perk.GetSpecialRequirements();
            if (requirements == null)
                return true;

            for (int i = 0; i < requirements.Count; i++)
            {
                PerkSpecialRequirement requirement = requirements[i];
                if (requirement == null)
                    continue;

                if (GetSpecialValue(playerState, requirement.GetStat()) < requirement.GetMinimumValue())
                    return false;
            }

            return true;
        }

        private static bool MeetsSkillRequirements(PlayerState playerState, PerkDefinition perk)
        {
            List<PerkSkillRequirement> requirements = perk.GetSkillRequirements();
            if (requirements == null)
                return true;

            for (int i = 0; i < requirements.Count; i++)
            {
                PerkSkillRequirement requirement = requirements[i];
                if (requirement == null || requirement.GetSkill() == PlayerSkill.None)
                    continue;

                if (GetSkillValue(playerState, requirement.GetSkill()) < requirement.GetMinimumValue())
                    return false;
            }

            return true;
        }

        private static bool MeetsPrerequisitePerks(PlayerState playerState, PerkDefinition perk)
        {
            List<PerkDefinition> prerequisites = perk.GetPrerequisitePerks();
            if (prerequisites == null)
                return true;

            for (int i = 0; i < prerequisites.Count; i++)
            {
                PerkDefinition prerequisite = prerequisites[i];
                if (prerequisite && !playerState.HasPerk(prerequisite))
                    return false;
            }

            return true;
        }

        private static bool HasMutuallyExclusivePerk(PlayerState playerState, PerkDefinition perk)
        {
            List<PerkDefinition> mutuallyExclusivePerks = perk.GetMutuallyExclusivePerks();
            if (mutuallyExclusivePerks == null)
                return false;

            for (int i = 0; i < mutuallyExclusivePerks.Count; i++)
            {
                PerkDefinition mutuallyExclusivePerk = mutuallyExclusivePerks[i];
                if (mutuallyExclusivePerk && playerState.HasPerk(mutuallyExclusivePerk))
                    return true;
            }

            return false;
        }

        private static int GetSpecialValue(PlayerState playerState, PerkSpecialStat stat)
        {
            if (!playerState)
                return 0;

            switch (stat)
            {
                case PerkSpecialStat.Strength:
                    return playerState.GetStrength();
                case PerkSpecialStat.Perception:
                    return playerState.GetPerception();
                case PerkSpecialStat.Endurance:
                    return playerState.GetEndurance();
                case PerkSpecialStat.Charisma:
                    return playerState.GetCharisma();
                case PerkSpecialStat.Intelligence:
                    return playerState.GetIntelligence();
                case PerkSpecialStat.Agility:
                    return playerState.GetAgility();
                case PerkSpecialStat.Luck:
                    return playerState.GetLuck();
                default:
                    return 0;
            }
        }

        private static int GetSkillValue(PlayerState playerState, PlayerSkill skill)
        {
            if (!playerState)
                return 0;

            switch (skill)
            {
                case PlayerSkill.Barter:
                    return playerState.GetBarter();
                case PlayerSkill.BigGuns:
                    return playerState.GetBigGuns();
                case PlayerSkill.EnergyWeapons:
                    return playerState.GetEnergyWeapons();
                case PlayerSkill.Explosives:
                    return playerState.GetExplosives();
                case PlayerSkill.Lockpick:
                    return playerState.GetLockpick();
                case PlayerSkill.Medicine:
                    return playerState.GetMedicine();
                case PlayerSkill.MeleeWeapons:
                    return playerState.GetMeleeWeapons();
                case PlayerSkill.Repair:
                    return playerState.GetRepair();
                case PlayerSkill.Science:
                    return playerState.GetScience();
                case PlayerSkill.SmallGuns:
                    return playerState.GetSmallGuns();
                case PlayerSkill.Sneak:
                    return playerState.GetSneak();
                case PlayerSkill.Speech:
                    return playerState.GetSpeech();
                case PlayerSkill.Unarmed:
                    return playerState.GetUnarmed();
                default:
                    return 0;
            }
        }

        private static string GetSpecialRequirementDisplayName(PerkSpecialStat stat)
        {
            switch (stat)
            {
                case PerkSpecialStat.Strength:
                    return "STR";
                case PerkSpecialStat.Perception:
                    return "PER";
                case PerkSpecialStat.Endurance:
                    return "END";
                case PerkSpecialStat.Charisma:
                    return "CHR";
                case PerkSpecialStat.Intelligence:
                    return "INT";
                case PerkSpecialStat.Agility:
                    return "AGL";
                case PerkSpecialStat.Luck:
                    return "LCK";
                default:
                    return string.Empty;
            }
        }

        private static string GetSkillDisplayName(PlayerSkill skill)
        {
            switch (skill)
            {
                case PlayerSkill.Barter:
                    return "Barter";
                case PlayerSkill.BigGuns:
                    return "Big Guns";
                case PlayerSkill.EnergyWeapons:
                    return "Energy Weapons";
                case PlayerSkill.Explosives:
                    return "Explosives";
                case PlayerSkill.Lockpick:
                    return "Lockpick";
                case PlayerSkill.Medicine:
                    return "Medicine";
                case PlayerSkill.MeleeWeapons:
                    return "Melee Weapons";
                case PlayerSkill.Repair:
                    return "Repair";
                case PlayerSkill.Science:
                    return "Science";
                case PlayerSkill.SmallGuns:
                    return "Small Guns";
                case PlayerSkill.Sneak:
                    return "Sneak";
                case PlayerSkill.Speech:
                    return "Speech";
                case PlayerSkill.Unarmed:
                    return "Unarmed";
                default:
                    return string.Empty;
            }
        }

        private static bool IsCombatBlockingLevelUp(PlayerState playerState)
        {
            if (playerState && playerState.GetCombatMode())
                return true;

            NPCCombat[] npcCombats = FindObjectsByType<NPCCombat>(FindObjectsInactive.Exclude);
            for (int i = 0; i < npcCombats.Length; i++)
            {
                NPCCombat npcCombat = npcCombats[i];
                if (!npcCombat || !npcCombat.isActiveAndEnabled)
                    continue;

                if (npcCombat.IsAggroedOrSearchingForPlayer())
                    return true;
            }

            return false;
        }

        private void SetOpenState(bool open, bool forceWithoutSideEffects)
        {
            bool wasOpen = isOpen;
            isOpen = open;
            isWaitingForSelection = open;

            if (open)
                activeOpenController = this;
            else if (activeOpenController == this)
                activeOpenController = null;

            if (open)
                SetHierarchyActive(transform, true);

            if (canvasGroup)
            {
                canvasGroup.alpha = open ? 1f : 0f;
                canvasGroup.interactable = open;
                canvasGroup.blocksRaycasts = open;
            }

            if (open)
                SetActiveSafe(gameObject, true);

            if (forceWithoutSideEffects || wasOpen == open)
                return;

            if (open)
            {
                CancelPendingGameplayInputRestore();
                ApplyPipBoyPaletteColorOverrides(gameObject);
                DisableNonInteractiveGraphicRaycasts();
                ConfigureActionTextButtons();
                SetTextIfChanged(welcomeToText, "WELCOME TO LEVEL " + activeLevel);

                if (cameraRigOrbit)
                    cameraRigOrbit.SetInputEnabled(false);

                if (cameraControlZoom)
                    cameraControlZoom.SetInputEnabled(false);

                if (pauseGameWhenOpen)
                    PauseGameTime();

                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;

                if (controls != null)
                {
                    controls.UI.Enable();
                    if (disableGameplayActionsWhenOpen)
                        SetGameplayActionsEnabled(false);
                }

                return;
            }

            if (pauseGameWhenOpen)
                ResumeGameTime();

            lastCloseUnscaledTime = Time.unscaledTime;

            if (controls != null)
            {
                if (disableGameplayActionsWhenOpen)
                    RestoreGameplayActionsAfterKeyboardRelease();
                controls.UI.Disable();
            }

            if (cameraRigOrbit)
                cameraRigOrbit.SetInputEnabled(true);
            else
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }

            if (cameraControlZoom)
                cameraControlZoom.SetInputEnabled(true);

            activePlayerState = null;
            activeLevel = 0;
            hoveredRowObject = null;
            ClearSelection();
        }

        private void EnsureInitialized()
        {
            if (hasInitialized)
                return;

            if (!canvasGroup)
                canvasGroup = GetComponent<CanvasGroup>();

            if (!canvasGroup)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (!welcomeToText)
                welcomeToText = FindChildComponentByName<TMP_Text>(transform, WelcomeToTextName);

            if (!perkScroll)
                perkScroll = FindChildComponentByName<ScrollRect>(transform, PerkScrollName);

            if (!perkContentRoot)
                perkContentRoot = perkScroll && perkScroll.content ? perkScroll.content : FindChildTransformByName(transform, "Content");

            if (!perkDescriptionText)
                perkDescriptionText = FindChildComponentByName<TMP_Text>(transform, PerkDescriptionTextName);

            if (!perkRequirementsText)
                perkRequirementsText = FindChildComponentByName<TMP_Text>(transform, PerkRequirementsTextName);

            if (!perkRanksText)
                perkRanksText = FindChildComponentByName<TMP_Text>(transform, PerkRanksTextName);

            if (!continueText)
                continueText = FindChildComponentByName<TMP_Text>(transform, ContinueTextName);

            if (!resetText)
                resetText = FindChildComponentByName<TMP_Text>(transform, ResetTextName);

            if (!selectedBox)
                selectedBox = FindChildGameObjectByName(transform, SelectedBoxName);

            if (!selectedBox)
                selectedBox = FindChildGameObjectByName(transform, SelectedBoxPersistentName);

            if (!selectedBox)
                selectedBox = FindChildGameObjectByName(transform, PlayerSelectedBoxName);

            if (!playerControls)
                playerControls = FindAnyObjectByType<PlayerControls>();

            if (!cameraRigOrbit)
                cameraRigOrbit = FindAnyObjectByType<CameraRigOrbit>();

            if (!cameraControlZoom)
                cameraControlZoom = FindAnyObjectByType<CameraControlZoom>();

            controls = playerControls && playerControls.Controls != null
                ? playerControls.Controls
                : new InputSystemActions();
            DisableGraphicRaycasts(selectedBox);
            EnsureLayoutIgnored(selectedBox);
            DisableNonInteractiveGraphicRaycasts();
            ConfigureActionTextButtons();
            SetActiveSafe(selectedBox, false);
            hasInitialized = true;
        }

        private Transform ResolvePerkContentRoot()
        {
            if (perkScroll && perkScroll.content)
                return perkScroll.content;

            return perkContentRoot;
        }

        private PerkDefinition ResolvePerkForRow(GameObject rowObject)
        {
            if (!rowObject)
                return null;

            return perkByRowObject.TryGetValue(rowObject, out PerkDefinition perk) ? perk : null;
        }

        private bool DoesRowMeetRequirements(GameObject rowObject)
        {
            return rowObject &&
                   rowMeetsRequirements.TryGetValue(rowObject, out bool meetsRequirements) &&
                   meetsRequirements;
        }

        private void RefreshSelectedBoxIndicator()
        {
            if (!selectedBox)
                return;

            if (!isOpen || !selectedRowObject || !selectedPerk || !DoesRowMeetRequirements(selectedRowObject))
            {
                SetActiveSafe(selectedBox, false);
                return;
            }

            if (!PositionSelectedBox(selectedRowObject))
            {
                SetActiveSafe(selectedBox, false);
                return;
            }

            SetActiveSafe(selectedBox, true);
        }

        private bool PositionSelectedBox(GameObject rowObject)
        {
            if (!selectedBox || !rowObject)
                return false;

            RectTransform rowRect = rowObject.transform as RectTransform;
            if (!rowRect || !(rowRect.parent is RectTransform listParentRect))
                return false;

            if (!IsRowVisibleInViewport(rowRect, listParentRect))
                return false;

            TMP_Text rowLabel = rowObject.GetComponentInChildren<TMP_Text>(true);
            RectTransform rowLabelRect = rowLabel ? rowLabel.rectTransform : null;

            if (!TryGetSelectionIndicatorWorldPosition(rowRect, rowLabelRect, listParentRect, out Vector3 worldPosition))
                return false;

            RectTransform selectedBoxRect = selectedBox.transform as RectTransform;
            if (!selectedBoxRect)
                return false;

            if (selectedBoxRect.parent != listParentRect)
                selectedBoxRect.SetParent(listParentRect, true);

            selectedBoxRect.localScale = Vector3.one;
            selectedBoxRect.position = worldPosition;
            SetRectTransformBeforeSibling(selectedBoxRect, rowRect);
            return true;
        }

        private bool IsRowVisibleInViewport(RectTransform rowRect, RectTransform listParentRect)
        {
            if (!rowRect || !listParentRect || !perkScroll)
                return true;

            RectTransform viewport = perkScroll.viewport ? perkScroll.viewport : perkScroll.transform as RectTransform;
            if (!viewport)
                return true;

            if (!TryGetRectLocalBounds(rowRect, listParentRect, out float rowMinX, out float rowMaxX, out float rowMinY, out float rowMaxY))
                return false;

            if (!TryGetRectLocalBounds(viewport, listParentRect, out float viewportMinX, out float viewportMaxX, out float viewportMinY, out float viewportMaxY))
                return true;

            const float edgeEpsilon = 0.5f;
            bool insideHorizontally = rowMinX >= viewportMinX - edgeEpsilon && rowMaxX <= viewportMaxX + edgeEpsilon;
            bool insideVertically = rowMinY >= viewportMinY - edgeEpsilon && rowMaxY <= viewportMaxY + edgeEpsilon;
            return insideHorizontally && insideVertically;
        }

        private bool TryGetSelectionIndicatorWorldPosition(
            RectTransform rowRect,
            RectTransform rowLabelRect,
            RectTransform listParentRect,
            out Vector3 worldPosition)
        {
            worldPosition = Vector3.zero;

            if (!TryGetRectLocalBounds(rowRect, listParentRect, out float rowMinX, out _, out float rowMinY, out float rowMaxY))
                return false;

            float textStartLocalX = rowMinX;
            float indicatorLocalY = (rowMinY + rowMaxY) * 0.5f;

            if (rowLabelRect &&
                TryGetRectLocalBounds(rowLabelRect, listParentRect, out float labelMinX, out _, out float labelMinY, out float labelMaxY))
            {
                textStartLocalX = labelMinX;
                indicatorLocalY = (labelMinY + labelMaxY) * 0.5f;
            }

            float indicatorLeftLocalX = rowMinX;
            if (TryGetScrollRectScrollbarEdgesLocalX(perkScroll, listParentRect, out _, out float scrollbarRightEdgeLocalX))
                indicatorLeftLocalX = scrollbarRightEdgeLocalX;

            float indicatorLocalX = (indicatorLeftLocalX + textStartLocalX) * 0.5f;
            worldPosition = listParentRect.TransformPoint(new Vector3(indicatorLocalX, indicatorLocalY, 0f));
            return true;
        }

        private void ForwardScrollToPerkScroll(BaseEventData eventData)
        {
            if (!perkScroll || !(eventData is PointerEventData pointerEventData))
                return;

            perkScroll.OnScroll(pointerEventData);
            pointerEventData.Use();
            RefreshSelectedBoxIndicator();
        }

        private void SetGameplayActionsEnabled(bool enabled)
        {
            if (controls == null)
                return;

            InputSystemActions.PlayerActions player = controls.Player;
            if (enabled)
            {
                player.Move.Enable();
                player.Look.Enable();
                player.Attack.Enable();
                player.Interact.Enable();
                player.Crouch.Enable();
                player.Jump.Enable();
                player.ToggleRun.Enable();
                player.Sprint.Enable();
                player.Holster.Enable();
                player.Block.Enable();
                player.Grab.Enable();
                player.Reload.Enable();
                player.PipBoy.Enable();
                return;
            }

            player.Move.Disable();
            player.Look.Disable();
            player.Attack.Disable();
            player.Interact.Disable();
            player.Crouch.Disable();
            player.Jump.Disable();
            player.ToggleRun.Disable();
            player.Sprint.Disable();
            player.Holster.Disable();
            player.Block.Disable();
            player.Grab.Disable();
            player.Reload.Disable();
            player.PipBoy.Disable();
        }

        private void RestoreGameplayActionsAfterKeyboardRelease()
        {
            CancelPendingGameplayInputRestore();

            MonoBehaviour coroutineHost = ResolveGameplayInputRestoreCoroutineHost();
            if (!coroutineHost || !coroutineHost.isActiveAndEnabled)
            {
                SetGameplayActionsEnabled(true);
                return;
            }

            gameplayInputRestoreCoroutineHost = coroutineHost;
            gameplayInputRestoreCoroutine = coroutineHost.StartCoroutine(RestoreGameplayActionsAfterKeyboardReleaseCoroutine());
        }

        private IEnumerator RestoreGameplayActionsAfterKeyboardReleaseCoroutine()
        {
            // Wait one frame so the close-frame keypress cannot propagate into gameplay actions.
            yield return null;

            while (IsContinueKeyPressed())
                yield return null;

            gameplayInputRestoreCoroutine = null;
            gameplayInputRestoreCoroutineHost = null;

            if (isOpen || !disableGameplayActionsWhenOpen)
                yield break;

            SetGameplayActionsEnabled(true);
        }

        private void RestoreGameplayActionsAfterLevelUpQueue()
        {
            if (isOpen || !disableGameplayActionsWhenOpen)
                return;

            CancelPendingGameplayInputRestore();
            SetGameplayActionsEnabled(true);
        }

        private void CancelPendingGameplayInputRestore()
        {
            if (gameplayInputRestoreCoroutineHost && gameplayInputRestoreCoroutine != null)
                gameplayInputRestoreCoroutineHost.StopCoroutine(gameplayInputRestoreCoroutine);

            gameplayInputRestoreCoroutine = null;
            gameplayInputRestoreCoroutineHost = null;
        }

        private MonoBehaviour ResolveGameplayInputRestoreCoroutineHost()
        {
            if (playerControls && playerControls.isActiveAndEnabled)
                return playerControls;

            return isActiveAndEnabled ? this : null;
        }

        private static bool IsContinueKeyPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.eKey != null && keyboard.eKey.isPressed;
        }

        private void PauseGameTime()
        {
            cachedTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        private void ResumeGameTime()
        {
            Time.timeScale = cachedTimeScale;
        }

        private void ApplyPipBoyPaletteColorOverrides(GameObject rootObject)
        {
            if (!rootObject)
                return;

            Graphic[] graphics = rootObject.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (!graphic || graphic.gameObject.name == RuntimeThemeBackgroundExclusionObjectName)
                    continue;

                graphic.color = RemapPipBoyPaletteColor(graphic.color);
            }

            Selectable[] selectables = rootObject.GetComponentsInChildren<Selectable>(true);
            for (int i = 0; i < selectables.Length; i++)
            {
                Selectable selectable = selectables[i];
                if (!selectable || selectable.gameObject.name == RuntimeThemeBackgroundExclusionObjectName)
                    continue;

                ColorBlock colorBlock = selectable.colors;
                colorBlock.normalColor = RemapPipBoyPaletteColor(colorBlock.normalColor);
                colorBlock.highlightedColor = RemapPipBoyPaletteColor(colorBlock.highlightedColor);
                colorBlock.pressedColor = RemapPipBoyPaletteColor(colorBlock.pressedColor);
                colorBlock.selectedColor = RemapPipBoyPaletteColor(colorBlock.selectedColor);
                colorBlock.disabledColor = RemapPipBoyPaletteColor(colorBlock.disabledColor);
                selectable.colors = colorBlock;
            }
        }

        private void DisableNonInteractiveGraphicRaycasts()
        {
            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (!graphic)
                    continue;

                graphic.raycastTarget = false;
            }
        }

        private Color RemapPipBoyPaletteColor(Color sourceColor)
        {
            if (IsApproximatelyColor(sourceColor, 1f, 1f, 1f))
                return WithMultipliedAlpha(pipBoyLightColor, sourceColor.a);

            if (IsApproximatelyColor(sourceColor, 0f, 0f, 0f))
                return WithMultipliedAlpha(pipBoyDarkColor, sourceColor.a);

            return sourceColor;
        }

        private static bool IsApproximatelyColor(Color color, float red, float green, float blue)
        {
            return Mathf.Abs(color.r - red) <= PipBoyPaletteColorTolerance &&
                   Mathf.Abs(color.g - green) <= PipBoyPaletteColorTolerance &&
                   Mathf.Abs(color.b - blue) <= PipBoyPaletteColorTolerance;
        }

        private static Color WithMultipliedAlpha(Color color, float alphaMultiplier)
        {
            return new Color(color.r, color.g, color.b, color.a * alphaMultiplier);
        }

        private static void EnsureOutlineSegments(RectTransform rowRect, Color color)
        {
            if (!rowRect)
                return;

            EnsureOutlineSegment(rowRect, "OutlineTop", color, OutlineSegmentEdge.Top);
            EnsureOutlineSegment(rowRect, "OutlineBottom", color, OutlineSegmentEdge.Bottom);
            EnsureOutlineSegment(rowRect, "OutlineLeft", color, OutlineSegmentEdge.Left);
            EnsureOutlineSegment(rowRect, "OutlineRight", color, OutlineSegmentEdge.Right);
            SetOutlineSegmentsActive(rowRect, false);
        }

        private static void EnsureOutlineSegment(
            RectTransform parent,
            string segmentName,
            Color color,
            OutlineSegmentEdge edge)
        {
            RectTransform segmentRect = parent.Find(segmentName) as RectTransform;
            if (!segmentRect)
            {
                GameObject segmentObject = new GameObject(segmentName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                segmentRect = segmentObject.GetComponent<RectTransform>();
                segmentRect.SetParent(parent, false);
            }

            segmentRect.localScale = Vector3.one;
            segmentRect.localRotation = Quaternion.identity;
            ConfigureOutlineSegmentRect(segmentRect, edge);
            segmentRect.SetAsLastSibling();

            Image image = segmentRect.GetComponent<Image>();
            if (!image)
                image = segmentRect.gameObject.AddComponent<Image>();

            image.color = color;
            image.sprite = null;
            image.type = Image.Type.Simple;
            image.preserveAspect = false;
            image.raycastTarget = false;
        }

        private static void ConfigureOutlineSegmentRect(RectTransform segmentRect, OutlineSegmentEdge edge)
        {
            if (!segmentRect)
                return;

            float thickness = PerkHoverOutlineThickness;
            float leftExtension = PerkHoverOutlineLeftExtension;
            float topInset = PerkHoverOutlineTopInset;
            float bottomInset = PerkHoverOutlineBottomInset;

            switch (edge)
            {
                case OutlineSegmentEdge.Top:
                    segmentRect.anchorMin = new Vector2(0f, 1f);
                    segmentRect.anchorMax = new Vector2(1f, 1f);
                    segmentRect.pivot = new Vector2(0f, 1f);
                    segmentRect.offsetMin = new Vector2(-leftExtension, -topInset - thickness);
                    segmentRect.offsetMax = new Vector2(0f, -topInset);
                    break;
                case OutlineSegmentEdge.Bottom:
                    segmentRect.anchorMin = new Vector2(0f, 0f);
                    segmentRect.anchorMax = new Vector2(1f, 0f);
                    segmentRect.pivot = new Vector2(0f, 0f);
                    segmentRect.offsetMin = new Vector2(-leftExtension, bottomInset);
                    segmentRect.offsetMax = new Vector2(0f, bottomInset + thickness);
                    break;
                case OutlineSegmentEdge.Left:
                    segmentRect.anchorMin = new Vector2(0f, 0f);
                    segmentRect.anchorMax = new Vector2(0f, 1f);
                    segmentRect.pivot = new Vector2(0f, 1f);
                    segmentRect.offsetMin = new Vector2(-leftExtension, bottomInset);
                    segmentRect.offsetMax = new Vector2(-leftExtension + thickness, -topInset);
                    break;
                case OutlineSegmentEdge.Right:
                    segmentRect.anchorMin = new Vector2(1f, 0f);
                    segmentRect.anchorMax = new Vector2(1f, 1f);
                    segmentRect.pivot = new Vector2(1f, 1f);
                    segmentRect.offsetMin = new Vector2(-thickness, bottomInset);
                    segmentRect.offsetMax = new Vector2(0f, -topInset);
                    break;
            }
        }

        private static void SetOutlineSegmentsActive(RectTransform rowRect, bool active)
        {
            if (!rowRect)
                return;

            SetActiveSafe(FindDirectChildGameObject(rowRect, "OutlineTop"), active);
            SetActiveSafe(FindDirectChildGameObject(rowRect, "OutlineBottom"), active);
            SetActiveSafe(FindDirectChildGameObject(rowRect, "OutlineLeft"), active);
            SetActiveSafe(FindDirectChildGameObject(rowRect, "OutlineRight"), active);
        }

        private static bool TryGetRectLocalBounds(RectTransform rect, RectTransform localSpaceRect, out float minX, out float maxX, out float minY, out float maxY)
        {
            minX = maxX = minY = maxY = 0f;
            if (!rect || !localSpaceRect)
                return false;

            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);

            Vector3 local0 = localSpaceRect.InverseTransformPoint(corners[0]);
            Vector3 local1 = localSpaceRect.InverseTransformPoint(corners[1]);
            Vector3 local2 = localSpaceRect.InverseTransformPoint(corners[2]);
            Vector3 local3 = localSpaceRect.InverseTransformPoint(corners[3]);

            minX = Mathf.Min(local0.x, local1.x, local2.x, local3.x);
            maxX = Mathf.Max(local0.x, local1.x, local2.x, local3.x);
            minY = Mathf.Min(local0.y, local1.y, local2.y, local3.y);
            maxY = Mathf.Max(local0.y, local1.y, local2.y, local3.y);
            return true;
        }

        private static bool TryGetScrollRectScrollbarEdgesLocalX(
            ScrollRect scrollRect,
            RectTransform localSpaceRect,
            out float leftEdgeLocalX,
            out float rightEdgeLocalX)
        {
            leftEdgeLocalX = 0f;
            rightEdgeLocalX = 0f;
            if (!scrollRect || !localSpaceRect)
                return false;

            Scrollbar verticalScrollbar = scrollRect.verticalScrollbar;
            if (!verticalScrollbar)
                return false;

            RectTransform scrollbarRect = verticalScrollbar.transform as RectTransform;
            if (!scrollbarRect)
                return false;

            Vector3[] scrollbarWorldCorners = new Vector3[4];
            scrollbarRect.GetWorldCorners(scrollbarWorldCorners);
            Vector3 leftEdgeWorldPoint = (scrollbarWorldCorners[0] + scrollbarWorldCorners[1]) * 0.5f;
            Vector3 rightEdgeWorldPoint = (scrollbarWorldCorners[2] + scrollbarWorldCorners[3]) * 0.5f;
            leftEdgeLocalX = localSpaceRect.InverseTransformPoint(leftEdgeWorldPoint).x;
            rightEdgeLocalX = localSpaceRect.InverseTransformPoint(rightEdgeWorldPoint).x;
            return true;
        }

        private static void SetRectTransformBeforeSibling(RectTransform movingRectTransform, RectTransform targetRectTransform)
        {
            if (!movingRectTransform || !targetRectTransform)
                return;

            if (movingRectTransform.parent != targetRectTransform.parent)
                return;

            int targetSiblingIndex = targetRectTransform.GetSiblingIndex();
            int movingSiblingIndex = movingRectTransform.GetSiblingIndex();

            if (movingSiblingIndex < targetSiblingIndex)
                targetSiblingIndex = Mathf.Max(0, targetSiblingIndex - 1);

            movingRectTransform.SetSiblingIndex(targetSiblingIndex);
        }

        private static void EnsureLayoutIgnored(GameObject targetObject)
        {
            if (!targetObject)
                return;

            LayoutElement layoutElement = targetObject.GetComponent<LayoutElement>();
            if (!layoutElement)
                layoutElement = targetObject.AddComponent<LayoutElement>();

            layoutElement.ignoreLayout = true;
        }

        private static void DisableGraphicRaycasts(GameObject targetObject)
        {
            if (!targetObject)
                return;

            Graphic[] graphics = targetObject.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i])
                    graphics[i].raycastTarget = false;
            }
        }

        private static T FindChildComponentByName<T>(Transform root, string childName) where T : Component
        {
            if (!root || string.IsNullOrWhiteSpace(childName))
                return null;

            T[] components = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] && components[i].name == childName)
                    return components[i];
            }

            return null;
        }

        private static Transform FindChildTransformByName(Transform root, string childName)
        {
            if (!root || string.IsNullOrWhiteSpace(childName))
                return null;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] && transforms[i].name == childName)
                    return transforms[i];
            }

            return null;
        }

        private static GameObject FindChildGameObjectByName(Transform root, string childName)
        {
            Transform child = FindChildTransformByName(root, childName);
            return child ? child.gameObject : null;
        }

        private static GameObject FindDirectChildGameObject(Transform root, string childName)
        {
            if (!root)
                return null;

            Transform child = root.Find(childName);
            return child ? child.gameObject : null;
        }

        private enum OutlineSegmentEdge
        {
            Top,
            Bottom,
            Left,
            Right
        }

        private static LevelUpUIController FindOrCreateActiveController()
        {
            Transform levelUpRoot = FindTransformByName(LevelUpRootName);
            if (!levelUpRoot)
                return null;

            SetHierarchyActive(levelUpRoot, true);

            LevelUpUIController controller = levelUpRoot.GetComponent<LevelUpUIController>();
            if (!controller)
                controller = levelUpRoot.gameObject.AddComponent<LevelUpUIController>();

            controller.EnsureInitialized();
            return controller;
        }

        private static LevelUpUIController FindFirstInSceneIncludingInactive()
        {
            LevelUpUIController[] controllers = Resources.FindObjectsOfTypeAll<LevelUpUIController>();
            for (int i = 0; i < controllers.Length; i++)
            {
                LevelUpUIController controller = controllers[i];
                if (!controller)
                    continue;

                GameObject controllerObject = controller.gameObject;
                if (!controllerObject || !controllerObject.scene.IsValid() || !controllerObject.scene.isLoaded)
                    continue;

                return controller;
            }

            return null;
        }

        private static Transform FindTransformByName(string objectName)
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] && transforms[i].name == objectName)
                    return transforms[i];
            }

            return null;
        }

        private static void SetHierarchyActive(Transform root, bool active)
        {
            if (!root)
                return;

            if (!active)
            {
                root.gameObject.SetActive(false);
                return;
            }

            Transform current = root;
            while (current)
            {
                GameObject currentObject = current.gameObject;
                if (!currentObject.activeSelf)
                    currentObject.SetActive(true);

                current = current.parent;
            }
        }

        private static void SetActiveSafe(GameObject targetObject, bool active)
        {
            if (!targetObject || targetObject.activeSelf == active)
                return;

            targetObject.SetActive(active);
        }

        private static void SetTextIfChanged(TMP_Text textComponent, string value)
        {
            if (!textComponent)
                return;

            string nextValue = value ?? string.Empty;
            if (textComponent.text == nextValue)
                return;

            textComponent.text = nextValue;
        }

        private readonly struct LevelUpRequest
        {
            public readonly PlayerState PlayerState;
            public readonly int Level;

            public LevelUpRequest(PlayerState playerState, int level)
            {
                PlayerState = playerState;
                Level = level;
            }
        }
    }
}
