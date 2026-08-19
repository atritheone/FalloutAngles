// imports
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;



// class
public class PipBoyController : MonoBehaviour
{
    private enum MainTab
    {
        Stats,
        Items,
        Data
    }

    [System.Serializable]
    private struct ButtonHighlight
    {
        public GameObject outline;
        public GameObject background;
    }
    
    // variables
    // The root canvas group that will be shown/hidden.
    [SerializeField] private CanvasGroup pipBoyCanvasGroup;

    [Header("Runtime Theme")]
    [SerializeField] private Color pipBoyLightColor = new Color32(0x55, 0xD3, 0xF2, 0xFF);
    [SerializeField] private Color pipBoyDarkColor = new Color32(0x00, 0x0E, 0x93, 0xFF);

    // Disable the Pip-Boy UI hierarchy in the scene when closed.
    [SerializeField] private bool disableInHierarchyWhenClosed = true;

    // Restore any editor-disabled descendants once on first open.
    [SerializeField] private bool restoreDisabledDescendantsOnFirstOpen = false;

    // Optional background root to toggle with PipBoy visibility.
    [SerializeField] private GameObject pipBoyBackgroundRoot;

    // The first selected UI object when opening the PipBoy.
    [SerializeField] private GameObject firstSelectedUIObject;

    // The PlayerControls that owns the action maps.
    [SerializeField] private PlayerControls playerControls;

    // Camera orbit controller to disable while the PipBoy is open.
    [SerializeField] private CameraRigOrbit cameraRigOrbit;

    // Camera zoom controller to disable while the PipBoy owns scroll input.
    [SerializeField] private CameraControlZoom cameraControlZoom;

    // Cached input actions.
    private InputSystemActions controls;

    // Disable gameplay actions while the PipBoy is open.
    [SerializeField] private bool disableGameplayActionsWhenOpen = true;

    // Pause the game while the PipBoy is open.
    [SerializeField] private bool pauseGameWhenOpen = true;

    // Prevent duplicate toggle callbacks from immediately undoing a state change.
    [SerializeField, Min(0f)] private float toggleDebounceSeconds = 0.15f;

    // Cached input callback for toggling the PipBoy.
    private System.Action<InputAction.CallbackContext> onPipBoyPerformed;
    private System.Action<InputAction.CallbackContext> onDropPerformed;

    // Whether the PipBoy is currently open.
    private bool isOpen;
    private bool hasRestoredDisabledDescendants;
    private bool hasAwakeInitialized;
    private bool hasStartInitialized;
    private bool isRunningAwakeInitialization;
    private MainTab currentMainTab = MainTab.Stats;
    private float nextAllowedToggleUnscaledTime = -1f;

    // Cached time scale so it can be restored after pausing.
    private float cachedTimeScale = 1f;

    // The top-level content root for Stats.
    [Header("Main Content Roots")]
    [SerializeField] private GameObject statsContentRoot;

    // The top-level content root for Items.
    [SerializeField] private GameObject itemsContentRoot;

    // The top-level content root for Data.
    [SerializeField] private GameObject dataContentRoot;

    // Optional higher-level panels that sit behind subtab panels.
    [Header("Main Panels (Behind Subtabs)")]
    [SerializeField] private GameObject statsPanelRoot;

    [SerializeField] private GameObject itemsPanelRoot;

    [SerializeField] private GameObject dataPanelRoot;

    [Header("Main Tab Highlights")]
    [SerializeField] private GameObject statsMainHighlight;

    [SerializeField] private GameObject itemsMainHighlight;

    [SerializeField] private GameObject dataMainHighlight;

    // Subtab button roots.
    [Header("Subtab Button Roots")]
    [SerializeField] private GameObject statsSubtabRoot;

    [SerializeField] private GameObject itemsSubtabRoot;

    [SerializeField] private GameObject dataSubtabRoot;

    // Optional explicit subtab buttons (if you don't have a shared root).
    [Header("Subtab Buttons")]
    [SerializeField] private GameObject[] statsSubtabButtons;

    [SerializeField] private GameObject[] itemsSubtabButtons;

    [SerializeField] private GameObject[] dataSubtabButtons;

    [Header("Stats Subtab Highlights")]
    [SerializeField] private ButtonHighlight statsStatusHighlight;

    [SerializeField] private ButtonHighlight statsSpecialHighlight;

    [SerializeField] private ButtonHighlight statsSkillsHighlight;

    [SerializeField] private ButtonHighlight statsPerksHighlight;

    [SerializeField] private ButtonHighlight statsGeneralHighlight;

    [Header("Items Subtab Highlights")]
    [SerializeField] private ButtonHighlight itemsWeaponsHighlight;

    [SerializeField] private ButtonHighlight itemsApparelHighlight;

    [SerializeField] private ButtonHighlight itemsAidHighlight;

    [SerializeField] private ButtonHighlight itemsMiscHighlight;

    [SerializeField] private ButtonHighlight itemsAmmoHighlight;

    [Header("Data Subtab Highlights")]
    [SerializeField] private ButtonHighlight dataLocalMapHighlight;

    [SerializeField] private ButtonHighlight dataWorldMapHighlight;

    [SerializeField] private ButtonHighlight dataQuestsHighlight;

    [SerializeField] private ButtonHighlight dataNotesHighlight;

    [SerializeField] private ButtonHighlight dataRadioHighlight;

    // Stats sub-panels.
    [Header("Stats Sub Panels")]
    [SerializeField] private GameObject statsStatusPanel;

    [SerializeField] private GameObject statsSpecialPanel;

    [SerializeField] private GameObject statsSkillsPanel;

    [SerializeField] private GameObject statsPerksPanel;

    [SerializeField] private GameObject statsGeneralPanel;

    // Items sub-panels.
    [Header("Items Sub Panels")]
    [SerializeField] private GameObject itemsWeaponsPanel;

    [SerializeField] private GameObject itemsApparelPanel;

    [SerializeField] private GameObject itemsAidPanel;

    [SerializeField] private GameObject itemsMiscPanel;

    [SerializeField] private GameObject itemsAmmoPanel;

    // Data sub-panels.
    [Header("Data Sub Panels")]
    [SerializeField] private GameObject dataLocalMapPanel;

    [SerializeField] private GameObject dataWorldMapPanel;

    [SerializeField] private GameObject dataQuestsPanel;

    [SerializeField] private GameObject dataNotesPanel;

    [SerializeField] private GameObject dataRadioPanel;

    // Cached active subtab panels per main category.
    private GameObject lastStatsPanel;
    private GameObject lastItemsPanel;
    private GameObject lastDataPanel;

    [Header("Weapons List")]
    [SerializeField] private PlayerInventory playerInventory;
    [SerializeField] private PlayerWeaponController playerWeaponController;
    [SerializeField] private ScrollRect weaponsScrollRect;
    [SerializeField] private Transform weaponsListContentRoot;
    [SerializeField] private GameObject weaponEntryButtonPrefab;
    [SerializeField] private TMP_Text dmgItemText;
    [SerializeField] private TMP_Text valItemText;
    [SerializeField] private Image cndBarFillImage;
    [SerializeField] private TMP_Text wgItemText;
    [SerializeField] private TMP_Text ammoItemText;
    [SerializeField] private Image selectedBoxImage;
    [SerializeField] private Image selectedBoxBackgroundImage;
    [SerializeField] private Image equippedSelectedBoxImage;
    [SerializeField] private ButtonHighlight hoveredWeaponEntryHighlight;
    [SerializeField] private ButtonHighlight hoveredAidEntryHighlight;
    [SerializeField] private ButtonHighlight hoveredMiscEntryHighlight;
    [SerializeField] private ButtonHighlight hoveredAmmoEntryHighlight;
    [SerializeField] private GameObject[] hoveredWeaponStatObjects;
    [SerializeField] private GameObject[] hoveredAidStatObjects;
    [SerializeField] private GameObject[] hoveredMiscStatObjects;
    [SerializeField] private GameObject[] hoveredAmmoStatObjects;

    [Header("Aid List")]
    [SerializeField] private ScrollRect aidScrollRect;
    [SerializeField] private Transform aidListContentRoot;
    [SerializeField] private GameObject aidEntryButtonPrefab;
    [SerializeField] private TMP_Text aidValItemText;
    [SerializeField] private TMP_Text aidWgItemText;
    [SerializeField] private TMP_Text aidEffectsText;
    [SerializeField] private TMP_Text aidEffectsItemText;

    [Header("Misc List")]
    [SerializeField] private ScrollRect miscScrollRect;
    [SerializeField] private Transform miscListContentRoot;
    [SerializeField] private GameObject miscEntryButtonPrefab;
    [SerializeField] private TMP_Text miscValItemText;
    [SerializeField] private TMP_Text miscWgItemText;

    [Header("Ammo List")]
    [SerializeField] private ScrollRect ammoScrollRect;
    [SerializeField] private Transform ammoListContentRoot;
    [SerializeField] private GameObject ammoEntryButtonPrefab;
    [SerializeField] private TMP_Text ammoValItemText;
    [SerializeField] private TMP_Text ammoWgItemText;

    [Header("Quests List")]
    [SerializeField] private QuestController questController;
    [SerializeField] private ScrollRect questsScrollRect;
    [SerializeField] private Transform questsListContentRoot;
    [SerializeField] private GameObject questEntryButtonPrefab;
    [SerializeField] private TMP_Text questObjectiveTextTemplate;
    [SerializeField] private Image questSelectedBoxImage;
    [SerializeField] private ButtonHighlight hoveredQuestEntryHighlight;
    [SerializeField, Range(0.0f, 1.0f)] private float completedQuestEntryAlpha = 0.42f;
    [SerializeField, Range(0.0f, 1.0f)] private float completedQuestObjectiveAlpha = 0.42f;
    [SerializeField, Min(0.0f)] private float questEntryBoxTextGap = 5.0f;
    [SerializeField, Min(0.0f)] private float questObjectiveBoxTextGap = 6.0f;
    [SerializeField, Min(0.0f)] private float questObjectiveScrollViewGap = 6.0f;
    [SerializeField, Min(0.0f)] private float questObjectiveVerticalSpacing = 8.0f;

    [Header("Item Drop")]
    [SerializeField] private Transform dropSpawnTransform;
    [SerializeField] private System.Collections.Generic.List<GameObject> fallbackWeaponWorldPrefabs = new System.Collections.Generic.List<GameObject>();
    [SerializeField] private System.Collections.Generic.List<GameObject> fallbackAidWorldPrefabs = new System.Collections.Generic.List<GameObject>();
    [SerializeField] private System.Collections.Generic.List<GameObject> fallbackMiscWorldPrefabs = new System.Collections.Generic.List<GameObject>();
    [SerializeField] private System.Collections.Generic.List<GameObject> fallbackAmmoWorldPrefabs = new System.Collections.Generic.List<GameObject>();

    [Header("Stats Player Values")]
    [SerializeField] private PlayerState playerState;
    [SerializeField] private PlayerAid playerAidSystem;
    [SerializeField] private TMP_Text hpPlayerText;
    [SerializeField] private TMP_Text drPlayerText;
    [SerializeField] private TMP_Text wgPlayerText;
    [SerializeField] private TMP_Text capsPlayerText;
    [SerializeField] private TMP_Text apPlayerText;
    [SerializeField] private TMP_Text lvlPlayerText;
    [SerializeField] private TMP_Text xpPlayerText;

    // Runtime-spawned weapons list entries.
    private readonly System.Collections.Generic.List<GameObject> spawnedWeaponEntryButtons = new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.List<GameObject> spawnedAidEntryButtons = new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.List<GameObject> spawnedMiscEntryButtons = new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.List<GameObject> spawnedAmmoEntryButtons = new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.List<GameObject> spawnedQuestEntryButtons = new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.List<GameObject> spawnedQuestObjectiveRows = new System.Collections.Generic.List<GameObject>();
    private readonly System.Collections.Generic.List<Image> resolvedCndBarFillImages = new System.Collections.Generic.List<Image>();
    private readonly System.Collections.Generic.Dictionary<VerticalLayoutGroup, int> baseLeftPaddingByLayoutGroup =
        new System.Collections.Generic.Dictionary<VerticalLayoutGroup, int>();
    private readonly System.Collections.Generic.Dictionary<RectTransform, float> baseEntryLabelAnchoredXByRectTransform =
        new System.Collections.Generic.Dictionary<RectTransform, float>();
    private readonly System.Collections.Generic.Dictionary<GameObject, PipBoyListEntryState> pooledEntryStateByObject =
        new System.Collections.Generic.Dictionary<GameObject, PipBoyListEntryState>();
    private bool weaponsListDirty = true;
    private bool aidListDirty = true;
    private bool miscListDirty = true;
    private bool ammoListDirty = true;
    private bool isSubscribedToInventory;
    private bool questsListDirty = true;
    private bool isSubscribedToQuestController;
    private PlayerInventory.InventoryEntry selectedWeaponInventoryEntry;
    private PlayerInventory.InventoryEntry hoveredWeaponInventoryEntry;
    private PlayerInventory.InventoryEntry hoveredAidInventoryEntry;
    private PlayerInventory.InventoryEntry hoveredMiscInventoryEntry;
    private PlayerInventory.InventoryEntry hoveredAmmoInventoryEntry;
    private QuestRuntimeState hoveredQuestState;
    private Image currentQuestSelectedBoxImage;
    private PlayerInventory.InventoryEntry pendingWeaponInventoryEntry;
    private bool hasPendingWeaponEquipOnClose;
    private static readonly string[] DefaultHoveredWeaponStatObjectNames =
    {
        "MidLine1",
        "MidLine2",
        "DAMText",
        "DAMItemText",
        "WGText",
        "WGItemText",
        "VALText",
        "VALItemText",
        "CNDText",
        "CNDBarBackground"
    };
    private static readonly string[] DefaultHoveredAidStatObjectNames =
    {
        "MidLine1",
        "MidLine2",
        "VALText",
        "VALItemText",
        "WGText",
        "WGItemText",
        "EffectsText",
        "EffectsItemText"
    };
    private static readonly string[] DefaultHoveredMiscStatObjectNames =
    {
        "MidLine1",
        "MidLine2",
        "VALText",
        "VALItemText",
        "WGText",
        "WGItemText"
    };
    private static readonly string[] DefaultHoveredAmmoStatObjectNames =
    {
        "MidLine1",
        "MidLine2",
        "VALText",
        "VALItemText",
        "WGText",
        "WGItemText"
    };

    private const int WeaponRowBindingMode = 1;
    private const int AidRowBindingMode = 2;
    private const int MiscRowBindingMode = 3;
    private const int AmmoRowBindingMode = 4;
    private const int QuestRowBindingMode = 5;
    private const float QuestEntryFontSize = 10.0f;
    private const float RuntimeQuestObjectiveTextWidth = 150.0f;
    private const int MiscAndAmmoListLeftPaddingOffsetPixels = -10;
    private const float AidMiscAmmoEntryTextHorizontalOffsetPixels = 5.0f;
    private const float MiscAndAmmoHighlightRightCompensationPixels = 10.0f;
    private const float PipBoyPaletteColorTolerance = 0.01f;
    private const float SelectionOutlineThickness = 2.0f;
    private const float QuestBoxOutlineThickness = 1.0f;
    private const string PrimaryTabContainerObjectName = "PrimaryTabContainer";
    private const string RuntimeThemeBackgroundExclusionObjectName = "Background";
    private static Sprite fallbackConditionBarFillSprite;

    private sealed class PipBoyListEntryState
    {
        public PlayerInventory.InventoryEntry BoundEntry;
        public QuestRuntimeState BoundQuestState;
        public string BoundLabel = string.Empty;
        public int BoundMode;
        public Button EntryButton;
        public TextMeshProUGUI EntryLabel;
        public EventTrigger EntryEventTrigger;
        public RectTransform RuntimeBoxRect;
        public CanvasGroup CanvasGroup;
    }

    

    // methods
    private void Awake()
    {
        if (hasAwakeInitialized)
            return;

        hasAwakeInitialized = true;
        isRunningAwakeInitialization = true;

        // If the canvas group is not assigned, try to find it on this object.
        if (!pipBoyCanvasGroup)
            pipBoyCanvasGroup = GetComponentInChildren<CanvasGroup>(true);

        // If PlayerControls is not assigned, try to find it in the scene.
        if (!playerControls)
            playerControls = FindAnyObjectByType<PlayerControls>();

        // If CameraRigOrbit is not assigned, try to find it in the scene.
        if (!cameraRigOrbit)
            cameraRigOrbit = FindAnyObjectByType<CameraRigOrbit>();

        // If CameraControlZoom is not assigned, try to find it in the scene.
        if (!cameraControlZoom)
            cameraControlZoom = FindAnyObjectByType<CameraControlZoom>();

        // If PlayerInventory is not assigned, try to find it in the scene.
        if (!playerInventory)
            playerInventory = FindAnyObjectByType<PlayerInventory>();

        // If PlayerWeaponController is not assigned, try to find it in the scene.
        if (!playerWeaponController)
            playerWeaponController = FindAnyObjectByType<PlayerWeaponController>();

        // If PlayerState is not assigned, try to find it in the scene.
        if (!playerState)
            playerState = FindAnyObjectByType<PlayerState>();

        // If Aid system is not assigned, try to find it in the scene.
        if (!playerAidSystem)
            playerAidSystem = FindAnyObjectByType<PlayerAid>();

        // Create a runtime Aid system on the player when one is not already present.
        if (!playerAidSystem && playerState)
            playerAidSystem = playerState.GetComponent<PlayerAid>() ?? playerState.gameObject.AddComponent<PlayerAid>();

        // Auto-wire known stats text fields if they were not assigned in inspector.
        if (!hpPlayerText)
            hpPlayerText = FindChildComponentByName<TMP_Text>("HPPlayerText");

        if (!drPlayerText)
            drPlayerText = FindChildComponentByName<TMP_Text>("DRPlayerText");

        if (!wgPlayerText)
            wgPlayerText = FindChildComponentByName<TMP_Text>("WgPlayerText");

        if (!wgPlayerText)
            wgPlayerText = FindChildComponentByName<TMP_Text>("WGPlayerText");

        if (!capsPlayerText)
            capsPlayerText = FindChildComponentByName<TMP_Text>("CapsPlayerText");

        if (!apPlayerText)
            apPlayerText = FindChildComponentByName<TMP_Text>("APPlayerText");

        if (!lvlPlayerText)
            lvlPlayerText = FindChildComponentByName<TMP_Text>("LVLPlayerText");

        if (!xpPlayerText)
            xpPlayerText = FindChildComponentByName<TMP_Text>("XPPlayerText");

        if (!dmgItemText)
            dmgItemText = FindChildComponentByName<TMP_Text>("DAMItemText");

        if (!valItemText)
            valItemText = FindChildComponentByName<TMP_Text>("VALItemText");

        if (!cndBarFillImage)
            cndBarFillImage = FindChildComponentByNameInRoot<Image>("CNDBarFill", itemsWeaponsPanel ? itemsWeaponsPanel.transform : null);

        if (!cndBarFillImage)
            cndBarFillImage = FindChildComponentByName<Image>("CNDBarFill");

        AutoWireConditionBarFillImages();

        if (!wgItemText)
            wgItemText = FindChildComponentByName<TMP_Text>("WGItemText");

        if (!ammoItemText)
            ammoItemText = FindChildComponentByNameInRoot<TMP_Text>("AmmoText", itemsWeaponsPanel ? itemsWeaponsPanel.transform : null);

        if (!ammoItemText)
            ammoItemText = FindChildComponentByName<TMP_Text>("AmmoText");

        if (!aidScrollRect && itemsAidPanel)
            aidScrollRect = itemsAidPanel.GetComponentInChildren<ScrollRect>(true);

        if (!aidListContentRoot && aidScrollRect && aidScrollRect.content)
            aidListContentRoot = aidScrollRect.content;

        if (!aidValItemText)
            aidValItemText = FindChildComponentByNameInRoot<TMP_Text>("VALItemText", itemsAidPanel ? itemsAidPanel.transform : null);

        if (!aidWgItemText)
            aidWgItemText = FindChildComponentByNameInRoot<TMP_Text>("WGItemText", itemsAidPanel ? itemsAidPanel.transform : null);

        if (!aidEffectsText)
            aidEffectsText = FindChildComponentByNameInRoot<TMP_Text>("EffectsText", itemsAidPanel ? itemsAidPanel.transform : null);

        if (!aidEffectsItemText)
            aidEffectsItemText = FindChildComponentByNameInRoot<TMP_Text>("EffectsItemText", itemsAidPanel ? itemsAidPanel.transform : null);

        if (!aidValItemText)
            aidValItemText = valItemText;

        if (!aidWgItemText)
            aidWgItemText = wgItemText;

        if (!aidEntryButtonPrefab)
            aidEntryButtonPrefab = miscEntryButtonPrefab ? miscEntryButtonPrefab : weaponEntryButtonPrefab;

        if (!miscScrollRect && itemsMiscPanel)
            miscScrollRect = itemsMiscPanel.GetComponentInChildren<ScrollRect>(true);

        if (!miscListContentRoot && miscScrollRect && miscScrollRect.content)
            miscListContentRoot = miscScrollRect.content;

        if (!miscValItemText)
            miscValItemText = FindChildComponentByNameInRoot<TMP_Text>("VALItemText", itemsMiscPanel ? itemsMiscPanel.transform : null);

        if (!miscWgItemText)
            miscWgItemText = FindChildComponentByNameInRoot<TMP_Text>("WGItemText", itemsMiscPanel ? itemsMiscPanel.transform : null);

        if (!miscValItemText)
            miscValItemText = valItemText;

        if (!miscWgItemText)
            miscWgItemText = wgItemText;

        if (!miscEntryButtonPrefab)
            miscEntryButtonPrefab = weaponEntryButtonPrefab;

        if (!ammoScrollRect && itemsAmmoPanel)
            ammoScrollRect = itemsAmmoPanel.GetComponentInChildren<ScrollRect>(true);

        if (!ammoListContentRoot && ammoScrollRect && ammoScrollRect.content)
            ammoListContentRoot = ammoScrollRect.content;

        if (!ammoValItemText)
            ammoValItemText = FindChildComponentByNameInRoot<TMP_Text>("VALItemText", itemsAmmoPanel ? itemsAmmoPanel.transform : null);

        if (!ammoWgItemText)
            ammoWgItemText = FindChildComponentByNameInRoot<TMP_Text>("WGItemText", itemsAmmoPanel ? itemsAmmoPanel.transform : null);

        if (!ammoValItemText)
            ammoValItemText = valItemText;

        if (!ammoWgItemText)
            ammoWgItemText = wgItemText;

        if (!ammoEntryButtonPrefab)
            ammoEntryButtonPrefab = miscEntryButtonPrefab ? miscEntryButtonPrefab : weaponEntryButtonPrefab;

        if (!questController)
            questController = QuestController.FindOrCreate();

        if (!questsScrollRect && dataQuestsPanel)
            questsScrollRect = dataQuestsPanel.GetComponentInChildren<ScrollRect>(true);

        if (!questsListContentRoot && questsScrollRect && questsScrollRect.content)
            questsListContentRoot = questsScrollRect.content;

        if (!questEntryButtonPrefab)
            questEntryButtonPrefab = weaponEntryButtonPrefab;

        if (!questObjectiveTextTemplate)
            questObjectiveTextTemplate = FindChildComponentByNameInRoot<TMP_Text>("ObjectiveText", dataQuestsPanel ? dataQuestsPanel.transform : null);

        if (!questSelectedBoxImage)
            questSelectedBoxImage = FindChildComponentByNameInRoot<Image>("SelectedBox", dataQuestsPanel ? dataQuestsPanel.transform : null);

        ConfigureQuestTemplateObject(questObjectiveTextTemplate ? questObjectiveTextTemplate.gameObject : null);
        ConfigureQuestTemplateObject(questSelectedBoxImage ? questSelectedBoxImage.gameObject : null);
        if (!currentQuestSelectedBoxImage)
            currentQuestSelectedBoxImage = questSelectedBoxImage;

        ConfigureWeaponEntrySelectionIndicator(currentQuestSelectedBoxImage);
        HideQuestTemplateObjects();

        AutoWireWeaponEntrySelectionIndicators();
        ConfigureWeaponEntrySelectionIndicator(selectedBoxImage);
        ConfigureWeaponEntrySelectionIndicator(selectedBoxBackgroundImage);
        ConfigureWeaponEntrySelectionIndicator(equippedSelectedBoxImage);
        SetWeaponEntrySelectionIndicatorsVisible(false, false, false);

        AutoWireHoveredWeaponEntryHighlight();
        DisableButtonHighlightRaycasts(hoveredWeaponEntryHighlight);
        SetButtonHighlight(hoveredWeaponEntryHighlight, false, false);

        AutoWireHoveredAidEntryHighlight();
        DisableButtonHighlightRaycasts(hoveredAidEntryHighlight);
        SetButtonHighlight(hoveredAidEntryHighlight, false, false);

        AutoWireHoveredMiscEntryHighlight();
        DisableButtonHighlightRaycasts(hoveredMiscEntryHighlight);
        SetButtonHighlight(hoveredMiscEntryHighlight, false, false);

        AutoWireHoveredAmmoEntryHighlight();
        DisableButtonHighlightRaycasts(hoveredAmmoEntryHighlight);
        SetButtonHighlight(hoveredAmmoEntryHighlight, false, false);
        AutoWireHoveredQuestEntryHighlight();
        DisableButtonHighlightRaycasts(hoveredQuestEntryHighlight);
        SetButtonHighlight(hoveredQuestEntryHighlight, false, false);

        AutoWireHoveredWeaponStatObjects();
        DisableHoveredWeaponStatObjectRaycasts();
        SetHoveredWeaponStatObjectsVisible(false);

        AutoWireHoveredAidStatObjects();
        DisableHoveredAidStatObjectRaycasts();
        SetHoveredAidStatObjectsVisible(false);

        AutoWireHoveredMiscStatObjects();
        DisableHoveredMiscStatObjectRaycasts();
        SetHoveredMiscStatObjectsVisible(false);

        AutoWireHoveredAmmoStatObjects();
        DisableHoveredAmmoStatObjectRaycasts();
        SetHoveredAmmoStatObjectsVisible(false);

        // Cache the input actions if we found controls.
        if (playerControls)
            controls = playerControls.Controls;

        // Cache the toggle input callback.
        onPipBoyPerformed = _ => TogglePipBoy();
        onDropPerformed = OnDropPerformed;

        // Start with the PipBoy closed.
        SetOpenState(false);

        // Keep PipBoy UI palette consistent by remapping white/black authored colors.
        ApplyPipBoyPaletteColorOverrides();

        isRunningAwakeInitialization = false;
    }


