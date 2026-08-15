using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EmergencyRoad
{
    [DisallowMultipleComponent]
    public sealed class EmergencyRoadUIButtonMotion : MonoBehaviour, IPointerEnterHandler,
        IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("UI OBJECT - DRAG DIRECTLY")]
        [SerializeField] private RectTransform animatedTarget;

        [Header("HOVER AND PRESS")]
        [SerializeField, Range(1f, 1.2f)] private float hoverScale = 1.06f;
        [SerializeField, Range(.8f, 1f)] private float pressedScale = .96f;
        [SerializeField, Range(0f, 12f)] private float hoverLift = 3f;
        [SerializeField, Range(.02f, .3f)] private float smoothTime = .08f;

        private Selectable selectable;
        private Vector3 baseScale;
        private Vector2 basePosition;
        private Vector3 scaleVelocity;
        private Vector2 positionVelocity;
        private bool hovered;
        private bool pressed;

        private void Awake()
        {
            if (animatedTarget == null) animatedTarget = transform as RectTransform;
            selectable = GetComponent<Selectable>();
            CaptureBasePose();
        }

        private void OnEnable()
        {
            if (animatedTarget == null) animatedTarget = transform as RectTransform;
            CaptureBasePose();
            hovered = pressed = false;
        }

        private void Update()
        {
            if (animatedTarget == null) return;
            bool interactive = selectable == null || selectable.IsInteractable();
            float multiplier = interactive ? pressed ? pressedScale : hovered ? hoverScale : 1f : 1f;
            Vector3 targetScale = baseScale * multiplier;
            Vector2 targetPosition = basePosition + Vector2.up * (interactive && hovered && !pressed ? hoverLift : 0f);
            animatedTarget.localScale = Vector3.SmoothDamp(animatedTarget.localScale, targetScale,
                ref scaleVelocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
            animatedTarget.anchoredPosition = Vector2.SmoothDamp(animatedTarget.anchoredPosition, targetPosition,
                ref positionVelocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        }

        public void OnPointerEnter(PointerEventData eventData) => hovered = true;
        public void OnPointerExit(PointerEventData eventData) { hovered = false; pressed = false; }
        public void OnPointerDown(PointerEventData eventData) { if (eventData.button == PointerEventData.InputButton.Left) pressed = true; }
        public void OnPointerUp(PointerEventData eventData) { if (eventData.button == PointerEventData.InputButton.Left) pressed = false; }

        private void OnDisable()
        {
            if (animatedTarget != null)
            {
                animatedTarget.localScale = baseScale;
                animatedTarget.anchoredPosition = basePosition;
            }
        }

        private void CaptureBasePose()
        {
            if (animatedTarget == null) return;
            baseScale = animatedTarget.localScale;
            basePosition = animatedTarget.anchoredPosition;
        }
    }
}
