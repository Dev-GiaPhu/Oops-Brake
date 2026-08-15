using UnityEngine;

namespace EmergencyRoad
{
    [DisallowMultipleComponent]
    public sealed class EmergencyRoadAudio : MonoBehaviour
    {
        public static EmergencyRoadAudio Instance { get; private set; }

        [Header("AUDIO SOURCES - DRAG FROM THIS SCENE")]
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource sfxSource;

        // Shared clips have one authoritative source: EmergencyRoadCatalog.
        private AudioClip backgroundMusic;
        private AudioClip buttonClickSound;
        private AudioClip coinSound;
        private AudioClip defaultHornSound;
        private AudioClip collisionSound;

        public AudioSource MusicSource => musicSource;
        public AudioSource SfxSource => sfxSource;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[Emergency Road] Có nhiều EmergencyRoadAudio trong scene. Chỉ giữ một Audio Service được author trực tiếp trong scene.", this);
                enabled = false;
                return;
            }

            Instance = this;
            ValidateReferences();
            ApplyVolumes();

            if (musicSource != null)
            {
                musicSource.loop = true;
                if (backgroundMusic != null) musicSource.clip = backgroundMusic;
                if (musicSource.clip != null && !musicSource.isPlaying) musicSource.Play();
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void ConfigureFromCatalog(EmergencyRoadCatalog catalog)
        {
            if (catalog == null) return;
            backgroundMusic = catalog.musicClip;
            buttonClickSound = catalog.uiClickClip;
            coinSound = catalog.coinClip;
            defaultHornSound = catalog.hornClip;
            collisionSound = catalog.crashClip;

            if (musicSource != null)
            {
                musicSource.clip = backgroundMusic;
                musicSource.loop = true;
                if (musicSource.clip != null && !musicSource.isPlaying) musicSource.Play();
            }
        }

        public void ConfigureSources(AudioSource music, AudioSource sfx)
        {
            musicSource = music;
            sfxSource = sfx;
        }

        private void ValidateReferences()
        {
            if (musicSource == null)
                Debug.LogError("[Emergency Road] EmergencyRoadAudio thiếu Music Source. Kéo AudioSource từ scene vào Inspector.", this);
            if (sfxSource == null)
                Debug.LogError("[Emergency Road] EmergencyRoadAudio thiếu SFX Source. Kéo AudioSource từ scene vào Inspector.", this);
        }

        public void ApplyVolumes()
        {
            if (musicSource != null)
                musicSource.volume = EmergencyRoadProfile.Current.musicVolume * 0.25f;
            if (sfxSource != null)
                sfxSource.volume = EmergencyRoadProfile.Current.sfxVolume;
        }

        public void Click()
        {
            if (sfxSource != null && buttonClickSound != null)
                sfxSource.PlayOneShot(buttonClickSound, 0.45f);
        }

        public void Coin()
        {
            if (sfxSource != null && coinSound != null)
                sfxSource.PlayOneShot(coinSound, 0.65f);
        }

        public double Horn(AudioClip vehicleHorn = null, int vehicleIndex = -1)
        {
            AudioClip clip = vehicleHorn != null ? vehicleHorn : defaultHornSound;
            if (sfxSource != null && clip != null)
            {
                sfxSource.PlayOneShot(clip, 0.85f);
                return AudioSettings.dspTime + clip.length / Mathf.Max(.01f, Mathf.Abs(sfxSource.pitch));
            }
            return AudioSettings.dspTime + .25d;
        }

        public void Crash()
        {
            if (sfxSource != null && collisionSound != null)
                sfxSource.PlayOneShot(collisionSound, 1f);
        }
    }
}
