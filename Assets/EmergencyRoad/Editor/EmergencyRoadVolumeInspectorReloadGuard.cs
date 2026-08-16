#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.Rendering;

namespace EmergencyRoad.Editor
{
    [InitializeOnLoad]
    internal static class EmergencyRoadVolumeInspectorReloadGuard
    {
        static EmergencyRoadVolumeInspectorReloadGuard()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= PrepareVolumeInspectorForObjectInvalidation;
            AssemblyReloadEvents.beforeAssemblyReload += PrepareVolumeInspectorForObjectInvalidation;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += RepairInvalidVolumeEditorsAfterReload;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                PrepareVolumeInspectorForObjectInvalidation();
            else if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += RepairInvalidVolumeEditorsAfterReload;
        }

        private static void PrepareVolumeInspectorForObjectInvalidation()
        {
            // Unity 6 can keep embedded URP Volume editors alive with null targets across a domain reload.
            ActiveEditorTracker tracker = ActiveEditorTracker.sharedTracker;
            if (!HasVolumeEditor(tracker, false)) return;

            tracker.isLocked = false;
            Selection.activeObject = null;
            // Do not call ForceRebuild here: the targets are being destroyed and
            // rebuilding now is what creates MotionBlurEditor with a null target.
        }

        private static void RepairInvalidVolumeEditorsAfterReload()
        {
            // This runs only after Unity has recreated the inspected objects.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RepairInvalidVolumeEditorsAfterReload;
                return;
            }

            ActiveEditorTracker tracker = ActiveEditorTracker.sharedTracker;
            if (!HasVolumeEditor(tracker, true)) return;

            tracker.isLocked = false;
            Selection.activeObject = null;
            tracker.ForceRebuild();
        }

        private static bool HasVolumeEditor(ActiveEditorTracker tracker, bool invalidOnly)
        {
            foreach (UnityEditor.Editor editor in tracker.activeEditors)
            {
                if (editor == null) continue;
                bool isVolumeEditor = editor.target is VolumeProfile
                    || editor.target is VolumeComponent
                    || IsVolumeComponentEditor(editor.GetType());
                if (!isVolumeEditor) continue;
                if (!invalidOnly || editor.target == null || editor.targets == null || editor.targets.Length == 0)
                    return true;
            }
            return false;
        }

        private static bool IsVolumeComponentEditor(System.Type type)
        {
            for (System.Type current = type; current != null; current = current.BaseType)
                if (current.Name == "VolumeComponentEditor") return true;
            return false;
        }
    }
}
#endif
