using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Temporary touch controls for lane changes and firing on mobile builds.</summary>
    public sealed class VoxelMobileControls : MonoBehaviour
    {
        private VoxelCarController target;
        private CanvasGroup canvasGroup;
        private Texture2D fireButtonTexture;
        private static bool fireHeld;

        /// <summary>Read by all mounted guns in addition to the keyboard Ctrl binding.</summary>
        public static bool IsFireHeld => fireHeld;

        public void Configure(VoxelCarController controller) => target = controller;

        private void Awake() => BuildHud();

        private void OnDisable() => fireHeld = false;

        private void Update()
        {
            if (canvasGroup == null)
                return;

            bool hidden = !Application.isPlaying || target == null || target.IsDestroyed ||
                VoxelPlayerDeathScreen.IsShowing || VoxelMissionProgress.Active?.IsComplete == true;
            canvasGroup.alpha = hidden ? 0f : VoxelStartCountdown.CurrentGameplayHudAlpha;
            canvasGroup.blocksRaycasts = canvasGroup.alpha > 0.01f;
            if (hidden)
                fireHeld = false;
        }

        internal void ChangeLane(int direction)
        {
            target?.RequestLaneChangeDirection(direction);
        }

        internal void SetFireHeld(bool value)
        {
            fireHeld = value && target != null && !target.IsDestroyed &&
                VoxelMissionProgress.Active?.IsComplete != true;
        }

        private void BuildHud()
        {
            RectTransform canvas = VoxelMenuUi.CreateCanvas(transform, "Temporary Mobile Controls");
            canvas.GetComponent<Canvas>().sortingOrder = 105;
            canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            // 200px targets are deliberately larger than the recommended 44dp touch area.
            // Their positions leave a 90px inset and a 70px gap beneath the Fire control.
            CreateControl(canvas, "Move Left Button", "◀", new Vector2(0f, 0f),
                new Vector2(150f, 150f), new Vector2(200f, 200f), MobileControlAction.Left,
                new Color(0.05f, 0.08f, 0.13f, 0.72f), 104);
            CreateControl(canvas, "Move Right Button", "▶", new Vector2(1f, 0f),
                new Vector2(-150f, 150f), new Vector2(200f, 200f), MobileControlAction.Right,
                new Color(0.05f, 0.08f, 0.13f, 0.72f), 104);
            CreateControl(canvas, "Fire Button", "FIRE", new Vector2(1f, 0f),
                new Vector2(-150f, 390f), new Vector2(200f, 200f), MobileControlAction.Fire,
                new Color(0.60f, 0.13f, 0.05f, 0.78f), 52, CreateFireButtonSprite());
        }

        private void CreateControl(Transform parent, string name, string label, Vector2 anchor,
            Vector2 position, Vector2 size, MobileControlAction action, Color colour, int fontSize,
            Sprite backgroundSprite = null)
        {
            var controlObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image),
                typeof(VoxelMobileControlButton));
            controlObject.transform.SetParent(parent, false);
            RectTransform rect = controlObject.GetComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            Image image = controlObject.GetComponent<Image>();
            image.color = colour;
            image.sprite = backgroundSprite;

            var input = controlObject.GetComponent<VoxelMobileControlButton>();
            input.Configure(this, action);
            Text text = VoxelMenuUi.CreateText(controlObject.transform, "Label", label, fontSize,
                TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f), Vector2.zero, size);
            text.font = Resources.Load<Font>("Fonts/IMPACTED") ?? VoxelHudStyles.HudFont;
            text.fontStyle = FontStyle.Normal;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        private Sprite CreateFireButtonSprite()
        {
            const int textureSize = 128;
            fireButtonTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = "Runtime Fire Button Disc",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float radius = textureSize * 0.5f;
            for (int y = 0; y < textureSize; y++)
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                fireButtonTexture.SetPixel(x, y, new Color(1f, 1f, 1f, distance <= 1f ? 1f : 0f));
            }
            fireButtonTexture.Apply();
            return Sprite.Create(fireButtonTexture, new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f), textureSize);
        }

        private void OnDestroy()
        {
            if (fireButtonTexture != null)
                Destroy(fireButtonTexture);
        }
    }

    internal enum MobileControlAction { Left, Right, Fire }

    /// <summary>Pointer-down handling lets FIRE remain active for the duration of a touch.</summary>
    internal sealed class VoxelMobileControlButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private VoxelMobileControls owner;
        private MobileControlAction action;

        public void Configure(VoxelMobileControls controls, MobileControlAction controlAction)
        {
            owner = controls;
            action = controlAction;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (action == MobileControlAction.Fire)
                owner?.SetFireHeld(true);
            else
                owner?.ChangeLane(action == MobileControlAction.Left ? -1 : 1);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (action == MobileControlAction.Fire)
                owner?.SetFireHeld(false);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (action == MobileControlAction.Fire)
                owner?.SetFireHeld(false);
        }
    }
}
