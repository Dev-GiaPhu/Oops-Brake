using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace EmergencyRoad
{
    [DisallowMultipleComponent]
    public sealed class MenuGameManager : MonoBehaviour
    {
        [Header("SCENE REFERENCES - DRAG DIRECTLY")]
        [SerializeField] private EmergencyRoadCatalog catalog;
        [SerializeField] private EmergencyRoadMenuView sceneView;
        [SerializeField, Tooltip("Kéo Empty GameObject nằm đúng tâm bàn xoay vào đây. Không dùng model xe làm pivot.")]
        private Transform vehiclePreviewPivot;
        [SerializeField] private EmergencyRoadAudio audioService;

        [Header("PREVIEW")]
        [SerializeField, Min(0.1f)] private float previewTargetFootprint = 6f;

        private GameObject preview;
        private int index;
        private bool initialized;

        public EmergencyRoadCatalog Catalog => catalog;
        public EmergencyRoadMenuView SceneView => sceneView;
        public Transform VehiclePreviewPivot => vehiclePreviewPivot;

        private void Start() => InitializeAuthoredMenu();

        public void ConfigureSceneReferences(EmergencyRoadCatalog data, EmergencyRoadMenuView view, Transform previewPivot, EmergencyRoadAudio audio)
        {
            catalog = data;
            sceneView = view;
            vehiclePreviewPivot = previewPivot;
            audioService = audio;
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
            catalog.SynchronizePlayerVehicleData();
            EmergencyRoadUI.SetFont(catalog.uiFont);
            audioService.ConfigureFromCatalog(catalog);
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
            if (catalog == null) { Debug.LogError("[Emergency Road] Menu Controller thiếu Catalog. Kéo EmergencyRoadCatalog.asset vào Inspector.", this); valid = false; }
            if (sceneView == null) { Debug.LogError("[Emergency Road] Menu Controller thiếu Menu View. Kéo EmergencyRoadMenuView trong scene vào Inspector.", this); valid = false; }
            if (vehiclePreviewPivot == null) { Debug.LogError("[Emergency Road] Menu Controller thiếu Vehicle Preview Pivot. Kéo Empty GameObject ở tâm bàn xoay vào Inspector.", this); valid = false; }
            if (audioService == null) { Debug.LogError("[Emergency Road] Menu Controller thiếu Audio Service. Kéo EmergencyRoadAudio trong scene vào Inspector.", this); valid = false; }
            return valid;
        }

        private void BindSceneUI()
        {
            Bind(sceneView.previous, () => Select(index - 1));
            Bind(sceneView.next, () => Select(index + 1));
            Bind(sceneView.vehicleAction, VehicleAction);
            Bind(sceneView.play, Play);
            Bind(sceneView.settingsOpen, OpenSettings);
            Bind(sceneView.quit, Application.Quit);
            Bind(sceneView.sideCollision, ToggleSideCollision);
            Bind(sceneView.selectVehicle, OpenGarage);
            Bind(sceneView.garageBack, CloseGarage);
            Bind(sceneView.controlsOpen, OpenControls);
            Bind(sceneView.settingsClose, CloseSettings);
            Bind(sceneView.controlsBack, BackToSettings);
            Bind(sceneView.music, v =>
            {
                EmergencyRoadProfile.Current.musicVolume = v;
                audioService.ApplyVolumes();
                EmergencyRoadProfile.Save();
            }, EmergencyRoadProfile.Current.musicVolume);
            Bind(sceneView.sfx, v =>
            {
                EmergencyRoadProfile.Current.sfxVolume = v;
                audioService.ApplyVolumes();
                EmergencyRoadProfile.Save();
            }, EmergencyRoadProfile.Current.sfxVolume);

            ShowOnlyContent(sceneView.mainMenuPanel, true);
            RefreshSideCollisionLabel();
        }

        private void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { audioService.Click(); action(); });
        }

        private static void Bind(Slider slider, UnityEngine.Events.UnityAction<float> action, float value)
        {
            if (slider == null) return;
            slider.onValueChanged.RemoveAllListeners();
            slider.SetValueWithoutNotify(value);
            slider.onValueChanged.AddListener(action);
        }

        private void OpenGarage()
        {
            Select(ResolveOwnedSelection());
            ShowOnlyContent(sceneView.garagePanel);
        }

        private void CloseGarage()
        {
            Select(ResolveOwnedSelection());
            ShowOnlyContent(sceneView.mainMenuPanel);
        }

        private void OpenSettings()
        {
            ShowOnlyContent(sceneView.settingsPanel);
        }

        private void CloseSettings()
        {
            ShowOnlyContent(sceneView.mainMenuPanel);
        }

        private void OpenControls()
        {
            ShowOnlyContent(sceneView.controlsPanel);
        }

        private void BackToSettings()
        {
            ShowOnlyContent(sceneView.settingsPanel);
        }

        /// <summary>
        /// Chỉ đổi các content panel của Menu. Top Bar/HUD nằm ngoài nhóm này nên
        /// luôn được giữ lại khi mở Garage, Settings hoặc Controls.
        /// </summary>
        private void ShowOnlyContent(GameObject targetPanel, bool immediate = false)
        {
            SetPanel(sceneView.mainMenuPanel, targetPanel == sceneView.mainMenuPanel, immediate);
            SetPanel(sceneView.garagePanel, targetPanel == sceneView.garagePanel, immediate);
            SetPanel(sceneView.settingsPanel, targetPanel == sceneView.settingsPanel, immediate);
            SetPanel(sceneView.controlsPanel, targetPanel == sceneView.controlsPanel, immediate);
        }

        private static bool PanelIsVisible(GameObject panel)
        {
            if (panel == null) return false;
            EmergencyPanelTransition transition = panel.GetComponent<EmergencyPanelTransition>();
            return transition != null ? transition.TargetVisible : panel.activeSelf;
        }

        private static void SetPanel(GameObject panel, bool visible, bool immediate = false)
        {
            if (panel == null) return;
            EmergencyPanelTransition transition = panel.GetComponent<EmergencyPanelTransition>();
            if (transition != null)
                transition.SetVisible(visible, immediate);
            else
                panel.SetActive(visible);
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
                sceneView.sideCollisionLabel.text = EmergencyRoadProfile.Current.sideCollisionEnabled ? "ON" : "OFF";
        }

        private void Select(int next)
        {
            int vehicleCount = catalog.PlayerVehicleCount;
            if (vehicleCount == 0) return;

            index = (next + vehicleCount) % vehicleCount;
            if (preview != null) Destroy(preview);

            GameObject vehiclePrefab = catalog.PlayerVehiclePrefab(index);
            if (vehiclePrefab != null)
            {
                preview = Instantiate(vehiclePrefab, vehiclePreviewPivot, false);
                preview.name = $"Preview - {vehiclePrefab.name}";
                preview.transform.localPosition = Vector3.zero;
                preview.transform.localRotation = Quaternion.identity;
                preview.transform.localScale = Vector3.one;
                NormalizePreview(preview, previewTargetFootprint);
            }

            if (sceneView.vehicleName != null) sceneView.vehicleName.text = catalog.PlayerVehicleName(index);
            if (sceneView.wallet != null) sceneView.wallet.text = $"{EmergencyRoadProfile.Current.coins:N0}";

            bool unlocked = EmergencyRoadProfile.IsUnlocked(index);
            int price = catalog.PlayerVehiclePrice(index);
            bool selected = unlocked && EmergencyRoadProfile.Current.selectedVehicle == index;

            if (sceneView.price != null)
                sceneView.price.text = unlocked
                    ? (selected ? "OWNED • EQUIPPED" : "OWNED • READY")
                    : $"LOCKED • PRICE  ● {price:N0}";

            if (sceneView.vehicleActionLabel != null)
                sceneView.vehicleActionLabel.text = unlocked ? (selected ? "EQUIPPED" : "SELECT") : "UNLOCK";
            if (sceneView.vehicleAction != null)
                sceneView.vehicleAction.interactable = vehiclePrefab != null;
        }

        private static void NormalizePreview(GameObject vehicle, float targetFootprint)
        {
            Renderer[] renderers = vehicle.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float footprint = Mathf.Max(.01f, Mathf.Max(bounds.size.x, bounds.size.z));
            vehicle.transform.localScale *= targetFootprint / footprint;

            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Transform pivot = vehicle.transform.parent;
            Vector3 centerLocal = pivot.InverseTransformPoint(bounds.center);
            // Chỉ căn giữa trên bàn xoay. Độ cao Y do từng prefab tự cấu hình để
            // bánh xe, xích xe và gầu xúc tiếp xúc mặt sàn đúng theo model riêng.
            vehicle.transform.localPosition += new Vector3(-centerLocal.x, 0f, -centerLocal.z);
        }

        private int ResolveOwnedSelection()
        {
            int vehicleCount = catalog.PlayerVehicleCount;
            if (vehicleCount == 0) return 0;
            int requested = Mathf.Clamp(EmergencyRoadProfile.Current.selectedVehicle, 0, vehicleCount - 1);
            if (EmergencyRoadProfile.IsUnlocked(requested) && catalog.PlayerVehiclePrefab(requested) != null) return requested;
            for (int candidate = 0; candidate < vehicleCount; candidate++)
                if (EmergencyRoadProfile.IsUnlocked(candidate) && catalog.PlayerVehiclePrefab(candidate) != null) return candidate;
            return 0;
        }

        private void VehicleAction()
        {
            if (catalog.PlayerVehiclePrefab(index) == null)
            {
                if (sceneView.price != null) sceneView.price.text = "VEHICLE PREFAB MISSING";
                return;
            }
            int price = catalog.PlayerVehiclePrice(index);
            if (!EmergencyRoadProfile.TryUnlock(index, price))
            {
                if (sceneView.price != null) sceneView.price.text = "NOT ENOUGH COINS";
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
