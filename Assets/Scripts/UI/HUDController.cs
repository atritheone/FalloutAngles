// imports
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;



// class
namespace UI
{
    public class FalloutHUDController : MonoBehaviour
    {
        private const float MinMaxValue = 0.01f;
        private const float MinRayDistance = 0.01f;
        private const string HiddenText = "HIDDEN";
        private const string CautionText = "CAUTION";
        private const string DangerText = "DANGER";

        // Cached gameplay references.
        private PlayerState playerState;
        private PlayerMovement playerMovement;
        private PlayerStealth playerStealth;
        private PlayerWeaponController playerWeaponController;
        private PlayerCombat playerCombat;
        private PlayerInventory playerInventory;
        private Transform headingTransform;

        // Cached HUD element references.
        private Image hpBarFill;
        private Image apBarFill;
        private Image conditionBarFill;
        private TMP_Text ammoText;
        private TMP_Text compassText;
        private GameObject stealthPanel;
        private TMP_Text stealthText;
        private Graphic leftStealthSeparator;
        private Graphic rightStealthSeparator;
        private GameObject npcHpPanel;
        private TMP_Text npcHpNameText;
        private Image npcHpBar;

        [Header("Stealth HUD")]
        [SerializeField] private bool useOriginalHiddenStealthColors = true;
        [SerializeField] private Color hiddenStealthColor = Color.white;
        [SerializeField] private Color alertStealthColor = Color.red;
        [SerializeField, Min(0.1f)] private float dangerFlashSpeed = 8f;
        [SerializeField, Range(0f, 1f)] private float dangerFlashMinAlpha = 0.2f;
        [SerializeField, Min(0f)] private float stealthSeparatorGapPixels = 10f;

        [Header("NPC HP HUD")]
        [SerializeField, Min(0.1f)] private float npcHpPanelHoldSeconds = 5f;
        [SerializeField, Min(0.01f)] private float npcHpTargetRayDistance = 250f;
        [SerializeField] private LayerMask npcHpTargetLayers = ~0;
        [SerializeField] private QueryTriggerInteraction npcHpTargetTriggerInteraction = QueryTriggerInteraction.Collide;

        // Last displayed values to avoid redundant UI writes.
        private int lastMagazineAmmo = -1;
        private int lastReserveAmmo = -1;
        private string lastCompassHeading = string.Empty;
        private bool hasDefaultStealthTextColor;
        private bool hasDefaultLeftStealthSeparatorColor;
        private bool hasDefaultRightStealthSeparatorColor;
        private Color defaultStealthTextColor;
        private Color defaultLeftStealthSeparatorColor;
        private Color defaultRightStealthSeparatorColor;
        private NPCState trackedNpcHpTarget;
        private float npcHpPanelVisibleUntil;
        private readonly RaycastHit[] npcHpTargetHits = new RaycastHit[32];


        // methods
        private void Awake()
        {
            ResolveUiReferences();
            ResolveGameplayReferences();
            SetNpcHpPanelActive(false);
            RefreshHud();
        }


        private void OnEnable()
        {
            PlayerCombat.PlayerDamagedNpc += HandlePlayerDamagedNpc;
        }


        private void OnDisable()
        {
            PlayerCombat.PlayerDamagedNpc -= HandlePlayerDamagedNpc;
        }


        private void Update()
        {
            ResolveGameplayReferences();
            RefreshHud();
        }


