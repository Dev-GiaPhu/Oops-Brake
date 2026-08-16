using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EmergencyRoad
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class EmergencyRoadStartupIntro : MonoBehaviour
    {
        [Header("SCENE OBJECTS - DRAG DIRECTLY")]
        [SerializeField] private Image backgroundImage;
        [SerializeField] private RectTransform logoStartMarker;
        [SerializeField] private RectTransform menuLogo;
        [SerializeField] private EmergencyRoadLogoMotion logoIdleMotion;
        [SerializeField] private AudioSource introAudioSource;
        [SerializeField] private AudioClip introClip;

        [Header("INTRO TIMING")]
        [SerializeField, Min(.1f)] private float displayDuration = 2f;
        [SerializeField, Min(.1f)] private float logoPopDuration = .45f;
        [SerializeField, Min(.1f)] private float travelDuration = .85f;
        [SerializeField, Min(.1f)] private float backgroundFadeDuration = .65f;

        [Header("CARTOON LOGO MOTION")]
        [SerializeField, Range(1f, 2f)] private float introLogoScale = 1.35f;
        [SerializeField, Range(0f, 240f)] private float travelArcHeight = 90f;
        [SerializeField, Range(0f, 20f)] private float travelTilt = 8f;

        [Header("BEHAVIOUR")]
        [SerializeField] private bool playOncePerApplication = true;
        [SerializeField] private bool allowSkip = true;
        [SerializeField, Min(0f)] private float skipDelay = .4f;

        private static bool playedThisApplication;
        private Transform originalParent;
        private int originalSiblingIndex;
        private Vector2 originalAnchorMin;
        private Vector2 originalAnchorMax;
        private Vector2 originalAnchoredPosition;
        private Vector2 originalSizeDelta;
        private Vector2 originalPivot;
        private Vector3 originalLocalScale;
        private Quaternion originalLocalRotation;
        private Vector3 destinationWorldPosition;
        private Vector2 originalRectSize;
        private Color backgroundColor;
        private float startedAt;
        private bool logoPrepared;
        private bool skipRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetApplicationState()
        {
            playedThisApplication = false;
        }

        private IEnumerator Start()
        {
            if (!ValidateReferences())
            {
                enabled = false;
                yield break;
            }

            if (playOncePerApplication && playedThisApplication)
            {
                FinishInstantly();
                yield break;
            }

            playedThisApplication = true;
            startedAt = Time.unscaledTime;
            skipRequested = false;
            backgroundColor = backgroundImage.color;
            backgroundImage.raycastTarget = true;
            PrepareMenuLogo();

            if (introAudioSource != null && introClip != null)
            {
                introAudioSource.clip = introClip;
                introAudioSource.Play();
            }

            yield return AnimateLogoPop();

            float remainingDisplay = Mathf.Max(0f, displayDuration - logoPopDuration);
            float elapsed = 0f;
            while (elapsed < remainingDisplay && !skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            if (!skipRequested)
                yield return AnimateLogoToMenu();

            FinishInstantly();
        }

        private void Update()
        {
            if (!allowSkip || Time.unscaledTime - startedAt < skipDelay) return;
            bool keyboardPressed = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
            bool mousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if (keyboardPressed || mousePressed) skipRequested = true;
        }

        private bool ValidateReferences()
        {
            bool valid = true;
            if (backgroundImage == null) { Debug.LogError("[Emergency Road] Intro thiếu Background Image.", this); valid = false; }
            if (logoStartMarker == null) { Debug.LogError("[Emergency Road] Intro thiếu Logo Start Marker.", this); valid = false; }
            if (menuLogo == null) { Debug.LogError("[Emergency Road] Intro thiếu logo hiện tại của Menu.", this); valid = false; }
            return valid;
        }

        private void PrepareMenuLogo()
        {
            if (logoIdleMotion != null) logoIdleMotion.enabled = false;

            originalParent = menuLogo.parent;
            originalSiblingIndex = menuLogo.GetSiblingIndex();
            originalAnchorMin = menuLogo.anchorMin;
            originalAnchorMax = menuLogo.anchorMax;
            originalAnchoredPosition = menuLogo.anchoredPosition;
            originalSizeDelta = menuLogo.sizeDelta;
            originalPivot = menuLogo.pivot;
            originalLocalScale = menuLogo.localScale;
            originalLocalRotation = menuLogo.localRotation;
            destinationWorldPosition = menuLogo.position;
            originalRectSize = menuLogo.rect.size;

            menuLogo.SetParent(transform, true);
            menuLogo.anchorMin = new Vector2(.5f, .5f);
            menuLogo.anchorMax = new Vector2(.5f, .5f);
            menuLogo.pivot = new Vector2(.5f, .5f);
            menuLogo.sizeDelta = originalRectSize;
            menuLogo.position = logoStartMarker.position;
            menuLogo.localScale = originalLocalScale * .35f;
            menuLogo.localRotation = Quaternion.Euler(0f, 0f, -10f);
            logoPrepared = true;
        }

        private IEnumerator AnimateLogoPop()
        {
            Vector3 startScale = menuLogo.localScale;
            Vector3 targetScale = originalLocalScale * introLogoScale;
            Quaternion startRotation = menuLogo.localRotation;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, -3f);
            float elapsed = 0f;

            while (elapsed < logoPopDuration && !skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(.1f, logoPopDuration));
                float back = EaseOutBack(t);
                menuLogo.localScale = Vector3.LerpUnclamped(startScale, targetScale, back);
                menuLogo.localRotation = Quaternion.SlerpUnclamped(startRotation, targetRotation, back);
                yield return null;
            }

            menuLogo.localScale = targetScale;
            menuLogo.localRotation = targetRotation;
        }

        private IEnumerator AnimateLogoToMenu()
        {
            Vector3 startPosition = menuLogo.position;
            Vector3 startScale = menuLogo.localScale;
            Quaternion startRotation = menuLogo.localRotation;
            float totalDuration = Mathf.Max(travelDuration, backgroundFadeDuration);
            float elapsed = 0f;

            while (elapsed < totalDuration && !skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                float moveT = Mathf.Clamp01(elapsed / Mathf.Max(.1f, travelDuration));
                float fadeT = Mathf.Clamp01(elapsed / Mathf.Max(.1f, backgroundFadeDuration));
                float moveEase = Smooth(moveT);
                float fadeEase = Smooth(fadeT);

                Vector3 position = Vector3.LerpUnclamped(startPosition, destinationWorldPosition, moveEase);
                position += Vector3.up * (4f * moveEase * (1f - moveEase) * travelArcHeight);
                menuLogo.position = position;
                menuLogo.localScale = Vector3.LerpUnclamped(startScale, originalLocalScale, moveEase);
                Quaternion travelRotation = originalLocalRotation * Quaternion.Euler(0f, 0f,
                    Mathf.Sin(moveEase * Mathf.PI) * travelTilt);
                menuLogo.localRotation = Quaternion.SlerpUnclamped(startRotation, travelRotation, moveEase);

                Color faded = backgroundColor;
                faded.a = backgroundColor.a * (1f - fadeEase);
                backgroundImage.color = faded;
                yield return null;
            }
        }

        private void FinishInstantly()
        {
            if (introAudioSource != null && introAudioSource.isPlaying)
                introAudioSource.Stop();

            if (logoPrepared)
            {
                menuLogo.SetParent(originalParent, false);
                menuLogo.SetSiblingIndex(Mathf.Clamp(originalSiblingIndex, 0, originalParent.childCount - 1));
                menuLogo.anchorMin = originalAnchorMin;
                menuLogo.anchorMax = originalAnchorMax;
                menuLogo.anchoredPosition = originalAnchoredPosition;
                menuLogo.sizeDelta = originalSizeDelta;
                menuLogo.pivot = originalPivot;
                menuLogo.localScale = originalLocalScale;
                menuLogo.localRotation = originalLocalRotation;
                logoPrepared = false;
                if (logoIdleMotion != null) logoIdleMotion.enabled = true;
            }

            if (backgroundImage != null)
            {
                Color hidden = backgroundImage.color;
                hidden.a = 0f;
                backgroundImage.color = hidden;
                backgroundImage.raycastTarget = false;
            }

            gameObject.SetActive(false);
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float value = t - 1f;
            return 1f + c3 * value * value * value + c1 * value * value;
        }
    }
}
