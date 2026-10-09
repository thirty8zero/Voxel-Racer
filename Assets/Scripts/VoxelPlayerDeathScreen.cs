using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Returns to the main menu after the player's car has been destroyed.</summary>
    public sealed class VoxelPlayerDeathScreen : MonoBehaviour
    {
        public static VoxelPlayerDeathScreen Active { get; private set; }
        // Includes the cinematic before the panel appears, keeping HUD/input hidden.
        public static bool IsShowing { get; private set; }

        public VoxelCarController target;
        public string mainMenuSceneName = "MainMenu";

        private bool isLoading;
        private bool isShown;
        private bool pausedForEscape;
        private float previousTimeScale;
        private const float DeathSlowMotionScale = .25f;
        private const float DeathSlowMotionDuration = 2f;
        private const float UiFadeDuration = .35f;
        private const float UiSlideDuration = .6f;
        private bool deathSequenceStarted, deathSlowMotionActive;
        private float deathPreviousTimeScale, deathPreviousFixedStep, deathSlowMotionRemaining, uiRevealElapsed;
        private CanvasGroup failureUiGroup;
        private RectTransform failureHeader, failureRestart;
        private Text failureTitle, failureReason;
        private Vector2 failureCanvasSize;
        private VoxelCameraFollow deathCamera;

        private void OnEnable() => Active = this;

        public void BeginPlayerDeath(VoxelCarController player)
        {
            if (player == null || !player.IsDestroyed || target != player || deathSequenceStarted || isShown) return;
            VoxelPauseMenu.ResumeActivePause();
            deathSequenceStarted = true;
            IsShowing = true;
            VoxelMobileControls.ClearHeldInput();
            VoxelStartCountdown.Active?.HideForPlayerDeath();
            deathPreviousTimeScale = Time.timeScale;
            deathPreviousFixedStep = Time.fixedDeltaTime;
            deathSlowMotionRemaining = DeathSlowMotionDuration;
            deathSlowMotionActive = true;
            Time.timeScale = deathPreviousTimeScale * DeathSlowMotionScale;
            // Keep physics at its normal real-time cadence during slow motion, including interpolation.
            Time.fixedDeltaTime = deathPreviousFixedStep * DeathSlowMotionScale;
            deathCamera = Camera.main?.GetComponent<VoxelCameraFollow>();
            if (deathCamera != null && deathCamera.target != player.transform) deathCamera = null;
            deathCamera?.BeginDeathSequence(player);
        }

        public void ShowBossEscaped()
        {
            if(isShown || deathSequenceStarted) return;
            VoxelPauseMenu.ResumeActivePause();
            previousTimeScale=Time.timeScale;pausedForEscape=true;Time.timeScale=0;
            Show();
            if(failureHeader!=null)
            {
                failureReason=VoxelMenuUi.CreateText(failureHeader,"Failure Reason","THE BOSS ESCAPED",38,
                    TextAnchor.MiddleCenter,new Vector2(.5f,0),new Vector2(0,38),new Vector2(900,60));
                StretchTextWidth(failureReason.rectTransform,24f);
                FitFailureHeader();
            }
        }

        public void Configure(VoxelCarController player) => target = player;

        private void OnDisable()
        {
            RestoreDeathTimeScale();
            if(pausedForEscape) {Time.timeScale=previousTimeScale;pausedForEscape=false;}
            if (Active == this)
            {
                IsShowing = false;
                Active = null;
            }
        }

        private void Update()
        {
            if (!deathSequenceStarted && !isShown && target != null && target.IsDestroyed)
                BeginPlayerDeath(target);
            TickDeathSequence(Time.unscaledDeltaTime);
        }

        private void TickDeathSequence(float unscaledDeltaTime)
        {
            if (deathSlowMotionActive)
            {
                deathSlowMotionRemaining -= Mathf.Max(0f, unscaledDeltaTime);
                if (deathSlowMotionRemaining <= 0f) RestoreDeathTimeScale();
            }
            if (deathSequenceStarted && !isShown && !deathSlowMotionActive && target != null &&
                target.IsWreckResting && (deathCamera == null || deathCamera.DeathSequenceComplete))
                Show();
            if (failureUiGroup != null)
            {
                var canvasRect = (RectTransform)failureUiGroup.transform;
                if (canvasRect.rect.size != failureCanvasSize) FitFailureHeader();
                uiRevealElapsed += Mathf.Max(0f, unscaledDeltaTime);
                failureUiGroup.alpha = Mathf.Clamp01(uiRevealElapsed / UiFadeDuration);
                float progress = Mathf.Clamp01(uiRevealElapsed / UiSlideDuration);
                float eased = 1f - Mathf.Pow(1f - progress, 3f);
                failureHeader.anchoredPosition = new Vector2(0f, Mathf.Lerp(270f, -28f, eased));
                failureRestart.anchoredPosition = new Vector2(0f, Mathf.Lerp(-210f, 32f, eased));
                failureUiGroup.interactable = progress >= 1f;
            }
        }

        private void RestoreDeathTimeScale()
        {
            if (!deathSlowMotionActive) return;
            deathSlowMotionActive = false;
            // Release only our slowdown; another system may have taken the clock.
            if (Mathf.Approximately(Time.timeScale, deathPreviousTimeScale * DeathSlowMotionScale))
                Time.timeScale = deathPreviousTimeScale;
            // Unity quantizes fixed time; allow one microsecond rather than relative float equality.
            if (Mathf.Abs(Time.fixedDeltaTime - deathPreviousFixedStep * DeathSlowMotionScale) < .000001f)
                Time.fixedDeltaTime = deathPreviousFixedStep;
        }

        private void Show()
        {
            VoxelPauseMenu.ResumeActivePause();
            isShown = true;
            IsShowing = true;
            VoxelStartCountdown.Active?.HideForPlayerDeath();
            RectTransform canvas = VoxelMenuUi.CreateCanvas(transform, "Mission Failed UI");
            var scaler = canvas.GetComponent<CanvasScaler>();
            scaler.matchWidthOrHeight = 1f;
            scaler.enabled = false; scaler.enabled = true;
            canvas.GetComponent<Canvas>().sortingOrder = 1000;
            failureUiGroup = canvas.gameObject.AddComponent<CanvasGroup>();
            failureUiGroup.alpha = 0f;
            failureUiGroup.interactable = false;
            uiRevealElapsed = 0f;
            var header = VoxelMenuUi.CreatePanel(canvas, "Failure Header", new Vector2(.5f, 1f),
                new Vector2(0f, 270f), new Vector2(0f, 230f));
            // Halve the remaining light through the backdrop again (17.5% -> 8.75%).
            header.color = new Color(.005f, .00625f, .01f, .9125f); header.raycastTarget = false;
            failureHeader = header.rectTransform;
            failureHeader.pivot = new Vector2(.5f, 1f);

            Text title = VoxelMenuUi.CreateText(failureHeader, "Mission Failed Title", "MISSION FAILED", 160,
                TextAnchor.MiddleCenter, new Vector2(.5f, 1f), new Vector2(0f, -95f), new Vector2(0f, 170f));
            failureTitle = title;
            title.resizeTextForBestFit = true; title.resizeTextMinSize = 48; title.resizeTextMaxSize = 160;
            Font brokenGlass = Resources.Load<Font>("Fonts/BrokenGlass");
            if (brokenGlass != null)
                title.font = brokenGlass;
            title.fontStyle = FontStyle.Normal;
            title.color = new Color(0.96f, 0.16f, 0.08f);
            FitFailureHeader();

            var restart = VoxelMenuUi.CreatePanel(canvas, "Failure Restart", new Vector2(.5f, 0f),
                new Vector2(0f, -210f), new Vector2(0f, 170f));
            restart.color = new Color(.02f, .025f, .04f, .65f); restart.raycastTarget = false;
            failureRestart = restart.rectTransform;
            failureRestart.anchorMin = new Vector2(.28f, 0f); failureRestart.anchorMax = new Vector2(.72f, 0f);
            failureRestart.pivot = new Vector2(.5f, 0f);
            var button = VoxelMenuUi.CreateButton(failureRestart, "Main Menu Button", "RESTART", 78,
                new Vector2(.5f, .5f), Vector2.zero, new Vector2(0f, 110f), ReturnToMainMenu);
            StretchTextWidth((RectTransform)button.transform,24f);
            var label = button.GetComponentInChildren<Text>();
            label.rectTransform.anchorMin = Vector2.zero; label.rectTransform.anchorMax = Vector2.one;
            label.rectTransform.sizeDelta = Vector2.zero;
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 30; label.resizeTextMaxSize = 78;
        }

        private void FitFailureHeader()
        {
            var canvasRect = (RectTransform)failureUiGroup.transform;
            failureCanvasSize = canvasRect.rect.size;
            // Fit the visible glyphs, not the font's taller line box. Retain best-fit on narrow screens.
            Vector2 textSize = new Vector2(Mathf.Max(1f, failureCanvasSize.x * .88f - 48f), 170f);
            failureTitle.rectTransform.sizeDelta = textSize;
            var generator = failureTitle.cachedTextGenerator;
            generator.Populate(failureTitle.text, failureTitle.GetGenerationSettings(textSize));
            var vertices = generator.verts;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            bool foundGlyph = false;
            // Unity 6 does not append the old four dummy vertices: subtracting four drops the final letter.
            // Include every real quad, ignoring only empty whitespace/dummy geometry if present.
            for (int i = 0; i + 3 < vertices.Count; i += 4)
            {
                Vector2 quadMin = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                Vector2 quadMax = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector2 point = vertices[i + corner].position / failureTitle.pixelsPerUnit;
                    quadMin = Vector2.Min(quadMin, point); quadMax = Vector2.Max(quadMax, point);
                }
                if (quadMax.x <= quadMin.x || quadMax.y <= quadMin.y) continue;
                minimum = Vector2.Min(minimum, quadMin); maximum = Vector2.Max(maximum, quadMax);
                foundGlyph = true;
            }
            if (!foundGlyph) return;
            Vector2 glyphSize = maximum - minimum;
            Vector2 glyphCentre = (minimum + maximum) * .5f;
            const float padding = 12f;
            failureHeader.sizeDelta = glyphSize + new Vector2(padding * 2f, padding * 2f + (failureReason != null ? 60f : 0f));
            failureTitle.rectTransform.anchoredPosition = new Vector2(-glyphCentre.x, -padding - glyphSize.y * .5f - glyphCentre.y);
        }

        private static void StretchTextWidth(RectTransform rect, float margin)
        {
            rect.anchorMin = new Vector2(0f, rect.anchorMin.y);
            rect.anchorMax = new Vector2(1f, rect.anchorMax.y);
            rect.sizeDelta = new Vector2(-margin * 2f, rect.sizeDelta.y);
        }

        private void ReturnToMainMenu()
        {
            if (isLoading)
                return;

            int buildIndex = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/" + mainMenuSceneName + ".unity");
            if (buildIndex < 0)
            {
                Debug.LogError("Main Menu scene is not enabled in Build Settings: " + mainMenuSceneName);
                return;
            }

            VoxelCarRunState.BeginNewRun(VoxelCarSelectionState.GetSelectedOrDefault());
            VoxelTrackProgressState.BeginSequence();
            isLoading = true;
            SceneManager.LoadScene(buildIndex);
        }
    }
}