        private void ResolveUiReferences()
        {
            if (hpBarFill == null)
                hpBarFill = FindChildComponentByName<Image>("HPBarFill");

            if (apBarFill == null)
                apBarFill = FindChildComponentByName<Image>("APBarFill");

            if (conditionBarFill == null)
                conditionBarFill = FindChildComponentByName<Image>("ConditionBarFill");

            if (ammoText == null)
                ammoText = FindChildComponentByName<TMP_Text>("AmmoText");

            if (compassText == null)
                compassText = FindChildComponentByName<TMP_Text>("CompassText");

            if (npcHpPanel == null)
            {
                Transform npcHpPanelTransform = FindChildByName(transform, "NPCHPPanel");
                if (npcHpPanelTransform != null)
                    npcHpPanel = npcHpPanelTransform.gameObject;
            }

            Transform npcHpRoot = npcHpPanel != null ? npcHpPanel.transform : transform;

            if (npcHpNameText == null)
                npcHpNameText = FindChildComponentByName<TMP_Text>(npcHpRoot, "NPCHPNameText");

            if (npcHpBar == null)
                npcHpBar = FindChildComponentByName<Image>(npcHpRoot, "NPCHPBar");

            if (npcHpBar == null)
                npcHpBar = FindChildComponentByName<Image>(npcHpRoot, "NPCHPBarFill");

            if (stealthPanel == null)
            {
                Transform stealthPanelTransform = FindChildByName(transform, "StealthPanel");
                if (stealthPanelTransform != null)
                    stealthPanel = stealthPanelTransform.gameObject;
            }

            Transform stealthRoot = stealthPanel != null ? stealthPanel.transform : transform;

            if (stealthText == null)
                stealthText = FindChildComponentByName<TMP_Text>(stealthRoot, "StealthText");

            if (leftStealthSeparator == null)
                leftStealthSeparator = FindFirstGraphic(stealthRoot, "LeftSeparator", "LeftSeperator", "StealthLeftSeparator", "StealthLeftSeperator");

            if (rightStealthSeparator == null)
                rightStealthSeparator = FindFirstGraphic(stealthRoot, "RightSeparator", "RightSeperator", "StealthRightSeparator", "StealthRightSeperator");

            if (leftStealthSeparator == null)
                leftStealthSeparator = FindFirstGraphicByTokens(stealthRoot, "left", "separator", "seperator");

            if (rightStealthSeparator == null)
                rightStealthSeparator = FindFirstGraphicByTokens(stealthRoot, "right", "separator", "seperator");

            CacheDefaultStealthColors();
        }


        private void ResolveGameplayReferences()
        {
            if (playerState == null)
                playerState = FindAnyObjectByType<PlayerState>();

            if (playerMovement == null)
                playerMovement = FindAnyObjectByType<PlayerMovement>();

            if (playerStealth == null)
                playerStealth = FindAnyObjectByType<PlayerStealth>();

            if (playerStealth == null && playerMovement != null)
                playerStealth = playerMovement.gameObject.AddComponent<PlayerStealth>();

            if (playerWeaponController == null)
                playerWeaponController = FindAnyObjectByType<PlayerWeaponController>();

            if (playerCombat == null)
                playerCombat = FindAnyObjectByType<PlayerCombat>();

            if (playerInventory == null)
                playerInventory = FindAnyObjectByType<PlayerInventory>();

            if (headingTransform == null)
            {
                Camera mainCamera = Camera.main;

                if (mainCamera != null)
                    headingTransform = mainCamera.transform;
                else if (playerState != null)
                    headingTransform = playerState.transform;
            }
        }


        private void RefreshHud()
        {
            RefreshHpApBars();
            RefreshConditionBar();
            RefreshAmmoText();
            RefreshCompassText();
            RefreshStealthPanel();
            RefreshNpcHpPanel();
        }


        private void RefreshHpApBars()
        {
            float hpFillAmount = 1.0f;
            float apFillAmount = 1.0f;

            if (playerState != null)
            {
                float currentHp = Mathf.Max(0.0f, playerState.GetHealthPoints());
                float maxHp = Mathf.Max(MinMaxValue, playerState.GetMaxHealthPoints());
                hpFillAmount = Mathf.Clamp01(currentHp / maxHp);

                float currentAp = Mathf.Max(0.0f, playerState.GetActionPoints());
                float maxAp = Mathf.Max(MinMaxValue, playerState.GetMaxActionPoints());
                apFillAmount = Mathf.Clamp01(currentAp / maxAp);
            }

            if (hpBarFill != null)
                hpBarFill.fillAmount = hpFillAmount;

            if (apBarFill != null)
                apBarFill.fillAmount = apFillAmount;
        }


        private void RefreshConditionBar()
        {
            if (conditionBarFill == null)
                return;

            float conditionPercent = 100.0f;

            if (TryGetEquippedWeaponConditionPercent(out float resolvedConditionPercent))
                conditionPercent = resolvedConditionPercent;

            conditionBarFill.fillAmount = Mathf.Clamp01(conditionPercent / 100.0f);
        }


        private void RefreshAmmoText()
        {
            if (ammoText == null)
                return;

            int magazineRounds = 0;
            int reserveRounds = 0;

            if (playerWeaponController != null)
            {
                magazineRounds = Mathf.Max(0, playerWeaponController.GetCurrentWeaponAmmo());
                reserveRounds = Mathf.Max(0, playerWeaponController.GetCurrentWeaponReserveAmmo());
            }

            if (magazineRounds == lastMagazineAmmo && reserveRounds == lastReserveAmmo)
                return;

            lastMagazineAmmo = magazineRounds;
            lastReserveAmmo = reserveRounds;
            ammoText.text = $"{magazineRounds}/{reserveRounds}";
        }


