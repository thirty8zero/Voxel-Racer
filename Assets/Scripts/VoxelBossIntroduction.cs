using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>A scene-owned boss reveal. Locks input and the bonus clock without stopping the road.</summary>
    [DefaultExecutionOrder(-900)]
    public sealed class VoxelBossIntroduction : MonoBehaviour
    {
        public static VoxelBossIntroduction Active { get; private set; }
        public static bool IsShowing => Active != null && Active.IsPresenting;
        public static bool BlocksControls(VoxelCarController car) => IsShowing && Active.player == car;
        public static bool PausesMission(VoxelMissionProgress progress) => IsShowing && Active.mission == progress;
        public bool IsPresenting { get; private set; }
        public float Elapsed { get; private set; }

        private VoxelCarController player;
        private VoxelMissionProgress mission;
        private Action completed;
        private RectTransform canvas, slide, impact;
        private RawImage artwork;
        private Sequence motion;
        private float slideIn, hold, slideOut, shakeDuration, shakePixels, viewportWidth;
        private float TotalDuration => slideIn + hold + slideOut;

        public bool Begin(VoxelBossDefinition settings, VoxelCarController target,
            VoxelMissionProgress progress, Action onComplete)
        {
            if (settings == null || settings.introductionPoster == null || target == null || progress == null)
                return false;
            Cancel();
            if (Active != null && Active != this) Active.Cancel();
            player = target;
            mission = progress;
            completed = onComplete;
            slideIn = Mathf.Max(.01f, settings.introductionSlideInDuration);
            hold = Mathf.Max(0, settings.introductionHoldDuration);
            slideOut = Mathf.Max(.01f, settings.introductionSlideOutDuration);
            shakeDuration = Mathf.Min(hold, Mathf.Max(0, settings.introductionShakeDuration));
            shakePixels = Mathf.Max(0, settings.introductionShakePixels);
            BuildView();
            artwork.texture = settings.introductionPoster;
            artwork.GetComponent<AspectRatioFitter>().aspectRatio = artwork.texture.width / (float)artwork.texture.height;
            canvas.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            Elapsed = 0;
            IsPresenting = true;
            Active = this;
            VoxelMobileControls.ClearHeldInput();
            RebuildMotion();
            return true;
        }

        private void Update()
        {
            if (!IsPresenting) return;
            if (player == null || player.IsDestroyed || mission == null || mission.IsComplete || mission.IsFailed ||
                VoxelLoadingScreen.IsLoading)
            { Cancel(); return; }
            Advance(Time.unscaledDeltaTime);
        }

        internal void Advance(float seconds)
        {
            if (!IsPresenting || VoxelPauseMenu.IsPaused) return;
            if (Mathf.Abs(canvas.rect.width - viewportWidth) > 1f) RebuildMotion();
            Elapsed = Mathf.Min(TotalDuration, Elapsed + Mathf.Max(0, seconds));
            // A paused DOTween sequence is sampled explicitly, so pause and previews share the same clock.
            motion.Goto(Elapsed, false);
            float landedFor = Elapsed - slideIn;
            float envelope = shakeDuration > 0 ? 1f - Mathf.Clamp01(landedFor / shakeDuration) : 0;
            impact.anchoredPosition = landedFor >= 0 && landedFor < shakeDuration
                ? new Vector2(Mathf.Sin(landedFor * 137f), Mathf.Sin(landedFor * 173f)) * (shakePixels * envelope)
                : Vector2.zero;
            if (Elapsed < TotalDuration) return;
            Action callback = completed;
            Cancel(); // Unlock before the encounter starts the normal boss entrance.
            callback?.Invoke();
        }

        private void RebuildMotion()
        {
            motion?.Kill();
            viewportWidth = Mathf.Max(1, canvas.rect.width);
            slide.anchoredPosition = new Vector2(viewportWidth + shakePixels + 2, 0);
            motion = DOTween.Sequence().SetAutoKill(false).Pause();
            motion.Append(slide.DOAnchorPosX(0, slideIn).SetEase(Ease.OutCubic));
            motion.AppendInterval(hold);
            motion.Append(slide.DOAnchorPosX(-viewportWidth - shakePixels - 2, slideOut).SetEase(Ease.InCubic));
            motion.Goto(Elapsed, false);
        }

        public void Cancel()
        {
            completed = null;
            motion?.Kill();
            motion = null;
            IsPresenting = false;
            if (Active == this) Active = null;
            if (canvas != null) canvas.gameObject.SetActive(false);
            if (impact != null) impact.anchoredPosition = Vector2.zero;
            VoxelMobileControls.ClearHeldInput();
        }

        private void BuildView()
        {
            if (canvas != null) return;
            var root = new GameObject("Boss Introduction Canvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(transform, false);
            canvas = (RectTransform)root.transform;
            var overlay = root.GetComponent<Canvas>();
            overlay.renderMode = RenderMode.ScreenSpaceOverlay;
            overlay.sortingOrder = 10500; // Below pause/loading, above the race HUD.
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 1;
            var blocker = Rect("Input Blocker", canvas).gameObject.AddComponent<Image>();
            blocker.color = Color.clear;
            blocker.raycastTarget = true;
            slide = Rect("Sliding Poster", canvas);
            impact = Rect("Impact Shake", slide);
            var background = impact.gameObject.AddComponent<Image>();
            background.color = Color.black;
            background.raycastTarget = false;
            var image = new GameObject("Boss Poster", typeof(RectTransform), typeof(RawImage), typeof(AspectRatioFitter));
            image.transform.SetParent(impact, false);
            artwork = image.GetComponent<RawImage>();
            artwork.raycastTarget = false;
            image.GetComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private void OnDisable() => Cancel();
        private void OnDestroy() => Cancel();
    }
}
