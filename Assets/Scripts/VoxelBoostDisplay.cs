using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Bottom-right round boost control with a draining/recharging red radial dial.</summary>
    public sealed class VoxelBoostDisplay : MonoBehaviour
    {
        private VoxelBoostController boost;
        private CanvasGroup canvasGroup;
        private Image boostRing;
        private Text boostText;
        private Texture2D ringTexture;
        private Texture2D discTexture;

        public void Configure(VoxelBoostController controller) => boost = controller;

        private void Awake() => BuildHud();

        private void Update()
        {
            if (canvasGroup == null)
                return;

            bool hide = !Application.isPlaying || boost == null || boost.Tuning == null ||
                VoxelPlayerDeathScreen.IsShowing || VoxelMissionProgress.Active?.IsComplete == true;
            canvasGroup.alpha = hide ? 0f : VoxelStartCountdown.CurrentGameplayHudAlpha;
            if (canvasGroup.alpha <= 0f)
                return;

            boostRing.fillAmount = boost.ChargePercent;
            boostText.color = new Color(0.93f, 0.08f, 0.07f, boost.IsReady || boost.IsBoosting ? 1f : 0.3f);
        }

        private void BuildHud()
        {
            RectTransform canvas = VoxelMenuUi.CreateCanvas(transform, "Boost HUD");
            canvas.GetComponent<Canvas>().sortingOrder = 100;
            canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            Sprite discSprite = CreateDiscSprite();
            var buttonObject = new GameObject("Boost Button", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(canvas, false);
            RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(1f, 0f);
            buttonRect.anchorMax = new Vector2(1f, 0f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            // Sit directly beneath the upper-right countdown dial rather than in the lower corner.
            buttonRect.anchoredPosition = new Vector2(-155f, -435f);
            buttonRect.sizeDelta = new Vector2(198f, 198f);
            Image buttonImage = buttonObject.GetComponent<Image>();
            buttonImage.sprite = discSprite;
            buttonImage.color = new Color(0.02f, 0.02f, 0.03f, 0.35f);
            Button button = buttonObject.GetComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => boost?.TryActivateBoost());

            boostRing = CreateRingImage(canvas, CreateRingSprite());
            boostRing.name = "Boost Charge Ring";
            boostRing.color = new Color(0.93f, 0.08f, 0.07f, 1f);

            // The 220px ring has a 78% inner radius. This 158px-wide safe area
            // maximises the label without letting IMPACTED touch the radial bar.
            boostText = VoxelMenuUi.CreateText(canvas, "Boost Label", "BOOST", 52, TextAnchor.MiddleCenter,
                new Vector2(1f, 1f), new Vector2(-155f, -435f), new Vector2(158f, 100f));
            boostText.font = Resources.Load<Font>("Fonts/IMPACTED") ?? VoxelHudStyles.HudFont;
            boostText.resizeTextForBestFit = true;
            boostText.resizeTextMinSize = 12;
            boostText.resizeTextMaxSize = 52;
            boostText.color = new Color(0.93f, 0.08f, 0.07f, 1f);
            boostText.raycastTarget = false;
        }

        private static Image CreateRingImage(Transform parent, Sprite sprite)
        {
            var ringObject = new GameObject("Boost Radial Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ringObject.transform.SetParent(parent, false);
            RectTransform rect = ringObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-155f, -435f);
            rect.sizeDelta = new Vector2(220f, 220f);
            Image image = ringObject.GetComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Filled;
            image.fillMethod = Image.FillMethod.Radial360;
            image.fillOrigin = (int)Image.Origin360.Top;
            image.fillClockwise = true;
            image.raycastTarget = false;
            return image;
        }

        private Sprite CreateRingSprite()
        {
            const int textureSize = 128;
            ringTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = "Runtime Boost Ring", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            float radius = textureSize * 0.5f;
            for (int y = 0; y < textureSize; y++)
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                ringTexture.SetPixel(x, y, new Color(1f, 1f, 1f, distance >= 0.78f && distance <= 1f ? 1f : 0f));
            }
            ringTexture.Apply();
            return Sprite.Create(ringTexture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
        }

        private Sprite CreateDiscSprite()
        {
            const int textureSize = 128;
            discTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = "Runtime Boost Button Disc", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp
            };
            float radius = textureSize * 0.5f;
            for (int y = 0; y < textureSize; y++)
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(radius, radius)) / radius;
                discTexture.SetPixel(x, y, new Color(1f, 1f, 1f, distance <= 1f ? 1f : 0f));
            }
            discTexture.Apply();
            return Sprite.Create(discTexture, new Rect(0f, 0f, textureSize, textureSize), new Vector2(0.5f, 0.5f), textureSize);
        }

        private void OnDestroy()
        {
            if (ringTexture != null) Destroy(ringTexture);
            if (discTexture != null) Destroy(discTexture);
        }
    }
}
