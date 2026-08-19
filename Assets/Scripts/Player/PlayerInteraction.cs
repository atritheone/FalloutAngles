// imports
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Unity.Cinemachine;



// class
public class PlayerInteraction : MonoBehaviour
{
    private const float MinRayDistance = 0.01f;
    private const float MinFixedDeltaTime = 0.0001f;
    private const float MinGrabDirectionSqr = 0.000001f;
    private const int RaycastHitsCacheSize = 64;
    private const int SpherecastHitsCacheSize = 64;
    private const int HoverLookupCacheLimit = 512;

    
    // variables
    // Fired when the hovered interact target changes (useful for UI prompts).
    [Serializable] public class HoverChangedEvent : UnityEvent<string, Component> { }

    [Serializable]
    private class SceneReferencesGroup
    {
        // Camera used to raycast from the interaction screen point.
        public Camera mainCamera;

        // Optional reference to player state to match crosshair behavior.
        public PlayerState playerState;

        // Optional combat provider so interaction/grab uses the same recoil-adjusted screen point as the UI crosshair.
        public PlayerCombat playerCombat;

        // Pip-Boy controller used to block gameplay interaction while UI is open.
        public PipBoyController pipBoyController;

        // Container UI controller used to block gameplay interaction while looting.
        public ContainerController containerController;
    }

    [Serializable]
    private class InteractionRaycastGroup
    {
        // Which layers are considered interactable by raycast (set this to your world / items layers).
        public LayerMask interactLayers = ~0;

        // If true, use the largest configured interaction range instead of maxDistance.
        public bool useInfiniteRaycastDistance = true;

        // Max raycast distance (used only when infinite distance is off).
        public float maxDistance = 5f;

        // Max distance for picking up world items, even when general raycasts are infinite.
        public float maxWorldItemPickupDistance = 3f;

        // Max distance for interacting with non-world-item targets (for example containers/doors).
        public float maxInteractableDistance = 3f;

        // Max distance for looting dead NPCs. Checked against the hit corpse collider, not the NPC root transform.
        public float maxCorpseInteractDistance = 3f;

        // Radius used only when checking corpse limb colliders, so standing players can target ragdolls without pixel-perfect aim.
        public float corpseRaycastRadius = 0.35f;

        // If true, trigger colliders can be interacted with via raycast.
        public bool includeTriggerColliders = true;
    }

    [Serializable]
    private class GrabSettingsGroup
    {
        // Layers considered grabbable.
        public LayerMask grabbableLayers;

        // Fixed distance from the grab origin where grabbed objects are held.
        public float fixedGrabDistance = 2.0f;

        // Raises the grab origin on the player so short hold distances can still line up near the crosshair.
        [Min(0f)] public float grabOriginHeightOffset = 0.6f;

        // Cap on grab follow speed while snapped to crosshair.
        public float maxGrabMoveSpeed = 40.0f;
    }

    [Serializable]
    private class EventsGroup
    {
        // Event invoked when hover target or prompt changes.
        public HoverChangedEvent onHoverChanged;
    }

    private struct HoverLookup
    {
        public WorldItem worldItem;
        public IPlayerInteractTarget interactTarget;
        public Component interactComponent;
        public NPC npc;
        public NPCState npcState;
    }

    [Header("Scene References")]
    [SerializeField] private SceneReferencesGroup sceneReferences = new SceneReferencesGroup();

    [Header("Interaction Raycast")]
    [SerializeField] private InteractionRaycastGroup interactionRaycast = new InteractionRaycastGroup();

    [Header("Grab Settings")]
    [SerializeField] private GrabSettingsGroup grabSettings = new GrabSettingsGroup();

    [Header("Events")]
    [SerializeField] private EventsGroup eventsGroup = new EventsGroup();

    [SerializeField, HideInInspector] private bool hasMigratedLegacyInspectorFields;

    [FormerlySerializedAs("mainCamera")] [SerializeField, HideInInspector] private Camera legacyMainCamera;
    [FormerlySerializedAs("playerState")] [SerializeField, HideInInspector] private PlayerState legacyPlayerState;
    [FormerlySerializedAs("pipBoyController")] [SerializeField, HideInInspector] private PipBoyController legacyPipBoyController;
    [FormerlySerializedAs("interactLayers")] [SerializeField, HideInInspector] private LayerMask legacyInteractLayers = ~0;
    [FormerlySerializedAs("useInfiniteRaycastDistance")] [SerializeField, HideInInspector] private bool legacyUseInfiniteRaycastDistance = true;
    [FormerlySerializedAs("maxDistance")] [SerializeField, HideInInspector] private float legacyMaxDistance = 5f;
    [FormerlySerializedAs("maxWorldItemPickupDistance")] [SerializeField, HideInInspector] private float legacyMaxWorldItemPickupDistance = 3f;
    [FormerlySerializedAs("maxInteractableDistance")] [SerializeField, HideInInspector] private float legacyMaxInteractableDistance = 3f;
    [FormerlySerializedAs("includeTriggerColliders")] [SerializeField, HideInInspector] private bool legacyIncludeTriggerColliders = true;
    [FormerlySerializedAs("onHoverChanged")] [SerializeField, HideInInspector] private HoverChangedEvent legacyOnHoverChanged;
    [FormerlySerializedAs("grabbableLayers")] [SerializeField, HideInInspector] private LayerMask legacyGrabbableLayers;
    [FormerlySerializedAs("maxGrabMoveSpeed")] [SerializeField, HideInInspector] private float legacyMaxGrabMoveSpeed = 40.0f;

    private Camera mainCamera
    {
        get => sceneReferences.mainCamera;
        set => sceneReferences.mainCamera = value;
    }

    private PlayerState playerState
    {
        get => sceneReferences.playerState;
        set => sceneReferences.playerState = value;
    }

    private PipBoyController pipBoyController
    {
        get => sceneReferences.pipBoyController;
        set => sceneReferences.pipBoyController = value;
    }

    private PlayerCombat playerCombat
    {
        get => sceneReferences.playerCombat;
        set => sceneReferences.playerCombat = value;
    }

    private ContainerController ContainerController
    {
        get => sceneReferences.containerController;
        set => sceneReferences.containerController = value;
    }

    private LayerMask interactLayers
    {
        get => interactionRaycast.interactLayers;
        set => interactionRaycast.interactLayers = value;
    }

    private bool useInfiniteRaycastDistance
    {
        get => interactionRaycast.useInfiniteRaycastDistance;
        set => interactionRaycast.useInfiniteRaycastDistance = value;
    }

