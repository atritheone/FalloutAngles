// imports
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;



// class
namespace UI
{
    public class CrosshairController : MonoBehaviour
    {
        
        // variables
        // The RectTransform of the crosshair UI element.
        [SerializeField] private RectTransform crosshairRect;

        // The Canvas the crosshair belongs to.
        [SerializeField] private Canvas canvas;

        // Reference to the player's state (combat vs non-combat).
        [SerializeField] private PlayerState playerState;

        // Optional combat provider so UI crosshair uses the same recoil-adjusted screen point as firing logic.
        [SerializeField] private PlayerCombat playerCombat;

        // Optional orbit provider used to hide the crosshair during combat MMB orbit.
        [SerializeField] private CameraRigOrbit cameraRigOrbit;

        // Reference to player interaction for hover/target UI state.
        [SerializeField] private PlayerInteraction playerInteraction;

        // Camera used to raycast from the active crosshair point for hostile NPC tinting.
        [SerializeField] private Camera targetCamera;

        // UI graphics that should change colour when the crosshair is over an alerted enemy.
        [SerializeField] private Graphic[] crosshairTintGraphics;

        // Colour applied when the crosshair is over an aggroed or searching enemy.
        [SerializeField] private Color alertedEnemyCrosshairColor = Color.red;

        // Layers checked when testing whether the crosshair is over an enemy.
        [SerializeField] private LayerMask enemyTargetLayers = ~0;

        // Max distance for enemy crosshair colour checks.
        [SerializeField, Min(0.01f)] private float enemyTargetRayDistance = 250f;

        // Whether enemy target checks should include trigger colliders.
        [SerializeField] private QueryTriggerInteraction enemyTargetTriggerInteraction = QueryTriggerInteraction.Collide;

        // Optional container UI controller used to suppress crosshair while container UI is open.
        [SerializeField] private ContainerController containerController;

        // Optional terminal UI controller used to suppress crosshair while terminal UI is open.
        [SerializeField] private TerminalController terminalController;

        // Optional dialogue UI controller used to suppress crosshair while dialogue is open.
        [SerializeField] private DialogueController dialogueController;

        // Ring graphic shown only when an interactable target is hovered.
        [SerializeField] private GameObject hoverRing;

        // Cached RectTransform for positioning the hover ring around the active crosshair point.
        private RectTransform hoverRingRect;

        // Prompt text shown for the current interaction action.
        [SerializeField] private TMP_Text interactText;

        // Item display name text shown only for world item pickups.
        [SerializeField] private TMP_Text itemNameText;

        // Cached rects for positioning prompt text relative to crosshair.
        private RectTransform interactTextRect;
        private RectTransform itemNameTextRect;

        // Cached offsets from center-crosshair position to preserve existing layout.
        private Vector2 interactTextOffset;
        private Vector2 itemNameTextOffset;
        private bool hasCachedTextOffsets;

        // Cached RectTransform of the canvas for coordinate conversion.
        private RectTransform canvasRect;

        // Canvas group used to fade crosshair visuals without disabling this component.
        [SerializeField] private CanvasGroup crosshairCanvasGroup;

        // Track whether crosshair visuals are currently suppressed due to a blocking UI.
        private bool isSuppressedByBlockingUi;

        // Stores the normal alpha to restore after temporary suppression.
        private float visibleCrosshairAlpha = 1f;

        // Cached interaction UI state to avoid redundant text/layout churn.
        private bool hasInteractionUiCache;
        private bool cachedShowHover;
        private string cachedPrompt;
        private string cachedItemName;

        // Cached original crosshair graphic colours so alert tinting can restore them.
        private Color[] defaultCrosshairGraphicColors;
        private bool crosshairThreatColorActive;
        private bool hasAppliedCrosshairThreatColor;
        private Color lastAppliedAlertedEnemyCrosshairColor;

