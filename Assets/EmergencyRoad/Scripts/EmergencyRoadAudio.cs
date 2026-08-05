using UnityEngine;

namespace EmergencyRoad
{
    public sealed class EmergencyRoadAudio : MonoBehaviour
    {
        public static EmergencyRoadAudio Instance { get; private set; }
        private AudioSource music;
        private AudioSource sfx;
        private AudioClip clickClip;
        private AudioClip coinClip;
        private AudioClip hornClip;
        private AudioClip crashClip;

        public static void Ensure(EmergencyRoadCatalog catalog=null)
        {
            if (Instance != null) { Instance.Configure(catalog); return; }
            var go = new GameObject("Audio Service");
            DontDestroyOnLoad(go);
            Instance = go.AddComponent<EmergencyRoadAudio>();
            Instance.Configure(catalog);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            music = gameObject.AddComponent<AudioSource>();
            sfx = gameObject.AddComponent<AudioSource>();
            var catalog=Resources.Load<EmergencyRoadCatalog>("EmergencyRoadCatalog");
            music.loop = true;
            music.clip = catalog!=null&&catalog.musicClip!=null?catalog.musicClip:CreateTone("City Pulse", 110f, 4f, true);
            clickClip = catalog!=null&&catalog.uiClickClip!=null?catalog.uiClickClip:CreateTone("Click", 520f, .06f, false);
            coinClip = catalog!=null&&catalog.coinClip!=null?catalog.coinClip:CreateTone("Coin", 880f, .12f, false);
            hornClip = catalog!=null&&catalog.hornClip!=null?catalog.hornClip:CreateTone("Horn", 260f, .28f, false);
            crashClip = catalog!=null&&catalog.crashClip!=null?catalog.crashClip:CreateTone("Crash", 75f, .45f, false);
            ApplyVolumes();
            music.Play();
        }

        public void Configure(EmergencyRoadCatalog catalog)
        {
            if(catalog==null||music==null||sfx==null)return;music.clip=catalog.musicClip!=null?catalog.musicClip:music.clip;clickClip=catalog.uiClickClip!=null?catalog.uiClickClip:clickClip;coinClip=catalog.coinClip!=null?catalog.coinClip:coinClip;hornClip=catalog.hornClip!=null?catalog.hornClip:hornClip;crashClip=catalog.crashClip!=null?catalog.crashClip:crashClip;if(!music.isPlaying)music.Play();ApplyVolumes();
        }

        public void ApplyVolumes()
        {
            music.volume = EmergencyRoadProfile.Current.musicVolume * .25f;
            sfx.volume = EmergencyRoadProfile.Current.sfxVolume;
        }

        public void Click() => sfx.PlayOneShot(clickClip, .45f);
        public void Coin() => sfx.PlayOneShot(coinClip, .65f);
        public void Horn() => sfx.PlayOneShot(hornClip, .85f);
        public void Crash() => sfx.PlayOneShot(crashClip, 1f);

        private static AudioClip CreateTone(string title, float frequency, float duration, bool musical)
        {
            const int rate = 22050;
            int count = Mathf.CeilToInt(rate * duration);
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / rate;
                float f = musical ? frequency * (1f + Mathf.Floor(t * 2f) % 4f * .25f) : frequency;
                float envelope = musical ? .7f : Mathf.Clamp01(1f - t / duration);
                samples[i] = Mathf.Sin(2f * Mathf.PI * f * t) * envelope * .25f;
            }
            var clip = AudioClip.Create(title, count, 1, rate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
