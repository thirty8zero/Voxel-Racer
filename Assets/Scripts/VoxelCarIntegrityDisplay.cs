using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Top-left radial integrity HUD for the player's damageable car voxels.</summary>
    public sealed class VoxelCarIntegrityDisplay : MonoBehaviour
    {
        public VoxelCarController target;

        private CanvasGroup canvasGroup;
        private Image healthRing;
        private Text integrityLabelText;
        private Text percentageText;
        private Font voxelFont;
        private Font glitchGoblinFont;
        private Texture2D ringTexture;

        private void Awake()
        {
            BuildHud();
        }

        private void Update()
        {
            if (canvasGroup == null)
                return;

            if (!Application.isPlaying || target == null || VoxelPlayerDeathScreen.IsShowing)
            {
                canvasGroup.alpha = 0f;
                return;
            }

            float alpha = VoxelStartCountdown.CurrentGameplayHudAlpha;
            canvasGroup.alpha = alpha;
            if (alpha <= 0f)
                return;

            float integrity = Mathf.Clamp01(target.IntegrityPercent / 100f);
            healthRing.fillAmount = integrity;
            healthRing.color = Color.Lerp(new Color(0.95f, 0.12f, 0.08f),
                new Color(0.16f, 0.92f, 0.28f), integrity);

            Font activeFont = integrity <= 0.5f && glitchGoblinFont != null ? glitchGoblinFont : voxelFont;
            integrityLabelText.font = voxelFont;
            percentageText.font = activeFont;
            percentageText.text = Mathf.CeilToInt(target.IntegrityPercent) + "%";
        }

        private void BuildHud()
        {
            voxelFont = Resources.Load<Font>("Fonts/PixelifySans");
            if (voxelFont == null)
                voxelFont = VoxelHudStyles.HudFont;
            glitchGoblinFont = Resources.Load<Font>("Fonts/GlitchGoblin");

            RectTransform canvas = VoxelMenuUi.CreateCanvas(transform, "Player Integrity HUD");
            canvas.GetComponent<Canvas>().sortingOrder = 100;
            canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            Sprite ringSprite = CreateRingSprite();
            Image backgroundRing = CreateRingImage(canvas, ringSprite);
            backgroundRing.name = "Integrity Ring Background";
            backgroundRing.fillAmount = 1f;
            backgroundRing.color = new Color(0.02f, 0.025f, 0.04f, 0.58f);

            healthRing = CreateRingImage(canvas, ringSprite);
            healthRing.name = "Integrity Ring";
            healthRing.color = new Color(0.16f, 0.92f, 0.28f);

            percentageText = VoxelMenuUi.CreateText(canvas, "Integrity Percentage", string.Empty, 110,
                TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(155f, -140f), new Vector2(190f, 100f));
            percentageText.resizeTextForBestFit = true;
            percentageText.resizeTextMinSize = 12;
            percentageText.resizeTextMaxSize = 110;
            percentageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            percentageText.verticalOverflow = VerticalWrapMode.Truncate;

            integrityLabelText = VoxelMenuUi.CreateText(canvas, "Integrity Label", "INTEGRITY", 30,
                TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(155f, -205f), new Vector2(190f, 42f));
            integrityLabelText.resizeTextForBestFit = true;
            integrityLabelText.resizeTextMinSize = 12;
            integrityLabelText.resizeTextMaxSize = 30;
        }

        private static Image CreateRingImage(Transform parent, Sprite sprite)
        {
            var ringObject = new GameObject("Radial Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ringObject.transform.SetParent(parent, false);
            RectTransform rect = ringObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(155f, -165f);
            rect.sizeDelta = new Vector2(280f, 280f);

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
            const float innerRadius = 0.76f;
            ringTexture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
            {
                name = "Runtime Integrity Ring",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            float radius = textureSize * 0.5f;
            for (int y = 0; y < textureSize; y++)
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f),
                    new Vector2(radius, radius)) / radius;
                float alpha = distance >= innerRadius && distance <= 1f ? 1f : 0f;
                ringTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            ringTexture.Apply();
            return Sprite.Create(ringTexture, new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f), textureSize);
        }

        private void OnDestroy()
        {
            if (ringTexture != null)
                Destroy(ringTexture);
        }
    }
}
