using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using UnityEngine.UI;



public class TerminalController : MonoBehaviour
{
    private const string DefaultContentRootName = "ContentContainer";
    private const string HackingContentRootName = "HackingContentContainer";
    private const string HackingRuntimeContentRootName = "HackingRuntimeContent";
    private const string RuntimeContentRootName = "TerminalRuntimeContent";
    private const string FrameObjectName = "Frame";
    private const string HackingFrameObjectName = "HackingFrame";
    private const string OperatingTextName = "OperatingText";
    private const string PromptMessageTextName = "PromptMessageText";
    private const string AttemptsTextName = "AttemptsText";
    private const string AttemptBoxNamePrefix = "AttemptBox";
    private const string AnswerTextName = "AnswerText";
    private const string HardwareEntryTextName = "HardwareEntryText";
    private const string BreakTextName = "BreakText";
    private const string BootToolTextName = "BootToolText";
    private const string HardwareLockTextName = "HardwareLockText";
    private const string EnterPasswordTextName = "EnterPasswordText";
    private const string ArrowTextName = "ArrowText";
    private const string HackingMessagesRootName = "HackingMessages";
    private const string HackingMessageArrowTextName = "HackingMessageArrowText";
    private const string HackingMessageTextName = "HackingMessageText";
    private const string CursorImageName = "Cursor";
    private const string OptionHoverRectangleName = "HoverRectangle";
    private const string BodyOptionsSpacerName = "BodyOptionsSpacer";
    private const float InteractCloseReopenCooldownSeconds = 0.15f;
    private const float HoveredOptionRectangleExtraHeight = 4.0f;
    private const float HackingIntroPostHardwareLockDelaySeconds = 1.0f;
    private const int HackingColumnCount = 4;
    private const int HackingMemoryAddressColumnStride = 0x0C;
    private const int HackingMinimumSymbolsBetweenWords = 2;
    private const int HackingSpecialSequenceMinLength = 3;
    private const string HackingNonBracketSpecialCharacters = "!@#$%^&*-_=+;:'\",./?\\|`~";
    private const string HackingLockoutImminentText = "Warning: Lockout Imminent";

    private static float lastInteractCloseUnscaledTime = float.NegativeInfinity;

    private struct RenderedTerminalOption
    {
        public string Label;
        public TerminalOption Option;
        public TerminalOptionAction Action;
        public string TargetPageId;
        public bool AddCurrentPageToHistory;
    }

    private sealed class TextWriteAnimation
    {
        public TMP_Text Text;
        public int TotalVisibleCharacters;
        public float CharacterAccumulator;
        public float CharactersPerSecond;
        public Action OnCompleted;
    }

    private sealed class HackingEntry
    {
        public string DisplayText;
        public string AnswerText;
        public string Word;
        public bool IsWord;
        public int WordStartIndex = -1;
        public int[] WordIds;
        public int[] SpecialIds;
        public readonly List<HackingTarget> Targets = new List<HackingTarget>();
    }

    private sealed class HackingTarget
    {
        public int TargetIndex;
        public int EntryIndex;
        public int StartIndex;
        public int Length;
        public int WordId = -1;
        public int SpecialId = -1;
        public string Text;
        public string AnswerText;
        public bool IsWord;
        public bool IsSpecialSequence;
        public bool IsPassword;
        public bool HasBeenSelected;
        public readonly List<HackingTargetPart> Parts = new List<HackingTargetPart>();
        public readonly List<HackingTargetPart> HighlightParts = new List<HackingTargetPart>();
    }

    private sealed class HackingTargetPart
    {
        public int EntryIndex;
        public int StartIndex;
        public int Length;
        public string Text;
        public Button Button;
        public Image HoverRectangle;
        public TMP_Text Label;
    }

    private sealed class PlacedHackingWord
    {
        public string Word;
        public bool IsPassword;
    }

    private sealed class HackingSpecialSequence
    {
        public string Text;
    }

    private sealed class HackingGenerationHistory
    {
        public string Password = string.Empty;
        public readonly HashSet<string> Words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    [Header("Terminal Data")]
    [SerializeField] private TerminalDefinition defaultTerminalDefinition;

    [Header("UI Roots")]
    [SerializeField] private CanvasGroup terminalCanvasGroup;
    [SerializeField] private GameObject frameRoot;
    [SerializeField] private RectTransform contentRoot;
    [SerializeField] private RectTransform runtimeContentRoot;
    [SerializeField] private Button optionButtonPrefab;
    [SerializeField] private GameObject firstSelectedUIObject;

    [Header("Generated Layout")]
    [SerializeField] private bool createContentRootIfMissing = true;
    [SerializeField] private Vector2 generatedContentPadding = new Vector2(52.0f, 48.0f);
    [SerializeField] private float generatedElementSpacing = 12.0f;
    [SerializeField] private float optionHeight = 28.0f;
    [SerializeField, Min(1.0f)] private float titleUnderlineHeight = 1.0f;

    [Header("Generated Text")]
    [SerializeField] private TMP_FontAsset terminalFont;
    [SerializeField] private Color terminalTextColor = new Color32(0x4E, 0xFF, 0x61, 0xFF);
    [SerializeField] private Color selectedOptionTextColor = Color.white;
    [SerializeField] private Color hoveredOptionRectangleColor = new Color32(0x4E, 0xFF, 0x61, 0x80);
    [SerializeField] private Color hoveredOptionTextColor = Color.black;
    [SerializeField] private float titleFontSize = 22.0f;
    [SerializeField] private float bodyFontSize = 18.0f;
    [SerializeField] private float optionFontSize = 18.0f;

    [Header("Text Write Animation")]
    [SerializeField] private bool animateTextOnAppear = true;
    [SerializeField, Min(1.0f)] private float textWriteCharactersPerSecond = 70.0f;
    [SerializeField, Min(1.0f)] private float bodyTextWriteCharactersPerSecond = 70.0f;
    [SerializeField, Min(1.0f)] private float frameTextWriteCharactersPerSecond = 70.0f;

    [Header("Prompt Message")]
    [SerializeField] private TMP_Text promptMessageText;
    [SerializeField, Min(0.0f)] private float promptMessageDurationSeconds = 2.0f;

    [Header("Terminal Cursor")]
    [SerializeField] private Image terminalCursorImage;
    [SerializeField, Min(0.01f)] private float terminalCursorBlinkIntervalSeconds = 0.5f;

    [Header("Hacking")]
    [SerializeField] private RectTransform hackingContentRoot;
    [SerializeField] private RectTransform hackingRuntimeContentRoot;
    [SerializeField] private GameObject hackingFrameRoot;
    [SerializeField] private TMP_Text hardwareEntryText;
    [SerializeField] private TMP_Text breakText;
    [SerializeField] private TMP_Text bootToolText;
    [SerializeField] private TMP_Text hardwareLockText;
    [SerializeField] private TMP_Text attemptsText;
    [SerializeField] private TMP_Text answerText;
    [SerializeField] private TMP_Text enterPasswordText;
    [SerializeField] private TMP_Text arrowText;
    [SerializeField] private RectTransform hackingMessagesRoot;
    [SerializeField] private TMP_Text hackingMessageArrowText;
    [SerializeField] private TMP_Text hackingMessageText;
    [SerializeField] private Image hackingCursorImage;
    [SerializeField] private GameObject[] attemptBoxes = new GameObject[4];
    [SerializeField] private bool enforceHackingScienceRequirement = true;
    [SerializeField, Min(1)] private int hackingAttempts = 4;
    [SerializeField, Min(4)] private int hackingRowsPerSelectableColumn = 12;
    [SerializeField, Min(4)] private int hackingCandidateWordCount = 10;
    [SerializeField, Min(0)] private int hackingSpecialSequenceMinCount = 1;
    [SerializeField, Min(0)] private int hackingSpecialSequenceMaxCount = 6;
    [SerializeField, Min(1.0f)] private float hackingRowHeight = 24.0f;
    [SerializeField, Min(0.0f)] private float hackingColumnSpacing = 10.0f;
    [SerializeField, Min(0.0f)] private float hackingRowSpacing = 0.0f;
    [SerializeField, Min(1.0f)] private float hackingFontSize = 18.0f;
    [FormerlySerializedAs("hackingMessageMaxLines")]
    [SerializeField, Min(1)] private int hackingLogLineCount = 12;
    [SerializeField] private float hackingLowPunctuationTextOffsetY = -5.0f;
    [SerializeField] private Color hackingHoveredEntryRectangleColor = new Color32(0x4E, 0xFF, 0x61, 0x80);
    [SerializeField] private Color hackingHoveredEntryTextColor = Color.black;
    [SerializeField] private Color hackingSelectedEntryTextColor = Color.white;
    [SerializeField] private Vector2 hackingCursorOffset = new Vector2(1.0f, -1.0f);
    [SerializeField, Min(1.0f)] private float hackingIntroWriteCharactersPerSecond = 70.0f;

    [Header("Open Behavior")]
    [SerializeField] private bool disableInHierarchyWhenClosed = true;
    [SerializeField] private bool disableGameplayActionsWhenOpen = true;
    [SerializeField] private bool pauseGameWhenOpen = true;
    [SerializeField] private bool hidePlayerHudWhenOpen = true;
    [SerializeField] private bool closeOnCancel = true;
    [SerializeField] private PlayerControls playerControls;
    [SerializeField] private CameraRigOrbit cameraRigOrbit;
    [SerializeField] private CameraControlZoom cameraControlZoom;
    [SerializeField] private CanvasGroup playerHudCanvasGroup;

    private Terminal activeTerminal;
    private TerminalDocument activeDocument;
    private TerminalPage activePage;
    private GameObject activeInteractor;
    private InputSystemActions controls;

    private bool hasInitialized;
    private bool isOpen;
    private bool hasCachedTimeScale;
    private float cachedTimeScale = 1.0f;
    private int ignoreInputUntilFrame = -1;
    private bool hasCachedPlayerHudState;
    private float cachedPlayerHudAlpha = 1.0f;
    private bool cachedPlayerHudInteractable;
    private bool cachedPlayerHudBlocksRaycasts;
    private bool promptMessageVisible;
    private bool hasWarnedAboutZeroTerminalScale;
    private bool hasCachedTerminalCursorColor;
    private bool hasCachedHackingCursorColor;
    private bool hasCachedHackingCursorAnchoredPosition;
    private Color cachedTerminalCursorColor = Color.white;
    private Color cachedHackingCursorColor = Color.white;
    private Vector2 cachedHackingCursorAnchoredPosition;
    private float promptMessageHideUnscaledTime;
    private TextWriteAnimation activeTextWriteAnimation;
    private Coroutine hackingOutcomeCoroutine;
    private int lastTextWriteAnimationCompletedFrame = -1;

    private readonly List<string> pageHistory = new List<string>();
    private readonly List<Button> spawnedOptionButtons = new List<Button>();
    private readonly List<Image> spawnedOptionHoverRectangles = new List<Image>();
    private readonly List<TMP_Text> spawnedOptionLabels = new List<TMP_Text>();
    private readonly List<bool> spawnedOptionInteractionReady = new List<bool>();
    private readonly List<RenderedTerminalOption> renderedOptions = new List<RenderedTerminalOption>();
    private readonly Queue<TextWriteAnimation> pendingTextWriteAnimations = new Queue<TextWriteAnimation>();
    private readonly HashSet<string> animatedPersistentTextKeys = new HashSet<string>();
    private int hoveredOptionIndex = -1;
    private int selectedOptionIndex = -1;
    private int lastOptionSelectFrame = -1;
    private int bodyPageStartIndex;
    private int nextBodyPageStartIndex;
    private int bodyContinuationInputAllowedFrame = -1;
    private bool isAwaitingBodyContinuationClick;
    private bool isHackingActive;
    private bool isHackingLockedOut;
    private TMP_Text bodyMeasurementText;
    private int hackingAttemptsRemaining;
    private int selectedHackingTargetIndex = -1;
    private int hoveredHackingTargetIndex = -1;
    private string hackingPassword = string.Empty;
    private bool hasCachedEnterPasswordPromptText;
    private string defaultEnterPasswordPromptText = "Enter Password";
    private TextAlignmentOptions defaultEnterPasswordPromptAlignment = TextAlignmentOptions.TopLeft;
    private int hackingIntroSequenceToken;
    private bool isHackingIntroSequenceActive;
    private readonly List<GameObject> hackingIntroTemporarilyHiddenObjects = new List<GameObject>();
    private readonly List<HackingEntry> hackingEntries = new List<HackingEntry>();
    private readonly List<HackingTarget> hackingTargets = new List<HackingTarget>();
    private readonly List<PlacedHackingWord> placedHackingWords = new List<PlacedHackingWord>();
    private readonly List<HackingSpecialSequence> placedHackingSpecialSequences = new List<HackingSpecialSequence>();
    private readonly Dictionary<int, HackingTarget> hackingWordTargetsByWordId = new Dictionary<int, HackingTarget>();
    private readonly Dictionary<int, HackingTarget> hackingSpecialTargetsById = new Dictionary<int, HackingTarget>();
    private readonly List<string> hackingMessageLines = new List<string>();
    private readonly Dictionary<Terminal, HackingGenerationHistory> hackingHistoryByTerminal = new Dictionary<Terminal, HackingGenerationHistory>();



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
        if (isOpen && !gameObject.activeInHierarchy)
            CloseWithoutInputCooldown();
    }


    private void OnDestroy()
    {
        if (bodyMeasurementText)
            Destroy(bodyMeasurementText.gameObject);
    }


    private void Update()
    {
        if (!isOpen)
            return;

        UpdateTextWriteAnimations();
        UpdatePromptMessageVisibility();
        UpdateTerminalCursorBlink();

        if (isHackingActive)
        {
            HandleHackingKeyboardInput();
            return;
        }

        HandleBodyPaginationInput();
        HandleKeyboardInput();
    }


    public bool IsOpen()
    {
        return isOpen;
    }


    public Terminal GetCurrentTerminal()
    {
        return activeTerminal;
    }


    public GameObject GetCurrentInteractor()
    {
        return activeInteractor;
    }


    public void OpenDefaultTerminal()
    {
        Open(defaultTerminalDefinition, null, string.Empty);
    }


    public void Open(TerminalDefinition definition)
    {
        Open(definition, null, string.Empty);
    }


    public void Open(TerminalDefinition definition, GameObject interactor, string fallbackTerminalName = "")
    {
        TerminalDocument document = definition ? definition.GetDocument() : null;
        Open(document, interactor, fallbackTerminalName);
    }


    public void Open(TerminalDocument document)
    {
        Open(document, null, string.Empty);
    }


    public void Open(TerminalDocument document, GameObject interactor, string fallbackTerminalName = "")
    {
        EnsureInitialized();

        isHackingActive = false;
        activeTerminal = null;
        activeInteractor = interactor;
        activeDocument = document ?? CreateFallbackDocument(fallbackTerminalName);
        pageHistory.Clear();
        ResetTextWriteAnimationState(true);

        SetOpenState(true, false);
        ShowStartupPage();
    }


    public void OpenForTerminal(Terminal terminal, GameObject interactor)
    {
        EnsureInitialized();

        isHackingActive = false;
        activeTerminal = terminal;
        activeInteractor = interactor;
        activeDocument = terminal && terminal.GetTerminalDefinition()
            ? terminal.GetTerminalDefinition().GetDocument()
            : CreateFallbackDocument(terminal ? terminal.GetTerminalName() : string.Empty);
        pageHistory.Clear();
        ResetTextWriteAnimationState(true);

        SetOpenState(true, false);
        ShowStartupPage();
    }


    public bool OpenHackingForTerminal(Terminal terminal, GameObject interactor)
    {
        EnsureInitialized();

        if (!terminal || !terminal.IsLocked() || terminal.GetLockType() == Terminal.LockType.Password)
            return false;

        int requiredScience = terminal.GetRequiredScienceSkill();
        int interactorScience = GetInteractorScience(interactor);
        if (enforceHackingScienceRequirement && interactorScience < requiredScience)
            return false;

        activeTerminal = terminal;
        activeInteractor = interactor;
        activeDocument = terminal.GetTerminalDefinition()
            ? terminal.GetTerminalDefinition().GetDocument()
            : CreateFallbackDocument(terminal.GetTerminalName());
        pageHistory.Clear();
        ResetTextWriteAnimationState(true);

        SetOpenState(true, false);
        BeginHacking(terminal.GetLockType());
        return true;
    }


    public bool ShouldEnforceHackingScienceRequirement()
    {
        return enforceHackingScienceRequirement;
    }


    public void Close()
    {
        lastInteractCloseUnscaledTime = Time.unscaledTime;
        CloseWithoutInputCooldown();
    }


    public static bool IsInteractCloseCooldownActive()
    {
        return Time.unscaledTime - lastInteractCloseUnscaledTime <= InteractCloseReopenCooldownSeconds;
    }


    public static TerminalController FindFirstInSceneIncludingInactive()
    {
        TerminalController[] candidates = Resources.FindObjectsOfTypeAll<TerminalController>();

        for (int i = 0; i < candidates.Length; i++)
        {
            TerminalController candidate = candidates[i];
            if (!candidate)
                continue;

            GameObject candidateObject = candidate.gameObject;
            if (!candidateObject || !candidateObject.scene.IsValid())
                continue;

            return candidate;
        }

        return null;
    }


    private void EnsureInitialized()
    {
        if (hasInitialized)
            return;

        if (!terminalCanvasGroup)
            terminalCanvasGroup = GetComponent<CanvasGroup>();

        if (!terminalCanvasGroup)
            terminalCanvasGroup = gameObject.AddComponent<CanvasGroup>();

        if (!contentRoot)
            contentRoot = FindChildRectTransformByName(DefaultContentRootName);

        if (!contentRoot && createContentRootIfMissing)
            contentRoot = CreateGeneratedContentRootHost();

        if (!runtimeContentRoot)
            runtimeContentRoot = FindDirectChildRectTransform(contentRoot, RuntimeContentRootName);

        if (!runtimeContentRoot && contentRoot)
            runtimeContentRoot = CreateRuntimeContentRoot(contentRoot);

        ResolveTerminalFrameRoot();
        EnsureRuntimeContentClipping();

        if (!playerControls)
            playerControls = FindAnyObjectByType<PlayerControls>();

        controls = playerControls ? playerControls.Controls : null;

        if (!cameraRigOrbit)
            cameraRigOrbit = FindAnyObjectByType<CameraRigOrbit>();

        if (!cameraControlZoom)
            cameraControlZoom = FindAnyObjectByType<CameraControlZoom>();

        if (!playerHudCanvasGroup)
            playerHudCanvasGroup = FindPlayerHudCanvasGroup();

        if (!promptMessageText)
            promptMessageText = FindChildComponentByName<TMP_Text>(PromptMessageTextName);

        if (!terminalCursorImage)
            terminalCursorImage = FindChildComponentByName<Image>(CursorImageName);

        ResolveHackingUiReferences();
        ConfigurePromptMessageText();
        ConfigureTerminalCursorImage();
        ConfigureHackingCursorImage();
        HidePromptMessageImmediate();
        SetHackingUiVisible(false);
        ConfigureRuntimeContentLayout();
        hasInitialized = true;
    }


    private void SetOpenState(bool open, bool forceWithoutSideEffects)
    {
        bool wasOpen = isOpen;

        if (!forceWithoutSideEffects && !wasOpen && open && !gameObject.activeSelf)
            gameObject.SetActive(true);

        isOpen = open;

        if (isOpen)
            SetTerminalHierarchyActive(true);

        if (terminalCanvasGroup)
        {
            terminalCanvasGroup.alpha = isOpen ? 1.0f : 0.0f;
            terminalCanvasGroup.interactable = isOpen;
            terminalCanvasGroup.blocksRaycasts = isOpen;
        }

        UpdateTerminalCursorBlink(true);

        if (forceWithoutSideEffects || wasOpen == isOpen)
        {
            if (!isOpen && disableInHierarchyWhenClosed)
                SetTerminalHierarchyActive(false);

            return;
        }

        if (isOpen)
        {
            ignoreInputUntilFrame = Time.frameCount;
            HidePromptMessageImmediate();
            RegisterFrameTextWriteAnimations();

            if (pauseGameWhenOpen && !hasCachedTimeScale)
            {
                cachedTimeScale = Time.timeScale;
                hasCachedTimeScale = true;
                Time.timeScale = 0.0f;
            }

            if (disableGameplayActionsWhenOpen)
                SetGameplayActionsEnabled(false);

            controls?.UI.Enable();

            if (cameraRigOrbit)
                cameraRigOrbit.SetInputEnabled(false);

            if (cameraControlZoom)
                cameraControlZoom.SetInputEnabled(false);

            if (hidePlayerHudWhenOpen)
                SetPlayerHudVisible(false);

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            SelectFirstOption();
            return;
        }

        HidePromptMessageImmediate();
        CancelHackingIntroSequence();
        ResetTextWriteAnimationState(true);
        StopHackingOutcomeDelay();
        isHackingActive = false;
        ClearHackingContent();
        SetHackingUiVisible(false);

        if (hidePlayerHudWhenOpen)
            SetPlayerHudVisible(true);

        if (cameraRigOrbit)
            cameraRigOrbit.SetInputEnabled(true);
        else
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        if (cameraControlZoom)
            cameraControlZoom.SetInputEnabled(true);

        if (disableGameplayActionsWhenOpen)
            SetGameplayActionsEnabled(true);

        controls?.UI.Disable();

        if (pauseGameWhenOpen && hasCachedTimeScale)
        {
            Time.timeScale = cachedTimeScale;
            hasCachedTimeScale = false;
        }

        activeTerminal = null;
        activeInteractor = null;

        if (disableInHierarchyWhenClosed)
            SetTerminalHierarchyActive(false);
    }