    private float maxDistance
    {
        get => interactionRaycast.maxDistance;
        set => interactionRaycast.maxDistance = value;
    }

    private float maxWorldItemPickupDistance
    {
        get => interactionRaycast.maxWorldItemPickupDistance;
        set => interactionRaycast.maxWorldItemPickupDistance = value;
    }

    private float maxInteractableDistance
    {
        get => interactionRaycast.maxInteractableDistance;
        set => interactionRaycast.maxInteractableDistance = value;
    }

    private float maxCorpseInteractDistance
    {
        get => interactionRaycast.maxCorpseInteractDistance;
        set => interactionRaycast.maxCorpseInteractDistance = value;
    }

    private float corpseRaycastRadius
    {
        get => interactionRaycast.corpseRaycastRadius;
        set => interactionRaycast.corpseRaycastRadius = value;
    }

    private bool includeTriggerColliders
    {
        get => interactionRaycast.includeTriggerColliders;
        set => interactionRaycast.includeTriggerColliders = value;
    }

    private HoverChangedEvent onHoverChanged
    {
        get => eventsGroup.onHoverChanged;
        set => eventsGroup.onHoverChanged = value;
    }

    private LayerMask grabbableLayers
    {
        get => grabSettings.grabbableLayers;
        set => grabSettings.grabbableLayers = value;
    }

    private float fixedGrabDistance
    {
        get => grabSettings.fixedGrabDistance;
        set => grabSettings.fixedGrabDistance = value;
    }

    private float grabOriginHeightOffset
    {
        get => grabSettings.grabOriginHeightOffset;
        set => grabSettings.grabOriginHeightOffset = value;
    }

    private float maxGrabMoveSpeed
    {
        get => grabSettings.maxGrabMoveSpeed;
        set => grabSettings.maxGrabMoveSpeed = value;
    }

    // The input actions wrapper generated from InputSystemActions.inputactions.
    private InputSystemActions inputActions;

    // Cached currently hovered WorldItem (pickup).
    private WorldItem hoveredWorldItem;

    // Cached currently hovered generic interactable component.
    private Component hoveredInteractable;

    // Cached prompt text for the current hover target.
    private string hoveredPrompt;

    // Cached query trigger interaction mode.
    private QueryTriggerInteraction triggerMode;

    // Currently grabbed transform (null when not grabbing).
    private Transform grabbedTransform;

    // Cached rigidbody of grabbed object if present.
    private Rigidbody grabbedRigidbody;

    // Original rigidbody gravity value before grabbing.
    private bool grabbedUsedGravity;

    // Original rigidbody kinematic value before grabbing.
    private bool grabbedWasKinematic;

    // Original rigidbody collision mode before grabbing.
    private CollisionDetectionMode grabbedCollisionMode;

    // The world-space point the grabbed rigidbody is pulled toward.
    private Vector3 grabbedTargetPoint;

    // Player colliders temporarily ignored while an object is grabbed.
    private Collider[] ignoredPlayerGrabColliders;

    // Grabbed-object colliders temporarily ignored against the player.
    private Collider[] ignoredObjectGrabColliders;

    // Tracks whether input was blocked last frame so one-time transitions can run.
    private bool wasInputBlockedByPipBoy;

    // Cache for non-alloc raycasts to avoid per-frame GC churn.
    private RaycastHit[] raycastHitsCache;

    // Cache for non-alloc corpse spherecasts to avoid per-frame GC churn.
    private RaycastHit[] spherecastHitsCache;

    private readonly Dictionary<Transform, HoverLookup> hoverLookupCache = new Dictionary<Transform, HoverLookup>();
    private readonly List<Component> componentScanBuffer = new List<Component>(8);
    private readonly List<Transform> childScanBuffer = new List<Transform>(32);
    private TerminalController terminalController;
    private DialogueController dialogueController;

    
    
    // methods
    private void OnValidate()
    {
        MigrateLegacyInspectorFields();
    }

    private void MigrateLegacyInspectorFields()
    {
        if (sceneReferences == null)
            sceneReferences = new SceneReferencesGroup();

        if (interactionRaycast == null)
            interactionRaycast = new InteractionRaycastGroup();

        if (grabSettings == null)
            grabSettings = new GrabSettingsGroup();

        if (eventsGroup == null)
            eventsGroup = new EventsGroup();

        if (hasMigratedLegacyInspectorFields)
            return;

        if (sceneReferences.mainCamera == null && legacyMainCamera != null)
            sceneReferences.mainCamera = legacyMainCamera;

        if (sceneReferences.playerState == null && legacyPlayerState != null)
            sceneReferences.playerState = legacyPlayerState;

        if (sceneReferences.pipBoyController == null && legacyPipBoyController != null)
            sceneReferences.pipBoyController = legacyPipBoyController;

        interactionRaycast.interactLayers = legacyInteractLayers;
        interactionRaycast.useInfiniteRaycastDistance = legacyUseInfiniteRaycastDistance;
        interactionRaycast.maxDistance = legacyMaxDistance;
        interactionRaycast.maxWorldItemPickupDistance = legacyMaxWorldItemPickupDistance;
        interactionRaycast.maxInteractableDistance = legacyMaxInteractableDistance;
        interactionRaycast.includeTriggerColliders = legacyIncludeTriggerColliders;

        if (eventsGroup.onHoverChanged == null && legacyOnHoverChanged != null)
            eventsGroup.onHoverChanged = legacyOnHoverChanged;

        grabSettings.grabbableLayers = legacyGrabbableLayers;
        grabSettings.maxGrabMoveSpeed = legacyMaxGrabMoveSpeed;

        hasMigratedLegacyInspectorFields = true;
    }

