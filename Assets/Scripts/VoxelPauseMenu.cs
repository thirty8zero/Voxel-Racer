using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
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
        private Button devButton;
        private Button devBackButton;
        private GameObject settingsPage, devPage;
        private Text pageTitle, devFeedback;
        private bool paused;
        private float previousTimeScale;
        private bool previousAudioPause;

        public void Configure(VoxelCarController player) => target = player;

        private void OnEnable() => Active = this;

        private void Update()
        {
            if (VoxelLoadingScreen.IsLoading || VoxelMissionBriefing.IsShowing) return;
            if (!Application.isPlaying) return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetPaused(!paused);
        }

        public void SetPaused(bool value)
        {
            if (value == paused) return;
            if (value)
            {
                if (VoxelMissionBriefing.IsShowing) return;
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
                ShowDevPage(false);
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

        private void SkipMission()
        {
            if (!CanUseDeveloperActions) return;

            int buildIndex = SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/RepairUpgrade.unity");
            if (buildIndex < 0)
            {
                Debug.LogError("Repair scene is not enabled in Build Settings: RepairUpgrade");
                return;
            }

            VoxelCarRunState.Capture(target);
            // As with a normal finish, the garage retains this mission's repair prices
            // and advances the campaign only when its next-mission button is pressed.
            SetPaused(false);
            VoxelLoadingScreen.LoadScene(buildIndex);
        }

        private bool CanUseDeveloperActions => paused && devPage != null && devPage.activeSelf &&
            !VoxelLoadingScreen.IsLoading && target != null && target.isActiveAndEnabled && !target.IsDestroyed &&
            !VoxelPlayerDeathScreen.IsShowing && VoxelMissionProgress.Active?.IsComplete != true &&
            VoxelMissionProgress.Active?.IsFailed != true;

        private void RestoreHealth()
        {
            if (!CanUseDeveloperActions) return;
            VoxelDeveloperActions.RestoreHealth(target);
            devFeedback.text = "HEALTH RESTORED TO 100%";
        }

        private void GiveAllUpgrades()
        {
            if (!CanUseDeveloperActions) return;
            VoxelDeveloperActions.GiveAllUpgrades(target);
            devFeedback.text = "ALL COMPATIBLE UPGRADES FITTED";
        }

        private void ShowDevPage(bool show)
        {
            settingsPage.SetActive(!show);
            devPage.SetActive(show);
            pageTitle.text = show ? "DEV" : "PAUSED";
            devFeedback.text = string.Empty;
            EventSystem.current?.SetSelectedGameObject(show ? devBackButton.gameObject : devButton.gameObject);
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
            pageTitle = VoxelMenuUi.CreateText(panel.transform, "Title", "PAUSED", 108, TextAnchor.MiddleLeft,
                centre, new Vector2(0, 304), new Vector2(648, 126));
            VoxelMenuUi.CreateText(panel.transform, "Pause Symbol", "II", 74, TextAnchor.MiddleCenter,
                centre, new Vector2(286, 304), new Vector2(80, 90)).color = cyan;
            Decoration(panel.transform, "Header Divider", new Vector2(0, 222), new Vector2(648, 2), new Color(.15f, .23f, .29f));
            settingsPage = CreatePage(panel.transform, "Settings Page");
            devPage = CreatePage(panel.transform, "DEV Page");
            Transform settings = settingsPage.transform;
            fpsToggle = CreateSettingRow(settings, "FPS Toggle", "FPS COUNTER", "SHOW FRAME RATE", 156f);
            fpsToggle.onValueChanged.AddListener(VoxelFpsCounter.SetVisible);
            waveToggle = CreateSettingRow(settings, "Wave Debug Toggle", "WAVE DEBUG", "SHOW TRAFFIC SPAWN DETAILS", 54f);
            waveToggle.onValueChanged.AddListener(VoxelFpsCounter.SetWaveDebugVisible);
            controlSchemeToggle = CreateSettingRow(settings, "Control Scheme B Toggle", "Control Scheme B", "SWAP BRAKE AND STEERING POSITIONS", -48f);
            controlSchemeToggle.SetIsOnWithoutNotify(VoxelMobileControls.UseControlSchemeB);
            controlSchemeToggle.onValueChanged.AddListener(VoxelMobileControls.SetControlSchemeB);
            resumeButton = VoxelMenuUi.CreateButton(settings, "Resume Button", "RESUME", 50,
                centre, new Vector2(0f, -159f), new Vector2(648f, 82f), () => SetPaused(false));
            resumeButton.image.color = cyan;
            resumeButton.GetComponentInChildren<Text>().color = new Color(.015f, .035f, .055f);
            var colours = resumeButton.colors;
            colours.highlightedColor = Color.white; colours.selectedColor = Color.white;
            colours.pressedColor = new Color(.68f, .82f, .90f);
            resumeButton.colors = colours;
            devButton = VoxelMenuUi.CreateButton(settings, "DEV Button", "DEV", 32,
                centre, new Vector2(0f, -247f), new Vector2(648f, 66f), () => ShowDevPage(true));
            devButton.image.color = new Color(.10f, .16f, .22f);
            devButton.GetComponentInChildren<Text>().color = cyan;
            VoxelMenuUi.CreateText(settings, "DEV Description", "DEVELOPER TOOLS", 19,
                TextAnchor.MiddleCenter, centre, new Vector2(0f, -302f), new Vector2(648f, 28f)).color = muted;
            Button reset = VoxelMenuUi.CreateButton(settings, "Reset Run Button", "RESET RUN", 32,
                centre, new Vector2(0f, -357f), new Vector2(648f, 64f), ResetRun);
            reset.image.color = new Color(.14f, .07f, .085f);
            reset.GetComponentInChildren<Text>().color = new Color(1f, .55f, .52f);
            VoxelMenuUi.CreateText(settings, "Reset Run Description", "CLEARS CASH, UPGRADES AND PROGRESS\nRETURNS TO MAIN MENU", 20,
                TextAnchor.MiddleCenter, centre, new Vector2(0f, -416f), new Vector2(648f, 40f)).color = muted;
            CreateDevAction("Skip Mission Button", "SKIP MISSION", "GARAGE BEFORE NEXT MISSION - NO COMPLETION PAYOUT", 140, SkipMission);
            CreateDevAction("Restore Health Button", "RESTORE FULL HEALTH", "INSTANT BODY, TYRE AND ARMOUR REPAIR", -10, RestoreHealth);
            CreateDevAction("Give All Upgrades Button", "GIVE ALL UPGRADES", "HIGHEST COMPATIBLE LEVELS - FREE", -160, GiveAllUpgrades);
            devFeedback = VoxelMenuUi.CreateText(devPage.transform, "DEV Feedback", string.Empty, 24,
                TextAnchor.MiddleCenter, centre, new Vector2(0, -282), new Vector2(648, 40));
            devFeedback.color = new Color(.55f, 1f, .65f);
            devBackButton = VoxelMenuUi.CreateButton(devPage.transform, "DEV Back Button", "BACK", 40,
                centre, new Vector2(0, -374), new Vector2(648, 82), () => ShowDevPage(false));
            devBackButton.image.color = new Color(.10f, .16f, .22f);
            devBackButton.GetComponentInChildren<Text>().color = cyan;
            Decoration(panel.transform, "Escape Key", new Vector2(130, 388), new Vector2(54, 30), new Color(.12f, .16f, .21f));
            VoxelMenuUi.CreateText(panel.transform, "Escape Key Label", "ESC", 19, TextAnchor.MiddleCenter,
                centre, new Vector2(130, 388), new Vector2(54, 30)).color = muted;
            VoxelMenuUi.CreateText(panel.transform, "Escape Hint", "TO RESUME", 20, TextAnchor.MiddleLeft,
                centre, new Vector2(252, 388), new Vector2(150, 32)).color = muted;
            devPage.SetActive(false);
            menu.SetActive(false);

            void CreateDevAction(string name, string label, string description, float y, UnityEngine.Events.UnityAction action)
            {
                var button = VoxelMenuUi.CreateButton(devPage.transform, name, label, 36,
                    centre, new Vector2(0, y), new Vector2(648, 90), action);
                button.image.color = new Color(.10f, .16f, .22f);
                button.GetComponentInChildren<Text>().color = cyan;
                VoxelMenuUi.CreateText(devPage.transform, name + " Description", description, 19,
                    TextAnchor.MiddleCenter, centre, new Vector2(0, y - 65), new Vector2(648, 28)).color = muted;
            }
        }

        private static GameObject CreatePage(Transform parent, string name)
        {
            var page = new GameObject(name, typeof(RectTransform));
            page.transform.SetParent(parent, false);
            var rect = (RectTransform)page.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return page;
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
