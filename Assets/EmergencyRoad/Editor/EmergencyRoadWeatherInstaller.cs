#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace EmergencyRoad.Editor
{
    public static class EmergencyRoadWeatherInstaller
    {
        private const string GameScenePath = "Assets/Scenes/Game.unity";
        private const string WetnessShaderPath = "Assets/EmergencyRoad/Shaders/EmergencyGlobalWetness.shader";
        private const string RainShaderPath = "Assets/EmergencyRoad/Shaders/EmergencyRainDrop.shader";
        private const string WetnessMaterialPath = "Assets/EmergencyRoad/Resources/EmergencyGlobalWetness.mat";
        private const string RainMaterialPath = "Assets/EmergencyRoad/Resources/EmergencyRainDrop.mat";
        private const string PcRendererPath = "Assets/Settings/PC_Renderer.asset";
        private const string MobileRendererPath = "Assets/Settings/Mobile_Renderer.asset";
        private const string FeatureName = "Emergency Global Wetness - Clear Weather Has Zero Cost";

        [MenuItem("Tools/Emergency Road/Weather/Install Or Repair Weather System")]
        public static void InstallOrRepairFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Emergency Road] Hay thoat Play Mode truoc khi cai dat Weather System.");
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != GameScenePath)
            {
                Debug.LogWarning("[Emergency Road] Hay mo scene Game roi chay lai lenh Weather/Install Or Repair.");
                return;
            }

            EnsureWeatherAssets();
            bool changed = EnsureWeatherForScene(scene);
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Emergency Road] Weather System da san sang: Clear/Rain, sam chop, do uot toan canh, vung nuoc va gon mua.");
            Selection.activeObject = FindInScene<EmergencyWeatherSystem>(scene)?.gameObject;
        }

        public static bool EnsureWeatherForScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded || scene.path != GameScenePath) return false;

            EnsureWeatherAssets();
            EmergencyRoadSceneAuthoring authoring = FindInScene<EmergencyRoadSceneAuthoring>(scene);
            Transform preferredParent = authoring != null && authoring.PreviewRoot != null
                ? authoring.PreviewRoot
                : authoring != null ? authoring.transform : null;
            Camera gameplayCamera = FindInScene<Camera>(scene);
            return EnsureWeatherForScene(scene, preferredParent, gameplayCamera);
        }

        public static bool EnsureWeatherForScene(Scene scene, Transform preferredParent, Camera gameplayCamera)
        {
            if (!scene.IsValid() || !scene.isLoaded) return false;

            bool changed = false;
            EmergencyWeatherSystem weather = FindInScene<EmergencyWeatherSystem>(scene);
            if (weather == null)
            {
                GameObject root = new("WEATHER SYSTEM - SCENE AUTHORED");
                SceneManager.MoveGameObjectToScene(root, scene);
                if (preferredParent != null) root.transform.SetParent(preferredParent, false);
                weather = root.AddComponent<EmergencyWeatherSystem>();
                Undo.RegisterCreatedObjectUndo(root, "Create Emergency Road Weather System");
                changed = true;
            }

            SerializedObject weatherObject = new(weather);
            EmergencyRoadGame gameplay = GetReference<EmergencyRoadGame>(weatherObject, "gameplay");
            if (gameplay == null)
            {
                gameplay = FindInScene<EmergencyRoadGame>(scene);
                changed |= SetReference(weatherObject, "gameplay", gameplay);
            }

            ParticleSystem rain = GetReference<ParticleSystem>(weatherObject, "rainParticles");
            if (rain == null)
            {
                rain = EnsureRainParticles(weather.transform, ref changed);
                changed |= SetReference(weatherObject, "rainParticles", rain);
            }

            Light lightning = GetReference<Light>(weatherObject, "lightningLight");
            if (lightning == null)
            {
                lightning = EnsureLightning(weather.transform, ref changed);
                changed |= SetReference(weatherObject, "lightningLight", lightning);
            }

            AudioSource rainAudio = GetReference<AudioSource>(weatherObject, "rainAudioSource");
            if (rainAudio == null)
            {
                rainAudio = EnsureAudioSource(weather.transform, "Rain Loop Audio - Drop Clip Here", true, ref changed);
                changed |= SetReference(weatherObject, "rainAudioSource", rainAudio);
            }

            AudioSource thunderAudio = GetReference<AudioSource>(weatherObject, "thunderAudioSource");
            if (thunderAudio == null)
            {
                thunderAudio = EnsureAudioSource(weather.transform, "Thunder One Shot Audio - Drop Clips On Parent", false, ref changed);
                changed |= SetReference(weatherObject, "thunderAudioSource", thunderAudio);
            }

            Transform followTarget = GetReference<Transform>(weatherObject, "followTarget");
            if (followTarget == null && gameplayCamera != null)
                changed |= SetReference(weatherObject, "followTarget", gameplayCamera.transform);

            weatherObject.ApplyModifiedPropertiesWithoutUndo();
            if (changed)
            {
                EditorUtility.SetDirty(weather);
                EditorSceneManager.MarkSceneDirty(scene);
            }

            return changed;
        }

        public static void EnsureWeatherAssets()
        {
            Material wetnessMaterial = EnsureMaterial(WetnessMaterialPath, WetnessShaderPath, null);
            EnsureMaterial(RainMaterialPath, RainShaderPath, new Color(.58f, .78f, 1f, .58f));
            if (wetnessMaterial == null) return;

            EnsureRendererFeature(PcRendererPath, wetnessMaterial);
            EnsureRendererFeature(MobileRendererPath, wetnessMaterial);
        }

        private static Material EnsureMaterial(string materialPath, string shaderPath, Color? color)
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderPath);
            if (shader == null) return null;

            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(shader) { name = System.IO.Path.GetFileNameWithoutExtension(materialPath) };
                if (color.HasValue) material.SetColor("_BaseColor", color.Value);
                AssetDatabase.CreateAsset(material, materialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static void EnsureRendererFeature(string rendererPath, Material wetnessMaterial)
        {
            ScriptableRendererData rendererData = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(rendererPath);
            if (rendererData == null) return;

            EmergencyWetnessRendererFeature feature = null;
            foreach (ScriptableRendererFeature candidate in rendererData.rendererFeatures)
            {
                if (candidate is EmergencyWetnessRendererFeature wetnessFeature)
                {
                    feature = wetnessFeature;
                    break;
                }
            }

            bool created = false;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<EmergencyWetnessRendererFeature>();
                feature.name = FeatureName;
                feature.hideFlags = HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(feature, rendererData);
                rendererData.rendererFeatures.Add(feature);
                created = true;
            }

            bool changed = created;
            changed |= AssignIfDifferent(ref feature.passMaterial, wetnessMaterial);
            changed |= AssignIfDifferent(ref feature.injectionPoint, FullScreenPassRendererFeature.InjectionPoint.BeforeRenderingPostProcessing);
            ScriptableRenderPassInput inputs = ScriptableRenderPassInput.Color | ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal;
            changed |= AssignIfDifferent(ref feature.requirements, inputs);

            if (!feature.fetchColorBuffer) { feature.fetchColorBuffer = true; changed = true; }
            if (feature.bindDepthStencilAttachment) { feature.bindDepthStencilAttachment = false; changed = true; }
            if (feature.passIndex != 0) { feature.passIndex = 0; changed = true; }
            if (feature.name != FeatureName) { feature.name = FeatureName; changed = true; }

            if (!changed) return;

            feature.Create();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(rendererData);
            rendererData.SetDirty();
            AssetDatabase.SaveAssetIfDirty(rendererData);
            RepairRendererFeatureMap(rendererData, feature);
        }

        private static void RepairRendererFeatureMap(ScriptableRendererData rendererData, ScriptableRendererFeature feature)
        {
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId)) return;

            int featureIndex = rendererData.rendererFeatures.IndexOf(feature);
            if (featureIndex < 0) return;

            SerializedObject rendererObject = new(rendererData);
            SerializedProperty map = rendererObject.FindProperty("m_RendererFeatureMap");
            if (map == null) return;

            while (map.arraySize < rendererData.rendererFeatures.Count)
                map.InsertArrayElementAtIndex(map.arraySize);
            while (map.arraySize > rendererData.rendererFeatures.Count)
                map.DeleteArrayElementAtIndex(map.arraySize - 1);

            map.GetArrayElementAtIndex(featureIndex).longValue = localId;
            rendererObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rendererData);
        }

        private static ParticleSystem EnsureRainParticles(Transform parent, ref bool changed)
        {
            Transform child = parent.Find("Rain VFX - EDIT PARTICLE HERE");
            GameObject rainObject;
            if (child == null)
            {
                rainObject = new GameObject("Rain VFX - EDIT PARTICLE HERE", typeof(ParticleSystem));
                rainObject.transform.SetParent(parent, false);
                changed = true;
            }
            else
            {
                rainObject = child.gameObject;
            }

            ParticleSystem particles = rainObject.GetComponent<ParticleSystem>();
            if (particles == null)
            {
                particles = rainObject.AddComponent<ParticleSystem>();
                changed = true;
            }

            if (!changed) return particles;

            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            rainObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var main = particles.main;
            main.duration = 2f;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.8f, 1.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(27f, 36f);
            main.startSize = new ParticleSystem.MinMaxCurve(.025f, .05f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(.7f, .84f, 1f, .38f), new Color(.82f, .91f, 1f, .62f));
            main.maxParticles = 1200;

            var emission = particles.emission;
            emission.rateOverTime = 0f;

            var shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(36f, 54f, .5f);

            var noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = .16f;
            noise.frequency = .12f;
            noise.scrollSpeed = .25f;
            noise.octaveCount = 1;

            var colorOverLifetime = particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fade = new();
            fade.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, .08f),
                    new GradientAlphaKey(.7f, .75f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = fade;

            ParticleSystemRenderer renderer = rainObject.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = .08f;
            renderer.lengthScale = 5.5f;
            renderer.cameraVelocityScale = 0f;
            renderer.sortMode = ParticleSystemSortMode.Distance;
            Material rainMaterial = AssetDatabase.LoadAssetAtPath<Material>(RainMaterialPath);
            if (renderer.sharedMaterial == null) renderer.sharedMaterial = rainMaterial;

            EditorUtility.SetDirty(particles);
            EditorUtility.SetDirty(renderer);
            return particles;
        }

        private static Light EnsureLightning(Transform parent, ref bool changed)
        {
            Transform child = parent.Find("Lightning Flash Light - EDIT HERE");
            GameObject lightObject;
            if (child == null)
            {
                lightObject = new GameObject("Lightning Flash Light - EDIT HERE", typeof(Light));
                lightObject.transform.SetParent(parent, false);
                changed = true;
            }
            else
            {
                lightObject = child.gameObject;
            }

            Light light = lightObject.GetComponent<Light>();
            if (light == null)
            {
                light = lightObject.AddComponent<Light>();
                changed = true;
            }

            if (changed)
            {
                lightObject.transform.localRotation = Quaternion.Euler(48f, -32f, 0f);
                light.type = LightType.Directional;
                light.color = new Color(.7f, .82f, 1f);
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                light.enabled = false;
                EditorUtility.SetDirty(light);
            }

            return light;
        }

        private static AudioSource EnsureAudioSource(Transform parent, string name, bool loop, ref bool changed)
        {
            Transform child = parent.Find(name);
            GameObject audioObject;
            if (child == null)
            {
                audioObject = new GameObject(name, typeof(AudioSource));
                audioObject.transform.SetParent(parent, false);
                changed = true;
            }
            else
            {
                audioObject = child.gameObject;
            }

            AudioSource source = audioObject.GetComponent<AudioSource>();
            if (source == null)
            {
                source = audioObject.AddComponent<AudioSource>();
                changed = true;
            }

            if (changed)
            {
                source.playOnAwake = false;
                source.loop = loop;
                source.spatialBlend = 0f;
                source.priority = loop ? 96 : 64;
                EditorUtility.SetDirty(source);
            }

            return source;
        }

        private static T GetReference<T>(SerializedObject owner, string propertyName) where T : Object
        {
            SerializedProperty property = owner.FindProperty(propertyName);
            return property != null ? property.objectReferenceValue as T : null;
        }

        private static bool SetReference(SerializedObject owner, string propertyName, Object value)
        {
            SerializedProperty property = owner.FindProperty(propertyName);
            if (property == null || property.objectReferenceValue == value) return false;
            property.objectReferenceValue = value;
            return true;
        }

        private static bool AssignIfDifferent<T>(ref T current, T value)
        {
            if (System.Collections.Generic.EqualityComparer<T>.Default.Equals(current, value)) return false;
            current = value;
            return true;
        }

        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                T result = root.GetComponentInChildren<T>(true);
                if (result != null) return result;
            }
            return null;
        }
    }
}
#endif
