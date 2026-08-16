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
            AssemblyReloadEvents.beforeAssemblyReload -= ClearInvalidVolumeEditors;
            AssemblyReloadEvents.beforeAssemblyReload += ClearInvalidVolumeEditors;
        }

        private static void ClearInvalidVolumeEditors()
        {
            // Unity 6 can keep embedded URP Volume editors alive with null targets across a domain reload.
            ActiveEditorTracker tracker = ActiveEditorTracker.sharedTracker;
            bool hasVolumeEditor = false;
            foreach (UnityEditor.Editor editor in tracker.activeEditors)
            {
                if (editor == null) continue;
                if (editor.target is VolumeProfile || editor.target is VolumeComponent || IsVolumeComponentEditor(editor.GetType()))
                {
                    hasVolumeEditor = true;
                    break;
                }
            }

            if (!hasVolumeEditor) return;
            tracker.isLocked = false;
            Selection.activeObject = null;
            tracker.ForceRebuild();
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
