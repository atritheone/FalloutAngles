// imports
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;



// class
public class ContainerController : MonoBehaviour
{
    private const float InteractCloseReopenCooldownSeconds = 0.15f;
    private const float PlayerHeaderRightLineLeftTrimPixels = 10.0f;
    private const float HeaderCategoryTextToRightArrowGapPixels = 10.0f;
    private const float PipBoyPaletteColorTolerance = 0.01f;
    private const float SelectionOutlineThickness = 2.0f;
    private const float ScrollGestureOwnerReleaseDelaySeconds = 0.18f;
    private const string PrimaryTabContainerObjectName = "PrimaryTabContainer";
    private const string RuntimeThemeBackgroundExclusionObjectName = "Background";
    private static float lastInteractCloseUnscaledTime = float.NegativeInfinity;
    private static readonly PlayerInventory.InventoryCategory[] InventoryCategories =
    {
        PlayerInventory.InventoryCategory.Weapons,
        PlayerInventory.InventoryCategory.Apparel,
        PlayerInventory.InventoryCategory.Aid,
        PlayerInventory.InventoryCategory.Misc,
        PlayerInventory.InventoryCategory.Ammo
    };

    private struct InventoryListRow
    {
        public ScriptableObject ItemDefinition;
        public string DisplayName;
        public int Quantity;
    }

    private enum PlayerInventoryListCategory
    {
        Items = 0,
        Weapons = 1,
        Apparel = 2,
        Aid = 3,
        Misc = 4,
        Ammo = 5
    }

    [System.Serializable]
    private struct ButtonHighlight
    {
        public GameObject outline;
        public GameObject background;
    }

    // variables
    // Optional canvas group used to show/hide and toggle interaction.
    [SerializeField] private CanvasGroup containerCanvasGroup;

    [Header("Runtime Theme")]
    [SerializeField] private Color pipBoyLightColor = new Color32(0x55, 0xD3, 0xF2, 0xFF);
    [SerializeField] private Color pipBoyDarkColor = new Color32(0x00, 0x0E, 0x93, 0xFF);

    // If true, this object is disabled when UI is closed and enabled when opened.
    [SerializeField] private bool disableInHierarchyWhenClosed = true;

    // Restore any editor-disabled descendants once on first open.
    [SerializeField] private bool restoreDisabledDescendantsOnFirstOpen = false;

    // The first selected UI object when opening this menu.
    [SerializeField] private GameObject firstSelectedUIObject;

    // Shared controls provider.
    [SerializeField] private PlayerControls playerControls;

    // Optional camera orbit controller to pause camera look while UI is open.
    [SerializeField] private CameraRigOrbit cameraRigOrbit;

    // Optional camera zoom controller to pause scroll zoom while UI is open.
    [SerializeField] private CameraControlZoom cameraControlZoom;

    // Disable gameplay actions while this menu is open.
    [SerializeField] private bool disableGameplayActionsWhenOpen = true;

    // Pause the game while this menu is open.
    [SerializeField] private bool pauseGameWhenOpen = true;

    // Close this menu when UI Cancel is pressed.
    [SerializeField] private bool closeOnCancel = true;

    // Player weight text shown in the container UI (current/max).
    [SerializeField] private TMP_Text playerWgText;

    // Player inventory list scroll.
    [SerializeField] private ScrollRect playerScroll;

    // Container inventory list scroll.
    [SerializeField] private ScrollRect containerScroll;

    // Header text that shows the currently selected player inventory category.
    [SerializeField] private TMP_Text playerInventoryCategoriesText;

    // Clickable button on the player inventory category text.
    [SerializeField] private Button playerInventoryCategoriesButton;

    // Left category arrow button for player inventory.
    [SerializeField] private Button playerInventoryArrowLeftButton;

    // Right category arrow button for player inventory.
    [SerializeField] private Button playerInventoryArrowRightButton;

    // Left category arrow rect in the player inventory header.
    [SerializeField] private RectTransform playerInventoryArrowLeftRect;

    // Right category arrow rect in the player inventory header.
    [SerializeField] private RectTransform playerInventoryArrowRightRect;

    // Left top line segment in the player inventory header.
    [SerializeField] private RectTransform playerInventoryTopLineLeftRect;

    // Right top line segment in the player inventory header (this width is adjusted at runtime).
    [SerializeField] private RectTransform playerInventoryTopLineRightRect;

    // Header text that shows the currently selected container inventory category.
    [SerializeField] private TMP_Text containerInventoryCategoriesText;

    // Clickable button on the container inventory category text.
    [SerializeField] private Button containerInventoryCategoriesButton;

    // Left category arrow button for container inventory.
    [SerializeField] private Button containerInventoryArrowLeftButton;

    // Right category arrow button for container inventory.
    [SerializeField] private Button containerInventoryArrowRightButton;

    // Left category arrow rect in the container inventory header.
    [SerializeField] private RectTransform containerInventoryArrowLeftRect;

    // Right category arrow rect in the container inventory header.
    [SerializeField] private RectTransform containerInventoryArrowRightRect;

    // Left top line segment in the container inventory header.
    [SerializeField] private RectTransform containerInventoryTopLineLeftRect;

    // Right top line segment in the container inventory header (this width is adjusted at runtime).
    [SerializeField] private RectTransform containerInventoryTopLineRightRect;

    // Optional explicit player inventory content root.
    [SerializeField] private Transform playerListContentRoot;

    // Optional explicit container inventory content root.
    [SerializeField] private Transform containerListContentRoot;

    // Entry prefab used for inventory list rows.
    [SerializeField] private GameObject itemEntryPrefab;

    // Selection box shown beside the currently equipped player inventory item.
    [SerializeField] private GameObject playerEquippedSelectedBox;

    // Hover highlight visuals for the player list rows.
    [SerializeField] private ButtonHighlight playerListEntryHighlight;

    // Hover highlight visuals for the container list rows.
    [SerializeField] private ButtonHighlight containerListEntryHighlight;

    // Mid line reference used as the right horizontal boundary for player list highlights.
    [SerializeField] private RectTransform playerHighlightMidLine1;

    // Mid line reference used as the left horizontal boundary for container list highlights.
    [SerializeField] private RectTransform containerHighlightMidLine2;

    // Hovered-item stat lines in the container UI details panel.
    [SerializeField] private GameObject itemLine1;
    [SerializeField] private GameObject itemLine2;
    [SerializeField] private GameObject itemLine3;
    [SerializeField] private GameObject itemLine4;
    [SerializeField] private GameObject itemLine5;

    // Hovered-item stat text in the container UI details panel.
    [SerializeField] private TMP_Text hoveredDamDrLabelText;
    [SerializeField] private TMP_Text hoveredDamDrItemText;
    [SerializeField] private TMP_Text hoveredCndLabelText;
    [SerializeField] private TMP_Text hoveredWgLabelText;
    [SerializeField] private TMP_Text hoveredValLabelText;
    [SerializeField] private TMP_Text hoveredWgItemText;
    [SerializeField] private TMP_Text hoveredValItemText;
    [SerializeField] private TMP_Text hoveredItemInfoText;

    // Hovered-item condition bar visuals.
    [SerializeField] private GameObject cndBarBackground;
    [SerializeField] private Image cndBarFillImage;

    // Quantity selection prompt root shown when moving stacked items.
    [SerializeField] private GameObject multipleSliderObject;

    // Slider used to choose transfer quantity.
    [SerializeField] private Slider multipleQuantitySlider;

    // Text that shows selected quantity / total quantity.
    [SerializeField] private TMP_Text quantityNumberText;

    // Runtime input actions cache.
    private InputSystemActions controls;

    // Cached callback for UI cancel.
    private System.Action<InputAction.CallbackContext> onCancelPerformed;

    // Tracks whether callbacks are currently registered.
    private bool isCancelCallbackRegistered;

    // Coroutine that delays gameplay input restore until keys are released after closing the UI.
    private Coroutine gameplayInputRestoreCoroutine;

    // Host that owns the delayed gameplay input restore coroutine.
    private MonoBehaviour gameplayInputRestoreCoroutineHost;

    // Tracks whether one-time initialization has run.
    private bool hasInitialized;

    // Whether this UI is currently open.
    private bool isOpen;
    private bool hasRestoredDisabledDescendants;

    // Cached time scale restored when the menu closes.
    private float cachedTimeScale = 1f;

    // Active container being viewed.
    private Container activeContainer;

    // Active interactor who opened the container.
    private GameObject activeInteractor;

    // Active player inventory used to populate player stat text while container UI is open.
    private PlayerInventory activePlayerInventory;

    // Active player weapon controller used to resolve equipped items.
    private PlayerWeaponController activePlayerWeaponController;

    // Active player state used to control combat/weapon-in-hand flags.
    private PlayerState activePlayerState;

    // Pooled spawned entries for the player inventory list.
    private readonly List<GameObject> spawnedPlayerListEntries = new List<GameObject>();

    // Pooled spawned entries for the container inventory list.
    private readonly List<GameObject> spawnedContainerListEntries = new List<GameObject>();

    // Currently hovered row object in the player list.
    private GameObject hoveredPlayerListEntryObject;

    // Currently hovered row object in the container list.
    private GameObject hoveredContainerListEntryObject;

    // Player list item definition binding for each active spawned row object.
    private readonly Dictionary<GameObject, ScriptableObject> playerListDefinitionByEntryObject =
        new Dictionary<GameObject, ScriptableObject>();

    // Player list total quantity binding for each active spawned row object.
    private readonly Dictionary<GameObject, int> playerListQuantityByEntryObject =
        new Dictionary<GameObject, int>();

    // Container list item definition binding for each active spawned row object.
    private readonly Dictionary<GameObject, ScriptableObject> containerListDefinitionByEntryObject =
        new Dictionary<GameObject, ScriptableObject>();

    // Container list total quantity binding for each active spawned row object.
    private readonly Dictionary<GameObject, int> containerListQuantityByEntryObject =
        new Dictionary<GameObject, int>();

    // Currently selected player inventory category shown in the container UI.
    [SerializeField] private PlayerInventoryListCategory currentPlayerInventoryListCategory = PlayerInventoryListCategory.Items;

    // Currently selected container inventory category shown in the container UI.
    [SerializeField] private PlayerInventoryListCategory currentContainerInventoryListCategory = PlayerInventoryListCategory.Items;

    // Cached base anchored position for the player header right line (before runtime width adjustments).
    private Vector2 playerHeaderRightLineBaseAnchoredPosition;

    // Whether the player header right line base anchored position has been captured.
    private bool hasPlayerHeaderRightLineBaseAnchoredPosition;

    // Cached spacing between category text right edge and right-arrow left edge in measurement space.
    private float playerHeaderCategoryTextToRightArrowGapInMeasurementSpace;

    // Whether category-text to right-arrow gap has been captured.
    private bool hasPlayerHeaderCategoryTextToRightArrowGap;

    // Cached base anchored position for the container header right line (before runtime width adjustments).
    private Vector2 containerHeaderRightLineBaseAnchoredPosition;

    // Whether the container header right line base anchored position has been captured.
    private bool hasContainerHeaderRightLineBaseAnchoredPosition;

    // Cached spacing between container category text right edge and right-arrow left edge in measurement space.
    private float containerHeaderCategoryTextToRightArrowGapInMeasurementSpace;

    // Whether container category-text to right-arrow gap has been captured.
    private bool hasContainerHeaderCategoryTextToRightArrowGap;

    // Cached base anchored position for the container header right arrow.
    private Vector2 containerHeaderRightArrowBaseAnchoredPosition;

    // Whether the container header right arrow base anchored position has been captured.
    private bool hasContainerHeaderRightArrowBaseAnchoredPosition;

    // Currently hovered row that owns the details panel contents.
    private GameObject hoveredItemStatsRowObject;
    private bool hoveredItemStatsRowIsPlayerList;

    // Runtime references to condition fill images used by the hovered details panel.
    private readonly List<Image> resolvedHoveredCndBarFillImages = new List<Image>();
    private static Sprite fallbackConditionBarFillSprite;

    // Active quantity prompt transfer state.
    private bool isQuantityTransferPromptOpen;
    private bool pendingQuantityTransferIsPlayerList;
    private ScriptableObject pendingQuantityTransferDefinition;
    private int pendingQuantityTransferTotalQuantity;

    // Keeps smooth wheel/trackpad momentum tied to the list where the gesture started.
    private ScrollRect activeScrollGestureOwner;
    private float activeScrollGestureLastUnscaledTime = float.NegativeInfinity;
    private float cachedPlayerScrollSensitivity = 1.0f;
    private float cachedContainerScrollSensitivity = 1.0f;
    private bool hasCachedPlayerScrollSensitivity;
    private bool hasCachedContainerScrollSensitivity;



    // methods
    private void Awake()
    {
        EnsureInitialized();
        SetOpenState(false, true);
    }


    private void OnEnable()
    {
        EnsureInitialized();

        if (isOpen)
            RegisterCancelCallback();
    }


    private void OnDisable()
    {
        if (isOpen)
            CancelPendingGameplayInputRestore();
        UnregisterCancelCallback();
        CloseQuantityTransferPrompt(true);
        SetActiveContainer(null);
        SetActivePlayerInventory(null);
        SetActivePlayerWeaponController(null);
        SetActivePlayerState(null);
        ClearActiveScrollGestureOwner();
        playerListDefinitionByEntryObject.Clear();
        playerListQuantityByEntryObject.Clear();
        containerListDefinitionByEntryObject.Clear();
        containerListQuantityByEntryObject.Clear();
        ClearHoveredItemStats();
        ClearPlayerEquippedSelectedBoxIndicator();
    }


    private void Update()
    {
        if (!isOpen)
            return;

        Keyboard keyboard = Keyboard.current;
        if (isQuantityTransferPromptOpen)
        {
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                ConfirmQuantityTransferPromptSelection();
                return;
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                CancelQuantityTransferPrompt();
                return;
            }

            return;
        }

        if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
        {
            lastInteractCloseUnscaledTime = Time.unscaledTime;
            Close();
            return;
        }

        if (keyboard != null && keyboard.aKey.wasPressedThisFrame)
        {
            TryTransferAllContainerItemsToPlayerAndClose();
            return;
        }

