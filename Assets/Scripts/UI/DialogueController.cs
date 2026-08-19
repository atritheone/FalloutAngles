using System;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class DialogueController : MonoBehaviour
{
    private const float ReferenceDialogueFontSize = 21.0f;
    private const float NpcNameFontSizeAtReference = 22.0f;
    private const float ArrowFontSizeAtReference = 30.0f;
    private const float InteractCloseReopenCooldownSeconds = 0.15f;
    private const int MaxVisibleChoicesWithoutScroll = 3;
    private const float ScreenBottomMarginPixels = 92.0f;
    private const float DialoguePanelDefaultWidth = 780.0f;
    private const float DialoguePanelMinViewportMargin = 64.0f;
    private const float DialoguePanelMinWidth = 300.0f;
    private const float DialoguePanelDefaultHeight = 120.0f;
    private const float RowMinimumHeight = 40.0f;
    private const float RowVerticalPadding = 14.0f;
    private const float RowTextVerticalInset = 7.0f;
    private const float RowSpacing = 10.0f;
    private const float OptionsPanelVerticalPadding = 18.0f;
    private const float PlaybackVerticalPadding = 20.0f;
    private const float PlaybackTextVerticalInset = 10.0f;
    private const float PlaybackMinHeight = 54.0f;
    private const float TopBottomLineHeight = 2.0f;
    private const float SelectionOutlineThickness = 2.0f;
    private const float LeftScrollGuideWidth = 2.0f;
    private const float ArrowColumnWidth = 40.0f;
    private const float ArrowInset = 22.0f;
    private const float ScrollGuideXOffset = 10.0f;
    private const float ArrowSize = 30.0f;
    private const float ChoiceTextLeftPadding = 22.0f;
    private const float ChoiceTextRightPadding = 18.0f;
    private const float PlaybackTextLeftPadding = 22.0f;
    private const float PlaybackTextRightPadding = 22.0f;
    private const float NameTopInsetPixels = 42.0f;
    private const float NameRightInsetPixels = 42.0f;
    private const float NameDefaultWidthPixels = 420.0f;
    private const float NameDefaultHeightPixels = 34.0f;
    private const string EditorPreviewNpcName = "NPC";
    private const string EditorPreviewPlaybackText = "Dialogue preview line.";
    private const float NavigateRepeatDelaySeconds = 0.32f;
    private const float NavigateRepeatRateSeconds = 0.12f;
    private const float MinLookDirectionSqr = 0.0001f;
    private static readonly int AnimatorXVelocityHash = Animator.StringToHash("xVelocity");
    private static readonly int AnimatorZVelocityHash = Animator.StringToHash("zVelocity");
    private static readonly int AnimatorRunningHash = Animator.StringToHash("IsRunning");
    private static readonly int AnimatorSprintingHash = Animator.StringToHash("IsSprinting");

    private static float lastInteractCloseUnscaledTime = float.NegativeInfinity;

    [Serializable]
    private sealed class DialogueOptionRow
    {
        public RectTransform root;
        public RectTransform outlineRoot;
        public Image background;
        public Button button;
        public TMP_Text label;
        public EventTrigger eventTrigger;
    }

    private struct DialogueAnimatorState
    {
        public Animator animator;
        public AnimatorUpdateMode updateMode;
        public bool hasXVelocity;
        public bool hasZVelocity;
        public bool hasRunning;
        public bool hasSprinting;
    }

    private struct DialogueRootBodyState
    {
        public Rigidbody body;
        public bool wasKinematic;
        public RigidbodyInterpolation interpolation;
        public bool isOverridden;
    }

    [Header("Open Behavior")]
    [SerializeField] private CanvasGroup dialogueCanvasGroup;
    [SerializeField] private PlayerControls playerControls;
    [SerializeField] private CameraRigOrbit cameraRigOrbit;
    [SerializeField] private bool disableGameplayActionsWhenOpen = true;
    [SerializeField] private bool pauseGameWhenOpen = true;
    [SerializeField] private bool hidePlayerHudWhenOpen = true;
    [SerializeField] private bool hideCrosshairWhenOpen = true;
    [SerializeField] private bool closeOnCancel = true;
    [SerializeField] private bool disableInHierarchyWhenClosed = true;
    [SerializeField] private bool showInEditMode = true;
    [SerializeField] private CanvasGroup playerHudCanvasGroup;
    [SerializeField] private CanvasGroup crosshairCanvasGroup;

    [Header("Typography")]
    [SerializeField, Min(8.0f)] private float dialogueFontSize = ReferenceDialogueFontSize;

    [Header("Layout")]
    [SerializeField, Min(DialoguePanelMinWidth)] private float dialoguePanelWidth = DialoguePanelDefaultWidth;

    [Header("Theme")]
    [SerializeField] private TMP_FontAsset dialogueFont;
    [SerializeField] private Color lineColor = new Color32(0x4E, 0xFF, 0x61, 0xFF);
    [SerializeField] private Color panelBackgroundColor = new Color32(0x00, 0x12, 0x00, 0xB8);
    [SerializeField] private Color selectedBackgroundColor = new Color32(0x4E, 0xFF, 0x61, 0x1F);
    [SerializeField] private Color textColor = new Color32(0x4E, 0xFF, 0x61, 0xFF);

    [Header("Dialogue Participants")]
    [SerializeField, Min(0.0f)] private float dialoguePartnerTurnSpeedDegreesPerSecond = 240.0f;

    private InputSystemActions controls;
    private bool hasInitialized;
    private bool isOpen;
    private bool isPlaybackVisible;
    private bool isShowingChoices;
    private bool hasCachedNavigateInput;
    private bool hasCachedTimeScale;
    private float cachedTimeScale = 1.0f;
    private bool hasCachedPlayerHudState;
    private float cachedPlayerHudAlpha = 1.0f;
    private bool cachedPlayerHudInteractable;
    private bool cachedPlayerHudBlocksRaycasts;
    private bool hasCachedCrosshairState;
    private float cachedCrosshairAlpha = 1.0f;
    private bool cachedCrosshairInteractable;
    private bool cachedCrosshairBlocksRaycasts;
    private float nextNavigateRepeatUnscaledTime;

    private NPC activeNpc;
    private GameObject activeInteractor;
    private DialogueDefinition activeDialogueDefinition;
    private DialogueTreeDefinition activeTree;
    private DialogueNodeDefinition currentNode;

    private readonly List<DialogueChoiceDefinition> currentChoices = new List<DialogueChoiceDefinition>();
    private readonly List<DialogueOptionRow> optionRows = new List<DialogueOptionRow>();
    private readonly List<DialogueAnimatorState> dialogueAnimatorStates = new List<DialogueAnimatorState>();
    private readonly List<Animator> dialogueParticipantAnimators = new List<Animator>();
    private readonly List<RigBuilder> dialogueParticipantRigBuilders = new List<RigBuilder>();

    private RectTransform rootRectTransform;
    private RectTransform playbackPanelRect;
    private RectTransform optionsPanelRect;
    private RectTransform optionsContentRect;
    private RectTransform optionsSelectionOutlineRect;
    private RectTransform optionsSelectionBackgroundRect;
    private RectTransform optionsSelectionOutlineTopRect;
    private RectTransform optionsSelectionOutlineBottomRect;
    private RectTransform optionsSelectionOutlineLeftRect;
    private RectTransform optionsSelectionOutlineRightRect;
    private RectTransform optionsTopLineRect;
    private RectTransform optionsBottomLineRect;
    private RectTransform optionsScrollGuideRect;
    private TMP_Text npcNameText;
    private TMP_Text playbackText;
    private TMP_Text scrollUpText;
    private TMP_Text scrollDownText;
    private Image playbackBackgroundImage;
    private Image optionsBackgroundImage;
    private Image optionsSelectionOutlineImage;
    private Image optionsSelectionBackgroundImage;

    private int selectedChoiceIndex;
    private int visibleChoiceStartIndex;
    private Rigidbody activeInteractorRootRigidbody;
    private Rigidbody activeNpcRootRigidbody;
    private DialogueRootBodyState activeInteractorRootBodyState;
    private DialogueRootBodyState activeNpcRootBodyState;
    private bool editorPreviewRefreshQueued;
    [NonSerialized] private bool isRuntimeGeneratedInstance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeDialogueUiExists()
    {
        EnsureRuntimeInstanceExists();
    }

    public static DialogueController FindFirstInSceneIncludingInactive()
    {
        DialogueController controller = FindExistingController();
        return controller ? controller : EnsureRuntimeInstanceExists();
    }

    public static bool IsInteractCloseCooldownActive()
    {
        return Time.unscaledTime - lastInteractCloseUnscaledTime <= InteractCloseReopenCooldownSeconds;
    }

    public bool IsOpen()
    {
        return isOpen;
    }

    public bool OpenForNpc(NPC npc, GameObject interactor)
    {
        EnsureInitialized();
        ApplyTypography();

        if (!npc || !npc.HasDialogue())
            return false;

        DialogueDefinition dialogueDefinition = npc.GetDialogueDefinition();
        if (!dialogueDefinition)
            return false;

        DialogueTreeDefinition entryTree = dialogueDefinition.GetEntryTree();
        if (entryTree == null)
            return false;

        DialogueNodeDefinition startNode = entryTree.GetStartNode();
        if (startNode == null)
            return false;

        activeNpc = npc;
        activeInteractor = interactor;
        activeDialogueDefinition = dialogueDefinition;
        activeTree = entryTree;

        SetOpenState(true);
        EnterNode(startNode);
        return true;
    }

    public void Close()
    {
        if (!isOpen)
            return;

        lastInteractCloseUnscaledTime = Time.unscaledTime;
        SetOpenState(false);
    }

    private void Awake()
    {
        hasInitialized = false;
        if (Application.isPlaying)
        {
            EnsureInitialized();
            SetOpenState(false, true);
            return;
        }

        QueueEditorPreviewRefresh();
    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            EnsureInitialized();
            return;
        }

        QueueEditorPreviewRefresh();
    }

    private void OnDisable()
    {
        if (Application.isPlaying)
            EndDialogueParticipantOverrides();

        CancelQueuedEditorPreviewRefresh();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        QueueEditorPreviewRefresh();
    }

    private void Update()
    {
        if (!isOpen)
            return;

        UpdateDialoguePartnerFacing();
        EnforceDialogueParticipantsIdleMotion();

        if (isPlaybackVisible && IsPlaybackAdvanceRequested())
        {
            AdvanceFromPlaybackState();
        }

        if (!isShowingChoices)
            return;

        ProcessChoiceNavigation();

        if (WasChoiceSubmitPressed())
            SelectChoice(selectedChoiceIndex);

        if (closeOnCancel && WasCancelPressed())
            Close();
    }

    private void EnsureInitialized()
    {
        if (hasInitialized)
            return;

        if (!playerControls)
            playerControls = FindAnyObjectByType<PlayerControls>();

        if (!cameraRigOrbit)
            cameraRigOrbit = FindAnyObjectByType<CameraRigOrbit>();

        controls = playerControls ? playerControls.Controls : null;
        if (!playerHudCanvasGroup)
            playerHudCanvasGroup = FindPlayerHudCanvasGroup();
        if (!crosshairCanvasGroup)
            crosshairCanvasGroup = FindCrosshairCanvasGroup();

        EnsureRootHierarchy();
        ApplyTypography();
        ResolveThemeFromTerminalUi();
        ApplyTheme();

        hasInitialized = true;
    }

    private void EnsureRootHierarchy()
    {
        rootRectTransform = transform as RectTransform;
        if (!rootRectTransform)
            rootRectTransform = gameObject.AddComponent<RectTransform>();

        EnsureRootHasCanvasParent();

        rootRectTransform.anchorMin = Vector2.zero;
        rootRectTransform.anchorMax = Vector2.one;
        rootRectTransform.offsetMin = Vector2.zero;
        rootRectTransform.offsetMax = Vector2.zero;
        rootRectTransform.localScale = Vector3.one;

        if (!dialogueCanvasGroup)
            dialogueCanvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        bool hasExistingNpcNameText = npcNameText || FindText("NpcNameText", rootRectTransform);
        npcNameText = npcNameText ? npcNameText : EnsureText(
            "NpcNameText",
            rootRectTransform,
            TextAlignmentOptions.TopRight,
            GetNpcNameFontSize(),
            Application.isPlaying ? string.Empty : EditorPreviewNpcName);
        ConfigureText(
            npcNameText,
            TextAlignmentOptions.TopRight,
            GetNpcNameFontSize(),
            Application.isPlaying ? string.Empty : EditorPreviewNpcName,
            !hasExistingNpcNameText || Application.isPlaying);
        if (!hasExistingNpcNameText && npcNameText)
        {
            RectTransform npcNameRect = npcNameText.rectTransform;
            npcNameRect.anchorMin = new Vector2(1.0f, 1.0f);
            npcNameRect.anchorMax = new Vector2(1.0f, 1.0f);
            npcNameRect.pivot = new Vector2(1.0f, 1.0f);
            npcNameRect.anchoredPosition = new Vector2(-NameRightInsetPixels, -NameTopInsetPixels);
            npcNameRect.sizeDelta = new Vector2(NameDefaultWidthPixels, NameDefaultHeightPixels);
        }

        playbackPanelRect = playbackPanelRect ? playbackPanelRect : FindRect("PlaybackPanel", rootRectTransform);
        playbackPanelRect = playbackPanelRect ? playbackPanelRect : CreatePanel("PlaybackPanel", rootRectTransform, GetConfiguredPanelWidth());
        EnsurePanelUsesFixedWidthAnchoring(playbackPanelRect);
        playbackBackgroundImage = playbackBackgroundImage ? playbackBackgroundImage : EnsureImageComponent(playbackPanelRect.gameObject);
        playbackText = playbackText ? playbackText : EnsureText("PlaybackText", playbackPanelRect, TextAlignmentOptions.TopLeft, GetBodyFontSize(), string.Empty);
        ConfigureText(playbackText, TextAlignmentOptions.TopLeft, GetBodyFontSize(), string.Empty, Application.isPlaying);

        optionsPanelRect = optionsPanelRect ? optionsPanelRect : FindRect("OptionsPanel", rootRectTransform);
        optionsPanelRect = optionsPanelRect ? optionsPanelRect : CreatePanel("OptionsPanel", rootRectTransform, GetConfiguredPanelWidth());
        EnsurePanelUsesFixedWidthAnchoring(optionsPanelRect);
        optionsBackgroundImage = optionsBackgroundImage ? optionsBackgroundImage : EnsureImageComponent(optionsPanelRect.gameObject);
        optionsTopLineRect = optionsTopLineRect ? optionsTopLineRect : FindRect("TopLine", optionsPanelRect);
        optionsTopLineRect = optionsTopLineRect ? optionsTopLineRect : CreateImageRect("TopLine", optionsPanelRect, Vector2.zero, Vector2.zero);
        EnsureImageComponent(optionsTopLineRect.gameObject);
        optionsBottomLineRect = optionsBottomLineRect ? optionsBottomLineRect : FindRect("BottomLine", optionsPanelRect);
        optionsBottomLineRect = optionsBottomLineRect ? optionsBottomLineRect : CreateImageRect("BottomLine", optionsPanelRect, Vector2.zero, Vector2.zero);
        EnsureImageComponent(optionsBottomLineRect.gameObject);
        optionsScrollGuideRect = optionsScrollGuideRect ? optionsScrollGuideRect : FindRect("ScrollGuide", optionsPanelRect);
        optionsScrollGuideRect = optionsScrollGuideRect ? optionsScrollGuideRect : CreateImageRect("ScrollGuide", optionsPanelRect, Vector2.zero, Vector2.zero);
        EnsureImageComponent(optionsScrollGuideRect.gameObject);
        scrollUpText = scrollUpText ? scrollUpText : EnsureText("ScrollUpText", optionsPanelRect, TextAlignmentOptions.Center, GetArrowFontSize(), "^");
        ConfigureText(scrollUpText, TextAlignmentOptions.Center, GetArrowFontSize(), "^");
        scrollDownText = scrollDownText ? scrollDownText : EnsureText("ScrollDownText", optionsPanelRect, TextAlignmentOptions.Center, GetArrowFontSize(), "v");
        ConfigureText(scrollDownText, TextAlignmentOptions.Center, GetArrowFontSize(), "v");

        bool hasExistingOptionsContentRect = optionsContentRect || FindRect("OptionsContent", optionsPanelRect);
        optionsContentRect = optionsContentRect ? optionsContentRect : FindRect("OptionsContent", optionsPanelRect);
        optionsContentRect = optionsContentRect ? optionsContentRect : CreateRect("OptionsContent", optionsPanelRect);
        if (!hasExistingOptionsContentRect && optionsContentRect)
        {
            optionsContentRect.anchorMin = new Vector2(0.0f, 1.0f);
            optionsContentRect.anchorMax = new Vector2(1.0f, 1.0f);
            optionsContentRect.pivot = new Vector2(0.0f, 1.0f);
            optionsContentRect.anchoredPosition = Vector2.zero;
            optionsContentRect.sizeDelta = Vector2.zero;
        }

        optionsSelectionOutlineRect = optionsSelectionOutlineRect ? optionsSelectionOutlineRect : FindRect("SelectionOutline", optionsContentRect);
        optionsSelectionOutlineRect = optionsSelectionOutlineRect ? optionsSelectionOutlineRect : CreateImageRect("SelectionOutline", optionsContentRect, Vector2.zero, Vector2.one);
        optionsSelectionOutlineImage = optionsSelectionOutlineImage ? optionsSelectionOutlineImage : EnsureImageComponent(optionsSelectionOutlineRect.gameObject);
        if (optionsSelectionOutlineImage)
            optionsSelectionOutlineImage.enabled = false;

        optionsSelectionOutlineTopRect = optionsSelectionOutlineTopRect ? optionsSelectionOutlineTopRect : FindRect("OutlineTop", optionsSelectionOutlineRect);
        optionsSelectionOutlineTopRect = optionsSelectionOutlineTopRect ? optionsSelectionOutlineTopRect : CreateImageRect("OutlineTop", optionsSelectionOutlineRect, Vector2.zero, Vector2.zero);
        EnsureImageComponent(optionsSelectionOutlineTopRect.gameObject);
        ConfigureSelectionOutlineSegment(optionsSelectionOutlineTopRect, new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f), new Vector2(0.0f, 1.0f), Vector2.zero, new Vector2(0.0f, SelectionOutlineThickness));

        optionsSelectionOutlineBottomRect = optionsSelectionOutlineBottomRect ? optionsSelectionOutlineBottomRect : FindRect("OutlineBottom", optionsSelectionOutlineRect);
        optionsSelectionOutlineBottomRect = optionsSelectionOutlineBottomRect ? optionsSelectionOutlineBottomRect : CreateImageRect("OutlineBottom", optionsSelectionOutlineRect, Vector2.zero, Vector2.zero);
        EnsureImageComponent(optionsSelectionOutlineBottomRect.gameObject);
        ConfigureSelectionOutlineSegment(optionsSelectionOutlineBottomRect, new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f), new Vector2(0.0f, 0.0f), Vector2.zero, new Vector2(0.0f, SelectionOutlineThickness));

        optionsSelectionOutlineLeftRect = optionsSelectionOutlineLeftRect ? optionsSelectionOutlineLeftRect : FindRect("OutlineLeft", optionsSelectionOutlineRect);
        optionsSelectionOutlineLeftRect = optionsSelectionOutlineLeftRect ? optionsSelectionOutlineLeftRect : CreateImageRect("OutlineLeft", optionsSelectionOutlineRect, Vector2.zero, Vector2.zero);
        EnsureImageComponent(optionsSelectionOutlineLeftRect.gameObject);
        ConfigureSelectionOutlineSegment(optionsSelectionOutlineLeftRect, new Vector2(0.0f, 0.0f), new Vector2(0.0f, 1.0f), new Vector2(0.0f, 1.0f), Vector2.zero, new Vector2(SelectionOutlineThickness, 0.0f));

        optionsSelectionOutlineRightRect = optionsSelectionOutlineRightRect ? optionsSelectionOutlineRightRect : FindRect("OutlineRight", optionsSelectionOutlineRect);
        optionsSelectionOutlineRightRect = optionsSelectionOutlineRightRect ? optionsSelectionOutlineRightRect : CreateImageRect("OutlineRight", optionsSelectionOutlineRect, Vector2.zero, Vector2.zero);
        EnsureImageComponent(optionsSelectionOutlineRightRect.gameObject);
        ConfigureSelectionOutlineSegment(optionsSelectionOutlineRightRect, new Vector2(1.0f, 0.0f), new Vector2(1.0f, 1.0f), new Vector2(1.0f, 1.0f), Vector2.zero, new Vector2(SelectionOutlineThickness, 0.0f));
        optionsSelectionOutlineRect.gameObject.SetActive(false);

        bool hasExistingSelectionBackgroundRect = optionsSelectionBackgroundRect || FindRect("SelectionBackground", optionsSelectionOutlineRect);
        optionsSelectionBackgroundRect = optionsSelectionBackgroundRect ? optionsSelectionBackgroundRect : FindRect("SelectionBackground", optionsSelectionOutlineRect);
        optionsSelectionBackgroundRect = optionsSelectionBackgroundRect ? optionsSelectionBackgroundRect : CreateImageRect("SelectionBackground", optionsSelectionOutlineRect, Vector2.zero, Vector2.one);
        optionsSelectionBackgroundImage = optionsSelectionBackgroundImage ? optionsSelectionBackgroundImage : EnsureImageComponent(optionsSelectionBackgroundRect.gameObject);
        if (!hasExistingSelectionBackgroundRect && optionsSelectionBackgroundRect)
        {
            optionsSelectionBackgroundRect.anchorMin = Vector2.zero;
            optionsSelectionBackgroundRect.anchorMax = Vector2.one;
            optionsSelectionBackgroundRect.offsetMin = new Vector2(2.0f, 2.0f);
            optionsSelectionBackgroundRect.offsetMax = new Vector2(-2.0f, -2.0f);
            optionsSelectionBackgroundRect.localScale = Vector3.one;
        }
        optionsSelectionBackgroundRect.gameObject.SetActive(false);
        optionsSelectionOutlineRect.SetAsFirstSibling();

        optionRows.Clear();
        while (optionRows.Count < MaxVisibleChoicesWithoutScroll)
            optionRows.Add(ResolveOptionRow(optionRows.Count));
    }

    private void EnsurePanelUsesFixedWidthAnchoring(RectTransform panelRect)
    {
        if (!panelRect)
            return;

        float panelX = panelRect.anchoredPosition.x;
        float panelY = panelRect.anchoredPosition.y;
        float panelHeight = panelRect.sizeDelta.y > 1.0f ? panelRect.sizeDelta.y : DialoguePanelDefaultHeight;

        panelRect.anchorMin = new Vector2(0.5f, 0.0f);
        panelRect.anchorMax = new Vector2(0.5f, 0.0f);
        panelRect.pivot = new Vector2(0.5f, 0.0f);
        panelRect.anchoredPosition = new Vector2(panelX, panelY);
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, GetClampedPanelWidth(panelRect, GetConfiguredPanelWidth()));
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, panelHeight);
        panelRect.localScale = Vector3.one;
    }

    private void EnforceFixedPanelWidths()
    {
        EnsurePanelUsesFixedWidthAnchoring(playbackPanelRect);
        EnsurePanelUsesFixedWidthAnchoring(optionsPanelRect);
    }

    private float GetConfiguredPanelWidth()
    {
        return Mathf.Max(DialoguePanelMinWidth, dialoguePanelWidth);
    }

    private void EnsureRootHasCanvasParent()
    {
        if (!rootRectTransform)
            return;

        Canvas parentCanvas = rootRectTransform.GetComponentInParent<Canvas>(true);
        if (parentCanvas)
            return;

        if (!isRuntimeGeneratedInstance)
        {
            EnsureRootCanvasComponents();
            return;
        }

        Canvas targetCanvas = ResolveTargetCanvas();
        if (!targetCanvas)
            targetCanvas = CreateFallbackCanvas();

        RectTransform canvasRectTransform = targetCanvas ? targetCanvas.transform as RectTransform : null;
        if (!canvasRectTransform || rootRectTransform.parent == canvasRectTransform)
            return;

        rootRectTransform.SetParent(canvasRectTransform, false);
        rootRectTransform.SetAsLastSibling();
    }

    private void EnsureRootCanvasComponents()
    {
        if (!rootRectTransform)
            return;

        Canvas rootCanvas = rootRectTransform.GetComponent<Canvas>();
        if (!rootCanvas)
            rootCanvas = rootRectTransform.gameObject.AddComponent<Canvas>();
        ConfigureCanvasForDialogueUi(rootCanvas);

        if (!rootRectTransform.GetComponent<CanvasScaler>())
        {
            CanvasScaler scaler = rootRectTransform.gameObject.AddComponent<CanvasScaler>();
            ConfigureCanvasScalerForDialogueUi(scaler);
        }

        if (!rootRectTransform.GetComponent<GraphicRaycaster>())
            rootRectTransform.gameObject.AddComponent<GraphicRaycaster>();
    }

    private void ApplyTheme()
    {
        ApplyTextTheme(npcNameText);
        ApplyTextTheme(playbackText);
        ApplyTextTheme(scrollUpText);
        ApplyTextTheme(scrollDownText);

        if (playbackBackgroundImage)
            playbackBackgroundImage.color = panelBackgroundColor;

        if (optionsBackgroundImage)
            optionsBackgroundImage.color = panelBackgroundColor;

        if (optionsSelectionOutlineImage)
            optionsSelectionOutlineImage.color = lineColor;

        if (optionsSelectionBackgroundImage)
            optionsSelectionBackgroundImage.color = selectedBackgroundColor;

        SetImageColor(optionsSelectionOutlineTopRect, lineColor);
        SetImageColor(optionsSelectionOutlineBottomRect, lineColor);
        SetImageColor(optionsSelectionOutlineLeftRect, lineColor);
        SetImageColor(optionsSelectionOutlineRightRect, lineColor);
        SetImageColor(optionsTopLineRect, lineColor);
        SetImageColor(optionsBottomLineRect, lineColor);
        SetImageColor(optionsScrollGuideRect, lineColor);

        for (int i = 0; i < optionRows.Count; i++)
        {
            DialogueOptionRow row = optionRows[i];
            if (row == null)
                continue;

            ApplyTextTheme(row.label);

            if (row.background)
                row.background.color = selectedBackgroundColor;
        }
    }

    private void ApplyTypography()
    {
        EnforceFixedPanelWidths();

        if (npcNameText)
            npcNameText.fontSize = GetNpcNameFontSize();

        if (playbackText)
            playbackText.fontSize = GetBodyFontSize();

        if (scrollUpText)
            scrollUpText.fontSize = GetArrowFontSize();

        if (scrollDownText)
            scrollDownText.fontSize = GetArrowFontSize();

        for (int i = 0; i < optionRows.Count; i++)
        {
            DialogueOptionRow row = optionRows[i];
            if (row?.label)
                row.label.fontSize = GetBodyFontSize();
        }

        if (isShowingChoices)
            RefreshChoicesView();
        else if (isPlaybackVisible && playbackText)
            LayoutPlaybackPanel(playbackText.text);
        else if (!Application.isPlaying && playbackText)
            LayoutPlaybackPanel(playbackText.text);
    }

    private void ApplyTextTheme(TMP_Text text)
    {
        if (!text)
            return;

        if (dialogueFont)
            text.font = dialogueFont;

        text.color = textColor;
        text.raycastTarget = false;
    }

    private void ResolveThemeFromTerminalUi()
    {
        if (dialogueFont && lineColor.a > 0.0f && textColor.a > 0.0f)
            return;

        TerminalController terminalController = TerminalController.FindFirstInSceneIncludingInactive();
        if (!terminalController)
            return;

        if (!dialogueFont && TryGetPrivateField(terminalController, "terminalFont", out TMP_FontAsset resolvedFont) && resolvedFont)
            dialogueFont = resolvedFont;

        if (TryGetPrivateField(terminalController, "terminalTextColor", out Color resolvedLineColor))
        {
            lineColor = resolvedLineColor;
            textColor = resolvedLineColor;
        }

        if (TryGetPrivateField(terminalController, "hoveredOptionRectangleColor", out Color resolvedSelectionColor))
        {
            resolvedSelectionColor.a = Mathf.Clamp01(Mathf.Max(resolvedSelectionColor.a, 0.14f));
            selectedBackgroundColor = resolvedSelectionColor;
        }

        panelBackgroundColor = new Color(0.0f, 0.08f, 0.0f, 0.72f);
    }

    private float GetBodyFontSize()
    {
        return Mathf.Max(8.0f, dialogueFontSize);
    }

    private float GetNpcNameFontSize()
    {
        return GetBodyFontSize() * (NpcNameFontSizeAtReference / ReferenceDialogueFontSize);
    }

    private float GetArrowFontSize()
    {
        return GetBodyFontSize() * (ArrowFontSizeAtReference / ReferenceDialogueFontSize);
    }

    private float ScaleFromReference(float referenceValue)
    {
        return referenceValue * (GetBodyFontSize() / ReferenceDialogueFontSize);
    }

    private void EnterNode(DialogueNodeDefinition node)
    {
        currentNode = node;
        SetNpcName(ResolveSpeakerName(node));

        currentChoices.Clear();
        if (node != null)
        {
            List<DialogueChoiceDefinition> nodeChoices = node.GetChoices();
            if (nodeChoices != null)
            {
                for (int i = 0; i < nodeChoices.Count; i++)
                {
                    DialogueChoiceDefinition choice = nodeChoices[i];
                    if (choice != null)
                        currentChoices.Add(choice);
                }
            }
        }

        selectedChoiceIndex = 0;
        visibleChoiceStartIndex = 0;

        string dialogueText = node != null ? node.GetDialogueText() : string.Empty;
        if (!string.IsNullOrWhiteSpace(dialogueText))
        {
            ShowPlayback(dialogueText.Trim());
            return;
        }

        AdvanceFromPlaybackState();
    }

    private void ShowPlayback(string dialogueText)
    {
        isPlaybackVisible = true;
        isShowingChoices = false;

        if (playbackText)
            playbackText.fontSize = GetBodyFontSize();

        playbackText.text = dialogueText;
        LayoutPlaybackPanel(dialogueText);

        SetPlaybackVisible(true);
        SetOptionsVisible(false);

    }

    private void AdvanceFromPlaybackState()
    {
        isPlaybackVisible = false;

        if (currentChoices.Count > 0)
        {
            ShowChoices();
            return;
        }

        if (currentNode == null || currentNode.ShouldExitDialogueIfNoChoices())
        {
            Close();
            return;
        }

        Close();
    }

    private void ShowChoices()
    {
        isShowingChoices = true;
        SetPlaybackVisible(false);
        SetOptionsVisible(true);
        RefreshChoicesView();
    }

    private void RefreshChoicesView()
    {
        UpdatePanelWidth(optionsPanelRect, GetConfiguredPanelWidth());

        float optionsPanelVerticalPadding = ScaleFromReference(OptionsPanelVerticalPadding);
        float arrowColumnWidth = ArrowColumnWidth;
        float choiceTextLeftPadding = ChoiceTextLeftPadding;
        float choiceTextRightPadding = ChoiceTextRightPadding;
        float rowMinimumHeight = ScaleFromReference(RowMinimumHeight);
        float rowVerticalPadding = ScaleFromReference(RowVerticalPadding);
        float rowVerticalInset = ScaleFromReference(RowTextVerticalInset);
        float rowSpacing = ScaleFromReference(RowSpacing);

        int visibleChoiceCount = Mathf.Min(MaxVisibleChoicesWithoutScroll, currentChoices.Count);
        float availableWidth = GetCurrentPanelWidth(optionsPanelRect);
        float rowContentWidth = Mathf.Max(120.0f, availableWidth - arrowColumnWidth - choiceTextLeftPadding - choiceTextRightPadding);
        bool canScroll = currentChoices.Count > MaxVisibleChoicesWithoutScroll;
        bool canScrollUp = visibleChoiceStartIndex > 0;
        bool canScrollDown = visibleChoiceStartIndex + visibleChoiceCount < currentChoices.Count;
        RectTransform selectedRowRect = null;

        float currentY = optionsPanelVerticalPadding;
        for (int i = 0; i < optionRows.Count; i++)
        {
            DialogueOptionRow row = optionRows[i];
            if (row == null || row.root == null)
                continue;

            bool isVisible = i < visibleChoiceCount;
            row.root.gameObject.SetActive(isVisible);
            if (!isVisible)
                continue;

            int choiceIndex = visibleChoiceStartIndex + i;
            DialogueChoiceDefinition choice = currentChoices[choiceIndex];
            string playerText = choice != null ? choice.GetPlayerText() : string.Empty;
            row.label.fontSize = GetBodyFontSize();
            row.label.text = string.IsNullOrWhiteSpace(playerText) ? "..." : playerText.Trim();

            float rowHeight = Mathf.Max(rowMinimumHeight, CalculatePreferredTextHeight(row.label, rowContentWidth) + rowVerticalPadding);
            row.root.anchorMin = new Vector2(0.0f, 1.0f);
            row.root.anchorMax = new Vector2(1.0f, 1.0f);
            row.root.pivot = new Vector2(0.0f, 1.0f);
            row.root.anchoredPosition = new Vector2(0.0f, -currentY);
            row.root.sizeDelta = new Vector2(0.0f, rowHeight);

            row.label.rectTransform.anchorMin = Vector2.zero;
            row.label.rectTransform.anchorMax = Vector2.one;
            row.label.rectTransform.offsetMin = new Vector2(arrowColumnWidth + choiceTextLeftPadding, rowVerticalInset);
            row.label.rectTransform.offsetMax = new Vector2(-choiceTextRightPadding, -rowVerticalInset);

            bool isSelected = choiceIndex == selectedChoiceIndex;
            if (isSelected)
                selectedRowRect = row.root;

            currentY += rowHeight + rowSpacing;
        }

        float contentHeight = visibleChoiceCount > 0
            ? currentY - rowSpacing + optionsPanelVerticalPadding
            : optionsPanelVerticalPadding * 2.0f;

        float optionsPanelTopY = optionsPanelRect.anchoredPosition.y + optionsPanelRect.sizeDelta.y;
        Vector2 optionsPanelSize = optionsPanelRect.sizeDelta;
        optionsPanelSize.y = contentHeight;
        optionsPanelRect.sizeDelta = optionsPanelSize;
        Vector2 optionsPanelPosition = optionsPanelRect.anchoredPosition;
        optionsPanelPosition.y = optionsPanelTopY - contentHeight;
        optionsPanelRect.anchoredPosition = optionsPanelPosition;
        optionsContentRect.sizeDelta = new Vector2(0.0f, contentHeight);

        UpdateOptionsSelectionHighlight(selectedRowRect);
        PositionOptionsFrame(contentHeight, canScroll, canScrollUp, canScrollDown, optionsPanelVerticalPadding, arrowColumnWidth);
    }

    private void UpdateOptionsSelectionHighlight(RectTransform selectedRowRect)
    {
        if (!optionsSelectionOutlineRect)
            return;

        bool shouldShowHighlight = isShowingChoices && selectedRowRect;
        optionsSelectionOutlineRect.gameObject.SetActive(shouldShowHighlight);
        if (!shouldShowHighlight)
            return;

        optionsSelectionOutlineRect.anchorMin = selectedRowRect.anchorMin;
        optionsSelectionOutlineRect.anchorMax = selectedRowRect.anchorMax;
        optionsSelectionOutlineRect.pivot = selectedRowRect.pivot;
        optionsSelectionOutlineRect.anchoredPosition = selectedRowRect.anchoredPosition;
        optionsSelectionOutlineRect.sizeDelta = selectedRowRect.sizeDelta;
        optionsSelectionOutlineRect.localScale = Vector3.one;

        SetRectTransformBeforeSibling(optionsSelectionOutlineRect, selectedRowRect);
    }

    private void PositionOptionsFrame(float panelHeight, bool canScroll, bool canScrollUp, bool canScrollDown, float optionsPanelVerticalPadding, float arrowColumnWidth)
    {
        float topBottomLineHeight = ScaleFromReference(TopBottomLineHeight);
        float leftScrollGuideWidth = ScaleFromReference(LeftScrollGuideWidth);
        float arrowInsetX = ArrowInset;
        float arrowInsetY = ScaleFromReference(ArrowInset);
        float scrollGuideOffset = ScrollGuideXOffset;
        float arrowSize = ScaleFromReference(ArrowSize);

        optionsTopLineRect.anchorMin = new Vector2(0.0f, 1.0f);
        optionsTopLineRect.anchorMax = new Vector2(1.0f, 1.0f);
        optionsTopLineRect.pivot = new Vector2(0.0f, 1.0f);
        optionsTopLineRect.anchoredPosition = Vector2.zero;
        optionsTopLineRect.sizeDelta = new Vector2(0.0f, topBottomLineHeight);

        optionsBottomLineRect.anchorMin = new Vector2(0.0f, 0.0f);
        optionsBottomLineRect.anchorMax = new Vector2(1.0f, 0.0f);
        optionsBottomLineRect.pivot = new Vector2(0.0f, 0.0f);
        optionsBottomLineRect.anchoredPosition = Vector2.zero;
        optionsBottomLineRect.sizeDelta = new Vector2(0.0f, topBottomLineHeight);

        optionsScrollGuideRect.gameObject.SetActive(canScroll);
        if (canScroll)
        {
            optionsScrollGuideRect.anchorMin = new Vector2(0.0f, 0.0f);
            optionsScrollGuideRect.anchorMax = new Vector2(0.0f, 1.0f);
            optionsScrollGuideRect.pivot = new Vector2(0.0f, 1.0f);
            optionsScrollGuideRect.anchoredPosition = new Vector2(arrowColumnWidth - scrollGuideOffset, -optionsPanelVerticalPadding);
            optionsScrollGuideRect.sizeDelta = new Vector2(leftScrollGuideWidth, -optionsPanelVerticalPadding * 2.0f);
        }

        LayoutArrowLabel(scrollUpText, canScroll && canScrollUp, new Vector2(arrowInsetX, -arrowInsetY), arrowSize);
        LayoutArrowLabel(scrollDownText, canScroll && canScrollDown, new Vector2(arrowInsetX, -(panelHeight - arrowInsetY)), arrowSize);
    }

    private void LayoutArrowLabel(TMP_Text label, bool visible, Vector2 anchoredPosition, float size)
    {
        if (!label)
            return;

        label.fontSize = GetArrowFontSize();
        label.gameObject.SetActive(visible);
        if (!visible)
            return;

        RectTransform labelRect = label.rectTransform;
        labelRect.anchorMin = new Vector2(0.0f, 1.0f);
        labelRect.anchorMax = new Vector2(0.0f, 1.0f);
        labelRect.pivot = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = anchoredPosition;
        labelRect.sizeDelta = new Vector2(size, size);
    }

    private void LayoutPlaybackPanel(string dialogueText)
    {
        UpdatePanelWidth(playbackPanelRect, GetConfiguredPanelWidth());

        float playbackTextLeftPadding = PlaybackTextLeftPadding;
        float playbackTextRightPadding = PlaybackTextRightPadding;
        float playbackVerticalPadding = ScaleFromReference(PlaybackVerticalPadding);
        float playbackTextVerticalInset = ScaleFromReference(PlaybackTextVerticalInset);
        float playbackMinHeight = ScaleFromReference(PlaybackMinHeight);

        float availableWidth = GetCurrentPanelWidth(playbackPanelRect);
        float textWidth = Mathf.Max(180.0f, availableWidth - playbackTextLeftPadding - playbackTextRightPadding);
        float textHeight = CalculatePreferredTextHeight(playbackText, textWidth);
        float panelHeight = Mathf.Max(playbackMinHeight, textHeight + playbackVerticalPadding * 2.0f);

        Vector2 playbackPanelSize = playbackPanelRect.sizeDelta;
        playbackPanelSize.y = panelHeight;
        playbackPanelRect.sizeDelta = playbackPanelSize;
        playbackText.rectTransform.anchorMin = Vector2.zero;
        playbackText.rectTransform.anchorMax = Vector2.one;
        playbackText.rectTransform.offsetMin = new Vector2(playbackTextLeftPadding, playbackTextVerticalInset);
        playbackText.rectTransform.offsetMax = new Vector2(-playbackTextRightPadding, -playbackTextVerticalInset);
    }

    private void ProcessChoiceNavigation()
    {
        int direction = 0;

        if (WasMoveUpPressed())
            direction = -1;
        else if (WasMoveDownPressed())
            direction = 1;
        else
            direction = ReadHeldNavigateDirection();

        if (direction == 0)
        {
            hasCachedNavigateInput = false;
            return;
        }

        if (!MoveSelection(direction))
            return;

        if (!hasCachedNavigateInput)
        {
            hasCachedNavigateInput = true;
            nextNavigateRepeatUnscaledTime = Time.unscaledTime + NavigateRepeatDelaySeconds;
        }
        else
        {
            nextNavigateRepeatUnscaledTime = Time.unscaledTime + NavigateRepeatRateSeconds;
        }
    }

    private int ReadHeldNavigateDirection()
    {
        if (controls == null)
            return 0;

        Vector2 navigateValue = controls.UI.Navigate.ReadValue<Vector2>();
        if (Mathf.Abs(navigateValue.y) < 0.5f || Time.unscaledTime < nextNavigateRepeatUnscaledTime)
            return 0;

        return navigateValue.y > 0.0f ? -1 : 1;
    }

    private bool MoveSelection(int direction)
    {
        if (currentChoices.Count == 0)
            return false;

        int newIndex = Mathf.Clamp(selectedChoiceIndex + direction, 0, currentChoices.Count - 1);
        if (newIndex == selectedChoiceIndex)
            return false;

        selectedChoiceIndex = newIndex;
        EnsureSelectionVisible();
        RefreshChoicesView();
        return true;
    }

    private void EnsureSelectionVisible()
    {
        int maxStartIndex = Mathf.Max(0, currentChoices.Count - MaxVisibleChoicesWithoutScroll);
        if (selectedChoiceIndex < visibleChoiceStartIndex)
            visibleChoiceStartIndex = selectedChoiceIndex;
        else if (selectedChoiceIndex >= visibleChoiceStartIndex + MaxVisibleChoicesWithoutScroll)
            visibleChoiceStartIndex = selectedChoiceIndex - MaxVisibleChoicesWithoutScroll + 1;

        visibleChoiceStartIndex = Mathf.Clamp(visibleChoiceStartIndex, 0, maxStartIndex);
    }

    private void SelectChoice(int choiceIndex)
    {
        if (choiceIndex < 0 || choiceIndex >= currentChoices.Count)
            return;

        DialogueChoiceDefinition choice = currentChoices[choiceIndex];
        if (choice == null)
            return;

        ExecuteExternalActions(choice.GetExternalActions());

        if (choice.ShouldExitDialogue() || !choice.HasNextNode())
        {
            Close();
            return;
        }

        if (activeTree != null && activeTree.TryGetNode(choice.GetNextNodeId(), out DialogueNodeDefinition nextNode))
        {
            EnterNode(nextNode);
            return;
        }

        Debug.LogWarning($"DialogueController could not resolve next node '{choice.GetNextNodeId()}'. Closing dialogue.", this);
        Close();
    }

    private void ExecuteExternalActions(List<DialogueExternalActionDefinition> actions)
    {
        if (actions == null || actions.Count == 0)
            return;

        for (int i = 0; i < actions.Count; i++)
        {
            DialogueExternalActionDefinition action = actions[i];
            if (action == null)
                continue;

            switch (action.GetActionType())
            {
                case DialogueExternalActionType.None:
                    break;
                case DialogueExternalActionType.OpenDoor:
                    SendTargetMessage(action.GetTargetId(), "Open");
                    break;
                case DialogueExternalActionType.UnlockDoor:
                    SendTargetMessage(action.GetTargetId(), "Unlock");
                    break;
                case DialogueExternalActionType.CustomSignal:
                    string signalName = string.IsNullOrWhiteSpace(action.GetStringParameter()) ? action.GetTargetId() : action.GetStringParameter();
                    if (!string.IsNullOrWhiteSpace(signalName) && activeNpc)
                        activeNpc.gameObject.SendMessage(signalName, SendMessageOptions.DontRequireReceiver);
                    break;
                case DialogueExternalActionType.StartQuest:
                case DialogueExternalActionType.SetQuestStage:
                case DialogueExternalActionType.CompleteQuest:
                case DialogueExternalActionType.FailQuest:
                case DialogueExternalActionType.SetCurrentQuest:
                case DialogueExternalActionType.ClearCurrentQuest:
                case DialogueExternalActionType.DisplayQuestObjective:
                case DialogueExternalActionType.CompleteQuestObjective:
                case DialogueExternalActionType.FailQuestObjective:
                case DialogueExternalActionType.HideQuestObjective:
                case DialogueExternalActionType.SetCurrentQuestObjective:
                    ExecuteQuestAction(action);
                    break;
                default:
                    Debug.LogWarning($"DialogueController encountered unsupported dialogue action '{action.GetActionType()}'.", this);
                    break;
            }
        }
    }

    private void ExecuteQuestAction(DialogueExternalActionDefinition action)
    {
        if (action == null)
            return;

        string questId = string.IsNullOrWhiteSpace(action.GetTargetId())
            ? action.GetStringParameter()
            : action.GetTargetId();
        questId = string.IsNullOrWhiteSpace(questId) ? string.Empty : questId.Trim();

        if (action.GetActionType() != DialogueExternalActionType.ClearCurrentQuest && string.IsNullOrWhiteSpace(questId))
        {
            Debug.LogWarning($"DialogueController quest action '{action.GetActionType()}' has no quest id.", this);
            return;
        }

        QuestController questController = QuestController.FindOrCreate();
        bool handled = action.GetActionType() switch
        {
            DialogueExternalActionType.StartQuest => questController.StartQuest(questId),
            DialogueExternalActionType.SetQuestStage => questController.SetQuestStage(questId, action.GetStageValue()),
            DialogueExternalActionType.CompleteQuest => questController.CompleteQuest(questId),
            DialogueExternalActionType.FailQuest => questController.FailQuest(questId),
            DialogueExternalActionType.SetCurrentQuest => questController.SetCurrentQuest(questId),
            DialogueExternalActionType.ClearCurrentQuest => questController.ClearCurrentQuest(),
            DialogueExternalActionType.DisplayQuestObjective => questController.DisplayObjective(questId, action.GetStageValue()),
            DialogueExternalActionType.CompleteQuestObjective => questController.CompleteObjective(questId, action.GetStageValue()),
            DialogueExternalActionType.FailQuestObjective => questController.FailObjective(questId, action.GetStageValue()),
            DialogueExternalActionType.HideQuestObjective => questController.HideObjective(questId, action.GetStageValue()),
            DialogueExternalActionType.SetCurrentQuestObjective => questController.SetCurrentObjective(questId, action.GetStageValue()),
            _ => false
        };

        if (!handled)
            Debug.LogWarning($"DialogueController could not execute quest action '{action.GetActionType()}' for quest id '{questId}' and value {action.GetStageValue()}. Make sure the QuestController has that QuestDefinition registered and the stage/objective id exists.", this);
    }

    private void SendTargetMessage(string targetId, string message)
    {
        if (string.IsNullOrWhiteSpace(targetId))
            return;

        GameObject target = FindSceneGameObjectByName(targetId.Trim());
        if (!target)
        {
            Debug.LogWarning($"DialogueController could not find scene target '{targetId}' for message '{message}'.", this);
            return;
        }

        target.SendMessage(message, SendMessageOptions.DontRequireReceiver);
    }

    private void SetNpcName(string speakerName)
    {
        if (!npcNameText)
            return;

        npcNameText.fontSize = GetNpcNameFontSize();
        npcNameText.text = string.IsNullOrWhiteSpace(speakerName) ? "NPC" : speakerName.Trim();
    }

    private string ResolveSpeakerName(DialogueNodeDefinition node)
    {
        if (node != null)
        {
            string overrideName = node.GetSpeakerNameOverride();
            if (!string.IsNullOrWhiteSpace(overrideName))
                return overrideName.Trim();
        }

        if (activeNpc && !string.IsNullOrWhiteSpace(activeNpc.GetNPCName()))
            return activeNpc.GetNPCName().Trim();

        if (activeDialogueDefinition && !string.IsNullOrWhiteSpace(activeDialogueDefinition.GetDialogueName()))
            return activeDialogueDefinition.GetDialogueName().Trim();

        return "NPC";
    }

    private void SetOpenState(bool open)
    {
        SetOpenState(open, false);
    }

    private void SetOpenState(bool open, bool forceWithoutSideEffects)
    {
        bool wasOpen = isOpen;

        if (!forceWithoutSideEffects && !wasOpen && open && !gameObject.activeSelf)
            gameObject.SetActive(true);

        isOpen = open;

        if (isOpen)
            SetDialogueHierarchyActive(true);

        if (dialogueCanvasGroup)
        {
            dialogueCanvasGroup.alpha = isOpen ? 1.0f : 0.0f;
            dialogueCanvasGroup.interactable = isOpen;
            dialogueCanvasGroup.blocksRaycasts = isOpen;
        }

        if (forceWithoutSideEffects || wasOpen == isOpen)
        {
            if (!isOpen)
            {
                EndDialogueParticipantOverrides();
                if (disableInHierarchyWhenClosed)
                    SetDialogueHierarchyActive(false);
            }

            return;
        }

        if (isOpen)
        {
            EnforceFixedPanelWidths();
            ApplyTypography();

            if (pauseGameWhenOpen && !hasCachedTimeScale)
            {
                cachedTimeScale = Time.timeScale;
                hasCachedTimeScale = true;
                Time.timeScale = 0.0f;
            }

            if (cameraRigOrbit)
                cameraRigOrbit.SetInputEnabled(false);

            if (hidePlayerHudWhenOpen)
                SetPlayerHudVisible(false);

            if (hideCrosshairWhenOpen)
                SetCrosshairVisible(false);

            if (controls != null)
            {
                controls.UI.Enable();
                if (disableGameplayActionsWhenOpen)
                    SetGameplayActionsEnabled(false);
            }

            BeginDialogueParticipantOverrides();

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            return;
        }

        EndDialogueParticipantOverrides();
        currentChoices.Clear();
        currentNode = null;
        activeNpc = null;
        activeInteractor = null;
        activeTree = null;
        activeDialogueDefinition = null;
        isPlaybackVisible = false;
        isShowingChoices = false;
        selectedChoiceIndex = 0;
        visibleChoiceStartIndex = 0;
        hasCachedNavigateInput = false;

        if (controls != null)
        {
            if (disableGameplayActionsWhenOpen)
                SetGameplayActionsEnabled(true);

            controls.UI.Disable();
        }

        if (hideCrosshairWhenOpen)
            SetCrosshairVisible(true);

        if (hidePlayerHudWhenOpen)
            SetPlayerHudVisible(true);

        if (pauseGameWhenOpen && hasCachedTimeScale)
        {
            Time.timeScale = cachedTimeScale;
            hasCachedTimeScale = false;
        }

        if (cameraRigOrbit)
            cameraRigOrbit.SetInputEnabled(true);
        else
        {
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        SetPlaybackVisible(false);
        SetOptionsVisible(false);
        UpdateOptionsSelectionHighlight(null);

        if (disableInHierarchyWhenClosed)
            SetDialogueHierarchyActive(false);
    }

    private void SetDialogueHierarchyActive(bool active)
    {
        if (gameObject.activeSelf != active)
            gameObject.SetActive(active);
    }

    private void SetPlaybackVisible(bool visible)
    {
        if (playbackPanelRect && playbackPanelRect.gameObject.activeSelf != visible)
            playbackPanelRect.gameObject.SetActive(visible);
    }

    private void SetOptionsVisible(bool visible)
    {
        if (optionsPanelRect && optionsPanelRect.gameObject.activeSelf != visible)
            optionsPanelRect.gameObject.SetActive(visible);
    }

    private void BeginDialogueParticipantOverrides()
    {
        EndDialogueParticipantOverrides();

        CacheDialogueParticipant(activeInteractor);
        if (activeNpc)
            CacheDialogueParticipant(activeNpc.gameObject);
        CacheDialogueParticipantRigBuilders(activeInteractor);
        if (activeNpc)
            CacheDialogueParticipantRigBuilders(activeNpc.gameObject);

        activeInteractorRootRigidbody = activeInteractor ? activeInteractor.GetComponent<Rigidbody>() : null;
        activeNpcRootRigidbody = activeNpc ? activeNpc.GetComponent<Rigidbody>() : null;
        BeginDialogueRootBodyOverride(activeInteractorRootRigidbody, ref activeInteractorRootBodyState);
        BeginDialogueRootBodyOverride(activeNpcRootRigidbody, ref activeNpcRootBodyState);

        EnforceDialogueParticipantsIdleMotion();
    }

    private void EndDialogueParticipantOverrides()
    {
        for (int i = 0; i < dialogueAnimatorStates.Count; i++)
        {
            DialogueAnimatorState state = dialogueAnimatorStates[i];
            if (state.animator)
                state.animator.updateMode = state.updateMode;
        }

        SynchronizeDialogueParticipantAnimationPose();
        dialogueAnimatorStates.Clear();
        dialogueParticipantAnimators.Clear();
        dialogueParticipantRigBuilders.Clear();
        EndDialogueRootBodyOverride(ref activeInteractorRootBodyState);
        EndDialogueRootBodyOverride(ref activeNpcRootBodyState);
        activeInteractorRootRigidbody = null;
        activeNpcRootRigidbody = null;
    }

    private static void BeginDialogueRootBodyOverride(Rigidbody body, ref DialogueRootBodyState state)
    {
        state = default;
        if (!body)
            return;

        state.body = body;
        state.wasKinematic = body.isKinematic;
        state.interpolation = body.interpolation;
        state.isOverridden = true;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.interpolation = RigidbodyInterpolation.None;

        if (!state.wasKinematic)
            body.isKinematic = true;
    }

    private static void EndDialogueRootBodyOverride(ref DialogueRootBodyState state)
    {
        if (!state.isOverridden)
            return;

        Rigidbody body = state.body;
        if (body)
        {
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            body.interpolation = state.interpolation;
            body.isKinematic = state.wasKinematic;
        }

        state = default;
    }

    private void CacheDialogueParticipant(GameObject participantRoot)
    {
        if (!participantRoot)
            return;

        Animator[] animators = participantRoot.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            Animator animator = animators[i];
            if (!animator)
                continue;

            if (!IsDialogueParticipantAnimatorTracked(animator))
                dialogueParticipantAnimators.Add(animator);

            if (IsDialogueAnimatorTracked(animator))
                continue;

            DialogueAnimatorState state = new DialogueAnimatorState
            {
                animator = animator,
                updateMode = animator.updateMode
            };

            CacheDialogueAnimatorLocomotionParameters(animator, ref state);
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            dialogueAnimatorStates.Add(state);
        }
    }

    private void CacheDialogueParticipantRigBuilders(GameObject participantRoot)
    {
        if (!participantRoot)
            return;

        RigBuilder[] rigBuilders = participantRoot.GetComponentsInChildren<RigBuilder>(true);
        for (int i = 0; i < rigBuilders.Length; i++)
        {
            RigBuilder rigBuilder = rigBuilders[i];
            if (!rigBuilder || IsDialogueParticipantRigBuilderTracked(rigBuilder))
                continue;

            dialogueParticipantRigBuilders.Add(rigBuilder);
        }
    }

    private static void CacheDialogueAnimatorLocomotionParameters(Animator animator, ref DialogueAnimatorState state)
    {
        if (!animator)
            return;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Float:
                    if (parameter.nameHash == AnimatorXVelocityHash)
                        state.hasXVelocity = true;
                    else if (parameter.nameHash == AnimatorZVelocityHash)
                        state.hasZVelocity = true;

                    break;
                case AnimatorControllerParameterType.Bool:
                    if (parameter.nameHash == AnimatorRunningHash)
                        state.hasRunning = true;
                    else if (parameter.nameHash == AnimatorSprintingHash)
                        state.hasSprinting = true;

                    break;
            }
        }
    }

    private bool IsDialogueAnimatorTracked(Animator animator)
    {
        for (int i = 0; i < dialogueAnimatorStates.Count; i++)
        {
            if (dialogueAnimatorStates[i].animator == animator)
                return true;
        }

        return false;
    }

    private bool IsDialogueParticipantAnimatorTracked(Animator animator)
    {
        for (int i = 0; i < dialogueParticipantAnimators.Count; i++)
        {
            if (dialogueParticipantAnimators[i] == animator)
                return true;
        }

        return false;
    }

    private bool IsDialogueParticipantRigBuilderTracked(RigBuilder rigBuilder)
    {
        for (int i = 0; i < dialogueParticipantRigBuilders.Count; i++)
        {
            if (dialogueParticipantRigBuilders[i] == rigBuilder)
                return true;
        }

        return false;
    }

    private void SynchronizeDialogueParticipantRigPose()
    {
        for (int i = 0; i < dialogueParticipantRigBuilders.Count; i++)
        {
            RigBuilder rigBuilder = dialogueParticipantRigBuilders[i];
            if (!rigBuilder || !rigBuilder.isActiveAndEnabled || !rigBuilder.enabled)
                continue;

            rigBuilder.Build();
        }
    }

    private void SynchronizeDialogueParticipantAnimationPose()
    {
        for (int i = 0; i < dialogueParticipantAnimators.Count; i++)
        {
            Animator animator = dialogueParticipantAnimators[i];
            if (!animator || !animator.isActiveAndEnabled || !animator.isInitialized)
                continue;

            animator.Update(0.0f);
        }
    }

    private void EnforceDialogueParticipantsIdleMotion()
    {
        EnforceDialogueRootBodyIdle(activeInteractorRootRigidbody);
        EnforceDialogueRootBodyIdle(activeNpcRootRigidbody);

        for (int i = 0; i < dialogueAnimatorStates.Count; i++)
        {
            DialogueAnimatorState state = dialogueAnimatorStates[i];
            Animator animator = state.animator;
            if (!animator)
                continue;

            if (state.hasXVelocity)
                animator.SetFloat(AnimatorXVelocityHash, 0.0f);

            if (state.hasZVelocity)
                animator.SetFloat(AnimatorZVelocityHash, 0.0f);

            if (state.hasRunning)
                animator.SetBool(AnimatorRunningHash, false);

            if (state.hasSprinting)
                animator.SetBool(AnimatorSprintingHash, false);
        }
    }

    private static void EnforceDialogueRootBodyIdle(Rigidbody body)
    {
        if (!body || body.isKinematic)
            return;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private void UpdateDialoguePartnerFacing()
    {
        if (!activeNpc || !activeInteractor)
            return;

        Transform npcTransform = activeNpc.transform;
        Transform interactorTransform = activeInteractor.transform;
        if (!npcTransform || !interactorTransform)
            return;

        Vector3 lookDirection = interactorTransform.position - npcTransform.position;
        lookDirection.y = 0.0f;
        if (lookDirection.sqrMagnitude <= MinLookDirectionSqr)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
        Rigidbody npcBody = ResolveDialogueNpcBody(npcTransform);
        Quaternion currentRotation = npcTransform.rotation;
        float turnStep = dialoguePartnerTurnSpeedDegreesPerSecond * Time.unscaledDeltaTime;
        Quaternion nextRotation = turnStep <= 0.0f
            ? targetRotation
            : Quaternion.RotateTowards(currentRotation, targetRotation, turnStep);
        bool rotationChanged = Quaternion.Angle(currentRotation, nextRotation) > 0.001f;

        npcTransform.rotation = nextRotation;

        if (npcBody)
        {
            if (!npcBody.isKinematic)
                npcBody.angularVelocity = Vector3.zero;

            npcBody.rotation = nextRotation;
        }

        if (rotationChanged)
        {
            SynchronizeDialogueParticipantRigPose();
            SynchronizeDialogueParticipantAnimationPose();
        }
    }

    private Rigidbody ResolveDialogueNpcBody(Transform npcTransform)
    {
        if (activeNpcRootRigidbody && activeNpcRootRigidbody.transform == npcTransform)
            return activeNpcRootRigidbody;

        Rigidbody resolvedBody = activeNpc ? activeNpc.GetComponent<Rigidbody>() : null;
        if (resolvedBody && resolvedBody.transform == npcTransform)
            return resolvedBody;

        return null;
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
        }
        else
        {
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
    }

    private CanvasGroup FindPlayerHudCanvasGroup()
    {
        UI.FalloutHUDController hudController = FindAnyObjectByType<UI.FalloutHUDController>(FindObjectsInactive.Include);
        if (!hudController)
            return null;

        CanvasGroup hudCanvas = hudController.GetComponent<CanvasGroup>();
        if (!hudCanvas)
            hudCanvas = hudController.gameObject.AddComponent<CanvasGroup>();

        return hudCanvas;
    }

    private CanvasGroup FindCrosshairCanvasGroup()
    {
        UI.CrosshairController crosshairController = FindAnyObjectByType<UI.CrosshairController>(FindObjectsInactive.Include);
        if (!crosshairController)
            return null;

        CanvasGroup crosshairCanvas = crosshairController.GetComponent<CanvasGroup>();
        if (!crosshairCanvas)
            crosshairCanvas = crosshairController.gameObject.AddComponent<CanvasGroup>();

        return crosshairCanvas;
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

    private void SetCrosshairVisible(bool visible)
    {
        if (!crosshairCanvasGroup)
            crosshairCanvasGroup = FindCrosshairCanvasGroup();

        if (!crosshairCanvasGroup)
            return;

        if (!visible)
        {
            if (!hasCachedCrosshairState)
            {
                cachedCrosshairAlpha = crosshairCanvasGroup.alpha;
                cachedCrosshairInteractable = crosshairCanvasGroup.interactable;
                cachedCrosshairBlocksRaycasts = crosshairCanvasGroup.blocksRaycasts;
                hasCachedCrosshairState = true;
            }

            crosshairCanvasGroup.alpha = 0.0f;
            crosshairCanvasGroup.interactable = false;
            crosshairCanvasGroup.blocksRaycasts = false;
            return;
        }

        if (!hasCachedCrosshairState)
            return;

        crosshairCanvasGroup.alpha = cachedCrosshairAlpha;
        crosshairCanvasGroup.interactable = cachedCrosshairInteractable;
        crosshairCanvasGroup.blocksRaycasts = cachedCrosshairBlocksRaycasts;
        hasCachedCrosshairState = false;
    }

    private void RefreshEditorPreview()
    {
        if (Application.isPlaying || !showInEditMode || !gameObject.activeSelf)
            return;

        if (dialogueCanvasGroup)
        {
            dialogueCanvasGroup.alpha = 1.0f;
            dialogueCanvasGroup.interactable = false;
            dialogueCanvasGroup.blocksRaycasts = false;
        }

        if (npcNameText && string.IsNullOrWhiteSpace(npcNameText.text))
            npcNameText.text = EditorPreviewNpcName;

        if (playbackText)
        {
            if (string.IsNullOrWhiteSpace(playbackText.text))
                playbackText.text = EditorPreviewPlaybackText;

            LayoutPlaybackPanel(playbackText.text);
        }

        bool isPlaybackPanelVisible = playbackPanelRect && playbackPanelRect.gameObject.activeSelf;
        bool isOptionsPanelVisible = optionsPanelRect && optionsPanelRect.gameObject.activeSelf;
        if (!isPlaybackPanelVisible && !isOptionsPanelVisible)
            SetPlaybackVisible(true);

        UpdateOptionsSelectionHighlight(null);
    }

    private void QueueEditorPreviewRefresh()
    {
#if UNITY_EDITOR
        if (Application.isPlaying || editorPreviewRefreshQueued || IsPrefabAsset())
            return;

        editorPreviewRefreshQueued = true;
        EditorApplication.delayCall -= RunQueuedEditorPreviewRefresh;
        EditorApplication.delayCall += RunQueuedEditorPreviewRefresh;
#endif
    }

    private void CancelQueuedEditorPreviewRefresh()
    {
#if UNITY_EDITOR
        if (!editorPreviewRefreshQueued)
            return;

        editorPreviewRefreshQueued = false;
        EditorApplication.delayCall -= RunQueuedEditorPreviewRefresh;
#endif
    }

#if UNITY_EDITOR
    private bool IsPrefabAsset()
    {
        return this && PrefabUtility.IsPartOfPrefabAsset(gameObject);
    }

    private void RunQueuedEditorPreviewRefresh()
    {
        EditorApplication.delayCall -= RunQueuedEditorPreviewRefresh;
        editorPreviewRefreshQueued = false;

        if (!this || Application.isPlaying || IsPrefabAsset() || !gameObject.activeSelf)
            return;

        EnsureInitialized();
        ApplyTypography();
        ApplyTheme();
        RefreshEditorPreview();
    }
#endif

    private bool IsPlaybackAdvanceRequested()
    {
        if (!isPlaybackVisible)
            return false;

        Mouse mouse = Mouse.current;
        return mouse != null && mouse.leftButton.wasPressedThisFrame;
    }

    private bool WasChoiceSubmitPressed()
    {
        if (controls != null && controls.UI.Submit.WasPressedThisFrame())
            return true;

        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame);
    }

    private bool WasCancelPressed()
    {
        if (controls != null && controls.UI.Cancel.WasPressedThisFrame())
            return true;

        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
    }

    private bool WasMoveUpPressed()
    {
        if (controls != null)
        {
            Vector2 navigateValue = controls.UI.Navigate.ReadValue<Vector2>();
            if (navigateValue.y > 0.5f && !hasCachedNavigateInput)
                return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.scroll.ReadValue().y > 0.0f)
            return true;

        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.upArrowKey.wasPressedThisFrame || keyboard.wKey.wasPressedThisFrame);
    }

    private bool WasMoveDownPressed()
    {
        if (controls != null)
        {
            Vector2 navigateValue = controls.UI.Navigate.ReadValue<Vector2>();
            if (navigateValue.y < -0.5f && !hasCachedNavigateInput)
                return true;
        }

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.scroll.ReadValue().y < 0.0f)
            return true;

        Keyboard keyboard = Keyboard.current;
        return keyboard != null && (keyboard.downArrowKey.wasPressedThisFrame || keyboard.sKey.wasPressedThisFrame);
    }

    private DialogueOptionRow ResolveOptionRow(int slotIndex)
    {
        RectTransform rowRect = FindRect($"OptionRow{slotIndex}", optionsContentRect);
        rowRect = rowRect ? rowRect : CreateRect($"OptionRow{slotIndex}", optionsContentRect);
        Image buttonImage = EnsureImageComponent(rowRect.gameObject);
        buttonImage.color = Color.clear;
        buttonImage.raycastTarget = true;

        Button button = rowRect.gameObject.GetComponent<Button>() ?? rowRect.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = buttonImage;

        RectTransform outlineRect = FindRect("Outline", rowRect);
        outlineRect = outlineRect ? outlineRect : CreateRect("Outline", rowRect);
        outlineRect.anchorMin = Vector2.zero;
        outlineRect.anchorMax = Vector2.one;
        outlineRect.offsetMin = Vector2.zero;
        outlineRect.offsetMax = Vector2.zero;
        outlineRect.localScale = Vector3.one;
        Image outlineFill = outlineRect.GetComponent<Image>();
        if (outlineFill)
        {
            outlineFill.color = Color.clear;
            outlineFill.raycastTarget = false;
            outlineFill.enabled = false;
        }
        RectTransform backgroundRect = FindRect("Background", rowRect);
        if (backgroundRect)
        {
            Image background = backgroundRect.GetComponent<Image>();
            if (background)
            {
                background.raycastTarget = false;
                background.enabled = false;
            }

            backgroundRect.gameObject.SetActive(false);
        }
        outlineRect.gameObject.SetActive(false);

        TMP_Text label = EnsureText("Label", rowRect, TextAlignmentOptions.TopLeft, GetBodyFontSize(), string.Empty);
        ConfigureText(label, TextAlignmentOptions.TopLeft, GetBodyFontSize(), string.Empty);
        label.rectTransform.SetAsLastSibling();

        EventTrigger eventTrigger = rowRect.gameObject.GetComponent<EventTrigger>() ?? rowRect.gameObject.AddComponent<EventTrigger>();
        eventTrigger.triggers ??= new List<EventTrigger.Entry>();
        eventTrigger.triggers.Clear();

        int capturedSlotIndex = slotIndex;
        AddEventTrigger(eventTrigger, EventTriggerType.PointerEnter, _ => OnRowPointerEnter(capturedSlotIndex));
        AddEventTrigger(eventTrigger, EventTriggerType.PointerClick, _ => OnRowPointerClick(capturedSlotIndex));

        return new DialogueOptionRow
        {
            root = rowRect,
            outlineRoot = outlineRect,
            background = null,
            button = button,
            label = label,
            eventTrigger = eventTrigger
        };
    }

    private void OnRowPointerEnter(int slotIndex)
    {
        int choiceIndex = visibleChoiceStartIndex + slotIndex;
        if (choiceIndex < 0 || choiceIndex >= currentChoices.Count || choiceIndex == selectedChoiceIndex)
            return;

        selectedChoiceIndex = choiceIndex;
        RefreshChoicesView();
    }

    private void OnRowPointerClick(int slotIndex)
    {
        SelectChoice(visibleChoiceStartIndex + slotIndex);
    }

    private static void AddEventTrigger(EventTrigger eventTrigger, EventTriggerType triggerType, Action<BaseEventData> action)
    {
        if (eventTrigger == null || action == null)
            return;

        EventTrigger.Entry entry = new EventTrigger.Entry { eventID = triggerType };
        entry.callback.AddListener(eventData => action(eventData));
        eventTrigger.triggers.Add(entry);
    }

    private static void SetOptionRowOutlineVisible(DialogueOptionRow row, bool visible)
    {
        if (row == null || !row.outlineRoot)
            return;

        row.outlineRoot.gameObject.SetActive(visible);
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

    private static void ConfigureSelectionOutlineSegment(
        RectTransform rectTransform,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 pivot,
        Vector2 anchoredPosition,
        Vector2 sizeDelta)
    {
        if (!rectTransform)
            return;

        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = pivot;
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = sizeDelta;
        rectTransform.localScale = Vector3.one;
    }

    private static DialogueController FindExistingController()
    {
        DialogueController[] controllers = Resources.FindObjectsOfTypeAll<DialogueController>();
        DialogueController authoredActiveInScene = null;
        DialogueController authoredInactiveInScene = null;
        DialogueController generatedActiveInScene = null;
        DialogueController generatedInactiveInScene = null;

        for (int i = 0; i < controllers.Length; i++)
        {
            DialogueController controller = controllers[i];
            if (!controller)
                continue;

            GameObject controllerObject = controller.gameObject;
            if (!controllerObject.scene.IsValid() || !controllerObject.scene.isLoaded)
                continue;

            bool isActive = controllerObject.activeInHierarchy;
            if (controller.isRuntimeGeneratedInstance)
            {
                if (isActive)
                    generatedActiveInScene ??= controller;
                else
                    generatedInactiveInScene ??= controller;

                continue;
            }

            if (isActive)
                authoredActiveInScene ??= controller;
            else
                authoredInactiveInScene ??= controller;
        }

        return authoredActiveInScene
            ? authoredActiveInScene
            : authoredInactiveInScene
                ? authoredInactiveInScene
                : generatedActiveInScene
                    ? generatedActiveInScene
                    : generatedInactiveInScene;
    }

    private static DialogueController EnsureRuntimeInstanceExists()
    {
        DialogueController existing = FindExistingController();
        if (existing)
            return existing;

        Canvas targetCanvas = ResolveTargetCanvas();
        if (!targetCanvas)
            targetCanvas = CreateFallbackCanvas();

        GameObject dialogueObject = new GameObject("DialogueUI", typeof(RectTransform), typeof(CanvasGroup), typeof(DialogueController));
        RectTransform dialogueRect = dialogueObject.GetComponent<RectTransform>();
        dialogueRect.SetParent(targetCanvas.transform, false);
        dialogueRect.anchorMin = Vector2.zero;
        dialogueRect.anchorMax = Vector2.one;
        dialogueRect.offsetMin = Vector2.zero;
        dialogueRect.offsetMax = Vector2.zero;
        dialogueRect.SetAsLastSibling();
        DialogueController controller = dialogueObject.GetComponent<DialogueController>();
        if (controller)
            controller.isRuntimeGeneratedInstance = true;

        return controller;
    }

    private static Canvas ResolveTargetCanvas()
    {
        UI.CrosshairController crosshairController = FindAnyObjectByType<UI.CrosshairController>(FindObjectsInactive.Include);
        if (crosshairController)
        {
            Canvas crosshairCanvas = crosshairController.GetComponentInParent<Canvas>(true);
            if (crosshairCanvas)
                return crosshairCanvas;
        }

        Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
        Canvas fallbackCanvas = null;
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (!canvas || !canvas.gameObject.scene.IsValid() || !canvas.gameObject.scene.isLoaded)
                continue;

            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return canvas;

            if (!fallbackCanvas)
                fallbackCanvas = canvas;
        }

        return fallbackCanvas;
    }

    private static Canvas CreateFallbackCanvas()
    {
        GameObject canvasObject = new GameObject("DialogueCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        ConfigureCanvasForDialogueUi(canvas);

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        ConfigureCanvasScalerForDialogueUi(scaler);
        return canvas;
    }

    private static void ConfigureCanvasForDialogueUi(Canvas canvas)
    {
        if (!canvas)
            return;

        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    }

    private static void ConfigureCanvasScalerForDialogueUi(CanvasScaler scaler)
    {
        if (!scaler)
            return;

        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920.0f, 1080.0f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private float CalculatePreferredTextHeight(TMP_Text label, float width)
    {
        if (!label)
            return ScaleFromReference(RowMinimumHeight);

        Vector2 preferredValues = label.GetPreferredValues(label.text, width, 0.0f);
        return Mathf.Max(label.fontSize, preferredValues.y);
    }

    private static float GetCurrentPanelWidth(RectTransform panelRect)
    {
        if (!panelRect)
            return 820.0f;

        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
        return panelRect.rect.width > 1.0f ? panelRect.rect.width : 820.0f;
    }

    private static bool TryGetPrivateField<TValue>(object source, string fieldName, out TValue value)
    {
        value = default;
        if (source == null || string.IsNullOrWhiteSpace(fieldName))
            return false;

        FieldInfo fieldInfo = source.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (fieldInfo == null || fieldInfo.FieldType != typeof(TValue))
            return false;

        object rawValue = fieldInfo.GetValue(source);
        if (rawValue is not TValue typedValue)
            return false;

        value = typedValue;
        return true;
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

    private static RectTransform CreatePanel(string name, RectTransform parent, float preferredWidth)
    {
        RectTransform rectTransform = CreateRect(name, parent);
        ConfigurePanel(rectTransform, preferredWidth);
        return rectTransform;
    }

    private static void ConfigurePanel(RectTransform rectTransform, float preferredWidth)
    {
        if (!rectTransform)
            return;

        rectTransform.anchorMin = new Vector2(0.5f, 0.0f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.0f);
        rectTransform.pivot = new Vector2(0.5f, 0.0f);
        rectTransform.anchoredPosition = new Vector2(0.0f, ScreenBottomMarginPixels);
        rectTransform.sizeDelta = new Vector2(GetClampedPanelWidth(rectTransform, preferredWidth), DialoguePanelDefaultHeight);
        rectTransform.localScale = Vector3.one;
    }

    private void UpdatePanelWidth(RectTransform rectTransform, float preferredWidth)
    {
        if (!rectTransform)
            return;

        EnsurePanelUsesFixedWidthAnchoring(rectTransform);
        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, GetClampedPanelWidth(rectTransform, preferredWidth));
    }

    private static float GetClampedPanelWidth(RectTransform rectTransform, float preferredWidth)
    {
        if (!rectTransform)
            return preferredWidth;

        RectTransform parentRect = rectTransform.parent as RectTransform;
        if (!parentRect)
            return preferredWidth;

        float maxWidth = parentRect.rect.width - DialoguePanelMinViewportMargin * 2.0f;
        if (maxWidth <= 1.0f)
            return preferredWidth;

        return Mathf.Clamp(preferredWidth, DialoguePanelMinWidth, Mathf.Max(DialoguePanelMinWidth, maxWidth));
    }

    private static RectTransform CreateRect(string name, RectTransform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.localScale = Vector3.one;
        return rectTransform;
    }

    private static RectTransform CreateImageRect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
        gameObject.GetComponent<Image>().raycastTarget = false;
        return rectTransform;
    }


    private static TMP_Text CreateText(string name, RectTransform parent, TextAlignmentOptions alignment, float fontSize, string text)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.localScale = Vector3.one;

        TextMeshProUGUI textComponent = gameObject.GetComponent<TextMeshProUGUI>();
        ConfigureText(textComponent, alignment, fontSize, text);
        return textComponent;
    }

    private static TMP_Text EnsureText(string name, RectTransform parent, TextAlignmentOptions alignment, float fontSize, string text)
    {
        RectTransform rectTransform = FindRect(name, parent);
        if (!rectTransform)
            return CreateText(name, parent, alignment, fontSize, text);

        TextMeshProUGUI textComponent = rectTransform.GetComponent<TextMeshProUGUI>();
        if (!textComponent)
            textComponent = rectTransform.gameObject.AddComponent<TextMeshProUGUI>();

        ConfigureText(textComponent, alignment, fontSize, text);
        return textComponent;
    }

    private static void ConfigureText(TMP_Text textComponent, TextAlignmentOptions alignment, float fontSize, string text, bool overwriteText = true)
    {
        if (!textComponent)
            return;

        if (overwriteText)
            textComponent.text = text;

        textComponent.fontSize = fontSize;
        textComponent.alignment = alignment;
        textComponent.textWrappingMode = TextWrappingModes.Normal;
        textComponent.overflowMode = TextOverflowModes.Overflow;
        textComponent.raycastTarget = false;
    }

    private static RectTransform FindRect(string childName, RectTransform parent)
    {
        if (!parent || string.IsNullOrWhiteSpace(childName))
            return null;

        Transform child = parent.Find(childName);
        return child as RectTransform;
    }

    private static TMP_Text FindText(string childName, RectTransform parent)
    {
        RectTransform rectTransform = FindRect(childName, parent);
        return rectTransform ? rectTransform.GetComponent<TMP_Text>() : null;
    }

    private static Image EnsureImageComponent(GameObject gameObject)
    {
        Image image = gameObject.GetComponent<Image>();
        if (!image)
            image = gameObject.AddComponent<Image>();

        image.type = Image.Type.Sliced;
        image.raycastTarget = false;
        return image;
    }

    private static void SetImageColor(RectTransform rectTransform, Color color)
    {
        if (!rectTransform)
            return;

        Image image = rectTransform.GetComponent<Image>();
        if (image)
            image.color = color;
    }
}
