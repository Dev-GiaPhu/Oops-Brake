using System.Collections;
using UnityEngine;

namespace EmergencyRoad
{
    [DisallowMultipleComponent]
    public sealed class EmergencyWeatherSystem : MonoBehaviour
    {
        public enum WeatherState
        {
            Clear,
            Rain
        }

        private static readonly int WetnessId = Shader.PropertyToID("_EmergencyWetness");
        private static readonly int RainIntensityId = Shader.PropertyToID("_EmergencyRainIntensity");
        private static readonly int PuddleAmountId = Shader.PropertyToID("_EmergencyPuddleAmount");
        private static readonly int WetDarkeningId = Shader.PropertyToID("_EmergencyWetDarkening");
        private static readonly int PuddleScaleId = Shader.PropertyToID("_EmergencyPuddleScale");
        private static readonly int RippleStrengthId = Shader.PropertyToID("_EmergencyRippleStrength");
        private static readonly int TrackDistanceId = Shader.PropertyToID("_EmergencyTrackDistance");

        [Header("THAM CHIEU TRONG SCENE")]
        [SerializeField] private EmergencyRoadGame gameplay;
        [SerializeField] private Transform followTarget;
        [SerializeField] private ParticleSystem rainParticles;
        [SerializeField] private Light lightningLight;
        [SerializeField] private AudioSource rainAudioSource;
        [SerializeField] private AudioSource thunderAudioSource;

        [Header("AM THANH - KEO THA DE THAY")]
        [SerializeField] private AudioClip rainLoop;
        [SerializeField] private AudioClip[] thunderClips;
        [SerializeField, Range(0f, 1f)] private float rainVolume = .55f;
        [SerializeField, Range(0f, 1f)] private float thunderVolume = .9f;

        [Header("THOI TIET NGAY / DEM")]
        [SerializeField] private WeatherState startingWeather = WeatherState.Clear;
        [SerializeField] private bool randomWeather = true;
        [SerializeField] private bool randomizeOnStart = true;
        [SerializeField, Range(0f, 1f)] private float dayRainChance = .35f;
        [SerializeField, Range(0f, 1f)] private float nightRainChance = .55f;
        [SerializeField] private Vector2 clearDurationRange = new(35f, 70f);
        [SerializeField] private Vector2 rainDurationRange = new(28f, 55f);
        [SerializeField, Min(.1f)] private float transitionDuration = 4f;

        [Header("MUA")]
        [SerializeField, Min(0f)] private float maximumEmissionRate = 720f;
        [SerializeField, Min(.1f)] private float wetBuildUpSeconds = 8f;
        [SerializeField, Min(.1f)] private float dryOutSeconds = 18f;
        [SerializeField] private Vector3 rainFollowOffset = new(0f, 13f, 22f);

        [Header("MAT DUONG UOT VA VUNG NUOC")]
        [SerializeField, Range(0f, 1f)] private float puddleAmount = .78f;
        [SerializeField, Range(0f, .5f)] private float wetDarkening = .14f;
        [SerializeField, Range(.02f, 1f)] private float puddleWorldScale = .14f;
        [SerializeField, Range(0f, 2f)] private float rippleStrength = 1f;

        [Header("SUONG KHI MUA")]
        [SerializeField] private bool controlRainFog = true;
        [SerializeField] private Color rainFogColor = new(.44f, .52f, .58f, 1f);
        [SerializeField, Min(0f)] private float rainFogDensity = .012f;

        [Header("SAM CHOP")]
        [SerializeField] private bool lightningEnabled = true;
        [SerializeField] private Vector2 lightningIntervalRange = new(7f, 18f);
        [SerializeField] private Vector2 thunderDelayRange = new(.35f, 1.6f);
        [SerializeField, Min(0f)] private float dayLightningIntensity = 1.8f;
        [SerializeField, Min(0f)] private float nightLightningIntensity = 3.6f;

        private WeatherState currentWeather;
        private float rainBlend;
        private float wetness;
        private float weatherTimer;
        private float lightningTimer;
        private bool isFlashing;
        private bool initialFogEnabled;
        private Color initialFogColor;
        private FogMode initialFogMode;
        private float initialFogDensity;

        public WeatherState CurrentWeather => currentWeather;
        public float RainBlend => rainBlend;
        public float Wetness => wetness;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGlobalWetness()
        {
            Shader.SetGlobalFloat(WetnessId, 0f);
            Shader.SetGlobalFloat(RainIntensityId, 0f);
            Shader.SetGlobalFloat(TrackDistanceId, 0f);
        }

        private void Awake()
        {
            initialFogEnabled = RenderSettings.fog;
            initialFogColor = RenderSettings.fogColor;
            initialFogMode = RenderSettings.fogMode;
            initialFogDensity = RenderSettings.fogDensity;

            if (followTarget == null && Camera.main != null)
                followTarget = Camera.main.transform;

            PrepareSceneComponents();
            ApplyTuningGlobals();
        }

        private void OnEnable()
        {
            DayNightCycle.NightStateChanged += OnNightStateChanged;
        }

