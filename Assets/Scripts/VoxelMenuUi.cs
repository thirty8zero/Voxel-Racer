using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Small runtime UI factory shared by the two lightweight menu scenes.</summary>
    public static class VoxelMenuUi
    {
        public static RectTransform CreateCanvas(Transform parent, string name)
        {
            EnsureEventSystem();
            var canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(parent, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            // CanvasScaler enables before these runtime values are assigned. Toggling it
            // forces the reference resolution to be applied on the first visible frame.
            scaler.enabled = false;
            scaler.enabled = true;
            return canvasObject.GetComponent<RectTransform>();
        }

        public static Text CreateText(Transform parent, string name, string value, int fontSize,
            TextAnchor alignment, Vector2 anchor, Vector2 position, Vector2 size)
        {
            var textObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(parent, false);
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Text text = textObject.GetComponent<Text>();
            text.font = VoxelHudStyles.HudFont;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.text = value;
            text.raycastTarget = false;
            return text;
        }

        public static Button CreateButton(Transform parent, string name, string label, int fontSize,
            Vector2 anchor, Vector2 position, Vector2 size, UnityAction clicked)
        {
            var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = buttonObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = buttonObject.GetComponent<Image>();
            image.color = new Color(0.14f, 0.18f, 0.27f, 0.96f);
            Button button = buttonObject.GetComponent<Button>();
            var colours = button.colors;
            colours.normalColor = Color.white;
            colours.highlightedColor = new Color(1f, 0.66f, 0.20f, 1f);
            colours.pressedColor = new Color(0.88f, 0.36f, 0.08f, 1f);
            colours.selectedColor = colours.highlightedColor;
            button.colors = colours;
            button.onClick.AddListener(clicked);
            CreateText(buttonObject.transform, "Label", label, fontSize, TextAnchor.MiddleCenter,
                new Vector2(0.5f, 0.5f), Vector2.zero, size);
            return button;
        }

        public static Image CreatePanel(Transform parent, string name, Vector2 anchor,
            Vector2 position, Vector2 size)
        {
            var panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            panelObject.transform.SetParent(parent, false);
            RectTransform rect = panelObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = panelObject.GetComponent<Image>();
            image.color = new Color(0.035f, 0.045f, 0.075f, 0.88f);
            return image;
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindFirstObjectByType<EventSystem>() != null)
                return;
            var eventSystemObject = new GameObject("Menu Event System", typeof(EventSystem));
            InputSystemUIInputModule inputModule = eventSystemObject.AddComponent<InputSystemUIInputModule>();
            inputModule.AssignDefaultActions();
        }
    }
}