        private void RefreshCompassText()
        {
            if (compassText == null)
                return;

            string heading = "N";

            if (headingTransform != null)
                heading = ConvertYawToCompassHeading(headingTransform.eulerAngles.y);

            if (heading == lastCompassHeading)
                return;

            lastCompassHeading = heading;
            compassText.text = heading;
        }


        private void RefreshStealthPanel()
        {
            if (stealthPanel == null)
                return;

            if (playerStealth == null || !playerStealth.IsStealthActive)
            {
                if (stealthPanel.activeSelf)
                    stealthPanel.SetActive(false);

                return;
            }

            if (!stealthPanel.activeSelf)
                stealthPanel.SetActive(true);

            PlayerStealth.StealthState stealthState = playerStealth.RefreshStealthState();
            switch (stealthState)
            {
                case PlayerStealth.StealthState.Danger:
                    SetStealthText(DangerText);
                    ApplyDangerStealthFlash();
                    break;
                case PlayerStealth.StealthState.Caution:
                    SetStealthText(CautionText);
                    ApplyStealthElementColor(alertStealthColor);
                    break;
                default:
                    SetStealthText(HiddenText);
                    ApplyHiddenStealthColors();
                    break;
            }

            LayoutStealthSeparatorsAroundText();
        }


        private void RefreshNpcHpPanel()
        {
            if (npcHpPanel == null)
                return;

            if (TryAcquireCrosshairNpcHpTarget(out NPCState crosshairTarget))
                ShowNpcHpTarget(crosshairTarget);

            if (trackedNpcHpTarget == null || Time.time > npcHpPanelVisibleUntil)
            {
                trackedNpcHpTarget = null;
                SetNpcHpPanelActive(false);
                return;
            }

            RefreshNpcHpTargetDisplay(trackedNpcHpTarget);
        }


        private void HandlePlayerDamagedNpc(NPCState targetNpc)
        {
            ShowNpcHpTarget(targetNpc);
        }


        private void ShowNpcHpTarget(NPCState targetNpc)
        {
            if (targetNpc == null)
                return;

            trackedNpcHpTarget = targetNpc;
            npcHpPanelVisibleUntil = Time.time + Mathf.Max(0.1f, npcHpPanelHoldSeconds);
            RefreshNpcHpTargetDisplay(targetNpc);
            SetNpcHpPanelActive(true);
        }


        private void RefreshNpcHpTargetDisplay(NPCState targetNpc)
        {
            if (targetNpc == null)
                return;

            if (npcHpNameText != null)
                npcHpNameText.text = ResolveNpcDisplayName(targetNpc);

            if (npcHpBar != null)
            {
                float currentHp = Mathf.Max(0f, targetNpc.GetHealthPoints());
                float maxHp = Mathf.Max(MinMaxValue, targetNpc.GetMaxHealthPoints());
                npcHpBar.fillAmount = Mathf.Clamp01(currentHp / maxHp);
            }
        }


        private void SetNpcHpPanelActive(bool active)
        {
            if (npcHpPanel != null && npcHpPanel.activeSelf != active)
                npcHpPanel.SetActive(active);
        }


        private bool TryAcquireCrosshairNpcHpTarget(out NPCState targetNpc)
        {
            targetNpc = null;

            if (playerState == null || !playerState.GetCombatMode())
                return false;

            Camera camera = Camera.main;
            if (camera == null)
                return false;

            Vector2 screenPoint = playerCombat != null
                ? playerCombat.GetCurrentCrosshairScreenPoint()
                : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);

            Ray ray = camera.ScreenPointToRay(screenPoint);
            int hitCount = Physics.RaycastNonAlloc(
                ray,
                npcHpTargetHits,
                Mathf.Max(MinRayDistance, npcHpTargetRayDistance),
                npcHpTargetLayers,
                npcHpTargetTriggerInteraction);

            if (hitCount <= 0)
                return false;

            Collider nearestCollider = null;
            float nearestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hitCollider = npcHpTargetHits[i].collider;
                if (hitCollider == null)
                    continue;

