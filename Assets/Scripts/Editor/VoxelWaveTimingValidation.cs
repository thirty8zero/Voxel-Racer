using System;
using System.Collections;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VoxelRacer.Editor
{
    /// <summary>Exercises the real spawner Update with controlled speeds in Play Mode.</summary>
    public static class VoxelWaveTimingValidation
    {
        private const string Pending = "VoxelRacer.WaveTimingCheck";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        [InitializeOnLoadMethod]
        private static void Hook()
        {
            EditorApplication.playModeStateChanged -= Changed;
            EditorApplication.playModeStateChanged += Changed;
        }

        [MenuItem("Tools/Voxel Racer/Validate Wave Timing in Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Start from Edit Mode.");
            SessionState.SetBool(Pending, true);
            EditorApplication.isPlaying = true;
        }

        private static void Changed(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(Pending, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode) EditorApplication.update += Validate;
            if (state == PlayModeStateChange.ExitingPlayMode)
            {
                EditorApplication.update -= Validate;
                SessionState.SetBool(Pending, false);
            }
        }

        private static void Set(object target, string field, object value) =>
            target.GetType().GetField(field, Private).SetValue(target, value);
        private static object Get(object target, string field) =>
            target.GetType().GetField(field, Private).GetValue(target);
        private static void Check(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
        private static void Equal(float actual, float expected, string message) =>
            Check(Mathf.Abs(actual - expected) < .0001f, message + ": " + actual + " != " + expected);

        private static void Validate()
        {
            if (!Application.isPlaying || EditorApplication.isPaused || Time.deltaTime <= 0f) return;
            EditorApplication.update -= Validate;
            var random = UnityEngine.Random.state;
            var previousPause = VoxelPauseMenu.Active;
            var activePause = typeof(VoxelPauseMenu).GetField("<Active>k__BackingField",
                BindingFlags.Static | BindingFlags.NonPublic);
            GameObject root = null;
            VoxelObstacleCarTuning tuning = null;
            string report;
            try
            {
                Check(!VoxelPauseMenu.IsPaused && VoxelMissionProgress.Active?.IsComplete != true,
                    "Run with gameplay unpaused and no completed mission.");
                root = new GameObject("Temporary wave timing QA");
                // No autonomous updates, visuals or source assets are needed.
                root.SetActive(false);
                var player = root.AddComponent<VoxelCarController>();
                player.topSpeed = 40f;
                var spawner = root.AddComponent<VoxelObstacleSpawner>();
                spawner.SetTarget(player);
                spawner.minimumSpawnInterval = spawner.maximumSpawnInterval = 3f;
                var update = typeof(VoxelObstacleSpawner).GetMethod("Update", Private);
                void Step() => update.Invoke(spawner, null);
                float Remaining() => (float)Get(spawner, "spawnTimeRemaining");
                void Speed(float value) => Set(player, "<CurrentSpeed>k__BackingField", value);
                void Rate(float speed, float expectedRate, string label)
                {
                    Speed(speed); Set(spawner, "spawnTimeRemaining", 10f); Step();
                    Equal(Remaining(), 10f - Time.deltaTime * expectedRate, label);
                }

                Rate(0f, 1f, "Stopped countdown");
                Rate(20f, 1f, "Below normal maximum");
                Rate(40f, 1f, "At normal maximum");
                Rate(60f, 1.5f, "Boosted countdown");
                player.SetBoostSpeedBonus(30f);
                Rate(20f, 1f, "Boost active below normal maximum");
                player.SetBoostSpeedBonus(0f);
                Rate(60f, 1.5f, "Residual speed after boost");
                Rate(40f, 1f, "Recovery to normal speed");
                player.SetEnginePerformance(25f, 0f);
                Rate(50f, 1f, "Upgraded engine at its new maximum");
                Rate(75f, 1.5f, "Above upgraded engine maximum");
                player.topSpeed = 0f;
                Rate(20f, 1f, "Zero maximum avoids division by zero");
                player.topSpeed = 40f;

                var countdown = root.AddComponent<VoxelStartCountdown>();
                spawner.SetStartCountdown(countdown);
                Set(spawner, "spawnTimeRemaining", 10f); Step();
                Equal(Remaining(), 10f, "Opening gate freezes countdown");
                spawner.SetStartCountdown(null);
                var pause = root.AddComponent<VoxelPauseMenu>();
                activePause.SetValue(null, pause); Set(pause, "paused", true);
                Step(); Equal(Remaining(), 10f, "Pause gate freezes countdown");
                Set(pause, "paused", false); activePause.SetValue(null, previousPause);
                Set(player, "<IsDestroyed>k__BackingField", true);
                Step(); Equal(Remaining(), 10f, "Destroyed player freezes countdown");
                Set(player, "<IsDestroyed>k__BackingField", false);

                // Build only path data so expired timers exercise real wave enqueueing.
                var road = root.AddComponent<EndlessVoxelRoad>();
                Set(road, "turnRandom", new System.Random(173));
                typeof(EndlessVoxelRoad).GetMethod("AppendSegment", Private).Invoke(road, new object[] { false });
                player.SetTrack(road, 0f);
                tuning = ScriptableObject.CreateInstance<VoxelObstacleCarTuning>();
                tuning.minimumObjectsPerWave = tuning.maximumObjectsPerWave = 3;
                tuning.obstacleCarSpawnChance = 0f; // No content is created by queued test requests.
                spawner.obstacleCarTuning = tuning;
                var queue = (ICollection)Get(spawner, "pendingSpawnRequests");

                Speed(75f); // Upgraded maximum remains 50m/s; countdown rate is 1.5.
                Set(spawner, "spawnTimeRemaining", Time.deltaTime * 1.25f);
                Step(); Check(queue.Count == 3, "Boost did not bring the wave forward.");
                Equal(Remaining(), 3f, "Next wave retains authored interval");
                Step(); Check(queue.Count == 2, "Queued wave must consume only one request per update.");
                Equal(Remaining(), 3f - Time.deltaTime * 1.5f, "New interval continues at boosted rate");
                typeof(VoxelObstacleSpawner).GetMethod("ScheduleNextSpawn", Private).Invoke(spawner, null);
                // Clear outstanding requests without changing the spawner's enabled state.
                Get(spawner, "pendingSpawnRequests").GetType().GetMethod("Clear").Invoke(queue, null);
                Speed(50f); Set(spawner, "spawnTimeRemaining", Time.deltaTime * 1.25f);
                Step(); Check(queue.Count == 0, "Normal speed spawned the wave too early.");
                Step(); Check(queue.Count == 3, "Normal speed failed to spawn when due.");

                spawner.SetStartCountdown(countdown);
                Set(countdown, "started", true);
                Set(countdown, "countdownStartedAt", Time.time - 2.1f);
                Get(spawner, "pendingSpawnRequests").GetType().GetMethod("Clear").Invoke(queue, null);
                Set(spawner, "spawnTimeRemaining", 100f);
                Step(); Check(queue.Count == 3, "Opening '1' did not force the first wave.");
                Equal(Remaining(), 3f, "Opening wave schedules normal interval");
                var finish = root.AddComponent<VoxelRunFinish>();
                Set(finish, "<HasFinished>k__BackingField", true); spawner.SetRunFinish(finish);
                Step(); Check(queue.Count == 0, "Mission completion did not clear pending requests.");
                Equal(Remaining(), 3f, "Finished run advanced countdown");
                report = "PASS (Play Mode simulation): real spawner Update at stopped/normal/boosted speeds, boost below maximum, residual speed, engine upgrades, zero maximum, opening/pause/death gates, earlier boosted wave, normal due wave, interval reset, one queued object per update, forced first wave and finish cleanup. Temporary objects removed; assets, purchases and random state preserved.";
            }
            catch (Exception e) { report = "FAIL: " + e; }
            finally
            {
                activePause.SetValue(null, previousPause);
                if (root != null) Object.Destroy(root);
                if (tuning != null) Object.Destroy(tuning);
                UnityEngine.Random.state = random;
            }
            Directory.CreateDirectory("Temp"); File.WriteAllText("Temp/WaveTimingValidation.txt", report);
            if (report.StartsWith("PASS")) Debug.Log(report); else Debug.LogError(report);
            EditorApplication.isPlaying = false;
        }
    }
}
