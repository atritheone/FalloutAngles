using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UI
{
    public class ExperienceUIController : MonoBehaviour
    {
        private const string ExperienceRootName = "ExperienceUI";
        private const string FirstLevelTextName = "FirstLvlText";
        private const string SecondLevelTextName = "SecondLvlText";
        private const string ExperienceTextName = "XPText";
        private const string ProgressArrowName = "ProgressArrow";
        private const string LeftSeparatorName = "XPInnerSeparator";
        private const string RightSeparatorName = "XPInnerSeparator (7)";
        private const string LevelUpRootName = "LevelUpUI";
        private const string LevelUpText = "LEVEL UP";
        private const float DefaultSkillExperienceGroupWindow = 0.25f;

        [Header("References")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TMP_Text firstLevelText;
        [SerializeField] private TMP_Text secondLevelText;
        [SerializeField] private TMP_Text experienceText;
        [SerializeField] private RectTransform progressArrow;
        [SerializeField] private RectTransform leftSeparator;
        [SerializeField] private RectTransform rightSeparator;
        [SerializeField] private GameObject levelUpRoot;

        [Header("Timing")]
        [SerializeField, Min(0f)] private float fadeInDuration = 0.25f;
        [SerializeField, Min(0f)] private float fullBarMoveDuration = 1.0f;
        [SerializeField, Min(0f)] private float minimumMoveDuration = 0.12f;
        [SerializeField, Min(0f)] private float levelTransitionPause = 0.12f;
        [SerializeField, Min(0f)] private float completedHoldDuration = 1.0f;
        [SerializeField, Min(0f)] private float fadeOutDuration = 0.35f;
        [SerializeField, Min(0f)] private float skillExperienceGroupWindow = 0.25f;
        [SerializeField] private bool useUnscaledTime;

        private readonly Queue<ExperienceAnimationRequest> queuedRequests = new Queue<ExperienceAnimationRequest>();
        private Coroutine activeRoutine;
        private bool hasPendingLevelUpSelections;

        public static ExperienceUIController FindOrCreate()
        {
            ExperienceUIController existingController = FindExistingRootController();
            if (existingController != null)
                return existingController;

            Transform experienceRoot = FindTransformByName(ExperienceRootName);
            if (experienceRoot == null)
                return null;

            bool rootWasActive = experienceRoot.gameObject.activeSelf;
            SetHierarchyActive(experienceRoot, true);

            ExperienceUIController controller = experienceRoot.gameObject.AddComponent<ExperienceUIController>();

            if (!rootWasActive)
                experienceRoot.gameObject.SetActive(false);

            return controller;
        }

        public static bool QueueExperienceChange(PlayerState sourcePlayerState, PlayerState.ExperienceChange change)
        {
            if (sourcePlayerState == null || change.Amount <= 0)
                return false;

            ExperienceUIController controller = FindOrCreateActiveController();
            if (controller == null)
                return false;

            controller.EnqueueExperienceChange(sourcePlayerState, change);
            return true;
        }

        private void Awake()
        {
            ResolveReferences();
            ConfigureHiddenState();
        }

        private void OnValidate()
        {
            fadeInDuration = Mathf.Max(0f, fadeInDuration);
            fullBarMoveDuration = Mathf.Max(0f, fullBarMoveDuration);
            minimumMoveDuration = Mathf.Max(0f, minimumMoveDuration);
            levelTransitionPause = Mathf.Max(0f, levelTransitionPause);
            completedHoldDuration = Mathf.Max(0f, completedHoldDuration);
            fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
            if (skillExperienceGroupWindow <= 0f)
                skillExperienceGroupWindow = DefaultSkillExperienceGroupWindow;
        }

        private void EnqueueExperienceChange(PlayerState sourcePlayerState, PlayerState.ExperienceChange change)
        {
            if (sourcePlayerState == null || change.Amount <= 0)
                return;

            queuedRequests.Enqueue(new ExperienceAnimationRequest(sourcePlayerState, change));

            if (activeRoutine == null && isActiveAndEnabled)
                activeRoutine = StartCoroutine(PlayQueuedRequests());
        }

        private IEnumerator PlayQueuedRequests()
        {
            while (queuedRequests.Count > 0 || hasPendingLevelUpSelections)
            {
                while (queuedRequests.Count > 0)
                {
                    ExperienceAnimationRequest request = queuedRequests.Dequeue();
                    yield return WaitForSkillExperienceGroupWindow(request);
                    request = DequeueGroupedSkillExperienceRequests(request);
                    yield return PlayRequest(request);
                }

                if (hasPendingLevelUpSelections)
                {
                    PrepareExperienceRootForLevelUpUi();
                    yield return LevelUpUIController.PlayQueuedLevelUpsWhenReady();
                    RestoreExperienceRootAfterLevelUpUi();
                }
            }

            activeRoutine = null;
            SetExperienceHierarchyActive(false);
        }

        private IEnumerator WaitForSkillExperienceGroupWindow(ExperienceAnimationRequest request)
        {
            if (!request.IsSkillLevelExperience)
                yield break;

            yield return Wait(GetSkillExperienceGroupWindow());
        }

        private ExperienceAnimationRequest DequeueGroupedSkillExperienceRequests(ExperienceAnimationRequest request)
        {
            if (!request.IsSkillLevelExperience)
                return request;

            while (queuedRequests.Count > 0)
            {
                ExperienceAnimationRequest nextRequest = queuedRequests.Peek();
                if (!CanGroupSkillExperienceRequests(request, nextRequest))
                    break;

                queuedRequests.Dequeue();
                request = request.CombineWith(nextRequest);
            }

            return request;
        }

        private bool CanGroupSkillExperienceRequests(
            ExperienceAnimationRequest currentRequest,
            ExperienceAnimationRequest nextRequest)
        {
            if (!currentRequest.IsSkillLevelExperience || !nextRequest.IsSkillLevelExperience)
                return false;

            if (currentRequest.SourcePlayerState != nextRequest.SourcePlayerState)
                return false;

            if (currentRequest.SkillExperienceSource != nextRequest.SkillExperienceSource)
                return false;

            return nextRequest.QueuedTime - currentRequest.QueuedTime <= GetSkillExperienceGroupWindow();
        }

        private float GetSkillExperienceGroupWindow()
        {
            return skillExperienceGroupWindow > 0f
                ? skillExperienceGroupWindow
                : DefaultSkillExperienceGroupWindow;
        }

        private IEnumerator PlayRequest(ExperienceAnimationRequest request)
        {
            SetExperienceHierarchyActive(true);
            ResolveReferences();

            if (!HasRequiredReferences())
                yield break;

            EnsureVisibleTransformScale();
            SetCanvasAlpha(0f);
            SetCanvasRaycasts(false);

            SetExperienceText("+" + request.Amount + "XP");
            SetLevelTexts(request.PreviousLevel);

            int displayLevel = request.PreviousLevel;
            int previousExperienceToNextLevel = ResolveExperienceToNextLevel(
                request,
                request.PreviousLevel,
                request.PreviousExperienceToNextLevel);
            float segmentStartPercent = GetProgressPercent(request.PreviousExperience, previousExperienceToNextLevel);
            SetArrowPercent(segmentStartPercent);

            yield return FadeCanvas(0f, 1f, fadeInDuration);

            bool showedLevelUpText = false;
            while (displayLevel < request.CurrentLevel)
            {
                yield return MoveArrow(segmentStartPercent, 1f);

                displayLevel++;
                SetLevelTexts(displayLevel);
                SetArrowPercent(0f);
                segmentStartPercent = 0f;

                if (!showedLevelUpText)
                {
                    SetExperienceText(LevelUpText);
                    showedLevelUpText = true;
                }

                yield return Wait(levelTransitionPause);
            }

            int currentExperienceToNextLevel = ResolveExperienceToNextLevel(
                request,
                request.CurrentLevel,
                request.CurrentExperienceToNextLevel);
            float targetPercent = GetProgressPercent(request.CurrentExperience, currentExperienceToNextLevel);
            yield return MoveArrow(segmentStartPercent, targetPercent);
            yield return Wait(completedHoldDuration);
            yield return FadeCanvas(1f, 0f, fadeOutDuration);
            SetCanvasRaycasts(false);
            QueueLevelUpSelectionsFromRequest(request);
        }

        private static void QueueLevelUpSelections(ExperienceAnimationRequest request)
        {
            if (request.SourcePlayerState == null || request.CurrentLevel <= request.PreviousLevel)
                return;

            for (int level = request.PreviousLevel + 1; level <= request.CurrentLevel; level++)
                LevelUpUIController.QueueLevelUp(request.SourcePlayerState, level);
        }

        private void QueueLevelUpSelectionsFromRequest(ExperienceAnimationRequest request)
        {
            if (request.SourcePlayerState == null || request.CurrentLevel <= request.PreviousLevel)
                return;

            hasPendingLevelUpSelections = true;
            QueueLevelUpSelections(request);
        }

        private IEnumerator MoveArrow(float fromPercent, float toPercent)
        {
            fromPercent = Mathf.Clamp01(fromPercent);
            toPercent = Mathf.Clamp01(toPercent);

            float distance = Mathf.Abs(toPercent - fromPercent);
            if (distance <= 0.0001f || fullBarMoveDuration <= 0f)
            {
                SetArrowPercent(toPercent);
                yield break;
            }

            float duration = Mathf.Max(minimumMoveDuration, distance * fullBarMoveDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += GetDeltaTime();
                float percent = Mathf.Clamp01(elapsed / duration);
                SetArrowPercent(Mathf.Lerp(fromPercent, toPercent, percent));
                yield return null;
            }

            SetArrowPercent(toPercent);
        }

        private IEnumerator FadeCanvas(float fromAlpha, float toAlpha, float duration)
        {
            if (duration <= 0f)
            {
                SetCanvasAlpha(toAlpha);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += GetDeltaTime();
                float percent = Mathf.Clamp01(elapsed / duration);
                SetCanvasAlpha(Mathf.Lerp(fromAlpha, toAlpha, percent));
                yield return null;
            }

            SetCanvasAlpha(toAlpha);
        }

        private IEnumerator Wait(float duration)
        {
            if (duration <= 0f)
                yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += GetDeltaTime();
                yield return null;
            }
        }

        private void SetArrowPercent(float percent)
        {
            if (progressArrow == null || leftSeparator == null || rightSeparator == null)
                return;

            float leftX = GetLocalXInArrowParent(leftSeparator);
            float rightX = GetLocalXInArrowParent(rightSeparator);
            float targetX = Mathf.Lerp(leftX, rightX, Mathf.Clamp01(percent));

            Vector2 anchoredPosition = progressArrow.anchoredPosition;
            anchoredPosition.x = targetX;
            progressArrow.anchoredPosition = anchoredPosition;
        }

        private float GetLocalXInArrowParent(RectTransform target)
        {
            if (target == null)
                return 0f;

            if (progressArrow != null && target.parent == progressArrow.parent)
                return target.anchoredPosition.x;

            RectTransform arrowParent = progressArrow != null ? progressArrow.parent as RectTransform : null;
            return arrowParent != null
                ? arrowParent.InverseTransformPoint(target.position).x
                : target.position.x;
        }

        private void SetLevelTexts(int level)
        {
            int currentLevel = Mathf.Clamp(level, 1, PlayerState.MaxPlayerLevel);
            int nextLevel = currentLevel < PlayerState.MaxPlayerLevel ? currentLevel + 1 : currentLevel;

            if (firstLevelText != null)
                firstLevelText.text = currentLevel.ToString();

            if (secondLevelText != null)
                secondLevelText.text = nextLevel.ToString();
        }

        private void SetExperienceText(string value)
        {
            if (experienceText != null)
                experienceText.text = value;
        }

        private static float GetProgressPercent(int experience, int experienceToNextLevel)
        {
            if (experienceToNextLevel <= 0)
                return 0f;

            return Mathf.Clamp01((float)Mathf.Max(0, experience) / experienceToNextLevel);
        }

        private static int ResolveExperienceToNextLevel(
            ExperienceAnimationRequest request,
            int level,
            int reportedExperienceToNextLevel)
        {
            if (reportedExperienceToNextLevel > 0 || level >= PlayerState.MaxPlayerLevel)
                return reportedExperienceToNextLevel;

            return request.SourcePlayerState != null
                ? request.SourcePlayerState.GetExperienceToNextLevelForLevel(level)
                : reportedExperienceToNextLevel;
        }

        private void SetCanvasAlpha(float alpha)
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            canvasGroup.alpha = Mathf.Clamp01(alpha);
        }

        private void SetCanvasRaycasts(bool enabled)
        {
            if (canvasGroup == null)
                return;

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = enabled;
        }

        private void ResolveReferences()
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            if (firstLevelText == null)
                firstLevelText = FindChildComponentByName<TMP_Text>(transform, FirstLevelTextName);

            if (secondLevelText == null)
                secondLevelText = FindChildComponentByName<TMP_Text>(transform, SecondLevelTextName);

            if (experienceText == null)
                experienceText = FindChildComponentByName<TMP_Text>(transform, ExperienceTextName);

            if (progressArrow == null)
                progressArrow = FindChildComponentByName<RectTransform>(transform, ProgressArrowName);

            if (leftSeparator == null)
                leftSeparator = FindChildComponentByName<RectTransform>(transform, LeftSeparatorName);

            if (rightSeparator == null)
                rightSeparator = FindChildComponentByName<RectTransform>(transform, RightSeparatorName);

            if (levelUpRoot == null)
            {
                Transform levelUpTransform = FindChildTransformByName(transform, LevelUpRootName);
                levelUpRoot = levelUpTransform ? levelUpTransform.gameObject : null;
            }
        }

        private bool HasRequiredReferences()
        {
            return canvasGroup != null
                   && firstLevelText != null
                   && secondLevelText != null
                   && experienceText != null
                   && progressArrow != null
                   && leftSeparator != null
                   && rightSeparator != null;
        }

        private void ConfigureHiddenState()
        {
            SetCanvasAlpha(0f);
            SetCanvasRaycasts(false);
        }

        private void EnsureVisibleTransformScale()
        {
            if (transform.localScale.sqrMagnitude <= 0.0001f)
                transform.localScale = Vector3.one;
        }

        private void SetExperienceHierarchyActive(bool active)
        {
            if (active)
            {
                SetHierarchyActive(transform, true);
                return;
            }

            if (gameObject.activeSelf)
                gameObject.SetActive(false);
        }

        private void PrepareExperienceRootForLevelUpUi()
        {
            if (!hasPendingLevelUpSelections)
                return;

            ResolveReferences();
            SetCanvasAlpha(1f);
            SetRootCanvasInput(true);
            SetNonLevelUpChildrenActive(false);
        }

        private void RestoreExperienceRootAfterLevelUpUi()
        {
            if (!hasPendingLevelUpSelections)
                return;

            SetNonLevelUpChildrenActive(true);
            SetCanvasAlpha(0f);
            SetCanvasRaycasts(false);
            hasPendingLevelUpSelections = false;
        }

        private void SetRootCanvasInput(bool enabled)
        {
            if (canvasGroup == null)
                canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
                canvasGroup = gameObject.AddComponent<CanvasGroup>();

            canvasGroup.interactable = enabled;
            canvasGroup.blocksRaycasts = enabled;
        }

        private void SetNonLevelUpChildrenActive(bool active)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                Transform child = transform.GetChild(i);
                if (!child)
                    continue;

                if (levelUpRoot && child == levelUpRoot.transform)
                    continue;

                if (child.gameObject.activeSelf != active)
                    child.gameObject.SetActive(active);
            }
        }

        private float GetDeltaTime()
        {
            return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        }

        private static T FindChildComponentByName<T>(Transform root, string childName) where T : Component
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
                return null;

            T[] components = root.GetComponentsInChildren<T>(true);
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] != null && components[i].name == childName)
                    return components[i];
            }

            return null;
        }

        private static Transform FindTransformByName(string objectName)
        {
            Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == objectName)
                    return transforms[i];
            }

            return null;
        }

        private static Transform FindChildTransformByName(Transform root, string childName)
        {
            if (root == null || string.IsNullOrWhiteSpace(childName))
                return null;

            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (transforms[i] != null && transforms[i].name == childName)
                    return transforms[i];
            }

            return null;
        }

        private static ExperienceUIController FindExistingRootController()
        {
            Transform experienceRoot = FindTransformByName(ExperienceRootName);
            if (experienceRoot == null)
                return null;

            return experienceRoot.GetComponent<ExperienceUIController>();
        }

        private static ExperienceUIController FindOrCreateActiveController()
        {
            Transform experienceRoot = FindTransformByName(ExperienceRootName);
            if (experienceRoot == null)
                return null;

            SetHierarchyActive(experienceRoot, true);

            ExperienceUIController controller = experienceRoot.GetComponent<ExperienceUIController>();
            if (controller == null)
                controller = experienceRoot.gameObject.AddComponent<ExperienceUIController>();

            controller.ResolveReferences();
            return controller;
        }

        private static void SetHierarchyActive(Transform root, bool active)
        {
            if (root == null)
                return;

            if (!active)
            {
                root.gameObject.SetActive(false);
                return;
            }

            Transform current = root;
            while (current != null)
            {
                GameObject currentObject = current.gameObject;
                if (!currentObject.activeSelf)
                    currentObject.SetActive(true);

                current = current.parent;
            }
        }

        private readonly struct ExperienceAnimationRequest
        {
            public readonly PlayerState SourcePlayerState;
            public readonly int Amount;
            public readonly int PreviousLevel;
            public readonly int PreviousExperience;
            public readonly int PreviousExperienceToNextLevel;
            public readonly int CurrentLevel;
            public readonly int CurrentExperience;
            public readonly int CurrentExperienceToNextLevel;
            public readonly PlayerSkill SkillExperienceSource;
            public readonly float QueuedTime;

            public ExperienceAnimationRequest(PlayerState sourcePlayerState, PlayerState.ExperienceChange change)
            {
                SourcePlayerState = sourcePlayerState;
                Amount = change.Amount;
                PreviousLevel = change.PreviousLevel;
                PreviousExperience = change.PreviousExperience;
                PreviousExperienceToNextLevel = change.PreviousExperienceToNextLevel;
                CurrentLevel = change.CurrentLevel;
                CurrentExperience = change.CurrentExperience;
                CurrentExperienceToNextLevel = change.CurrentExperienceToNextLevel;
                SkillExperienceSource = change.SkillExperienceSource;
                QueuedTime = Time.unscaledTime;
            }

            private ExperienceAnimationRequest(
                PlayerState sourcePlayerState,
                int amount,
                int previousLevel,
                int previousExperience,
                int previousExperienceToNextLevel,
                int currentLevel,
                int currentExperience,
                int currentExperienceToNextLevel,
                PlayerSkill skillExperienceSource,
                float queuedTime)
            {
                SourcePlayerState = sourcePlayerState;
                Amount = amount;
                PreviousLevel = previousLevel;
                PreviousExperience = previousExperience;
                PreviousExperienceToNextLevel = previousExperienceToNextLevel;
                CurrentLevel = currentLevel;
                CurrentExperience = currentExperience;
                CurrentExperienceToNextLevel = currentExperienceToNextLevel;
                SkillExperienceSource = skillExperienceSource;
                QueuedTime = queuedTime;
            }

            public bool IsSkillLevelExperience
            {
                get { return SkillExperienceSource != PlayerSkill.None; }
            }

            public ExperienceAnimationRequest CombineWith(ExperienceAnimationRequest nextRequest)
            {
                return new ExperienceAnimationRequest(
                    SourcePlayerState,
                    Amount + nextRequest.Amount,
                    PreviousLevel,
                    PreviousExperience,
                    PreviousExperienceToNextLevel,
                    nextRequest.CurrentLevel,
                    nextRequest.CurrentExperience,
                    nextRequest.CurrentExperienceToNextLevel,
                    SkillExperienceSource,
                    nextRequest.QueuedTime);
            }
        }
    }
}
