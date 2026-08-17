using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EmergencyRoad
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(10000)]
    public sealed class EmergencyRoadStartupIntro : MonoBehaviour
    {
        [Header("SCENE OBJECTS - DRAG DIRECTLY")]
        [SerializeField] private Camera menuCamera;
        [SerializeField, Tooltip("Empty GameObject dat tai goc camera bat dau intro.")]
        private Transform cameraIntroMarker;
        [SerializeField] private EmergencyRoadMenuCameraOrbit cameraOrbit;
        [SerializeField, Tooltip("RectTransform marker nam ben duoi man hinh.")]
        private RectTransform logoStartMarker;
        [SerializeField, Tooltip("RectTransform marker o dung tam man hinh.")]
        private RectTransform logoCenterMarker;
        [SerializeField] private RectTransform menuLogo;
        [SerializeField] private EmergencyRoadLogoMotion logoIdleMotion;
        [SerializeField] private AudioSource introAudioSource;
        [SerializeField] private AudioClip introClip;

        [Header("INTRO TIMING")]
        [SerializeField, Min(.1f)] private float logoEnterDuration = .8f;
        [SerializeField, Min(0f)] private float centerHoldDuration = 2f;
        [SerializeField, Min(.1f)] private float returnDuration = 1.35f;

        [Header("LOGO")]
        [SerializeField, Range(1f, 2f)] private float introLogoScale = 1.35f;
        [SerializeField, Tooltip("Pivot theo phan anh that cua logo, khong tinh vung trong suot cua PNG.")]
        private Vector2 logoVisualPivot = new(.5013f, .5215f);

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
        private Quaternion originalWorldRotation;
        private Vector3 destinationWorldPosition;
        private Vector2 originalRectSize;

        private Vector3 cameraHomePosition;
        private Quaternion cameraHomeRotation;
        private bool cameraOrbitWasEnabled;
        private bool cameraPrepared;
        private bool logoPrepared;
        private bool skipRequested;
        private float startedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetApplicationState()
        {
            playedThisApplication = false;
        }

        private void Awake()
        {
            // Backward-compatible cleanup for Menu scenes authored with the old
            // full-screen intro Image. The editor authoring tool removes it
            // permanently; this prevents one frame of the legacy image meanwhile.
            Graphic legacyBackground = GetComponent<Graphic>();
            if (legacyBackground != null) legacyBackground.enabled = false;
        }

        private IEnumerator Start()
        {
            if (!ValidateReferences())
            {
                // Keep the authored Menu usable if the editor setup has not been
                // run yet. The intro root may still contain an input-blocking
                // CanvasGroup from the legacy implementation.
                gameObject.SetActive(false);
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

            Canvas.ForceUpdateCanvases();
            PrepareCamera();
            PrepareMenuLogo();

            if (introAudioSource != null && introClip != null)
            {
                introAudioSource.clip = introClip;
                introAudioSource.Play();
            }

            yield return AnimateLogoToCenter();

            if (!skipRequested)
                yield return HoldLogoAtCenter();

            if (!skipRequested)
                yield return AnimateCameraAndLogoHome();

            FinishInstantly();
        }

        private void Update()
        {
            if (!allowSkip || Time.unscaledTime - startedAt < skipDelay) return;

            bool keyboardPressed = Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame;
            bool pointerPressed = Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
            bool touchPressed = Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            if (keyboardPressed || pointerPressed || touchPressed) skipRequested = true;
        }

        private bool ValidateReferences()
        {
            bool valid = true;
            if (menuCamera == null)
            {
                Debug.LogError("[Emergency Road] Intro thieu Menu Camera. Keo Camera Menu vao Inspector.", this);
                valid = false;
            }
            if (cameraIntroMarker == null)
            {
                Debug.LogError("[Emergency Road] Intro thieu Camera Intro Marker. Keo Empty GameObject dat goc intro vao Inspector.", this);
                valid = false;
            }
            if (logoStartMarker == null)
            {
                Debug.LogError("[Emergency Road] Intro thieu Logo Start Marker ben duoi man hinh.", this);
                valid = false;
            }
            if (logoCenterMarker == null)
            {
                Debug.LogError("[Emergency Road] Intro thieu Logo Center Marker o tam man hinh.", this);
                valid = false;
            }
            if (menuLogo == null)
            {
                Debug.LogError("[Emergency Road] Intro thieu logo hien tai cua Menu.", this);
                valid = false;
            }
            return valid;
        }

        private void PrepareCamera()
        {
            Transform cameraTransform = menuCamera.transform;
            cameraHomePosition = cameraTransform.position;
            cameraHomeRotation = cameraTransform.rotation;
            cameraPrepared = true;

            if (cameraOrbit != null)
            {
                cameraOrbitWasEnabled = cameraOrbit.enabled;
                cameraOrbit.enabled = false;
            }

            cameraTransform.SetPositionAndRotation(cameraIntroMarker.position, cameraIntroMarker.rotation);
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
            originalWorldRotation = menuLogo.rotation;

            Vector2 visualPivot = new(Mathf.Clamp01(logoVisualPivot.x), Mathf.Clamp01(logoVisualPivot.y));
            destinationWorldPosition = NormalizedRectPointToWorld(menuLogo, visualPivot);
            originalRectSize = menuLogo.rect.size;

            menuLogo.SetParent(transform, true);
            menuLogo.SetAsLastSibling();
            menuLogo.anchorMin = new Vector2(.5f, .5f);
            menuLogo.anchorMax = new Vector2(.5f, .5f);
            menuLogo.pivot = visualPivot;
            menuLogo.sizeDelta = originalRectSize;
            menuLogo.position = logoStartMarker.position;
            menuLogo.rotation = originalWorldRotation;
            menuLogo.localScale = originalLocalScale * introLogoScale;
            logoPrepared = true;
        }

        private IEnumerator AnimateLogoToCenter()
        {
            Vector3 startPosition = logoStartMarker.position;
            Vector3 targetPosition = logoCenterMarker.position;
            float elapsed = 0f;

            while (elapsed < logoEnterDuration && !skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(.1f, logoEnterDuration));
                float eased = Smoother(t);
                menuLogo.position = Vector3.LerpUnclamped(startPosition, targetPosition, eased);
                yield return null;
            }

            if (!skipRequested)
                menuLogo.position = targetPosition;
        }

        private IEnumerator HoldLogoAtCenter()
        {
            float elapsed = 0f;
            while (elapsed < centerHoldDuration && !skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                menuLogo.position = logoCenterMarker.position;
                yield return null;
            }
        }

        private IEnumerator AnimateCameraAndLogoHome()
        {
            Transform cameraTransform = menuCamera.transform;
            Vector3 cameraStartPosition = cameraTransform.position;
            Quaternion cameraStartRotation = cameraTransform.rotation;
            Vector3 logoStartPosition = menuLogo.position;
            Vector3 logoStartScale = menuLogo.localScale;
            Quaternion logoStartRotation = menuLogo.rotation;
            float elapsed = 0f;

            while (elapsed < returnDuration && !skipRequested)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / Mathf.Max(.1f, returnDuration));
                float eased = Smoother(t);

                cameraTransform.position = Vector3.LerpUnclamped(cameraStartPosition, cameraHomePosition, eased);
                cameraTransform.rotation = Quaternion.SlerpUnclamped(cameraStartRotation, cameraHomeRotation, eased);

                menuLogo.position = Vector3.LerpUnclamped(logoStartPosition, destinationWorldPosition, eased);
                menuLogo.localScale = Vector3.LerpUnclamped(logoStartScale, originalLocalScale, eased);
                menuLogo.rotation = Quaternion.SlerpUnclamped(logoStartRotation, originalWorldRotation, eased);
                yield return null;
            }

            if (!skipRequested)
            {
                cameraTransform.SetPositionAndRotation(cameraHomePosition, cameraHomeRotation);
                menuLogo.position = destinationWorldPosition;
                menuLogo.localScale = originalLocalScale;
                menuLogo.rotation = originalWorldRotation;
            }
        }

        private void FinishInstantly()
        {
            if (introAudioSource != null && introAudioSource.isPlaying)
                introAudioSource.Stop();

            if (cameraPrepared && menuCamera != null)
            {
                menuCamera.transform.SetPositionAndRotation(cameraHomePosition, cameraHomeRotation);
                if (cameraOrbit != null) cameraOrbit.enabled = cameraOrbitWasEnabled;
                cameraPrepared = false;
            }

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

            gameObject.SetActive(false);
        }

        private static float Smoother(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        private static Vector3 NormalizedRectPointToWorld(RectTransform rectTransform, Vector2 normalizedPoint)
        {
            Rect rect = rectTransform.rect;
            Vector3 localPoint = new(
                Mathf.LerpUnclamped(rect.xMin, rect.xMax, normalizedPoint.x),
                Mathf.LerpUnclamped(rect.yMin, rect.yMax, normalizedPoint.y),
                0f);
            return rectTransform.TransformPoint(localPoint);
        }
    }
}
