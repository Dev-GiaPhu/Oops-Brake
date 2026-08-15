#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

namespace EmergencyRoad.Editor
{
    [InitializeOnLoad]
    public static class EmergencyRoadAuthoredRuntimeMigration
    {
        private const string CatalogPath = "Assets/EmergencyRoad/Resources/EmergencyRoadCatalog.asset";
        private const string RuntimePrefabFolder = "Assets/EmergencyRoad/Prefabs/Runtime Authored";
        private const string RoadChunkPath = RuntimePrefabFolder + "/RoadChunkRoot.prefab";
        private const string GrassPath = RuntimePrefabFolder + "/GrassGround.prefab";
        private const string SoilPath = RuntimePrefabFolder + "/SoilGround.prefab";
        private const string ObstaclePath = RuntimePrefabFolder + "/ObstacleHazard.prefab";
        private const string StoppedVehiclePath = RuntimePrefabFolder + "/StoppedVehicleHazard.prefab";
        private const string SameDirectionPath = RuntimePrefabFolder + "/SameDirectionTraffic.prefab";
        private const string CrossTrafficPath = RuntimePrefabFolder + "/CrossTrafficHazard.prefab";
        private const string RouteMarkerPath = RuntimePrefabFolder + "/RouteMarker.prefab";
        private const string MotorRushPath = RuntimePrefabFolder + "/MotorRushHazard.prefab";
        private const string MotorWarningPath = RuntimePrefabFolder + "/MotorWarningLine.prefab";
        private const string MotorWarningMaterialPath = "Assets/EmergencyRoad/Resources/MotorWarning.mat";
        private const string ImpactVfxPath = RuntimePrefabFolder + "/VehicleImpactVFX.prefab";
        private const string MotorImpactVfxPath = RuntimePrefabFolder + "/MotorImpactVFX.prefab";

        static EmergencyRoadAuthoredRuntimeMigration()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (scene.path != "Assets/Scenes/Menu.unity" && scene.path != "Assets/Scenes/Game.unity") return;
            EditorApplication.delayCall += () => MigrateScene(scene, false);
        }

