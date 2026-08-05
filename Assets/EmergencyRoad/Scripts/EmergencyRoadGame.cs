using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using TMPro;

namespace EmergencyRoad
{
    public sealed class EmergencyRoadGame : MonoBehaviour
    {
        public static float LaneWidth { get; private set; } = 5.18f;
        public static float ChunkSpacing { get; private set; } = 28f;
        private EmergencyRoadCatalog catalog;
        private readonly List<RoadChunk> chunks = new();
        private EmergencyVehicleController player;
        private TMP_Text scoreText;
        private TMP_Text coinText;
        private GameObject pausePanel;
        private GameObject gameOverPanel;
        private TMP_Text gameOverScore;
        private TMP_Text hazardAlertText;
        private float distance;
        private float speed = 18f;
        private const float StartSpeed = 18f;
        private const float MaxSpeed = 45f;
        private const float VehicleLength = 4.2f;
        private float nextObstacleDistance = 20f;
        private int nextChunkSequence;
        private int nextCrossroadSequence;
        private int lastCrossroadSequence=-100;
        private int nextCrossroadRadius=1;
        private int lastCrossroadRadius=1;
        private bool paused;
        private bool ended;
        private EmergencyCameraJuice cameraJuice;
        private EmergencyRoadGameView sceneView;
        internal EmergencyRoadGameplaySettings Settings=>catalog!=null?catalog.gameplaySettings:null;
        internal EmergencyRoadCatalog Catalog=>catalog;
        public EmergencyVehicleController Player=>player;
        public bool Ended=>ended;
        public float Distance=>distance;
        public float CurrentSpeed=>speed;

        public static void Create(EmergencyRoadCatalog data, EmergencyRoadGameView sceneView, float laneWidth = 5.18f, float chunkSpacing = 28f)
        {
            LaneWidth = laneWidth;
            ChunkSpacing = chunkSpacing;
            var go = new GameObject("Emergency Road Game");
            var game = go.AddComponent<EmergencyRoadGame>();
            game.catalog = data;
            game.sceneView = sceneView;
            game.Build();
        }

        private void Build()
        {
            Time.timeScale = 1f;
            EmergencyRoadUI.SetFont(catalog.uiFont);
            BuildLightingAndCamera();
            BuildRoad();
            BuildPlayer();
            BuildHud();
            gameObject.AddComponent<MotorRushDirector>().Initialize(this,player.transform);
        }

        private void BuildLightingAndCamera()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.68f,.78f,.9f);
            RenderSettings.ambientEquatorColor = new Color(.48f,.57f,.62f);
            RenderSettings.ambientGroundColor = new Color(.3f,.34f,.28f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = new Color(.68f,.82f,.9f); RenderSettings.fogStartDistance = 42f; RenderSettings.fogEndDistance = 125f;
            var light = new GameObject("Sun", typeof(Light));
            light.transform.rotation = Quaternion.Euler(42,-28,0);
            light.GetComponent<Light>().type = LightType.Directional; light.GetComponent<Light>().intensity = 1.32f; light.GetComponent<Light>().color = new Color(1f,.96f,.88f); light.GetComponent<Light>().shadows=LightShadows.Soft;
            var cam = new GameObject("Chase Camera", typeof(Camera), typeof(AudioListener)); cam.tag = "MainCamera";
            cam.transform.SetPositionAndRotation(new Vector3(0,7.6f,-10.5f), Quaternion.Euler(22,0,0));
            var camera=cam.GetComponent<Camera>();camera.fieldOfView = 58f; camera.backgroundColor = new Color(.28f,.65f,.9f);camera.allowHDR=true;
            var cameraData=cam.AddComponent<UniversalAdditionalCameraData>();cameraData.renderPostProcessing=true;cameraData.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            if(catalog.postProcessProfile!=null){var volumeGo=new GameObject("Global Post Processing - Bloom ACES Grade",typeof(Volume));var volume=volumeGo.GetComponent<Volume>();volume.isGlobal=true;volume.priority=10;volume.profile=catalog.postProcessProfile;}
        }

        private void BuildRoad()
        {
            CreatePhysicsRoadSurface();
            nextObstacleDistance=Mathf.Max(20f,catalog.startingSafeDistance);
            var tuning=Settings;nextCrossroadSequence=Random.Range(tuning!=null?tuning.minStraightChunksBetweenIntersections:10,(tuning!=null?tuning.maxStraightChunksBetweenIntersections:14)+1);nextCrossroadRadius=CrossroadRadius(nextCrossroadSequence);
            int chunkCount=Mathf.Clamp(Mathf.CeilToInt(150f/ChunkSpacing)+3,12,28);
            for (int i = 0; i < chunkCount; i++)
            {
                bool cross=ShouldSpawnCrossroad(i);
                var chunk = RoadChunk.Create(transform, catalog, i * ChunkSpacing, cross, i, this);
                chunks.Add(chunk);
            }
            nextChunkSequence=chunkCount;
        }

        private void CreatePhysicsRoadSurface()
        {
            var ground=new GameObject("Mặt Đường Vật Lý",typeof(BoxCollider));ground.transform.SetParent(transform,false);ground.transform.localPosition=new Vector3(0,-.16f,80f);var collider=ground.GetComponent<BoxCollider>();collider.center=Vector3.zero;collider.size=new Vector3(Mathf.Max(40f,catalog.roadHalfWidth*4f),.3f,240f);collider.isTrigger=false;
        }

        private void BuildPlayer()
        {
            int selected = Mathf.Clamp(EmergencyRoadProfile.Current.selectedVehicle, 0, Mathf.Max(0,catalog.playerVehicles.Count-1));
            GameObject visual = catalog.playerVehicles.Count > 0 ? Instantiate(catalog.playerVehicles[selected]) : GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Player Emergency Vehicle";
            var root = new GameObject("Player"); root.transform.position = new Vector3(0,.55f,0);
            visual.transform.SetParent(root.transform, false); FitVehicle(visual, 2.2f, 4.2f);
            var collider = root.AddComponent<BoxCollider>(); collider.size = new Vector3(2.05f,1.5f,3.6f); collider.center = new Vector3(0,.65f,0); collider.isTrigger = true;
            var body = root.AddComponent<Rigidbody>(); body.isKinematic = true; body.useGravity = false;
            player = root.AddComponent<EmergencyVehicleController>(); player.Initialize(visual.transform, this,catalog.HornForVehicle(selected),selected);
            cameraJuice=Camera.main!=null?Camera.main.gameObject.AddComponent<EmergencyCameraJuice>():null;if(cameraJuice!=null)cameraJuice.Initialize(root.transform);
            CreateRoadDust(root.transform,catalog.vfxParticleMaterial);
        }

        private static void CreateRoadDust(Transform playerRoot,Material particleMaterial)
        {
            var go=new GameObject("Subtle Road Dust Trail",typeof(ParticleSystem));go.transform.SetParent(playerRoot);go.transform.localPosition=new Vector3(0,.1f,-1.8f);go.transform.localRotation=Quaternion.Euler(-90,0,0);
            var ps=go.GetComponent<ParticleSystem>();var main=ps.main;main.startLifetime=.45f;main.startSpeed=.35f;main.startSize=.24f;main.startColor=new Color(.7f,.72f,.68f,.18f);main.maxParticles=32;main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=ps.emission;emission.rateOverTime=14f;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(1.25f,.05f,.2f);
            if(particleMaterial!=null)go.GetComponent<ParticleSystemRenderer>().sharedMaterial=particleMaterial;
        }

        public static void FitVehicle(GameObject visual, float targetWidth, float targetLength)
        {
            var rs = visual.GetComponentsInChildren<Renderer>(); if (rs.Length == 0) return;
            var anchor=visual.transform.localPosition;var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            float scaleByWidth=targetWidth/Mathf.Max(.1f,b.size.x);float scaleByLength=targetLength/Mathf.Max(.1f,b.size.z);float uniformScale=Mathf.Min(scaleByWidth,scaleByLength*1.15f);visual.transform.localScale*=uniformScale;
            b=rs[0].bounds;foreach(var r in rs)b.Encapsulate(r.bounds);var parent=visual.transform.parent;Vector3 centerLocal=parent!=null?parent.InverseTransformPoint(b.center):b.center;Vector3 bottomLocal=parent!=null?parent.InverseTransformPoint(new Vector3(b.center.x,b.min.y,b.center.z)):new Vector3(b.center.x,b.min.y,b.center.z);visual.transform.localPosition+=new Vector3(anchor.x-centerLocal.x,anchor.y-bottomLocal.y,anchor.z-centerLocal.z);
        }

        public static void PositionOutsideRoad(GameObject building,int side,float roadHalfWidth,float margin)
        {
            var renderers=building.GetComponentsInChildren<Renderer>();if(renderers.Length==0){building.transform.localPosition=new Vector3(side*(roadHalfWidth+margin+4f),building.transform.localPosition.y,building.transform.localPosition.z);return;}
            var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            float targetInner=roadHalfWidth+margin;float x=side>0?targetInner+bounds.extents.x:-(targetInner+bounds.extents.x);var local=building.transform.localPosition;local.x=x-(bounds.center.x-building.transform.position.x);building.transform.localPosition=local;
        }

