using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TMPro;

namespace EmergencyRoad
{
    public static class EmergencyRoadUI
    {
        public static readonly Color Navy = new(0.035f, .06f, .12f, .96f);
        public static readonly Color Cyan = new(.05f, .85f, 1f, 1f);
        public static readonly Color Yellow = new(1f, .75f, .08f, 1f);
        private static TMP_FontAsset configuredFont;
        private static Sprite panelSprite,buttonSprite,sliderBackgroundSprite,sliderFillSprite,sliderHandleSprite;
        public static TMP_FontAsset Font => configuredFont;

        public static void SetFont(TMP_FontAsset font)
        {
            configuredFont = font;
        }

        public static void Configure(EmergencyRoadCatalog catalog)
        {
            if(catalog==null)return;SetFont(catalog.uiFont);panelSprite=catalog.panelSprite;buttonSprite=catalog.buttonSprite;sliderBackgroundSprite=catalog.sliderBackgroundSprite;sliderFillSprite=catalog.sliderFillSprite;sliderHandleSprite=catalog.sliderHandleSprite;
        }

        public static Canvas Canvas(string name)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = .5f;
            if (UnityEngine.Object.FindFirstObjectByType<EventSystem>() == null)
            {
                var eventGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                UnityEngine.Object.DontDestroyOnLoad(eventGo);
            }
            return canvas;
        }

        public static RectTransform Panel(Transform parent, string name, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            var image=go.GetComponent<Image>();image.color = color;image.sprite=panelSprite;if(panelSprite!=null)image.type=Image.Type.Sliced;
            return rt;
        }

        public static TMP_Text Label(Transform parent, string text, int size, Color color, TextAnchor align, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
        {
            var go = new GameObject("Text TMP", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            var label = go.GetComponent<TextMeshProUGUI>();
            label.font = Font; label.text = text; label.fontSize = size; label.color = color; label.alignment = ToTmpAlignment(align);
            label.enableAutoSizing = true; label.fontSizeMin = 12; label.fontSizeMax = size;
            return label;
        }

        private static TextAlignmentOptions ToTmpAlignment(TextAnchor anchor)=>anchor switch{TextAnchor.MiddleLeft=>TextAlignmentOptions.MidlineLeft,TextAnchor.MiddleRight=>TextAlignmentOptions.MidlineRight,TextAnchor.UpperLeft=>TextAlignmentOptions.TopLeft,TextAnchor.UpperCenter=>TextAlignmentOptions.Top,TextAnchor.UpperRight=>TextAlignmentOptions.TopRight,TextAnchor.LowerLeft=>TextAlignmentOptions.BottomLeft,TextAnchor.LowerCenter=>TextAlignmentOptions.Bottom,TextAnchor.LowerRight=>TextAlignmentOptions.BottomRight,_=>TextAlignmentOptions.Center};

        public static Button Button(Transform parent, string text, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax, Action action)
        {
            var rt = Panel(parent, text + " Button", color, anchorMin, anchorMax, offsetMin, offsetMax);
            var button = rt.gameObject.AddComponent<Button>();
            var buttonImage=rt.GetComponent<Image>();if(buttonSprite!=null){buttonImage.sprite=buttonSprite;buttonImage.type=Image.Type.Sliced;}
            var colors = button.colors; colors.highlightedColor = Color.Lerp(color, Color.white, .2f); colors.pressedColor = Color.Lerp(color, Color.black, .2f); button.colors = colors;
            Label(rt, text, 34, color.grayscale > .45f ? Navy : Color.white, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            button.onClick.AddListener(() => { EmergencyRoadAudio.Instance?.Click(); action?.Invoke(); });
            return button;
        }

        public static Slider Slider(Transform parent, Vector2 anchorMin, Vector2 anchorMax, float value, Action<float> changed)
        {
            var root = Panel(parent, "Slider", new Color(.1f,.16f,.24f,1f), anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            var rootImage=root.GetComponent<Image>();if(sliderBackgroundSprite!=null){rootImage.sprite=sliderBackgroundSprite;rootImage.type=Image.Type.Sliced;}
            var slider = root.gameObject.AddComponent<Slider>();
            var fillArea = Panel(root, "Fill Area", Color.clear, new Vector2(0,.2f), new Vector2(1,.8f), new Vector2(8,0), new Vector2(-8,0));
            var fill = Panel(fillArea, "Fill", Cyan, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            if(sliderFillSprite!=null){var image=fill.GetComponent<Image>();image.sprite=sliderFillSprite;image.type=Image.Type.Sliced;}
            var handleArea = Panel(root, "Handle Slide Area", Color.clear, Vector2.zero, Vector2.one, new Vector2(10,0), new Vector2(-10,0));
            var handle = Panel(handleArea, "Handle", Color.white, new Vector2(0,.5f), new Vector2(0,.5f), new Vector2(-16,-16), new Vector2(16,16));
            if(sliderHandleSprite!=null)handle.GetComponent<Image>().sprite=sliderHandleSprite;
            slider.fillRect = fill; slider.handleRect = handle; slider.targetGraphic = handle.GetComponent<Image>(); slider.value = value;
            slider.onValueChanged.AddListener(v => changed?.Invoke(v));
            return slider;
        }
    }
}