    private void CloseWithoutInputCooldown()
    {
        SetOpenState(false, false);
    }


    private void SetPlayerHudVisible(bool visible)
    {
        if (!playerHudCanvasGroup)
            playerHudCanvasGroup = FindPlayerHudCanvasGroup();

        if (!playerHudCanvasGroup)
            return;

        if (!visible)
        {
            if (!hasCachedPlayerHudState)
            {
                cachedPlayerHudAlpha = playerHudCanvasGroup.alpha;
                cachedPlayerHudInteractable = playerHudCanvasGroup.interactable;
                cachedPlayerHudBlocksRaycasts = playerHudCanvasGroup.blocksRaycasts;
                hasCachedPlayerHudState = true;
            }

            playerHudCanvasGroup.alpha = 0.0f;
            playerHudCanvasGroup.interactable = false;
            playerHudCanvasGroup.blocksRaycasts = false;
            return;
        }

        if (!hasCachedPlayerHudState)
            return;

        playerHudCanvasGroup.alpha = cachedPlayerHudAlpha;
        playerHudCanvasGroup.interactable = cachedPlayerHudInteractable;
        playerHudCanvasGroup.blocksRaycasts = cachedPlayerHudBlocksRaycasts;
        hasCachedPlayerHudState = false;
    }


    private void ShowStartupPage()
    {
        isHackingActive = false;
        ClearHackingContent();
        SetHackingUiVisible(false);

        TerminalPage startupPage = activeDocument != null ? activeDocument.GetStartupPage() : null;
        ShowPage(startupPage, false);
    }


    private void ShowPage(TerminalPage page, bool addCurrentPageToHistory)
    {
        if (page == null)
        {
            RenderMissingPage("Terminal data has no pages.");
            return;
        }

        if (addCurrentPageToHistory && activePage != null && !string.IsNullOrWhiteSpace(activePage.pageId))
            pageHistory.Add(activePage.pageId);

        activePage = page;
        bodyPageStartIndex = 0;
        nextBodyPageStartIndex = 0;
        bodyContinuationInputAllowedFrame = -1;
        isAwaitingBodyContinuationClick = false;
        RebuildPage();
    }


    private void RebuildPage()
    {
        EnsureInitialized();
        ClearRuntimeContent();
        renderedOptions.Clear();
        hoveredOptionIndex = -1;
        selectedOptionIndex = -1;
        isAwaitingBodyContinuationClick = false;

        if (!runtimeContentRoot)
            return;

        string title = activeDocument != null ? activeDocument.terminalTitle : string.Empty;
        string trimmedTitle = string.IsNullOrWhiteSpace(title) ? string.Empty : title.Trim();
        string body = activePage != null && !string.IsNullOrWhiteSpace(activePage.body) ? activePage.body.Trim() : string.Empty;
        bool hasBody = !string.IsNullOrWhiteSpace(body);
        float contentTextWidth = GetRuntimeContentTextWidth();
        float availableBodyOnlyHeight = GetAvailableBodyOnlyHeight(trimmedTitle, contentTextWidth);

        BuildRenderedOptions();

        if (!string.IsNullOrWhiteSpace(title))
        {
            TMP_Text titleText = CreateTextElement(
                "TerminalTitleText",
                trimmedTitle,
                titleFontSize,
                FontStyles.UpperCase,
                -1.0f,
                GetPersistentTextAnimationKey("Title", trimmedTitle));
            CreateTitleUnderline(titleText);
        }

        string visibleBody = string.Empty;
        float visibleBodyMaxHeight = -1.0f;
        bool shouldShowOptions = true;

        if (hasBody)
        {
            bodyPageStartIndex = Mathf.Clamp(bodyPageStartIndex, 0, body.Length);
            string remainingBody = bodyPageStartIndex < body.Length ? body.Substring(bodyPageStartIndex) : string.Empty;
            float availableFinalBodyHeight = GetAvailableFinalBodyHeight(trimmedTitle, contentTextWidth, renderedOptions.Count);

            if (BodyAndOptionsFitOnCurrentPage(remainingBody, contentTextWidth, availableFinalBodyHeight))
            {
                visibleBody = remainingBody;
                visibleBodyMaxHeight = renderedOptions.Count > 0 ? availableFinalBodyHeight : availableBodyOnlyHeight;
                nextBodyPageStartIndex = body.Length;
                bodyContinuationInputAllowedFrame = -1;
                isAwaitingBodyContinuationClick = false;
            }
            else
            {
                int pageEndIndex = FindBodyPageEndIndex(body, bodyPageStartIndex, availableBodyOnlyHeight, contentTextWidth);
                visibleBody = body.Substring(bodyPageStartIndex, pageEndIndex - bodyPageStartIndex).Trim();
                visibleBodyMaxHeight = availableBodyOnlyHeight;
                nextBodyPageStartIndex = SkipLeadingWhitespace(body, pageEndIndex);
                bodyContinuationInputAllowedFrame = Time.frameCount;
                isAwaitingBodyContinuationClick = true;
                shouldShowOptions = false;
            }
        }

        if (!string.IsNullOrWhiteSpace(visibleBody))
        {
            CreateTextElement(
                "TerminalBodyText",
                visibleBody,
                bodyFontSize,
                FontStyles.Normal,
                visibleBodyMaxHeight,
                "",
                true);
        }

        if (!shouldShowOptions)
        {
            selectedOptionIndex = -1;
            return;
        }

        if (hasBody && !string.IsNullOrWhiteSpace(visibleBody) && renderedOptions.Count > 0)
            CreateBodyOptionsSpacer();

        CreateOptionButtons();
        SelectFirstOption();
    }


    private void RenderMissingPage(string message)
    {
        EnsureInitialized();
        ClearRuntimeContent();
        renderedOptions.Clear();
        hoveredOptionIndex = -1;
        bodyPageStartIndex = 0;
        nextBodyPageStartIndex = 0;
        bodyContinuationInputAllowedFrame = -1;
        isAwaitingBodyContinuationClick = false;

        CreateTextElement("TerminalErrorText", message, bodyFontSize, FontStyles.Normal, -1.0f, "", true);
        renderedOptions.Add(new RenderedTerminalOption
        {
            Label = "> Log Off",
            Action = TerminalOptionAction.Close
        });
        CreateOptionButtons();
        SelectFirstOption();
    }


    private void BeginHacking(Terminal.LockType lockType)
    {
        EnsureInitialized();
        StopHackingOutcomeDelay();
        CancelHackingIntroSequence();
        ResetTextWriteAnimationState(true);
        ClearRuntimeContent();
        renderedOptions.Clear();
        hoveredOptionIndex = -1;
        selectedOptionIndex = -1;
        isAwaitingBodyContinuationClick = false;

        isHackingActive = true;
        isHackingLockedOut = false;
        hackingAttemptsRemaining = Mathf.Clamp(hackingAttempts, 1, 4);
        hoveredHackingTargetIndex = -1;
        selectedHackingTargetIndex = -1;
        hackingPassword = string.Empty;
        ClearHackingMessages();

        SetHackingUiVisible(true);
        BuildHackingEntries(lockType);
        RebuildHackingContent();
        RefreshHackingAttemptsUi();
        SetHackingAnswerText(string.Empty);
        UpdateHackingEntryVisuals();
        BeginHackingIntroSequence();
    }


    private void ResolveHackingUiReferences()
    {
        if (!hackingContentRoot)
            hackingContentRoot = FindChildComponentByName<RectTransform>(HackingContentRootName);

        if (!hackingFrameRoot)
        {
            RectTransform hackingFrameTransform = FindChildComponentByName<RectTransform>(HackingFrameObjectName);
            hackingFrameRoot = hackingFrameTransform ? hackingFrameTransform.gameObject : null;
        }

        if (!hardwareEntryText && hackingFrameRoot)
            hardwareEntryText = FindChildComponentByNameInRoot<TMP_Text>(HardwareEntryTextName, hackingFrameRoot.transform);

        if (!breakText && hackingFrameRoot)
            breakText = FindChildComponentByNameInRoot<TMP_Text>(BreakTextName, hackingFrameRoot.transform);

        if (!bootToolText && hackingFrameRoot)
            bootToolText = FindChildComponentByNameInRoot<TMP_Text>(BootToolTextName, hackingFrameRoot.transform);

        if (!hardwareLockText && hackingFrameRoot)
            hardwareLockText = FindChildComponentByNameInRoot<TMP_Text>(HardwareLockTextName, hackingFrameRoot.transform);

        if (!attemptsText)
            attemptsText = FindChildComponentByName<TMP_Text>(AttemptsTextName);

        if (!answerText)
            answerText = FindChildComponentByName<TMP_Text>(AnswerTextName);

        if (!enterPasswordText)
            enterPasswordText = FindChildComponentByName<TMP_Text>(EnterPasswordTextName);

        if (enterPasswordText && !hasCachedEnterPasswordPromptText)
        {
            if (!string.IsNullOrWhiteSpace(enterPasswordText.text))
                defaultEnterPasswordPromptText = enterPasswordText.text;

            defaultEnterPasswordPromptAlignment = enterPasswordText.alignment;
            hasCachedEnterPasswordPromptText = true;
        }

        if (!arrowText)
            arrowText = FindChildComponentByName<TMP_Text>(ArrowTextName);

        if (!hackingMessagesRoot && hackingFrameRoot)
            hackingMessagesRoot = FindDirectChildRectTransform(hackingFrameRoot.transform, HackingMessagesRootName);

        if (!hackingMessageArrowText && hackingFrameRoot)
            hackingMessageArrowText = FindChildComponentByNameInRoot<TMP_Text>(HackingMessageArrowTextName, hackingFrameRoot.transform);

        if (!hackingMessageText && hackingFrameRoot)
            hackingMessageText = FindChildComponentByNameInRoot<TMP_Text>(HackingMessageTextName, hackingFrameRoot.transform);

        if (!hackingCursorImage && hackingFrameRoot)
            hackingCursorImage = FindChildComponentByNameInRoot<Image>(CursorImageName, hackingFrameRoot.transform);

        if (attemptBoxes == null || attemptBoxes.Length != 4)
            attemptBoxes = new GameObject[4];

        for (int i = 0; i < attemptBoxes.Length; i++)
        {
            if (attemptBoxes[i])
                continue;

            attemptBoxes[i] = FindChildGameObjectByName(AttemptBoxNamePrefix + (i + 1));
        }
    }


    private void SetHackingUiVisible(bool visible)
    {
        ResolveHackingUiReferences();
        ResolveTerminalFrameRoot();

        if (contentRoot && contentRoot != hackingContentRoot)
            contentRoot.gameObject.SetActive(isOpen && !visible);

        if (frameRoot)
            frameRoot.SetActive(isOpen && !visible);

        if (hackingContentRoot)
            hackingContentRoot.gameObject.SetActive(isOpen && visible);

        if (hackingFrameRoot)
            hackingFrameRoot.SetActive(isOpen && visible);

        if (!visible)
        {
            CancelHackingIntroSequence();
            SetHackingAnswerText(string.Empty);
            ClearHackingMessages();
        }
        else if (!isHackingIntroSequenceActive)
        {
            RefreshHackingMessagesUi();
            UpdateHackingCursorPosition();
        }
    }


    private void BeginHackingIntroSequence()
    {
        ResolveHackingUiReferences();
        CancelHackingIntroSequence();

        if (!isHackingActive)
            return;

        isHackingIntroSequenceActive = true;
        hackingIntroSequenceToken++;
        int sequenceToken = hackingIntroSequenceToken;

        ShowHackingIntroTextImmediately(hardwareEntryText);
        HideHackingIntroText(breakText);
        HideHackingIntroText(bootToolText);
        HideHackingIntroText(hardwareLockText);
        HideHackingIntroText(enterPasswordText);
        HideHackingElementsForIntro();

        PlayHackingIntroWriteStep(
            breakText,
            sequenceToken,
            () => PlayHackingIntroWriteStep(
                bootToolText,
                sequenceToken,
                () => PlayHackingIntroWriteStep(
                    hardwareLockText,
                    sequenceToken,
                    () => StartCoroutine(HandleHackingIntroAfterHardwareLock(sequenceToken)))));
    }


    private void CancelHackingIntroSequence()
    {
        hackingIntroSequenceToken++;
        isHackingIntroSequenceActive = false;
        RestoreHackingElementsAfterIntro();
    }


    private void CompleteHackingIntroSequence(int sequenceToken)
    {
        if (!IsHackingIntroSequenceValid(sequenceToken))
            return;

        isHackingIntroSequenceActive = false;
        RestoreHackingElementsAfterIntro();
        RefreshHackingAttemptsUi();
        RefreshHackingMessagesUi();
        UpdateHackingEntryVisuals();
        UpdateHackingCursorPosition();
        UpdateTerminalCursorBlink(true);
    }


    private IEnumerator HandleHackingIntroAfterHardwareLock(int sequenceToken)
    {
        if (!IsHackingIntroSequenceValid(sequenceToken))
            yield break;

        yield return new WaitForSecondsRealtime(HackingIntroPostHardwareLockDelaySeconds);

        if (!IsHackingIntroSequenceValid(sequenceToken))
            yield break;

        HideHackingIntroText(hardwareLockText);
        HideHackingIntroText(bootToolText);
        HideHackingIntroText(breakText);

        PlayHackingIntroWriteStep(
            enterPasswordText,
            sequenceToken,
            () => CompleteHackingIntroSequence(sequenceToken));
    }


    private void PlayHackingIntroWriteStep(TMP_Text text, int sequenceToken, Action onCompleted)
    {
        if (!IsHackingIntroSequenceValid(sequenceToken))
            return;

        if (!text || !text.gameObject)
        {
            onCompleted?.Invoke();
            return;
        }

        if (!text.gameObject.activeSelf)
            text.gameObject.SetActive(true);

        ConfigureBaseText(text, hackingFontSize, FontStyles.Normal);
        if (text == enterPasswordText)
            text.alignment = defaultEnterPasswordPromptAlignment;
        RegisterTextWriteAnimation(
            text,
            "",
            false,
            hackingIntroWriteCharactersPerSecond,
            () =>
            {
                if (!IsHackingIntroSequenceValid(sequenceToken))
                    return;

                onCompleted?.Invoke();
            });
    }


    private bool IsHackingIntroSequenceValid(int sequenceToken)
    {
        return isHackingIntroSequenceActive &&
            sequenceToken == hackingIntroSequenceToken &&
            isOpen &&
            isHackingActive;
    }


    private void HideHackingElementsForIntro()
    {
        hackingIntroTemporarilyHiddenObjects.Clear();

        if (hackingContentRoot)
            TemporarilyHideHackingElement(hackingContentRoot.gameObject);

        if (!hackingFrameRoot)
            return;

        Transform frameTransform = hackingFrameRoot.transform;
        for (int i = 0; i < frameTransform.childCount; i++)
        {
            Transform child = frameTransform.GetChild(i);
            if (!child)
                continue;

            GameObject childObject = child.gameObject;
            if (IsHackingIntroTextObject(childObject))
                continue;

            TemporarilyHideHackingElement(childObject);
        }
    }


    private void TemporarilyHideHackingElement(GameObject element)
    {
        if (!element || !element.activeSelf)
            return;

        element.SetActive(false);
        hackingIntroTemporarilyHiddenObjects.Add(element);
    }


    private void RestoreHackingElementsAfterIntro()
    {
        for (int i = 0; i < hackingIntroTemporarilyHiddenObjects.Count; i++)
        {
            GameObject hiddenObject = hackingIntroTemporarilyHiddenObjects[i];
            if (hiddenObject && !hiddenObject.activeSelf)
                hiddenObject.SetActive(true);
        }

        hackingIntroTemporarilyHiddenObjects.Clear();
    }


    private bool IsHackingIntroTextObject(GameObject targetObject)
    {
        if (!targetObject)
            return false;

        return (hardwareEntryText && targetObject == hardwareEntryText.gameObject) ||
            (breakText && targetObject == breakText.gameObject) ||
            (bootToolText && targetObject == bootToolText.gameObject) ||
            (hardwareLockText && targetObject == hardwareLockText.gameObject) ||
            (enterPasswordText && targetObject == enterPasswordText.gameObject);
    }


    private void ShowHackingIntroTextImmediately(TMP_Text text)
    {
        if (!text || !text.gameObject)
            return;

        if (!text.gameObject.activeSelf)
            text.gameObject.SetActive(true);

        ConfigureBaseText(text, hackingFontSize, FontStyles.Normal);
        if (text == enterPasswordText)
            text.alignment = defaultEnterPasswordPromptAlignment;
        ShowTextImmediately(text);
    }


    private static void HideHackingIntroText(TMP_Text text)
    {
        if (!text || !text.gameObject)
            return;

        if (text.gameObject.activeSelf)
            text.gameObject.SetActive(false);
    }


    private RectTransform EnsureHackingRuntimeContentRoot()
    {
        if (!hackingContentRoot)
            return null;

        if (!hackingRuntimeContentRoot)
            hackingRuntimeContentRoot = FindDirectChildRectTransform(hackingContentRoot, HackingRuntimeContentRootName);

        if (!hackingRuntimeContentRoot)
        {
            GameObject runtimeObject = new GameObject(HackingRuntimeContentRootName, typeof(RectTransform));
            runtimeObject.transform.SetParent(hackingContentRoot, false);
            hackingRuntimeContentRoot = runtimeObject.transform as RectTransform;
        }

        if (hackingRuntimeContentRoot)
        {
            hackingRuntimeContentRoot.anchorMin = Vector2.zero;
            hackingRuntimeContentRoot.anchorMax = Vector2.one;
            hackingRuntimeContentRoot.offsetMin = Vector2.zero;
            hackingRuntimeContentRoot.offsetMax = Vector2.zero;

            HorizontalLayoutGroup layoutGroup = hackingRuntimeContentRoot.GetComponent<HorizontalLayoutGroup>();
            if (!layoutGroup)
                layoutGroup = hackingRuntimeContentRoot.gameObject.AddComponent<HorizontalLayoutGroup>();

            layoutGroup.spacing = Mathf.Max(0.0f, hackingColumnSpacing);
            layoutGroup.childControlWidth = true;
            layoutGroup.childControlHeight = true;
            layoutGroup.childForceExpandWidth = false;
            layoutGroup.childForceExpandHeight = false;
        }

        return hackingRuntimeContentRoot;
    }


    private void ClearHackingContent()
    {
        hackingEntries.Clear();
        hackingTargets.Clear();
        placedHackingWords.Clear();
        placedHackingSpecialSequences.Clear();
        hackingWordTargetsByWordId.Clear();
        hackingSpecialTargetsById.Clear();
        selectedHackingTargetIndex = -1;
        hoveredHackingTargetIndex = -1;
        hackingPassword = string.Empty;
        ClearHackingMessages();

        RectTransform runtimeRoot = hackingRuntimeContentRoot;
        if (!runtimeRoot && hackingContentRoot)
            runtimeRoot = FindDirectChildRectTransform(hackingContentRoot, HackingRuntimeContentRootName);

        if (!runtimeRoot)
            return;

        for (int i = runtimeRoot.childCount - 1; i >= 0; i--)
            Destroy(runtimeRoot.GetChild(i).gameObject);
    }