        public static void FitBuildingToLot(GameObject building,float maximumDepth)
        {
            var renderers=building.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);float scale=Mathf.Min(1f,maximumDepth/Mathf.Max(.1f,bounds.size.z));building.transform.localScale*=scale;bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);var local=building.transform.localPosition;local.y-=bounds.min.y;building.transform.localPosition=local;
        }

        private void BuildHud()
        {
            if(sceneView==null){Debug.LogError("Game scene is missing EmergencyRoadGameView. Rebuild the authored scenes.");return;}
            scoreText=sceneView.score;coinText=sceneView.coins;hazardAlertText=sceneView.hazardAlert;pausePanel=sceneView.pausePanel;gameOverPanel=sceneView.gameOverPanel;gameOverScore=sceneView.gameOverScore;
            Bind(sceneView.pause,TogglePause);Bind(sceneView.resume,TogglePause);Bind(sceneView.restartFromPause,Restart);Bind(sceneView.menuFromPause,Menu);Bind(sceneView.retry,Restart);Bind(sceneView.garage,Menu);
            pausePanel.SetActive(false);gameOverPanel.SetActive(false);
        }

        private static void Bind(Button button,UnityEngine.Events.UnityAction action){if(button==null)return;button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>{EmergencyRoadAudio.Instance?.Click();action();});}

        private void LegacyRuntimeHudIsNoLongerUsed()
        {
            var canvas = EmergencyRoadUI.Canvas("Game HUD");
            var bar = EmergencyRoadUI.Panel(canvas.transform,"HUD Bar",EmergencyRoadUI.Navy,new Vector2(0,.88f),Vector2.one,Vector2.zero,Vector2.zero);
            scoreText = EmergencyRoadUI.Label(bar,"0 m",38,Color.white,TextAnchor.MiddleLeft,new Vector2(.04f,0),new Vector2(.3f,1),Vector2.zero,Vector2.zero);
            coinText = EmergencyRoadUI.Label(bar,"● 0",38,EmergencyRoadUI.Yellow,TextAnchor.MiddleCenter,new Vector2(.37f,0),new Vector2(.63f,1),Vector2.zero,Vector2.zero);
            EmergencyRoadUI.Button(bar,"II",new Color(.1f,.45f,.65f,1),new Vector2(.89f,.15f),new Vector2(.96f,.85f),Vector2.zero,Vector2.zero,TogglePause);
            EmergencyRoadUI.Label(canvas.transform,"A / D  CHUYỂN LÀN     SPACE  BÓP CÒI",23,new Color(1,1,1,.65f),TextAnchor.MiddleCenter,new Vector2(.28f,.02f),new Vector2(.72f,.07f),Vector2.zero,Vector2.zero);
            hazardAlertText=EmergencyRoadUI.Label(canvas.transform,"",32,new Color(1f,.18f,.12f,1f),TextAnchor.MiddleCenter,new Vector2(.3f,.76f),new Vector2(.7f,.84f),Vector2.zero,Vector2.zero);hazardAlertText.fontStyle=FontStyles.Bold;
            pausePanel = Modal(canvas.transform,"TẠM DỪNG",out _);
            EmergencyRoadUI.Button(pausePanel.transform,"TIẾP TỤC",EmergencyRoadUI.Yellow,new Vector2(.2f,.46f),new Vector2(.8f,.6f),Vector2.zero,Vector2.zero,TogglePause);
            EmergencyRoadUI.Button(pausePanel.transform,"CHƠI LẠI",new Color(.08f,.45f,.65f,1),new Vector2(.2f,.29f),new Vector2(.8f,.43f),Vector2.zero,Vector2.zero,Restart);
            EmergencyRoadUI.Button(pausePanel.transform,"VỀ MENU",new Color(.65f,.18f,.22f,1),new Vector2(.2f,.12f),new Vector2(.8f,.26f),Vector2.zero,Vector2.zero,Menu);
            pausePanel.SetActive(false);
            gameOverPanel = Modal(canvas.transform,"HẾT LƯỢT!",out gameOverScore);
            var gameOverRect=(RectTransform)gameOverPanel.transform;gameOverRect.anchorMin=new Vector2(.045f,.18f);gameOverRect.anchorMax=new Vector2(.39f,.82f);
            EmergencyRoadUI.Button(gameOverPanel.transform,"CHƠI LẠI",EmergencyRoadUI.Yellow,new Vector2(.14f,.28f),new Vector2(.86f,.44f),Vector2.zero,Vector2.zero,Restart);
            EmergencyRoadUI.Button(gameOverPanel.transform,"VỀ NHÀ XE",new Color(.08f,.45f,.65f,1),new Vector2(.14f,.1f),new Vector2(.86f,.25f),Vector2.zero,Vector2.zero,Menu);
            gameOverPanel.SetActive(false);
        }

        private static GameObject Modal(Transform canvas,string title,out TMP_Text detail)
        {
            var panel = EmergencyRoadUI.Panel(canvas,title,EmergencyRoadUI.Navy,new Vector2(.32f,.2f),new Vector2(.68f,.8f),Vector2.zero,Vector2.zero).gameObject;
            EmergencyRoadUI.Label(panel.transform,title,58,EmergencyRoadUI.Cyan,TextAnchor.MiddleCenter,new Vector2(.08f,.72f),new Vector2(.92f,.94f),Vector2.zero,Vector2.zero);
            detail = EmergencyRoadUI.Label(panel.transform,"",30,Color.white,TextAnchor.MiddleCenter,new Vector2(.08f,.58f),new Vector2(.92f,.73f),Vector2.zero,Vector2.zero);
            return panel;
        }

        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && !ended) TogglePause();
            if (paused || ended) return;
            float start=Settings!=null?Settings.startSpeed:StartSpeed;float max=Settings!=null?Settings.maxSpeed:MaxSpeed;float ramp=Settings!=null?Settings.distanceToMaxSpeed:800f;speed=Mathf.Min(max,start+(max-start)*distance/Mathf.Max(10f,ramp));
            float dz = speed * Time.deltaTime; distance += dz;
            for (int i=0;i<chunks.Count;i++) chunks[i].Move(-dz);
            var first = chunks[0];
            if (first.PositionZ < -42f)
            {
                float lastZ = chunks[^1].PositionZ;
                chunks.RemoveAt(0);int sequence=nextChunkSequence++;first.Recycle(lastZ+ChunkSpacing,ShouldSpawnCrossroad(sequence),sequence);chunks.Add(first);
            }
            scoreText.text = $"{Mathf.FloorToInt(distance):N0} m";
            coinText.text = $"● {EmergencyRoadProfile.Current.coins:N0}";
        }

        public void AddCoin() { EmergencyRoadProfile.Current.coins++; EmergencyRoadProfile.Save(); EmergencyRoadAudio.Instance.Coin(); }
        internal void SpawnSameDirectionTraffic(int lane,float worldZ)
        {
            if(catalog==null||catalog.trafficVehicles.Count==0)return;var holder=new GameObject("Road Hazard - Same Direction Traffic");holder.transform.SetParent(transform);holder.transform.position=new Vector3(lane*LaneWidth,.05f,worldZ);holder.AddComponent<RoadHazard>();
            var prefab=catalog.trafficVehicles[Random.Range(0,catalog.trafficVehicles.Count)];var visual=Instantiate(prefab,holder.transform);visual.name=$"Moving {prefab.name}";visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;FitVehicle(visual,2.12f,4.05f);foreach(var oldCollider in visual.GetComponentsInChildren<Collider>())Destroy(oldCollider);
            var collider=holder.AddComponent<BoxCollider>();collider.isTrigger=true;collider.center=new Vector3(0,.72f,0);collider.size=new Vector3(2.35f,1.45f,3.55f);var body=holder.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
            var tuning=Settings;float minimum=tuning!=null?tuning.trafficMinimumRoadSpeed:8f;float maximum=Mathf.Max(minimum,tuning!=null?tuning.trafficMaximumRoadSpeed:14f);holder.AddComponent<SameDirectionTraffic>().Initialize(this,lane,Random.Range(minimum,maximum),tuning!=null?tuning.trafficLaneChangeChance:.38f,tuning!=null?tuning.trafficLaneDecisionInterval:2.2f,tuning!=null?tuning.trafficBrakingDistance:11f,tuning!=null?tuning.trafficBrakingStrength:10f);
        }
        internal float Difficulty01=>Mathf.InverseLerp(Settings!=null?Settings.startSpeed:StartSpeed,Settings!=null?Settings.maxSpeed:MaxSpeed,speed);
        private bool ShouldSpawnCrossroad(int sequence)
        {
            if(sequence<nextCrossroadSequence)return false;lastCrossroadSequence=sequence;lastCrossroadRadius=nextCrossroadRadius;var tuning=Settings;int min=tuning!=null?tuning.minStraightChunksBetweenIntersections:10;int max=Mathf.Max(min,tuning!=null?tuning.maxStraightChunksBetweenIntersections:14);nextCrossroadSequence=sequence+Random.Range(min,max+1);nextCrossroadRadius=CrossroadRadius(nextCrossroadSequence);return true;
        }
        private int CrossroadRadius(int sequence){if(catalog==null||catalog.crossroadPrefabs.Count==0)return 1;var prefab=catalog.crossroadPrefabs[sequence%catalog.crossroadPrefabs.Count];var metrics=catalog.MetricsFor(prefab);if(metrics==null)return 1;return Mathf.Max(1,Mathf.CeilToInt((metrics.rendererSize.z/Mathf.Max(.1f,ChunkSpacing)-1f)*.5f));}
        internal bool IsCrossroadClearance(int sequence)
        {
            return Mathf.Abs(sequence-lastCrossroadSequence)<=lastCrossroadRadius||Mathf.Abs(sequence-nextCrossroadSequence)<=nextCrossroadRadius;
        }
        internal bool IsCrossroadMeshCovered(int sequence){return Mathf.Abs(sequence-lastCrossroadSequence)<=lastCrossroadRadius||Mathf.Abs(sequence-nextCrossroadSequence)<=nextCrossroadRadius;}
        internal bool ShouldSpawnObstacle(int sequence,bool crossroad)
        {
            float generationDistance=sequence*ChunkSpacing;if(generationDistance+ChunkSpacing*.5f<nextObstacleDistance)return false;
            // The opening is intentionally busy, then gains a little more breathing room as speed rises.
            float minimumGap=VehicleLength*2f;float plannedGap=Mathf.Lerp(Settings!=null?Settings.gapAtStart:11.5f,Settings!=null?Settings.gapAtMaxSpeed:17.5f,Difficulty01);plannedGap=Mathf.Max(minimumGap,plannedGap);nextObstacleDistance=generationDistance+plannedGap;
            return true;
        }
        public void SetHazardAlert(string message){if(hazardAlertText!=null)hazardAlertText.text=message;}
        public void Crash(Vector3 impactDirection=default)
        {
            if (ended) return; ended=true;if(impactDirection.sqrMagnitude<.001f)impactDirection=Vector3.forward;EmergencyRoadAudio.Instance.Crash(); player.CrashVisual(impactDirection.normalized);cameraJuice?.EnterCrashView(impactDirection.normalized);
            int score=Mathf.FloorToInt(distance); EmergencyRoadProfile.Current.highScore=Mathf.Max(score,EmergencyRoadProfile.Current.highScore); EmergencyRoadProfile.Save();
            gameOverScore.text=$"{score:N0} m  •  KỶ LỤC {EmergencyRoadProfile.Current.highScore:N0} m"; gameOverPanel.SetActive(true);
        }
        private void TogglePause() { paused=!paused; Time.timeScale=paused?0f:1f; pausePanel.SetActive(paused); }
        private void Restart() { Time.timeScale=1f; SceneManager.LoadScene("Game"); }
        private void Menu() { Time.timeScale=1f; SceneManager.LoadScene("Menu"); }
    }

    public sealed class EmergencyVehicleController : MonoBehaviour
    {
        private Transform visual; private EmergencyRoadGame game;private AudioClip vehicleHorn;private int vehicleIndex; private int lane;private int previousLane; private float targetX; private float bump; private bool crashed; private Coroutine edgeEffect;private Coroutine hornEffect; private Vector3 baseScale;private Vector3 baseLocalPosition;
        public int CurrentLane=>lane;
        public void AutomationMoveTowardLane(int desiredLane,bool bypassSideSafety=false){desiredLane=Mathf.Clamp(desiredLane,-1,1);if(bypassSideSafety){lane=desiredLane;targetX=lane*EmergencyRoadGame.LaneWidth;return;}if(desiredLane!=lane)Shift(desiredLane>lane?1:-1);}
        public void Initialize(Transform view, EmergencyRoadGame owner,AudioClip horn,int selectedVehicleIndex) { visual=view; game=owner;vehicleHorn=horn;vehicleIndex=selectedVehicleIndex; targetX=0; baseScale=visual.localScale;baseLocalPosition=visual.localPosition; }
        private void Update()
        {
            if (crashed || Time.timeScale==0f || Keyboard.current==null) return;
            if (Keyboard.current.aKey.wasPressedThisFrame) Shift(-1);
            if (Keyboard.current.dKey.wasPressedThisFrame) Shift(1);
            if (Keyboard.current.spaceKey.wasPressedThisFrame) Honk();
            var p=transform.position; p.x=Mathf.SmoothDamp(p.x,targetX,ref bump,.12f); transform.position=p;
            visual.localRotation=Quaternion.Slerp(visual.localRotation,Quaternion.Euler(0,0,(targetX-p.x)*-5f),Time.deltaTime*8f);
        }
        private void Shift(int dir)
        {
            int next=Mathf.Clamp(lane+dir,-1,1);
            if(next==lane) { StartEdgeSqueeze(dir); return; }
            if(EmergencyRoadProfile.Current.sideCollisionEnabled&&IsLaneBlockedBesidePlayer(next)){StartEdgeSqueeze(dir);return;}
            previousLane=lane;lane=next; targetX=lane*EmergencyRoadGame.LaneWidth;
        }
        private bool IsLaneBlockedBesidePlayer(int targetLane)
        {
            // Only protect the part of the adjacent lane that is genuinely beside the player's body.
            // A hazard whose front edge has cleared the player's rear is no longer allowed to block a lane change.
            float playerZ=transform.position.z;var center=new Vector3(targetLane*EmergencyRoadGame.LaneWidth,1f,playerZ+.15f);var tuning=game.Settings;float halfLength=tuning!=null?tuning.sideCheckHalfLength:.68f;float release=tuning!=null?tuning.releaseBehindPlayer:.32f;
            var hits=Physics.OverlapBox(center,new Vector3(EmergencyRoadGame.LaneWidth*.36f,.85f,halfLength),Quaternion.identity,~0,QueryTriggerInteraction.Collide);
            foreach(var hit in hits)
            {
                if(hit.transform.IsChildOf(transform)||hit.bounds.center.z<playerZ-release||hit.bounds.min.z>=playerZ+1.15f)continue;
                if(hit.GetComponentInParent<RoadHazard>()!=null||hit.GetComponentInParent<MotorRushHazard>()!=null)return true;
            }
            return false;
        }
        private void StartEdgeSqueeze(int dir){if(edgeEffect!=null)StopCoroutine(edgeEffect);if(hornEffect!=null){StopCoroutine(hornEffect);hornEffect=null;}visual.localScale=baseScale;visual.localPosition=baseLocalPosition;edgeEffect=StartCoroutine(EdgeSqueeze(dir));}
        private IEnumerator EdgeSqueeze(int dir)
        {
            const float duration=.14f;float t=0;while(t<duration){t+=Time.deltaTime;float s=Mathf.Sin(Mathf.Clamp01(t/duration)*Mathf.PI);visual.localPosition=baseLocalPosition+Vector3.right*(dir*.16f*s);visual.localScale=Vector3.Scale(baseScale,new Vector3(1f-.34f*s,1f+.035f*s,1f+.08f*s));yield return null;}visual.localPosition=baseLocalPosition;visual.localScale=baseScale;edgeEffect=null;
        }
        private void Honk(){EmergencyRoadAudio.Instance.Horn(vehicleHorn,vehicleIndex);if(hornEffect!=null)StopCoroutine(hornEffect);if(edgeEffect!=null){StopCoroutine(edgeEffect);edgeEffect=null;}visual.localPosition=baseLocalPosition;visual.localScale=baseScale;hornEffect=StartCoroutine(HonkBounce());}
        private IEnumerator HonkBounce(){float t=0;while(t<.38f){t+=Time.deltaTime;float s=Mathf.Sin(t/.38f*Mathf.PI);visual.localScale=Vector3.Scale(baseScale,new Vector3(1f-.08f*s,1f+.32f*s,1f-.08f*s));yield return null;}visual.localScale=baseScale;hornEffect=null;}
        private void OnTriggerEnter(Collider other)
        {
            var pickup=other.GetComponentInParent<RoadPickup>();if(pickup!=null){game.AddCoin();pickup.Collect();return;}if(other.GetComponentInParent<RoadHazard>()==null)return;var impact=other.bounds.center-transform.position;bool sideContact=Mathf.Abs(impact.x)>1.15f&&Mathf.Abs(impact.z)<1.65f;if(sideContact){if(EmergencyRoadProfile.Current.sideCollisionEnabled){int side=impact.x>=0f?1:-1;lane=previousLane;targetX=lane*EmergencyRoadGame.LaneWidth;StartEdgeSqueeze(side);}return;}game.Crash(impact);
        }
        private void OnCollisionEnter(Collision collision){if(collision.gameObject.GetComponentInParent<RoadHazard>()!=null)game.Crash(collision.collider.bounds.center-transform.position);}
        public void CrashVisual(Vector3 impactDirection){crashed=true;DeformMeshes(impactDirection);EnableHeavyCrashPhysics(impactDirection);var localImpact=transform.InverseTransformDirection(impactDirection.normalized);EmergencyImpactVfx.Attach(transform,new Vector3(Mathf.Clamp(localImpact.x,-1f,1f)*.8f,.72f,Mathf.Clamp(localImpact.z,-1f,1f)*1.35f));StartCoroutine(Crumple(impactDirection));}
        private void EnableHeavyCrashPhysics(Vector3 impactDirection)
        {
            var body=GetComponent<Rigidbody>();var hitbox=GetComponent<BoxCollider>();if(body==null)return;var flat=new Vector3(impactDirection.x,0,impactDirection.z);if(flat.sqrMagnitude<.001f)flat=Vector3.forward;var push=-flat.normalized;
            if(hitbox!=null)hitbox.isTrigger=false;body.isKinematic=false;body.useGravity=true;body.mass=1650f;body.linearDamping=2.4f;body.angularDamping=3.8f;body.maxAngularVelocity=2.8f;body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.interpolation=RigidbodyInterpolation.Interpolate;body.centerOfMass=new Vector3(0,.28f,0);
            body.linearVelocity=push*1.65f+Vector3.up*1.15f;body.angularVelocity=new Vector3(push.z*.42f,-push.x*.24f,push.x*.82f);
        }
        private void DeformMeshes(Vector3 impactDirection)
        {
            foreach(var filter in visual.GetComponentsInChildren<MeshFilter>())
            {
                try{var source=filter.sharedMesh;if(source==null)continue;var mesh=Object.Instantiate(source);mesh.name=source.name+" - Runtime Crash Deformed";var vertices=mesh.vertices;var bounds=mesh.bounds;var localImpact=filter.transform.InverseTransformDirection(impactDirection).normalized;var ext=bounds.extents;float impactExtent=Mathf.Abs(localImpact.x)*ext.x+Mathf.Abs(localImpact.y)*ext.y+Mathf.Abs(localImpact.z)*ext.z;float dentDepth=Mathf.Max(.08f,impactExtent*.42f);var sideways=Vector3.Cross(Vector3.up,localImpact).normalized;for(int i=0;i<vertices.Length;i++){var offset=vertices[i]-bounds.center;var normalized=new Vector3(offset.x/Mathf.Max(.01f,ext.x),offset.y/Mathf.Max(.01f,ext.y),offset.z/Mathf.Max(.01f,ext.z));float facing=Vector3.Dot(normalized,localImpact);float influence=Mathf.Pow(Mathf.SmoothStep(0,1,Mathf.InverseLerp(-.05f,.88f,facing)),1.25f);float crease=Mathf.Sin(i*12.9898f+offset.y*17f);vertices[i]+=-localImpact*(dentDepth*influence*(.78f+.16f*crease));vertices[i]+=sideways*(dentDepth*.12f*influence*crease);vertices[i].y-=ext.y*.22f*influence*(.65f+.2f*Mathf.Abs(crease));}mesh.vertices=vertices;mesh.RecalculateBounds();mesh.RecalculateNormals();filter.sharedMesh=mesh;}catch(UnityException){/* Read/Write is enabled by the project builder for player vehicle FBX files. */}
            }
        }
        private IEnumerator Crumple(Vector3 impactDirection){float t=0;var localImpact=visual.parent!=null?visual.parent.InverseTransformDirection(impactDirection).normalized:impactDirection.normalized;while(t<.55f){t+=Time.unscaledDeltaTime;float s=Mathf.SmoothStep(0,1,Mathf.Clamp01(t/.42f));float recoil=Mathf.Sin(Mathf.Clamp01(t/.55f)*Mathf.PI)*.06f;visual.localPosition=baseLocalPosition-localImpact*(.2f*s+recoil);visual.localRotation=Quaternion.Euler((5f+Mathf.Abs(localImpact.z)*7f)*s,localImpact.x*11f*s,-localImpact.x*18f*s);yield return null;}}
    }

    public sealed class RoadPickup : MonoBehaviour { public void Collect(){gameObject.SetActive(false);} }
    public sealed class RoadHazard : MonoBehaviour { }
    public sealed class RoadRouteMarker:MonoBehaviour { public int SoleOpenLane{get;private set;} public void Configure(int lane){SoleOpenLane=Mathf.Clamp(lane,-1,1);} }

    internal sealed class RoadChunk
    {
        private readonly GameObject root; private readonly EmergencyRoadCatalog catalog; private readonly EmergencyRoadGame owner; private readonly List<GameObject> spawned=new();
        public float PositionZ=>root.transform.position.z;
        private RoadChunk(GameObject go,EmergencyRoadCatalog data,EmergencyRoadGame game){root=go;catalog=data;owner=game;}
        public static RoadChunk Create(Transform parent,EmergencyRoadCatalog data,float z,bool cross,int sequence,EmergencyRoadGame game=null){var chunk=new RoadChunk(new GameObject("Road Chunk"),data,game);chunk.root.transform.SetParent(parent);chunk.Recycle(z,cross,sequence);return chunk;}
        public void Move(float dz){root.transform.position+=Vector3.forward*dz;foreach(var go in spawned)if(go!=null&&go.TryGetComponent<SideCrossingHazard>(out var crossing))crossing.Tick(dz);}
        public void Recycle(float z,bool cross,int sequence)
        {
            foreach(var go in spawned)if(go!=null)Object.Destroy(go);spawned.Clear();root.transform.position=new Vector3(0,0,z);bool meshCovered=!cross&&owner!=null&&owner.IsCrossroadMeshCovered(sequence);root.name=cross?"Crossroad Chunk":meshCovered?"Crossroad Footprint Spacer":"Road_1 Chunk";
            GameObject roadPrefab=null;
            if(cross&&catalog.crossroadPrefabs.Count>0)roadPrefab=catalog.crossroadPrefabs[sequence%catalog.crossroadPrefabs.Count];
            else if(!meshCovered&&catalog.roadPrefabs.Count>0)roadPrefab=catalog.roadPrefabs[0];
            if(!meshCovered){GameObject road=roadPrefab!=null?Object.Instantiate(roadPrefab,root.transform):CreateFallbackRoad();road.transform.localPosition=Vector3.zero;road.transform.localRotation=Quaternion.identity;if(cross&&roadPrefab!=null){var metrics=catalog.MetricsFor(roadPrefab);if(metrics!=null)road.transform.localPosition=new Vector3(-metrics.rendererCenter.x,0,-metrics.rendererCenter.z);}spawned.Add(road);}
            if(cross)SpawnCrossroadExtensions(roadPrefab);else if(owner==null||!owner.IsCrossroadClearance(sequence))SpawnGroundAndDecorations(sequence);
            if(!meshCovered&&sequence>1&&(owner==null||owner.ShouldSpawnObstacle(sequence,cross)))SpawnGameplay(cross);
        }
        private GameObject CreateFallbackRoad(){var road=GameObject.CreatePrimitive(PrimitiveType.Cube);road.transform.SetParent(root.transform);road.transform.localScale=new Vector3(11,.2f,28);road.GetComponent<Renderer>().material.color=new Color(.1f,.12f,.15f);return road;}
        private void SpawnCrossroadExtensions(GameObject crossroadPrefab)
        {
            if(catalog.roadPrefabs.Count==0)return;const int tilesPerSide=6;var metrics=catalog.MetricsFor(crossroadPrefab);float crossHalfWidth=metrics!=null?metrics.rendererSize.x*.5f:catalog.roadHalfWidth;float crossHalfLength=metrics!=null?metrics.rendererSize.z*.5f:EmergencyRoadGame.ChunkSpacing*.5f;
            for(int side=-1;side<=1;side+=2)for(int i=0;i<tilesPerSide;i++)
            {
                var road=Object.Instantiate(catalog.roadPrefabs[0],root.transform);road.name=$"Cross Street {(side<0?"Left":"Right")} {i+1} - Exact Edge";road.transform.localRotation=Quaternion.Euler(0,90,0);road.transform.localPosition=new Vector3(side*(crossHalfWidth+.04f+(i+.5f)*catalog.roadLength),-.012f,0);spawned.Add(road);
            }
            int radius=metrics!=null?Mathf.Max(1,Mathf.CeilToInt((metrics.rendererSize.z/catalog.roadLength-1f)*.5f)):1;float nextInnerEdge=(radius+.5f)*catalog.roadLength;float gap=Mathf.Max(0,nextInnerEdge-crossHalfLength);if(gap>.03f)for(int side=-1;side<=1;side+=2){var connector=Object.Instantiate(catalog.roadPrefabs[0],root.transform);connector.name=$"Main Road Connector {(side<0?"Back":"Forward")} - {gap:F2}m";connector.transform.localPosition=new Vector3(0,-.006f,side*(crossHalfLength+gap*.5f));connector.transform.localScale=new Vector3(1,1,gap/catalog.roadLength);spawned.Add(connector);SpawnTransitionGround(side,crossHalfLength,gap);}SpawnBranchVerge(crossHalfWidth,nextInnerEdge,tilesPerSide);SpawnInnerCornerGrass(crossHalfWidth,crossHalfLength);SpawnBranchScenery(crossHalfWidth,tilesPerSide);
        }
        private void SpawnInnerCornerGrass(float crossHalfWidth,float crossHalfLength)
        {
            float width=Mathf.Max(.1f,crossHalfWidth-catalog.roadHalfWidth);float depth=Mathf.Max(.1f,crossHalfLength-catalog.roadHalfWidth);for(int xSide=-1;xSide<=1;xSide+=2)for(int zSide=-1;zSide<=1;zSide+=2){var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Crossroad Inner Corner Grass - Under Mesh Cutout";ground.transform.SetParent(root.transform);ground.transform.localPosition=new Vector3(xSide*(catalog.roadHalfWidth+width*.5f),-.28f,zSide*(catalog.roadHalfWidth+depth*.5f));ground.transform.localScale=new Vector3(width+.06f,.36f,depth+.06f);if(catalog.grassMaterial!=null)ground.GetComponent<Renderer>().sharedMaterial=catalog.grassMaterial;spawned.Add(ground);}
        }
        private void SpawnBranchVerge(float crossHalfWidth,float outerZ,int tilesPerSide)
        {
            float innerZ=catalog.roadHalfWidth-.45f;float depth=Mathf.Max(.1f,outerZ-innerZ);float outerX=crossHalfWidth+tilesPerSide*catalog.roadLength;float width=outerX-crossHalfWidth;for(int xSide=-1;xSide<=1;xSide+=2)for(int zSide=-1;zSide<=1;zSide+=2){var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Cross Street Grass Verge - Overlaps Sidewalk";ground.transform.SetParent(root.transform);ground.transform.localPosition=new Vector3(xSide*(crossHalfWidth+width*.5f),-.28f,zSide*(innerZ+depth*.5f));ground.transform.localScale=new Vector3(width+.08f,.36f,depth+.08f);if(catalog.grassMaterial!=null)ground.GetComponent<Renderer>().sharedMaterial=catalog.grassMaterial;spawned.Add(ground);}
        }
        private void SpawnBranchScenery(float crossHalfWidth,int tilesPerSide)
        {
            var lamp=catalog.streetDecorationPrefabs.Find(x=>x!=null&&x.name=="Light");for(int xSide=-1;xSide<=1;xSide+=2)for(int zSide=-1;zSide<=1;zSide+=2)for(int i=0;i<tilesPerSide;i++)
            {
                float x=xSide*(crossHalfWidth+(i+.5f)*catalog.roadLength);if(i%2==0&&catalog.decorationPrefabs.Count>0){var building=Object.Instantiate(catalog.decorationPrefabs[(i+(xSide>0?1:0)+(zSide>0?2:0))%catalog.decorationPrefabs.Count],root.transform);building.name="Cross Street Building - Fitted Lot";building.transform.localPosition=new Vector3(x,0,0);building.transform.localRotation=Quaternion.Euler(0,zSide>0?90:-90,0);FitBranchBuilding(building,x,zSide);spawned.Add(building);}else if(catalog.naturePrefabs.Count>0){var tree=Object.Instantiate(catalog.naturePrefabs[i%catalog.naturePrefabs.Count],root.transform);tree.name="Cross Street Tree";tree.transform.localPosition=new Vector3(x,0,zSide*(catalog.roadHalfWidth+3.2f));tree.transform.localRotation=Quaternion.Euler(0,(i*67+xSide*31+zSide*19)%360,0);spawned.Add(tree);}if(lamp!=null&&(i==1||i==4)){var light=Object.Instantiate(lamp,root.transform);light.name="Cross Street Light - Sidewalk";light.transform.localPosition=new Vector3(x,0,zSide*(catalog.roadHalfWidth-.9f));light.transform.localRotation=Quaternion.Euler(0,zSide>0?180:0,0);spawned.Add(light);}
            }
        }
        private void FitBranchBuilding(GameObject building,float targetX,int zSide)
        {
            var renderers=building.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);float scale=Mathf.Min(1f,Mathf.Min(catalog.roadLength*1.55f/Mathf.Max(.1f,bounds.size.x),5.8f/Mathf.Max(.1f,bounds.size.z)));building.transform.localScale*=scale;bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);var local=building.transform.localPosition;local.x=targetX-(bounds.center.x-building.transform.position.x);float desiredZ=zSide*(catalog.roadHalfWidth+.8f+bounds.extents.z);local.z=desiredZ-(bounds.center.z-building.transform.position.z);local.y-=bounds.min.y;building.transform.localPosition=local;
        }
        private void SpawnTransitionGround(int zSide,float crossHalfLength,float gap)
        {
            for(int xSide=-1;xSide<=1;xSide+=2){var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Intersection Corner Grass - Exact Gap Fill";ground.transform.SetParent(root.transform);ground.transform.localPosition=new Vector3(xSide*(catalog.roadHalfWidth+10f),-.28f,zSide*(crossHalfLength+gap*.5f));ground.transform.localScale=new Vector3(18f,.36f,gap+.04f);if(catalog.grassMaterial!=null)ground.GetComponent<Renderer>().sharedMaterial=catalog.grassMaterial;spawned.Add(ground);var soil=GameObject.CreatePrimitive(PrimitiveType.Cube);soil.name="Intersection Soil Edge - Exact Gap Fill";soil.transform.SetParent(root.transform);soil.transform.localPosition=new Vector3(xSide*(catalog.roadHalfWidth+.4f),-.12f,zSide*(crossHalfLength+gap*.5f));soil.transform.localScale=new Vector3(.8f,.12f,gap+.04f);if(catalog.soilMaterial!=null)soil.GetComponent<Renderer>().sharedMaterial=catalog.soilMaterial;spawned.Add(soil);}
        }
        private void SpawnGroundAndDecorations(int sequence)
        {
            for(int side=-1;side<=1;side+=2)
            {
                var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name=side<0?"Grass Ground Left":"Grass Ground Right";ground.transform.SetParent(root.transform);ground.transform.localPosition=new Vector3(side*(catalog.roadHalfWidth+10f),-.28f,0);ground.transform.localScale=new Vector3(18f,.36f,EmergencyRoadGame.ChunkSpacing+.08f);if(catalog.grassMaterial!=null)ground.GetComponent<Renderer>().sharedMaterial=catalog.grassMaterial;spawned.Add(ground);
                var soil=GameObject.CreatePrimitive(PrimitiveType.Cube);soil.name=side<0?"Narrow Soil Border Left":"Narrow Soil Border Right";soil.transform.SetParent(root.transform);soil.transform.localPosition=new Vector3(side*(catalog.roadHalfWidth+.4f),-.12f,0);soil.transform.localScale=new Vector3(.8f,.12f,EmergencyRoadGame.ChunkSpacing+.08f);if(catalog.soilMaterial!=null)soil.GetComponent<Renderer>().sharedMaterial=catalog.soilMaterial;spawned.Add(soil);
                if(catalog.decorationPrefabs.Count>0){var prefab=catalog.decorationPrefabs[(sequence*2+(side>0?1:0))%catalog.decorationPrefabs.Count];var go=Object.Instantiate(prefab,root.transform);go.name="City Building - Fitted To Lot";go.transform.localPosition=new Vector3(0,0,0);go.transform.localRotation=Quaternion.Euler(0,side>0?180:0,0);EmergencyRoadGame.FitBuildingToLot(go,EmergencyRoadGame.ChunkSpacing*.82f);EmergencyRoadGame.PositionOutsideRoad(go,side,catalog.roadHalfWidth,8.5f);spawned.Add(go);}
                for(int j=0;j<2;j++)
                {
                    float z=-EmergencyRoadGame.ChunkSpacing*.3f+j*EmergencyRoadGame.ChunkSpacing*.5f+(sequence%2)*2f;
                    if(catalog.naturePrefabs.Count>0){var tree=Object.Instantiate(catalog.naturePrefabs[(sequence+j)%catalog.naturePrefabs.Count],root.transform);tree.name="Roadside Tree - Grass Zone";tree.transform.localPosition=new Vector3(side*(catalog.roadHalfWidth+4.2f+j*2.6f),0,z);tree.transform.localRotation=Quaternion.Euler(0,(sequence*53+j*71)%360,0);tree.transform.localScale=Vector3.one*(.85f+((sequence+j)%3)*.12f);spawned.Add(tree);}
                }
                if(catalog.streetDecorationPrefabs.Count>0)
                {
                    var lampPrefab=catalog.streetDecorationPrefabs.Find(x=>x!=null&&x.name=="Light");
                    if(sequence%3==0&&lampPrefab!=null){var lamp=Object.Instantiate(lampPrefab,root.transform);lamp.name="Street Light - Sidewalk - Faces Road";lamp.transform.localPosition=new Vector3(side*(catalog.roadHalfWidth-.9f),0,-EmergencyRoadGame.ChunkSpacing*.25f);lamp.transform.localRotation=Quaternion.Euler(0,side<0?90:-90,0);spawned.Add(lamp);}
                    if(sequence%4==1){var propPrefab=catalog.streetDecorationPrefabs[(sequence+Mathf.Abs(side))%catalog.streetDecorationPrefabs.Count];if(propPrefab!=null&&propPrefab!=lampPrefab){var prop=Object.Instantiate(propPrefab,root.transform);prop.name="Sparse Sidewalk Detail";prop.transform.localPosition=new Vector3(side*(catalog.roadHalfWidth-.65f),0,EmergencyRoadGame.ChunkSpacing*.28f);prop.transform.localRotation=Quaternion.Euler(0,side<0?90:-90,0);spawned.Add(prop);}}
                }
            }
        }
        private void SpawnGameplay(bool cross)
        {
            if(cross){SpawnCrossing();return;}
            int openLane=Random.Range(-1,2);float obstacleZ=Random.Range(-EmergencyRoadGame.ChunkSpacing*.22f,EmergencyRoadGame.ChunkSpacing*.22f);
            // Every row leaves one or two usable lanes. Two-lane blocks are common early and ease off later.
            var tuning=owner!=null?owner.Settings:null;float doubleBlockChance=owner==null?.45f:Mathf.Lerp(tuning!=null?tuning.twoLaneBlockChanceAtStart:.58f,tuning!=null?tuning.twoLaneBlockChanceAtMaxSpeed:.32f,owner.Difficulty01);
            float movingTrafficChance=tuning!=null?tuning.sameDirectionTrafficChance:.28f;
            if(owner!=null&&catalog.trafficVehicles.Count>0&&Random.value<movingTrafficChance){int trafficLane=openLane;while(trafficLane==openLane)trafficLane=Random.Range(-1,2);owner.SpawnSameDirectionTraffic(trafficLane,root.transform.position.z+obstacleZ);for(int i=0;i<4;i++)SpawnCoin(openLane,-EmergencyRoadGame.ChunkSpacing*.38f+i*(EmergencyRoadGame.ChunkSpacing*.76f/3f));return;}
            bool doubleBlock=Random.value<doubleBlockChance;
            if(doubleBlock){SpawnRouteMarker(openLane,obstacleZ);for(int lane=-1;lane<=1;lane++)if(lane!=openLane)SpawnObstacle(lane,obstacleZ);}
            else{int blockedLane=openLane;while(blockedLane==openLane)blockedLane=Random.Range(-1,2);SpawnObstacle(blockedLane,obstacleZ);}
            for(int i=0;i<4;i++)SpawnCoin(openLane,-EmergencyRoadGame.ChunkSpacing*.38f+i*(EmergencyRoadGame.ChunkSpacing*.76f/3f));
        }
        private void SpawnRouteMarker(int soleOpenLane,float localZ){var marker=new GameObject($"Route Reservation - Lane {soleOpenLane}");marker.transform.SetParent(root.transform);marker.transform.localPosition=new Vector3(0,0,localZ);marker.AddComponent<RoadRouteMarker>().Configure(soleOpenLane);spawned.Add(marker);}
        private void SpawnObstacle(int lane,float localZ)
        {
            float roll=Random.value;
            var tuning=owner!=null?owner.Settings:null;float vehicleChance=tuning!=null?tuning.stoppedVehicleChance:.68f;float barrierChance=tuning!=null?tuning.barrierChance:.24f;if(roll<vehicleChance&&catalog.trafficVehicles.Count>0){SpawnParkedVehicle(lane,localZ);return;}

            var barriers=catalog.obstaclePrefabs.FindAll(x=>x!=null&&x.name.ToLowerInvariant().Contains("barrier"));
            GameObject prefab=roll<vehicleChance+barrierChance&&barriers.Count>0?barriers[Random.Range(0,barriers.Count)]:(catalog.obstaclePrefabs.Count>0?catalog.obstaclePrefabs[Random.Range(0,catalog.obstaclePrefabs.Count)]:null);
            string sourceName=prefab!=null?prefab.name:"fallback barrier";var go=prefab?Object.Instantiate(prefab,root.transform):GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=$"Road Hazard - {sourceName}";go.transform.localPosition=new Vector3(lane*EmergencyRoadGame.LaneWidth,.05f,localZ);go.transform.localRotation=Quaternion.identity;go.AddComponent<RoadHazard>();FitObstacleToLane(go,EmergencyRoadGame.LaneWidth*(tuning!=null?tuning.obstacleLaneWidth:.86f),sourceName);spawned.Add(go);
        }
        private void SpawnParkedVehicle(int lane,float localZ)
        {
            var holder=new GameObject("Road Hazard - Stopped Vehicle");holder.transform.SetParent(root.transform);holder.transform.localPosition=new Vector3(lane*EmergencyRoadGame.LaneWidth,.05f,localZ);holder.AddComponent<RoadHazard>();
            var tuning=owner!=null?owner.Settings:null;var size=tuning!=null?tuning.stoppedVehicleSize:new Vector2(2.18f,4.05f);var prefab=catalog.trafficVehicles[Random.Range(0,catalog.trafficVehicles.Count)];var visual=Object.Instantiate(prefab,holder.transform);visual.name=$"Stopped {prefab.name}";visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;EmergencyRoadGame.FitVehicle(visual,size.x,size.y);
            foreach(var oldCollider in visual.GetComponentsInChildren<Collider>())Object.Destroy(oldCollider);var box=holder.AddComponent<BoxCollider>();box.center=new Vector3(0,.72f,0);box.size=tuning!=null?tuning.stoppedVehicleHitbox:new Vector3(EmergencyRoadGame.LaneWidth*.82f,1.45f,3.55f);spawned.Add(holder);
        }
        private static void FitObstacleToLane(GameObject go,float targetWidth,string sourceName)
        {
            var renderers=go.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
            string id=sourceName.ToLowerInvariant();float visualWidth=id.Contains("cone")?.65f:id.Contains("box")?1.45f:id.Contains("trash")?1.85f:targetWidth;float scale=visualWidth/Mathf.Max(.05f,bounds.size.x);go.transform.localScale*=scale;
            foreach(var collider in go.GetComponentsInChildren<Collider>())Object.Destroy(collider);bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);var box=go.AddComponent<BoxCollider>();box.center=go.transform.InverseTransformPoint(bounds.center);box.size=new Vector3(targetWidth/Mathf.Max(.001f,go.transform.lossyScale.x),Mathf.Max(.35f,bounds.size.y*.88f/Mathf.Max(.001f,go.transform.lossyScale.y)),Mathf.Clamp(bounds.size.z*.82f,.5f,2.8f)/Mathf.Max(.001f,go.transform.lossyScale.z));
        }
        private void SpawnCoin(int lane,float localZ)
        {
            GameObject go;if(catalog.coinPrefab!=null){go=Object.Instantiate(catalog.coinPrefab,root.transform);go.name="Coin Pickup - Scene Prefab";}else{go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name="Coin Pickup - Fallback";go.transform.SetParent(root.transform);go.transform.localRotation=Quaternion.Euler(90,0,0);go.transform.localScale=new Vector3(.48f,.13f,.48f);go.GetComponent<Renderer>().material.color=EmergencyRoadUI.Yellow;}go.transform.localPosition=new Vector3(lane*EmergencyRoadGame.LaneWidth,1.1f,localZ);var c=go.GetComponentInChildren<Collider>()??go.AddComponent<SphereCollider>();c.isTrigger=true;if(go.GetComponent<RoadPickup>()==null)go.AddComponent<RoadPickup>();if(go.GetComponent<CoinSpinner>()==null)go.AddComponent<CoinSpinner>();spawned.Add(go);
        }
        private void SpawnCrossing()
        {
            int lane=FindSafeLane(root.transform.position.z+8f);bool left=Random.value<.5f;GameObject prefab=catalog.trafficVehicles.Count>0?catalog.trafficVehicles[Random.Range(0,catalog.trafficVehicles.Count)]:null;
            var go=prefab?Object.Instantiate(prefab,root.transform):GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Cross Traffic Hazard";go.transform.localPosition=new Vector3(left?-12f:12f,.45f,8f);go.transform.localRotation=Quaternion.identity;var tuning=owner!=null?owner.Settings:null;var size=tuning!=null?tuning.stoppedVehicleSize:new Vector2(2.18f,4.05f);EmergencyRoadGame.FitVehicle(go,size.x,size.y);go.transform.localRotation=Quaternion.Euler(0,left?90:-90,0);foreach(var oldCollider in go.GetComponentsInChildren<Collider>())Object.Destroy(oldCollider);go.AddComponent<RoadHazard>();var c=go.AddComponent<BoxCollider>();c.isTrigger=true;c.center=new Vector3(0,.72f,0);c.size=tuning!=null?tuning.stoppedVehicleHitbox:new Vector3(2.54f,1.45f,3.55f);var body=go.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;var mover=go.AddComponent<SideCrossingHazard>();mover.Configure(lane*EmergencyRoadGame.LaneWidth,left);spawned.Add(go);
        }
        private static int FindSafeLane(float worldZ)
        {
            int start=Random.Range(0,3);Physics.SyncTransforms();for(int i=0;i<3;i++){int lane=((start+i)%3)-1;var hits=Physics.OverlapBox(new Vector3(lane*EmergencyRoadGame.LaneWidth,.9f,worldZ),new Vector3(1.15f,.9f,2.3f),Quaternion.identity,~0,QueryTriggerInteraction.Collide);bool occupied=false;foreach(var hit in hits)if(hit.GetComponentInParent<RoadHazard>()!=null){occupied=true;break;}if(!occupied)return lane;}return Random.Range(-1,2);
        }
    }
    public sealed class CoinSpinner:MonoBehaviour{private float baseY;private float phase;private void Awake(){baseY=transform.localPosition.y;phase=Random.value*6.28f;}private void Update(){transform.Rotate(0,180f*Time.deltaTime,0,Space.Self);var p=transform.localPosition;p.y=baseY+Mathf.Sin(Time.time*3.2f+phase)*.13f;transform.localPosition=p;}}
    public sealed class EmergencyCameraJuice:MonoBehaviour
    {
        private Transform target;private Vector3 basePosition;private float shakeTime;private float shakeStrength;private Camera cam;private bool crashView;private Vector3 crashOffset;
        public void Initialize(Transform value){target=value;basePosition=transform.position;cam=GetComponent<Camera>();}
        public void Shake(float duration,float strength){shakeTime=duration;shakeStrength=strength;}
        public void EnterCrashView(Vector3 impactDirection){crashView=true;float side=impactDirection.x>=0f?-1f:1f;crashOffset=new Vector3(side*6.8f,4.4f,-6.8f);Shake(.48f,.28f);}
        private void LateUpdate(){if(target==null)return;float dt=Time.unscaledDeltaTime;if(crashView){var desired=target.position+crashOffset;if(shakeTime>0){shakeTime-=dt;desired+=(Vector3)Random.insideUnitCircle*shakeStrength*Mathf.Clamp01(shakeTime/.48f);}transform.position=Vector3.Lerp(transform.position,desired,1f-Mathf.Exp(-3.5f*dt));var aim=target.position+Vector3.left*1.65f+Vector3.up*.75f;var rotation=Quaternion.LookRotation(aim-transform.position,Vector3.up);transform.rotation=Quaternion.Slerp(transform.rotation,rotation,1f-Mathf.Exp(-4f*dt));if(cam!=null)cam.fieldOfView=Mathf.Lerp(cam.fieldOfView,49f,1f-Mathf.Exp(-3f*dt));return;}float targetX=target.position.x*.16f;var follow=basePosition+Vector3.right*targetX;if(shakeTime>0){shakeTime-=dt;follow+=(Vector3)Random.insideUnitCircle*shakeStrength*(shakeTime/.42f);}transform.position=Vector3.Lerp(transform.position,follow,1f-Mathf.Exp(-5f*dt));if(cam!=null)cam.fieldOfView=Mathf.Lerp(cam.fieldOfView,58f+Mathf.Abs(target.position.x)*.22f,1f-Mathf.Exp(-3f*dt));}
    }
    public sealed class SideCrossingHazard:MonoBehaviour
    {
        private float targetX;private bool fromLeft;private bool moving;
        public void Configure(float x,bool left){targetX=x;fromLeft=left;}
        public void Tick(float roadDelta){if(!moving&&transform.position.z<36f)moving=true;if(!moving)return;var p=transform.localPosition;p.x=Mathf.MoveTowards(p.x,targetX,8f*Time.deltaTime);transform.localPosition=p;}
    }

    public sealed class SameDirectionTraffic:MonoBehaviour
    {
        private EmergencyRoadGame game;private int lane;private float targetX;private float roadSpeed;private float cruiseSpeed;private float laneChangeChance;private float decisionInterval;private float brakingDistance;private float brakingStrength;private float nextDecision;private float nextSafetyDecision;private float xVelocity;private bool occupyingReservedEscape;
        public void Initialize(EmergencyRoadGame owner,int startLane,float speed,float changeChance,float interval,float brakeDistance,float brakeStrength){game=owner;lane=Mathf.Clamp(startLane,-1,1);targetX=lane*EmergencyRoadGame.LaneWidth;roadSpeed=cruiseSpeed=speed;laneChangeChance=changeChance;decisionInterval=Mathf.Max(.25f,interval);brakingDistance=Mathf.Max(4f,brakeDistance);brakingStrength=Mathf.Max(1f,brakeStrength);nextDecision=Time.time+Random.Range(decisionInterval*.65f,decisionInterval*1.35f);}
        private void Update()
        {
            if(game==null||game.Ended)return;if(Time.time>=nextSafetyDecision){nextSafetyDecision=Time.time+.16f;occupyingReservedEscape=LaneIsReservedEscape(lane);if(occupyingReservedEscape)EvacuateReservedEscapeLane();}float clearance=ForwardClearance(lane,brakingDistance+4f);float desiredSpeed=clearance<brakingDistance&&!occupyingReservedEscape?0f:cruiseSpeed;roadSpeed=Mathf.MoveTowards(roadSpeed,desiredSpeed,(desiredSpeed<roadSpeed?brakingStrength:brakingStrength*.35f)*Time.deltaTime);var position=transform.position;position.z+=(roadSpeed-game.CurrentSpeed)*Time.deltaTime;position.x=Mathf.SmoothDamp(position.x,targetX,ref xVelocity,.42f,EmergencyRoadGame.LaneWidth*1.65f);transform.position=position;
            if(position.z<-32f||position.z>165f){Destroy(gameObject);return;}if(Time.time<nextDecision||position.z<9f)return;nextDecision=Time.time+Random.Range(decisionInterval*.75f,decisionInterval*1.35f);int soleRoute=FindSoleOpenRoute();if(soleRoute>=-1){if(lane==soleRoute)LeaveSoleEscapeLane();return;}if(Random.value>laneChangeChance)return;
            int direction=Random.value<.5f?-1:1;int candidate=Mathf.Clamp(lane+direction,-1,1);if(candidate==lane)candidate=Mathf.Clamp(lane-direction,-1,1);if(candidate!=lane&&!LaneIsReservedEscape(candidate)&&LaneIsClear(candidate)){lane=candidate;targetX=lane*EmergencyRoadGame.LaneWidth;}
        }
        private int FindSoleOpenRoute(){int count=0,last=-2;for(int candidate=-1;candidate<=1;candidate++)if(ForwardClearance(candidate,brakingDistance+7f)>=brakingDistance){count++;last=candidate;}return count==1?last:-2;}
        private void LeaveSoleEscapeLane(){int left=lane-1,right=lane+1;if(left>=-1&&!LaneIsReservedEscape(left)&&LaneIsClear(left)){lane=left;targetX=lane*EmergencyRoadGame.LaneWidth;return;}if(right<=1&&!LaneIsReservedEscape(right)&&LaneIsClear(right)){lane=right;targetX=lane*EmergencyRoadGame.LaneWidth;}}
        private bool LaneIsReservedEscape(int candidate)
        {
            float back=transform.position.z-5f,front=transform.position.z+brakingDistance+EmergencyRoadGame.ChunkSpacing*1.6f;foreach(var marker in Object.FindObjectsByType<RoadRouteMarker>(FindObjectsSortMode.None))if(marker!=null&&marker.transform.position.z>=back&&marker.transform.position.z<=front&&marker.SoleOpenLane==candidate)return true;return false;
        }
        private void EvacuateReservedEscapeLane()
        {
            bool[] forbidden=new bool[3];float back=transform.position.z-5f,front=transform.position.z+brakingDistance+EmergencyRoadGame.ChunkSpacing*1.6f;foreach(var marker in Object.FindObjectsByType<RoadRouteMarker>(FindObjectsSortMode.None))if(marker!=null&&marker.transform.position.z>=back&&marker.transform.position.z<=front)forbidden[marker.SoleOpenLane+1]=true;
            int best=-99;for(int candidate=-1;candidate<=1;candidate++)if(!forbidden[candidate+1]&&LaneIsClear(candidate)&&(best==-99||Mathf.Abs(candidate-lane)<Mathf.Abs(best-lane)))best=candidate;if(best==-99||best==lane)return;int step=lane+System.Math.Sign(best-lane);if(LaneIsClear(step)){lane=step;targetX=lane*EmergencyRoadGame.LaneWidth;occupyingReservedEscape=LaneIsReservedEscape(lane);}
        }
        private float ForwardClearance(int candidate,float distance)
        {
            Physics.SyncTransforms();float nearest=float.PositiveInfinity;var center=new Vector3(candidate*EmergencyRoadGame.LaneWidth,.9f,transform.position.z+distance*.5f);var hits=Physics.OverlapBox(center,new Vector3(1.25f,.9f,distance*.5f),Quaternion.identity,~0,QueryTriggerInteraction.Collide);foreach(var hit in hits){if(hit.transform.IsChildOf(transform)||transform.IsChildOf(hit.transform))continue;if(hit.GetComponentInParent<RoadHazard>()==null)continue;float gap=hit.bounds.min.z-(transform.position.z+1.78f);if(gap>=-.1f)nearest=Mathf.Min(nearest,gap);}return nearest;
        }
        private bool LaneIsClear(int candidate)
        {
            Physics.SyncTransforms();var center=new Vector3(candidate*EmergencyRoadGame.LaneWidth,.9f,transform.position.z);var hits=Physics.OverlapBox(center,new Vector3(1.3f,.9f,5.5f),Quaternion.identity,~0,QueryTriggerInteraction.Collide);foreach(var hit in hits){if(hit.transform.IsChildOf(transform)||transform.IsChildOf(hit.transform))continue;if(hit.GetComponentInParent<RoadHazard>()!=null||hit.GetComponentInParent<EmergencyVehicleController>()!=null)return false;}return true;
        }
    }

    public sealed class MotorRushDirector:MonoBehaviour
    {
        private EmergencyRoadGame game;private Transform player;private float timer;private bool active;
        public void Initialize(EmergencyRoadGame owner,Transform playerTransform){game=owner;player=playerTransform;timer=Random.Range(10f,15f);}
        private void Update(){if(game==null||player==null||Time.timeScale==0f||active)return;timer-=Time.deltaTime;if(timer<=0)StartCoroutine(RunEvent());}
        private IEnumerator RunEvent()
        {
            active=true;var lineGo=new GameObject("MOTOR WARNING - Tracking Curved Red Path",typeof(LineRenderer));var line=lineGo.GetComponent<LineRenderer>();line.positionCount=22;line.useWorldSpace=true;line.widthMultiplier=.3f;line.numCapVertices=4;line.numCornerVertices=4;line.sharedMaterial=MotorRushHazard.WarningMaterial;line.startColor=new Color(1f,.02f,.01f,.8f);line.endColor=new Color(1f,.05f,.01f,.18f);
            float targetX=player.position.x;float t=0;game.SetHazardAlert("CẢNH BÁO: MÔ TÔ ĐANG LAO TỚI");
            while(t<2.2f){t+=Time.unscaledDeltaTime;targetX=Mathf.Lerp(targetX,player.position.x,1f-Mathf.Exp(-3.2f*Time.unscaledDeltaTime));float pulse=.22f+Mathf.Sin(t*18f)*.08f;line.widthMultiplier=pulse;UpdateWarningPath(line,targetX);yield return null;}
            game.SetHazardAlert("ĐÃ KHÓA HƯỚNG — NÉ NGAY!");line.startColor=Color.red;line.endColor=new Color(1,.05f,.01f,.7f);line.widthMultiplier=.42f;yield return new WaitForSecondsRealtime(.55f);
            var motor=MotorRushHazard.Create(targetX,game,player);Object.Destroy(lineGo);game.SetHazardAlert("");
            while(motor!=null)yield return null;
            timer=Random.Range(15f,23f);active=false;
        }
        private static void UpdateWarningPath(LineRenderer line,float targetX){for(int i=0;i<line.positionCount;i++){float z=Mathf.Lerp(-18f,72f,(float)i/(line.positionCount-1));float weave=Mathf.Sin(z*.28f)*.65f;line.SetPosition(i,new Vector3(targetX+weave,.08f,z));}}
    }

    public sealed class MotorRushHazard:MonoBehaviour
    {
        private static Material warningMaterial;private static Material bodyMaterial;private static Material darkMaterial;
        private EmergencyRoadGame game;private Transform player;private float speed=34f;private bool exploded;private bool pathLocked;private bool hasBeenInsideCamera;private float lockedTargetX;private float lateralVelocity;private float trackingVelocity;private Camera trackingCamera;private Renderer[] visualRenderers;private readonly Plane[] frustumPlanes=new Plane[6];private Rigidbody explodedBody;private bool compensateMapScroll;private float compensatedMapSpeed;
        public static Material WarningMaterial=>warningMaterial??=CreateMaterial(new Color(1f,.015f,.005f),true);
        public static MotorRushHazard Create(float x,EmergencyRoadGame owner,Transform playerTransform)
        {
            var root=new GameObject("Rushing Motorcycle Hazard - Delayed Smooth Tracking");root.transform.position=new Vector3(x+Mathf.Sin(-18f*.28f)*.5f,.45f,-18f);var hazard=root.AddComponent<MotorRushHazard>();hazard.game=owner;hazard.player=playerTransform;hazard.lockedTargetX=x;
            var collider=root.AddComponent<BoxCollider>();collider.isTrigger=true;collider.size=new Vector3(.95f,1.35f,2.25f);collider.center=new Vector3(0,.45f,0);var body=root.AddComponent<Rigidbody>();body.isKinematic=true;body.useGravity=false;
            if(owner!=null&&owner.Catalog!=null&&owner.Catalog.motorcyclePrefab!=null){var visual=Object.Instantiate(owner.Catalog.motorcyclePrefab,root.transform);visual.name="Motorcycle Visual - Scene Prefab";visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;EmergencyRoadGame.FitVehicle(visual,.9f,2.1f);foreach(var childCollider in visual.GetComponentsInChildren<Collider>())Object.Destroy(childCollider);}else BuildMotorVisual(root.transform);hazard.trackingCamera=Camera.main;hazard.visualRenderers=root.GetComponentsInChildren<Renderer>();return hazard;
        }
        private static void BuildMotorVisual(Transform root)
        {
            bodyMaterial??=CreateMaterial(new Color(.9f,.07f,.025f),false);darkMaterial??=CreateMaterial(new Color(.025f,.03f,.04f),false);
            Primitive(root,"Motor Body",PrimitiveType.Cube,new Vector3(0,.52f,0),new Vector3(.75f,.38f,1.45f),bodyMaterial,Quaternion.identity);
            Primitive(root,"Fuel Tank",PrimitiveType.Sphere,new Vector3(0,.78f,.15f),new Vector3(.65f,.5f,.72f),bodyMaterial,Quaternion.identity);
            Primitive(root,"Front Wheel",PrimitiveType.Cylinder,new Vector3(0,.32f,.88f),new Vector3(.48f,.18f,.48f),darkMaterial,Quaternion.Euler(0,0,90));
            Primitive(root,"Rear Wheel",PrimitiveType.Cylinder,new Vector3(0,.32f,-.88f),new Vector3(.48f,.18f,.48f),darkMaterial,Quaternion.Euler(0,0,90));
            Primitive(root,"Rider",PrimitiveType.Capsule,new Vector3(0,1.08f,-.08f),new Vector3(.5f,.72f,.5f),darkMaterial,Quaternion.Euler(14,0,0));
            Primitive(root,"Helmet",PrimitiveType.Sphere,new Vector3(0,1.56f,.18f),new Vector3(.46f,.46f,.46f),bodyMaterial,Quaternion.identity);
            Primitive(root,"Handlebar",PrimitiveType.Cube,new Vector3(0,.98f,.67f),new Vector3(1.05f,.1f,.1f),darkMaterial,Quaternion.identity);
        }
        private static void Primitive(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Material material,Quaternion rotation){var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent);go.transform.localPosition=position;go.transform.localScale=scale;go.transform.localRotation=rotation;go.GetComponent<Renderer>().sharedMaterial=material;Object.Destroy(go.GetComponent<Collider>());}
        private static Material CreateMaterial(Color color,bool emission){var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));material.color=color;if(emission){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",color*4f);}return material;}
        private void Update(){if(exploded)return;var p=transform.position;p.z+=speed*Time.deltaTime;if(!pathLocked&&player!=null){lockedTargetX=Mathf.SmoothDamp(lockedTargetX,player.position.x,ref trackingVelocity,.65f,3.5f);if(p.z>=-4f)pathLocked=true;}float desiredX=lockedTargetX+Mathf.Sin(p.z*.28f)*.5f;float previousX=p.x;p.x=Mathf.SmoothDamp(p.x,desiredX,ref lateralVelocity,.18f,7f);transform.position=p;float lateral=(p.x-previousX)/Mathf.Max(.001f,Time.deltaTime);float yaw=Mathf.Atan2(lateral,speed)*Mathf.Rad2Deg;float lean=Mathf.Clamp(-lateral*2f,-13f,13f);transform.rotation=Quaternion.Slerp(transform.rotation,Quaternion.Euler(0,yaw,lean),1f-Mathf.Exp(-8f*Time.deltaTime));UpdateCameraLifetime();}
        private void FixedUpdate(){if(!exploded||!compensateMapScroll||explodedBody==null||game==null)return;float current=game.CurrentSpeed;float delta=current-compensatedMapSpeed;if(Mathf.Abs(delta)>.001f){var velocity=explodedBody.linearVelocity;velocity.z-=delta;explodedBody.linearVelocity=velocity;compensatedMapSpeed=current;}}
        private void UpdateCameraLifetime()
        {
            if(IsInsideCameraView()){hasBeenInsideCamera=true;return;}if(hasBeenInsideCamera)Destroy(gameObject);
        }
        private bool IsInsideCameraView()
        {
            if(trackingCamera==null)trackingCamera=Camera.main;if(trackingCamera==null||visualRenderers==null||visualRenderers.Length==0)return false;int first=-1;for(int i=0;i<visualRenderers.Length;i++)if(visualRenderers[i]!=null){first=i;break;}if(first<0)return false;var bounds=visualRenderers[first].bounds;for(int i=first+1;i<visualRenderers.Length;i++)if(visualRenderers[i]!=null)bounds.Encapsulate(visualRenderers[i].bounds);GeometryUtility.CalculateFrustumPlanes(trackingCamera,frustumPlanes);return GeometryUtility.TestPlanesAABB(frustumPlanes,bounds);
        }
        private void OnTriggerEnter(Collider other)
        {
            if(exploded)return;if(other.GetComponentInParent<EmergencyVehicleController>()!=null){hasBeenInsideCamera=true;game.Crash(transform.position-other.transform.position);StartCoroutine(Explode(true));return;}if(other.GetComponentInParent<RoadHazard>()!=null)StartCoroutine(Explode(false));
        }
        private IEnumerator Explode(bool preserveAfterPlayerHit)
        {
            exploded=true;var physicsCollider=GetComponent<BoxCollider>();foreach(var c in GetComponentsInChildren<Collider>())if(c!=physicsCollider)c.enabled=false;if(physicsCollider!=null){physicsCollider.enabled=true;physicsCollider.isTrigger=false;physicsCollider.size=new Vector3(.9f,1.15f,1.9f);physicsCollider.center=new Vector3(0,.58f,0);}var body=GetComponent<Rigidbody>();if(body!=null){body.isKinematic=false;body.useGravity=true;body.mass=280f;body.linearDamping=1.15f;body.angularDamping=2.4f;body.maxAngularVelocity=5f;body.centerOfMass=new Vector3(0,.32f,0);body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.interpolation=RigidbodyInterpolation.Interpolate;compensateMapScroll=!preserveAfterPlayerHit&&game!=null;compensatedMapSpeed=compensateMapScroll?game.CurrentSpeed:0f;body.linearVelocity=new Vector3(Random.Range(-1.25f,1.25f),3.6f,6.5f-compensatedMapSpeed);body.angularVelocity=new Vector3(Random.Range(2.2f,4.2f),Random.Range(-2.2f,2.2f),Random.Range(-4.2f,4.2f));explodedBody=body;}
            EmergencyImpactVfx.Attach(transform,new Vector3(0,.7f,0));
            var psGo=new GameObject("Motor Impact Burst",typeof(ParticleSystem));psGo.transform.position=transform.position;var ps=psGo.GetComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.duration=.45f;main.loop=false;main.startLifetime=new ParticleSystem.MinMaxCurve(.55f,1.05f);main.startSpeed=new ParticleSystem.MinMaxCurve(5f,9f);main.startSize=new ParticleSystem.MinMaxCurve(.25f,.55f);main.startColor=new ParticleSystem.MinMaxGradient(new Color(1f,.08f,.01f),new Color(1f,.85f,.08f));main.maxParticles=36;var emission=ps.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,26)});var vfxMaterial=Resources.Load<Material>("EmergencyRoadVFX");if(vfxMaterial!=null)psGo.GetComponent<ParticleSystemRenderer>().sharedMaterial=vfxMaterial;ps.Play();var flash=new GameObject("Motor Explosion Flash",typeof(Light));flash.transform.position=transform.position;var light=flash.GetComponent<Light>();light.type=LightType.Point;light.range=9f;light.intensity=5f;light.color=new Color(1f,.28f,.03f);Destroy(flash,.18f);Destroy(psGo,1.5f);
            if(preserveAfterPlayerHit)yield break;
            yield return null;float neverSeenSafety=0;while(this!=null){bool inside=IsInsideCameraView();if(inside)hasBeenInsideCamera=true;if(hasBeenInsideCamera&&!inside){Destroy(gameObject);yield break;}if(!hasBeenInsideCamera){neverSeenSafety+=Time.deltaTime;if(neverSeenSafety>10f){Destroy(gameObject);yield break;}}yield return null;}
        }
    }

    public static class EmergencyImpactVfx
    {
        public static void Attach(Transform target,Vector3 localPosition)
        {
            if(target==null)return;var root=new GameObject("Khói và Lửa Va Chạm").transform;root.SetParent(target,false);root.localPosition=localPosition;
            Create(root,"Lửa",new Color(1f,.18f,.015f,1f),new Color(1f,.82f,.06f,.75f),.2f,.55f,.42f,18f,1.25f);
            Create(root,"Khói",new Color(.12f,.12f,.13f,.68f),new Color(.34f,.34f,.36f,.08f),.65f,1.45f,.7f,11f,.72f);
        }
        private static void Create(Transform parent,string name,Color start,Color end,float minimumLifetime,float maximumLifetime,float size,float rate,float speed)
        {
            var go=new GameObject(name,typeof(ParticleSystem));go.transform.SetParent(parent,false);var ps=go.GetComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var main=ps.main;main.loop=true;main.startLifetime=new ParticleSystem.MinMaxCurve(minimumLifetime,maximumLifetime);main.startSpeed=new ParticleSystem.MinMaxCurve(speed*.65f,speed*1.25f);main.startSize=new ParticleSystem.MinMaxCurve(size*.65f,size*1.25f);main.startColor=new ParticleSystem.MinMaxGradient(start,end);main.maxParticles=72;main.simulationSpace=ParticleSystemSimulationSpace.World;var emission=ps.emission;emission.rateOverTime=rate;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Cone;shape.angle=18f;shape.radius=.18f;go.transform.localRotation=Quaternion.Euler(-90,0,0);var color=ps.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(1,0),new GradientAlphaKey(0,1)});color.color=gradient;var noise=ps.noise;noise.enabled=true;noise.strength=.22f;noise.frequency=.55f;noise.scrollSpeed=.35f;var material=Resources.Load<Material>("EmergencyRoadVFX");if(material!=null)go.GetComponent<ParticleSystemRenderer>().sharedMaterial=material;ps.Play();
        }
    }
}
