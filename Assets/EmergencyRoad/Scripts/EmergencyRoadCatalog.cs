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

    [Serializable]
    public sealed class EmergencyRoadPlayerVehicleDefinition
    {
        [Tooltip("Prefab xe được dùng trong gara và gameplay.")]
        public GameObject prefab;

        [Tooltip("Tên xe hiển thị trong gara.")]
        public string displayName = "XE MỚI";

        [Min(0)]
        [Tooltip("Giá mở khóa. Đặt 0 nếu xe được miễn phí.")]
        public int price;

        [Tooltip("Âm còi riêng của xe. Để trống để dùng âm còi mặc định.")]
        public AudioClip hornClip;
    }

    [CreateAssetMenu(menuName = "Emergency Road/Game Catalog", fileName = "EmergencyRoadCatalog")]
    public sealed class EmergencyRoadCatalog : ScriptableObject
    {
        private static readonly string[] LegacyVehicleNames =
        {
            "XE CỨU THƯƠNG", "XE CẢNH SÁT", "XE CỨU HỎA", "TAXI", "XE BỒN", "XE ỦI", "XE XÚC"
        };

        private static readonly int[] LegacyVehiclePrices = { 0, 350, 700, 1100, 1600, 2200, 3000 };

        [Header("Drag Gameplay Settings Asset Here")]
        public EmergencyRoadGameplaySettings gameplaySettings;

        [Header("Player Vehicles - Prefab, Name And Price")]
        [Tooltip("Mỗi phần tử là một xe hoàn chỉnh: Prefab, tên hiển thị, giá và âm còi tùy chọn.")]
        public List<EmergencyRoadPlayerVehicleDefinition> playerVehicleCatalog = new();

        // Kept for compatibility with existing gameplay and project-builder code.
        // These lists are synchronized automatically from playerVehicleCatalog.
        [HideInInspector] public List<GameObject> playerVehicles = new();
        [HideInInspector] public List<AudioClip> vehicleHornClips = new();

        [Header("Drag Prefabs Here")]
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

        public int PlayerVehicleCount
        {
            get
            {
                SynchronizePlayerVehicleData();
                return playerVehicleCatalog.Count;
            }
        }

        private void OnEnable()
        {
            SynchronizePlayerVehicleData();
        }

        private void OnValidate()
        {
            SynchronizePlayerVehicleData();
        }

        public bool SynchronizePlayerVehicleData()
        {
            bool changed = false;
            playerVehicleCatalog ??= new List<EmergencyRoadPlayerVehicleDefinition>();
            playerVehicles ??= new List<GameObject>();
            vehicleHornClips ??= new List<AudioClip>();

            // One-time migration for existing projects that used two parallel lists.
            if (playerVehicleCatalog.Count == 0 && playerVehicles.Count > 0)
            {
                for (int i = 0; i < playerVehicles.Count; i++)
                {
                    GameObject prefab = playerVehicles[i];
                    AudioClip horn = i < vehicleHornClips.Count ? vehicleHornClips[i] : null;
                    playerVehicleCatalog.Add(new EmergencyRoadPlayerVehicleDefinition
                    {
                        prefab = prefab,
                        displayName = i < LegacyVehicleNames.Length
                            ? LegacyVehicleNames[i]
                            : DefaultVehicleName(prefab, i),
                        price = i < LegacyVehiclePrices.Length
                            ? LegacyVehiclePrices[i]
                            : LegacyVehiclePrices[LegacyVehiclePrices.Length - 1],
                        hornClip = horn
                    });
                }

                changed = true;
            }

            for (int i = 0; i < playerVehicleCatalog.Count; i++)
            {
                EmergencyRoadPlayerVehicleDefinition entry = playerVehicleCatalog[i];
                if (entry == null)
                {
                    entry = new EmergencyRoadPlayerVehicleDefinition
                    {
                        displayName = DefaultVehicleName(null, i)
                    };
                    playerVehicleCatalog[i] = entry;
                    changed = true;
                }

                string normalizedName = string.IsNullOrWhiteSpace(entry.displayName)
                    ? DefaultVehicleName(entry.prefab, i)
                    : entry.displayName.Trim();

                if (entry.displayName != normalizedName)
                {
                    entry.displayName = normalizedName;
                    changed = true;
                }

                int normalizedPrice = Mathf.Max(0, entry.price);
                if (entry.price != normalizedPrice)
                {
                    entry.price = normalizedPrice;
                    changed = true;
                }
            }

            if (!LegacyListsMatchCatalog())
            {
                playerVehicles.Clear();
                vehicleHornClips.Clear();

                foreach (EmergencyRoadPlayerVehicleDefinition entry in playerVehicleCatalog)
                {
                    playerVehicles.Add(entry != null ? entry.prefab : null);
                    vehicleHornClips.Add(entry != null ? entry.hornClip : null);
                }

                changed = true;
            }

            return changed;
        }

        private bool LegacyListsMatchCatalog()
        {
            if (playerVehicles.Count != playerVehicleCatalog.Count ||
                vehicleHornClips.Count != playerVehicleCatalog.Count)
                return false;

            for (int i = 0; i < playerVehicleCatalog.Count; i++)
            {
                EmergencyRoadPlayerVehicleDefinition entry = playerVehicleCatalog[i];
                GameObject expectedPrefab = entry != null ? entry.prefab : null;
                AudioClip expectedHorn = entry != null ? entry.hornClip : null;

                if (playerVehicles[i] != expectedPrefab || vehicleHornClips[i] != expectedHorn)
                    return false;
            }

            return true;
        }

        private static string DefaultVehicleName(GameObject prefab, int index)
        {
            if (prefab != null && !string.IsNullOrWhiteSpace(prefab.name))
                return prefab.name.Replace('_', ' ').ToUpperInvariant();

            return $"XE {index + 1}";
        }

        public EmergencyRoadPlayerVehicleDefinition PlayerVehicleAt(int index)
        {
            SynchronizePlayerVehicleData();
            return index >= 0 && index < playerVehicleCatalog.Count ? playerVehicleCatalog[index] : null;
        }

        public GameObject PlayerVehiclePrefab(int index)
        {
            return PlayerVehicleAt(index)?.prefab;
        }

        public string PlayerVehicleName(int index)
        {
            EmergencyRoadPlayerVehicleDefinition entry = PlayerVehicleAt(index);
            return entry != null ? entry.displayName : DefaultVehicleName(null, index);
        }

        public int PlayerVehiclePrice(int index)
        {
            EmergencyRoadPlayerVehicleDefinition entry = PlayerVehicleAt(index);
            return entry != null ? Mathf.Max(0, entry.price) : 0;
        }

        public EmergencyRoadPrefabMetrics MetricsFor(GameObject prefab)
        {
            return crossroadMetrics.Find(x => x != null && x.prefab == prefab);
        }

        public AudioClip HornForVehicle(int index)
        {
            EmergencyRoadPlayerVehicleDefinition entry = PlayerVehicleAt(index);
            return entry != null && entry.hornClip != null ? entry.hornClip : hornClip;
        }
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
        public bool firstPersonView;
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
