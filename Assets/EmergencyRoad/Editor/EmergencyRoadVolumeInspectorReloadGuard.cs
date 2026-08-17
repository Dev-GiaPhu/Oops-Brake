#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.Rendering;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace EmergencyRoad.Editor
{
    [InitializeOnLoad]
    internal static class EmergencyRoadVolumeInspectorReloadGuard
    {
        private static readonly Type InspectorWindowType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");
        private static readonly PropertyInfo InspectorTrackerProperty = InspectorWindowType?.GetProperty(
            "tracker", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly PropertyInfo InspectorLockedProperty = InspectorWindowType?.GetProperty(
            "isLocked", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        static EmergencyRoadVolumeInspectorReloadGuard()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= PrepareVolumeInspectorForObjectInvalidation;
            AssemblyReloadEvents.beforeAssemblyReload += PrepareVolumeInspectorForObjectInvalidation;
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.delayCall += RepairInvalidVolumeEditorsAfterReload;
        }

        private static void OnCompilationStarted(object context)
        {
            // At this point the inspected Volume sub-assets are still valid. Rebuild every Inspector now,
            // before URP destroys/recreates them during the following domain reload.
            ReleaseVolumeInspectors(rebuildWhileTargetsAreValid: true, invalidOnly: false);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                ReleaseVolumeInspectors(rebuildWhileTargetsAreValid: true, invalidOnly: false);
            else if (state == PlayModeStateChange.EnteredEditMode || state == PlayModeStateChange.EnteredPlayMode)
                EditorApplication.delayCall += RepairInvalidVolumeEditorsAfterReload;
        }

        private static void PrepareVolumeInspectorForObjectInvalidation()
        {
            // The reload has already started, so only release the selection here.
            // ForceRebuild at this late stage can itself construct a URP editor with a null target.
            ReleaseVolumeInspectors(rebuildWhileTargetsAreValid: false, invalidOnly: false);
        }

        private static void RepairInvalidVolumeEditorsAfterReload()
        {
            // This runs only after Unity has recreated the inspected objects.
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += RepairInvalidVolumeEditorsAfterReload;
                return;
            }

            ReleaseVolumeInspectors(rebuildWhileTargetsAreValid: true, invalidOnly: true);
        }

        private static void ReleaseVolumeInspectors(bool rebuildWhileTargetsAreValid, bool invalidOnly)
        {
            List<(ActiveEditorTracker tracker, UnityEngine.Object window)> volumeTrackers = new();
            HashSet<ActiveEditorTracker> visited = new();

            AddVolumeTracker(ActiveEditorTracker.sharedTracker, null, invalidOnly, visited, volumeTrackers);

            if (InspectorWindowType != null && InspectorTrackerProperty != null)
            {
                UnityEngine.Object[] inspectorWindows = Resources.FindObjectsOfTypeAll(InspectorWindowType);
                foreach (UnityEngine.Object window in inspectorWindows)
                {
                    ActiveEditorTracker tracker = InspectorTrackerProperty.GetValue(window) as ActiveEditorTracker;
                    AddVolumeTracker(tracker, window, invalidOnly, visited, volumeTrackers);
                }
            }

            if (volumeTrackers.Count == 0) return;

            foreach ((ActiveEditorTracker tracker, UnityEngine.Object window) in volumeTrackers)
            {
                tracker.isLocked = false;
                if (window != null)
                    InspectorLockedProperty?.SetValue(window, false);
            }

            Selection.activeObject = null;

            if (!rebuildWhileTargetsAreValid) return;

            foreach ((ActiveEditorTracker tracker, _) in volumeTrackers)
                tracker.ForceRebuild();
        }

        private static void AddVolumeTracker(
            ActiveEditorTracker tracker,
            UnityEngine.Object window,
            bool invalidOnly,
            HashSet<ActiveEditorTracker> visited,
            List<(ActiveEditorTracker tracker, UnityEngine.Object window)> result)
        {
            if (tracker == null || !visited.Add(tracker) || !HasVolumeEditor(tracker, invalidOnly)) return;
            result.Add((tracker, window));
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
