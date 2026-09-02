using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Mission-driven roadside turret spawning, kept separate from lane traffic waves.</summary>
    public sealed class VoxelRoadsideTurretSpawner : MonoBehaviour
    {
        private VoxelCarController target;
        private EndlessVoxelRoad path;
        private VoxelObstacleSpawner obstacleSpawner;
        private VoxelMissionTuning tuning;
        private VoxelStartCountdown countdown;
        private VoxelRunFinish runFinish;
        private float nextCheckTime;
        private bool spawnWindowOpened;

        public void Configure(VoxelCarController player, EndlessVoxelRoad road, VoxelObstacleSpawner trafficSpawner,
            VoxelMissionTuning missionTuning)
        {
            target = player;
            path = road;
            obstacleSpawner = trafficSpawner;
            tuning = missionTuning;
            countdown = GetComponent<VoxelStartCountdown>();
            runFinish = GetComponentInChildren<VoxelRunFinish>();
            nextCheckTime = float.PositiveInfinity;
            spawnWindowOpened = false;
        }

        private void Update()
        {
            if (target == null || path == null || tuning == null || tuning.roadsideTurretTuning == null || target.IsDestroyed)
                return;
            // SetupGameplay configures this spawner before it creates the countdown,
            // so resolve it lazily and never permit a spawn before the countdown's GO.
            if (countdown == null)
                countdown = GetComponent<VoxelStartCountdown>();
            if (countdown == null || !countdown.IsComplete || runFinish != null && runFinish.HasFinished)
                return;

            if (!spawnWindowOpened)
            {
                spawnWindowOpened = true;
                nextCheckTime = Time.time + tuning.roadsideTurretSpawnCheckInterval;
            }
            if (Time.time < nextCheckTime)
                return;

            nextCheckTime = Time.time + tuning.roadsideTurretSpawnCheckInterval;
            if (GetComponentsInChildren<VoxelRoadsideTurret>().Length >= tuning.maximumActiveRoadsideTurrets ||
                Random.value > tuning.roadsideTurretSpawnChance)
                return;

            float distance = target.TrackDistance + tuning.roadsideTurretSpawnDistanceAhead;
            VoxelRoadsideTurretTuning turretTuning = tuning.roadsideTurretTuning;
            if (obstacleSpawner != null && obstacleSpawner.HasStaticObstacleNearTrackDistance(distance,
                    turretTuning.staticObstacleClearance))
                return;

            path.EnsurePathCovers(distance + 10f);
            GameObject turretObject = new GameObject(turretTuning.displayName);
            turretObject.transform.SetParent(transform);
            var turret = turretObject.AddComponent<VoxelRoadsideTurret>();
            turret.Configure(target, path, turretTuning, distance, Random.value < 0.5f ? -1f : 1f);
            turretObject.AddComponent<VoxelFadeIn>();
        }
    }
}