    private void Awake()
    {
        MigrateLegacyInspectorFields();

        // Keep configured interaction ranges valid.
        maxWorldItemPickupDistance = Mathf.Max(MinRayDistance, maxWorldItemPickupDistance);
        maxInteractableDistance = Mathf.Max(MinRayDistance, maxInteractableDistance);
        maxCorpseInteractDistance = Mathf.Max(MinRayDistance, maxCorpseInteractDistance);
        corpseRaycastRadius = Mathf.Max(0f, corpseRaycastRadius);

        fixedGrabDistance = Mathf.Max(MinRayDistance, fixedGrabDistance);
        grabOriginHeightOffset = Mathf.Max(0f, grabOriginHeightOffset);

        // If no camera is assigned, use the scene's Main Camera.
        if (!mainCamera)
            mainCamera = Camera.main;

        // If no player state is assigned, find it in the scene.
        if (!playerState)
            playerState = FindAnyObjectByType<PlayerState>();

        // If no combat provider is assigned, find it in the scene.
        if (!playerCombat)
            playerCombat = FindAnyObjectByType<PlayerCombat>();

        // If no Pip-Boy controller is assigned, find it in the scene.
        if (!pipBoyController)
            pipBoyController = FindAnyObjectByType<PipBoyController>();

        // If no container UI is assigned, find one in the scene (including inactive hierarchy objects).
        if (!ContainerController)
            ContainerController = ContainerController.FindFirstInSceneIncludingInactive();

        // Create the input actions wrapper.
        inputActions = new InputSystemActions();

        // If no grabbable layers are configured, try common defaults.
        if (grabbableLayers.value == 0)
        {
            int itemLayer = LayerMask.NameToLayer("Item");
            int itemsLayer = LayerMask.NameToLayer("Items");

            if (itemLayer >= 0) grabbableLayers |= 1 << itemLayer;
            if (itemsLayer >= 0) grabbableLayers |= 1 << itemsLayer;
        }

        // Choose whether raycasts should include triggers.
        triggerMode = includeTriggerColliders ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;

        terminalController = TerminalController.FindFirstInSceneIncludingInactive();
        dialogueController = DialogueController.FindFirstInSceneIncludingInactive();
    }


    private void OnEnable()
    {
        var playerActions = inputActions.Player;

        // Enable the Player action map.
        playerActions.Enable();

        // Bind Interact input to our handler.
        playerActions.Interact.performed += OnInteractPerformed;

        // Bind Grab input to our handler.
        playerActions.Grab.performed += OnGrabPerformed;

        // Track the final rendered camera after Cinemachine has applied its brain update.
        CinemachineCore.CameraUpdatedEvent.AddListener(OnCinemachineCameraUpdated);
    }


    private void OnDisable()
    {
        var playerActions = inputActions.Player;

        // Unbind Interact input from our handler.
        playerActions.Interact.performed -= OnInteractPerformed;

        // Unbind Grab input from our handler.
        playerActions.Grab.performed -= OnGrabPerformed;

        CinemachineCore.CameraUpdatedEvent.RemoveListener(OnCinemachineCameraUpdated);

        // Always release grabbed object when this component is disabled.
        ReleaseGrabbedObject();
        hoverLookupCache.Clear();

        // Disable the Player action map.
        playerActions.Disable();
    }


    private void Update()
    {
        bool inputBlocked = IsInputBlockedByPipBoy();

        // Block all world interaction while Pip-Boy is open.
        if (inputBlocked)
        {
            if (!wasInputBlockedByPipBoy)
                ReleaseGrabbedObject();

            wasInputBlockedByPipBoy = true;
            SetHover(null, null, string.Empty);
            return;
        }

        wasInputBlockedByPipBoy = false;

        Camera cam = mainCamera;

        // Stop if we still don't have a camera reference.
        if (!cam)
            return;

        // Compute crosshair interaction point once for this frame.
        Vector2 screenPoint = GetInteractionScreenPosition();

        // Raycast and update hover state using the same screen point as crosshair behavior.
        UpdateHoverFromScreenPoint(screenPoint);
    }

    private void LateUpdate()
    {
        if (IsInputBlockedByPipBoy())
            return;

        // Fallback for frames without a Cinemachine brain update.
        UpdateGrabbedTargetPoint();
    }

    private void OnCinemachineCameraUpdated(CinemachineBrain brain)
    {
        if (!brain)
            return;

        Camera outputCamera = brain.OutputCamera;
        if (!outputCamera)
            return;

        mainCamera = outputCamera;
        UpdateGrabbedTargetPoint();
    }

