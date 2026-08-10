using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace EmergencyRoad
{
    public enum EmergencyRoadSceneKind { Menu, Game }

    /// <summary>
    /// Scene-authored manifest only. Runtime does not clone ScriptableObjects, search the hierarchy,
    /// or synthesize missing content. All references are visible in the Inspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EmergencyRoadSceneAuthoring : MonoBehaviour
    {
        [Header("SCENE")]
        [SerializeField] private EmergencyRoadSceneKind sceneKind;
        [SerializeField] private EmergencyRoadCatalog catalog;
        [SerializeField] private Transform previewRoot;

        [Header("AUTHORED CONTENT")]
        [SerializeField] private List<GameObject> playerVehicles = new();
        [SerializeField] private List<GameObject> trafficVehicles = new();
        [SerializeField] private List<GameObject> roadPrefabs = new();
        [SerializeField] private List<GameObject> crossroadPrefabs = new();
        [SerializeField] private List<GameObject> obstaclePrefabs = new();
        [SerializeField] private List<GameObject> decorationPrefabs = new();
        [SerializeField] private List<GameObject> naturePrefabs = new();
        [SerializeField] private List<GameObject> streetDecorationPrefabs = new();
        [SerializeField] private GameObject coinPrefab;
        [SerializeField] private GameObject motorcyclePrefab;

        [Header("AUDIO")]
        [SerializeField] private AudioClip musicClip;
        [SerializeField] private AudioClip uiClickClip;
        [SerializeField] private AudioClip coinClip;
        [SerializeField] private AudioClip hornClip;
        [SerializeField] private AudioClip crashClip;

        [Header("UI")]
        [SerializeField] private TMP_FontAsset uiFont;
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite buttonSprite;
        [SerializeField] private Sprite sliderBackgroundSprite;
        [SerializeField] private Sprite sliderFillSprite;
        [SerializeField] private Sprite sliderHandleSprite;

        [Header("WORLD METRICS")]
        [SerializeField] private float laneWidth = 5.18f;
        [SerializeField] private float roadLength = 28f;
        [SerializeField] private float roadHalfWidth = 5.5f;
        [SerializeField] private float startingSafeDistance = 48f;

        public EmergencyRoadSceneKind SceneKind => sceneKind;
        public EmergencyRoadCatalog Catalog => catalog;
        public Transform PreviewRoot => previewRoot;
        public float LaneWidth => laneWidth;
        public float RoadLength => roadLength;
        public bool HasRequiredPrefabContent => catalog != null && playerVehicles.Count > 0 && roadPrefabs.Count > 0;

        public void Configure(EmergencyRoadSceneKind kind, EmergencyRoadCatalog source, Transform authoredPreviewRoot, float authoredLaneWidth = 5.18f, float authoredRoadLength = 28f)
        {
            sceneKind = kind;
            catalog = source;
            previewRoot = authoredPreviewRoot;
            laneWidth = authoredLaneWidth;
            roadLength = authoredRoadLength;
            if (source == null) return;

            source.SynchronizePlayerVehicleData();
            playerVehicles = new List<GameObject>(source.playerVehicles);
            trafficVehicles = new List<GameObject>(source.trafficVehicles);
            roadPrefabs = new List<GameObject>(source.roadPrefabs);
            crossroadPrefabs = new List<GameObject>(source.crossroadPrefabs);
            obstaclePrefabs = new List<GameObject>(source.obstaclePrefabs);
            decorationPrefabs = new List<GameObject>(source.decorationPrefabs);
            naturePrefabs = new List<GameObject>(source.naturePrefabs);
            streetDecorationPrefabs = new List<GameObject>(source.streetDecorationPrefabs);
            coinPrefab = source.coinPrefab;
            motorcyclePrefab = source.motorcyclePrefab;
            musicClip = source.musicClip;
            uiClickClip = source.uiClickClip;
            coinClip = source.coinClip;
            hornClip = source.hornClip;
            crashClip = source.crashClip;
            uiFont = source.uiFont;
            panelSprite = source.panelSprite;
            buttonSprite = source.buttonSprite;
            sliderBackgroundSprite = source.sliderBackgroundSprite;
            sliderFillSprite = source.sliderFillSprite;
            sliderHandleSprite = source.sliderHandleSprite;
            roadHalfWidth = source.roadHalfWidth;
            startingSafeDistance = source.startingSafeDistance;
        }
    }
}
