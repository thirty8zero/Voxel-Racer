using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Shares the three-count and race gates between stationary and rolling mission starts.</summary>
    public sealed class VoxelStartCountdown : MonoBehaviour
    {
        internal const float Duration = 3f;
        private const string LaunchCue = "HUNT";
        private const float NumberSize = 144f, LaunchSize = 114f;
        public static VoxelStartCountdown Active { get; private set; }
        public VoxelCarController target;
        internal VoxelPlayerEngineAudio EngineAudio { get; set; }

        [Header("Race Opening Fade")]
        [Min(0f)] public float blackScreenDuration = 1f;
        [Min(0.01f)] public float fadeInDuration = 2f;

        private float openingStartedAt;
        private float countdownStartedAt;
        private bool started;
        private bool prepared;
        private bool waitingForLoadingScreen;
        private bool rollingOpening;
        private bool carrierSkipFade;
        private CanvasGroup openingFade;
        private string trackDisplayName;
        private bool isBossMission;
        private bool cueWarmed;
        private GUIStyle numberStyle;
        private GUIStyle trackStyle;

        public bool IsComplete => started && !carrierSkipFade && Time.time - countdownStartedAt >= Duration;
        internal bool IsCarrierSkipFadeActive => carrierSkipFade;

        public bool HasReachedFinalCount => started && Time.time - countdownStartedAt >= Duration - 1f;
        internal float CountdownElapsed => started ? Time.time - countdownStartedAt : 0f;
        internal bool IsRollingOpening => rollingOpening && !IsComplete;

        /// <summary>Keep the carrier opening empty through the countdown. Stationary starts prewarm traffic at 1.</summary>
        public bool IsTrafficSpawnWindowOpen => rollingOpening ? IsComplete : HasReachedFinalCount;

        /// <summary>Gameplay HUD fades in during the one-second launch cue.</summary>
        public float GameplayHudAlpha => !started
            ? 0f
            : Mathf.Clamp01(Time.time - countdownStartedAt - Duration);

        public static float CurrentGameplayHudAlpha => Active == null ? 1f : Active.GameplayHudAlpha;

        private void OnEnable() => Active = this;

        private void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        public void Prepare(VoxelCarController player, string trackName = null, bool? bossMission = null)
        {
            EndCarrierSkipFade();
            target = player;
            isBossMission = bossMission ?? (VoxelRacerBootstrap.ActiveTrack?.isBossLevel ?? false);
            trackDisplayName = isBossMission ? "Find and destroy the radar car, at all costs..." : string.Empty;
            // Rasterize the first visible glyphs while the loading screen still covers the race.
            Font font = VoxelHudStyles.HudFont;
            if (font != null && font.dynamic)
            {
                float scale = Screen.height / 1080f;
                font.RequestCharactersInTexture("321", ScaleFont(NumberSize, scale), FontStyle.Normal);
                font.RequestCharactersInTexture(LaunchCue, ScaleFont(LaunchSize, scale), FontStyle.Normal);
            }
            target.SetDrivingEnabled(false);
            prepared = true;
            started = false;
            rollingOpening = false;
            waitingForLoadingScreen = false;
            cueWarmed = false;
        }

        public void BeginCountdown()
        {
            if (target == null)
                return;

            EndCarrierSkipFade();
            target.SetDrivingEnabled(false);
            rollingOpening = false;
            EnsureOpeningFade();
            openingFade.gameObject.SetActive(true);
            openingFade.alpha = 1f;
            if (VoxelLoadingScreen.IsLoading)
            {
                started = false;
                waitingForLoadingScreen = true;
                return;
            }
            waitingForLoadingScreen = false;
            openingStartedAt = Time.time;
            countdownStartedAt = openingStartedAt + blackScreenDuration + fadeInDuration;
            started = true;
        }

        internal void PrepareRollingOpening() => rollingOpening = true;

        internal void BeginRollingCountdown()
        {
            waitingForLoadingScreen = false;
            countdownStartedAt = Time.time;
            started = true;
        }

        internal void SetCarrierSkipFade(float opacity)
        {
            EnsureOpeningFade();
            carrierSkipFade = true;
            openingFade.gameObject.SetActive(true);
            openingFade.alpha = Mathf.Clamp01(opacity);
        }

        internal void EndCarrierSkipFade()
        {
            if (!carrierSkipFade) return;
            carrierSkipFade = false;
            openingFade.alpha = 0f;
            openingFade.gameObject.SetActive(false);
        }

        private void Start()
        {
            if (target != null && !prepared)
                Prepare(target);
        }

        private void Update()
        {
            if (waitingForLoadingScreen && !VoxelLoadingScreen.IsLoading) BeginCountdown();
            if (target == null || !started)
                return;

            if (target.IsDestroyed)
            {
                HideForPlayerDeath();
                return;
            }

            UpdateOpeningFade();
            if (!rollingOpening && EngineAudio != null && !IsComplete)
            {
                float visibleFor = Time.time - openingStartedAt - blackScreenDuration;
                if (visibleFor >= 0f)
                    EngineAudio.StartIgnition();
                if (EngineAudio.IdleCueReached)
                    EngineAudio.BeginRoadSound();
            }
            if (IsComplete)
                target.SetDrivingEnabled(true);
        }

        private void EnsureOpeningFade()
        {
            if (openingFade != null)
                return;

            var canvasObject = new GameObject("Race Opening Fade", typeof(RectTransform),
                typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;

            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            var overlayObject = new GameObject("Black Screen", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(CanvasGroup));
            overlayObject.transform.SetParent(canvasObject.transform, false);
            RectTransform rect = overlayObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = overlayObject.GetComponent<Image>();
            image.color = Color.black;
            image.raycastTarget = true;
            openingFade = overlayObject.GetComponent<CanvasGroup>();
            openingFade.alpha = 1f;
        }

        private void UpdateOpeningFade()
        {
            if (carrierSkipFade) return;
            if (openingFade == null || !openingFade.gameObject.activeSelf)
                return;

            float elapsed = Time.time - openingStartedAt;
            if (elapsed <= blackScreenDuration)
            {
                openingFade.alpha = 1f;
                return;
            }

            float fadeElapsed = elapsed - blackScreenDuration;
            openingFade.alpha = 1f - Mathf.Clamp01(fadeElapsed / fadeInDuration);
            if (fadeElapsed >= fadeInDuration)
            {
                openingFade.alpha = 0f;
                openingFade.gameObject.SetActive(false);
            }
        }

        public void HideForPlayerDeath()
        {
            carrierSkipFade = false;
            if (openingFade != null)
                openingFade.gameObject.SetActive(false);
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !Application.isPlaying || target == null)
                return;

            if (!cueWarmed)
            {
                // Exercise the IMGUI draw path as well as the font atlas before the first visible cue.
                Color color = GUI.color;
                try
                {
                    GUI.color = Color.clear;
                    DrawCue(0f);
                    DrawCue(Duration);
                }
                finally { GUI.color = color; }
                cueWarmed = true;
            }

            if (VoxelLoadingScreen.IsLoading || !started || VoxelPauseMenu.IsPaused || VoxelPlayerDeathScreen.IsShowing)
                return;

            float elapsed = Time.time - countdownStartedAt;
            if (elapsed >= 0f && elapsed < 4f)
                DrawCue(elapsed);
        }

        private void DrawCue(float elapsed)
        {
            string text = elapsed < 1f ? "3" : elapsed < Duration - 1f ? "2" : elapsed < Duration ? "1" : LaunchCue;
            numberStyle ??= new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                font = VoxelHudStyles.HudFont,
                fontStyle = FontStyle.Normal,
                normal = { textColor = Color.white }
            };
            trackStyle ??= new GUIStyle(numberStyle);
            float scale = Screen.height / 1080f;
            float numberTop = Screen.height * 0.22f;
            numberStyle.fontSize = ScaleFont(elapsed < Duration ? NumberSize : LaunchSize, scale);
            DrawOutlinedLabel(new Rect(0f, numberTop, Screen.width, 160f * scale), text,
                numberStyle, 4f * scale, 1f, Color.white);

            // Boss instructions fade in during "3", hold through "2", and fade out during "1".
            float nameAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(elapsed, 3f - elapsed)));
            if (isBossMission && nameAlpha > 0f && !string.IsNullOrWhiteSpace(trackDisplayName))
            {
                trackStyle.fontSize = ScaleFont(72f, scale);
                float availableWidth = Screen.width * .9f;
                float textWidth = trackStyle.CalcSize(new GUIContent(trackDisplayName)).x;
                if (textWidth > availableWidth)
                    trackStyle.fontSize = Mathf.Max(1, Mathf.FloorToInt(trackStyle.fontSize * availableWidth / textWidth));
                DrawOutlinedLabel(new Rect(Screen.width * .05f, numberTop - 224f * scale,
                    availableWidth, 240f * scale), trackDisplayName, trackStyle, 2f * scale, nameAlpha, new Color(.70f, .86f, .96f));
            }
        }

        private static int ScaleFont(float size, float scale) => Mathf.Max(1, Mathf.RoundToInt(size * scale));

        private static void DrawOutlinedLabel(Rect rect, string text, GUIStyle style, float width, float alpha, Color color)
        {
            style.normal.textColor = new Color(.015f, .025f, .04f, alpha);
            for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    if (x == 0 && y == 0) continue;
                    GUI.Label(new Rect(rect.x + x * width, rect.y + y * width, rect.width, rect.height), text, style);
                }
            color.a *= alpha;
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }
    }
}
