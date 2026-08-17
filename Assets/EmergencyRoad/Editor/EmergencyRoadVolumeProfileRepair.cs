#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace EmergencyRoad.Editor
{
    /// <summary>
    /// Repairs VolumeProfile assets that contain missing/orphaned sub-assets.
    /// Unity 6 URP can otherwise create embedded component editors with null targets,
    /// producing SerializedObjectNotCreatableException repeatedly in the Inspector.
    /// </summary>
    [InitializeOnLoad]
    internal static class EmergencyRoadVolumeProfileRepair
    {
        private const string DefaultProfilePath = "Assets/Settings/DefaultVolumeProfile.asset";
        private const string TempProfilePath = "Assets/Settings/__EmergencyRoad_VolumeProfileRepair.asset";
        private const string SessionKey = "EmergencyRoad.VolumeProfileRepair.v3";

        private static bool repairQueued;
        private static bool repairing;

        static EmergencyRoadVolumeProfileRepair()
        {
            EditorApplication.delayCall += QueueAutomaticRepair;
        }

        [MenuItem("Tools/Emergency Road/Repair URP Volume Profiles")]
        public static void RepairFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Emergency Road] Thoat Play Mode truoc khi repair URP Volume Profiles.");
                return;
            }

            SessionState.SetBool(SessionKey, false);
            QueueRepair(forceDefaultProfile: true);
        }

        private static void QueueAutomaticRepair()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            QueueRepair(forceDefaultProfile: false);
        }

        private static void QueueRepair(bool forceDefaultProfile)
        {
            if (repairing || repairQueued) return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += () => QueueRepair(forceDefaultProfile);
                return;
            }

            List<string> contaminated = FindContaminatedProfiles(forceDefaultProfile);
            if (contaminated.Count == 0)
            {
                SessionState.SetBool(SessionKey, true);
                return;
            }

            repairQueued = true;

            // Dispose any embedded URP editors while their targets are still valid.
            // Rebuilding/importing a profile while its embedded editor is alive is
            // the direct path to Object-at-index-0-is-null errors in Unity 6.
            EmergencyRoadVolumeInspectorReloadGuard.ReleaseVolumeInspectors();

            // Give the Inspector one editor frame to destroy its embedded editors
            // before replacing/reimporting the contaminated profile assets.
            EditorApplication.delayCall += () => RepairProfiles(contaminated);
        }

        private static List<string> FindContaminatedProfiles(bool forceDefaultProfile)
        {
            List<string> result = new();
            string[] guids = AssetDatabase.FindAssets("t:VolumeProfile", new[] { "Assets" });

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
                if (profile == null) continue;

                bool strictCheck = string.Equals(path, DefaultProfilePath, StringComparison.OrdinalIgnoreCase);
                if ((forceDefaultProfile && strictCheck) || IsContaminated(profile, path, strictCheck))
                    result.Add(path);
            }

            return result;
        }

        private static bool IsContaminated(VolumeProfile profile, string path, bool strictCheck)
        {
            if (profile.components == null) return true;

            foreach (VolumeComponent component in profile.components)
                if (component == null) return true;

            if (!strictCheck) return false;

            string absolutePath = Path.GetFullPath(path);
            if (File.Exists(absolutePath))
            {
                string yaml = File.ReadAllText(absolutePath);
                if (yaml.Contains("m_Script: {fileID: 0}", StringComparison.Ordinal) ||
                    yaml.Contains("Unity.RenderPipelines.Core.Editor.Tests", StringComparison.Ordinal))
                    return true;
            }

            // DefaultVolumeProfile should contain only its main profile plus the
            // components referenced by profile.components. Extra sub-assets are
            // stale/orphaned data and must not survive a package/editor reload.
            UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            HashSet<VolumeComponent> referenced = new(profile.components);
            int loadedComponents = 0;

            foreach (UnityEngine.Object asset in allAssets)
            {
                if (asset == null) return true;
                if (asset is not VolumeComponent component) continue;

                loadedComponents++;
                if (!referenced.Contains(component)) return true;
            }

            return loadedComponents != referenced.Count;
        }

        private static void RepairProfiles(List<string> paths)
        {
            repairQueued = false;
            if (repairing) return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                repairQueued = true;
                EditorApplication.delayCall += () => RepairProfiles(paths);
                return;
            }

            repairing = true;
            int repairedCount = 0;

            try
            {
                foreach (string path in paths)
                {
                    if (RebuildProfileInPlace(path))
                        repairedCount++;
                }

                SessionState.SetBool(SessionKey, true);

                if (repairedCount > 0)
                {
                    Debug.Log(
                        $"[Emergency Road] Da repair {repairedCount} URP Volume Profile bi null/orphan sub-assets. " +
                        "GUID cua profile duoc giu nguyen; cac Volume reference khong bi mat.");
                }
            }
            catch (Exception exception)
            {
                SessionState.SetBool(SessionKey, false);
                Debug.LogException(exception);
            }
            finally
            {
                CleanupTempProfile();
                repairing = false;
            }
        }

        private static bool RebuildProfileInPlace(string path)
        {
            VolumeProfile sourceProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (sourceProfile == null) return false;

            List<VolumeComponent> sourceComponents = new();
            foreach (VolumeComponent component in sourceProfile.components)
                if (component != null) sourceComponents.Add(component);

            CleanupTempProfile();

            VolumeProfile cleanProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            cleanProfile.name = sourceProfile.name;
            AssetDatabase.CreateAsset(cleanProfile, TempProfilePath);

            foreach (VolumeComponent source in sourceComponents)
            {
                Type type = source.GetType();
                if (type == null || type.IsAbstract || !typeof(VolumeComponent).IsAssignableFrom(type))
                    continue;

                VolumeComponent clone = ScriptableObject.CreateInstance(type) as VolumeComponent;
                if (clone == null) continue;

                clone.name = source.name;
                clone.hideFlags = source.hideFlags;
                AssetDatabase.AddObjectToAsset(clone, cleanProfile);
                cleanProfile.components.Add(clone);

                // Copy the actual authored parameter values and object references;
                // no runtime/default values are synthesized.
                EditorUtility.CopySerialized(source, clone);
                clone.name = source.name;
                clone.hideFlags = source.hideFlags;
                EditorUtility.SetDirty(clone);
            }

            EditorUtility.SetDirty(cleanProfile);
            AssetDatabase.SaveAssets();

            string tempAbsolutePath = Path.GetFullPath(TempProfilePath);
            string destinationAbsolutePath = Path.GetFullPath(path);
            byte[] cleanSerializedAsset = File.ReadAllBytes(tempAbsolutePath);

            // Remove only the temporary asset through AssetDatabase. The original
            // .meta is never touched, so its GUID and all external references stay intact.
            AssetDatabase.DeleteAsset(TempProfilePath);
            File.WriteAllBytes(destinationAbsolutePath, cleanSerializedAsset);

            AssetDatabase.ImportAsset(
                path,
                ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

            VolumeProfile repaired = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            bool strictCheck = string.Equals(path, DefaultProfilePath, StringComparison.OrdinalIgnoreCase);
            if (repaired == null || IsContaminated(repaired, path, strictCheck))
            {
                Debug.LogError($"[Emergency Road] Volume profile van con du lieu loi sau repair: {path}");
                return false;
            }

            return true;
        }

        private static void CleanupTempProfile()
        {
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(TempProfilePath) != null)
                AssetDatabase.DeleteAsset(TempProfilePath);
        }
    }
}
#endif
