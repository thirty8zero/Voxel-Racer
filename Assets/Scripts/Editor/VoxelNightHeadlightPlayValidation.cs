using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    public static class VoxelNightHeadlightPlayValidation
    {
        private const string Pending = "VoxelRacer.NightHeadlightPlayCheck";
        private static Scene scene, previousScene;
        private static VoxelTrackDefinition track;
        private static VoxelRoadTuning roadTuning;
        private static VoxelEnemyVehicleTuning bossTuning;
        private static VoxelNightHeadlightSettings settings;
        private static VoxelEnemyCar enemy;
        private static VoxelObstacleCar traffic;
        private static VoxelNightHeadlights enemyLights;
        private static VoxelSpawnScaleIn growth;
        private static VoxelFadeIn fade;
        private static Camera camera;
        private static float savedScale, started;
        private static bool savedBackground;
        private static UnityEngine.Random.State random;
        private static int phase;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }
        [MenuItem("Tools/Voxel Racer/Lighting/Validate Night Headlights in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Start from Edit Mode.");
            SessionState.SetBool(Pending, true); EditorApplication.isPlaying = true;
        }
        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.delayCall += Setup;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Tick;
                Time.timeScale = savedScale; Application.runInBackground = savedBackground;
                SessionState.SetBool(Pending, false);
            }
        }
        private static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
        private static void Setup()
        {
            savedScale = Time.timeScale; savedBackground = Application.runInBackground; random = UnityEngine.Random.state;
            previousScene = SceneManager.GetActiveScene(); scene = SceneManager.CreateScene("Temporary night headlight play QA");
            SceneManager.SetActiveScene(scene); Application.runInBackground = true; Time.timeScale = 0;
            try
            {
                var root = new GameObject("Temporary headlight frame checks");
                track = Object.Instantiate(Resources.Load<VoxelTrackDefinition>("Tracks/Track01")); track.nightTime = true;
                roadTuning = Object.Instantiate(track.roadTuning);
                roadTuning.minimumCactiPerSegment = roadTuning.maximumCactiPerSegment = 0;
                roadTuning.turnChancePerSegment = 0;
                var road = root.AddComponent<EndlessVoxelRoad>(); road.enabled = false;
                road.trackDefinition = track; road.SetTuning(roadTuning); road.BuildInitialRoad();
                var player = new GameObject("QA player").AddComponent<VoxelCarController>(); player.transform.SetParent(root.transform);
                player.enabled = false; player.topSpeed = 0; player.SetTrack(road, 0);
                camera = new GameObject("QA beam camera").AddComponent<Camera>(); camera.transform.SetParent(root.transform);
                camera.enabled = false; camera.transform.position = new Vector3(-6, 6, 0); camera.transform.LookAt(new Vector3(0, 0, 40));
                enemy = new GameObject("QA enemy").AddComponent<VoxelEnemyCar>(); enemy.transform.SetParent(root.transform);
                enemy.Configure(player, VoxelObstacleCarTuning.Load(), Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/BlackInterceptorTuning"), road, 40, 5);
                enemy.enabled = false; enemyLights = enemy.GetComponent<VoxelNightHeadlights>();
                fade = enemy.gameObject.AddComponent<VoxelFadeIn>();
                VoxelSpawnScaleIn.Play(enemy.gameObject); growth = enemy.GetComponent<VoxelSpawnScaleIn>();
                traffic = new GameObject("QA oncoming civilian").AddComponent<VoxelObstacleCar>(); traffic.transform.SetParent(root.transform);
                traffic.Configure(player, VoxelObstacleCarTuning.Load(), false, road, 40, -5, 0); traffic.enabled = false;
                var boss = Resources.LoadAll<VoxelBossDefinition>("Bosses").First(b => b.bossPrefab != null);
                bossTuning = ScriptableObject.CreateInstance<VoxelEnemyVehicleTuning>(); bossTuning.modelPrefab = boss.bossPrefab; bossTuning.vehicleHealth = boss.health;
                var bossCar = new GameObject("QA entrance boss").AddComponent<VoxelEnemyCar>(); bossCar.transform.SetParent(root.transform);
                bossCar.ConfigureBoss(boss, road.roadWidth / road.laneCount, road.laneCount);
                bossCar.Configure(player, VoxelObstacleCarTuning.Load(), bossTuning, road, 70, 0); bossCar.enabled = false;
                Check(bossCar.GetComponent<VoxelNightHeadlights>().BoundLensCount > 0, "Zero-scale boss entrance lost its lamps.");
                Check(enemyLights != null && traffic.GetComponent<VoxelNightHeadlights>() != null && growth.Growth == 0f && fade.Opacity == 0f,
                    "Real night configure/fade/growth setup failed.");
                phase = 0; started = Time.realtimeSinceStartup; EditorApplication.update += Tick;
            }
            catch (Exception error) { Finish("FAIL: " + error); }
        }
        private static void Tick()
        {
            if (!Application.isPlaying || EditorApplication.isPaused || Time.realtimeSinceStartup - started < (phase == 1 ? .5f : phase == 2 ? 1.2f : .2f)) return;
            try
            {
                if (phase == 0)
                {
                    Check(growth.Growth == 0f && fade.Opacity == 0f, "Headlight spawn presentation advanced while paused.");
                    Time.timeScale = 1; phase = 1; started = Time.realtimeSinceStartup; return;
                }
                if (phase == 1)
                {
                    Check(growth.Growth > 0 && growth.Growth < 1 && fade.Opacity > 0 && fade.Opacity < 1,
                        "Headlights did not follow the one-second spawn fade/growth.");
                    VoxelNightHeadlights.SubmitDraws(camera, false);
                    Check(VoxelNightHeadlights.LastBeamCount == 6 && VoxelNightHeadlights.LastBatchCount == 1, "Live mixed traffic did not share six cards in one batch.");
                    phase = 2; started = Time.realtimeSinceStartup; return;
                }
                if (phase == 2)
                {
                    Check(growth.Growth == 1 && fade.Opacity == 1, "Completed spawn did not restore full headlight presentation.");
                    Check(enemy.GetComponentsInChildren<MeshRenderer>().Any(r => r.sharedMaterial == VoxelNightHeadlightSettings.Load().enemyLampMaterial),
                        "Spawn opacity restoration lost the glowing lamps.");
                    VoxelMissileValidation.Set(enemy, "hasBeenRammed", true);
                    phase = 3; started = Time.realtimeSinceStartup; return;
                }
                VoxelNightHeadlights.SubmitDraws(camera, false);
                Check(VoxelNightHeadlights.LastBeamCount == 4, "Destroyed vehicle kept its two beams.");
                Check(!enemy.GetComponentsInChildren<MeshRenderer>().Any(r => r.sharedMaterial == VoxelNightHeadlightSettings.Load().enemyLampMaterial),
                    "Wreck lenses remained powered.");
                traffic.gameObject.SetActive(false);
                VoxelNightHeadlights.SubmitDraws(camera, false);
                Check(VoxelNightHeadlights.LastBeamCount == 2, "Disabled civilian kept its beams.");
                traffic.gameObject.SetActive(true);
                VoxelNightHeadlights.SubmitDraws(camera, false);
                Check(VoxelNightHeadlights.LastBeamCount == 4, "Re-enabled civilian lost its headlights.");
                var trafficLights = traffic.GetComponent<VoxelNightHeadlights>();
                settings = Object.Instantiate(VoxelNightHeadlightSettings.Load());
                VoxelMissileValidation.Set(trafficLights, "settings", settings);
                settings.headlightsEnabled = false;
                VoxelMissileValidation.Call(trafficLights, "LateUpdate");
                Check(!traffic.GetComponentsInChildren<MeshRenderer>().Any(r => r.sharedMaterial == settings.lampMaterial), "Headlight master switch did not turn off the lenses.");
                settings.headlightsEnabled = true;
                VoxelMissileValidation.Call(trafficLights, "LateUpdate");
                Check(traffic.GetComponentsInChildren<MeshRenderer>().Any(r => r.sharedMaterial == settings.lampMaterial), "Headlight master switch did not restore lens emission.");
                Finish("PASS (Play Mode): real night civilian/enemy/boss configure; zero-scale boss entrance binds lamps; paused spawn stays dark; real-frame one-second opacity/growth and boss entrance; six cards share one batch; opaque material restoration retains lens glow; wreck turns off beams and glow; disable/re-enable restores registry. Temporary scene/tuning removed, RNG/clock/background restored, original startup scene retained. Not a phone GPU benchmark.");
            }
            catch (Exception error) { Finish("FAIL: " + error); }
        }
        private static void Finish(string report)
        {
            EditorApplication.update -= Tick;
            Directory.CreateDirectory("Temp/NightHeadlights"); File.WriteAllText("Temp/NightHeadlights/PlayValidation.txt", report); Debug.Log(report);
            SceneManager.SetActiveScene(previousScene);
            if (scene.IsValid()) SceneManager.UnloadSceneAsync(scene);
            if (track != null) Object.Destroy(track); if (roadTuning != null) Object.Destroy(roadTuning); if (bossTuning != null) Object.Destroy(bossTuning);
            if (settings != null) Object.Destroy(settings);
            UnityEngine.Random.state = random; Time.timeScale = savedScale; Application.runInBackground = savedBackground;
            EditorApplication.isPlaying = false;
        }
    }
}
