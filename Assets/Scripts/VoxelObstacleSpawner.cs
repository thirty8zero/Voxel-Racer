using UnityEngine;
using System.Collections.Generic;

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
        public VoxelEnemyVehicleTuning enemyCarTuning;

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

            float spawnDistanceAhead = obstacleCarTuning != null ? obstacleCarTuning.spawnDistanceAhead : 65f;
            if (runFinish != null && (runFinish.HasFinished ||
                target.TrackDistance + spawnDistanceAhead >= runFinish.FinishDistance - 12f))
                return;

            EndlessVoxelRoad path = target.TrackPath;
            float spawnTrackDistance = target.TrackDistance + spawnDistanceAhead;
            if (path == null)
                return;
            path.EnsurePathCovers(spawnTrackDistance + 10f);

            int minimumObjects = obstacleCarTuning != null ? obstacleCarTuning.minimumObjectsPerWave : 1;
            int maximumObjects = obstacleCarTuning != null ? obstacleCarTuning.maximumObjectsPerWave : 1;
            int requestedObjects = Random.Range(Mathf.Min(minimumObjects, maximumObjects),
                Mathf.Max(minimumObjects, maximumObjects) + 1);
            for (int index = 0; index < requestedObjects; index++)
                SpawnObject(path, spawnTrackDistance);

            ScheduleNextSpawn();
        }

        private void SpawnObject(EndlessVoxelRoad path, float distance)
        {
            bool spawnTrafficCar = obstacleCarTuning != null && Random.value < obstacleCarTuning.obstacleCarSpawnChance;
            if (spawnTrafficCar)
            {
                if (enemyCarTuning != null && Random.value < obstacleCarTuning.enemyCarSpawnChance)
                {
                    if (!TryFindEmptyVehicleLane(out float enemyLaneOffset))
                        return;

                    var enemy = new GameObject("Black Enemy Interceptor").AddComponent<VoxelEnemyCar>();
                    enemy.transform.SetParent(transform);
                    enemy.Configure(target, obstacleCarTuning, enemyCarTuning, path, distance, enemyLaneOffset);
                    enemy.gameObject.AddComponent<VoxelFadeIn>();
                    return;
                }

                bool sameDirection = Random.value >= obstacleCarTuning.oppositeDirectionChance;
                if (!TryFindCivilianLane(sameDirection, out float civilianLaneOffset, out float matchingSpeed))
                    return;

                var obstacle = new GameObject(sameDirection ? "Red Traffic Car (Same Direction)" : "Red Traffic Car (Oncoming)")
                    .AddComponent<VoxelObstacleCar>();
                obstacle.transform.SetParent(transform);
                obstacle.Configure(target, obstacleCarTuning, sameDirection, path, distance, civilianLaneOffset, matchingSpeed);
                obstacle.gameObject.AddComponent<VoxelFadeIn>();
            }
            else
            {
                if (!TryFindCompletelyEmptyLane(out float laneOffset))
                    return;

                var obstacle = new GameObject("Brown Voxel Obstacle").AddComponent<VoxelObstacle>();
                obstacle.transform.SetParent(transform);
                obstacle.Configure(target, path, distance, laneOffset);
                obstacle.gameObject.AddComponent<VoxelFadeIn>();
            }
        }

        private bool TryFindEmptyVehicleLane(out float laneOffset)
        {
            var availableLanes = new List<float>();
            for (int laneIndex = 0; laneIndex < laneCount; laneIndex++)
            {
                float candidateOffset = GetLaneOffset(laneIndex);
                if (!HasAnyVehicleInLane(candidateOffset) && !HasStaticObstacleInLane(candidateOffset))
                    availableLanes.Add(candidateOffset);
            }

            if (availableLanes.Count == 0)
            {
                laneOffset = 0f;
                return false;
            }

            laneOffset = availableLanes[Random.Range(0, availableLanes.Count)];
            return true;
        }

        private bool TryFindCivilianLane(bool travelsWithPlayer, out float laneOffset, out float matchingSpeed)
        {
            var availableLanes = new List<(float offset, float speed)>();
            for (int laneIndex = 0; laneIndex < laneCount; laneIndex++)
            {
                float candidateOffset = GetLaneOffset(laneIndex);
                if (HasEnemyInLane(candidateOffset) || HasStaticObstacleInLane(candidateOffset))
                    continue;

                bool hasCivilian = false;
                bool compatible = true;
                float laneSpeed = 0f;
                foreach (var civilian in GetComponentsInChildren<VoxelObstacleCar>())
                {
                    if (!IsInLane(civilian.LaneOffset, candidateOffset))
                        continue;
                    if (civilian.TravelsWithPlayer != travelsWithPlayer)
                    {
                        compatible = false;
                        break;
                    }

                    if (!hasCivilian)
                        laneSpeed = civilian.TravelSpeed;
                    else if (Mathf.Abs(civilian.TravelSpeed - laneSpeed) > obstacleCarTuning.sameLaneCivilianSpeedTolerance)
                    {
                        compatible = false;
                        break;
                    }
                    hasCivilian = true;
                }

                if (compatible)
                    availableLanes.Add((candidateOffset, hasCivilian ? laneSpeed : -1f));
            }

            if (availableLanes.Count == 0)
            {
                laneOffset = 0f;
                matchingSpeed = -1f;
                return false;
            }

            var selectedLane = availableLanes[Random.Range(0, availableLanes.Count)];
            laneOffset = selectedLane.offset;
            matchingSpeed = selectedLane.speed;
            return true;
        }

        private bool HasAnyVehicleInLane(float candidateOffset)
        {
            foreach (var civilian in GetComponentsInChildren<VoxelObstacleCar>())
                if (IsInLane(civilian.LaneOffset, candidateOffset))
                    return true;

            foreach (var enemy in GetComponentsInChildren<VoxelEnemyCar>())
                if (IsInLane(enemy.LaneOffset, candidateOffset))
                    return true;

            return false;
        }

        private bool HasEnemyInLane(float candidateOffset)
        {
            foreach (var enemy in GetComponentsInChildren<VoxelEnemyCar>())
                if (IsInLane(enemy.LaneOffset, candidateOffset))
                    return true;
            return false;
        }

        private bool TryFindCompletelyEmptyLane(out float laneOffset)
        {
            var availableLanes = new List<float>();
            for (int laneIndex = 0; laneIndex < laneCount; laneIndex++)
            {
                float candidateOffset = GetLaneOffset(laneIndex);
                if (!HasAnyVehicleInLane(candidateOffset) && !HasStaticObstacleInLane(candidateOffset))
                    availableLanes.Add(candidateOffset);
            }

            if (availableLanes.Count == 0)
            {
                laneOffset = 0f;
                return false;
            }

            laneOffset = availableLanes[Random.Range(0, availableLanes.Count)];
            return true;
        }

        private bool HasStaticObstacleInLane(float candidateOffset)
        {
            foreach (var obstacle in GetComponentsInChildren<VoxelObstacle>())
                if (IsInLane(obstacle.LaneOffset, candidateOffset))
                    return true;
            return false;
        }

        private float GetLaneOffset(int laneIndex) => (laneIndex - (laneCount - 1) * 0.5f) * laneWidth;
        private bool IsInLane(float firstOffset, float secondOffset) => Mathf.Abs(firstOffset - secondOffset) <= laneWidth * 0.25f;

        private void ScheduleNextSpawn()
        {
            nextSpawnTime = Time.time + Random.Range(minimumSpawnInterval, maximumSpawnInterval);
        }
    }
}
