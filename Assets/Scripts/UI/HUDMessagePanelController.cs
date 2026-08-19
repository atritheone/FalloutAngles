using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class HUDMessagePanelController : MonoBehaviour
    {
        private const string MessageTextName = "MessageText";
        private const string TopLineName = "TopLine";
        private const string TopBarName = "TopBar";
        private const string SideLineName = "SideLine";
        private const string SideBarName = "SideBar";

        private static HUDMessagePanelController instance;

        [Header("References")]
        [SerializeField] private TMP_Text messageText;
        [SerializeField] private Image topLineImage;
        [SerializeField] private Image sideLineImage;
        [SerializeField] private CanvasGroup panelCanvasGroup;

        [Header("Timing")]
        [SerializeField] private float fadeInDuration = 0.25f;
        [SerializeField] private float visibleDuration = 3.0f;
        [SerializeField] private float fadeOutDuration = 0.25f;
        [SerializeField] private bool useUnscaledTime;

        [Header("State")]
        [SerializeField] private bool clearTextWhenHidden = true;
        [SerializeField] private bool disableRaycastsOnControlledGraphics = true;
        [SerializeField] private bool forceMessageTextLeftAlignment = true;

        private readonly Queue<MessageRequest> queuedMessages = new Queue<MessageRequest>();
        private Coroutine activeRoutine;
        private string activeMessage = string.Empty;
        private float activeVisibleSeconds;
        private float activeFadeOutSeconds;
        private float activeVisibleDeadline;
        private int activeMessageResetVersion;

        public static bool Show(string message)
        {
            return TryResolveInstance(out HUDMessagePanelController controller)
                   && controller.DisplayMessage(message);
        }

        public static bool Show(string message, float visibleSeconds)
        {
            return TryResolveInstance(out HUDMessagePanelController controller)
                   && controller.DisplayMessage(message, visibleSeconds);
        }

        public static bool Show(string message, float visibleSeconds, float fadeSeconds)
        {
            return TryResolveInstance(out HUDMessagePanelController controller)
                   && controller.DisplayMessage(message, visibleSeconds, fadeSeconds);
        }

        public static bool Show(string message, float visibleSeconds, float fadeInSeconds, float fadeOutSeconds)
        {
            return TryResolveInstance(out HUDMessagePanelController controller)
                   && controller.DisplayMessage(message, visibleSeconds, fadeInSeconds, fadeOutSeconds);
        }

        public static bool Queue(string message)
        {
            return TryResolveInstance(out HUDMessagePanelController controller)
                   && controller.QueueMessage(message);
        }

        public static bool Queue(string message, float visibleSeconds)
        {
            return TryResolveInstance(out HUDMessagePanelController controller)
                   && controller.QueueMessage(message, visibleSeconds);
        }

        public static bool Queue(string message, float visibleSeconds, float fadeSeconds)
        {
            return TryResolveInstance(out HUDMessagePanelController controller)
                   && controller.QueueMessage(message, visibleSeconds, fadeSeconds);
        }

        public static bool Queue(string message, float visibleSeconds, float fadeInSeconds, float fadeOutSeconds)
        {
            return TryResolveInstance(out HUDMessagePanelController controller)
                   && controller.QueueMessage(message, visibleSeconds, fadeInSeconds, fadeOutSeconds);
        }

        public bool DisplayMessage(string message)
        {
            return DisplayMessage(message, visibleDuration, fadeInDuration, fadeOutDuration);
        }

        public bool DisplayMessage(string message, float visibleSeconds)
        {
            return DisplayMessage(message, visibleSeconds, fadeInDuration, fadeOutDuration);
        }

        public bool DisplayMessage(string message, float visibleSeconds, float fadeSeconds)
        {
            return DisplayMessage(message, visibleSeconds, fadeSeconds, fadeSeconds);
        }

        public bool DisplayMessage(string message, float visibleSeconds, float fadeInSeconds, float fadeOutSeconds)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            MessageRequest request = new MessageRequest(message, visibleSeconds, fadeInSeconds, fadeOutSeconds);
            queuedMessages.Clear();

            if (IsActiveMessage(request))
                ResetActiveMessageHold(request);
            else
                StartMessage(request);

            return true;
        }

        public bool QueueMessage(string message)
        {
            return QueueMessage(message, visibleDuration, fadeInDuration, fadeOutDuration);
        }

        public bool QueueMessage(string message, float visibleSeconds)
        {
            return QueueMessage(message, visibleSeconds, fadeInDuration, fadeOutDuration);
        }

        public bool QueueMessage(string message, float visibleSeconds, float fadeSeconds)
        {
            return QueueMessage(message, visibleSeconds, fadeSeconds, fadeSeconds);
        }

        public bool QueueMessage(string message, float visibleSeconds, float fadeInSeconds, float fadeOutSeconds)
        {
            if (string.IsNullOrWhiteSpace(message))
                return false;

            MessageRequest request = new MessageRequest(message, visibleSeconds, fadeInSeconds, fadeOutSeconds);

            if (IsActiveMessage(request))
                ResetActiveMessageHold(request);
            else if (activeRoutine == null)
                StartMessage(request);
            else
                queuedMessages.Enqueue(request);

            return true;
        }

        public void HideImmediately()
        {
            queuedMessages.Clear();

            if (activeRoutine != null)
            {
                StopCoroutine(activeRoutine);
                activeRoutine = null;
            }

            SetPanelAlpha(0.0f);
            ClearActiveMessageState();

            if (clearTextWhenHidden && messageText != null)
                messageText.text = string.Empty;
        }

        private static bool TryResolveInstance(out HUDMessagePanelController controller)
        {
            if (instance == null)
                instance = FindAnyObjectByType<HUDMessagePanelController>(FindObjectsInactive.Include);

            controller = instance;

            if (controller != null)
                return true;

            Debug.LogWarning("No HUDMessagePanelController was found in the scene.");
            return false;
        }

        private void Awake()
        {
            instance = this;
            ResolveReferences();
            ConfigureMessageText();
            ConfigureControlledGraphics();
            ClearHiddenStartupState();
        }

        private void Start()
        {
            if (activeRoutine == null)
                SetPanelAlpha(0.0f);
        }

        private void OnDestroy()
        {
            if (instance == this)
                instance = null;
        }

        private void OnValidate()
        {
            fadeInDuration = Mathf.Max(0.0f, fadeInDuration);
            visibleDuration = Mathf.Max(0.0f, visibleDuration);
            fadeOutDuration = Mathf.Max(0.0f, fadeOutDuration);
        }

        private void ClearHiddenStartupState()
        {
            queuedMessages.Clear();
            activeRoutine = null;
            ClearActiveMessageState();

            if (clearTextWhenHidden && messageText != null)
                messageText.text = string.Empty;
        }

        private void StartMessage(MessageRequest request)
        {
            ResolveReferences();
            ConfigureMessageText();

            if (activeRoutine != null)
                StopCoroutine(activeRoutine);

            activeMessageResetVersion = 0;
            activeRoutine = StartCoroutine(DisplayRoutine(request));
        }

        private IEnumerator DisplayRoutine(MessageRequest request)
        {
            activeMessage = request.Message;
            activeVisibleSeconds = request.VisibleSeconds;
            activeFadeOutSeconds = request.FadeOutSeconds;
            activeVisibleDeadline = 0.0f;

            if (messageText != null)
                messageText.text = request.Message;

            yield return FadePanel(0.0f, 1.0f, request.FadeInSeconds);

            while (true)
            {
                ResetActiveVisibleDeadline();
                yield return WaitForActiveMessageHold();

                int resetVersion = activeMessageResetVersion;
                yield return FadePanelUnlessActiveMessageReset(1.0f, 0.0f, activeFadeOutSeconds, resetVersion);

                if (resetVersion == activeMessageResetVersion)
                    break;

                SetPanelAlpha(1.0f);
            }

            if (clearTextWhenHidden && messageText != null)
                messageText.text = string.Empty;

            activeRoutine = null;
            ClearActiveMessageState();
            PlayNextQueuedMessage();
        }

        private void PlayNextQueuedMessage()
        {
            if (queuedMessages.Count == 0)
                return;

            StartMessage(queuedMessages.Dequeue());
        }

        private IEnumerator FadePanel(float fromAlpha, float toAlpha, float duration)
        {
            if (duration <= 0.0f)
            {
                SetPanelAlpha(toAlpha);
                yield break;
            }

            float elapsed = 0.0f;

            while (elapsed < duration)
            {
                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float percent = Mathf.Clamp01(elapsed / duration);
                SetPanelAlpha(Mathf.Lerp(fromAlpha, toAlpha, percent));
                yield return null;
            }

            SetPanelAlpha(toAlpha);
        }

        private IEnumerator WaitForActiveMessageHold()
        {
            while (GetCurrentTime() < activeVisibleDeadline)
                yield return null;
        }

        private IEnumerator FadePanelUnlessActiveMessageReset(float fromAlpha, float toAlpha, float duration, int resetVersion)
        {
            if (duration <= 0.0f)
            {
                if (resetVersion == activeMessageResetVersion)
                    SetPanelAlpha(toAlpha);

                yield break;
            }

            float elapsed = 0.0f;

            while (elapsed < duration)
            {
                if (resetVersion != activeMessageResetVersion)
                    yield break;

                elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
                float percent = Mathf.Clamp01(elapsed / duration);
                SetPanelAlpha(Mathf.Lerp(fromAlpha, toAlpha, percent));
                yield return null;
            }

            if (resetVersion == activeMessageResetVersion)
                SetPanelAlpha(toAlpha);
        }

        private bool IsActiveMessage(MessageRequest request)
        {
            return activeRoutine != null && activeMessage == request.Message;
        }

        private void ResetActiveMessageHold(MessageRequest request)
        {
            activeVisibleSeconds = request.VisibleSeconds;
            activeFadeOutSeconds = request.FadeOutSeconds;
            ResetActiveVisibleDeadline();
            activeMessageResetVersion++;
        }

        private void ResetActiveVisibleDeadline()
        {
            activeVisibleDeadline = GetCurrentTime() + activeVisibleSeconds;
        }

        private float GetCurrentTime()
        {
            return useUnscaledTime ? Time.unscaledTime : Time.time;
        }

        private void ClearActiveMessageState()
        {
            activeMessage = string.Empty;
            activeVisibleSeconds = 0.0f;
            activeFadeOutSeconds = 0.0f;
            activeVisibleDeadline = 0.0f;
            activeMessageResetVersion = 0;
        }

        private void SetPanelAlpha(float alpha)
        {
            ResolveReferences();

            float clampedAlpha = Mathf.Clamp01(alpha);

            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = clampedAlpha;
                panelCanvasGroup.interactable = false;
                panelCanvasGroup.blocksRaycasts = false;
            }
            else
            {
                SetGraphicAlpha(messageText, clampedAlpha);
                SetGraphicAlpha(topLineImage, clampedAlpha);
                SetGraphicAlpha(sideLineImage, clampedAlpha);
            }
        }

        private void ResolveReferences()
        {
            if (panelCanvasGroup == null)
                panelCanvasGroup = GetComponent<CanvasGroup>();

            if (panelCanvasGroup == null)
                panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (messageText == null)
                messageText = FindChildComponentByName<TMP_Text>(MessageTextName);

            if (topLineImage == null)
                topLineImage = FindChildComponentByName<Image>(TopLineName);

            if (topLineImage == null)
                topLineImage = FindChildComponentByName<Image>(TopBarName);

            if (sideLineImage == null)
                sideLineImage = FindChildComponentByName<Image>(SideLineName);

            if (sideLineImage == null)
                sideLineImage = FindChildComponentByName<Image>(SideBarName);
        }

        private void ConfigureMessageText()
        {
            if (messageText == null)
                return;

            if (forceMessageTextLeftAlignment)
                messageText.horizontalAlignment = HorizontalAlignmentOptions.Left;

            messageText.raycastTarget = false;
        }

        private void ConfigureControlledGraphics()
        {
            if (!disableRaycastsOnControlledGraphics)
                return;

            Graphic[] graphics = GetComponentsInChildren<Graphic>(true);

            for (int i = 0; i < graphics.Length; i++)
            {
                if (graphics[i] != null)
                    graphics[i].raycastTarget = false;
            }
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

        private static void SetGraphicAlpha(Graphic graphic, float alpha)
        {
            if (graphic == null)
                return;

            Color color = graphic.color;
            color.a = alpha;
            graphic.color = color;
        }

        private readonly struct MessageRequest
        {
            public readonly string Message;
            public readonly float VisibleSeconds;
            public readonly float FadeInSeconds;
            public readonly float FadeOutSeconds;

            public MessageRequest(string message, float visibleSeconds, float fadeInSeconds, float fadeOutSeconds)
            {
                Message = message.Trim();
                VisibleSeconds = Mathf.Max(0.0f, visibleSeconds);
                FadeInSeconds = Mathf.Max(0.0f, fadeInSeconds);
                FadeOutSeconds = Mathf.Max(0.0f, fadeOutSeconds);
            }
        }
    }
}
