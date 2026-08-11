#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EmergencyRoad.Editor
{
    /// <summary>
    /// Editor-only scene wiring. Runtime never searches for or creates authored objects.
    /// This runs after scripts compile so Menu.unity is already wired before Play is pressed.
    /// </summary>
    [InitializeOnLoad]
    internal static class EmergencyRoadAutoTestInstaller
    {
        private const string MenuScenePath = "Assets/Scenes/Menu.unity";
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string PreviewPivotName = "Selected Vehicle Preview (Ambulance)";

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
        }

        private static void InstallAuthoredScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += InstallAuthoredScenes;
                return;
            }

            InstallMenuScene();
            InstallAutoTester();
        }

        private static void InstallMenuScene()
        {
            if (!System.IO.File.Exists(MenuScenePath)) return;

            Scene activeScene = SceneManager.GetActiveScene();
            bool alreadyLoaded = activeScene.path == MenuScenePath;
            Scene menuScene = alreadyLoaded
                ? activeScene
                : EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Additive);

            EmergencyRoadMenuView view = FindInScene<EmergencyRoadMenuView>(menuScene);
            EmergencyRoadSceneAuthoring authoring = FindInScene<EmergencyRoadSceneAuthoring>(menuScene);

            if (view == null || authoring == null)
            {
                Debug.LogError("[Emergency Road] Menu.unity thiếu EmergencyRoadMenuView hoặc EmergencyRoadSceneAuthoring.");
                if (!alreadyLoaded) EditorSceneManager.CloseScene(menuScene, true);
                return;
            }

            Transform pivot = FindTransform(menuScene, PreviewPivotName);
            if (pivot != null && pivot.GetComponentsInChildren<Renderer>(true).Length > 0)
            {
                Transform parent = pivot.parent;
                Vector3 position = pivot.position;
                Quaternion rotation = pivot.rotation;

                GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(pivot.gameObject);
                Object.DestroyImmediate(prefabRoot != null ? prefabRoot : pivot.gameObject);

                GameObject emptyPivot = new(PreviewPivotName);
                emptyPivot.transform.SetParent(parent != null ? parent : authoring.PreviewRoot, true);
                emptyPivot.transform.SetPositionAndRotation(position, rotation);
                emptyPivot.transform.localScale = Vector3.one;
                pivot = emptyPivot.transform;
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

            if (!alreadyLoaded)
                EditorSceneManager.CloseScene(menuScene, true);
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

            Scene active = SceneManager.GetActiveScene();
            bool opened = active.path != GameScenePath;
            Scene scene = opened ? EditorSceneManager.OpenScene(GameScenePath, OpenSceneMode.Additive) : active;

            EmergencyRoadAutoTester existing = FindInScene<EmergencyRoadAutoTester>(scene);
            if (existing == null)
            {
                GameObject go = new("AUTO TEST DRIVER", typeof(EmergencyRoadAutoTester));
                SceneManager.MoveGameObjectToScene(go, scene);
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log("[Emergency Road] Added scene-authored AUTO TEST DRIVER to Game.unity (disabled until Run On Play is checked).");
            }

            if (opened) EditorSceneManager.CloseScene(scene, true);
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