    private void FixedUpdate()
    {
        if (IsInputBlockedByPipBoy())
            return;

        // Apply physics-based pulling in fixed timestep.
        UpdateGrabbedObjectPhysics();
    }


    
    private void OnInteractPerformed(InputAction.CallbackContext context)
    {
        if (IsInputBlockedByPipBoy())
            return;

        // If we have a hovered world item, attempt pickup first.
        if (hoveredWorldItem)
        {
            if (!IsHoveredWorldItemInPickupRange())
            {
                SetHover(null, null, string.Empty);
                return;
            }

            // Attempt to pick up using this player as the picker.
            hoveredWorldItem.TryPickup(gameObject);

            // Stop further execution.
            return;
        }

        // If we have a hovered generic interactable, invoke it.
        if (hoveredInteractable is IPlayerInteractTarget target)
        {
            bool skipRootDistanceCheck = hoveredInteractable is Container container && container.UsesLootInteractionPrompt();
            if (!skipRootDistanceCheck && !IsInteractableWithinRange(hoveredInteractable))
            {
                SetHover(null, null, string.Empty);
                return;
            }

            // Call the interact function on the target.
            target.Interact(gameObject);

            // Stop further execution.
            return;
        }

        if (hoveredInteractable is NPC npc)
        {
            if (TryGetDeadNpcLootContainer(npc, out Container lootContainer))
            {
                lootContainer.Interact(gameObject);
                return;
            }

            if (IsNpcDialogueBlockedByCombatState(npc))
            {
                SetHover(null, null, string.Empty);
                return;
            }

            if (!IsInteractableWithinRange(npc))
            {
                SetHover(null, null, string.Empty);
                return;
            }

            DialogueController dialogueController = DialogueController.FindFirstInSceneIncludingInactive();
            if (dialogueController)
                dialogueController.OpenForNpc(npc, gameObject);
        }

        if (hoveredInteractable is NPCState npcState)
        {
            if (!TryGetDeadNpcLootContainer(npcState, out Container lootContainer))
                return;

            lootContainer.Interact(gameObject);
        }
    }


    
    private void UpdateHoverFromScreenPoint(Vector2 screenPoint)
    {
        Camera cam = mainCamera;

        // Generate a ray from the camera through the interaction screen point.
        Ray ray = cam.ScreenPointToRay(screenPoint);

        // Decide ray distance.
        float dist = GetInteractionRaycastDistance();

        bool hitSomething = TryGetInteractionHit(ray, dist, out RaycastHit hit, out HoverLookup lookup);
        if (!hitSomething)
            hitSomething = TryGetCorpseInteractionHit(ray, GetCorpseInteractionRaycastDistance(), out hit, out lookup);

        // If nothing hit, clear hover.
        if (!hitSomething)
        {
            // Clear hover.
            SetHover(null, null, string.Empty);

            // Stop further execution.
            return;
        }

        // Get the hit transform.
        Transform hitTransform = hit.collider ? hit.collider.transform : null;

        // Stop if transform is missing.
        if (!hitTransform)
        {
            // Clear hover.
            SetHover(null, null, string.Empty);

            // Stop further execution.
            return;
        }

        // Prefer WorldItem pickup if present on the hit object or its parents.
        WorldItem worldItem = lookup.worldItem;

        // If we found a world item, set hover to it.
        if (worldItem)
        {
            if (!IsWorldItemWithinPickupRange(worldItem))
            {
                SetHover(null, null, string.Empty);
                return;
            }

            // Item prompt text is just the action; item name is shown separately by UI.
            string prompt = "Pick Up";

            // Set hover as world item.
            SetHover(worldItem, null, prompt);

            // Stop further execution.
            return;
        }

        // Walk the hierarchy looking for something implementing the interface.
        if (lookup.interactComponent && lookup.interactTarget != null)
        {
            IPlayerInteractTarget interactTarget = lookup.interactTarget;
            Component interactComp = lookup.interactComponent;
            Container lootContainerComponent = interactComp as Container;
            bool isLootContainer = lootContainerComponent && lootContainerComponent.UsesLootInteractionPrompt();
            bool isCorpseLootContainer = isLootContainer && IsCorpseLootContainer(lootContainerComponent);
            bool isWithinRange = isLootContainer
                ? (isCorpseLootContainer ? IsHitWithinCorpseInteractableRange(hit) : IsHitWithinInteractableRange(hit))
                : IsInteractableWithinRange(interactComp);

            if (!isWithinRange)
            {
                SetHover(null, null, string.Empty);
                return;
            }

            // Read prompt text from the target.
            string prompt = interactTarget.GetInteractionText(gameObject);

            // Set hover as generic target.
            SetHover(null, interactComp, prompt);

            // Stop further execution.
            return;
        }

        // NPCs can still surface hover UI even when they do not yet implement a formal interact target.
        if (lookup.npc)
        {
            NPC npc = lookup.npc;
            if (TryGetDeadNpcLootContainer(npc, out Container lootContainer))
            {
                if (!IsHitWithinCorpseInteractableRange(hit))
                {
                    SetHover(null, null, string.Empty);
                    return;
                }

                string lootPrompt = lootContainer.GetInteractionText(gameObject);
                SetHover(null, lootContainer, lootPrompt);
                return;
            }

            if (!IsInteractableWithinRange(npc))
            {
                SetHover(null, null, string.Empty);
                return;
            }

            if (IsNpcDialogueBlockedByCombatState(npc))
            {
                SetHover(null, null, string.Empty);
                return;
            }

            string prompt = npc.HasDialogue()
                ? "Talk"
                : string.Empty;

            SetHover(null, npc, prompt);
            return;
        }

        if (lookup.npcState)
        {
            NPCState npcState = lookup.npcState;
            if (!TryGetDeadNpcLootContainer(npcState, out Container lootContainer))
            {
                SetHover(null, null, string.Empty);
                return;
            }

            if (!IsHitWithinCorpseInteractableRange(hit))
            {
                SetHover(null, null, string.Empty);
                return;
            }

            string lootPrompt = lootContainer.GetInteractionText(gameObject);
            SetHover(null, npcState, lootPrompt);
            return;
        }

        // Nothing interactable found on hit hierarchy, clear hover.
        SetHover(null, null, string.Empty);
    }

    private float GetInteractionRaycastDistance()
    {
        if (!useInfiniteRaycastDistance)
            return Mathf.Max(MinRayDistance, maxDistance);

        float largestInteractionRange = Mathf.Max(maxWorldItemPickupDistance, maxInteractableDistance);
        return Mathf.Max(MinRayDistance, largestInteractionRange, maxDistance);
    }

    private float GetCorpseInteractionRaycastDistance()
    {
        float corpseDistance = Mathf.Max(MinRayDistance, maxCorpseInteractDistance);

        if (!useInfiniteRaycastDistance)
            return corpseDistance;

        return Mathf.Max(corpseDistance, maxDistance);
    }

    private bool TryResolveHoverLookup(Transform hitTransform, out HoverLookup lookup)
    {
        lookup = default;

        if (!hitTransform)
            return false;

        if (hoverLookupCache.TryGetValue(hitTransform, out lookup))
            return HasResolvedHoverTarget(lookup);

        if (hoverLookupCache.Count >= HoverLookupCacheLimit)
            hoverLookupCache.Clear();

        lookup.worldItem = FindInParents<WorldItem>(hitTransform);
        if (lookup.worldItem)
        {
            hoverLookupCache[hitTransform] = lookup;
            return true;
        }

        if (TryFindInteractTarget(hitTransform, out lookup.interactTarget, out lookup.interactComponent))
        {
            hoverLookupCache[hitTransform] = lookup;
            return true;
        }

        lookup.npc = FindInParents<NPC>(hitTransform);
        if (!lookup.npc)
            lookup.npc = FindInChildren<NPC>(hitTransform);

        if (lookup.npc)
        {
            hoverLookupCache[hitTransform] = lookup;
            return true;
        }

        lookup.npcState = FindInParents<NPCState>(hitTransform);
        if (!lookup.npcState)
            lookup.npcState = FindInChildren<NPCState>(hitTransform);

        hoverLookupCache[hitTransform] = lookup;
        return lookup.npcState;
    }

    private static bool HasResolvedHoverTarget(HoverLookup lookup)
    {
        return lookup.worldItem ||
               (lookup.interactComponent && lookup.interactTarget != null) ||
               lookup.npc ||
               lookup.npcState;
    }

    private bool TryFindInteractTarget(
        Transform hitTransform,
        out IPlayerInteractTarget interactTarget,
        out Component interactComp)
    {
        interactTarget = null;
        interactComp = null;

        if (!hitTransform)
            return false;

        if (TryFindInteractTargetInParents(hitTransform, out interactTarget, out interactComp))
            return true;

        return TryFindInteractTargetInChildren(hitTransform, out interactTarget, out interactComp);
    }

    private bool TryFindInteractTargetInParents(
        Transform hitTransform,
        out IPlayerInteractTarget interactTarget,
        out Component interactComp)
    {
        interactTarget = null;
        interactComp = null;

        for (Transform current = hitTransform; current; current = current.parent)
        {
            if (TryFindInteractTargetOnTransform(current, out interactTarget, out interactComp))
                return true;
        }

        return false;
    }

