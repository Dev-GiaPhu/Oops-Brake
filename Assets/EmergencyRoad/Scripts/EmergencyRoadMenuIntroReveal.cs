using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace EmergencyRoad
{
    [DisallowMultipleComponent]
    public sealed class EmergencyRoadMenuIntroReveal : MonoBehaviour
    {
        [Header("MENU UI - DRAG DIRECTLY")]
        [SerializeField] private Graphic menuPanelBackground;
        [SerializeField] private EmergencyPanelTransition topBarTransition;
        [SerializeField] private EmergencyPanelTransition[] buttonTransitions;

        [Header("COMMERCIAL STYLE REVEAL")]
        [SerializeField, Min(0f)] private float leadDelay = .055f;
        [SerializeField, Min(0f)] private float buttonStagger = .075f;
        [SerializeField, Min(.05f)] private float backgroundFadeDuration = .3f;
        [SerializeField, Min(0f)] private float settlePadding = .035f;

        private Color shownBackgroundColor;
        private bool shownBackgroundRaycastTarget;
        private bool shownStateCaptured;
        private bool prepared;
        private Coroutine backgroundRoutine;

        private void Awake()
        {
            CaptureShownState();
        }

        public void PrepareHidden()
        {
            CaptureShownState();
            StopBackgroundRoutine();

            if (menuPanelBackground != null)
            {
                Color hidden = shownBackgroundColor;
                hidden.a = 0f;
                menuPanelBackground.color = hidden;
                menuPanelBackground.raycastTarget = false;
            }

            if (topBarTransition != null)
                topBarTransition.HideImmediate();

            if (buttonTransitions != null)
            {
                foreach (EmergencyPanelTransition transition in buttonTransitions)
                    if (transition != null) transition.HideImmediate();
            }

            prepared = true;
        }

        public void ShowImmediate()
        {
            CaptureShownState();
            StopBackgroundRoutine();

            if (menuPanelBackground != null)
            {
                menuPanelBackground.color = shownBackgroundColor;
                menuPanelBackground.raycastTarget = shownBackgroundRaycastTarget;
            }

            if (topBarTransition != null)
                topBarTransition.ShowImmediate();

            if (buttonTransitions != null)
            {
                foreach (EmergencyPanelTransition transition in buttonTransitions)
                    if (transition != null) transition.ShowImmediate();
            }

            prepared = false;
        }

        public IEnumerator Reveal()
        {
            if (!prepared)
            {
                ShowImmediate();
                yield break;
            }

            if (menuPanelBackground != null)
                backgroundRoutine = StartCoroutine(FadeBackgroundIn());

            if (topBarTransition != null)
                topBarTransition.Show();

            if (leadDelay > 0f)
                yield return WaitUnscaled(leadDelay);

            float longestTransition = topBarTransition != null ? topBarTransition.Duration : 0f;
            if (buttonTransitions != null)
            {
                for (int i = 0; i < buttonTransitions.Length; i++)
                {
                    EmergencyPanelTransition transition = buttonTransitions[i];
                    if (transition != null)
                    {
                        transition.Show();
                        longestTransition = Mathf.Max(longestTransition, transition.Duration);
                    }

                    if (i < buttonTransitions.Length - 1 && buttonStagger > 0f)
                        yield return WaitUnscaled(buttonStagger);
                }
            }

            float settleDuration = Mathf.Max(backgroundFadeDuration, longestTransition) + settlePadding;
            if (settleDuration > 0f)
                yield return WaitUnscaled(settleDuration);

            StopBackgroundRoutine();
            if (menuPanelBackground != null)
            {
                menuPanelBackground.color = shownBackgroundColor;
                menuPanelBackground.raycastTarget = shownBackgroundRaycastTarget;
            }

            prepared = false;
        }

        private IEnumerator FadeBackgroundIn()
        {
            float elapsed = 0f;

            while (elapsed < backgroundFadeDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(.05f, backgroundFadeDuration));
                float eased = EaseOutCubic(t);
                Color color = shownBackgroundColor;
                color.a = Mathf.LerpUnclamped(0f, shownBackgroundColor.a, eased);
                menuPanelBackground.color = color;
                yield return null;
            }

            menuPanelBackground.color = shownBackgroundColor;
            backgroundRoutine = null;
        }

        private void CaptureShownState()
        {
            if (shownStateCaptured) return;
            if (menuPanelBackground != null)
            {
                shownBackgroundColor = menuPanelBackground.color;
                shownBackgroundRaycastTarget = menuPanelBackground.raycastTarget;
            }
            shownStateCaptured = true;
        }

        private void StopBackgroundRoutine()
        {
            if (backgroundRoutine == null) return;
            StopCoroutine(backgroundRoutine);
            backgroundRoutine = null;
        }

        private static IEnumerator WaitUnscaled(float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private static float EaseOutCubic(float t)
        {
            t = Mathf.Clamp01(t);
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }

        private void OnValidate()
        {
            leadDelay = Mathf.Max(0f, leadDelay);
            buttonStagger = Mathf.Max(0f, buttonStagger);
            backgroundFadeDuration = Mathf.Max(.05f, backgroundFadeDuration);
            settlePadding = Mathf.Max(0f, settlePadding);
        }
    }
}
