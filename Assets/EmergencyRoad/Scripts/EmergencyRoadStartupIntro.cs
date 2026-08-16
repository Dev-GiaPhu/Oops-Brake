using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace EmergencyRoad
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-10000)]
    public sealed class EmergencyRoadStartupIntro : MonoBehaviour
    {
        [Header("SCENE REFERENCES - DRAG DIRECTLY")]
        [SerializeField] private CanvasGroup introCanvas;
        [SerializeField] private RectTransform logoRoot;
        [SerializeField] private AudioSource introAudioSource;
        [SerializeField] private AudioClip introClip;

        [Header("INTRO TIMING")]
        [SerializeField, Min(.05f)] private float fadeInDuration = .45f;
        [SerializeField, Min(0f)] private float holdDuration = 1.35f;
        [SerializeField, Min(.05f)] private float fadeOutDuration = .65f;
        [SerializeField, Range(.5f, 1f)] private float logoStartScale = .82f;

        [Header("BEHAVIOUR")]
        [SerializeField] private bool playOncePerApplication = true;
        [SerializeField] private bool allowSkip = true;
        [SerializeField, Min(0f)] private float skipDelay = .45f;

        private static bool playedThisApplication;
        private float startedAt;
        private bool skipRequested;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetApplicationState()
        {
            playedThisApplication = false;
        }

        private IEnumerator Start()
        {
            if (introCanvas == null)
            {
                Debug.LogError("[Emergency Road] Startup Intro thiếu Intro Canvas.", this);
                enabled = false;
                yield break;
            }

            if (playOncePerApplication && playedThisApplication)
            {
                HideImmediate();
                yield break;
            }

            playedThisApplication = true;
            startedAt = Time.unscaledTime;
            skipRequested = false;
            gameObject.SetActive(true);
            introCanvas.alpha = 0f;
            introCanvas.interactable = true;
            introCanvas.blocksRaycasts = true;
            SetLogoScale(logoStartScale);

            if (introAudioSource != null && introClip != null)
            {
                introAudioSource.clip = introClip;
                introAudioSource.Play();
            }

            yield return Animate(0f, 1f, fadeInDuration, true);
            yield return WaitForHoldOrSkip();
            yield return Animate(introCanvas.alpha, 0f, fadeOutDuration, false);
            HideImmediate();
        }

        private void Update()
        {
            if (!allowSkip || Time.unscaledTime - startedAt < skipDelay) return;

            bool keyboardPressed = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
            bool mousePressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            if (keyboardPressed || mousePressed) skipRequested = true;
        }

        private IEnumerator Animate(float from, float to, float duration, bool scaleLogo)
        {
            float elapsed = 0f;
            while (elapsed < duration && !skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(.05f, duration));
                float eased = t * t * (3f - 2f * t);
                introCanvas.alpha = Mathf.LerpUnclamped(from, to, eased);
                if (scaleLogo)
                    SetLogoScale(Mathf.LerpUnclamped(logoStartScale, 1f, eased));
                yield return null;
            }

            introCanvas.alpha = to;
            if (scaleLogo) SetLogoScale(1f);
        }

        private IEnumerator WaitForHoldOrSkip()
        {
            float elapsed = 0f;
            while (elapsed < holdDuration && !skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void HideImmediate()
        {
            if (introAudioSource != null && introAudioSource.isPlaying)
                introAudioSource.Stop();

            if (introCanvas != null)
            {
                introCanvas.alpha = 0f;
                introCanvas.interactable = false;
                introCanvas.blocksRaycasts = false;
            }

            gameObject.SetActive(false);
        }

        private void SetLogoScale(float value)
        {
            if (logoRoot != null) logoRoot.localScale = Vector3.one * value;
        }
    }
}
