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
        public static VoxelMissionBriefing Active { get; private set; }
        public static bool IsShowing => Active != null && Active.shown;
        private static VoxelTrackDefinition stationaryRetryTrack;
        private static Action returnToOrigin;
        private static readonly Color Ice = new(.69f, .84f, .96f), Silver = new(.47f, .56f, .65f);
        private RectTransform canvas, content, cards;
        private RawImage background;
        private Text title, objective, objectiveLabel, objectiveSuffix, pageLabel;
        private Button startButton, backButton, previousButton, nextButton;
        private VoxelMissionBriefingSettings settings;
        private List<VoxelMissionBriefingEnemy> enemies;
        private readonly List<(RawImage image, VoxelMissionBriefingPreview preview)> animatedPreviews = new();
        private float previewsStartedAt;
        private Action launch, back;
        private bool shown, alongsideRace;
        private int page, openedFrame;
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
            enemies = VoxelMissionBriefingContent.Enemies(track); page = 0;
            if (canvas == null) Build();
            bool boss = track != null && track.boss != null;
            title.text = VoxelTrackDefinition.ResolveDisplayName(track).ToUpperInvariant();
            objectiveLabel.text = boss ? "MISSION OBJECTIVE" : "SCORE OBJECTIVE";
            objective.text = VoxelMissionBriefingContent.Objective(track);
            objective.fontSize = boss ? 56 : alongsideRace ? 84 : 128;
            objective.resizeTextMaxSize = objective.fontSize;
            if (alongsideRace)
                SetRect(objective.rectTransform, 50, 316, boss ? 800 : 360, 92);
            else
                SetRect(objective.rectTransform, 535, 271, boss ? 1030 : 340, 108);
            objectiveSuffix.gameObject.SetActive(!boss);
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
            if (alongsideRace)
            {
                float panelWidth = VoxelPostRaceContinue.PanelWidth;
                float scale = Mathf.Min(width * .58f / panelWidth, height / 1080f);
                content.localScale = Vector3.one * scale;
                Vector2 center = new(safe.xMin * ratio.x + content.sizeDelta.x * .5f * scale, safe.center.y * ratio.y);
                content.anchoredPosition = center - size * .5f;
                return;
            }
            content.localScale = Vector3.one * Mathf.Min(width / 1920f, height / 1080f);
            content.anchoredPosition = Vector2.Scale(safe.center, ratio) - size * .5f;
        }
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
            content.anchorMin = content.anchorMax = content.pivot = new Vector2(.5f, .5f); content.sizeDelta = new Vector2(1920, 1080);
            cards = new GameObject("Enemy Cards", typeof(RectTransform)).GetComponent<RectTransform>(); cards.SetParent(content, false);
            cards.anchorMin = cards.anchorMax = cards.pivot = new Vector2(.5f, .5f);
            if (alongsideRace)
            {
                BuildRacePanel();
                return;
            }
            Accent(content, 60, 58);
            Label(content, "Briefing Heading", "MISSION BRIEFING", 48, 145, 32, 1200, 76).color = Ice;
            title = Label(content, "Mission Name", "", 140, 60, 104, 1800, 148);
            Panel(content, "Objective Panel", 54, 260, 1560, 140, new Color(.025f, .04f, .075f, .95f));
            objectiveLabel = Label(content, "Objective Label", "", 52, 90, 271, 425, 108);
            objective = Label(content, "Objective", "", 128, 535, 271, 340, 108); objective.color = Ice;
            objectiveSuffix = Label(content, "Objective Suffix", "POINTS\nTO COMPLETE", 48, 900, 271, 670, 108);
            Accent(content, 60, 425);
            Label(content, "Enemies Heading", "ENEMIES YOU MAY ENCOUNTER", 52, 145, 404, 1300, 65);
            var rule = Panel(content, "Silver Rule", 1460, 439, 400, 2, Silver); rule.edgeColor = Silver;
            cards.sizeDelta = content.sizeDelta;
            backButton = Button(content, "Back", "<  BACK", 54, 930, 360, 124, false, GoBack, 64);
            startButton = Button(content, "Start Mission", "START MISSION  >", 1264, 930, 600, 124, true, Accept, 64);
            previousButton = Button(content, "Previous Enemies", "<", 1510, 407, 70, 58, false, () => { page--; RebuildCards(); });
            nextButton = Button(content, "Next Enemies", ">", 1790, 407, 70, 58, false, () => { page++; RebuildCards(); });
            pageLabel = Label(content, "Enemy Page", "", 28, 1585, 407, 200, 58, TextAnchor.MiddleCenter);
        }

        private void BuildRacePanel()
        {
            float width = VoxelPostRaceContinue.PanelWidth;
            content.sizeDelta = new Vector2(width + 20f, 1080f);
            cards.sizeDelta = content.sizeDelta;
            Panel(content, "Briefing Panel", 20, 20, width, 1040, new Color(.015f, .035f, .065f, .93f));
            cards.SetAsLastSibling();
            Accent(content, 50, 64);
            Label(content, "Briefing Heading", "MISSION BRIEFING", 45, 135, 42, 705, 74).color = Ice;
            title = Label(content, "Mission Name", "", 92, 50, 113, 800, 130);
            Panel(content, "Objective Panel", 44, 248, 820, 174, new Color(.025f, .04f, .075f, .95f));
            objectiveLabel = Label(content, "Objective Label", "", 42, 56, 260, 780, 56);
            objective = Label(content, "Objective", "", 84, 50, 316, 360, 92);
            objective.color = Ice;
            objectiveSuffix = Label(content, "Objective Suffix", "POINTS TO COMPLETE", 36, 425, 316, 413, 92);
            Label(content, "Enemies Heading", "ENEMIES YOU MAY ENCOUNTER", 39, 48, 448, 520, 52);
            previousButton = Button(content, "Previous Enemies", "<", 576, 444, 65, 58, false, () => { page--; RebuildCards(); });
            nextButton = Button(content, "Next Enemies", ">", 792, 444, 65, 58, false, () => { page++; RebuildCards(); });
            pageLabel = Label(content, "Enemy Page", "", 32, 648, 444, 137, 58, TextAnchor.MiddleCenter);
            backButton = Button(content, "Back", "<  BACK", 44, 924, 240, 106, false, GoBack, 44);
            startButton = Button(content, "Start Mission", "START MISSION  >", 302, 924, 562, 106, true, Accept, 62);
        }

        private void RebuildCards()
        {
            animatedPreviews.Clear();
            previewsStartedAt = Time.unscaledTime;
            for (int i = cards.childCount - 1; i >= 0; i--)
            { var child = cards.GetChild(i).gameObject; child.SetActive(false); if (Application.isPlaying) Destroy(child); else DestroyImmediate(child); }
            int pageSize = alongsideRace ? 2 : 4;
            int pages = Mathf.Max(1, Mathf.CeilToInt(enemies.Count / (float)pageSize)); page = Mathf.Clamp(page, 0, pages - 1);
            previousButton.gameObject.SetActive(pages > 1); nextButton.gameObject.SetActive(pages > 1); pageLabel.gameObject.SetActive(pages > 1);
            previousButton.interactable = page > 0; nextButton.interactable = page < pages - 1; pageLabel.text = (page + 1) + " / " + pages;
            int count = Mathf.Min(pageSize, enemies.Count - page * pageSize);
            float width = alongsideRace ? 820 : count > 0 ? Mathf.Min(540, (1812 - 22 * (count - 1)) / count) : 540;
            float left = (1920 - (count * width + (count - 1) * 22)) * .5f;
            for (int i = 0; i < count; i++)
            {
                var enemy = enemies[page * pageSize + i];
                bool unknownBoss = enemy.Source is VoxelBossDefinition;
                var card = Panel(cards, unknownBoss ? "Unknown Boss" : "Enemy - " + enemy.Name,
                    alongsideRace ? 44 : left + i * (width + 22), alongsideRace ? 514 + 192 * i : 480,
                    width, alongsideRace ? 176 : 412, new Color(.025f, .04f, .07f, .96f));
                if (unknownBoss)
                {
                    Label(card.transform, "Unknown Boss", "???", alongsideRace ? 132 : 240, 20,
                        alongsideRace ? 10 : 40, width - 40, alongsideRace ? 156 : 330, TextAnchor.MiddleCenter).color = Ice;
                    continue;
                }
                var frame = new GameObject("Model Frame", typeof(RectTransform)).GetComponent<RectTransform>(); frame.SetParent(card.transform, false);
                SetRect(frame, 8, 12, alongsideRace ? 270 : width - 16, alongsideRace ? 152 : 280);
                var model = Stretch("Three Quarter Model", frame).gameObject.AddComponent<RawImage>(); model.raycastTarget = false;
                var preview = settings != null ? settings.PreviewRecordFor(enemy.Source) : null;
                model.texture = preview != null ? preview.IsAnimated ? preview.animation : preview.image : null;
                model.enabled = model.texture != null;
                if (preview != null && preview.IsAnimated)
                { model.uvRect = preview.FrameUv(0); animatedPreviews.Add((model, preview)); }
                if (model.texture != null) { var fit = model.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = preview.AspectRatio; }
                else Label(card.transform, "Missing Preview", "MODEL PREVIEW", 25, 20, alongsideRace ? 55 : 125,
                    alongsideRace ? 252 : width - 40, 65, TextAnchor.MiddleCenter).color = Silver;
                float textX = alongsideRace ? 296 : 28, textWidth = width - textX - 28;
                Label(card.transform, "Enemy Name", enemy.Name.ToUpperInvariant(), 39, textX, alongsideRace ? 25 : 296, textWidth, 56);
                var line = Panel(card.transform, "Card Rule", textX, alongsideRace ? 88 : 358, textWidth, 1, Silver); line.edgeColor = Silver;
                Label(card.transform, "Threat", enemy.Trait, 27, textX, alongsideRace ? 100 : 366, textWidth, 38).color = new Color(.65f, .71f, .79f);
            }
            if (count == 0) Label(cards, "No Enemies", "NO HOSTILE VEHICLES REPORTED", 44,
                alongsideRace ? 50 : 160, 580, alongsideRace ? 800 : 1600, 120, TextAnchor.MiddleCenter);
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
