using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Holds the player at the mission start, then presents a short race countdown.</summary>
    public sealed class VoxelStartCountdown : MonoBehaviour
    {
        public static VoxelStartCountdown Active { get; private set; }
        public VoxelCarController target;

        [Header("Race Opening Fade")]
        [Min(0f)] public float blackScreenDuration = 1f;
        [Min(0.01f)] public float fadeInDuration = 2f;

        private float openingStartedAt;
        private float countdownStartedAt;
        private bool started;
        private bool prepared;
        private bool waitingForLoadingScreen;
        private CanvasGroup openingFade;
        private string trackDisplayName;
        private bool isBossMission;
        private GUIStyle numberStyle;
        private GUIStyle trackStyle;

        public bool IsComplete => started && Time.time - countdownStartedAt >= 3f;

        /// <summary>Traffic may enter during the final displayed "1" second so it is present when driving begins.</summary>
        public bool IsTrafficSpawnWindowOpen => started && Time.time - countdownStartedAt >= 2f;

        /// <summary>Gameplay HUD fades in during the one-second "GO!" phase.</summary>
        public float GameplayHudAlpha => !started
            ? 0f
            : Mathf.Clamp01(Time.time - countdownStartedAt - 3f);

        public static float CurrentGameplayHudAlpha => Active == null ? 1f : Active.GameplayHudAlpha;

        private void OnEnable() => Active = this;

        private void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        public void Prepare(VoxelCarController player, string trackName = null, bool? bossMission = null)
        {
            target = player;
            isBossMission = bossMission ?? (VoxelRacerBootstrap.ActiveTrack?.isBossLevel ?? false);
            trackDisplayName = isBossMission ? "Find The Radar..." : trackName ?? VoxelRacerBootstrap.ActiveTrack?.displayName ?? string.Empty;
            target.SetDrivingEnabled(false);
            prepared = true;
            started = false;
            waitingForLoadingScreen = false;
        }

        public void BeginCountdown()
        {
            if (target == null)
                return;

            target.SetDrivingEnabled(false);
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
            if (openingFade != null)
                openingFade.gameObject.SetActive(false);
        }

        private void OnGUI()
        {
            if (VoxelLoadingScreen.IsLoading) return;
            if (!Application.isPlaying || target == null || !started || VoxelPauseMenu.IsPaused || VoxelPlayerDeathScreen.IsShowing)
                return;

            float elapsed = Time.time - countdownStartedAt;
            if (elapsed < 0f)
                return;
            if (elapsed >= 4f)
                return;

            string text = elapsed < 1f ? "3" : elapsed < 2f ? "2" : elapsed < 3f ? "1" : "GO!";
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
            numberStyle.fontSize = Mathf.Max(1, Mathf.RoundToInt((text == "GO!" ? 114f : 144f) * scale));
            DrawOutlinedLabel(new Rect(0f, numberTop, Screen.width, 160f * scale), text,
                numberStyle, 4f * scale, 1f, Color.white);

            // Fade up throughout "3", peak at "2", and disappear as "1" begins.
            float nameAlpha = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Min(elapsed, 2f - elapsed)));
            if (nameAlpha > 0f && !string.IsNullOrWhiteSpace(trackDisplayName))
            {
                trackStyle.fontSize = Mathf.Max(1, Mathf.RoundToInt(numberStyle.fontSize * (isBossMission ? 1.05f : 1.2f)));
                float availableWidth = Screen.width * .9f;
                float textWidth = trackStyle.CalcSize(new GUIContent(trackDisplayName)).x;
                if (textWidth > availableWidth)
                    trackStyle.fontSize = Mathf.Max(1, Mathf.FloorToInt(trackStyle.fontSize * availableWidth / textWidth));
                DrawOutlinedLabel(new Rect(Screen.width * .05f, numberTop - 224f * scale,
                    availableWidth, 240f * scale), trackDisplayName, trackStyle, 2f * scale, nameAlpha, isBossMission ? Color.red : Color.yellow);
            }
        }

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
