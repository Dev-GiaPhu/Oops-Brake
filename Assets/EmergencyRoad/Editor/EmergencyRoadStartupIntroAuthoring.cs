#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EmergencyRoad.Editor
{
    /// <summary>
    /// Authors the Menu startup intro entirely in the scene.
    /// Runtime only consumes serialized Inspector references and never creates fallback objects.
    /// </summary>
    public static class EmergencyRoadStartupIntroAuthoring
    {
        private const string MenuPath = "Assets/Scenes/Menu.unity";
        private const string IntroRootName = "Startup Intro - Camera + Logo";
        private const string MarkerGroupName = "INTRO MARKERS - SCENE AUTHORED";
        private const string CameraMarkerName = "Intro Camera Start Marker (MOVE THIS)";
        private const string LogoStartName = "Logo Start - Below Screen";
        private const string LogoCenterName = "Logo Center - Screen Centre";

        [MenuItem("Tools/Emergency Road/Setup Menu Startup Intro")]
        public static void SetupMenuStartupIntro()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Emergency Road] Thoat Play Mode truoc khi setup Startup Intro.");
                return;
            }

            Scene menuScene = SceneManager.GetSceneByPath(MenuPath);
            bool openedTemporarily = !menuScene.IsValid() || !menuScene.isLoaded;

            if (openedTemporarily)
                menuScene = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Additive);

            bool changed = SetupScene(menuScene);
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(menuScene);
                EditorSceneManager.SaveScene(menuScene);
                Debug.Log(
                    "[Emergency Road] Startup Intro da duoc wire bang scene references. " +
                    "Di chuyen Empty GameObject 'Intro Camera Start Marker (MOVE THIS)' de chon goc camera bat dau.");
            }

            if (openedTemporarily)
                EditorSceneManager.CloseScene(menuScene, true);
        }

        [DidReloadScripts]
        private static void WireLoadedMenuAfterScriptsReload()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;

                Scene menuScene = SceneManager.GetSceneByPath(MenuPath);
                if (!menuScene.IsValid() || !menuScene.isLoaded) return;

                if (!SetupScene(menuScene)) return;
                EditorSceneManager.MarkSceneDirty(menuScene);
                EditorSceneManager.SaveScene(menuScene);
            };
        }

        private static bool SetupScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return false;

            EmergencyRoadMenuView view = FindInScene<EmergencyRoadMenuView>(scene);
            Camera menuCamera = FindInScene<Camera>(scene);
            EmergencyRoadLogoMotion logoMotion = FindInScene<EmergencyRoadLogoMotion>(scene);

            if (view == null)
            {
                Debug.LogError("[Emergency Road] Menu scene thieu EmergencyRoadMenuView.");
                return false;
            }
            if (menuCamera == null)
            {
                Debug.LogError("[Emergency Road] Menu scene thieu Camera.");
                return false;
            }

            EmergencyRoadStartupIntro intro = FindInScene<EmergencyRoadStartupIntro>(scene);
            SerializedObject existingIntroSO = intro != null ? new SerializedObject(intro) : null;
            RectTransform menuLogo = existingIntroSO != null
                ? GetReference<RectTransform>(existingIntroSO, "menuLogo")
                : null;
            EmergencyRoadLogoMotion existingLogoMotion = existingIntroSO != null
                ? GetReference<EmergencyRoadLogoMotion>(existingIntroSO, "logoIdleMotion")
                : null;

            if (menuLogo == null && logoMotion != null)
                menuLogo = logoMotion.transform as RectTransform;
            if (existingLogoMotion == null)
                existingLogoMotion = logoMotion;

            if (menuLogo == null)
            {
                Debug.LogError(
                    "[Emergency Road] Khong tim thay Menu Logo. " +
                    "Logo can co EmergencyRoadLogoMotion hoac da duoc keo vao Startup Intro.");
                return false;
            }

            Canvas canvas = menuLogo.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = view.GetComponentInParent<Canvas>();
            if (canvas == null) canvas = FindInScene<Canvas>(scene);
            if (canvas == null)
            {
                Debug.LogError("[Emergency Road] Menu scene thieu Canvas de dat intro logo markers.");
                return false;
            }

            bool changed = false;
            if (intro == null)
            {
                GameObject introObject = new(
                    IntroRootName,
                    typeof(RectTransform),
                    typeof(CanvasGroup),
                    typeof(EmergencyRoadStartupIntro));
                SceneManager.MoveGameObjectToScene(introObject, scene);
                introObject.transform.SetParent(canvas.transform, false);
                intro = introObject.GetComponent<EmergencyRoadStartupIntro>();
                changed = true;
            }

            if (intro.gameObject.name != IntroRootName)
            {
                intro.gameObject.name = IntroRootName;
                changed = true;
            }

            RectTransform introRoot = intro.transform as RectTransform;
            if (introRoot == null)
            {
                Debug.LogError("[Emergency Road] Startup Intro phai nam tren RectTransform trong Menu Canvas.", intro);
                return false;
            }

            if (introRoot.parent != canvas.transform)
            {
                introRoot.SetParent(canvas.transform, false);
                changed = true;
            }

            changed |= SetFullScreenRect(introRoot);
            if (introRoot.GetSiblingIndex() != introRoot.parent.childCount - 1)
            {
                introRoot.SetAsLastSibling();
                changed = true;
            }

            Graphic legacyBackground = intro.GetComponent<Graphic>();
            if (legacyBackground != null)
            {
                Object.DestroyImmediate(legacyBackground);
                changed = true;
            }

            CanvasGroup blocker = intro.GetComponent<CanvasGroup>();
            if (blocker == null)
            {
                blocker = intro.gameObject.AddComponent<CanvasGroup>();
                changed = true;
            }
            if (!Mathf.Approximately(blocker.alpha, 1f))
            {
                blocker.alpha = 1f;
                changed = true;
            }
            if (blocker.interactable)
            {
                blocker.interactable = false;
                changed = true;
            }
            if (!blocker.blocksRaycasts)
            {
                blocker.blocksRaycasts = true;
                changed = true;
            }

            SerializedObject introSO = new(intro);
            introSO.Update();

            RectTransform logoStart = GetReference<RectTransform>(introSO, "logoStartMarker");
            bool legacyStartMarker = logoStart != null && logoStart.name != LogoStartName;
            if (logoStart == null)
            {
                logoStart = CreateRectMarker(scene, introRoot, LogoStartName);
                changed = true;
            }
            else if (logoStart.parent != introRoot)
            {
                logoStart.SetParent(introRoot, false);
                changed = true;
            }

            if (legacyStartMarker || logoStart.name != LogoStartName)
            {
                logoStart.name = LogoStartName;
                changed = true;
            }
            if (legacyStartMarker || IsUnconfiguredMarker(logoStart))
                changed |= ConfigureLogoStartMarker(logoStart);

            RectTransform logoCenter = GetReference<RectTransform>(introSO, "logoCenterMarker");
            if (logoCenter == null)
            {
                logoCenter = FindRectTransform(scene, LogoCenterName);
                if (logoCenter == null)
                    logoCenter = CreateRectMarker(scene, introRoot, LogoCenterName);
                changed = true;
            }
            if (logoCenter.parent != introRoot)
            {
                logoCenter.SetParent(introRoot, false);
                changed = true;
            }
            if (logoCenter.name != LogoCenterName)
            {
                logoCenter.name = LogoCenterName;
                changed = true;
            }
            changed |= ConfigureLogoCenterMarker(logoCenter);

            Transform cameraMarker = GetReference<Transform>(introSO, "cameraIntroMarker");
            if (cameraMarker == null)
            {
                cameraMarker = FindTransform(scene, CameraMarkerName);
                if (cameraMarker == null)
                {
                    Transform markerParent = EnsureMarkerGroup(scene);
                    GameObject markerObject = new(CameraMarkerName);
                    SceneManager.MoveGameObjectToScene(markerObject, scene);
                    markerObject.transform.SetParent(markerParent, true);
                    markerObject.transform.SetPositionAndRotation(
                        menuCamera.transform.position,
                        menuCamera.transform.rotation);
                    cameraMarker = markerObject.transform;
                }
                changed = true;
            }
            else if (cameraMarker.name != CameraMarkerName)
            {
                cameraMarker.name = CameraMarkerName;
                changed = true;
            }

            EmergencyRoadMenuCameraOrbit cameraOrbit =
                menuCamera.GetComponent<EmergencyRoadMenuCameraOrbit>();

            changed |= SetReference(introSO, "menuCamera", menuCamera);
            changed |= SetReference(introSO, "cameraIntroMarker", cameraMarker);
            changed |= SetReference(introSO, "cameraOrbit", cameraOrbit);
            changed |= SetReference(introSO, "logoStartMarker", logoStart);
            changed |= SetReference(introSO, "logoCenterMarker", logoCenter);
            changed |= SetReference(introSO, "menuLogo", menuLogo);
            changed |= SetReference(introSO, "logoIdleMotion", existingLogoMotion);

            if (introSO.ApplyModifiedPropertiesWithoutUndo())
                changed = true;

            if (!intro.gameObject.activeSelf)
            {
                intro.gameObject.SetActive(true);
                changed = true;
            }

            EditorUtility.SetDirty(intro);
            EditorUtility.SetDirty(blocker);
            return changed;
        }

        private static Transform EnsureMarkerGroup(Scene scene)
        {
            Transform existing = FindTransform(scene, MarkerGroupName);
            if (existing != null) return existing;

            GameObject group = new(MarkerGroupName);
            SceneManager.MoveGameObjectToScene(group, scene);

            EmergencyRoadSceneAuthoring authoring = FindInScene<EmergencyRoadSceneAuthoring>(scene);
            if (authoring != null)
                group.transform.SetParent(authoring.transform, false);

            return group.transform;
        }

        private static RectTransform CreateRectMarker(Scene scene, RectTransform parent, string name)
        {
            GameObject marker = new(name, typeof(RectTransform));
            SceneManager.MoveGameObjectToScene(marker, scene);
            RectTransform rect = marker.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static bool ConfigureLogoStartMarker(RectTransform marker)
        {
            bool changed = false;
            Vector2 anchor = new(.5f, 0f);
            if (marker.anchorMin != anchor) { marker.anchorMin = anchor; changed = true; }
            if (marker.anchorMax != anchor) { marker.anchorMax = anchor; changed = true; }
            Vector2 pivot = new(.5f, .5f);
            if (marker.pivot != pivot) { marker.pivot = pivot; changed = true; }
            Vector2 position = new(0f, -360f);
            if (marker.anchoredPosition != position) { marker.anchoredPosition = position; changed = true; }
            if (marker.sizeDelta != Vector2.zero) { marker.sizeDelta = Vector2.zero; changed = true; }
            return changed;
        }

        private static bool ConfigureLogoCenterMarker(RectTransform marker)
        {
            bool changed = false;
            Vector2 anchor = new(.5f, .5f);
            if (marker.anchorMin != anchor) { marker.anchorMin = anchor; changed = true; }
            if (marker.anchorMax != anchor) { marker.anchorMax = anchor; changed = true; }
            if (marker.pivot != anchor) { marker.pivot = anchor; changed = true; }
            if (marker.anchoredPosition != Vector2.zero) { marker.anchoredPosition = Vector2.zero; changed = true; }
            if (marker.sizeDelta != Vector2.zero) { marker.sizeDelta = Vector2.zero; changed = true; }
            return changed;
        }

        private static bool SetFullScreenRect(RectTransform rect)
        {
            bool changed = false;
            if (rect.anchorMin != Vector2.zero) { rect.anchorMin = Vector2.zero; changed = true; }
            if (rect.anchorMax != Vector2.one) { rect.anchorMax = Vector2.one; changed = true; }
            if (rect.offsetMin != Vector2.zero) { rect.offsetMin = Vector2.zero; changed = true; }
            if (rect.offsetMax != Vector2.zero) { rect.offsetMax = Vector2.zero; changed = true; }
            Vector2 pivot = new(.5f, .5f);
            if (rect.pivot != pivot) { rect.pivot = pivot; changed = true; }
            return changed;
        }

        private static bool IsUnconfiguredMarker(RectTransform marker)
        {
            return marker.anchorMin == marker.anchorMax &&
                   marker.anchorMin == new Vector2(.5f, .5f) &&
                   marker.anchoredPosition == Vector2.zero;
        }

        private static T GetReference<T>(SerializedObject serializedObject, string propertyName)
            where T : Object
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as T : null;
        }

        private static bool SetReference(
            SerializedObject serializedObject,
            string propertyName,
            Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == value) return false;
            property.objectReferenceValue = value;
            return true;
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
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == objectName) return candidate;
            return null;
        }

        private static RectTransform FindRectTransform(Scene scene, string objectName)
        {
            Transform value = FindTransform(scene, objectName);
            return value as RectTransform;
        }
    }
}
#endif