    private void BuildHackingEntries(Terminal.LockType lockType)
    {
        hackingEntries.Clear();
        placedHackingWords.Clear();
        placedHackingSpecialSequences.Clear();

        Vector2Int lengthRange = activeTerminal
            ? activeTerminal.GetPasswordLengthRange()
            : new Vector2Int(4, 5);
        int desiredWordCount = Mathf.Clamp(hackingCandidateWordCount, 1, Mathf.Max(1, hackingRowsPerSelectableColumn * 2));
        HackingGenerationHistory previousGeneration = GetHackingGenerationHistory(activeTerminal);
        int selectedWordLength = ResolveHackingPasswordLength(lengthRange);
        List<string> candidateWords = GetHackingCandidateWords(selectedWordLength, desiredWordCount, previousGeneration != null ? previousGeneration.Words : null);

        if (candidateWords.Count == 0)
            candidateWords.Add(CreateFallbackHackingWord(selectedWordLength));

        Shuffle(candidateWords);

        desiredWordCount = Mathf.Min(desiredWordCount, candidateWords.Count);
        int totalEntrySlots = Mathf.Max(1, hackingRowsPerSelectableColumn * 2);
        int displayLength = Mathf.Max(12, GetMaxStringLength(candidateWords) + 2);
        int rowsPerColumn = Mathf.Max(1, hackingRowsPerSelectableColumn);

        List<char[]> rowCharacters = new List<char[]>(totalEntrySlots);
        List<int[]> rowWordIds = new List<int[]>(totalEntrySlots);
        List<int[]> rowSpecialIds = new List<int[]>(totalEntrySlots);
        for (int i = 0; i < totalEntrySlots; i++)
        {
            rowCharacters.Add(BuildSpecialCharacterString(displayLength).ToCharArray());
            int[] wordIds = new int[displayLength];
            int[] specialIds = new int[displayLength];
            for (int characterIndex = 0; characterIndex < wordIds.Length; characterIndex++)
            {
                wordIds[characterIndex] = -1;
                specialIds[characterIndex] = -1;
            }

            rowWordIds.Add(wordIds);
            rowSpecialIds.Add(specialIds);
        }

        bool hasWrappedWord = false;
        for (int wordIndex = 0; wordIndex < candidateWords.Count && placedHackingWords.Count < desiredWordCount; wordIndex++)
        {
            string word = candidateWords[wordIndex];
            if (string.IsNullOrWhiteSpace(word))
                continue;

            bool requireWrap = !hasWrappedWord && word.Length > 1 && rowsPerColumn > 1;
            if (!TryPlaceHackingWord(word, displayLength, rowsPerColumn, rowCharacters, rowWordIds, requireWrap, out bool wrapped))
            {
                if (requireWrap)
                    TryPlaceHackingWord(word, displayLength, rowsPerColumn, rowCharacters, rowWordIds, false, out wrapped);
            }

            if (wrapped)
                hasWrappedWord = true;
        }

        BuildHackingSpecialSequences(lockType, displayLength, rowsPerColumn, rowCharacters, rowWordIds, rowSpecialIds);

        for (int i = 0; i < totalEntrySlots; i++)
        {
            hackingEntries.Add(new HackingEntry
            {
                DisplayText = new string(rowCharacters[i]),
                WordIds = rowWordIds[i],
                SpecialIds = rowSpecialIds[i]
            });
        }

        PlacedHackingWord passwordWord = ChooseHackingPasswordWord(placedHackingWords, previousGeneration);
        if (passwordWord != null)
            passwordWord.IsPassword = true;

        hackingPassword = passwordWord != null ? passwordWord.Word : candidateWords[0];
        SaveHackingGenerationHistory(placedHackingWords, hackingPassword);
    }


    private bool TryPlaceHackingWord(
        string word,
        int displayLength,
        int rowsPerColumn,
        List<char[]> rowCharacters,
        List<int[]> rowWordIds,
        bool requireWrappedWord,
        out bool wrapped)
    {
        wrapped = false;
        if (string.IsNullOrWhiteSpace(word) || displayLength <= 0 || rowsPerColumn <= 0 || rowCharacters == null || rowWordIds == null)
            return false;

        int columnCount = 2;
        int streamLength = rowsPerColumn * displayLength;
        int safeWordLength = Mathf.Min(word.Length, streamLength);
        if (safeWordLength <= 0)
            return false;

        List<int> candidateStarts = new List<int>();
        for (int column = 0; column < columnCount; column++)
        {
            int columnRowOffset = column * rowsPerColumn;
            for (int start = 0; start <= streamLength - safeWordLength; start++)
            {
                int rowInColumn = start / displayLength;
                int offsetInRow = start % displayLength;
                bool wrapsToNextRow = offsetInRow + safeWordLength > displayLength && rowInColumn < rowsPerColumn - 1;
                if (requireWrappedWord && !wrapsToNextRow)
                    continue;

                if (CanPlaceHackingWordAt(columnRowOffset, rowsPerColumn, start, word, displayLength, rowCharacters, rowWordIds))
                    candidateStarts.Add((column * streamLength) + start);
            }
        }

        if (candidateStarts.Count == 0)
            return false;

        int selectedStart = candidateStarts[UnityEngine.Random.Range(0, candidateStarts.Count)];
        int selectedColumn = selectedStart / streamLength;
        int selectedColumnStart = selectedStart % streamLength;
        int selectedColumnRowOffset = selectedColumn * rowsPerColumn;
        int wordId = placedHackingWords.Count;
        placedHackingWords.Add(new PlacedHackingWord { Word = word.Trim().ToUpperInvariant() });

        for (int i = 0; i < safeWordLength; i++)
        {
            int streamIndex = selectedColumnStart + i;
            int entryIndex = selectedColumnRowOffset + (streamIndex / displayLength);
            int characterIndex = streamIndex % displayLength;
            rowCharacters[entryIndex][characterIndex] = char.ToUpperInvariant(word[i]);
            rowWordIds[entryIndex][characterIndex] = wordId;
        }

        wrapped = (selectedColumnStart % displayLength) + safeWordLength > displayLength;
        return true;
    }


    private static bool CanPlaceHackingWordAt(
        int columnRowOffset,
        int rowsPerColumn,
        int start,
        string word,
        int displayLength,
        List<char[]> rowCharacters,
        List<int[]> rowWordIds)
    {
        if (string.IsNullOrEmpty(word) || displayLength <= 0 || rowCharacters == null || rowWordIds == null)
            return false;

        int streamLength = Mathf.Max(0, rowsPerColumn * displayLength);
        int reservedStart = Mathf.Max(0, start - HackingMinimumSymbolsBetweenWords);
        int reservedEnd = Mathf.Min(streamLength - 1, start + word.Length + HackingMinimumSymbolsBetweenWords - 1);

        for (int streamIndex = reservedStart; streamIndex <= reservedEnd; streamIndex++)
        {
            int entryIndex = columnRowOffset + (streamIndex / displayLength);
            int characterIndex = streamIndex % displayLength;
            if (entryIndex < 0 || entryIndex >= rowWordIds.Count || characterIndex < 0 || characterIndex >= rowWordIds[entryIndex].Length)
                return false;

            if (rowWordIds[entryIndex][characterIndex] >= 0)
                return false;
        }

        return true;
    }


    private void BuildHackingSpecialSequences(
        Terminal.LockType lockType,
        int displayLength,
        int rowsPerColumn,
        List<char[]> rowCharacters,
        List<int[]> rowWordIds,
        List<int[]> rowSpecialIds)
    {
        placedHackingSpecialSequences.Clear();

        if (displayLength <= 0 || rowsPerColumn <= 0 || rowCharacters == null || rowWordIds == null || rowSpecialIds == null)
            return;

        int desiredSequenceCount = ResolveHackingSpecialSequenceCount(lockType);
        for (int i = 0; i < desiredSequenceCount; i++)
        {
            if (!TryPlaceHackingSpecialSequence(displayLength, rowsPerColumn, rowCharacters, rowWordIds, rowSpecialIds))
                break;
        }
    }


    private int ResolveHackingSpecialSequenceCount(Terminal.LockType lockType)
    {
        int minCount = Mathf.Max(0, Mathf.Min(hackingSpecialSequenceMinCount, hackingSpecialSequenceMaxCount));
        int maxCount = Mathf.Max(minCount, Mathf.Max(hackingSpecialSequenceMinCount, hackingSpecialSequenceMaxCount));
        if (maxCount <= minCount)
            return minCount;

        float lowerDifficultyWeight = 1.0f - (GetHackingDifficultyIndex(lockType) / 4.0f);
        return Mathf.RoundToInt(Mathf.Lerp(minCount, maxCount, lowerDifficultyWeight));
    }


    private static int GetHackingDifficultyIndex(Terminal.LockType lockType)
    {
        if (lockType == Terminal.LockType.VeryEasy) return 0;
        if (lockType == Terminal.LockType.Easy) return 1;
        if (lockType == Terminal.LockType.Average) return 2;
        if (lockType == Terminal.LockType.Hard) return 3;
        if (lockType == Terminal.LockType.VeryHard) return 4;
        return 1;
    }


    private bool TryPlaceHackingSpecialSequence(
        int displayLength,
        int rowsPerColumn,
        List<char[]> rowCharacters,
        List<int[]> rowWordIds,
        List<int[]> rowSpecialIds)
    {
        int columnCount = 2;
        int streamLength = rowsPerColumn * displayLength;
        int maxSequenceLength = Mathf.Min(streamLength, Mathf.Max(HackingSpecialSequenceMinLength, displayLength + 4));

        for (int attempt = 0; attempt < 64; attempt++)
        {
            int sequenceLength = UnityEngine.Random.Range(HackingSpecialSequenceMinLength, maxSequenceLength + 1);
            List<int> candidateStarts = new List<int>();
            for (int column = 0; column < columnCount; column++)
            {
                int columnRowOffset = column * rowsPerColumn;
                for (int start = 0; start <= streamLength - sequenceLength; start++)
                {
                    if (CanPlaceHackingSpecialSequenceAt(columnRowOffset, start, sequenceLength, displayLength, rowWordIds, rowSpecialIds))
                        candidateStarts.Add((column * streamLength) + start);
                }
            }

            if (candidateStarts.Count == 0)
                continue;

            int selectedStart = candidateStarts[UnityEngine.Random.Range(0, candidateStarts.Count)];
            int selectedColumn = selectedStart / streamLength;
            int selectedColumnStart = selectedStart % streamLength;
            int selectedColumnRowOffset = selectedColumn * rowsPerColumn;
            PlaceHackingSpecialSequence(selectedColumnRowOffset, selectedColumnStart, sequenceLength, displayLength, rowCharacters, rowSpecialIds);
            return true;
        }

        return false;
    }


    private static bool CanPlaceHackingSpecialSequenceAt(
        int columnRowOffset,
        int start,
        int sequenceLength,
        int displayLength,
        List<int[]> rowWordIds,
        List<int[]> rowSpecialIds)
    {
        if (sequenceLength < HackingSpecialSequenceMinLength)
            return false;

        for (int i = 0; i < sequenceLength; i++)
        {
            int streamIndex = start + i;
            if (GetHackingStreamWordId(columnRowOffset, streamIndex, displayLength, rowWordIds) >= 0 ||
                GetHackingStreamSpecialId(columnRowOffset, streamIndex, displayLength, rowSpecialIds) >= 0)
            {
                return false;
            }
        }

        return true;
    }


    private void PlaceHackingSpecialSequence(
        int columnRowOffset,
        int start,
        int sequenceLength,
        int displayLength,
        List<char[]> rowCharacters,
        List<int[]> rowSpecialIds)
    {
        char openingCharacter;
        char closingCharacter;
        GetRandomHackingBracketPair(out openingCharacter, out closingCharacter);

        int specialId = placedHackingSpecialSequences.Count;
        SetHackingStreamCharacter(columnRowOffset, start, displayLength, rowCharacters, openingCharacter);
        SetHackingStreamSpecialId(columnRowOffset, start, displayLength, rowSpecialIds, specialId);

        for (int i = 1; i < sequenceLength - 1; i++)
        {
            int streamIndex = start + i;
            SetHackingStreamCharacter(columnRowOffset, streamIndex, displayLength, rowCharacters, GetRandomHackingNonBracketSpecialCharacter());
            SetHackingStreamSpecialId(columnRowOffset, streamIndex, displayLength, rowSpecialIds, specialId);
        }

        int end = start + sequenceLength - 1;
        SetHackingStreamCharacter(columnRowOffset, end, displayLength, rowCharacters, closingCharacter);
        SetHackingStreamSpecialId(columnRowOffset, end, displayLength, rowSpecialIds, specialId);

        string sequenceText = GetHackingStreamText(columnRowOffset, start, end, displayLength, rowCharacters);
        placedHackingSpecialSequences.Add(new HackingSpecialSequence { Text = sequenceText });
    }


    private static void GetRandomHackingBracketPair(out char openingCharacter, out char closingCharacter)
    {
        switch (UnityEngine.Random.Range(0, 4))
        {
            case 0:
                openingCharacter = '<';
                closingCharacter = '>';
                return;
            case 1:
                openingCharacter = '{';
                closingCharacter = '}';
                return;
            case 2:
                openingCharacter = '[';
                closingCharacter = ']';
                return;
            default:
                openingCharacter = '(';
                closingCharacter = ')';
                return;
        }
    }


    private static bool IsHackingBracketCharacter(char character)
    {
        return character == '<' ||
               character == '>' ||
               character == '{' ||
               character == '}' ||
               character == '[' ||
               character == ']' ||
               character == '(' ||
               character == ')';
    }


    private static char GetHackingStreamCharacter(int columnRowOffset, int streamIndex, int displayLength, List<char[]> rowCharacters)
    {
        int entryIndex = columnRowOffset + (streamIndex / displayLength);
        int characterIndex = streamIndex % displayLength;
        if (rowCharacters == null || entryIndex < 0 || entryIndex >= rowCharacters.Count || characterIndex < 0 || characterIndex >= rowCharacters[entryIndex].Length)
            return '\0';

        return rowCharacters[entryIndex][characterIndex];
    }


    private static void SetHackingStreamCharacter(int columnRowOffset, int streamIndex, int displayLength, List<char[]> rowCharacters, char value)
    {
        int entryIndex = columnRowOffset + (streamIndex / displayLength);
        int characterIndex = streamIndex % displayLength;
        if (rowCharacters == null || entryIndex < 0 || entryIndex >= rowCharacters.Count || characterIndex < 0 || characterIndex >= rowCharacters[entryIndex].Length)
            return;

        rowCharacters[entryIndex][characterIndex] = value;
    }


    private static int GetHackingStreamWordId(int columnRowOffset, int streamIndex, int displayLength, List<int[]> rowWordIds)
    {
        int entryIndex = columnRowOffset + (streamIndex / displayLength);
        int characterIndex = streamIndex % displayLength;
        if (rowWordIds == null || entryIndex < 0 || entryIndex >= rowWordIds.Count || characterIndex < 0 || characterIndex >= rowWordIds[entryIndex].Length)
            return -1;

        return rowWordIds[entryIndex][characterIndex];
    }


    private static int GetHackingStreamSpecialId(int columnRowOffset, int streamIndex, int displayLength, List<int[]> rowSpecialIds)
    {
        int entryIndex = columnRowOffset + (streamIndex / displayLength);
        int characterIndex = streamIndex % displayLength;
        if (rowSpecialIds == null || entryIndex < 0 || entryIndex >= rowSpecialIds.Count || characterIndex < 0 || characterIndex >= rowSpecialIds[entryIndex].Length)
            return -1;

        return rowSpecialIds[entryIndex][characterIndex];
    }


    private static void SetHackingStreamSpecialId(int columnRowOffset, int streamIndex, int displayLength, List<int[]> rowSpecialIds, int specialId)
    {
        int entryIndex = columnRowOffset + (streamIndex / displayLength);
        int characterIndex = streamIndex % displayLength;
        if (rowSpecialIds == null || entryIndex < 0 || entryIndex >= rowSpecialIds.Count || characterIndex < 0 || characterIndex >= rowSpecialIds[entryIndex].Length)
            return;

        rowSpecialIds[entryIndex][characterIndex] = specialId;
    }


    private static string GetHackingStreamText(int columnRowOffset, int start, int end, int displayLength, List<char[]> rowCharacters)
    {
        if (end < start)
            return string.Empty;

        char[] text = new char[(end - start) + 1];
        for (int i = 0; i < text.Length; i++)
            text[i] = GetHackingStreamCharacter(columnRowOffset, start + i, displayLength, rowCharacters);

        return new string(text);
    }


