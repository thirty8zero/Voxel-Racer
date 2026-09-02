using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Upper-right radial mission timer with colour-coded remaining time.</summary>
    public sealed class VoxelMissionTimerDisplay : MonoBehaviour
    {
        private VoxelMissionProgress mission;
        private CanvasGroup canvasGroup;
        private Image backgroundRing;
        private Image timerRing;
        private Text timerText;
        private Texture2D ringTexture;
        private Font voxelFont;

        public void Configure(VoxelMissionProgress progress) => mission = progress;

        private void Awake()
        {
            BuildHud();
        }

        private void Update()
        {
            if (canvasGroup == null)
                return;

            if (!Application.isPlaying || mission == null || mission.Tuning == null || VoxelPlayerDeathScreen.IsShowing)
            {
                canvasGroup.alpha = 0f;
                return;
            }

            float hudAlpha = VoxelStartCountdown.CurrentGameplayHudAlpha;
            canvasGroup.alpha = hudAlpha;
            if (hudAlpha <= 0f)
                return;

            float remainingPercent = Mathf.Clamp01(mission.RemainingTime / mission.Tuning.timeLimitSeconds);
            timerRing.fillAmount = remainingPercent;
            timerRing.color = GetTimerColour(remainingPercent);

            float dialScale = mission.RemainingTime > 0f && mission.RemainingTime <= 10f
                ? 1.08f + Mathf.Sin(Time.unscaledTime * 14f) * 0.1f
                : 1f;

            bool timeIsUp = mission.RemainingTime <= 0f;
            SetDialScale(dialScale, timeIsUp);
            timerText.fontSize = timeIsUp ? 44 : 76;
            timerText.resizeTextMaxSize = timeIsUp ? 44 : 76;
            int remainingSeconds = Mathf.CeilToInt(mission.RemainingTime);
            timerText.text = timeIsUp
                ? "TIME'S\nUP!"
                : remainingSeconds / 60 + ":" + (remainingSeconds % 60).ToString("00");
        }

        private void BuildHud()
        {
            voxelFont = Resources.Load<Font>("Fonts/IMPACTED");
            if (voxelFont == null)
                voxelFont = VoxelHudStyles.HudFont;

            RectTransform canvas = VoxelMenuUi.CreateCanvas(transform, "Mission Timer HUD");
            canvas.GetComponent<Canvas>().sortingOrder = 100;
            canvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f;

            Sprite ringSprite = CreateRingSprite();
            backgroundRing = CreateRingImage(canvas, ringSprite);
            backgroundRing.name = "Timer Ring Background";
            backgroundRing.fillAmount = 1f;
            backgroundRing.color = new Color(0.02f, 0.025f, 0.04f, 0.58f);

            timerRing = CreateRingImage(canvas, ringSprite);
            timerRing.name = "Timer Ring";

            timerText = VoxelMenuUi.CreateText(canvas, "Timer Text", string.Empty, 76,
                TextAnchor.MiddleCenter, new Vector2(1f, 1f), new Vector2(-155f, -165f), new Vector2(195f, 110f));
            timerText.font = voxelFont;
            timerText.resizeTextForBestFit = true;
            timerText.resizeTextMinSize = 12;
            timerText.resizeTextMaxSize = 76;
        }

        private static Color GetTimerColour(float remainingPercent)
        {
            Color green = new Color(0.16f, 0.92f, 0.28f);
            Color yellow = new Color(1f, 0.78f, 0.05f);
            Color red = new Color(0.95f, 0.12f, 0.08f);
            if (remainingPercent > 0.5f)
                return Color.Lerp(yellow, green, (remainingPercent - 0.5f) * 2f);
            if (remainingPercent > 0.25f)
                return Color.Lerp(red, yellow, (remainingPercent - 0.25f) * 4f);
            return red;
        }

        private void SetDialScale(float scale, bool timeIsUp)
        {
            Vector3 dialScale = Vector3.one * scale;
            backgroundRing.transform.localScale = dialScale;
            timerRing.transform.localScale = dialScale;
            // Keep the radial dial unchanged while making the two-line timeout
            // message exactly twice its normal rendered size.
            timerText.transform.localScale = dialScale * (timeIsUp ? 2f : 1f);
        }

        private static Image CreateRingImage(Transform parent, Sprite sprite)
        {
            var ringObject = new GameObject("Radial Timer Ring", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            ringObject.transform.SetParent(parent, false);
            RectTransform rect = ringObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-155f, -165f);
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
                name = "Runtime Mission Timer Ring",
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