    private bool TryFindInteractTargetInChildren(
        Transform hitTransform,
        out IPlayerInteractTarget interactTarget,
        out Component interactComp)
    {
        interactTarget = null;
        interactComp = null;

        childScanBuffer.Clear();
        childScanBuffer.Add(hitTransform);

        for (int i = 0; i < childScanBuffer.Count; i++)
        {
            Transform current = childScanBuffer[i];
            if (!current)
                continue;

            if (current != hitTransform && TryFindInteractTargetOnTransform(current, out interactTarget, out interactComp))
                return true;

            for (int childIndex = 0; childIndex < current.childCount; childIndex++)
                childScanBuffer.Add(current.GetChild(childIndex));
        }

        return false;
    }

    private bool TryFindInteractTargetOnTransform(
        Transform targetTransform,
        out IPlayerInteractTarget interactTarget,
        out Component interactComp)
    {
        interactTarget = null;
        interactComp = null;

        if (!targetTransform)
            return false;

        componentScanBuffer.Clear();
        targetTransform.GetComponents<Component>(componentScanBuffer);

        for (int i = 0; i < componentScanBuffer.Count; i++)
        {
            Component component = componentScanBuffer[i];
            if (!component)
                continue;

            if (component is not IPlayerInteractTarget candidate)
                continue;

            interactTarget = candidate;
            interactComp = component;
            return true;
        }

        return false;
    }

    private T FindInParents<T>(Transform hitTransform) where T : Component
    {
        if (!hitTransform)
            return null;

        for (Transform current = hitTransform; current; current = current.parent)
        {
            T component = current.GetComponent<T>();
            if (component)
                return component;
        }

        return null;
    }

    private T FindInChildren<T>(Transform hitTransform) where T : Component
    {
        if (!hitTransform)
            return null;

        childScanBuffer.Clear();
        childScanBuffer.Add(hitTransform);

        for (int i = 0; i < childScanBuffer.Count; i++)
        {
            Transform current = childScanBuffer[i];
            if (!current)
                continue;

            if (current != hitTransform)
            {
                T component = current.GetComponent<T>();
                if (component)
                    return component;
            }

            for (int childIndex = 0; childIndex < current.childCount; childIndex++)
                childScanBuffer.Add(current.GetChild(childIndex));
        }

        return null;
    }

    private static string GetSafeNpcName(NPC npc)
    {
        if (!npc)
            return "NPC";

        string npcName = npc.GetNPCName();
        return string.IsNullOrWhiteSpace(npcName) ? "NPC" : npcName.Trim();
    }

    private static bool TryGetDeadNpcLootContainer(NPC npc, out Container lootContainer)
    {
        lootContainer = null;

        if (!npc)
            return false;

        NPCState npcState = npc.GetState();
        if (!TryGetDeadNpcLootContainer(npcState, out lootContainer))
            return false;

        if (!lootContainer)
            lootContainer = npc.GetComponent<Container>();
        if (!lootContainer)
            lootContainer = npc.GetComponentInParent<Container>();
        if (!lootContainer)
            lootContainer = npc.GetComponentInChildren<Container>(true);

        return lootContainer != null;
    }

    private static bool TryGetDeadNpcLootContainer(NPCState npcState, out Container lootContainer)
    {
        lootContainer = null;

        if (!npcState || !npcState.IsDead())
            return false;

        lootContainer = npcState.GetDeathLootContainer();
        if (!lootContainer)
            lootContainer = npcState.GetComponent<Container>();
        if (!lootContainer)
            lootContainer = npcState.GetComponentInParent<Container>();
        if (!lootContainer)
            lootContainer = npcState.GetComponentInChildren<Container>(true);

        return lootContainer != null;
    }

    private static bool IsCorpseLootContainer(Container container)
    {
        if (!container || !container.UsesLootInteractionPrompt())
            return false;

        NPCState npcState = container.GetComponent<NPCState>();
        if (!npcState)
            npcState = container.GetComponentInParent<NPCState>();
        if (!npcState)
            npcState = container.GetComponentInChildren<NPCState>(true);

        return TryGetDeadNpcLootContainer(npcState, out Container lootContainer) && lootContainer == container;
    }

    private static bool IsNpcDialogueBlockedByCombatState(NPC npc)
    {
        if (!npc)
            return false;

        NPCCombat npcCombat = npc.GetComponent<NPCCombat>();
        if (!npcCombat)
            npcCombat = npc.GetComponentInParent<NPCCombat>();
        if (!npcCombat)
            npcCombat = npc.GetComponentInChildren<NPCCombat>(true);

        return npcCombat && npcCombat.IsAggroedOrSearchingForPlayer();
    }

    private bool TryGetInteractionHit(Ray ray, float distance, out RaycastHit hit, out HoverLookup lookup)
    {
        hit = default;
        lookup = default;

        if (raycastHitsCache == null || raycastHitsCache.Length != RaycastHitsCacheSize)
            raycastHitsCache = new RaycastHit[RaycastHitsCacheSize];

        int hitCount = Physics.RaycastNonAlloc(ray, raycastHitsCache, distance, interactLayers, triggerMode);
        if (hitCount == 0)
            return false;

        float nearestDistance = float.PositiveInfinity;
        bool found = false;
        RaycastHit nearestHit = default;
        HoverLookup nearestLookup = default;
        Transform self = transform;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit currentHit = raycastHitsCache[i];
            Collider currentCollider = currentHit.collider;
            if (!currentCollider)
                continue;

            if (currentCollider.transform.IsChildOf(self))
                continue;

            if (!TryResolveHoverLookup(currentCollider.transform, out HoverLookup currentLookup))
                continue;

            if (currentHit.distance < nearestDistance)
            {
                nearestDistance = currentHit.distance;
                nearestHit = currentHit;
                nearestLookup = currentLookup;
                found = true;
            }
        }

        if (!found)
            return false;

