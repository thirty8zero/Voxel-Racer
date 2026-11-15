using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Scene-owned mission objectives and enemy previews, with a live carrier layout and a full-screen fallback.</summary>
    public sealed class VoxelMissionBriefing : MonoBehaviour
    {
        private const float RacePanelInset = 20f;
        private const float InnerWidth = 838f, CardHeight = 232f, CardGap = 20f, BannerHeight = 45f;
        private const float ViewportHeight = 2 * CardHeight + CardGap;
        public static VoxelMissionBriefing Active { get; private set; }
        public static bool IsShowing => Active != null && Active.shown;
        private static VoxelTrackDefinition stationaryRetryTrack;
        private static Action returnToOrigin;
        private static readonly Color Ice = new(.69f, .84f, .96f), Silver = new(.47f, .56f, .65f);
        private RectTransform canvas, content, cards;
        private RawImage background;
        private Text title, objective, bossObjective;
        private Button startButton;
        private ScrollRect enemyScroll;
        private VoxelMissionBriefingSettings settings;
        private List<VoxelMissionBriefingEnemy> enemies;
        private readonly List<(RawImage image, VoxelMissionBriefingPreview preview)> animatedPreviews = new();
        private float previewsStartedAt;
        private Action launch, back;
        private bool shown, alongsideRace;
        private int openedFrame;
        public VoxelTrackDefinition Track { get; private set; }
        public int EnemyCount => enemies?.Count ?? 0;
        public bool IsPresenting => shown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() { Active = null; stationaryRetryTrack = null; returnToOrigin = null; }
        internal static void SetRaceBackAction(Action action) => returnToOrigin = action;
        internal static Action TakeRaceBackAction()
        {
            var action = returnToOrigin;
            returnToOrigin = null;
            return action;
        }
        public static void UseStationaryOpeningOnRetry(VoxelTrackDefinition track) => stationaryRetryTrack = track;
        public static bool ConsumeStationaryRetry(VoxelTrackDefinition track)
        {
            bool approved = stationaryRetryTrack != null && stationaryRetryTrack == track;
            stationaryRetryTrack = null;
            return approved;
        }
        public static VoxelMissionBriefing Present(Transform owner, VoxelTrackDefinition track, Action onStart,
            Action onBack = null, bool alongsideRace = false)
        {
            if (IsShowing) return Active;
            var view = owner.GetComponent<VoxelMissionBriefing>() ?? owner.gameObject.AddComponent<VoxelMissionBriefing>();
            view.Show(track, onStart, onBack, alongsideRace);
            return view;
        }
        public void Show(VoxelTrackDefinition track, Action onStart, Action onBack = null, bool alongsideRace = false)
        {
            if (Active != null && Active != this) Active.Close();
            Close();
            if (canvas != null && this.alongsideRace != alongsideRace)
            {
                if (Application.isPlaying) Destroy(canvas.gameObject); else DestroyImmediate(canvas.gameObject);
                canvas = null;
            }
            this.alongsideRace = alongsideRace;
            Track = track; launch = onStart; back = onBack; settings = VoxelMissionBriefingSettings.Load();
            enemies = VoxelMissionBriefingContent.Enemies(track);
            if (canvas == null) Build();
            bool boss = track != null && track.boss != null;
            title.text = VoxelTrackDefinition.ResolveDisplayName(track).ToUpperInvariant();
            SetRect(title.rectTransform, 52, 109, boss ? InnerWidth : 590, 78);
            objective.transform.parent.gameObject.SetActive(!boss);
            bossObjective.gameObject.SetActive(boss);
            var target = boss ? bossObjective : objective;
            target.text = VoxelMissionBriefingContent.Objective(track);
            background.texture = settings != null ? settings.BackgroundFor(track) : null;
            background.enabled = !alongsideRace && background.texture != null;
            if (background.texture != null) background.GetComponent<AspectRatioFitter>().aspectRatio = background.texture.width / (float)background.texture.height;
            canvas.Find("Background Dim").GetComponent<Image>().color =
                new Color(0, 0, 0, alongsideRace ? 0f : settings != null ? settings.backgroundDim : .12f);
            shown = true; Active = this; openedFrame = Time.frameCount;
            canvas.gameObject.SetActive(true); RebuildCards(); Layout();
            VoxelMobileControls.ClearHeldInput(); EventSystem.current?.SetSelectedGameObject(null);
        }
        public void Accept()
        {
            if (!shown || VoxelLoadingScreen.IsLoading) return;
            var callback = launch; Close(); callback?.Invoke();
        }
        public void GoBack()
        {
            if (!shown || VoxelLoadingScreen.IsLoading) return;
            var callback = back; Close(); callback?.Invoke();
        }
        public void Close()
        {
            shown = false; launch = back = null;
            if (Active == this) Active = null;
            if (canvas != null) canvas.gameObject.SetActive(false);
            VoxelMobileControls.ClearHeldInput();
        }
        private void Update()
        {
            if (!shown) return;
            Layout();
            AnimatePreviews(Time.unscaledTime - previewsStartedAt);
            if (VoxelLoadingScreen.IsLoading || Time.frameCount <= openedFrame + 1) return;
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) GoBack();
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame) Accept();
            else VoxelEditorNavigation.Activate(startButton);
        }
        private void Layout()
        {
            if (canvas == null || content == null) return;
            var size = canvas.rect.size;
            Rect safe = Application.isPlaying ? Screen.safeArea : new Rect(0, 0, Screen.width, Screen.height);
            Vector2 ratio = new(size.x / Mathf.Max(1, Screen.width), size.y / Mathf.Max(1, Screen.height));
            float width = safe.width * ratio.x, height = safe.height * ratio.y;
            float scale = RacePanelScale(width, height);
            content.localScale = Vector3.one * scale;
            Vector2 center = new(safe.xMin * ratio.x + content.sizeDelta.x * .5f * scale, safe.center.y * ratio.y);
            content.anchoredPosition = center - size * .5f;
        }

        internal static float RacePanelRightViewport(Rect safeArea, float screenWidth) =>
            (safeArea.xMin + (RacePanelInset + VoxelPostRaceContinue.PanelWidth) *
                RacePanelScale(safeArea.width, safeArea.height)) / screenWidth;

        private static float RacePanelScale(float width, float height) =>
            Mathf.Min(width * .58f / VoxelPostRaceContinue.PanelWidth, height / 1080f);

        private void Build()
        {
            canvas = VoxelMenuUi.CreateCanvas(transform, "Mission Briefing Canvas");
            canvas.GetComponent<Canvas>().sortingOrder = 11000;
            canvas.GetComponent<CanvasScaler>().matchWidthOrHeight = 1;
            var fill = Stretch("Scene Blocker", canvas).gameObject.AddComponent<Image>(); fill.color = alongsideRace ? Color.clear : new Color(.035f, .05f, .09f); fill.raycastTarget = true;
            background = Stretch("Background Image", canvas).gameObject.AddComponent<RawImage>(); background.raycastTarget = false;
            background.gameObject.AddComponent<AspectRatioFitter>().aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            Stretch("Background Dim", canvas).gameObject.AddComponent<Image>().raycastTarget = false;
            content = new GameObject("Safe Content", typeof(RectTransform)).GetComponent<RectTransform>(); content.SetParent(canvas, false);
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f);
            float width = VoxelPostRaceContinue.PanelWidth;
            content.sizeDelta = new Vector2(width + RacePanelInset, 1080f);
            var panel = Panel(content, "Briefing Panel", RacePanelInset, 20, width, 1040, new Color(.043f, .078f, .118f));
            panel.useGradient = true;
            panel.topColor = new Color(.085f, .145f, .19f);
            Accent(content, 55, 66);
            Label(content, "Briefing Heading", "MISSION BRIEFING", 37, 134, 48, 490, 56).color = Ice;
            title = Label(content, "Mission Name", "", 76, 52, 109, 590, 78);

            var points = Panel(content, "Objective Panel", 665, 50, 223, 139, new Color(.095f, .17f, .23f));
            var objectiveLabel = Label(points.transform, "Objective Label", "POINTS TO COMPLETE", 27, 8, 9, 207, 34, TextAnchor.MiddleCenter);
            objectiveLabel.color = Ice;
            objective = Label(points.transform, "Objective", "", 89, 8, 44, 207, 84, TextAnchor.MiddleCenter);
            objective.color = Ice;
            bossObjective = Label(content, "Boss Objective", "", 36, 52, 181, InnerWidth, 42);
            bossObjective.color = Ice;

            var rule = Panel(content, "Header Divider", 52, 225, InnerWidth, 2, Silver);
            rule.edgeColor = new Color(.25f, .34f, .40f);
            Label(content, "Enemies Heading", "ENEMIES YOU MAY ENCOUNTER", 37, 52, 246, InnerWidth, 50);
            BuildEnemyScroll();
            Button(content, "Back", "BACK", 52, 925, 243, 102, false, GoBack, 45);
            startButton = Button(content, "Start Mission", "START MISSION", 315, 925, 575, 102, true, Accept, 60);
            var arrow = new GameObject("Start Chevron", typeof(RectTransform)).GetComponent<RectTransform>();
            arrow.SetParent(startButton.transform, false);
            SetRect(arrow, 478, 51, 0, 0);
            for (int side = -1; side <= 1; side += 2)
            {
                var stroke = new GameObject("Chevron Stroke", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                stroke.transform.SetParent(arrow, false);
                stroke.rectTransform.sizeDelta = new Vector2(20, 3);
                stroke.rectTransform.anchoredPosition = new Vector2(-7, 7 * side);
                stroke.rectTransform.localRotation = Quaternion.Euler(0, 0, -45 * side);
                stroke.color = new Color(.02f, .04f, .075f);
                stroke.raycastTarget = false;
            }
        }

        private void BuildEnemyScroll()
        {
            var scroll = new GameObject("Enemy Scroll", typeof(RectTransform), typeof(ScrollRect));
            scroll.transform.SetParent(content, false);
            SetRect((RectTransform)scroll.transform, 52, 306, InnerWidth, ViewportHeight);
            enemyScroll = scroll.GetComponent<ScrollRect>();
            var viewport = new GameObject("Enemy Viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D));
            viewport.transform.SetParent(scroll.transform, false);
            SetRect((RectTransform)viewport.transform, 0, 0, InnerWidth, ViewportHeight);
            viewport.GetComponent<Image>().color = Color.clear;
            cards = new GameObject("Enemy Cards", typeof(RectTransform)).GetComponent<RectTransform>();
            cards.SetParent(viewport.transform, false);
            enemyScroll.viewport = (RectTransform)viewport.transform;
            enemyScroll.content = cards;
            enemyScroll.horizontal = false;
            enemyScroll.movementType = ScrollRect.MovementType.Clamped;
            enemyScroll.scrollSensitivity = 46;

            var track = new GameObject("Enemy Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar));
            track.transform.SetParent(scroll.transform, false);
            SetRect((RectTransform)track.transform, InnerWidth - 12, 0, 12, ViewportHeight);
            track.GetComponent<Image>().color = new Color(.15f, .23f, .31f);
            var slidingArea = Stretch("Sliding Area", track.transform);
            var handle = Stretch("Handle", slidingArea).gameObject.AddComponent<Image>();
            handle.color = Ice;
            var scrollbar = track.GetComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            enemyScroll.verticalScrollbar = scrollbar;
        }

        private void RebuildCards()
        {
            animatedPreviews.Clear();
            previewsStartedAt = Time.unscaledTime;
            for (int i = cards.childCount - 1; i >= 0; i--)
            {
                var child = cards.GetChild(i).gameObject;
                child.SetActive(false);
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }

            bool overflow = enemies.Count > 4;
            float width = InnerWidth - (overflow ? 26 : 0);
            float cardWidth = (width - CardGap) * .5f;
            int rows = (enemies.Count + 1) / 2;
            SetRect(enemyScroll.viewport, 0, 0, width, ViewportHeight);
            SetRect(cards, 0, 0, width, Mathf.Max(ViewportHeight, rows * (CardHeight + CardGap) - CardGap));
            enemyScroll.vertical = overflow;
            enemyScroll.verticalScrollbar.gameObject.SetActive(overflow);
            enemyScroll.StopMovement();
            for (int i = 0; i < enemies.Count; i++)
                BuildEnemyCard(enemies[i], i % 2 * (cardWidth + CardGap), i / 2 * (CardHeight + CardGap), cardWidth);
            if (enemies.Count == 0)
                Label(cards, "No Enemies", "NO HOSTILE VEHICLES REPORTED", 40, 0, 38, width, 90, TextAnchor.MiddleCenter);
            enemyScroll.verticalNormalizedPosition = 1f;
        }

        private void BuildEnemyCard(VoxelMissionBriefingEnemy enemy, float x, float y, float width)
        {
            bool unknownBoss = enemy.Source is VoxelBossDefinition;
            var card = Panel(cards, unknownBoss ? "Unknown Boss" : "Enemy - " + enemy.Name,
                x, y, width, CardHeight, new Color(.06f, .12f, .18f));
            if (unknownBoss)
            {
                Label(card.transform, "Unknown Boss", "???", 120, 16, 0, width - 32, CardHeight, TextAnchor.MiddleCenter).color = Ice;
                return;
            }

            var inset = Panel(card.transform, "Portrait Clip", 2, 2, width - 4, CardHeight - 4, Color.white);
            inset.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            float portraitHeight = CardHeight - BannerHeight - 2;
            var frame = new GameObject("Model Frame", typeof(RectTransform)).GetComponent<RectTransform>();
            frame.SetParent(inset.transform, false);
            SetRect(frame, 0, 0, width - 4, portraitHeight);
            var backdrop = Stretch("Portrait Backdrop", frame).gameObject.AddComponent<Image>();
            backdrop.color = new Color32(44, 46, 52, 255);
            backdrop.raycastTarget = false;
            var model = Stretch("Three Quarter Model", frame).gameObject.AddComponent<RawImage>();
            model.raycastTarget = false;
            var preview = settings != null ? settings.PreviewRecordFor(enemy.Source) : null;
            model.texture = preview != null
                ? preview.IsAnimated && !preview.UsesPortraitDish ? preview.AnimationImage : preview.StillImage
                : null;
            model.enabled = model.texture != null;
            if (preview != null && preview.IsAnimated)
            {
                var animated = model;
                if (preview.UsesPortraitDish)
                {
                    animated = Stretch("Spinning Radar Dish", model.transform).gameObject.AddComponent<RawImage>();
                    animated.raycastTarget = false;
                    animated.texture = preview.AnimationImage;
                    animated.rectTransform.anchorMin = preview.portraitDishPlacement.min;
                    animated.rectTransform.anchorMax = preview.portraitDishPlacement.max;
                }
                animated.uvRect = preview.FrameUv(0);
                animatedPreviews.Add((animated, preview));
            }
            if (model.texture != null)
            {
                var fit = model.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                fit.aspectRatio = preview.AspectRatio;
            }
            else
                Label(frame, "Missing Preview", "MODEL PREVIEW", 25, 0, 0, width - 4, portraitHeight, TextAnchor.MiddleCenter).color = Silver;
            Label(card.transform, "Enemy Name", enemy.Name.ToUpperInvariant(), 32, 15,
                CardHeight - BannerHeight, width - 30, BannerHeight);
        }
        private void AnimatePreviews(float elapsed)
        {
            foreach (var entry in animatedPreviews) entry.image.uvRect = entry.preview.FrameUv(elapsed);
        }
        private static VoxelGaragePanel Panel(Transform parent, string name, float x, float y, float w, float h, Color color)
        {
            var rect = new GameObject(name, typeof(RectTransform), typeof(VoxelGaragePanel)).GetComponent<RectTransform>(); rect.SetParent(parent, false); SetRect(rect, x, y, w, h);
            var graphic = rect.GetComponent<VoxelGaragePanel>(); graphic.color = color; graphic.edgeColor = Silver; graphic.raycastTarget = false; return graphic;
        }
        private static void Accent(Transform parent, float x, float y)
        {
            for (int i = 0; i < 3; i++)
            {
                var rect = new GameObject("Silver Accent", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
                SetRect(rect, x + i * 23, y, 16, 28); rect.localRotation = Quaternion.Euler(0, 0, -20);
                var image = rect.GetComponent<Image>(); image.color = Color.Lerp(Ice, Silver, i * .3f); image.raycastTarget = false;
            }
        }
        private static Text Label(Transform parent, string name, string text, int font, float x, float y, float w, float h, TextAnchor alignment = TextAnchor.MiddleLeft)
        {
            var label = VoxelMenuUi.CreateText(parent, name, text, font, alignment, Vector2.zero, Vector2.zero, new Vector2(w, h));
            SetRect(label.rectTransform, x, y, w, h); label.resizeTextForBestFit = true; label.resizeTextMinSize = Mathf.Min(22, font); label.resizeTextMaxSize = font; return label;
        }
        private static Button Button(Transform parent, string name, string text, float x, float y, float w, float h, bool primary, UnityEngine.Events.UnityAction action, int fontSize = 43)
        {
            var graphic = Panel(parent, name, x, y, w, h, primary ? Ice : new Color(.025f, .04f, .075f)); graphic.raycastTarget = true;
            var button = graphic.gameObject.AddComponent<Button>(); button.targetGraphic = graphic;
            var colors = button.colors; colors.highlightedColor = new Color(.87f, .94f, 1); colors.pressedColor = new Color(.65f, .78f, .9f); colors.selectedColor = colors.highlightedColor; button.colors = colors;
            var label = Label(graphic.transform, "Label", text, fontSize, 10, 0, w - 20, h, TextAnchor.MiddleCenter);
            if (primary) label.color = new Color(.02f, .04f, .075f); button.onClick.AddListener(action); return button;
        }
        private static RectTransform Stretch(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
        private static void SetRect(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1); rect.sizeDelta = new Vector2(width, height); rect.anchoredPosition = new Vector2(x, -y);
        }
        private void OnDisable() => Close();
        private void OnDestroy() => Close();
    }
}
