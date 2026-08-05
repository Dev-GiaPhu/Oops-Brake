#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor.Callbacks;
using TMPro;

namespace EmergencyRoad.Editor
{
    [InitializeOnLoad]
    public static class EmergencyRoadProjectBuilder
    {
        private const string Root = "Assets/EmergencyRoad";
        private const string CatalogPath = Root + "/Resources/EmergencyRoadCatalog.asset";
        private const string MenuPath = "Assets/Scenes/Menu.unity";
        private const string GrassMaterialPath = Root + "/Resources/GrassGround.mat";
        private const string SoilMaterialPath = Root + "/Resources/SoilGround.mat";
        private const string VolumeProfilePath = Root + "/Resources/EmergencyRoadPostFX.asset";
        private const string VfxMaterialPath = Root + "/Resources/EmergencyRoadVFX.mat";
        private const string GameplaySettingsPath = Root + "/Resources/EmergencyRoadGameplaySettings.asset";
        private const string UiFontPath = Root + "/Fonts/Baloo2-Variable.ttf";
        private const string TmpFontPath = Root + "/Fonts/Baloo2-Vietnamese-TMP.asset";
        private const string CoinPrefabPath = Root + "/Prefabs/Coin.prefab";
        private const string MotorcyclePrefabPath = Root + "/Prefabs/Motorcycle.prefab";

        static EmergencyRoadProjectBuilder()
        {
            EditorApplication.delayCall += AutoBuild;
        }

        [DidReloadScripts]
        private static void RebuildAfterScriptsReload(){EditorApplication.delayCall+=AutoBuild;}

        private static void AutoBuild()
        {
            if (EditorApplication.isCompiling) { EditorApplication.delayCall += AutoBuild; return; }
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if(!EnsureTmpEssentialResources()){EditorApplication.delayCall+=AutoBuild;return;}
            bool authoredScenesMissing = !File.Exists(MenuPath) || !File.ReadAllText(MenuPath).Contains("Authoring Version 18 - Scene Components TMP") ||
                                         !File.Exists("Assets/Scenes/Game.unity") || !File.ReadAllText("Assets/Scenes/Game.unity").Contains("Authoring Version 18 - Scene Components TMP");
            if (authoredScenesMissing || AssetDatabase.LoadAssetAtPath<EmergencyRoadCatalog>(CatalogPath) == null || AssetDatabase.LoadAssetAtPath<EmergencyRoadGameplaySettings>(GameplaySettingsPath)==null) BuildGame();
        }

        private static bool EnsureTmpEssentialResources()
        {
            if(AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset")!=null)return true;string packageRoot=Directory.GetDirectories("Library/PackageCache","com.unity.ugui@*").FirstOrDefault();string package=packageRoot==null?string.Empty:Path.GetFullPath(Path.Combine(packageRoot,"Package Resources","TMP Essential Resources.unitypackage"));if(!File.Exists(package)){Debug.LogError("[Emergency Road] Không tìm thấy TMP Essential Resources.");return false;}AssetDatabase.ImportPackage(package,false);AssetDatabase.Refresh();return AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset")!=null;
        }

        [MenuItem("Tools/Emergency Road/Build Game")]
        public static void BuildGame()
        {
            if(!EnsureTmpEssentialResources()){Debug.LogError("[Emergency Road] Chưa thể cài TMP Essential Resources.");return;}
            Directory.CreateDirectory(Root + "/Resources");
            Directory.CreateDirectory("Assets/Scenes");
            BuildCatalog();
            BuildScenes();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Emergency Road] Game catalog and Menu/Game build scenes are ready.");
        }

