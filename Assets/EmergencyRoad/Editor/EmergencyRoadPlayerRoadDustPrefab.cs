#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace EmergencyRoad.Editor
{
    [InitializeOnLoad]
    public static class EmergencyRoadPlayerRoadDustPrefab
    {
        private const string PrefabPath = "Assets/EmergencyRoad/Resources/PlayerRoadDust.prefab";
        private const string VfxMaterialPath = "Assets/EmergencyRoad/Resources/EmergencyRoadVFX.mat";

        static EmergencyRoadPlayerRoadDustPrefab()
        {
            EditorApplication.delayCall += EnsurePrefabExists;
        }

        [MenuItem("Tools/Emergency Road/Create Missing Player Road Dust Prefab")]
        public static void EnsurePrefabExists()
        {
            if (EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
                return;

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));

            var root = new GameObject("PlayerRoadDust");
            root.transform.localPosition = new Vector3(0f, 0.1f, -1.8f);
            root.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            var particles = root.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = particles.main;
            main.duration = 5f;
            main.loop = true;
            main.playOnAwake = true;
            main.startLifetime = 0.45f;
            main.startSpeed = 0.35f;
            main.startSize = 0.24f;
            main.startColor = new Color(0.7f, 0.72f, 0.68f, 0.18f);
            main.maxParticles = 32;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 14f;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(1.25f, 0.05f, 0.2f);

            var renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(VfxMaterialPath);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Emergency Road] Đã tạo prefab khói sau xe tại Assets/EmergencyRoad/Resources/PlayerRoadDust.prefab. Từ giờ chỉnh trực tiếp prefab này trong Inspector.");
        }
    }
}
#endif