    private void RebuildHackingContent()
    {
        RectTransform runtimeRoot = EnsureHackingRuntimeContentRoot();
        if (!runtimeRoot)
            return;

        hackingTargets.Clear();
        hackingWordTargetsByWordId.Clear();
        hackingSpecialTargetsById.Clear();
        selectedHackingTargetIndex = -1;
        hoveredHackingTargetIndex = -1;

        for (int i = runtimeRoot.childCount - 1; i >= 0; i--)
            Destroy(runtimeRoot.GetChild(i).gameObject);

        float memoryColumnWidth = GetHackingTextPreferredWidth("0xFFFF");
        float selectableColumnWidth = GetHackingSelectableColumnPreferredWidth();

        RectTransform memoryColumnA = CreateHackingColumn(runtimeRoot, "MemoryAddressColumn_A", memoryColumnWidth);
        RectTransform selectableColumnA = CreateHackingColumn(runtimeRoot, "SelectableColumn_A", selectableColumnWidth);
        RectTransform memoryColumnB = CreateHackingColumn(runtimeRoot, "MemoryAddressColumn_B", memoryColumnWidth);
        RectTransform selectableColumnB = CreateHackingColumn(runtimeRoot, "SelectableColumn_B", selectableColumnWidth);

        int rowsPerColumn = Mathf.Max(1, hackingRowsPerSelectableColumn);
        int startAddress = UnityEngine.Random.Range(0xF000, 0xF900);

        for (int row = 0; row < rowsPerColumn; row++)
        {
            CreateHackingMemoryAddressRow(memoryColumnA, startAddress + row * HackingMemoryAddressColumnStride);
            CreateHackingSelectableRow(selectableColumnA, row);
        }

        int secondColumnStartAddress = startAddress + rowsPerColumn * HackingMemoryAddressColumnStride;
        for (int row = 0; row < rowsPerColumn; row++)
        {
            CreateHackingMemoryAddressRow(memoryColumnB, secondColumnStartAddress + row * HackingMemoryAddressColumnStride);
            CreateHackingSelectableRow(selectableColumnB, rowsPerColumn + row);
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(runtimeRoot);
    }


    private float GetHackingSelectableColumnPreferredWidth()
    {
        float preferredWidth = 1.0f;

        for (int i = 0; i < hackingEntries.Count; i++)
        {
            HackingEntry entry = hackingEntries[i];
            if (entry == null)
                continue;

            preferredWidth = Mathf.Max(preferredWidth, GetHackingTextPreferredWidth(entry.DisplayText));
        }

        return preferredWidth;
    }


    private float GetHackingTextPreferredWidth(string text)
    {
        TMP_Text measurementText = EnsureBodyMeasurementText();
        if (!measurementText)
            return Mathf.Max(1.0f, (text ?? string.Empty).Length * hackingFontSize * 0.6f);

        ConfigureBaseText(measurementText, hackingFontSize, FontStyles.Normal);
        measurementText.textWrappingMode = TextWrappingModes.NoWrap;
        measurementText.overflowMode = TextOverflowModes.Overflow;
        measurementText.text = text ?? string.Empty;
        measurementText.ForceMeshUpdate(true, true);

        Vector2 preferredValues = measurementText.GetPreferredValues(measurementText.text, float.PositiveInfinity, float.PositiveInfinity);
        return Mathf.Max(1.0f, preferredValues.x);
    }


    private RectTransform CreateHackingColumn(Transform parent, string columnName, float preferredWidth)
    {
        GameObject columnObject = new GameObject(columnName, typeof(RectTransform));
        columnObject.transform.SetParent(parent, false);

        RectTransform columnRect = columnObject.transform as RectTransform;
        if (columnRect)
        {
            columnRect.anchorMin = new Vector2(0.0f, 1.0f);
            columnRect.anchorMax = new Vector2(0.0f, 1.0f);
            columnRect.pivot = new Vector2(0.0f, 1.0f);
        }

        VerticalLayoutGroup layoutGroup = columnObject.AddComponent<VerticalLayoutGroup>();
        layoutGroup.spacing = Mathf.Max(0.0f, hackingRowSpacing);
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;

        LayoutElement layoutElement = columnObject.AddComponent<LayoutElement>();
        layoutElement.preferredWidth = Mathf.Max(1.0f, preferredWidth);
        layoutElement.flexibleWidth = 0.0f;

        return columnRect;
    }


    private void CreateHackingMemoryAddressRow(Transform parent, int address)
    {
        TMP_Text text = CreateHackingTextRow(parent, "MemoryAddress", "0x" + (address & 0xFFFF).ToString("X4"));
        if (text)
            text.raycastTarget = false;
    }


    private void CreateHackingSelectableRow(Transform parent, int entryIndex)
    {
        if (entryIndex < 0 || entryIndex >= hackingEntries.Count)
            return;

        HackingEntry entry = hackingEntries[entryIndex];
        GameObject rowObject = new GameObject("HackingEntry_" + entryIndex, typeof(RectTransform));
        rowObject.transform.SetParent(parent, false);

        RectTransform rowRect = rowObject.transform as RectTransform;
        if (rowRect)
        {
            rowRect.anchorMin = new Vector2(0.0f, 1.0f);
            rowRect.anchorMax = new Vector2(1.0f, 1.0f);
            rowRect.pivot = new Vector2(0.5f, 1.0f);
            rowRect.sizeDelta = new Vector2(0.0f, hackingRowHeight);
        }

        LayoutElement layoutElement = rowObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = hackingRowHeight;
        layoutElement.preferredHeight = hackingRowHeight;
        layoutElement.flexibleWidth = 1.0f;

        HorizontalLayoutGroup rowLayout = rowObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 0.0f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;

        CreateHackingTargetsForEntry(rowObject.transform, entryIndex);
    }


    private void CreateHackingTargetsForEntry(Transform parent, int entryIndex)
    {
        HackingEntry entry = hackingEntries[entryIndex];
        if (entry == null || string.IsNullOrEmpty(entry.DisplayText))
            return;

        entry.Targets.Clear();

        int index = 0;
        while (index < entry.DisplayText.Length)
        {
            int wordId = GetHackingEntryWordId(entry, index);
            if (wordId >= 0)
            {
                int length = 1;
                while (index + length < entry.DisplayText.Length && GetHackingEntryWordId(entry, index + length) == wordId)
                    length++;

                HackingTarget target = GetOrCreateHackingWordTarget(wordId, entryIndex, index);
                string text = entry.DisplayText.Substring(index, length);
                HackingTargetPart part = CreateHackingTargetPart(parent, target, target.TargetIndex, entryIndex, index, text);
                target.Parts.Add(part);
                target.HighlightParts.Add(part);
                entry.Targets.Add(target);
                index += length;
                continue;
            }

            int specialId = GetHackingEntrySpecialId(entry, index);
            if (specialId >= 0)
            {
                string specialCharacter = entry.DisplayText.Substring(index, 1);
                HackingTarget sequenceTarget = GetOrCreateHackingSpecialTarget(specialId, entryIndex, index);
                if (IsHackingBracketCharacter(specialCharacter[0]))
                {
                    HackingTargetPart part = CreateHackingTargetPart(parent, sequenceTarget, sequenceTarget.TargetIndex, entryIndex, index, specialCharacter);
                    sequenceTarget.Parts.Add(part);
                    sequenceTarget.HighlightParts.Add(part);
                    entry.Targets.Add(sequenceTarget);
                }
                else
                {
                    HackingTarget specialCharacterTarget = CreateHackingCharacterTarget(entryIndex, index, specialCharacter);
                    HackingTargetPart specialCharacterPart = CreateHackingTargetPart(parent, specialCharacterTarget, specialCharacterTarget.TargetIndex, entryIndex, index, specialCharacter);
                    specialCharacterTarget.Parts.Add(specialCharacterPart);
                    specialCharacterTarget.HighlightParts.Add(specialCharacterPart);
                    sequenceTarget.HighlightParts.Add(specialCharacterPart);
                    entry.Targets.Add(specialCharacterTarget);
                    hackingTargets.Add(specialCharacterTarget);
                }

                index++;
                continue;
            }

            string character = entry.DisplayText.Substring(index, 1);
            HackingTarget characterTarget = CreateHackingCharacterTarget(entryIndex, index, character);
            HackingTargetPart characterPart = CreateHackingTargetPart(parent, characterTarget, characterTarget.TargetIndex, entryIndex, index, character);
            characterTarget.Parts.Add(characterPart);
            characterTarget.HighlightParts.Add(characterPart);
            entry.Targets.Add(characterTarget);
            hackingTargets.Add(characterTarget);
            index++;
        }
    }


    private HackingTarget GetOrCreateHackingWordTarget(int wordId, int entryIndex, int startIndex)
    {
        if (hackingWordTargetsByWordId.TryGetValue(wordId, out HackingTarget existingTarget))
            return existingTarget;

        PlacedHackingWord placedWord = wordId >= 0 && wordId < placedHackingWords.Count
            ? placedHackingWords[wordId]
            : null;
        string word = placedWord != null ? placedWord.Word : string.Empty;

        HackingTarget target = new HackingTarget
        {
            TargetIndex = hackingTargets.Count,
            EntryIndex = entryIndex,
            StartIndex = startIndex,
            Length = string.IsNullOrEmpty(word) ? 1 : word.Length,
            WordId = wordId,
            Text = word,
            AnswerText = word,
            IsWord = true,
            IsPassword = placedWord != null && placedWord.IsPassword
        };

        hackingTargets.Add(target);
        hackingWordTargetsByWordId[wordId] = target;
        return target;
    }


    private HackingTarget GetOrCreateHackingSpecialTarget(int specialId, int entryIndex, int startIndex)
    {
        if (hackingSpecialTargetsById.TryGetValue(specialId, out HackingTarget existingTarget))
            return existingTarget;

        HackingSpecialSequence sequence = specialId >= 0 && specialId < placedHackingSpecialSequences.Count
            ? placedHackingSpecialSequences[specialId]
            : null;
        string text = sequence != null ? sequence.Text : string.Empty;

        HackingTarget target = new HackingTarget
        {
            TargetIndex = hackingTargets.Count,
            EntryIndex = entryIndex,
            StartIndex = startIndex,
            Length = string.IsNullOrEmpty(text) ? 1 : text.Length,
            SpecialId = specialId,
            Text = text,
            AnswerText = text,
            IsSpecialSequence = true
        };

        hackingTargets.Add(target);
        hackingSpecialTargetsById[specialId] = target;
        return target;
    }


    private HackingTarget CreateHackingCharacterTarget(int entryIndex, int startIndex, string text)
    {
        return new HackingTarget
        {
            TargetIndex = hackingTargets.Count,
            EntryIndex = entryIndex,
            StartIndex = startIndex,
            Length = 1,
            Text = text,
            AnswerText = text,
            IsWord = false
        };
    }


    private HackingTargetPart CreateHackingTargetPart(Transform parent, HackingTarget target, int targetIndex, int entryIndex, int startIndex, string text)
    {
        GameObject buttonObject = new GameObject("HackingTarget_" + entryIndex + "_" + startIndex, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        float preferredWidth = GetHackingTextPreferredWidth(text);

        RectTransform buttonRect = buttonObject.transform as RectTransform;
        if (buttonRect)
            buttonRect.sizeDelta = new Vector2(preferredWidth, hackingRowHeight);

        LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.minWidth = preferredWidth;
        layoutElement.preferredWidth = preferredWidth;
        layoutElement.minHeight = hackingRowHeight;
        layoutElement.preferredHeight = hackingRowHeight;
        layoutElement.flexibleWidth = 0.0f;

        Image image = buttonObject.GetComponent<Image>();
        image.color = Color.clear;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => SelectHackingTarget(targetIndex));

        Image hoverRectangle = EnsureHackingHoverRectangle(button);

        GameObject labelObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.transform as RectTransform;
        if (labelRect)
        {
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        ConfigureHackingText(label, text);
        ApplyHackingTargetTextOffset(labelRect, text);

        label.raycastTarget = false;

        EventTrigger trigger = buttonObject.GetComponent<EventTrigger>();
        if (!trigger)
            trigger = buttonObject.AddComponent<EventTrigger>();

        AddHackingPointerEnterTrigger(trigger, targetIndex);
        AddHackingPointerExitTrigger(trigger, targetIndex);

        return new HackingTargetPart
        {
            EntryIndex = entryIndex,
            StartIndex = startIndex,
            Length = Mathf.Max(1, text.Length),
            Text = text,
            Button = button,
            HoverRectangle = hoverRectangle,
            Label = label
        };
    }


    private TMP_Text CreateHackingTextRow(Transform parent, string rowName, string text)
    {
        GameObject textObject = new GameObject(rowName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform textRect = textObject.transform as RectTransform;
        if (textRect)
            textRect.sizeDelta = new Vector2(0.0f, hackingRowHeight);

        LayoutElement layoutElement = textObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = hackingRowHeight;
        layoutElement.preferredHeight = hackingRowHeight;

        TMP_Text tmpText = textObject.GetComponent<TMP_Text>();
        ConfigureHackingText(tmpText, text);
        return tmpText;
    }


    private void ConfigureHackingText(TMP_Text text, string value)
    {
        ConfigureBaseText(text, hackingFontSize, FontStyles.Normal);
        text.text = value;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        ShowTextImmediately(text);
    }


    private void ApplyHackingTargetTextOffset(RectTransform labelRect, string text)
    {
        if (!labelRect || !ShouldLowerHackingTargetText(text))
            return;

        Vector2 offset = new Vector2(0.0f, hackingLowPunctuationTextOffsetY);
        labelRect.offsetMin += offset;
        labelRect.offsetMax += offset;
    }


    private static bool ShouldLowerHackingTargetText(string text)
    {
        if (string.IsNullOrEmpty(text) || text.Length != 1)
            return false;

        char character = text[0];
        return character == '.' || character == ',' || character == ';';
    }


    private Image EnsureHackingHoverRectangle(Button button)
    {
        if (!button)
            return null;

        GameObject hoverObject = new GameObject(OptionHoverRectangleName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        hoverObject.transform.SetParent(button.transform, false);
        hoverObject.transform.SetAsFirstSibling();

        RectTransform hoverRect = hoverObject.transform as RectTransform;
        if (hoverRect)
        {
            float verticalBleed = Mathf.Max(0.0f, (HoveredOptionRectangleExtraHeight * 0.5f) - 2.0f);
            hoverRect.anchorMin = Vector2.zero;
            hoverRect.anchorMax = Vector2.one;
            hoverRect.offsetMin = new Vector2(0.0f, -verticalBleed);
            hoverRect.offsetMax = new Vector2(0.0f, verticalBleed);
        }

        Image hoverImage = hoverObject.GetComponent<Image>();
        hoverImage.color = Color.clear;
        hoverImage.raycastTarget = false;
        return hoverImage;
    }


    private void SetHoveredHackingTarget(int targetIndex)
    {
        if (!isHackingActive || targetIndex < 0 || targetIndex >= hackingTargets.Count)
            return;

        HackingTarget target = hackingTargets[targetIndex];
        HackingEntry entry = GetHackingEntry(target);
        if (target == null || entry == null || target.HasBeenSelected || isHackingLockedOut)
            return;

        hoveredHackingTargetIndex = targetIndex;
        SetHackingAnswerText(target.AnswerText);
        UpdateHackingEntryVisuals();
    }


    private void ClearHoveredHackingTarget(int targetIndex, BaseEventData eventData = null)
    {
        if (hoveredHackingTargetIndex != targetIndex)
            return;

        if (IsPointerInsideHackingTarget(eventData, targetIndex))
            return;

        hoveredHackingTargetIndex = -1;
        RefreshHackingAnswerTextFromActiveTarget();
        UpdateHackingEntryVisuals();
    }


    private bool IsPointerInsideHackingTarget(BaseEventData eventData, int targetIndex)
    {
        PointerEventData pointerEventData = eventData as PointerEventData;
        if (pointerEventData == null || targetIndex < 0 || targetIndex >= hackingTargets.Count)
            return false;

        HackingTarget target = hackingTargets[targetIndex];
        if (target == null)
            return false;

        List<HackingTargetPart> parts = target.HighlightParts.Count > 0 ? target.HighlightParts : target.Parts;
        Camera eventCamera = pointerEventData.pressEventCamera ? pointerEventData.pressEventCamera : pointerEventData.enterEventCamera;
        for (int i = 0; i < parts.Count; i++)
        {
            HackingTargetPart part = parts[i];
            if (part == null || !part.Button)
                continue;

            RectTransform partRect = part.Button.transform as RectTransform;
            if (partRect && RectTransformUtility.RectangleContainsScreenPoint(partRect, pointerEventData.position, eventCamera))
                return true;
        }

        return false;
    }


    private void RefreshHackingAnswerTextFromActiveTarget()
    {
        int activeTargetIndex = GetActiveHackingTargetIndex();
        HackingTarget target = activeTargetIndex >= 0 && activeTargetIndex < hackingTargets.Count
            ? hackingTargets[activeTargetIndex]
            : null;

        if (target == null || target.HasBeenSelected || isHackingLockedOut)
        {
            SetHackingAnswerText(string.Empty);
            return;
        }

        SetHackingAnswerText(target.AnswerText);
    }


    private void SelectHackingTarget(int targetIndex)
    {
        if (!isHackingActive || isHackingLockedOut || targetIndex < 0 || targetIndex >= hackingTargets.Count)
            return;

        HackingTarget target = hackingTargets[targetIndex];
        HackingEntry entry = GetHackingEntry(target);
        if (target == null || entry == null || target.HasBeenSelected)
            return;

        selectedHackingTargetIndex = targetIndex;

        if (target.IsSpecialSequence)
        {
            SelectHackingSpecialSequence(target);
            return;
        }

        if (!target.IsWord)
        {
            SetHackingAnswerText(target.AnswerText);
            UpdateHackingEntryVisuals();
            return;
        }

        target.HasBeenSelected = true;
        AddHackingMessage(target.Text);

        if (target.IsPassword && string.Equals(target.Text, hackingPassword, StringComparison.OrdinalIgnoreCase))
        {
            AddHackingMessage("Exact match!");
            AddHackingMessage("Access granted.");
            StartHackingOutcomeDelay(true);
            return;
        }

        hackingAttemptsRemaining = Mathf.Max(0, hackingAttemptsRemaining - 1);
        int likeness = GetPasswordLikeness(target.Text, hackingPassword);
        AddHackingMessage("Entry denied.");
        AddHackingMessage(likeness + "/" + hackingPassword.Length + " correct.");
        RefreshHackingAttemptsUi();

        if (hackingAttemptsRemaining <= 0)
        {
            isHackingLockedOut = true;
            AddHackingMessage("Access denied.");
            StartHackingOutcomeDelay(false);
        }

        UpdateHackingEntryVisuals();
    }


    private void SelectHackingSpecialSequence(HackingTarget target)
    {
        if (target == null)
            return;

        target.HasBeenSelected = true;
        SetHackingAnswerText(target.AnswerText);
        AddHackingMessage(target.Text);

        int maxAttempts = Mathf.Clamp(hackingAttempts, 1, 4);
        bool replenishAllowance = UnityEngine.Random.value < 0.5f;

        if (replenishAllowance)
        {
            hackingAttemptsRemaining = maxAttempts;
            RefreshHackingAttemptsUi();
            AddHackingMessage("Allowance\nreplenished.");
        }
        else
        {
            bool removedDud = TryRemoveHackingDud();
            AddHackingMessage(removedDud ? "Dud removed." : "No dud to remove.");
        }

        UpdateHackingEntryVisuals();
    }


    private bool TryRemoveHackingDud()
    {
        List<HackingTarget> availableDuds = new List<HackingTarget>();
        for (int i = 0; i < hackingTargets.Count; i++)
        {
            HackingTarget target = hackingTargets[i];
            if (target != null && target.IsWord && !target.IsPassword && !target.HasBeenSelected)
                availableDuds.Add(target);
        }

        if (availableDuds.Count == 0)
            return false;

        HackingTarget dud = availableDuds[UnityEngine.Random.Range(0, availableDuds.Count)];
        dud.HasBeenSelected = true;
        ReplaceHackingTargetText(dud, '.');
        return true;
    }


    private void ReplaceHackingTargetText(HackingTarget target, char replacementCharacter)
    {
        if (target == null)
            return;

        target.Text = new string(replacementCharacter, Mathf.Max(1, target.Length));
        target.AnswerText = target.Text;

        for (int i = 0; i < target.Parts.Count; i++)
        {
            HackingTargetPart part = target.Parts[i];
            if (part == null)
                continue;

            part.Text = new string(replacementCharacter, Mathf.Max(1, part.Length));
            if (part.Label)
            {
                part.Label.text = part.Text;
                ShowTextImmediately(part.Label);
            }
        }
    }


    private HackingEntry GetHackingEntry(HackingTarget target)
    {
        if (target == null || target.EntryIndex < 0 || target.EntryIndex >= hackingEntries.Count)
            return null;

        return hackingEntries[target.EntryIndex];
    }


    private static int GetHackingEntryWordId(HackingEntry entry, int characterIndex)
    {
        if (entry == null || entry.WordIds == null || characterIndex < 0 || characterIndex >= entry.WordIds.Length)
            return -1;

        return entry.WordIds[characterIndex];
    }


    private static int GetHackingEntrySpecialId(HackingEntry entry, int characterIndex)
    {
        if (entry == null || entry.SpecialIds == null || characterIndex < 0 || characterIndex >= entry.SpecialIds.Length)
            return -1;

        return entry.SpecialIds[characterIndex];
    }


    private void StartHackingOutcomeDelay(bool success)
    {
        StopHackingOutcomeDelay();
        isHackingLockedOut = true;
        UpdateHackingEntryVisuals();
        hackingOutcomeCoroutine = StartCoroutine(HandleHackingOutcomeAfterDelay(success));
    }


    private IEnumerator HandleHackingOutcomeAfterDelay(bool success)
    {
        yield return new WaitForSecondsRealtime(2.0f);

        hackingOutcomeCoroutine = null;
        if (!isOpen || !isHackingActive)
            yield break;

        if (success)
        {
            CompleteHackingSuccess();
            yield break;
        }

        Close();
    }


    private void StopHackingOutcomeDelay()
    {
        if (hackingOutcomeCoroutine == null)
            return;

        StopCoroutine(hackingOutcomeCoroutine);
        hackingOutcomeCoroutine = null;
    }


    private void CompleteHackingSuccess()
    {
        if (activeTerminal)
            activeTerminal.UnlockWithHacking(activeInteractor);

        isHackingActive = false;
        isHackingLockedOut = false;
        ClearHackingContent();
        SetHackingUiVisible(false);
        ShowStartupPage();
    }


    private void RefreshHackingAttemptsUi()
    {
        ResolveHackingUiReferences();

        int clampedAttempts = Mathf.Clamp(hackingAttemptsRemaining, 0, 4);
        RefreshHackingEnterPasswordText(clampedAttempts);

        if (attemptsText)
        {
            ConfigureBaseText(attemptsText, hackingFontSize, FontStyles.Normal);
            attemptsText.text = clampedAttempts + " Attempt(s) Left:";
            ShowTextImmediately(attemptsText);
        }

        if (attemptBoxes == null)
            return;

        for (int i = 0; i < attemptBoxes.Length; i++)
        {
            GameObject attemptBox = attemptBoxes[i];
            if (!attemptBox)
                continue;

            attemptBox.SetActive(i < clampedAttempts);
        }
    }


    private void RefreshHackingEnterPasswordText(int clampedAttempts)
    {
        if (!enterPasswordText)
            return;

        ConfigureBaseText(enterPasswordText, hackingFontSize, FontStyles.Normal);
        enterPasswordText.alignment = defaultEnterPasswordPromptAlignment;
        enterPasswordText.text = clampedAttempts == 1
            ? HackingLockoutImminentText
            : defaultEnterPasswordPromptText;
        ShowTextImmediately(enterPasswordText);
    }


    private void SetHackingAnswerText(string text)
    {
        if (!answerText)
            answerText = FindChildComponentByName<TMP_Text>(AnswerTextName);

        if (!answerText)
            return;

        ConfigureBaseText(answerText, hackingFontSize, FontStyles.Normal);
        answerText.text = text ?? string.Empty;
        ShowTextImmediately(answerText);
        UpdateHackingCursorPosition();
        UpdateTerminalCursorBlink(true);
    }


    private void AddHackingMessage(string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        string normalizedMessage = message.Replace("\r\n", "\n").Replace('\r', '\n');
        string[] lines = normalizedMessage.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;

            hackingMessageLines.Add(line.Trim());
        }

        RefreshHackingMessagesUi();
    }


    private void ClearHackingMessages()
    {
        hackingMessageLines.Clear();
        RefreshHackingMessagesUi();
    }


    private void RefreshHackingMessagesUi()
    {
        EnsureHackingMessagesUi();

        if (!hackingMessageArrowText || !hackingMessageText)
            return;

        int visibleLogLines = UpdateHackingMessagesLayout();
        int visibleLineCount = Mathf.Min(hackingMessageLines.Count, visibleLogLines);
        if (visibleLineCount <= 0)
        {
            hackingMessageArrowText.text = string.Empty;
            hackingMessageText.text = string.Empty;
            ShowTextImmediately(hackingMessageArrowText);
            ShowTextImmediately(hackingMessageText);
            return;
        }

        int firstLineIndex = Mathf.Max(0, hackingMessageLines.Count - visibleLineCount);
        List<string> visibleMessages = new List<string>(visibleLineCount);
        List<string> visibleArrows = new List<string>(visibleLineCount);

        for (int i = firstLineIndex; i < hackingMessageLines.Count; i++)
        {
            visibleMessages.Add(hackingMessageLines[i]);
            visibleArrows.Add(">");
        }

        hackingMessageArrowText.text = string.Join("\n", visibleArrows);
        hackingMessageText.text = string.Join("\n", visibleMessages);
        ShowTextImmediately(hackingMessageArrowText);
        ShowTextImmediately(hackingMessageText);
    }


    private void EnsureHackingMessagesUi()
    {
        if (!hackingFrameRoot)
        {
            RectTransform hackingFrameTransform = FindChildComponentByName<RectTransform>(HackingFrameObjectName);
            hackingFrameRoot = hackingFrameTransform ? hackingFrameTransform.gameObject : null;
        }

        if (!hackingFrameRoot)
            return;

        if (!hackingMessagesRoot)
            hackingMessagesRoot = FindDirectChildRectTransform(hackingFrameRoot.transform, HackingMessagesRootName);

        if (!hackingMessagesRoot)
        {
            GameObject messagesObject = new GameObject(HackingMessagesRootName, typeof(RectTransform));
            messagesObject.transform.SetParent(hackingFrameRoot.transform, false);
            hackingMessagesRoot = messagesObject.transform as RectTransform;
        }

        if (hackingMessagesRoot)
        {
            RectMask2D messageMask = hackingMessagesRoot.GetComponent<RectMask2D>();
            if (messageMask)
                Destroy(messageMask);

            Graphic rootGraphic = hackingMessagesRoot.GetComponent<Graphic>();
            if (rootGraphic)
            {
                rootGraphic.color = Color.clear;
                rootGraphic.raycastTarget = false;
            }

            hackingMessagesRoot.anchorMin = Vector2.zero;
            hackingMessagesRoot.anchorMax = Vector2.one;
            hackingMessagesRoot.offsetMin = Vector2.zero;
            hackingMessagesRoot.offsetMax = Vector2.zero;
            hackingMessagesRoot.SetAsLastSibling();
        }

        if (!hackingMessageArrowText)
            hackingMessageArrowText = FindChildComponentByNameInRoot<TMP_Text>(HackingMessageArrowTextName, hackingFrameRoot.transform);

        if (!hackingMessageArrowText)
            hackingMessageArrowText = CreateHackingMessageText(HackingMessageArrowTextName);

        if (!hackingMessageText)
            hackingMessageText = FindChildComponentByNameInRoot<TMP_Text>(HackingMessageTextName, hackingFrameRoot.transform);

        if (!hackingMessageText)
            hackingMessageText = CreateHackingMessageText(HackingMessageTextName);

        ConfigureHackingMessageText(hackingMessageArrowText);
        ConfigureHackingMessageText(hackingMessageText);

        if (hackingMessageArrowText)
            hackingMessageArrowText.transform.SetAsLastSibling();

        if (hackingMessageText)
            hackingMessageText.transform.SetAsLastSibling();
    }


    private TMP_Text CreateHackingMessageText(string objectName)
    {
        if (!hackingMessagesRoot)
            return null;

        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(hackingMessagesRoot, false);
        return textObject.GetComponent<TMP_Text>();
    }


    private void ConfigureHackingMessageText(TMP_Text text)
    {
        if (!text)
            return;

        ConfigureBaseText(text, hackingFontSize, FontStyles.Normal);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Truncate;
        text.alignment = TextAlignmentOptions.BottomLeft;
        text.raycastTarget = false;
        ShowTextImmediately(text);
    }


    private int UpdateHackingMessagesLayout()
    {
        int visibleLogLines = GetHackingLogLineCount();
        if (!hackingMessagesRoot || !hackingMessageArrowText || !hackingMessageText || !answerText)
            return visibleLogLines;

        RectTransform rootRect = hackingMessagesRoot;
        RectTransform answerRect = answerText.rectTransform;
        RectTransform arrowRect = arrowText ? arrowText.rectTransform : null;
        if (!rootRect || !answerRect)
            return visibleLogLines;

        Canvas.ForceUpdateCanvases();

        float lineHeight = GetHackingMessageLineHeight();
        Vector3[] answerCorners = new Vector3[4];
        answerRect.GetWorldCorners(answerCorners);

        Vector2 answerBottomLeft = rootRect.InverseTransformPoint(answerCorners[0]);
        Vector2 answerTopLeft = rootRect.InverseTransformPoint(answerCorners[1]);
        Vector2 answerTopRight = rootRect.InverseTransformPoint(answerCorners[2]);

        float bottomY = answerTopLeft.y + lineHeight;
        float height = Mathf.Max(lineHeight, visibleLogLines * lineHeight);

        float arrowLeft = answerBottomLeft.x - GetHackingTextPreferredWidth("> ");
        float arrowWidth = GetHackingTextPreferredWidth(">");
        if (arrowRect)
        {
            Vector3[] arrowCorners = new Vector3[4];
            arrowRect.GetWorldCorners(arrowCorners);
            Vector2 arrowBottomLeft = rootRect.InverseTransformPoint(arrowCorners[0]);
            Vector2 arrowTopRight = rootRect.InverseTransformPoint(arrowCorners[2]);
            arrowLeft = arrowBottomLeft.x;
            arrowWidth = Mathf.Max(1.0f, arrowTopRight.x - arrowBottomLeft.x);
        }

        float textLeft = answerBottomLeft.x;
        float textWidth = Mathf.Max(1.0f, answerTopRight.x - answerBottomLeft.x);
        SetChildRectFromParentLocalBounds(hackingMessageArrowText.rectTransform, rootRect, arrowLeft, bottomY, arrowWidth, height);
        SetChildRectFromParentLocalBounds(hackingMessageText.rectTransform, rootRect, textLeft, bottomY, textWidth, height);
        return visibleLogLines;
    }


    private int GetHackingLogLineCount()
    {
        return Mathf.Max(1, hackingLogLineCount);
    }


    private float GetHackingMessageLineHeight()
    {
        TMP_Text referenceText = answerText ? answerText : hackingMessageText;
        if (referenceText)
        {
            TMP_Text measurementText = EnsureBodyMeasurementText();
            if (measurementText)
            {
                ConfigureBaseText(measurementText, referenceText.fontSize, referenceText.fontStyle);
                measurementText.textWrappingMode = TextWrappingModes.NoWrap;
                measurementText.overflowMode = TextOverflowModes.Overflow;
                measurementText.text = "A\nA";
                measurementText.ForceMeshUpdate(true, true);

                TMP_TextInfo textInfo = measurementText.textInfo;
                if (textInfo.lineCount > 0 && textInfo.lineInfo[0].lineHeight > 0.0f)
                    return Mathf.Max(1.0f, textInfo.lineInfo[0].lineHeight);
            }

            return Mathf.Max(1.0f, referenceText.fontSize);
        }

        return Mathf.Max(1.0f, hackingFontSize);
    }


    private static void SetChildRectFromParentLocalBounds(RectTransform childRect, RectTransform parentRect, float x, float y, float width, float height)
    {
        if (!childRect || !parentRect)
            return;

        childRect.anchorMin = Vector2.zero;
        childRect.anchorMax = Vector2.zero;
        childRect.pivot = Vector2.zero;
        childRect.anchoredPosition = new Vector2(x - parentRect.rect.xMin, y - parentRect.rect.yMin);
        childRect.sizeDelta = new Vector2(Mathf.Max(1.0f, width), Mathf.Max(1.0f, height));
    }


    private void UpdateHackingEntryVisuals()
    {
        for (int i = 0; i < hackingTargets.Count; i++)
        {
            HackingTarget target = hackingTargets[i];
            HackingEntry entry = GetHackingEntry(target);
            if (target == null || entry == null)
                continue;

            bool isAvailable = !target.HasBeenSelected && !isHackingLockedOut;
            for (int partIndex = 0; partIndex < target.Parts.Count; partIndex++)
            {
                HackingTargetPart part = target.Parts[partIndex];
                if (part == null)
                    continue;

                if (part.HoverRectangle)
                    part.HoverRectangle.color = Color.clear;

                if (part.Label)
                    part.Label.color = target.HasBeenSelected && !target.IsSpecialSequence
                        ? hackingSelectedEntryTextColor
                        : terminalTextColor;

                if (part.Button)
                    part.Button.interactable = isAvailable;
            }
        }

        for (int i = 0; i < hackingTargets.Count; i++)
        {
            HackingTarget target = hackingTargets[i];
            HackingEntry entry = GetHackingEntry(target);
            if (target == null || entry == null)
                continue;

            bool isAvailable = !target.HasBeenSelected && !isHackingLockedOut;
            bool isActiveTarget = i == GetActiveHackingTargetIndex() && isAvailable;
            if (!isActiveTarget)
                continue;

            List<HackingTargetPart> visualParts = target.HighlightParts.Count > 0 ? target.HighlightParts : target.Parts;
            for (int partIndex = 0; partIndex < visualParts.Count; partIndex++)
            {
                HackingTargetPart part = visualParts[partIndex];
                if (part == null)
                    continue;

                if (part.HoverRectangle)
                    part.HoverRectangle.color = hackingHoveredEntryRectangleColor;

                if (part.Label)
                    part.Label.color = hackingHoveredEntryTextColor;
            }
        }
    }


    private int GetActiveHackingTargetIndex()
    {
        return hoveredHackingTargetIndex >= 0 ? hoveredHackingTargetIndex : selectedHackingTargetIndex;
    }


    private void HandleHackingKeyboardInput()
    {
        if (ignoreInputUntilFrame >= Time.frameCount)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (isHackingIntroSequenceActive)
            return;

        if (keyboard.eKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        if (closeOnCancel && keyboard.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
        {
            MoveHackingSelection(-1);
            return;
        }

        if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
        {
            MoveHackingSelection(1);
            return;
        }

        if (keyboard.enterKey.wasPressedThisFrame ||
            keyboard.numpadEnterKey.wasPressedThisFrame ||
            keyboard.spaceKey.wasPressedThisFrame)
        {
            SelectHackingTarget(selectedHackingTargetIndex);
        }
    }


    private void MoveHackingSelection(int delta)
    {
        if (hackingTargets.Count == 0 || isHackingLockedOut)
            return;

        int current = selectedHackingTargetIndex < 0 ? 0 : selectedHackingTargetIndex;
        for (int step = 0; step < hackingTargets.Count; step++)
        {
            current = (current + delta + hackingTargets.Count) % hackingTargets.Count;
            HackingTarget target = hackingTargets[current];
            HackingEntry entry = GetHackingEntry(target);
            if (entry == null || target == null || target.HasBeenSelected)
                continue;

            selectedHackingTargetIndex = current;
            hoveredHackingTargetIndex = -1;
            SetHackingAnswerText(target.AnswerText);
            UpdateHackingEntryVisuals();
            return;
        }
    }


    private void BuildRenderedOptions()
    {
        if (activePage == null)
            return;

        if (activePage.options != null)
        {
            for (int i = 0; i < activePage.options.Count; i++)
            {
                TerminalOption option = activePage.options[i];
                if (option == null)
                    continue;

                renderedOptions.Add(new RenderedTerminalOption
                {
                    Label = FormatOptionLabel(option.label),
                    Option = option,
                    Action = option.action,
                    TargetPageId = option.targetPageId,
                    AddCurrentPageToHistory = option.addCurrentPageToHistory
                });
            }
        }

        if (activePage.includeBackOption && !IsActivePageStartupPage())
        {
            renderedOptions.Add(new RenderedTerminalOption
            {
                Label = FormatOptionLabel(activePage.backOptionLabel),
                Action = TerminalOptionAction.Back
            });
        }

        if (activePage.includeExitOption)
        {
            renderedOptions.Add(new RenderedTerminalOption
            {
                Label = FormatOptionLabel(activePage.exitOptionLabel),
                Action = TerminalOptionAction.Close
            });
        }
    }


    private void CreateOptionButtons()
    {
        for (int i = 0; i < renderedOptions.Count; i++)
        {
            int optionIndex = i;
            Button button = optionButtonPrefab
                ? Instantiate(optionButtonPrefab, runtimeContentRoot)
                : CreateGeneratedOptionButton(runtimeContentRoot);

            button.name = "TerminalOption_" + i;
            Image hoverRectangle = ConfigureOptionButton(button);
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => SelectOption(optionIndex));

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);

            EventTrigger trigger = button.GetComponent<EventTrigger>();
            if (!trigger)
                trigger = button.gameObject.AddComponent<EventTrigger>();

            AddPointerEnterTrigger(trigger, optionIndex);
            AddPointerExitTrigger(trigger, optionIndex);

            spawnedOptionButtons.Add(button);
            spawnedOptionHoverRectangles.Add(hoverRectangle);
            spawnedOptionLabels.Add(label);
            spawnedOptionInteractionReady.Add(false);
            SetOptionInteractionReady(optionIndex, false);

            if (label)
                ConfigureOptionLabel(label, renderedOptions[i].Label, optionIndex);
            else
                SetOptionInteractionReady(optionIndex, true);
        }

        Canvas.ForceUpdateCanvases();

        if (runtimeContentRoot)
            LayoutRebuilder.ForceRebuildLayoutImmediate(runtimeContentRoot);
    }


    private Image ConfigureOptionButton(Button button)
    {
        if (!button)
            return null;

        button.interactable = true;
        button.transition = Selectable.Transition.None;

        RectTransform buttonRect = button.transform as RectTransform;
        if (buttonRect)
        {
            buttonRect.anchorMin = new Vector2(0.0f, 1.0f);
            buttonRect.anchorMax = new Vector2(1.0f, 1.0f);
            buttonRect.pivot = new Vector2(0.5f, 1.0f);
            buttonRect.sizeDelta = new Vector2(0.0f, optionHeight);
        }

        Image hoverRectangle = EnsureOptionHoverRectangle(button);

        if (hoverRectangle)
        {
            hoverRectangle.color = Color.clear;
            hoverRectangle.raycastTarget = false;
        }

        Graphic targetGraphic = button.targetGraphic;
        if (!targetGraphic || targetGraphic == hoverRectangle)
        {
            targetGraphic = button.GetComponent<Graphic>();
            if (!targetGraphic)
                targetGraphic = button.gameObject.AddComponent<Image>();

            button.targetGraphic = targetGraphic;
        }

        if (targetGraphic)
        {
            targetGraphic.color = Color.clear;
            targetGraphic.raycastTarget = true;
        }

        LayoutElement layoutElement = button.GetComponent<LayoutElement>();
        if (!layoutElement)
            layoutElement = button.gameObject.AddComponent<LayoutElement>();

        layoutElement.minHeight = optionHeight;
        layoutElement.preferredHeight = optionHeight;
        layoutElement.flexibleWidth = 1.0f;

        return hoverRectangle;
    }


    private Button CreateGeneratedOptionButton(Transform parent)
    {
        GameObject buttonObject = new GameObject("TerminalOption", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.transform as RectTransform;
        if (buttonRect)
        {
            buttonRect.anchorMin = new Vector2(0.0f, 1.0f);
            buttonRect.anchorMax = new Vector2(1.0f, 1.0f);
            buttonRect.pivot = new Vector2(0.5f, 1.0f);
            buttonRect.sizeDelta = new Vector2(0.0f, optionHeight);
        }

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.0f, 0.0f, 0.0f, 0.0f);

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.None;

        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.0f, 0.0f, 0.0f, 0.0f);
        colors.highlightedColor = new Color(1.0f, 1.0f, 1.0f, 0.08f);
        colors.selectedColor = new Color(1.0f, 1.0f, 1.0f, 0.12f);
        colors.pressedColor = new Color(1.0f, 1.0f, 1.0f, 0.18f);
        button.colors = colors;

        LayoutElement layoutElement = buttonObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = optionHeight;
        layoutElement.preferredHeight = optionHeight;

        GameObject labelObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(buttonObject.transform, false);

        RectTransform labelRect = labelObject.transform as RectTransform;
        if (labelRect)
        {
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        return button;
    }


    private void SelectOption(int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= renderedOptions.Count)
            return;

        if (!IsOptionInteractionReady(optionIndex))
            return;

        if (IsTextWriteAnimationActive() || lastTextWriteAnimationCompletedFrame >= Time.frameCount)
            return;

        if (lastOptionSelectFrame == Time.frameCount)
            return;

        lastOptionSelectFrame = Time.frameCount;
        selectedOptionIndex = optionIndex;
        UpdateSelectedOptionVisuals();

        RenderedTerminalOption renderedOption = renderedOptions[optionIndex];
        TerminalOption sourceOption = renderedOption.Option;
        sourceOption?.onSelected?.Invoke();

        bool hasPromptMessage = sourceOption != null && !string.IsNullOrWhiteSpace(sourceOption.promptMessage);
        if (hasPromptMessage)
            ShowPromptMessage(sourceOption.promptMessage);
        else
            HidePromptMessageImmediate();

        switch (renderedOption.Action)
        {
            case TerminalOptionAction.Navigate:
                NavigateToPage(renderedOption.TargetPageId, renderedOption.AddCurrentPageToHistory);
                break;

            case TerminalOptionAction.Back:
                GoBackOrClose(false);
                break;

            case TerminalOptionAction.Close:
                Close();
                break;

            case TerminalOptionAction.InvokeEvent:
                break;

            case TerminalOptionAction.PromptMessage:
                break;
        }
    }


    private void NavigateToPage(string pageId, bool addCurrentPageToHistory)
    {
        if (activeDocument == null || !activeDocument.TryGetPage(pageId, out TerminalPage targetPage))
        {
            RenderMissingPage("Missing terminal page: " + pageId);
            return;
        }

        ShowPage(targetPage, addCurrentPageToHistory);
    }


    private bool IsActivePageStartupPage()
    {
        if (activeDocument == null || activePage == null)
            return false;

        string activePageId = string.IsNullOrWhiteSpace(activePage.pageId) ? string.Empty : activePage.pageId.Trim();
        string startupPageId = string.IsNullOrWhiteSpace(activeDocument.startupPageId) ? string.Empty : activeDocument.startupPageId.Trim();

        if (!string.IsNullOrEmpty(startupPageId))
            return string.Equals(activePageId, startupPageId, StringComparison.OrdinalIgnoreCase);

        return activeDocument.pages != null &&
            activeDocument.pages.Count > 0 &&
            ReferenceEquals(activeDocument.pages[0], activePage);
    }


    private void GoBackOrClose(bool closeWhenNoHistory)
    {
        if (pageHistory.Count == 0)
        {
            if (closeWhenNoHistory)
                Close();

            return;
        }

        string previousPageId = pageHistory[pageHistory.Count - 1];
        pageHistory.RemoveAt(pageHistory.Count - 1);

        if (activeDocument != null && activeDocument.TryGetPage(previousPageId, out TerminalPage previousPage))
        {
            ShowPage(previousPage, false);
            return;
        }

        if (closeWhenNoHistory)
            Close();
    }


    private void HandleBodyPaginationInput()
    {
        if (!isAwaitingBodyContinuationClick ||
            ignoreInputUntilFrame >= Time.frameCount ||
            bodyContinuationInputAllowedFrame >= Time.frameCount ||
            IsTextWriteAnimationActive() ||
            lastTextWriteAnimationCompletedFrame >= Time.frameCount)
        {
            return;
        }

        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasReleasedThisFrame)
            return;

        bodyPageStartIndex = nextBodyPageStartIndex;
        RebuildPage();
    }


    private void HandleKeyboardInput()
    {
        if (ignoreInputUntilFrame >= Time.frameCount)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        if (keyboard.eKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        if (closeOnCancel && keyboard.escapeKey.wasPressedThisFrame)
        {
            Close();
            return;
        }

        if (IsTextWriteAnimationActive() || lastTextWriteAnimationCompletedFrame >= Time.frameCount)
            return;

        if (keyboard.backspaceKey.wasPressedThisFrame)
        {
            GoBackOrClose(false);
            return;
        }

        if (isAwaitingBodyContinuationClick)
            return;

        if (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame)
        {
            MoveSelection(-1);
            return;
        }

        if (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame)
        {
            MoveSelection(1);
            return;
        }

        if (keyboard.enterKey.wasPressedThisFrame ||
            keyboard.numpadEnterKey.wasPressedThisFrame ||
            keyboard.spaceKey.wasPressedThisFrame)
        {
            if (selectedOptionIndex >= 0)
                SelectOption(selectedOptionIndex);
        }
    }


    private void MoveSelection(int delta)
    {
        if (renderedOptions.Count == 0)
            return;

        if (selectedOptionIndex < 0)
            selectedOptionIndex = 0;
        else
            selectedOptionIndex = (selectedOptionIndex + delta + renderedOptions.Count) % renderedOptions.Count;

        UpdateSelectedOptionVisuals();
    }


    private void SelectFirstOption()
    {
        if (spawnedOptionButtons.Count == 0)
        {
            hoveredOptionIndex = -1;
            selectedOptionIndex = -1;

            if (firstSelectedUIObject && EventSystem.current)
                EventSystem.current.SetSelectedGameObject(firstSelectedUIObject);

            return;
        }

        selectedOptionIndex = 0;
        UpdateSelectedOptionVisuals();
    }


    private void UpdateSelectedOptionVisuals()
    {
        for (int i = 0; i < spawnedOptionHoverRectangles.Count; i++)
        {
            Image hoverRectangle = spawnedOptionHoverRectangles[i];
            if (!hoverRectangle)
                continue;

            hoverRectangle.color = i == hoveredOptionIndex && IsOptionInteractionReady(i)
                ? hoveredOptionRectangleColor
                : Color.clear;
            hoverRectangle.raycastTarget = false;
        }

        for (int i = 0; i < spawnedOptionLabels.Count; i++)
        {
            TMP_Text label = spawnedOptionLabels[i];
            if (!label)
                continue;

            if (i == hoveredOptionIndex && IsOptionInteractionReady(i))
                label.color = hoveredOptionTextColor;
            else
                label.color = i == selectedOptionIndex ? selectedOptionTextColor : terminalTextColor;
        }

        if (selectedOptionIndex >= 0 &&
            selectedOptionIndex < spawnedOptionButtons.Count &&
            EventSystem.current)
        {
            EventSystem.current.SetSelectedGameObject(spawnedOptionButtons[selectedOptionIndex].gameObject);
        }
    }


    private bool IsOptionInteractionReady(int optionIndex)
    {
        return optionIndex >= 0 &&
            optionIndex < spawnedOptionInteractionReady.Count &&
            spawnedOptionInteractionReady[optionIndex];
    }


    private void SetOptionInteractionReady(int optionIndex, bool ready)
    {
        if (optionIndex < 0 || optionIndex >= spawnedOptionInteractionReady.Count)
            return;

        spawnedOptionInteractionReady[optionIndex] = ready;

        if (optionIndex < spawnedOptionButtons.Count)
        {
            Button button = spawnedOptionButtons[optionIndex];
            if (button)
                button.interactable = ready;
        }

        if (!ready && hoveredOptionIndex == optionIndex)
            hoveredOptionIndex = -1;

        UpdateSelectedOptionVisuals();
    }


    private void ClearRuntimeContent()
    {
        spawnedOptionButtons.Clear();
        spawnedOptionHoverRectangles.Clear();
        spawnedOptionLabels.Clear();
        spawnedOptionInteractionReady.Clear();
        hoveredOptionIndex = -1;

        if (!runtimeContentRoot)
            return;

        for (int i = runtimeContentRoot.childCount - 1; i >= 0; i--)
            Destroy(runtimeContentRoot.GetChild(i).gameObject);
    }


    private void ShowPromptMessage(string message)
    {
        if (!promptMessageText)
            promptMessageText = FindChildComponentByName<TMP_Text>(PromptMessageTextName);

        if (!promptMessageText || string.IsNullOrWhiteSpace(message))
            return;

        ConfigurePromptMessageText();
        string trimmedMessage = message.Trim();
        promptMessageText.text = trimmedMessage;
        promptMessageText.gameObject.SetActive(true);
        RegisterTextWriteAnimation(promptMessageText);
        promptMessageVisible = true;
        promptMessageHideUnscaledTime = Time.unscaledTime +
            Mathf.Max(0.0f, promptMessageDurationSeconds) +
            GetEstimatedTextWriteDuration(trimmedMessage, textWriteCharactersPerSecond);
        UpdateTerminalCursorBlink(true);
    }


    private void UpdatePromptMessageVisibility()
    {
        if (!promptMessageVisible)
            return;

        if (Time.unscaledTime >= promptMessageHideUnscaledTime)
            HidePromptMessageImmediate();
    }


    private void HidePromptMessageImmediate()
    {
        promptMessageVisible = false;
        promptMessageHideUnscaledTime = 0.0f;

        if (!promptMessageText)
        {
            UpdateTerminalCursorBlink(true);
            return;
        }

        promptMessageText.text = string.Empty;

        if (promptMessageText.gameObject.activeSelf)
            promptMessageText.gameObject.SetActive(false);

        UpdateTerminalCursorBlink(true);
    }


    private void UpdateTextWriteAnimations()
    {
        if (!animateTextOnAppear)
        {
            CompleteTextWriteAnimations();
            return;
        }

        TextWriteAnimation animation = GetCurrentTextWriteAnimation();
        if (animation == null)
            return;

        float charactersAvailable = Mathf.Max(1.0f, animation.CharactersPerSecond) * Time.unscaledDeltaTime;
        if (charactersAvailable <= 0.0f)
            return;

        float totalAvailable = animation.CharacterAccumulator + charactersAvailable;
        int charactersToReveal = Mathf.FloorToInt(totalAvailable);
        animation.CharacterAccumulator = totalAvailable - charactersToReveal;

        if (charactersToReveal <= 0)
            return;

        int currentVisibleCharacters = Mathf.Clamp(animation.Text.maxVisibleCharacters, 0, animation.TotalVisibleCharacters);
        int remainingCharacters = animation.TotalVisibleCharacters - currentVisibleCharacters;
        int appliedCharacters = Mathf.Min(remainingCharacters, charactersToReveal);

        animation.Text.maxVisibleCharacters = currentVisibleCharacters + appliedCharacters;

        if (animation.Text.maxVisibleCharacters < animation.TotalVisibleCharacters)
            return;

        CompleteActiveTextWriteAnimation();
    }


    private TextWriteAnimation GetCurrentTextWriteAnimation()
    {
        if (IsValidTextWriteAnimation(activeTextWriteAnimation))
            return activeTextWriteAnimation;

        activeTextWriteAnimation = null;

        while (pendingTextWriteAnimations.Count > 0)
        {
            TextWriteAnimation candidate = pendingTextWriteAnimations.Dequeue();
            if (!IsValidTextWriteAnimation(candidate))
                continue;

            activeTextWriteAnimation = candidate;
            return activeTextWriteAnimation;
        }

        return null;
    }


    private bool IsTextWriteAnimationActive()
    {
        return GetCurrentTextWriteAnimation() != null;
    }


    private static bool IsValidTextWriteAnimation(TextWriteAnimation animation)
    {
        return animation != null &&
            animation.Text &&
            animation.Text.gameObject &&
            animation.Text.gameObject.activeInHierarchy &&
            animation.TotalVisibleCharacters > 0;
    }


    private void CompleteActiveTextWriteAnimation()
    {
        CompleteTextWriteAnimation(activeTextWriteAnimation);

        activeTextWriteAnimation = null;
        lastTextWriteAnimationCompletedFrame = Time.frameCount;
    }


    private void CompleteTextWriteAnimations()
    {
        CompleteTextWriteAnimation(activeTextWriteAnimation);

        activeTextWriteAnimation = null;

        while (pendingTextWriteAnimations.Count > 0)
        {
            TextWriteAnimation pendingAnimation = pendingTextWriteAnimations.Dequeue();
            CompleteTextWriteAnimation(pendingAnimation);
        }
    }


    private static void CompleteTextWriteAnimation(TextWriteAnimation animation)
    {
        if (animation == null)
            return;

        if (animation.Text)
            ShowTextImmediately(animation.Text);

        animation.OnCompleted?.Invoke();
    }


    private void ResetTextWriteAnimationState(bool clearPersistentKeys)
    {
        CompleteTextWriteAnimations();
        lastTextWriteAnimationCompletedFrame = -1;

        if (clearPersistentKeys)
            animatedPersistentTextKeys.Clear();
    }


    private void RegisterFrameTextWriteAnimations()
    {
        RectTransform terminalFrameRoot = ResolveTerminalFrameRoot();
        if (!terminalFrameRoot)
            return;

        TMP_Text[] frameTexts = terminalFrameRoot.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text operatingText = null;

        for (int i = 0; i < frameTexts.Length; i++)
        {
            TMP_Text frameText = frameTexts[i];
            if (!frameText || frameText == promptMessageText)
                continue;

            if (string.Equals(frameText.name, OperatingTextName, StringComparison.Ordinal))
            {
                operatingText = frameText;
                continue;
            }

            RegisterFrameTextWriteAnimation(frameText, terminalFrameRoot);
        }

        if (operatingText)
            RegisterFrameTextWriteAnimation(operatingText, terminalFrameRoot);
    }


    private void RegisterFrameTextWriteAnimation(TMP_Text frameText, RectTransform frameRoot)
    {
        if (!frameText)
            return;

        if (!frameText.gameObject.activeInHierarchy)
        {
            ShowTextImmediately(frameText);
            return;
        }

        RegisterTextWriteAnimation(
            frameText,
            GetPersistentTextAnimationKey("Frame", GetTransformPath(frameText.transform, frameRoot)),
            false,
            frameTextWriteCharactersPerSecond);
    }


    private void RegisterTextWriteAnimation(
        TMP_Text text,
        string persistentAnimationKey = "",
        bool useBodyWriteSpeed = false,
        float customCharactersPerSecond = -1.0f,
        Action onCompleted = null)
    {
        if (!text)
            return;

        if (!animateTextOnAppear ||
            !isOpen ||
            string.IsNullOrEmpty(text.text) ||
            HasPersistentTextAlreadyAnimated(persistentAnimationKey))
        {
            ShowTextImmediately(text);
            onCompleted?.Invoke();
            return;
        }

        MarkPersistentTextAnimated(persistentAnimationKey);
        text.ForceMeshUpdate(true, true);

        int totalVisibleCharacters = text.textInfo.characterCount;
        if (totalVisibleCharacters <= 0)
        {
            ShowTextImmediately(text);
            onCompleted?.Invoke();
            return;
        }

        text.maxVisibleCharacters = 0;
        pendingTextWriteAnimations.Enqueue(new TextWriteAnimation
        {
            Text = text,
            TotalVisibleCharacters = totalVisibleCharacters,
            CharactersPerSecond = GetTextWriteCharactersPerSecond(useBodyWriteSpeed, customCharactersPerSecond),
            OnCompleted = onCompleted
        });
    }


    private bool HasPersistentTextAlreadyAnimated(string persistentAnimationKey)
    {
        return !string.IsNullOrEmpty(persistentAnimationKey) &&
            animatedPersistentTextKeys.Contains(persistentAnimationKey);
    }


    private void MarkPersistentTextAnimated(string persistentAnimationKey)
    {
        if (!string.IsNullOrEmpty(persistentAnimationKey))
            animatedPersistentTextKeys.Add(persistentAnimationKey);
    }


    private float GetTextWriteCharactersPerSecond(bool useBodyWriteSpeed, float customCharactersPerSecond = -1.0f)
    {
        if (customCharactersPerSecond > 0.0f)
            return Mathf.Max(1.0f, customCharactersPerSecond);

        return Mathf.Max(1.0f, useBodyWriteSpeed ? bodyTextWriteCharactersPerSecond : textWriteCharactersPerSecond);
    }


    private float GetEstimatedTextWriteDuration(string text, float charactersPerSecond)
    {
        if (!animateTextOnAppear || string.IsNullOrEmpty(text))
            return 0.0f;

        return text.Length / Mathf.Max(1.0f, charactersPerSecond);
    }


    private static void ShowTextImmediately(TMP_Text text)
    {
        if (text)
            text.maxVisibleCharacters = int.MaxValue;
    }


    private static string GetPersistentTextAnimationKey(string scope, string text)
    {
        return scope + ":" + (text ?? string.Empty);
    }


    private static string GetTransformPath(Transform transform, Transform root)
    {
        if (!transform)
            return string.Empty;

        if (!root || transform == root)
            return transform.name;

        Stack<string> pathParts = new Stack<string>();
        Transform current = transform;

        while (current && current != root)
        {
            pathParts.Push(current.name);
            current = current.parent;
        }

        if (current == root)
            pathParts.Push(root.name);

        return string.Join("/", pathParts);
    }


    private TMP_Text CreateTextElement(
        string objectName,
        string text,
        float fontSize,
        FontStyles fontStyle,
        float maxHeight = -1.0f,
        string persistentAnimationKey = "",
        bool useBodyWriteSpeed = false)
    {
        if (maxHeight > 0.0f)
        {
            return CreateClippedTextElement(
                objectName,
                text,
                fontSize,
                fontStyle,
                maxHeight,
                persistentAnimationKey,
                useBodyWriteSpeed);
        }

        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(runtimeContentRoot, false);

        TMP_Text tmpText = textObject.GetComponent<TMP_Text>();
        ConfigureBaseText(tmpText, fontSize, fontStyle);
        tmpText.text = text;
        tmpText.raycastTarget = false;

        ContentSizeFitter fitter = textObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        RegisterTextWriteAnimation(tmpText, persistentAnimationKey, useBodyWriteSpeed);
        return tmpText;
    }


    private TMP_Text CreateClippedTextElement(
        string objectName,
        string text,
        float fontSize,
        FontStyles fontStyle,
        float maxHeight,
        string persistentAnimationKey,
        bool useBodyWriteSpeed)
    {
        float textWidth = GetRuntimeContentTextWidth();
        float preferredHeight = MeasureTextHeight(text, fontSize, fontStyle, textWidth);
        float clampedHeight = Mathf.Min(Mathf.Max(1.0f, maxHeight), Mathf.Max(1.0f, preferredHeight));

        GameObject containerObject = new GameObject(objectName, typeof(RectTransform), typeof(RectMask2D));
        containerObject.transform.SetParent(runtimeContentRoot, false);

        RectTransform containerRect = containerObject.transform as RectTransform;
        if (containerRect)
        {
            containerRect.anchorMin = new Vector2(0.0f, 1.0f);
            containerRect.anchorMax = new Vector2(1.0f, 1.0f);
            containerRect.pivot = new Vector2(0.5f, 1.0f);
            containerRect.sizeDelta = new Vector2(0.0f, clampedHeight);
        }

        LayoutElement layoutElement = containerObject.AddComponent<LayoutElement>();
        layoutElement.minHeight = clampedHeight;
        layoutElement.preferredHeight = clampedHeight;
        layoutElement.flexibleHeight = 0.0f;

        GameObject textObject = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(containerObject.transform, false);

        RectTransform textRect = textObject.transform as RectTransform;
        if (textRect)
        {
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        TMP_Text tmpText = textObject.GetComponent<TMP_Text>();
        ConfigureBaseText(tmpText, fontSize, fontStyle);
        tmpText.text = text;
        tmpText.overflowMode = TextOverflowModes.Truncate;
        tmpText.raycastTarget = false;

        RegisterTextWriteAnimation(tmpText, persistentAnimationKey, useBodyWriteSpeed);
        return tmpText;
    }


    private void CreateTitleUnderline(TMP_Text titleText)
    {
        if (!titleText || !runtimeContentRoot)
            return;

        GameObject rowObject = new GameObject("TerminalTitleUnderline", typeof(RectTransform));
        rowObject.transform.SetParent(runtimeContentRoot, false);

        RectTransform rowRect = rowObject.transform as RectTransform;
        if (rowRect)
        {
            rowRect.anchorMin = new Vector2(0.0f, 1.0f);
            rowRect.anchorMax = new Vector2(1.0f, 1.0f);
            rowRect.pivot = new Vector2(0.5f, 1.0f);
            rowRect.sizeDelta = new Vector2(0.0f, titleUnderlineHeight);
        }

        LayoutElement rowLayoutElement = rowObject.AddComponent<LayoutElement>();
        rowLayoutElement.minHeight = titleUnderlineHeight;
        rowLayoutElement.preferredHeight = titleUnderlineHeight;

        GameObject lineObject = new GameObject("Line", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        lineObject.transform.SetParent(rowObject.transform, false);

        RectTransform lineRect = lineObject.transform as RectTransform;
        if (lineRect)
        {
            float lineWidth = GetTitleUnderlineWidth(titleText);
            lineRect.anchorMin = new Vector2(0.0f, 0.5f);
            lineRect.anchorMax = new Vector2(0.0f, 0.5f);
            lineRect.pivot = new Vector2(0.0f, 0.5f);
            lineRect.anchoredPosition = Vector2.zero;
            lineRect.sizeDelta = new Vector2(lineWidth, titleUnderlineHeight);
        }

        Image lineImage = lineObject.GetComponent<Image>();
        lineImage.color = terminalTextColor;
        lineImage.raycastTarget = false;
    }


    private void CreateBodyOptionsSpacer()
    {
        if (!runtimeContentRoot)
            return;

        float spacerHeight = GetBodyOptionsSpacerHeight();

        GameObject spacerObject = new GameObject(BodyOptionsSpacerName, typeof(RectTransform));
        spacerObject.transform.SetParent(runtimeContentRoot, false);

        RectTransform spacerRect = spacerObject.transform as RectTransform;
        if (spacerRect)
        {
            spacerRect.anchorMin = new Vector2(0.0f, 1.0f);
            spacerRect.anchorMax = new Vector2(1.0f, 1.0f);
            spacerRect.pivot = new Vector2(0.5f, 1.0f);
            spacerRect.sizeDelta = new Vector2(0.0f, spacerHeight);
        }

        LayoutElement spacerLayoutElement = spacerObject.AddComponent<LayoutElement>();
        spacerLayoutElement.minHeight = spacerHeight;
        spacerLayoutElement.preferredHeight = spacerHeight;
        spacerLayoutElement.flexibleHeight = 0.0f;
    }


    private bool BodyAndOptionsFitOnCurrentPage(string body, float textWidth, float availableBodyHeight)
    {
        if (string.IsNullOrWhiteSpace(body))
            return true;

        int maxLineCount = GetMaxVisibleTextLineCount(availableBodyHeight, bodyFontSize, FontStyles.Normal, textWidth);
        if (maxLineCount <= 0)
            return false;

        return GetRenderedTextLineCount(body, bodyFontSize, FontStyles.Normal, textWidth) <= maxLineCount;
    }


    private int FindBodyPageEndIndex(string body, int startIndex, float maxHeight, float textWidth)
    {
        if (string.IsNullOrEmpty(body))
            return 0;

        startIndex = Mathf.Clamp(startIndex, 0, body.Length);
        if (startIndex >= body.Length)
            return body.Length;

        int maxLineCount = GetMaxVisibleTextLineCount(maxHeight, bodyFontSize, FontStyles.Normal, textWidth);
        if (maxLineCount <= 0)
            return Mathf.Min(startIndex + 1, body.Length);

        TMP_Text measurementText = EnsureBodyMeasurementText();
        if (!measurementText)
            return Mathf.Min(startIndex + 1, body.Length);

        string remainingBody = body.Substring(startIndex).TrimStart();
        int skippedLeadingWhitespace = body.Length - startIndex - remainingBody.Length;
        if (string.IsNullOrEmpty(remainingBody))
            return body.Length;

        ConfigureTextForMeasurement(measurementText, bodyFontSize, FontStyles.Normal, textWidth, 100000.0f);
        measurementText.text = remainingBody;
        measurementText.ForceMeshUpdate(true, true);

        TMP_TextInfo textInfo = measurementText.textInfo;
        if (textInfo.lineCount <= 0)
            return Mathf.Min(startIndex + 1, body.Length);

        int targetLineIndex = Mathf.Min(maxLineCount, textInfo.lineCount) - 1;
        TMP_LineInfo targetLine = textInfo.lineInfo[targetLineIndex];
        int visibleEndIndex = targetLine.lastCharacterIndex + 1;

        if (visibleEndIndex < remainingBody.Length)
        {
            int wordBreakIndex = FindLastWhitespaceBreak(remainingBody, 0, visibleEndIndex);
            if (wordBreakIndex > 0)
                visibleEndIndex = wordBreakIndex;
        }

        int endIndex = startIndex + skippedLeadingWhitespace + visibleEndIndex;
        return Mathf.Clamp(endIndex, startIndex + 1, body.Length);
    }


    private static int FindLastWhitespaceBreak(string text, int startIndex, int endIndex)
    {
        int safeEndIndex = Mathf.Clamp(endIndex, startIndex + 1, text.Length);

        for (int i = safeEndIndex - 1; i > startIndex; i--)
        {
            if (char.IsWhiteSpace(text[i]))
                return i + 1;
        }

        return endIndex;
    }


    private static int SkipLeadingWhitespace(string text, int startIndex)
    {
        int index = Mathf.Clamp(startIndex, 0, string.IsNullOrEmpty(text) ? 0 : text.Length);

        while (!string.IsNullOrEmpty(text) && index < text.Length && char.IsWhiteSpace(text[index]))
            index++;

        return index;
    }


    private float GetAvailableBodyOnlyHeight(string title, float textWidth)
    {
        return Mathf.Max(0.0f, GetRuntimeContentInnerHeight() - GetHeaderHeight(title, textWidth));
    }


    private float GetAvailableFinalBodyHeight(string title, float textWidth, int optionCount)
    {
        float availableHeight = GetAvailableBodyOnlyHeight(title, textWidth);
        if (optionCount <= 0)
            return availableHeight;

        float spacing = GetRuntimeElementSpacing();
        float optionsHeight = GetOptionsHeight(optionCount);
        return availableHeight - spacing - GetBodyOptionsSpacerHeight() - spacing - optionsHeight;
    }


    private float GetHeaderHeight(string title, float textWidth)
    {
        if (string.IsNullOrWhiteSpace(title))
            return 0.0f;

        float spacing = GetRuntimeElementSpacing();
        return MeasureTextHeight(title, titleFontSize, FontStyles.UpperCase, textWidth) + spacing + titleUnderlineHeight + spacing;
    }


    private float GetOptionsHeight(int optionCount)
    {
        if (optionCount <= 0)
            return 0.0f;

        float spacing = GetRuntimeElementSpacing();
        return (optionCount * optionHeight) + ((optionCount - 1) * spacing);
    }


    private float GetBodyOptionsSpacerHeight()
    {
        float desiredGapHeight = optionHeight + HoveredOptionRectangleExtraHeight;
        return Mathf.Max(0.0f, desiredGapHeight - (GetRuntimeElementSpacing() * 2.0f));
    }


    private float GetRuntimeContentTextWidth()
    {
        RectOffset padding = GetRuntimeContentPadding();
        return Mathf.Max(1.0f, GetRuntimeContentViewportWidth() - padding.left - padding.right);
    }


    private float GetRuntimeContentInnerHeight()
    {
        RectOffset padding = GetRuntimeContentPadding();
        return Mathf.Max(1.0f, GetRuntimeContentViewportHeight() - padding.top - padding.bottom);
    }


    private float GetRuntimeContentViewportWidth()
    {
        Canvas.ForceUpdateCanvases();

        RectTransform viewportRect = contentRoot ? contentRoot : runtimeContentRoot ? runtimeContentRoot.parent as RectTransform : null;
        if (viewportRect && viewportRect.rect.width > 0.0f)
            return viewportRect.rect.width;

        RectTransform terminalRect = transform as RectTransform;
        if (terminalRect && terminalRect.rect.width > 0.0f)
            return terminalRect.rect.width;

        return 1000.0f;
    }


    private float GetRuntimeContentViewportHeight()
    {
        Canvas.ForceUpdateCanvases();

        RectTransform viewportRect = contentRoot ? contentRoot : runtimeContentRoot ? runtimeContentRoot.parent as RectTransform : null;
        if (viewportRect && viewportRect.rect.height > 0.0f)
            return viewportRect.rect.height;

        RectTransform terminalRect = transform as RectTransform;
        if (terminalRect && terminalRect.rect.height > 0.0f)
            return terminalRect.rect.height;

        return 600.0f;
    }


    private RectOffset GetRuntimeContentPadding()
    {
        VerticalLayoutGroup layoutGroup = runtimeContentRoot ? runtimeContentRoot.GetComponent<VerticalLayoutGroup>() : null;
        if (layoutGroup != null)
            return layoutGroup.padding;

        int horizontalPadding = Mathf.RoundToInt(Mathf.Max(0.0f, generatedContentPadding.x));
        int verticalPadding = Mathf.RoundToInt(Mathf.Max(0.0f, generatedContentPadding.y));
        return new RectOffset(horizontalPadding, horizontalPadding, verticalPadding, verticalPadding);
    }


    private float GetRuntimeElementSpacing()
    {
        VerticalLayoutGroup layoutGroup = runtimeContentRoot ? runtimeContentRoot.GetComponent<VerticalLayoutGroup>() : null;
        return layoutGroup != null ? Mathf.Max(0.0f, layoutGroup.spacing) : Mathf.Max(0.0f, generatedElementSpacing);
    }


    private float MeasureTextHeight(string text, float fontSize, FontStyles fontStyle, float textWidth)
    {
        if (string.IsNullOrEmpty(text))
            return 0.0f;

        TMP_Text measurementText = EnsureBodyMeasurementText();
        if (!measurementText)
            return 0.0f;

        ConfigureTextForMeasurement(measurementText, fontSize, fontStyle, textWidth, 100000.0f);
        measurementText.text = text;
        measurementText.ForceMeshUpdate(true, true);

        Vector2 preferredValues = measurementText.GetPreferredValues(text, Mathf.Max(1.0f, textWidth), float.PositiveInfinity);
        return Mathf.Max(0.0f, preferredValues.y);
    }


    private int GetRenderedTextLineCount(string text, float fontSize, FontStyles fontStyle, float textWidth)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        TMP_Text measurementText = EnsureBodyMeasurementText();
        if (!measurementText)
            return 0;

        ConfigureTextForMeasurement(measurementText, fontSize, fontStyle, textWidth, 100000.0f);
        measurementText.text = text;
        measurementText.ForceMeshUpdate(true, true);
        return measurementText.textInfo.lineCount;
    }


    private int GetMaxVisibleTextLineCount(float maxHeight, float fontSize, FontStyles fontStyle, float textWidth)
    {
        if (maxHeight <= 0.0f)
            return 0;

        float lineHeight = GetMeasuredLineHeight(fontSize, fontStyle, textWidth);
        if (lineHeight <= 0.0f)
            return 0;

        return Mathf.Max(1, Mathf.FloorToInt((maxHeight + 0.01f) / lineHeight));
    }


    private float GetMeasuredLineHeight(float fontSize, FontStyles fontStyle, float textWidth)
    {
        TMP_Text measurementText = EnsureBodyMeasurementText();
        if (!measurementText)
            return fontSize;

        ConfigureTextForMeasurement(measurementText, fontSize, fontStyle, textWidth, 100000.0f);
        measurementText.text = "A\nA";
        measurementText.ForceMeshUpdate(true, true);

        TMP_TextInfo textInfo = measurementText.textInfo;
        if (textInfo.lineCount > 0 && textInfo.lineInfo[0].lineHeight > 0.0f)
            return textInfo.lineInfo[0].lineHeight;

        return Mathf.Max(1.0f, fontSize);
    }


    private void ConfigureTextForMeasurement(TMP_Text text, float fontSize, FontStyles fontStyle, float textWidth, float textHeight)
    {
        ConfigureBaseText(text, fontSize, fontStyle);
        text.color = Color.clear;
        text.overflowMode = TextOverflowModes.Overflow;
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(1.0f, textWidth));
        text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight);
    }


    private TMP_Text EnsureBodyMeasurementText()
    {
        if (bodyMeasurementText)
            return bodyMeasurementText;

        GameObject measurementObject = new GameObject("TerminalBodyMeasurementText", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        measurementObject.hideFlags = HideFlags.HideAndDontSave;
        measurementObject.transform.SetParent(transform, false);

        RectTransform measurementRect = measurementObject.transform as RectTransform;
        if (measurementRect)
        {
            measurementRect.anchorMin = Vector2.zero;
            measurementRect.anchorMax = Vector2.zero;
            measurementRect.sizeDelta = Vector2.zero;
        }

        bodyMeasurementText = measurementObject.GetComponent<TMP_Text>();
        bodyMeasurementText.raycastTarget = false;
        return bodyMeasurementText;
    }


    private float GetTitleUnderlineWidth(TMP_Text titleText)
    {
        if (!titleText)
            return 0.0f;

        titleText.ForceMeshUpdate();
        float preferredWidth = titleText.GetPreferredValues(titleText.text).x;
        float contentWidth = runtimeContentRoot ? runtimeContentRoot.rect.width : preferredWidth;

        if (contentWidth <= 0.0f)
            contentWidth = preferredWidth;

        return Mathf.Max(0.0f, Mathf.Min(preferredWidth, contentWidth));
    }


    private void ConfigureOptionLabel(TMP_Text label, string text, int optionIndex)
    {
        ConfigureBaseText(label, optionFontSize, FontStyles.Normal);
        label.text = text;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        RegisterTextWriteAnimation(label, "", false, -1.0f, () => SetOptionInteractionReady(optionIndex, true));
    }


    private void ConfigureBaseText(TMP_Text text, float fontSize, FontStyles fontStyle)
    {
        if (!text)
            return;

        if (terminalFont)
            text.font = terminalFont;

        text.color = terminalTextColor;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
        text.alignment = TextAlignmentOptions.TopLeft;
    }


    private static Image EnsureOptionHoverRectangle(Button button)
    {
        if (!button)
            return null;

        Transform existingHoverRectangle = button.transform.Find(OptionHoverRectangleName);
        Image hoverRectangle = existingHoverRectangle
            ? existingHoverRectangle.GetComponent<Image>()
            : null;

        if (!hoverRectangle)
        {
            GameObject hoverRectangleObject = new GameObject(OptionHoverRectangleName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            hoverRectangleObject.transform.SetParent(button.transform, false);
            hoverRectangle = hoverRectangleObject.GetComponent<Image>();
        }

        RectTransform hoverRectangleRect = hoverRectangle.transform as RectTransform;
        if (hoverRectangleRect)
        {
            float verticalBleed = HoveredOptionRectangleExtraHeight * 0.5f;
            hoverRectangleRect.anchorMin = Vector2.zero;
            hoverRectangleRect.anchorMax = Vector2.one;
            hoverRectangleRect.pivot = new Vector2(0.5f, 0.5f);
            hoverRectangleRect.offsetMin = new Vector2(0.0f, -verticalBleed);
            hoverRectangleRect.offsetMax = new Vector2(0.0f, verticalBleed);
        }

        hoverRectangle.transform.SetAsFirstSibling();

        hoverRectangle.color = Color.clear;
        hoverRectangle.raycastTarget = false;
        return hoverRectangle;
    }


    private void SetHoveredOption(int optionIndex)
    {
        if (optionIndex < 0 || optionIndex >= renderedOptions.Count)
            return;

        if (!IsOptionInteractionReady(optionIndex))
            return;

        hoveredOptionIndex = optionIndex;
        selectedOptionIndex = optionIndex;
        UpdateSelectedOptionVisuals();
    }


    private void ClearHoveredOption(int optionIndex)
    {
        if (hoveredOptionIndex != optionIndex)
            return;

        hoveredOptionIndex = -1;
        UpdateSelectedOptionVisuals();
    }


    private void ConfigureRuntimeContentLayout()
    {
        if (!runtimeContentRoot)
            return;

        VerticalLayoutGroup layoutGroup = runtimeContentRoot.GetComponent<VerticalLayoutGroup>();
        if (!layoutGroup)
            layoutGroup = runtimeContentRoot.gameObject.AddComponent<VerticalLayoutGroup>();

        int horizontalPadding = Mathf.RoundToInt(Mathf.Max(0.0f, generatedContentPadding.x));
        int verticalPadding = Mathf.RoundToInt(Mathf.Max(0.0f, generatedContentPadding.y));
        layoutGroup.padding = new RectOffset(horizontalPadding, horizontalPadding, verticalPadding, verticalPadding);
        layoutGroup.spacing = Mathf.Max(0.0f, generatedElementSpacing);
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = true;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;

        ContentSizeFitter fitter = runtimeContentRoot.GetComponent<ContentSizeFitter>();
        if (!fitter)
            fitter = runtimeContentRoot.gameObject.AddComponent<ContentSizeFitter>();

        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }


    private void EnsureRuntimeContentClipping()
    {
        RectTransform clippingRoot = contentRoot ? contentRoot : runtimeContentRoot;
        if (!clippingRoot)
            return;

        RectMask2D rectMask = clippingRoot.GetComponent<RectMask2D>();
        if (!rectMask)
            clippingRoot.gameObject.AddComponent<RectMask2D>();
    }


    private RectTransform CreateGeneratedContentRootHost()
    {
        GameObject hostObject = new GameObject(DefaultContentRootName, typeof(RectTransform));
        hostObject.transform.SetParent(transform, false);

        RectTransform hostRect = hostObject.transform as RectTransform;
        if (hostRect)
        {
            hostRect.anchorMin = Vector2.zero;
            hostRect.anchorMax = Vector2.one;
            hostRect.offsetMin = Vector2.zero;
            hostRect.offsetMax = Vector2.zero;
        }

        return hostRect;
    }


    private RectTransform CreateRuntimeContentRoot(RectTransform parent)
    {
        GameObject rootObject = new GameObject(RuntimeContentRootName, typeof(RectTransform));
        rootObject.transform.SetParent(parent, false);

        RectTransform rootRect = rootObject.transform as RectTransform;
        if (rootRect)
        {
            rootRect.anchorMin = new Vector2(0.0f, 1.0f);
            rootRect.anchorMax = new Vector2(1.0f, 1.0f);
            rootRect.pivot = new Vector2(0.5f, 1.0f);
            rootRect.anchoredPosition = Vector2.zero;
            rootRect.sizeDelta = Vector2.zero;
        }

        return rootRect;
    }


    private CanvasGroup FindPlayerHudCanvasGroup()
    {
        UI.FalloutHUDController hudController = FindAnyObjectByType<UI.FalloutHUDController>(FindObjectsInactive.Include);
        if (!hudController)
            return null;

        CanvasGroup hudCanvasGroup = hudController.GetComponent<CanvasGroup>();
        if (!hudCanvasGroup)
            hudCanvasGroup = hudController.gameObject.AddComponent<CanvasGroup>();

        return hudCanvasGroup;
    }


    private void SetTerminalHierarchyActive(bool active)
    {
        if (!terminalCanvasGroup)
            return;

        GameObject terminalRoot = terminalCanvasGroup.gameObject;
        if (!terminalRoot)
            return;

        if (active)
        {
            EnsureTerminalRootHasVisibleScale(terminalRoot);

            Transform current = terminalRoot.transform;
            while (current)
            {
                GameObject currentObject = current.gameObject;
                if (!currentObject.activeSelf)
                    currentObject.SetActive(true);

                current = current.parent;
            }

            SetDirectChildrenActive(terminalRoot.transform, true);
            return;
        }

        if (terminalRoot.activeSelf)
            terminalRoot.SetActive(false);
    }


    private static void SetDirectChildrenActive(Transform rootTransform, bool active)
    {
        if (!rootTransform)
            return;

        int childCount = rootTransform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            Transform child = rootTransform.GetChild(i);
            if (!child)
                continue;

            GameObject childObject = child.gameObject;
            if (childObject.activeSelf != active)
                childObject.SetActive(active);
        }
    }


    private RectTransform FindChildRectTransformByName(string childName)
    {
        RectTransform[] rectTransforms = GetComponentsInChildren<RectTransform>(true);

        for (int i = 0; i < rectTransforms.Length; i++)
        {
            RectTransform rectTransform = rectTransforms[i];
            if (!rectTransform || rectTransform == transform)
                continue;

            if (!string.Equals(rectTransform.name, childName, StringComparison.Ordinal))
                continue;

            if (HasAncestorNamed(rectTransform, FrameObjectName))
                continue;

            return rectTransform;
        }

        return null;
    }


    private RectTransform ResolveTerminalFrameRoot()
    {
        if (!frameRoot)
        {
            RectTransform frameTransform = FindChildRectTransformByName(FrameObjectName);
            frameRoot = frameTransform ? frameTransform.gameObject : null;
        }

        return frameRoot ? frameRoot.transform as RectTransform : null;
    }


    private T FindChildComponentByName<T>(string childName) where T : Component
    {
        T[] components = GetComponentsInChildren<T>(true);

        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (!component || component.transform == transform)
                continue;

            if (string.Equals(component.name, childName, StringComparison.Ordinal))
                return component;
        }

        return null;
    }


    private static T FindChildComponentByNameInRoot<T>(string childName, Transform root) where T : Component
    {
        if (!root)
            return null;

        T[] components = root.GetComponentsInChildren<T>(true);

        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (!component)
                continue;

            if (string.Equals(component.name, childName, StringComparison.Ordinal))
                return component;
        }

        return null;
    }