        private void Start()
        {
            WeatherState initial = randomizeOnStart ? PickWeatherForCurrentLight() : startingWeather;
            SetWeather(initial, true);
            ScheduleLightning();
        }

        private void OnDisable()
        {
            DayNightCycle.NightStateChanged -= OnNightStateChanged;
            StopAllCoroutines();
            isFlashing = false;

            if (lightningLight != null)
            {
                lightningLight.intensity = 0f;
                lightningLight.enabled = false;
            }

            if (rainAudioSource != null)
                rainAudioSource.Stop();

            RestoreFog();
            ResetGlobalWetness();
        }

        private void Update()
        {
            float delta = Time.deltaTime;
            UpdateWeatherSchedule(delta);
            UpdateRainAndWetness(delta);
            UpdateAudio();
            UpdateLightning(delta);
            UpdateFog();
        }

        private void LateUpdate()
        {
            // Chunks move backward while the player stays near the origin. Adding the exact
            // accumulated scroll distance makes procedural puddles remain glued to each chunk.
            Shader.SetGlobalFloat(TrackDistanceId, gameplay != null ? gameplay.Distance : 0f);

            if (followTarget == null || rainParticles == null) return;

            Vector3 flatForward = Vector3.ProjectOnPlane(followTarget.forward, Vector3.up).normalized;
            if (flatForward.sqrMagnitude < .01f) flatForward = Vector3.forward;
            rainParticles.transform.position = followTarget.position
                + Vector3.up * rainFollowOffset.y
                + flatForward * rainFollowOffset.z
                + followTarget.right * rainFollowOffset.x;
        }

        [ContextMenu("Thoi tiet/Clear")]
        public void SetClear()
        {
            SetWeather(WeatherState.Clear, false);
        }

        [ContextMenu("Thoi tiet/Rain")]
        public void SetRain()
        {
            SetWeather(WeatherState.Rain, false);
        }

        [ContextMenu("Thoi tiet/Randomize Now")]
        public void RandomizeNow()
        {
            SetWeather(PickWeatherForCurrentLight(), false);
        }

        public void ConfigureSceneReferences(
            Transform cameraTarget,
            ParticleSystem rain,
            Light lightning,
            AudioSource rainSource,
            AudioSource thunderSource)
        {
            followTarget = cameraTarget;
            rainParticles = rain;
            lightningLight = lightning;
            rainAudioSource = rainSource;
            thunderAudioSource = thunderSource;
        }

        private void PrepareSceneComponents()
        {
            if (rainParticles != null)
            {
                var emission = rainParticles.emission;
                emission.rateOverTime = 0f;
                if (!rainParticles.isPlaying) rainParticles.Play(true);
            }

            if (lightningLight != null)
            {
                lightningLight.intensity = 0f;
                lightningLight.enabled = false;
            }

            if (rainAudioSource != null)
            {
                rainAudioSource.playOnAwake = false;
                rainAudioSource.loop = true;
                rainAudioSource.clip = rainLoop;
                rainAudioSource.volume = 0f;
            }

            if (thunderAudioSource != null)
            {
                thunderAudioSource.playOnAwake = false;
                thunderAudioSource.loop = false;
            }
        }

        private void SetWeather(WeatherState state, bool immediate)
        {
            currentWeather = state;
            weatherTimer = RandomDurationFor(state);

            if (!immediate) return;

            rainBlend = state == WeatherState.Rain ? 1f : 0f;
            wetness = rainBlend;
            ApplyRainVisuals();
            UpdateFog();
        }

        private void UpdateWeatherSchedule(float delta)
        {
            if (!randomWeather) return;

            weatherTimer -= delta;
            if (weatherTimer > 0f) return;

            WeatherState next = currentWeather == WeatherState.Rain
                ? WeatherState.Clear
                : PickWeatherForCurrentLight();
            SetWeather(next, false);
        }

        private WeatherState PickWeatherForCurrentLight()
        {
            float chance = DayNightCycle.IsNight ? nightRainChance : dayRainChance;
            return Random.value <= chance ? WeatherState.Rain : WeatherState.Clear;
        }

        private float RandomDurationFor(WeatherState state)
        {
            Vector2 range = state == WeatherState.Rain ? rainDurationRange : clearDurationRange;
            float minimum = Mathf.Max(1f, Mathf.Min(range.x, range.y));
            float maximum = Mathf.Max(minimum, Mathf.Max(range.x, range.y));
            return Random.Range(minimum, maximum);
        }

        private void UpdateRainAndWetness(float delta)
        {
            float targetRain = currentWeather == WeatherState.Rain ? 1f : 0f;
            rainBlend = Mathf.MoveTowards(rainBlend, targetRain, delta / Mathf.Max(.1f, transitionDuration));

            float wetTarget = targetRain;
            float wetDuration = wetTarget > wetness ? wetBuildUpSeconds : dryOutSeconds;
            wetness = Mathf.MoveTowards(wetness, wetTarget, delta / Mathf.Max(.1f, wetDuration));
            ApplyRainVisuals();
        }

