using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using TMPro;

namespace EmergencyRoad
{
    public enum EmergencyRoadSceneKind { Menu, Game }

    [System.Serializable]
    public sealed class EmergencyRoadVehicleSetup
    {
        public GameObject vehiclePrefab;
        public AudioClip hornSound;
    }

    /// <summary>All replaceable scene content is exposed here. Runtime reads this component, not the catalog asset.</summary>
    public sealed class EmergencyRoadSceneAuthoring : MonoBehaviour
    {
        [Header("SCENE")]
        [SerializeField] private EmergencyRoadSceneKind sceneKind;
        [SerializeField] private Transform previewRoot;
        [SerializeField, Min(2.5f)] private float laneWidth = 5.18f;
        [SerializeField, Min(15f)] private float chunkSpacing = 28f;
        [SerializeField, Min(20f)] private float startingSafeDistance = 48f;

        [Header("PLAYER VEHICLES - PREFAB AND HORN FOR EACH CAR")]
        [SerializeField] private List<EmergencyRoadVehicleSetup> playerVehicleSetups = new();
        [SerializeField, HideInInspector] private List<GameObject> playerVehicles = new();
        [Header("TRAFFIC VEHICLE PREFABS - DRAG HERE")]
        [SerializeField] private List<GameObject> trafficVehicles = new();
        [Header("ROAD AND CROSSROAD PREFABS - DRAG HERE")]
        [SerializeField] private List<GameObject> roadPrefabs = new();
        [SerializeField] private List<GameObject> crossroadPrefabs = new();
        [SerializeField] private List<EmergencyRoadPrefabMetrics> crossroadMetrics = new();
        [Header("GAMEPLAY PREFABS - DRAG HERE")]
        [SerializeField] private GameObject coinPrefab;
        [SerializeField] private GameObject motorcyclePrefab;
        [SerializeField] private List<GameObject> obstaclePrefabs = new();
        [Header("CITY PREFABS - DRAG HERE")]
        [SerializeField] private List<GameObject> buildingPrefabs = new();
        [SerializeField] private List<GameObject> treePrefabs = new();
        [SerializeField] private List<GameObject> streetDecorationPrefabs = new();

        [Header("MATERIALS AND EFFECTS - DRAG HERE")]
        [SerializeField] private Material grassMaterial;
        [SerializeField] private Material soilMaterial;
        [SerializeField] private Material particleMaterial;
        [SerializeField] private VolumeProfile postProcessProfile;

        [Header("AUDIO - DRAG EACH CLIP HERE")]
        [SerializeField] private AudioClip backgroundMusic;
        [SerializeField] private AudioClip buttonClickSound;
        [SerializeField] private AudioClip coinSound;
        [SerializeField] private AudioClip hornSound;
        [SerializeField] private AudioClip collisionSound;

        [Header("UI ART - DRAG EACH SPRITE HERE")]
        [SerializeField] private TMP_FontAsset uiFont;
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite buttonSprite;
        [SerializeField] private Sprite sliderBackgroundSprite;
        [SerializeField] private Sprite sliderFillSprite;
        [SerializeField] private Sprite sliderHandleSprite;

        [Header("SCENE UI COMPONENTS - DRAG EACH OBJECT HERE")]
        [SerializeField] private Canvas sceneCanvas;
        [SerializeField] private Image topBarImage;
        [SerializeField] private Image garageOrHudPanelImage;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject controlsPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private List<Button> sceneButtons = new();
        [SerializeField] private List<Slider> sceneSliders = new();
        [SerializeField] private List<Image> sceneImages = new();

        [SerializeField, HideInInspector] private EmergencyRoadGameplaySettings gameplaySettings;
        public EmergencyRoadSceneKind SceneKind => sceneKind;
        public float LaneWidth => laneWidth;
        public float ChunkSpacing => chunkSpacing;
        public Transform PreviewRoot => previewRoot;
        public bool HasRequiredPrefabContent=>(playerVehicleSetups!=null&&playerVehicleSetups.Count>0||playerVehicles!=null&&playerVehicles.Count>0)&&roadPrefabs!=null&&roadPrefabs.Count>0;

