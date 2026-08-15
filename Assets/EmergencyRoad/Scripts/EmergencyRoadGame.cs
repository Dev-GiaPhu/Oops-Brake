using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace EmergencyRoad
{
    [DisallowMultipleComponent]
    public sealed class EmergencyRoadGame : MonoBehaviour
    {
        public static float LaneWidth { get; private set; } = 5.18f;
        public static float ChunkSpacing { get; private set; } = 28f;

        [Header("SCENE REFERENCES - DRAG DIRECTLY")]
        [SerializeField] private EmergencyRoadCatalog catalog;
        [SerializeField] private EmergencyRoadGameView sceneView;
        [SerializeField] private EmergencyRoadAudio audioService;
        [SerializeField] private Transform runtimeWorldRoot;
        [SerializeField] private GameObject editModeWorldPreview;
        [SerializeField] private Transform playerRoot;
        [SerializeField] private Transform playerVisualRoot;
        [SerializeField] private EmergencyVehicleController player;
        [SerializeField] private BoxCollider roadPhysicsSurface;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private EmergencyCameraJuice cameraJuice;
        [SerializeField] private EmergencyRoadContinuousCoinSpawner coinSpawner;
        [SerializeField] private MotorRushDirector motorRushDirector;

        [Header("RUNTIME SPAWN PREFABS - DRAG PREFABS HERE")]
        [SerializeField] private GameObject roadChunkRootPrefab;
        [SerializeField] private GameObject grassGroundPrefab;
        [SerializeField] private GameObject soilGroundPrefab;
        [SerializeField] private GameObject obstacleHazardPrefab;
        [SerializeField] private GameObject stoppedVehicleHazardPrefab;
        [SerializeField] private GameObject sameDirectionTrafficPrefab;
        [SerializeField] private GameObject crossTrafficHazardPrefab;
        [SerializeField] private GameObject routeMarkerPrefab;
        [SerializeField] private GameObject motorRushHazardPrefab;
        [SerializeField] private GameObject motorWarningLinePrefab;
        [SerializeField] private GameObject impactVfxPrefab;
        [SerializeField] private GameObject motorImpactVfxPrefab;

        private readonly List<RoadChunk> chunks = new();
        private readonly List<RoadHazard> activeHazards = new();
        private readonly List<RoadRouteMarker> activeRouteMarkers = new();
        private readonly List<MotorRushHazard> activeMotorHazards = new();

        private TMP_Text scoreText;
        private TMP_Text coinText;
        private GameObject pausePanel;
        private GameObject gameOverPanel;
        private TMP_Text gameOverScore;
        private TMP_Text hazardAlertText;
        private float distance;
        private int collectedCoins;
        private float speed = 18f;
        private const float StartSpeed = 18f;
        private const float MaxSpeed = 45f;
        private const float VehicleLength = 4.2f;
        private float nextObstacleDistance = 20f;
        private int nextChunkSequence;
        private int nextCrossroadSequence;
        private int lastCrossroadSequence = -100;
        private int nextCrossroadRadius = 1;
        private int lastCrossroadRadius = 1;
        private int escapeLane;
        private int escapeTargetLane;
        private bool hasEscapeLane;
        private bool paused;
        private bool ended;
        private bool initialized;

        internal EmergencyRoadGameplaySettings Settings => catalog != null ? catalog.gameplaySettings : null;
        internal EmergencyRoadCatalog Catalog => catalog;
        internal EmergencyRoadAudio Audio => audioService;
        public EmergencyVehicleController Player => player;
        public bool Ended => ended;
        public float Distance => distance;
        public float CurrentSpeed => speed;
        public IReadOnlyList<RoadHazard> ActiveHazards => activeHazards;
        public IReadOnlyList<MotorRushHazard> ActiveMotorHazards => activeMotorHazards;
        internal GameObject RoadChunkRootPrefab => roadChunkRootPrefab;
        internal GameObject GrassGroundPrefab => grassGroundPrefab;
        internal GameObject SoilGroundPrefab => soilGroundPrefab;
        internal GameObject ObstacleHazardPrefab => obstacleHazardPrefab;
        internal GameObject StoppedVehicleHazardPrefab => stoppedVehicleHazardPrefab;
        internal GameObject SameDirectionTrafficPrefab => sameDirectionTrafficPrefab;
        internal GameObject CrossTrafficHazardPrefab => crossTrafficHazardPrefab;
        internal GameObject RouteMarkerPrefab => routeMarkerPrefab;
        internal GameObject ImpactVfxPrefab => impactVfxPrefab;
        internal GameObject MotorImpactVfxPrefab => motorImpactVfxPrefab;
        internal Camera GameplayCamera => gameplayCamera;

        private void Start() => InitializeAuthoredGame();

        public void InitializeAuthoredGame()
        {
            if (initialized) return;
            if (!ValidateReferences())
            {
                enabled = false;
                return;
            }

            initialized = true;
            collectedCoins = 0;
            Time.timeScale = 1f;
            catalog.SynchronizePlayerVehicleData();
            LaneWidth = Mathf.Max(2f, catalog.laneWidth);
            ChunkSpacing = Mathf.Max(1f, catalog.roadLength);
            EmergencyRoadUI.SetFont(catalog.uiFont);
            audioService.ConfigureFromCatalog(catalog);
            audioService.ApplyVolumes();

            if (editModeWorldPreview != null) editModeWorldPreview.SetActive(false);
            if (roadPhysicsSurface != null) roadPhysicsSurface.enabled = true;

            BuildPlayer();
            BuildRoad();
            BuildHud();

            if (coinSpawner != null) coinSpawner.Configure(this, catalog.coinPrefab);
            if (motorRushDirector != null) motorRushDirector.Configure(this, motorWarningLinePrefab, motorRushHazardPrefab);
        }

        private bool ValidateReferences()
        {
            bool valid = true;
            valid &= Require(catalog, "Catalog");
            valid &= Require(sceneView, "Game View");
            valid &= Require(audioService, "Audio Service");
            valid &= Require(runtimeWorldRoot, "Runtime World Root");
            valid &= Require(playerRoot, "Player Root");
            valid &= Require(playerVisualRoot, "Player Visual Root");
            valid &= Require(player, "Emergency Vehicle Controller");
            valid &= Require(gameplayCamera, "Gameplay Camera");
            valid &= Require(cameraJuice, "Camera Juice");
            valid &= Require(roadChunkRootPrefab, "Road Chunk Root Prefab");
            valid &= Require(grassGroundPrefab, "Grass Ground Prefab");
            valid &= Require(soilGroundPrefab, "Soil Ground Prefab");
            return valid;
        }

        private bool Require(Object value, string label)
        {
            if (value != null) return true;
            Debug.LogError($"[Emergency Road] Game Controller thiếu {label}. Kéo object/prefab tương ứng vào Inspector; runtime sẽ không tự tìm hoặc tự tạo.", this);
            return false;
        }

        private void BuildPlayer()
        {
            int count = catalog.PlayerVehicleCount;
            if (count == 0)
            {
                Debug.LogError("[Emergency Road] Không có Player Vehicle prefab trong Catalog.", this);
                return;
            }

            int selected = Mathf.Clamp(EmergencyRoadProfile.Current.selectedVehicle, 0, count - 1);
            GameObject vehiclePrefab = catalog.PlayerVehiclePrefab(selected);
            if (vehiclePrefab == null)
            {
                Debug.LogError($"[Emergency Road] Xe index {selected} chưa được kéo prefab vào Catalog.", this);
                return;
            }

            GameObject visual = Instantiate(vehiclePrefab, playerVisualRoot, false);
            visual.name = "Player Emergency Vehicle";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.identity;
            visual.transform.localScale = Vector3.one;
            FitVehicle(visual, 2.2f, 4.2f, false);

            player.Initialize(visual.transform, this, catalog.HornForVehicle(selected), selected);
            cameraJuice.Initialize(playerRoot);

        }

        private void BuildRoad()
        {
            hasEscapeLane = false;
            nextObstacleDistance = Mathf.Max(20f, catalog.startingSafeDistance);
            EmergencyRoadGameplaySettings tuning = Settings;
            int min = tuning != null ? tuning.minStraightChunksBetweenIntersections : 10;
            int max = tuning != null ? tuning.maxStraightChunksBetweenIntersections : 14;
            nextCrossroadSequence = Random.Range(min, Mathf.Max(min, max) + 1);
            nextCrossroadRadius = CrossroadRadius(nextCrossroadSequence);

            int chunkCount = Mathf.Clamp(Mathf.CeilToInt(150f / ChunkSpacing) + 3, 12, 28);
            for (int i = 0; i < chunkCount; i++)
            {
                RoadChunk chunk = RoadChunk.Create(runtimeWorldRoot, catalog, i * ChunkSpacing, ShouldSpawnCrossroad(i), i, this);
                if (chunk != null) chunks.Add(chunk);
            }
            nextChunkSequence = chunkCount;
        }

        private void BuildHud()
        {
            scoreText = sceneView.score;
            coinText = sceneView.coins;
            if (coinText != null) coinText.text = "0";
            hazardAlertText = sceneView.hazardAlert;
            pausePanel = sceneView.pausePanel;
            gameOverPanel = sceneView.gameOverPanel;
            gameOverScore = sceneView.gameOverScore;

            Bind(sceneView.pause, TogglePause);
            Bind(sceneView.resume, TogglePause);
            Bind(sceneView.restartFromPause, Restart);
            Bind(sceneView.menuFromPause, Menu);
            Bind(sceneView.retry, Restart);
            Bind(sceneView.garage, Menu);

            if (pausePanel != null) pausePanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
        }

        private void Bind(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null) return;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => { audioService.Click(); action(); });
        }

        private void Update()
        {
            if (!initialized) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !ended) TogglePause();
            if (paused || ended || chunks.Count == 0) return;

            float start = Settings != null ? Settings.startSpeed : StartSpeed;
            float max = Settings != null ? Settings.maxSpeed : MaxSpeed;
            float ramp = Settings != null ? Settings.distanceToMaxSpeed : 800f;
            speed = Mathf.Min(max, start + (max - start) * distance / Mathf.Max(10f, ramp));

            float dz = speed * Time.deltaTime;
            distance += dz;
            for (int i = 0; i < chunks.Count; i++) chunks[i].Move(-dz);

            RoadChunk first = chunks[0];
            if (first.PositionZ < -42f)
            {
                float lastZ = chunks[^1].PositionZ;
                chunks.RemoveAt(0);
                int sequence = nextChunkSequence++;
                first.Recycle(lastZ + ChunkSpacing, ShouldSpawnCrossroad(sequence), sequence);
                chunks.Add(first);
            }

            if (scoreText != null) scoreText.text = $"{Mathf.FloorToInt(distance):N0} m";
            if (coinText != null) coinText.text = $"{collectedCoins:N0}";
        }

        public void AddCoin()
        {
            collectedCoins++;
            EmergencyRoadProfile.Current.coins++;
            EmergencyRoadProfile.Save();
            audioService.Coin();
        }

        internal void RegisterHazard(RoadHazard hazard)
        {
            if (hazard != null && !activeHazards.Contains(hazard)) activeHazards.Add(hazard);
        }

        internal void RegisterRouteMarker(RoadRouteMarker marker)
        {
            if (marker != null && !activeRouteMarkers.Contains(marker)) activeRouteMarkers.Add(marker);
        }

        internal void RegisterMotor(MotorRushHazard motor)
        {
            if (motor != null && !activeMotorHazards.Contains(motor)) activeMotorHazards.Add(motor);
        }

        internal bool IsLaneReserved(int lane, float minimumZ, float maximumZ)
        {
            for (int i = activeRouteMarkers.Count - 1; i >= 0; i--)
            {
                RoadRouteMarker marker = activeRouteMarkers[i];
                if (marker == null)
                {
                    activeRouteMarkers.RemoveAt(i);
                    continue;
                }
                float z = marker.transform.position.z;
                if (marker.SoleOpenLane == lane && z >= minimumZ && z <= maximumZ) return true;
            }
            return false;
        }

        internal void FillReservedLanes(float minimumZ, float maximumZ, bool[] result)
        {
            if (result == null || result.Length < 3) return;
            result[0] = result[1] = result[2] = false;
            for (int i = activeRouteMarkers.Count - 1; i >= 0; i--)
            {
                RoadRouteMarker marker = activeRouteMarkers[i];
                if (marker == null)
                {
                    activeRouteMarkers.RemoveAt(i);
                    continue;
                }
                float z = marker.transform.position.z;
                if (z >= minimumZ && z <= maximumZ) result[marker.SoleOpenLane + 1] = true;
            }
        }

        internal void PlanEscapeLanes(int requestedLane, bool wantsTwoLaneBlock, out int firstOpenLane, out int secondOpenLane)
        {
            requestedLane = Mathf.Clamp(requestedLane, -1, 1);
            secondOpenLane = -99;
            if (!hasEscapeLane)
            {
                escapeLane = escapeTargetLane = requestedLane;
                hasEscapeLane = true;
            }

            if (!wantsTwoLaneBlock)
            {
                firstOpenLane = escapeLane;
                return;
            }

            if (escapeLane == escapeTargetLane && requestedLane != escapeLane)
                escapeTargetLane = requestedLane;

            if (escapeLane != escapeTargetLane)
            {
                firstOpenLane = escapeLane;
                secondOpenLane = escapeLane + System.Math.Sign(escapeTargetLane - escapeLane);
                escapeLane = secondOpenLane;
                return;
            }

            firstOpenLane = escapeLane;
        }

        internal float ResolveMotorTargetX(float requestedX, float minimumZ, float maximumZ)
        {
            int requestedLane = Mathf.Clamp(Mathf.RoundToInt(requestedX / LaneWidth), -1, 1);
            if (!IsLaneReserved(requestedLane, minimumZ, maximumZ)) return requestedLane * LaneWidth;
            int bestLane = requestedLane;
            int bestDistance = int.MaxValue;
            for (int lane = -1; lane <= 1; lane++)
            {
                if (IsLaneReserved(lane, minimumZ, maximumZ)) continue;
                int distance = Mathf.Abs(lane - requestedLane);
                if (distance >= bestDistance) continue;
                bestLane = lane;
                bestDistance = distance;
            }
            return bestLane * LaneWidth;
        }

        internal void SpawnSameDirectionTraffic(int lane, float worldZ)
        {
            if (sameDirectionTrafficPrefab == null || catalog.trafficVehicles.Count == 0)
            {
                Debug.LogError("[Emergency Road] Thiếu Same Direction Traffic Prefab hoặc Traffic Vehicle Prefab trong Inspector.", this);
                return;
            }

            GameObject holder = Instantiate(sameDirectionTrafficPrefab, runtimeWorldRoot, false);
            holder.name = "Road Hazard - Same Direction Traffic";
            holder.transform.position = new Vector3(lane * LaneWidth, .05f, worldZ);

            RoadHazard hazard = holder.GetComponent<RoadHazard>();
            BoxCollider hitbox = holder.GetComponent<BoxCollider>();
            Rigidbody body = holder.GetComponent<Rigidbody>();
            SameDirectionTraffic mover = holder.GetComponent<SameDirectionTraffic>();
            EmergencyTrafficCrashResponder responder = holder.GetComponent<EmergencyTrafficCrashResponder>();
            if (hazard == null || hitbox == null || body == null || mover == null || responder == null)
            {
                Debug.LogError("[Emergency Road] Same Direction Traffic Prefab phải chứa sẵn RoadHazard + BoxCollider + Rigidbody + SameDirectionTraffic + EmergencyTrafficCrashResponder.", holder);
                Destroy(holder);
                return;
            }

            GameObject source = catalog.trafficVehicles[Random.Range(0, catalog.trafficVehicles.Count)];
            GameObject visual = Instantiate(source, holder.transform, false);
            visual.name = $"Moving {source.name}";
            DisableVisualColliders(visual);
            FitVehicle(visual, 2.12f, 4.05f);

            hitbox.isTrigger = true;
            hitbox.center = new Vector3(0, .72f, 0);
            hitbox.size = new Vector3(2.35f, 1.45f, 3.55f);
            body.isKinematic = true;
            body.useGravity = false;

            EmergencyRoadGameplaySettings tuning = Settings;
            float minimum = tuning != null ? tuning.trafficMinimumRoadSpeed : 8f;
            float maximum = Mathf.Max(minimum, tuning != null ? tuning.trafficMaximumRoadSpeed : 14f);
            mover.Initialize(this, lane, Random.Range(minimum, maximum), tuning != null ? tuning.trafficLaneChangeChance : .38f,
                tuning != null ? tuning.trafficLaneDecisionInterval : 2.2f,
                tuning != null ? tuning.trafficBrakingDistance : 11f,
                tuning != null ? tuning.trafficBrakingStrength : 10f);
            responder.Configure(this, true, impactVfxPrefab);
            RegisterHazard(hazard);
        }

        internal static void DisableVisualColliders(GameObject visual)
        {
            if (visual == null) return;
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
        }

        internal float Difficulty01 => Mathf.InverseLerp(Settings != null ? Settings.startSpeed : StartSpeed, Settings != null ? Settings.maxSpeed : MaxSpeed, speed);

        private bool ShouldSpawnCrossroad(int sequence)
        {
            if (sequence < nextCrossroadSequence) return false;
            lastCrossroadSequence = sequence;
            lastCrossroadRadius = nextCrossroadRadius;
            EmergencyRoadGameplaySettings tuning = Settings;
            int min = tuning != null ? tuning.minStraightChunksBetweenIntersections : 10;
            int max = Mathf.Max(min, tuning != null ? tuning.maxStraightChunksBetweenIntersections : 14);
            nextCrossroadSequence = sequence + Random.Range(min, max + 1);
            nextCrossroadRadius = CrossroadRadius(nextCrossroadSequence);
            return true;
        }

        private int CrossroadRadius(int sequence)
        {
            if (catalog == null || catalog.crossroadPrefabs.Count == 0) return 1;
            GameObject prefab = catalog.crossroadPrefabs[sequence % catalog.crossroadPrefabs.Count];
            EmergencyRoadPrefabMetrics metrics = catalog.MetricsFor(prefab);
            if (metrics == null) return 1;
            return Mathf.Max(1, Mathf.CeilToInt((metrics.rendererSize.z / Mathf.Max(.1f, ChunkSpacing) - 1f) * .5f));
        }

        internal bool IsCrossroadClearance(int sequence) => Mathf.Abs(sequence - lastCrossroadSequence) <= lastCrossroadRadius || Mathf.Abs(sequence - nextCrossroadSequence) <= nextCrossroadRadius;
        internal bool IsCrossroadMeshCovered(int sequence) => Mathf.Abs(sequence - lastCrossroadSequence) <= lastCrossroadRadius || Mathf.Abs(sequence - nextCrossroadSequence) <= nextCrossroadRadius;

        internal bool ShouldSpawnObstacle(int sequence, bool crossroad)
        {
            float generationDistance = sequence * ChunkSpacing;
            if (generationDistance < nextObstacleDistance) return false;
            float minimumGap = Settings != null ? Settings.obstacleGapAtMinimumSpeed : 28f;
            float maximumGap = Settings != null ? Settings.obstacleGapAtMaximumSpeed : 70f;
            float speedScaledGap = Mathf.Lerp(minimumGap, maximumGap, Difficulty01);
            nextObstacleDistance = generationDistance + Mathf.Max(VehicleLength * 2f, speedScaledGap);
            return true;
        }

        public void SetHazardAlert(string message)
        {
            if (hazardAlertText != null) hazardAlertText.text = message;
        }

        public void SpawnImpactVfx(Transform target, Vector3 localPosition)
        {
            if (impactVfxPrefab == null || target == null) return;
            GameObject instance = Instantiate(impactVfxPrefab, target, false);
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.identity;
        }

        public void SpawnMotorImpactVfx(Vector3 worldPosition)
        {
            if (motorImpactVfxPrefab == null) return;
            GameObject instance = Instantiate(motorImpactVfxPrefab, runtimeWorldRoot, true);
            instance.transform.position = worldPosition;
        }

        public void Crash(Vector3 impactDirection = default)
        {
            if (ended) return;
            ended = true;
            if (impactDirection.sqrMagnitude < .001f) impactDirection = Vector3.forward;
            audioService.Crash();
            player.CrashVisual(impactDirection.normalized);
            cameraJuice.EnterCrashView(impactDirection.normalized);

            int score = Mathf.FloorToInt(distance);
            EmergencyRoadProfile.Current.highScore = Mathf.Max(score, EmergencyRoadProfile.Current.highScore);
            EmergencyRoadProfile.Save();
            if (gameOverScore != null) gameOverScore.text = $"{score:N0} m  •  BEST {EmergencyRoadProfile.Current.highScore:N0} m";
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
        }

        private void TogglePause()
        {
            paused = !paused;
            Time.timeScale = paused ? 0f : 1f;
            if (pausePanel != null) pausePanel.SetActive(paused);
        }

        private void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Game");
        }

        private void Menu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Menu");
        }

        public static void FitVehicle(GameObject visual, float targetWidth, float targetLength, bool alignRendererBounds = true)
        {
            Renderer[] allRenderers = visual.GetComponentsInChildren<Renderer>(true);
            List<Renderer> renderers = new(allRenderers.Length);
            foreach (Renderer renderer in allRenderers)
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer) renderers.Add(renderer);
            if (renderers.Count == 0) return;
            Vector3 anchor = visual.transform.localPosition;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Count; i++) bounds.Encapsulate(renderers[i].bounds);
            float scaleByWidth = targetWidth / Mathf.Max(.1f, bounds.size.x);
            float scaleByLength = targetLength / Mathf.Max(.1f, bounds.size.z);
            visual.transform.localScale *= Mathf.Min(scaleByWidth, scaleByLength * 1.15f);
            if (!alignRendererBounds) return;
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Count; i++) bounds.Encapsulate(renderers[i].bounds);
            Transform parent = visual.transform.parent;
            Vector3 centerLocal = parent != null ? parent.InverseTransformPoint(bounds.center) : bounds.center;
            Vector3 bottomLocal = parent != null ? parent.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)) : new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            visual.transform.localPosition += new Vector3(anchor.x - centerLocal.x, anchor.y - bottomLocal.y, anchor.z - centerLocal.z);
        }

        public static void PositionOutsideRoad(GameObject building, int side, float roadHalfWidth, float margin)
        {
            Renderer[] renderers = building.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float targetInner = roadHalfWidth + margin;
            float x = side > 0 ? targetInner + bounds.extents.x : -(targetInner + bounds.extents.x);
            Vector3 local = building.transform.localPosition;
            local.x = x - (bounds.center.x - building.transform.position.x);
            building.transform.localPosition = local;
        }

        public static void FitBuildingToLot(GameObject building, float maximumDepth, float maximumWidth = float.PositiveInfinity)
        {
            Renderer[] renderers = building.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float depthScale = maximumDepth / Mathf.Max(.1f, bounds.size.z);
            float widthScale = float.IsPositiveInfinity(maximumWidth) ? 1f : maximumWidth / Mathf.Max(.1f, bounds.size.x);
            building.transform.localScale *= Mathf.Min(1f, Mathf.Min(depthScale, widthScale));
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Vector3 local = building.transform.localPosition;
            local.y -= bounds.min.y;
            building.transform.localPosition = local;
        }
    }

    internal sealed class RoadChunk
    {
        private readonly GameObject root;
        private readonly EmergencyRoadCatalog catalog;
        private readonly EmergencyRoadGame owner;
        private readonly List<GameObject> spawned = new();

        public float PositionZ => root.transform.localPosition.z;

        private RoadChunk(GameObject rootObject, EmergencyRoadCatalog data, EmergencyRoadGame game)
        {
            root = rootObject;
            catalog = data;
            owner = game;
        }

        public static RoadChunk Create(Transform parent, EmergencyRoadCatalog data, float z, bool cross, int sequence, EmergencyRoadGame game)
        {
            if (game.RoadChunkRootPrefab == null) return null;
            GameObject root = Object.Instantiate(game.RoadChunkRootPrefab, parent, false);
            root.name = "Road Chunk Runtime";
            var chunk = new RoadChunk(root, data, game);
            chunk.Recycle(z, cross, sequence);
            return chunk;
        }

        public void Move(float dz)
        {
            root.transform.localPosition += Vector3.forward * dz;
            foreach (GameObject go in spawned)
                if (go != null && go.TryGetComponent<SideCrossingHazard>(out SideCrossingHazard crossing)) crossing.Tick(dz);
        }

        public void Recycle(float z, bool cross, int sequence)
        {
            foreach (GameObject go in spawned) if (go != null) Object.Destroy(go);
            spawned.Clear();
            root.transform.localPosition = new Vector3(0, 0, z);
            bool meshCovered = !cross && owner.IsCrossroadMeshCovered(sequence);
            root.name = cross ? "Crossroad Chunk" : meshCovered ? "Crossroad Footprint Spacer" : "Road_1 Chunk";

            GameObject roadPrefab = null;
            if (cross && catalog.crossroadPrefabs.Count > 0) roadPrefab = catalog.crossroadPrefabs[sequence % catalog.crossroadPrefabs.Count];
            else if (!meshCovered && catalog.roadPrefabs.Count > 0) roadPrefab = catalog.roadPrefabs[0];

            if (!meshCovered)
            {
                if (roadPrefab == null)
                {
                    Debug.LogError("[Emergency Road] Thiếu Road/Crossroad prefab trong Catalog. Không tạo fallback primitive.");
                    return;
                }
                GameObject road = Object.Instantiate(roadPrefab, root.transform, false);
                road.transform.localPosition = Vector3.zero;
                road.transform.localRotation = Quaternion.identity;
                if (cross)
                {
                    EmergencyRoadPrefabMetrics metrics = catalog.MetricsFor(roadPrefab);
                    if (metrics != null) road.transform.localPosition = new Vector3(-metrics.rendererCenter.x, 0, -metrics.rendererCenter.z);
                }
                spawned.Add(road);
            }

            if (cross) SpawnCrossroadExtensions(roadPrefab);
            else if (!owner.IsCrossroadClearance(sequence)) SpawnGroundAndDecorations(sequence);
            if (!meshCovered && sequence > 1 && owner.ShouldSpawnObstacle(sequence, cross)) SpawnGameplay(cross);
        }

        private GameObject SpawnGroundPrefab(GameObject prefab, string name, Vector3 position, Vector3 scale)
        {
            if (prefab == null) return null;
            GameObject ground = Object.Instantiate(prefab, root.transform, false);
            ground.name = name;
            ground.transform.localPosition = position;
            ground.transform.localRotation = Quaternion.identity;
            ground.transform.localScale = scale;
            spawned.Add(ground);
            return ground;
        }

        private void SpawnCrossroadExtensions(GameObject crossroadPrefab)
        {
            if (catalog.roadPrefabs.Count == 0) return;
            const int tilesPerSide = 6;
            EmergencyRoadPrefabMetrics metrics = catalog.MetricsFor(crossroadPrefab);
            float crossHalfWidth = metrics != null ? metrics.rendererSize.x * .5f : catalog.roadHalfWidth;
            float crossHalfLength = metrics != null ? metrics.rendererSize.z * .5f : EmergencyRoadGame.ChunkSpacing * .5f;

            for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < tilesPerSide; i++)
            {
                GameObject road = Object.Instantiate(catalog.roadPrefabs[0], root.transform, false);
                road.name = $"Cross Street {(side < 0 ? "Left" : "Right")} {i + 1}";
                road.transform.localRotation = Quaternion.Euler(0, 90, 0);
                road.transform.localPosition = new Vector3(side * (crossHalfWidth + .04f + (i + .5f) * catalog.roadLength), -.012f, 0);
                spawned.Add(road);
            }

            int radius = metrics != null ? Mathf.Max(1, Mathf.CeilToInt((metrics.rendererSize.z / catalog.roadLength - 1f) * .5f)) : 1;
            float nextInnerEdge = (radius + .5f) * catalog.roadLength;
            float gap = Mathf.Max(0, nextInnerEdge - crossHalfLength);
            if (gap > .03f)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject connector = Object.Instantiate(catalog.roadPrefabs[0], root.transform, false);
                    connector.name = "Main Road Connector";
                    connector.transform.localPosition = new Vector3(0, -.006f, side * (crossHalfLength + gap * .5f));
                    connector.transform.localScale = new Vector3(1, 1, gap / catalog.roadLength);
                    spawned.Add(connector);
                    SpawnTransitionGround(side, crossHalfLength, gap);
                }
            }
            SpawnBranchVerge(crossHalfWidth, nextInnerEdge, tilesPerSide);
            SpawnInnerCornerGrass(crossHalfWidth, crossHalfLength);
            SpawnBranchScenery(crossHalfWidth, tilesPerSide);
        }

        private void SpawnInnerCornerGrass(float crossHalfWidth, float crossHalfLength)
        {
            float width = Mathf.Max(.1f, crossHalfWidth - catalog.roadHalfWidth);
            float depth = Mathf.Max(.1f, crossHalfLength - catalog.roadHalfWidth);
            for (int xSide = -1; xSide <= 1; xSide += 2)
            for (int zSide = -1; zSide <= 1; zSide += 2)
                SpawnGroundPrefab(owner.GrassGroundPrefab, "Crossroad Inner Corner Grass",
                    new Vector3(xSide * (catalog.roadHalfWidth + width * .5f), -.28f, zSide * (catalog.roadHalfWidth + depth * .5f)),
                    new Vector3(width + .06f, .36f, depth + .06f));
        }

        private void SpawnBranchVerge(float crossHalfWidth, float outerZ, int tilesPerSide)
        {
            float innerZ = catalog.roadHalfWidth - .45f;
            float depth = Mathf.Max(.1f, outerZ - innerZ);
            float outerX = crossHalfWidth + tilesPerSide * catalog.roadLength;
            float width = outerX - crossHalfWidth;
            for (int xSide = -1; xSide <= 1; xSide += 2)
            for (int zSide = -1; zSide <= 1; zSide += 2)
                SpawnGroundPrefab(owner.GrassGroundPrefab, "Cross Street Grass Verge",
                    new Vector3(xSide * (crossHalfWidth + width * .5f), -.28f, zSide * (innerZ + depth * .5f)),
                    new Vector3(width + .08f, .36f, depth + .08f));
        }

        private void SpawnTransitionGround(int zSide, float crossHalfLength, float gap)
        {
            float grassWidth = owner.Settings != null ? owner.Settings.roadsideGrassWidth : 42f;
            float grassInnerEdge = catalog.roadHalfWidth;
            for (int xSide = -1; xSide <= 1; xSide += 2)
            {
                SpawnGroundPrefab(owner.GrassGroundPrefab, "Intersection Corner Grass",
                    new Vector3(xSide * (grassInnerEdge + grassWidth * .5f), -.28f, zSide * (crossHalfLength + gap * .5f)),
                    new Vector3(grassWidth, .36f, gap + .04f));
            }
        }

        private void SpawnBranchScenery(float crossHalfWidth, int tilesPerSide)
        {
            GameObject lamp = catalog.streetDecorationPrefabs.Find(x => x != null && x.name == "Light");
            for (int xSide = -1; xSide <= 1; xSide += 2)
            for (int zSide = -1; zSide <= 1; zSide += 2)
            for (int i = 0; i < tilesPerSide; i++)
            {
                float x = xSide * (crossHalfWidth + (i + .5f) * catalog.roadLength);
                if (i % 2 == 0 && catalog.decorationPrefabs.Count > 0)
                {
                    GameObject building = Object.Instantiate(catalog.decorationPrefabs[(i + (xSide > 0 ? 1 : 0) + (zSide > 0 ? 2 : 0)) % catalog.decorationPrefabs.Count], root.transform, false);
                    building.transform.localPosition = new Vector3(x, 0, 0);
                    building.transform.localRotation = Quaternion.Euler(0, zSide > 0 ? 90 : -90, 0);
                    FitBranchBuilding(building, x, zSide);
                    spawned.Add(building);
                }
                else if (catalog.naturePrefabs.Count > 0)
                {
                    GameObject tree = Object.Instantiate(catalog.naturePrefabs[i % catalog.naturePrefabs.Count], root.transform, false);
                    tree.transform.localPosition = new Vector3(x, 0, zSide * (catalog.roadHalfWidth + 3.2f));
                    tree.transform.localRotation = Quaternion.Euler(0, (i * 67 + xSide * 31 + zSide * 19) % 360, 0);
                    spawned.Add(tree);
                }
                if (lamp != null && (i == 1 || i == 4))
                {
                    GameObject light = Object.Instantiate(lamp, root.transform, false);
                    light.transform.localPosition = new Vector3(x, 0, zSide * (catalog.roadHalfWidth - .9f));
                    light.transform.localRotation = Quaternion.Euler(0, zSide > 0 ? 180 : 0, 0);
                    spawned.Add(light);
                }
            }
        }

        private void FitBranchBuilding(GameObject building, float targetX, int zSide)
        {
            Renderer[] renderers = building.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            float scale = Mathf.Min(1f, Mathf.Min(catalog.roadLength * 1.55f / Mathf.Max(.1f, bounds.size.x), 5.8f / Mathf.Max(.1f, bounds.size.z)));
            building.transform.localScale *= scale;
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            Vector3 local = building.transform.localPosition;
            local.x = targetX - (bounds.center.x - building.transform.position.x);
            local.z = zSide * (catalog.roadHalfWidth + .8f + bounds.extents.z) - (bounds.center.z - building.transform.position.z);
            local.y -= bounds.min.y;
            building.transform.localPosition = local;
        }

        private void SpawnGroundAndDecorations(int sequence)
        {
            EmergencyRoadGameplaySettings tuning = owner.Settings;
            float grassWidth = tuning != null ? tuning.roadsideGrassWidth : 42f;
            float grassInnerEdge = catalog.roadHalfWidth;
            for (int side = -1; side <= 1; side += 2)
            {
                SpawnGroundPrefab(owner.GrassGroundPrefab, side < 0 ? "Grass Ground Left" : "Grass Ground Right",
                    new Vector3(side * (grassInnerEdge + grassWidth * .5f), -.28f, 0), new Vector3(grassWidth, .36f, EmergencyRoadGame.ChunkSpacing + .08f));
                SpawnRoadsideBuildings(sequence, side, grassWidth);

                for (int j = 0; j < 2; j++)
                {
                    float z = -EmergencyRoadGame.ChunkSpacing * .3f + j * EmergencyRoadGame.ChunkSpacing * .5f + (sequence % 2) * 2f;
                    if (catalog.naturePrefabs.Count > 0)
                    {
                        GameObject tree = Object.Instantiate(catalog.naturePrefabs[(sequence + j) % catalog.naturePrefabs.Count], root.transform, false);
                        tree.transform.localPosition = new Vector3(side * (catalog.roadHalfWidth + 4.2f + j * 2.6f), 0, z);
                        tree.transform.localRotation = Quaternion.Euler(0, (sequence * 53 + j * 71) % 360, 0);
                        tree.transform.localScale = Vector3.one * (.85f + ((sequence + j) % 3) * .12f);
                        spawned.Add(tree);
                    }
                }

                GameObject lamp = catalog.streetDecorationPrefabs.Find(x => x != null && x.name == "Light");
                if (sequence % 3 == 0 && lamp != null)
                {
                    GameObject light = Object.Instantiate(lamp, root.transform, false);
                    light.transform.localPosition = new Vector3(side * (catalog.roadHalfWidth - .9f), 0, -EmergencyRoadGame.ChunkSpacing * .25f);
                    light.transform.localRotation = Quaternion.Euler(0, side < 0 ? 90 : -90, 0);
                    spawned.Add(light);
                }
            }
        }

        private void SpawnRoadsideBuildings(int sequence, int side, float grassWidth)
        {
            if (catalog.decorationPrefabs.Count == 0) return;
            EmergencyRoadGameplaySettings tuning = owner.Settings;
            int rows = tuning != null ? tuning.roadsideBuildingRows : 2;
            float gap = tuning != null ? tuning.roadsideBuildingGap : 1.25f;
            float setback = tuning != null ? tuning.roadsideBuildingSetback : 10f;
            float usableWidth = Mathf.Max(4f, grassWidth - setback - gap * Mathf.Max(0, rows - 1));
            float maximumBuildingWidth = usableWidth / Mathf.Max(1, rows);
            float nextInnerEdge = catalog.roadHalfWidth + setback;

            for (int row = 0; row < rows; row++)
            {
                int prefabIndex = (sequence * 2 + row + (side > 0 ? 1 : 0)) % catalog.decorationPrefabs.Count;
                GameObject building = Object.Instantiate(catalog.decorationPrefabs[prefabIndex], root.transform, false);
                building.transform.localRotation = Quaternion.Euler(0, side > 0 ? 180 : 0, 0);
                EmergencyRoadGame.FitBuildingToLot(building, EmergencyRoadGame.ChunkSpacing * .82f, maximumBuildingWidth);
                EmergencyRoadGame.PositionOutsideRoad(building, side, catalog.roadHalfWidth, nextInnerEdge - catalog.roadHalfWidth);

                Bounds bounds = CalculateRendererBounds(building);
                nextInnerEdge = (side > 0 ? bounds.max.x : -bounds.min.x) + gap;
                spawned.Add(building);
            }
        }

        private static Bounds CalculateRendererBounds(GameObject target)
        {
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(target.transform.position, Vector3.zero);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private void SpawnGameplay(bool cross)
        {
            if (cross)
            {
                SpawnCrossing();
                return;
            }

            int requestedOpenLane = Random.Range(-1, 2);
            float obstacleZ = Random.Range(-EmergencyRoadGame.ChunkSpacing * .22f, EmergencyRoadGame.ChunkSpacing * .22f);
            EmergencyRoadGameplaySettings tuning = owner.Settings;
            float doubleBlockChance = Mathf.Lerp(tuning != null ? tuning.twoLaneBlockChanceAtStart : .58f,
                tuning != null ? tuning.twoLaneBlockChanceAtMaxSpeed : .32f, owner.Difficulty01);
            float movingTrafficChance = tuning != null ? tuning.sameDirectionTrafficChance : .28f;
            bool wantsTwoLaneBlock = Random.value < doubleBlockChance;
            owner.PlanEscapeLanes(requestedOpenLane, wantsTwoLaneBlock, out int openLane, out int transitionLane);
            SpawnRouteMarker(openLane, obstacleZ);
            if (transitionLane >= -1) SpawnRouteMarker(transitionLane, obstacleZ);

            if (catalog.trafficVehicles.Count > 0 && Random.value < movingTrafficChance)
            {
                int trafficLane = openLane;
                while (trafficLane == openLane || trafficLane == transitionLane) trafficLane = Random.Range(-1, 2);
                owner.SpawnSameDirectionTraffic(trafficLane, root.transform.position.z + obstacleZ);
                return;
            }

            if (wantsTwoLaneBlock && transitionLane < -1)
            {
                for (int lane = -1; lane <= 1; lane++) if (lane != openLane) SpawnObstacle(lane, obstacleZ);
            }
            else
            {
                int blockedLane = Random.Range(-1, 2);
                while (blockedLane == openLane || blockedLane == transitionLane) blockedLane = Random.Range(-1, 2);
                SpawnObstacle(blockedLane, obstacleZ);
            }
        }

        private void SpawnRouteMarker(int soleOpenLane, float localZ)
        {
            if (owner.RouteMarkerPrefab == null)
            {
                Debug.LogError("[Emergency Road] Thiếu Route Marker Prefab trên Game Controller.");
                return;
            }
            GameObject markerObject = Object.Instantiate(owner.RouteMarkerPrefab, root.transform, false);
            markerObject.transform.localPosition = new Vector3(0, 0, localZ);
            RoadRouteMarker marker = markerObject.GetComponent<RoadRouteMarker>();
            if (marker == null)
            {
                Debug.LogError("[Emergency Road] Route Marker Prefab phải chứa sẵn RoadRouteMarker.", markerObject);
                Object.Destroy(markerObject);
                return;
            }
            marker.Configure(soleOpenLane);
            owner.RegisterRouteMarker(marker);
            spawned.Add(markerObject);
        }

        private void SpawnObstacle(int lane, float localZ)
        {
            if (owner.ObstacleHazardPrefab == null)
            {
                Debug.LogError("[Emergency Road] Thiếu Obstacle Hazard Wrapper Prefab trên Game Controller.");
                return;
            }

            float roll = Random.value;
            EmergencyRoadGameplaySettings tuning = owner.Settings;
            float vehicleChance = tuning != null ? tuning.stoppedVehicleChance : .68f;
            float barrierChance = tuning != null ? tuning.barrierChance : .24f;
            if (roll < vehicleChance && catalog.trafficVehicles.Count > 0)
            {
                SpawnParkedVehicle(lane, localZ);
                return;
            }

            List<GameObject> barriers = catalog.obstaclePrefabs.FindAll(x => x != null && x.name.ToLowerInvariant().Contains("barrier"));
            GameObject source = roll < vehicleChance + barrierChance && barriers.Count > 0
                ? barriers[Random.Range(0, barriers.Count)]
                : (catalog.obstaclePrefabs.Count > 0 ? catalog.obstaclePrefabs[Random.Range(0, catalog.obstaclePrefabs.Count)] : null);
            if (source == null) return;

            GameObject holder = Object.Instantiate(owner.ObstacleHazardPrefab, root.transform, false);
            holder.name = $"Road Hazard - {source.name}";
            holder.transform.localPosition = new Vector3(lane * EmergencyRoadGame.LaneWidth, .05f, localZ);
            RoadHazard hazard = holder.GetComponent<RoadHazard>();
            BoxCollider hitbox = holder.GetComponent<BoxCollider>();
            if (hazard == null || hitbox == null)
            {
                Debug.LogError("[Emergency Road] Obstacle Hazard Wrapper Prefab phải chứa sẵn RoadHazard + BoxCollider.", holder);
                Object.Destroy(holder);
                return;
            }

            GameObject visual = Object.Instantiate(source, holder.transform, false);
            EmergencyRoadGame.DisableVisualColliders(visual);
            FitObstacleToLane(visual, hitbox, EmergencyRoadGame.LaneWidth * (tuning != null ? tuning.obstacleLaneWidth : .86f), source.name);
            owner.RegisterHazard(hazard);
            spawned.Add(holder);
        }

        private void SpawnParkedVehicle(int lane, float localZ)
        {
            if (owner.StoppedVehicleHazardPrefab == null || catalog.trafficVehicles.Count == 0) return;
            GameObject holder = Object.Instantiate(owner.StoppedVehicleHazardPrefab, root.transform, false);
            holder.transform.localPosition = new Vector3(lane * EmergencyRoadGame.LaneWidth, .05f, localZ);
            RoadHazard hazard = holder.GetComponent<RoadHazard>();
            BoxCollider box = holder.GetComponent<BoxCollider>();
            EmergencyTrafficCrashResponder responder = holder.GetComponent<EmergencyTrafficCrashResponder>();
            if (hazard == null || box == null || responder == null)
            {
                Debug.LogError("[Emergency Road] Stopped Vehicle Prefab phải chứa sẵn RoadHazard + BoxCollider + EmergencyTrafficCrashResponder.", holder);
                Object.Destroy(holder);
                return;
            }

            EmergencyRoadGameplaySettings tuning = owner.Settings;
            Vector2 size = tuning != null ? tuning.stoppedVehicleSize : new Vector2(2.18f, 4.05f);
            GameObject source = catalog.trafficVehicles[Random.Range(0, catalog.trafficVehicles.Count)];
            GameObject visual = Object.Instantiate(source, holder.transform, false);
            EmergencyRoadGame.DisableVisualColliders(visual);
            EmergencyRoadGame.FitVehicle(visual, size.x, size.y);
            box.center = new Vector3(0, .72f, 0);
            box.size = tuning != null ? tuning.stoppedVehicleHitbox : new Vector3(EmergencyRoadGame.LaneWidth * .82f, 1.45f, 3.55f);
            responder.Configure(owner, true, owner.ImpactVfxPrefab);
            owner.RegisterHazard(hazard);
            spawned.Add(holder);
        }

        private static void FitObstacleToLane(GameObject visual, BoxCollider hitbox, float targetWidth, string sourceName)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            string id = sourceName.ToLowerInvariant();
            float visualWidth = id.Contains("cone") ? .65f : id.Contains("box") ? 1.45f : id.Contains("trash") ? 1.85f : targetWidth;
            visual.transform.localScale *= visualWidth / Mathf.Max(.05f, bounds.size.x);
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            hitbox.center = hitbox.transform.InverseTransformPoint(bounds.center);
            hitbox.size = new Vector3(targetWidth, Mathf.Max(.35f, bounds.size.y * .88f), Mathf.Clamp(bounds.size.z * .82f, .5f, 2.8f));
        }

        private void SpawnCrossing()
        {
            if (owner.CrossTrafficHazardPrefab == null || catalog.trafficVehicles.Count == 0) return;
            bool left = Random.value < .5f;
            EmergencyRoadGameplaySettings tuning = owner.Settings;
            float branchLaneZ = left ? -EmergencyRoadGame.LaneWidth : EmergencyRoadGame.LaneWidth;
            float worldZ = root.transform.position.z + branchLaneZ;
            int lane = FindCrossTrafficBlockLane(worldZ, left, tuning != null ? tuning.crossTrafficRouteLookDistance : 24f);
            if (lane < -1) return;

            GameObject holder = Object.Instantiate(owner.CrossTrafficHazardPrefab, root.transform, false);
            holder.transform.localPosition = new Vector3(left ? -12f : 12f, .05f, branchLaneZ);
            holder.transform.localRotation = Quaternion.identity;
            RoadHazard hazard = holder.GetComponent<RoadHazard>();
            BoxCollider hitbox = holder.GetComponent<BoxCollider>();
            Rigidbody body = holder.GetComponent<Rigidbody>();
            SideCrossingHazard mover = holder.GetComponent<SideCrossingHazard>();
            EmergencyTrafficCrashResponder responder = holder.GetComponent<EmergencyTrafficCrashResponder>();
            if (hazard == null || hitbox == null || body == null || mover == null || responder == null)
            {
                Debug.LogError("[Emergency Road] Cross Traffic Prefab phải chứa sẵn RoadHazard + BoxCollider + Rigidbody + SideCrossingHazard + EmergencyTrafficCrashResponder.", holder);
                Object.Destroy(holder);
                return;
            }

            GameObject source = catalog.trafficVehicles[Random.Range(0, catalog.trafficVehicles.Count)];
            GameObject visual = Object.Instantiate(source, holder.transform, false);
            EmergencyRoadGame.DisableVisualColliders(visual);
            Vector2 size = tuning != null ? tuning.stoppedVehicleSize : new Vector2(2.18f, 4.05f);
            EmergencyRoadGame.FitVehicle(visual, size.x, size.y);
            visual.transform.localRotation = Quaternion.Euler(0, left ? 90 : -90, 0);

            hitbox.isTrigger = true;
            hitbox.center = new Vector3(0, .72f, 0);
            hitbox.size = tuning != null ? tuning.stoppedVehicleHitbox : new Vector3(2.54f, 1.45f, 3.55f);
            body.isKinematic = true;
            body.useGravity = false;
            mover.Configure(lane * EmergencyRoadGame.LaneWidth, left, tuning != null ? tuning.crossTrafficSpeed : 11.5f, tuning != null ? tuning.crossTrafficStartDistance : 44f);
            responder.Configure(owner, false, owner.ImpactVfxPrefab);
            owner.RegisterHazard(hazard);
            spawned.Add(holder);
        }

        private int FindCrossTrafficBlockLane(float worldZ, bool fromLeft, float routeLookDistance)
        {
            int[] priority = fromLeft ? new[] { -1, 0, 1 } : new[] { 1, 0, -1 };
            float protectedDistance = Mathf.Max(routeLookDistance, EmergencyRoadGame.ChunkSpacing * 2.25f);
            Physics.SyncTransforms();
            foreach (int lane in priority)
                if (!owner.IsLaneReserved(lane, worldZ - protectedDistance, worldZ + protectedDistance) && !IsHazardLaneOccupied(lane, worldZ)) return lane;
            return -99;
        }

        private static bool IsHazardLaneOccupied(int lane, float worldZ)
        {
            Collider[] hits = Physics.OverlapBox(new Vector3(lane * EmergencyRoadGame.LaneWidth, .9f, worldZ), new Vector3(1.15f, .9f, 2.3f), Quaternion.identity, ~0, QueryTriggerInteraction.Collide);
            foreach (Collider hit in hits) if (hit.GetComponentInParent<RoadHazard>() != null) return true;
            return false;
        }
    }
}
