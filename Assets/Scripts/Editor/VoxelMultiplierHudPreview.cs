using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace VoxelRacer.Editor
{
    /// <summary>Exercises the real multiplier HUD without touching a run, purchases or cash.</summary>
    public sealed class VoxelMultiplierHudPreview : EditorWindow
    {
        private GameObject temporary;
        private VoxelMissionProgress mission;
        private VoxelMissionTuning tuning;
        private VoxelMissionTimerDisplay timer;
        private Scene previewScene;
        private Camera camera;
        private RenderTexture image;
        private GameObject createdEvents;
        private Image ring;
        private Text timerLabel;
        private CanvasGroup group;
        private double feedbackUntil, extensionAt = double.NegativeInfinity;
        private readonly MethodInfo refresh = typeof(VoxelMissionProgress).GetMethod("RefreshInstrumentCluster", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly MethodInfo dial = typeof(VoxelMissionTimerDisplay).GetMethod("SetDialScale", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly MethodInfo colour = typeof(VoxelMissionTimerDisplay).GetMethod("GetTimerColour", BindingFlags.Static | BindingFlags.NonPublic);
        private readonly MethodInfo tick = typeof(VoxelMissionProgress).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);

        [MenuItem("Tools/Voxel Racer/Preview Live Multiplier HUD")]
        public static void Open()
        {
            if (Application.isPlaying) { Debug.Log("Open the instrument preview from Edit Mode."); return; }
            GetWindow<VoxelMultiplierHudPreview>("Live Multiplier Preview").Show();
        }
        private void OnEnable()
        {
            minSize = new Vector2(1400, 550);
            previewScene = EditorSceneManager.NewPreviewScene();
            temporary = new GameObject("Temporary Multiplier HUD Preview") { hideFlags = HideFlags.HideAndDontSave };
            SceneManager.MoveGameObjectToScene(temporary, previewScene);
            mission = temporary.AddComponent<VoxelMissionProgress>();
            mission.enabled = false;
            tuning = CreateInstance<VoxelMissionTuning>();
            tuning.hideFlags = HideFlags.HideAndDontSave;
            ResetPreview();
            var events = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            timer = temporary.AddComponent<VoxelMissionTimerDisplay>(); timer.enabled = false;
            typeof(VoxelMissionTimerDisplay).GetMethod("BuildHud", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(timer, null);
            if (events == null) createdEvents = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>()?.gameObject;
            timer.Configure(mission);
            var canvas = temporary.GetComponentInChildren<Canvas>();
            group = canvas.GetComponent<CanvasGroup>();
            ring = canvas.transform.Find("Timer Ring").GetComponent<Image>();
            timerLabel = canvas.transform.Find("Timer Text").GetComponent<Text>();
            camera = new GameObject("Instrument preview camera").AddComponent<Camera>(); camera.transform.SetParent(temporary.transform, false);
            camera.scene = previewScene; camera.enabled = false; camera.cullingMask = 1 << 31;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .09f, .12f);
            camera.nearClipPlane = .1f; camera.farClipPlane = 10;
            image = new RenderTexture(1920, 1080, 24); camera.targetTexture = image;
            canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = 1;
            foreach (var item in temporary.GetComponentsInChildren<Transform>(true)) item.gameObject.layer = 31;
            EditorApplication.update += Repaint;
            EditorApplication.playModeStateChanged += OnPlayMode;
        }
        public void ResetPreview()
        {
            tuning.requiredPoints = 1200;
            tuning.timeBonusCurrencyMultiplier = 2.35f;
            mission.Configure(tuning);
            typeof(VoxelMissionProgress).GetProperty("Points").SetValue(mission, 986);
            feedbackUntil = 0; extensionAt = double.NegativeInfinity;
        }
        public void ShowExpiry() => mission.AdvanceBonusClock(mission.RemainingTime);
        private void ChangePreview(float amount, string reason)
        {
            mission.ChangeMultiplier(amount, reason);
            // This window inspects the native readout; real reward flights are exercised in Play Mode.
            ((System.Collections.IList)typeof(VoxelMissionProgress).GetField("multiplierFlights", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(mission)).Clear();
            typeof(VoxelMissionProgress).GetField("displayedMultiplierBonus", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mission, mission.EffectiveTimeBonusMultiplier);
            typeof(VoxelMissionProgress).GetField("lastMultiplierWasNegative", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mission, amount < 0);
            feedbackUntil = EditorApplication.timeSinceStartup + .32;
        }
        private void OnGUI()
        {
            if (mission == null) return;
            EditorGUI.DrawRect(new Rect(0, 0, position.width, position.height), new Color(.07f, .09f, .12f));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Enemy voxels +0.10")) ChangePreview(.1f, "ENEMY VOXELS");
            if (GUILayout.Button("Civilian hit -0.10")) ChangePreview(-.1f, "CIVILIAN DAMAGE");
            if (GUILayout.Button("Civilian destroyed -1.00")) ChangePreview(-1, "CIVILIAN DESTROYED");
            if (GUILayout.Button("Last 8 seconds")) mission.AdvanceBonusClock(Mathf.Max(0, mission.RemainingTime - 8));
            if (GUILayout.Button("Expire")) ShowExpiry();
            if (GUILayout.Button("Overtime +1 second")) { ShowExpiry(); mission.AdvanceBonusClock(1); feedbackUntil = EditorApplication.timeSinceStartup + .32; }
            if (GUILayout.Button("+15 seconds") && mission.AddBonusTime(15)) extensionAt = EditorApplication.timeSinceStartup;
            if (GUILayout.Button("Reset")) ResetPreview();
            GUILayout.EndHorizontal();
            tick.Invoke(mission, null);
            typeof(VoxelMissionProgress).GetField("multiplierPulseUntil", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(mission,
                Time.unscaledTime + Mathf.Max(0, (float)(feedbackUntil - EditorApplication.timeSinceStartup)));
            bool expired = mission.RemainingTime <= 0;
            float extension = Mathf.Clamp01(1 - (float)(EditorApplication.timeSinceStartup - extensionAt) / 1.5f);
            ring.fillAmount = Mathf.Clamp01(mission.RemainingTime / tuning.timeLimitSeconds);
            ring.color = Color.Lerp((Color)colour.Invoke(null, new object[] { ring.fillAmount }), Color.cyan, extension);
            float clock = (float)EditorApplication.timeSinceStartup;
            float scale = !expired && mission.RemainingTime <= 10 ? 1.08f + Mathf.Sin(clock * 14) * .1f : 1;
            scale += .25f * extension * Mathf.Abs(Mathf.Sin(clock * 12));
            dial.Invoke(timer, new object[] { scale, expired });
            int seconds = Mathf.CeilToInt(mission.RemainingTime);
            timerLabel.text = expired ? "TIME'S\nUP!" : seconds / 60 + ":" + (seconds % 60).ToString("00");
            timerLabel.fontSize = timerLabel.resizeTextMaxSize = expired ? 44 : VoxelMissionTimerDisplay.CountdownFontSize;
            refresh.Invoke(mission, new object[] { temporary.GetComponentInChildren<VoxelMissionInstrumentCluster>() });
            group.alpha = 1;
            Canvas.ForceUpdateCanvases(); camera.Render();
            float width = Mathf.Min(position.width - 40, (position.height - 80) * 2);
            GUI.DrawTextureWithTexCoords(new Rect((position.width - width) * .5f, 60, width, width * .5f), image,
                new Rect(1200f / 1920, 720f / 1080, 720f / 1920, 360f / 1080));
        }
        private void OnPlayMode(PlayModeStateChange state) { if (state == PlayModeStateChange.ExitingEditMode) Close(); }
        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
            EditorApplication.playModeStateChanged -= OnPlayMode;
            if (camera != null) camera.targetTexture = null;
            if (temporary != null) DestroyImmediate(temporary);
            if (previewScene.IsValid()) EditorSceneManager.ClosePreviewScene(previewScene);
            if (image != null) { image.Release(); DestroyImmediate(image); }
            if (createdEvents != null) DestroyImmediate(createdEvents);
            if (tuning != null) DestroyImmediate(tuning);
        }
    }
}