        private void ApplyRainVisuals()
        {
            if (rainParticles != null)
            {
                var emission = rainParticles.emission;
                emission.rateOverTime = maximumEmissionRate * rainBlend;
            }

            Shader.SetGlobalFloat(WetnessId, wetness);
            Shader.SetGlobalFloat(RainIntensityId, rainBlend);
        }

        private void ApplyTuningGlobals()
        {
            Shader.SetGlobalFloat(PuddleAmountId, puddleAmount);
            Shader.SetGlobalFloat(WetDarkeningId, wetDarkening);
            Shader.SetGlobalFloat(PuddleScaleId, puddleWorldScale);
            Shader.SetGlobalFloat(RippleStrengthId, rippleStrength);
        }

        private void UpdateAudio()
        {
            if (rainAudioSource == null) return;

            if (rainAudioSource.clip != rainLoop)
                rainAudioSource.clip = rainLoop;

            float profileVolume = EmergencyRoadProfile.Current.sfxVolume;
            rainAudioSource.volume = rainBlend * rainVolume * profileVolume;

            if (rainAudioSource.clip != null && rainBlend > .01f && !rainAudioSource.isPlaying)
                rainAudioSource.Play();
            else if (rainBlend <= .001f && rainAudioSource.isPlaying)
                rainAudioSource.Stop();
        }

        private void UpdateLightning(float delta)
        {
            if (!lightningEnabled || isFlashing || currentWeather != WeatherState.Rain || rainBlend < .7f)
                return;

            lightningTimer -= delta;
            if (lightningTimer > 0f) return;

            StartCoroutine(FlashLightning());
            ScheduleLightning();
        }

        private IEnumerator FlashLightning()
        {
            isFlashing = true;
            int flashes = Random.Range(1, 4);
            float peak = DayNightCycle.IsNight ? nightLightningIntensity : dayLightningIntensity;

            for (int flash = 0; flash < flashes; flash++)
            {
                if (lightningLight != null)
                {
                    lightningLight.enabled = true;
                    lightningLight.intensity = peak * Random.Range(.72f, 1.08f);
                }

                yield return new WaitForSeconds(Random.Range(.035f, .075f));

                if (lightningLight != null)
                    lightningLight.intensity = 0f;

                if (flash + 1 < flashes)
                    yield return new WaitForSeconds(Random.Range(.045f, .13f));
            }

            if (lightningLight != null)
                lightningLight.enabled = false;

            StartCoroutine(PlayThunderAfterDelay());
            isFlashing = false;
        }

        private IEnumerator PlayThunderAfterDelay()
        {
            float minimum = Mathf.Max(0f, Mathf.Min(thunderDelayRange.x, thunderDelayRange.y));
            float maximum = Mathf.Max(minimum, Mathf.Max(thunderDelayRange.x, thunderDelayRange.y));
            yield return new WaitForSeconds(Random.Range(minimum, maximum));

            if (thunderAudioSource == null || thunderClips == null || thunderClips.Length == 0)
                yield break;

            AudioClip clip = thunderClips[Random.Range(0, thunderClips.Length)];
            if (clip != null)
                thunderAudioSource.PlayOneShot(clip, thunderVolume * EmergencyRoadProfile.Current.sfxVolume);
        }

        private void ScheduleLightning()
        {
            float minimum = Mathf.Max(.5f, Mathf.Min(lightningIntervalRange.x, lightningIntervalRange.y));
            float maximum = Mathf.Max(minimum, Mathf.Max(lightningIntervalRange.x, lightningIntervalRange.y));
            lightningTimer = Random.Range(minimum, maximum);
        }

        private void UpdateFog()
        {
            if (!controlRainFog) return;

            RenderSettings.fog = initialFogEnabled || rainBlend > .001f;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(initialFogColor, rainFogColor, rainBlend);
            RenderSettings.fogDensity = Mathf.Lerp(initialFogDensity, rainFogDensity, rainBlend);
        }

        private void RestoreFog()
        {
            if (!controlRainFog) return;

            RenderSettings.fog = initialFogEnabled;
            RenderSettings.fogColor = initialFogColor;
            RenderSettings.fogMode = initialFogMode;
            RenderSettings.fogDensity = initialFogDensity;
        }

        private void OnNightStateChanged(bool isNight)
        {
            if (randomWeather && currentWeather == WeatherState.Clear && weatherTimer <= 2f)
                weatherTimer = 2f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            clearDurationRange.x = Mathf.Max(1f, clearDurationRange.x);
            clearDurationRange.y = Mathf.Max(clearDurationRange.x, clearDurationRange.y);
            rainDurationRange.x = Mathf.Max(1f, rainDurationRange.x);
            rainDurationRange.y = Mathf.Max(rainDurationRange.x, rainDurationRange.y);
            lightningIntervalRange.x = Mathf.Max(.5f, lightningIntervalRange.x);
            lightningIntervalRange.y = Mathf.Max(lightningIntervalRange.x, lightningIntervalRange.y);
            ApplyTuningGlobals();
        }
#endif
    }
}