        hit = nearestHit;
        lookup = nearestLookup;
        return true;
    }

    private bool TryGetCorpseInteractionHit(Ray ray, float distance, out RaycastHit hit, out HoverLookup lookup)
    {
        hit = default;
        lookup = default;

        if (raycastHitsCache == null || raycastHitsCache.Length != RaycastHitsCacheSize)
            raycastHitsCache = new RaycastHit[RaycastHitsCacheSize];

        int corpseRaycastMask = ~0;
        int ignoreRaycastLayer = LayerMask.NameToLayer("Ignore Raycast");
        if (ignoreRaycastLayer >= 0)
            corpseRaycastMask &= ~(1 << ignoreRaycastLayer);

        int hitCount = Physics.RaycastNonAlloc(ray, raycastHitsCache, distance, corpseRaycastMask, QueryTriggerInteraction.Collide);
        if (TrySelectCorpseInteractionHit(raycastHitsCache, hitCount, out hit, out lookup))
            return true;

        float sphereRadius = Mathf.Max(0f, corpseRaycastRadius);
        if (sphereRadius <= 0f)
            return false;

        if (spherecastHitsCache == null || spherecastHitsCache.Length != SpherecastHitsCacheSize)
            spherecastHitsCache = new RaycastHit[SpherecastHitsCacheSize];

        int sphereHitCount = Physics.SphereCastNonAlloc(
            ray,
            sphereRadius,
            spherecastHitsCache,
            distance,
            corpseRaycastMask,
            QueryTriggerInteraction.Collide);

        return TrySelectCorpseInteractionHit(spherecastHitsCache, sphereHitCount, out hit, out lookup);
    }

    private bool TrySelectCorpseInteractionHit(
        RaycastHit[] hits,
        int hitCount,
        out RaycastHit hit,
        out HoverLookup lookup)
    {
        hit = default;
        lookup = default;

        if (hits == null || hitCount == 0)
            return false;

        float nearestDistance = float.PositiveInfinity;
        bool found = false;
        RaycastHit nearestHit = default;
        HoverLookup nearestLookup = default;
        Transform self = transform;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit currentHit = hits[i];
            Collider currentCollider = currentHit.collider;
            if (!currentCollider)
                continue;

            Transform hitTransform = currentCollider.transform;
            if (!hitTransform || hitTransform.IsChildOf(self))
                continue;

            if (!TryResolveCorpseHoverLookup(hitTransform, out HoverLookup currentLookup))
                continue;

            if (IsRootCorpseCollider(currentCollider, currentLookup))
                continue;

            if (!IsHitWithinCorpseInteractableRange(currentHit))
                continue;

            if (currentHit.distance < nearestDistance)
            {
                nearestDistance = currentHit.distance;
                nearestHit = currentHit;
                nearestLookup = currentLookup;
                found = true;
            }
        }

        if (!found)
            return false;

        hit = nearestHit;
        lookup = nearestLookup;
        return true;
    }

    private static bool IsRootCorpseCollider(Collider corpseCollider, HoverLookup lookup)
    {
        if (!corpseCollider)
            return false;

        Transform colliderTransform = corpseCollider.transform;
        if (!colliderTransform)
            return false;

        if (lookup.npc && colliderTransform == lookup.npc.transform)
            return true;

        return lookup.npcState && colliderTransform == lookup.npcState.transform;
    }

    private bool TryResolveCorpseHoverLookup(Transform hitTransform, out HoverLookup lookup)
    {
        lookup = default;

        if (!hitTransform)
            return false;

        lookup.npc = FindInParents<NPC>(hitTransform);
        if (!lookup.npc)
            lookup.npc = FindInChildren<NPC>(hitTransform);

        if (lookup.npc && TryGetDeadNpcLootContainer(lookup.npc, out _))
            return true;

        lookup.npcState = FindInParents<NPCState>(hitTransform);
        if (!lookup.npcState)
            lookup.npcState = FindInChildren<NPCState>(hitTransform);

        return lookup.npcState && TryGetDeadNpcLootContainer(lookup.npcState, out _);
    }

    private void OnGrabPerformed(InputAction.CallbackContext context)
    {
        if (IsInputBlockedByPipBoy())
            return;

        // Toggle behavior: release if already grabbing.
        if (grabbedTransform)
        {
            ReleaseGrabbedObject();
            return;
        }

        // Attempt to acquire a new grab target under the crosshair.
        TryStartGrab(GetInteractionScreenPosition());
    }

    private void TryStartGrab(Vector2 screenPoint)
    {
        Camera cam = mainCamera;

        // Stop if camera is missing.
        if (!cam)
            return;

        // Stop if no grabbable layers are configured.
        if (grabbableLayers.value == 0)
            return;

        // Raycast from crosshair.
        Ray ray = cam.ScreenPointToRay(screenPoint);
        float dist = GetInteractionRaycastDistance();
        int grabRayMask = interactLayers.value | grabbableLayers.value;
        bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, dist, grabRayMask, triggerMode);

        // Stop if nothing grabbable was hit.
        if (!hitSomething || !hit.collider)
            return;

        // Select nearest ancestor in a grabbable layer.
        Transform candidate = FindNearestLayerTransformInMask(hit.collider.transform, grabbableLayers);
        if (!candidate)
            return;

        if (candidate.IsChildOf(transform))
            return;

        // Find rigidbody to move with physics.
        Rigidbody candidateRigidbody = hit.rigidbody ? hit.rigidbody : candidate.GetComponentInParent<Rigidbody>(true);
        if (!candidateRigidbody)
            return;

        if (candidateRigidbody.transform.IsChildOf(transform))
            return;

        // Start grabbing this object with rigidbody physics.
        grabbedTransform = candidate;
        grabbedRigidbody = candidateRigidbody;

        // Report grab state so movement/combat systems can enforce restrictions.
        if (playerState)
            playerState.SetHasGrabbedItem(true);

        grabbedWasKinematic = grabbedRigidbody.isKinematic;
        grabbedUsedGravity = grabbedRigidbody.useGravity;
        grabbedCollisionMode = grabbedRigidbody.collisionDetectionMode;

        // Keep object fully dynamic while grabbed so collisions are respected.
        grabbedRigidbody.isKinematic = false;
        grabbedRigidbody.useGravity = false;
        grabbedRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Clear any existing momentum so the object immediately follows the grab target.
        grabbedRigidbody.linearVelocity = Vector3.zero;
        grabbedRigidbody.angularVelocity = Vector3.zero;

        BeginIgnoringGrabbedObjectPlayerCollisions();

        // Initialize target point immediately.
        UpdateGrabbedTargetPoint();
        SnapGrabbedObjectToTargetPoint();
    }

    private void UpdateGrabbedTargetPoint()
    {
        // Stop when nothing is currently grabbed.
        if (!grabbedTransform || !grabbedRigidbody)
            return;

        Vector3 targetPoint = GetGrabTargetPoint();
        grabbedTargetPoint = targetPoint;
    }

    private float GetGrabHoldDistance()
    {
        return Mathf.Max(MinRayDistance, fixedGrabDistance);
    }

    private Vector3 GetGrabTargetPoint()
    {
        float radius = GetGrabHoldDistance();
        Vector3 center = GetGrabOrigin();
        Vector3 forward = GetPlayerForward();
        Camera cam = mainCamera;

        if (!cam)
            return center + forward * radius;

        Ray crosshairRay = cam.ScreenPointToRay(GetInteractionScreenPosition());
        if (TryGetForwardHemisphereSphereIntersection(crosshairRay, center, radius, forward, out Vector3 hitPoint))
            return hitPoint;

        Vector3 closestPointOnRay = GetClosestPointOnRay(crosshairRay, center);
        Vector3 direction = closestPointOnRay - center;

        if (direction.sqrMagnitude < MinGrabDirectionSqr)
            direction = crosshairRay.direction;

        direction = ClampDirectionToForwardHemisphere(direction, forward);

        if (direction.sqrMagnitude < MinGrabDirectionSqr)
            direction = forward;

        return center + direction.normalized * radius;
    }

    private Vector3 GetGrabOrigin()
    {
        return transform.position + GetPlayerUp() * Mathf.Max(0f, grabOriginHeightOffset);
    }

    private Vector3 GetPlayerForward()
    {
        Vector3 forward = transform.forward;
        if (forward.sqrMagnitude >= MinGrabDirectionSqr)
            return forward.normalized;

        return Vector3.forward;
    }

    private Vector3 GetPlayerUp()
    {
        Vector3 up = transform.up;
        if (up.sqrMagnitude >= MinGrabDirectionSqr)
            return up.normalized;

        return Vector3.up;
    }

    private static bool TryGetForwardHemisphereSphereIntersection(
        Ray ray,
        Vector3 center,
        float radius,
        Vector3 forward,
        out Vector3 point)
    {
        point = Vector3.zero;

        Vector3 originToCenter = ray.origin - center;
        float b = Vector3.Dot(originToCenter, ray.direction);
        float c = originToCenter.sqrMagnitude - radius * radius;
        float discriminant = b * b - c;

        if (discriminant < 0f)
            return false;

        float root = Mathf.Sqrt(discriminant);
        float nearestValidDistance = float.PositiveInfinity;
        bool found = false;

        TrySelectHemisphereIntersection(ray, center, forward, -b - root, ref nearestValidDistance, ref found, ref point);
        TrySelectHemisphereIntersection(ray, center, forward, -b + root, ref nearestValidDistance, ref found, ref point);

        return found;
    }

    private static void TrySelectHemisphereIntersection(
        Ray ray,
        Vector3 center,
        Vector3 forward,
        float distance,
        ref float nearestValidDistance,
        ref bool found,
        ref Vector3 point)
    {
        if (distance < 0f || distance >= nearestValidDistance)
            return;

        Vector3 candidate = ray.GetPoint(distance);
        Vector3 centerToCandidate = candidate - center;
        if (Vector3.Dot(centerToCandidate, forward) < 0f)
            return;

        nearestValidDistance = distance;
        point = candidate;
        found = true;
    }

    private static Vector3 GetClosestPointOnRay(Ray ray, Vector3 point)
    {
        float distance = Vector3.Dot(point - ray.origin, ray.direction);
        distance = Mathf.Max(0f, distance);
        return ray.GetPoint(distance);
    }

    private static Vector3 ClampDirectionToForwardHemisphere(Vector3 direction, Vector3 forward)
    {
        float backwardAmount = Vector3.Dot(direction, forward);
        if (backwardAmount >= 0f)
            return direction;

        return direction - forward * backwardAmount;
    }

    private void SnapGrabbedObjectToTargetPoint()
    {
        Rigidbody body = grabbedRigidbody;
        if (!body)
            return;

        Vector3 bodyOriginToCenterOfMass = body.worldCenterOfMass - body.position;
        body.position = grabbedTargetPoint - bodyOriginToCenterOfMass;

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private void UpdateGrabbedObjectPhysics()
    {
        Rigidbody body = grabbedRigidbody;

        // Stop when nothing is currently grabbed.
        if (!body)
            return;

        // Snap toward target each physics step by driving velocity directly.
        Vector3 toTarget = grabbedTargetPoint - body.worldCenterOfMass;
        float fixedDeltaTime = Mathf.Max(MinFixedDeltaTime, Time.fixedDeltaTime);
        Vector3 requiredVelocity = toTarget / fixedDeltaTime;
        float speedCap = Mathf.Max(0.01f, maxGrabMoveSpeed);

        body.linearVelocity = Vector3.ClampMagnitude(requiredVelocity, speedCap);
        body.angularVelocity = Vector3.zero;
    }

    private void ReleaseGrabbedObject()
    {
        // Stop if nothing is currently grabbed.
        if (!grabbedTransform && !grabbedRigidbody)
            return;

        RestoreIgnoredGrabCollisions();

        // Restore rigidbody state.
        if (grabbedRigidbody)
        {
            grabbedRigidbody.isKinematic = grabbedWasKinematic;
            grabbedRigidbody.useGravity = grabbedUsedGravity;

            // Keep released dynamic/gravity objects in a safe collision mode to avoid tunneling through floors.
            if (!grabbedWasKinematic && grabbedUsedGravity)
            {
                grabbedRigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                grabbedRigidbody.linearVelocity = Vector3.zero;
                grabbedRigidbody.angularVelocity = Vector3.zero;
                grabbedRigidbody.WakeUp();
            }
            else
            {
                grabbedRigidbody.collisionDetectionMode = grabbedCollisionMode;
            }
        }

        // Clear grab state.
        grabbedTransform = null;
        grabbedRigidbody = null;
        grabbedTargetPoint = Vector3.zero;

        // Report grab state cleared.
        if (playerState)
            playerState.SetHasGrabbedItem(false);
    }

    private void BeginIgnoringGrabbedObjectPlayerCollisions()
    {
        RestoreIgnoredGrabCollisions();

        if (!grabbedRigidbody)
            return;

        ignoredPlayerGrabColliders = GetComponentsInChildren<Collider>(true);
        ignoredObjectGrabColliders = grabbedRigidbody.GetComponentsInChildren<Collider>(true);

        if (ignoredPlayerGrabColliders == null || ignoredObjectGrabColliders == null)
            return;

        for (int i = 0; i < ignoredPlayerGrabColliders.Length; i++)
        {
            Collider playerCollider = ignoredPlayerGrabColliders[i];
            if (!playerCollider)
                continue;

            for (int j = 0; j < ignoredObjectGrabColliders.Length; j++)
            {
                Collider objectCollider = ignoredObjectGrabColliders[j];
                if (!objectCollider || objectCollider == playerCollider)
                    continue;

                Physics.IgnoreCollision(playerCollider, objectCollider, true);
            }
        }
    }

    private void RestoreIgnoredGrabCollisions()
    {
        if (ignoredPlayerGrabColliders == null || ignoredObjectGrabColliders == null)
            return;

        for (int i = 0; i < ignoredPlayerGrabColliders.Length; i++)
        {
            Collider playerCollider = ignoredPlayerGrabColliders[i];
            if (!playerCollider)
                continue;

            for (int j = 0; j < ignoredObjectGrabColliders.Length; j++)
            {
                Collider objectCollider = ignoredObjectGrabColliders[j];
                if (!objectCollider || objectCollider == playerCollider)
                    continue;

                Physics.IgnoreCollision(playerCollider, objectCollider, false);
            }
        }

        ignoredPlayerGrabColliders = null;
        ignoredObjectGrabColliders = null;
    }

    private static Transform FindNearestLayerTransformInMask(Transform start, LayerMask mask)
    {
        // Walk up from hit transform until we find a layer included in the mask.
        Transform current = start;
        while (current != null)
        {
            if (IsLayerInMask(current.gameObject.layer, mask))
                return current;

            current = current.parent;
        }

        return null;
    }

    private static bool IsLayerInMask(int layer, LayerMask mask)
    {
        return (mask.value & (1 << layer)) != 0;
    }

    private bool IsHoveredWorldItemInPickupRange()
    {
        if (!hoveredWorldItem)
            return false;

        return IsWorldItemWithinPickupRange(hoveredWorldItem);
    }

    private bool IsWorldItemWithinPickupRange(WorldItem worldItem)
    {
        if (!worldItem)
            return false;

        float allowedDistance = Mathf.Max(0.01f, maxWorldItemPickupDistance);
        float allowedDistanceSqr = allowedDistance * allowedDistance;
        Vector3 delta = worldItem.transform.position - transform.position;
        return delta.sqrMagnitude <= allowedDistanceSqr;
    }

    private bool IsInteractableWithinRange(Component interactable)
    {
        if (!interactable)
            return false;

        float allowedDistance = Mathf.Max(MinRayDistance, maxInteractableDistance);
        float allowedDistanceSqr = allowedDistance * allowedDistance;
        Vector3 delta = interactable.transform.position - transform.position;
        return delta.sqrMagnitude <= allowedDistanceSqr;
    }

    private bool IsHitWithinInteractableRange(RaycastHit hit)
    {
        float allowedDistance = Mathf.Max(MinRayDistance, maxInteractableDistance);
        return IsHitWithinRange(hit, allowedDistance);
    }

    private bool IsHitWithinCorpseInteractableRange(RaycastHit hit)
    {
        float allowedDistance = Mathf.Max(MinRayDistance, maxCorpseInteractDistance);
        return IsHitWithinRange(hit, allowedDistance);
    }

    private bool IsHitWithinRange(RaycastHit hit, float allowedDistance)
    {
        float allowedDistanceSqr = allowedDistance * allowedDistance;
        Vector3 hitPoint = hit.collider ? hit.collider.ClosestPoint(transform.position) : hit.point;

        if ((hitPoint - transform.position).sqrMagnitude <= allowedDistanceSqr)
            return true;

        return hit.distance <= allowedDistance;
    }

    private Vector2 GetInteractionScreenPosition()
    {
        // In combat mode, use the same screen position as the UI crosshair.
        if (playerState != null && playerState.GetCombatMode())
        {
            if (playerCombat != null)
                return playerCombat.GetCurrentCrosshairScreenPoint();

            return GetMouseScreenPosition();
        }

        return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
    }


    private static Vector2 GetMouseScreenPosition()
    {
        Mouse mouse = Mouse.current;

        // If no mouse is available, default to center screen.
        if (mouse == null)
            return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

        return mouse.position.ReadValue();
    }


    
    private void SetHover(WorldItem newWorldItem, Component newInteractable, string newPrompt)
    {
        // Check if anything actually changed.
        bool changed =
            hoveredWorldItem != newWorldItem ||
            hoveredInteractable != newInteractable ||
            hoveredPrompt != newPrompt;

        // If nothing changed, do nothing.
        if (!changed)
            return;

        // Store new hover values.
        hoveredWorldItem = newWorldItem;

        hoveredInteractable = newInteractable;

        hoveredPrompt = newPrompt;

        // Fire hover changed event for UI.
        onHoverChanged?.Invoke(hoveredPrompt, GetCurrentHoveredComponent());
    }


    
    private Component GetCurrentHoveredComponent()
    {
        // If a world item is hovered, return it.
        if (hoveredWorldItem)
            return hoveredWorldItem;

        // Otherwise return the generic interactable.
        return hoveredInteractable;
    }


    
    public string GetCurrentPromptText()
    {
        // Return the current prompt text (can be empty).
        return hoveredPrompt;
    }


    
    public Component GetCurrentTarget()
    {
        // Return the current hovered target component (or null).
        return GetCurrentHoveredComponent();
    }


    public WorldItem GetCurrentWorldItem()
    {
        // Return hovered world item if one is currently targeted.
        return hoveredWorldItem;
    }


    public bool HasCurrentTarget()
    {
        // Return true when any interactable target is currently hovered.
        return hoveredWorldItem != null || hoveredInteractable != null;
    }

    private bool IsInputBlockedByPipBoy()
    {
        if (UI.ConsoleController.IsOpen)
            return true;

        if (ContainerController.IsInteractCloseCooldownActive())
            return true;

        ContainerController activeContainerController = ContainerController;
        if (!activeContainerController)
        {
            activeContainerController = ContainerController.FindFirstInSceneIncludingInactive();
            ContainerController = activeContainerController;
        }

        if (activeContainerController && activeContainerController.IsOpen())
            return true;

        if (TerminalController.IsInteractCloseCooldownActive())
            return true;

        if (DialogueController.IsInteractCloseCooldownActive())
            return true;

        if (UI.LevelUpUIController.IsInputBlockActive())
            return true;

        TerminalController activeTerminalController = terminalController;
        if (!activeTerminalController)
        {
            activeTerminalController = TerminalController.FindFirstInSceneIncludingInactive();
            terminalController = activeTerminalController;
        }

        if (activeTerminalController && activeTerminalController.IsOpen())
            return true;

        DialogueController activeDialogueController = dialogueController;
        if (!activeDialogueController)
        {
            activeDialogueController = DialogueController.FindFirstInSceneIncludingInactive();
            dialogueController = activeDialogueController;
        }

        if (activeDialogueController && activeDialogueController.IsOpen())
            return true;

        PipBoyController controller = pipBoyController;
        if (!controller)
        {
            controller = FindAnyObjectByType<PipBoyController>();
            pipBoyController = controller;
        }

        return controller && controller.IsOpen();
    }
}



// interface
public interface IPlayerInteractTarget
{
    
    // methods
    // Return the prompt text to show when hovered.
    string GetInteractionText(GameObject interactor);

    // Execute interaction when the Interact action is performed.
    void Interact(GameObject interactor);
}
