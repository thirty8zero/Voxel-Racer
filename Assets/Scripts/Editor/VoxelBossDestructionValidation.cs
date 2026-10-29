using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelBossDestructionValidation
    {
        private const string Pending = "VoxelRacer.BossDestructionValidation";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        private static readonly List<string> report = new();
        private static Scene scene, previousScene;
        private static GameObject root;
        private static VoxelCarController player;
        private static VoxelObstacleCarTuning traffic;
        private static VoxelMissionProgress mission;
        private static VoxelMissionTuning missionTuning;
        private static VoxelCameraFollow cameraFollow;
        private static VoxelCameraTuning cameraTuning;
        private static VoxelRunFinish finish;
        private static Camera camera;
        private static Camera[] previousCameras;
        private static bool[] previousCameraEnabled;
        private static GameObject[] previousRoots;
        private static bool[] previousRootActive;
        private static FieldInfo[] globals;
        private static object[] globalValues;
        private static VoxelGarageUpgradeDockValidation.SavedState savedState;
        private static Dictionary<Object, string> snapshots;
        private static float previousScale, previousStep;
        private static bool previousBackground, captured, pictured, verifiedSlowMotion, verifiedHandback;
        private static UnityEngine.Random.State random;
        private static double startedAt;
        private static int warmupFrame;
        private static Case live;

        private static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Equal(float actual, float expected, string message) => Check(Mathf.Abs(actual - expected) < .001f, message + ": " + actual + " != " + expected);
        private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
        private static void Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }

        [MenuItem("Tools/Voxel Racer/Validate Boss Destruction in Play Mode")]
        public static void Run()
        {
            Check(!EditorApplication.isPlayingOrWillChangePlaymode, "Start in Edit Mode.");
            Check(SceneManager.GetActiveScene().name == "MainMenu", "Start from the idle MainMenu scene.");
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Start;
            if (state == PlayModeStateChange.ExitingPlayMode && captured) Complete(false, "Interrupted validation.");
        }

        private sealed class Case : IDisposable
        {
            public readonly VoxelBossDefinition Settings;
            public readonly VoxelEnemyVehicleTuning Tuning;
            public readonly VoxelEnemyCar Enemy;
            public readonly MeshRenderer[] Pieces;
            private readonly HashSet<GameObject> existingRoots;
            private readonly GameObject go;

            public Case()
            {
                existingRoots = scene.GetRootGameObjects().ToHashSet();
                Settings = Object.Instantiate(Resources.Load<VoxelBossDefinition>("Bosses/RedVanBoss"));
                Settings.entranceDuration = 0;
                Tuning = Settings.CreateRuntimeVehicleTuning();
                go = new GameObject("Boss QA"); go.transform.SetParent(root.transform, false);
                Enemy = go.AddComponent<VoxelEnemyCar>(); Enemy.enabled = false;
                var track = Resources.Load<VoxelTrackDefinition>("Tracks/TrackBoss01");
                Enemy.ConfigureBoss(Settings, track.roadTuning.roadWidth / track.roadTuning.laneCount, track.roadTuning.laneCount);
                Enemy.Configure(player, traffic, Tuning, null, 0, 0);
                Pieces = go.GetComponentsInChildren<MeshRenderer>();
            }
            public int Count => Pieces.Count(r => r != null && r.enabled && r.gameObject.activeInHierarchy && r.GetComponentInParent<VoxelIndestructiblePart>() == null);
            public void Dispose()
            {
                if (go != null) Object.DestroyImmediate(go);
                foreach (var obj in scene.GetRootGameObjects()) if (!existingRoots.Contains(obj)) Object.DestroyImmediate(obj);
                Object.DestroyImmediate(Tuning); Object.DestroyImmediate(Settings);
            }
        }

        private static void Start()
        {
            try
            {
                previousScene = SceneManager.GetActiveScene(); previousScale = Time.timeScale; previousStep = Time.fixedDeltaTime;
                previousBackground = Application.runInBackground; random = UnityEngine.Random.state;
                savedState = new VoxelGarageUpgradeDockValidation.SavedState();
                globals = new[] { typeof(VoxelMissionProgress), typeof(VoxelStartCountdown), typeof(VoxelPauseMenu), typeof(VoxelLoadingScreen) }
                    .Select(t => t.GetField("<Active>k__BackingField", Static)).ToArray();
                globalValues = globals.Select(f => f.GetValue(null)).ToArray(); foreach (var field in globals) field.SetValue(null, null);
                previousCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
                previousCameraEnabled = previousCameras.Select(c => c.enabled).ToArray(); foreach (var c in previousCameras) c.enabled = false;
                previousRoots = previousScene.GetRootGameObjects(); previousRootActive = previousRoots.Select(go => go.activeSelf).ToArray();
                foreach (var go in previousRoots) go.SetActive(false);
                captured = true; Application.runInBackground = true; Time.timeScale = .8f; Time.fixedDeltaTime = .03f;
                report.Clear(); pictured = verifiedSlowMotion = verifiedHandback = false;
                snapshots = Resources.LoadAll<VoxelEnemyVehicleTuning>("EnemyVehicles").Cast<Object>()
                    .Concat(new Object[] { Resources.Load<VoxelBossDefinition>("Bosses/RedVanBoss") }).ToDictionary(a => a, a => EditorJsonUtility.ToJson(a));
                scene = SceneManager.CreateScene("Temporary boss destruction QA"); SceneManager.SetActiveScene(scene);
                root = new GameObject("Boss destruction QA");
                var playerRoot = new GameObject("Player QA"); playerRoot.transform.SetParent(root.transform, false);
                playerRoot.transform.position = new Vector3(-10, 0, -8);
                Object.Instantiate(Resources.Load<VoxelCarDefinition>("Cars/SpyCar2PlayerCar").visualPrefab, playerRoot.transform);
                player = playerRoot.AddComponent<VoxelCarController>(); player.enabled = false; player.ResetIntegrityBaseline();
                Set(player, "nextDamageTime", float.PositiveInfinity); Set(player, "<CurrentSpeed>k__BackingField", 20f);
                traffic = Object.Instantiate(Resources.Load<VoxelTrackDefinition>("Tracks/TrackBoss01").obstacleCarTuning);
                missionTuning = ScriptableObject.CreateInstance<VoxelMissionTuning>();
                missionTuning.completionCurrencyAward = 0; missionTuning.requiredPoints = int.MaxValue;
                missionTuning.completionSlowMotionDuration = .8f; missionTuning.completionSlowMotionTimeScale = .6f;
                mission = root.AddComponent<VoxelMissionProgress>(); mission.enabled = false; mission.Configure(missionTuning);
                globals[0].SetValue(null, mission);
                CheckDamageSettings();
                CheckDestructionSettings();
                CheckCompletionOptions();
                StartLiveSequence();
                warmupFrame = Time.frameCount + 3;
                EditorApplication.update += BeginLiveAfterWarmup;
            }
            catch (Exception error) { Complete(false, error.ToString()); }
        }

        private static void CheckDamageSettings()
        {
            using (var c = new Case())
            {
                var ordinary = Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BlackInterceptorTuning");
                Check(c.Tuning != ordinary && c.Tuning.modelPrefab == c.Settings.bossPrefab, "Runtime tuning not independent");
                Equal(c.Tuning.ramDebrisUpwardForce, c.Settings.ramDebris.upwardForce, "Boss ram debris ownership");
                var protectedPiece = c.Pieces.First(r => r.GetComponentInParent<VoxelIndestructiblePart>() != null);
                float health = c.Enemy.CurrentHealth;
                c.Enemy.TakeProjectileHit(protectedPiece.transform, 100, protectedPiece.bounds.center, Vector3.forward);
                Equal(c.Enemy.CurrentHealth, health, "Protected structure damaged");
                var voxel = c.Pieces.First(r => r.GetComponentInParent<VoxelIndestructiblePart>() == null);
                c.Settings.gunHealthDamageMultiplier = 2;
                int before = c.Count;
                c.Enemy.TakeProjectileHit(voxel.transform, 1, voxel.bounds.center, Vector3.forward);
                Equal(c.Enemy.CurrentHealth, health - 2, "Independent gun health multiplier");
                Check(c.Count == before, "Gun multiplier changed voxel damage");
                c.Settings.ramDamageVoxelsMin = c.Settings.ramDamageVoxelsMax = 12;
                traffic.obstacleDamageVoxelsMin = traffic.obstacleDamageVoxelsMax = 777;
                before = c.Count; health = c.Enemy.CurrentHealth;
                Call(c.Enemy, "RamByPlayer", (Vector3?)Vector3.right);
                Check(before - c.Count == 12, "Boss ram used track voxel range");
                Equal(c.Enemy.CurrentHealth, health - c.Settings.playerRamDamage, "Ram health changed");
                c.Settings.maximumVoxelsRemovedPerRam = 5; before = c.Count;
                Call(c.Enemy, "RamByPlayer", (Vector3?)Vector3.left);
                Check(before - c.Count == 5, "Boss ram cap ignored");
            }
            using (var c = new Case())
            {
                c.Settings.useFixedMissileVoxelDamage = true; c.Settings.missileVoxelsRemoved = 50; c.Settings.maximumMissileDebris = 3;
                int before = c.Count; float health = c.Enemy.CurrentHealth;
                c.Enemy.TakeMissileBlast(new Vector3(0, .65f, -20), 1.8f, 35, Vector3.forward, c.Pieces, true);
                Check(before - c.Count == 50, "Fixed missile count not honoured at low rear impact");
                Equal(c.Enemy.CurrentHealth, health - 35, "Missile health applied more than once");
                Check(Object.FindObjectsByType<VoxelDebris>(FindObjectsSortMode.None).Count(d => d.gameObject.scene == scene) == 3, "Missile physical debris budget ignored");
                before = c.Count; health = c.Enemy.CurrentHealth;
                c.Enemy.TakeMissileBlast(Vector3.one * 1000, 1.8f, 35, Vector3.forward, c.Pieces);
                Check(c.Count == before, "Distant splash carved fixed voxels"); Equal(c.Enemy.CurrentHealth, health, "Distant splash damaged boss");
                c.Settings.missileVoxelsRemoved = 0; c.Settings.missileHealthDamageMultiplier = 2;
                c.Enemy.TakeMissileBlast(Vector3.zero, 0, 35, Vector3.forward, c.Pieces, true);
                Check(c.Count == before, "Zero fixed missile count removed pieces"); Equal(c.Enemy.CurrentHealth, health - 70, "Missile health multiplier");
                c.Settings.missileHealthDamageMultiplier = 0; health = c.Enemy.CurrentHealth;
                c.Enemy.TakeMissileBlast(Vector3.zero, 10, 35, Vector3.forward, c.Pieces, true);
                Equal(c.Enemy.CurrentHealth, health, "Zero missile multiplier ignored");
                c.Settings.missileHealthDamageMultiplier = 1; c.Settings.missileVoxelsRemoved = 30;
                var rig = c.Enemy.GetComponentInChildren<VoxelBossSpikeRig>();
                var doors = rig.leftDoor.GetComponentsInChildren<MeshRenderer>().Concat(rig.rightDoor.GetComponentsInChildren<MeshRenderer>()).Where(r => r.gameObject.activeInHierarchy).ToArray();
                Set(c.Enemy, "<SpikePhase>k__BackingField", VoxelEnemyCar.SpikeAttackPhase.Warning);
                c.Enemy.TakeMissileBlast(Vector3.zero, 100, 35, Vector3.forward, c.Pieces, true);
                Check(doors.All(r => r.gameObject.activeInHierarchy), "Fixed missile damage stripped the active attack rig");
                var wheels = c.Pieces.Where(r => r.gameObject.activeInHierarchy && r.GetComponentsInParent<Transform>().Any(t => t.name == "Obstacle Voxel Wheel")).ToArray();
                c.Settings.missileVoxelsRemoved = 100000;
                c.Enemy.TakeMissileBlast(Vector3.zero, 100, 35, Vector3.forward, c.Pieces, true);
                Check(wheels.All(r => r.gameObject.activeInHierarchy), "Fixed missile damage removed wheels while protected attack bodywork remained");
            }
            using (var c = new Case())
            {
                c.Settings.useFixedMissileVoxelDamage = false; c.Settings.missileBlastRadiusMultiplier = 2;
                var p = new Vector3(0, .65f, -10);
                int expected = c.Pieces.Count(r => VoxelSphericalBlast.Contains(r, p, 3.6f) && !r.GetComponentsInParent<Transform>().Any(t => t.name == "Obstacle Voxel Wheel"));
                int before = c.Count;
                c.Enemy.TakeMissileBlast(p, 1.8f, 35, Vector3.forward, c.Pieces, true);
                Check(before - c.Count == expected, "Boss blast-radius multiplier differs from spherical damage");
            }
            report.Add("PASS independent runtime tuning, gun/ram health and voxel damage, fixed/radius missile modes, splash gating, zero/custom values, debris cap and active-rig protection.");
        }

        private static void CheckDestructionSettings()
        {
            foreach (bool detach in new[] { false, true })
            using (var c = new Case())
            {
                c.Settings.onDestroyVoxelsRemoved = 300; c.Settings.maximumDestructionRemovalFraction = 1;
                c.Settings.maximumDestructionDebris = 7; c.Settings.detachSpikesOnDestruction = detach;
                int initial = c.Count, callbacks = 0;
                c.Enemy.Defeated += _ => callbacks++;
                c.Enemy.TakeProjectileHit(null, c.Enemy.CurrentHealth, Vector3.zero, Vector3.forward);
                Check(c.Count == initial - 300, "Boss death voxel count ignored");
                Check(Object.FindObjectsByType<VoxelDebris>(FindObjectsSortMode.None).Count(d => d.gameObject.scene == scene) == 7, "Death physical debris cap ignored");
                Check(Object.FindObjectsByType<VoxelBossSpikeDebris>(FindObjectsSortMode.None).Count(d => d.gameObject.scene == scene) == (detach ? 3 : 0), "Wrong detached spike count");
                c.Enemy.TakeProjectileHit(null, 10000, Vector3.zero, Vector3.forward);
                Check(callbacks == 1 && c.Count == initial - 300, "Repeated boss death callback/cleanup");
            }
            report.Add("PASS exact boss death removal independent of seven-fragment budget, optional three-spike detachment and duplicate-death guards.");
            using (var c = new Case())
            {
                c.Settings.onDestroyVoxelsRemoved = 500; c.Settings.maximumDestructionRemovalFraction = .1f;
                c.Settings.maximumDestructionDebris = 0; c.Settings.detachSpikesOnDestruction = false;
                int initial = c.Count;
                c.Enemy.TakeProjectileHit(null, c.Enemy.CurrentHealth, Vector3.zero, Vector3.forward);
                Check(c.Count == initial - Mathf.FloorToInt(initial * .1f), "Fractional death-removal cap ignored");
                Check(Object.FindObjectsByType<VoxelDebris>(FindObjectsSortMode.None).All(d => d.gameObject.scene != scene), "Zero death debris budget emitted fragments");
            }
        }

        private static void CheckCompletionOptions()
        {
            using var c = new Case();
            c.Settings.overrideCompletionSlowMotion = false;
            mission.Configure(missionTuning); globals[0].SetValue(null, mission);
            mission.SetBossEncounter(true); mission.ShowBoss(c.Enemy, "Boss options QA");
            Call(mission, "BeginCompletionSlowMotion");
            Equal(Time.timeScale, .48f, "Boss fallback to track slowdown"); Equal(Time.fixedDeltaTime, .018f, "Track slowdown cadence");
            mission.CancelCompletionSlowMotion(); Equal(Time.timeScale, .8f, "Custom fallback clock restoration");
            c.Settings.overrideCompletionSlowMotion = true; c.Settings.completionSlowMotionDuration = 0;
            Call(mission, "BeginCompletionSlowMotion"); Check(!mission.IsCompletionSlowMotionActive, "Zero boss slowdown duration ignored");
            c.Settings.completionSlowMotionDuration = 1; c.Settings.completionSlowMotionTimeScale = 1;
            Call(mission, "BeginCompletionSlowMotion"); Check(!mission.IsCompletionSlowMotionActive, "Full-speed override acquired slow-motion clock");
            report.Add("PASS track slowdown fallback, custom scale restoration and zero-duration/full-speed disable options.");
        }

        private static void StartLiveSequence()
        {
            live = new Case();
            live.Settings.onDestroyVoxelsRemoved = 800; live.Settings.maximumDestructionRemovalFraction = 1; live.Settings.maximumDestructionDebris = 12;
            live.Settings.useWorldScaleForDestructionExplosion = true;
            live.Settings.destructionExplosionScale = live.Tuning.explosionEffectScale = 3f;
            live.Settings.destroyedLifetime = live.Tuning.destroyedLifetime = 1.3f;
            live.Settings.spikeDebrisLifetime = .6f; live.Settings.spikeFireSmokeDuration = .15f;
            live.Settings.overrideCompletionSlowMotion = true; live.Settings.completionSlowMotionDuration = .35f; live.Settings.completionSlowMotionTimeScale = .2f;
            live.Settings.overrideFinishStopDuration = true; live.Settings.finishStopDuration = .6f;
            live.Settings.focusCameraOnDestruction = true; live.Settings.destructionCameraDuration = 2; live.Settings.destructionCameraBlendDuration = .15f;
            var cameraRoot = new GameObject("Boss QA Camera", typeof(Camera)); cameraRoot.transform.SetParent(root.transform, false); cameraRoot.tag = "MainCamera";
            camera = cameraRoot.GetComponent<Camera>(); camera.transform.position = new Vector3(20, 14, -25); camera.transform.LookAt(new Vector3(0, 4, 0));
            camera.nearClipPlane = .1f; camera.farClipPlane = 300; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.12f, .15f, .2f);
            cameraFollow = cameraRoot.AddComponent<VoxelCameraFollow>(); cameraFollow.target = player.transform;
            cameraTuning = Object.Instantiate(VoxelCameraTuning.Load()); cameraTuning.finishSequenceDuration = .25f; cameraFollow.tuning = cameraTuning;
            var light = new GameObject("QA light", typeof(Light)); light.transform.SetParent(root.transform, false);
            light.GetComponent<Light>().type = LightType.Directional; light.GetComponent<Light>().intensity = 1.8f; light.transform.rotation = Quaternion.Euler(45, 25, 0);
            mission.Configure(missionTuning); globals[0].SetValue(null, mission); mission.SetBossEncounter(true); mission.ShowBoss(live.Enemy, "Boss QA");
            var finishRoot = new GameObject("Finish marker QA"); finishRoot.transform.SetParent(root.transform, false);
            finish = finishRoot.AddComponent<VoxelRunFinish>(); finish.Configure(player, mission);
            cameraFollow.enabled = false;
        }

        private static void BeginLiveAfterWarmup()
        {
            if (!Application.isPlaying || EditorApplication.isPaused || Time.frameCount < warmupFrame) return;
            EditorApplication.update -= BeginLiveAfterWarmup;
            try
            {
                cameraFollow.enabled = true;
                live.Enemy.Defeated += _ => mission.CompleteBossEncounter();
                live.Enemy.TakeProjectileHit(null, live.Enemy.CurrentHealth, Vector3.zero, Vector3.forward);
                Check(mission.IsComplete && mission.IsCompletionSlowMotionActive, "Boss completion did not start slowdown");
                Equal(Time.timeScale, .16f, "Boss slow-motion override"); Equal(Time.fixedDeltaTime, .006f, "Boss physics cadence");
                Call(finish, "Update");
                Equal(player.PlannedFinishStopDuration, .6f, "Boss coast-duration override");
                Check(!finish.FinishCameraComplete && !cameraFollow.BossDestructionFocusComplete && live.Enemy.gameObject.activeInHierarchy, "Boss camera did not hold the wreck before results");
                var spikes = Object.FindObjectsByType<VoxelBossSpikeDebris>(FindObjectsSortMode.None).Where(s => s.gameObject.scene == scene).ToArray();
                Check(spikes.Length == 3 && spikes.All(s => s.transform.parent == null && s.GetComponentsInChildren<MeshRenderer>().Length == 4 &&
                    s.GetComponentsInChildren<MeshRenderer>().All(r => r.enabled)), "Spikes not independent visible shaft/tip assemblies");
                Check(spikes.All(s => s.GetComponentsInChildren<ParticleSystem>().Length == 2 && s.GetComponent<VoxelVehicleDamageEffects>().SmokeActive && s.GetComponent<VoxelVehicleDamageEffects>().FireActive), "Each spike needs its own enabled smoke/fire pair");
                Check(scene.GetRootGameObjects().Count(go => go.name == "Voxel Destruction Explosion" && Mathf.Approximately(go.transform.localScale.x, live.Settings.spikeExplosionScale)) == 3,
                    "Spike bursts inherited the shared explosion's global scale");
                live.Enemy.enabled = true; player.enabled = true;
                startedAt = Time.realtimeSinceStartupAsDouble;
                EditorApplication.update += Tick;
            }
            catch (Exception error) { Complete(false, error.ToString()); }
        }

        private static void Tick()
        {
            if (!Application.isPlaying || EditorApplication.isPaused) return;
            try
            {
                double elapsed = Time.realtimeSinceStartupAsDouble - startedAt;
                if (!pictured && elapsed >= .7)
                {
                    Capture(); pictured = true;
                }
                if (!verifiedSlowMotion && elapsed >= .4)
                {
                    Call(mission, "TickCompletionSlowMotion");
                    Check(!mission.IsCompletionSlowMotionActive, "Boss real-time slowdown duration did not end");
                    Equal(Time.timeScale, .8f, "Boss clock restoration"); Equal(Time.fixedDeltaTime, .03f, "Boss fixed-step restoration"); verifiedSlowMotion = true;
                }
                if (!verifiedHandback && elapsed >= 2.7)
                {
                    Check(cameraFollow.BossDestructionFocusComplete && finish.FinishCameraComplete, "Boss focus did not return to the normal player finish sequence");
                    Equal(player.CurrentSpeed, 0, "Boss coasting did not stop the player");
                    Check(live.Enemy == null, "Expired boss wreck was kept alive for the focus camera");
                    verifiedHandback = true;
                }
                if (elapsed < 4.2) return;
                Check(Object.FindObjectsByType<VoxelBossSpikeDebris>(FindObjectsSortMode.None).All(s => s.gameObject.scene != scene), "Detached spike/effects lifetime leaked");
                foreach (var pair in snapshots) Check(pair.Value == EditorJsonUtility.ToJson(pair.Key), "Source tuning changed: " + pair.Key.name);
                report.Add("PASS live three-spike physics and fire/smoke, realtime boss slowdown/restoration, independent coast duration, camera focus/expired-target fallback/player handback, reward gating and fragment/particle cleanup.");
                Complete(true, "All boss destruction checks passed.");
            }
            catch (Exception error) { Complete(false, error.ToString()); }
        }

        private static void Capture()
        {
            Directory.CreateDirectory("Temp/BossDestruction");
            Check(live.Enemy != null, "Boss wreck expired before the early presentation capture");
            var bossPoint = live.Enemy.transform.position + Vector3.up * live.Settings.destructionCameraLookHeight;
            var viewport = camera.WorldToViewportPoint(bossPoint);
            report.Add("Live focus capture: boss viewport=" + viewport + ", camera=" + camera.transform.position + ", FOV=" + camera.fieldOfView);
            Check(viewport.z > 0 && Mathf.Abs(viewport.x - .5f) < .25f && Mathf.Abs(viewport.y - .5f) < .25f, "Boss focus did not frame the explosion centre");
            var rt = RenderTexture.GetTemporary(1280, 800, 24);
            var old = camera.targetTexture; var active = RenderTexture.active;
            Texture2D image = null;
            try
            {
                camera.targetTexture = rt; camera.Render(); RenderTexture.active = rt;
                image = new Texture2D(1280, 800, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0); image.Apply();
                File.WriteAllBytes("Temp/BossDestruction/ExplodingSpikes.png", image.EncodeToPNG());
            }
            finally { camera.targetTexture = old; RenderTexture.active = active; if (image != null) Object.DestroyImmediate(image); RenderTexture.ReleaseTemporary(rt); }
        }

        private static void Complete(bool success, string message)
        {
            EditorApplication.update -= Tick;
            EditorApplication.update -= BeginLiveAfterWarmup;
            SessionState.SetBool(Pending, false);
            if (captured)
            {
                if (mission != null) mission.CancelCompletionSlowMotion();
                live?.Dispose(); live = null;
                if (root != null) Object.DestroyImmediate(root);
                if (traffic != null) Object.DestroyImmediate(traffic);
                if (missionTuning != null) Object.DestroyImmediate(missionTuning);
                if (cameraTuning != null) Object.DestroyImmediate(cameraTuning);
                for (int i = 0; i < globals.Length; i++) globals[i].SetValue(null, globalValues[i]);
                for (int i = 0; i < previousRoots.Length; i++) if (previousRoots[i] != null) previousRoots[i].SetActive(previousRootActive[i]);
                for (int i = 0; i < previousCameras.Length; i++) if (previousCameras[i] != null) previousCameras[i].enabled = previousCameraEnabled[i];
                Time.timeScale = previousScale; Time.fixedDeltaTime = previousStep; Application.runInBackground = previousBackground; UnityEngine.Random.state = random;
                savedState?.Dispose(); savedState = null;
                SceneManager.SetActiveScene(previousScene); if (scene.IsValid()) SceneManager.UnloadSceneAsync(scene);
                captured = false;
            }
            Directory.CreateDirectory("Temp/BossDestruction"); report.Insert(0, (success ? "PASS: " : "FAIL: ") + message);
            File.WriteAllLines("Temp/BossDestruction/Validation.txt", report);
            if (success) Debug.Log(report[0]); else Debug.LogError(report[0]);
            EditorApplication.isPlaying = false;
        }
    }
}
