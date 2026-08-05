using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;

namespace EmergencyRoad
{
    public sealed class EmergencyRoadMenu : MonoBehaviour
    {
        private EmergencyRoadCatalog catalog;
        private Transform previewRoot;
        private GameObject preview;
        private TMP_Text vehicleName;
        private TMP_Text wallet;
        private TMP_Text priceText;
        private Button actionButton;
        private Button sideCollisionButton;
        private GameObject settings;
        private GameObject controls;
        private EmergencyRoadMenuView sceneView;
        private int index;
        private static readonly int[] Prices = { 0, 350, 700, 1100, 1600, 2200, 3000 };
        private static readonly string[] VehicleNames = { "XE CỨU THƯƠNG", "XE CẢNH SÁT", "XE CỨU HỎA", "TAXI", "XE BỒN", "XE ỦI", "XE XÚC" };

        public static void Create(EmergencyRoadCatalog data, EmergencyRoadMenuView sceneView)
        {
            var root = new GameObject("Menu Controller");
            var menu = root.AddComponent<EmergencyRoadMenu>();
            menu.catalog = data;
            menu.sceneView = sceneView;
            menu.Build();
        }

        private void Build()
        {
            EmergencyRoadUI.SetFont(catalog.uiFont);
            SetupWorld();
            BindSceneUI();
            index = Mathf.Clamp(EmergencyRoadProfile.Current.selectedVehicle, 0, catalog.playerVehicles.Count - 1);
            Select(index);
        }

        private void BindSceneUI()
        {
            if(sceneView==null){Debug.LogError("Menu scene is missing EmergencyRoadMenuView. Rebuild the authored scenes.");return;}
            wallet=sceneView.wallet;vehicleName=sceneView.vehicleName;priceText=sceneView.price;actionButton=sceneView.vehicleAction;sideCollisionButton=sceneView.sideCollision;settings=sceneView.settingsPanel;controls=sceneView.controlsPanel;
            Bind(sceneView.previous,()=>Select(index-1));Bind(sceneView.next,()=>Select(index+1));Bind(sceneView.vehicleAction,VehicleAction);Bind(sceneView.play,Play);Bind(sceneView.settingsOpen,ToggleSettings);Bind(sceneView.quit,Application.Quit);Bind(sceneView.sideCollision,ToggleSideCollision);
            Bind(sceneView.selectVehicle,OpenGarage);Bind(sceneView.garageBack,CloseGarage);
            Bind(sceneView.controlsOpen,()=>{settings.SetActive(false);controls.SetActive(true);});Bind(sceneView.settingsClose,ToggleSettings);Bind(sceneView.controlsBack,()=>{controls.SetActive(false);settings.SetActive(true);});
            Bind(sceneView.music,v=>{EmergencyRoadProfile.Current.musicVolume=v;EmergencyRoadAudio.Instance.ApplyVolumes();EmergencyRoadProfile.Save();},EmergencyRoadProfile.Current.musicVolume);
            Bind(sceneView.sfx,v=>{EmergencyRoadProfile.Current.sfxVolume=v;EmergencyRoadAudio.Instance.ApplyVolumes();EmergencyRoadProfile.Save();},EmergencyRoadProfile.Current.sfxVolume);
            sceneView.mainMenuPanel.SetActive(true);sceneView.garagePanel.SetActive(false);settings.SetActive(false);controls.SetActive(false);RefreshSideCollisionLabel();
        }

        private static void Bind(Button button,UnityEngine.Events.UnityAction action){if(button==null)return;button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>{EmergencyRoadAudio.Instance?.Click();action();});}
        private static void Bind(Slider slider,UnityEngine.Events.UnityAction<float> action,float value){if(slider==null)return;slider.onValueChanged.RemoveAllListeners();slider.SetValueWithoutNotify(value);slider.onValueChanged.AddListener(action);}