    private void Start()
    {
        if (hasStartInitialized)
            return;

        hasStartInitialized = true;

        // Default to Stats and Status on boot.
        ShowStats();

        ShowStatsStatus();

        MarkAllItemListsDirty();
        MarkQuestsListDirty();
        ClearHoveredWeaponItemStats();
        ClearHoveredAidItemStats();
        ClearHoveredMiscItemStats();
        ClearHoveredAmmoItemStats();
        ClearHoveredQuest();

        if (isOpen)
        {
            RefreshVisibleItemLists();
            RefreshVisibleQuestList();
            RefreshStatsPlayerTexts();
        }
    }

    private void Update()
    {
        if (!isOpen)
            return;

        RefreshVisibleItemLists();
        RefreshVisibleQuestList();
        RefreshWeaponEntrySelectionIndicators();
        RefreshStatsPlayerTexts();
    }


    public void TogglePipBoy()
    {
        EnsureInitializedForExternalAccess();

        float now = Time.unscaledTime;
        if (now <= nextAllowedToggleUnscaledTime)
            return;

        nextAllowedToggleUnscaledTime = now + toggleDebounceSeconds;

        // Flip the open state.
        SetOpenState(!isOpen);
    }

    public bool IsOpen()
    {
        return isOpen;
    }


    private void OnEnable()
    {
        if (controls != null)
        {
            controls.Player.PipBoy.performed += onPipBoyPerformed;
            controls.UI.Drop.performed += onDropPerformed;
        }

        if (isOpen)
        {
            SubscribeToInventoryChanges();
            SubscribeToQuestControllerChanges();
        }
    }


    private void OnDisable()
    {
        if (controls != null)
        {
            controls.Player.PipBoy.performed -= onPipBoyPerformed;
            controls.UI.Drop.performed -= onDropPerformed;
        }

        UnsubscribeFromInventoryChanges();
        UnsubscribeFromQuestControllerChanges();
    }


    public void SetOpenState(bool open)
    {
        if (!isRunningAwakeInitialization)
            EnsureInitializedForExternalAccess();

        bool wasOpen = isOpen;

        // Store the state.
        isOpen = open;
        EventSystem eventSystem = EventSystem.current;

        // Ensure the Pip-Boy hierarchy is active before enabling UI interaction.
        if (isOpen)
            SetPipBoyHierarchyActive(true);

        // If we have a canvas group, apply visibility and interaction.
        if (pipBoyCanvasGroup)
        {
            // Set alpha to show or hide.
            pipBoyCanvasGroup.alpha = isOpen ? 1f : 0f;

            // Enable clicking when open.
            pipBoyCanvasGroup.interactable = isOpen;

            // Enable raycasts when open.
            pipBoyCanvasGroup.blocksRaycasts = isOpen;
        }

        // Toggle optional background root.
        SetActiveSafe(pipBoyBackgroundRoot, isOpen);

        // If opening, unlock the cursor.
        if (isOpen)
        {
            if (!wasOpen)
                ShowCurrentMainTab();

            if (!wasOpen)
            {
                SubscribeToInventoryChanges();
                SubscribeToQuestControllerChanges();
            }

            if (cameraRigOrbit)
                cameraRigOrbit.SetInputEnabled(false);

            if (cameraControlZoom)
                cameraControlZoom.SetInputEnabled(false);

            if (pauseGameWhenOpen)
                PauseGameTime();

            // Make the cursor visible.
            Cursor.visible = true;

            // Unlock the cursor for UI interaction.
            Cursor.lockState = CursorLockMode.None;

            // Disable gameplay inputs and enable UI inputs.
            if (controls != null)
            {
                controls.UI.Enable();
                if (disableGameplayActionsWhenOpen)
                    SetGameplayActionsEnabled(false);
            }

            // Clear any previous selection.
            if (eventSystem)
                eventSystem.SetSelectedGameObject(null);

            // Set the first selected UI object if provided.
            if (eventSystem && firstSelectedUIObject)
                eventSystem.SetSelectedGameObject(firstSelectedUIObject);

            MarkAllItemListsDirty();
            MarkQuestsListDirty();
            RefreshVisibleItemLists();
            RefreshVisibleQuestList();
            RefreshStatsPlayerTexts();
            ApplyPipBoyPaletteColorOverrides();
        }
        else
        {
            if (wasOpen)
            {
                UnsubscribeFromInventoryChanges();
                UnsubscribeFromQuestControllerChanges();
            }

            if (pauseGameWhenOpen)
                ResumeGameTime();

            // If no orbit controller is present, fall back to default gameplay cursor style.
            if (!cameraRigOrbit)
            {
                // Hide the cursor when returning to gameplay.
                Cursor.visible = false;

                // Lock the cursor back to gameplay style.
                Cursor.lockState = CursorLockMode.Locked;
            }

            // Return to gameplay inputs and disable UI inputs.
            if (controls != null)
            {
                if (disableGameplayActionsWhenOpen)
                    SetGameplayActionsEnabled(true);
                controls.UI.Disable();
            }

            // Clear selection so UI doesn't keep focus.
            if (eventSystem)
                eventSystem.SetSelectedGameObject(null);

            ApplyPendingWeaponEquipOnClose();

            // Re-enable camera controls after the UI input map has been released.
            if (cameraRigOrbit)
                cameraRigOrbit.SetInputEnabled(true);

            if (cameraControlZoom)
                cameraControlZoom.SetInputEnabled(true);

            if (disableInHierarchyWhenClosed)
                SetPipBoyHierarchyActive(false);
        }
    }


    public void ShowStats()
    {
        currentMainTab = MainTab.Stats;

        // Enable the stats root.
        SetActiveSafe(statsContentRoot, true);

        // Disable other roots.
        SetActiveSafe(itemsContentRoot, false);

        SetActiveSafe(dataContentRoot, false);

        // Toggle higher-level panels.
        SetActiveSafe(statsPanelRoot, true);
        SetActiveSafe(itemsPanelRoot, false);
        SetActiveSafe(dataPanelRoot, false);

        // Show stats subtabs and hide others.
        SetSubtabsVisible(stats: true, items: false, data: false);

        // Restore last stats subtab, falling back to the default.
        var targetPanel = lastStatsPanel ? lastStatsPanel : statsStatusPanel;
        SetStatsPanelActive(targetPanel);

        SetMainTabHighlight(stats: true, items: false, data: false);
    }


    public void ShowItems()
    {
        currentMainTab = MainTab.Items;

        // Enable the items root.
        SetActiveSafe(itemsContentRoot, true);

        // Disable other roots.
        SetActiveSafe(statsContentRoot, false);

        SetActiveSafe(dataContentRoot, false);

        // Toggle higher-level panels.
        SetActiveSafe(statsPanelRoot, false);
        SetActiveSafe(itemsPanelRoot, true);
        SetActiveSafe(dataPanelRoot, false);

        // Show items subtabs and hide others.
        SetSubtabsVisible(stats: false, items: true, data: false);

        // Restore last items subtab, falling back to the default.
        var targetPanel = lastItemsPanel ? lastItemsPanel : itemsWeaponsPanel;
        SetItemsPanelActive(targetPanel);

        SetMainTabHighlight(stats: false, items: true, data: false);
    }


    public void ShowData()
    {
        currentMainTab = MainTab.Data;

        // Enable the data root.
        SetActiveSafe(dataContentRoot, true);

        // Disable other roots.
        SetActiveSafe(statsContentRoot, false);

        SetActiveSafe(itemsContentRoot, false);

        // Toggle higher-level panels.
        SetActiveSafe(statsPanelRoot, false);
        SetActiveSafe(itemsPanelRoot, false);
        SetActiveSafe(dataPanelRoot, true);

        // Show data subtabs and hide others.
        SetSubtabsVisible(stats: false, items: false, data: true);

        // Restore last data subtab, falling back to the default.
        var targetPanel = lastDataPanel ? lastDataPanel : dataLocalMapPanel;
        SetDataPanelActive(targetPanel);

        SetMainTabHighlight(stats: false, items: false, data: true);
    }


    public void ShowStatsStatus()
    {
        // Make sure we are in the Stats category.
        ShowStats();

        // Activate only the chosen stats panel.
        SetStatsPanelActive(statsStatusPanel);
    }


    public void ShowStatsSpecial()
    {
        // Make sure we are in the Stats category.
        ShowStats();

        // Activate only the chosen stats panel.
        SetStatsPanelActive(statsSpecialPanel);
    }


    public void ShowStatsSkills()
    {
        // Make sure we are in the Stats category.
        ShowStats();

        // Activate only the chosen stats panel.
        SetStatsPanelActive(statsSkillsPanel);
    }


    public void ShowStatsPerks()
    {
        // Make sure we are in the Stats category.
        ShowStats();

        // Activate only the chosen stats panel.
        SetStatsPanelActive(statsPerksPanel);
    }


    public void ShowStatsGeneral()
    {
        // Make sure we are in the Stats category.
        ShowStats();

        // Activate only the chosen stats panel.
        SetStatsPanelActive(statsGeneralPanel);
    }


    public void ShowItemsWeapons()
    {
        // Make sure we are in the Items category.
        ShowItems();

        // Activate only the chosen items panel.
        SetItemsPanelActive(itemsWeaponsPanel);

        // Keep the weapons list in sync when this panel is shown.
        weaponsListDirty = true;
        RefreshVisibleItemLists();
    }


    public void ShowItemsApparel()
    {
        // Make sure we are in the Items category.
        ShowItems();

        // Activate only the chosen items panel.
        SetItemsPanelActive(itemsApparelPanel);
    }


    public void ShowItemsAid()
    {
        // Make sure we are in the Items category.
        ShowItems();

        // Activate only the chosen items panel.
        SetItemsPanelActive(itemsAidPanel);

        // Keep the aid list in sync when this panel is shown.
        aidListDirty = true;
        RefreshVisibleItemLists();
    }


    public void ShowItemsMisc()
    {
        // Make sure we are in the Items category.
        ShowItems();

        // Activate only the chosen items panel.
        SetItemsPanelActive(itemsMiscPanel);

        // Keep the misc list in sync when this panel is shown.
        miscListDirty = true;
        RefreshVisibleItemLists();
    }


    public void ShowItemsAmmo()
    {
        // Make sure we are in the Items category.
        ShowItems();

        // Activate only the chosen items panel.
        SetItemsPanelActive(itemsAmmoPanel);

        // Keep the ammo list in sync when this panel is shown.
        ammoListDirty = true;
        RefreshVisibleItemLists();
    }


    public void ShowDataLocalMap()
    {
        // Make sure we are in the Data category.
        ShowData();

        // Activate only the chosen data panel.
        SetDataPanelActive(dataLocalMapPanel);
    }


    public void ShowDataWorldMap()
    {
        // Make sure we are in the Data category.
        ShowData();

        // Activate only the chosen data panel.
        SetDataPanelActive(dataWorldMapPanel);
    }


    public void ShowDataQuests()
    {
        // Make sure we are in the Data category.
        ShowData();

        // Activate only the chosen data panel.
        SetDataPanelActive(dataQuestsPanel);
        MarkQuestsListDirty();
        RefreshVisibleQuestList();
    }


    public void ShowDataNotes()
    {
        // Make sure we are in the Data category.
        ShowData();

        // Activate only the chosen data panel.
        SetDataPanelActive(dataNotesPanel);
    }


    public void ShowDataRadio()
    {
        // Make sure we are in the Data category.
        ShowData();

        // Activate only the chosen data panel.
        SetDataPanelActive(dataRadioPanel);
    }

    private void SetStatsPanelActive(GameObject activePanel)
    {
        if (activePanel)
            lastStatsPanel = activePanel;

        SetActiveSafe(statsStatusPanel, activePanel == statsStatusPanel);
        SetActiveSafe(statsSpecialPanel, activePanel == statsSpecialPanel);
        SetActiveSafe(statsSkillsPanel, activePanel == statsSkillsPanel);
        SetActiveSafe(statsPerksPanel, activePanel == statsPerksPanel);
        SetActiveSafe(statsGeneralPanel, activePanel == statsGeneralPanel);

        SetButtonHighlight(statsStatusHighlight, activePanel == statsStatusPanel, true);
        SetButtonHighlight(statsSpecialHighlight, activePanel == statsSpecialPanel, true);
        SetButtonHighlight(statsSkillsHighlight, activePanel == statsSkillsPanel, true);
        SetButtonHighlight(statsPerksHighlight, activePanel == statsPerksPanel, true);
        SetButtonHighlight(statsGeneralHighlight, activePanel == statsGeneralPanel, true);
    }

    private void SetItemsPanelActive(GameObject activePanel)
    {
        if (activePanel)
            lastItemsPanel = activePanel;

        SetActiveSafe(itemsWeaponsPanel, activePanel == itemsWeaponsPanel);
        SetActiveSafe(itemsApparelPanel, activePanel == itemsApparelPanel);
        SetActiveSafe(itemsAidPanel, activePanel == itemsAidPanel);
        SetActiveSafe(itemsMiscPanel, activePanel == itemsMiscPanel);
        SetActiveSafe(itemsAmmoPanel, activePanel == itemsAmmoPanel);

        SetButtonHighlight(itemsWeaponsHighlight, activePanel == itemsWeaponsPanel, true);
        SetButtonHighlight(itemsApparelHighlight, activePanel == itemsApparelPanel, true);
        SetButtonHighlight(itemsAidHighlight, activePanel == itemsAidPanel, true);
        SetButtonHighlight(itemsMiscHighlight, activePanel == itemsMiscPanel, true);
        SetButtonHighlight(itemsAmmoHighlight, activePanel == itemsAmmoPanel, true);

        RefreshWeaponEntrySelectionIndicators();
    }

    private void SetDataPanelActive(GameObject activePanel)
    {
        if (activePanel)
            lastDataPanel = activePanel;

        SetActiveSafe(dataLocalMapPanel, activePanel == dataLocalMapPanel);
        SetActiveSafe(dataWorldMapPanel, activePanel == dataWorldMapPanel);
        SetActiveSafe(dataQuestsPanel, activePanel == dataQuestsPanel);
        SetActiveSafe(dataNotesPanel, activePanel == dataNotesPanel);
        SetActiveSafe(dataRadioPanel, activePanel == dataRadioPanel);

        SetButtonHighlight(dataLocalMapHighlight, activePanel == dataLocalMapPanel, true);
        SetButtonHighlight(dataWorldMapHighlight, activePanel == dataWorldMapPanel, true);
        SetButtonHighlight(dataQuestsHighlight, activePanel == dataQuestsPanel, true);
        SetButtonHighlight(dataNotesHighlight, activePanel == dataNotesPanel, true);
        SetButtonHighlight(dataRadioHighlight, activePanel == dataRadioPanel, true);

        if (activePanel == dataQuestsPanel)
        {
            MarkQuestsListDirty();
            RefreshVisibleQuestList();
        }
    }

    private void SetSubtabsVisible(bool stats, bool items, bool data)
    {
        SetActiveSafe(statsSubtabRoot, stats);
        SetActiveSafe(itemsSubtabRoot, items);
        SetActiveSafe(dataSubtabRoot, data);

        SetSubtabButtonsActive(statsSubtabButtons, stats);
        SetSubtabButtonsActive(itemsSubtabButtons, items);
        SetSubtabButtonsActive(dataSubtabButtons, data);
    }

    private void SetSubtabButtonsActive(GameObject[] buttons, bool active)
    {
        if (buttons == null) return;

        for (int i = 0; i < buttons.Length; i++)
            SetActiveSafe(buttons[i], active);
    }

    private void SetMainTabHighlight(bool stats, bool items, bool data)
    {
        SetActiveSafe(statsMainHighlight, stats);
        SetActiveSafe(itemsMainHighlight, items);
        SetActiveSafe(dataMainHighlight, data);
    }

    private void SetButtonHighlight(ButtonHighlight highlight, bool outlineActive, bool backgroundActive)
    {
        PrepareButtonHighlightGraphic(highlight.outline, outlineActive);
        PrepareButtonHighlightGraphic(highlight.background, backgroundActive);
        SetActiveSafe(highlight.outline, outlineActive);
        SetActiveSafe(highlight.background, backgroundActive);
    }

