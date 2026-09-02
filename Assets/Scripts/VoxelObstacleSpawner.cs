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
        private VoxelStaticObstacleSpawnEntry[] staticObstacleSpawns;

        private VoxelCarController target;
        private VoxelStartCountdown countdown;
        private VoxelRunFinish runFinish;
        private float nextSpawnTime;
        private bool trafficSpawnWindowOpened;

        public void SetTarget(VoxelCarController player) => target = player;
        public void SetStartCountdown(VoxelStartCountdown value) => countdown = value;
        public void SetRunFinish(VoxelRunFinish value) => runFinish = value;
        public void SetStaticObstacleSpawns(VoxelStaticObstacleSpawnEntry[] entries) => staticObstacleSpawns = entries;

        private void Start()
        {
            nextSpawnTime = Time.time + Random.Range(minimumSpawnInterval, maximumSpawnInterval);
        }

        private void Update()
        {
            if (!Application.isPlaying || target == null || target.IsDestroyed)
                return;

            if (countdown != null && !countdown.IsTrafficSpawnWindowOpen)
                return;

            // Force the first wave when the countdown changes to "1", rather than
            // waiting for a spawn interval that may otherwise elapse after "GO!".
            if (countdown != null && !trafficSpawnWindowOpened)
            {
                trafficSpawnWindowOpened = true;
                nextSpawnTime = Time.time;
            }

            if (Time.time < nextSpawnTime)
                return;

            float spawnDistanceAhead = obstacleCarTuning != null ? obstacleCarTuning.spawnDistanceAhead : 65f;
            if (runFinish != null && runFinish.HasFinished)
                return;

            EndlessVoxelRoad path = target.TrackPath;
            float spawnTrackDistance = target.TrackDistance + spawnDistanceAhead;
            if (path == null)
                return;

            int minimumObjects = obstacleCarTuning != null ? obstacleCarTuning.minimumObjectsPerWave : 1;
            int maximumObjects = obstacleCarTuning != null ? obstacleCarTuning.maximumObjectsPerWave : 1;
            int requestedObjects = Random.Range(Mathf.Min(minimumObjects, maximumObjects),
                Mathf.Max(minimumObjects, maximumObjects) + 1);
            float maximumWaveOffset = obstacleCarTuning != null
                ? Mathf.Max(obstacleCarTuning.minimumWaveObjectDistanceOffset, obstacleCarTuning.maximumWaveObjectDistanceOffset)
                : 20f;
            path.EnsurePathCovers(spawnTrackDistance + Mathf.Max(0, requestedObjects - 1) * maximumWaveOffset + 10f);

            float objectDistance = spawnTrackDistance;
            for (int index = 0; index < requestedObjects; index++)
            {
                if (index > 0)
                {
                    float minimumOffset = obstacleCarTuning != null ? obstacleCarTuning.minimumWaveObjectDistanceOffset : 12f;
                    float maximumOffset = obstacleCarTuning != null ? obstacleCarTuning.maximumWaveObjectDistanceOffset : 20f;
                    objectDistance += Random.Range(Mathf.Min(minimumOffset, maximumOffset), Mathf.Max(minimumOffset, maximumOffset));
                }

                SpawnObject(path, objectDistance);
            }

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
                // Turrets shoot straight through all lanes. Keep later static waves
                // away from their firing line as well as avoiding existing obstacles
                // when the turret itself is initially placed.
                foreach (var turret in FindObjectsByType<VoxelRoadsideTurret>(FindObjectsSortMode.None))
                    if (Mathf.Abs(turret.TrackDistance - distance) <= 12f)
                        return;

                if (!TryFindCompletelyEmptyLane(out float laneOffset))
                    return;

                VoxelStaticObstacleDefinition definition = ChooseStaticObstacle();
                if (definition == null)
                    return;

                switch (definition.obstacleType)
                {
                    case VoxelStaticObstacleType.Pothole:
                    {
                        var pothole = new GameObject(definition.displayName).AddComponent<VoxelPotholeObstacle>();
                        pothole.transform.SetParent(transform);
                        pothole.Configure(target, path, definition, distance, laneOffset, laneWidth);
                        pothole.gameObject.AddComponent<VoxelFadeIn>();
                        break;
                    }
                    case VoxelStaticObstacleType.FuelDrums:
                    {
                        var drums = new GameObject(definition.displayName).AddComponent<VoxelFuelDrumObstacle>();
                        drums.transform.SetParent(transform);
                        drums.Configure(target, path, definition, distance, laneOffset);
                        drums.gameObject.AddComponent<VoxelFadeIn>();
                        break;
                    }
                    default:
                    {
                        var obstacle = new GameObject(definition.displayName).AddComponent<VoxelObstacle>();
                        obstacle.transform.SetParent(transform);
                        obstacle.Configure(target, path, definition, distance, laneOffset);
                        obstacle.gameObject.AddComponent<VoxelFadeIn>();
                        break;
                    }
                }
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
                if (enemy.OccupiesLane(candidateOffset, laneWidth))
                    return true;

            return false;
        }

        private bool HasEnemyInLane(float candidateOffset)
        {
            foreach (var enemy in GetComponentsInChildren<VoxelEnemyCar>())
                if (enemy.OccupiesLane(candidateOffset, laneWidth))
                    return true;
            return false;
        }

        /// <summary>Finds a genuinely clear adjacent lane for a damaged interceptor to evade into.</summary>
        public bool TryFindSafeEnemyLane(VoxelEnemyCar requester, out float laneOffset)
        {
            var safeLanes = new List<float>();
            for (int laneIndex = 0; laneIndex < laneCount; laneIndex++)
            {
                float candidateOffset = GetLaneOffset(laneIndex);
                float lateralDistance = Mathf.Abs(candidateOffset - requester.LaneOffset);
                if (lateralDistance < laneWidth * 0.75f || lateralDistance > laneWidth * 1.25f)
                    continue;
                if (IsEnemyLaneClear(requester, candidateOffset))
                    safeLanes.Add(candidateOffset);
            }

            if (safeLanes.Count == 0)
            {
                laneOffset = requester.LaneOffset;
                return false;
            }

            laneOffset = safeLanes[Random.Range(0, safeLanes.Count)];
            return true;
        }

        private bool IsEnemyLaneClear(VoxelEnemyCar requester, float candidateOffset)
        {
            // The interceptor travels forward, so objects already behind it (between
            // the interceptor and player) are safe to merge behind. Only objects at
            // or ahead of it can be reached and therefore block the lane change.
            if (IsInLane(target.CurrentLaneOffset, candidateOffset) &&
                target.TrackDistance >= requester.TrackDistance)
                return false;

            foreach (var civilian in GetComponentsInChildren<VoxelObstacleCar>())
                if (IsInLane(civilian.LaneOffset, candidateOffset) &&
                    civilian.TrackDistance >= requester.TrackDistance)
                    return false;

            foreach (var enemy in GetComponentsInChildren<VoxelEnemyCar>())
                if (enemy != requester && enemy.OccupiesLane(candidateOffset, laneWidth) &&
                    enemy.TrackDistance >= requester.TrackDistance)
                    return false;

            foreach (var obstacle in GetComponentsInChildren<VoxelObstacle>())
                if (IsInLane(obstacle.LaneOffset, candidateOffset) &&
                    obstacle.TrackDistance >= requester.TrackDistance)
                    return false;
            foreach (var pothole in GetComponentsInChildren<VoxelPotholeObstacle>())
                if (IsInLane(pothole.LaneOffset, candidateOffset) &&
                    pothole.TrackDistance >= requester.TrackDistance)
                    return false;
            foreach (var drums in GetComponentsInChildren<VoxelFuelDrumObstacle>())
                if (IsInLane(drums.LaneOffset, candidateOffset) &&
                    drums.TrackDistance >= requester.TrackDistance)
                    return false;

            return true;
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
            foreach (var pothole in GetComponentsInChildren<VoxelPotholeObstacle>())
                if (IsInLane(pothole.LaneOffset, candidateOffset))
                    return true;
            foreach (var drums in GetComponentsInChildren<VoxelFuelDrumObstacle>())
                if (IsInLane(drums.LaneOffset, candidateOffset))
                    return true;
            return false;
        }

        /// <summary>Used by roadside hazards to avoid creating an unavoidable cross-road wall beside a static obstacle.</summary>
        public bool HasStaticObstacleNearTrackDistance(float candidateDistance, float clearance)
        {
            foreach (var obstacle in GetComponentsInChildren<VoxelObstacle>())
                if (Mathf.Abs(obstacle.TrackDistance - candidateDistance) <= clearance)
                    return true;
            foreach (var pothole in GetComponentsInChildren<VoxelPotholeObstacle>())
                if (Mathf.Abs(pothole.TrackDistance - candidateDistance) <= clearance)
                    return true;
            foreach (var drums in GetComponentsInChildren<VoxelFuelDrumObstacle>())
                if (Mathf.Abs(drums.TrackDistance - candidateDistance) <= clearance)
                    return true;
            return false;
        }

        private VoxelStaticObstacleDefinition ChooseStaticObstacle()
        {
            if (staticObstacleSpawns == null || staticObstacleSpawns.Length == 0)
                return null;

            float totalWeight = 0f;
            foreach (VoxelStaticObstacleSpawnEntry entry in staticObstacleSpawns)
                if (entry != null && entry.obstacle != null)
                    totalWeight += Mathf.Max(0f, entry.spawnWeight);
            if (totalWeight <= 0f)
                return null;

            float selectedWeight = Random.value * totalWeight;
            foreach (VoxelStaticObstacleSpawnEntry entry in staticObstacleSpawns)
            {
                if (entry == null || entry.obstacle == null)
                    continue;
                selectedWeight -= Mathf.Max(0f, entry.spawnWeight);
                if (selectedWeight <= 0f)
                    return entry.obstacle;
            }
            return null;
        }

        private float GetLaneOffset(int laneIndex) => (laneIndex - (laneCount - 1) * 0.5f) * laneWidth;
        private bool IsInLane(float firstOffset, float secondOffset) => Mathf.Abs(firstOffset - secondOffset) <= laneWidth * 0.25f;

        private void ScheduleNextSpawn()
        {
            nextSpawnTime = Time.time + Random.Range(minimumSpawnInterval, maximumSpawnInterval);
        }
    }
}
