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
        private Button resumeButton;
        private bool paused;
        private float previousTimeScale;
        private bool previousAudioPause;

        public void Configure(VoxelCarController player) => target = player;

        private void OnEnable() => Active = this;

        private void Update()
        {
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
                menu.SetActive(true);
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
            backdrop.color = new Color(0f, 0f, 0f, .65f);
            Image panel = VoxelMenuUi.CreatePanel(root, "Pause Panel", centre, Vector2.zero, new Vector2(620f, 440f));
            VoxelMenuUi.CreateText(panel.transform, "Title", "PAUSED", 68, TextAnchor.MiddleCenter,
                centre, new Vector2(0f, 138f), new Vector2(560f, 90f));
            VoxelMenuUi.CreateText(panel.transform, "FPS Label", "FPS COUNTER", 34, TextAnchor.MiddleLeft,
                centre, new Vector2(-40f, 24f), new Vector2(390f, 60f));
            Image toggleImage = VoxelMenuUi.CreatePanel(panel.transform, "FPS Toggle", centre,
                new Vector2(215f, 24f), new Vector2(60f, 60f));
            toggleImage.color = new Color(.18f, .23f, .32f, 1f);
            Image checkmark = VoxelMenuUi.CreatePanel(toggleImage.transform, "Checkmark", centre,
                Vector2.zero, new Vector2(36f, 36f));
            checkmark.color = new Color(.2f, .9f, .4f);
            checkmark.raycastTarget = false;
            fpsToggle = toggleImage.gameObject.AddComponent<Toggle>();
            fpsToggle.targetGraphic = toggleImage;
            fpsToggle.graphic = checkmark;
            fpsToggle.onValueChanged.AddListener(VoxelFpsCounter.SetVisible);
            resumeButton = VoxelMenuUi.CreateButton(panel.transform, "Resume Button", "RESUME", 44,
                centre, new Vector2(0f, -84f), new Vector2(480f, 86f), () => SetPaused(false));
            VoxelMenuUi.CreateText(panel.transform, "Escape Hint", "ESC TO RESUME", 22, TextAnchor.MiddleCenter,
                centre, new Vector2(0f, -163f), new Vector2(500f, 40f));
            menu.SetActive(false);
        }
    }
}