    private static void PrepareButtonHighlightGraphic(GameObject highlightObject, bool shouldBeVisible)
    {
        if (!highlightObject || !shouldBeVisible)
            return;

        RectTransform rectTransform = highlightObject.transform as RectTransform;
        if (rectTransform)
            rectTransform.localScale = Vector3.one;

        Graphic[] graphics = highlightObject.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (!graphic)
                continue;

            graphic.enabled = true;
            Color color = graphic.color;
            if (color.a <= 0.0f)
            {
                color.a = 1.0f;
                graphic.color = color;
            }
        }
    }


    private void SetActiveSafe(GameObject target, bool active)
    {
        // Do nothing if the object is missing.
        if (!target) return;

        // Apply active state only when needed.
        if (target.activeSelf != active)
            target.SetActive(active);
    }

    private void EnsureInitializedForExternalAccess()
    {
        if (!hasAwakeInitialized)
            Awake();

        if (!hasStartInitialized)
            Start();
    }

    private void ShowCurrentMainTab()
    {
        switch (currentMainTab)
        {
            case MainTab.Items:
                ShowItems();
                break;
            case MainTab.Data:
                ShowData();
                break;
            default:
                ShowStats();
                break;
        }
    }

    private void SetPipBoyHierarchyActive(bool active)
    {
        if (!pipBoyCanvasGroup)
            return;

        GameObject pipBoyRoot = pipBoyCanvasGroup.gameObject;
        if (!pipBoyRoot)
            return;

        bool controllerLivesInsideRoot = transform == pipBoyRoot.transform || transform.IsChildOf(pipBoyRoot.transform);

        if (active)
        {
            // Ensure this root and any disabled parents become active.
            Transform current = pipBoyRoot.transform;
            while (current)
            {
                GameObject currentObject = current.gameObject;
                if (!currentObject.activeSelf)
                    currentObject.SetActive(true);

                current = current.parent;
            }

            // Restore top-level UI branches every time open is requested.
            SetDirectChildrenActive(pipBoyRoot.transform, true);

            if (restoreDisabledDescendantsOnFirstOpen && !hasRestoredDisabledDescendants)
            {
                SetDescendantsActive(pipBoyRoot.transform, true);
                hasRestoredDisabledDescendants = true;
            }

            return;
        }

        // If this controller lives on/under the same root, keep that root alive and disable only its children.
        if (controllerLivesInsideRoot)
        {
            SetDirectChildrenActive(pipBoyRoot.transform, false);
            return;
        }

        if (pipBoyRoot.activeSelf)
            pipBoyRoot.SetActive(false);
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
        }
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

    private void SubscribeToInventoryChanges()
    {
        if (isSubscribedToInventory) return;

        if (!playerInventory)
            playerInventory = FindAnyObjectByType<PlayerInventory>();

        if (!playerInventory) return;

        playerInventory.OnInventoryChanged += OnInventoryChanged;
        isSubscribedToInventory = true;
    }

    private void UnsubscribeFromInventoryChanges()
    {
        if (!isSubscribedToInventory) return;

        if (playerInventory)
            playerInventory.OnInventoryChanged -= OnInventoryChanged;

        isSubscribedToInventory = false;
    }

    private void OnInventoryChanged()
    {
        MarkAllItemListsDirty();
        RefreshVisibleItemLists();
    }

    private void MarkAllItemListsDirty()
    {
        weaponsListDirty = true;
        aidListDirty = true;
        miscListDirty = true;
        ammoListDirty = true;
    }

    private void RefreshVisibleItemLists()
    {
        if (!isOpen) return;

        if (weaponsListDirty && itemsWeaponsPanel && itemsWeaponsPanel.activeInHierarchy)
            RefreshWeaponsList();

        if (aidListDirty && itemsAidPanel && itemsAidPanel.activeInHierarchy)
            RefreshAidList();

        if (miscListDirty && itemsMiscPanel && itemsMiscPanel.activeInHierarchy)
            RefreshMiscList();

        if (ammoListDirty && itemsAmmoPanel && itemsAmmoPanel.activeInHierarchy)
            RefreshAmmoList();
    }

    private void SubscribeToQuestControllerChanges()
    {
        if (isSubscribedToQuestController)
            return;

        if (!questController)
            questController = QuestController.FindOrCreate();

        if (!questController)
            return;

        questController.QuestStarted += OnQuestRuntimeChanged;
        questController.QuestUpdated += OnQuestRuntimeChanged;
        questController.QuestCompleted += OnQuestRuntimeChanged;
        questController.QuestFailed += OnQuestRuntimeChanged;
        questController.CurrentQuestChanged += OnQuestRuntimeChanged;
        questController.QuestStageChanged += OnQuestStageChanged;
        questController.QuestObjectiveChanged += OnQuestObjectiveRuntimeChanged;
        questController.CurrentObjectiveChanged += OnQuestObjectiveRuntimeChanged;
        isSubscribedToQuestController = true;
    }

    private void UnsubscribeFromQuestControllerChanges()
    {
        if (!isSubscribedToQuestController)
            return;

        if (questController)
        {
            questController.QuestStarted -= OnQuestRuntimeChanged;
            questController.QuestUpdated -= OnQuestRuntimeChanged;
            questController.QuestCompleted -= OnQuestRuntimeChanged;
            questController.QuestFailed -= OnQuestRuntimeChanged;
            questController.CurrentQuestChanged -= OnQuestRuntimeChanged;
            questController.QuestStageChanged -= OnQuestStageChanged;
            questController.QuestObjectiveChanged -= OnQuestObjectiveRuntimeChanged;
            questController.CurrentObjectiveChanged -= OnQuestObjectiveRuntimeChanged;
        }

        isSubscribedToQuestController = false;
    }

    private void OnQuestRuntimeChanged(QuestRuntimeState _)
    {
        MarkQuestsListDirty();
        RefreshVisibleQuestList();
    }

    private void OnQuestStageChanged(QuestRuntimeState _, int __)
    {
        MarkQuestsListDirty();
        RefreshVisibleQuestList();
    }

    private void OnQuestObjectiveRuntimeChanged(QuestRuntimeState _, QuestObjectiveRuntimeState __)
    {
        MarkQuestsListDirty();
        RefreshVisibleQuestList();
    }

    private void MarkQuestsListDirty()
    {
        questsListDirty = true;
    }

    private void RefreshVisibleQuestList()
    {
        if (!isOpen) return;

        if (questsListDirty && dataQuestsPanel && dataQuestsPanel.activeInHierarchy)
            RefreshQuestsList();
    }

    private void RefreshWeaponsList()
    {
        Transform listParent = ResolveWeaponsListParent();

        ClearHoveredWeaponItemStats();

        if (!listParent) return;
        if (!weaponEntryButtonPrefab) return;

        if (!playerInventory)
            playerInventory = FindAnyObjectByType<PlayerInventory>();

        if (!playerInventory) return;

        var weapons = playerInventory.GetCategoryItems(PlayerInventory.InventoryCategory.Weapons);
        if (weapons == null) return;

        var sortedWeapons = new System.Collections.Generic.List<PlayerInventory.InventoryEntry>(weapons);
        sortedWeapons.Sort((left, right) => string.Compare(
            GetInventoryEntrySortName(left),
            GetInventoryEntrySortName(right),
            System.StringComparison.OrdinalIgnoreCase));

        var visibleEntries = new System.Collections.Generic.List<PlayerInventory.InventoryEntry>(sortedWeapons.Count);
        for (int i = 0; i < sortedWeapons.Count; i++)
        {
            var entry = sortedWeapons[i];
            if (entry == null) continue;

            var itemDefinition = entry.GetItemDefinition();
            if (!itemDefinition) continue;

            visibleEntries.Add(entry);
        }

        if (selectedWeaponInventoryEntry != null && !visibleEntries.Contains(selectedWeaponInventoryEntry))
            selectedWeaponInventoryEntry = null;

        if (hoveredWeaponInventoryEntry != null && !visibleEntries.Contains(hoveredWeaponInventoryEntry))
            hoveredWeaponInventoryEntry = null;

        EnsurePooledEntries(spawnedWeaponEntryButtons, visibleEntries.Count, weaponEntryButtonPrefab, listParent);

        for (int i = 0; i < visibleEntries.Count; i++)
        {
            var entry = visibleEntries[i];
            var itemDefinition = entry.GetItemDefinition();

            string displayName = GetDisplayNameFromDefinition(itemDefinition, itemDefinition.name);
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = itemDefinition.name;

            int quantity = Mathf.Max(1, entry.GetQuantity());
            string entryLabel = quantity > 1 ? $"{displayName} ({quantity})" : displayName;

            GameObject pooledEntry = spawnedWeaponEntryButtons[i];
            SetActiveSafe(pooledEntry, true);
            BindWeaponEntryButton(pooledEntry, entry, entryLabel);
        }

        SetPooledEntriesActiveCount(spawnedWeaponEntryButtons, visibleEntries.Count);
        RefreshWeaponEntrySelectionIndicators();
        weaponsListDirty = false;
    }

    private void RefreshMiscList()
    {
        Transform listParent = ResolveMiscListParent();
        ApplyListEntryLeftPaddingOffset(listParent, MiscAndAmmoListLeftPaddingOffsetPixels);

        ClearHoveredMiscItemStats();

        GameObject entryPrefab = miscEntryButtonPrefab ? miscEntryButtonPrefab : weaponEntryButtonPrefab;

        if (!listParent) return;
        if (!entryPrefab) return;

        if (!playerInventory)
            playerInventory = FindAnyObjectByType<PlayerInventory>();

        if (!playerInventory) return;

        var miscItems = playerInventory.GetCategoryItems(PlayerInventory.InventoryCategory.Misc);
        if (miscItems == null) return;

        var sortedMiscItems = new System.Collections.Generic.List<PlayerInventory.InventoryEntry>(miscItems);
        sortedMiscItems.Sort((left, right) => string.Compare(
            GetInventoryEntrySortName(left),
            GetInventoryEntrySortName(right),
            System.StringComparison.OrdinalIgnoreCase));

        var visibleEntries = new System.Collections.Generic.List<PlayerInventory.InventoryEntry>(sortedMiscItems.Count);
        for (int i = 0; i < sortedMiscItems.Count; i++)
        {
            var entry = sortedMiscItems[i];
            if (entry == null) continue;

            var itemDefinition = entry.GetItemDefinition();
            if (!(itemDefinition is MiscDefinition)) continue;

            visibleEntries.Add(entry);
        }

        EnsurePooledEntries(spawnedMiscEntryButtons, visibleEntries.Count, entryPrefab, listParent);

        for (int i = 0; i < visibleEntries.Count; i++)
        {
            var entry = visibleEntries[i];
            var itemDefinition = entry.GetItemDefinition();

            string displayName = GetDisplayNameFromDefinition(itemDefinition, itemDefinition.name);
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = itemDefinition.name;

            int quantity = Mathf.Max(0, entry.GetQuantity());
            string entryLabel = $"{displayName} ({quantity})";

            GameObject pooledEntry = spawnedMiscEntryButtons[i];
            SetActiveSafe(pooledEntry, true);
            BindMiscEntryButton(pooledEntry, entry, entryLabel);
        }

        SetPooledEntriesActiveCount(spawnedMiscEntryButtons, visibleEntries.Count);
        miscListDirty = false;
    }

    private void RefreshAidList()
    {
        Transform listParent = ResolveAidListParent();
        ApplyListEntryLeftPaddingOffset(listParent, MiscAndAmmoListLeftPaddingOffsetPixels);

        ClearHoveredAidItemStats();

        GameObject entryPrefab = aidEntryButtonPrefab ? aidEntryButtonPrefab : miscEntryButtonPrefab ? miscEntryButtonPrefab : weaponEntryButtonPrefab;

        if (!listParent) return;
        if (!entryPrefab) return;

        if (!playerInventory)
            playerInventory = FindAnyObjectByType<PlayerInventory>();

        if (!playerInventory) return;

        var aidItems = playerInventory.GetCategoryItems(PlayerInventory.InventoryCategory.Aid);
        if (aidItems == null) return;

        var sortedAidItems = new System.Collections.Generic.List<PlayerInventory.InventoryEntry>(aidItems);
        sortedAidItems.Sort((left, right) => string.Compare(
            GetInventoryEntrySortName(left),
            GetInventoryEntrySortName(right),
            System.StringComparison.OrdinalIgnoreCase));

        var visibleEntries = new System.Collections.Generic.List<PlayerInventory.InventoryEntry>(sortedAidItems.Count);
        for (int i = 0; i < sortedAidItems.Count; i++)
        {
            var entry = sortedAidItems[i];
            if (entry == null) continue;

            var itemDefinition = entry.GetItemDefinition();
            if (!(itemDefinition is AidDefinition)) continue;

            visibleEntries.Add(entry);
        }

        EnsurePooledEntries(spawnedAidEntryButtons, visibleEntries.Count, entryPrefab, listParent);

        for (int i = 0; i < visibleEntries.Count; i++)
        {
            var entry = visibleEntries[i];
            var itemDefinition = entry.GetItemDefinition();

            string displayName = GetDisplayNameFromDefinition(itemDefinition, itemDefinition.name);
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = itemDefinition.name;

            int quantity = Mathf.Max(0, entry.GetQuantity());
            string entryLabel = $"{displayName} ({quantity})";

            GameObject pooledEntry = spawnedAidEntryButtons[i];
            SetActiveSafe(pooledEntry, true);
            BindAidEntryButton(pooledEntry, entry, entryLabel);
        }

        SetPooledEntriesActiveCount(spawnedAidEntryButtons, visibleEntries.Count);
        aidListDirty = false;
    }

    private void RefreshAmmoList()
    {
        Transform listParent = ResolveAmmoListParent();
        ApplyListEntryLeftPaddingOffset(listParent, MiscAndAmmoListLeftPaddingOffsetPixels);

        ClearHoveredAmmoItemStats();

        GameObject entryPrefab = ammoEntryButtonPrefab ? ammoEntryButtonPrefab : miscEntryButtonPrefab ? miscEntryButtonPrefab : weaponEntryButtonPrefab;

        if (!listParent) return;
        if (!entryPrefab) return;

        if (!playerInventory)
            playerInventory = FindAnyObjectByType<PlayerInventory>();

        if (!playerInventory) return;

        var ammoItems = playerInventory.GetCategoryItems(PlayerInventory.InventoryCategory.Ammo);
        if (ammoItems == null) return;

        var sortedAmmoItems = new System.Collections.Generic.List<PlayerInventory.InventoryEntry>(ammoItems);
        sortedAmmoItems.Sort((left, right) => string.Compare(
            GetInventoryEntrySortName(left),
            GetInventoryEntrySortName(right),
            System.StringComparison.OrdinalIgnoreCase));

        var visibleEntries = new System.Collections.Generic.List<PlayerInventory.InventoryEntry>(sortedAmmoItems.Count);
        for (int i = 0; i < sortedAmmoItems.Count; i++)
        {
            var entry = sortedAmmoItems[i];
            if (entry == null) continue;

            var itemDefinition = entry.GetItemDefinition();
            if (!(itemDefinition is AmmoDefinition)) continue;

            visibleEntries.Add(entry);
        }

        EnsurePooledEntries(spawnedAmmoEntryButtons, visibleEntries.Count, entryPrefab, listParent);

        for (int i = 0; i < visibleEntries.Count; i++)
        {
            var entry = visibleEntries[i];
            var itemDefinition = entry.GetItemDefinition();

            string displayName = GetDisplayNameFromDefinition(itemDefinition, itemDefinition.name);
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = itemDefinition.name;

            int quantity = Mathf.Max(0, entry.GetQuantity());
            string entryLabel = $"{displayName} ({quantity})";

            GameObject pooledEntry = spawnedAmmoEntryButtons[i];
            SetActiveSafe(pooledEntry, true);
            BindAmmoEntryButton(pooledEntry, entry, entryLabel);
        }

        SetPooledEntriesActiveCount(spawnedAmmoEntryButtons, visibleEntries.Count);
        ammoListDirty = false;
    }

    private void RefreshQuestsList()
    {
        HideQuestTemplateObjects();
        ClearHoveredQuestEntryHighlight();

        Transform listParent = ResolveQuestsListParent();
        if (!listParent)
            return;

        GameObject entryPrefab = questEntryButtonPrefab ? questEntryButtonPrefab : weaponEntryButtonPrefab;
        if (!entryPrefab)
            return;

        if (!questController)
            questController = QuestController.FindOrCreate();

        if (!questController)
            return;

        System.Collections.Generic.List<QuestRuntimeState> visibleQuests = BuildVisibleQuestStates();

        if (hoveredQuestState != null && !visibleQuests.Contains(hoveredQuestState))
            hoveredQuestState = null;

        EnsurePooledEntries(spawnedQuestEntryButtons, visibleQuests.Count, entryPrefab, listParent);

        for (int i = 0; i < visibleQuests.Count; i++)
        {
            QuestRuntimeState questState = visibleQuests[i];
            GameObject pooledEntry = spawnedQuestEntryButtons[i];
            SetActiveSafe(pooledEntry, true);
            BindQuestEntryButton(pooledEntry, questState);
        }

        SetPooledEntriesActiveCount(spawnedQuestEntryButtons, visibleQuests.Count);
        ForceRebuildQuestListLayout(listParent);
        RefreshCurrentQuestSelectionIndicator(visibleQuests);
        RefreshQuestObjectiveDetails(GetQuestStateForObjectiveDetails(visibleQuests));
        questsListDirty = false;
    }

    private static void ForceRebuildQuestListLayout(Transform listParent)
    {
        Canvas.ForceUpdateCanvases();

        RectTransform listParentRect = listParent as RectTransform;
        if (listParentRect)
            LayoutRebuilder.ForceRebuildLayoutImmediate(listParentRect);

        Canvas.ForceUpdateCanvases();
    }

    private System.Collections.Generic.List<QuestRuntimeState> BuildVisibleQuestStates()
    {
        System.Collections.Generic.List<QuestRuntimeState> visibleQuests = new System.Collections.Generic.List<QuestRuntimeState>();
        if (!questController)
            return visibleQuests;

        IReadOnlyList<QuestRuntimeState> questStates = questController.GetQuestStates();
        if (questStates == null)
            return visibleQuests;

        for (int i = 0; i < questStates.Count; i++)
        {
            QuestRuntimeState state = questStates[i];
            if (state == null || !state.GetDefinition() || state.GetStatus() == QuestStatus.Inactive)
                continue;

            visibleQuests.Add(state);
        }

        visibleQuests.Sort(CompareQuestStatesForPipBoyList);
        return visibleQuests;
    }

    private static int CompareQuestStatesForPipBoyList(QuestRuntimeState left, QuestRuntimeState right)
    {
        if (left == right)
            return 0;

        if (left == null)
            return 1;

        if (right == null)
            return -1;

        if (left.IsCurrentQuest() != right.IsCurrentQuest())
            return left.IsCurrentQuest() ? -1 : 1;

        int leftStatusRank = GetQuestStatusSortRank(left.GetStatus());
        int rightStatusRank = GetQuestStatusSortRank(right.GetStatus());
        if (leftStatusRank != rightStatusRank)
            return leftStatusRank.CompareTo(rightStatusRank);

        QuestDefinition leftDefinition = left.GetDefinition();
        QuestDefinition rightDefinition = right.GetDefinition();
        int priorityCompare = rightDefinition.GetPriority().CompareTo(leftDefinition.GetPriority());
        if (priorityCompare != 0)
            return priorityCompare;

        return string.Compare(
            leftDefinition.GetDisplayName(),
            rightDefinition.GetDisplayName(),
            System.StringComparison.OrdinalIgnoreCase);
    }

    private static int GetQuestStatusSortRank(QuestStatus status)
    {
        return status switch
        {
            QuestStatus.Active => 0,
            QuestStatus.Completed => 1,
            QuestStatus.Failed => 2,
            _ => 3
        };
    }

    private QuestRuntimeState GetQuestStateForObjectiveDetails(System.Collections.Generic.List<QuestRuntimeState> visibleQuests)
    {
        if (hoveredQuestState != null && visibleQuests != null && visibleQuests.Contains(hoveredQuestState))
            return hoveredQuestState;

        if (questController)
        {
            QuestRuntimeState currentQuest = questController.GetCurrentQuest();
            if (currentQuest != null && currentQuest.GetStatus() != QuestStatus.Inactive)
                return currentQuest;
        }

        return visibleQuests != null && visibleQuests.Count > 0 ? visibleQuests[0] : null;
    }

    private string GetInventoryEntrySortName(PlayerInventory.InventoryEntry inventoryEntry)
    {
        if (inventoryEntry == null) return string.Empty;

        ScriptableObject itemDefinition = inventoryEntry.GetItemDefinition();
        if (!itemDefinition) return string.Empty;

        string displayName = GetDisplayNameFromDefinition(itemDefinition, itemDefinition.name);
        if (string.IsNullOrWhiteSpace(displayName))
            displayName = itemDefinition.name;

        return displayName ?? string.Empty;
    }

    public void EquipWeaponFromInventoryEntry(PlayerInventory.InventoryEntry inventoryEntry)
    {
        // Stop if the inventory entry is missing.
        if (inventoryEntry == null) return;

        // Selecting the same weapon instance toggles back to default unarmed.
        if (selectedWeaponInventoryEntry == inventoryEntry)
        {
            PersistSelectedWeaponMagazineFromController(inventoryEntry);
            UnequipCurrentWeaponToUnarmed();
            selectedWeaponInventoryEntry = null;
            RefreshWeaponEntrySelectionIndicators();
            return;
        }

        // Persist currently selected weapon magazine before switching to another weapon instance.
        if (selectedWeaponInventoryEntry != null)
            PersistSelectedWeaponMagazineFromController(selectedWeaponInventoryEntry);

        // When switching weapons in combat mode, force the current weapon out of hand first.
        if (!playerState)
            playerState = FindAnyObjectByType<PlayerState>();

        if (!playerWeaponController)
            playerWeaponController = FindAnyObjectByType<PlayerWeaponController>();

        bool shouldClearWeaponInHand = playerState
            && playerState.GetCombatMode()
            && playerState.GetWeaponInHand();

        if (shouldClearWeaponInHand)
        {
            // Defer equip until Pip-Boy closes so the new weapon equips from holstered state.
            pendingWeaponInventoryEntry = inventoryEntry;
            hasPendingWeaponEquipOnClose = true;
            return;
        }

        bool equipped = EquipWeaponFromItemDefinition(inventoryEntry.GetItemDefinition());
        if (equipped)
        {
            selectedWeaponInventoryEntry = inventoryEntry;
            ApplySelectedWeaponInstanceToController(inventoryEntry);
        }

        RefreshWeaponEntrySelectionIndicators();
    }

    private void ApplyPendingWeaponEquipOnClose()
    {
        if (!hasPendingWeaponEquipOnClose) return;

        hasPendingWeaponEquipOnClose = false;

        PlayerInventory.InventoryEntry pendingEntry = pendingWeaponInventoryEntry;
        pendingWeaponInventoryEntry = null;

        if (pendingEntry == null) return;

        if (!playerState)
            playerState = FindAnyObjectByType<PlayerState>();

        if (!playerWeaponController)
            playerWeaponController = FindAnyObjectByType<PlayerWeaponController>();

        // Force old in-hand visuals off and restart handoff from holstered timing.
        if (playerWeaponController)
            playerWeaponController.HideEquippedWeaponInHandImmediate();

        if (playerState)
            playerState.SetWeaponInHand(false);

        bool equipped = EquipWeaponFromItemDefinition(pendingEntry.GetItemDefinition());
        if (equipped)
        {
            selectedWeaponInventoryEntry = pendingEntry;
            ApplySelectedWeaponInstanceToController(pendingEntry);
        }

        RefreshWeaponEntrySelectionIndicators();
    }

    public bool EquipWeaponFromItemDefinition(ScriptableObject itemDefinition)
    {
        // Stop if this item is missing or not a weapon.
        if (!(itemDefinition is WeaponDefinition weaponDefinition)) return false;

        // Auto-find PlayerWeaponController if not set.
        if (!playerWeaponController)
            playerWeaponController = FindAnyObjectByType<PlayerWeaponController>();

        // Stop if weapon controller is still missing.
        if (!playerWeaponController) return false;

        string displayName = weaponDefinition.GetDisplayName();
        string assetName = itemDefinition.name;

        // Try display name first, then asset name as a fallback.
        bool equipped = playerWeaponController.TryEquipWeaponByName(displayName);
        if (!equipped)
            equipped = playerWeaponController.TryEquipWeaponByName(assetName);

        return equipped;
    }

    private void UnequipCurrentWeaponToUnarmed()
    {
        // Auto-find PlayerWeaponController if not set.
        if (!playerWeaponController)
            playerWeaponController = FindAnyObjectByType<PlayerWeaponController>();

        if (!playerWeaponController) return;

        // Revert to the default fallback when no specific weapon is selected.
        playerWeaponController.TryEquipUnarmed();
    }

    private void ApplySelectedWeaponInstanceToController(PlayerInventory.InventoryEntry inventoryEntry)
    {
        // Stop if entry is missing.
        if (inventoryEntry == null) return;

        // Stop if this is not a weapon entry.
        if (!(inventoryEntry.GetItemDefinition() is WeaponDefinition weaponDefinition)) return;

        if (!playerWeaponController)
            playerWeaponController = FindAnyObjectByType<PlayerWeaponController>();

        if (!playerInventory)
            playerInventory = FindAnyObjectByType<PlayerInventory>();

        // Stop if dependencies are missing.
        if (!playerWeaponController || !playerInventory) return;

        string instanceId = playerInventory.GetInstanceId(inventoryEntry, 0);
        int loadedMagazineRounds = Mathf.Max(0, playerInventory.GetInstanceLoadedMagazineRounds(inventoryEntry, 0));
        int reserveAmmo = 0;
        AmmoDefinition ammoType = weaponDefinition.GetAmmoType();
        if (ammoType != null)
            reserveAmmo = Mathf.Max(0, playerInventory.GetAmmoCount(ammoType));

        playerWeaponController.SetEquippedInventoryWeaponInstanceId(instanceId);
        playerWeaponController.SetCurrentWeaponAmmo(loadedMagazineRounds);
        playerWeaponController.SetCurrentWeaponReserveAmmo(reserveAmmo);
    }

    private void PersistSelectedWeaponMagazineFromController(PlayerInventory.InventoryEntry inventoryEntry)
    {
        // Stop if entry is missing.
        if (inventoryEntry == null) return;

        // Stop if this is not a weapon entry.
        if (!(inventoryEntry.GetItemDefinition() is WeaponDefinition weaponDefinition)) return;

        if (!playerWeaponController)
            playerWeaponController = FindAnyObjectByType<PlayerWeaponController>();

        if (!playerInventory)
            playerInventory = FindAnyObjectByType<PlayerInventory>();

        // Stop if dependencies are missing.
        if (!playerWeaponController || !playerInventory) return;

        // Stop if this inventory entry is not the currently equipped weapon definition.
        PlayerWeaponController.WeaponEntry currentWeaponEntry = playerWeaponController.GetCurrentWeapon();
        if (currentWeaponEntry == null) return;

        string equippedWeaponName = currentWeaponEntry.WeaponName ?? string.Empty;
        bool matchesDisplayName = string.Equals(
            equippedWeaponName,
            weaponDefinition.GetDisplayName(),
            System.StringComparison.OrdinalIgnoreCase);
        bool matchesItemId = string.Equals(
            equippedWeaponName,
            weaponDefinition.GetItemId(),
            System.StringComparison.OrdinalIgnoreCase);
        bool matchesAssetName = string.Equals(
            equippedWeaponName,
            weaponDefinition.name,
            System.StringComparison.OrdinalIgnoreCase);

        if (!matchesDisplayName && !matchesItemId && !matchesAssetName)
            return;

        // When controller is bound to this exact instance, combat already persists loaded rounds directly to inventory.
        string entryInstanceId = playerInventory.GetInstanceId(inventoryEntry, 0);
        string boundInstanceId = playerWeaponController.GetEquippedInventoryWeaponInstanceId();
        if (!string.IsNullOrWhiteSpace(entryInstanceId) && entryInstanceId == boundInstanceId)
            return;

        int loadedMagazineRounds = Mathf.Max(0, playerWeaponController.GetCurrentWeaponAmmo());
        playerInventory.SetInstanceLoadedMagazineRounds(inventoryEntry, 0, loadedMagazineRounds);
    }

    private void ClearSpawnedWeaponEntries()
    {
        SetPooledEntriesActiveCount(spawnedWeaponEntryButtons, 0);
    }

    private void ClearSpawnedAidEntries()
    {
        SetPooledEntriesActiveCount(spawnedAidEntryButtons, 0);
    }

    private void ClearSpawnedMiscEntries()
    {
        SetPooledEntriesActiveCount(spawnedMiscEntryButtons, 0);
    }

    private void ClearSpawnedAmmoEntries()
    {
        SetPooledEntriesActiveCount(spawnedAmmoEntryButtons, 0);
    }

    private void EnsurePooledEntries(
        System.Collections.Generic.List<GameObject> pooledEntries,
        int requiredCount,
        GameObject entryPrefab,
        Transform listParent)
    {
        if (pooledEntries == null || entryPrefab == null || listParent == null)
            return;

        for (int i = 0; i < pooledEntries.Count; i++)
        {
            GameObject pooledEntry = pooledEntries[i];
            if (!pooledEntry) continue;

            if (pooledEntry.transform.parent != listParent)
                pooledEntry.transform.SetParent(listParent, false);
        }

        while (pooledEntries.Count < requiredCount)
        {
            GameObject spawnedEntry = Instantiate(entryPrefab, listParent);
            ApplyPipBoyPaletteColorOverrides(spawnedEntry);
            pooledEntries.Add(spawnedEntry);
        }
    }

    private void SetPooledEntriesActiveCount(System.Collections.Generic.List<GameObject> pooledEntries, int activeCount)
    {
        if (pooledEntries == null) return;

        for (int i = 0; i < pooledEntries.Count; i++)
            SetActiveSafe(pooledEntries[i], i < activeCount);
    }

    private PipBoyListEntryState ResolveEntryState(GameObject spawnedEntry)
    {
        if (!spawnedEntry) return null;

        if (!pooledEntryStateByObject.TryGetValue(spawnedEntry, out PipBoyListEntryState state))
        {
            state = new PipBoyListEntryState();
            pooledEntryStateByObject[spawnedEntry] = state;
        }

        if (!state.EntryButton)
            state.EntryButton = spawnedEntry.GetComponent<Button>() ??
                                spawnedEntry.GetComponentInChildren<Button>(true);

        if (!state.EntryLabel)
            state.EntryLabel = spawnedEntry.GetComponentInChildren<TextMeshProUGUI>(true);

        if (state.EntryLabel)
            state.EntryLabel.raycastTarget = false;

        if (state.EntryButton && !state.EntryEventTrigger)
            state.EntryEventTrigger = state.EntryButton.GetComponent<EventTrigger>() ??
                                      state.EntryButton.gameObject.AddComponent<EventTrigger>();

        if (state.EntryEventTrigger && state.EntryEventTrigger.triggers == null)
            state.EntryEventTrigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();

        return state;
    }

    private void BindWeaponEntryButton(GameObject spawnedEntry, PlayerInventory.InventoryEntry inventoryEntry, string entryLabel)
    {
        // Stop if the entry is missing.
        if (!spawnedEntry) return;

        // Stop if the inventory entry is missing.
        if (inventoryEntry == null) return;

        PipBoyListEntryState state = ResolveEntryState(spawnedEntry);
        if (state == null) return;

        // Stop if no Button exists on the spawned entry.
        if (!state.EntryButton) return;

        if (state.EntryLabel)
            SetTextIfChanged(state.EntryLabel, entryLabel);

        bool hasMatchingBinding =
            state.BoundMode == WeaponRowBindingMode &&
            state.BoundEntry == inventoryEntry &&
            state.BoundLabel == entryLabel;

        if (hasMatchingBinding)
            return;

        state.EntryButton.onClick.RemoveAllListeners();
        state.EntryButton.onClick.AddListener(() => EquipWeaponFromInventoryEntry(inventoryEntry));

        EventTrigger entryEventTrigger = state.EntryEventTrigger;
        if (!entryEventTrigger) return;

        entryEventTrigger.triggers.Clear();

        EventTrigger.Entry pointerEnterEntry = new EventTrigger.Entry();
        pointerEnterEntry.eventID = EventTriggerType.PointerEnter;
        pointerEnterEntry.callback.AddListener(_ => OnWeaponEntryPointerEnter(spawnedEntry, inventoryEntry));
        entryEventTrigger.triggers.Add(pointerEnterEntry);

        EventTrigger.Entry pointerExitEntry = new EventTrigger.Entry();
        pointerExitEntry.eventID = EventTriggerType.PointerExit;
        pointerExitEntry.callback.AddListener(_ => ClearHoveredWeaponItemStats());
        entryEventTrigger.triggers.Add(pointerExitEntry);

        AddScrollForwardingTrigger(entryEventTrigger, weaponsScrollRect);

        state.BoundMode = WeaponRowBindingMode;
        state.BoundEntry = inventoryEntry;
        state.BoundLabel = entryLabel ?? string.Empty;
    }

    private void BindMiscEntryButton(GameObject spawnedEntry, PlayerInventory.InventoryEntry inventoryEntry, string entryLabel)
    {
        // Stop if the entry is missing.
        if (!spawnedEntry) return;

        // Stop if the inventory entry is missing.
        if (inventoryEntry == null) return;

        PipBoyListEntryState state = ResolveEntryState(spawnedEntry);
        if (state == null) return;

        // Stop if no Button exists on the spawned entry.
        if (!state.EntryButton) return;

        if (state.EntryLabel)
        {
            ApplyEntryLabelHorizontalOffset(state.EntryLabel, AidMiscAmmoEntryTextHorizontalOffsetPixels);
            SetTextIfChanged(state.EntryLabel, entryLabel);
        }

        bool hasMatchingBinding =
            state.BoundMode == MiscRowBindingMode &&
            state.BoundEntry == inventoryEntry &&
            state.BoundLabel == entryLabel;

        if (hasMatchingBinding)
            return;

        state.EntryButton.onClick.RemoveAllListeners();

        EventTrigger entryEventTrigger = state.EntryEventTrigger;
        if (!entryEventTrigger) return;

        entryEventTrigger.triggers.Clear();

        EventTrigger.Entry pointerEnterEntry = new EventTrigger.Entry();
        pointerEnterEntry.eventID = EventTriggerType.PointerEnter;
        pointerEnterEntry.callback.AddListener(_ => OnMiscEntryPointerEnter(spawnedEntry, inventoryEntry));
        entryEventTrigger.triggers.Add(pointerEnterEntry);

        EventTrigger.Entry pointerExitEntry = new EventTrigger.Entry();
        pointerExitEntry.eventID = EventTriggerType.PointerExit;
        pointerExitEntry.callback.AddListener(_ => ClearHoveredMiscItemStats());
        entryEventTrigger.triggers.Add(pointerExitEntry);

        AddScrollForwardingTrigger(entryEventTrigger, miscScrollRect);

        state.BoundMode = MiscRowBindingMode;
        state.BoundEntry = inventoryEntry;
        state.BoundLabel = entryLabel ?? string.Empty;
    }

    private void BindAidEntryButton(GameObject spawnedEntry, PlayerInventory.InventoryEntry inventoryEntry, string entryLabel)
    {
        // Stop if the entry is missing.
        if (!spawnedEntry) return;

        // Stop if the inventory entry is missing.
        if (inventoryEntry == null) return;

        PipBoyListEntryState state = ResolveEntryState(spawnedEntry);
        if (state == null) return;

        // Stop if no Button exists on the spawned entry.
        if (!state.EntryButton) return;

        if (state.EntryLabel)
        {
            ApplyEntryLabelHorizontalOffset(state.EntryLabel, AidMiscAmmoEntryTextHorizontalOffsetPixels);
            SetTextIfChanged(state.EntryLabel, entryLabel);
        }

        bool hasMatchingBinding =
            state.BoundMode == AidRowBindingMode &&
            state.BoundEntry == inventoryEntry &&
            state.BoundLabel == entryLabel;

        if (hasMatchingBinding)
            return;

        state.EntryButton.onClick.RemoveAllListeners();
        state.EntryButton.onClick.AddListener(() => ConsumeAidFromInventoryEntry(inventoryEntry));

        EventTrigger entryEventTrigger = state.EntryEventTrigger;
        if (!entryEventTrigger) return;

        entryEventTrigger.triggers.Clear();

        EventTrigger.Entry pointerEnterEntry = new EventTrigger.Entry();
        pointerEnterEntry.eventID = EventTriggerType.PointerEnter;
        pointerEnterEntry.callback.AddListener(_ => OnAidEntryPointerEnter(spawnedEntry, inventoryEntry));
        entryEventTrigger.triggers.Add(pointerEnterEntry);

        EventTrigger.Entry pointerExitEntry = new EventTrigger.Entry();
        pointerExitEntry.eventID = EventTriggerType.PointerExit;
        pointerExitEntry.callback.AddListener(_ => ClearHoveredAidItemStats());
        entryEventTrigger.triggers.Add(pointerExitEntry);

        AddScrollForwardingTrigger(entryEventTrigger, aidScrollRect);

        state.BoundMode = AidRowBindingMode;
        state.BoundEntry = inventoryEntry;
        state.BoundLabel = entryLabel ?? string.Empty;
    }

    private void ConsumeAidFromInventoryEntry(PlayerInventory.InventoryEntry inventoryEntry)
    {
        // Stop if entry is missing.
        if (inventoryEntry == null) return;

        if (!playerAidSystem)
            playerAidSystem = FindAnyObjectByType<PlayerAid>();

        if (!playerAidSystem && playerState)
            playerAidSystem = playerState.GetComponent<PlayerAid>() ?? playerState.gameObject.AddComponent<PlayerAid>();

        // Stop if no aid system can be resolved.
        if (!playerAidSystem)
        {
            Debug.LogWarning("Cannot consume aid item because no Aid component was found in the scene.");
            return;
        }

        bool consumed = playerAidSystem.TryConsumeInventoryEntry(inventoryEntry);
        if (!consumed) return;

        if (hoveredAidInventoryEntry == inventoryEntry)
            ClearHoveredAidItemStats();

        RefreshStatsPlayerTexts();
    }

    private void BindAmmoEntryButton(GameObject spawnedEntry, PlayerInventory.InventoryEntry inventoryEntry, string entryLabel)
    {
        // Stop if the entry is missing.
        if (!spawnedEntry) return;

        // Stop if the inventory entry is missing.
        if (inventoryEntry == null) return;

        PipBoyListEntryState state = ResolveEntryState(spawnedEntry);
        if (state == null) return;

        // Stop if no Button exists on the spawned entry.
        if (!state.EntryButton) return;

        if (state.EntryLabel)
        {
            ApplyEntryLabelHorizontalOffset(state.EntryLabel, AidMiscAmmoEntryTextHorizontalOffsetPixels);
            SetTextIfChanged(state.EntryLabel, entryLabel);
        }

        bool hasMatchingBinding =
            state.BoundMode == AmmoRowBindingMode &&
            state.BoundEntry == inventoryEntry &&
            state.BoundLabel == entryLabel;

        if (hasMatchingBinding)
            return;

        state.EntryButton.onClick.RemoveAllListeners();

        EventTrigger entryEventTrigger = state.EntryEventTrigger;
        if (!entryEventTrigger) return;

        entryEventTrigger.triggers.Clear();

        EventTrigger.Entry pointerEnterEntry = new EventTrigger.Entry();
        pointerEnterEntry.eventID = EventTriggerType.PointerEnter;
        pointerEnterEntry.callback.AddListener(_ => OnAmmoEntryPointerEnter(spawnedEntry, inventoryEntry));
        entryEventTrigger.triggers.Add(pointerEnterEntry);

        EventTrigger.Entry pointerExitEntry = new EventTrigger.Entry();
        pointerExitEntry.eventID = EventTriggerType.PointerExit;
        pointerExitEntry.callback.AddListener(_ => ClearHoveredAmmoItemStats());
        entryEventTrigger.triggers.Add(pointerExitEntry);

        AddScrollForwardingTrigger(entryEventTrigger, ammoScrollRect);

        state.BoundMode = AmmoRowBindingMode;
        state.BoundEntry = inventoryEntry;
        state.BoundLabel = entryLabel ?? string.Empty;
    }

    private void BindQuestEntryButton(GameObject spawnedEntry, QuestRuntimeState questState)
    {
        if (!spawnedEntry || questState == null || !questState.GetDefinition())
            return;

        PipBoyListEntryState state = ResolveEntryState(spawnedEntry);
        if (state == null || !state.EntryButton)
            return;

        state.EntryButton.transition = Selectable.Transition.None;

        string entryLabel = questState.GetDefinition().GetDisplayName();
        if (string.IsNullOrWhiteSpace(entryLabel))
            entryLabel = questState.GetDefinition().GetQuestId();

        bool isCompleted = questState.GetStatus() == QuestStatus.Completed || questState.GetStatus() == QuestStatus.Failed;
        float rowAlpha = isCompleted ? completedQuestEntryAlpha : 1.0f;

        ConfigureQuestEntryLabel(state.EntryLabel, entryLabel);
        HideQuestEntryRuntimeBox(state);
        HideQuestEntryButtonBackground(state);
        ApplyQuestEntryRowAlpha(spawnedEntry, rowAlpha);

        bool hasMatchingBinding =
            state.BoundMode == QuestRowBindingMode &&
            state.BoundQuestState == questState &&
            state.BoundLabel == entryLabel;

        state.EntryButton.onClick.RemoveAllListeners();
        if (questState.GetStatus() == QuestStatus.Active)
            state.EntryButton.onClick.AddListener(() => SelectQuestFromPipBoy(questState));

        EventTrigger entryEventTrigger = state.EntryEventTrigger;
        if (!entryEventTrigger)
            return;

        entryEventTrigger.triggers.Clear();

        EventTrigger.Entry pointerEnterEntry = new EventTrigger.Entry();
        pointerEnterEntry.eventID = EventTriggerType.PointerEnter;
        pointerEnterEntry.callback.AddListener(_ => OnQuestEntryPointerEnter(spawnedEntry, questState));
        entryEventTrigger.triggers.Add(pointerEnterEntry);

        EventTrigger.Entry pointerExitEntry = new EventTrigger.Entry();
        pointerExitEntry.eventID = EventTriggerType.PointerExit;
        pointerExitEntry.callback.AddListener(_ => OnQuestEntryPointerExit(questState));
        entryEventTrigger.triggers.Add(pointerExitEntry);

        AddScrollForwardingTrigger(entryEventTrigger, questsScrollRect);

        if (hasMatchingBinding)
            return;

        state.BoundMode = QuestRowBindingMode;
        state.BoundEntry = null;
        state.BoundQuestState = questState;
        state.BoundLabel = entryLabel ?? string.Empty;
    }

    private void ApplyQuestEntryRowAlpha(GameObject spawnedEntry, float alpha)
    {
        if (!spawnedEntry)
            return;

        TMP_Text[] textElements = spawnedEntry.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < textElements.Length; i++)
        {
            TMP_Text textElement = textElements[i];
            if (!textElement)
                continue;

            Color color = textElement.color;
            color.a = alpha;
            textElement.color = color;
        }
    }

    private void HideQuestEntryButtonBackground(PipBoyListEntryState state)
    {
        if (state == null || !state.EntryButton)
            return;

        Graphic targetGraphic = state.EntryButton.targetGraphic;
        if (!targetGraphic || targetGraphic is TMP_Text)
            return;

        Color color = targetGraphic.color;
        color.a = 0.0f;
        targetGraphic.color = color;
        targetGraphic.raycastTarget = true;
    }

    private void HideQuestEntryRuntimeBox(PipBoyListEntryState state)
    {
        if (state == null || !state.RuntimeBoxRect)
            return;

        SetActiveSafe(state.RuntimeBoxRect.gameObject, false);
    }

    private void ConfigureQuestEntryLabel(TextMeshProUGUI entryLabel, string entryLabelText)
    {
        if (!entryLabel)
            return;

        RectTransform labelRectTransform = entryLabel.rectTransform;
        if (labelRectTransform)
        {
            Vector2 offsetMin = labelRectTransform.offsetMin;
            Vector2 offsetMax = labelRectTransform.offsetMax;
            offsetMin.x = GetQuestBoxSize().x + questEntryBoxTextGap;
            offsetMax.x = 0.0f;
            labelRectTransform.offsetMin = offsetMin;
            labelRectTransform.offsetMax = offsetMax;
        }

        entryLabel.raycastTarget = false;
        entryLabel.fontSize = QuestEntryFontSize;
        entryLabel.textWrappingMode = TextWrappingModes.Normal;
        entryLabel.overflowMode = TextOverflowModes.Overflow;
        entryLabel.alignment = TextAlignmentOptions.Left;
        SetTextIfChanged(entryLabel, entryLabelText);

        RectTransform rowRectTransform = entryLabel.GetComponentInParent<Button>(true)
            ? entryLabel.GetComponentInParent<Button>(true).transform as RectTransform
            : null;
        if (rowRectTransform)
            ConfigureQuestEntryPreferredHeight(rowRectTransform, entryLabel);
    }

    private void ConfigureQuestEntryPreferredHeight(RectTransform rowRectTransform, TextMeshProUGUI entryLabel)
    {
        if (!rowRectTransform || !entryLabel)
            return;

        LayoutElement layoutElement = rowRectTransform.GetComponent<LayoutElement>() ?? rowRectTransform.gameObject.AddComponent<LayoutElement>();
        float availableWidth = Mathf.Max(1.0f, GetQuestEntryVisualWidth(rowRectTransform) - GetQuestBoxSize().x - questEntryBoxTextGap);
        Vector2 preferredValues = entryLabel.GetPreferredValues(entryLabel.text, availableWidth, 0.0f);
        float preferredHeight = Mathf.Max(rowRectTransform.sizeDelta.y, entryLabel.fontSize + 6.0f, preferredValues.y);
        layoutElement.preferredHeight = preferredHeight;
        layoutElement.minHeight = preferredHeight;
    }

    private void ConfigureQuestEntryBox(GameObject spawnedEntry, PipBoyListEntryState state, bool filled, float alpha)
    {
        if (!spawnedEntry || state == null)
            return;

        RectTransform entryRectTransform = spawnedEntry.transform as RectTransform;
        if (!entryRectTransform)
            return;

        if (!state.RuntimeBoxRect)
            state.RuntimeBoxRect = CreateQuestBox("QuestEntryBox", entryRectTransform);

        if (!state.RuntimeBoxRect)
            return;

        Vector2 boxSize = GetQuestBoxSize();
        state.RuntimeBoxRect.sizeDelta = boxSize;
        state.RuntimeBoxRect.anchorMin = new Vector2(0.0f, 1.0f);
        state.RuntimeBoxRect.anchorMax = new Vector2(0.0f, 1.0f);
        state.RuntimeBoxRect.pivot = new Vector2(0.5f, 0.5f);
        state.RuntimeBoxRect.localScale = Vector3.one;
        state.RuntimeBoxRect.localRotation = Quaternion.identity;

        RectTransform listParentRect = entryRectTransform.parent as RectTransform;
        RectTransform entryLabelRectTransform = state.EntryLabel ? state.EntryLabel.rectTransform : null;
        if (TryGetQuestEntrySelectionIndicatorWorldPosition(
                entryRectTransform,
                entryLabelRectTransform,
                listParentRect,
                out Vector3 boxWorldPosition))
        {
            state.RuntimeBoxRect.position = boxWorldPosition;
        }
        else
        {
            state.RuntimeBoxRect.anchoredPosition = new Vector2(boxSize.x * 0.5f, -GetQuestEntryFirstLineCenterY(spawnedEntry));
        }

        SetQuestBoxVisual(state.RuntimeBoxRect, filled, alpha);
    }

    private float GetQuestEntryFirstLineCenterY(GameObject spawnedEntry)
    {
        TextMeshProUGUI label = spawnedEntry ? spawnedEntry.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        if (!label)
            return GetQuestBoxSize().y * 0.5f;

        return Mathf.Max(GetQuestBoxSize().y * 0.5f, label.fontSize * 0.55f);
    }

    private void SelectQuestFromPipBoy(QuestRuntimeState questState)
    {
        if (questState == null || !questState.GetDefinition() || questState.GetStatus() != QuestStatus.Active)
            return;

        if (!questController)
            questController = QuestController.FindOrCreate();

        if (questController && questController.SetCurrentQuest(questState.GetDefinition()))
        {
            MarkQuestsListDirty();
            RefreshVisibleQuestList();
        }
    }

    private void OnQuestEntryPointerEnter(GameObject spawnedEntry, QuestRuntimeState questState)
    {
        hoveredQuestState = questState;
        RefreshQuestObjectiveDetails(questState);
        UpdateHoveredQuestButtonHighlight(spawnedEntry);
    }

    private void OnQuestEntryPointerExit(QuestRuntimeState questState)
    {
        if (hoveredQuestState == questState)
            hoveredQuestState = null;

        ClearHoveredQuestEntryHighlight();
        MarkQuestsListDirty();
    }

    private void ClearHoveredQuest()
    {
        hoveredQuestState = null;
        ClearHoveredQuestEntryHighlight();
        RefreshQuestObjectiveDetails(null);
    }

    private void RefreshQuestObjectiveDetails(QuestRuntimeState questState)
    {
        HideQuestTemplateObjects();

        if (!dataQuestsPanel)
            return;

        SetPooledEntriesActiveCount(spawnedQuestObjectiveRows, 0);

        if (questState == null || !questState.GetDefinition() || !questObjectiveTextTemplate)
            return;

        QuestDefinition definition = questState.GetDefinition();
        System.Collections.Generic.List<QuestObjectiveDefinition> objectiveDefinitions = definition.GetObjectives();
        if (objectiveDefinitions == null || objectiveDefinitions.Count == 0)
            return;

        RectTransform templateRectTransform = questObjectiveTextTemplate.rectTransform;
        if (!templateRectTransform)
            return;

        Vector2 boxSize = GetQuestBoxSize();
        float rowStartX = GetQuestObjectiveRowStartX(templateRectTransform, boxSize);
        float rowStartY = GetQuestObjectiveRowStartY(templateRectTransform);
        float objectiveTextWidth = GetQuestObjectiveTextWidth(templateRectTransform, rowStartX, boxSize);
        float yOffset = 0.0f;
        int visibleObjectiveCount = 0;

        for (int i = 0; i < objectiveDefinitions.Count; i++)
        {
            QuestObjectiveDefinition objectiveDefinition = objectiveDefinitions[i];
            if (objectiveDefinition == null)
                continue;

            QuestObjectiveRuntimeState objectiveState = questState.GetObjectiveState(objectiveDefinition.GetObjectiveId());
            if (objectiveState == null)
                continue;

            QuestObjectiveState state = objectiveState.GetState();
            if (state == QuestObjectiveState.Hidden || state == QuestObjectiveState.Failed)
                continue;

            GameObject rowObject = GetOrCreateQuestObjectiveRow(visibleObjectiveCount);
            if (!rowObject)
                continue;

            RectTransform rowRectTransform = rowObject.transform as RectTransform;
            TMP_Text rowText = rowObject.GetComponentInChildren<TMP_Text>(true);
            RectTransform boxRectTransform = FindChildComponentByNameInRoot<RectTransform>("QuestObjectiveBox", rowObject.transform);
            CanvasGroup canvasGroup = rowObject.GetComponent<CanvasGroup>() ?? rowObject.AddComponent<CanvasGroup>();

            bool isCompleted = state == QuestObjectiveState.Completed;
            bool isCurrent = state == QuestObjectiveState.Displayed &&
                             objectiveDefinition.GetObjectiveId() == questState.GetCurrentObjectiveId();
            float alpha = isCompleted ? completedQuestObjectiveAlpha : 1.0f;

            SetActiveSafe(rowObject, true);
            canvasGroup.alpha = alpha;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            if (rowRectTransform)
            {
                rowRectTransform.anchorMin = new Vector2(0.0f, 1.0f);
                rowRectTransform.anchorMax = new Vector2(0.0f, 1.0f);
                rowRectTransform.pivot = new Vector2(0.0f, 1.0f);
                rowRectTransform.anchoredPosition = new Vector2(rowStartX, rowStartY - yOffset);
                rowRectTransform.localScale = Vector3.one;
                rowRectTransform.localRotation = Quaternion.identity;
            }

            if (rowText)
            {
                rowText.text = objectiveDefinition.GetDisplayText();
                rowText.raycastTarget = false;
                rowText.textWrappingMode = TextWrappingModes.Normal;
                rowText.overflowMode = TextOverflowModes.Overflow;

                RectTransform textRectTransform = rowText.rectTransform;
                textRectTransform.anchorMin = new Vector2(0.0f, 1.0f);
                textRectTransform.anchorMax = new Vector2(0.0f, 1.0f);
                textRectTransform.pivot = new Vector2(0.0f, 1.0f);
                textRectTransform.anchoredPosition = new Vector2(boxSize.x + questObjectiveBoxTextGap, 0.0f);
                textRectTransform.sizeDelta = new Vector2(objectiveTextWidth, templateRectTransform.sizeDelta.y);
                textRectTransform.localScale = Vector3.one;
                textRectTransform.localRotation = Quaternion.identity;
            }

            if (boxRectTransform)
            {
                bool showBox = isCompleted || isCurrent;
                SetActiveSafe(boxRectTransform.gameObject, showBox);
                if (showBox)
                {
                    boxRectTransform.anchorMin = new Vector2(0.0f, 1.0f);
                    boxRectTransform.anchorMax = new Vector2(0.0f, 1.0f);
                    boxRectTransform.pivot = new Vector2(0.5f, 0.5f);
                    boxRectTransform.sizeDelta = boxSize;
                    boxRectTransform.anchoredPosition = new Vector2(boxSize.x * 0.5f, -GetQuestObjectiveFirstLineCenterY(rowText));
                    boxRectTransform.localScale = Vector3.one;
                    boxRectTransform.localRotation = Quaternion.identity;
                    SetQuestBoxVisual(boxRectTransform, isCompleted, 1.0f);
                }
            }

            float rowHeight = CalculateQuestObjectiveRowHeight(rowText, objectiveTextWidth);
            if (rowRectTransform)
                rowRectTransform.sizeDelta = new Vector2(boxSize.x + questObjectiveBoxTextGap + objectiveTextWidth, rowHeight);

            yOffset += rowHeight + questObjectiveVerticalSpacing;
            visibleObjectiveCount++;
        }

        SetPooledEntriesActiveCount(spawnedQuestObjectiveRows, visibleObjectiveCount);
    }

    private GameObject GetOrCreateQuestObjectiveRow(int rowIndex)
    {
        if (rowIndex < 0 || !dataQuestsPanel || !questObjectiveTextTemplate)
            return null;

        while (spawnedQuestObjectiveRows.Count <= rowIndex)
        {
            GameObject rowObject = new GameObject("QuestObjectiveRow", typeof(RectTransform), typeof(CanvasGroup));
            rowObject.transform.SetParent(dataQuestsPanel.transform, false);

            GameObject textObject = Instantiate(questObjectiveTextTemplate.gameObject, rowObject.transform);
            textObject.name = "ObjectiveTextRuntime";
            SetActiveSafe(textObject, true);

            RectTransform boxRectTransform = CreateQuestBox("QuestObjectiveBox", rowObject.transform as RectTransform);
            SetActiveSafe(boxRectTransform ? boxRectTransform.gameObject : null, false);

            ApplyPipBoyPaletteColorOverrides(rowObject);
            spawnedQuestObjectiveRows.Add(rowObject);
        }

        return spawnedQuestObjectiveRows[rowIndex];
    }

    private float GetQuestObjectiveRowStartX(RectTransform templateRectTransform, Vector2 boxSize)
    {
        if (!templateRectTransform)
            return 0.0f;

        RectTransform parentRectTransform = dataQuestsPanel ? dataQuestsPanel.transform as RectTransform : null;
        if (!parentRectTransform)
            return templateRectTransform.anchoredPosition.x -
                   templateRectTransform.sizeDelta.x * templateRectTransform.pivot.x -
                   boxSize.x -
                   questObjectiveBoxTextGap;

        Rect parentRect = parentRectTransform.rect;
        float parentLeftEdgeX = -parentRect.width * parentRectTransform.pivot.x;

        if (TryGetQuestsScrollViewRightEdgeLocalX(parentRectTransform, out float scrollViewRightEdgeLocalX))
            return scrollViewRightEdgeLocalX + questObjectiveScrollViewGap - parentLeftEdgeX;

        if (TryGetRectLocalBounds(
                templateRectTransform,
                parentRectTransform,
                out float templateMinX,
                out _,
                out _,
                out _))
        {
            return templateMinX - boxSize.x - questObjectiveBoxTextGap - parentLeftEdgeX;
        }

        return templateRectTransform.anchoredPosition.x -
               templateRectTransform.sizeDelta.x * templateRectTransform.pivot.x -
               boxSize.x -
               questObjectiveBoxTextGap;
    }

    private float GetQuestObjectiveRowStartY(RectTransform templateRectTransform)
    {
        if (!templateRectTransform)
            return 0.0f;

        RectTransform parentRectTransform = dataQuestsPanel ? dataQuestsPanel.transform as RectTransform : null;
        if (parentRectTransform &&
            TryGetRectLocalBounds(
                templateRectTransform,
                parentRectTransform,
                out _,
                out _,
                out _,
                out float templateMaxY))
        {
            Rect parentRect = parentRectTransform.rect;
            float parentTopEdgeY = parentRect.height * (1.0f - parentRectTransform.pivot.y);
            return templateMaxY - parentTopEdgeY;
        }

        return templateRectTransform.anchoredPosition.y +
               templateRectTransform.sizeDelta.y * (1.0f - templateRectTransform.pivot.y);
    }

    private float GetQuestObjectiveTextWidth(RectTransform templateRectTransform, float rowStartX, Vector2 boxSize)
    {
        return RuntimeQuestObjectiveTextWidth;
    }

    private float CalculateQuestObjectiveRowHeight(TMP_Text rowText, float textWidth)
    {
        if (!rowText)
            return GetQuestBoxSize().y;

        Vector2 preferredValues = rowText.GetPreferredValues(rowText.text, Mathf.Max(1.0f, textWidth), 0.0f);
        return Mathf.Max(GetQuestBoxSize().y, rowText.fontSize + 4.0f, preferredValues.y);
    }

    private float GetQuestObjectiveFirstLineCenterY(TMP_Text rowText)
    {
        if (!rowText)
            return GetQuestBoxSize().y * 0.5f;

        return Mathf.Max(GetQuestBoxSize().y * 0.5f, rowText.fontSize * 0.55f);
    }

    private RectTransform CreateQuestBox(string objectName, RectTransform parent)
    {
        if (!parent)
            return null;

        GameObject boxObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform boxRectTransform = boxObject.GetComponent<RectTransform>();
        boxRectTransform.SetParent(parent, false);
        boxRectTransform.sizeDelta = GetQuestBoxSize();
        boxRectTransform.localScale = Vector3.one;
        boxRectTransform.localRotation = Quaternion.identity;

        Image image = boxObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = GetQuestBoxColor(1.0f);
        image.enabled = false;

        EnsureLayoutIgnored(boxObject);
        CreateQuestBoxOutlineSegment("Top", boxRectTransform);
        CreateQuestBoxOutlineSegment("Bottom", boxRectTransform);
        CreateQuestBoxOutlineSegment("Left", boxRectTransform);
        CreateQuestBoxOutlineSegment("Right", boxRectTransform);
        return boxRectTransform;
    }

    private void CreateQuestBoxOutlineSegment(string segmentName, RectTransform parent)
    {
        GameObject segmentObject = new GameObject(segmentName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        RectTransform segmentRectTransform = segmentObject.GetComponent<RectTransform>();
        segmentRectTransform.SetParent(parent, false);
        segmentRectTransform.localScale = Vector3.one;
        segmentRectTransform.localRotation = Quaternion.identity;

        Image image = segmentObject.GetComponent<Image>();
        image.raycastTarget = false;
        image.color = GetQuestBoxColor(1.0f);
    }

    private void SetQuestBoxVisual(RectTransform boxRectTransform, bool filled, float alpha)
    {
        if (!boxRectTransform)
            return;

        Image fillImage = boxRectTransform.GetComponent<Image>();
        if (fillImage)
        {
            fillImage.enabled = filled;
            fillImage.color = GetQuestBoxColor(filled ? alpha : 0.0f);
        }

        float thickness = Mathf.Max(1.0f, QuestBoxOutlineThickness);
        for (int i = 0; i < boxRectTransform.childCount; i++)
        {
            RectTransform segment = boxRectTransform.GetChild(i) as RectTransform;
            if (!segment)
                continue;

            Image segmentImage = segment.GetComponent<Image>();
            if (segmentImage)
            {
                segmentImage.enabled = !filled;
                segmentImage.color = GetQuestBoxColor(alpha);
            }

            SetActiveSafe(segment.gameObject, !filled);

            switch (segment.name)
            {
                case "Top":
                    segment.anchorMin = new Vector2(0.0f, 1.0f);
                    segment.anchorMax = new Vector2(1.0f, 1.0f);
                    segment.pivot = new Vector2(0.0f, 1.0f);
                    segment.anchoredPosition = Vector2.zero;
                    segment.sizeDelta = new Vector2(0.0f, thickness);
                    break;
                case "Bottom":
                    segment.anchorMin = new Vector2(0.0f, 0.0f);
                    segment.anchorMax = new Vector2(1.0f, 0.0f);
                    segment.pivot = new Vector2(0.0f, 0.0f);
                    segment.anchoredPosition = Vector2.zero;
                    segment.sizeDelta = new Vector2(0.0f, thickness);
                    break;
                case "Left":
                    segment.anchorMin = new Vector2(0.0f, 0.0f);
                    segment.anchorMax = new Vector2(0.0f, 1.0f);
                    segment.pivot = new Vector2(0.0f, 1.0f);
                    segment.anchoredPosition = Vector2.zero;
                    segment.sizeDelta = new Vector2(thickness, 0.0f);
                    break;
                case "Right":
                    segment.anchorMin = new Vector2(1.0f, 0.0f);
                    segment.anchorMax = new Vector2(1.0f, 1.0f);
                    segment.pivot = new Vector2(1.0f, 1.0f);
                    segment.anchoredPosition = Vector2.zero;
                    segment.sizeDelta = new Vector2(thickness, 0.0f);
                    break;
            }
        }
    }

    private Vector2 GetQuestBoxSize()
    {
        if (questSelectedBoxImage && questSelectedBoxImage.rectTransform)
        {
            Vector2 size = questSelectedBoxImage.rectTransform.sizeDelta;
            if (size.x > 0.0f && size.y > 0.0f)
                return size;
        }

        return new Vector2(4.0f, 4.0f);
    }

    private Color GetQuestBoxColor(float alpha)
    {
        Color color = questSelectedBoxImage ? questSelectedBoxImage.color : pipBoyLightColor;
        if (color.a <= 0.001f)
            color = pipBoyLightColor;

        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private static void ConfigureQuestTemplateObject(GameObject templateObject)
    {
        if (!templateObject)
            return;

        Graphic[] graphics = templateObject.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            if (graphics[i])
                graphics[i].raycastTarget = false;
        }

        EnsureLayoutIgnored(templateObject);
    }

    private void HideQuestTemplateObjects()
    {
        SetActiveSafe(questObjectiveTextTemplate ? questObjectiveTextTemplate.gameObject : null, false);
        if (questSelectedBoxImage && questSelectedBoxImage != currentQuestSelectedBoxImage)
            SetActiveSafe(questSelectedBoxImage.gameObject, false);
    }

    private void AddScrollForwardingTrigger(EventTrigger entryEventTrigger, ScrollRect scrollRect)
    {
        if (!entryEventTrigger || !scrollRect)
            return;

        EventTrigger.Entry scrollEntry = new EventTrigger.Entry();
        scrollEntry.eventID = EventTriggerType.Scroll;
        scrollEntry.callback.AddListener(eventData => ForwardScrollToScrollRect(eventData, scrollRect));
        entryEventTrigger.triggers.Add(scrollEntry);
    }

    private void ApplyEntryLabelHorizontalOffset(TextMeshProUGUI entryLabel, float horizontalOffsetPixels)
    {
        if (!entryLabel)
            return;

        RectTransform labelRectTransform = entryLabel.rectTransform;
        if (!labelRectTransform)
            return;

        if (!baseEntryLabelAnchoredXByRectTransform.TryGetValue(labelRectTransform, out float baseAnchoredX))
        {
            baseAnchoredX = labelRectTransform.anchoredPosition.x;
            baseEntryLabelAnchoredXByRectTransform[labelRectTransform] = baseAnchoredX;
        }

        Vector2 anchoredPosition = labelRectTransform.anchoredPosition;
        float targetAnchoredX = baseAnchoredX + horizontalOffsetPixels;
        if (Mathf.Approximately(anchoredPosition.x, targetAnchoredX))
            return;

        anchoredPosition.x = targetAnchoredX;
        labelRectTransform.anchoredPosition = anchoredPosition;
    }

    private void ApplyListEntryLeftPaddingOffset(Transform listParent, int leftPaddingOffsetPixels)
    {
        if (!listParent)
            return;

        VerticalLayoutGroup layoutGroup = listParent.GetComponent<VerticalLayoutGroup>();
        if (!layoutGroup)
            return;

        if (!baseLeftPaddingByLayoutGroup.TryGetValue(layoutGroup, out int baseLeftPadding))
        {
            baseLeftPadding = layoutGroup.padding != null ? layoutGroup.padding.left : 0;
            baseLeftPaddingByLayoutGroup[layoutGroup] = baseLeftPadding;
        }

        RectOffset padding = layoutGroup.padding ?? new RectOffset();
        int targetLeftPadding = Mathf.Max(0, baseLeftPadding + leftPaddingOffsetPixels);
        if (padding.left == targetLeftPadding)
            return;

        padding.left = targetLeftPadding;
        layoutGroup.padding = padding;

        RectTransform layoutRectTransform = layoutGroup.transform as RectTransform;
        if (layoutRectTransform)
            LayoutRebuilder.MarkLayoutForRebuild(layoutRectTransform);
    }

    private void ForwardScrollToScrollRect(BaseEventData eventData, ScrollRect scrollRect)
    {
        if (!scrollRect || eventData == null)
            return;

        if (!(eventData is PointerEventData pointerEventData))
            return;

        scrollRect.OnScroll(pointerEventData);
    }

    private void OnWeaponEntryPointerEnter(GameObject spawnedEntry, PlayerInventory.InventoryEntry inventoryEntry)
    {
        UpdateHoveredWeaponItemStats(inventoryEntry);
        UpdateHoveredWeaponEntryHighlight(spawnedEntry, inventoryEntry);
    }

    private void OnAidEntryPointerEnter(GameObject spawnedEntry, PlayerInventory.InventoryEntry inventoryEntry)
    {
        UpdateHoveredAidItemStats(inventoryEntry);
        UpdateHoveredAidButtonHighlight(spawnedEntry);
    }

    private void OnMiscEntryPointerEnter(GameObject spawnedEntry, PlayerInventory.InventoryEntry inventoryEntry)
    {
        UpdateHoveredMiscItemStats(inventoryEntry);
        UpdateHoveredMiscButtonHighlight(spawnedEntry);
    }

    private void OnAmmoEntryPointerEnter(GameObject spawnedEntry, PlayerInventory.InventoryEntry inventoryEntry)
    {
        UpdateHoveredAmmoItemStats(inventoryEntry);
        UpdateHoveredAmmoButtonHighlight(spawnedEntry);
    }

    private void UpdateHoveredWeaponEntryHighlight(GameObject spawnedEntry, PlayerInventory.InventoryEntry inventoryEntry)
    {
        UpdateHoveredWeaponButtonHighlight(spawnedEntry);

        if (!spawnedEntry)
        {
            RefreshWeaponEntrySelectionIndicators();
            return;
        }

        RectTransform entryRectTransform = spawnedEntry.GetComponent<RectTransform>();
        if (!entryRectTransform)
        {
            RefreshWeaponEntrySelectionIndicators();
            return;
        }

        bool hoveredEntryIsEquipped = selectedWeaponInventoryEntry != null && selectedWeaponInventoryEntry == inventoryEntry;
        PositionWeaponEntrySelectionIndicators(entryRectTransform, !hoveredEntryIsEquipped);
    }

    private void ClearHoveredWeaponEntryHighlight()
    {
        SetButtonHighlight(hoveredWeaponEntryHighlight, false, false);
        RefreshWeaponEntrySelectionIndicators();
    }

    private void UpdateHoveredWeaponButtonHighlight(GameObject spawnedEntry)
    {
        if (!spawnedEntry)
        {
            SetButtonHighlight(hoveredWeaponEntryHighlight, false, false);
            return;
        }

        RectTransform entryRectTransform = spawnedEntry.GetComponent<RectTransform>();
        if (!entryRectTransform || !entryRectTransform.parent)
        {
            SetButtonHighlight(hoveredWeaponEntryHighlight, false, false);
            return;
        }

        RectTransform listParentRect = entryRectTransform.parent as RectTransform;
        if (!listParentRect)
        {
            SetButtonHighlight(hoveredWeaponEntryHighlight, false, false);
            return;
        }

        TextMeshProUGUI entryLabel = spawnedEntry.GetComponentInChildren<TextMeshProUGUI>(true);
        RectTransform entryLabelRectTransform = entryLabel ? entryLabel.rectTransform : null;

        int baseSiblingIndex = entryRectTransform.GetSiblingIndex();
        bool hasScrollbarRightEdge = TryGetWeaponsScrollbarRightEdgeLocalX(listParentRect, out float scrollbarRightEdgeLocalX);
        bool hasOutline = PositionHoveredWeaponHighlightElement(
            hoveredWeaponEntryHighlight.outline,
            entryRectTransform,
            entryLabelRectTransform,
            listParentRect,
            baseSiblingIndex + 1,
            hasScrollbarRightEdge,
            scrollbarRightEdgeLocalX,
            0.0f,
            5.0f,
            -1.0f);
        bool hasBackground = PositionHoveredWeaponHighlightElement(
            hoveredWeaponEntryHighlight.background,
            entryRectTransform,
            entryLabelRectTransform,
            listParentRect,
            baseSiblingIndex,
            hasScrollbarRightEdge,
            scrollbarRightEdgeLocalX,
            1.0f,
            6.0f,
            -2.0f);

        SetHoveredEntryHighlightSiblingOrder(entryRectTransform, hoveredWeaponEntryHighlight, hasOutline, hasBackground);
        SetButtonHighlight(hoveredWeaponEntryHighlight, hasOutline, hasBackground);
    }

    private void UpdateHoveredMiscButtonHighlight(GameObject spawnedEntry)
    {
        if (!spawnedEntry)
        {
            SetButtonHighlight(hoveredMiscEntryHighlight, false, false);
            return;
        }

        RectTransform entryRectTransform = spawnedEntry.GetComponent<RectTransform>();
        if (!entryRectTransform || !entryRectTransform.parent)
        {
            SetButtonHighlight(hoveredMiscEntryHighlight, false, false);
            return;
        }

        RectTransform listParentRect = entryRectTransform.parent as RectTransform;
        if (!listParentRect)
        {
            SetButtonHighlight(hoveredMiscEntryHighlight, false, false);
            return;
        }

        TextMeshProUGUI entryLabel = spawnedEntry.GetComponentInChildren<TextMeshProUGUI>(true);
        RectTransform entryLabelRectTransform = entryLabel ? entryLabel.rectTransform : null;

        int baseSiblingIndex = entryRectTransform.GetSiblingIndex();
        bool hasScrollbarRightEdge = TryGetMiscScrollbarRightEdgeLocalX(listParentRect, out float scrollbarRightEdgeLocalX);
        float outlineRightInsetPixels = 5.0f - MiscAndAmmoHighlightRightCompensationPixels;
        float backgroundRightInsetPixels = 6.0f - MiscAndAmmoHighlightRightCompensationPixels;
        bool hasOutline = PositionHoveredWeaponHighlightElement(
            hoveredMiscEntryHighlight.outline,
            entryRectTransform,
            entryLabelRectTransform,
            listParentRect,
            baseSiblingIndex + 1,
            hasScrollbarRightEdge,
            scrollbarRightEdgeLocalX,
            0.0f,
            outlineRightInsetPixels,
            -1.0f);
        bool hasBackground = PositionHoveredWeaponHighlightElement(
            hoveredMiscEntryHighlight.background,
            entryRectTransform,
            entryLabelRectTransform,
            listParentRect,
            baseSiblingIndex,
            hasScrollbarRightEdge,
            scrollbarRightEdgeLocalX,
            1.0f,
            backgroundRightInsetPixels,
            -2.0f);

        SetHoveredEntryHighlightSiblingOrder(entryRectTransform, hoveredMiscEntryHighlight, hasOutline, hasBackground);
        SetButtonHighlight(hoveredMiscEntryHighlight, hasOutline, hasBackground);
    }

    private void UpdateHoveredAidButtonHighlight(GameObject spawnedEntry)
    {
        if (!spawnedEntry)
        {
            SetButtonHighlight(hoveredAidEntryHighlight, false, false);
            return;
        }

        RectTransform entryRectTransform = spawnedEntry.GetComponent<RectTransform>();
        if (!entryRectTransform || !entryRectTransform.parent)
        {
            SetButtonHighlight(hoveredAidEntryHighlight, false, false);
            return;
        }

        RectTransform listParentRect = entryRectTransform.parent as RectTransform;
        if (!listParentRect)
        {
            SetButtonHighlight(hoveredAidEntryHighlight, false, false);
            return;
        }

        TextMeshProUGUI entryLabel = spawnedEntry.GetComponentInChildren<TextMeshProUGUI>(true);
        RectTransform entryLabelRectTransform = entryLabel ? entryLabel.rectTransform : null;

        int baseSiblingIndex = entryRectTransform.GetSiblingIndex();
        bool hasScrollbarRightEdge = TryGetAidScrollbarRightEdgeLocalX(listParentRect, out float scrollbarRightEdgeLocalX);
        float outlineRightInsetPixels = 5.0f - MiscAndAmmoHighlightRightCompensationPixels;
        float backgroundRightInsetPixels = 6.0f - MiscAndAmmoHighlightRightCompensationPixels;
        bool hasOutline = PositionHoveredWeaponHighlightElement(
            hoveredAidEntryHighlight.outline,
            entryRectTransform,
            entryLabelRectTransform,
            listParentRect,
            baseSiblingIndex + 1,
            hasScrollbarRightEdge,
            scrollbarRightEdgeLocalX,
            0.0f,
            outlineRightInsetPixels,
            -1.0f);
        bool hasBackground = PositionHoveredWeaponHighlightElement(
            hoveredAidEntryHighlight.background,
            entryRectTransform,
            entryLabelRectTransform,
            listParentRect,
            baseSiblingIndex,
            hasScrollbarRightEdge,
            scrollbarRightEdgeLocalX,
            1.0f,
            backgroundRightInsetPixels,
            -2.0f);

        SetHoveredEntryHighlightSiblingOrder(entryRectTransform, hoveredAidEntryHighlight, hasOutline, hasBackground);
        SetButtonHighlight(hoveredAidEntryHighlight, hasOutline, hasBackground);
    }

    private void ClearHoveredAidEntryHighlight()
    {
        SetButtonHighlight(hoveredAidEntryHighlight, false, false);
    }

    private void ClearHoveredMiscEntryHighlight()
    {
        SetButtonHighlight(hoveredMiscEntryHighlight, false, false);
    }

    private void UpdateHoveredAmmoButtonHighlight(GameObject spawnedEntry)
    {
        if (!spawnedEntry)
        {
            SetButtonHighlight(hoveredAmmoEntryHighlight, false, false);
            return;
        }

        RectTransform entryRectTransform = spawnedEntry.GetComponent<RectTransform>();
        if (!entryRectTransform || !entryRectTransform.parent)
        {
            SetButtonHighlight(hoveredAmmoEntryHighlight, false, false);
            return;
        }

        RectTransform listParentRect = entryRectTransform.parent as RectTransform;
        if (!listParentRect)
        {
            SetButtonHighlight(hoveredAmmoEntryHighlight, false, false);
            return;
        }

        TextMeshProUGUI entryLabel = spawnedEntry.GetComponentInChildren<TextMeshProUGUI>(true);
        RectTransform entryLabelRectTransform = entryLabel ? entryLabel.rectTransform : null;

        int baseSiblingIndex = entryRectTransform.GetSiblingIndex();
        bool hasScrollbarRightEdge = TryGetAmmoScrollbarRightEdgeLocalX(listParentRect, out float scrollbarRightEdgeLocalX);
        float outlineRightInsetPixels = 5.0f - MiscAndAmmoHighlightRightCompensationPixels;
        float backgroundRightInsetPixels = 6.0f - MiscAndAmmoHighlightRightCompensationPixels;
        bool hasOutline = PositionHoveredWeaponHighlightElement(
            hoveredAmmoEntryHighlight.outline,
            entryRectTransform,
            entryLabelRectTransform,
            listParentRect,
            baseSiblingIndex + 1,
            hasScrollbarRightEdge,
            scrollbarRightEdgeLocalX,
            0.0f,
            outlineRightInsetPixels,
            -1.0f);
        bool hasBackground = PositionHoveredWeaponHighlightElement(
            hoveredAmmoEntryHighlight.background,
            entryRectTransform,
            entryLabelRectTransform,
            listParentRect,
            baseSiblingIndex,
            hasScrollbarRightEdge,
            scrollbarRightEdgeLocalX,
            1.0f,
            backgroundRightInsetPixels,
            -2.0f);

        SetHoveredEntryHighlightSiblingOrder(entryRectTransform, hoveredAmmoEntryHighlight, hasOutline, hasBackground);
        SetButtonHighlight(hoveredAmmoEntryHighlight, hasOutline, hasBackground);
    }

    private void ClearHoveredAmmoEntryHighlight()
    {
        SetButtonHighlight(hoveredAmmoEntryHighlight, false, false);
    }

    private void UpdateHoveredQuestButtonHighlight(GameObject spawnedEntry)
    {
        if (!spawnedEntry)
        {
            SetButtonHighlight(hoveredQuestEntryHighlight, false, false);
            return;
        }

        RectTransform entryRectTransform = spawnedEntry.GetComponent<RectTransform>();
        if (!entryRectTransform || !entryRectTransform.parent)
        {
            SetButtonHighlight(hoveredQuestEntryHighlight, false, false);
            return;
        }

        RectTransform listParentRect = entryRectTransform.parent as RectTransform;
        if (!listParentRect)
        {
            SetButtonHighlight(hoveredQuestEntryHighlight, false, false);
            return;
        }

        TextMeshProUGUI entryLabel = spawnedEntry.GetComponentInChildren<TextMeshProUGUI>(true);
        RectTransform entryLabelRectTransform = entryLabel ? entryLabel.rectTransform : null;

        const float questHighlightLeftEdgeOffsetPixels = 5.0f;
        int baseSiblingIndex = entryRectTransform.GetSiblingIndex();
        bool hasScrollbarRightEdge = TryGetQuestsScrollbarRightEdgeLocalX(listParentRect, out float scrollbarRightEdgeLocalX);
        bool hasOutline = PositionHoveredWeaponHighlightElement(
            hoveredQuestEntryHighlight.outline,
            entryRectTransform,
            entryLabelRectTransform,
            listParentRect,
            baseSiblingIndex + 1,
            hasScrollbarRightEdge,
            scrollbarRightEdgeLocalX,
            questHighlightLeftEdgeOffsetPixels,
            5.0f,
            -1.0f);
        bool hasBackground = PositionHoveredWeaponHighlightElement(
            hoveredQuestEntryHighlight.background,
            entryRectTransform,
            entryLabelRectTransform,
            listParentRect,
            baseSiblingIndex,
            hasScrollbarRightEdge,
            scrollbarRightEdgeLocalX,
            questHighlightLeftEdgeOffsetPixels + 1.0f,
            6.0f,
            -2.0f);

        SetHoveredEntryHighlightSiblingOrder(entryRectTransform, hoveredQuestEntryHighlight, hasOutline, hasBackground);
        SetButtonHighlight(hoveredQuestEntryHighlight, hasOutline, hasBackground);
    }

    private void ClearHoveredQuestEntryHighlight()
    {
        SetButtonHighlight(hoveredQuestEntryHighlight, false, false);
    }

    private float GetQuestEntryVisualWidth(RectTransform entryRectTransform)
    {
        if (questEntryButtonPrefab && questEntryButtonPrefab.transform is RectTransform prefabRectTransform)
        {
            float prefabWidth = prefabRectTransform.sizeDelta.x;
            if (prefabWidth > 0.0f)
                return prefabWidth;
        }

        return entryRectTransform ? Mathf.Max(1.0f, entryRectTransform.rect.width) : 140.0f;
    }

    private void RefreshCurrentQuestSelectionIndicator(System.Collections.Generic.List<QuestRuntimeState> visibleQuests = null)
    {
        Transform listParent = ResolveQuestsListParent();
        RectTransform listParentRect = listParent as RectTransform;
        if (!dataQuestsPanel || !dataQuestsPanel.activeInHierarchy || !listParentRect)
        {
            SetCurrentQuestSelectionIndicatorVisible(false);
            return;
        }

        EnsureCurrentQuestSelectionIndicator(listParentRect);
        if (!currentQuestSelectedBoxImage)
        {
            SetCurrentQuestSelectionIndicatorVisible(false);
            return;
        }

        if (!questController)
            questController = QuestController.FindOrCreate();

        QuestRuntimeState currentQuest = ResolveCurrentQuestSelectionIndicatorState(visibleQuests);
        if (currentQuest == null)
        {
            SetCurrentQuestSelectionIndicatorVisible(false);
            return;
        }

        if (!TryGetSpawnedQuestEntryRect(currentQuest, out RectTransform entryRectTransform))
        {
            SetCurrentQuestSelectionIndicatorVisible(false);
            return;
        }

        TextMeshProUGUI entryLabel = entryRectTransform.GetComponentInChildren<TextMeshProUGUI>(true);
        RectTransform entryLabelRectTransform = entryLabel ? entryLabel.rectTransform : null;

        if (!TryGetQuestEntrySelectionIndicatorWorldPosition(
                entryRectTransform,
                entryLabelRectTransform,
                listParentRect,
                out Vector3 indicatorWorldPosition))
        {
            SetCurrentQuestSelectionIndicatorVisible(false);
            return;
        }

        RectTransform indicatorRectTransform = currentQuestSelectedBoxImage.rectTransform;
        if (indicatorRectTransform)
        {
            indicatorRectTransform.SetParent(listParentRect, false);
            indicatorRectTransform.anchorMin = new Vector2(0.0f, 1.0f);
            indicatorRectTransform.anchorMax = new Vector2(0.0f, 1.0f);
            indicatorRectTransform.pivot = new Vector2(0.5f, 0.5f);
            indicatorRectTransform.localScale = Vector3.one;
            indicatorRectTransform.localRotation = Quaternion.identity;
            indicatorRectTransform.position = indicatorWorldPosition;
            indicatorRectTransform.SetAsLastSibling();
        }

        SetCurrentQuestSelectionIndicatorVisible(true);
    }

    private QuestRuntimeState ResolveCurrentQuestSelectionIndicatorState(System.Collections.Generic.List<QuestRuntimeState> visibleQuests)
    {
        QuestRuntimeState currentQuest = questController ? questController.GetCurrentQuest() : null;
        if (currentQuest != null && currentQuest.GetStatus() == QuestStatus.Active)
            return currentQuest;

        if (visibleQuests == null)
            return null;

        for (int i = 0; i < visibleQuests.Count; i++)
        {
            QuestRuntimeState questState = visibleQuests[i];
            if (questState != null && questState.GetStatus() == QuestStatus.Active)
                return questState;
        }

        return null;
    }

    private void EnsureCurrentQuestSelectionIndicator(RectTransform listParentRect)
    {
        if (!listParentRect)
            return;

        if (currentQuestSelectedBoxImage)
        {
            if (currentQuestSelectedBoxImage.rectTransform.parent != listParentRect)
                currentQuestSelectedBoxImage.rectTransform.SetParent(listParentRect, false);

            return;
        }

        currentQuestSelectedBoxImage = questSelectedBoxImage;
        if (!currentQuestSelectedBoxImage)
            return;

        currentQuestSelectedBoxImage.rectTransform.SetParent(listParentRect, false);
        currentQuestSelectedBoxImage.raycastTarget = false;
        EnsureLayoutIgnored(currentQuestSelectedBoxImage.gameObject);
        SetActiveSafe(currentQuestSelectedBoxImage.gameObject, false);
    }

    private bool TryGetSpawnedQuestEntryRect(QuestRuntimeState questState, out RectTransform entryRectTransform)
    {
        entryRectTransform = null;
        if (questState == null)
            return false;

        for (int i = 0; i < spawnedQuestEntryButtons.Count; i++)
        {
            GameObject candidate = spawnedQuestEntryButtons[i];
            if (!candidate || !candidate.activeInHierarchy)
                continue;

            if (!pooledEntryStateByObject.TryGetValue(candidate, out PipBoyListEntryState state) || state == null)
                continue;

            if (state.BoundMode != QuestRowBindingMode || state.BoundQuestState != questState)
                continue;

            entryRectTransform = candidate.GetComponent<RectTransform>();
            return entryRectTransform != null;
        }

        return false;
    }

    private bool TryGetQuestEntrySelectionIndicatorWorldPosition(
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
        float entryCenterLocalY = (entryMinY + entryMaxY) * 0.5f;

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
            entryCenterLocalY = (labelMinY + labelMaxY) * 0.5f;
        }

        float indicatorLeftEdgeLocalX = entryMinX;
        if (TryGetQuestsScrollbarRightEdgeLocalX(listParentRect, out float scrollbarRightEdgeLocalX))
            indicatorLeftEdgeLocalX = scrollbarRightEdgeLocalX;

        float indicatorLocalX = (indicatorLeftEdgeLocalX + textStartLocalX) * 0.5f;
        indicatorWorldPosition = listParentRect.TransformPoint(new Vector3(indicatorLocalX, entryCenterLocalY, 0.0f));
        return true;
    }

    private void SetCurrentQuestSelectionIndicatorVisible(bool visible)
    {
        if (currentQuestSelectedBoxImage)
        {
            currentQuestSelectedBoxImage.raycastTarget = false;
            currentQuestSelectedBoxImage.enabled = true;
            currentQuestSelectedBoxImage.color = GetQuestBoxColor(1.0f);

            RectTransform rectTransform = currentQuestSelectedBoxImage.rectTransform;
            if (rectTransform)
            {
                rectTransform.sizeDelta = GetQuestBoxSize();
                rectTransform.localScale = Vector3.one;
                rectTransform.SetAsLastSibling();
            }
        }

        SetActiveSafe(currentQuestSelectedBoxImage ? currentQuestSelectedBoxImage.gameObject : null, visible);
    }

    private void RefreshWeaponEntrySelectionIndicators()
    {
        if (!itemsWeaponsPanel || !itemsWeaponsPanel.activeInHierarchy)
        {
            SetWeaponEntrySelectionIndicatorsVisible(false, false, false);
            return;
        }

        bool isWeaponsListScrolling = IsWeaponsListScrolling();
        RectTransform selectedEntryRect = null;
        bool hasSelectedEntryRect = selectedWeaponInventoryEntry != null &&
                                    TryGetSpawnedWeaponEntryRect(selectedWeaponInventoryEntry, out selectedEntryRect);

        if (hasSelectedEntryRect)
            PositionEquippedWeaponEntrySelectionIndicator(selectedEntryRect);
        else
            SetEquippedWeaponEntrySelectionIndicatorVisible(false);

        if (!isWeaponsListScrolling &&
            hoveredWeaponInventoryEntry != null &&
            TryGetSpawnedWeaponEntryRect(hoveredWeaponInventoryEntry, out RectTransform hoveredEntryRect))
        {
            bool hoveredEntryIsEquipped = selectedWeaponInventoryEntry != null && selectedWeaponInventoryEntry == hoveredWeaponInventoryEntry;
            PositionWeaponEntrySelectionIndicators(hoveredEntryRect, !hoveredEntryIsEquipped);
            return;
        }

        if (hasSelectedEntryRect)
        {
            PositionWeaponEntrySelectionIndicators(selectedEntryRect, false);
            return;
        }

        if (hoveredWeaponInventoryEntry != null &&
            TryGetSpawnedWeaponEntryRect(hoveredWeaponInventoryEntry, out RectTransform fallbackHoveredEntryRect))
        {
            PositionWeaponEntrySelectionIndicators(fallbackHoveredEntryRect, true);
            return;
        }

        SetPrimaryWeaponEntrySelectionIndicatorsVisible(false, false);
    }

    private bool IsWeaponsListScrolling()
    {
        if (!weaponsScrollRect)
            return false;

        Vector2 scrollVelocity = weaponsScrollRect.velocity;
        return scrollVelocity.sqrMagnitude > 0.01f;
    }

    private bool TryGetSpawnedWeaponEntryRect(
        PlayerInventory.InventoryEntry inventoryEntry,
        out RectTransform entryRectTransform)
    {
        entryRectTransform = null;
        if (inventoryEntry == null)
            return false;

        for (int i = 0; i < spawnedWeaponEntryButtons.Count; i++)
        {
            GameObject candidate = spawnedWeaponEntryButtons[i];
            if (!candidate || !candidate.activeInHierarchy)
                continue;

            if (!pooledEntryStateByObject.TryGetValue(candidate, out PipBoyListEntryState state) || state == null)
                continue;

            if (state.BoundMode != WeaponRowBindingMode || state.BoundEntry != inventoryEntry)
                continue;

            entryRectTransform = candidate.GetComponent<RectTransform>();
            return entryRectTransform != null;
        }

        return false;
    }

    private void PositionWeaponEntrySelectionIndicators(RectTransform entryRectTransform, bool showBackground)
    {
        if (!entryRectTransform || !(entryRectTransform.parent is RectTransform listParentRect))
        {
            SetPrimaryWeaponEntrySelectionIndicatorsVisible(false, false);
            return;
        }

        TextMeshProUGUI entryLabel = entryRectTransform.GetComponentInChildren<TextMeshProUGUI>(true);
        RectTransform entryLabelRectTransform = entryLabel ? entryLabel.rectTransform : null;

        if (!TryGetWeaponEntrySelectionIndicatorWorldPosition(
                entryRectTransform,
                entryLabelRectTransform,
                listParentRect,
                out Vector3 indicatorWorldPosition))
        {
            SetPrimaryWeaponEntrySelectionIndicatorsVisible(false, false);
            return;
        }

        SetSelectionIndicatorWorldPosition(selectedBoxImage, indicatorWorldPosition);
        SetSelectionIndicatorWorldPosition(selectedBoxBackgroundImage, indicatorWorldPosition);
        SetPrimaryWeaponEntrySelectionIndicatorsVisible(selectedBoxImage != null, showBackground && selectedBoxBackgroundImage != null);
    }

    private void PositionEquippedWeaponEntrySelectionIndicator(RectTransform entryRectTransform)
    {
        if (!entryRectTransform || !(entryRectTransform.parent is RectTransform listParentRect))
        {
            SetEquippedWeaponEntrySelectionIndicatorVisible(false);
            return;
        }

        TextMeshProUGUI entryLabel = entryRectTransform.GetComponentInChildren<TextMeshProUGUI>(true);
        RectTransform entryLabelRectTransform = entryLabel ? entryLabel.rectTransform : null;

        if (!TryGetWeaponEntrySelectionIndicatorWorldPosition(
                entryRectTransform,
                entryLabelRectTransform,
                listParentRect,
                out Vector3 indicatorWorldPosition))
        {
            SetEquippedWeaponEntrySelectionIndicatorVisible(false);
            return;
        }

        SetSelectionIndicatorWorldPosition(equippedSelectedBoxImage, indicatorWorldPosition);
        SetEquippedWeaponEntrySelectionIndicatorVisible(equippedSelectedBoxImage != null);
    }

    private bool TryGetWeaponEntrySelectionIndicatorWorldPosition(
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
        float entryCenterLocalY = (entryMinY + entryMaxY) * 0.5f;

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
            entryCenterLocalY = (labelMinY + labelMaxY) * 0.5f;
        }

        if (!TryGetWeaponsScrollbarRightEdgeLocalX(listParentRect, out float scrollbarRightEdgeLocalX))
            return false;

        float indicatorLocalX = (scrollbarRightEdgeLocalX + textStartLocalX) * 0.5f;
        indicatorWorldPosition = listParentRect.TransformPoint(new Vector3(indicatorLocalX, entryCenterLocalY, 0.0f));
        return true;
    }

    private static void SetSelectionIndicatorWorldPosition(Image indicatorImage, Vector3 worldPosition)
    {
        if (!indicatorImage)
            return;

        RectTransform indicatorRectTransform = indicatorImage.rectTransform;
        if (!indicatorRectTransform)
            return;

        indicatorRectTransform.position = worldPosition;
    }

    private void SetWeaponEntrySelectionIndicatorsVisible(
        bool selectedBoxVisible,
        bool selectedBoxBackgroundVisible,
        bool equippedSelectedBoxVisible)
    {
        SetPrimaryWeaponEntrySelectionIndicatorsVisible(selectedBoxVisible, selectedBoxBackgroundVisible);
        SetEquippedWeaponEntrySelectionIndicatorVisible(equippedSelectedBoxVisible);
    }

    private void SetPrimaryWeaponEntrySelectionIndicatorsVisible(bool selectedBoxVisible, bool selectedBoxBackgroundVisible)
    {
        SetActiveSafe(selectedBoxImage ? selectedBoxImage.gameObject : null, selectedBoxVisible);
        SetActiveSafe(selectedBoxBackgroundImage ? selectedBoxBackgroundImage.gameObject : null, selectedBoxBackgroundVisible);
    }

    private void SetEquippedWeaponEntrySelectionIndicatorVisible(bool equippedSelectedBoxVisible)
    {
        SetActiveSafe(equippedSelectedBoxImage ? equippedSelectedBoxImage.gameObject : null, equippedSelectedBoxVisible);
    }

    private static bool PositionHoveredWeaponHighlightElement(
        GameObject highlightObject,
        RectTransform targetEntryRect,
        RectTransform targetLabelRect,
        RectTransform targetParentRect,
        int siblingIndex,
        bool useForcedLeftEdge,
        float forcedLeftEdgeLocalX,
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
        highlightRect.localScale = Vector3.one;
        highlightRect.localRotation = Quaternion.identity;

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

        float highlightMinX = (useForcedLeftEdge ? forcedLeftEdgeLocalX : entryMinX) + leftInsetPixels;
        float highlightMaxX = entryMaxX - rightInsetPixels;
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

        // Keep hover visuals behind row text by inserting both immediately before the row.
        // Insert outline first so background sits above it, leaving a visible border.
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

        // If the element is already before target, account for the removal shift.
        if (movingSiblingIndex < targetSiblingIndex)
            targetSiblingIndex = Mathf.Max(0, targetSiblingIndex - 1);

        movingRectTransform.SetSiblingIndex(targetSiblingIndex);
    }

    private bool TryGetWeaponsScrollbarRightEdgeLocalX(RectTransform localSpaceRect, out float rightEdgeLocalX)
    {
        return TryGetScrollRectScrollbarRightEdgeLocalX(weaponsScrollRect, localSpaceRect, out rightEdgeLocalX);
    }

    private bool TryGetAidScrollbarRightEdgeLocalX(RectTransform localSpaceRect, out float rightEdgeLocalX)
    {
        return TryGetScrollRectScrollbarRightEdgeLocalX(aidScrollRect, localSpaceRect, out rightEdgeLocalX);
    }

    private bool TryGetMiscScrollbarRightEdgeLocalX(RectTransform localSpaceRect, out float rightEdgeLocalX)
    {
        return TryGetScrollRectScrollbarRightEdgeLocalX(miscScrollRect, localSpaceRect, out rightEdgeLocalX);
    }

    private bool TryGetAmmoScrollbarRightEdgeLocalX(RectTransform localSpaceRect, out float rightEdgeLocalX)
    {
        return TryGetScrollRectScrollbarRightEdgeLocalX(ammoScrollRect, localSpaceRect, out rightEdgeLocalX);
    }

    private bool TryGetQuestsScrollbarRightEdgeLocalX(RectTransform localSpaceRect, out float rightEdgeLocalX)
    {
        return TryGetScrollRectScrollbarRightEdgeLocalX(questsScrollRect, localSpaceRect, out rightEdgeLocalX);
    }

    private bool TryGetQuestsScrollViewRightEdgeLocalX(RectTransform localSpaceRect, out float rightEdgeLocalX)
    {
        rightEdgeLocalX = 0.0f;
        if (!localSpaceRect || !questsScrollRect)
            return false;

        RectTransform scrollRectTransform = questsScrollRect.transform as RectTransform;
        if (!scrollRectTransform)
            return false;

        Vector3[] worldCorners = new Vector3[4];
        scrollRectTransform.GetWorldCorners(worldCorners);
        Vector3 rightEdgeWorldPoint = (worldCorners[2] + worldCorners[3]) * 0.5f;
        Vector3 rightEdgeLocalPoint = localSpaceRect.InverseTransformPoint(rightEdgeWorldPoint);
        rightEdgeLocalX = rightEdgeLocalPoint.x;
        return true;
    }

    private static bool TryGetScrollRectScrollbarRightEdgeLocalX(
        ScrollRect scrollRect,
        RectTransform localSpaceRect,
        out float rightEdgeLocalX)
    {
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
        Vector3 rightEdgeWorldPoint = (scrollbarWorldCorners[2] + scrollbarWorldCorners[3]) * 0.5f;
        Vector3 rightEdgeLocalPoint = localSpaceRect.InverseTransformPoint(rightEdgeWorldPoint);
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
        return true;
    }

    private static void EnsureLayoutIgnored(GameObject highlightObject)
    {
        if (!highlightObject) return;

        LayoutElement layoutElement = highlightObject.GetComponent<LayoutElement>();
        if (!layoutElement)
            layoutElement = highlightObject.AddComponent<LayoutElement>();

        layoutElement.ignoreLayout = true;
    }

    private void UpdateHoveredWeaponItemStats(PlayerInventory.InventoryEntry inventoryEntry)
    {
        if (inventoryEntry == null)
        {
            ClearHoveredWeaponItemStats();
            return;
        }

        ScriptableObject itemDefinition = inventoryEntry.GetItemDefinition();
        if (!(itemDefinition is WeaponDefinition weaponDefinition))
        {
            ClearHoveredWeaponItemStats();
            return;
        }

        int damage = Mathf.Max(0, weaponDefinition.GetDamage());
        float value = playerInventory ? Mathf.Max(0.0f, playerInventory.GetInstanceValue(inventoryEntry, 0)) : 0.0f;
        float conditionPercent = playerInventory ? Mathf.Clamp(playerInventory.GetInstanceConditionPercent(inventoryEntry, 0), 0.0f, 100.0f) : 0.0f;
        float weight = Mathf.Max(0.0f, weaponDefinition.GetWeight());
        int loadedMagazineRounds = playerInventory ? Mathf.Max(0, playerInventory.GetInstanceLoadedMagazineRounds(inventoryEntry, 0)) : 0;
        AmmoDefinition ammoType = weaponDefinition.GetAmmoType();
        int reserveAmmoRounds = (playerInventory && ammoType) ? Mathf.Max(0, playerInventory.GetAmmoCount(ammoType)) : 0;
        string ammoDisplayName = ammoType ? ammoType.GetDisplayName() : string.Empty;
        if (string.IsNullOrWhiteSpace(ammoDisplayName) && ammoType)
            ammoDisplayName = ammoType.name;

        hoveredWeaponInventoryEntry = inventoryEntry;
        SetHoveredWeaponStatObjectsVisible(true);

        SetTextIfChanged(dmgItemText, damage.ToString());
        SetTextIfChanged(valItemText, FormatValue(value));
        SetConditionBarFillAmount(Mathf.Clamp01(conditionPercent / 100.0f));
        SetTextIfChanged(wgItemText, weight.ToString("0.#"));
        SetTextIfChanged(
            ammoItemText,
            ammoType ? $"{ammoDisplayName} ({loadedMagazineRounds}/{reserveAmmoRounds})" : string.Empty);
    }

    private void ClearHoveredWeaponItemStats()
    {
        hoveredWeaponInventoryEntry = null;
        ClearHoveredWeaponEntryHighlight();
        SetHoveredWeaponStatObjectsVisible(false);
        SetConditionBarFillAmount(0.0f);
        SetTextIfChanged(ammoItemText, string.Empty);
    }

    private void UpdateHoveredAidItemStats(PlayerInventory.InventoryEntry inventoryEntry)
    {
        if (inventoryEntry == null)
        {
            ClearHoveredAidItemStats();
            return;
        }

        ScriptableObject itemDefinition = inventoryEntry.GetItemDefinition();
        if (!(itemDefinition is AidDefinition aidDefinition))
        {
            ClearHoveredAidItemStats();
            return;
        }

        float itemValue = Mathf.Max(0.0f, aidDefinition.GetValue());
        float itemWeight = Mathf.Max(0.0f, aidDefinition.GetWeight());
        string effectsText = BuildAidEffectsDisplayText(aidDefinition);

        hoveredAidInventoryEntry = inventoryEntry;
        SetHoveredAidStatObjectsVisible(true);
        SetTextIfChanged(aidValItemText, FormatValue(itemValue));
        SetTextIfChanged(aidWgItemText, itemWeight.ToString("0.#"));
        SetTextIfChanged(aidEffectsItemText, effectsText);
    }

    private void ClearHoveredAidItemStats()
    {
        hoveredAidInventoryEntry = null;
        ClearHoveredAidEntryHighlight();
        SetHoveredAidStatObjectsVisible(false);
        SetTextIfChanged(aidValItemText, string.Empty);
        SetTextIfChanged(aidWgItemText, string.Empty);
        SetTextIfChanged(aidEffectsItemText, string.Empty);
    }

    private void UpdateHoveredMiscItemStats(PlayerInventory.InventoryEntry inventoryEntry)
    {
        if (inventoryEntry == null)
        {
            ClearHoveredMiscItemStats();
            return;
        }

        ScriptableObject itemDefinition = inventoryEntry.GetItemDefinition();
        if (!(itemDefinition is MiscDefinition miscDefinition))
        {
            ClearHoveredMiscItemStats();
            return;
        }

        float itemValue = Mathf.Max(0.0f, miscDefinition.GetValue());
        float itemWeight = Mathf.Max(0.0f, miscDefinition.GetWeight());

        hoveredMiscInventoryEntry = inventoryEntry;
        SetHoveredMiscStatObjectsVisible(true);
        SetTextIfChanged(miscValItemText, FormatValue(itemValue));
        SetTextIfChanged(miscWgItemText, itemWeight.ToString("0.#"));
    }

    private void ClearHoveredMiscItemStats()
    {
        hoveredMiscInventoryEntry = null;
        ClearHoveredMiscEntryHighlight();
        SetHoveredMiscStatObjectsVisible(false);
        SetTextIfChanged(miscValItemText, string.Empty);
        SetTextIfChanged(miscWgItemText, string.Empty);
    }

    private void UpdateHoveredAmmoItemStats(PlayerInventory.InventoryEntry inventoryEntry)
    {
        if (inventoryEntry == null)
        {
            ClearHoveredAmmoItemStats();
            return;
        }

        ScriptableObject itemDefinition = inventoryEntry.GetItemDefinition();
        if (!(itemDefinition is AmmoDefinition ammoDefinition))
        {
            ClearHoveredAmmoItemStats();
            return;
        }

        float itemValue = playerInventory ? Mathf.Max(0.0f, playerInventory.GetInstanceValue(inventoryEntry, 0)) : 0.0f;
        float itemWeight = Mathf.Max(0.0f, ammoDefinition.GetWeight());

        hoveredAmmoInventoryEntry = inventoryEntry;
        SetHoveredAmmoStatObjectsVisible(true);
        SetTextIfChanged(ammoValItemText, FormatValue(itemValue));
        SetTextIfChanged(ammoWgItemText, itemWeight.ToString("0.#"));
    }

    private void ClearHoveredAmmoItemStats()
    {
        hoveredAmmoInventoryEntry = null;
        ClearHoveredAmmoEntryHighlight();
        SetHoveredAmmoStatObjectsVisible(false);
        SetTextIfChanged(ammoValItemText, string.Empty);
        SetTextIfChanged(ammoWgItemText, string.Empty);
    }

    private void OnDropPerformed(InputAction.CallbackContext context)
    {
        // Drop only while Pip-Boy is open on a supported items panel.
        if (!isOpen) return;

        bool isWeaponsPanelActive = itemsWeaponsPanel && itemsWeaponsPanel.activeInHierarchy;
        bool isAidPanelActive = itemsAidPanel && itemsAidPanel.activeInHierarchy;
        bool isMiscPanelActive = itemsMiscPanel && itemsMiscPanel.activeInHierarchy;
        bool isAmmoPanelActive = itemsAmmoPanel && itemsAmmoPanel.activeInHierarchy;

        if (isWeaponsPanelActive)
        {
            if (hoveredWeaponInventoryEntry == null) return;
            TryDropInventoryEntry(hoveredWeaponInventoryEntry);
            return;
        }

        if (isMiscPanelActive)
        {
            if (hoveredMiscInventoryEntry == null) return;
            TryDropInventoryEntry(hoveredMiscInventoryEntry);
            return;
        }

        if (isAidPanelActive)
        {
            if (hoveredAidInventoryEntry == null) return;
            TryDropInventoryEntry(hoveredAidInventoryEntry);
            return;
        }

        if (isAmmoPanelActive)
        {
            if (hoveredAmmoInventoryEntry == null) return;
            TryDropInventoryEntry(hoveredAmmoInventoryEntry);
        }
    }

    public bool TryDropInventoryEntry(PlayerInventory.InventoryEntry inventoryEntry)
    {
        // Stop if entry is missing.
        if (inventoryEntry == null) return false;

        if (!playerInventory)
            playerInventory = FindAnyObjectByType<PlayerInventory>();

        // Stop if inventory cannot be found.
        if (!playerInventory) return false;

        ScriptableObject itemDefinition = inventoryEntry.GetItemDefinition();
        if (!itemDefinition) return false;

        // Persist live magazine state before creating a dropped world weapon.
        if (itemDefinition is WeaponDefinition && selectedWeaponInventoryEntry == inventoryEntry)
            PersistSelectedWeaponMagazineFromController(inventoryEntry);

        GameObject worldPrefab = ResolveWorldDropPrefab(itemDefinition);
        if (!worldPrefab)
        {
            string itemName = GetDisplayNameFromDefinition(itemDefinition, itemDefinition.name);
            Debug.LogWarning($"No world drop prefab configured for '{itemName}'.");
            return false;
        }

        float conditionPercent = Mathf.Clamp(playerInventory.GetInstanceConditionPercent(inventoryEntry, 0), 0.0f, 100.0f);
        int loadedMagazineRounds = Mathf.Max(0, playerInventory.GetInstanceLoadedMagazineRounds(inventoryEntry, 0));
        int droppedQuantity = 1;
        GameObject droppedObject = Instantiate(worldPrefab, ResolveDropSpawnPosition(), ResolveDropSpawnRotation());
        PrepareDroppedObjectPhysics(droppedObject);

        WorldItem worldItem = droppedObject.GetComponent<WorldItem>();
        if (!worldItem)
            worldItem = droppedObject.GetComponentInChildren<WorldItem>(true);

        if (worldItem)
        {
            bool shouldOverrideWorldDefinition = true;
            if (itemDefinition is AmmoDefinition droppedAmmoDefinition)
            {
                ScriptableObject existingWorldDefinition = worldItem.GetItemDefinition();
                if (existingWorldDefinition is AmmoItemDefinition existingAmmoItemDefinition &&
                    existingAmmoItemDefinition.GetAmmoDefinition() == droppedAmmoDefinition)
                {
                    // Keep prefab-authored ammo container definition when it already targets this ammo type.
                    shouldOverrideWorldDefinition = false;
                }
            }

            if (shouldOverrideWorldDefinition)
                worldItem.SetItemDefinition(itemDefinition);

            worldItem.SetQuantity(droppedQuantity);
            worldItem.SetConditionPercent(conditionPercent);

            if (itemDefinition is WeaponDefinition)
            {
                WeaponItem weaponItem = worldItem.GetComponent<WeaponItem>();
                if (!weaponItem)
                    Debug.LogWarning($"Dropped weapon prefab '{worldPrefab.name}' is missing a WeaponItem component.");
                else
                    weaponItem.SetLoadedMagazineRounds(loadedMagazineRounds);
            }
            else if (itemDefinition is AmmoDefinition)
            {
                AmmoItem ammoItem = worldItem.GetComponent<AmmoItem>();
                if (ammoItem)
                    ammoItem.SetRounds(droppedQuantity);
            }
        }
        else
        {
            Debug.LogWarning($"Dropped item prefab '{worldPrefab.name}' has no WorldItem component.");
        }

        bool removed = playerInventory.RemoveInventoryEntry(inventoryEntry, 1);
        if (!removed)
        {
            Destroy(droppedObject);
            return false;
        }

        if (itemDefinition is WeaponDefinition && selectedWeaponInventoryEntry == inventoryEntry)
        {
            UnequipCurrentWeaponToUnarmed();
            selectedWeaponInventoryEntry = null;
        }

        if (hoveredWeaponInventoryEntry == inventoryEntry)
            ClearHoveredWeaponItemStats();

        if (hoveredAidInventoryEntry == inventoryEntry)
            ClearHoveredAidItemStats();

        if (hoveredMiscInventoryEntry == inventoryEntry)
            ClearHoveredMiscItemStats();

        if (hoveredAmmoInventoryEntry == inventoryEntry)
            ClearHoveredAmmoItemStats();

        RefreshWeaponEntrySelectionIndicators();
        return true;
    }

    private GameObject ResolveWorldDropPrefab(ScriptableObject itemDefinition)
    {
        if (!itemDefinition) return null;

        if (itemDefinition is WeaponDefinition weaponDefinition)
            return ResolveWorldWeaponPrefab(weaponDefinition);

        if (itemDefinition is AidDefinition aidDefinition)
            return ResolveWorldAidPrefab(aidDefinition);

        if (itemDefinition is MiscDefinition miscDefinition)
            return ResolveWorldMiscPrefab(miscDefinition);

        if (itemDefinition is AmmoItemDefinition ammoItemDefinition)
        {
            GameObject mappedWorldPrefab = ammoItemDefinition.GetWorldPrefab();
            if (mappedWorldPrefab) return mappedWorldPrefab;

            AmmoDefinition containedAmmoDefinition = ammoItemDefinition.GetAmmoDefinition();
            if (containedAmmoDefinition)
                return ResolveWorldAmmoPrefab(containedAmmoDefinition);
        }

        if (itemDefinition is AmmoDefinition ammoDefinition)
            return ResolveWorldAmmoPrefab(ammoDefinition);

        return null;
    }

    private GameObject ResolveWorldWeaponPrefab(WeaponDefinition weaponDefinition)
    {
        if (!weaponDefinition) return null;

        // Prefer direct definition mapping when available.
        GameObject mappedWorldPrefab = weaponDefinition.GetWorldPrefab();
        if (mappedWorldPrefab) return mappedWorldPrefab;

        // Fall back to Pip-Boy mapping list.
        for (int i = 0; i < fallbackWeaponWorldPrefabs.Count; i++)
        {
            GameObject fallbackPrefab = fallbackWeaponWorldPrefabs[i];
            if (!fallbackPrefab) continue;

            WorldItem fallbackWorldItem = fallbackPrefab.GetComponent<WorldItem>();
            if (!fallbackWorldItem)
                fallbackWorldItem = fallbackPrefab.GetComponentInChildren<WorldItem>(true);

            if (!fallbackWorldItem) continue;
            if (fallbackWorldItem.GetItemDefinition() != weaponDefinition) continue;
            return fallbackPrefab;
        }

        return null;
    }

    private GameObject ResolveWorldMiscPrefab(MiscDefinition miscDefinition)
    {
        if (!miscDefinition) return null;

        // Prefer direct definition mapping when available.
        GameObject mappedWorldPrefab = miscDefinition.GetWorldPrefab();
        if (mappedWorldPrefab) return mappedWorldPrefab;

        // Fall back to Pip-Boy mapping list.
        for (int i = 0; i < fallbackMiscWorldPrefabs.Count; i++)
        {
            GameObject fallbackPrefab = fallbackMiscWorldPrefabs[i];
            if (!fallbackPrefab) continue;

            WorldItem fallbackWorldItem = fallbackPrefab.GetComponent<WorldItem>();
            if (!fallbackWorldItem)
                fallbackWorldItem = fallbackPrefab.GetComponentInChildren<WorldItem>(true);

            if (!fallbackWorldItem) continue;
            if (fallbackWorldItem.GetItemDefinition() != miscDefinition) continue;
            return fallbackPrefab;
        }

        return null;
    }

    private GameObject ResolveWorldAidPrefab(AidDefinition aidDefinition)
    {
        if (!aidDefinition) return null;

        // Fall back to Pip-Boy mapping list.
        for (int i = 0; i < fallbackAidWorldPrefabs.Count; i++)
        {
            GameObject fallbackPrefab = fallbackAidWorldPrefabs[i];
            if (!fallbackPrefab) continue;

            WorldItem fallbackWorldItem = fallbackPrefab.GetComponent<WorldItem>();
            if (!fallbackWorldItem)
                fallbackWorldItem = fallbackPrefab.GetComponentInChildren<WorldItem>(true);

            if (!fallbackWorldItem) continue;
            if (fallbackWorldItem.GetItemDefinition() != aidDefinition) continue;
            return fallbackPrefab;
        }

        return null;
    }

    private GameObject ResolveWorldAmmoPrefab(AmmoDefinition ammoDefinition)
    {
        if (!ammoDefinition) return null;

        // Fall back to Pip-Boy mapping list.
        for (int i = 0; i < fallbackAmmoWorldPrefabs.Count; i++)
        {
            GameObject fallbackPrefab = fallbackAmmoWorldPrefabs[i];
            if (!fallbackPrefab) continue;

            WorldItem fallbackWorldItem = fallbackPrefab.GetComponent<WorldItem>();
            if (!fallbackWorldItem)
                fallbackWorldItem = fallbackPrefab.GetComponentInChildren<WorldItem>(true);

            if (!fallbackWorldItem) continue;
            ScriptableObject fallbackDefinition = fallbackWorldItem.GetItemDefinition();
            if (fallbackDefinition == ammoDefinition)
                return fallbackPrefab;

            if (fallbackDefinition is AmmoItemDefinition fallbackAmmoItemDefinition &&
                fallbackAmmoItemDefinition.GetAmmoDefinition() == ammoDefinition)
                return fallbackPrefab;
        }

        return null;
    }

    private Vector3 ResolveDropSpawnPosition()
    {
        return dropSpawnTransform ? dropSpawnTransform.position : transform.position;
    }

    private Quaternion ResolveDropSpawnRotation()
    {
        return dropSpawnTransform ? dropSpawnTransform.rotation : transform.rotation;
    }

    private static void PrepareDroppedObjectPhysics(GameObject droppedObject)
    {
        if (!droppedObject) return;

        Collider[] colliders = droppedObject.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider collider = colliders[i];
            if (!collider) continue;

            // Ensure dropped pickups can collide with world geometry.
            collider.enabled = true;
            collider.isTrigger = false;
        }

        Rigidbody[] rigidbodies = droppedObject.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            Rigidbody body = rigidbodies[i];
            if (!body) continue;

            // Force a stable physical drop state to prevent tunneling.
            body.isKinematic = false;
            body.useGravity = true;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
        }
    }

    private void AutoWireWeaponEntrySelectionIndicators()
    {
        if (!selectedBoxImage)
        {
            selectedBoxImage = FindChildComponentByNameInRoot<Image>(
                "SelectedBox",
                itemsWeaponsPanel ? itemsWeaponsPanel.transform : null);

            if (!selectedBoxImage)
                selectedBoxImage = FindChildComponentByName<Image>("SelectedBox");
        }

        if (!selectedBoxBackgroundImage)
        {
            selectedBoxBackgroundImage = FindChildComponentByNameInRoot<Image>(
                "SelectedBoxBackground",
                itemsWeaponsPanel ? itemsWeaponsPanel.transform : null);

            if (!selectedBoxBackgroundImage)
                selectedBoxBackgroundImage = FindChildComponentByName<Image>("SelectedBoxBackground");
        }

        if (!equippedSelectedBoxImage)
        {
            equippedSelectedBoxImage = FindChildComponentByNameInRoot<Image>(
                "SelectedBoxEquipped",
                itemsWeaponsPanel ? itemsWeaponsPanel.transform : null);

            if (!equippedSelectedBoxImage)
                equippedSelectedBoxImage = FindChildComponentByName<Image>("SelectedBoxEquipped");
        }

        if (!equippedSelectedBoxImage && selectedBoxImage)
            equippedSelectedBoxImage = CreateRuntimeSelectionIndicatorDuplicate("SelectedBoxEquipped", selectedBoxImage);
    }

    private static void ConfigureWeaponEntrySelectionIndicator(Image indicatorImage)
    {
        if (!indicatorImage)
            return;

        indicatorImage.raycastTarget = false;
        EnsureLayoutIgnored(indicatorImage.gameObject);
    }

    private static Image CreateRuntimeSelectionIndicatorDuplicate(string objectName, Image templateImage)
    {
        if (!templateImage)
            return null;

        RectTransform templateRect = templateImage.rectTransform;
        if (!templateRect || !templateRect.parent)
            return null;

        GameObject cloneObject = Instantiate(templateImage.gameObject, templateRect.parent);
        cloneObject.name = objectName;
        cloneObject.SetActive(false);

        Image cloneImage = cloneObject.GetComponent<Image>();
        if (!cloneImage)
            return null;

        cloneImage.raycastTarget = false;
        EnsureLayoutIgnored(cloneObject);
        return cloneImage;
    }

    private void AutoWireHoveredWeaponEntryHighlight()
    {
        bool hasOutline = hoveredWeaponEntryHighlight.outline != null;
        bool hasBackground = hoveredWeaponEntryHighlight.background != null;
        if (hasOutline && hasBackground)
            return;

        Transform weaponsListParent = ResolveWeaponsListParent();

        if (!hasOutline)
        {
            Transform outlineTransform =
                FindChildComponentByNameInRoot<Transform>("ButtonOutline", weaponsListParent);

            if (!outlineTransform && itemsWeaponsPanel)
                outlineTransform = FindChildComponentByNameInRoot<Transform>("ButtonOutline", itemsWeaponsPanel.transform);

            hoveredWeaponEntryHighlight.outline = outlineTransform ? outlineTransform.gameObject : null;
        }

        if (!hasBackground)
        {
            Transform backgroundTransform =
                FindChildComponentByNameInRoot<Transform>("ButtonBackground", weaponsListParent);

            if (!backgroundTransform && itemsWeaponsPanel)
                backgroundTransform = FindChildComponentByNameInRoot<Transform>("ButtonBackground", itemsWeaponsPanel.transform);

            hoveredWeaponEntryHighlight.background = backgroundTransform ? backgroundTransform.gameObject : null;
        }

        if (!hoveredWeaponEntryHighlight.background)
            hoveredWeaponEntryHighlight.background = CreateRuntimeHoveredWeaponEntryHighlightElement(
                "HoveredWeaponButtonBackground",
                itemsWeaponsHighlight.background,
                WithAlpha(pipBoyDarkColor, 0.85f),
                weaponsListParent);

        if (!hoveredWeaponEntryHighlight.outline)
            hoveredWeaponEntryHighlight.outline = CreateRuntimeHoveredWeaponEntryHighlightElement(
                "HoveredWeaponButtonOutline",
                itemsWeaponsHighlight.outline,
                pipBoyLightColor,
                weaponsListParent);
    }

    private void AutoWireHoveredAidEntryHighlight()
    {
        bool hasOutline = hoveredAidEntryHighlight.outline != null;
        bool hasBackground = hoveredAidEntryHighlight.background != null;
        if (hasOutline && hasBackground)
            return;

        Transform aidListParent = ResolveAidListParent();

        if (!hasOutline)
        {
            Transform outlineTransform =
                FindChildComponentByNameInRoot<Transform>("ButtonOutline", aidListParent);

            if (!outlineTransform && itemsAidPanel)
                outlineTransform = FindChildComponentByNameInRoot<Transform>("ButtonOutline", itemsAidPanel.transform);

            hoveredAidEntryHighlight.outline = outlineTransform ? outlineTransform.gameObject : null;
        }

        if (!hasBackground)
        {
            Transform backgroundTransform =
                FindChildComponentByNameInRoot<Transform>("ButtonBackground", aidListParent);

            if (!backgroundTransform && itemsAidPanel)
                backgroundTransform = FindChildComponentByNameInRoot<Transform>("ButtonBackground", itemsAidPanel.transform);

            hoveredAidEntryHighlight.background = backgroundTransform ? backgroundTransform.gameObject : null;
        }

        if (!hoveredAidEntryHighlight.background)
            hoveredAidEntryHighlight.background = CreateRuntimeHoveredAidEntryHighlightElement(
                "HoveredAidButtonBackground",
                itemsAidHighlight.background,
                WithAlpha(pipBoyDarkColor, 0.85f),
                aidListParent);

        if (!hoveredAidEntryHighlight.outline)
            hoveredAidEntryHighlight.outline = CreateRuntimeHoveredAidEntryHighlightElement(
                "HoveredAidButtonOutline",
                itemsAidHighlight.outline,
                pipBoyLightColor,
                aidListParent);
    }

    private void AutoWireHoveredMiscEntryHighlight()
    {
        bool hasOutline = hoveredMiscEntryHighlight.outline != null;
        bool hasBackground = hoveredMiscEntryHighlight.background != null;
        if (hasOutline && hasBackground)
            return;

        Transform miscListParent = ResolveMiscListParent();

        if (!hasOutline)
        {
            Transform outlineTransform =
                FindChildComponentByNameInRoot<Transform>("ButtonOutline", miscListParent);

            if (!outlineTransform && itemsMiscPanel)
                outlineTransform = FindChildComponentByNameInRoot<Transform>("ButtonOutline", itemsMiscPanel.transform);

            hoveredMiscEntryHighlight.outline = outlineTransform ? outlineTransform.gameObject : null;
        }

        if (!hasBackground)
        {
            Transform backgroundTransform =
                FindChildComponentByNameInRoot<Transform>("ButtonBackground", miscListParent);

            if (!backgroundTransform && itemsMiscPanel)
                backgroundTransform = FindChildComponentByNameInRoot<Transform>("ButtonBackground", itemsMiscPanel.transform);

            hoveredMiscEntryHighlight.background = backgroundTransform ? backgroundTransform.gameObject : null;
        }

        if (!hoveredMiscEntryHighlight.background)
            hoveredMiscEntryHighlight.background = CreateRuntimeHoveredMiscEntryHighlightElement(
                "HoveredMiscButtonBackground",
                itemsMiscHighlight.background,
                WithAlpha(pipBoyDarkColor, 0.85f),
                miscListParent);

        if (!hoveredMiscEntryHighlight.outline)
            hoveredMiscEntryHighlight.outline = CreateRuntimeHoveredMiscEntryHighlightElement(
                "HoveredMiscButtonOutline",
                itemsMiscHighlight.outline,
                pipBoyLightColor,
                miscListParent);
    }

    private void AutoWireHoveredAmmoEntryHighlight()
    {
        bool hasOutline = hoveredAmmoEntryHighlight.outline != null;
        bool hasBackground = hoveredAmmoEntryHighlight.background != null;
        if (hasOutline && hasBackground)
            return;

        Transform ammoListParent = ResolveAmmoListParent();

        if (!hasOutline)
        {
            Transform outlineTransform =
                FindChildComponentByNameInRoot<Transform>("ButtonOutline", ammoListParent);

            if (!outlineTransform && itemsAmmoPanel)
                outlineTransform = FindChildComponentByNameInRoot<Transform>("ButtonOutline", itemsAmmoPanel.transform);

            hoveredAmmoEntryHighlight.outline = outlineTransform ? outlineTransform.gameObject : null;
        }

        if (!hasBackground)
        {
            Transform backgroundTransform =
                FindChildComponentByNameInRoot<Transform>("ButtonBackground", ammoListParent);

            if (!backgroundTransform && itemsAmmoPanel)
                backgroundTransform = FindChildComponentByNameInRoot<Transform>("ButtonBackground", itemsAmmoPanel.transform);

            hoveredAmmoEntryHighlight.background = backgroundTransform ? backgroundTransform.gameObject : null;
        }

        if (!hoveredAmmoEntryHighlight.background)
            hoveredAmmoEntryHighlight.background = CreateRuntimeHoveredAmmoEntryHighlightElement(
                "HoveredAmmoButtonBackground",
                itemsAmmoHighlight.background,
                WithAlpha(pipBoyDarkColor, 0.85f),
                ammoListParent);

        if (!hoveredAmmoEntryHighlight.outline)
            hoveredAmmoEntryHighlight.outline = CreateRuntimeHoveredAmmoEntryHighlightElement(
                "HoveredAmmoButtonOutline",
                itemsAmmoHighlight.outline,
                pipBoyLightColor,
                ammoListParent);
    }

    private void AutoWireHoveredQuestEntryHighlight()
    {
        bool hasOutline = hoveredQuestEntryHighlight.outline != null;
        bool hasBackground = hoveredQuestEntryHighlight.background != null;

        Transform questsListParent = ResolveQuestsListParent();

        if (!hasOutline)
        {
            Transform outlineTransform =
                FindChildComponentByNameInRoot<Transform>("ButtonOutline", questsListParent);

            if (!outlineTransform && dataQuestsPanel)
                outlineTransform = FindChildComponentByNameInRoot<Transform>("ButtonOutline", dataQuestsPanel.transform);

            hoveredQuestEntryHighlight.outline = outlineTransform ? outlineTransform.gameObject : null;
        }

        if (!hasBackground)
        {
            Transform backgroundTransform =
                FindChildComponentByNameInRoot<Transform>("ButtonBackground", questsListParent);

            if (!backgroundTransform && dataQuestsPanel)
                backgroundTransform = FindChildComponentByNameInRoot<Transform>("ButtonBackground", dataQuestsPanel.transform);

            hoveredQuestEntryHighlight.background = backgroundTransform ? backgroundTransform.gameObject : null;
        }

        ConfigureHoveredEntryHighlightElement(hoveredQuestEntryHighlight.outline, questsListParent);
        ConfigureHoveredEntryHighlightElement(hoveredQuestEntryHighlight.background, questsListParent);
    }

    private void ConfigureHoveredEntryHighlightElement(GameObject highlightObject, Transform listParent)
    {
        if (!highlightObject)
            return;

        RectTransform rectTransform = highlightObject.transform as RectTransform;
        if (rectTransform)
        {
            if (listParent && rectTransform.parent != listParent)
                rectTransform.SetParent(listParent, false);

            rectTransform.localScale = Vector3.one;
            rectTransform.localRotation = Quaternion.identity;
        }

        EnsureLayoutIgnored(highlightObject);
        DisableGraphicRaycasts(highlightObject, true);
        PrepareButtonHighlightGraphic(highlightObject, true);
        SetActiveSafe(highlightObject, false);
    }

    private GameObject CreateRuntimeHoveredWeaponEntryHighlightElement(
        string objectName,
        GameObject templateObject,
        Color fallbackColor,
        Transform fallbackParent)
    {
        return CreateRuntimeHoveredEntryHighlightElement(
            objectName,
            templateObject,
            fallbackColor,
            fallbackParent,
            itemsWeaponsPanel);
    }

    private GameObject CreateRuntimeHoveredAidEntryHighlightElement(
        string objectName,
        GameObject templateObject,
        Color fallbackColor,
        Transform fallbackParent)
    {
        return CreateRuntimeHoveredEntryHighlightElement(
            objectName,
            templateObject,
            fallbackColor,
            fallbackParent,
            itemsAidPanel);
    }

    private GameObject CreateRuntimeHoveredMiscEntryHighlightElement(
        string objectName,
        GameObject templateObject,
        Color fallbackColor,
        Transform fallbackParent)
    {
        return CreateRuntimeHoveredEntryHighlightElement(
            objectName,
            templateObject,
            fallbackColor,
            fallbackParent,
            itemsMiscPanel);
    }

    private GameObject CreateRuntimeHoveredAmmoEntryHighlightElement(
        string objectName,
        GameObject templateObject,
        Color fallbackColor,
        Transform fallbackParent)
    {
        return CreateRuntimeHoveredEntryHighlightElement(
            objectName,
            templateObject,
            fallbackColor,
            fallbackParent,
            itemsAmmoPanel);
    }

    private GameObject CreateRuntimeHoveredEntryHighlightElement(
        string objectName,
        GameObject templateObject,
        Color fallbackColor,
        Transform fallbackParent,
        GameObject fallbackPanel)
    {
        Transform parentTransform = fallbackParent;
        if (!parentTransform && fallbackPanel)
            parentTransform = fallbackPanel.transform;

        if (!parentTransform)
            return null;

        GameObject highlightObject;

        if (templateObject)
        {
            highlightObject = Instantiate(templateObject, parentTransform);
            highlightObject.name = objectName;
        }
        else
        {
            highlightObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform createdRectTransform = highlightObject.GetComponent<RectTransform>();
            if (createdRectTransform)
                createdRectTransform.SetParent(parentTransform, false);

            Image createdImage = highlightObject.GetComponent<Image>();
            if (createdImage)
            {
                createdImage.color = fallbackColor;
                createdImage.raycastTarget = false;
            }
        }

        EnsureLayoutIgnored(highlightObject);
        DisableGraphicRaycasts(highlightObject);
        SetActiveSafe(highlightObject, false);

        return highlightObject;
    }

    private static void DisableButtonHighlightRaycasts(ButtonHighlight highlight)
    {
        DisableGraphicRaycasts(highlight.outline, true);
        DisableGraphicRaycasts(highlight.background, true);
    }

    private static void DisableGraphicRaycasts(GameObject rootObject, bool disableMasking = false)
    {
        if (!rootObject) return;

        Graphic[] graphics = rootObject.GetComponentsInChildren<Graphic>(true);
        for (int i = 0; i < graphics.Length; i++)
        {
            Graphic graphic = graphics[i];
            if (!graphic) continue;
            graphic.raycastTarget = false;
            if (disableMasking && graphic is MaskableGraphic maskableGraphic)
                maskableGraphic.maskable = false;
        }
    }

    private void ApplyPipBoyPaletteColorOverrides()
    {
        bool appliedToConfiguredRoot = false;

        if (pipBoyCanvasGroup)
        {
            ApplyPipBoyPaletteColorOverrides(pipBoyCanvasGroup.gameObject);
            appliedToConfiguredRoot = true;
        }

        if (pipBoyBackgroundRoot)
        {
            ApplyPipBoyPaletteColorOverrides(pipBoyBackgroundRoot);
            appliedToConfiguredRoot = true;
        }

        if (!appliedToConfiguredRoot && transform)
            ApplyPipBoyPaletteColorOverrides(gameObject);
    }

    private void ApplyPipBoyPaletteColorOverrides(GameObject rootObject)
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
            return WithAlpha(pipBoyLightColor, sourceColor.a);

        if (IsApproximatelyColor(sourceColor, 0.0f, 0.0f, 0.0f))
            return WithAlpha(pipBoyDarkColor, sourceColor.a);

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
        Color targetBackgroundColor = WithAlpha(pipBoyDarkColor, 1.0f);

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

    private void AutoWireHoveredWeaponStatObjects()
    {
        bool hasAssignedObjects = hoveredWeaponStatObjects != null && hoveredWeaponStatObjects.Length > 0;
        if (hasAssignedObjects) return;

        hoveredWeaponStatObjects = new GameObject[DefaultHoveredWeaponStatObjectNames.Length];

        for (int i = 0; i < DefaultHoveredWeaponStatObjectNames.Length; i++)
        {
            Transform objectTransform = FindChildComponentByName<Transform>(DefaultHoveredWeaponStatObjectNames[i]);
            hoveredWeaponStatObjects[i] = objectTransform ? objectTransform.gameObject : null;
        }
    }

    private void DisableHoveredWeaponStatObjectRaycasts()
    {
        DisableHoveredStatObjectRaycasts(hoveredWeaponStatObjects);
    }

    private void DisableHoveredAidStatObjectRaycasts()
    {
        DisableHoveredStatObjectRaycasts(hoveredAidStatObjects);
    }

    private void DisableHoveredMiscStatObjectRaycasts()
    {
        DisableHoveredStatObjectRaycasts(hoveredMiscStatObjects);
    }

    private void DisableHoveredAmmoStatObjectRaycasts()
    {
        DisableHoveredStatObjectRaycasts(hoveredAmmoStatObjects);
    }

    private static void DisableHoveredStatObjectRaycasts(GameObject[] hoveredStatObjects)
    {
        if (hoveredStatObjects == null) return;

        for (int i = 0; i < hoveredStatObjects.Length; i++)
        {
            GameObject hoveredObject = hoveredStatObjects[i];
            if (!hoveredObject) continue;

            Graphic[] graphics = hoveredObject.GetComponentsInChildren<Graphic>(true);
            for (int graphicIndex = 0; graphicIndex < graphics.Length; graphicIndex++)
            {
                if (!graphics[graphicIndex]) continue;
                graphics[graphicIndex].raycastTarget = false;
            }
        }
    }

    private void SetHoveredWeaponStatObjectsVisible(bool visible)
    {
        SetHoveredStatObjectsVisible(hoveredWeaponStatObjects, visible);
    }

    private void SetHoveredAidStatObjectsVisible(bool visible)
    {
        SetHoveredStatObjectsVisible(hoveredAidStatObjects, visible);
    }

    private void SetHoveredMiscStatObjectsVisible(bool visible)
    {
        SetHoveredStatObjectsVisible(hoveredMiscStatObjects, visible);
    }

    private void SetHoveredAmmoStatObjectsVisible(bool visible)
    {
        SetHoveredStatObjectsVisible(hoveredAmmoStatObjects, visible);
    }

    private void SetHoveredStatObjectsVisible(GameObject[] hoveredStatObjects, bool visible)
    {
        if (hoveredStatObjects == null) return;

        for (int i = 0; i < hoveredStatObjects.Length; i++)
            SetActiveSafe(hoveredStatObjects[i], visible);
    }

    private void AutoWireHoveredAidStatObjects()
    {
        bool hasAssignedObjects = hoveredAidStatObjects != null && hoveredAidStatObjects.Length > 0;
        if (hasAssignedObjects) return;

        hoveredAidStatObjects = new GameObject[DefaultHoveredAidStatObjectNames.Length];

        Transform searchRoot = itemsAidPanel ? itemsAidPanel.transform : null;
        for (int i = 0; i < DefaultHoveredAidStatObjectNames.Length; i++)
        {
            hoveredAidStatObjects[i] = ResolveAidHoveredStatObjectByName(DefaultHoveredAidStatObjectNames[i], searchRoot);
        }
    }

    private GameObject ResolveAidHoveredStatObjectByName(string objectName, Transform searchRoot)
    {
        if (objectName == "EffectsText" && aidEffectsText)
            return aidEffectsText.gameObject;

        if (objectName == "EffectsItemText" && aidEffectsItemText)
            return aidEffectsItemText.gameObject;

        Transform objectTransform = FindChildComponentByNameInRoot<Transform>(objectName, searchRoot);
        return objectTransform ? objectTransform.gameObject : null;
    }

    private void AutoWireHoveredMiscStatObjects()
    {
        bool hasAssignedObjects = hoveredMiscStatObjects != null && hoveredMiscStatObjects.Length > 0;
        if (hasAssignedObjects) return;

        hoveredMiscStatObjects = new GameObject[DefaultHoveredMiscStatObjectNames.Length];

        Transform searchRoot = itemsMiscPanel ? itemsMiscPanel.transform : null;
        for (int i = 0; i < DefaultHoveredMiscStatObjectNames.Length; i++)
        {
            Transform objectTransform = FindChildComponentByNameInRoot<Transform>(DefaultHoveredMiscStatObjectNames[i], searchRoot);
            hoveredMiscStatObjects[i] = objectTransform ? objectTransform.gameObject : null;
        }
    }

    private void AutoWireHoveredAmmoStatObjects()
    {
        bool hasAssignedObjects = hoveredAmmoStatObjects != null && hoveredAmmoStatObjects.Length > 0;
        if (hasAssignedObjects) return;

        hoveredAmmoStatObjects = new GameObject[DefaultHoveredAmmoStatObjectNames.Length];

        Transform searchRoot = itemsAmmoPanel ? itemsAmmoPanel.transform : null;
        for (int i = 0; i < DefaultHoveredAmmoStatObjectNames.Length; i++)
        {
            Transform objectTransform = FindChildComponentByNameInRoot<Transform>(DefaultHoveredAmmoStatObjectNames[i], searchRoot);
            hoveredAmmoStatObjects[i] = objectTransform ? objectTransform.gameObject : null;
        }
    }

    private Transform ResolveWeaponsListParent()
    {
        if (weaponsScrollRect && weaponsScrollRect.content)
            return weaponsScrollRect.content;

        if (!weaponsListContentRoot) return null;

        // If Viewport is assigned by mistake, use its Content child.
        if (weaponsListContentRoot.name == "Viewport")
        {
            Transform contentChild = weaponsListContentRoot.Find("Content");
            if (contentChild)
                return contentChild;
        }

        // If Scroll View root is assigned, use its Viewport/Content.
        Transform viewport = weaponsListContentRoot.Find("Viewport");
        if (viewport)
        {
            Transform viewportContent = viewport.Find("Content");
            if (viewportContent)
                return viewportContent;
        }

        return weaponsListContentRoot;
    }

    private Transform ResolveAidListParent()
    {
        if (aidScrollRect && aidScrollRect.content)
            return aidScrollRect.content;

        if (!aidListContentRoot) return null;

        // If Viewport is assigned by mistake, use its Content child.
        if (aidListContentRoot.name == "Viewport")
        {
            Transform contentChild = aidListContentRoot.Find("Content");
            if (contentChild)
                return contentChild;
        }

        // If Scroll View root is assigned, use its Viewport/Content.
        Transform viewport = aidListContentRoot.Find("Viewport");
        if (viewport)
        {
            Transform viewportContent = viewport.Find("Content");
            if (viewportContent)
                return viewportContent;
        }

        return aidListContentRoot;
    }

    private Transform ResolveMiscListParent()
    {
        if (miscScrollRect && miscScrollRect.content)
            return miscScrollRect.content;

        if (!miscListContentRoot) return null;

        // If Viewport is assigned by mistake, use its Content child.
        if (miscListContentRoot.name == "Viewport")
        {
            Transform contentChild = miscListContentRoot.Find("Content");
            if (contentChild)
                return contentChild;
        }

        // If Scroll View root is assigned, use its Viewport/Content.
        Transform viewport = miscListContentRoot.Find("Viewport");
        if (viewport)
        {
            Transform viewportContent = viewport.Find("Content");
            if (viewportContent)
                return viewportContent;
        }

        return miscListContentRoot;
    }

    private Transform ResolveAmmoListParent()
    {
        if (ammoScrollRect && ammoScrollRect.content)
            return ammoScrollRect.content;

        if (!ammoListContentRoot) return null;

        // If Viewport is assigned by mistake, use its Content child.
        if (ammoListContentRoot.name == "Viewport")
        {
            Transform contentChild = ammoListContentRoot.Find("Content");
            if (contentChild)
                return contentChild;
        }

        // If Scroll View root is assigned, use its Viewport/Content.
        Transform viewport = ammoListContentRoot.Find("Viewport");
        if (viewport)
        {
            Transform viewportContent = viewport.Find("Content");
            if (viewportContent)
                return viewportContent;
        }

        return ammoListContentRoot;
    }

    private Transform ResolveQuestsListParent()
    {
        if (questsScrollRect && questsScrollRect.content)
            return questsScrollRect.content;

        if (!questsListContentRoot) return null;

        if (questsListContentRoot.name == "Viewport")
        {
            Transform contentChild = questsListContentRoot.Find("Content");
            if (contentChild)
                return contentChild;
        }

        Transform viewport = questsListContentRoot.Find("Viewport");
        if (viewport)
        {
            Transform contentChild = viewport.Find("Content");
            if (contentChild)
                return contentChild;
        }

        return questsListContentRoot;
    }

    private string GetDisplayNameFromDefinition(ScriptableObject definition, string fallback)
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

    private void RefreshStatsPlayerTexts()
    {
        if (!isOpen)
            return;

        // Stop if player state cannot be resolved.
        if (!playerState)
        {
            playerState = FindAnyObjectByType<PlayerState>();
            if (!playerState) return;
        }

        if (!playerInventory)
            playerInventory = FindAnyObjectByType<PlayerInventory>();

        int currentHp = Mathf.RoundToInt(playerState.GetHealthPoints());
        int maxHp = Mathf.RoundToInt(playerState.GetMaxHealthPoints());
        int currentAp = Mathf.RoundToInt(playerState.GetActionPoints());
        int maxAp = Mathf.RoundToInt(playerState.GetMaxActionPoints());
        int level = playerState.GetLevel();
        int experience = playerState.GetExperience();
        int damageResistance = playerInventory ? Mathf.Max(0, playerInventory.GetTotalDamageResistance()) : 0;
        int caps = playerInventory ? Mathf.Max(0, playerInventory.GetCaps()) : 0;
        float currentWeight = playerInventory ? Mathf.Max(0f, playerInventory.GetWeight()) : 0f;
        float maxWeight = playerInventory ? Mathf.Max(0f, playerInventory.GetMaxWeight()) : 0f;
        string weightText = $"{currentWeight:0.#}/{maxWeight:0.#}";
        string hpTextValue = $"{currentHp}/{maxHp}";
        string apTextValue = $"{currentAp}/{maxAp}";

        SetTextIfChanged(hpPlayerText, hpTextValue);
        SetTextIfChanged(drPlayerText, damageResistance.ToString());
        SetTextIfChanged(wgPlayerText, weightText);
        SetTextIfChanged(capsPlayerText, caps.ToString());
        SetTextIfChanged(apPlayerText, apTextValue);
        SetTextIfChanged(lvlPlayerText, level.ToString());
        SetTextIfChanged(xpPlayerText, experience.ToString());
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

    private static void SetImageFillAmountIfChanged(Image imageComponent, float fillAmount)
    {
        if (!imageComponent)
            return;

        float clampedFillAmount = Mathf.Clamp01(fillAmount);
        if (Mathf.Approximately(imageComponent.fillAmount, clampedFillAmount))
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

    private void AutoWireConditionBarFillImages()
    {
        resolvedCndBarFillImages.Clear();
        AddConditionBarFillImage(cndBarFillImage);

        Transform searchRoot = itemsWeaponsPanel ? itemsWeaponsPanel.transform : transform;
        if (searchRoot)
        {
            Image[] candidateImages = searchRoot.GetComponentsInChildren<Image>(true);
            for (int i = 0; i < candidateImages.Length; i++)
            {
                Image candidateImage = candidateImages[i];
                if (!candidateImage || candidateImage.name != "CNDBarFill")
                    continue;

                AddConditionBarFillImage(candidateImage);
            }
        }

        if (resolvedCndBarFillImages.Count == 0)
            AddConditionBarFillImage(FindChildComponentByName<Image>("CNDBarFill"));

        for (int i = 0; i < resolvedCndBarFillImages.Count; i++)
            ConfigureConditionBarFillImage(resolvedCndBarFillImages[i]);
    }

    private void AddConditionBarFillImage(Image imageComponent)
    {
        if (!imageComponent)
            return;

        if (resolvedCndBarFillImages.Contains(imageComponent))
            return;

        resolvedCndBarFillImages.Add(imageComponent);
    }

    private void SetConditionBarFillAmount(float fillAmount)
    {
        if (resolvedCndBarFillImages.Count == 0)
            AutoWireConditionBarFillImages();

        float clampedFillAmount = Mathf.Clamp01(fillAmount);
        for (int i = 0; i < resolvedCndBarFillImages.Count; i++)
            SetImageFillAmountIfChanged(resolvedCndBarFillImages[i], clampedFillAmount);
    }

    private static string FormatValue(float value)
    {
        return Mathf.Max(0.0f, value).ToString("0.##");
    }

    private static string BuildAidEffectsDisplayText(AidDefinition aidDefinition)
    {
        if (!aidDefinition)
            return string.Empty;

        System.Collections.Generic.List<AidEffectDefinition> effects = aidDefinition.GetEffects();
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

        if (target == AidEffectTarget.MaxActionPoints)
            return baseName;

        return $"Max {baseName}";
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

    private T FindChildComponentByNameInRoot<T>(string childName, Transform rootTransform) where T : Component
    {
        if (!rootTransform) return null;

        T[] components = rootTransform.GetComponentsInChildren<T>(true);

        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] != null && components[i].name == childName)
                return components[i];
        }

        return null;
    }
}
