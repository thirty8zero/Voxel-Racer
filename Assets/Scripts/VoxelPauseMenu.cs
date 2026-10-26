using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>Owns the race pause, keeping menu input and the FPS display live.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class VoxelPauseMenu : MonoBehaviour
    {
        public static VoxelPauseMenu Active { get; private set; }
        public static bool IsPaused => Active != null && Active.paused;

        private VoxelCarController target;
        private GameObject menu;
        private Toggle fpsToggle;
        private Toggle waveToggle;
        private Toggle controlSchemeToggle;
        private Button resumeButton;
        private bool paused;
        private float previousTimeScale;
        private bool previousAudioPause;

        public void Configure(VoxelCarController player) => target = player;

        private void OnEnable() => Active = this;

        private void Update()
        {
            if (VoxelLoadingScreen.IsLoading) return;
            if (!Application.isPlaying) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetPaused(!paused);
        }

        public void SetPaused(bool value)
        {
            if (value == paused) return;
            if (value)
            {
                if (!Application.isPlaying || target == null || !target.isActiveAndEnabled || target.IsDestroyed ||
                    VoxelPlayerDeathScreen.IsShowing || VoxelMissionProgress.Active?.IsComplete == true ||
                    VoxelMissionProgress.Active?.IsFailed == true || Time.timeScale <= 0f)
                    return;
                if (menu == null) BuildMenu();
                previousTimeScale = Time.timeScale;
                previousAudioPause = AudioListener.pause;
                paused = true;
                Time.timeScale = 0f;
                AudioListener.pause = true;
                VoxelMobileControls.ClearHeldInput();
                fpsToggle.SetIsOnWithoutNotify(VoxelFpsCounter.ShowCounter);
                waveToggle.SetIsOnWithoutNotify(VoxelFpsCounter.ShowWaveDebug);
                controlSchemeToggle.SetIsOnWithoutNotify(VoxelMobileControls.UseControlSchemeB);
                menu.SetActive(true);
                Canvas.ForceUpdateCanvases();
                EventSystem.current?.SetSelectedGameObject(resumeButton.gameObject);
            }
            else
            {
                paused = false;
                Time.timeScale = previousTimeScale;
                AudioListener.pause = previousAudioPause;
                VoxelMobileControls.ClearHeldInput();
                if (menu != null) menu.SetActive(false);
                EventSystem.current?.SetSelectedGameObject(null);
            }
        }

        public static void ResumeActivePause() => Active?.SetPaused(false);

        private void ResetRun()
        {
            if (paused) VoxelCarRunState.ResetAndReturnToMainMenu();
        }

        private void OnDisable()
        {
            SetPaused(false);
            if (Active == this) Active = null;
        }

        private void BuildMenu()
        {
            RectTransform root = VoxelMenuUi.CreateCanvas(transform, "Pause Menu");
            root.GetComponent<Canvas>().sortingOrder = 11000;
            menu = root.gameObject;
            var centre = new Vector2(.5f, .5f);
            Image backdrop = VoxelMenuUi.CreatePanel(root, "Pause Backdrop", centre, Vector2.zero, Vector2.zero);
            backdrop.rectTransform.anchorMin = Vector2.zero;
            backdrop.rectTransform.anchorMax = Vector2.one;
            backdrop.rectTransform.offsetMin = backdrop.rectTransform.offsetMax = Vector2.zero;
            backdrop.color = new Color(0f, 0f, 0f, .70f);
            Decoration(root, "Card Shadow", new Vector2(10, -12), new Vector2(768, 920), new Color(0, 0, 0, .4f));
            Decoration(root, "Card Border", Vector2.zero, new Vector2(764, 916), new Color(.15f, .23f, .29f));
            Image panel = VoxelMenuUi.CreatePanel(root, "Pause Panel", centre, Vector2.zero, new Vector2(760f, 912f));
            panel.color = new Color(.035f, .045f, .065f, .99f);
            var cyan = new Color(.28f, .80f, 1f);
            var muted = new Color(.54f, .62f, .70f);
            Decoration(panel.transform, "Top Accent", new Vector2(0, 454), new Vector2(760, 4), cyan);
            VoxelMenuUi.CreateText(panel.transform, "Brand", "ROAD SPECTRE", 25, TextAnchor.MiddleLeft,
                centre, new Vector2(0, 388), new Vector2(648, 38)).color = cyan;
            VoxelMenuUi.CreateText(panel.transform, "Title", "PAUSED", 108, TextAnchor.MiddleLeft,
                centre, new Vector2(0, 304), new Vector2(648, 126));
            VoxelMenuUi.CreateText(panel.transform, "Pause Symbol", "II", 74, TextAnchor.MiddleCenter,
                centre, new Vector2(286, 304), new Vector2(80, 90)).color = cyan;
            Decoration(panel.transform, "Header Divider", new Vector2(0, 222), new Vector2(648, 2), new Color(.15f, .23f, .29f));
            fpsToggle = CreateSettingRow(panel.transform, "FPS Toggle", "FPS COUNTER", "SHOW FRAME RATE", 156f);
            fpsToggle.onValueChanged.AddListener(VoxelFpsCounter.SetVisible);
            waveToggle = CreateSettingRow(panel.transform, "Wave Debug Toggle", "WAVE DEBUG", "SHOW TRAFFIC SPAWN DETAILS", 54f);
            waveToggle.onValueChanged.AddListener(VoxelFpsCounter.SetWaveDebugVisible);
            controlSchemeToggle = CreateSettingRow(panel.transform, "Control Scheme B Toggle", "Control Scheme B", "SWAP BRAKE AND STEERING POSITIONS", -48f);
            controlSchemeToggle.SetIsOnWithoutNotify(VoxelMobileControls.UseControlSchemeB);
            controlSchemeToggle.onValueChanged.AddListener(VoxelMobileControls.SetControlSchemeB);
            resumeButton = VoxelMenuUi.CreateButton(panel.transform, "Resume Button", "RESUME", 50,
                centre, new Vector2(0f, -177f), new Vector2(648f, 100f), () => SetPaused(false));
            resumeButton.image.color = cyan;
            resumeButton.GetComponentInChildren<Text>().color = new Color(.015f, .035f, .055f);
            var colours = resumeButton.colors;
            colours.highlightedColor = Color.white; colours.selectedColor = Color.white;
            colours.pressedColor = new Color(.68f, .82f, .90f);
            resumeButton.colors = colours;
            Button reset = VoxelMenuUi.CreateButton(panel.transform, "Reset Run Button", "RESET RUN", 32,
                centre, new Vector2(0f, -284f), new Vector2(648f, 74f), ResetRun);
            reset.image.color = new Color(.14f, .07f, .085f);
            reset.GetComponentInChildren<Text>().color = new Color(1f, .55f, .52f);
            VoxelMenuUi.CreateText(panel.transform, "Reset Run Description", "CLEARS CASH, UPGRADES AND PROGRESS\nRETURNS TO MAIN MENU", 20,
                TextAnchor.MiddleCenter, centre, new Vector2(0f, -358f), new Vector2(648f, 56f)).color = muted;
            Decoration(panel.transform, "Escape Key", new Vector2(-77, -417), new Vector2(54, 30), new Color(.12f, .16f, .21f));
            VoxelMenuUi.CreateText(panel.transform, "Escape Key Label", "ESC", 19, TextAnchor.MiddleCenter,
                centre, new Vector2(-77, -417), new Vector2(54, 30)).color = muted;
            VoxelMenuUi.CreateText(panel.transform, "Escape Hint", "TO RESUME", 20, TextAnchor.MiddleLeft,
                centre, new Vector2(45, -417), new Vector2(150, 32)).color = muted;
            menu.SetActive(false);
        }

        private static Image Decoration(Transform parent, string name, Vector2 position, Vector2 size, Color colour)
        {
            var image = VoxelMenuUi.CreatePanel(parent, name, new Vector2(.5f, .5f), position, size);
            image.color = colour; image.raycastTarget = false;
            return image;
        }

        private static Toggle CreateSettingRow(Transform parent, string name, string label, string description, float y)
        {
            var centre = new Vector2(.5f, .5f);
            var row = VoxelMenuUi.CreatePanel(parent, name, centre, new Vector2(0, y), new Vector2(648, 88));
            row.color = new Color(.065f, .085f, .115f);
            VoxelMenuUi.CreateText(row.transform, "Label", label, 30, TextAnchor.MiddleLeft,
                centre, new Vector2(-53, 15), new Vector2(486, 38));
            VoxelMenuUi.CreateText(row.transform, "Description", description, 19, TextAnchor.MiddleLeft,
                centre, new Vector2(-53, -20), new Vector2(486, 27)).color = new Color(.54f, .62f, .70f);
            var box = Decoration(row.transform, "Toggle Box", new Vector2(265, 0), new Vector2(48, 48), new Color(.18f, .26f, .34f));
            var check = Decoration(box.transform, "Checkmark", Vector2.zero, new Vector2(26, 26), new Color(.28f, .80f, 1f));
            var toggle = row.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box; toggle.graphic = check;
            return toggle;
        }
    }
}