        [MenuItem("Tools/Emergency Road/Migrate All To Inspector References")]
        public static void MigrateAllScenes()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("[Emergency Road] Thoát Play Mode trước khi migrate scene references.");
                return;
            }

            EnsureRuntimePrefabs();
            string previous = SceneManager.GetActiveScene().path;
            MigrateSceneAtPath("Assets/Scenes/Menu.unity");
            MigrateSceneAtPath("Assets/Scenes/Game.unity");
            if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Emergency Road] Đã chuyển Menu/Game sang Inspector references. Runtime không còn cần Find/new GameObject fallback.");
        }

        private static void MigrateSceneAtPath(string path)
        {
            if (!File.Exists(path)) return;
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            if (MigrateScene(scene, true)) EditorSceneManager.SaveScene(scene);
        }

        private static bool MigrateScene(Scene scene, bool saveAssets)
        {
            if (!scene.IsValid() || !scene.isLoaded) return false;
            EmergencyRoadSceneAuthoring authoring = FindInScene<EmergencyRoadSceneAuthoring>(scene);
            if (authoring == null) return false;

            EmergencyRoadCatalog catalog = authoring.Catalog != null
                ? authoring.Catalog
                : AssetDatabase.LoadAssetAtPath<EmergencyRoadCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError("[Emergency Road] Không tìm thấy EmergencyRoadCatalog.asset để wire scene.");
                return false;
            }

            EnsureRuntimePrefabs();
            EnsureEventSystem(scene);
            bool changed = authoring.SceneKind == EmergencyRoadSceneKind.Menu
                ? WireMenu(scene, authoring, catalog)
                : WireGame(scene, authoring, catalog);

            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (saveAssets) AssetDatabase.SaveAssets();
            }
            return changed;
        }

        private static bool WireMenu(Scene scene, EmergencyRoadSceneAuthoring authoring, EmergencyRoadCatalog catalog)
        {
            Transform root = authoring.transform;
            EmergencyRoadMenuView view = FindInScene<EmergencyRoadMenuView>(scene);
            if (view == null)
            {
                Debug.LogError("[Emergency Road] Menu scene thiếu EmergencyRoadMenuView.");
                return false;
            }
            Transform pivot = FindTransform(scene, "Selected Vehicle Preview (Ambulance)");
            if (pivot == null)
            {
                GameObject pivotObject = new("Selected Vehicle Preview (Ambulance)");
                pivotObject.transform.SetParent(authoring.PreviewRoot != null ? authoring.PreviewRoot : root, false);
                pivot = pivotObject.transform;
                Transform podium = FindTransform(scene, "Garage Podium");
                pivot.position = podium != null ? new Vector3(podium.position.x, podium.position.y + .35f, podium.position.z) : new Vector3(3.4f, .2f, 0f);
            }
            else if (pivot.GetComponentsInChildren<Renderer>(true).Length > 0)
            {
                Transform parent = pivot.parent;
                Vector3 position = pivot.position;
                Quaternion rotation = pivot.rotation;
                Vector3 scale = pivot.localScale;
                Object.DestroyImmediate(pivot.gameObject);
                GameObject emptyPivot = new("Selected Vehicle Preview (Ambulance)");
                emptyPivot.transform.SetParent(parent, true);
                emptyPivot.transform.SetPositionAndRotation(position, rotation);
                emptyPivot.transform.localScale = scale;
                pivot = emptyPivot.transform;
            }

            view.vehiclePreviewPivot = pivot;
            if (view.vehicleActionLabel == null && view.vehicleAction != null) view.vehicleActionLabel = view.vehicleAction.GetComponentInChildren<TMP_Text>(true);
            if (view.sideCollisionLabel == null && view.sideCollision != null) view.sideCollisionLabel = view.sideCollision.GetComponentInChildren<TMP_Text>(true);

            EmergencyRoadAudio audio = EnsureAudioService(scene, root, catalog);
            GameObject controllerObject = FindDirectOrSceneObject(scene, "Menu Controller");
            if (controllerObject == null)
            {
                controllerObject = new GameObject("Menu Controller");
                controllerObject.transform.SetParent(root, false);
            }
            MenuGameManager menu = GetOrAdd<MenuGameManager>(controllerObject);
            menu.ConfigureSceneReferences(catalog, view, pivot, audio);
            EditorUtility.SetDirty(menu);
            EditorUtility.SetDirty(view);
            return true;
        }

        private static bool WireGame(Scene scene, EmergencyRoadSceneAuthoring authoring, EmergencyRoadCatalog catalog)
        {
            Transform root = authoring.transform;
            EmergencyRoadGameView view = FindInScene<EmergencyRoadGameView>(scene);
            if (view == null)
            {
                Debug.LogError("[Emergency Road] Game scene thiếu EmergencyRoadGameView.");
                return false;
            }

            EmergencyRoadSceneUIFactory.EnsureFirstPersonMirrors(view);

            EmergencyRoadAudio audio = EnsureAudioService(scene, root, catalog);
            GameObject systems = FindDirectOrSceneObject(scene, "Gameplay Systems (runtime controller)");
            if (systems == null)
            {
                systems = new GameObject("Gameplay Systems (runtime controller)");
                systems.transform.SetParent(root, false);
            }

            EmergencyRoadGame game = GetOrAdd<EmergencyRoadGame>(systems);
            EmergencyRoadContinuousCoinSpawner coinSpawner = GetOrAdd<EmergencyRoadContinuousCoinSpawner>(systems);
            MotorRushDirector motorDirector = GetOrAdd<MotorRushDirector>(systems);

            Transform editPreview = FindTransform(scene, "Endless World Preview");
            Transform runtimeWorld = FindTransform(scene, "Runtime World Root");
            if (runtimeWorld == null)
            {
                GameObject go = new("Runtime World Root");
                go.transform.SetParent(root, false);
                runtimeWorld = go.transform;
            }

            Transform playerRoot = FindTransform(scene, "Player Vehicle Preview");
            if (playerRoot == null)
            {
                GameObject go = new("Player Vehicle Preview");
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(0, .55f, 0);
                playerRoot = go.transform;
            }

            BoxCollider playerCollider = GetOrAdd<BoxCollider>(playerRoot.gameObject);
            playerCollider.isTrigger = true;
            playerCollider.center = new Vector3(0, .65f, 0);
            playerCollider.size = new Vector3(2.05f, 1.5f, 3.6f);
            Rigidbody playerBody = GetOrAdd<Rigidbody>(playerRoot.gameObject);
            playerBody.isKinematic = true;
            playerBody.useGravity = false;
            EmergencyVehicleController player = GetOrAdd<EmergencyVehicleController>(playerRoot.gameObject);

            Transform visualRoot = FindDirectChild(playerRoot, "Player Visual Root");
            if (visualRoot == null)
            {
                GameObject go = new("Player Visual Root");
                go.transform.SetParent(playerRoot, false);
                visualRoot = go.transform;
            }
            foreach (Transform child in playerRoot)
            {
                if (child == visualRoot) continue;
                if (child.GetComponentsInChildren<Renderer>(true).Length > 0) child.gameObject.SetActive(false);
            }

            Camera camera = FindInScene<Camera>(scene);
            EmergencyCameraJuice cameraJuice = camera != null ? GetOrAdd<EmergencyCameraJuice>(camera.gameObject) : null;
            if (cameraJuice != null) cameraJuice.ConfigureCamera(camera);

            Transform physicsSurfaceTransform = FindDirectChild(runtimeWorld, "Road Physics Surface");
            if (physicsSurfaceTransform == null)
            {
                GameObject surface = new("Road Physics Surface");
                surface.transform.SetParent(runtimeWorld, false);
                physicsSurfaceTransform = surface.transform;
            }
            BoxCollider roadSurface = GetOrAdd<BoxCollider>(physicsSurfaceTransform.gameObject);
            physicsSurfaceTransform.localPosition = new Vector3(0, -.16f, 80f);
            roadSurface.center = Vector3.zero;
            roadSurface.size = new Vector3(Mathf.Max(40f, catalog.roadHalfWidth * 4f), .3f, 240f);
            roadSurface.isTrigger = false;

            EnsureCoinPrefab(catalog.coinPrefab);

            GameObject roadChunk = AssetDatabase.LoadAssetAtPath<GameObject>(RoadChunkPath);
            GameObject grass = AssetDatabase.LoadAssetAtPath<GameObject>(GrassPath);
            GameObject soil = AssetDatabase.LoadAssetAtPath<GameObject>(SoilPath);
            GameObject obstacle = AssetDatabase.LoadAssetAtPath<GameObject>(ObstaclePath);
            GameObject stopped = AssetDatabase.LoadAssetAtPath<GameObject>(StoppedVehiclePath);
            GameObject sameDirection = AssetDatabase.LoadAssetAtPath<GameObject>(SameDirectionPath);
            GameObject crossTraffic = AssetDatabase.LoadAssetAtPath<GameObject>(CrossTrafficPath);
            GameObject routeMarker = AssetDatabase.LoadAssetAtPath<GameObject>(RouteMarkerPath);
            GameObject motorRush = AssetDatabase.LoadAssetAtPath<GameObject>(MotorRushPath);
            GameObject warning = AssetDatabase.LoadAssetAtPath<GameObject>(MotorWarningPath);
            GameObject impact = AssetDatabase.LoadAssetAtPath<GameObject>(ImpactVfxPath);
            GameObject motorImpact = AssetDatabase.LoadAssetAtPath<GameObject>(MotorImpactVfxPath);

            SerializedObject gameSO = new(game);
            SetReference(gameSO, "catalog", catalog);
            SetReference(gameSO, "sceneView", view);
            SetReference(gameSO, "audioService", audio);
            SetReference(gameSO, "runtimeWorldRoot", runtimeWorld);
            SetReference(gameSO, "editModeWorldPreview", editPreview != null ? editPreview.gameObject : null);
            SetReference(gameSO, "playerRoot", playerRoot);
            SetReference(gameSO, "playerVisualRoot", visualRoot);
            SetReference(gameSO, "player", player);
            SetReference(gameSO, "roadPhysicsSurface", roadSurface);
            SetReference(gameSO, "gameplayCamera", camera);
            SetReference(gameSO, "cameraJuice", cameraJuice);
            SetReference(gameSO, "coinSpawner", coinSpawner);
            SetReference(gameSO, "motorRushDirector", motorDirector);
            SetReference(gameSO, "roadChunkRootPrefab", roadChunk);
            SetReference(gameSO, "grassGroundPrefab", grass);
            SetReference(gameSO, "soilGroundPrefab", soil);
            SetReference(gameSO, "obstacleHazardPrefab", obstacle);
            SetReference(gameSO, "stoppedVehicleHazardPrefab", stopped);
            SetReference(gameSO, "sameDirectionTrafficPrefab", sameDirection);
            SetReference(gameSO, "crossTrafficHazardPrefab", crossTraffic);
            SetReference(gameSO, "routeMarkerPrefab", routeMarker);
            SetReference(gameSO, "impactVfxPrefab", impact);
            SetReference(gameSO, "motorImpactVfxPrefab", motorImpact);
            gameSO.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject coinSO = new(coinSpawner);
            SetReference(coinSO, "game", game);
            coinSO.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject motorSO = new(motorDirector);
            SetReference(motorSO, "game", game);
            SetReference(motorSO, "warningLinePrefab", warning);
            SetReference(motorSO, "motorRushHazardPrefab", motorRush);
            motorSO.ApplyModifiedPropertiesWithoutUndo();

            EmergencyRoadAutoTester tester = FindInScene<EmergencyRoadAutoTester>(scene);
            if (tester != null)
            {
                SerializedObject testerSO = new(tester);
                SetReference(testerSO, "game", game);
                testerSO.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorUtility.SetDirty(game);
            EditorUtility.SetDirty(coinSpawner);
            EditorUtility.SetDirty(motorDirector);
            return true;
        }

        private static EmergencyRoadAudio EnsureAudioService(Scene scene, Transform parent, EmergencyRoadCatalog catalog)
        {
            EmergencyRoadAudio audio = FindInScene<EmergencyRoadAudio>(scene);
            if (audio == null)
            {
                GameObject go = new("Audio Service - SCENE AUTHORED");
                go.transform.SetParent(parent, false);
                audio = go.AddComponent<EmergencyRoadAudio>();
                AudioSource music = go.AddComponent<AudioSource>();
                music.playOnAwake = false;
                music.loop = true;
                AudioSource sfx = go.AddComponent<AudioSource>();
                sfx.playOnAwake = false;
                audio.ConfigureSources(music, sfx);
            }
            else
            {
                AudioSource[] sources = audio.GetComponents<AudioSource>();
                AudioSource music = sources.Length > 0 ? sources[0] : audio.gameObject.AddComponent<AudioSource>();
                AudioSource sfx = sources.Length > 1 ? sources[1] : audio.gameObject.AddComponent<AudioSource>();
                audio.ConfigureSources(music, sfx);
            }
            audio.ConfigureFromCatalog(catalog);
            EditorUtility.SetDirty(audio);
            return audio;
        }

        private static void EnsureEventSystem(Scene scene)
        {
            if (FindInScene<EventSystem>(scene) != null) return;
            GameObject eventSystem = new("EventSystem - SCENE AUTHORED", typeof(EventSystem), typeof(InputSystemUIInputModule));
            SceneManager.MoveGameObjectToScene(eventSystem, scene);
        }

        private static void EnsureRuntimePrefabs()
        {
            Directory.CreateDirectory(RuntimePrefabFolder);
            EmergencyRoadCatalog catalog = AssetDatabase.LoadAssetAtPath<EmergencyRoadCatalog>(CatalogPath);
            if (catalog == null) return;

            EnsureEmptyPrefab(RoadChunkPath, "RoadChunkRoot");
            EnsureGroundPrefab(GrassPath, "GrassGround", catalog.grassMaterial);
            EnsureGroundPrefab(SoilPath, "SoilGround", catalog.soilMaterial);
            EnsureObstaclePrefab();
            EnsureStoppedVehiclePrefab();
            EnsureSameDirectionPrefab();
            EnsureCrossTrafficPrefab();
            EnsureRouteMarkerPrefab();
            EnsureMotorRushPrefab(catalog);
            EnsureMotorWarningPrefab();
            EnsureImpactVfxPrefab(catalog);
            EnsureMotorImpactVfxPrefab(catalog);
            AssetDatabase.SaveAssets();
        }

        private static void EnsureEmptyPrefab(string path, string name)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            GameObject root = new(name);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void EnsureGroundPrefab(string path, string name, Material material)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
            GameObject root = GameObject.CreatePrimitive(PrimitiveType.Cube);
            root.name = name;
            Object.DestroyImmediate(root.GetComponent<Collider>());
            if (material != null) root.GetComponent<Renderer>().sharedMaterial = material;
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
        }

        private static void EnsureObstaclePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ObstaclePath) != null) return;
            GameObject root = new("ObstacleHazard");
            root.AddComponent<RoadHazard>();
            root.AddComponent<BoxCollider>();
            PrefabUtility.SaveAsPrefabAsset(root, ObstaclePath);
            Object.DestroyImmediate(root);
        }

        private static void EnsureStoppedVehiclePrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(StoppedVehiclePath) != null) return;
            GameObject root = new("StoppedVehicleHazard");
            root.AddComponent<RoadHazard>();
            BoxCollider box = root.AddComponent<BoxCollider>();
            EmergencyTrafficCrashResponder responder = root.AddComponent<EmergencyTrafficCrashResponder>();
            responder.ConfigurePrefabReferences(box, null, null, null);
            PrefabUtility.SaveAsPrefabAsset(root, StoppedVehiclePath);
            Object.DestroyImmediate(root);
        }

        private static void EnsureSameDirectionPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(SameDirectionPath) != null) return;
            GameObject root = new("SameDirectionTraffic");
            root.AddComponent<RoadHazard>();
            BoxCollider box = root.AddComponent<BoxCollider>();
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            SameDirectionTraffic mover = root.AddComponent<SameDirectionTraffic>();
            EmergencyTrafficCrashResponder responder = root.AddComponent<EmergencyTrafficCrashResponder>();
            responder.ConfigurePrefabReferences(box, body, mover, null);
            PrefabUtility.SaveAsPrefabAsset(root, SameDirectionPath);
            Object.DestroyImmediate(root);
        }

        private static void EnsureCrossTrafficPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(CrossTrafficPath) != null) return;
            GameObject root = new("CrossTrafficHazard");
            root.AddComponent<RoadHazard>();
            BoxCollider box = root.AddComponent<BoxCollider>();
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            SideCrossingHazard mover = root.AddComponent<SideCrossingHazard>();
            EmergencyTrafficCrashResponder responder = root.AddComponent<EmergencyTrafficCrashResponder>();
            responder.ConfigurePrefabReferences(box, body, null, mover);
            PrefabUtility.SaveAsPrefabAsset(root, CrossTrafficPath);
            Object.DestroyImmediate(root);
        }

        private static void EnsureRouteMarkerPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(RouteMarkerPath) != null) return;
            GameObject root = new("RouteMarker");
            root.AddComponent<RoadRouteMarker>();
            PrefabUtility.SaveAsPrefabAsset(root, RouteMarkerPath);
            Object.DestroyImmediate(root);
        }

        private static void EnsureMotorRushPrefab(EmergencyRoadCatalog catalog)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MotorRushPath) != null) return;
            GameObject root = new("MotorRushHazard");
            root.AddComponent<MotorRushHazard>();
            BoxCollider box = root.AddComponent<BoxCollider>();
            box.isTrigger = false;
            box.size = new Vector3(.95f, 1.35f, 2.25f);
            box.center = new Vector3(0, .45f, 0);
            Rigidbody body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            PrefabUtility.SaveAsPrefabAsset(root, MotorRushPath);
            Object.DestroyImmediate(root);
        }

        private static void EnsureMotorWarningPrefab()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MotorWarningPath) != null) return;
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MotorWarningMaterialPath);
            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
                material = new Material(shader) { color = new Color(1f, .015f, .005f, 1f) };
                AssetDatabase.CreateAsset(material, MotorWarningMaterialPath);
            }
            GameObject root = new("MotorWarningLine");
            LineRenderer line = root.AddComponent<LineRenderer>();
            line.positionCount = 22;
            line.useWorldSpace = true;
            line.widthMultiplier = .3f;
            line.numCapVertices = 4;
            line.numCornerVertices = 4;
            line.sharedMaterial = material;
            line.startColor = new Color(1f, .02f, .01f, .8f);
            line.endColor = new Color(1f, .05f, .01f, .18f);
            PrefabUtility.SaveAsPrefabAsset(root, MotorWarningPath);
            Object.DestroyImmediate(root);
        }

        private static void EnsureImpactVfxPrefab(EmergencyRoadCatalog catalog)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(ImpactVfxPath) != null) return;
            GameObject root = new("VehicleImpactVFX");
            ParticleSystem fire = CreateParticle(root.transform, "Fire", catalog.vfxParticleMaterial, Color.white, .55f, .12f, 90f, 1.25f, 240);
            ConfigureFireColor(fire);
            Light fireLight = fire.gameObject.AddComponent<Light>();
            fireLight.type = LightType.Point;
            fireLight.color = new Color(1f, .24f, .025f);
            fireLight.intensity = 2.4f;
            fireLight.range = 5.5f;
            CreateParticle(root.transform, "Smoke", catalog.vfxParticleMaterial, new Color(.18f, .18f, .2f, .48f), 1.25f, .48f, 11f, .72f);
            PrefabUtility.SaveAsPrefabAsset(root, ImpactVfxPath);
            Object.DestroyImmediate(root);
        }

        private static void EnsureMotorImpactVfxPrefab(EmergencyRoadCatalog catalog)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(MotorImpactVfxPath) != null) return;
            GameObject root = new("MotorImpactVFX");
            ParticleSystem ps = root.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = .45f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.55f, 1.05f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(.07f, .16f);
            main.startColor = Color.white;
            main.maxParticles = 140;
            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0, 110) });
            ConfigureFireColor(ps);
            if (catalog.vfxParticleMaterial != null) root.GetComponent<ParticleSystemRenderer>().sharedMaterial = catalog.vfxParticleMaterial;
            ps.Play();
            Light light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 9f;
            light.intensity = 5f;
            light.color = new Color(1f, .28f, .03f);
            root.AddComponent<EmergencyTimedDestroy>();
            PrefabUtility.SaveAsPrefabAsset(root, MotorImpactVfxPath);
            Object.DestroyImmediate(root);
        }

        private static ParticleSystem CreateParticle(Transform parent, string name, Material material, Color color, float lifetime, float size, float rate, float speed, int maxParticles = 72)
        {
            GameObject go = new(name, typeof(ParticleSystem));
            go.transform.SetParent(parent, false);
            go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            ParticleSystem ps = go.GetComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            main.maxParticles = maxParticles;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = .18f;
            if (material != null) go.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
            ps.Play();
            return ps;
        }

        private static void ConfigureFireColor(ParticleSystem fire)
        {
            var color = fire.colorOverLifetime;
            color.enabled = true;
            Gradient gradient = new();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, .9f, .2f), 0f),
                    new GradientColorKey(new Color(1f, .24f, .015f), .45f),
                    new GradientColorKey(new Color(.32f, .015f, .005f), 1f)
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(.2f, 1f) });
            color.color = gradient;
        }

        private static void EnsureCoinPrefab(GameObject prefab)
        {
            if (prefab == null) return;
            string path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab")) return;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            bool changed = false;
            if (root.GetComponentInChildren<RoadPickup>(true) == null) { root.AddComponent<RoadPickup>(); changed = true; }
            if (root.GetComponentInChildren<CoinSpinner>(true) == null) { root.AddComponent<CoinSpinner>(); changed = true; }
            Collider collider = root.GetComponentInChildren<Collider>(true);
            if (collider == null) { collider = root.AddComponent<SphereCollider>(); changed = true; }
            if (!collider.isTrigger) { collider.isTrigger = true; changed = true; }
            if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
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
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                if (transform.name == objectName) return transform;
            return null;
        }

        private static GameObject FindDirectOrSceneObject(Scene scene, string objectName)
        {
            Transform transform = FindTransform(scene, objectName);
            return transform != null ? transform.gameObject : null;
        }

        private static Transform FindDirectChild(Transform parent, string objectName)
        {
            if (parent == null) return null;
            foreach (Transform child in parent) if (child.name == objectName) return child;
            return null;
        }

        private static T GetOrAdd<T>(GameObject gameObject) where T : Component
        {
            T value = gameObject.GetComponent<T>();
            return value != null ? value : gameObject.AddComponent<T>();
        }

        private static void SetReference(SerializedObject serializedObject, string propertyName, Object value)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property != null) property.objectReferenceValue = value;
        }
    }
}
#endif
