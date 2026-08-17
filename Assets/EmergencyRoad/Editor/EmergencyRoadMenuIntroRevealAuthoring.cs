#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace EmergencyRoad.Editor
{
    [InitializeOnLoad]
    public static class EmergencyRoadMenuIntroRevealAuthoring
    {
        private const string MenuPath = "Assets/Scenes/Menu.unity";
        private const string TopBarName = "Top Bar";

        static EmergencyRoadMenuIntroRevealAuthoring()
        {
            EditorApplication.delayCall += WireLoadedMenu;
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        [DidReloadScripts]
        private static void WireAfterScriptsReload()
        {
            EditorApplication.delayCall += WireLoadedMenu;
        }

        [MenuItem("Tools/Emergency Road/Setup Menu Commercial Reveal")]
        public static void SetupMenuCommercialReveal()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Emergency Road] Thoat Play Mode truoc khi setup Menu Commercial Reveal.");
                return;
            }

            Scene menuScene = SceneManager.GetSceneByPath(MenuPath);
            bool openedTemporarily = !menuScene.IsValid() || !menuScene.isLoaded;
            if (openedTemporarily)
                menuScene = EditorSceneManager.OpenScene(MenuPath, OpenSceneMode.Additive);

            if (WireScene(menuScene))
            {
                EditorSceneManager.MarkSceneDirty(menuScene);
                EditorSceneManager.SaveScene(menuScene);
                Debug.Log("[Emergency Road] Menu intro reveal da duoc wire va luu vao scene.");
            }

            if (openedTemporarily)
                EditorSceneManager.CloseScene(menuScene, true);
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.path != MenuPath) return;
            EditorApplication.delayCall += WireLoadedMenu;
        }

        private static void WireLoadedMenu()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode) return;

            Scene menuScene = SceneManager.GetSceneByPath(MenuPath);
            if (!menuScene.IsValid() || !menuScene.isLoaded) return;

            if (!WireScene(menuScene)) return;
            EditorSceneManager.MarkSceneDirty(menuScene);
            EditorSceneManager.SaveScene(menuScene);
        }

        private static bool WireScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return false;

            EmergencyRoadMenuView view = FindInScene<EmergencyRoadMenuView>(scene);
            if (view == null || view.mainMenuPanel == null)
            {
                Debug.LogWarning("[Emergency Road] Chua the wire intro reveal: Menu View/Main Menu Panel chua san sang.");
                return false;
            }

            EmergencyRoadStartupIntro intro = FindInScene<EmergencyRoadStartupIntro>(scene);
            if (intro == null)
            {
                EmergencyRoadStartupIntroAuthoring.SetupMenuStartupIntro();
                intro = FindInScene<EmergencyRoadStartupIntro>(scene);
                if (intro == null) return false;
            }

            bool changed = false;
            EmergencyRoadMenuIntroReveal reveal = intro.GetComponent<EmergencyRoadMenuIntroReveal>();
            if (reveal == null)
            {
                reveal = intro.gameObject.AddComponent<EmergencyRoadMenuIntroReveal>();
                changed = true;
            }

            Graphic panelBackground = view.mainMenuPanel.GetComponent<Graphic>();
            Transform topBar = FindTransform(scene, TopBarName);
            EmergencyPanelTransition topBarTransition = topBar != null
                ? EnsureTransition(
                    topBar.gameObject,
                    EmergencyPanelTransition.TransitionStyle.SlideFade,
                    new Vector2(0f, 30f),
                    1f,
                    .36f,
                    ref changed)
                : null;

            Button[] buttons =
            {
                view.play,
                view.selectVehicle,
                view.settingsOpen,
                view.quit
            };

            EmergencyPanelTransition[] buttonTransitions = new EmergencyPanelTransition[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                buttonTransitions[i] = EnsureTransition(
                    buttons[i].gameObject,
                    EmergencyPanelTransition.TransitionStyle.ScaleSlideFade,
                    new Vector2(52f, 0f),
                    .94f,
                    .34f,
                    ref changed);
            }

            SerializedObject revealSO = new(reveal);
            revealSO.Update();
            changed |= SetReference(revealSO, "menuPanelBackground", panelBackground);
            changed |= SetReference(revealSO, "topBarTransition", topBarTransition);
            changed |= SetReferenceArray(revealSO, "buttonTransitions", buttonTransitions);
            changed |= SetFloat(revealSO, "leadDelay", .055f);
            changed |= SetFloat(revealSO, "buttonStagger", .075f);
            changed |= SetFloat(revealSO, "backgroundFadeDuration", .3f);
            changed |= SetFloat(revealSO, "settlePadding", .035f);
            if (revealSO.ApplyModifiedPropertiesWithoutUndo()) changed = true;

            SerializedObject introSO = new(intro);
            introSO.Update();
            changed |= SetReference(introSO, "menuReveal", reveal);
            if (introSO.ApplyModifiedPropertiesWithoutUndo()) changed = true;

            if (changed)
            {
                EditorUtility.SetDirty(reveal);
                EditorUtility.SetDirty(intro);
                if (panelBackground != null) EditorUtility.SetDirty(panelBackground);
            }

            return changed;
        }

        private static EmergencyPanelTransition EnsureTransition(
            GameObject target,
            EmergencyPanelTransition.TransitionStyle style,
            Vector2 hiddenOffset,
            float hiddenScale,
            float duration,
            ref bool changed)
        {
            CanvasGroup canvasGroup = target.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = target.AddComponent<CanvasGroup>();
                changed = true;
            }

            EmergencyPanelTransition transition = target.GetComponent<EmergencyPanelTransition>();
            if (transition == null)
            {
                transition = target.AddComponent<EmergencyPanelTransition>();
                changed = true;
            }

            SerializedObject transitionSO = new(transition);
            transitionSO.Update();
            changed |= SetReference(transitionSO, "canvasGroup", canvasGroup);
            changed |= SetReference(transitionSO, "animatedRoot", target.transform as RectTransform);
            changed |= SetEnum(transitionSO, "style", (int)style);
            changed |= SetFloat(transitionSO, "duration", duration);
            changed |= SetVector2(transitionSO, "hiddenOffset", hiddenOffset);
            changed |= SetFloat(transitionSO, "hiddenScale", hiddenScale);
            changed |= SetBool(transitionSO, "deactivateWhenHidden", true);
            if (transitionSO.ApplyModifiedPropertiesWithoutUndo()) changed = true;

            EditorUtility.SetDirty(canvasGroup);
            EditorUtility.SetDirty(transition);
            return transition;
        }

        private static bool SetReference(SerializedObject serializedObject, string propertyName, Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == value) return false;
            property.objectReferenceValue = value;
            return true;
        }

        private static bool SetReferenceArray(
            SerializedObject serializedObject,
            string propertyName,
            Object[] values)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null) return false;

            bool changed = property.arraySize != values.Length;
            if (property.arraySize != values.Length) property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                SerializedProperty element = property.GetArrayElementAtIndex(i);
                if (element.objectReferenceValue == values[i]) continue;
                element.objectReferenceValue = values[i];
                changed = true;
            }
            return changed;
        }

        private static bool SetFloat(SerializedObject serializedObject, string propertyName, float value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || Mathf.Approximately(property.floatValue, value)) return false;
            property.floatValue = value;
            return true;
        }

        private static bool SetVector2(SerializedObject serializedObject, string propertyName, Vector2 value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.vector2Value == value) return false;
            property.vector2Value = value;
            return true;
        }

        private static bool SetBool(SerializedObject serializedObject, string propertyName, bool value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.boolValue == value) return false;
            property.boolValue = value;
            return true;
        }

        private static bool SetEnum(SerializedObject serializedObject, string propertyName, int value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null || property.enumValueIndex == value) return false;
            property.enumValueIndex = value;
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
    }
}
#endif
