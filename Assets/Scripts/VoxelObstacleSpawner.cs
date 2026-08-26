using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Creates simple lane obstacles ahead of the moving car.</summary>
    public sealed class VoxelObstacleSpawner : MonoBehaviour
    {
        [Header("Spawn Timing")]
        [Min(0.1f)] public float minimumSpawnInterval = 2.5f;
        [Min(0.1f)] public float maximumSpawnInterval = 4.5f;

        [Header("Lane Layout")]
        [Min(1)] public int laneCount = 4;
        [Min(0.1f)] public float laneWidth = 3f;

        [Header("Obstacle Types")]
        [Tooltip("Controls traffic-car spawn frequency, direction, speed, impacts, and debris.")]
        public VoxelObstacleCarTuning obstacleCarTuning;

        private VoxelCarController target;
        private VoxelStartCountdown countdown;
        private VoxelRunFinish runFinish;
        private float nextSpawnTime;

        public void SetTarget(VoxelCarController player) => target = player;
        public void SetStartCountdown(VoxelStartCountdown value) => countdown = value;
        public void SetRunFinish(VoxelRunFinish value) => runFinish = value;

        private void Start()
        {
            nextSpawnTime = Time.time + Random.Range(minimumSpawnInterval, maximumSpawnInterval);
        }

        private void Update()
        {
            if (!Application.isPlaying || target == null ||
                (countdown != null && !countdown.IsComplete) || Time.time < nextSpawnTime)
                return;

            int lane = Random.Range(0, laneCount);
            float laneOffset = (lane - (laneCount - 1) * 0.5f) * laneWidth;
            float spawnDistanceAhead = obstacleCarTuning != null ? obstacleCarTuning.spawnDistanceAhead : 65f;
            if (runFinish != null && (runFinish.HasFinished ||
                target.TrackDistance + spawnDistanceAhead >= runFinish.FinishDistance - 12f))
                return;

            EndlessVoxelRoad path = target.TrackPath;
            float spawnTrackDistance = target.TrackDistance + spawnDistanceAhead;
            if (path == null)
                return;
            path.EnsurePathCovers(spawnTrackDistance + 10f);

            bool spawnTrafficCar = obstacleCarTuning != null && Random.value < obstacleCarTuning.obstacleCarSpawnChance;
            if (spawnTrafficCar)
            {
                bool sameDirection = Random.value >= obstacleCarTuning.oppositeDirectionChance;
                var obstacle = new GameObject(sameDirection ? "Red Traffic Car (Same Direction)" : "Red Traffic Car (Oncoming)")
                    .AddComponent<VoxelObstacleCar>();
                obstacle.transform.SetParent(transform);
                obstacle.Configure(target, obstacleCarTuning, sameDirection, path, spawnTrackDistance, laneOffset);
                obstacle.gameObject.AddComponent<VoxelFadeIn>();
            }
            else
            {
                var obstacle = new GameObject("Brown Voxel Obstacle").AddComponent<VoxelObstacle>();
                obstacle.transform.SetParent(transform);
                obstacle.Configure(target, path, spawnTrackDistance, laneOffset);
                obstacle.gameObject.AddComponent<VoxelFadeIn>();
            }

            nextSpawnTime = Time.time + Random.Range(minimumSpawnInterval, maximumSpawnInterval);
        }
    }
}