        private void LegacyRuntimeUiIsNoLongerUsed()
        {
            var canvas = EmergencyRoadUI.Canvas("Main Menu UI");
            var top = EmergencyRoadUI.Panel(canvas.transform, "Top Bar", EmergencyRoadUI.Navy, new Vector2(0, .86f), Vector2.one, Vector2.zero, Vector2.zero);
            EmergencyRoadUI.Label(top, "BIỆT ĐỘI KHẨN CẤP", 64, Color.white, TextAnchor.MiddleLeft, new Vector2(.05f,0), new Vector2(.65f,1), Vector2.zero, Vector2.zero);
            wallet = EmergencyRoadUI.Label(top, "", 38, EmergencyRoadUI.Yellow, TextAnchor.MiddleRight, new Vector2(.7f,0), new Vector2(.95f,1), Vector2.zero, Vector2.zero);

            var garage = EmergencyRoadUI.Panel(canvas.transform, "Garage", new Color(.02f,.035f,.075f,.88f), new Vector2(.05f,.08f), new Vector2(.42f,.8f), Vector2.zero, Vector2.zero);
            EmergencyRoadUI.Label(garage, "NHÀ XE", 40, EmergencyRoadUI.Cyan, TextAnchor.MiddleCenter, new Vector2(.1f,.82f), new Vector2(.9f,.96f), Vector2.zero, Vector2.zero);
            vehicleName = EmergencyRoadUI.Label(garage, "", 44, Color.white, TextAnchor.MiddleCenter, new Vector2(.08f,.62f), new Vector2(.92f,.8f), Vector2.zero, Vector2.zero);
            priceText = EmergencyRoadUI.Label(garage, "", 28, EmergencyRoadUI.Yellow, TextAnchor.MiddleCenter, new Vector2(.08f,.51f), new Vector2(.92f,.63f), Vector2.zero, Vector2.zero);
            EmergencyRoadUI.Button(garage, "‹", EmergencyRoadUI.Cyan, new Vector2(.08f,.34f), new Vector2(.27f,.48f), Vector2.zero, Vector2.zero, () => Select(index - 1));
            EmergencyRoadUI.Button(garage, "›", EmergencyRoadUI.Cyan, new Vector2(.73f,.34f), new Vector2(.92f,.48f), Vector2.zero, Vector2.zero, () => Select(index + 1));
            actionButton = EmergencyRoadUI.Button(garage, "CHỌN", EmergencyRoadUI.Yellow, new Vector2(.16f,.14f), new Vector2(.84f,.3f), Vector2.zero, Vector2.zero, VehicleAction);

            var right = EmergencyRoadUI.Panel(canvas.transform, "Actions", Color.clear, new Vector2(.66f,.13f), new Vector2(.95f,.68f), Vector2.zero, Vector2.zero);
            EmergencyRoadUI.Button(right, "CHƠI", EmergencyRoadUI.Yellow, new Vector2(0,.66f), Vector2.one, Vector2.zero, Vector2.zero, Play);
            EmergencyRoadUI.Button(right, "CÀI ĐẶT", new Color(.08f,.45f,.65f,1), new Vector2(0,.35f), new Vector2(1,.61f), Vector2.zero, Vector2.zero, ToggleSettings);
            EmergencyRoadUI.Button(right, "THOÁT", new Color(.75f,.16f,.2f,1), new Vector2(0,.04f), new Vector2(1,.3f), Vector2.zero, Vector2.zero, () => Application.Quit());

            settings = BuildSettings(canvas.transform);
            controls = BuildControls(canvas.transform);
        }