        RefreshPlayerEquippedSelectedBoxIndicator();
    }


    public bool IsOpen()
    {
        return isOpen;
    }


    public Container GetCurrentContainer()
    {
        return activeContainer;
    }


    public GameObject GetCurrentInteractor()
    {
        return activeInteractor;
    }


    public void OpenForContainer(Container container, GameObject interactor)
    {
        EnsureInitialized();

        SetActiveContainer(container);
        activeInteractor = interactor;
        ResolveAndSubscribeActivePlayerInventory();
        SetOpenState(true, false);
    }


    public void Close()
    {
        SetOpenState(false, false);
    }


    public void SelectPreviousPlayerInventoryCategory()
    {
        CyclePlayerInventoryCategory(-1);
    }


    public void SelectNextPlayerInventoryCategory()
    {
        CyclePlayerInventoryCategory(1);
    }


    public void SelectPreviousContainerInventoryCategory()
    {
        CycleContainerInventoryCategory(-1);
    }


    public void SelectNextContainerInventoryCategory()
    {
        CycleContainerInventoryCategory(1);
    }


    // Convenience hook for UI Button onClick events.
    public void CloseContainerUI()
    {
        Close();
    }


    public static ContainerController FindFirstInSceneIncludingInactive()
    {
        ContainerController[] candidates = Resources.FindObjectsOfTypeAll<ContainerController>();

        for (int i = 0; i < candidates.Length; i++)
        {
            ContainerController candidate = candidates[i];
            if (!candidate)
                continue;

            GameObject candidateObject = candidate.gameObject;
            if (!candidateObject.scene.IsValid() || !candidateObject.scene.isLoaded)
                continue;

            return candidate;
        }

        return null;
    }


    public static bool IsInteractCloseCooldownActive()
    {
        return Time.unscaledTime - lastInteractCloseUnscaledTime <= InteractCloseReopenCooldownSeconds;
    }


    private void EnsureInitialized()
    {
        if (hasInitialized)
            return;

        if (!containerCanvasGroup)
            containerCanvasGroup = GetComponentInChildren<CanvasGroup>(true);

        if (!playerControls)
            playerControls = FindAnyObjectByType<PlayerControls>();

        if (!cameraRigOrbit)
            cameraRigOrbit = FindAnyObjectByType<CameraRigOrbit>();

        if (!cameraControlZoom)
            cameraControlZoom = FindAnyObjectByType<CameraControlZoom>();

        if (!playerScroll)
            playerScroll = FindChildComponentByName<ScrollRect>("PlayerScroll");

        if (!containerScroll)
            containerScroll = FindChildComponentByName<ScrollRect>("ContainerScroll");

        if (!playerInventoryCategoriesText)
            playerInventoryCategoriesText = FindChildComponentByName<TMP_Text>("PlayerInventoryCategoriesText");

        if (!playerInventoryCategoriesButton && playerInventoryCategoriesText)
            playerInventoryCategoriesButton = playerInventoryCategoriesText.GetComponent<Button>();

        if (!playerInventoryCategoriesButton && playerInventoryCategoriesText)
            playerInventoryCategoriesButton = playerInventoryCategoriesText.GetComponentInParent<Button>();

        if (!playerInventoryArrowLeftButton)
            playerInventoryArrowLeftButton = FindChildComponentByName<Button>("PlayerInventoryArrowLeft");

        if (!playerInventoryArrowRightButton)
            playerInventoryArrowRightButton = FindChildComponentByName<Button>("PlayerInventoryArrowRight");

        if (!playerInventoryArrowLeftRect && playerInventoryArrowLeftButton)
            playerInventoryArrowLeftRect = playerInventoryArrowLeftButton.transform as RectTransform;

        if (!playerInventoryArrowLeftRect)
            playerInventoryArrowLeftRect = FindChildComponentByName<RectTransform>("PlayerInventoryArrowLeft");

        if (!playerInventoryArrowLeftButton && playerInventoryArrowLeftRect)
            playerInventoryArrowLeftButton = playerInventoryArrowLeftRect.GetComponent<Button>();

        if (!playerInventoryArrowLeftButton && playerInventoryArrowLeftRect)
            playerInventoryArrowLeftButton = playerInventoryArrowLeftRect.GetComponentInParent<Button>();

        if (!playerInventoryArrowRightRect && playerInventoryArrowRightButton)
            playerInventoryArrowRightRect = playerInventoryArrowRightButton.transform as RectTransform;

        if (!playerInventoryArrowRightRect)
            playerInventoryArrowRightRect = FindChildComponentByName<RectTransform>("PlayerInventoryArrowRight");

        if (!playerInventoryArrowRightButton && playerInventoryArrowRightRect)
            playerInventoryArrowRightButton = playerInventoryArrowRightRect.GetComponent<Button>();

        if (!playerInventoryArrowRightButton && playerInventoryArrowRightRect)
            playerInventoryArrowRightButton = playerInventoryArrowRightRect.GetComponentInParent<Button>();

        if (!playerInventoryTopLineLeftRect)
            playerInventoryTopLineLeftRect = FindChildComponentByName<RectTransform>("TopLine1 (2)");

        if (!playerInventoryTopLineRightRect)
            playerInventoryTopLineRightRect = FindChildComponentByName<RectTransform>("TopLine1 (1)");

        if (!containerInventoryCategoriesText)
            containerInventoryCategoriesText = FindChildComponentByName<TMP_Text>("ContainerInventoryCategoriesText");

        if (!containerInventoryCategoriesButton && containerInventoryCategoriesText)
            containerInventoryCategoriesButton = containerInventoryCategoriesText.GetComponent<Button>();

        if (!containerInventoryCategoriesButton && containerInventoryCategoriesText)
            containerInventoryCategoriesButton = containerInventoryCategoriesText.GetComponentInParent<Button>();

        if (!containerInventoryArrowLeftButton)
            containerInventoryArrowLeftButton = FindChildComponentByName<Button>("ContainerInventoryArrowLeft");

        if (!containerInventoryArrowRightButton)
            containerInventoryArrowRightButton = FindChildComponentByName<Button>("ContainerInventoryArrowRight");

        if (!containerInventoryArrowLeftRect && containerInventoryArrowLeftButton)
            containerInventoryArrowLeftRect = containerInventoryArrowLeftButton.transform as RectTransform;

        if (!containerInventoryArrowLeftRect)
            containerInventoryArrowLeftRect = FindChildComponentByName<RectTransform>("ContainerInventoryArrowLeft");

        if (!containerInventoryArrowLeftButton && containerInventoryArrowLeftRect)
            containerInventoryArrowLeftButton = containerInventoryArrowLeftRect.GetComponent<Button>();

        if (!containerInventoryArrowLeftButton && containerInventoryArrowLeftRect)
            containerInventoryArrowLeftButton = containerInventoryArrowLeftRect.GetComponentInParent<Button>();

        if (!containerInventoryArrowRightRect && containerInventoryArrowRightButton)
            containerInventoryArrowRightRect = containerInventoryArrowRightButton.transform as RectTransform;

        if (!containerInventoryArrowRightRect)
            containerInventoryArrowRightRect = FindChildComponentByName<RectTransform>("ContainerInventoryArrowRight");

        if (!containerInventoryArrowRightButton && containerInventoryArrowRightRect)
            containerInventoryArrowRightButton = containerInventoryArrowRightRect.GetComponent<Button>();

        if (!containerInventoryArrowRightButton && containerInventoryArrowRightRect)
            containerInventoryArrowRightButton = containerInventoryArrowRightRect.GetComponentInParent<Button>();

        if (!containerInventoryTopLineLeftRect)
            containerInventoryTopLineLeftRect = FindChildComponentByName<RectTransform>("TopLine2 (2)");

        if (!containerInventoryTopLineRightRect)
            containerInventoryTopLineRightRect = FindChildComponentByName<RectTransform>("TopLine2 (1)");

        CapturePlayerHeaderRightLineBaseAnchoredPositionIfNeeded();
        CaptureContainerHeaderRightLineBaseAnchoredPositionIfNeeded();

        if (!playerListContentRoot && playerScroll && playerScroll.content)
            playerListContentRoot = playerScroll.content;

        if (!containerListContentRoot && containerScroll && containerScroll.content)
            containerListContentRoot = containerScroll.content;

        if (!playerWgText)
            playerWgText = FindChildComponentByName<TMP_Text>("PlayerWgText");

        if (!playerWgText)
            playerWgText = FindChildComponentByName<TMP_Text>("WgPlayerText");

        if (!playerWgText)
            playerWgText = FindChildComponentByName<TMP_Text>("WGPlayerText");

        Transform containerUiRoot = ResolveContainerUiRoot();

        if (!itemLine1)
            itemLine1 = FindChildComponentByNameInRoot<Transform>("ItemLine1", containerUiRoot)?.gameObject;

        if (!itemLine2)
            itemLine2 = FindChildComponentByNameInRoot<Transform>("ItemLine2", containerUiRoot)?.gameObject;

        if (!itemLine3)
            itemLine3 = FindChildComponentByNameInRoot<Transform>("ItemLine3", containerUiRoot)?.gameObject;

        if (!itemLine4)
            itemLine4 = FindChildComponentByNameInRoot<Transform>("ItemLine4", containerUiRoot)?.gameObject;

        if (!itemLine5)
            itemLine5 = FindChildComponentByNameInRoot<Transform>("ItemLine5", containerUiRoot)?.gameObject;

        if (!hoveredDamDrLabelText)
            hoveredDamDrLabelText = FindChildComponentByNameInRoot<TMP_Text>("DAM/DRText", containerUiRoot);

        if (!hoveredDamDrLabelText)
            hoveredDamDrLabelText = FindChildComponentByNameInRoot<TMP_Text>("DAMText", containerUiRoot);

        if (!hoveredDamDrItemText)
            hoveredDamDrItemText = FindChildComponentByNameInRoot<TMP_Text>("DAM/DRItemText", containerUiRoot);

        if (!hoveredDamDrItemText)
            hoveredDamDrItemText = FindChildComponentByNameInRoot<TMP_Text>("DAMItemText", containerUiRoot);

        if (!hoveredCndLabelText)
            hoveredCndLabelText = FindChildComponentByNameInRoot<TMP_Text>("CNDText", containerUiRoot);

        if (!hoveredWgLabelText)
            hoveredWgLabelText = FindChildComponentByNameInRoot<TMP_Text>("WGText", containerUiRoot);

        if (!hoveredValLabelText)
            hoveredValLabelText = FindChildComponentByNameInRoot<TMP_Text>("VALText", containerUiRoot);

        if (!hoveredWgItemText)
            hoveredWgItemText = FindChildComponentByNameInRoot<TMP_Text>("WGItemText", containerUiRoot);

        if (!hoveredValItemText)
            hoveredValItemText = FindChildComponentByNameInRoot<TMP_Text>("VALItemText", containerUiRoot);

        if (!hoveredItemInfoText)
            hoveredItemInfoText = FindChildComponentByNameInRoot<TMP_Text>("ItemInfoText", containerUiRoot);

        if (!cndBarBackground)
            cndBarBackground = FindChildComponentByNameInRoot<Transform>("CNDBarBackground", containerUiRoot)?.gameObject;

        if (!cndBarFillImage)
            cndBarFillImage = FindChildComponentByNameInRoot<Image>("CNDBarFill", containerUiRoot);

        if (!multipleSliderObject)
            multipleSliderObject = FindChildComponentByNameInRoot<Transform>("MultipleSlider", containerUiRoot)?.gameObject;

        if (!multipleQuantitySlider && multipleSliderObject)
            multipleQuantitySlider = multipleSliderObject.GetComponentInChildren<Slider>(true);

        if (!multipleQuantitySlider && multipleSliderObject)
            multipleQuantitySlider = FindChildComponentByNameInRoot<Slider>("Slider", multipleSliderObject.transform);

        if (!multipleQuantitySlider)
            multipleQuantitySlider = FindChildComponentByNameInRoot<Slider>("Slider", containerUiRoot);

        if (!quantityNumberText && multipleSliderObject)
            quantityNumberText = FindChildComponentByNameInRoot<TMP_Text>("QuantityNumberText", multipleSliderObject.transform);

        if (!quantityNumberText)
            quantityNumberText = FindChildComponentByNameInRoot<TMP_Text>("QuantityNumberText", containerUiRoot);

        if (multipleQuantitySlider)
        {
            multipleQuantitySlider.wholeNumbers = true;
            multipleQuantitySlider.onValueChanged.RemoveListener(OnMultipleQuantitySliderValueChanged);
            multipleQuantitySlider.onValueChanged.AddListener(OnMultipleQuantitySliderValueChanged);
        }

        CloseQuantityTransferPrompt(true);

        AutoWireHoveredConditionBarFillImages();
        DisableHoveredItemStatObjectRaycasts();
        ClearHoveredItemStats();

        DisableGraphicRaycasts(playerEquippedSelectedBox);
        EnsureLayoutIgnored(playerEquippedSelectedBox);
        ClearPlayerEquippedSelectedBoxIndicator();

        EnsureButtonHighlightOutlineSegments(playerListEntryHighlight);
        EnsureButtonHighlightOutlineSegments(containerListEntryHighlight);
        DisableButtonHighlightRaycasts(playerListEntryHighlight);
        DisableButtonHighlightRaycasts(containerListEntryHighlight);
        SetButtonHighlight(playerListEntryHighlight, false, false);
        SetButtonHighlight(containerListEntryHighlight, false, false);

        RegisterPlayerInventoryCategoryArrowCallbacks();
        RegisterContainerInventoryCategoryArrowCallbacks();
        ConfigureIndependentScrollRects();
        RefreshPlayerInventoryCategoryHeader();
        RefreshContainerInventoryCategoryHeader();
        ApplyContainerPipBoyPaletteColorOverrides();

        if (playerControls)
            controls = playerControls.Controls;

        onCancelPerformed = _ =>
        {
            if (!isOpen)
                return;

            if (isQuantityTransferPromptOpen)
            {
                CancelQuantityTransferPrompt();
                return;
            }

            if (closeOnCancel)
                Close();
        };

        hasInitialized = true;
    }


    private void SetOpenState(bool open, bool forceWithoutSideEffects)
    {
        bool wasOpen = isOpen;

        if (!forceWithoutSideEffects && !wasOpen && open && !gameObject.activeSelf)
            gameObject.SetActive(true);

        isOpen = open;

        if (isOpen)
            SetContainerHierarchyActive(true);

        if (containerCanvasGroup)
        {
            containerCanvasGroup.alpha = isOpen ? 1f : 0f;
            containerCanvasGroup.interactable = isOpen;
            containerCanvasGroup.blocksRaycasts = isOpen;
        }

        if (!isOpen)
            CloseQuantityTransferPrompt(true);

        if (forceWithoutSideEffects)
            return;

        if (wasOpen == isOpen)
            return;

        if (isOpen)
        {
            CancelPendingGameplayInputRestore();

            // Re-capture container header baselines when opening so spacing is measured from final layout state.
            hasContainerHeaderRightArrowBaseAnchoredPosition = false;
            hasContainerHeaderCategoryTextToRightArrowGap = false;

            // Always start both lists at the catch-all category when opening.
            currentPlayerInventoryListCategory = PlayerInventoryListCategory.Items;
            currentContainerInventoryListCategory = PlayerInventoryListCategory.Items;

            RegisterCancelCallback();
            ResolveAndSubscribeActivePlayerInventory();
            RefreshPlayerInventoryCategoryHeader();
            RefreshContainerInventoryCategoryHeader();
            RefreshPlayerWeightText();
            RefreshInventoryLists();
            ApplyContainerPipBoyPaletteColorOverrides();
            CloseQuantityTransferPrompt(true);

            if (cameraRigOrbit)
                cameraRigOrbit.SetInputEnabled(false);

            if (cameraControlZoom)
                cameraControlZoom.SetInputEnabled(false);

            if (pauseGameWhenOpen && !wasOpen)
                PauseGameTime();

            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (controls != null)
            {
                controls.UI.Enable();
                if (disableGameplayActionsWhenOpen)
                    SetGameplayActionsEnabled(false);
            }

            if (UnityEngine.EventSystems.EventSystem.current && firstSelectedUIObject)
                UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(firstSelectedUIObject);

            return;
        }

        UnregisterCancelCallback();

        if (pauseGameWhenOpen && wasOpen)
            ResumeGameTime();

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

        SetActiveContainer(null);
        activeInteractor = null;
        SetActivePlayerInventory(null);
        SetActivePlayerWeaponController(null);
        SetActivePlayerState(null);
        CloseQuantityTransferPrompt(true);
        ClearPlayerListHoveredEntryHighlight();
        ClearContainerListHoveredEntryHighlight();
        ClearActiveScrollGestureOwner();
        playerListDefinitionByEntryObject.Clear();
        playerListQuantityByEntryObject.Clear();
        containerListDefinitionByEntryObject.Clear();
        containerListQuantityByEntryObject.Clear();
        ClearHoveredItemStats();
        ClearPlayerEquippedSelectedBoxIndicator();

        if (disableInHierarchyWhenClosed && gameObject.activeSelf)
            SetContainerHierarchyActive(false);
    }


    private void RegisterPlayerInventoryCategoryArrowCallbacks()
    {
        if (playerInventoryCategoriesButton)
        {
            playerInventoryCategoriesButton.onClick.RemoveListener(SelectNextPlayerInventoryCategory);
            playerInventoryCategoriesButton.onClick.AddListener(SelectNextPlayerInventoryCategory);
        }

        if (playerInventoryArrowLeftButton)
        {
            playerInventoryArrowLeftButton.onClick.RemoveListener(SelectPreviousPlayerInventoryCategory);
            playerInventoryArrowLeftButton.onClick.AddListener(SelectPreviousPlayerInventoryCategory);
        }

        if (playerInventoryArrowRightButton)
        {
            playerInventoryArrowRightButton.onClick.RemoveListener(SelectNextPlayerInventoryCategory);
            playerInventoryArrowRightButton.onClick.AddListener(SelectNextPlayerInventoryCategory);
        }
    }


    private void RegisterContainerInventoryCategoryArrowCallbacks()
    {
        if (containerInventoryCategoriesButton)
        {
            containerInventoryCategoriesButton.onClick.RemoveListener(SelectNextContainerInventoryCategory);
            containerInventoryCategoriesButton.onClick.AddListener(SelectNextContainerInventoryCategory);
        }

        if (containerInventoryArrowLeftButton)
        {
            containerInventoryArrowLeftButton.onClick.RemoveListener(SelectPreviousContainerInventoryCategory);
            containerInventoryArrowLeftButton.onClick.AddListener(SelectPreviousContainerInventoryCategory);
        }

        if (containerInventoryArrowRightButton)
        {
            containerInventoryArrowRightButton.onClick.RemoveListener(SelectNextContainerInventoryCategory);
            containerInventoryArrowRightButton.onClick.AddListener(SelectNextContainerInventoryCategory);
        }
    }


    private void CyclePlayerInventoryCategory(int step)
    {
        int categoryCount = 6;
        int currentIndex = (int)currentPlayerInventoryListCategory;
        int nextIndex = (currentIndex + step) % categoryCount;
        if (nextIndex < 0)
            nextIndex += categoryCount;

        PlayerInventoryListCategory nextCategory = (PlayerInventoryListCategory)nextIndex;
        if (nextCategory == currentPlayerInventoryListCategory)
            return;

        currentPlayerInventoryListCategory = nextCategory;
        RefreshPlayerInventoryCategoryHeader();

        if (isOpen)
            RefreshInventoryLists();
    }


    private void CycleContainerInventoryCategory(int step)
    {
        int categoryCount = 6;
        int currentIndex = (int)currentContainerInventoryListCategory;
        int nextIndex = (currentIndex + step) % categoryCount;
        if (nextIndex < 0)
            nextIndex += categoryCount;

        PlayerInventoryListCategory nextCategory = (PlayerInventoryListCategory)nextIndex;
        if (nextCategory == currentContainerInventoryListCategory)
            return;

        currentContainerInventoryListCategory = nextCategory;
        RefreshContainerInventoryCategoryHeader();

        if (isOpen)
            RefreshInventoryLists();
    }


    private void RefreshPlayerInventoryCategoryHeader()
    {
        SetTextIfChanged(playerInventoryCategoriesText, GetPlayerInventoryCategoryLabel(currentPlayerInventoryListCategory));

        if (playerInventoryCategoriesText)
        {
            playerInventoryCategoriesText.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(playerInventoryCategoriesText.rectTransform);
        }

        UpdatePlayerInventoryHeaderRightLineWidth();
    }


    private void RefreshContainerInventoryCategoryHeader()
    {
        SetTextIfChanged(
            containerInventoryCategoriesText,
            GetContainerInventoryCategoryLabel(currentContainerInventoryListCategory));

        if (containerInventoryCategoriesText)
        {
            containerInventoryCategoriesText.ForceMeshUpdate();
            LayoutRebuilder.ForceRebuildLayoutImmediate(containerInventoryCategoriesText.rectTransform);
        }

        UpdateContainerInventoryHeaderRightLineWidth();
    }


    private void UpdatePlayerInventoryHeaderRightLineWidth()
    {
        if (!playerHighlightMidLine1 ||
            !playerInventoryTopLineLeftRect ||
            !playerInventoryTopLineRightRect ||
            !playerInventoryArrowLeftRect ||
            !playerInventoryArrowRightRect ||
            !playerInventoryCategoriesText)
        {
            return;
        }

        RectTransform measurementSpaceRect = playerInventoryTopLineLeftRect.parent as RectTransform;
        if (!measurementSpaceRect)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(measurementSpaceRect);

        CapturePlayerHeaderRightLineBaseAnchoredPositionIfNeeded();
        RestorePlayerHeaderRightLineBaseAnchoredPosition();
        CapturePlayerHeaderCategoryTextToRightArrowGapIfNeeded(measurementSpaceRect);
        PositionPlayerHeaderRightArrowFromCategoryText(measurementSpaceRect);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(measurementSpaceRect);

        float currentRightLineWidth = playerInventoryTopLineRightRect.rect.width;
        if (IsFiniteFloat(currentRightLineWidth) == false)
            SetRectTransformLocalWidth(playerInventoryTopLineRightRect, 0.0f);

        // Target width must be measured in the same space as positional bounds math below.
        float targetWidthInMeasurementSpace = GetRectWidthInLocalSpace(playerHighlightMidLine1, measurementSpaceRect);
        if (targetWidthInMeasurementSpace <= 0.001f || IsFiniteFloat(targetWidthInMeasurementSpace) == false)
            return;

        if (!TryGetCombinedRectLocalBoundsX(
                measurementSpaceRect,
                out float fixedElementsMinLocalX,
                out _,
                playerInventoryTopLineLeftRect,
                playerInventoryArrowLeftRect,
                playerInventoryCategoriesText.rectTransform,
                playerInventoryArrowRightRect))
        {
            return;
        }

        if (!TryGetRectLocalBounds(
                playerInventoryArrowRightRect,
                measurementSpaceRect,
                out _,
                out float rightArrowMaxLocalX,
                out _,
                out _))
        {
            return;
        }

        if (!IsFiniteFloat(fixedElementsMinLocalX) ||
            !IsFiniteFloat(rightArrowMaxLocalX))
        {
            return;
        }

        // Width before the right line is taken from the final right edge of the right arrow.
        float widthBeforeRightLine = Mathf.Max(0.0f, rightArrowMaxLocalX - fixedElementsMinLocalX);
        if (!IsFiniteFloat(widthBeforeRightLine))
            widthBeforeRightLine = 0.0f;

        float requiredRightLineWidthInMeasurementSpace = Mathf.Max(0.0f, targetWidthInMeasurementSpace - widthBeforeRightLine);
        if (IsFiniteFloat(requiredRightLineWidthInMeasurementSpace) == false)
            requiredRightLineWidthInMeasurementSpace = 0.0f;
        requiredRightLineWidthInMeasurementSpace = Mathf.Min(requiredRightLineWidthInMeasurementSpace, targetWidthInMeasurementSpace);
        float leftTrimInMeasurementSpace = Mathf.Min(
            PlayerHeaderRightLineLeftTrimPixels,
            requiredRightLineWidthInMeasurementSpace);
        float trimmedRightLineWidthInMeasurementSpace =
            requiredRightLineWidthInMeasurementSpace - leftTrimInMeasurementSpace;

        // Convert measurement-space width back to this rect's local width before applying.
        float rightLineLocalXAxisScaleInMeasurementSpace =
            GetRectLocalXAxisScaleInLocalSpace(playerInventoryTopLineRightRect, measurementSpaceRect);
        if (rightLineLocalXAxisScaleInMeasurementSpace <= 0.0001f ||
            IsFiniteFloat(rightLineLocalXAxisScaleInMeasurementSpace) == false)
        {
            return;
        }

        float requiredRightLineLocalWidth =
            trimmedRightLineWidthInMeasurementSpace / rightLineLocalXAxisScaleInMeasurementSpace;
        if (IsFiniteFloat(requiredRightLineLocalWidth) == false)
            requiredRightLineLocalWidth = 0.0f;

        SetRectTransformLocalWidth(playerInventoryTopLineRightRect, requiredRightLineLocalWidth);
        ApplyPlayerHeaderRightLineLeftTrimOffset(leftTrimInMeasurementSpace);
        AlignPlayerHeaderRightLineRightEdgeToMidLine1(measurementSpaceRect);
        LayoutRebuilder.ForceRebuildLayoutImmediate(measurementSpaceRect);
    }


    private void CapturePlayerHeaderCategoryTextToRightArrowGapIfNeeded(RectTransform measurementSpaceRect)
    {
        if (hasPlayerHeaderCategoryTextToRightArrowGap ||
            !measurementSpaceRect ||
            !playerInventoryCategoriesText ||
            !playerInventoryArrowRightRect)
        {
            return;
        }

        playerHeaderCategoryTextToRightArrowGapInMeasurementSpace = HeaderCategoryTextToRightArrowGapPixels;
        hasPlayerHeaderCategoryTextToRightArrowGap = true;
    }


    private void PositionPlayerHeaderRightArrowFromCategoryText(RectTransform measurementSpaceRect)
    {
        if (!measurementSpaceRect ||
            !hasPlayerHeaderCategoryTextToRightArrowGap ||
            !playerInventoryCategoriesText ||
            !playerInventoryArrowRightRect)
        {
            return;
        }

        if (!TryGetPlayerInventoryCategoryTextRenderedRightEdgeLocalX(
                measurementSpaceRect,
                out float categoryTextMaxLocalX))
        {
            return;
        }

        if (!TryGetRectLocalBounds(
                playerInventoryArrowRightRect,
                measurementSpaceRect,
                out float rightArrowMinLocalX,
                out _,
                out _,
                out _))
        {
            return;
        }

        float desiredRightArrowMinLocalX =
            categoryTextMaxLocalX + playerHeaderCategoryTextToRightArrowGapInMeasurementSpace;
        float movementDeltaInMeasurementSpace = desiredRightArrowMinLocalX - rightArrowMinLocalX;
        if (Mathf.Abs(movementDeltaInMeasurementSpace) <= 0.001f ||
            IsFiniteFloat(movementDeltaInMeasurementSpace) == false)
        {
            return;
        }

        Transform rightArrowParentTransform = playerInventoryArrowRightRect.parent;
        if (!rightArrowParentTransform)
            return;

        Vector3 worldMovementDelta = measurementSpaceRect.TransformVector(
            new Vector3(movementDeltaInMeasurementSpace, 0.0f, 0.0f));
        Vector3 parentMovementDelta = rightArrowParentTransform.InverseTransformVector(worldMovementDelta);

        Vector2 nextAnchoredPosition = playerInventoryArrowRightRect.anchoredPosition;
        nextAnchoredPosition.x += parentMovementDelta.x;
        nextAnchoredPosition.y += parentMovementDelta.y;
        playerInventoryArrowRightRect.anchoredPosition = nextAnchoredPosition;
    }


    private bool TryGetPlayerInventoryCategoryTextRenderedRightEdgeLocalX(
        RectTransform measurementSpaceRect,
        out float rightEdgeLocalX)
    {
        return TryGetRenderedTextEdgeFacingRectLocalX(
            playerInventoryCategoriesText,
            measurementSpaceRect,
            playerInventoryArrowRightRect,
            out rightEdgeLocalX);
    }


    private void CapturePlayerHeaderRightLineBaseAnchoredPositionIfNeeded()
    {
        if (hasPlayerHeaderRightLineBaseAnchoredPosition || !playerInventoryTopLineRightRect)
            return;

        playerHeaderRightLineBaseAnchoredPosition = playerInventoryTopLineRightRect.anchoredPosition;
        hasPlayerHeaderRightLineBaseAnchoredPosition = true;
    }


    private void RestorePlayerHeaderRightLineBaseAnchoredPosition()
    {
        if (!hasPlayerHeaderRightLineBaseAnchoredPosition || !playerInventoryTopLineRightRect)
            return;

        playerInventoryTopLineRightRect.anchoredPosition = playerHeaderRightLineBaseAnchoredPosition;
    }


    private void ApplyPlayerHeaderRightLineLeftTrimOffset(float leftTrimInMeasurementSpace)
    {
        if (!hasPlayerHeaderRightLineBaseAnchoredPosition || !playerInventoryTopLineRightRect)
            return;

        float trimAmount = IsFiniteFloat(leftTrimInMeasurementSpace)
            ? Mathf.Max(0.0f, leftTrimInMeasurementSpace)
            : 0.0f;

        float rightEdgePreserveOffsetX = trimAmount * (1.0f - playerInventoryTopLineRightRect.pivot.x);
        Vector2 nextAnchoredPosition = playerHeaderRightLineBaseAnchoredPosition;
        nextAnchoredPosition.x += rightEdgePreserveOffsetX;
        playerInventoryTopLineRightRect.anchoredPosition = nextAnchoredPosition;
    }


    private void AlignPlayerHeaderRightLineRightEdgeToMidLine1(RectTransform measurementSpaceRect)
    {
        if (!measurementSpaceRect || !playerInventoryTopLineRightRect || !playerHighlightMidLine1)
            return;

        if (!TryGetRectLocalBounds(
                playerHighlightMidLine1,
                measurementSpaceRect,
                out _,
                out float targetRightEdgeLocalX,
                out _,
                out _))
        {
            return;
        }

        if (!TryGetRectLocalBounds(
                playerInventoryTopLineRightRect,
                measurementSpaceRect,
                out _,
                out float currentRightEdgeLocalX,
                out _,
                out _))
        {
            return;
        }

        float movementDeltaInMeasurementSpace = targetRightEdgeLocalX - currentRightEdgeLocalX;
        if (Mathf.Abs(movementDeltaInMeasurementSpace) <= 0.001f ||
            IsFiniteFloat(movementDeltaInMeasurementSpace) == false)
        {
            return;
        }

        Transform rightLineParentTransform = playerInventoryTopLineRightRect.parent;
        if (!rightLineParentTransform)
            return;

        Vector3 worldMovementDelta = measurementSpaceRect.TransformVector(
            new Vector3(movementDeltaInMeasurementSpace, 0.0f, 0.0f));
        Vector3 parentMovementDelta = rightLineParentTransform.InverseTransformVector(worldMovementDelta);

        Vector2 nextAnchoredPosition = playerInventoryTopLineRightRect.anchoredPosition;
        nextAnchoredPosition.x += parentMovementDelta.x;
        nextAnchoredPosition.y += parentMovementDelta.y;
        playerInventoryTopLineRightRect.anchoredPosition = nextAnchoredPosition;
    }


    private void UpdateContainerInventoryHeaderRightLineWidth()
    {
        if (!containerHighlightMidLine2 ||
            !containerInventoryTopLineLeftRect ||
            !containerInventoryTopLineRightRect ||
            !containerInventoryArrowLeftRect ||
            !containerInventoryArrowRightRect ||
            !containerInventoryCategoriesText)
        {
            return;
        }

        RectTransform measurementSpaceRect = containerInventoryTopLineLeftRect.parent as RectTransform;
        if (!measurementSpaceRect)
            return;

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(measurementSpaceRect);

        CaptureContainerHeaderRightLineBaseAnchoredPositionIfNeeded();
        RestoreContainerHeaderRightLineBaseAnchoredPosition();
        CaptureContainerHeaderRightArrowBaseAnchoredPositionIfNeeded();
        RestoreContainerHeaderRightArrowBaseAnchoredPosition();
        CaptureContainerHeaderCategoryTextToRightArrowGapIfNeeded(measurementSpaceRect);
        PositionContainerHeaderRightArrowFromCategoryText(measurementSpaceRect);
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(measurementSpaceRect);

        float currentRightLineWidth = containerInventoryTopLineRightRect.rect.width;
        if (IsFiniteFloat(currentRightLineWidth) == false)
            SetRectTransformLocalWidth(containerInventoryTopLineRightRect, 0.0f);

        // Target width must be measured in the same space as positional bounds math below.
        float targetWidthInMeasurementSpace = GetRectWidthInLocalSpace(containerHighlightMidLine2, measurementSpaceRect);
        if (targetWidthInMeasurementSpace <= 0.001f || IsFiniteFloat(targetWidthInMeasurementSpace) == false)
            return;

        if (!TryGetCombinedRectLocalBoundsX(
                measurementSpaceRect,
                out float fixedElementsMinLocalX,
                out _,
                containerInventoryTopLineLeftRect,
                containerInventoryArrowLeftRect,
                containerInventoryCategoriesText.rectTransform,
                containerInventoryArrowRightRect))
        {
            return;
        }

        if (!TryGetRectLocalBounds(
                containerInventoryArrowRightRect,
                measurementSpaceRect,
                out _,
                out float rightArrowMaxLocalX,
                out _,
                out _))
        {
            return;
        }

        if (!IsFiniteFloat(fixedElementsMinLocalX) ||
            !IsFiniteFloat(rightArrowMaxLocalX))
        {
            return;
        }

        // Width before the right line is taken from the final right edge of the right arrow.
        float widthBeforeRightLine = Mathf.Max(0.0f, rightArrowMaxLocalX - fixedElementsMinLocalX);
        if (!IsFiniteFloat(widthBeforeRightLine))
            widthBeforeRightLine = 0.0f;

        float requiredRightLineWidthInMeasurementSpace = Mathf.Max(0.0f, targetWidthInMeasurementSpace - widthBeforeRightLine);
        if (IsFiniteFloat(requiredRightLineWidthInMeasurementSpace) == false)
            requiredRightLineWidthInMeasurementSpace = 0.0f;
        requiredRightLineWidthInMeasurementSpace = Mathf.Min(requiredRightLineWidthInMeasurementSpace, targetWidthInMeasurementSpace);
        float leftTrimInMeasurementSpace = Mathf.Min(
            PlayerHeaderRightLineLeftTrimPixels,
            requiredRightLineWidthInMeasurementSpace);
        float trimmedRightLineWidthInMeasurementSpace =
            requiredRightLineWidthInMeasurementSpace - leftTrimInMeasurementSpace;

        float rightLineLocalXAxisScaleInMeasurementSpace =
            GetRectLocalXAxisScaleInLocalSpace(containerInventoryTopLineRightRect, measurementSpaceRect);
        if (rightLineLocalXAxisScaleInMeasurementSpace <= 0.0001f ||
            IsFiniteFloat(rightLineLocalXAxisScaleInMeasurementSpace) == false)
        {
            return;
        }

        float requiredRightLineLocalWidth =
            trimmedRightLineWidthInMeasurementSpace / rightLineLocalXAxisScaleInMeasurementSpace;
        if (IsFiniteFloat(requiredRightLineLocalWidth) == false)
            requiredRightLineLocalWidth = 0.0f;

        SetRectTransformLocalWidth(containerInventoryTopLineRightRect, requiredRightLineLocalWidth);
        ApplyContainerHeaderRightLineLeftTrimOffset(leftTrimInMeasurementSpace);
        AlignContainerHeaderRightLineRightEdgeToMidLine2(measurementSpaceRect);
        LayoutRebuilder.ForceRebuildLayoutImmediate(measurementSpaceRect);
    }


    private void CaptureContainerHeaderCategoryTextToRightArrowGapIfNeeded(RectTransform measurementSpaceRect)
    {
        if (hasContainerHeaderCategoryTextToRightArrowGap ||
            !measurementSpaceRect ||
            !containerInventoryCategoriesText ||
            !containerInventoryArrowRightRect)
        {
            return;
        }

        containerHeaderCategoryTextToRightArrowGapInMeasurementSpace = HeaderCategoryTextToRightArrowGapPixels;
        hasContainerHeaderCategoryTextToRightArrowGap = true;
    }


    private void PositionContainerHeaderRightArrowFromCategoryText(RectTransform measurementSpaceRect)
    {
        if (!measurementSpaceRect ||
            !hasContainerHeaderCategoryTextToRightArrowGap ||
            !containerInventoryCategoriesText ||
            !containerInventoryArrowRightRect)
        {
            return;
        }

        if (!TryGetContainerInventoryCategoryTextRenderedRightEdgeLocalX(
                measurementSpaceRect,
                out float categoryTextMaxLocalX))
        {
            return;
        }

        if (!TryGetRectLocalBounds(
                containerInventoryArrowRightRect,
                measurementSpaceRect,
                out float rightArrowMinLocalX,
                out _,
                out _,
                out _))
        {
            return;
        }

        float desiredRightArrowMinLocalX =
            categoryTextMaxLocalX + containerHeaderCategoryTextToRightArrowGapInMeasurementSpace;
        float movementDeltaInMeasurementSpace = desiredRightArrowMinLocalX - rightArrowMinLocalX;
        if (Mathf.Abs(movementDeltaInMeasurementSpace) <= 0.001f ||
            IsFiniteFloat(movementDeltaInMeasurementSpace) == false)
        {
            return;
        }

        Transform rightArrowParentTransform = containerInventoryArrowRightRect.parent;
        if (!rightArrowParentTransform)
            return;

        Vector3 worldMovementDelta = measurementSpaceRect.TransformVector(
            new Vector3(movementDeltaInMeasurementSpace, 0.0f, 0.0f));
        Vector3 parentMovementDelta = rightArrowParentTransform.InverseTransformVector(worldMovementDelta);

        Vector2 nextAnchoredPosition = containerInventoryArrowRightRect.anchoredPosition;
        nextAnchoredPosition.x += parentMovementDelta.x;
        nextAnchoredPosition.y += parentMovementDelta.y;
        containerInventoryArrowRightRect.anchoredPosition = nextAnchoredPosition;
    }


    private bool TryGetContainerInventoryCategoryTextRenderedRightEdgeLocalX(
        RectTransform measurementSpaceRect,
        out float rightEdgeLocalX)
    {
        return TryGetRenderedTextRightEdgeLocalX(
            containerInventoryCategoriesText,
            measurementSpaceRect,
            out rightEdgeLocalX);
    }


    private static bool TryGetRenderedTextRightEdgeLocalX(
        TMP_Text textComponent,
        RectTransform measurementSpaceRect,
        out float rightEdgeLocalX)
    {
        return TryGetRenderedTextEdgeFacingRectLocalX(
            textComponent,
            measurementSpaceRect,
            null,
            out rightEdgeLocalX);
    }


    private static bool TryGetRenderedTextEdgeFacingRectLocalX(
        TMP_Text textComponent,
        RectTransform measurementSpaceRect,
        RectTransform facingRect,
        out float edgeLocalX)
    {
        edgeLocalX = 0.0f;
        if (!textComponent || !measurementSpaceRect)
            return false;

        RectTransform textRectTransform = textComponent.rectTransform;
        if (!textRectTransform)
            return false;

        textComponent.ForceMeshUpdate();
        TMP_TextInfo textInfo = textComponent.textInfo;
        bool hasRenderedPoint = false;
        float minRenderedXInMeasurementSpace = float.PositiveInfinity;
        float maxRenderedRightInMeasurementSpace = float.NegativeInfinity;

        int characterCount = textInfo != null ? textInfo.characterCount : 0;
        for (int characterIndex = 0; characterIndex < characterCount; characterIndex++)
        {
            TMP_CharacterInfo characterInfo = textInfo.characterInfo[characterIndex];
            if (!characterInfo.isVisible)
                continue;

            Vector3[] characterCorners =
            {
                characterInfo.bottomLeft,
                characterInfo.topLeft,
                characterInfo.topRight,
                characterInfo.bottomRight
            };

            for (int cornerIndex = 0; cornerIndex < characterCorners.Length; cornerIndex++)
            {
                Vector3 worldPoint = textRectTransform.TransformPoint(characterCorners[cornerIndex]);
                Vector3 measurementPoint = measurementSpaceRect.InverseTransformPoint(worldPoint);
                if (!IsFiniteFloat(measurementPoint.x))
                    continue;

                if (!hasRenderedPoint || measurementPoint.x > maxRenderedRightInMeasurementSpace)
                {
                    hasRenderedPoint = true;
                    maxRenderedRightInMeasurementSpace = measurementPoint.x;
                }

                if (!hasRenderedPoint || measurementPoint.x < minRenderedXInMeasurementSpace)
                    minRenderedXInMeasurementSpace = measurementPoint.x;
            }
        }

        if (!hasRenderedPoint)
        {
            Bounds fallbackLocalBounds = textComponent.textBounds;
            if (!IsFiniteFloat(fallbackLocalBounds.min.x) ||
                !IsFiniteFloat(fallbackLocalBounds.max.x) ||
                !IsFiniteFloat(fallbackLocalBounds.min.y) ||
                !IsFiniteFloat(fallbackLocalBounds.max.y))
            {
                return false;
            }

            Vector3[] fallbackCorners =
            {
                new Vector3(fallbackLocalBounds.min.x, fallbackLocalBounds.min.y, 0.0f),
                new Vector3(fallbackLocalBounds.min.x, fallbackLocalBounds.max.y, 0.0f),
                new Vector3(fallbackLocalBounds.max.x, fallbackLocalBounds.max.y, 0.0f),
                new Vector3(fallbackLocalBounds.max.x, fallbackLocalBounds.min.y, 0.0f)
            };

            for (int cornerIndex = 0; cornerIndex < fallbackCorners.Length; cornerIndex++)
            {
                Vector3 worldPoint = textRectTransform.TransformPoint(fallbackCorners[cornerIndex]);
                Vector3 measurementPoint = measurementSpaceRect.InverseTransformPoint(worldPoint);
                if (!IsFiniteFloat(measurementPoint.x))
                    continue;

                if (!hasRenderedPoint || measurementPoint.x > maxRenderedRightInMeasurementSpace)
                {
                    hasRenderedPoint = true;
                    maxRenderedRightInMeasurementSpace = measurementPoint.x;
                }

                if (!hasRenderedPoint || measurementPoint.x < minRenderedXInMeasurementSpace)
                    minRenderedXInMeasurementSpace = measurementPoint.x;
            }

            if (!hasRenderedPoint)
                return false;
        }

        float textCenterLocalX = (minRenderedXInMeasurementSpace + maxRenderedRightInMeasurementSpace) * 0.5f;
        bool useMaxEdge = true;

        if (facingRect &&
            TryGetRectLocalBounds(
                facingRect,
                measurementSpaceRect,
                out float facingMinLocalX,
                out float facingMaxLocalX,
                out _,
                out _))
        {
            float facingCenterLocalX = (facingMinLocalX + facingMaxLocalX) * 0.5f;
            useMaxEdge = facingCenterLocalX >= textCenterLocalX;
        }

        edgeLocalX = useMaxEdge ? maxRenderedRightInMeasurementSpace : minRenderedXInMeasurementSpace;
        return true;
    }


    private static bool TryGetRectEdgeFacingRectLocalX(
        RectTransform sourceRect,
        RectTransform facingRect,
        RectTransform measurementSpaceRect,
        out float edgeLocalX)
    {
        edgeLocalX = 0.0f;
        if (!sourceRect || !measurementSpaceRect)
            return false;

        if (!TryGetRectLocalBounds(
                sourceRect,
                measurementSpaceRect,
                out float sourceMinLocalX,
                out float sourceMaxLocalX,
                out _,
                out _))
        {
            return false;
        }

        bool useMaxEdge = true;
        if (facingRect &&
            TryGetRectLocalBounds(
                facingRect,
                measurementSpaceRect,
                out float facingMinLocalX,
                out float facingMaxLocalX,
                out _,
                out _))
        {
            float sourceCenterLocalX = (sourceMinLocalX + sourceMaxLocalX) * 0.5f;
            float facingCenterLocalX = (facingMinLocalX + facingMaxLocalX) * 0.5f;
            useMaxEdge = facingCenterLocalX >= sourceCenterLocalX;
        }

        edgeLocalX = useMaxEdge ? sourceMaxLocalX : sourceMinLocalX;
        return true;
    }


    private void CaptureContainerHeaderRightLineBaseAnchoredPositionIfNeeded()
    {
        if (hasContainerHeaderRightLineBaseAnchoredPosition || !containerInventoryTopLineRightRect)
            return;

        containerHeaderRightLineBaseAnchoredPosition = containerInventoryTopLineRightRect.anchoredPosition;
        hasContainerHeaderRightLineBaseAnchoredPosition = true;
    }


    private void RestoreContainerHeaderRightLineBaseAnchoredPosition()
    {
        if (!hasContainerHeaderRightLineBaseAnchoredPosition || !containerInventoryTopLineRightRect)
            return;

        containerInventoryTopLineRightRect.anchoredPosition = containerHeaderRightLineBaseAnchoredPosition;
    }


    private void CaptureContainerHeaderRightArrowBaseAnchoredPositionIfNeeded()
    {
        if (hasContainerHeaderRightArrowBaseAnchoredPosition || !containerInventoryArrowRightRect)
            return;

        containerHeaderRightArrowBaseAnchoredPosition = containerInventoryArrowRightRect.anchoredPosition;
        hasContainerHeaderRightArrowBaseAnchoredPosition = true;
    }


    private void RestoreContainerHeaderRightArrowBaseAnchoredPosition()
    {
        if (!hasContainerHeaderRightArrowBaseAnchoredPosition || !containerInventoryArrowRightRect)
            return;

        containerInventoryArrowRightRect.anchoredPosition = containerHeaderRightArrowBaseAnchoredPosition;
    }


    private void ApplyContainerHeaderRightLineLeftTrimOffset(float leftTrimInMeasurementSpace)
    {
        if (!hasContainerHeaderRightLineBaseAnchoredPosition || !containerInventoryTopLineRightRect)
            return;

        float trimAmount = IsFiniteFloat(leftTrimInMeasurementSpace)
            ? Mathf.Max(0.0f, leftTrimInMeasurementSpace)
            : 0.0f;

        float rightEdgePreserveOffsetX = trimAmount * (1.0f - containerInventoryTopLineRightRect.pivot.x);
        Vector2 nextAnchoredPosition = containerHeaderRightLineBaseAnchoredPosition;
        nextAnchoredPosition.x += rightEdgePreserveOffsetX;
        containerInventoryTopLineRightRect.anchoredPosition = nextAnchoredPosition;
    }


    private void AlignContainerHeaderRightLineRightEdgeToMidLine2(RectTransform measurementSpaceRect)
    {
        if (!measurementSpaceRect || !containerInventoryTopLineRightRect || !containerHighlightMidLine2)
            return;

        if (!TryGetRectLocalBounds(
                containerHighlightMidLine2,
                measurementSpaceRect,
                out _,
                out float targetRightEdgeLocalX,
                out _,
                out _))
        {
            return;
        }

        if (!TryGetRectLocalBounds(
                containerInventoryTopLineRightRect,
                measurementSpaceRect,
                out _,
                out float currentRightEdgeLocalX,
                out _,
                out _))
        {
            return;
        }

        float movementDeltaInMeasurementSpace = targetRightEdgeLocalX - currentRightEdgeLocalX;
        if (Mathf.Abs(movementDeltaInMeasurementSpace) <= 0.001f ||
            IsFiniteFloat(movementDeltaInMeasurementSpace) == false)
        {
            return;
        }

        Transform rightLineParentTransform = containerInventoryTopLineRightRect.parent;
        if (!rightLineParentTransform)
            return;

        Vector3 worldMovementDelta = measurementSpaceRect.TransformVector(
            new Vector3(movementDeltaInMeasurementSpace, 0.0f, 0.0f));
        Vector3 parentMovementDelta = rightLineParentTransform.InverseTransformVector(worldMovementDelta);

        Vector2 nextAnchoredPosition = containerInventoryTopLineRightRect.anchoredPosition;
        nextAnchoredPosition.x += parentMovementDelta.x;
        nextAnchoredPosition.y += parentMovementDelta.y;
        containerInventoryTopLineRightRect.anchoredPosition = nextAnchoredPosition;
    }


    private static void SetRectTransformLocalWidth(RectTransform rectTransform, float requiredLocalWidth)
    {
        if (!rectTransform)
            return;

        float sanitizedLocalWidth = IsFiniteFloat(requiredLocalWidth) ? requiredLocalWidth : 0.0f;
        float clampedLocalWidth = Mathf.Max(0.0f, sanitizedLocalWidth);

        LayoutElement layoutElement = rectTransform.GetComponent<LayoutElement>();
        if (layoutElement)
        {
            layoutElement.minWidth = clampedLocalWidth;
            layoutElement.preferredWidth = clampedLocalWidth;
            layoutElement.flexibleWidth = 0.0f;
        }

        rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, clampedLocalWidth);
    }


    private static bool IsFiniteFloat(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }


    private static float GetRectWidthInLocalSpace(RectTransform sourceRect, RectTransform localSpaceRect)
    {
        if (!sourceRect || !localSpaceRect)
            return 0.0f;

        Vector3[] corners = new Vector3[4];
        sourceRect.GetWorldCorners(corners);
        Vector3 worldLeft = (corners[0] + corners[1]) * 0.5f;
        Vector3 worldRight = (corners[2] + corners[3]) * 0.5f;
        Vector3 worldWidthVector = worldRight - worldLeft;
        Vector3 localWidthVector = localSpaceRect.InverseTransformVector(worldWidthVector);
        float resolvedWidth = Mathf.Abs(localWidthVector.x);

        if (IsFiniteFloat(resolvedWidth) == false || resolvedWidth <= 0.0f)
            resolvedWidth = Mathf.Abs(sourceRect.rect.width);

        return IsFiniteFloat(resolvedWidth) ? resolvedWidth : 0.0f;
    }


    private static float GetRectLocalXAxisScaleInLocalSpace(RectTransform sourceRect, RectTransform localSpaceRect)
    {
        if (!sourceRect || !localSpaceRect)
            return 0.0f;

        Vector3 worldLocalXAxis = sourceRect.TransformVector(Vector3.right);
        Vector3 localXAxisInSpace = localSpaceRect.InverseTransformVector(worldLocalXAxis);
        float resolvedScale = Mathf.Abs(localXAxisInSpace.x);
        return IsFiniteFloat(resolvedScale) ? resolvedScale : 0.0f;
    }


    private static bool TryGetCombinedRectLocalBoundsX(
        RectTransform localSpaceRect,
        out float combinedMinX,
        out float combinedMaxX,
        RectTransform firstRect,
        RectTransform secondRect,
        RectTransform thirdRect,
        RectTransform fourthRect)
    {
        combinedMinX = 0.0f;
        combinedMaxX = 0.0f;

        if (!localSpaceRect)
            return false;

        RectTransform[] rects = { firstRect, secondRect, thirdRect, fourthRect };
        bool hasAnyRect = false;

        for (int i = 0; i < rects.Length; i++)
        {
            RectTransform rect = rects[i];
            if (!rect)
                continue;

            if (!TryGetRectLocalBounds(rect, localSpaceRect, out float minX, out float maxX, out _, out _))
                continue;

            if (!hasAnyRect)
            {
                combinedMinX = minX;
                combinedMaxX = maxX;
                hasAnyRect = true;
                continue;
            }

            combinedMinX = Mathf.Min(combinedMinX, minX);
            combinedMaxX = Mathf.Max(combinedMaxX, maxX);
        }

        return hasAnyRect;
    }


    private static string GetPlayerInventoryCategoryLabel(PlayerInventoryListCategory category)
    {
        if (category == PlayerInventoryListCategory.Weapons) return "WEAPONS";
        if (category == PlayerInventoryListCategory.Apparel) return "APPAREL";
        if (category == PlayerInventoryListCategory.Aid) return "AID";
        if (category == PlayerInventoryListCategory.Misc) return "MISC";
        if (category == PlayerInventoryListCategory.Ammo) return "AMMO";
        return "ITEMS";
    }


    private string GetContainerInventoryCategoryLabel(PlayerInventoryListCategory category)
    {
        if (category == PlayerInventoryListCategory.Items)
            return GetActiveContainerDisplayNameOrFallback();

        return GetPlayerInventoryCategoryLabel(category);
    }


    private string GetActiveContainerDisplayNameOrFallback()
    {
        string fallbackName = "Container";
        if (!activeContainer)
            return fallbackName;

        string resolvedName = activeContainer.GetContainerName();
        if (string.IsNullOrWhiteSpace(resolvedName))
            return fallbackName;

        return resolvedName.Trim();
    }


    private static PlayerInventory.InventoryCategory ConvertPlayerInventoryListCategory(PlayerInventoryListCategory category)
    {
        if (category == PlayerInventoryListCategory.Apparel) return PlayerInventory.InventoryCategory.Apparel;
        if (category == PlayerInventoryListCategory.Aid) return PlayerInventory.InventoryCategory.Aid;
        if (category == PlayerInventoryListCategory.Misc) return PlayerInventory.InventoryCategory.Misc;
        if (category == PlayerInventoryListCategory.Ammo) return PlayerInventory.InventoryCategory.Ammo;
        return PlayerInventory.InventoryCategory.Weapons;
    }


    private void RegisterCancelCallback()
    {
        if (controls == null || isCancelCallbackRegistered)
            return;

        controls.UI.Cancel.performed += onCancelPerformed;
        isCancelCallbackRegistered = true;
    }


    private void UnregisterCancelCallback()
    {
        if (controls == null || !isCancelCallbackRegistered)
            return;

        controls.UI.Cancel.performed -= onCancelPerformed;
        isCancelCallbackRegistered = false;
    }


    private void SetGameplayActionsEnabled(bool enabled)
    {
        if (controls == null) return;

        var player = controls.Player;
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
        // Always wait one frame so the close-frame keypress cannot propagate into gameplay actions.
        yield return null;

        while (IsAnyKeyboardKeyPressed())
            yield return null;

        gameplayInputRestoreCoroutine = null;
        gameplayInputRestoreCoroutineHost = null;

        if (isOpen || !disableGameplayActionsWhenOpen)
            yield break;

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

        if (isActiveAndEnabled)
            return this;

        return null;
    }


    private static bool IsAnyKeyboardKeyPressed()
    {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard.anyKey != null && keyboard.anyKey.isPressed;
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


    private void ResolveAndSubscribeActivePlayerInventory()
    {
        PlayerInventory resolvedInventory = null;
        PlayerWeaponController resolvedWeaponController = null;
        PlayerState resolvedPlayerState = null;

        if (activeInteractor)
        {
            resolvedInventory = activeInteractor.GetComponentInParent<PlayerInventory>(true);
            resolvedWeaponController = activeInteractor.GetComponentInParent<PlayerWeaponController>(true);
            resolvedPlayerState = activeInteractor.GetComponentInParent<PlayerState>(true);
        }

        if (!resolvedInventory)
            resolvedInventory = FindAnyObjectByType<PlayerInventory>();

        if (!resolvedWeaponController)
            resolvedWeaponController = FindAnyObjectByType<PlayerWeaponController>();

        if (!resolvedPlayerState)
            resolvedPlayerState = FindAnyObjectByType<PlayerState>();

        SetActivePlayerInventory(resolvedInventory);
        SetActivePlayerWeaponController(resolvedWeaponController);
        SetActivePlayerState(resolvedPlayerState);
    }


    private void SetActivePlayerInventory(PlayerInventory newInventory)
    {
        if (activePlayerInventory == newInventory)
            return;

        if (activePlayerInventory)
            activePlayerInventory.OnInventoryChanged -= OnActivePlayerInventoryChanged;

        activePlayerInventory = newInventory;

        if (activePlayerInventory)
            activePlayerInventory.OnInventoryChanged += OnActivePlayerInventoryChanged;
    }


    private void SetActivePlayerWeaponController(PlayerWeaponController newWeaponController)
    {
        activePlayerWeaponController = newWeaponController;
    }


    private void SetActivePlayerState(PlayerState newPlayerState)
    {
        activePlayerState = newPlayerState;
    }


    private void SetActiveContainer(Container newContainer)
    {
        if (activeContainer == newContainer)
            return;

        if (activeContainer)
            activeContainer.OnInventoryChanged -= OnActiveContainerInventoryChanged;

        activeContainer = newContainer;

        if (activeContainer)
            activeContainer.OnInventoryChanged += OnActiveContainerInventoryChanged;

        // Re-capture container header text-to-arrow spacing using the active container label.
        hasContainerHeaderCategoryTextToRightArrowGap = false;

        RefreshContainerInventoryCategoryHeader();
    }


    private void OnActivePlayerInventoryChanged()
    {
        if (!isOpen)
            return;

        if (isQuantityTransferPromptOpen)
            CloseQuantityTransferPrompt(true);

        RefreshPlayerWeightText();
        RefreshInventoryLists();
    }


    private void OnActiveContainerInventoryChanged()
    {
        if (!isOpen)
            return;

        if (isQuantityTransferPromptOpen)
            CloseQuantityTransferPrompt(true);

        RefreshInventoryLists();
    }


    private void RefreshPlayerWeightText()
    {
        float currentWeight = activePlayerInventory ? Mathf.Max(0f, activePlayerInventory.GetWeight()) : 0f;
        float maxWeight = activePlayerInventory ? Mathf.Max(0f, activePlayerInventory.GetMaxWeight()) : 0f;
        string weightText = $"{currentWeight:0.#}/{maxWeight:0.#}";

        SetTextIfChanged(playerWgText, weightText);
    }


    private void RefreshInventoryLists()
    {
        if (!isOpen)
            return;

        RefreshPlayerInventoryList();
        RefreshContainerInventoryList();
        RefreshHoveredInventoryListHighlights();
        RefreshPlayerEquippedSelectedBoxIndicator();
    }


    private void RefreshPlayerInventoryList()
    {
        Transform listParent = ResolvePlayerListParent();
        List<InventoryListRow> rows = BuildRowsFromPlayerInventory(activePlayerInventory, currentPlayerInventoryListCategory);
        PopulateInventoryList(listParent, spawnedPlayerListEntries, rows, true);
    }


    private void RefreshContainerInventoryList()
    {
        Transform listParent = ResolveContainerListParent();
        List<InventoryListRow> rows = BuildRowsFromContainerInventory(activeContainer, currentContainerInventoryListCategory);
        PopulateInventoryList(listParent, spawnedContainerListEntries, rows, false);
    }


    private List<InventoryListRow> BuildRowsFromPlayerInventory(
        PlayerInventory inventory,
        PlayerInventoryListCategory selectedCategory)
    {
        Dictionary<ScriptableObject, int> totalsByDefinition = new Dictionary<ScriptableObject, int>();
        List<InventoryListRow> rows = new List<InventoryListRow>();

        if (!inventory)
            return rows;

        if (selectedCategory == PlayerInventoryListCategory.Items)
        {
            for (int categoryIndex = 0; categoryIndex < InventoryCategories.Length; categoryIndex++)
            {
                PlayerInventory.InventoryCategory category = InventoryCategories[categoryIndex];
                IReadOnlyList<PlayerInventory.InventoryEntry> categoryItems = inventory.GetCategoryItems(category);
                AddCategoryEntriesToTotals(categoryItems, totalsByDefinition);
            }
        }
        else
        {
            PlayerInventory.InventoryCategory inventoryCategory = ConvertPlayerInventoryListCategory(selectedCategory);
            IReadOnlyList<PlayerInventory.InventoryEntry> categoryItems = inventory.GetCategoryItems(inventoryCategory);
            AddCategoryEntriesToTotals(categoryItems, totalsByDefinition);
        }

        BuildSortedRowsFromTotals(totalsByDefinition, rows);
        return rows;
    }


    private List<InventoryListRow> BuildRowsFromContainerInventory(
        Container container,
        PlayerInventoryListCategory selectedCategory)
    {
        Dictionary<ScriptableObject, int> totalsByDefinition = new Dictionary<ScriptableObject, int>();
        List<InventoryListRow> rows = new List<InventoryListRow>();

        if (!container)
            return rows;

        if (selectedCategory == PlayerInventoryListCategory.Items)
        {
            for (int categoryIndex = 0; categoryIndex < InventoryCategories.Length; categoryIndex++)
            {
                PlayerInventory.InventoryCategory category = InventoryCategories[categoryIndex];
                IReadOnlyList<PlayerInventory.InventoryEntry> categoryItems = container.GetCategoryItems(category);
                AddCategoryEntriesToTotals(categoryItems, totalsByDefinition);
            }
        }
        else
        {
            PlayerInventory.InventoryCategory category = ConvertPlayerInventoryListCategory(selectedCategory);
            IReadOnlyList<PlayerInventory.InventoryEntry> categoryItems = container.GetCategoryItems(category);
            AddCategoryEntriesToTotals(categoryItems, totalsByDefinition);
        }

        BuildSortedRowsFromTotals(totalsByDefinition, rows);
        return rows;
    }


    private void AddCategoryEntriesToTotals(
        IReadOnlyList<PlayerInventory.InventoryEntry> categoryItems,
        Dictionary<ScriptableObject, int> totalsByDefinition)
    {
        if (categoryItems == null || totalsByDefinition == null)
            return;

        for (int i = 0; i < categoryItems.Count; i++)
        {
            PlayerInventory.InventoryEntry entry = categoryItems[i];
            if (entry == null)
                continue;

            ScriptableObject itemDefinition = entry.GetItemDefinition();
            if (!itemDefinition)
                continue;

            int quantity = Mathf.Max(0, entry.GetQuantity());
            if (quantity <= 0)
                continue;

            if (totalsByDefinition.TryGetValue(itemDefinition, out int existingQuantity))
            {
                totalsByDefinition[itemDefinition] = existingQuantity + quantity;
                continue;
            }

            totalsByDefinition.Add(itemDefinition, quantity);
        }
    }


    private void BuildSortedRowsFromTotals(
        Dictionary<ScriptableObject, int> totalsByDefinition,
        List<InventoryListRow> outputRows)
    {
        if (totalsByDefinition == null || outputRows == null)
            return;

        outputRows.Clear();

        foreach (KeyValuePair<ScriptableObject, int> pair in totalsByDefinition)
        {
            ScriptableObject itemDefinition = pair.Key;
            int quantity = Mathf.Max(0, pair.Value);
            if (!itemDefinition || quantity <= 0)
                continue;

            string displayName = GetDisplayNameFromDefinition(itemDefinition, itemDefinition.name);
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = itemDefinition.name;

            outputRows.Add(new InventoryListRow
            {
                ItemDefinition = itemDefinition,
                DisplayName = displayName,
                Quantity = quantity
            });
        }

        outputRows.Sort((left, right) =>
        {
            int nameComparison = string.Compare(left.DisplayName, right.DisplayName, System.StringComparison.OrdinalIgnoreCase);
            if (nameComparison != 0)
                return nameComparison;

            return string.Compare(left.DisplayName, right.DisplayName, System.StringComparison.Ordinal);
        });
    }


    private void PopulateInventoryList(
        Transform listParent,
        List<GameObject> pooledEntries,
        List<InventoryListRow> rows,
        bool isPlayerList)
    {
        int targetCount = rows != null ? rows.Count : 0;

        if (!listParent || !itemEntryPrefab)
        {
            SetPooledEntriesActive(pooledEntries, 0);
            if (isPlayerList)
            {
                playerListDefinitionByEntryObject.Clear();
                playerListQuantityByEntryObject.Clear();
                ClearPlayerListHoveredEntryHighlight();
                ClearPlayerEquippedSelectedBoxIndicator();
            }
            else
            {
                containerListDefinitionByEntryObject.Clear();
                containerListQuantityByEntryObject.Clear();
                ClearContainerListHoveredEntryHighlight();
            }
            return;
        }

        EnsurePooledEntries(pooledEntries, targetCount, itemEntryPrefab, listParent);

        if (isPlayerList)
        {
            playerListDefinitionByEntryObject.Clear();
            playerListQuantityByEntryObject.Clear();
        }
        else
        {
            containerListDefinitionByEntryObject.Clear();
            containerListQuantityByEntryObject.Clear();
        }

        for (int i = 0; i < targetCount; i++)
        {
            GameObject rowObject = pooledEntries[i];
            if (!rowObject)
                continue;

            if (rowObject.transform.parent != listParent)
                rowObject.transform.SetParent(listParent, false);

            rowObject.transform.SetSiblingIndex(i);
            if (!rowObject.activeSelf)
                rowObject.SetActive(true);

            InventoryListRow row = rows[i];
            string rowLabel = row.Quantity > 1 ? $"{row.DisplayName} ({row.Quantity})" : row.DisplayName;
            TMP_Text rowText = rowObject.GetComponentInChildren<TMP_Text>(true);
            SetTextIfChanged(rowText, rowLabel);
            BindInventoryListEntryEvents(rowObject, isPlayerList);

            if (isPlayerList)
            {
                ScriptableObject rowItemDefinition = row.ItemDefinition;
                if (rowItemDefinition)
                    playerListDefinitionByEntryObject[rowObject] = rowItemDefinition;

                playerListQuantityByEntryObject[rowObject] = Mathf.Max(0, row.Quantity);
            }
            else
            {
                ScriptableObject rowItemDefinition = row.ItemDefinition;
                if (rowItemDefinition)
                    containerListDefinitionByEntryObject[rowObject] = rowItemDefinition;

                containerListQuantityByEntryObject[rowObject] = Mathf.Max(0, row.Quantity);
            }
        }

        SetPooledEntriesActive(pooledEntries, targetCount);

        if (isPlayerList)
        {
            ValidateHoveredListEntryReference(true, pooledEntries);
            RefreshPlayerEquippedSelectedBoxIndicator();
        }
        else
            ValidateHoveredListEntryReference(false, pooledEntries);
    }


    private void EnsurePooledEntries(
        List<GameObject> pooledEntries,
        int targetCount,
        GameObject entryPrefab,
        Transform parent)
    {
        if (pooledEntries == null)
            return;

        for (int i = pooledEntries.Count - 1; i >= 0; i--)
        {
            if (pooledEntries[i])
                continue;

            pooledEntries.RemoveAt(i);
        }

        for (int i = pooledEntries.Count; i < targetCount; i++)
        {
            GameObject spawnedEntry = Instantiate(entryPrefab, parent, false);
            ApplyContainerPipBoyPaletteColorOverrides(spawnedEntry);
            pooledEntries.Add(spawnedEntry);
        }
    }


    private static void SetPooledEntriesActive(List<GameObject> pooledEntries, int activeCount)
    {
        if (pooledEntries == null)
            return;

        for (int i = 0; i < pooledEntries.Count; i++)
        {
            GameObject rowObject = pooledEntries[i];
            if (!rowObject)
                continue;

            bool shouldBeActive = i < activeCount;
            if (rowObject.activeSelf != shouldBeActive)
                rowObject.SetActive(shouldBeActive);
        }
    }


    private void BindInventoryListEntryEvents(GameObject rowObject, bool isPlayerList)
    {
        if (!rowObject)
            return;

        TMP_Text rowText = rowObject.GetComponentInChildren<TMP_Text>(true);
        if (rowText)
            rowText.raycastTarget = false;

        EventTrigger eventTrigger = rowObject.GetComponent<EventTrigger>();
        if (!eventTrigger)
            eventTrigger = rowObject.AddComponent<EventTrigger>();

        if (eventTrigger.triggers == null)
            eventTrigger.triggers = new List<EventTrigger.Entry>();

        eventTrigger.triggers.Clear();

        EventTrigger.Entry pointerEnterEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerEnter
        };
        pointerEnterEntry.callback.AddListener(_ => OnInventoryListEntryPointerEnter(rowObject, isPlayerList));
        eventTrigger.triggers.Add(pointerEnterEntry);

        EventTrigger.Entry pointerExitEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerExit
        };
        pointerExitEntry.callback.AddListener(_ => OnInventoryListEntryPointerExit(rowObject, isPlayerList));
        eventTrigger.triggers.Add(pointerExitEntry);

        EventTrigger.Entry pointerClickEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.PointerClick
        };
        pointerClickEntry.callback.AddListener(eventData => OnInventoryListEntryPointerClick(rowObject, isPlayerList, eventData));
        eventTrigger.triggers.Add(pointerClickEntry);

        ScrollRect ownerScrollRect = isPlayerList ? playerScroll : containerScroll;
        if (ownerScrollRect)
        {
            EventTrigger.Entry scrollEntry = new EventTrigger.Entry
            {
                eventID = EventTriggerType.Scroll
            };
            scrollEntry.callback.AddListener(eventData => ForwardScrollToScrollRect(eventData, ownerScrollRect));
            eventTrigger.triggers.Add(scrollEntry);
        }
    }


    private void OnInventoryListEntryPointerEnter(GameObject rowObject, bool isPlayerList)
    {
        UpdateHoveredItemStatsFromRow(rowObject, isPlayerList);

        if (isPlayerList)
        {
            hoveredPlayerListEntryObject = rowObject;
            UpdateHoveredListButtonHighlight(rowObject, playerListEntryHighlight, true);
            return;
        }

        hoveredContainerListEntryObject = rowObject;
        UpdateHoveredListButtonHighlight(rowObject, containerListEntryHighlight, false);
    }


    private void OnInventoryListEntryPointerExit(GameObject rowObject, bool isPlayerList)
    {
        if (isPlayerList)
        {
            if (hoveredPlayerListEntryObject == rowObject)
                ClearPlayerListHoveredEntryHighlight();
            return;
        }

        if (hoveredContainerListEntryObject == rowObject)
            ClearContainerListHoveredEntryHighlight();
    }


    private void OnInventoryListEntryPointerClick(GameObject rowObject, bool isPlayerList, BaseEventData eventData)
    {
        if (!isOpen || !rowObject || !activeContainer || !activePlayerInventory)
            return;

        if (isQuantityTransferPromptOpen)
            return;

        if (eventData is PointerEventData pointerEventData &&
            pointerEventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (TryOpenQuantityTransferPromptForRow(rowObject, isPlayerList))
            return;

        bool transferSucceeded = isPlayerList
            ? TryTransferPlayerListEntryToContainer(rowObject, 1)
            : TryTransferContainerListEntryToPlayer(rowObject, 1);
        if (!transferSucceeded)
            return;

        RefreshPlayerWeightText();
        RefreshInventoryLists();
    }


    private void UpdateHoveredItemStatsFromRow(GameObject rowObject, bool isPlayerList)
    {
        if (!isOpen || !rowObject)
        {
            ClearHoveredItemStats();
            return;
        }

        if (!TryResolveInventoryEntryForRow(
                rowObject,
                isPlayerList,
                out ScriptableObject itemDefinition,
                out PlayerInventory.InventoryEntry inventoryEntry))
        {
            ClearHoveredItemStats();
            return;
        }

        WeaponDefinition weaponDefinition = itemDefinition as WeaponDefinition;
        ApparelDefinition apparelDefinition = itemDefinition as ApparelDefinition;
        AidDefinition aidDefinition = itemDefinition as AidDefinition;
        bool isWeapon = weaponDefinition != null;
        bool isApparel = apparelDefinition != null;
        bool isAid = aidDefinition != null;
        bool showDamDrAndCondition = isWeapon || isApparel;

        int instanceIndex = ResolveDisplayInstanceIndex(inventoryEntry);
        float itemValue = GetDefinitionValueOrDefault(itemDefinition);
        float conditionPercent = 0.0f;
        int loadedMagazineRounds = 0;

        bool supportsInstanceValue =
            itemDefinition is WeaponDefinition ||
            itemDefinition is ApparelDefinition ||
            itemDefinition is MiscDefinition ||
            itemDefinition is AmmoDefinition ||
            itemDefinition is AmmoItemDefinition;

        if (instanceIndex >= 0 &&
            TryGetHoveredEntryInstanceStats(
                inventoryEntry,
                isPlayerList,
                instanceIndex,
                out float instanceValue,
                out float instanceConditionPercent,
                out int instanceLoadedMagazineRounds))
        {
            if (supportsInstanceValue)
                itemValue = Mathf.Max(0.0f, instanceValue);

            conditionPercent = Mathf.Clamp(instanceConditionPercent, 0.0f, 100.0f);
            loadedMagazineRounds = Mathf.Max(0, instanceLoadedMagazineRounds);
        }

        float itemWeight = Mathf.Max(0.0f, GetDefinitionWeightOrDefault(itemDefinition));

        string damOrDrLabel = string.Empty;
        string damOrDrValue = string.Empty;
        if (isWeapon)
        {
            damOrDrLabel = "DAM";
            damOrDrValue = Mathf.Max(0, weaponDefinition.GetDamage()).ToString();
        }
        else if (isApparel)
        {
            damOrDrLabel = "DR";
            damOrDrValue = Mathf.Max(0, apparelDefinition.GetDamageResistance()).ToString();
        }

        AmmoDefinition ammoType = isWeapon ? weaponDefinition.GetAmmoType() : null;
        bool weaponUsesAmmunition = isWeapon && ammoType;

        int reserveAmmoRounds = 0;
        if (weaponUsesAmmunition && activePlayerInventory)
            reserveAmmoRounds = Mathf.Max(0, activePlayerInventory.GetAmmoCount(ammoType));

        string ammoDisplayName = string.Empty;
        if (weaponUsesAmmunition)
        {
            ammoDisplayName = ammoType.GetDisplayName();
            if (string.IsNullOrWhiteSpace(ammoDisplayName))
                ammoDisplayName = ammoType.name;
        }

        string itemInfo = string.Empty;
        if (isAid)
            itemInfo = BuildAidEffectsDisplayText(aidDefinition);
        else if (weaponUsesAmmunition)
            itemInfo = $"{ammoDisplayName} ({loadedMagazineRounds}/{reserveAmmoRounds})";

        bool showItemInfo = !string.IsNullOrWhiteSpace(itemInfo);

        hoveredItemStatsRowObject = rowObject;
        hoveredItemStatsRowIsPlayerList = isPlayerList;

        SetHoveredItemGeneralStatsVisible(true);
        SetHoveredItemConditionStatsVisible(showDamDrAndCondition);
        SetHoveredItemInfoVisible(showItemInfo);

        SetTextIfChanged(hoveredDamDrLabelText, damOrDrLabel);
        SetTextIfChanged(hoveredDamDrItemText, damOrDrValue);
        SetTextIfChanged(hoveredWgItemText, itemWeight.ToString("0.#"));
        SetTextIfChanged(hoveredValItemText, FormatValue(itemValue));
        SetTextIfChanged(hoveredItemInfoText, itemInfo);
        SetHoveredConditionBarFillAmount(showDamDrAndCondition ? Mathf.Clamp01(conditionPercent / 100.0f) : 0.0f);
    }


    private void RefreshHoveredItemStatsFromCurrentHoveredRow()
    {
        if (!hoveredItemStatsRowObject || !hoveredItemStatsRowObject.activeInHierarchy)
        {
            ClearHoveredItemStats();
            return;
        }

        UpdateHoveredItemStatsFromRow(hoveredItemStatsRowObject, hoveredItemStatsRowIsPlayerList);
    }


    private void ClearHoveredItemStatsIfOwnedByRow(GameObject rowObject, bool isPlayerList)
    {
        if (!rowObject)
            return;

        if (hoveredItemStatsRowObject != rowObject || hoveredItemStatsRowIsPlayerList != isPlayerList)
            return;

        ClearHoveredItemStats();
    }


    private void ClearHoveredItemStats()
    {
        hoveredItemStatsRowObject = null;
        hoveredItemStatsRowIsPlayerList = false;

        SetHoveredItemGeneralStatsVisible(false);
        SetHoveredItemConditionStatsVisible(false);
        SetHoveredItemInfoVisible(false);

        SetTextIfChanged(hoveredDamDrLabelText, string.Empty);
        SetTextIfChanged(hoveredDamDrItemText, string.Empty);
        SetTextIfChanged(hoveredWgItemText, string.Empty);
        SetTextIfChanged(hoveredValItemText, string.Empty);
        SetTextIfChanged(hoveredItemInfoText, string.Empty);
        SetHoveredConditionBarFillAmount(0.0f);
    }


    private bool TryResolveInventoryEntryForRow(
        GameObject rowObject,
        bool isPlayerList,
        out ScriptableObject itemDefinition,
        out PlayerInventory.InventoryEntry inventoryEntry)
    {
        itemDefinition = null;
        inventoryEntry = null;

        if (!rowObject)
            return false;

        if (isPlayerList)
        {
            if (!playerListDefinitionByEntryObject.TryGetValue(rowObject, out itemDefinition) || !itemDefinition)
                return false;

            return TryFindFirstPlayerInventoryEntryForDefinition(
                itemDefinition,
                currentPlayerInventoryListCategory,
                out inventoryEntry);
        }

        if (!containerListDefinitionByEntryObject.TryGetValue(rowObject, out itemDefinition) || !itemDefinition)
            return false;

        return TryFindFirstContainerInventoryEntryForDefinition(
            itemDefinition,
            currentContainerInventoryListCategory,
            out inventoryEntry);
    }


    private static int ResolveDisplayInstanceIndex(PlayerInventory.InventoryEntry inventoryEntry)
    {
        if (inventoryEntry == null)
            return -1;

        IReadOnlyList<PlayerInventory.ItemInstanceData> instances = inventoryEntry.GetItemInstances();
        if (instances == null || instances.Count == 0)
            return -1;

        return 0;
    }


    private bool TryGetHoveredEntryInstanceStats(
        PlayerInventory.InventoryEntry inventoryEntry,
        bool isPlayerList,
        int instanceIndex,
        out float instanceValue,
        out float instanceConditionPercent,
        out int loadedMagazineRounds)
    {
        instanceValue = 0.0f;
        instanceConditionPercent = 0.0f;
        loadedMagazineRounds = 0;
        if (inventoryEntry == null || instanceIndex < 0)
            return false;

        if (isPlayerList)
        {
            if (!activePlayerInventory)
                return false;

            instanceValue = Mathf.Max(0.0f, activePlayerInventory.GetInstanceValue(inventoryEntry, instanceIndex));
            instanceConditionPercent = Mathf.Clamp(activePlayerInventory.GetInstanceConditionPercent(inventoryEntry, instanceIndex), 0.0f, 100.0f);
            loadedMagazineRounds = Mathf.Max(0, activePlayerInventory.GetInstanceLoadedMagazineRounds(inventoryEntry, instanceIndex));
            return true;
        }

        if (!activeContainer)
            return false;

        instanceValue = Mathf.Max(0.0f, activeContainer.GetInstanceValue(inventoryEntry, instanceIndex));
        instanceConditionPercent = Mathf.Clamp(activeContainer.GetInstanceConditionPercent(inventoryEntry, instanceIndex), 0.0f, 100.0f);
        loadedMagazineRounds = Mathf.Max(0, activeContainer.GetInstanceLoadedMagazineRounds(inventoryEntry, instanceIndex));
        return true;
    }


    private void SetHoveredItemGeneralStatsVisible(bool visible)
    {
        SetActiveSafe(itemLine3, visible);
        SetActiveSafe(itemLine5, visible);
        SetActiveSafe(hoveredWgLabelText ? hoveredWgLabelText.gameObject : null, visible);
        SetActiveSafe(hoveredValLabelText ? hoveredValLabelText.gameObject : null, visible);
        SetActiveSafe(hoveredWgItemText ? hoveredWgItemText.gameObject : null, visible);
        SetActiveSafe(hoveredValItemText ? hoveredValItemText.gameObject : null, visible);
    }


    private void SetHoveredItemConditionStatsVisible(bool visible)
    {
        SetActiveSafe(itemLine1, visible);
        SetActiveSafe(itemLine2, visible);
        SetActiveSafe(hoveredDamDrLabelText ? hoveredDamDrLabelText.gameObject : null, visible);
        SetActiveSafe(hoveredDamDrItemText ? hoveredDamDrItemText.gameObject : null, visible);
        SetActiveSafe(hoveredCndLabelText ? hoveredCndLabelText.gameObject : null, visible);
        SetActiveSafe(cndBarBackground, visible);
        SetActiveSafe(cndBarFillImage ? cndBarFillImage.gameObject : null, visible);
    }


    private void SetHoveredItemInfoVisible(bool visible)
    {
        SetActiveSafe(itemLine4, visible);
        SetActiveSafe(hoveredItemInfoText ? hoveredItemInfoText.gameObject : null, visible);
    }


    private bool TryTransferPlayerListEntryToContainer(GameObject rowObject, int amount)
    {
        if (!rowObject ||
            !activeContainer ||
            !activePlayerInventory ||
            !playerListDefinitionByEntryObject.TryGetValue(rowObject, out ScriptableObject itemDefinition) ||
            !itemDefinition)
        {
            return false;
        }

        return TryTransferPlayerDefinitionToContainer(itemDefinition, amount);
    }


    private bool TryTransferContainerListEntryToPlayer(GameObject rowObject, int amount)
    {
        if (!rowObject ||
            !activeContainer ||
            !activePlayerInventory ||
            !containerListDefinitionByEntryObject.TryGetValue(rowObject, out ScriptableObject itemDefinition) ||
            !itemDefinition)
        {
            return false;
        }

        return TryTransferContainerDefinitionToPlayer(itemDefinition, amount);
    }


    private bool TryTransferPlayerDefinitionToContainer(ScriptableObject itemDefinition, int amount)
    {
        if (!itemDefinition || !activeContainer || !activePlayerInventory)
            return false;

        int remaining = Mathf.Max(0, amount);
        if (remaining <= 0)
            return false;

        bool transferredAny = false;
        bool transferredEquippedWeapon = false;

        while (remaining > 0)
        {
            if (!TryFindFirstPlayerInventoryEntryForDefinition(
                    itemDefinition,
                    currentPlayerInventoryListCategory,
                    out PlayerInventory.InventoryEntry sourceEntry))
            {
                break;
            }

            int sourceQuantity = Mathf.Max(0, sourceEntry.GetQuantity());
            if (sourceQuantity <= 0)
                break;

            int transferAmount = Mathf.Min(remaining, sourceQuantity);
            if (!transferredEquippedWeapon && IsPlayerWeaponEntryTransferringEquippedInstance(sourceEntry))
                transferredEquippedWeapon = true;

            if (!activeContainer.TryTransferFromPlayer(activePlayerInventory, sourceEntry, transferAmount))
                break;

            transferredAny = true;
            remaining -= transferAmount;
        }

        if (transferredAny && transferredEquippedWeapon)
            UnequipTransferredPlayerWeaponAndRefreshInventories();

        return transferredAny && remaining <= 0;
    }


    private bool TryTransferContainerDefinitionToPlayer(ScriptableObject itemDefinition, int amount)
    {
        if (!itemDefinition || !activeContainer || !activePlayerInventory)
            return false;

        int remaining = Mathf.Max(0, amount);
        if (remaining <= 0)
            return false;

        bool transferredAny = false;
        while (remaining > 0)
        {
            if (!TryFindFirstContainerInventoryEntryForDefinition(
                    itemDefinition,
                    currentContainerInventoryListCategory,
                    out PlayerInventory.InventoryEntry sourceEntry))
            {
                break;
            }

            int sourceQuantity = Mathf.Max(0, sourceEntry.GetQuantity());
            if (sourceQuantity <= 0)
                break;

            int transferAmount = Mathf.Min(remaining, sourceQuantity);
            if (!activeContainer.TryTransferToPlayer(activePlayerInventory, sourceEntry, transferAmount))
                break;

            transferredAny = true;
            remaining -= transferAmount;
        }

        return transferredAny && remaining <= 0;
    }


    private bool TryOpenQuantityTransferPromptForRow(GameObject rowObject, bool isPlayerList)
    {
        if (!rowObject || isQuantityTransferPromptOpen)
            return false;

        ScriptableObject itemDefinition = null;
        int totalQuantity = 0;

        if (isPlayerList)
        {
            playerListDefinitionByEntryObject.TryGetValue(rowObject, out itemDefinition);
            playerListQuantityByEntryObject.TryGetValue(rowObject, out totalQuantity);
        }
        else
        {
            containerListDefinitionByEntryObject.TryGetValue(rowObject, out itemDefinition);
            containerListQuantityByEntryObject.TryGetValue(rowObject, out totalQuantity);
        }

        if (!itemDefinition)
            return false;

        int resolvedTotalQuantity = GetTotalQuantityForDefinition(itemDefinition, isPlayerList);
        if (resolvedTotalQuantity > 0)
            totalQuantity = resolvedTotalQuantity;

        if (totalQuantity <= 1)
            return false;

        if (!multipleSliderObject || !multipleQuantitySlider)
            return false;

        pendingQuantityTransferIsPlayerList = isPlayerList;
        pendingQuantityTransferDefinition = itemDefinition;
        pendingQuantityTransferTotalQuantity = Mathf.Max(1, totalQuantity);
        isQuantityTransferPromptOpen = true;

        multipleQuantitySlider.wholeNumbers = true;
        multipleQuantitySlider.minValue = 1.0f;
        multipleQuantitySlider.maxValue = pendingQuantityTransferTotalQuantity;
        multipleQuantitySlider.SetValueWithoutNotify(1.0f);
        UpdateQuantityTransferPromptText();
        SetActiveSafe(multipleSliderObject, true);
        return true;
    }


    private void ConfirmQuantityTransferPromptSelection()
    {
        if (!isQuantityTransferPromptOpen || !pendingQuantityTransferDefinition)
            return;

        bool transferFromPlayer = pendingQuantityTransferIsPlayerList;
        ScriptableObject itemDefinition = pendingQuantityTransferDefinition;
        int quantityToTransfer = GetSelectedQuantityFromPrompt();

        CloseQuantityTransferPrompt(true);

        bool transferSucceeded = transferFromPlayer
            ? TryTransferPlayerDefinitionToContainer(itemDefinition, quantityToTransfer)
            : TryTransferContainerDefinitionToPlayer(itemDefinition, quantityToTransfer);
        if (!transferSucceeded)
            return;

        RefreshPlayerWeightText();
        RefreshInventoryLists();
    }


    private void CancelQuantityTransferPrompt()
    {
        if (!isQuantityTransferPromptOpen)
            return;

        CloseQuantityTransferPrompt(true);
    }


    private void CloseQuantityTransferPrompt(bool hideSliderObject)
    {
        isQuantityTransferPromptOpen = false;
        pendingQuantityTransferIsPlayerList = false;
        pendingQuantityTransferDefinition = null;
        pendingQuantityTransferTotalQuantity = 0;

        if (quantityNumberText)
            SetTextIfChanged(quantityNumberText, string.Empty);

        if (hideSliderObject)
            SetActiveSafe(multipleSliderObject, false);
    }


    private void OnMultipleQuantitySliderValueChanged(float _)
    {
        UpdateQuantityTransferPromptText();
    }


    private void UpdateQuantityTransferPromptText()
    {
        if (!quantityNumberText)
            return;

        if (!isQuantityTransferPromptOpen || pendingQuantityTransferTotalQuantity <= 0)
        {
            SetTextIfChanged(quantityNumberText, string.Empty);
            return;
        }

        int selectedQuantity = GetSelectedQuantityFromPrompt();
        SetTextIfChanged(quantityNumberText, $"{selectedQuantity}/{pendingQuantityTransferTotalQuantity}");
    }


    private int GetSelectedQuantityFromPrompt()
    {
        if (pendingQuantityTransferTotalQuantity <= 0)
            return 0;

        if (!multipleQuantitySlider)
            return Mathf.Clamp(1, 1, pendingQuantityTransferTotalQuantity);

        int selectedQuantity = Mathf.RoundToInt(multipleQuantitySlider.value);
        return Mathf.Clamp(selectedQuantity, 1, pendingQuantityTransferTotalQuantity);
    }


    private int GetTotalQuantityForDefinition(ScriptableObject itemDefinition, bool isPlayerList)
    {
        if (!itemDefinition)
            return 0;

        int totalQuantity = 0;
        if (isPlayerList)
        {
            if (!activePlayerInventory)
                return 0;

            if (currentPlayerInventoryListCategory == PlayerInventoryListCategory.Items)
            {
                for (int categoryIndex = 0; categoryIndex < InventoryCategories.Length; categoryIndex++)
                    totalQuantity += CountQuantityForDefinition(
                        activePlayerInventory.GetCategoryItems(InventoryCategories[categoryIndex]),
                        itemDefinition);
            }
            else
            {
                PlayerInventory.InventoryCategory category =
                    ConvertPlayerInventoryListCategory(currentPlayerInventoryListCategory);
                totalQuantity += CountQuantityForDefinition(activePlayerInventory.GetCategoryItems(category), itemDefinition);
            }

            return Mathf.Max(0, totalQuantity);
        }

        if (!activeContainer)
            return 0;

        if (currentContainerInventoryListCategory == PlayerInventoryListCategory.Items)
        {
            for (int categoryIndex = 0; categoryIndex < InventoryCategories.Length; categoryIndex++)
                totalQuantity += CountQuantityForDefinition(
                    activeContainer.GetCategoryItems(InventoryCategories[categoryIndex]),
                    itemDefinition);
        }
        else
        {
            PlayerInventory.InventoryCategory category =
                ConvertPlayerInventoryListCategory(currentContainerInventoryListCategory);
            totalQuantity += CountQuantityForDefinition(activeContainer.GetCategoryItems(category), itemDefinition);
        }

        return Mathf.Max(0, totalQuantity);
    }


    private static int CountQuantityForDefinition(
        IReadOnlyList<PlayerInventory.InventoryEntry> entries,
        ScriptableObject itemDefinition)
    {
        if (entries == null || !itemDefinition)
            return 0;

        int total = 0;
        for (int i = 0; i < entries.Count; i++)
        {
            PlayerInventory.InventoryEntry entry = entries[i];
            if (entry == null || entry.GetItemDefinition() != itemDefinition)
                continue;

            total += Mathf.Max(0, entry.GetQuantity());
        }

        return Mathf.Max(0, total);
    }


    private bool IsPlayerWeaponEntryTransferringEquippedInstance(PlayerInventory.InventoryEntry sourceEntry)
    {
        if (sourceEntry == null ||
            !(sourceEntry.GetItemDefinition() is WeaponDefinition weaponDefinition) ||
            !activePlayerWeaponController)
        {
            return false;
        }

        string equippedInstanceId = activePlayerWeaponController.GetEquippedInventoryWeaponInstanceId();
        if (!string.IsNullOrWhiteSpace(equippedInstanceId))
        {
            IReadOnlyList<PlayerInventory.ItemInstanceData> instances = sourceEntry.GetItemInstances();
            int transferInstanceIndex = instances != null ? instances.Count - 1 : -1;
            if (transferInstanceIndex < 0)
                return false;

            string transferInstanceId = sourceEntry.GetInstanceId(transferInstanceIndex);
            if (string.IsNullOrWhiteSpace(transferInstanceId))
                return false;

            return string.Equals(transferInstanceId, equippedInstanceId, System.StringComparison.Ordinal);
        }

        PlayerWeaponController.WeaponEntry currentWeaponEntry = activePlayerWeaponController.GetCurrentWeapon();
        if (currentWeaponEntry == null)
            return false;

        string equippedWeaponName = currentWeaponEntry.WeaponName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(equippedWeaponName))
            return false;

        return DoesWeaponDefinitionMatchEquippedWeaponName(weaponDefinition, equippedWeaponName);
    }


    private void UnequipTransferredPlayerWeaponAndRefreshInventories()
    {
        if (!activePlayerWeaponController)
            return;

        ForceActivePlayerOutOfCombatAndWeaponOutOfHand();

        if (!activePlayerWeaponController.TryEquipUnarmed())
            return;

        RefreshInventoryLists();
    }


    private void ForceActivePlayerOutOfCombatAndWeaponOutOfHand()
    {
        if (!activePlayerState && activeInteractor)
            activePlayerState = activeInteractor.GetComponentInParent<PlayerState>(true);

        if (!activePlayerState)
            activePlayerState = FindAnyObjectByType<PlayerState>();

        if (!activePlayerState)
            return;

        activePlayerState.SetCombatMode(false);
        activePlayerState.SetWeaponInHand(false);
    }


    private void TryTransferAllContainerItemsToPlayerAndClose()
    {
        if (!activeContainer || !activePlayerInventory)
            return;

        TransferAllContainerItemsToPlayer();
        lastInteractCloseUnscaledTime = Time.unscaledTime;
        Close();
    }


    private void TransferAllContainerItemsToPlayer()
    {
        for (int categoryIndex = 0; categoryIndex < InventoryCategories.Length; categoryIndex++)
        {
            IReadOnlyList<PlayerInventory.InventoryEntry> categoryEntries =
                activeContainer.GetCategoryItems(InventoryCategories[categoryIndex]);
            if (categoryEntries == null || categoryEntries.Count == 0)
                continue;

            List<PlayerInventory.InventoryEntry> categoryEntriesSnapshot =
                new List<PlayerInventory.InventoryEntry>(categoryEntries);

            for (int entryIndex = 0; entryIndex < categoryEntriesSnapshot.Count; entryIndex++)
            {
                PlayerInventory.InventoryEntry sourceEntry = categoryEntriesSnapshot[entryIndex];
                if (sourceEntry == null)
                    continue;

                int quantityToTransfer = sourceEntry.GetQuantity();
                if (quantityToTransfer <= 0)
                    continue;

                activeContainer.TryTransferToPlayer(activePlayerInventory, sourceEntry, quantityToTransfer);
            }
        }
    }


    private bool TryFindFirstPlayerInventoryEntryForDefinition(
        ScriptableObject itemDefinition,
        PlayerInventoryListCategory selectedCategory,
        out PlayerInventory.InventoryEntry matchingEntry)
    {
        matchingEntry = null;
        if (!activePlayerInventory || !itemDefinition)
            return false;

        if (selectedCategory == PlayerInventoryListCategory.Items)
        {
            for (int categoryIndex = 0; categoryIndex < InventoryCategories.Length; categoryIndex++)
            {
                IReadOnlyList<PlayerInventory.InventoryEntry> entries =
                    activePlayerInventory.GetCategoryItems(InventoryCategories[categoryIndex]);
                if (TryFindFirstInventoryEntryByDefinition(entries, itemDefinition, out matchingEntry))
                    return true;
            }

            return false;
        }

        PlayerInventory.InventoryCategory category = ConvertPlayerInventoryListCategory(selectedCategory);
        return TryFindFirstInventoryEntryByDefinition(activePlayerInventory.GetCategoryItems(category), itemDefinition, out matchingEntry);
    }


    private bool TryFindFirstContainerInventoryEntryForDefinition(
        ScriptableObject itemDefinition,
        PlayerInventoryListCategory selectedCategory,
        out PlayerInventory.InventoryEntry matchingEntry)
    {
        matchingEntry = null;
        if (!activeContainer || !itemDefinition)
            return false;

        if (selectedCategory == PlayerInventoryListCategory.Items)
        {
            for (int categoryIndex = 0; categoryIndex < InventoryCategories.Length; categoryIndex++)
            {
                IReadOnlyList<PlayerInventory.InventoryEntry> entries =
                    activeContainer.GetCategoryItems(InventoryCategories[categoryIndex]);
                if (TryFindFirstInventoryEntryByDefinition(entries, itemDefinition, out matchingEntry))
                    return true;
            }

            return false;
        }

        PlayerInventory.InventoryCategory category = ConvertPlayerInventoryListCategory(selectedCategory);
        return TryFindFirstInventoryEntryByDefinition(activeContainer.GetCategoryItems(category), itemDefinition, out matchingEntry);
    }


    private static bool TryFindFirstInventoryEntryByDefinition(
        IReadOnlyList<PlayerInventory.InventoryEntry> entries,
        ScriptableObject itemDefinition,
        out PlayerInventory.InventoryEntry matchingEntry)
    {
        matchingEntry = null;
        if (entries == null || !itemDefinition)
            return false;

        for (int i = 0; i < entries.Count; i++)
        {
            PlayerInventory.InventoryEntry candidate = entries[i];
            if (candidate == null || candidate.GetItemDefinition() != itemDefinition)
                continue;

            if (candidate.GetQuantity() <= 0)
                continue;

            matchingEntry = candidate;
            return true;
        }

        return false;
    }


    private void RefreshHoveredInventoryListHighlights()
    {
        if (hoveredPlayerListEntryObject && hoveredPlayerListEntryObject.activeInHierarchy)
            UpdateHoveredListButtonHighlight(hoveredPlayerListEntryObject, playerListEntryHighlight, true);
        else
            ClearPlayerListHoveredEntryHighlight();

        if (hoveredContainerListEntryObject && hoveredContainerListEntryObject.activeInHierarchy)
            UpdateHoveredListButtonHighlight(hoveredContainerListEntryObject, containerListEntryHighlight, false);
        else
            ClearContainerListHoveredEntryHighlight();

        RefreshHoveredItemStatsFromCurrentHoveredRow();
    }


    private void ValidateHoveredListEntryReference(bool isPlayerList, List<GameObject> activePool)
    {
        if (activePool == null)
            return;

        if (isPlayerList)
        {
            if (hoveredPlayerListEntryObject == null)
                return;

            if (!activePool.Contains(hoveredPlayerListEntryObject))
            {
                ClearPlayerListHoveredEntryHighlight();
                return;
            }

            return;
        }

        if (hoveredContainerListEntryObject == null)
            return;

        if (!activePool.Contains(hoveredContainerListEntryObject))
            ClearContainerListHoveredEntryHighlight();
    }


    private void ClearPlayerListHoveredEntryHighlight()
    {
        ClearHoveredItemStatsIfOwnedByRow(hoveredPlayerListEntryObject, true);
        hoveredPlayerListEntryObject = null;
        SetButtonHighlight(playerListEntryHighlight, false, false);
    }


    private void ClearContainerListHoveredEntryHighlight()
    {
        ClearHoveredItemStatsIfOwnedByRow(hoveredContainerListEntryObject, false);
        hoveredContainerListEntryObject = null;
        SetButtonHighlight(containerListEntryHighlight, false, false);
    }


    private void UpdateHoveredListButtonHighlight(
        GameObject rowObject,
        ButtonHighlight highlight,
        bool isPlayerList)
    {
        if (!rowObject)
        {
            SetButtonHighlight(highlight, false, false);
            return;
        }

        RectTransform rowRectTransform = rowObject.GetComponent<RectTransform>();
        if (!rowRectTransform || !rowRectTransform.parent)
        {
            SetButtonHighlight(highlight, false, false);
            return;
        }

        RectTransform listParentRect = rowRectTransform.parent as RectTransform;
        if (!listParentRect)
        {
            SetButtonHighlight(highlight, false, false);
            return;
        }

        TMP_Text rowLabel = rowObject.GetComponentInChildren<TMP_Text>(true);
        RectTransform rowLabelRectTransform = rowLabel ? rowLabel.rectTransform : null;

        float horizontalRangeMinX = 0.0f;
        float horizontalRangeMaxX = 0.0f;
        bool hasCustomHorizontalRange = isPlayerList
            ? TryGetPlayerHighlightHorizontalRangeLocalX(listParentRect, out horizontalRangeMinX, out horizontalRangeMaxX)
            : TryGetContainerHighlightHorizontalRangeLocalX(listParentRect, out horizontalRangeMinX, out horizontalRangeMaxX);

        if (!hasCustomHorizontalRange)
        {
            if (TryGetRectLocalBounds(
                    rowRectTransform,
                    listParentRect,
                    out float rowMinX,
                    out float rowMaxX,
                    out _,
                    out _))
            {
                horizontalRangeMinX = rowMinX;
                horizontalRangeMaxX = rowMaxX;
                hasCustomHorizontalRange = true;
            }
        }

        EnsureHighlightOutlineSegments(highlight.outline);
        bool hasOutline = PositionHoveredListHighlightElement(
            highlight.outline,
            rowRectTransform,
            rowLabelRectTransform,
            listParentRect,
            hasCustomHorizontalRange,
            horizontalRangeMinX,
            horizontalRangeMaxX,
            0.0f,
            5.0f,
            -1.0f);

        bool hasBackground = PositionHoveredListHighlightElement(
            highlight.background,
            rowRectTransform,
            rowLabelRectTransform,
            listParentRect,
            hasCustomHorizontalRange,
            horizontalRangeMinX,
            horizontalRangeMaxX,
            1.0f,
            6.0f,
            -2.0f);

        SetHoveredEntryHighlightSiblingOrder(rowRectTransform, highlight, hasOutline, hasBackground);
        SetButtonHighlight(highlight, hasOutline, hasBackground);
    }


    private bool TryGetPlayerHighlightHorizontalRangeLocalX(
        RectTransform localSpaceRect,
        out float minX,
        out float maxX)
    {
        minX = 0.0f;
        maxX = 0.0f;

        if (!localSpaceRect)
            return false;

        if (!playerHighlightMidLine1)
            return false;

        if (!TryGetScrollRectScrollbarEdgesLocalX(playerScroll, localSpaceRect, out _, out float playerScrollbarRightX))
            return false;

        if (!TryGetRectLocalBounds(playerHighlightMidLine1, localSpaceRect, out _, out float midLineRightX, out _, out _))
            return false;

        minX = Mathf.Min(playerScrollbarRightX, midLineRightX);
        maxX = Mathf.Max(playerScrollbarRightX, midLineRightX);
        return true;
    }


    private bool TryGetContainerHighlightHorizontalRangeLocalX(
        RectTransform localSpaceRect,
        out float minX,
        out float maxX)
    {
        minX = 0.0f;
        maxX = 0.0f;

        if (!localSpaceRect)
            return false;

        if (!containerHighlightMidLine2)
            return false;

        if (!TryGetScrollRectScrollbarEdgesLocalX(containerScroll, localSpaceRect, out float containerScrollbarLeftX, out _))
            return false;

        if (!TryGetRectLocalBounds(containerHighlightMidLine2, localSpaceRect, out float midLineLeftX, out _, out _, out _))
            return false;

        minX = Mathf.Min(containerScrollbarLeftX, midLineLeftX);
        maxX = Mathf.Max(containerScrollbarLeftX, midLineLeftX);
        return true;
    }


    private void RefreshPlayerEquippedSelectedBoxIndicator()
    {
        if (!isOpen || !playerEquippedSelectedBox)
        {
            ClearPlayerEquippedSelectedBoxIndicator();
            return;
        }

        if (!TryGetEquippedPlayerItemDefinition(out ScriptableObject equippedDefinition))
        {
            ClearPlayerEquippedSelectedBoxIndicator();
            return;
        }

        if (!TryFindPlayerListEntryObjectForDefinition(equippedDefinition, out GameObject equippedEntryObject))
        {
            ClearPlayerEquippedSelectedBoxIndicator();
            return;
        }

        if (!PositionPlayerEquippedSelectedBox(equippedEntryObject))
        {
            ClearPlayerEquippedSelectedBoxIndicator();
            return;
        }

        SetActiveSafe(playerEquippedSelectedBox, true);
    }


    private void ClearPlayerEquippedSelectedBoxIndicator()
    {
        SetActiveSafe(playerEquippedSelectedBox, false);
    }


    private bool TryGetEquippedPlayerItemDefinition(out ScriptableObject equippedItemDefinition)
    {
        equippedItemDefinition = null;

        if (!activePlayerInventory || !activePlayerWeaponController)
            return false;

        if (TryResolveEquippedWeaponDefinitionFromInstanceBinding(out WeaponDefinition instanceBoundWeaponDefinition))
        {
            equippedItemDefinition = instanceBoundWeaponDefinition;
            return true;
        }

        if (TryResolveEquippedWeaponDefinitionFromCurrentWeaponName(out WeaponDefinition currentWeaponDefinition))
        {
            equippedItemDefinition = currentWeaponDefinition;
            return true;
        }

        return false;
    }


    private bool TryResolveEquippedWeaponDefinitionFromInstanceBinding(out WeaponDefinition equippedWeaponDefinition)
    {
        equippedWeaponDefinition = null;

        if (!activePlayerInventory || !activePlayerWeaponController)
            return false;

        string equippedInstanceId = activePlayerWeaponController.GetEquippedInventoryWeaponInstanceId();
        if (string.IsNullOrWhiteSpace(equippedInstanceId))
            return false;

        IReadOnlyList<PlayerInventory.InventoryEntry> weaponEntries =
            activePlayerInventory.GetCategoryItems(PlayerInventory.InventoryCategory.Weapons);
        if (weaponEntries == null)
            return false;

        for (int entryIndex = 0; entryIndex < weaponEntries.Count; entryIndex++)
        {
            PlayerInventory.InventoryEntry entry = weaponEntries[entryIndex];
            if (entry == null || !(entry.GetItemDefinition() is WeaponDefinition weaponDefinition))
                continue;

            IReadOnlyList<PlayerInventory.ItemInstanceData> itemInstances = entry.GetItemInstances();
            if (itemInstances == null)
                continue;

            for (int instanceIndex = 0; instanceIndex < itemInstances.Count; instanceIndex++)
            {
                PlayerInventory.ItemInstanceData itemInstance = itemInstances[instanceIndex];
                if (itemInstance == null)
                    continue;

                if (!string.Equals(itemInstance.GetInstanceId(), equippedInstanceId, System.StringComparison.Ordinal))
                    continue;

                equippedWeaponDefinition = weaponDefinition;
                return true;
            }
        }

        return false;
    }


    private bool TryResolveEquippedWeaponDefinitionFromCurrentWeaponName(out WeaponDefinition equippedWeaponDefinition)
    {
        equippedWeaponDefinition = null;

        if (!activePlayerInventory || !activePlayerWeaponController)
            return false;

        PlayerWeaponController.WeaponEntry currentWeaponEntry = activePlayerWeaponController.GetCurrentWeapon();
        if (currentWeaponEntry == null)
            return false;

        string equippedWeaponName = currentWeaponEntry.WeaponName ?? string.Empty;
        if (string.IsNullOrWhiteSpace(equippedWeaponName))
            return false;

        IReadOnlyList<PlayerInventory.InventoryEntry> weaponEntries =
            activePlayerInventory.GetCategoryItems(PlayerInventory.InventoryCategory.Weapons);
        if (weaponEntries == null)
            return false;

        for (int entryIndex = 0; entryIndex < weaponEntries.Count; entryIndex++)
        {
            PlayerInventory.InventoryEntry entry = weaponEntries[entryIndex];
            if (entry == null || !(entry.GetItemDefinition() is WeaponDefinition weaponDefinition))
                continue;

            if (!DoesWeaponDefinitionMatchEquippedWeaponName(weaponDefinition, equippedWeaponName))
                continue;

            equippedWeaponDefinition = weaponDefinition;
            return true;
        }

        return false;
    }


    private bool TryFindPlayerListEntryObjectForDefinition(
        ScriptableObject itemDefinition,
        out GameObject rowObject)
    {
        rowObject = null;
        if (!itemDefinition)
            return false;

        for (int i = 0; i < spawnedPlayerListEntries.Count; i++)
        {
            GameObject candidate = spawnedPlayerListEntries[i];
            if (!candidate || !candidate.activeInHierarchy)
                continue;

            if (!playerListDefinitionByEntryObject.TryGetValue(candidate, out ScriptableObject candidateDefinition) ||
                !candidateDefinition)
            {
                continue;
            }

            if (candidateDefinition != itemDefinition)
                continue;

            rowObject = candidate;
            return true;
        }

        return false;
    }


    private bool PositionPlayerEquippedSelectedBox(GameObject rowObject)
    {
        if (!playerEquippedSelectedBox || !rowObject)
            return false;

        RectTransform rowRectTransform = rowObject.GetComponent<RectTransform>();
        if (!rowRectTransform || !(rowRectTransform.parent is RectTransform listParentRect))
            return false;

        if (!IsPlayerListEntryVisibleInViewport(rowRectTransform, listParentRect))
            return false;

        TMP_Text rowLabel = rowObject.GetComponentInChildren<TMP_Text>(true);
        RectTransform rowLabelRectTransform = rowLabel ? rowLabel.rectTransform : null;

        if (!TryGetPlayerSelectionIndicatorWorldPosition(
                rowRectTransform,
                rowLabelRectTransform,
                listParentRect,
                out Vector3 indicatorWorldPosition))
        {
            return false;
        }

        RectTransform selectedBoxRectTransform = playerEquippedSelectedBox.transform as RectTransform;
        if (!selectedBoxRectTransform)
            return false;

        if (selectedBoxRectTransform.parent != listParentRect)
            selectedBoxRectTransform.SetParent(listParentRect, true);

        selectedBoxRectTransform.position = indicatorWorldPosition;
        SetRectTransformBeforeSibling(selectedBoxRectTransform, rowRectTransform);
        return true;
    }


    private bool IsPlayerListEntryVisibleInViewport(RectTransform rowRectTransform, RectTransform listParentRect)
    {
        if (!rowRectTransform || !listParentRect || !playerScroll)
            return true;

        RectTransform viewportRectTransform = playerScroll.viewport
            ? playerScroll.viewport
            : playerScroll.transform as RectTransform;
        if (!viewportRectTransform)
            return true;

        if (!TryGetRectLocalBounds(
                rowRectTransform,
                listParentRect,
                out float rowMinX,
                out float rowMaxX,
                out float rowMinY,
                out float rowMaxY))
        {
            return false;
        }

        if (!TryGetRectLocalBounds(
                viewportRectTransform,
                listParentRect,
                out float viewportMinX,
                out float viewportMaxX,
                out float viewportMinY,
                out float viewportMaxY))
        {
            return true;
        }

        const float edgeEpsilon = 0.5f;
        bool insideHorizontally = rowMinX >= viewportMinX - edgeEpsilon && rowMaxX <= viewportMaxX + edgeEpsilon;
        bool insideVertically = rowMinY >= viewportMinY - edgeEpsilon && rowMaxY <= viewportMaxY + edgeEpsilon;
        return insideHorizontally && insideVertically;
    }


    private bool TryGetPlayerSelectionIndicatorWorldPosition(
        RectTransform entryRectTransform,
        RectTransform entryLabelRectTransform,
        RectTransform listParentRect,
        out Vector3 indicatorWorldPosition)
    {
        indicatorWorldPosition = Vector3.zero;
        if (!entryRectTransform || !listParentRect)
            return false;

        if (!TryGetRectLocalBounds(
                entryRectTransform,
                listParentRect,
                out float entryMinX,
                out _,
                out float entryMinY,
                out float entryMaxY))
        {
            return false;
        }

        float textStartLocalX = entryMinX;
        float indicatorLocalY = (entryMinY + entryMaxY) * 0.5f;

        if (entryLabelRectTransform &&
            TryGetRectLocalBounds(
                entryLabelRectTransform,
                listParentRect,
                out float labelMinX,
                out _,
                out float labelMinY,
                out float labelMaxY))
        {
            textStartLocalX = labelMinX;
            indicatorLocalY = (labelMinY + labelMaxY) * 0.5f;
        }

        if (!TryGetScrollRectScrollbarEdgesLocalX(playerScroll, listParentRect, out _, out float scrollbarRightEdgeLocalX))
            return false;

        float indicatorLocalX = (scrollbarRightEdgeLocalX + textStartLocalX) * 0.5f;
        indicatorWorldPosition = listParentRect.TransformPoint(new Vector3(indicatorLocalX, indicatorLocalY, 0.0f));
        return true;
    }


    private static bool DoesWeaponDefinitionMatchEquippedWeaponName(WeaponDefinition weaponDefinition, string equippedWeaponName)
    {
        if (!weaponDefinition || string.IsNullOrWhiteSpace(equippedWeaponName))
            return false;

        if (string.Equals(weaponDefinition.GetDisplayName(), equippedWeaponName, System.StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(weaponDefinition.GetItemId(), equippedWeaponName, System.StringComparison.OrdinalIgnoreCase))
            return true;

        if (string.Equals(weaponDefinition.name, equippedWeaponName, System.StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }


    private Transform ResolveContainerUiRoot()
    {
        if (containerCanvasGroup && containerCanvasGroup.transform)
            return containerCanvasGroup.transform;

        return transform;
    }


    private void AutoWireHoveredConditionBarFillImages()
    {
        resolvedHoveredCndBarFillImages.Clear();
        AddHoveredConditionBarFillImage(cndBarFillImage);

        Transform searchRoot = cndBarBackground ? cndBarBackground.transform : ResolveContainerUiRoot();
        if (searchRoot)
        {
            Image[] candidateImages = searchRoot.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < candidateImages.Length; i++)
            {
                Image candidateImage = candidateImages[i];
                if (!candidateImage || candidateImage.name != "CNDBarFill")
                    continue;

                AddHoveredConditionBarFillImage(candidateImage);
            }
        }

        if (resolvedHoveredCndBarFillImages.Count == 0)
            AddHoveredConditionBarFillImage(FindChildComponentByNameInRoot<Image>("CNDBarFill", ResolveContainerUiRoot()));

        for (int i = 0; i < resolvedHoveredCndBarFillImages.Count; i++)
            ConfigureConditionBarFillImage(resolvedHoveredCndBarFillImages[i]);
    }


    private void AddHoveredConditionBarFillImage(Image imageComponent)
    {
        if (!imageComponent)
            return;

        if (resolvedHoveredCndBarFillImages.Contains(imageComponent))
            return;

        resolvedHoveredCndBarFillImages.Add(imageComponent);
    }


    private void SetHoveredConditionBarFillAmount(float fillAmount)
    {
        if (resolvedHoveredCndBarFillImages.Count == 0)
            AutoWireHoveredConditionBarFillImages();

        float clampedFillAmount = Mathf.Clamp01(fillAmount);
        for (int i = 0; i < resolvedHoveredCndBarFillImages.Count; i++)
            SetImageFillAmountIfChanged(resolvedHoveredCndBarFillImages[i], clampedFillAmount);
    }


    private static void SetImageFillAmountIfChanged(Image imageComponent, float fillAmount)
    {
        if (!imageComponent)
            return;

        float clampedFillAmount = Mathf.Clamp01(fillAmount);
        if (Mathf.Abs(imageComponent.fillAmount - clampedFillAmount) < 0.0001f)
            return;

        imageComponent.fillAmount = clampedFillAmount;
    }


    private static void ConfigureConditionBarFillImage(Image imageComponent)
    {
        if (!imageComponent)
            return;

        imageComponent.raycastTarget = false;
        if (!imageComponent.sprite)
            imageComponent.sprite = GetFallbackConditionBarFillSprite();

        imageComponent.type = Image.Type.Filled;
        imageComponent.fillMethod = Image.FillMethod.Horizontal;
        imageComponent.fillOrigin = (int)Image.OriginHorizontal.Left;
        imageComponent.fillClockwise = true;
        imageComponent.fillAmount = Mathf.Clamp01(imageComponent.fillAmount);
    }


    private static Sprite GetFallbackConditionBarFillSprite()
    {
        if (fallbackConditionBarFillSprite)
            return fallbackConditionBarFillSprite;

        Texture2D whiteTexture = Texture2D.whiteTexture;
        Rect spriteRect = new Rect(0.0f, 0.0f, whiteTexture.width, whiteTexture.height);
        Vector2 spritePivot = new Vector2(0.5f, 0.5f);
        fallbackConditionBarFillSprite = Sprite.Create(whiteTexture, spriteRect, spritePivot, 100.0f);
        return fallbackConditionBarFillSprite;
    }


    private void DisableHoveredItemStatObjectRaycasts()
    {
        GameObject[] hoveredStatObjects =
        {
            itemLine1,
            itemLine2,
            itemLine3,
            itemLine4,
            itemLine5,
            hoveredDamDrLabelText ? hoveredDamDrLabelText.gameObject : null,
            hoveredDamDrItemText ? hoveredDamDrItemText.gameObject : null,
            hoveredCndLabelText ? hoveredCndLabelText.gameObject : null,
            hoveredWgLabelText ? hoveredWgLabelText.gameObject : null,
            hoveredValLabelText ? hoveredValLabelText.gameObject : null,
            hoveredWgItemText ? hoveredWgItemText.gameObject : null,
            hoveredValItemText ? hoveredValItemText.gameObject : null,
            hoveredItemInfoText ? hoveredItemInfoText.gameObject : null,
            cndBarBackground,
            cndBarFillImage ? cndBarFillImage.gameObject : null
        };

        for (int i = 0; i < hoveredStatObjects.Length; i++)
            DisableGraphicRaycasts(hoveredStatObjects[i]);
    }


    private static float GetDefinitionWeightOrDefault(ScriptableObject definition)
    {
        if (!definition)
            return 0.0f;

        if (definition is WeaponDefinition weaponDefinition) return weaponDefinition.GetWeight();
        if (definition is ApparelDefinition apparelDefinition) return apparelDefinition.GetWeight();
        if (definition is AidDefinition aidDefinition) return aidDefinition.GetWeight();
        if (definition is MiscDefinition miscDefinition) return miscDefinition.GetWeight();
        if (definition is AmmoDefinition ammoDefinition) return ammoDefinition.GetWeight();
        if (definition is AmmoItemDefinition ammoItemDefinition) return ammoItemDefinition.GetWeight();
        return 0.0f;
    }


    private static float GetDefinitionValueOrDefault(ScriptableObject definition)
    {
        if (!definition)
            return 0.0f;

        if (definition is WeaponDefinition weaponDefinition) return weaponDefinition.GetBaseValue();
        if (definition is ApparelDefinition apparelDefinition) return apparelDefinition.GetBaseValue();
        if (definition is AidDefinition aidDefinition) return aidDefinition.GetValue();
        if (definition is MiscDefinition miscDefinition) return miscDefinition.GetValue();
        if (definition is AmmoDefinition ammoDefinition) return ammoDefinition.GetValue();
        if (definition is AmmoItemDefinition ammoItemDefinition) return ammoItemDefinition.GetValue();
        return 0.0f;
    }


    private static string FormatValue(float value)
    {
        return Mathf.Max(0.0f, value).ToString("0.##");
    }


    private static string BuildAidEffectsDisplayText(AidDefinition aidDefinition)
    {
        if (!aidDefinition)
            return string.Empty;

        List<AidEffectDefinition> effects = aidDefinition.GetEffects();
        if (effects == null || effects.Count == 0)
            return string.Empty;

        System.Text.StringBuilder builder = new System.Text.StringBuilder();
        for (int i = 0; i < effects.Count; i++)
        {
            AidEffectDefinition effect = effects[i];
            if (effect == null)
                continue;

            string effectText = FormatAidEffectDisplayText(effect);
            if (string.IsNullOrWhiteSpace(effectText))
                continue;

            if (builder.Length > 0)
                builder.Append(", ");

            builder.Append(effectText);
        }

        return builder.ToString();
    }


    private static string FormatAidEffectDisplayText(AidEffectDefinition effect)
    {
        if (effect == null)
            return string.Empty;

        string targetText = GetAidEffectTargetDisplayName(effect.GetTarget(), effect.ModifiesMaximumValue());
        float magnitude = effect.GetMagnitude();

        if (Mathf.Approximately(magnitude, 0.0f))
            return targetText;

        bool isPercent = effect.GetOperation() == AidEffectOperation.AddPercent;
        string signedMagnitude = FormatSignedMagnitude(magnitude);
        if (isPercent)
            signedMagnitude = $"{signedMagnitude}%";

        return $"{targetText} {signedMagnitude}";
    }


    private static string FormatSignedMagnitude(float magnitude)
    {
        float absoluteMagnitude = Mathf.Abs(magnitude);
        bool isWholeNumber = Mathf.Approximately(absoluteMagnitude, Mathf.Round(absoluteMagnitude));
        string numberText = isWholeNumber
            ? Mathf.RoundToInt(absoluteMagnitude).ToString()
            : absoluteMagnitude.ToString("0.##");

        string sign = magnitude >= 0.0f ? "+" : "-";
        return $"{sign}{numberText}";
    }


    private static string GetAidEffectTargetDisplayName(AidEffectTarget target, bool modifiesMaximumValue)
    {
        string baseName = target switch
        {
            AidEffectTarget.Health => "Health",
            AidEffectTarget.Radiation => "Radiation",
            AidEffectTarget.ActionPoints => "Action Points",
            AidEffectTarget.MaxActionPoints => "Max Action Points",
            AidEffectTarget.Strength => "Strength",
            AidEffectTarget.Perception => "Perception",
            AidEffectTarget.Endurance => "Endurance",
            AidEffectTarget.Charisma => "Charisma",
            AidEffectTarget.Intelligence => "Intelligence",
            AidEffectTarget.Agility => "Agility",
            AidEffectTarget.Luck => "Luck",
            AidEffectTarget.SneakSkill => "Sneak",
            AidEffectTarget.DamagePercent => "Damage",
            AidEffectTarget.DamageResistance => "Damage Resistance",
            AidEffectTarget.RadiationResistance => "Radiation Resistance",
            AidEffectTarget.FireResistance => "Fire Resistance",
            AidEffectTarget.BottleCaps => "Caps",
            AidEffectTarget.EquippedWeaponCondition => "Weapon Condition",
            AidEffectTarget.StealthField => "Stealth Field",
            AidEffectTarget.RandomEffectBundle => "Random Effect",
            _ => target.ToString()
        };

        if (!modifiesMaximumValue)
            return baseName;

        if (target == AidEffectTarget.Health)
            return "Max Health";

        if (target == AidEffectTarget.ActionPoints)
            return "Max Action Points";

        return $"Max {baseName}";
    }


    private static bool PositionHoveredListHighlightElement(
        GameObject highlightObject,
        RectTransform targetEntryRect,
        RectTransform targetLabelRect,
        RectTransform targetParentRect,
        bool useHorizontalRangeOverride,
        float horizontalRangeMinX,
        float horizontalRangeMaxX,
        float leftInsetPixels,
        float rightInsetPixels,
        float verticalPaddingPixels)
    {
        if (!highlightObject || !targetEntryRect || !targetParentRect)
            return false;

        RectTransform highlightRect = highlightObject.GetComponent<RectTransform>();
        if (!highlightRect)
            return false;

        EnsureLayoutIgnored(highlightObject);

        if (highlightRect.parent != targetParentRect)
            highlightRect.SetParent(targetParentRect, false);

        if (!TryGetRectLocalBounds(
                targetEntryRect,
                targetParentRect,
                out float entryMinX,
                out float entryMaxX,
                out _,
                out _))
        {
            return false;
        }

        float labelMinY = 0.0f;
        float labelMaxY = 0.0f;
        bool hasLabelBounds = targetLabelRect &&
                              TryGetRectLocalBounds(targetLabelRect, targetParentRect, out _, out _, out labelMinY, out labelMaxY);

        float baseMinY = hasLabelBounds ? labelMinY : 0.0f;
        float baseMaxY = hasLabelBounds ? labelMaxY : 0.0f;
        if (!hasLabelBounds &&
            !TryGetRectLocalBounds(targetEntryRect, targetParentRect, out _, out _, out baseMinY, out baseMaxY))
        {
            return false;
        }

        float rangeMinX = entryMinX;
        float rangeMaxX = entryMaxX;

        if (useHorizontalRangeOverride)
        {
            rangeMinX = Mathf.Min(horizontalRangeMinX, horizontalRangeMaxX);
            rangeMaxX = Mathf.Max(horizontalRangeMinX, horizontalRangeMaxX);
        }

        float highlightMaxX = rangeMaxX - rightInsetPixels;
        float highlightMinX = rangeMinX + leftInsetPixels;

        if (highlightMaxX <= highlightMinX)
            return false;

        float expandedMinY = baseMinY - verticalPaddingPixels;
        float expandedMaxY = baseMaxY + verticalPaddingPixels;

        float highlightHeight = expandedMaxY - expandedMinY;
        if (highlightHeight <= 0.001f)
            return false;

        Rect parentRect = targetParentRect.rect;
        float parentLeftEdgeX = -parentRect.width * targetParentRect.pivot.x;
        float parentTopEdgeY = parentRect.height * (1.0f - targetParentRect.pivot.y);
        float leftInset = highlightMinX - parentLeftEdgeX;
        float topInset = parentTopEdgeY - expandedMaxY;
        float highlightWidth = highlightMaxX - highlightMinX;

        highlightRect.anchorMin = new Vector2(0.0f, 1.0f);
        highlightRect.anchorMax = new Vector2(0.0f, 1.0f);
        highlightRect.pivot = new Vector2(0.0f, 1.0f);
        highlightRect.anchoredPosition = Vector2.zero;
        highlightRect.localScale = Vector3.one;
        highlightRect.localRotation = Quaternion.identity;
        highlightRect.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Left, leftInset, highlightWidth);
        highlightRect.SetInsetAndSizeFromParentEdge(RectTransform.Edge.Top, topInset, highlightHeight);

        return true;
    }


    private static void SetHoveredEntryHighlightSiblingOrder(
        RectTransform entryRectTransform,
        ButtonHighlight highlight,
        bool hasOutline,
        bool hasBackground)
    {
        if (!entryRectTransform)
            return;

        RectTransform backgroundRectTransform = hasBackground
            ? highlight.background ? highlight.background.GetComponent<RectTransform>() : null
            : null;
        RectTransform outlineRectTransform = hasOutline
            ? highlight.outline ? highlight.outline.GetComponent<RectTransform>() : null
            : null;

        if (outlineRectTransform)
            SetRectTransformBeforeSibling(outlineRectTransform, entryRectTransform);

        if (backgroundRectTransform)
            SetRectTransformBeforeSibling(backgroundRectTransform, entryRectTransform);
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


    private static bool TryGetScrollRectScrollbarEdgesLocalX(
        ScrollRect scrollRect,
        RectTransform localSpaceRect,
        out float leftEdgeLocalX,
        out float rightEdgeLocalX)
    {
        leftEdgeLocalX = 0.0f;
        rightEdgeLocalX = 0.0f;
        if (!localSpaceRect || !scrollRect)
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
        Vector3 leftEdgeLocalPoint = localSpaceRect.InverseTransformPoint(leftEdgeWorldPoint);
        Vector3 rightEdgeLocalPoint = localSpaceRect.InverseTransformPoint(rightEdgeWorldPoint);
        leftEdgeLocalX = leftEdgeLocalPoint.x;
        rightEdgeLocalX = rightEdgeLocalPoint.x;
        return true;
    }


    private static bool TryGetRectLocalBounds(
        RectTransform sourceRect,
        RectTransform relativeToRect,
        out float minX,
        out float maxX,
        out float minY,
        out float maxY)
    {
        minX = 0.0f;
        maxX = 0.0f;
        minY = 0.0f;
        maxY = 0.0f;

        if (!sourceRect || !relativeToRect)
            return false;

        Vector3[] worldCorners = new Vector3[4];
        sourceRect.GetWorldCorners(worldCorners);

        Vector3 localBottomLeft = relativeToRect.InverseTransformPoint(worldCorners[0]);
        Vector3 localTopLeft = relativeToRect.InverseTransformPoint(worldCorners[1]);
        Vector3 localTopRight = relativeToRect.InverseTransformPoint(worldCorners[2]);
        Vector3 localBottomRight = relativeToRect.InverseTransformPoint(worldCorners[3]);

        minX = Mathf.Min(localBottomLeft.x, localTopLeft.x, localTopRight.x, localBottomRight.x);
        maxX = Mathf.Max(localBottomLeft.x, localTopLeft.x, localTopRight.x, localBottomRight.x);
        minY = Mathf.Min(localBottomLeft.y, localTopLeft.y, localTopRight.y, localBottomRight.y);
        maxY = Mathf.Max(localBottomLeft.y, localTopLeft.y, localTopRight.y, localBottomRight.y);
        if (!IsFiniteFloat(minX) || !IsFiniteFloat(maxX) || !IsFiniteFloat(minY) || !IsFiniteFloat(maxY))
            return false;

        return true;
    }


    private void ForwardScrollToScrollRect(BaseEventData eventData, ScrollRect scrollRect)
    {
        if (!scrollRect || eventData == null)
            return;

        if (!(eventData is PointerEventData pointerEventData))
            return;

        ScrollRect gestureOwner = ResolveScrollGestureOwner(scrollRect);
        if (gestureOwner)
            ApplyScrollToGestureOwner(pointerEventData, gestureOwner);

        pointerEventData.Use();
    }


    private ScrollRect ResolveScrollGestureOwner(ScrollRect requestedScrollRect)
    {
        if (!requestedScrollRect)
            return null;

        float now = Time.unscaledTime;
        bool ownerExpired = now - activeScrollGestureLastUnscaledTime > ScrollGestureOwnerReleaseDelaySeconds;
        if (ownerExpired || !activeScrollGestureOwner || !activeScrollGestureOwner.gameObject.activeInHierarchy)
            activeScrollGestureOwner = requestedScrollRect;

        activeScrollGestureLastUnscaledTime = now;
        return activeScrollGestureOwner ? activeScrollGestureOwner : requestedScrollRect;
    }


    private void ClearActiveScrollGestureOwner()
    {
        activeScrollGestureOwner = null;
        activeScrollGestureLastUnscaledTime = float.NegativeInfinity;
    }


    private void ConfigureIndependentScrollRects()
    {
        ConfigureIndependentScrollRect(
            playerScroll,
            ref cachedPlayerScrollSensitivity,
            ref hasCachedPlayerScrollSensitivity);
        ConfigureIndependentScrollRect(
            containerScroll,
            ref cachedContainerScrollSensitivity,
            ref hasCachedContainerScrollSensitivity);
    }


    private void ConfigureIndependentScrollRect(
        ScrollRect scrollRect,
        ref float cachedScrollSensitivity,
        ref bool hasCachedScrollSensitivity)
    {
        if (!scrollRect)
            return;

        if (!hasCachedScrollSensitivity)
        {
            cachedScrollSensitivity = Mathf.Max(0.0f, scrollRect.scrollSensitivity);
            if (Mathf.Approximately(cachedScrollSensitivity, 0.0f))
                cachedScrollSensitivity = 1.0f;

            hasCachedScrollSensitivity = true;
        }

        AddScrollForwardingTrigger(scrollRect.gameObject, scrollRect);

        // Native ScrollRect.OnScroll follows the current hovered target. The controller re-applies
        // the cached sensitivity after choosing the active gesture owner.
        scrollRect.scrollSensitivity = 0.0f;
    }


    private void AddScrollForwardingTrigger(GameObject targetObject, ScrollRect ownerScrollRect)
    {
        if (!targetObject || !ownerScrollRect)
            return;

        EventTrigger eventTrigger = targetObject.GetComponent<EventTrigger>();
        if (!eventTrigger)
            eventTrigger = targetObject.AddComponent<EventTrigger>();

        if (eventTrigger.triggers == null)
            eventTrigger.triggers = new List<EventTrigger.Entry>();

        EventTrigger.Entry scrollEntry = new EventTrigger.Entry
        {
            eventID = EventTriggerType.Scroll
        };
        scrollEntry.callback.AddListener(eventData => ForwardScrollToScrollRect(eventData, ownerScrollRect));
        eventTrigger.triggers.Add(scrollEntry);
    }


    private void ApplyScrollToGestureOwner(PointerEventData pointerEventData, ScrollRect scrollRect)
    {
        if (pointerEventData == null || !scrollRect)
            return;

        float cachedScrollSensitivity = ResolveCachedScrollSensitivity(scrollRect);
        float previousScrollSensitivity = scrollRect.scrollSensitivity;
        scrollRect.scrollSensitivity = cachedScrollSensitivity;
        scrollRect.OnScroll(pointerEventData);
        scrollRect.scrollSensitivity = previousScrollSensitivity;
    }


    private float ResolveCachedScrollSensitivity(ScrollRect scrollRect)
    {
        if (scrollRect == playerScroll && hasCachedPlayerScrollSensitivity)
            return cachedPlayerScrollSensitivity;

        if (scrollRect == containerScroll && hasCachedContainerScrollSensitivity)
            return cachedContainerScrollSensitivity;

        float scrollSensitivity = scrollRect ? scrollRect.scrollSensitivity : 1.0f;
        return Mathf.Approximately(scrollSensitivity, 0.0f) ? 1.0f : scrollSensitivity;
    }


    private static void EnsureLayoutIgnored(GameObject highlightObject)
    {
        if (!highlightObject)
            return;

        LayoutElement layoutElement = highlightObject.GetComponent<LayoutElement>();
        if (!layoutElement)
            layoutElement = highlightObject.AddComponent<LayoutElement>();

        layoutElement.ignoreLayout = true;
    }


    private static void EnsureButtonHighlightOutlineSegments(ButtonHighlight highlight)
    {
        EnsureHighlightOutlineSegments(highlight.outline);
    }


    private static void EnsureHighlightOutlineSegments(GameObject outlineObject)
    {
        if (!outlineObject)
            return;

        RectTransform outlineRect = outlineObject.GetComponent<RectTransform>();
        if (!outlineRect)
            return;

        Image outlineFillImage = outlineObject.GetComponent<Image>();
        Color outlineColor = outlineFillImage ? outlineFillImage.color : Color.white;
        if (outlineFillImage)
        {
            outlineFillImage.raycastTarget = false;
            outlineFillImage.enabled = false;
        }

        RectTransform topRect = EnsureOutlineSegmentRect(outlineRect, "OutlineTop", outlineColor);
        ConfigureSelectionOutlineSegment(
            topRect,
            new Vector2(0.0f, 1.0f),
            new Vector2(1.0f, 1.0f),
            new Vector2(0.0f, 1.0f),
            Vector2.zero,
            new Vector2(0.0f, SelectionOutlineThickness));

        RectTransform bottomRect = EnsureOutlineSegmentRect(outlineRect, "OutlineBottom", outlineColor);
        ConfigureSelectionOutlineSegment(
            bottomRect,
            new Vector2(0.0f, 0.0f),
            new Vector2(1.0f, 0.0f),
            new Vector2(0.0f, 0.0f),
            Vector2.zero,
            new Vector2(0.0f, SelectionOutlineThickness));

        RectTransform leftRect = EnsureOutlineSegmentRect(outlineRect, "OutlineLeft", outlineColor);
        ConfigureSelectionOutlineSegment(
            leftRect,
            new Vector2(0.0f, 0.0f),
            new Vector2(0.0f, 1.0f),
            new Vector2(0.0f, 1.0f),
            Vector2.zero,
            new Vector2(SelectionOutlineThickness, 0.0f));

        RectTransform rightRect = EnsureOutlineSegmentRect(outlineRect, "OutlineRight", outlineColor);
        ConfigureSelectionOutlineSegment(
            rightRect,
            new Vector2(1.0f, 0.0f),
            new Vector2(1.0f, 1.0f),
            new Vector2(1.0f, 1.0f),
            Vector2.zero,
            new Vector2(SelectionOutlineThickness, 0.0f));
    }


    private static RectTransform EnsureOutlineSegmentRect(RectTransform outlineRect, string segmentName, Color color)
    {
        if (!outlineRect)
            return null;

        RectTransform segmentRect = outlineRect.Find(segmentName) as RectTransform;
        if (!segmentRect)
        {
            GameObject segmentObject = new GameObject(segmentName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            segmentRect = segmentObject.GetComponent<RectTransform>();
            segmentRect.SetParent(outlineRect, false);
        }

        segmentRect.localScale = Vector3.one;
        segmentRect.localRotation = Quaternion.identity;

        Image segmentImage = segmentRect.GetComponent<Image>();
        if (!segmentImage)
            segmentImage = segmentRect.gameObject.AddComponent<Image>();

        segmentImage.color = color;
        segmentImage.raycastTarget = false;
        return segmentRect;
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


    private static void DisableButtonHighlightRaycasts(ButtonHighlight highlight)
    {
        DisableGraphicRaycasts(highlight.outline);
        DisableGraphicRaycasts(highlight.background);
    }


    private static void DisableGraphicRaycasts(GameObject targetObject)
    {
        if (!targetObject)
            return;

        Graphic[] graphics = targetObject.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (!graphics[i])
                continue;

            graphics[i].raycastTarget = false;
        }
    }

    private void ApplyContainerPipBoyPaletteColorOverrides()
    {
        bool appliedToConfiguredRoot = false;

        if (containerCanvasGroup)
        {
            ApplyContainerPipBoyPaletteColorOverrides(containerCanvasGroup.gameObject);
            appliedToConfiguredRoot = true;
        }

        if (!appliedToConfiguredRoot && transform)
            ApplyContainerPipBoyPaletteColorOverrides(gameObject);
    }

    private void ApplyContainerPipBoyPaletteColorOverrides(GameObject rootObject)
    {
        if (!rootObject)
            return;

        Graphic[] graphics = rootObject.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (!graphic)
                continue;

            if (IsWithinPrimaryTabContainer(graphic.transform))
                continue;

            if (graphic.gameObject.name == RuntimeThemeBackgroundExclusionObjectName)
                continue;

            graphic.color = RemapPipBoyPaletteColor(graphic.color);
        }

        Selectable[] selectables = rootObject.GetComponentsInChildren<Selectable>(true);
        for (int i = 0; i < selectables.Length; i++)
        {
            Selectable selectable = selectables[i];
            if (!selectable || IsWithinPrimaryTabContainer(selectable.transform))
                continue;

            if (selectable.gameObject.name == RuntimeThemeBackgroundExclusionObjectName)
                continue;

            ColorBlock colorBlock = selectable.colors;
            colorBlock.normalColor = RemapPipBoyPaletteColor(colorBlock.normalColor);
            colorBlock.highlightedColor = RemapPipBoyPaletteColor(colorBlock.highlightedColor);
            colorBlock.pressedColor = RemapPipBoyPaletteColor(colorBlock.pressedColor);
            colorBlock.selectedColor = RemapPipBoyPaletteColor(colorBlock.selectedColor);
            colorBlock.disabledColor = RemapPipBoyPaletteColor(colorBlock.disabledColor);
            selectable.colors = colorBlock;
        }

        NormalizePanelAndButtonBackgrounds(rootObject);
    }

    private Color RemapPipBoyPaletteColor(Color sourceColor)
    {
        if (IsApproximatelyColor(sourceColor, 1.0f, 1.0f, 1.0f))
            return WithMultipliedAlpha(pipBoyLightColor, sourceColor.a);

        if (IsApproximatelyColor(sourceColor, 0.0f, 0.0f, 0.0f))
            return WithMultipliedAlpha(pipBoyDarkColor, sourceColor.a);

        return sourceColor;
    }

    private static bool IsApproximatelyColor(Color color, float red, float green, float blue)
    {
        return Mathf.Abs(color.r - red) <= PipBoyPaletteColorTolerance &&
               Mathf.Abs(color.g - green) <= PipBoyPaletteColorTolerance &&
               Mathf.Abs(color.b - blue) <= PipBoyPaletteColorTolerance;
    }

    private static bool IsWithinPrimaryTabContainer(Transform targetTransform)
    {
        Transform currentTransform = targetTransform;
        while (currentTransform)
        {
            if (currentTransform.name == PrimaryTabContainerObjectName)
                return true;

            currentTransform = currentTransform.parent;
        }

        return false;
    }

    private void NormalizePanelAndButtonBackgrounds(GameObject rootObject)
    {
        if (!rootObject)
            return;

        Image[] images = rootObject.GetComponentsInChildren<Image>(true);
        Color targetBackgroundColor = pipBoyDarkColor;

        for (int i = 0; i < images.Length; i++)
        {
            Image image = images[i];
            if (!image || IsWithinPrimaryTabContainer(image.transform))
                continue;

            string imageObjectName = image.gameObject.name;
            if (imageObjectName == RuntimeThemeBackgroundExclusionObjectName)
                continue;

            bool isButtonBackground = imageObjectName.Contains("ButtonBackground");
            bool isPanelBackground = !isButtonBackground && imageObjectName.Contains("Panel");
            bool isScrollViewBackground = imageObjectName == "Scroll View" ||
                                          imageObjectName == "Viewport";
            if (!isButtonBackground && !isPanelBackground && !isScrollViewBackground)
                continue;

            image.color = targetBackgroundColor;
            image.sprite = null;
            image.type = Image.Type.Simple;
        }
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        return new Color(color.r, color.g, color.b, alpha);
    }


    private static Color WithMultipliedAlpha(Color color, float alphaMultiplier)
    {
        return new Color(color.r, color.g, color.b, color.a * alphaMultiplier);
    }


    private static void SetButtonHighlight(ButtonHighlight highlight, bool outlineActive, bool backgroundActive)
    {
        SetActiveSafe(highlight.outline, outlineActive);
        SetActiveSafe(highlight.background, backgroundActive);
    }


    private static void SetActiveSafe(GameObject targetObject, bool active)
    {
        if (!targetObject)
            return;

        if (targetObject.activeSelf == active)
            return;

        targetObject.SetActive(active);
    }

    private void SetContainerHierarchyActive(bool active)
    {
        if (!containerCanvasGroup)
            return;

        GameObject containerRoot = containerCanvasGroup.gameObject;
        if (!containerRoot)
            return;

        if (active)
        {
            // Ensure this root and any disabled parents become active.
            Transform current = containerRoot.transform;
            while (current)
            {
                GameObject currentObject = current.gameObject;
                if (!currentObject.activeSelf)
                    currentObject.SetActive(true);

                current = current.parent;
            }

            // Restore top-level UI branches every time open is requested.
            SetDirectChildrenActive(containerRoot.transform, true);

            if (restoreDisabledDescendantsOnFirstOpen && !hasRestoredDisabledDescendants)
            {
                SetDescendantsActive(containerRoot.transform, true);
                hasRestoredDisabledDescendants = true;
            }

            return;
        }

        if (containerRoot.activeSelf)
            containerRoot.SetActive(false);
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

    private static void SetDescendantsActive(Transform rootTransform, bool active)
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

            SetDescendantsActive(child, active);
        }
    }


    private Transform ResolvePlayerListParent()
    {
        if (playerScroll && playerScroll.content)
            return playerScroll.content;

        return playerListContentRoot;
    }


    private Transform ResolveContainerListParent()
    {
        if (containerScroll && containerScroll.content)
            return containerScroll.content;

        return containerListContentRoot;
    }


    private static string GetDisplayNameFromDefinition(ScriptableObject definition, string fallback)
    {
        if (!definition) return fallback;

        if (definition is WeaponDefinition weaponDefinition) return weaponDefinition.GetDisplayName();
        if (definition is ApparelDefinition apparelDefinition) return apparelDefinition.GetDisplayName();
        if (definition is AidDefinition aidDefinition) return aidDefinition.GetDisplayName();
        if (definition is MiscDefinition miscDefinition) return miscDefinition.GetDisplayName();
        if (definition is AmmoDefinition ammoDefinition) return ammoDefinition.GetDisplayName();
        if (definition is AmmoItemDefinition ammoItemDefinition) return ammoItemDefinition.GetDisplayName();
        return fallback;
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


    private T FindChildComponentByNameInRoot<T>(string childName, Transform rootTransform) where T : Component
    {
        if (string.IsNullOrWhiteSpace(childName))
            return null;

        if (!rootTransform)
            return FindChildComponentByName<T>(childName);

        T[] components = rootTransform.GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component != null && component.name == childName)
                return component;
        }

        return null;
    }


    private T FindChildComponentByName<T>(string childName) where T : Component
    {
        T[] components = GetComponentsInChildren<T>(true);

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null && components[i].name == childName)
                return components[i];
        }

        return null;
    }
}
