#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EmergencyRoad.Editor
{
    /// <summary>
    /// Editor-only wiring for Menu.unity.
    /// This script never opens, edits or saves Game.unity.
    /// Runtime still uses only references already serialized in the menu scene.
    /// </summary>
    [InitializeOnLoad]
    internal static class EmergencyRoadAutoTestInstaller
    {
        private const string MenuScenePath = "Assets/Scenes/Menu.unity";
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

        private static bool isInstalling;

        static EmergencyRoadAutoTestInstaller()
        {
            EditorApplication.delayCall += InstallMenuScene;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.path != MenuScenePath || isInstalling)
                return;

            EditorApplication.delayCall += InstallMenuScene;
        }

        private static void InstallMenuScene()
        {
            if (isInstalling || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                return;

            if (!System.IO.File.Exists(MenuScenePath))
                return;

            isInstalling = true;

            try
            {
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
                    if (!alreadyLoaded)
                        EditorSceneManager.CloseScene(menuScene, true);
                    return;
                }

                Transform pivot = EnsureEmptyPreviewPivot(menuScene, authoring);
                RemoveAuthoredAmbulanceSamples(authoring, pivot);

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
            finally
            {
                isInstalling = false;
            }
        }

        private static Transform EnsureEmptyPreviewPivot(Scene menuScene, EmergencyRoadSceneAuthoring authoring)
        {
            Transform pivot = FindTransform(menuScene, PreviewPivotName);

            if (pivot != null && pivot.GetComponentsInChildren<Renderer>(true).Length > 0)
            {
                Transform parent = pivot.parent;
                Vector3 localPosition = pivot.localPosition;
                Quaternion localRotation = pivot.localRotation;

                GameObject prefabRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(pivot.gameObject);
                Object.DestroyImmediate(prefabRoot != null ? prefabRoot : pivot.gameObject);

                GameObject emptyPivot = new(PreviewPivotName);
                emptyPivot.transform.SetParent(parent != null ? parent : authoring.PreviewRoot, false);
                emptyPivot.transform.localPosition = localPosition;
                emptyPivot.transform.localRotation = localRotation;
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

            return pivot;
        }

        /// <summary>
        /// Removes only the old authored ambulance sample from the Menu preview area.
        /// The actual selected ambulance is still spawned normally by EmergencyRoadMenu at runtime.
        /// </summary>
        private static void RemoveAuthoredAmbulanceSamples(EmergencyRoadSceneAuthoring authoring, Transform pivot)
        {
            if (authoring == null || authoring.PreviewRoot == null)
                return;

            SerializedObject authoringObject = new(authoring);
            SerializedProperty playerVehicles = authoringObject.FindProperty("playerVehicles");
            if (playerVehicles == null || !playerVehicles.isArray || playerVehicles.arraySize == 0)
                return;

            GameObject ambulancePrefab = playerVehicles.GetArrayElementAtIndex(0).objectReferenceValue as GameObject;
            if (ambulancePrefab == null)
                return;

            string ambulancePath = AssetDatabase.GetAssetPath(ambulancePrefab);
            if (string.IsNullOrEmpty(ambulancePath))
                return;

            HashSet<GameObject> rootsToRemove = new();
            Transform[] transforms = authoring.PreviewRoot.GetComponentsInChildren<Transform>(true);

            foreach (Transform candidate in transforms)
            {
                if (candidate == null || candidate == authoring.PreviewRoot || candidate == pivot)
                    continue;

                if (pivot != null && candidate.IsChildOf(pivot))
                    continue;

                GameObject instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(candidate.gameObject);
                if (instanceRoot == null || rootsToRemove.Contains(instanceRoot))
                    continue;

                GameObject sourceRoot = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
                if (sourceRoot == null)
                    continue;

                string sourcePath = AssetDatabase.GetAssetPath(sourceRoot);
                if (sourcePath == ambulancePath)
                    rootsToRemove.Add(instanceRoot);
            }

            foreach (GameObject sample in rootsToRemove)
                Object.DestroyImmediate(sample);
        }

        private static void SeedVehiclesDirectly(EmergencyRoadMenu menu, EmergencyRoadSceneAuthoring authoring)
        {
            SerializedObject menuObject = new(menu);
            SerializedProperty vehicles = menuObject.FindProperty("vehicles");
            if (vehicles == null || vehicles.arraySize > 0)
                return;

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
                    : prefab != null
                        ? prefab.name.Replace('_', ' ').ToUpperInvariant()
                        : $"XE {i + 1}";
                entry.FindPropertyRelative("price").intValue = i < VehiclePrices.Length ? VehiclePrices[i] : 0;
                entry.FindPropertyRelative("hornClip").objectReferenceValue = null;
                entry.FindPropertyRelative("previewLocalPosition").vector3Value = Vector3.zero;
                entry.FindPropertyRelative("previewLocalEuler").vector3Value = Vector3.zero;
                entry.FindPropertyRelative("previewScale").floatValue = 1f;
            }

            menuObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T value = root.GetComponentInChildren<T>(true);
                if (value != null)
                    return value;
            }

            return null;
        }

        private static Transform FindTransform(Scene scene, string objectName)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                Transform found = FindTransformRecursive(root.transform, objectName);
                if (found != null)
                    return found;
            }

            return null;
        }

        private static Transform FindTransformRecursive(Transform root, string objectName)
        {
            if (root.name == objectName)
                return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindTransformRecursive(root.GetChild(i), objectName);
                if (found != null)
                    return found;
            }

            return null;
        }
    }
}
#endif
