#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EmergencyRoad.Editor
{
    /// <summary>
    /// Editor-only scene wiring. Runtime never searches for or creates authored objects.
    /// Menu preview data is repaired whenever Menu.unity is opened and immediately before Play.
    /// </summary>
    [InitializeOnLoad]
    internal static class EmergencyRoadAutoTestInstaller
    {
        private const string MenuScenePath = "Assets/Scenes/Menu.unity";
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string PreviewPivotName = "Selected Vehicle Preview (Ambulance)";

        private static bool installing;

        private static readonly string[] VehicleNames =
        {
            "XE CỨU THƯƠNG",
            "XE CẢNH SÁT",
            "XE CỨU HỎA",
            "TAXI",
            "XE BỒN",
            "XE ỦI",
            "XE XÚC"
        };

        private static readonly int[] VehiclePrices = { 0, 350, 700, 1100, 1600, 2200, 3000 };

        static EmergencyRoadAutoTestInstaller()
        {
            EditorApplication.delayCall += InstallAuthoredScenes;
            EditorSceneManager.sceneOpened += OnSceneOpened;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (installing || scene.path != MenuScenePath) return;
            EditorApplication.delayCall += () => RepairLoadedMenuScene(scene);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode || installing) return;

            Scene menuScene = SceneManager.GetSceneByPath(MenuScenePath);
            if (menuScene.IsValid() && menuScene.isLoaded)
                RepairLoadedMenuScene(menuScene);
        }

        private static void InstallAuthoredScenes()
        {
            if (installing) return;

            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += InstallAuthoredScenes;
                return;
            }

            installing = true;
            try
            {
                InstallMenuScene();
                InstallAutoTester();
            }
            finally
            {
                installing = false;
            }
        }

        private static void InstallMenuScene()
        {
            if (!System.IO.File.Exists(MenuScenePath)) return;

            Scene loaded = SceneManager.GetSceneByPath(MenuScenePath);
            bool alreadyLoaded = loaded.IsValid() && loaded.isLoaded;
            Scene menuScene = alreadyLoaded
                ? loaded
                : EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Additive);

            RepairLoadedMenuScene(menuScene);

            if (!alreadyLoaded)
                EditorSceneManager.CloseScene(menuScene, true);
        }

        private static void RepairLoadedMenuScene(Scene menuScene)
        {
            if (!menuScene.IsValid() || !menuScene.isLoaded || installing && EditorApplication.isPlaying)
                return;

            bool previousInstalling = installing;
            installing = true;

            try
            {
                EmergencyRoadMenuView view = FindInScene<EmergencyRoadMenuView>(menuScene);
                EmergencyRoadSceneAuthoring authoring = FindInScene<EmergencyRoadSceneAuthoring>(menuScene);

                if (view == null || authoring == null)
                {
                    Debug.LogError("[Emergency Road] Menu.unity thiếu EmergencyRoadMenuView hoặc EmergencyRoadSceneAuthoring.");
                    return;
                }

                Transform pivot = EnsureEmptyPreviewPivot(menuScene, authoring);
                if (pivot == null)
                {
                    Debug.LogError("[Emergency Road] Không thể tạo/gán Vehicle Preview Pivot trong Menu.unity.");
                    return;
                }

                view.vehiclePreviewPivot = pivot;

                if (view.vehicleActionLabel == null && view.vehicleAction != null)
                    view.vehicleActionLabel = view.vehicleAction.GetComponentInChildren<TMPro.TMP_Text>(true);

                if (view.sideCollisionLabel == null && view.sideCollision != null)
                    view.sideCollisionLabel = view.sideCollision.GetComponentInChildren<TMPro.TMP_Text>(true);

                EditorUtility.SetDirty(view);

                EmergencyRoadMenu menu = view.GetComponent<EmergencyRoadMenu>();
                if (menu == null)
                    menu = view.gameObject.AddComponent<EmergencyRoadMenu>();

                EmergencyRoadAudio audio = FindInScene<EmergencyRoadAudio>(menuScene);
                menu.ConfigureSceneReferences(null, view, pivot, audio);
                SeedVehiclesDirectly(menu, authoring);

                EditorUtility.SetDirty(menu);
                EditorSceneManager.MarkSceneDirty(menuScene);
                EditorSceneManager.SaveScene(menuScene);
            }
            finally
            {
                installing = previousInstalling;
            }
        }

        private static Transform EnsureEmptyPreviewPivot(Scene menuScene, EmergencyRoadSceneAuthoring authoring)
        {
            Transform pivot = FindTransform(menuScene, PreviewPivotName);

            if (pivot != null && !IsCleanEmptyPivot(pivot))
            {
                Transform parent = pivot.parent;
                Vector3 localPosition = pivot.localPosition;
                Quaternion localRotation = pivot.localRotation;

                GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(pivot.gameObject);
                GameObject objectToRemove = prefabRoot != null ? prefabRoot : pivot.gameObject;

                Object.DestroyImmediate(objectToRemove);

                GameObject emptyPivot = new(PreviewPivotName);
                emptyPivot.transform.SetParent(parent != null ? parent : authoring.PreviewRoot, false);
                emptyPivot.transform.localPosition = localPosition;
                emptyPivot.transform.localRotation = localRotation;
                emptyPivot.transform.localScale = Vector3.one;
                pivot = emptyPivot.transform;

                Debug.Log("[Emergency Road] Đã bỏ xe cứu thương preview mẫu, chỉ giữ Empty Vehicle Preview Pivot.");
            }

            if (pivot == null)
            {
                GameObject emptyPivot = new(PreviewPivotName);
                emptyPivot.transform.SetParent(authoring.PreviewRoot, false);
                emptyPivot.transform.localPosition = new Vector3(3.4f, 0.2f, 0f);
                emptyPivot.transform.localRotation = Quaternion.identity;
                emptyPivot.transform.localScale = Vector3.one;
                pivot = emptyPivot.transform;
            }

            return pivot;
        }

        private static bool IsCleanEmptyPivot(Transform pivot)
        {
            if (pivot == null) return false;
            if (PrefabUtility.IsPartOfPrefabInstance(pivot.gameObject)) return false;
            if (pivot.childCount > 0) return false;

            Component[] components = pivot.GetComponents<Component>();
            return components.Length == 1 && components[0] is Transform;
        }

        private static void SeedVehiclesDirectly(EmergencyRoadMenu menu, EmergencyRoadSceneAuthoring authoring)
        {
            SerializedObject menuObject = new(menu);
            SerializedProperty vehicles = menuObject.FindProperty("vehicles");
            if (vehicles == null || vehicles.arraySize > 0) return;

            SerializedObject authoringObject = new(authoring);
            SerializedProperty playerVehicles = authoringObject.FindProperty("playerVehicles");
            if (playerVehicles == null || !playerVehicles.isArray || playerVehicles.arraySize == 0)
            {
                Debug.LogError("[Emergency Road] MENU SCENE AUTHORING chưa có Player Vehicles để gán vào garage.", authoring);
                return;
            }

            vehicles.arraySize = playerVehicles.arraySize;
            for (int i = 0; i < playerVehicles.arraySize; i++)
            {
                GameObject prefab = playerVehicles.GetArrayElementAtIndex(i).objectReferenceValue as GameObject;
                SerializedProperty entry = vehicles.GetArrayElementAtIndex(i);

                entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                entry.FindPropertyRelative("displayName").stringValue = i < VehicleNames.Length
                    ? VehicleNames[i]
                    : (prefab != null ? prefab.name.Replace('_', ' ').ToUpperInvariant() : $"XE {i + 1}");
                entry.FindPropertyRelative("price").intValue = i < VehiclePrices.Length ? VehiclePrices[i] : 0;
                entry.FindPropertyRelative("hornClip").objectReferenceValue = null;
                entry.FindPropertyRelative("previewLocalPosition").vector3Value = Vector3.zero;
                entry.FindPropertyRelative("previewLocalEuler").vector3Value = Vector3.zero;
                entry.FindPropertyRelative("previewScale").floatValue = 1f;
            }

            menuObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void InstallAutoTester()
        {
            if (!System.IO.File.Exists(GameScenePath)) return;

            Scene loaded = SceneManager.GetSceneByPath(GameScenePath);
            bool alreadyLoaded = loaded.IsValid() && loaded.isLoaded;
            Scene scene = alreadyLoaded
                ? loaded
                : EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive);

            EmergencyRoadAutoTester existing = FindInScene<EmergencyRoadAutoTester>(scene);
            if (existing == null)
            {
                GameObject go = new("AUTO TEST DRIVER", typeof(EmergencyRoadAutoTester));
                SceneManager.MoveGameObjectToScene(go, scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[Emergency Road] Added scene-authored AUTO TEST DRIVER to Game.unity (disabled until Run On Play is checked).");
            }

            if (!alreadyLoaded)
                EditorSceneManager.CloseScene(scene, true);
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T value = root.GetComponentInChildren<T>(true);
                if (value != null) return value;
            }

            return null;
        }

        private static Transform FindTransform(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindTransformRecursive(root.transform, objectName);
                if (found != null) return found;
            }

            return null;
        }

        private static Transform FindTransformRecursive(Transform root, string objectName)
        {
            if (root.name == objectName) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindTransformRecursive(root.GetChild(i), objectName);
                if (found != null) return found;
            }

            return null;
        }
    }
}
#endif
