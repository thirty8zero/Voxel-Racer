using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Top-left radial integrity HUD for the player's damageable car voxels.</summary>
    public sealed class VoxelCarIntegrityDisplay : MonoBehaviour
    {
        public static VoxelCarIntegrityDisplay Active { get; private set; }
        public VoxelCarController target;

        private CanvasGroup canvasGroup;
        private Image backgroundRing;
        private Image healthRing;
        private Text integrityLabelText;
        private Text percentageText;
        private Font voxelFont;
        private Font glitchGoblinFont;
        private Texture2D ringTexture;
        private Sprite ringSprite;
        private Texture2D dialTexture;
        private Sprite dialSprite;
        private float damagePulseStartedAt = -1f;
        private const float DamagePulseDuration = 0.28f;

        private void OnEnable() => Active = this;

        private void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        /// <summary>Brief red radial flash used whenever the player actually loses voxels.</summary>
        public void PulseDamage() => damagePulseStartedAt = Time.unscaledTime;

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

            RefreshIntegrity();
        }

        private void RefreshIntegrity()
        {
            float integrity = Mathf.Clamp01(target.IntegrityPercent / 100f);
            healthRing.fillAmount = integrity;
            Color normalRingColour = Color.Lerp(new Color(0.95f, 0.12f, 0.08f),
                new Color(0.16f, 0.92f, 0.28f), integrity);
            float pulseProgress = damagePulseStartedAt < 0f ? 1f :
                Mathf.Clamp01((Time.unscaledTime - damagePulseStartedAt) / DamagePulseDuration);
            float pulse = damagePulseStartedAt < 0f || pulseProgress >= 1f
                ? 0f
                : Mathf.Sin(pulseProgress * Mathf.PI);
            healthRing.color = Color.Lerp(normalRingColour, new Color(1f, 0.05f, 0.03f), pulse);
            Vector3 radialScale = Vector3.one * (1f + pulse * 0.11f);
            healthRing.transform.localScale = radialScale;
            backgroundRing.transform.localScale = radialScale;

            Font activeFont = integrity <= 0.5f && glitchGoblinFont != null ? glitchGoblinFont : voxelFont;
            integrityLabelText.font = voxelFont;
            percentageText.font = activeFont;
            percentageText.text = Mathf.CeilToInt(target.IntegrityPercent) + "%";
        }

        private void BuildHud()
        {
            voxelFont = Resources.Load<Font>("Fonts/VCR_OSD_MONO_1.001");
            if (voxelFont == null)
                voxelFont = VoxelHudStyles.HudFont;
            glitchGoblinFont = Resources.Load<Font>("Fonts/GlitchGoblin");

            RectTransform canvas = VoxelMenuUi.CreateCanvas(transform, "Player Integrity HUD");
            canvas.GetComponent<Canvas>().sortingOrder = 110;
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.matchWidthOrHeight = 1f; scaler.enabled = false; scaler.enabled = true;
            canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            var disc = new GameObject("Integrity Dial Backplate", typeof(RectTransform), typeof(Image));
            disc.transform.SetParent(canvas, false);
            var dr = (RectTransform)disc.transform;
            dr.anchorMin = dr.anchorMax = new Vector2(0, 1);
            dr.anchoredPosition = new Vector2(155, -165); dr.sizeDelta = new Vector2(304, 304);
            dialTexture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            for (int y = 0; y < 256; y++) for (int x = 0; x < 256; x++)
            {
                float r = Vector2.Distance(new Vector2(x + .5f, y + .5f), new Vector2(128,128)) / 128;
                Color c = r > .975f ? new Color(.30f,.38f,.40f,.8f) : new Color(.015f,.027f,.031f,.95f);
                if (r > 1) c.a = 0;
                dialTexture.SetPixel(x,y,c);
            }
            dialTexture.Apply(); dialTexture.wrapMode = TextureWrapMode.Clamp;
            dialSprite = Sprite.Create(dialTexture, new Rect(0,0,256,256), new Vector2(.5f,.5f));
            disc.GetComponent<Image>().sprite = dialSprite; disc.GetComponent<Image>().raycastTarget = false;
            ringSprite = CreateRingSprite();
            backgroundRing = CreateRingImage(canvas, ringSprite);
            backgroundRing.name = "Integrity Ring Background";
            backgroundRing.fillAmount = 1f;
            backgroundRing.color = new Color(0.025f, 0.15f, 0.075f, 0.9f);

            healthRing = CreateRingImage(canvas, ringSprite);
            healthRing.name = "Integrity Ring";
            healthRing.color = new Color(0.16f, 0.92f, 0.28f);

            percentageText = VoxelMenuUi.CreateText(canvas, "Integrity Percentage", string.Empty, 110,
                TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(155f, -140f), new Vector2(190f, 100f));
            percentageText.resizeTextForBestFit = true;
            percentageText.resizeTextMinSize = 12;
            percentageText.resizeTextMaxSize = 110;
            percentageText.font = voxelFont;
            percentageText.text = "100%";
            percentageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            percentageText.verticalOverflow = VerticalWrapMode.Truncate;

            integrityLabelText = VoxelMenuUi.CreateText(canvas, "Integrity Label", "INTEGRITY", 30,
                TextAnchor.MiddleCenter, new Vector2(0f, 1f), new Vector2(155f, -205f), new Vector2(190f, 42f));
            integrityLabelText.resizeTextForBestFit = true;
            integrityLabelText.resizeTextMinSize = 12;
            integrityLabelText.resizeTextMaxSize = 30;
            integrityLabelText.font = voxelFont;
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
            const int textureSize = 512;
            const float innerRadius = 0.74f;
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
                float angle = Mathf.Atan2(y + .5f - radius, x + .5f - radius) * Mathf.Rad2Deg + 180;
                float sector = angle % 15f;
                float alpha = distance >= innerRadius && distance <= .98f && sector > 1.0f && sector < 14f ? 1f : 0f;
                float shade = distance > .91f ? .65f : 1f;
                ringTexture.SetPixel(x, y, new Color(shade, shade, shade, alpha));
            }
            ringTexture.Apply();
            return Sprite.Create(ringTexture, new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f), textureSize);
        }

        private void OnDestroy()
        {
            if (ringTexture != null)
                Destroy(ringTexture);
            if (ringSprite != null) Destroy(ringSprite);
            if (dialTexture != null) Destroy(dialTexture);
            if (dialSprite != null) Destroy(dialSprite);
        }
    }
}