                Transform hitTransform = hitCollider.transform;
                if (playerState != null && hitTransform != null && hitTransform.IsChildOf(playerState.transform))
                    continue;

                float hitDistance = npcHpTargetHits[i].distance;
                if (hitDistance >= nearestDistance)
                    continue;

                nearestCollider = hitCollider;
                nearestDistance = hitDistance;
            }

            if (nearestCollider == null || !TryGetNpcState(nearestCollider, out NPCState npcState))
                return false;

            if (!IsValidCrosshairNpcHpTarget(npcState, nearestCollider))
                return false;

            targetNpc = npcState;
            return true;
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


        private static bool IsValidCrosshairNpcHpTarget(NPCState npcState, Collider hitCollider)
        {
            if (npcState == null || npcState.IsDead())
                return false;

            NPCCombat npcCombat = GetNpcCombat(npcState, hitCollider);
            return npcCombat != null && npcCombat.IsAggroedOrSearchingForPlayer();
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


        private static string ResolveNpcDisplayName(NPCState targetNpc)
        {
            if (targetNpc == null)
                return "NPC";

            NPC npc = targetNpc.GetComponentInParent<NPC>();
            if (npc == null)
                npc = targetNpc.GetComponentInChildren<NPC>(true);

            string npcName = npc != null ? npc.GetNPCName() : targetNpc.GetNPCName();
            return string.IsNullOrWhiteSpace(npcName) ? "NPC" : npcName.Trim();
        }


        private void SetStealthText(string value)
        {
            if (stealthText != null && stealthText.text != value)
                stealthText.text = value;
        }


        private void ApplyDangerStealthFlash()
        {
            float alpha = Mathf.Lerp(dangerFlashMinAlpha, 1f, Mathf.PingPong(Time.unscaledTime * dangerFlashSpeed, 1f));
            Color flashColor = alertStealthColor;
            flashColor.a *= alpha;
            ApplyStealthElementColor(flashColor);
        }


        private void ApplyStealthElementColor(Color color)
        {
            if (stealthText != null)
                stealthText.color = color;

            if (leftStealthSeparator != null)
                leftStealthSeparator.color = color;

            if (rightStealthSeparator != null)
                rightStealthSeparator.color = color;
        }


        private void ApplyHiddenStealthColors()
        {
            if (!useOriginalHiddenStealthColors)
            {
                ApplyStealthElementColor(hiddenStealthColor);
                return;
            }

            if (stealthText != null)
                stealthText.color = hasDefaultStealthTextColor ? defaultStealthTextColor : hiddenStealthColor;

            if (leftStealthSeparator != null)
                leftStealthSeparator.color = hasDefaultLeftStealthSeparatorColor ? defaultLeftStealthSeparatorColor : hiddenStealthColor;

            if (rightStealthSeparator != null)
                rightStealthSeparator.color = hasDefaultRightStealthSeparatorColor ? defaultRightStealthSeparatorColor : hiddenStealthColor;
        }


        private void CacheDefaultStealthColors()
        {
            if (stealthText != null && !hasDefaultStealthTextColor)
            {
                defaultStealthTextColor = stealthText.color;
                hasDefaultStealthTextColor = true;
            }

            if (leftStealthSeparator != null && !hasDefaultLeftStealthSeparatorColor)
            {
                defaultLeftStealthSeparatorColor = leftStealthSeparator.color;
                hasDefaultLeftStealthSeparatorColor = true;
            }

            if (rightStealthSeparator != null && !hasDefaultRightStealthSeparatorColor)
            {
                defaultRightStealthSeparatorColor = rightStealthSeparator.color;
                hasDefaultRightStealthSeparatorColor = true;
            }
        }


        private void LayoutStealthSeparatorsAroundText()
        {
            if (stealthText == null || leftStealthSeparator == null || rightStealthSeparator == null)
                return;

            RectTransform textRect = stealthText.rectTransform;
            RectTransform leftRect = leftStealthSeparator.rectTransform;
            RectTransform rightRect = rightStealthSeparator.rectTransform;
            if (textRect == null || leftRect == null || rightRect == null)
                return;

            stealthText.ForceMeshUpdate();
            Bounds textBounds = stealthText.textBounds;
            Vector3 textCenter = textBounds.center;
            Vector3 leftEdgeWorld = textRect.TransformPoint(new Vector3(textBounds.min.x, textCenter.y, 0f));
            Vector3 rightEdgeWorld = textRect.TransformPoint(new Vector3(textBounds.max.x, textCenter.y, 0f));

            LayoutStealthSeparator(leftRect, leftEdgeWorld, true);
            LayoutStealthSeparator(rightRect, rightEdgeWorld, false);
        }


        private void LayoutStealthSeparator(RectTransform separatorRect, Vector3 innerEdgeWorldPosition, bool isLeftSeparator)
        {
            separatorRect.pivot = isLeftSeparator ? new Vector2(1f, 0.5f) : new Vector2(0f, 0.5f);

            RectTransform parentRect = separatorRect.parent as RectTransform;
            if (parentRect == null)
                return;

            Vector3 localInnerEdgePosition = parentRect.InverseTransformPoint(innerEdgeWorldPosition);
            localInnerEdgePosition.x += isLeftSeparator ? -stealthSeparatorGapPixels : stealthSeparatorGapPixels;
            localInnerEdgePosition.z = separatorRect.localPosition.z;
            separatorRect.localPosition = localInnerEdgePosition;
        }


        private bool TryGetEquippedWeaponConditionPercent(out float conditionPercent)
        {
            conditionPercent = 0.0f;

            if (playerWeaponController == null || playerInventory == null)
                return false;

            string equippedInstanceId = playerWeaponController.GetEquippedInventoryWeaponInstanceId();
            if (string.IsNullOrWhiteSpace(equippedInstanceId))
                return false;

            var weaponEntries = playerInventory.GetCategoryItems(PlayerInventory.InventoryCategory.Weapons);
            if (weaponEntries == null)
                return false;

            for (int entryIndex = 0; entryIndex < weaponEntries.Count; entryIndex++)
            {
                PlayerInventory.InventoryEntry entry = weaponEntries[entryIndex];
                if (entry == null)
                    continue;

                var itemInstances = entry.GetItemInstances();
                if (itemInstances == null)
                    continue;

                for (int instanceIndex = 0; instanceIndex < itemInstances.Count; instanceIndex++)
                {
                    PlayerInventory.ItemInstanceData instance = itemInstances[instanceIndex];
                    if (instance == null)
                        continue;

                    if (!string.Equals(instance.GetInstanceId(), equippedInstanceId, StringComparison.Ordinal))
                        continue;

                    conditionPercent = Mathf.Clamp(playerInventory.GetInstanceConditionPercent(entry, instanceIndex), 0.0f, 100.0f);
                    return true;
                }
            }

            return false;
        }


        private static string ConvertYawToCompassHeading(float yaw)
        {
            yaw = Mathf.Repeat(yaw, 360.0f);
            int directionIndex = Mathf.RoundToInt(yaw / 45.0f) % 8;

            return directionIndex switch
            {
                0 => "N",
                1 => "NE",
                2 => "E",
                3 => "SE",
                4 => "S",
                5 => "SW",
                6 => "W",
                _ => "NW"
            };
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


        private static Transform FindChildByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
                return null;

            Transform[] children = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                Transform child = children[i];
                if (child != null && child.name == childName)
                    return child;
            }

            return null;
        }


