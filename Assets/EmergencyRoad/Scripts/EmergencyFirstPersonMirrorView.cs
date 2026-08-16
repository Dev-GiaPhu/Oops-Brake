using UnityEngine;
using UnityEngine.UI;

namespace EmergencyRoad
{
    [DisallowMultipleComponent]
    public sealed class EmergencyFirstPersonMirrorView : MonoBehaviour
    {
        [Header("MIRROR IMAGES - DRAG FROM THIS SCENE")]
        [SerializeField] private RawImage leftMirror;
        [SerializeField] private RawImage rightMirror;

        [Header("MIRROR FRAMES - DRAG PARENT OBJECTS HERE")]
        [SerializeField] private RectTransform leftFrame;
        [SerializeField] private RectTransform rightFrame;

        [Header("SLIDE ANIMATION")]
        [SerializeField, Min(.05f)] private float slideDuration = .28f;
        [SerializeField, Min(0f)] private float hiddenPadding = 24f;

        private RectTransform leftRect;
        private RectTransform rightRect;
        private Vector2 leftShown;
        private Vector2 rightShown;
        private float visibility;
        private float targetVisibility;
        private bool initialized;
        private bool leftOriginalActive;
        private bool rightOriginalActive;
        private bool leftOriginalEnabled;
        private bool rightOriginalEnabled;

        public bool IsConfigured => leftMirror != null && rightMirror != null;

        public void Configure(RawImage left, RawImage right)
        {
            leftMirror = left;
            rightMirror = right;
            initialized = false;
        }

        public void ConfigureFrames(RectTransform left, RectTransform right)
        {
            leftFrame = left;
            rightFrame = right;
            initialized = false;
        }

        private void Awake()
        {
            CacheLayout();
            ApplyLayout();
        }

        private void OnEnable()
        {
            if (initialized && Application.isPlaying)
                ApplyLayout();
        }

        private void OnDisable()
        {
            RestoreAuthoredLayout();
        }

        public void SetVisible(bool visible, bool immediate = false)
        {
            CacheLayout();
            targetVisibility = visible ? 1f : 0f;
            if (immediate) visibility = targetVisibility;
            ApplyLayout();
        }

        private void Update()
        {
            if (!initialized || Mathf.Approximately(visibility, targetVisibility)) return;
            visibility = Mathf.MoveTowards(visibility, targetVisibility,
                Time.unscaledDeltaTime / Mathf.Max(.05f, slideDuration));
            ApplyLayout();
        }

        private void CacheLayout()
        {
            if (initialized || !IsConfigured) return;
            // Move the parent frames when assigned. Moving both a frame and its
            // child RawImage would apply the slide offset twice.
            leftFrame = ResolveAuthoredFrame(leftMirror, leftFrame);
            rightFrame = ResolveAuthoredFrame(rightMirror, rightFrame);
            leftRect = leftFrame != null ? leftFrame : leftMirror.rectTransform;
            rightRect = rightFrame != null ? rightFrame : rightMirror.rectTransform;
            leftShown = leftRect.anchoredPosition;
            rightShown = rightRect.anchoredPosition;
            leftOriginalActive = leftRect.gameObject.activeSelf;
            rightOriginalActive = rightRect.gameObject.activeSelf;
            leftOriginalEnabled = leftMirror.enabled;
            rightOriginalEnabled = rightMirror.enabled;
            initialized = true;
        }

        private RectTransform ResolveAuthoredFrame(RawImage mirror, RectTransform assignedFrame)
        {
            if (assignedFrame != null) return assignedFrame;
            RectTransform parent = mirror.rectTransform.parent as RectTransform;
            return parent != null && parent != transform ? parent : null;
        }

        private void ApplyLayout()
        {
            if (!initialized) return;
            float eased = visibility * visibility * (3f - 2f * visibility);
            Vector2 leftHidden = leftShown + Vector2.left * (leftRect.rect.width + hiddenPadding);
            Vector2 rightHidden = rightShown + Vector2.right * (rightRect.rect.width + hiddenPadding);
            leftRect.anchoredPosition = Vector2.LerpUnclamped(leftHidden, leftShown, eased);
            rightRect.anchoredPosition = Vector2.LerpUnclamped(rightHidden, rightShown, eased);
            bool visible = visibility > .001f;
            if (leftFrame != null) leftFrame.gameObject.SetActive(visible);
            if (rightFrame != null) rightFrame.gameObject.SetActive(visible);
            leftMirror.enabled = visible;
            rightMirror.enabled = visible;
        }

        private void RestoreAuthoredLayout()
        {
            if (!initialized) return;
            leftRect.anchoredPosition = leftShown;
            rightRect.anchoredPosition = rightShown;
            leftRect.gameObject.SetActive(leftOriginalActive);
            rightRect.gameObject.SetActive(rightOriginalActive);
            leftMirror.enabled = leftOriginalEnabled;
            rightMirror.enabled = rightOriginalEnabled;
        }
    }
}
