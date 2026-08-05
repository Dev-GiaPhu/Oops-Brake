using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace EmergencyRoad
{
    /// <summary>One shared layout factory: the editor authoring tool uses it; gameplay only binds to its scene objects.</summary>
    public static class EmergencyRoadSceneUIFactory
    {
        public static EmergencyRoadMenuView CreateMenu(Transform parent)
        {
            var canvas = CreateCanvas(parent, "Main Menu UI - SCENE AUTHORED");
            var view = canvas.gameObject.AddComponent<EmergencyRoadMenuView>();
            var top = EmergencyRoadUI.Panel(canvas.transform, "Top Bar", EmergencyRoadUI.Navy, new(0,.86f), Vector2.one, Vector2.zero, Vector2.zero);
            EmergencyRoadUI.Label(top, "BIỆT ĐỘI KHẨN CẤP", 64, Color.white, TextAnchor.MiddleLeft, new(.05f,0), new(.65f,1), Vector2.zero, Vector2.zero);
            view.wallet = EmergencyRoadUI.Label(top, "● 0", 38, EmergencyRoadUI.Yellow, TextAnchor.MiddleRight, new(.7f,0), new(.95f,1), Vector2.zero, Vector2.zero);

            var garage = EmergencyRoadUI.Panel(canvas.transform, "Garage", new(.02f,.035f,.075f,.88f), new(.05f,.08f), new(.42f,.8f), Vector2.zero, Vector2.zero);
            EmergencyRoadUI.Label(garage, "NHÀ XE", 40, EmergencyRoadUI.Cyan, TextAnchor.MiddleCenter, new(.1f,.82f), new(.9f,.96f), Vector2.zero, Vector2.zero);
            view.vehicleName = EmergencyRoadUI.Label(garage, "XE CỨU THƯƠNG", 44, Color.white, TextAnchor.MiddleCenter, new(.08f,.62f), new(.92f,.8f), Vector2.zero, Vector2.zero);
            view.price = EmergencyRoadUI.Label(garage, "ĐÃ MỞ • CÙNG HIỆU NĂNG", 28, EmergencyRoadUI.Yellow, TextAnchor.MiddleCenter, new(.08f,.51f), new(.92f,.63f), Vector2.zero, Vector2.zero);
            view.previous = EmergencyRoadUI.Button(garage, "‹", EmergencyRoadUI.Cyan, new(.08f,.34f), new(.27f,.48f), Vector2.zero, Vector2.zero, null);
            view.next = EmergencyRoadUI.Button(garage, "›", EmergencyRoadUI.Cyan, new(.73f,.34f), new(.92f,.48f), Vector2.zero, Vector2.zero, null);
            view.vehicleAction = EmergencyRoadUI.Button(garage, "CHỌN", EmergencyRoadUI.Yellow, new(.16f,.14f), new(.84f,.3f), Vector2.zero, Vector2.zero, null);

            var actions = EmergencyRoadUI.Panel(canvas.transform, "Actions", Color.clear, new(.66f,.13f), new(.95f,.68f), Vector2.zero, Vector2.zero);
            view.play = EmergencyRoadUI.Button(actions, "CHƠI", EmergencyRoadUI.Yellow, new(0,.66f), Vector2.one, Vector2.zero, Vector2.zero, null);
            view.settingsOpen = EmergencyRoadUI.Button(actions, "CÀI ĐẶT", new(.08f,.45f,.65f,1), new(0,.35f), new(1,.61f), Vector2.zero, Vector2.zero, null);
            view.quit = EmergencyRoadUI.Button(actions, "THOÁT", new(.75f,.16f,.2f,1), new(0,.04f), new(1,.3f), Vector2.zero, Vector2.zero, null);

            view.settingsPanel = EmergencyRoadUI.Panel(canvas.transform, "Settings Modal", EmergencyRoadUI.Navy, new(.28f,.2f), new(.72f,.8f), Vector2.zero, Vector2.zero).gameObject;
            var sp = view.settingsPanel.transform;
            EmergencyRoadUI.Label(sp, "CÀI ĐẶT", 52, EmergencyRoadUI.Cyan, TextAnchor.MiddleCenter, new(.1f,.8f), new(.9f,.96f), Vector2.zero, Vector2.zero);
            EmergencyRoadUI.Label(sp, "ÂM NHẠC", 28, Color.white, TextAnchor.MiddleLeft, new(.1f,.61f), new(.4f,.72f), Vector2.zero, Vector2.zero);
            view.music = EmergencyRoadUI.Slider(sp, new(.4f,.63f), new(.88f,.69f), 1, null);
            EmergencyRoadUI.Label(sp, "HIỆU ỨNG", 28, Color.white, TextAnchor.MiddleLeft, new(.1f,.45f), new(.4f,.56f), Vector2.zero, Vector2.zero);
            view.sfx = EmergencyRoadUI.Slider(sp, new(.4f,.47f), new(.88f,.53f), 1, null);
            EmergencyRoadUI.Label(sp, "VA CHẠM BÊN HÔNG", 25, Color.white, TextAnchor.MiddleLeft, new(.1f,.31f), new(.52f,.41f), Vector2.zero, Vector2.zero);
            view.sideCollision = EmergencyRoadUI.Button(sp, "BẬT", new(.08f,.45f,.65f,1), new(.52f,.32f), new(.88f,.41f), Vector2.zero, Vector2.zero, null);
            view.controlsOpen = EmergencyRoadUI.Button(sp, "ĐIỀU KHIỂN", new(.08f,.45f,.65f,1), new(.12f,.1f), new(.58f,.24f), Vector2.zero, Vector2.zero, null);
            view.settingsClose = EmergencyRoadUI.Button(sp, "ĐÓNG", new(.65f,.18f,.22f,1), new(.62f,.1f), new(.88f,.24f), Vector2.zero, Vector2.zero, null);

            view.controlsPanel = EmergencyRoadUI.Panel(canvas.transform, "Controls Modal", EmergencyRoadUI.Navy, new(.28f,.2f), new(.72f,.8f), Vector2.zero, Vector2.zero).gameObject;
            var cp = view.controlsPanel.transform;
            EmergencyRoadUI.Label(cp, "ĐIỀU KHIỂN", 52, EmergencyRoadUI.Cyan, TextAnchor.MiddleCenter, new(.1f,.8f), new(.9f,.96f), Vector2.zero, Vector2.zero);
            EmergencyRoadUI.Label(cp, "A / D\nCHUYỂN LÀN\n\nSPACE\nBÓP CÒI VUI NHỘN\n\nESC\nTẠM DỪNG", 34, Color.white, TextAnchor.MiddleCenter, new(.08f,.25f), new(.92f,.78f), Vector2.zero, Vector2.zero);
            view.controlsBack = EmergencyRoadUI.Button(cp, "QUAY LẠI", EmergencyRoadUI.Yellow, new(.28f,.08f), new(.72f,.2f), Vector2.zero, Vector2.zero, null);
            view.settingsPanel.SetActive(false); view.controlsPanel.SetActive(false);
            return view;
        }

        public static EmergencyRoadGameView CreateGame(Transform parent)
        {
            var canvas = CreateCanvas(parent, "Game HUD - SCENE AUTHORED");
            var view = canvas.gameObject.AddComponent<EmergencyRoadGameView>();
            var bar = EmergencyRoadUI.Panel(canvas.transform,"HUD Bar",EmergencyRoadUI.Navy,new(0,.88f),Vector2.one,Vector2.zero,Vector2.zero);
            view.score=EmergencyRoadUI.Label(bar,"0 m",38,Color.white,TextAnchor.MiddleLeft,new(.04f,0),new(.3f,1),Vector2.zero,Vector2.zero);
            view.coins=EmergencyRoadUI.Label(bar,"● 0",38,EmergencyRoadUI.Yellow,TextAnchor.MiddleCenter,new(.37f,0),new(.63f,1),Vector2.zero,Vector2.zero);
            view.pause=EmergencyRoadUI.Button(bar,"Ⅱ",new(.1f,.45f,.65f,1),new(.89f,.15f),new(.96f,.85f),Vector2.zero,Vector2.zero,null);
            EmergencyRoadUI.Label(canvas.transform,"A / D  CHUYỂN LÀN     SPACE  BÓP CÒI",23,new(1,1,1,.65f),TextAnchor.MiddleCenter,new(.28f,.02f),new(.72f,.07f),Vector2.zero,Vector2.zero);
            view.hazardAlert=EmergencyRoadUI.Label(canvas.transform,"",32,new(1f,.18f,.12f,1),TextAnchor.MiddleCenter,new(.3f,.76f),new(.7f,.84f),Vector2.zero,Vector2.zero); view.hazardAlert.fontStyle=FontStyles.Bold;
            view.pausePanel=Modal(canvas.transform,"TẠM DỪNG",out _);
            view.resume=EmergencyRoadUI.Button(view.pausePanel.transform,"TIẾP TỤC",EmergencyRoadUI.Yellow,new(.2f,.46f),new(.8f,.6f),Vector2.zero,Vector2.zero,null);
            view.restartFromPause=EmergencyRoadUI.Button(view.pausePanel.transform,"CHƠI LẠI",new(.08f,.45f,.65f,1),new(.2f,.29f),new(.8f,.43f),Vector2.zero,Vector2.zero,null);
            view.menuFromPause=EmergencyRoadUI.Button(view.pausePanel.transform,"VỀ MENU",new(.65f,.18f,.22f,1),new(.2f,.12f),new(.8f,.26f),Vector2.zero,Vector2.zero,null);
            view.gameOverPanel=Modal(canvas.transform,"HẾT LƯỢT!",out view.gameOverScore); var r=(RectTransform)view.gameOverPanel.transform;r.anchorMin=new(.045f,.18f);r.anchorMax=new(.39f,.82f);
            view.retry=EmergencyRoadUI.Button(view.gameOverPanel.transform,"CHƠI LẠI",EmergencyRoadUI.Yellow,new(.14f,.28f),new(.86f,.44f),Vector2.zero,Vector2.zero,null);
            view.garage=EmergencyRoadUI.Button(view.gameOverPanel.transform,"VỀ NHÀ XE",new(.08f,.45f,.65f,1),new(.14f,.1f),new(.86f,.25f),Vector2.zero,Vector2.zero,null);
            view.pausePanel.SetActive(false); view.gameOverPanel.SetActive(false);
            return view;
        }

        private static Canvas CreateCanvas(Transform parent,string name)
        {
            var go=new GameObject(name,typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));go.transform.SetParent(parent,false);
            var canvas=go.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=100;
            var scaler=go.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new(1920,1080);scaler.matchWidthOrHeight=.5f;
            return canvas;
        }

        private static GameObject Modal(Transform canvas,string title,out TMP_Text detail)
        {
            var panel=EmergencyRoadUI.Panel(canvas,title,EmergencyRoadUI.Navy,new(.32f,.2f),new(.68f,.8f),Vector2.zero,Vector2.zero).gameObject;
            EmergencyRoadUI.Label(panel.transform,title,58,EmergencyRoadUI.Cyan,TextAnchor.MiddleCenter,new(.08f,.72f),new(.92f,.94f),Vector2.zero,Vector2.zero);
            detail=EmergencyRoadUI.Label(panel.transform,"",30,Color.white,TextAnchor.MiddleCenter,new(.08f,.58f),new(.92f,.73f),Vector2.zero,Vector2.zero);return panel;
        }
    }
}
