using UnityEngine;
using UnityEngine.UI;

namespace EmergencyRoad
{
    [DisallowMultipleComponent]
    public sealed class EmergencyFirstPersonMirrorView : MonoBehaviour
    {
        [Header("MIRROR IMAGES - DRAG FROM THIS SCENE")]
        [SerializeField] private Image leftMirror;
        [SerializeField] private Image rightMirror;

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

        public bool IsConfigured => leftMirror != null && rightMirror != null;

        public void Configure(Image left, Image right)
        {
            leftMirror = left;
            rightMirror = right;
        }

        private void Awake()
        {
            CacheLayout();
            ApplyLayout();
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
            leftRect = leftMirror.rectTransform;
            rightRect = rightMirror.rectTransform;
            leftShown = leftRect.anchoredPosition;
            rightShown = rightRect.anchoredPosition;
            initialized = true;
        }

        private void ApplyLayout()
        {
            if (!initialized) return;
            float eased = visibility * visibility * (3f - 2f * visibility);
            Vector2 leftHidden = leftShown + Vector2.left * (leftRect.rect.width + hiddenPadding);
            Vector2 rightHidden = rightShown + Vector2.right * (rightRect.rect.width + hiddenPadding);
            leftRect.anchoredPosition = Vector2.LerpUnclamped(leftHidden, leftShown, eased);
            rightRect.anchoredPosition = Vector2.LerpUnclamped(rightHidden, rightShown, eased);
            leftMirror.enabled = visibility > .001f;
            rightMirror.enabled = visibility > .001f;
        }
    }
}
