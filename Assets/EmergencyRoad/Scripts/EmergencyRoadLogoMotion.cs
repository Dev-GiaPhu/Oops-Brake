using UnityEngine;

namespace EmergencyRoad
{
    [DisallowMultipleComponent]
    public sealed class EmergencyRoadLogoMotion : MonoBehaviour
    {
        [Header("LOGO OBJECT - DRAG DIRECTLY")]
        [SerializeField] private RectTransform animatedTarget;

        [Header("IDLE ANIMATION")]
        [SerializeField, Range(.1f, 4f)] private float speed = 1.25f;
        [SerializeField, Range(0f, .12f)] private float pulseAmount = .035f;
        [SerializeField, Range(0f, 15f)] private float floatDistance = 5f;
        [SerializeField, Range(0f, 4f)] private float tiltDegrees = 1.2f;

        [Header("SMOOTH INTRO HANDOFF")]
        [SerializeField, Min(0f)] private float resumeBlendDuration = .28f;

        private Vector3 baseScale;
        private Vector2 basePosition;
        private Quaternion baseRotation;
        private float animationWeight;

        private void Awake()
        {
            if (animatedTarget == null) animatedTarget = transform as RectTransform;
            CaptureBasePose();
        }

        private void OnEnable()
        {
            if (animatedTarget == null) animatedTarget = transform as RectTransform;
            CaptureBasePose();
            animationWeight = resumeBlendDuration <= 0f ? 1f : 0f;
        }

        private void Update()
        {
            if (animatedTarget == null) return;
            animationWeight = Mathf.MoveTowards(animationWeight, 1f,
                Time.unscaledDeltaTime / Mathf.Max(.01f, resumeBlendDuration));
            float blend = animationWeight * animationWeight * (3f - 2f * animationWeight);
            float phase = Time.unscaledTime * speed;
            float wave = Mathf.Sin(phase * Mathf.PI * 2f);
            animatedTarget.localScale = baseScale * (1f + wave * pulseAmount * blend);
            animatedTarget.anchoredPosition = basePosition + Vector2.up * (wave * floatDistance * blend);
            animatedTarget.localRotation = baseRotation * Quaternion.Euler(0f, 0f,
                Mathf.Sin(phase * Mathf.PI) * tiltDegrees * blend);
        }

        private void OnDisable()
        {
            if (animatedTarget == null) return;
            animatedTarget.localScale = baseScale;
            animatedTarget.anchoredPosition = basePosition;
            animatedTarget.localRotation = baseRotation;
        }

        private void CaptureBasePose()
        {
            if (animatedTarget == null) return;
            baseScale = animatedTarget.localScale;
            basePosition = animatedTarget.anchoredPosition;
            baseRotation = animatedTarget.localRotation;
        }
    }
}
