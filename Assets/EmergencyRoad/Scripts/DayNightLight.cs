using UnityEngine;

namespace EmergencyRoad
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public sealed class DayNightLight : MonoBehaviour
    {
        [SerializeField] private bool invert;

        [Header("SMOOTH LIGHT TRANSITION")]
        [SerializeField, Min(.05f)] private float fadeInDuration = .9f;
        [SerializeField, Min(.05f)] private float fadeOutDuration = .65f;
        [SerializeField, Min(0f)] private float randomStartDelay = .3f;

        [Header("STARTUP FLICKER")]
        [SerializeField, Min(0f)] private float startupFlickerDuration = .42f;
        [SerializeField, Range(4f, 40f)] private float flickerSpeed = 18f;
        [SerializeField, Range(0f, .8f)] private float flickerMinimum = .08f;

        private Light controlledLight;
        private float fullIntensity;
        private float startIntensity;
        private float transitionTime;
        private float flickerPhase;
        private bool targetOn;
        private bool stateInitialized;
        private bool transitioning;

        private void Awake()
        {
            controlledLight = GetComponent<Light>();
            fullIntensity = Mathf.Max(0f, controlledLight.intensity);
            flickerPhase = Random.value * 100f;
        }

        private void OnEnable()
        {
            DayNightCycle.NightStateChanged += ApplyNightState;
            ApplyNightState(DayNightCycle.HasActiveCycle && DayNightCycle.IsNight);
        }

        private void OnDisable()
        {
            DayNightCycle.NightStateChanged -= ApplyNightState;
        }

        private void Update()
        {
            if (!transitioning || controlledLight == null) return;
            transitionTime += Time.deltaTime;
            if (transitionTime < 0f) return;

            float duration = targetOn ? fadeInDuration : fadeOutDuration;
            float progress = Mathf.Clamp01(transitionTime / duration);
            float smoothProgress = progress * progress * (3f - 2f * progress);
            float targetIntensity = targetOn ? fullIntensity : 0f;
            float intensity = Mathf.Lerp(startIntensity, targetIntensity, smoothProgress);

            if (targetOn && transitionTime < startupFlickerDuration)
            {
                float fadeFlicker = 1f - transitionTime / Mathf.Max(.001f, startupFlickerDuration);
                float waveA = Mathf.Sin((transitionTime * flickerSpeed + flickerPhase) * 6.283185f);
                float waveB = Mathf.Sin((transitionTime * flickerSpeed * 1.73f + flickerPhase * .37f) * 6.283185f);
                float pulse = waveA + waveB * .55f > .05f ? 1f : flickerMinimum;
                intensity *= Mathf.Lerp(1f, pulse, fadeFlicker);
            }

            controlledLight.intensity = intensity;
            if (progress < 1f) return;

            controlledLight.intensity = targetIntensity;
            controlledLight.enabled = targetOn;
            transitioning = false;
        }

        private void ApplyNightState(bool isNight)
        {
            targetOn = invert ? !isNight : isNight;
            if (!stateInitialized)
            {
                stateInitialized = true;
                controlledLight.enabled = targetOn;
                controlledLight.intensity = targetOn ? fullIntensity : 0f;
                return;
            }

            startIntensity = controlledLight.enabled ? controlledLight.intensity : 0f;
            transitionTime = targetOn ? -Random.Range(0f, randomStartDelay) : 0f;
            if (targetOn) controlledLight.enabled = true;
            transitioning = true;
        }
    }
}
