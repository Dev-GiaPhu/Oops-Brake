#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace EmergencyRoad.Editor
{
    [InitializeOnLoad]
    public static class EmergencyRoadTrafficMeshReadability
    {
        static EmergencyRoadTrafficMeshReadability()
        {
            EditorApplication.delayCall += EnsureTrafficMeshesAreReadable;
        }

        private static void EnsureTrafficMeshesAreReadable()
        {
            if (EditorApplication.isCompiling)
            {
                EditorApplication.delayCall += EnsureTrafficMeshesAreReadable;
                return;
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            var catalog = Resources.Load<EmergencyRoadCatalog>("EmergencyRoadCatalog");
            if (catalog == null || catalog.trafficVehicles == null) return;

            var modelPaths = new HashSet<string>();
            foreach (var prefab in catalog.trafficVehicles)
            {
                if (prefab == null) continue;
                string prefabPath = AssetDatabase.GetAssetPath(prefab);
                foreach (string dependency in AssetDatabase.GetDependencies(prefabPath, true))
                {
                    if (AssetImporter.GetAtPath(dependency) is ModelImporter)
                        modelPaths.Add(dependency);
                }
            }

            foreach (string path in modelPaths)
            {
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer || importer.isReadable) continue;
                importer.isReadable = true;
                importer.SaveAndReimport();
                Debug.Log($"[Emergency Road] Enabled Read/Write for traffic crash deformation: {path}");
            }
        }
    }
}
#endif