        private static T FindChildComponentByName<T>(Transform root, string childName) where T : Component
        {
            Transform child = FindChildByName(root, childName);
            return child != null ? child.GetComponent<T>() : null;
        }


        private static Graphic FindFirstGraphic(Transform root, params string[] names)
        {
            if (root == null || names == null)
                return null;

            for (int i = 0; i < names.Length; i++)
            {
                Transform child = FindChildByName(root, names[i]);
                if (child == null)
                    continue;

                Graphic graphic = child.GetComponent<Graphic>();
                if (graphic != null)
                    return graphic;
            }

            return null;
        }


        private static Graphic FindFirstGraphicByTokens(Transform root, string sideToken, params string[] separatorTokens)
        {
            if (root == null || string.IsNullOrWhiteSpace(sideToken) || separatorTokens == null)
                return null;

            string normalizedSideToken = sideToken.ToLowerInvariant();
            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null)
                    continue;

                string objectName = graphic.name.ToLowerInvariant();
                if (!objectName.Contains(normalizedSideToken))
                    continue;

                for (int j = 0; j < separatorTokens.Length; j++)
                {
                    string separatorToken = separatorTokens[j];
                    if (!string.IsNullOrWhiteSpace(separatorToken) && objectName.Contains(separatorToken.ToLowerInvariant()))
                        return graphic;
                }
            }

            return null;
        }
    }
}
