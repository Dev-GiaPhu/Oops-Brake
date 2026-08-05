using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;

namespace EmergencyRoad
{
    [Serializable]
    public sealed class EmergencyRoadPrefabMetrics
    {
        public GameObject prefab;
        public Vector3 rendererSize;
        public Vector3 rendererCenter;
    }

    [CreateAssetMenu(menuName = "Emergency Road/Game Catalog", fileName = "EmergencyRoadCatalog")]
    public sealed class EmergencyRoadCatalog : ScriptableObject
    {
        [Header("Drag Gameplay Settings Asset Here")]
        public EmergencyRoadGameplaySettings gameplaySettings;
        [Header("Drag Prefabs Here")]
        public List<GameObject> playerVehicles = new();
        public List<AudioClip> vehicleHornClips = new();
        public List<GameObject> trafficVehicles = new();
        public List<GameObject> roadPrefabs = new();
        public List<GameObject> crossroadPrefabs = new();
        [Header("Measured Automatically - Do Not Guess")]
        public List<EmergencyRoadPrefabMetrics> crossroadMetrics = new();
        public List<GameObject> obstaclePrefabs = new();
        public List<GameObject> decorationPrefabs = new();
        public List<GameObject> naturePrefabs = new();
        public List<GameObject> streetDecorationPrefabs = new();
        public Material grassMaterial;
        public Material soilMaterial;
        public Material vfxParticleMaterial;
        public VolumeProfile postProcessProfile;
        public GameObject coinPrefab;
        public GameObject motorcyclePrefab;
        [Header("Drag Vietnamese UI Font Here")]
        public TMP_FontAsset uiFont;
        public Sprite panelSprite;
        public Sprite buttonSprite;
        public Sprite sliderBackgroundSprite;
        public Sprite sliderFillSprite;
        public Sprite sliderHandleSprite;
        [Header("Replaceable Audio Clips (optional - generated fallback is used when empty)")]
        public AudioClip musicClip;
        public AudioClip uiClickClip;
        public AudioClip coinClip;
        public AudioClip hornClip;
        public AudioClip crashClip;
        [Min(1f)] public float roadLength = 28f;
        [Min(1f)] public float roadHalfWidth = 5.5f;
        [Min(2f)] public float laneWidth = 5.18f;
        [Min(20f)] public float startingSafeDistance = 48f;

        public EmergencyRoadPrefabMetrics MetricsFor(GameObject prefab)=>crossroadMetrics.Find(x=>x!=null&&x.prefab==prefab);
        public AudioClip HornForVehicle(int index)=>index>=0&&index<vehicleHornClips.Count&&vehicleHornClips[index]!=null?vehicleHornClips[index]:hornClip;
    }

    [Serializable]
    public sealed class EmergencyRoadSave
    {
        public int version = 1;
        public int coins;
        public int selectedVehicle;
        public int highScore;
        public float musicVolume = 0.75f;
        public float sfxVolume = 0.9f;
        public bool sideCollisionEnabled = false;
        public List<int> unlockedVehicles = new() { 0 };
    }

    public static class EmergencyRoadProfile
    {
        private const string Key = "EmergencyRoad.Profile.v1";
        private static EmergencyRoadSave current;

        public static EmergencyRoadSave Current => current ??= Load();

        private static EmergencyRoadSave Load()
        {
            if (!PlayerPrefs.HasKey(Key)) return new EmergencyRoadSave();
            try
            {
                var value = JsonUtility.FromJson<EmergencyRoadSave>(PlayerPrefs.GetString(Key));
                if (value == null) return new EmergencyRoadSave();
                value.unlockedVehicles ??= new List<int> { 0 };
                if (!value.unlockedVehicles.Contains(0)) value.unlockedVehicles.Add(0);
                value.musicVolume = Mathf.Clamp01(value.musicVolume);
                value.sfxVolume = Mathf.Clamp01(value.sfxVolume);
                value.sideCollisionEnabled = false;
                return value;
            }
            catch { return new EmergencyRoadSave(); }
        }

        public static bool IsUnlocked(int index) => Current.unlockedVehicles.Contains(index);

        public static bool TryUnlock(int index, int price)
        {
            if (IsUnlocked(index)) return true;
            if (Current.coins < price) return false;
            Current.coins -= price;
            Current.unlockedVehicles.Add(index);
            Save();
            return true;
        }

        public static void Save()
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(Current));
            PlayerPrefs.Save();
        }
    }
}