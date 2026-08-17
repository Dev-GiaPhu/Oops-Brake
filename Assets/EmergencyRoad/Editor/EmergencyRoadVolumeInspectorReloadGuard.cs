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
    /// Workaround for Unity 6 Inspector/URP editor state surviving play-mode or script
    /// reload with destroyed targets. The play-mode path deliberately unlocks every
    /// Inspector, because Unity's stale-target regression is not limited to Volume
    /// editors once an Inspector has been locked. Never ForceRebuild an invalid
    /// editor; disposing it before the transition is the safe path.
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

        private static bool playTransitionPrepared;

        static EmergencyRoadVolumeInspectorReloadGuard()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseVolumeInspectors;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseVolumeInspectors;

            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationStarted += OnCompilationStarted;

            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.update += OnEditorUpdate;
        }

        private static void OnCompilationStarted(object context)
        {
            ReleaseVolumeInspectors();
        }

        private static void OnEditorUpdate()
        {
            bool preparingPlay = EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isPlaying;

            if (preparingPlay)
            {
                if (!playTransitionPrepared)
                {
                    playTransitionPrepared = true;
                    ReleaseAllInspectorsBeforePlay();
                }
            }
            else
            {
                playTransitionPrepared = false;
            }
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                // Fallback in case Unity reaches the play-mode callback before our
                // update-loop transition detector on a particular editor frame.
                ReleaseAllInspectorsBeforePlay();
            }
            else if (state == PlayModeStateChange.ExitingPlayMode)
            {
                ReleaseAllInspectorsBeforePlay();
            }
        }

        private static void ReleaseAllInspectorsBeforePlay()
        {
            HashSet<ActiveEditorTracker> visited = new();
            UnlockTracker(ActiveEditorTracker.sharedTracker, visited);

            if (InspectorWindowType != null)
            {
                UnityEngine.Object[] windows = Resources.FindObjectsOfTypeAll(InspectorWindowType);
                foreach (UnityEngine.Object window in windows)
                {
                    TryUnlockWindow(window, visited);
                }
            }

            // Clear every selection before Unity starts replacing scene/editor
            // objects for Play Mode. This disposes embedded Volume component editors
            // while their targets are still valid.
            Selection.objects = Array.Empty<UnityEngine.Object>();
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
                    ActiveEditorTracker tracker = GetTracker(window);
                    AddVolumeTracker(tracker, window, visited, trackers);
                }
            }

            if (trackers.Count == 0)
                return;

            foreach ((ActiveEditorTracker tracker, UnityEngine.Object window) in trackers)
            {
                UnlockTracker(tracker, null);
                if (window != null)
                    TrySetInspectorWindowLocked(window, false);
            }

            Selection.objects = Array.Empty<UnityEngine.Object>();
        }

        private static void TryUnlockWindow(UnityEngine.Object window, HashSet<ActiveEditorTracker> visited)
        {
            TrySetInspectorWindowLocked(window, false);
            UnlockTracker(GetTracker(window), visited);
        }

        private static ActiveEditorTracker GetTracker(UnityEngine.Object window)
        {
            if (window == null || InspectorTrackerProperty == null)
                return null;

            try
            {
                return InspectorTrackerProperty.GetValue(window) as ActiveEditorTracker;
            }
            catch
            {
                return null;
            }
        }

        private static void UnlockTracker(ActiveEditorTracker tracker, HashSet<ActiveEditorTracker> visited)
        {
            if (tracker == null)
                return;

            if (visited != null && !visited.Add(tracker))
                return;

            try
            {
                tracker.isLocked = false;
            }
            catch
            {
                // A tracker can disappear while Unity is rebuilding Editor windows.
            }
        }

        private static void TrySetInspectorWindowLocked(UnityEngine.Object window, bool value)
        {
            if (window == null || InspectorLockedProperty == null || !InspectorLockedProperty.CanWrite)
                return;

            try
            {
                InspectorLockedProperty.SetValue(window, value);
            }
            catch
            {
                // Reflection details differ slightly between Unity 6 patch versions.
            }
        }

        private static void AddVolumeTracker(
            ActiveEditorTracker tracker,
            UnityEngine.Object window,
            HashSet<ActiveEditorTracker> visited,
            List<(ActiveEditorTracker tracker, UnityEngine.Object window)> result)
        {
            if (tracker == null || !visited.Add(tracker) || !HasVolumeEditor(tracker))
                return;

            result.Add((tracker, window));
        }

        private static bool HasVolumeEditor(ActiveEditorTracker tracker)
        {
            UnityEditor.Editor[] editors;
            try
            {
                editors = tracker.activeEditors;
            }
            catch
            {
                return false;
            }

            foreach (UnityEditor.Editor editor in editors)
            {
                if (editor == null)
                    continue;

                UnityEngine.Object target = null;
                try
                {
                    target = editor.target;
                }
                catch
                {
                    // A destroyed target is exactly the state we want to dispose.
                }

                if (target is Volume ||
                    target is VolumeProfile ||
                    target is VolumeComponent ||
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
