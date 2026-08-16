using System.Collections;
using UnityEngine;

namespace EmergencyRoad
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(CanvasGroup))]
    public sealed class EmergencyPanelTransition : MonoBehaviour
    {
        public enum TransitionStyle
        {
            Fade,
            ScaleFade,
            SlideFade,
            ScaleSlideFade
        }

        [Header("PANEL REFERENCES - EDIT IN SCENE")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform animatedRoot;

        [Header("ANIMATION")]
        [SerializeField] private TransitionStyle style = TransitionStyle.ScaleFade;
        [SerializeField, Min(.05f)] private float duration = .26f;
        [SerializeField] private Vector2 hiddenOffset = new(48f, 0f);
        [SerializeField, Range(.75f, 1f)] private float hiddenScale = .92f;
        [SerializeField] private bool deactivateWhenHidden = true;

        private Coroutine animationRoutine;
        private Vector2 shownPosition;
        private Vector3 shownScale;
        private bool initialized;
        private bool targetVisible;

        public bool TargetVisible => targetVisible;

        private void Awake()
        {
            EnsureInitialized();
        }

        public void Show() => SetVisible(true);

        public void Hide() => SetVisible(false);

        public void ShowImmediate() => SetVisible(true, true);

        public void HideImmediate() => SetVisible(false, true);

        public void SetVisible(bool visible, bool immediate = false)
        {
            EnsureInitialized();
            targetVisible = visible;

            if (animationRoutine != null)
            {
                StopCoroutine(animationRoutine);
                animationRoutine = null;
            }

            if (visible && !gameObject.activeSelf)
                gameObject.SetActive(true);

            if (immediate)
            {
                ApplyState(visible ? 1f : 0f);
                FinishTransition(visible);
                return;
            }

            if (!gameObject.activeSelf)
                return;

            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
            animationRoutine = StartCoroutine(Animate(visible));
        }

        private IEnumerator Animate(bool visible)
        {
            float startAlpha = canvasGroup.alpha;
            Vector2 startPosition = animatedRoot.anchoredPosition;
            Vector3 startScale = animatedRoot.localScale;
            Vector2 destinationPosition = UsesSlide && !visible ? shownPosition + hiddenOffset : shownPosition;
            Vector3 destinationScale = UsesScale && !visible ? shownScale * hiddenScale : shownScale;
            float destinationAlpha = visible ? 1f : 0f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(.05f, duration));
                float eased = t * t * (3f - 2f * t);
                canvasGroup.alpha = Mathf.LerpUnclamped(startAlpha, destinationAlpha, eased);
                animatedRoot.anchoredPosition = Vector2.LerpUnclamped(startPosition, destinationPosition, eased);
                animatedRoot.localScale = Vector3.LerpUnclamped(startScale, destinationScale, eased);
                yield return null;
            }

            ApplyState(visible ? 1f : 0f);
            FinishTransition(visible);
            animationRoutine = null;
        }

        private void FinishTransition(bool visible)
        {
            canvasGroup.interactable = visible;
            canvasGroup.blocksRaycasts = visible;
            if (!visible && deactivateWhenHidden)
                gameObject.SetActive(false);
        }

        private void ApplyState(float visibility)
        {
            float clamped = Mathf.Clamp01(visibility);
            canvasGroup.alpha = clamped;
            animatedRoot.anchoredPosition = UsesSlide
                ? Vector2.LerpUnclamped(shownPosition + hiddenOffset, shownPosition, clamped)
                : shownPosition;
            animatedRoot.localScale = UsesScale
                ? Vector3.LerpUnclamped(shownScale * hiddenScale, shownScale, clamped)
                : shownScale;
        }

        private void EnsureInitialized()
        {
            if (initialized) return;
            if (animatedRoot == null) animatedRoot = transform as RectTransform;
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            shownPosition = animatedRoot.anchoredPosition;
            shownScale = animatedRoot.localScale;
            targetVisible = gameObject.activeSelf;
            initialized = true;
        }

        private bool UsesScale => style == TransitionStyle.ScaleFade || style == TransitionStyle.ScaleSlideFade;
        private bool UsesSlide => style == TransitionStyle.SlideFade || style == TransitionStyle.ScaleSlideFade;

        private void OnValidate()
        {
            duration = Mathf.Max(.05f, duration);
            hiddenScale = Mathf.Clamp(hiddenScale, .75f, 1f);
        }
    }
}