        private static void BuildCatalog()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<EmergencyRoadCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<EmergencyRoadCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }

            var gameplay=AssetDatabase.LoadAssetAtPath<EmergencyRoadGameplaySettings>(GameplaySettingsPath);if(gameplay==null){gameplay=ScriptableObject.CreateInstance<EmergencyRoadGameplaySettings>();AssetDatabase.CreateAsset(gameplay,GameplaySettingsPath);}if(gameplay.minStraightChunksBetweenIntersections<6)gameplay.minStraightChunksBetweenIntersections=10;if(gameplay.maxStraightChunksBetweenIntersections<gameplay.minStraightChunksBetweenIntersections)gameplay.maxStraightChunksBetweenIntersections=14;if(gameplay.intersectionClearanceChunks<1)gameplay.intersectionClearanceChunks=1;catalog.gameplaySettings=gameplay;EditorUtility.SetDirty(gameplay);
            catalog.playerVehicles = LoadPrefabs("Assets/Low Poly Simple Urban City 3D Asset Pack/Prefabs/Vehicles/Emergency_Vehicles")
                .OrderBy(x => VehicleOrder(x.name)).ToList();
            EnsureVehicleMeshesReadable(catalog.playerVehicles);
            catalog.trafficVehicles = LoadPrefabs("Assets/Low Poly Simple Urban City 3D Asset Pack/Prefabs/Vehicles/Cars")
                .Concat(LoadPrefabs("Assets/Low Poly Simple Urban City 3D Asset Pack/Prefabs/Vehicles/Pickups")).ToList();

            string roadFolder = "Assets/Low Poly Simple Urban City 3D Asset Pack/Prefabs/Roads";
            catalog.roadPrefabs = LoadPrefabs(roadFolder).Where(x => x.name == "Road_1").ToList();
            catalog.crossroadPrefabs = LoadPrefabs(roadFolder).Where(x => x.name.StartsWith("Road_Crossroads", StringComparison.Ordinal)).ToList();
            catalog.crossroadMetrics=catalog.crossroadPrefabs.Select(prefab=>{var bounds=MeasurePrefab(prefab);Debug.Log($"[Emergency Road Exact Bounds] {prefab.name} size={bounds.size} center={bounds.center}");return new EmergencyRoadPrefabMetrics{prefab=prefab,rendererSize=bounds.size,rendererCenter=bounds.center};}).ToList();

            string props = "Assets/Low Poly Simple Urban City 3D Asset Pack/Prefabs/Props";
            catalog.obstaclePrefabs = LoadPrefabs(props).Where(x => x.name.Contains("Barrier") || x.name.Contains("Cone") || x.name.StartsWith("Box_") || x.name.Contains("Trash_Big")).ToList();
            catalog.decorationPrefabs = LoadPrefabs("Assets/Low Poly Simple Urban City 3D Asset Pack/Prefabs/Buildings").Take(12).ToList();
            catalog.naturePrefabs = LoadPrefabs(props).Where(x => x.name == "Tree_1" || x.name == "Tree_2").ToList();
            catalog.streetDecorationPrefabs = LoadPrefabs(props).Where(x => x.name == "Light" || x.name == "Hydrant" || x.name == "Fence" || x.name.StartsWith("Road_Sign_")).ToList();
            catalog.grassMaterial = CreateLitMaterial(GrassMaterialPath, new Color(.32f,.62f,.2f), .02f, .1f);
            catalog.soilMaterial = CreateLitMaterial(SoilMaterialPath, new Color(.52f,.4f,.22f), 0f, .08f);
            catalog.vfxParticleMaterial = CreateVfxMaterial();
            catalog.postProcessProfile = CreatePostFxProfile();
            catalog.uiFont = CreateTmpFontAsset();
            CreateDefaultGameplayPrefabs(catalog);
            if(catalog.roadPrefabs.Count>0){var bounds=MeasurePrefab(catalog.roadPrefabs[0]);catalog.roadLength=Mathf.Max(1f,bounds.size.z-.04f);catalog.roadHalfWidth=Mathf.Max(1f,bounds.extents.x);catalog.laneWidth=catalog.roadHalfWidth*.47f;}
            EditorUtility.SetDirty(catalog);
        }

        private static Bounds MeasurePrefab(GameObject prefab)
        {
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab);instance.hideFlags=HideFlags.HideAndDontSave;instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var renderers=instance.GetComponentsInChildren<Renderer>();var bounds=renderers.Length>0?renderers[0].bounds:new Bounds(Vector3.zero,new Vector3(11,1,28));foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);UnityEngine.Object.DestroyImmediate(instance);return bounds;
        }

        private static TMP_FontAsset CreateTmpFontAsset()
        {
            var asset=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);if(asset!=null&&AssetDatabase.LoadAllAssetsAtPath(TmpFontPath).Length>=3)return asset;if(asset!=null)AssetDatabase.DeleteAsset(TmpFontPath);var source=AssetDatabase.LoadAssetAtPath<Font>(UiFontPath);if(source==null)return null;asset=TMP_FontAsset.CreateFontAsset(source,90,9,UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA,1024,1024,AtlasPopulationMode.Dynamic,true);asset.name="Baloo 2 Vietnamese TMP";AssetDatabase.CreateAsset(asset,TmpFontPath);if(asset.material!=null&&!AssetDatabase.Contains(asset.material)){asset.material.name="Baloo 2 Vietnamese Material";AssetDatabase.AddObjectToAsset(asset.material,asset);}foreach(var texture in asset.atlasTextures)if(texture!=null&&!AssetDatabase.Contains(texture)){texture.name="Baloo 2 Vietnamese Atlas";AssetDatabase.AddObjectToAsset(texture,asset);}EditorUtility.SetDirty(asset);AssetDatabase.SaveAssets();return asset;
        }

        private static void CreateDefaultGameplayPrefabs(EmergencyRoadCatalog catalog)
        {
            Directory.CreateDirectory(Root+"/Prefabs");catalog.coinPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(CoinPrefabPath);if(catalog.coinPrefab==null){var root=new GameObject("Coin");var coin=GameObject.CreatePrimitive(PrimitiveType.Cylinder);coin.transform.SetParent(root.transform,false);coin.transform.localRotation=Quaternion.Euler(90,0,0);coin.transform.localScale=new Vector3(.48f,.13f,.48f);coin.GetComponent<Renderer>().sharedMaterial=CreateLitMaterial(Root+"/Resources/Coin.mat",new Color(1f,.72f,.03f),.25f,.7f);catalog.coinPrefab=PrefabUtility.SaveAsPrefabAsset(root,CoinPrefabPath);UnityEngine.Object.DestroyImmediate(root);}
            catalog.motorcyclePrefab=AssetDatabase.LoadAssetAtPath<GameObject>(MotorcyclePrefabPath);if(catalog.motorcyclePrefab==null){var root=new GameObject("Motorcycle");var red=CreateLitMaterial(Root+"/Resources/MotorcycleBody.mat",new Color(.9f,.07f,.025f),.12f,.45f);var dark=CreateLitMaterial(Root+"/Resources/MotorcycleDark.mat",new Color(.025f,.03f,.04f),.05f,.3f);PrefabPrimitive(root.transform,"Thân",PrimitiveType.Cube,new Vector3(0,.52f,0),new Vector3(.75f,.38f,1.45f),Quaternion.identity,red);PrefabPrimitive(root.transform,"Bình xăng",PrimitiveType.Sphere,new Vector3(0,.78f,.15f),new Vector3(.65f,.5f,.72f),Quaternion.identity,red);PrefabPrimitive(root.transform,"Bánh trước",PrimitiveType.Cylinder,new Vector3(0,.32f,.88f),new Vector3(.48f,.18f,.48f),Quaternion.Euler(0,0,90),dark);PrefabPrimitive(root.transform,"Bánh sau",PrimitiveType.Cylinder,new Vector3(0,.32f,-.88f),new Vector3(.48f,.18f,.48f),Quaternion.Euler(0,0,90),dark);PrefabPrimitive(root.transform,"Người lái",PrimitiveType.Capsule,new Vector3(0,1.08f,-.08f),new Vector3(.5f,.72f,.5f),Quaternion.Euler(14,0,0),dark);catalog.motorcyclePrefab=PrefabUtility.SaveAsPrefabAsset(root,MotorcyclePrefabPath);UnityEngine.Object.DestroyImmediate(root);}
        }

        private static void PrefabPrimitive(Transform parent,string name,PrimitiveType type,Vector3 position,Vector3 scale,Quaternion rotation,Material material)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.transform.localRotation=rotation;go.GetComponent<Renderer>().sharedMaterial=material;UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
        }

        private static void EnsureVehicleMeshesReadable(IEnumerable<GameObject> prefabs)
        {
            var modelPaths=new HashSet<string>();foreach(var prefab in prefabs){string prefabPath=AssetDatabase.GetAssetPath(prefab);foreach(string dependency in AssetDatabase.GetDependencies(prefabPath,true))if(AssetImporter.GetAtPath(dependency) is ModelImporter)modelPaths.Add(dependency);}foreach(string path in modelPaths)if(AssetImporter.GetAtPath(path) is ModelImporter importer&&!importer.isReadable){importer.isReadable=true;importer.SaveAndReimport();}
        }

        private static Material CreateLitMaterial(string path,Color color,float metallic,float smoothness)
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
            material.color=color;material.SetFloat("_Metallic",metallic);material.SetFloat("_Smoothness",smoothness);EditorUtility.SetDirty(material);return material;
        }

        private static Material CreateVfxMaterial()
        {
            var material=AssetDatabase.LoadAssetAtPath<Material>(VfxMaterialPath);if(material==null){var shader=Shader.Find("Universal Render Pipeline/Particles/Unlit");if(shader==null)shader=Shader.Find("Universal Render Pipeline/Unlit");material=new Material(shader);AssetDatabase.CreateAsset(material,VfxMaterialPath);}material.color=Color.white;EditorUtility.SetDirty(material);return material;
        }

        private static VolumeProfile CreatePostFxProfile()
        {
            var profile=AssetDatabase.LoadAssetAtPath<VolumeProfile>(VolumeProfilePath);
            if(profile==null){profile=ScriptableObject.CreateInstance<VolumeProfile>();AssetDatabase.CreateAsset(profile,VolumeProfilePath);}
            var bloom=GetOrAdd<Bloom>(profile);bloom.active=true;bloom.intensity.Override(.28f);bloom.threshold.Override(1.05f);bloom.scatter.Override(.55f);bloom.highQualityFiltering.Override(true);
            var grading=GetOrAdd<ColorAdjustments>(profile);grading.active=true;grading.postExposure.Override(.32f);grading.contrast.Override(4f);grading.saturation.Override(5f);grading.colorFilter.Override(new Color(1f,.99f,.96f));
            var vignette=GetOrAdd<Vignette>(profile);vignette.active=true;vignette.intensity.Override(.12f);vignette.smoothness.Override(.55f);
            var tone=GetOrAdd<Tonemapping>(profile);tone.active=true;tone.mode.Override(TonemappingMode.ACES);
            var balance=GetOrAdd<WhiteBalance>(profile);balance.active=true;balance.temperature.Override(4f);balance.tint.Override(2f);
            EditorUtility.SetDirty(profile);return profile;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T:VolumeComponent
        {
            if(profile.TryGet<T>(out var component))return component;return profile.Add<T>(true);
        }

        private static int VehicleOrder(string name)
        {
            string[] order = { "Ambulance", "Police", "Fire_Truck", "Taxi", "Gas_Truck", "Bulldozer", "Excavator" };
            int index = Array.IndexOf(order, name); return index < 0 ? 100 : index;
        }

        private static IEnumerable<GameObject> LoadPrefabs(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return Enumerable.Empty<GameObject>();
            return AssetDatabase.FindAssets("t:Prefab", new[] { folder }).Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>).Where(x => x != null);
        }

        private static void BuildScenes()
        {
            var previous = SceneManager.GetActiveScene().path;
            string gamePath = "Assets/Scenes/Game.unity";
            ComposeMenuScene(AssetDatabase.LoadAssetAtPath<EmergencyRoadCatalog>(CatalogPath));
            ComposeGameScene(AssetDatabase.LoadAssetAtPath<EmergencyRoadCatalog>(CatalogPath));

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuPath, true),
                new EditorBuildSettingsScene(gamePath, true)
            };
            string reopen = previous == gamePath ? gamePath : MenuPath;
            EditorSceneManager.OpenScene(reopen);
        }

        private static void ComposeMenuScene(EmergencyRoadCatalog catalog)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("MENU SCENE AUTHORING");
            new GameObject("Authoring Version 18 - Scene Components TMP").transform.SetParent(root.transform);
            var preview = new GameObject("Preview Root (visible in Edit Mode)").transform;
            preview.SetParent(root.transform);
            CreateCamera(preview, "Garage Camera", new Vector3(8, 5.2f, -9), new Vector3(17,-35,0));
            CreateLight(preview, "Garage Key Light", new Vector3(45,-35,0));
            CreatePostFx(preview,catalog);
            var podium = GameObject.CreatePrimitive(PrimitiveType.Cylinder); podium.name="Garage Podium"; podium.transform.SetParent(preview); podium.transform.position=new Vector3(3.4f,-.25f,0); podium.transform.localScale=new Vector3(3.5f,.15f,3.5f);
            if (catalog.playerVehicles.Count > 0)
            {
                var car=(GameObject)PrefabUtility.InstantiatePrefab(catalog.playerVehicles[0],scene); car.name="Selected Vehicle Preview (Ambulance)"; car.transform.SetParent(preview); car.transform.position=new Vector3(3.4f,.2f,0);
            }
            CreateCanvasPreview(preview,"Main Menu Canvas",new[]{"Top Bar - BIỆT ĐỘI KHẨN CẤP","Nhà Xe Panel","Tên Xe và Giá","Xe Trước Button","Xe Sau Button","Mở Khóa hoặc Chọn Button","Chơi Button","Cài Đặt Button","Thoát Button","Cài Đặt Modal","Âm Nhạc Slider","Hiệu Ứng Slider","Va Chạm Bên Hông Toggle","Điều Khiển Modal"});
            root.AddComponent<EmergencyRoadSceneAuthoring>().Configure(EmergencyRoadSceneKind.Menu,catalog,preview);
            EditorSceneManager.SaveScene(scene,MenuPath);
        }

        private static void ComposeGameScene(EmergencyRoadCatalog catalog)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("GAME SCENE AUTHORING");
            new GameObject("Authoring Version 18 - Scene Components TMP").transform.SetParent(root.transform);
            var preview = new GameObject("Preview Root (visible in Edit Mode)").transform; preview.SetParent(root.transform);
            CreateCamera(preview,"Chase Camera",new Vector3(0,7.6f,-10.5f),new Vector3(22,0,0));
            CreateLight(preview,"Sun",Vector3.zero);
            CreatePostFx(preview,catalog);
            var world=new GameObject("Endless World Preview").transform;world.SetParent(preview);
            int previewChunkCount=Mathf.Clamp(Mathf.CeilToInt(150f/catalog.roadLength)+3,12,28);
            var previewCrossPrefab=catalog.crossroadPrefabs.Count>0?catalog.crossroadPrefabs[0]:null;var previewMetrics=previewCrossPrefab!=null?catalog.MetricsFor(previewCrossPrefab):null;int previewCrossRadius=previewMetrics!=null?Mathf.Max(1,Mathf.CeilToInt((previewMetrics.rendererSize.z/catalog.roadLength-1f)*.5f)):1;
            for(int i=0;i<previewChunkCount;i++)
            {
                bool cross=i==10||i==23;bool meshCovered=!cross&&(Mathf.Abs(i-10)<=previewCrossRadius||Mathf.Abs(i-23)<=previewCrossRadius);bool nearCross=cross||meshCovered;
                GameObject prefab=cross&&catalog.crossroadPrefabs.Count>0?catalog.crossroadPrefabs[0]:(!meshCovered&&catalog.roadPrefabs.Count>0?catalog.roadPrefabs[0]:null);
                var chunk=new GameObject(cross?$"Chunk {i:00} - Decorative Crossroad":meshCovered?$"Chunk {i:00} - Crossroad Footprint Spacer":$"Chunk {i:00} - Road_1 (3 Lanes)").transform;chunk.SetParent(world);chunk.localPosition=new Vector3(0,0,i*catalog.roadLength);
                if(prefab!=null){var road=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);road.name=prefab.name;road.transform.SetParent(chunk);road.transform.localPosition=cross&&previewMetrics!=null?new Vector3(-previewMetrics.rendererCenter.x,0,-previewMetrics.rendererCenter.z):Vector3.zero;road.transform.localRotation=Quaternion.identity;}
                if(cross)CreateCrossroadExtensions(scene,chunk,catalog);else if(!nearCross){CreateGroundStrip(chunk,-1,catalog);CreateGroundStrip(chunk,1,catalog);}
                if(!cross&&!nearCross&&catalog.decorationPrefabs.Count>0)
                {
                    for(int side=-1;side<=1;side+=2){var building=(GameObject)PrefabUtility.InstantiatePrefab(catalog.decorationPrefabs[(i+(side>0?1:0))%catalog.decorationPrefabs.Count],scene);building.name=$"City Building {(side<0?"Left":"Right")} - Fitted To Lot";building.transform.SetParent(chunk);building.transform.localPosition=Vector3.zero;building.transform.localRotation=Quaternion.Euler(0,side>0?180:0,0);EmergencyRoadGame.FitBuildingToLot(building,catalog.roadLength*.82f);EmergencyRoadGame.PositionOutsideRoad(building,side,catalog.roadHalfWidth,8.5f);CreateRoadsideDetails(scene,chunk,side,i,catalog);}
                }
            }
            var playerRoot=new GameObject("Player Vehicle Preview").transform;playerRoot.SetParent(preview);playerRoot.position=new Vector3(0,.55f,0);
            if(catalog.playerVehicles.Count>0){var car=(GameObject)PrefabUtility.InstantiatePrefab(catalog.playerVehicles[0],scene);car.name="Ambulance Visual";car.transform.SetParent(playerRoot);car.transform.localPosition=Vector3.zero;}
            var gameplay=new GameObject("Gameplay Systems (runtime controller)").transform;gameplay.SetParent(preview);
            new GameObject("Lane Input - A D").transform.SetParent(gameplay);
            new GameObject("Horn - Space").transform.SetParent(gameplay);
            new GameObject("Road Recycling and Spawners").transform.SetParent(gameplay);
            new GameObject("Cross Traffic Spawner").transform.SetParent(gameplay);
            CreateCanvasPreview(preview,"Game HUD Canvas",new[]{"Quãng Đường Text","Tiền Counter","Tạm Dừng Button","Gợi Ý Điều Khiển","Tạm Dừng Panel","Hết Lượt Panel","Chơi Lại Button","Về Nhà Xe Button"});
            root.AddComponent<EmergencyRoadSceneAuthoring>().Configure(EmergencyRoadSceneKind.Game,catalog,preview,catalog.laneWidth,catalog.roadLength);
            EditorSceneManager.SaveScene(scene,"Assets/Scenes/Game.unity");
        }

        private static void CreateCamera(Transform parent,string name,Vector3 position,Vector3 euler)
        {
            var go=new GameObject(name,typeof(Camera),typeof(AudioListener),typeof(UniversalAdditionalCameraData));go.transform.SetParent(parent);go.transform.position=position;go.transform.rotation=Quaternion.Euler(euler);go.tag="MainCamera";var data=go.GetComponent<UniversalAdditionalCameraData>();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.FastApproximateAntialiasing;go.GetComponent<Camera>().allowHDR=true;
        }

        private static void CreateLight(Transform parent,string name,Vector3 euler)
        {
            var go=new GameObject(name,typeof(Light));go.transform.SetParent(parent);go.transform.rotation=Quaternion.Euler(euler==Vector3.zero?new Vector3(42,-28,0):euler);var light=go.GetComponent<Light>();light.type=LightType.Directional;light.intensity=1.25f;
        }

        private static void CreatePostFx(Transform parent,EmergencyRoadCatalog catalog)
        {
            var go=new GameObject("Global Volume - Bloom ACES Color Grade",typeof(Volume));go.transform.SetParent(parent);var volume=go.GetComponent<Volume>();volume.isGlobal=true;volume.priority=10;volume.sharedProfile=catalog.postProcessProfile;
        }

        private static void CreateGroundStrip(Transform chunk,int side,EmergencyRoadCatalog catalog)
        {
            var group=new GameObject(side<0?"LEFT ROADSIDE - Grass Trees Lights":"RIGHT ROADSIDE - Grass Trees Lights").transform;group.SetParent(chunk);group.localPosition=Vector3.zero;
            var grass=GameObject.CreatePrimitive(PrimitiveType.Cube);grass.name="Continuous Grass Ground";grass.transform.SetParent(group);grass.transform.localPosition=new Vector3(side*(catalog.roadHalfWidth+10f),-.28f,0);grass.transform.localScale=new Vector3(18f,.36f,catalog.roadLength+.08f);if(catalog.grassMaterial!=null)grass.GetComponent<Renderer>().sharedMaterial=catalog.grassMaterial;
            var soil=GameObject.CreatePrimitive(PrimitiveType.Cube);soil.name="Narrow Soil Border";soil.transform.SetParent(group);soil.transform.localPosition=new Vector3(side*(catalog.roadHalfWidth+.4f),-.12f,0);soil.transform.localScale=new Vector3(.8f,.12f,catalog.roadLength+.08f);if(catalog.soilMaterial!=null)soil.GetComponent<Renderer>().sharedMaterial=catalog.soilMaterial;
        }

        private static void CreateCrossroadExtensions(Scene scene,Transform chunk,EmergencyRoadCatalog catalog)
        {
            if(catalog.roadPrefabs.Count==0)return;var group=new GameObject("FULL CROSS STREET - Exact Measured Edges").transform;group.SetParent(chunk);var crossPrefab=catalog.crossroadPrefabs.Count>0?catalog.crossroadPrefabs[0]:null;var metrics=catalog.MetricsFor(crossPrefab);float crossHalfWidth=metrics!=null?metrics.rendererSize.x*.5f:catalog.roadHalfWidth;float crossHalfLength=metrics!=null?metrics.rendererSize.z*.5f:catalog.roadLength*.5f;
            for(int side=-1;side<=1;side+=2)for(int i=0;i<6;i++){var road=(GameObject)PrefabUtility.InstantiatePrefab(catalog.roadPrefabs[0],scene);road.name=$"Cross Street {(side<0?"Left":"Right")} {i+1} - Exact Edge";road.transform.SetParent(group);road.transform.localRotation=Quaternion.Euler(0,90,0);road.transform.localPosition=new Vector3(side*(crossHalfWidth+.04f+(i+.5f)*catalog.roadLength),-.012f,0);}
            int radius=metrics!=null?Mathf.Max(1,Mathf.CeilToInt((metrics.rendererSize.z/catalog.roadLength-1f)*.5f)):1;float gap=Mathf.Max(0,(radius+.5f)*catalog.roadLength-crossHalfLength);if(gap>.03f)for(int side=-1;side<=1;side+=2){var connector=(GameObject)PrefabUtility.InstantiatePrefab(catalog.roadPrefabs[0],scene);connector.name=$"Main Road Connector {(side<0?"Back":"Forward")} - {gap:F2}m";connector.transform.SetParent(group);connector.transform.localPosition=new Vector3(0,-.006f,side*(crossHalfLength+gap*.5f));connector.transform.localScale=new Vector3(1,1,gap/catalog.roadLength);for(int xSide=-1;xSide<=1;xSide+=2){var grass=GameObject.CreatePrimitive(PrimitiveType.Cube);grass.name="Intersection Corner Grass - Exact Gap Fill";grass.transform.SetParent(group);grass.transform.localPosition=new Vector3(xSide*(catalog.roadHalfWidth+10f),-.28f,side*(crossHalfLength+gap*.5f));grass.transform.localScale=new Vector3(18f,.36f,gap+.04f);if(catalog.grassMaterial!=null)grass.GetComponent<Renderer>().sharedMaterial=catalog.grassMaterial;}}
            float innerZ=catalog.roadHalfWidth-.45f;float outerZ=(radius+.5f)*catalog.roadLength;float vergeDepth=outerZ-innerZ;float outerX=crossHalfWidth+6*catalog.roadLength;float vergeWidth=outerX-crossHalfWidth;for(int xSide=-1;xSide<=1;xSide+=2)for(int zSide=-1;zSide<=1;zSide+=2){var grass=GameObject.CreatePrimitive(PrimitiveType.Cube);grass.name="Cross Street Grass Verge - Overlaps Sidewalk";grass.transform.SetParent(group);grass.transform.localPosition=new Vector3(xSide*(crossHalfWidth+vergeWidth*.5f),-.28f,zSide*(innerZ+vergeDepth*.5f));grass.transform.localScale=new Vector3(vergeWidth+.08f,.36f,vergeDepth+.08f);if(catalog.grassMaterial!=null)grass.GetComponent<Renderer>().sharedMaterial=catalog.grassMaterial;}
            float cornerWidth=crossHalfWidth-catalog.roadHalfWidth;float cornerDepth=crossHalfLength-catalog.roadHalfWidth;for(int xSide=-1;xSide<=1;xSide+=2)for(int zSide=-1;zSide<=1;zSide+=2){var grass=GameObject.CreatePrimitive(PrimitiveType.Cube);grass.name="Crossroad Inner Corner Grass - Under Mesh Cutout";grass.transform.SetParent(group);grass.transform.localPosition=new Vector3(xSide*(catalog.roadHalfWidth+cornerWidth*.5f),-.28f,zSide*(catalog.roadHalfWidth+cornerDepth*.5f));grass.transform.localScale=new Vector3(cornerWidth+.06f,.36f,cornerDepth+.06f);if(catalog.grassMaterial!=null)grass.GetComponent<Renderer>().sharedMaterial=catalog.grassMaterial;}
            CreateCrossStreetScenery(scene,group,catalog,crossHalfWidth,6);
        }

        private static void CreateCrossStreetScenery(Scene scene,Transform group,EmergencyRoadCatalog catalog,float crossHalfWidth,int tiles)
        {
            var lamp=catalog.streetDecorationPrefabs.FirstOrDefault(x=>x!=null&&x.name=="Light");for(int xSide=-1;xSide<=1;xSide+=2)for(int zSide=-1;zSide<=1;zSide+=2)for(int i=0;i<tiles;i++){float x=xSide*(crossHalfWidth+(i+.5f)*catalog.roadLength);if(i%2==0&&catalog.decorationPrefabs.Count>0){var building=(GameObject)PrefabUtility.InstantiatePrefab(catalog.decorationPrefabs[(i+(xSide>0?1:0)+(zSide>0?2:0))%catalog.decorationPrefabs.Count],scene);building.name="Cross Street Building - Fitted Lot";building.transform.SetParent(group);building.transform.localPosition=new Vector3(x,0,0);building.transform.localRotation=Quaternion.Euler(0,zSide>0?90:-90,0);FitBranchBuilding(building,x,zSide,catalog);}else if(catalog.naturePrefabs.Count>0){var tree=(GameObject)PrefabUtility.InstantiatePrefab(catalog.naturePrefabs[i%catalog.naturePrefabs.Count],scene);tree.name="Cross Street Tree";tree.transform.SetParent(group);tree.transform.localPosition=new Vector3(x,0,zSide*(catalog.roadHalfWidth+3.2f));}if(lamp!=null&&(i==1||i==4)){var light=(GameObject)PrefabUtility.InstantiatePrefab(lamp,scene);light.name="Cross Street Light - Sidewalk";light.transform.SetParent(group);light.transform.localPosition=new Vector3(x,0,zSide*(catalog.roadHalfWidth-.9f));light.transform.localRotation=Quaternion.Euler(0,zSide>0?180:0,0);}}
        }
        private static void FitBranchBuilding(GameObject building,float targetX,int zSide,EmergencyRoadCatalog catalog)
        {
            var renderers=building.GetComponentsInChildren<Renderer>();if(renderers.Length==0)return;var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);float scale=Mathf.Min(1f,Mathf.Min(catalog.roadLength*1.55f/Mathf.Max(.1f,bounds.size.x),5.8f/Mathf.Max(.1f,bounds.size.z)));building.transform.localScale*=scale;bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);var local=building.transform.localPosition;local.x=targetX-(bounds.center.x-building.transform.position.x);local.z=zSide*(catalog.roadHalfWidth+.8f+bounds.extents.z)-(bounds.center.z-building.transform.position.z);local.y-=bounds.min.y;building.transform.localPosition=local;
        }

        private static void CreateRoadsideDetails(Scene scene,Transform chunk,int side,int sequence,EmergencyRoadCatalog catalog)
        {
            var group=chunk.Find(side<0?"LEFT ROADSIDE - Grass Trees Lights":"RIGHT ROADSIDE - Grass Trees Lights");if(group==null)return;
            for(int j=0;j<2&&catalog.naturePrefabs.Count>0;j++){var tree=(GameObject)PrefabUtility.InstantiatePrefab(catalog.naturePrefabs[(sequence+j)%catalog.naturePrefabs.Count],scene);tree.name=$"Tree {j+1} - Grass Zone";tree.transform.SetParent(group);tree.transform.localPosition=new Vector3(side*(catalog.roadHalfWidth+4.2f+j*2.6f),0,-catalog.roadLength*.3f+j*catalog.roadLength*.5f+(sequence%2)*.7f);tree.transform.localRotation=Quaternion.Euler(0,(sequence*53+j*71)%360,0);}
            var lampPrefab=catalog.streetDecorationPrefabs.FirstOrDefault(x=>x!=null&&x.name=="Light");if(sequence%3==0&&lampPrefab!=null){var lamp=(GameObject)PrefabUtility.InstantiatePrefab(lampPrefab,scene);lamp.name="Street Light - Sidewalk - Faces Road";lamp.transform.SetParent(group);lamp.transform.localPosition=new Vector3(side*(catalog.roadHalfWidth-.9f),0,-catalog.roadLength*.25f);lamp.transform.localRotation=Quaternion.Euler(0,side<0?90:-90,0);}
            if(sequence%4==1){var prop=catalog.streetDecorationPrefabs.FirstOrDefault(x=>x!=null&&x.name!="Light");if(prop!=null){var detail=(GameObject)PrefabUtility.InstantiatePrefab(prop,scene);detail.name="Sparse Sidewalk Detail";detail.transform.SetParent(group);detail.transform.localPosition=new Vector3(side*(catalog.roadHalfWidth-.65f),0,catalog.roadLength*.28f);detail.transform.localRotation=Quaternion.Euler(0,side<0?90:-90,0);}}
        }

        private static void CreateCanvasPreview(Transform parent,string name,IEnumerable<string> children)
        {
            var go=new GameObject(name,typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(parent);var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
            foreach(string childName in children)
            {
                var child=new GameObject(childName,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));child.transform.SetParent(go.transform,false);var rect=child.GetComponent<RectTransform>();GetPreviewRect(name,childName,out var min,out var max);rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=rect.offsetMax=Vector2.zero;var image=child.GetComponent<Image>();image.color=childName.Contains("Panel")||childName.Contains("Modal")||childName.Contains("Garage")?new Color(.025f,.065f,.12f,.9f):childName.Contains("Top Bar")?new Color(.02f,.05f,.1f,.96f):childName.Contains("Play")||childName.Contains("Unlock")?new Color(1f,.62f,.08f,.96f):new Color(.06f,.45f,.68f,.92f);image.raycastTarget=true;
                if(childName.Contains("Button")||childName.Contains("Toggle")){var button=child.AddComponent<Button>();button.targetGraphic=image;}
                if(childName.Contains("Slider")){var slider=child.AddComponent<Slider>();slider.targetGraphic=image;}
                var label=new GameObject("Nhãn TMP",typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));label.transform.SetParent(child.transform,false);var labelRect=label.GetComponent<RectTransform>();labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;var text=label.GetComponent<TextMeshProUGUI>();text.text=PreviewDisplayName(childName);text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(TmpFontPath);text.fontSize=20;text.alignment=TextAlignmentOptions.Center;text.color=Color.white;text.raycastTarget=false;
            }
        }

        private static string PreviewDisplayName(string name)
        {
            return name.Replace("Top Bar - ",string.Empty).Replace(" Button",string.Empty).Replace(" Panel",string.Empty).Replace(" Modal",string.Empty).Replace(" Slider",string.Empty).Replace(" Toggle",string.Empty).Replace(" Counter",string.Empty).Replace(" Text",string.Empty).ToUpperInvariant();
        }

        private static void GetPreviewRect(string canvas,string item,out Vector2 min,out Vector2 max)
        {
            min=new Vector2(.35f,.4f);max=new Vector2(.65f,.48f);
            if(canvas.Contains("Menu"))
            {
                if(item.Contains("Top Bar")){min=new(0,.86f);max=Vector2.one;}
                else if(item=="Nhà Xe Panel"){min=new(.05f,.08f);max=new(.42f,.8f);}
                else if(item.Contains("Tên Xe")){min=new(.09f,.57f);max=new(.38f,.69f);}
                else if(item.Contains("Xe Trước")){min=new(.08f,.35f);max=new(.16f,.47f);}
                else if(item.Contains("Xe Sau")){min=new(.31f,.35f);max=new(.39f,.47f);}
                else if(item.Contains("Mở Khóa")){min=new(.11f,.17f);max=new(.36f,.3f);}
                else if(item=="Chơi Button"){min=new(.67f,.55f);max=new(.94f,.69f);}
                else if(item=="Cài Đặt Button"){min=new(.67f,.37f);max=new(.94f,.5f);}
                else if(item=="Thoát Button"){min=new(.67f,.19f);max=new(.94f,.32f);}
                else if(item=="Cài Đặt Modal"){min=new(.27f,.16f);max=new(.73f,.82f);}
                else if(item.Contains("Âm Nhạc")){min=new(.39f,.57f);max=new(.65f,.62f);}
                else if(item.Contains("Hiệu Ứng")){min=new(.39f,.47f);max=new(.65f,.52f);}
                else if(item.Contains("Va Chạm Bên Hông")){min=new(.47f,.34f);max=new(.65f,.41f);}
                else if(item=="Điều Khiển Modal"){min=new(.3f,.2f);max=new(.7f,.76f);}
            }
            else
            {
                if(item.Contains("Quãng Đường")){min=new(.035f,.9f);max=new(.25f,.975f);}
                else if(item.Contains("Tiền")){min=new(.7f,.9f);max=new(.87f,.975f);}
                else if(item=="Tạm Dừng Button"){min=new(.89f,.89f);max=new(.965f,.975f);}
                else if(item.Contains("Gợi Ý")){min=new(.28f,.02f);max=new(.72f,.075f);}
                else if(item=="Tạm Dừng Panel"){min=new(.31f,.2f);max=new(.69f,.8f);}
                else if(item=="Hết Lượt Panel"){min=new(.045f,.18f);max=new(.39f,.82f);}
                else if(item=="Chơi Lại Button"){min=new(.095f,.33f);max=new(.34f,.43f);}
                else if(item=="Về Nhà Xe Button"){min=new(.095f,.2f);max=new(.34f,.3f);}
            }
        }
    }
}
#endif