        // Cache for non-alloc enemy raycasts.
        private readonly RaycastHit[] enemyTargetHits = new RaycastHit[32];

        
        
        
        // methods
        private void Awake()
        {
            // If no crosshair RectTransform is assigned, try to get it from this object.
            if (crosshairRect == null)
                crosshairRect = GetComponent<RectTransform>();

            // If no canvas is assigned, search for one in the parent hierarchy.
            if (canvas == null)
                canvas = GetComponentInParent<Canvas>();

            // If no player state is assigned, find it in the scene.
            if (playerState == null)
                playerState = FindAnyObjectByType<PlayerState>();

            // If no player interaction is assigned, find it in the scene.
            if (playerInteraction == null)
                playerInteraction = FindAnyObjectByType<PlayerInteraction>();

            if (targetCamera == null)
                targetCamera = Camera.main;

            // If no player combat is assigned, find it in the scene.
            if (playerCombat == null)
                playerCombat = FindAnyObjectByType<PlayerCombat>();

            // If no orbit controller is assigned, find it in the scene.
            if (cameraRigOrbit == null)
                cameraRigOrbit = FindAnyObjectByType<CameraRigOrbit>();

            if (containerController == null)
                containerController = ContainerController.FindFirstInSceneIncludingInactive();

            if (terminalController == null)
                terminalController = TerminalController.FindFirstInSceneIncludingInactive();

            if (dialogueController == null)
                dialogueController = DialogueController.FindFirstInSceneIncludingInactive();

            // Auto-wire known child objects if fields were not assigned in inspector.
            if (hoverRing == null)
                hoverRing = FindChildByName("HoverRing");

            if (hoverRing != null)
                hoverRingRect = hoverRing.GetComponent<RectTransform>();

            if (interactText == null)
                interactText = FindChildComponentByName<TMP_Text>("InteractText");

            if (itemNameText == null)
                itemNameText = FindChildComponentByName<TMP_Text>("ItemNameText");

            if (interactText != null)
                interactTextRect = interactText.rectTransform;

            if (itemNameText != null)
                itemNameTextRect = itemNameText.rectTransform;

            // Cache the canvas RectTransform if a canvas exists.
            if (canvas != null)
                canvasRect = canvas.transform as RectTransform;

            if (crosshairCanvasGroup == null && crosshairRect != null)
                crosshairCanvasGroup = crosshairRect.GetComponent<CanvasGroup>();

            if (crosshairCanvasGroup == null && crosshairRect != null)
                crosshairCanvasGroup = crosshairRect.gameObject.AddComponent<CanvasGroup>();

            if (crosshairCanvasGroup != null)
            {
                visibleCrosshairAlpha = Mathf.Clamp01(crosshairCanvasGroup.alpha);
                crosshairCanvasGroup.interactable = false;
                crosshairCanvasGroup.blocksRaycasts = false;
            }

            // Start hidden until we have a valid interact target.
            SetInteractionUi(false, string.Empty, string.Empty);
            CacheCrosshairTintGraphics();
            SetCrosshairThreatColorActive(false);
        }


        
        private void Update()
        {
            bool suppressForBlockingUi = IsContainerUiOpen() || IsTerminalUiOpen() || IsDialogueUiOpen();
            bool combatMode = playerState != null && playerState.GetCombatMode();
            bool suppressForCombatOrbit = IsCombatOrbitCrosshairSuppressed(combatMode);
            SetCrosshairSuppressedForBlockingUi(suppressForBlockingUi || suppressForCombatOrbit);

            if (suppressForBlockingUi || suppressForCombatOrbit)
            {
                SetInteractionUi(false, string.Empty, string.Empty);
                SetCrosshairThreatColorActive(false);
                return;
            }

            // Stop if any required references are missing.
            if (crosshairRect == null || canvasRect == null)
            {
                SetCrosshairThreatColorActive(false);
                return;
            }

            // Choose screen position based on whether the player is in combat mode.
            Vector2 screenPosition = combatMode
                ? GetCombatScreenPosition()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            // Determine which camera to use depending on canvas render mode.
            Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

            CacheTextOffsetsIfNeeded(uiCamera);

            // Convert screen position to local canvas space.
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    canvasRect,
                    screenPosition,
                    uiCamera,
                    out Vector2 localPoint))
            {
                // Move the crosshair to the calculated local position.
                crosshairRect.anchoredPosition = localPoint;

                // Keep hover ring centered on the same screen point, even if it is not a child of crosshair.
                UpdateHoverRingPosition(screenPosition, uiCamera);

                // Keep interaction labels around the same active crosshair point.
                UpdateTextPosition(interactTextRect, interactTextOffset, screenPosition, uiCamera);
                UpdateTextPosition(itemNameTextRect, itemNameTextOffset, screenPosition, uiCamera);
            }