        public EmergencyRoadCatalog CreateRuntimeCatalog()
        {
            var data=ScriptableObject.CreateInstance<EmergencyRoadCatalog>();data.hideFlags=HideFlags.DontSave;
            data.gameplaySettings=gameplaySettings;data.playerVehicles=new List<GameObject>(playerVehicles);data.trafficVehicles=new List<GameObject>(trafficVehicles);data.roadPrefabs=new List<GameObject>(roadPrefabs);data.crossroadPrefabs=new List<GameObject>(crossroadPrefabs);data.crossroadMetrics=new List<EmergencyRoadPrefabMetrics>(crossroadMetrics);data.obstaclePrefabs=new List<GameObject>(obstaclePrefabs);data.decorationPrefabs=new List<GameObject>(buildingPrefabs);data.naturePrefabs=new List<GameObject>(treePrefabs);data.streetDecorationPrefabs=new List<GameObject>(streetDecorationPrefabs);data.coinPrefab=coinPrefab;data.motorcyclePrefab=motorcyclePrefab;data.grassMaterial=grassMaterial;data.soilMaterial=soilMaterial;data.vfxParticleMaterial=particleMaterial;data.postProcessProfile=postProcessProfile;data.uiFont=uiFont;data.panelSprite=panelSprite;data.buttonSprite=buttonSprite;data.sliderBackgroundSprite=sliderBackgroundSprite;data.sliderFillSprite=sliderFillSprite;data.sliderHandleSprite=sliderHandleSprite;data.musicClip=backgroundMusic;data.uiClickClip=buttonClickSound;data.coinClip=coinSound;data.hornClip=hornSound;data.crashClip=collisionSound;data.roadLength=chunkSpacing;data.laneWidth=laneWidth;data.startingSafeDistance=startingSafeDistance;data.roadHalfWidth=roadPrefabs.Count>0?11.03214f:5.5f;
            if(playerVehicleSetups!=null&&playerVehicleSetups.Count>0){data.playerVehicles.Clear();data.vehicleHornClips.Clear();foreach(var setup in playerVehicleSetups)if(setup!=null&&setup.vehiclePrefab!=null){data.playerVehicles.Add(setup.vehiclePrefab);data.vehicleHornClips.Add(setup.hornSound);}}
            return data;
        }

        public void Configure(EmergencyRoadSceneKind kind,EmergencyRoadCatalog source,Transform preview,float lanes=5.18f,float spacing=28f)
        {
            sceneKind=kind;previewRoot=preview;laneWidth=lanes;chunkSpacing=spacing;gameplaySettings=source.gameplaySettings;playerVehicles=new(source.playerVehicles);trafficVehicles=new(source.trafficVehicles);roadPrefabs=new(source.roadPrefabs);crossroadPrefabs=new(source.crossroadPrefabs);crossroadMetrics=new(source.crossroadMetrics);coinPrefab=source.coinPrefab;motorcyclePrefab=source.motorcyclePrefab;obstaclePrefabs=new(source.obstaclePrefabs);buildingPrefabs=new(source.decorationPrefabs);treePrefabs=new(source.naturePrefabs);streetDecorationPrefabs=new(source.streetDecorationPrefabs);grassMaterial=source.grassMaterial;soilMaterial=source.soilMaterial;particleMaterial=source.vfxParticleMaterial;postProcessProfile=source.postProcessProfile;uiFont=source.uiFont;panelSprite=source.panelSprite;buttonSprite=source.buttonSprite;sliderBackgroundSprite=source.sliderBackgroundSprite;sliderFillSprite=source.sliderFillSprite;sliderHandleSprite=source.sliderHandleSprite;backgroundMusic=source.musicClip;buttonClickSound=source.uiClickClip;coinSound=source.coinClip;hornSound=source.hornClip;collisionSound=source.crashClip;
            playerVehicleSetups=new List<EmergencyRoadVehicleSetup>();for(int i=0;i<source.playerVehicles.Count;i++)playerVehicleSetups.Add(new EmergencyRoadVehicleSetup{vehiclePrefab=source.playerVehicles[i],hornSound=source.HornForVehicle(i)});
            startingSafeDistance=source.startingSafeDistance;sceneCanvas=preview.GetComponentInChildren<Canvas>(true);if(sceneCanvas!=null){sceneImages=new(sceneCanvas.GetComponentsInChildren<Image>(true));sceneButtons=new(sceneCanvas.GetComponentsInChildren<Button>(true));sceneSliders=new(sceneCanvas.GetComponentsInChildren<Slider>(true));foreach(var image in sceneImages){if(image.name.Contains("Top Bar"))topBarImage=image;else if(image.name.Contains("Garage")||image.name.Contains("HUD"))garageOrHudPanelImage=image;else if(image.name.Contains("Settings")||image.name.Contains("Cài Đặt"))settingsPanel=image.gameObject;else if(image.name.Contains("Controls")||image.name.Contains("Điều Khiển"))controlsPanel=image.gameObject;else if(image.name.Contains("Pause")||image.name.Contains("Tạm Dừng"))pausePanel=image.gameObject;else if(image.name.Contains("Game Over")||image.name.Contains("Hết Lượt"))gameOverPanel=image.gameObject;}}
        }
    }
}
