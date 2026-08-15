using UnityEngine;

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

        public EmergencyRoadSceneKind SceneKind => sceneKind;
        public EmergencyRoadCatalog Catalog => catalog;
        public Transform PreviewRoot => previewRoot;
        public float LaneWidth => catalog != null ? catalog.laneWidth : 5.18f;
        public float RoadLength => catalog != null ? catalog.roadLength : 28f;
        public bool HasRequiredPrefabContent => catalog != null && catalog.PlayerVehicleCount > 0 && catalog.roadPrefabs.Count > 0;

        public void Configure(EmergencyRoadSceneKind kind, EmergencyRoadCatalog source, Transform authoredPreviewRoot, float authoredLaneWidth = 5.18f, float authoredRoadLength = 28f)
        {
            sceneKind = kind;
            catalog = source;
            previewRoot = authoredPreviewRoot;
            if (source != null) source.SynchronizePlayerVehicleData();
        }
    }
}