            // Keep crosshair UI state synced to the current hover target.
            UpdateInteractionUi();
            UpdateCrosshairThreatColor(screenPosition);
        }


        
        private static Vector2 GetMouseScreenPosition()
        {
            Mouse mouse = Mouse.current;

            // If no mouse is available, default to the center of the screen.
            if (mouse == null)
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            // Read and return the current mouse position.
            return mouse.position.ReadValue();
        }


        private Vector2 GetCombatScreenPosition()
        {
            if (playerCombat != null)
                return playerCombat.GetCurrentCrosshairScreenPoint();

            return GetMouseScreenPosition();
        }


        private bool IsCombatOrbitCrosshairSuppressed(bool combatMode)
        {
            if (!combatMode)
                return false;

            if (cameraRigOrbit == null)
                cameraRigOrbit = FindAnyObjectByType<CameraRigOrbit>();

            return cameraRigOrbit != null && cameraRigOrbit.IsCombatOrbitCrosshairFrozen;
        }


        private void UpdateInteractionUi()
        {
            // If we cannot read interaction state, hide interaction UI.
            if (playerInteraction == null)
            {
                SetInteractionUi(false, string.Empty, string.Empty);
                return;
            }

            // Read current hovered target.
            Component hoveredTarget = playerInteraction.GetCurrentTarget();
            bool hasTarget = hoveredTarget != null;

            // If no target is hovered, hide everything.
            if (!hasTarget)
            {
                SetInteractionUi(false, string.Empty, string.Empty);
                return;
            }

            // For world pickups, force standardized prompt + item name.
            if (hoveredTarget is WorldItem worldItem)
            {
                SetInteractionUi(true, "E) Pick Up", worldItem.GetDisplayName());
                return;
            }

            if (hoveredTarget is Container container && container.UsesLootInteractionPrompt())
            {
                string lootPrompt = FormatPrompt(playerInteraction.GetCurrentPromptText());
                SetInteractionUi(true, lootPrompt, container.GetContainerName());
                return;
            }

            if (hoveredTarget is NPCState npcState && TryGetDeathLootContainer(npcState, out Container corpseContainer))
            {
                string lootPrompt = FormatPrompt(playerInteraction.GetCurrentPromptText());
                SetInteractionUi(true, lootPrompt, corpseContainer.GetContainerName());
                return;
            }

            if (TryGetHoveredNpc(hoveredTarget, out NPC npc))
            {
                string npcName = GetSafeNpcName(npc);
                string npcPrompt = npc.HasDialogue() ? "E) Talk" : string.Empty;
                SetInteractionUi(true, npcPrompt, npcName);
                return;
            }

            // For other interactables, show only the relevant action prompt.
            string prompt = FormatPrompt(playerInteraction.GetCurrentPromptText());
            SetInteractionUi(true, prompt, string.Empty);
        }


        private void UpdateCrosshairThreatColor(Vector2 screenPosition)
        {
            SetCrosshairThreatColorActive(IsCrosshairOverAlertedEnemy(screenPosition));
        }


        private bool IsCrosshairOverAlertedEnemy(Vector2 screenPosition)
        {
            Camera cam = GetTargetCamera();
            if (cam == null)
                return false;

            float rayDistance = Mathf.Max(0.01f, enemyTargetRayDistance);
            Ray ray = cam.ScreenPointToRay(screenPosition);
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                enemyTargetHits,
                rayDistance,
                enemyTargetLayers,
                enemyTargetTriggerInteraction);

            if (hitCount <= 0)
                return false;

            Collider nearestCollider = null;
            float nearestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = enemyTargetHits[i].collider;
                if (hitCollider == null)
                    continue;

                Transform hitTransform = hitCollider.transform;
                if (playerState != null && hitTransform != null && hitTransform.IsChildOf(playerState.transform))
                    continue;

                float hitDistance = enemyTargetHits[i].distance;
                if (hitDistance >= nearestDistance)
                    continue;

                nearestCollider = hitCollider;
                nearestDistance = hitDistance;
            }

            if (nearestCollider == null)
                return false;

            return TryGetNpcState(nearestCollider, out NPCState npcState) &&
                   IsAlertedEnemy(npcState, nearestCollider);
        }


        private Camera GetTargetCamera()
        {
            if (targetCamera != null)
                return targetCamera;

            targetCamera = Camera.main;
            return targetCamera;
        }


        private static bool TryGetNpcState(Collider hitCollider, out NPCState npcState)
        {
            npcState = null;

            if (hitCollider == null)
                return false;

            npcState = hitCollider.GetComponentInParent<NPCState>();
            if (npcState != null)
                return true;

            npcState = hitCollider.GetComponentInChildren<NPCState>(true);
            if (npcState != null)
                return true;

            NPC npc = hitCollider.GetComponentInParent<NPC>();
            if (npc == null)
                npc = hitCollider.GetComponentInChildren<NPC>(true);

            if (npc == null)
                return false;

            npcState = npc.GetState();
            return npcState != null;
        }


        private static bool IsAlertedEnemy(NPCState npcState, Collider hitCollider)
        {
            if (npcState == null || npcState.IsDead())
                return false;

            if (npcState.GetCombatMode())
                return true;

            NPCCombat npcCombat = GetNpcCombat(npcState, hitCollider);
            return npcCombat != null && npcCombat.IsSearchingForPlayer();
        }


        private static NPCCombat GetNpcCombat(NPCState npcState, Collider hitCollider)
        {
            NPCCombat npcCombat = null;

            if (npcState != null)
            {
                npcCombat = npcState.GetComponent<NPCCombat>();
                if (npcCombat == null)
                    npcCombat = npcState.GetComponentInParent<NPCCombat>();
                if (npcCombat == null)
                    npcCombat = npcState.GetComponentInChildren<NPCCombat>(true);
            }

            if (npcCombat != null || hitCollider == null)
                return npcCombat;

            npcCombat = hitCollider.GetComponentInParent<NPCCombat>();
            if (npcCombat == null)
                npcCombat = hitCollider.GetComponentInChildren<NPCCombat>(true);

            return npcCombat;
        }


        private void CacheCrosshairTintGraphics()
        {
            if ((crosshairTintGraphics == null || crosshairTintGraphics.Length == 0) && crosshairRect != null)
                crosshairTintGraphics = crosshairRect.GetComponentsInChildren<Graphic>(true);

            if (crosshairTintGraphics == null || crosshairTintGraphics.Length == 0)
                crosshairTintGraphics = GetComponentsInChildren<Graphic>(true);

            int graphicCount = crosshairTintGraphics != null ? crosshairTintGraphics.Length : 0;
            defaultCrosshairGraphicColors = new Color[graphicCount];

            for (int i = 0; i < graphicCount; i++)
            {
                Graphic graphic = crosshairTintGraphics[i];
                defaultCrosshairGraphicColors[i] = graphic != null ? graphic.color : Color.white;
            }
        }


        private void SetCrosshairThreatColorActive(bool active)
        {
            if (crosshairTintGraphics == null ||
                defaultCrosshairGraphicColors == null ||
                defaultCrosshairGraphicColors.Length != crosshairTintGraphics.Length)
            {
                CacheCrosshairTintGraphics();
            }

            if (hasAppliedCrosshairThreatColor &&
                crosshairThreatColorActive == active &&
                (!active || lastAppliedAlertedEnemyCrosshairColor == alertedEnemyCrosshairColor))
            {
                return;
            }

            crosshairThreatColorActive = active;
            hasAppliedCrosshairThreatColor = true;
            lastAppliedAlertedEnemyCrosshairColor = alertedEnemyCrosshairColor;

            if (crosshairTintGraphics == null)
                return;

            for (int i = 0; i < crosshairTintGraphics.Length; i++)
            {
                Graphic graphic = crosshairTintGraphics[i];
                if (graphic == null)
                    continue;

                Color targetColor = active
                    ? alertedEnemyCrosshairColor
                    : GetDefaultCrosshairGraphicColor(i);

                if (graphic.color != targetColor)
                    graphic.color = targetColor;
            }
        }


        private Color GetDefaultCrosshairGraphicColor(int index)
        {
            if (defaultCrosshairGraphicColors == null ||
                index < 0 ||
                index >= defaultCrosshairGraphicColors.Length)
            {
                return Color.white;
            }

            return defaultCrosshairGraphicColors[index];
        }


        private void UpdateHoverRingPosition(Vector2 screenPosition, Camera uiCamera)
        {
            if (hoverRingRect == null)
                return;

            RectTransform ringParentRect = hoverRingRect.parent as RectTransform;
            if (ringParentRect == null)
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    ringParentRect,
                    screenPosition,
                    uiCamera,
                    out Vector2 ringLocalPoint))
            {
                hoverRingRect.anchoredPosition = ringLocalPoint;
            }
        }


        private void CacheTextOffsetsIfNeeded(Camera uiCamera)
        {
            if (hasCachedTextOffsets)
                return;

            interactTextOffset = GetOffsetFromScreenCenter(interactTextRect, uiCamera);
            itemNameTextOffset = GetOffsetFromScreenCenter(itemNameTextRect, uiCamera);
            hasCachedTextOffsets = true;
        }


        private static Vector2 GetOffsetFromScreenCenter(RectTransform uiRect, Camera uiCamera)
        {
            if (uiRect == null)
                return Vector2.zero;

            RectTransform parentRect = uiRect.parent as RectTransform;
            if (parentRect == null)
                return uiRect.anchoredPosition;

            Vector2 centerScreen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    centerScreen,
                    uiCamera,
                    out Vector2 centerLocalPoint))
            {
                return uiRect.anchoredPosition - centerLocalPoint;
            }

            return uiRect.anchoredPosition;
        }


        private static void UpdateTextPosition(
            RectTransform textRect,
            Vector2 offset,
            Vector2 screenPosition,
            Camera uiCamera)
        {
            if (textRect == null)
                return;

            RectTransform parentRect = textRect.parent as RectTransform;
            if (parentRect == null)
                return;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    screenPosition,
                    uiCamera,
                    out Vector2 localPoint))
            {
                textRect.anchoredPosition = localPoint + offset;
            }
        }


        private void SetInteractionUi(bool showHover, string prompt, string itemName)
        {
            if (hasInteractionUiCache &&
                cachedShowHover == showHover &&
                cachedPrompt == prompt &&
                cachedItemName == itemName)
            {
                return;
            }

            hasInteractionUiCache = true;
            cachedShowHover = showHover;
            cachedPrompt = prompt;
            cachedItemName = itemName;

            if (hoverRing != null)
            {
                if (hoverRing.activeSelf != showHover)
                    hoverRing.SetActive(showHover);
            }

            if (interactText != null)
            {
                bool hasPrompt = showHover && !string.IsNullOrWhiteSpace(prompt);
                if (interactText.gameObject.activeSelf != hasPrompt)
                    interactText.gameObject.SetActive(hasPrompt);

                string promptText = hasPrompt ? prompt : string.Empty;
                if (interactText.text != promptText)
                    interactText.text = promptText;
            }

            if (itemNameText != null)
            {
                bool hasName = showHover && !string.IsNullOrWhiteSpace(itemName);
                if (itemNameText.gameObject.activeSelf != hasName)
                    itemNameText.gameObject.SetActive(hasName);

                string itemText = hasName ? itemName : string.Empty;
                if (itemNameText.text != itemText)
                    itemNameText.text = itemText;
            }
        }


        private static string FormatPrompt(string rawPrompt)
        {
            // Normalize prompt to a readable "E) <Action>" format.
            if (string.IsNullOrWhiteSpace(rawPrompt))
                return string.Empty;

            string prompt = rawPrompt.Trim();

            if (prompt.StartsWith("E)"))
                return prompt;

            return "E) " + prompt;
        }


        private static bool TryGetHoveredNpc(Component hoveredTarget, out NPC npc)
        {
            npc = null;

            if (hoveredTarget == null)
                return false;

            npc = hoveredTarget.GetComponentInParent<NPC>();
            return npc != null;
        }


        private static bool TryGetDeathLootContainer(NPCState npcState, out Container container)
        {
            container = null;

            if (npcState == null || !npcState.IsDead())
                return false;

            container = npcState.GetDeathLootContainer();
            if (!container)
                container = npcState.GetComponent<Container>();
            if (!container)
                container = npcState.GetComponentInParent<Container>();
            if (!container)
                container = npcState.GetComponentInChildren<Container>(true);

            return container != null;
        }


        private static string GetSafeNpcName(NPC npc)
        {
            if (npc == null)
                return "NPC";

            string npcName = npc.GetNPCName();
            return string.IsNullOrWhiteSpace(npcName) ? "NPC" : npcName.Trim();
        }


        private bool IsContainerUiOpen()
        {
            if (containerController == null)
                containerController = ContainerController.FindFirstInSceneIncludingInactive();

            return containerController != null && containerController.IsOpen();
        }


        private bool IsTerminalUiOpen()
        {
            if (terminalController == null)
                terminalController = TerminalController.FindFirstInSceneIncludingInactive();

            return terminalController != null && terminalController.IsOpen();
        }


        private bool IsDialogueUiOpen()
        {
            if (dialogueController == null)
                dialogueController = DialogueController.FindFirstInSceneIncludingInactive();

            return dialogueController != null && dialogueController.IsOpen();
        }


        private void SetCrosshairSuppressedForBlockingUi(bool suppressed)
        {
            if (isSuppressedByBlockingUi == suppressed)
                return;

            isSuppressedByBlockingUi = suppressed;

            if (crosshairCanvasGroup == null)
                return;

            if (suppressed)
            {
                visibleCrosshairAlpha = Mathf.Clamp01(crosshairCanvasGroup.alpha);
                crosshairCanvasGroup.alpha = 0f;
                return;
            }

            crosshairCanvasGroup.alpha = visibleCrosshairAlpha;
        }


        private GameObject FindChildByName(string childName)
        {
            Transform[] children = GetComponentsInChildren<Transform>(true);

            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name == childName)
                    return children[i].gameObject;
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
}