    private static RectTransform FindDirectChildRectTransform(Transform parent, string childName)
    {
        if (!parent)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child && string.Equals(child.name, childName, StringComparison.Ordinal))
                return child as RectTransform;
        }

        return null;
    }


    private static bool HasAncestorNamed(Transform child, string ancestorName)
    {
        Transform current = child.parent;

        while (current)
        {
            if (string.Equals(current.name, ancestorName, StringComparison.Ordinal))
                return true;

            current = current.parent;
        }

        return false;
    }


    private static string FormatOptionLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
            return "> Continue";

        string trimmedLabel = label.Trim();
        return trimmedLabel.StartsWith(">", StringComparison.Ordinal) ? trimmedLabel : "> " + trimmedLabel;
    }


    private static void AddPointerEnterTrigger(EventTrigger trigger, int optionIndex)
    {
        if (!trigger)
            return;

        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };

        entry.callback.AddListener(eventData =>
        {
            TerminalController controller = trigger.GetComponentInParent<TerminalController>();
            if (controller)
                controller.SetHoveredOption(optionIndex);
        });

        trigger.triggers.Add(entry);
    }


    private static void AddPointerExitTrigger(EventTrigger trigger, int optionIndex)
    {
        if (!trigger)
            return;

        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerExit
        };

        entry.callback.AddListener(eventData =>
        {
            TerminalController controller = trigger.GetComponentInParent<TerminalController>();
            if (controller)
                controller.ClearHoveredOption(optionIndex);
        });

        trigger.triggers.Add(entry);
    }


    private void AddHackingPointerEnterTrigger(EventTrigger trigger, int targetIndex)
    {
        if (!trigger)
            return;

        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };

        entry.callback.AddListener(eventData => SetHoveredHackingTarget(targetIndex));
        trigger.triggers.Add(entry);
    }


    private void AddHackingPointerExitTrigger(EventTrigger trigger, int targetIndex)
    {
        if (!trigger)
            return;

        EventTrigger.Entry entry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerExit
        };

        entry.callback.AddListener(eventData => ClearHoveredHackingTarget(targetIndex, eventData));
        trigger.triggers.Add(entry);
    }


    private int ResolveHackingPasswordLength(Vector2Int lengthRange)
    {
        int minLength = Mathf.Max(1, Mathf.Min(lengthRange.x, lengthRange.y));
        int maxLength = Mathf.Max(minLength, Mathf.Max(lengthRange.x, lengthRange.y));
        return UnityEngine.Random.Range(minLength, maxLength + 1);
    }


    private List<string> GetHackingCandidateWords(int wordLength, int desiredWordCount, ISet<string> excludedWords)
    {
        List<string> candidates = new List<string>();
        string[] wordPool = GetHackingWordPool();
        int safeWordLength = Mathf.Max(1, wordLength);

        for (int i = 0; i < wordPool.Length; i++)
        {
            string word = wordPool[i];
            if (string.IsNullOrWhiteSpace(word) || word.Length != safeWordLength)
                continue;

            if (!candidates.Contains(word))
                candidates.Add(word);
        }

        Shuffle(candidates);

        if (excludedWords != null && excludedWords.Count > 0)
        {
            List<string> nonRepeatingCandidates = candidates.FindAll(word => !excludedWords.Contains(word));
            if (nonRepeatingCandidates.Count > 0)
                candidates = nonRepeatingCandidates;
        }

        candidates = SelectDissimilarHackingWords(candidates, Mathf.Max(desiredWordCount, desiredWordCount * 3));

        return candidates;
    }


    private static List<string> SelectDissimilarHackingWords(List<string> candidates, int maxCount)
    {
        List<string> selectedWords = new List<string>();
        if (candidates == null || maxCount <= 0)
            return selectedWords;

        for (int i = 0; i < candidates.Count && selectedWords.Count < maxCount; i++)
        {
            string candidate = candidates[i];
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            bool isTooSimilar = false;
            for (int selectedIndex = 0; selectedIndex < selectedWords.Count; selectedIndex++)
            {
                if (!AreHackingWordsSimilar(candidate, selectedWords[selectedIndex]))
                    continue;

                isTooSimilar = true;
                break;
            }

            if (!isTooSimilar)
                selectedWords.Add(candidate);
        }

        return selectedWords;
    }


    private static bool AreHackingWordsSimilar(string first, string second)
    {
        string normalizedFirst = NormalizeHackingWordForSimilarity(first);
        string normalizedSecond = NormalizeHackingWordForSimilarity(second);
        if (string.IsNullOrEmpty(normalizedFirst) || string.IsNullOrEmpty(normalizedSecond))
            return false;

        if (string.Equals(normalizedFirst, normalizedSecond, StringComparison.OrdinalIgnoreCase))
            return true;

        int lengthDifference = Mathf.Abs(normalizedFirst.Length - normalizedSecond.Length);
        int shortestLength = Mathf.Min(normalizedFirst.Length, normalizedSecond.Length);
        if (shortestLength >= 5 && lengthDifference <= 2)
        {
            string shorter = normalizedFirst.Length <= normalizedSecond.Length ? normalizedFirst : normalizedSecond;
            string longer = normalizedFirst.Length > normalizedSecond.Length ? normalizedFirst : normalizedSecond;
            if (longer.StartsWith(shorter, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return GetHackingWordEditDistance(normalizedFirst, normalizedSecond) <= 2;
    }


    private static string NormalizeHackingWordForSimilarity(string word)
    {
        if (string.IsNullOrWhiteSpace(word))
            return string.Empty;

        string value = word.Trim().ToUpperInvariant();
        if (value.Length > 6 && value.EndsWith("ING", StringComparison.Ordinal))
            return value.Substring(0, value.Length - 3);

        if (value.Length > 5 && value.EndsWith("ED", StringComparison.Ordinal))
            return value.Substring(0, value.Length - 2);

        if (value.Length > 5 && value.EndsWith("ES", StringComparison.Ordinal))
            return value.Substring(0, value.Length - 2);

        if (value.Length > 4 && value.EndsWith("S", StringComparison.Ordinal))
            return value.Substring(0, value.Length - 1);

        return value;
    }


    private static int GetHackingWordEditDistance(string first, string second)
    {
        if (string.IsNullOrEmpty(first))
            return string.IsNullOrEmpty(second) ? 0 : second.Length;

        if (string.IsNullOrEmpty(second))
            return first.Length;

        int[] previousRow = new int[second.Length + 1];
        int[] currentRow = new int[second.Length + 1];

        for (int i = 0; i <= second.Length; i++)
            previousRow[i] = i;

        for (int firstIndex = 1; firstIndex <= first.Length; firstIndex++)
        {
            currentRow[0] = firstIndex;
            for (int secondIndex = 1; secondIndex <= second.Length; secondIndex++)
            {
                int substitutionCost = first[firstIndex - 1] == second[secondIndex - 1] ? 0 : 1;
                currentRow[secondIndex] = Mathf.Min(
                    Mathf.Min(currentRow[secondIndex - 1] + 1, previousRow[secondIndex] + 1),
                    previousRow[secondIndex - 1] + substitutionCost);
            }

            (previousRow, currentRow) = (currentRow, previousRow);
        }

        return previousRow[second.Length];
    }


    private HackingGenerationHistory GetHackingGenerationHistory(Terminal terminal)
    {
        if (!terminal)
            return null;

        return hackingHistoryByTerminal.TryGetValue(terminal, out HackingGenerationHistory history)
            ? history
            : null;
    }


    private PlacedHackingWord ChooseHackingPasswordWord(List<PlacedHackingWord> words, HackingGenerationHistory previousGeneration)
    {
        if (words == null || words.Count == 0)
            return null;

        List<PlacedHackingWord> passwordChoices = words;
        if (previousGeneration != null && !string.IsNullOrWhiteSpace(previousGeneration.Password))
        {
            List<PlacedHackingWord> nonRepeatingPasswordChoices = words.FindAll(word =>
                word != null &&
                !string.Equals(word.Word, previousGeneration.Password, StringComparison.OrdinalIgnoreCase));

            if (nonRepeatingPasswordChoices.Count > 0)
                passwordChoices = nonRepeatingPasswordChoices;
        }

        return passwordChoices[UnityEngine.Random.Range(0, passwordChoices.Count)];
    }


    private void SaveHackingGenerationHistory(List<PlacedHackingWord> words, string password)
    {
        if (!activeTerminal)
            return;

        if (!hackingHistoryByTerminal.TryGetValue(activeTerminal, out HackingGenerationHistory history))
        {
            history = new HackingGenerationHistory();
            hackingHistoryByTerminal.Add(activeTerminal, history);
        }

        history.Password = password ?? string.Empty;
        history.Words.Clear();

        if (words == null)
            return;

        for (int i = 0; i < words.Count; i++)
        {
            PlacedHackingWord word = words[i];
            if (word == null || string.IsNullOrWhiteSpace(word.Word))
                continue;

            history.Words.Add(word.Word);
        }
    }


    private static int GetMaxStringLength(IList<string> values)
    {
        int maxLength = 0;
        if (values == null)
            return maxLength;

        for (int i = 0; i < values.Count; i++)
        {
            string value = values[i];
            if (!string.IsNullOrEmpty(value))
                maxLength = Mathf.Max(maxLength, value.Length);
        }

        return maxLength;
    }


    private static string[] GetHackingWordPool()
    {
        const string wordPool =
            // Very Easy: 4-5 characters.
            "DATA NODE USER CORE LOCK CODE SAFE DOOR PORT LINK VAULT ADMIN ENTRY POWER PANEL DRIVE ROUTE ARRAY LOGIN CABLE " +
            "ZONE GRID CHIP WIRE BYTE SEED CELL FUSE GATE BEAM DIAL TOOL VENT PUMP TANK FUEL HEAT COOL MAPS FILE DISK MAIN BASE BANK ROOM HALL DECK WING LIFT RAIL WAVE TONE ECHO WARD UNIT TEST PLAN LOGS PATH EXIT ALARM LASER RADAR TIMER INPUT LOCKS DOORS PORTS LINKS CORES NODES USERS FILES SCANS SIGNS PIPES WIRES VALVE GAUGE MOTOR LIGHT RELAY RADIO TOWER FIELD LEVEL FLOOR STEEL METAL GLASS STONE WATER STEAM DUCTS VENTS FUSES TUBES GEARS KEYS LAMP LENS SEAL FANS BARS BOLT NUTS PINS " +
            // Easy: 6-8 characters.
            "ACCESS SYSTEM SERVER MEMORY ONLINE SIGNAL REMOTE MODULE BUFFER SOURCE CONTROL NETWORK SCIENCE MACHINE CIRCUIT ARCHIVE STORAGE FIREWALL TERMINAL OVERRIDE DATABASE CHECKSUM PASSWORD ENCRYPTS DECODERS ARCHIVES NETWORKS CONSOLES REACTORS " +
            "STATION CONSOLE DISPLAY MONITOR REACTOR TURBINE BATTERY SENSOR CAMERA SWITCH SOCKET ROUTER BRIDGE FILTER VALVES GAUGES MOTORS LIGHTS RELAYS RADIOS TOWERS FIELDS LEVELS FLOORS PANELS CABLES CIRCUITS COMMAND PROGRAM PROCESS PACKAGE SERVICE REQUEST RESPONSE ACCOUNT PROFILE RECORD REPORT LOGINS PROTOCOL OVERSEER SECURITY CALIBER MEDICAL ROBOTIC UTILITY FACTORY HANGARS BARRACKS ARMORY SUPPLY TRAFFIC TRANSIT FREIGHT SHUTTLE LANDING LAUNCH HARBOR TUNNEL LADDER ELEVATE SHIELD DRIVER CLIENT DEVICE WINDOW SCREEN KEYPAD HEADER FOOTER NUMBER VECTOR MATRIX RANDOM BINARY SCRIPT LIBRARY VERSION SESSION BACKUP RESTORE UPDATE " +
            // Average: 9-10 characters.
            "OPERATION SELECTION CONNECTED OVERSEERS EMERGENCY PERSONNEL CALIBRATE PROTECTION INDUSTRIAL PROCESSING RESEARCHER DIAGNOSTIC REGULATOR EQUIPMENT DIRECTIVE GENERATED SYNTHETIC TELEMETRY LIBRARIES MIGRATION " +
            "ALGORITHM FRAMEWORK INTERFACE DIRECTORY PROCESSOR GENERATOR FIREWALLS TERMINALS OVERRIDES CONTROLLER OPERATORS WORKSPACE ASSEMBLER DEBUGGERS ENCRYPTOR PASSWORDS BACKDOORS SWITCHBOX MAINFRAME DATAFILES CODEBOOKS REBOOTING SHUTDOWNS ACTUATORS REPAIRING INSPECTOR SCHEDULER OPERATING VALIDATOR ISOLATION FAILSAFES CARTRIDGE CYLINDERS TRANSFORM GEOMETRIC HYDRAULIC PNEUMATIC MAGNETRON OSCILLATE FREQUENCY AMPLIFIER CAPACITOR RESISTORS INSULATOR CONDUCTOR GENERATES REGISTERS DATABASES PROCESSES ANALYZERS DETECTORS RECEIVERS REPEATERS ACTUATION CONTROLLED ENCRYPTION DECRYPTION COMPRESSOR CONVERTERS PARAMETERS SIMULATION AUTOMATION OPERATIONS COLLECTION INSPECTION NAVIGATION MANAGEMENT ALLOCATION RESOLUTION ACTIVATION INDICATORS CONTAINERS SELECTIONS CONNECTORS INVENTORY PATCHWORKS BLUEPRINTS DATASTORE ROUTEWAYS DATABANKS " +
            // Hard: 11-12 characters.
            "MAINTENANCE OBSERVATION TRANSCRIBED REPLICATION RESTORATION ENGINEERING CONTAINMENT TRANSMITTER SUPERVISION EXPERIMENTS ACCELERATORS BATTLEFIELD CENTRIFUGES DISASSEMBLE DISTRIBUTORS FABRICATION GEOTHERMALS RECALIBRATES VENTILATION " +
            "ACCELERATOR CALIBRATION CALCULATORS CONNECTIONS CONTROLLERS CONVERSIONS COORDINATES DEACTIVATED DEPLOYMENTS DIAGNOSTICS DIRECTORIES DISCONNECTS DOCUMENTING ELECTRONICS ENCRYPTIONS ENVIRONMENT EXAMINATION FABRICATORS GENERATIONS IDENTIFIERS INFORMATION INSTALLATION INSTRUCTIONS INTEGRATION INTERFACING LABORATORIES MEASUREMENTS OPTIMIZATION OSCILLATORS PROCESSIONS PROGRAMMERS PROTECTIONS REACTIVATED REASSEMBLED RECONSTRUCT REGULATIONS REPLACEMENT RESISTANCES SUBROUTINES SUPERVISORS TELEMETRIES TEMPERATURE TRANSCEIVER VENTILATORS WORKSTATION ASSIGNMENTS AUTHORIZERS CHECKPOINTS CREDENTIALS DISTRIBUTION ELECTRICALLY ENVIRONMENTS IDENTIFYING INTERLOCKED NAVIGATIONS RECONNECTION REFINEMENTS REINFORCING REINITIALIZE RESTORATIVE SECUREMENTS TERMINATORS TRANSFORMER TRANSPORTER VERIFICATION WORKAROUNDS APPLICATIONS ARCHITECTS ASSORTMENTS ATTACHMENTS BIOSENSORS BREAKPOINTS BROADCASTS CLASSIFIERS COMPARTMENT COMPONENTS COMPRESSIONS CONDUCTANCE DECOMPILERS DISPATCHERS DUPLICATIONS FIREWALLING OVERCHARGED PERFORMANCES PERIPHERALS PREPARATION PROCESSINGS PROJECTILES REACTIVATES REPOSITORIES RESTARTABLE SEQUENCINGS SIMULATIONS STABILIZERS SYNTHESIZER TELEPORTERS TRANSLATORS VENTILATING " +
            // Very Hard: 13-15 characters.
            "COMMUNICATION CONFIGURATION AUTHENTICATION RECONSTRUCTION ADMINISTRATION INFRASTRUCTURE REPRESENTATION TRANSFORMATION MICROPROCESSOR INSTRUMENTATION RESPONSIBILITY ENVIRONMENTAL INTERNATIONAL CLASSIFICATION DECONTAMINATE IDENTIFICATION ORGANIZATIONAL INTERROGATIONS TRANSMISSIONS DOCUMENTATION CLASSIFICATIONS MICROPROCESSORS REPRESENTATIONS TRANSFORMATIONS ADMINISTRATIONS INFRASTRUCTURES IMPLEMENTATIONS RECONSTRUCTIONS STANDARDIZATION " +
            "ACCUMULATIONS AMPLIFICATION ARCHITECTURAL AUTHORIZATIONS CENTRALIZATION CERTIFICATION COMMUNICATIONS COMPLICATIONS COMPUTATIONAL CONCENTRATION CONTAMINATION DECLASSIFYING DECOMMISSIONED DECONTAMINATION DEMONSTRATION DETERMINATION DISASSEMBLING DISCONNECTION DISTRIBUTIONS DOCUMENTARIES EFFECTIVENESS ELECTRIFICATION ENCAPSULATION ESTABLISHMENT EXPERIMENTING FRAGMENTATION IMPLEMENTATION INITIALIZATION INSTALLATIONS INSTANTIATION INTERROGATION INVESTIGATION MANUFACTURING MODIFICATIONS OBSERVATIONAL OPTIMIZATIONS PARAMETERIZED PRIORITIZATION REACTIVATIONS RECALIBRATION RECONNECTIONS RECONSTRUCTED REDEPLOYMENTS REINFORCEMENT REINITIALIZED RELATIONSHIPS REORGANIZATION REPROGRAMMING RESTRUCTURING RETRANSMITTING SEGMENTATIONS STANDARDIZING SUPERCOMPUTER TELECOMMUTING TRANSPORTATION VERIFICATIONS VULNERABILITY AUTHENTICATORS CONFIGURATORS SYNCHRONIZERS ADMINISTRATORS ARCHITECTURES AUTHENTICATED CONSIDERATION DATASTRUCTURE DEFRAGMENTING DELIBERATIONS DIFFERENTIALS ELECTROMAGNETS EXPERIMENTERS HARMONIZATION HYPERTHREADING IMPLANTATIONS INFORMATIONAL INTEROPERABLE MAGNETIZATION MECHANIZATION MICROANALYSIS MODULARIZATION NEUTRALIZATION ORGANIZATIONS PARALLELIZING PREPROCESSING RECONCILIATION REINITIALIZES REINSTALLATION RESYNCHRONIZE VISUALIZATION";

        return wordPool.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
    }


    private static string CreateFallbackHackingWord(int length)
    {
        const string fallback = "TERMINAL";
        int safeLength = Mathf.Max(1, length);

        if (safeLength <= fallback.Length)
            return fallback.Substring(0, safeLength);

        string value = fallback;
        while (value.Length < safeLength)
            value += "X";

        return value.Substring(0, safeLength);
    }


    private static string BuildHackingMemoryStringWithWord(string word, int displayLength, out int wordStartIndex)
    {
        wordStartIndex = -1;
        int safeLength = Mathf.Max(1, displayLength);
        string safeWord = string.IsNullOrWhiteSpace(word) ? CreateFallbackHackingWord(Mathf.Min(4, safeLength)) : word.Trim().ToUpperInvariant();

        if (safeWord.Length >= safeLength)
        {
            wordStartIndex = 0;
            return safeWord.Substring(0, safeLength);
        }

        char[] characters = BuildSpecialCharacterString(safeLength).ToCharArray();
        int startIndex = UnityEngine.Random.Range(0, safeLength - safeWord.Length + 1);
        wordStartIndex = startIndex;
        for (int i = 0; i < safeWord.Length; i++)
            characters[startIndex + i] = safeWord[i];

        return new string(characters);
    }


    private static string BuildSpecialCharacterString(int length)
    {
        int safeLength = Mathf.Max(1, length);
        char[] characters = new char[safeLength];

        for (int i = 0; i < characters.Length; i++)
            characters[i] = GetRandomHackingNonBracketSpecialCharacter();

        return new string(characters);
    }


    private static char GetRandomHackingNonBracketSpecialCharacter()
    {
        return HackingNonBracketSpecialCharacters[UnityEngine.Random.Range(0, HackingNonBracketSpecialCharacters.Length)];
    }


    private static int GetPasswordLikeness(string guess, string password)
    {
        if (string.IsNullOrEmpty(guess) || string.IsNullOrEmpty(password))
            return 0;

        int max = Mathf.Min(guess.Length, password.Length);
        int likeness = 0;
        for (int i = 0; i < max; i++)
        {
            if (char.ToUpperInvariant(guess[i]) == char.ToUpperInvariant(password[i]))
                likeness++;
        }

        return likeness;
    }


    private static void Shuffle<T>(IList<T> values)
    {
        if (values == null)
            return;

        for (int i = values.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            (values[i], values[swapIndex]) = (values[swapIndex], values[i]);
        }
    }


    private int GetInteractorScience(GameObject interactor)
    {
        if (!interactor)
            return 0;

        PlayerState playerState = interactor.GetComponentInParent<PlayerState>(true);
        return playerState ? Mathf.Clamp(playerState.GetScience(), 0, 100) : 0;
    }


    private GameObject FindChildGameObjectByName(string childName)
    {
        Transform[] transforms = GetComponentsInChildren<Transform>(true);

        for (int i = 0; i < transforms.Length; i++)
        {
            Transform child = transforms[i];
            if (!child || child == transform)
                continue;

            if (string.Equals(child.name, childName, StringComparison.Ordinal))
                return child.gameObject;
        }

        return null;
    }

    private TerminalDocument CreateFallbackDocument(string fallbackTerminalName)
    {
        string safeName = string.IsNullOrWhiteSpace(fallbackTerminalName) ? "Terminal" : fallbackTerminalName.Trim();

        TerminalDocument document = new TerminalDocument
        {
            terminalTitle = safeName,
            startupPageId = "main",
            pages = new List<TerminalPage>
            {
                new TerminalPage
                {
                    pageId = "main",
                    body = "No terminal data loaded.",
                    includeBackOption = false,
                    includeExitOption = true,
                    exitOptionLabel = "> Log Off",
                    options = new List<TerminalOption>()
                }
            }
        };

        return document;
    }


    private void SetGameplayActionsEnabled(bool enabled)
    {
        if (!playerControls)
            playerControls = FindAnyObjectByType<PlayerControls>();

        controls = playerControls ? playerControls.Controls : controls;

        if (controls == null)
            return;

        if (enabled)
            controls.Player.Enable();
        else
            controls.Player.Disable();
    }


    private void ConfigurePromptMessageText()
    {
        if (!promptMessageText)
            return;

        if (terminalFont)
            promptMessageText.font = terminalFont;

        promptMessageText.color = terminalTextColor;
        promptMessageText.textWrappingMode = TextWrappingModes.Normal;
        promptMessageText.overflowMode = TextOverflowModes.Overflow;
        promptMessageText.alignment = TextAlignmentOptions.TopLeft;
        promptMessageText.raycastTarget = false;
    }


    private void ConfigureTerminalCursorImage()
    {
        if (!terminalCursorImage)
            return;

        if (!hasCachedTerminalCursorColor)
        {
            cachedTerminalCursorColor = terminalCursorImage.color;
            hasCachedTerminalCursorColor = true;
        }

        terminalCursorImage.raycastTarget = false;
    }


    private void ConfigureHackingCursorImage()
    {
        if (!hackingCursorImage)
            return;

        if (!hasCachedHackingCursorColor)
        {
            cachedHackingCursorColor = hackingCursorImage.color;
            hasCachedHackingCursorColor = true;
        }

        RectTransform cursorRect = hackingCursorImage.rectTransform;
        if (cursorRect && !hasCachedHackingCursorAnchoredPosition)
        {
            cachedHackingCursorAnchoredPosition = cursorRect.anchoredPosition;
            hasCachedHackingCursorAnchoredPosition = true;
        }

        hackingCursorImage.raycastTarget = false;
    }


    private void UpdateTerminalCursorBlink(bool forceStateRefresh = false)
    {
        if (!terminalCursorImage && !hackingCursorImage)
            return;

        bool shouldBlink = isOpen && !promptMessageVisible && terminalCanvasGroup && terminalCanvasGroup.alpha > 0.0f;
        if (!shouldBlink)
        {
            SetTerminalCursorVisible(false);
            SetHackingCursorVisible(false);
            return;
        }

        float interval = Mathf.Max(0.01f, terminalCursorBlinkIntervalSeconds);
        bool visible = Mathf.FloorToInt(Time.unscaledTime / interval) % 2 == 0;

        if (isHackingActive)
        {
            SetTerminalCursorVisible(false);
            if (isHackingIntroSequenceActive)
            {
                SetHackingCursorVisible(false);
            }
            else
            {
                UpdateHackingCursorPosition();

                if (forceStateRefresh || (hackingCursorImage && hackingCursorImage.gameObject.activeSelf != visible))
                    SetHackingCursorVisible(visible);
            }

            return;
        }

        SetHackingCursorVisible(false);

        if (forceStateRefresh || (terminalCursorImage && terminalCursorImage.gameObject.activeSelf != visible))
            SetTerminalCursorVisible(visible);
    }


    private void SetTerminalCursorVisible(bool visible)
    {
        if (!terminalCursorImage)
            return;

        if (terminalCursorImage.gameObject.activeSelf != visible)
            terminalCursorImage.gameObject.SetActive(visible);

        if (visible && hasCachedTerminalCursorColor)
            terminalCursorImage.color = cachedTerminalCursorColor;
    }


    private void SetHackingCursorVisible(bool visible)
    {
        if (!hackingCursorImage)
            return;

        if (hackingCursorImage.gameObject.activeSelf != visible)
            hackingCursorImage.gameObject.SetActive(visible);

        if (visible && hasCachedHackingCursorColor)
            hackingCursorImage.color = cachedHackingCursorColor;
    }


    private void UpdateHackingCursorPosition()
    {
        if (!hackingCursorImage)
            return;

        ConfigureHackingCursorImage();

        RectTransform cursorRect = hackingCursorImage.rectTransform;
        if (!cursorRect)
            return;

        if (!answerText || string.IsNullOrEmpty(answerText.text))
        {
            RestoreHackingCursorPosition(cursorRect);
            return;
        }

        answerText.ForceMeshUpdate(true, true);
        TMP_TextInfo textInfo = answerText.textInfo;
        int lastVisibleCharacterIndex = -1;

        for (int i = textInfo.characterCount - 1; i >= 0; i--)
        {
            if (!textInfo.characterInfo[i].isVisible)
                continue;

            lastVisibleCharacterIndex = i;
            break;
        }

        if (lastVisibleCharacterIndex < 0)
        {
            RestoreHackingCursorPosition(cursorRect);
            return;
        }

        TMP_CharacterInfo characterInfo = textInfo.characterInfo[lastVisibleCharacterIndex];
        TMP_LineInfo lineInfo = textInfo.lineInfo[Mathf.Clamp(characterInfo.lineNumber, 0, textInfo.lineCount - 1)];
        float lineCenterY = (lineInfo.ascender + lineInfo.descender) * 0.5f;
        float spaceWidth = GetHackingCursorSpaceWidth();
        Vector3 cursorLocalTextPosition = new Vector3(
            characterInfo.topRight.x + (spaceWidth * 0.5f) + hackingCursorOffset.x,
            lineCenterY + hackingCursorOffset.y,
            0.0f);
        Vector3 cursorWorldPosition = answerText.transform.TransformPoint(cursorLocalTextPosition);

        RectTransform cursorParent = cursorRect.parent as RectTransform;
        if (cursorParent)
        {
            Vector3 cursorParentLocalPosition = cursorParent.InverseTransformPoint(cursorWorldPosition);
            cursorRect.localPosition = new Vector3(
                cursorParentLocalPosition.x,
                cursorParentLocalPosition.y,
                cursorRect.localPosition.z);
            return;
        }

        cursorRect.position = cursorWorldPosition;
    }


    private void RestoreHackingCursorPosition(RectTransform cursorRect)
    {
        if (!cursorRect || !hasCachedHackingCursorAnchoredPosition)
            return;

        cursorRect.anchoredPosition = cachedHackingCursorAnchoredPosition;
    }


    private float GetHackingCursorSpaceWidth()
    {
        if (!answerText)
            return Mathf.Max(1.0f, hackingFontSize * 0.5f);

        TMP_Text measurementText = EnsureBodyMeasurementText();
        if (!measurementText)
            return Mathf.Max(1.0f, answerText.fontSize * 0.5f);

        ConfigureBaseText(measurementText, answerText.fontSize, answerText.fontStyle);
        measurementText.textWrappingMode = TextWrappingModes.NoWrap;
        measurementText.overflowMode = TextOverflowModes.Overflow;
        measurementText.text = "A A";
        measurementText.ForceMeshUpdate(true, true);

        Vector2 spacedWidth = measurementText.GetPreferredValues("A A", float.PositiveInfinity, float.PositiveInfinity);
        Vector2 compactWidth = measurementText.GetPreferredValues("AA", float.PositiveInfinity, float.PositiveInfinity);
        return Mathf.Max(1.0f, spacedWidth.x - compactWidth.x);
    }


    private void EnsureTerminalRootHasVisibleScale(GameObject terminalRoot)
    {
        if (!terminalRoot || terminalRoot.transform.localScale != Vector3.zero)
            return;

        terminalRoot.transform.localScale = Vector3.one;

        if (hasWarnedAboutZeroTerminalScale)
            return;

        hasWarnedAboutZeroTerminalScale = true;
        Debug.LogWarning("TerminalUI had a zero RectTransform scale, so TerminalController restored it to one for rendering and UI raycasts.", this);
    }
}
