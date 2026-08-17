#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.Rendering;

namespace EmergencyRoad.Editor
{
    /// <summary>
    /// Prevents Unity 6 URP embedded Volume editors from surviving a script/play-mode
    /// reload with destroyed targets. Never ForceRebuild an invalid Volume editor:
    /// doing so is what can create Bloom/DoF/MotionBlur editors with null targets.
    /// </summary>
    [InitializeOnLoad]
    internal static class EmergencyRoadVolumeInspectorReloadGuard
    {
        private static readonly Type InspectorWindowType =
            typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.InspectorWindow");

        private static readonly PropertyInfo InspectorTrackerProperty = InspectorWindowType?.GetProperty(
            "tracker", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly PropertyInfo InspectorLockedProperty = InspectorWindowType?.GetProperty(
            "isLocked", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        static EmergencyRoadVolumeInspectorReloadGuard()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseVolumeInspectors;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseVolumeInspectors;

            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationStarted += OnCompilationStarted;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnCompilationStarted(object context)
        {
            ReleaseVolumeInspectors();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode || state == PlayModeStateChange.ExitingPlayMode)
                ReleaseVolumeInspectors();
        }

        internal static void ReleaseVolumeInspectors()
        {
            List<(ActiveEditorTracker tracker, UnityEngine.Object window)> trackers = new();
            HashSet<ActiveEditorTracker> visited = new();

            AddVolumeTracker(ActiveEditorTracker.sharedTracker, null, visited, trackers);

            if (InspectorWindowType != null && InspectorTrackerProperty != null)
            {
                UnityEngine.Object[] windows = Resources.FindObjectsOfTypeAll(InspectorWindowType);
                foreach (UnityEngine.Object window in windows)
                {
                    ActiveEditorTracker tracker = InspectorTrackerProperty.GetValue(window) as ActiveEditorTracker;
                    AddVolumeTracker(tracker, window, visited, trackers);
                }
            }

            if (trackers.Count == 0) return;

            foreach ((ActiveEditorTracker tracker, UnityEngine.Object window) in trackers)
            {
                tracker.isLocked = false;
                if (window != null)
                    InspectorLockedProperty?.SetValue(window, false);
            }

            // Clearing selection lets Unity dispose the embedded URP editors while
            // their targets still exist. Intentionally do NOT call ForceRebuild.
            Selection.activeObject = null;
        }

        private static void AddVolumeTracker(
            ActiveEditorTracker tracker,
            UnityEngine.Object window,
            HashSet<ActiveEditorTracker> visited,
            List<(ActiveEditorTracker tracker, UnityEngine.Object window)> result)
        {
            if (tracker == null || !visited.Add(tracker) || !HasVolumeEditor(tracker)) return;
            result.Add((tracker, window));
        }

        private static bool HasVolumeEditor(ActiveEditorTracker tracker)
        {
            foreach (UnityEditor.Editor editor in tracker.activeEditors)
            {
                if (editor == null) continue;

                if (editor.target is Volume ||
                    editor.target is VolumeProfile ||
                    editor.target is VolumeComponent ||
                    IsVolumeEditor(editor.GetType()))
                    return true;
            }

            return false;
        }

        private static bool IsVolumeEditor(Type type)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                if (current.Name == "VolumeEditor" || current.Name == "VolumeComponentEditor")
                    return true;
            }

            return false;
        }
    }
}
#endif
