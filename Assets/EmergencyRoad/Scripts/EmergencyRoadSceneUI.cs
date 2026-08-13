#if UNITY_EDITOR
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EmergencyRoad
{
    /// <summary>EDITOR-ONLY scene UI authoring factory. Runtime only uses serialized scene references.</summary>
    public static class EmergencyRoadSceneUIFactory
    {
        private const string CoinIconPath = "Assets/Sources/GUI-BlueSky/ResourcesData/Sprites/Components/Icon_PictoIcons/PictoIcon_64/icon_coin.png";

        public static EmergencyRoadMenuView CreateMenu(Transform parent)
        {
            var canvas = CreateCanvas(parent, "Main Menu UI - SCENE AUTHORED");
            var view = canvas.gameObject.AddComponent<EmergencyRoadMenuView>();
            var top = EmergencyRoadUI.Panel(canvas.transform, "Top Bar", EmergencyRoadUI.Navy, new(0,.86f), Vector2.one, Vector2.zero, Vector2.zero);
            EmergencyRoadUI.Label(top, "EMERGENCY SQUAD", 64, Color.white, TextAnchor.MiddleLeft, new(.05f,0), new(.65f,1), Vector2.zero, Vector2.zero);
            EmergencyRoadUI.Icon(top, "Coin Icon", AssetDatabase.LoadAssetAtPath<Sprite>(CoinIconPath), new(.71f,.23f), new(.76f,.77f));
            view.wallet = EmergencyRoadUI.Label(top, "0", 38, EmergencyRoadUI.Yellow, TextAnchor.MiddleRight, new(.77f,0), new(.95f,1), Vector2.zero, Vector2.zero);

            var garage = EmergencyRoadUI.Panel(canvas.transform, "Garage Selector Panel", new(.02f,.035f,.075f,.92f), new(.75f,.06f), new(.98f,.82f), Vector2.zero, Vector2.zero);
            view.garagePanel = garage.gameObject;
            EmergencyRoadUI.Label(garage, "GARAGE", 40, EmergencyRoadUI.Cyan, TextAnchor.MiddleCenter, new(.1f,.82f), new(.9f,.96f), Vector2.zero, Vector2.zero);
            view.vehicleName = EmergencyRoadUI.Label(garage, "AMBULANCE", 44, Color.white, TextAnchor.MiddleCenter, new(.08f,.62f), new(.92f,.8f), Vector2.zero, Vector2.zero);
            view.price = EmergencyRoadUI.Label(garage, "OWNED", 28, EmergencyRoadUI.Yellow, TextAnchor.MiddleCenter, new(.08f,.51f), new(.92f,.63f), Vector2.zero, Vector2.zero);
            view.previous = EmergencyRoadUI.Button(garage, "‹", EmergencyRoadUI.Cyan, new(.08f,.34f), new(.27f,.48f), Vector2.zero, Vector2.zero, null);
            view.next = EmergencyRoadUI.Button(garage, "›", EmergencyRoadUI.Cyan, new(.73f,.34f), new(.92f,.48f), Vector2.zero, Vector2.zero, null);
            view.vehicleAction = EmergencyRoadUI.Button(garage, "SELECT", EmergencyRoadUI.Yellow, new(.12f,.16f), new(.88f,.3f), Vector2.zero, Vector2.zero, null);
            view.vehicleActionLabel = view.vehicleAction.GetComponentInChildren<TMP_Text>(true);
            view.garageBack = EmergencyRoadUI.Button(garage, "BACK", new(.08f,.45f,.65f,1), new(.12f,.03f), new(.88f,.13f), Vector2.zero, Vector2.zero, null);

            var actions = EmergencyRoadUI.Panel(canvas.transform, "Main Menu - Right 25 Percent", new(.02f,.035f,.075f,.78f), new(.75f,.06f), new(.98f,.82f), Vector2.zero, Vector2.zero);
            view.mainMenuPanel = actions.gameObject;
            view.play = EmergencyRoadUI.Button(actions, "PLAY", EmergencyRoadUI.Yellow, new(.08f,.7f), new(.92f,.88f), Vector2.zero, Vector2.zero, null);
            view.selectVehicle = EmergencyRoadUI.Button(actions, "SELECT VEHICLE", EmergencyRoadUI.Cyan, new(.08f,.49f), new(.92f,.67f), Vector2.zero, Vector2.zero, null);
            view.settingsOpen = EmergencyRoadUI.Button(actions, "SETTINGS", new(.08f,.45f,.65f,1), new(.08f,.28f), new(.92f,.46f), Vector2.zero, Vector2.zero, null);
            view.quit = EmergencyRoadUI.Button(actions, "QUIT", new(.75f,.16f,.2f,1), new(.08f,.07f), new(.92f,.25f), Vector2.zero, Vector2.zero, null);

            view.settingsPanel = EmergencyRoadUI.Panel(canvas.transform, "Settings Modal", EmergencyRoadUI.Navy, new(.28f,.2f), new(.72f,.8f), Vector2.zero, Vector2.zero).gameObject;
            var sp = view.settingsPanel.transform;
            EmergencyRoadUI.Label(sp, "SETTINGS", 52, EmergencyRoadUI.Cyan, TextAnchor.MiddleCenter, new(.1f,.8f), new(.9f,.96f), Vector2.zero, Vector2.zero);
            EmergencyRoadUI.Label(sp, "MUSIC", 28, Color.white, TextAnchor.MiddleLeft, new(.1f,.61f), new(.4f,.72f), Vector2.zero, Vector2.zero);
            view.music = EmergencyRoadUI.Slider(sp, new(.4f,.63f), new(.88f,.69f), 1, null);
            EmergencyRoadUI.Label(sp, "SOUND EFFECTS", 28, Color.white, TextAnchor.MiddleLeft, new(.1f,.45f), new(.4f,.56f), Vector2.zero, Vector2.zero);
            view.sfx = EmergencyRoadUI.Slider(sp, new(.4f,.47f), new(.88f,.53f), 1, null);
            EmergencyRoadUI.Label(sp, "SIDE COLLISIONS", 25, Color.white, TextAnchor.MiddleLeft, new(.1f,.31f), new(.52f,.41f), Vector2.zero, Vector2.zero);
            view.sideCollision = EmergencyRoadUI.Button(sp, "ON", new(.08f,.45f,.65f,1), new(.52f,.32f), new(.88f,.41f), Vector2.zero, Vector2.zero, null);
            view.sideCollisionLabel = view.sideCollision.GetComponentInChildren<TMP_Text>(true);
            view.controlsOpen = EmergencyRoadUI.Button(sp, "CONTROLS", new(.08f,.45f,.65f,1), new(.12f,.1f), new(.58f,.24f), Vector2.zero, Vector2.zero, null);
            view.settingsClose = EmergencyRoadUI.Button(sp, "CLOSE", new(.65f,.18f,.22f,1), new(.62f,.1f), new(.88f,.24f), Vector2.zero, Vector2.zero, null);

            view.controlsPanel = EmergencyRoadUI.Panel(canvas.transform, "Controls Modal", EmergencyRoadUI.Navy, new(.28f,.2f), new(.72f,.8f), Vector2.zero, Vector2.zero).gameObject;
            var cp = view.controlsPanel.transform;
            EmergencyRoadUI.Label(cp, "CONTROLS", 52, EmergencyRoadUI.Cyan, TextAnchor.MiddleCenter, new(.1f,.8f), new(.9f,.96f), Vector2.zero, Vector2.zero);
            EmergencyRoadUI.Label(cp, "A / D\nCHANGE LANES\n\nSPACE\nHONK THE HORN\n\nESC\nPAUSE", 34, Color.white, TextAnchor.MiddleCenter, new(.08f,.25f), new(.92f,.78f), Vector2.zero, Vector2.zero);
            view.controlsBack = EmergencyRoadUI.Button(cp, "BACK", EmergencyRoadUI.Yellow, new(.28f,.08f), new(.72f,.2f), Vector2.zero, Vector2.zero, null);
            view.garagePanel.SetActive(false);
            view.settingsPanel.SetActive(false);
            view.controlsPanel.SetActive(false);
            return view;
        }

        public static EmergencyRoadGameView CreateGame(Transform parent)
        {
            var canvas = CreateCanvas(parent, "Game HUD - SCENE AUTHORED");
            var view = canvas.gameObject.AddComponent<EmergencyRoadGameView>();
            var bar = EmergencyRoadUI.Panel(canvas.transform,"HUD Bar",EmergencyRoadUI.Navy,new(0,.88f),Vector2.one,Vector2.zero,Vector2.zero);
            view.score = EmergencyRoadUI.Label(bar,"0 m",38,Color.white,TextAnchor.MiddleLeft,new(.04f,0),new(.3f,1),Vector2.zero,Vector2.zero);
            EmergencyRoadUI.Icon(bar, "Coin Icon", AssetDatabase.LoadAssetAtPath<Sprite>(CoinIconPath), new(.43f,.2f), new(.47f,.8f));
            view.coins = EmergencyRoadUI.Label(bar,"0",38,EmergencyRoadUI.Yellow,TextAnchor.MiddleLeft,new(.48f,0),new(.61f,1),Vector2.zero,Vector2.zero);
            view.pause = EmergencyRoadUI.Button(bar,"II",new(.1f,.45f,.65f,1),new(.89f,.15f),new(.96f,.85f),Vector2.zero,Vector2.zero,null);
            EmergencyRoadUI.Label(canvas.transform,"A / D  CHANGE LANES     SPACE  HONK",23,new(1,1,1,.65f),TextAnchor.MiddleCenter,new(.28f,.02f),new(.72f,.07f),Vector2.zero,Vector2.zero);
            view.hazardAlert = EmergencyRoadUI.Label(canvas.transform,"",32,new(1f,.18f,.12f,1),TextAnchor.MiddleCenter,new(.3f,.76f),new(.7f,.84f),Vector2.zero,Vector2.zero);
            view.hazardAlert.fontStyle = FontStyles.Bold;
            view.pausePanel = Modal(canvas.transform,"PAUSED",out _);
            view.resume = EmergencyRoadUI.Button(view.pausePanel.transform,"RESUME",EmergencyRoadUI.Yellow,new(.2f,.46f),new(.8f,.6f),Vector2.zero,Vector2.zero,null);
            view.restartFromPause = EmergencyRoadUI.Button(view.pausePanel.transform,"PLAY AGAIN",new(.08f,.45f,.65f,1),new(.2f,.29f),new(.8f,.43f),Vector2.zero,Vector2.zero,null);
            view.menuFromPause = EmergencyRoadUI.Button(view.pausePanel.transform,"BACK TO MENU",new(.65f,.18f,.22f,1),new(.2f,.12f),new(.8f,.26f),Vector2.zero,Vector2.zero,null);
            view.gameOverPanel = Modal(canvas.transform,"GAME OVER!",out view.gameOverScore);
            var r = (RectTransform)view.gameOverPanel.transform;
            r.anchorMin = new(.045f,.18f);
            r.anchorMax = new(.39f,.82f);
            view.retry = EmergencyRoadUI.Button(view.gameOverPanel.transform,"PLAY AGAIN",EmergencyRoadUI.Yellow,new(.14f,.28f),new(.86f,.44f),Vector2.zero,Vector2.zero,null);
            view.garage = EmergencyRoadUI.Button(view.gameOverPanel.transform,"BACK TO GARAGE",new(.08f,.45f,.65f,1),new(.14f,.1f),new(.86f,.25f),Vector2.zero,Vector2.zero,null);
            view.pausePanel.SetActive(false);
            view.gameOverPanel.SetActive(false);
            return view;
        }

        private static Canvas CreateCanvas(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new(1920,1080);
            scaler.matchWidthOrHeight = .5f;
            EmergencyRoadUI.EnsureEventSystem();
            return canvas;
        }

        private static GameObject Modal(Transform canvas, string title, out TMP_Text detail)
        {
            var panel = EmergencyRoadUI.Panel(canvas,title,EmergencyRoadUI.Navy,new(.32f,.2f),new(.68f,.8f),Vector2.zero,Vector2.zero).gameObject;
            EmergencyRoadUI.Label(panel.transform,title,58,EmergencyRoadUI.Cyan,TextAnchor.MiddleCenter,new(.08f,.72f),new(.92f,.94f),Vector2.zero,Vector2.zero);
            detail = EmergencyRoadUI.Label(panel.transform,"",30,Color.white,TextAnchor.MiddleCenter,new(.08f,.58f),new(.92f,.73f),Vector2.zero,Vector2.zero);
            return panel;
        }
    }
}
#endif
