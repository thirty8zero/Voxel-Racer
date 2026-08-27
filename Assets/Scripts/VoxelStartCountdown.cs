using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Holds the player at the start line, then presents a short race countdown.</summary>
    public sealed class VoxelStartCountdown : MonoBehaviour
    {
        public static VoxelStartCountdown Active { get; private set; }
        public VoxelCarController target;

        [Header("Race Opening Fade")]
        [Min(0f)] public float blackScreenDuration = 1f;
        [Min(0.01f)] public float fadeInDuration = 1f;

        private float openingStartedAt;
        private float countdownStartedAt;
        private bool started;
        private bool prepared;
        private CanvasGroup openingFade;

        public bool IsComplete => started && Time.unscaledTime - countdownStartedAt >= 3f;

        /// <summary>Gameplay HUD fades in during the final one-second "1" phase.</summary>
        public float GameplayHudAlpha => !started
            ? 0f
            : Mathf.Clamp01(Time.unscaledTime - countdownStartedAt - 2f);

        public static float CurrentGameplayHudAlpha => Active == null ? 1f : Active.GameplayHudAlpha;

        private void OnEnable() => Active = this;

        private void OnDisable()
        {
            if (Active == this)
                Active = null;
        }

        public void Prepare(VoxelCarController player)
        {
            target = player;
            target.SetDrivingEnabled(false);
            prepared = true;
            started = false;
        }

        public void BeginCountdown()
        {
            if (target == null)
                return;

            target.SetDrivingEnabled(false);
            EnsureOpeningFade();
            openingFade.gameObject.SetActive(true);
            openingFade.alpha = 1f;
            openingStartedAt = Time.unscaledTime;
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

            float elapsed = Time.unscaledTime - openingStartedAt;
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
            if (!Application.isPlaying || target == null || !started || VoxelPlayerDeathScreen.IsShowing)
                return;

            float elapsed = Time.unscaledTime - countdownStartedAt;
            if (elapsed < 0f)
                return;
            if (elapsed >= 4f)
                return;

            string text = elapsed < 1f ? "3" : elapsed < 2f ? "2" : elapsed < 3f ? "1" : "GO!";
            var style = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                font = VoxelHudStyles.HudFont,
                fontSize = text == "GO!" ? 114 : 144,
                fontStyle = FontStyle.Normal,
                normal = { textColor = Color.white }
            };
            GUI.Label(new Rect(0f, Screen.height * 0.22f, Screen.width, 120f), text, style);
        }
    }
}
