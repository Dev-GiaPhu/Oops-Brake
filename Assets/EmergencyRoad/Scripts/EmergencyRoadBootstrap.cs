using UnityEngine;
using UnityEngine.SceneManagement;

namespace EmergencyRoad
{
    public static class EmergencyRoadBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Start() => Compose(SceneManager.GetActiveScene());

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Compose(scene);

        private static void Compose(Scene scene)
        {
            if (!scene.IsValid() || Object.FindFirstObjectByType<EmergencyRoadGame>() != null || Object.FindFirstObjectByType<EmergencyRoadMenu>() != null) return;
            var authoring = Object.FindFirstObjectByType<EmergencyRoadSceneAuthoring>(FindObjectsInactive.Include);
            var catalog = authoring != null && authoring.HasRequiredPrefabContent ? authoring.CreateRuntimeCatalog() : Resources.Load<EmergencyRoadCatalog>("EmergencyRoadCatalog");
            if (catalog == null) { Debug.LogError("Emergency Road catalog is missing. Run Tools > Emergency Road > Build Game."); return; }
            EmergencyRoadAudio.Ensure(catalog);
            EmergencyRoadUI.Configure(catalog);
            EmergencyRoadUI.EnsureEventSystem();
            var menuView = Object.FindFirstObjectByType<EmergencyRoadMenuView>(FindObjectsInactive.Include);
            var gameView = Object.FindFirstObjectByType<EmergencyRoadGameView>(FindObjectsInactive.Include);
            var activeView = menuView != null ? menuView.transform : gameView != null ? gameView.transform : null;
            if(authoring!=null&&authoring.PreviewRoot!=null&&activeView!=null&&activeView.IsChildOf(authoring.PreviewRoot))activeView.SetParent(authoring.transform,true);
            if (authoring != null && authoring.PreviewRoot != null) authoring.PreviewRoot.gameObject.SetActive(false);
            Debug.Log($"[Emergency Road] Runtime compose: {scene.name}, {catalog.playerVehicles.Count} vehicles, Road_1={(catalog.roadPrefabs.Count > 0 ? "ready" : "missing")}");
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) Object.Destroy(camera.gameObject);
            foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None)) Object.Destroy(light.gameObject);
            if ((authoring != null && authoring.SceneKind == EmergencyRoadSceneKind.Menu) || scene.name == "Menu") EmergencyRoadMenu.Create(catalog,menuView);
            else EmergencyRoadGame.Create(catalog,gameView,authoring != null ? authoring.LaneWidth : catalog.laneWidth, catalog.roadLength > 1f ? catalog.roadLength : (authoring != null ? authoring.ChunkSpacing : 28f));
        }
    }
}