        private void SetupWorld()
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.42f,.56f,.72f);RenderSettings.ambientEquatorColor=new Color(.2f,.28f,.36f);RenderSettings.ambientGroundColor=new Color(.08f,.1f,.12f);RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=new Color(.1f,.2f,.28f);RenderSettings.fogStartDistance=25f;RenderSettings.fogEndDistance=75f;
            var cameraGo = new GameObject("Garage Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetPositionAndRotation(new Vector3(8, 5.2f, -9), Quaternion.Euler(17,-35,0));
            cameraGo.GetComponent<Camera>().backgroundColor = new Color(.08f,.16f,.25f);cameraGo.GetComponent<Camera>().allowHDR=true;var cameraData=cameraGo.AddComponent<UniversalAdditionalCameraData>();cameraData.renderPostProcessing=true;cameraData.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            var light = new GameObject("Key Light", typeof(Light));
            light.transform.rotation = Quaternion.Euler(45,-35,0);
            light.GetComponent<Light>().type = LightType.Directional;
            light.GetComponent<Light>().intensity = 1.35f;
            light.GetComponent<Light>().shadows=LightShadows.Soft;light.GetComponent<Light>().color=new Color(1f,.88f,.72f);
            if(catalog.postProcessProfile!=null){var volumeGo=new GameObject("Garage Post Processing",typeof(Volume));var volume=volumeGo.GetComponent<Volume>();volume.isGlobal=true;volume.profile=catalog.postProcessProfile;}
            previewRoot = new GameObject("Vehicle Preview").transform;
            previewRoot.position = new Vector3(3.4f,.2f,0);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            floor.name = "Garage Podium"; floor.transform.position = new Vector3(3.4f,-.25f,0); floor.transform.localScale = new Vector3(3.5f,.15f,3.5f);
            floor.GetComponent<Renderer>().material.color = new Color(.08f,.14f,.2f);
        }

        private GameObject BuildSettings(Transform canvas)
        {
            var panel = EmergencyRoadUI.Panel(canvas, "Settings Modal", EmergencyRoadUI.Navy, new Vector2(.28f,.2f), new Vector2(.72f,.8f), Vector2.zero, Vector2.zero).gameObject;
            EmergencyRoadUI.Label(panel.transform, "CÀI ĐẶT", 52, EmergencyRoadUI.Cyan, TextAnchor.MiddleCenter, new Vector2(.1f,.8f), new Vector2(.9f,.96f), Vector2.zero, Vector2.zero);
            EmergencyRoadUI.Label(panel.transform, "ÂM NHẠC", 28, Color.white, TextAnchor.MiddleLeft, new Vector2(.1f,.61f), new Vector2(.4f,.72f), Vector2.zero, Vector2.zero);
            EmergencyRoadUI.Slider(panel.transform, new Vector2(.4f,.63f), new Vector2(.88f,.69f), EmergencyRoadProfile.Current.musicVolume, v => { EmergencyRoadProfile.Current.musicVolume=v; EmergencyRoadAudio.Instance.ApplyVolumes(); EmergencyRoadProfile.Save(); });
            EmergencyRoadUI.Label(panel.transform, "HIỆU ỨNG", 28, Color.white, TextAnchor.MiddleLeft, new Vector2(.1f,.45f), new Vector2(.4f,.56f), Vector2.zero, Vector2.zero);
            EmergencyRoadUI.Slider(panel.transform, new Vector2(.4f,.47f), new Vector2(.88f,.53f), EmergencyRoadProfile.Current.sfxVolume, v => { EmergencyRoadProfile.Current.sfxVolume=v; EmergencyRoadAudio.Instance.ApplyVolumes(); EmergencyRoadProfile.Save(); });
            EmergencyRoadUI.Label(panel.transform, "VA CHẠM BÊN HÔNG", 25, Color.white, TextAnchor.MiddleLeft, new Vector2(.1f,.31f), new Vector2(.52f,.41f), Vector2.zero, Vector2.zero);
            sideCollisionButton=EmergencyRoadUI.Button(panel.transform,"",new Color(.08f,.45f,.65f,1),new Vector2(.52f,.32f),new Vector2(.88f,.41f),Vector2.zero,Vector2.zero,ToggleSideCollision);RefreshSideCollisionLabel();
            EmergencyRoadUI.Button(panel.transform, "ĐIỀU KHIỂN", new Color(.08f,.45f,.65f,1), new Vector2(.12f,.1f), new Vector2(.58f,.24f), Vector2.zero, Vector2.zero, () => { panel.SetActive(false); controls.SetActive(true); });
            EmergencyRoadUI.Button(panel.transform, "ĐÓNG", new Color(.65f,.18f,.22f,1), new Vector2(.62f,.1f), new Vector2(.88f,.24f), Vector2.zero, Vector2.zero, ToggleSettings);
            panel.SetActive(false);
            return panel;
        }

        private GameObject BuildControls(Transform canvas)
        {
            var panel = EmergencyRoadUI.Panel(canvas, "Controls Modal", EmergencyRoadUI.Navy, new Vector2(.28f,.2f), new Vector2(.72f,.8f), Vector2.zero, Vector2.zero).gameObject;
            EmergencyRoadUI.Label(panel.transform, "ĐIỀU KHIỂN", 52, EmergencyRoadUI.Cyan, TextAnchor.MiddleCenter, new Vector2(.1f,.8f), new Vector2(.9f,.96f), Vector2.zero, Vector2.zero);
            EmergencyRoadUI.Label(panel.transform, "A / D\nCHUYỂN LÀN\n\nSPACE\nBÓP CÒI VUI NHỘN\n\nESC\nTẠM DỪNG", 34, Color.white, TextAnchor.MiddleCenter, new Vector2(.08f,.25f), new Vector2(.92f,.78f), Vector2.zero, Vector2.zero);
            EmergencyRoadUI.Button(panel.transform, "QUAY LẠI", EmergencyRoadUI.Yellow, new Vector2(.28f,.08f), new Vector2(.72f,.2f), Vector2.zero, Vector2.zero, () => { panel.SetActive(false); settings.SetActive(true); });
            panel.SetActive(false);
            return panel;
        }

        private void Update() { if (previewRoot != null) previewRoot.Rotate(0, 24f * Time.unscaledDeltaTime, 0); }
        private void OpenGarage(){sceneView.mainMenuPanel.SetActive(false);sceneView.garagePanel.SetActive(true);}
        private void CloseGarage(){sceneView.garagePanel.SetActive(false);sceneView.mainMenuPanel.SetActive(true);}
        private void ToggleSettings() { settings.SetActive(!settings.activeSelf); controls.SetActive(false); }
        private void ToggleSideCollision(){EmergencyRoadProfile.Current.sideCollisionEnabled=!EmergencyRoadProfile.Current.sideCollisionEnabled;EmergencyRoadProfile.Save();RefreshSideCollisionLabel();}
        private void RefreshSideCollisionLabel(){if(sideCollisionButton!=null)sideCollisionButton.GetComponentInChildren<TMP_Text>().text=EmergencyRoadProfile.Current.sideCollisionEnabled?"BẬT":"TẮT";}

        private void Select(int next)
        {
            if (catalog.playerVehicles.Count == 0) return;
            index = (next + catalog.playerVehicles.Count) % catalog.playerVehicles.Count;
            if (preview != null) Destroy(preview);
            preview = Instantiate(catalog.playerVehicles[index], previewRoot);
            preview.transform.localPosition = Vector3.zero; preview.transform.localRotation = Quaternion.identity;
            NormalizePreview(preview);
            vehicleName.text = index < VehicleNames.Length ? VehicleNames[index] : $"XE ĐẶC BIỆT {index + 1}";
            wallet.text = $"● {EmergencyRoadProfile.Current.coins:N0}";
            bool unlocked = EmergencyRoadProfile.IsUnlocked(index);
            int price = Prices[Mathf.Min(index, Prices.Length-1)];
            priceText.text = unlocked ? "ĐÃ MỞ • CÙNG HIỆU NĂNG" : $"GIÁ  ● {price:N0}";
            actionButton.GetComponentInChildren<TMP_Text>().text = unlocked ? (EmergencyRoadProfile.Current.selectedVehicle == index ? "ĐANG DÙNG" : "CHỌN") : "MỞ KHÓA";
        }

        private static void NormalizePreview(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            float scale = 6f / Mathf.Max(bounds.size.x, bounds.size.z);
            go.transform.localScale = Vector3.one * scale;
            bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            Vector3 target = go.transform.parent.position;
            go.transform.position += new Vector3(target.x-bounds.center.x,target.y-bounds.min.y,target.z-bounds.center.z);
        }

        private void VehicleAction()
        {
            int price = Prices[Mathf.Min(index, Prices.Length-1)];
            if (!EmergencyRoadProfile.TryUnlock(index, price)) { priceText.text = "KHÔNG ĐỦ TIỀN"; return; }
            EmergencyRoadProfile.Current.selectedVehicle = index; EmergencyRoadProfile.Save(); Select(index);
        }

        private void Play()
        {
            if (!EmergencyRoadProfile.IsUnlocked(index)) return;
            EmergencyRoadProfile.Current.selectedVehicle = index; EmergencyRoadProfile.Save();
            SceneManager.LoadScene("Game");
        }
    }
}
