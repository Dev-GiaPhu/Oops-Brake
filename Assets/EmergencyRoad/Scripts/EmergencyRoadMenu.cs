using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace EmergencyRoad
{
    [Serializable]
    public sealed class EmergencyRoadMenuVehicleEntry
    {
        [Tooltip("Prefab xe. Kéo trực tiếp prefab vào đây.")]
        public GameObject prefab;

        [Tooltip("Tên hiển thị trong garage.")]
        public string displayName = "XE MỚI";

        [Min(0)]
        [Tooltip("Giá mở khóa. 0 = miễn phí.")]
        public int price;

        [Tooltip("Âm còi riêng của xe. Có thể để trống.")]
        public AudioClip hornClip;

        [Header("GARAGE PREVIEW TRANSFORM")]
        [Tooltip("Vị trí local của xe so với tâm bàn xoay.")]
        public Vector3 previewLocalPosition = Vector3.zero;

        [Tooltip("Góc local của xe so với tâm bàn xoay.")]
        public Vector3 previewLocalEuler = Vector3.zero;

        [Min(.01f)]
        [Tooltip("Scale riêng khi hiện trong garage.")]
        public float previewScale = 1f;
    }

    [DisallowMultipleComponent]
    public sealed class EmergencyRoadMenu : MonoBehaviour
    {
        [Header("SCENE REFERENCES - DRAG DIRECTLY")]
        [SerializeField] private EmergencyRoadMenuView sceneView;
        [SerializeField] private EmergencyRoadAudio audioService;

        [Header("VEHICLES - DRAG DIRECTLY")]
        [SerializeField]
        [Tooltip("Danh sách xe dùng trực tiếp. Runtime không đọc EmergencyRoadCatalog.")]
        private List<EmergencyRoadMenuVehicleEntry> vehicles = new();

        [Header("PREVIEW")]
        [SerializeField, Min(0f)] private float previewRotationSpeed = 24f;

        private GameObject preview;
        private int index;
        private bool initialized;

        public EmergencyRoadMenuView SceneView => sceneView;
        public IReadOnlyList<EmergencyRoadMenuVehicleEntry> Vehicles => vehicles;

        private void Start()
        {
            InitializeAuthoredMenu();
        }

        /// <summary>
        /// Chỉ được Editor authoring gọi để ghi reference trực tiếp vào scene.
        /// Catalog chỉ được dùng một lần để migrate dữ liệu cũ sang list trực tiếp,
        /// không được giữ hoặc đọc ở runtime.
        /// </summary>
        public void ConfigureSceneReferences(
            EmergencyRoadCatalog sourceCatalog,
            EmergencyRoadMenuView view,
            Transform previewPivot,
            EmergencyRoadAudio audio)
        {
            sceneView = view;
            audioService = audio;

            if (sceneView != null && previewPivot != null)
                sceneView.vehiclePreviewPivot = previewPivot;

            if ((vehicles == null || vehicles.Count == 0) && sourceCatalog != null)
            {
                sourceCatalog.SynchronizePlayerVehicleData();
                vehicles = new List<EmergencyRoadMenuVehicleEntry>();

                for (int i = 0; i < sourceCatalog.PlayerVehicleCount; i++)
                {
                    vehicles.Add(new EmergencyRoadMenuVehicleEntry
                    {
                        prefab = sourceCatalog.PlayerVehiclePrefab(i),
                        displayName = sourceCatalog.PlayerVehicleName(i),
                        price = sourceCatalog.PlayerVehiclePrice(i),
                        hornClip = sourceCatalog.HornForVehicle(i),
                        previewLocalPosition = Vector3.zero,
                        previewLocalEuler = Vector3.zero,
                        previewScale = 1f
                    });
                }
            }

#if UNITY_EDITOR
            EditorUtility.SetDirty(this);
            if (sceneView != null) EditorUtility.SetDirty(sceneView);
            if (gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(gameObject.scene);
#endif
        }

        public void InitializeAuthoredMenu()
        {
            if (initialized) return;
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            initialized = true;
            if (audioService != null)
                audioService.ApplyVolumes();

            BindSceneUI();
            index = ResolveOwnedSelection();

            if (EmergencyRoadProfile.Current.selectedVehicle != index)
            {
                EmergencyRoadProfile.Current.selectedVehicle = index;
                EmergencyRoadProfile.Save();
            }

            Select(index);
        }

        private bool ValidateReferences()
        {
            bool valid = true;

            if (sceneView == null)
            {
                Debug.LogError("[Emergency Road] Menu Controller thiếu Scene View. Gán EmergencyRoadMenuView trực tiếp trong Inspector.", this);
                valid = false;
            }
            else if (sceneView.vehiclePreviewPivot == null)
            {
                Debug.LogError("[Emergency Road] EmergencyRoadMenuView thiếu Vehicle Preview Pivot. Pivot phải là Empty GameObject nằm đúng tâm bàn xoay.", sceneView);
                valid = false;
            }

            if (vehicles == null || vehicles.Count == 0)
            {
                Debug.LogError("[Emergency Road] Menu Controller chưa có Vehicles. Kéo prefab xe trực tiếp vào list Vehicles trong Inspector.", this);
                valid = false;
            }

            return valid;
        }

        private void BindSceneUI()
        {
            Bind(sceneView.previous, () => Select(index - 1));
            Bind(sceneView.next, () => Select(index + 1));
            Bind(sceneView.vehicleAction, VehicleAction);
            Bind(sceneView.play, Play);
            Bind(sceneView.settingsOpen, ToggleSettings);
            Bind(sceneView.quit, () => Application.Quit());
            Bind(sceneView.sideCollision, ToggleSideCollision);
            Bind(sceneView.selectVehicle, OpenGarage);
            Bind(sceneView.garageBack, CloseGarage);
            Bind(sceneView.controlsOpen, () =>
            {
                if (sceneView.settingsPanel != null) sceneView.settingsPanel.SetActive(false);
                if (sceneView.controlsPanel != null) sceneView.controlsPanel.SetActive(true);
            });
            Bind(sceneView.settingsClose, ToggleSettings);
            Bind(sceneView.controlsBack, () =>
            {
                if (sceneView.controlsPanel != null) sceneView.controlsPanel.SetActive(false);
                if (sceneView.settingsPanel != null) sceneView.settingsPanel.SetActive(true);
            });

            Bind(sceneView.music, value =>
            {
                EmergencyRoadProfile.Current.musicVolume = value;
                if (audioService != null) audioService.ApplyVolumes();
                EmergencyRoadProfile.Save();
            }, EmergencyRoadProfile.Current.musicVolume);

            Bind(sceneView.sfx, value =>
            {
                EmergencyRoadProfile.Current.sfxVolume = value;
                if (audioService != null) audioService.ApplyVolumes();
                EmergencyRoadProfile.Save();
            }, EmergencyRoadProfile.Current.sfxVolume);

            if (sceneView.mainMenuPanel != null) sceneView.mainMenuPanel.SetActive(true);
            if (sceneView.garagePanel != null) sceneView.garagePanel.SetActive(false);
            if (sceneView.settingsPanel != null) sceneView.settingsPanel.SetActive(false);
            if (sceneView.controlsPanel != null) sceneView.controlsPanel.SetActive(false);

            RefreshSideCollisionLabel();
        }

        private void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() =>
            {
                if (audioService != null) audioService.Click();
                action?.Invoke();
            });
        }

        private static void Bind(Slider slider, UnityEngine.Events.UnityAction<float> action, float value)
        {
            if (slider == null) return;
            slider.onValueChanged.RemoveAllListeners();
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(action);
        }

        private void Update()
        {
            if (!initialized || sceneView == null || sceneView.vehiclePreviewPivot == null) return;
            sceneView.vehiclePreviewPivot.Rotate(0f, previewRotationSpeed * Time.unscaledDeltaTime, 0f, Space.Self);
        }

        private void OpenGarage()
        {
            Select(ResolveOwnedSelection());
            if (sceneView.mainMenuPanel != null) sceneView.mainMenuPanel.SetActive(false);
            if (sceneView.garagePanel != null) sceneView.garagePanel.SetActive(true);
        }

        private void CloseGarage()
        {
            Select(ResolveOwnedSelection());
            if (sceneView.garagePanel != null) sceneView.garagePanel.SetActive(false);
            if (sceneView.mainMenuPanel != null) sceneView.mainMenuPanel.SetActive(true);
        }

        private void ToggleSettings()
        {
            if (sceneView.settingsPanel != null)
                sceneView.settingsPanel.SetActive(!sceneView.settingsPanel.activeSelf);

            if (sceneView.controlsPanel != null)
                sceneView.controlsPanel.SetActive(false);
        }

        private void ToggleSideCollision()
        {
            EmergencyRoadProfile.Current.sideCollisionEnabled = !EmergencyRoadProfile.Current.sideCollisionEnabled;
            EmergencyRoadProfile.Save();
            RefreshSideCollisionLabel();
        }

        private void RefreshSideCollisionLabel()
        {
            if (sceneView.sideCollisionLabel != null)
                sceneView.sideCollisionLabel.text = EmergencyRoadProfile.Current.sideCollisionEnabled ? "BẬT" : "TẮT";
        }

        private void Select(int next)
        {
            int count = vehicles.Count;
            if (count == 0) return;

            index = (next % count + count) % count;

            if (preview != null)
            {
                Destroy(preview);
                preview = null;
            }

            EmergencyRoadMenuVehicleEntry entry = vehicles[index];
            if (entry != null && entry.prefab != null)
            {
                preview = Instantiate(entry.prefab, sceneView.vehiclePreviewPivot, false);
                preview.name = $"Preview - {entry.prefab.name}";
                preview.transform.localPosition = entry.previewLocalPosition;
                preview.transform.localRotation = Quaternion.Euler(entry.previewLocalEuler);
                preview.transform.localScale = Vector3.one * Mathf.Max(.01f, entry.previewScale);
            }

            if (sceneView.vehicleName != null)
            {
                sceneView.vehicleName.text = entry != null && !string.IsNullOrWhiteSpace(entry.displayName)
                    ? entry.displayName
                    : $"XE {index + 1}";
            }

            if (sceneView.wallet != null)
                sceneView.wallet.text = $"● {EmergencyRoadProfile.Current.coins:N0}";

            bool unlocked = EmergencyRoadProfile.IsUnlocked(index);
            int unlockPrice = entry != null ? Mathf.Max(0, entry.price) : 0;
            bool selected = unlocked && EmergencyRoadProfile.Current.selectedVehicle == index;

            if (sceneView.price != null)
            {
                sceneView.price.text = unlocked
                    ? (selected ? "ĐÃ SỞ HỮU • ĐANG DÙNG" : "ĐÃ SỞ HỮU • SẴN SÀNG")
                    : $"CHƯA SỞ HỮU • GIÁ  ● {unlockPrice:N0}";
            }

            if (sceneView.vehicleActionLabel != null)
                sceneView.vehicleActionLabel.text = unlocked ? (selected ? "ĐANG DÙNG" : "CHỌN XE") : "MỞ KHÓA";

            if (sceneView.vehicleAction != null)
                sceneView.vehicleAction.interactable = entry != null && entry.prefab != null;
        }

        private int ResolveOwnedSelection()
        {
            if (vehicles == null || vehicles.Count == 0) return 0;

            int requested = Mathf.Clamp(EmergencyRoadProfile.Current.selectedVehicle, 0, vehicles.Count - 1);
            if (EmergencyRoadProfile.IsUnlocked(requested) && HasVehiclePrefab(requested))
                return requested;

            for (int candidate = 0; candidate < vehicles.Count; candidate++)
            {
                if (EmergencyRoadProfile.IsUnlocked(candidate) && HasVehiclePrefab(candidate))
                    return candidate;
            }

            return 0;
        }

        private bool HasVehiclePrefab(int vehicleIndex)
        {
            return vehicleIndex >= 0 && vehicleIndex < vehicles.Count &&
                   vehicles[vehicleIndex] != null && vehicles[vehicleIndex].prefab != null;
        }

        private void VehicleAction()
        {
            if (!HasVehiclePrefab(index))
            {
                if (sceneView.price != null) sceneView.price.text = "CHƯA GÁN PREFAB XE";
                return;
            }

            int unlockPrice = Mathf.Max(0, vehicles[index].price);
            if (!EmergencyRoadProfile.TryUnlock(index, unlockPrice))
            {
                if (sceneView.price != null) sceneView.price.text = "KHÔNG ĐỦ TIỀN";
                return;
            }

            EmergencyRoadProfile.Current.selectedVehicle = index;
            EmergencyRoadProfile.Save();
            Select(index);
        }

        private void Play()
        {
            int selected = ResolveOwnedSelection();
            EmergencyRoadProfile.Current.selectedVehicle = selected;
            EmergencyRoadProfile.Save();
            SceneManager.LoadScene("Game");
        }
    }
}
