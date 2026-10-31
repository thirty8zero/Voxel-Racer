using System;
using System.Collections.Generic;
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
        public float Duration => TotalDuration;
        public float AssembledAt => assemblyTime;

        private VoxelCarController player;
        private VoxelMissionProgress mission;
        private Action completed;
        private RectTransform canvas, slide, impact;
        private RawImage artwork;
        private Image backdrop, landingBackdrop;
        private readonly List<PanelView> panels = new();
        private bool layered;
        private float gap, assemblyTime, firstSide;
        private Sequence motion;
        private float slideIn, hold, slideOut, shakeDuration, shakePixels, viewportWidth, viewportHeight;
        private float TotalDuration => assemblyTime + hold + slideOut;

        private sealed class PanelView
        {
            internal RectTransform slide, fit, image;
            internal float start, sign;
            internal int drawOrder;
            internal bool fillScreen;
            internal float scale;
            internal Vector2 position, reference;
        }

        public bool Begin(VoxelBossDefinition settings, VoxelCarController target,
            VoxelMissionProgress progress, Action onComplete)
        {
            if (settings == null || !settings.HasIntroduction || target == null || progress == null)
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
            layered = settings.introductionSequence != null && settings.introductionSequence.HasPanels;
            gap = 0; firstSide = 1;
            if (layered)
            {
                var sequence = settings.introductionSequence;
                slideIn = Mathf.Max(.01f, sequence.slideInDuration);
                gap = Mathf.Max(0, sequence.delayBetweenPanels);
                hold = Mathf.Max(0, sequence.holdDuration);
                slideOut = Mathf.Max(.01f, sequence.slideOutDuration);
                shakeDuration = Mathf.Max(0, sequence.shakeDuration);
                shakePixels = Mathf.Max(0, sequence.shakePixels);
                firstSide = sequence.firstEntrySide == VoxelBossIntroSide.Left ? -1 : 1;
            }
            BuildView();
            ClearPanels();
            artwork.gameObject.SetActive(!layered);
            backdrop.enabled = !layered;
            landingBackdrop.enabled = false;
            if (layered) BuildPanels(settings.introductionSequence);
            else
            {
                artwork.texture = settings.introductionPoster;
                artwork.GetComponent<AspectRatioFitter>().aspectRatio = artwork.texture.width / (float)artwork.texture.height;
            }
            assemblyTime = layered ? panels.Count * slideIn + (panels.Count - 1) * gap : slideIn;
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
            if (Mathf.Abs(canvas.rect.width - viewportWidth) > 1f || Mathf.Abs(canvas.rect.height - viewportHeight) > 1f) RebuildMotion();
            Elapsed = Mathf.Min(TotalDuration, Elapsed + Mathf.Max(0, seconds));
            // A paused DOTween sequence is sampled explicitly, so pause and previews share the same clock.
            motion.Goto(Elapsed, false);
            // Only cover the scene once the first entry has landed. This sibling of the
            // shake root stays still during impacts and follows the composition's exit.
            landingBackdrop.enabled = Elapsed >= slideIn;
            if (layered)
                foreach (var panel in panels) panel.slide.gameObject.SetActive(Elapsed >= panel.start);
            float landedFor = Elapsed - slideIn;
            if (layered)
                foreach (var panel in panels)
                    if (Elapsed >= panel.start + slideIn) landedFor = Elapsed - panel.start - slideIn;
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
            viewportHeight = Mathf.Max(1, canvas.rect.height);
            float offscreen = viewportWidth + shakePixels + 2;
            float padding = shakePixels + 2;
            landingBackdrop.rectTransform.offsetMin = -Vector2.one * padding;
            landingBackdrop.rectTransform.offsetMax = Vector2.one * padding;
            slide.anchoredPosition = layered ? Vector2.zero : new Vector2(offscreen, 0);
            motion = DOTween.Sequence().SetAutoKill(false).Pause();
            if (layered)
            {
                foreach (var panel in panels)
                {
                    panel.slide.anchoredPosition = new Vector2(panel.sign * offscreen, 0);
                    panel.slide.gameObject.SetActive(Elapsed >= panel.start);
                    motion.Insert(panel.start, panel.slide.DOAnchorPosX(0, slideIn).SetEase(Ease.OutCubic));
                }
                motion.Insert(assemblyTime + hold, slide.DOAnchorPosX(-firstSide * offscreen, slideOut).SetEase(Ease.InCubic));
            }
            else
            {
                motion.Append(slide.DOAnchorPosX(0, slideIn).SetEase(Ease.OutCubic));
                motion.AppendInterval(hold);
                motion.Append(slide.DOAnchorPosX(-offscreen, slideOut).SetEase(Ease.InCubic));
            }
            motion.Goto(Elapsed, false);
            LayoutPanels();
        }

        private void BuildPanels(VoxelBossIntroductionSequence sequence)
        {
            foreach (var entry in sequence.panels)
            {
                if (entry == null || entry.texture == null) continue;
                int index = panels.Count;
                var panel = new PanelView
                {
                    slide = Rect("Panel " + index + " - " + entry.label, impact),
                    start = index * (slideIn + gap), sign = index % 2 == 0 ? firstSide : -firstSide,
                    drawOrder = entry.drawOrder, fillScreen = entry.fillScreen,
                    scale = Mathf.Max(.01f, entry.scale), position = entry.position,
                    reference = new Vector2(Mathf.Max(1, sequence.referenceResolution.x), Mathf.Max(1, sequence.referenceResolution.y))
                };
                panel.fit = Rect("Artwork Frame", panel.slide);
                panel.image = Rect("Artwork", panel.fit);
                var image = panel.image.gameObject.AddComponent<RawImage>();
                image.texture = entry.texture; image.raycastTarget = false;
                panels.Add(panel);
            }
            // Stable sorting changes visual stacking, never the panel clock or alternating direction.
            var ordered = new List<PanelView>(panels);
            ordered.Sort((a, b) => a.drawOrder != b.drawOrder ? a.drawOrder.CompareTo(b.drawOrder) : a.start.CompareTo(b.start));
            foreach (var panel in ordered) panel.slide.SetAsLastSibling();
        }

        private void LayoutPanels()
        {
            foreach (var panel in panels)
            {
                float factor = Mathf.Min(canvas.rect.width / panel.reference.x, canvas.rect.height / panel.reference.y);
                var size = panel.fillScreen ? canvas.rect.size : panel.reference * factor;
                panel.fit.anchorMin = panel.fit.anchorMax = new Vector2(.5f, .5f);
                panel.fit.sizeDelta = size; panel.fit.anchoredPosition = Vector2.zero;
                panel.image.localScale = Vector3.one * panel.scale;
                panel.image.anchoredPosition = panel.position * factor;
            }
        }

        private void ClearPanels()
        {
            foreach (var panel in panels)
            {
                panel.slide.gameObject.SetActive(false);
                if (Application.isPlaying) Destroy(panel.slide.gameObject); else DestroyImmediate(panel.slide.gameObject);
            }
            panels.Clear();
        }

        public void Cancel()
        {
            completed = null;
            motion?.Kill();
            motion = null;
            IsPresenting = false;
            if (Active == this) Active = null;
            if (canvas != null) canvas.gameObject.SetActive(false);
            if (landingBackdrop != null) landingBackdrop.enabled = false;
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
            landingBackdrop = Rect("Landing Black Backdrop", slide).gameObject.AddComponent<Image>();
            landingBackdrop.color = Color.black;
            landingBackdrop.raycastTarget = false;
            landingBackdrop.enabled = false;
            impact = Rect("Impact Shake", slide);
            backdrop = impact.gameObject.AddComponent<Image>();
            backdrop.color = Color.black;
            backdrop.raycastTarget = false;
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
