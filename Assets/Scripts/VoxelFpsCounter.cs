using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    /// <summary>A persistent, optional display measured with the real frame clock.</summary>
    public sealed class VoxelFpsCounter : MonoBehaviour
    {
        public const string PreferenceKey = "VoxelRacer.ShowFps";
        public const string WavePreferenceKey = "VoxelRacer.ShowWaveDebug";
        public static VoxelFpsCounter Active { get; private set; }
        public static bool ShowCounter => PlayerPrefs.GetInt(PreferenceKey, 1) != 0;
        public static bool ShowWaveDebug => PlayerPrefs.GetInt(WavePreferenceKey, 1) != 0;
        public int FramesPerSecond { get; private set; }

        private Text label;
        private Text waveLabel;
        private float nextWaveRefresh;
        private Canvas canvas;
        private float sampleDuration;
        private int sampleFrames;

        public static void EnsureExists()
        {
            if (Active == null)
                new GameObject("FPS Counter").AddComponent<VoxelFpsCounter>();
        }

        private void Awake()
        {
            if (Active != null && Active != this)
            {
                Destroy(gameObject);
                return;
            }
            Active = this;
            DontDestroyOnLoad(gameObject);
            RectTransform root = VoxelMenuUi.CreateCanvas(transform, "FPS Canvas");
            canvas = root.GetComponent<Canvas>();
            canvas.sortingOrder = 12000;
            Destroy(root.GetComponent<GraphicRaycaster>());
            label = VoxelMenuUi.CreateText(root, "FPS", "FPS: --", 24,
                TextAnchor.MiddleCenter, new Vector2(.5f, 0f), new Vector2(0f, 20f), new Vector2(180f, 32f));
            var shadow = label.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, .85f);
            shadow.effectDistance = new Vector2(1f, -1f);
            waveLabel = VoxelMenuUi.CreateText(root, "Wave Debug", "", 24,
                TextAnchor.MiddleCenter, new Vector2(.5f, 0f), new Vector2(0f, 56f), new Vector2(900f, 32f));
            waveLabel.color = new Color(.4f, .85f, 1f);
            var waveShadow = waveLabel.gameObject.AddComponent<Shadow>();
            waveShadow.effectColor = shadow.effectColor;
            waveShadow.effectDistance = shadow.effectDistance;
            RefreshVisibility();
        }

        public static void SetVisible(bool visible)
        {
            PlayerPrefs.SetInt(PreferenceKey, visible ? 1 : 0);
            PlayerPrefs.Save();
            Active?.RefreshVisibility();
        }

        public static void SetWaveDebugVisible(bool visible)
        {
            PlayerPrefs.SetInt(WavePreferenceKey, visible ? 1 : 0);
            PlayerPrefs.Save();
            Active?.RefreshVisibility();
        }

        private void RefreshVisibility()
        {
            if (canvas == null) return;
            label.enabled = ShowCounter;
            waveLabel.enabled = ShowWaveDebug && VoxelObstacleSpawner.Active != null;
            canvas.enabled = label.enabled || waveLabel.enabled;
            nextWaveRefresh = 0f;
            sampleDuration = 0f;
            sampleFrames = 0;
            if (label != null) label.text = "FPS: --";
        }

        private void Update()
        {
            if (canvas == null) return;
            waveLabel.enabled = ShowWaveDebug && VoxelObstacleSpawner.Active != null;
            canvas.enabled = ShowCounter || waveLabel.enabled;
            if (!canvas.enabled) return;
            // Place the baseline just inside the device's bottom safe area.
            float scale = Mathf.Max(.01f, canvas.scaleFactor);
            label.rectTransform.anchoredPosition = new Vector2(0f, Screen.safeArea.yMin / scale + 20f);
            waveLabel.rectTransform.anchoredPosition = new Vector2(0f, Screen.safeArea.yMin / scale + 56f);
            var spawner = VoxelObstacleSpawner.Active;
            waveLabel.enabled = ShowWaveDebug && spawner != null;
            if (waveLabel.enabled && Time.unscaledTime >= nextWaveRefresh)
            {
                nextWaveRefresh = Time.unscaledTime + .1f;
                string state = spawner.SpawningFinished ? "FINISHED" : spawner.WaitingForOpening ? "WAITING FOR START" :
                    "IN " + spawner.NextWaveSeconds.ToString("0.0") + "s" + (VoxelPauseMenu.IsPaused ? " (PAUSED)" : "");
                waveLabel.text = "WAVE " + (spawner.WavesSpawned + 1) + " " + state +
                    "  |  SPAWNED " + spawner.WavesSpawned + "  |  QUEUED " + spawner.QueuedObjects;
            }
            if (!ShowCounter) return;
            sampleDuration += Time.unscaledDeltaTime;
            sampleFrames++;
            if (sampleDuration < .5f) return;
            FramesPerSecond = Mathf.RoundToInt(sampleFrames / sampleDuration);
            label.text = "FPS: " + FramesPerSecond;
            sampleDuration = 0f;
            sampleFrames = 0;
        }

        private void OnDestroy()
        {
            if (Active == this) Active = null;
        }
    }
}
