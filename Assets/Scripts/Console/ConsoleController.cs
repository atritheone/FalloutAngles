using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace UI
{
    public class ConsoleController : MonoBehaviour
    {
        private const string ItemDatabaseResourcePath = "ItemDatabase";
        private const string DropPointName = "DropPoint";
        private const float SpawnScatterRadius = 0.18f;
        private const float InputLineHeight = 42f;
        private const float OutputLineHeight = 24f;
        private const float OutputVerticalPadding = 8f;
        private const float CursorBlinkIntervalSeconds = 0.45f;
        private const float SelectionRaycastDistance = 10000f;

        private static ConsoleController instance;
        private static bool npcAiEnabled = true;
        private static bool npcCombatAiEnabled = true;
        private static readonly Dictionary<Behaviour, bool> npcAiBehaviourEnabledState = new Dictionary<Behaviour, bool>();
        private static readonly Dictionary<Behaviour, bool> npcCombatAiBehaviourEnabledState = new Dictionary<Behaviour, bool>();
        private static readonly Dictionary<UnityEngine.AI.NavMeshAgent, NpcNavMeshAgentState> npcAiAgentState =
            new Dictionary<UnityEngine.AI.NavMeshAgent, NpcNavMeshAgentState>();

        [Header("References")]
        [SerializeField] private PlayerState playerState;
        [SerializeField] private PlayerMovement playerMovement;
        [SerializeField] private PlayerInventory playerInventory;
        [SerializeField] private QuestController questController;
        [SerializeField] private Transform dropPoint;
        [SerializeField] private ItemDatabase itemDatabase;

        [Header("Console Commands")]
        [SerializeField] [Min(0f)] private float killAllRadius = 60f;

        [Header("Console UI")]
        [SerializeField] private Canvas canvas;
        [SerializeField] private TMP_InputField inputField;
        [SerializeField] private TMP_Text promptText;
        [SerializeField] private TMP_Text outputText;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private RectTransform cursorRect;
        [SerializeField] private Image cursorImage;
        [SerializeField] private TMP_Text selectedObjectText;

        private readonly List<string> outputLines = new List<string>();
        private bool isOpen;
        private bool hasCachedPauseState;
        private float cachedTimeScale = 1f;
        private bool cachedCursorVisible;
        private CursorLockMode cachedCursorLockState;
        private GameObject selectedObject;

        public static bool IsOpen => instance != null && instance.isOpen;
        public static GameObject SelectedObject => instance ? instance.selectedObject : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureSceneInstance()
        {
            if (instance != null)
                return;

            GameObject consoleObject = new GameObject("ConsoleController");
            consoleObject.AddComponent<ConsoleController>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            ResolveGameplayReferences();
            ResolveItemDatabase();
            BuildUiIfNeeded();
            SetVisible(false);
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;

            if (isOpen)
                RestorePauseState();
        }

        private struct NpcNavMeshAgentState
        {
            public bool IsStopped;
            public Vector3 Velocity;

            public NpcNavMeshAgentState(UnityEngine.AI.NavMeshAgent agent)
            {
                IsStopped = agent != null && agent.enabled && agent.isOnNavMesh && agent.isStopped;
                Velocity = agent != null ? agent.velocity : Vector3.zero;
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
                return;

            if (keyboard.backquoteKey.wasPressedThisFrame)
            {
                ToggleConsole();
                return;
            }

            if (!isOpen)
                return;

            HandleSelectionClick();

            if (keyboard.escapeKey.wasPressedThisFrame)
            {
                CloseConsole();
                return;
            }

            if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                ExecuteCurrentInput();

            UpdateConsoleCursor();
        }

        public void ToggleConsole()
        {
            if (isOpen)
                CloseConsole();
            else
                OpenConsole();
        }

        public void OpenConsole()
        {
            if (isOpen)
                return;

            ResolveGameplayReferences();
            ResolveItemDatabase();
            BuildUiIfNeeded();
            CachePauseState();

            isOpen = true;
            Time.timeScale = 0f;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
            SetVisible(true);
            ClearInput();
            UpdatePanelHeight();
            FocusInput();
            UpdateConsoleCursor(true);
        }

        public void CloseConsole()
        {
            if (!isOpen)
                return;

            isOpen = false;
            ClearAndDeactivateInput();
            ClearSelection();
            SetCursorVisible(false);
            SetVisible(false);
            RestorePauseState();
        }

        private void ExecuteCurrentInput()
        {
            if (inputField == null)
                return;

            string commandLine = inputField.text;
            inputField.text = string.Empty;

            if (string.IsNullOrWhiteSpace(commandLine))
            {
                FocusInput();
                return;
            }

            WriteLine("> " + commandLine.Trim());
            ExecuteCommand(commandLine);
            FocusInput();
        }

        private void ExecuteCommand(string commandLine)
        {
            string[] tokens = Tokenize(commandLine);
            if (tokens.Length == 0)
                return;

            string command = tokens[0].ToLowerInvariant();
            switch (command)
            {
                case "tgm":
                    ExecuteToggleGodMode();
                    return;
                case "tcl":
                    ExecuteToggleNoClip();
                    return;
                case "tai":
                    ExecuteToggleNpcAi();
                    return;
                case "tcai":
                    ExecuteToggleNpcCombatAi();
                    return;
                case "spawn":
                    ExecuteSpawn(tokens);
                    return;
                case "giveme":
                    ExecuteGiveMe(tokens);
                    return;
                case "completeallobjectives":
                    ExecuteCompleteAllObjectives(tokens);
                    return;
                case "rewardxp":
                    ExecuteRewardExperience(tokens);
                    return;
                case "setlevel":
                    ExecuteSetLevel(tokens);
                    return;
                case "modpca":
                    ExecuteModifyPlayerAttribute(tokens);
                    return;
                case "forceav":
                    ExecuteForcePlayerSkill(tokens);
                    return;
                case "setcarry":
                    ExecuteSetCarryWeight(tokens);
                    return;
                case "set":
                    ExecuteSetCommand(tokens);
                    return;
                case "unlock":
                    ExecuteUnlockSelected(tokens);
                    return;
                case "kill":
                    ExecuteKillSelected(tokens);
                    return;
                case "resurrect":
                    ExecuteResurrectSelected(tokens);
                    return;
                case "killall":
                    ExecuteKillAll(tokens);
                    return;
                case "help":
                    WriteLine("Commands: tgm, tcl, tai, tcai, rewardxp <amount>, setlevel <level>, modpca <attribute> <amount>, forceav <skill> <value>, setcarry <weight>, set name <name>, unlock, kill, resurrect, killall, spawn <itemName> [quantity] [condition], giveme <itemName> [quantity] [condition], completeallobjectives [questName]");
                    return;
                default:
                    WriteLine("Unknown command: " + tokens[0]);
                    return;
            }
        }

        private void HandleSelectionClick()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame)
                return;

            Vector2 screenPosition = mouse.position.ReadValue();
            if (IsPointerInsideConsolePanel(screenPosition))
                return;

            Camera camera = Camera.main;
            if (!camera)
                return;

            Ray ray = camera.ScreenPointToRay(screenPosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, SelectionRaycastDistance, ~0, QueryTriggerInteraction.Collide))
                return;

            GameObject target = ResolveSelectableObject(hit.collider);
            if (!target)
                return;

            SelectObject(target);
            FocusInput();
        }

        private bool IsPointerInsideConsolePanel(Vector2 screenPosition)
        {
            if (!panelRect)
                return false;

            Camera uiCamera = canvas && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            return RectTransformUtility.RectangleContainsScreenPoint(panelRect, screenPosition, uiCamera);
        }

        private void SelectObject(GameObject target)
        {
            selectedObject = target;
            UpdateSelectedObjectLabel();
        }

        private void ClearSelection()
        {
            selectedObject = null;
            UpdateSelectedObjectLabel();
        }

        private void UpdateSelectedObjectLabel()
        {
            if (!selectedObjectText)
                return;

            selectedObjectText.text = selectedObject ? BuildSelectedObjectLabel(selectedObject) : string.Empty;
        }

        private static GameObject ResolveSelectableObject(Collider hitCollider)
        {
            if (!hitCollider)
                return null;

            NPC npc = hitCollider.GetComponentInParent<NPC>();
            if (npc)
                return npc.gameObject;

            NPCState npcState = hitCollider.GetComponentInParent<NPCState>();
            if (npcState)
                return npcState.gameObject;

            WorldItem worldItem = hitCollider.GetComponentInParent<WorldItem>();
            if (worldItem)
                return worldItem.gameObject;

            Container container = hitCollider.GetComponentInParent<Container>();
            if (container)
                return container.gameObject;

            Terminal terminal = hitCollider.GetComponentInParent<Terminal>();
            if (terminal)
                return terminal.gameObject;

            Rigidbody attachedRigidbody = hitCollider.attachedRigidbody;
            return attachedRigidbody ? attachedRigidbody.gameObject : hitCollider.gameObject;
        }

        private static string BuildSelectedObjectLabel(GameObject target)
        {
            if (!target)
                return string.Empty;

            string gameObjectName = target.name;
            string playerFacingName = ResolvePlayerFacingName(target);

            if (string.IsNullOrWhiteSpace(playerFacingName))
                playerFacingName = gameObjectName;

            return gameObjectName + " [" + playerFacingName + "]";
        }

        private static string ResolvePlayerFacingName(GameObject target)
        {
            if (!target)
                return string.Empty;

            NPC npc = target.GetComponent<NPC>();
            if (npc)
            {
                string npcName = npc.GetNPCName();
                if (!string.IsNullOrWhiteSpace(npcName))
                    return npcName;
            }

            NPCState npcState = target.GetComponent<NPCState>();
            if (npcState)
            {
                string npcName = npcState.GetNPCName();
                if (!string.IsNullOrWhiteSpace(npcName))
                    return npcName;
            }

            WorldItem worldItem = target.GetComponent<WorldItem>();
            if (worldItem)
            {
                string displayName = worldItem.GetDisplayName();
                if (!string.IsNullOrWhiteSpace(displayName))
                    return displayName;
            }

            Container container = target.GetComponent<Container>();
            if (container)
            {
                string containerName = container.GetContainerName();
                if (!string.IsNullOrWhiteSpace(containerName))
                    return containerName;
            }

            Terminal terminal = target.GetComponent<Terminal>();
            if (terminal)
            {
                string terminalName = terminal.GetTerminalName();
                if (!string.IsNullOrWhiteSpace(terminalName))
                    return terminalName;
            }

            PlayerState selectedPlayerState = target.GetComponent<PlayerState>();
            if (selectedPlayerState)
            {
                string playerName = selectedPlayerState.GetPlayerName();
                if (!string.IsNullOrWhiteSpace(playerName))
                    return playerName;
            }

            return string.Empty;
        }

        private void ExecuteToggleGodMode()
        {
            PlayerState state = playerState ? playerState : FindAnyObjectByType<PlayerState>();
            playerState = state;

            if (!state)
            {
                WriteLine("No PlayerState found.");
                return;
            }

            bool enabled = state.ToggleGodMode();
            WriteLine("God Mode " + FormatEnabled(enabled));
        }

        private void ExecuteToggleNoClip()
        {
            PlayerMovement movement = playerMovement ? playerMovement : FindAnyObjectByType<PlayerMovement>();
            playerMovement = movement;

            if (!movement)
            {
                WriteLine("No PlayerMovement found.");
                return;
            }

            bool enabled = movement.ToggleNoClip();
            WriteLine("Collision " + (enabled ? "off" : "on"));
        }

        private void ExecuteSetCommand(string[] tokens)
        {
            if (tokens.Length < 2)
            {
                WriteLine("Usage: set name <name>");
                return;
            }

            switch (tokens[1].ToLowerInvariant())
            {
                case "name":
                    ExecuteSetName(tokens);
                    return;
                default:
                    WriteLine("Unknown set command: " + tokens[1]);
                    return;
            }
        }

        private void ExecuteSetName(string[] tokens)
        {
            if (tokens.Length < 3)
            {
                WriteLine("Usage: set name <name>");
                return;
            }

            string name = JoinTokens(tokens, 2, tokens.Length - 1).Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                WriteLine("Player name cannot be empty.");
                return;
            }

            if (TrySetSelectedNpcName(name))
            {
                UpdateSelectedObjectLabel();
                return;
            }

            PlayerState state = playerState ? playerState : FindAnyObjectByType<PlayerState>();
            playerState = state;

            if (!state)
            {
                WriteLine("No PlayerState found.");
                return;
            }

            state.SetPlayerName(name);
            WriteLine("Player name -> " + state.GetPlayerName());
        }

        private bool TrySetSelectedNpcName(string name)
        {
            if (!selectedObject)
                return false;

            NPC npc = selectedObject.GetComponent<NPC>();
            if (!npc)
                npc = selectedObject.GetComponentInParent<NPC>();

            if (npc)
            {
                npc.SetNPCName(name);
                WriteLine("NPC name -> " + npc.GetNPCName());
                return true;
            }

            NPCState npcState = selectedObject.GetComponent<NPCState>();
            if (!npcState)
                npcState = selectedObject.GetComponentInParent<NPCState>();

            if (!npcState)
                return false;

            npcState.SetNPCName(name);
            WriteLine("NPC name -> " + npcState.GetNPCName());
            return true;
        }

        private void ExecuteUnlockSelected(string[] tokens)
        {
            if (tokens.Length != 1)
            {
                WriteLine("Usage: unlock");
                return;
            }

            if (!selectedObject)
            {
                WriteLine("No selected object.");
                return;
            }

            if (TryUnlockSelectedTerminal())
                return;

            if (TryUnlockSelectedContainer())
                return;

            WriteLine("Selected object is not lockable: " + selectedObject.name);
        }

        private bool TryUnlockSelectedTerminal()
        {
            Terminal terminal = selectedObject.GetComponent<Terminal>();
            if (!terminal)
                terminal = selectedObject.GetComponentInParent<Terminal>();
            if (!terminal)
                terminal = selectedObject.GetComponentInChildren<Terminal>(true);

            if (!terminal)
                return false;

            if (!terminal.IsLocked())
            {
                WriteLine("Terminal is already unlocked: " + terminal.GetTerminalName());
                return true;
            }

            terminal.Unlock();
            WriteLine("Unlocked terminal: " + terminal.GetTerminalName());
            return true;
        }

        private bool TryUnlockSelectedContainer()
        {
            Container container = selectedObject.GetComponent<Container>();
            if (!container)
                container = selectedObject.GetComponentInParent<Container>();
            if (!container)
                container = selectedObject.GetComponentInChildren<Container>(true);

            if (!container)
                return false;

            if (!container.IsLocked())
            {
                WriteLine("Object is already unlocked: " + container.GetContainerName());
                return true;
            }

            container.Unlock();
            WriteLine("Unlocked object: " + container.GetContainerName());
            return true;
        }

        private void ExecuteKillSelected(string[] tokens)
        {
            if (tokens.Length != 1)
            {
                WriteLine("Usage: kill");
                return;
            }

            if (!selectedObject)
            {
                WriteLine("No selected object.");
                return;
            }

            if (!TryResolveSelectedNpc(out NPC npc, out NPCState npcState))
            {
                WriteLine("Selected object is not an NPC: " + selectedObject.name);
                return;
            }

            if (npc && npc.IsEssential())
            {
                WriteLine("Cannot kill essential NPC: " + ResolveSelectedNpcName(npc, npcState));
                return;
            }

            if (npcState.IsDead())
            {
                WriteLine("NPC is already dead: " + ResolveSelectedNpcName(npc, npcState));
                return;
            }

            npcState.SetHealthPoints(0f);
            WriteLine("Killed NPC: " + ResolveSelectedNpcName(npc, npcState));
        }

        private void ExecuteResurrectSelected(string[] tokens)
        {
            if (tokens.Length != 1)
            {
                WriteLine("Usage: resurrect");
                return;
            }

            if (!selectedObject)
            {
                WriteLine("No selected object.");
                return;
            }

            if (!TryResolveSelectedNpc(out NPC npc, out NPCState npcState))
            {
                WriteLine("Selected object is not an NPC: " + selectedObject.name);
                return;
            }

            string npcName = ResolveSelectedNpcName(npc, npcState);
            if (!npcState.IsDead())
            {
                WriteLine("NPC is not dead: " + npcName);
                return;
            }

            float maxHealthPoints = npcState.GetMaxHealthPoints();
            if (maxHealthPoints <= 0f)
            {
                WriteLine("Cannot resurrect NPC with zero max health: " + npcName);
                return;
            }

            npcState.SetHealthPoints(maxHealthPoints);
            npcState.SetActionPoints(npcState.GetMaxActionPoints());
            ResetResurrectedNpcRuntimeState(npc, npcState);
            WriteLine("Resurrected NPC: " + npcName);
        }

        private void ExecuteKillAll(string[] tokens)
        {
            if (tokens.Length != 1)
            {
                WriteLine("Usage: killall");
                return;
            }

            if (!TryGetKillAllCenter(out Vector3 center))
            {
                WriteLine("No player or camera found for killall center.");
                return;
            }

            float radius = Mathf.Max(0f, killAllRadius);
            float radiusSqr = radius * radius;
            int killedCount = 0;
            int essentialSkippedCount = 0;
            HashSet<NPCState> processedStates = new HashSet<NPCState>();

            NPC[] npcs = FindObjectsByType<NPC>(FindObjectsInactive.Exclude);
            for (int i = 0; i < npcs.Length; i++)
            {
                NPC npc = npcs[i];
                if (!npc || !IsWithinKillAllRadius(npc.transform.position, center, radiusSqr))
                    continue;

                NPCState npcState = ResolveNpcState(npc.gameObject, npc);
                if (!npcState || processedStates.Contains(npcState))
                    continue;

                processedStates.Add(npcState);

                if (npc.IsEssential())
                {
                    essentialSkippedCount++;
                    continue;
                }

                if (npcState.IsDead())
                    continue;

                npcState.SetHealthPoints(0f);
                killedCount++;
            }

            NPCState[] npcStates = FindObjectsByType<NPCState>(FindObjectsInactive.Exclude);
            for (int i = 0; i < npcStates.Length; i++)
            {
                NPCState npcState = npcStates[i];
                if (!npcState || processedStates.Contains(npcState) || !IsWithinKillAllRadius(npcState.transform.position, center, radiusSqr))
                    continue;

                processedStates.Add(npcState);

                NPC npc = npcState.GetComponent<NPC>();
                if (!npc)
                    npc = npcState.GetComponentInParent<NPC>();
                if (!npc)
                    npc = npcState.GetComponentInChildren<NPC>(true);

                if (npc && npc.IsEssential())
                {
                    essentialSkippedCount++;
                    continue;
                }

                if (npcState.IsDead())
                    continue;

                npcState.SetHealthPoints(0f);
                killedCount++;
            }

            string result = "Killed " + killedCount + " NPC(s) within " + radius.ToString("0.##", CultureInfo.InvariantCulture) + " units.";
            if (essentialSkippedCount > 0)
                result += " Skipped " + essentialSkippedCount + " essential NPC(s).";

            WriteLine(result);
        }

        private bool TryGetKillAllCenter(out Vector3 center)
        {
            ResolveGameplayReferences();

            if (playerState)
            {
                center = playerState.transform.position;
                return true;
            }

            if (playerMovement)
            {
                center = playerMovement.transform.position;
                return true;
            }

            Camera camera = Camera.main;
            if (camera)
            {
                center = camera.transform.position;
                return true;
            }

            center = Vector3.zero;
            return false;
        }

        private static bool IsWithinKillAllRadius(Vector3 position, Vector3 center, float radiusSqr)
        {
            return (position - center).sqrMagnitude <= radiusSqr;
        }

        private bool TryResolveSelectedNpc(out NPC npc, out NPCState npcState)
        {
            npc = null;
            npcState = null;

            if (!selectedObject)
                return false;

            npc = selectedObject.GetComponent<NPC>();
            if (!npc)
                npc = selectedObject.GetComponentInParent<NPC>();
            if (!npc)
                npc = selectedObject.GetComponentInChildren<NPC>(true);

            npcState = ResolveNpcState(selectedObject, npc);
            return npcState != null;
        }

        private static void ResetResurrectedNpcRuntimeState(NPC npc, NPCState npcState)
        {
            GameObject source = npc ? npc.gameObject : npcState ? npcState.gameObject : null;
            if (!source || !npcState)
                return;

            npcState.SetCombatMode(false);
            npcState.SetWeaponInHand(false);

            NPCCombat combat = source.GetComponent<NPCCombat>();
            if (!combat)
                combat = source.GetComponentInParent<NPCCombat>();
            if (!combat)
                combat = source.GetComponentInChildren<NPCCombat>(true);
            if (combat)
                combat.StopKillhouseCombat(true);

            NPCAim aim = source.GetComponent<NPCAim>();
            if (!aim)
                aim = source.GetComponentInParent<NPCAim>();
            if (!aim)
                aim = source.GetComponentInChildren<NPCAim>(true);
            if (aim)
                aim.ClearAim();

            NPCMovement movement = source.GetComponent<NPCMovement>();
            if (!movement)
                movement = source.GetComponentInParent<NPCMovement>();
            if (!movement)
                movement = source.GetComponentInChildren<NPCMovement>(true);
            if (movement)
            {
                movement.RecoverAfterResurrection();
            }

            NPCWeaponController weaponController = source.GetComponent<NPCWeaponController>();
            if (!weaponController)
                weaponController = source.GetComponentInParent<NPCWeaponController>();
            if (!weaponController)
                weaponController = source.GetComponentInChildren<NPCWeaponController>(true);
            if (weaponController)
            {
                weaponController.TryEquipUnarmed();
                weaponController.HideEquippedWeaponInHandImmediate();
            }

            npcState.SetCombatMode(false);
            npcState.SetWeaponInHand(false);
        }

        private static NPCState ResolveNpcState(GameObject source, NPC npc)
        {
            NPCState npcState = npc ? npc.GetState() : null;
            if (npcState)
                return npcState;

            if (!source)
                return null;

            npcState = source.GetComponent<NPCState>();
            if (!npcState)
                npcState = source.GetComponentInParent<NPCState>();
            if (!npcState)
                npcState = source.GetComponentInChildren<NPCState>(true);

            return npcState;
        }

        private static string ResolveSelectedNpcName(NPC npc, NPCState npcState)
        {
            if (npc)
            {
                string npcName = npc.GetNPCName();
                if (!string.IsNullOrWhiteSpace(npcName))
                    return npcName;
            }

            if (npcState)
            {
                string npcName = npcState.GetNPCName();
                if (!string.IsNullOrWhiteSpace(npcName))
                    return npcName;
            }

            return "NPC";
        }

        private void ExecuteRewardExperience(string[] tokens)
        {
            if (tokens.Length != 2 || !int.TryParse(tokens[1], out int amount))
            {
                WriteLine("Usage: rewardxp <amount>");
                return;
            }

            PlayerState state = playerState ? playerState : FindAnyObjectByType<PlayerState>();
            playerState = state;

            if (!state)
            {
                WriteLine("No PlayerState found.");
                return;
            }

            int experience = state.AddExperience(amount);
            WriteLine("Player XP " + FormatSignedAmount(amount) + " -> " + experience);
        }

        private void ExecuteSetLevel(string[] tokens)
        {
            if (tokens.Length != 2 || !int.TryParse(tokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int level))
            {
                WriteLine("Usage: setlevel <level>");
                return;
            }

            if (level < 1 || level > PlayerState.MaxPlayerLevel)
            {
                WriteLine("Level must be between 1 and " + PlayerState.MaxPlayerLevel + ".");
                return;
            }

            PlayerState state = playerState ? playerState : FindAnyObjectByType<PlayerState>();
            playerState = state;

            if (!state)
            {
                WriteLine("No PlayerState found.");
                return;
            }

            int appliedLevel = state.SetLevel(level);
            WriteLine("Player level -> " + appliedLevel);
        }

        private void ExecuteModifyPlayerAttribute(string[] tokens)
        {
            if (tokens.Length != 3 || !int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount))
            {
                WriteLine("Usage: modpca <attribute> <amount>");
                return;
            }

            PlayerState state = playerState ? playerState : FindAnyObjectByType<PlayerState>();
            playerState = state;

            if (!state)
            {
                WriteLine("No PlayerState found.");
                return;
            }

            if (!TryModifyPlayerAttribute(state, tokens[1], amount, out string displayName, out int value))
            {
                WriteLine("Unknown S.P.E.C.I.A.L. attribute: " + tokens[1]);
                return;
            }

            WriteLine(displayName + " " + FormatSignedAmount(amount) + " -> " + value);
        }

        private static bool TryModifyPlayerAttribute(PlayerState state, string attribute, int amount, out string displayName, out int value)
        {
            displayName = null;
            value = 0;

            if (!state || string.IsNullOrWhiteSpace(attribute))
                return false;

            switch (NormalizeAttributeName(attribute))
            {
                case "s":
                case "str":
                case "strength":
                    displayName = "Strength";
                    value = AddPlayerAttribute(state.GetStrength(), amount, state.SetStrength);
                    return true;
                case "p":
                case "per":
                case "perception":
                    displayName = "Perception";
                    value = AddPlayerAttribute(state.GetPerception(), amount, state.SetPerception);
                    return true;
                case "e":
                case "end":
                case "endurance":
                    displayName = "Endurance";
                    value = AddPlayerAttribute(state.GetEndurance(), amount, state.SetEndurance);
                    return true;
                case "c":
                case "cha":
                case "charisma":
                    displayName = "Charisma";
                    value = AddPlayerAttribute(state.GetCharisma(), amount, state.SetCharisma);
                    return true;
                case "i":
                case "int":
                case "intelligence":
                    displayName = "Intelligence";
                    value = AddPlayerAttribute(state.GetIntelligence(), amount, state.SetIntelligence);
                    return true;
                case "a":
                case "agi":
                case "agility":
                    displayName = "Agility";
                    value = AddPlayerAttribute(state.GetAgility(), amount, state.SetAgility);
                    return true;
                case "l":
                case "lck":
                case "luck":
                    displayName = "Luck";
                    value = AddPlayerAttribute(state.GetLuck(), amount, state.SetLuck);
                    return true;
                default:
                    return false;
            }
        }

        private static int AddPlayerAttribute(int currentValue, int amount, Action<int> setValue)
        {
            long nextValue = (long)currentValue + amount;
            int clampedValue;

            if (nextValue < 0L)
                clampedValue = 0;
            else if (nextValue > int.MaxValue)
                clampedValue = int.MaxValue;
            else
                clampedValue = (int)nextValue;

            setValue(clampedValue);
            return clampedValue;
        }

        private static string NormalizeAttributeName(string value)
        {
            return value.Trim().Replace(".", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
        }

        private void ExecuteForcePlayerSkill(string[] tokens)
        {
            if (tokens.Length != 3 || !int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value))
            {
                WriteLine("Usage: forceav <skill> <value>");
                return;
            }

            if (value < 0 || value > PlayerState.MaxPlayerSkillValue)
            {
                WriteLine("Skill value must be between 0 and " + PlayerState.MaxPlayerSkillValue + ".");
                return;
            }

            PlayerState state = playerState ? playerState : FindAnyObjectByType<PlayerState>();
            playerState = state;

            if (!state)
            {
                WriteLine("No PlayerState found.");
                return;
            }

            if (!TrySetPlayerSkill(state, tokens[1], value, out string displayName, out int appliedValue))
            {
                WriteLine("Unknown skill: " + tokens[1]);
                return;
            }

            WriteLine(displayName + " -> " + appliedValue);
        }

        private static bool TrySetPlayerSkill(PlayerState state, string skill, int value, out string displayName, out int appliedValue)
        {
            displayName = null;
            appliedValue = 0;

            if (!state || string.IsNullOrWhiteSpace(skill))
                return false;

            switch (NormalizeSkillName(skill))
            {
                case "barter":
                    return SetPlayerSkill(value, state.SetBarter, state.GetBarter, "Barter", out displayName, out appliedValue);
                case "bigguns":
                case "biggun":
                    return SetPlayerSkill(value, state.SetBigGuns, state.GetBigGuns, "Big Guns", out displayName, out appliedValue);
                case "energyweapons":
                case "energyweapon":
                    return SetPlayerSkill(value, state.SetEnergyWeapons, state.GetEnergyWeapons, "Energy Weapons", out displayName, out appliedValue);
                case "explosives":
                case "explosive":
                    return SetPlayerSkill(value, state.SetExplosives, state.GetExplosives, "Explosives", out displayName, out appliedValue);
                case "lockpick":
                case "lockpicking":
                    return SetPlayerSkill(value, state.SetLockpick, state.GetLockpick, "Lockpick", out displayName, out appliedValue);
                case "medicine":
                case "medical":
                    return SetPlayerSkill(value, state.SetMedicine, state.GetMedicine, "Medicine", out displayName, out appliedValue);
                case "meleeweapons":
                case "meleeweapon":
                case "melee":
                    return SetPlayerSkill(value, state.SetMeleeWeapons, state.GetMeleeWeapons, "Melee Weapons", out displayName, out appliedValue);
                case "repair":
                    return SetPlayerSkill(value, state.SetRepair, state.GetRepair, "Repair", out displayName, out appliedValue);
                case "science":
                    return SetPlayerSkill(value, state.SetScience, state.GetScience, "Science", out displayName, out appliedValue);
                case "smallguns":
                case "smallgun":
                    return SetPlayerSkill(value, state.SetSmallGuns, state.GetSmallGuns, "Small Guns", out displayName, out appliedValue);
                case "sneak":
                    return SetPlayerSkill(value, state.SetSneak, state.GetSneak, "Sneak", out displayName, out appliedValue);
                case "speech":
                    return SetPlayerSkill(value, state.SetSpeech, state.GetSpeech, "Speech", out displayName, out appliedValue);
                case "unarmed":
                    return SetPlayerSkill(value, state.SetUnarmed, state.GetUnarmed, "Unarmed", out displayName, out appliedValue);
                default:
                    return false;
            }
        }

        private static bool SetPlayerSkill(int value, Action<int> setValue, Func<int> getValue, string name, out string displayName, out int appliedValue)
        {
            setValue(value);
            displayName = name;
            appliedValue = getValue();
            return true;
        }

        private static string NormalizeSkillName(string value)
        {
            return value.Trim().Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).ToLowerInvariant();
        }

        private void ExecuteSetCarryWeight(string[] tokens)
        {
            if (tokens.Length != 2 || !float.TryParse(tokens[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float maxWeight))
            {
                WriteLine("Usage: setcarry <weight>");
                return;
            }

            if (float.IsNaN(maxWeight) || float.IsInfinity(maxWeight) || maxWeight < 0.0f)
            {
                WriteLine("Carry weight must be a non-negative number.");
                return;
            }

            PlayerInventory inventory = playerInventory ? playerInventory : FindAnyObjectByType<PlayerInventory>();
            playerInventory = inventory;

            if (!inventory)
            {
                WriteLine("No PlayerInventory found.");
                return;
            }

            inventory.SetMaxWeight(maxWeight);
            WriteLine("Max carry weight -> " + inventory.GetMaxWeight().ToString("0.##", CultureInfo.InvariantCulture));
        }

        private void ExecuteToggleNpcAi()
        {
            npcAiEnabled = !npcAiEnabled;
            SetNpcAiEnabled(npcAiEnabled);
            WriteLine("NPC AI " + (npcAiEnabled ? "on" : "off"));
        }

        private void ExecuteToggleNpcCombatAi()
        {
            npcCombatAiEnabled = !npcCombatAiEnabled;
            SetNpcCombatAiEnabled(npcCombatAiEnabled);
            WriteLine("NPC combat AI " + (npcCombatAiEnabled ? "on" : "off"));
        }

        private static void SetNpcAiEnabled(bool enabled)
        {
            PruneNpcAiStateCache();
            PruneNpcCombatAiStateCache();

            if (!enabled)
            {
                npcAiBehaviourEnabledState.Clear();
                npcAiAgentState.Clear();
                DisableNpcAi();
                return;
            }

            RestoreNpcAi();

            if (!npcCombatAiEnabled)
                DisableNpcCombatAi();
        }

        private static void SetNpcCombatAiEnabled(bool enabled)
        {
            PruneNpcCombatAiStateCache();

            if (!enabled)
            {
                npcCombatAiBehaviourEnabledState.Clear();
                DisableNpcCombatAi();
                return;
            }

            RestoreNpcCombatAi();
        }

        private static void DisableNpcAi()
        {
            NPC[] npcs = FindObjectsByType<NPC>(FindObjectsInactive.Exclude);
            for (int i = 0; i < npcs.Length; i++)
            {
                NPC npc = npcs[i];
                if (!npc)
                    continue;

                DisableNpcAiBehaviours(npc);
                StopNpcNavMeshAgents(npc);
            }
        }

        private static void DisableNpcAiBehaviours(NPC npc)
        {
            DisableNpcAiBehaviours(npc.GetComponentsInChildren<NPCCombat>(true), npcAiBehaviourEnabledState);
            DisableNpcAiBehaviours(npc.GetComponentsInChildren<NPCMovement>(true), npcAiBehaviourEnabledState);
            DisableNpcAiBehaviours(npc.GetComponentsInChildren<NPCAim>(true), npcAiBehaviourEnabledState);
            DisableNpcAiBehaviours(npc.GetComponentsInChildren<NPCWeaponController>(true), npcAiBehaviourEnabledState);
            DisableNpcAiBehaviours(npc.GetComponentsInChildren<NPCTestDriver>(true), npcAiBehaviourEnabledState);
        }

        private static void DisableNpcCombatAi()
        {
            NPC[] npcs = FindObjectsByType<NPC>(FindObjectsInactive.Exclude);
            for (int i = 0; i < npcs.Length; i++)
            {
                NPC npc = npcs[i];
                if (!npc)
                    continue;

                DisableNpcAiBehaviours(npc.GetComponentsInChildren<NPCCombat>(true), npcCombatAiBehaviourEnabledState);
                DisableNpcAiBehaviours(npc.GetComponentsInChildren<NPCAim>(true), npcCombatAiBehaviourEnabledState);
                DisableNpcAiBehaviours(npc.GetComponentsInChildren<NPCWeaponController>(true), npcCombatAiBehaviourEnabledState);
            }
        }

        private static void DisableNpcAiBehaviours<T>(T[] behaviours, Dictionary<Behaviour, bool> enabledStateCache) where T : Behaviour
        {
            if (behaviours == null)
                return;

            for (int i = 0; i < behaviours.Length; i++)
            {
                T behaviour = behaviours[i];
                if (!behaviour)
                    continue;

                if (!enabledStateCache.ContainsKey(behaviour))
                {
                    bool previousEnabled = behaviour.enabled;
                    if (npcAiBehaviourEnabledState.TryGetValue(behaviour, out bool preTaiEnabled))
                        previousEnabled = preTaiEnabled;

                    enabledStateCache.Add(behaviour, previousEnabled);
                }

                behaviour.enabled = false;
            }
        }

        private static void StopNpcNavMeshAgents(NPC npc)
        {
            UnityEngine.AI.NavMeshAgent[] agents = npc.GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true);
            for (int i = 0; i < agents.Length; i++)
            {
                UnityEngine.AI.NavMeshAgent agent = agents[i];
                if (!agent || npcAiAgentState.ContainsKey(agent))
                    continue;

                npcAiAgentState.Add(agent, new NpcNavMeshAgentState(agent));
                if (agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                    agent.velocity = Vector3.zero;
                    agent.ResetPath();
                }
            }
        }

        private static void RestoreNpcAi()
        {
            foreach (KeyValuePair<Behaviour, bool> state in npcAiBehaviourEnabledState)
            {
                if (state.Key)
                    state.Key.enabled = state.Value;
            }

            foreach (KeyValuePair<UnityEngine.AI.NavMeshAgent, NpcNavMeshAgentState> state in npcAiAgentState)
            {
                UnityEngine.AI.NavMeshAgent agent = state.Key;
                if (!agent || !agent.enabled || !agent.isOnNavMesh)
                    continue;

                agent.isStopped = state.Value.IsStopped;
                agent.velocity = state.Value.Velocity;
            }

            npcAiBehaviourEnabledState.Clear();
            npcAiAgentState.Clear();
        }

        private static void RestoreNpcCombatAi()
        {
            if (npcAiEnabled)
            {
                foreach (KeyValuePair<Behaviour, bool> state in npcCombatAiBehaviourEnabledState)
                {
                    if (state.Key)
                        state.Key.enabled = state.Value;
                }
            }

            npcCombatAiBehaviourEnabledState.Clear();
        }

        private static void PruneNpcAiStateCache()
        {
            List<Behaviour> missingBehaviours = new List<Behaviour>();
            foreach (KeyValuePair<Behaviour, bool> state in npcAiBehaviourEnabledState)
            {
                if (!state.Key)
                    missingBehaviours.Add(state.Key);
            }

            for (int i = 0; i < missingBehaviours.Count; i++)
                npcAiBehaviourEnabledState.Remove(missingBehaviours[i]);

            List<UnityEngine.AI.NavMeshAgent> missingAgents = new List<UnityEngine.AI.NavMeshAgent>();
            foreach (KeyValuePair<UnityEngine.AI.NavMeshAgent, NpcNavMeshAgentState> state in npcAiAgentState)
            {
                if (!state.Key)
                    missingAgents.Add(state.Key);
            }

            for (int i = 0; i < missingAgents.Count; i++)
                npcAiAgentState.Remove(missingAgents[i]);
        }

        private static void PruneNpcCombatAiStateCache()
        {
            List<Behaviour> missingBehaviours = new List<Behaviour>();
            foreach (KeyValuePair<Behaviour, bool> state in npcCombatAiBehaviourEnabledState)
            {
                if (!state.Key)
                    missingBehaviours.Add(state.Key);
            }

            for (int i = 0; i < missingBehaviours.Count; i++)
                npcCombatAiBehaviourEnabledState.Remove(missingBehaviours[i]);
        }

        private void ExecuteSpawn(string[] tokens)
        {
            if (!TryResolveItemCommand(tokens, "spawn", out string itemName, out int quantity, out float conditionPercent, out GameObject prefab, out WorldItem prefabWorldItem))
                return;

            Transform spawnPoint = ResolveDropPoint();
            Vector3 basePosition = spawnPoint ? spawnPoint.position : ResolveFallbackSpawnPosition();
            Quaternion baseRotation = spawnPoint ? spawnPoint.rotation : Quaternion.identity;
            bool spawnAsStack = prefabWorldItem.IsStackable();
            int instancesToSpawn = spawnAsStack ? 1 : quantity;

            for (int i = 0; i < instancesToSpawn; i++)
            {
                Vector3 position = basePosition + GetSpawnOffset(i, instancesToSpawn);
                GameObject spawned = Instantiate(prefab, position, baseRotation);

                WorldItem spawnedWorldItem = spawned.GetComponent<WorldItem>();
                if (spawnedWorldItem != null)
                {
                    spawnedWorldItem.SetQuantity(spawnAsStack ? quantity : 1);
                    spawnedWorldItem.SetConditionPercent(conditionPercent);
                }

                SetSpawnedWeaponMagazineToDefault(spawnedWorldItem);

                Rigidbody body = spawned.GetComponent<Rigidbody>();
                if (body)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                }
            }

            WriteLine("Spawned " + quantity + " x " + itemName);
        }

        private void ExecuteGiveMe(string[] tokens)
        {
            if (!TryResolveItemCommand(tokens, "giveme", out string itemName, out int quantity, out float conditionPercent, out _, out WorldItem prefabWorldItem))
                return;

            PlayerInventory inventory = ResolvePlayerInventory();
            if (!inventory)
            {
                WriteLine("No PlayerInventory found.");
                return;
            }

            if (!TryAddWorldPrefabToInventory(inventory, prefabWorldItem, quantity, conditionPercent))
            {
                WriteLine("Could not add item to inventory: " + itemName);
                return;
            }

            WriteLine("Added " + quantity + " x " + itemName);
        }

        private void ExecuteCompleteAllObjectives(string[] tokens)
        {
            QuestController controller = ResolveQuestController();
            if (!controller)
            {
                WriteLine("No QuestController found.");
                return;
            }

            QuestDefinition definition;
            if (tokens.Length <= 1)
            {
                QuestRuntimeState currentQuest = controller.GetCurrentQuest();
                definition = currentQuest != null ? currentQuest.GetDefinition() : null;

                if (!definition)
                {
                    WriteLine("No current quest found.");
                    return;
                }
            }
            else
            {
                string questName = JoinTokens(tokens, 1, tokens.Length - 1);
                if (!controller.TryFindQuestDefinition(questName, out definition) || !definition)
                {
                    WriteLine("Unknown quest: " + questName);
                    return;
                }
            }

            if (!controller.CompleteAllObjectives(definition))
            {
                WriteLine("Could not complete objectives for quest: " + GetQuestDisplayName(definition));
                return;
            }

            WriteLine("Completed all objectives for quest: " + GetQuestDisplayName(definition));
        }

        private bool TryResolveItemCommand(
            string[] tokens,
            string commandName,
            out string itemName,
            out int quantity,
            out float conditionPercent,
            out GameObject prefab,
            out WorldItem prefabWorldItem)
        {
            itemName = string.Empty;
            quantity = 1;
            conditionPercent = 100.0f;
            prefab = null;
            prefabWorldItem = null;

            if (tokens.Length < 2)
            {
                WriteLine("Usage: " + commandName + " <itemName> [quantity] [condition]");
                return false;
            }

            int itemNameLastToken = tokens.Length - 1;

            if (tokens.Length >= 3 &&
                int.TryParse(tokens[tokens.Length - 2], out int parsedQuantityBeforeCondition) &&
                TryParseFloatInvariant(tokens[tokens.Length - 1], out float parsedCondition))
            {
                if (parsedCondition < 0.0f || parsedCondition > 100.0f)
                {
                    WriteLine("Condition must be between 0 and 100.");
                    return false;
                }

                quantity = Mathf.Max(1, parsedQuantityBeforeCondition);
                conditionPercent = parsedCondition;
                itemNameLastToken = tokens.Length - 3;
            }
            else if (int.TryParse(tokens[tokens.Length - 1], out int parsedQuantity))
            {
                quantity = Mathf.Max(1, parsedQuantity);
                itemNameLastToken = tokens.Length - 2;
            }

            if (itemNameLastToken < 1)
            {
                WriteLine("Usage: " + commandName + " <itemName> [quantity] [condition]");
                return false;
            }

            itemName = JoinTokens(tokens, 1, itemNameLastToken);
            ItemDatabase database = itemDatabase ? itemDatabase : Resources.Load<ItemDatabase>(ItemDatabaseResourcePath);
            itemDatabase = database;

            if (!database)
            {
                WriteLine("No ItemDatabase found in Resources.");
                return false;
            }

            if (!database.TryGetItemPrefab(itemName, out prefab) || !prefab)
            {
                WriteLine("Unknown item: " + itemName);
                return false;
            }

            prefabWorldItem = prefab.GetComponent<WorldItem>();
            if (!prefabWorldItem)
            {
                WriteLine("Item prefab is missing WorldItem: " + prefab.name);
                return false;
            }

            return true;
        }

        private bool TryAddWorldPrefabToInventory(PlayerInventory inventory, WorldItem prefabWorldItem, int quantity, float conditionPercent)
        {
            if (!inventory || !prefabWorldItem || quantity <= 0)
                return false;

            ScriptableObject itemDefinition = ResolveInventoryDefinition(prefabWorldItem.GetItemDefinition());
            if (!itemDefinition)
                return false;

            if (ShouldAddAsStack(itemDefinition, prefabWorldItem))
                return inventory.AddItem(itemDefinition, quantity);

            int loadedMagazineRounds = GetDefaultLoadedMagazineRounds(itemDefinition);

            for (int i = 0; i < quantity; i++)
            {
                if (!inventory.AddItemInstance(itemDefinition, conditionPercent, loadedMagazineRounds))
                    return false;
            }

            return true;
        }

        private static void SetSpawnedWeaponMagazineToDefault(WorldItem spawnedWorldItem)
        {
            if (!spawnedWorldItem)
                return;

            int loadedMagazineRounds = GetDefaultLoadedMagazineRounds(ResolveInventoryDefinition(spawnedWorldItem.GetItemDefinition()));
            if (loadedMagazineRounds <= 0)
                return;

            WeaponItem weaponItem = spawnedWorldItem.GetComponent<WeaponItem>();
            if (weaponItem != null)
                weaponItem.SetLoadedMagazineRounds(loadedMagazineRounds);
        }

        private static int GetDefaultLoadedMagazineRounds(ScriptableObject itemDefinition)
        {
            if (itemDefinition is WeaponDefinition weaponDefinition)
                return Mathf.Max(0, weaponDefinition.GetMagazineSize());

            return 0;
        }

        private static bool TryParseFloatInvariant(string token, out float value)
        {
            return float.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool ShouldAddAsStack(ScriptableObject itemDefinition, WorldItem prefabWorldItem)
        {
            if (!itemDefinition)
                return false;

            if (itemDefinition is WeaponDefinition || itemDefinition is ApparelDefinition)
                return false;

            if (itemDefinition is AidDefinition aidDefinition) return aidDefinition.IsStackable();
            if (itemDefinition is MiscDefinition miscDefinition) return miscDefinition.IsStackable();
            if (itemDefinition is AmmoDefinition ammoDefinition) return ammoDefinition.IsStackable();

            return prefabWorldItem != null && prefabWorldItem.IsStackable();
        }

        private static ScriptableObject ResolveInventoryDefinition(ScriptableObject itemDefinition)
        {
            if (itemDefinition is AmmoItemDefinition ammoItemDefinition)
                return ammoItemDefinition.GetAmmoDefinition();

            return itemDefinition;
        }

        private Transform ResolveDropPoint()
        {
            if (dropPoint)
                return dropPoint;

            PlayerState state = playerState ? playerState : FindAnyObjectByType<PlayerState>();
            playerState = state;

            if (state)
            {
                Transform[] children = state.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < children.Length; i++)
                {
                    if (children[i] && string.Equals(children[i].name, DropPointName, StringComparison.OrdinalIgnoreCase))
                    {
                        dropPoint = children[i];
                        return dropPoint;
                    }
                }
            }

            GameObject namedDropPoint = GameObject.Find(DropPointName);
            if (namedDropPoint)
                dropPoint = namedDropPoint.transform;

            return dropPoint;
        }

        private Vector3 ResolveFallbackSpawnPosition()
        {
            PlayerState state = playerState ? playerState : FindAnyObjectByType<PlayerState>();
            if (state)
                return state.transform.position + state.transform.forward;

            return Vector3.zero;
        }

        private static Vector3 GetSpawnOffset(int index, int count)
        {
            if (count <= 1)
                return Vector3.zero;

            float angle = (Mathf.PI * 2f * index) / count;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * SpawnScatterRadius;
        }

        private void ResolveGameplayReferences()
        {
            if (!playerState)
                playerState = FindAnyObjectByType<PlayerState>();

            if (!playerMovement)
                playerMovement = FindAnyObjectByType<PlayerMovement>();

            if (!playerInventory)
                playerInventory = FindAnyObjectByType<PlayerInventory>();

            if (!questController)
                questController = FindAnyObjectByType<QuestController>();

            if (!dropPoint)
                ResolveDropPoint();
        }

        private PlayerInventory ResolvePlayerInventory()
        {
            if (!playerInventory)
                playerInventory = FindAnyObjectByType<PlayerInventory>();

            return playerInventory;
        }

        private QuestController ResolveQuestController()
        {
            if (!questController)
                questController = QuestController.FindOrCreate();

            return questController;
        }

        private void ResolveItemDatabase()
        {
            if (!itemDatabase)
                itemDatabase = Resources.Load<ItemDatabase>(ItemDatabaseResourcePath);
        }

        private void CachePauseState()
        {
            cachedTimeScale = Time.timeScale;
            cachedCursorVisible = Cursor.visible;
            cachedCursorLockState = Cursor.lockState;
            hasCachedPauseState = true;
        }

        private void RestorePauseState()
        {
            if (!hasCachedPauseState)
                return;

            Time.timeScale = cachedTimeScale;
            Cursor.visible = cachedCursorVisible;
            Cursor.lockState = cachedCursorLockState;
            hasCachedPauseState = false;
        }

        private void BuildUiIfNeeded()
        {
            if (canvas && inputField && promptText && outputText && canvasGroup && panelRect && cursorRect && cursorImage && selectedObjectText)
                return;

            GameObject canvasObject = new GameObject("ConsoleCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            canvasObject.transform.SetParent(transform, false);

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGroup = canvasObject.GetComponent<CanvasGroup>();

            GameObject panelObject = new GameObject("ConsolePanel", typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(canvasObject.transform, false);
            panelRect = panelObject.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(0f, InputLineHeight);

            Image panelImage = panelObject.GetComponent<Image>();
            panelImage.color = new Color(0.12f, 0.12f, 0.12f, 0.78f);

            outputText = CreateText(panelObject.transform, "ConsoleOutput", 20f, TextAlignmentOptions.BottomLeft);
            RectTransform outputRect = outputText.rectTransform;
            outputRect.anchorMin = new Vector2(0f, 0f);
            outputRect.anchorMax = new Vector2(1f, 0f);
            outputRect.pivot = new Vector2(0.5f, 0f);
            outputRect.anchoredPosition = new Vector2(0f, InputLineHeight);
            outputRect.sizeDelta = new Vector2(-28f, 0f);
            outputText.text = string.Empty;

            promptText = CreateText(panelObject.transform, "ConsolePrompt", 22f, TextAlignmentOptions.Left);
            promptText.text = ">";
            RectTransform promptRect = promptText.rectTransform;
            promptRect.anchorMin = new Vector2(0f, 0f);
            promptRect.anchorMax = new Vector2(0f, 0f);
            promptRect.pivot = new Vector2(0f, 0f);
            promptRect.anchoredPosition = new Vector2(14f, 0f);
            promptRect.sizeDelta = new Vector2(18f, InputLineHeight);

            GameObject inputObject = new GameObject("ConsoleInput", typeof(RectTransform), typeof(TMP_InputField));
            inputObject.transform.SetParent(panelObject.transform, false);
            RectTransform inputRect = inputObject.GetComponent<RectTransform>();
            inputRect.anchorMin = new Vector2(0f, 0f);
            inputRect.anchorMax = new Vector2(1f, 0f);
            inputRect.pivot = new Vector2(0.5f, 0f);
            inputRect.offsetMin = new Vector2(34f, 0f);
            inputRect.offsetMax = new Vector2(-12f, InputLineHeight);

            TMP_Text inputText = CreateText(inputObject.transform, "Text", 22f, TextAlignmentOptions.Left);
            RectTransform textRect = inputText.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(0f, 5f);
            textRect.offsetMax = new Vector2(0f, -5f);

            inputField = inputObject.GetComponent<TMP_InputField>();
            inputField.textComponent = inputText;
            inputField.lineType = TMP_InputField.LineType.SingleLine;
            inputField.customCaretColor = true;
            inputField.caretColor = Color.clear;
            inputField.selectionColor = new Color(1f, 1f, 1f, 0.25f);
            inputField.caretWidth = 1;
            inputField.caretBlinkRate = 0.65f;

            GameObject cursorObject = new GameObject("ConsoleCursor", typeof(RectTransform), typeof(Image));
            cursorObject.transform.SetParent(inputObject.transform, false);
            cursorRect = cursorObject.GetComponent<RectTransform>();
            cursorRect.anchorMin = new Vector2(0f, 0.5f);
            cursorRect.anchorMax = new Vector2(0f, 0.5f);
            cursorRect.pivot = new Vector2(0f, 0.5f);
            cursorRect.sizeDelta = new Vector2(8f, 24f);

            cursorImage = cursorObject.GetComponent<Image>();
            cursorImage.color = Color.white;
            cursorImage.raycastTarget = false;

            selectedObjectText = CreateText(canvasObject.transform, "ConsoleSelectedObject", 24f, TextAlignmentOptions.Top);
            selectedObjectText.richText = false;
            RectTransform selectedObjectRect = selectedObjectText.rectTransform;
            selectedObjectRect.anchorMin = new Vector2(0.5f, 1f);
            selectedObjectRect.anchorMax = new Vector2(0.5f, 1f);
            selectedObjectRect.pivot = new Vector2(0.5f, 1f);
            selectedObjectRect.anchoredPosition = new Vector2(0f, -18f);
            selectedObjectRect.sizeDelta = new Vector2(1000f, 44f);
            UpdateSelectedObjectLabel();

            UpdatePanelHeight();
            UpdateConsoleCursor(true);
        }

        private TMP_Text CreateText(Transform parent, string objectName, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            textObject.transform.SetParent(parent, false);

            TMP_Text text = textObject.GetComponent<TMP_Text>();
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            return text;
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;

            if (canvas)
                canvas.enabled = visible;

            if (visible)
            {
                UpdatePanelHeight();
                UpdateSelectedObjectLabel();
            }
        }

        private void FocusInput()
        {
            if (!inputField)
                return;

            EnsureEventSystem();
            inputField.ActivateInputField();
            inputField.Select();
        }

        private void ClearInput()
        {
            if (!inputField)
                return;

            inputField.SetTextWithoutNotify(string.Empty);
            UpdateConsoleCursor(true);
        }

        private void ClearAndDeactivateInput()
        {
            if (!inputField)
                return;

            inputField.SetTextWithoutNotify(string.Empty);
            inputField.DeactivateInputField(true);
            UpdateConsoleCursor(true);

            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == inputField.gameObject)
                EventSystem.current.SetSelectedGameObject(null);
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
                return;

            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        private void WriteLine(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            outputLines.Add(line);

            if (outputText)
            {
                outputText.text = string.Join("\n", outputLines);
                UpdatePanelHeight();
            }

            Debug.Log("[Console] " + line);
        }

        private void UpdatePanelHeight()
        {
            if (!panelRect)
                return;

            int outputLineCount = outputLines.Count;
            float outputHeight = outputLineCount > 0
                ? (outputLineCount * OutputLineHeight) + OutputVerticalPadding
                : 0f;

            panelRect.sizeDelta = new Vector2(panelRect.sizeDelta.x, InputLineHeight + outputHeight);

            if (outputText)
            {
                RectTransform outputRect = outputText.rectTransform;
                outputRect.sizeDelta = new Vector2(outputRect.sizeDelta.x, outputHeight);
            }
        }

        private void UpdateConsoleCursor(bool forceVisible = false)
        {
            if (!cursorRect || !cursorImage || !inputField || !inputField.textComponent)
                return;

            bool shouldShow = isOpen && inputField.isFocused;
            if (!shouldShow)
            {
                SetCursorVisible(false);
                return;
            }

            TMP_Text textComponent = inputField.textComponent;
            RectTransform inputRect = inputField.GetComponent<RectTransform>();
            int caretPosition = Mathf.Clamp(inputField.caretPosition, 0, inputField.text.Length);
            string textBeforeCaret = caretPosition > 0 ? inputField.text.Substring(0, caretPosition) : string.Empty;
            float preferredWidth = textComponent.GetPreferredValues(textBeforeCaret).x;
            float maxX = inputRect ? Mathf.Max(0f, inputRect.rect.width - cursorRect.sizeDelta.x) : preferredWidth;
            float cursorX = Mathf.Clamp(preferredWidth, 0f, maxX);

            cursorRect.anchoredPosition = new Vector2(cursorX, 0f);

            bool blinkOn = forceVisible || Mathf.Repeat(Time.unscaledTime, CursorBlinkIntervalSeconds * 2f) < CursorBlinkIntervalSeconds;
            SetCursorVisible(blinkOn);
        }

        private void SetCursorVisible(bool visible)
        {
            if (cursorImage)
                cursorImage.enabled = visible;
        }

        private static string[] Tokenize(string commandLine)
        {
            List<string> tokens = new List<string>();
            bool inQuote = false;
            string current = string.Empty;

            for (int i = 0; i < commandLine.Length; i++)
            {
                char c = commandLine[i];
                if (c == '"')
                {
                    inQuote = !inQuote;
                    continue;
                }

                if (char.IsWhiteSpace(c) && !inQuote)
                {
                    if (!string.IsNullOrWhiteSpace(current))
                    {
                        tokens.Add(current);
                        current = string.Empty;
                    }

                    continue;
                }

                current += c;
            }

            if (!string.IsNullOrWhiteSpace(current))
                tokens.Add(current);

            return tokens.ToArray();
        }

        private static string JoinTokens(string[] tokens, int firstIndex, int lastIndex)
        {
            if (tokens == null || firstIndex > lastIndex)
                return string.Empty;

            List<string> parts = new List<string>();
            for (int i = firstIndex; i <= lastIndex; i++)
                parts.Add(tokens[i]);

            return string.Join(" ", parts);
        }

        private static string FormatEnabled(bool enabled)
        {
            return enabled ? "enabled" : "disabled";
        }

        private static string FormatSignedAmount(int amount)
        {
            return amount >= 0 ? "+" + amount : amount.ToString();
        }

        private static string GetQuestDisplayName(QuestDefinition definition)
        {
            if (!definition)
                return "Unknown";

            string displayName = definition.GetDisplayName();
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName;

            string questId = definition.GetQuestId();
            return string.IsNullOrWhiteSpace(questId) ? definition.name : questId;
        }
    }
}
