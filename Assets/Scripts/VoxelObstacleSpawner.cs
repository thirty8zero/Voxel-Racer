using UnityEngine;
using System.Collections.Generic;
using Unity.Profiling;

namespace VoxelRacer
{
    /// <summary>Creates simple lane obstacles ahead of the moving car.</summary>
    public sealed class VoxelObstacleSpawner : MonoBehaviour
    {
        public static VoxelObstacleSpawner Active { get; private set; }
        public int WavesSpawned { get; private set; }
        public int QueuedObjects => pendingSpawnRequests.Count;
        public bool WaitingForOpening => countdown != null && !countdown.IsTrafficSpawnWindowOpen;
        public bool SpawningFinished => !isActiveAndEnabled || target == null || target.IsDestroyed ||
            (runFinish != null && runFinish.HasFinished) || VoxelMissionProgress.Active?.IsComplete == true;
        /// <summary>Estimated game seconds at the current speed; the same rate drives Update.</summary>
        public float NextWaveSeconds => Mathf.Max(0f, spawnTimeRemaining) / WaveCountdownRate;
        private float WaveCountdownRate => target != null && target.EffectiveTopSpeed > 0f
            ? Mathf.Max(1f, target.CurrentSpeed / target.EffectiveTopSpeed) : 1f;

        private void OnEnable() => Active = this;
        private void OnDisable() { if (Active == this) Active = null; }

        [Header("Spawn Timing")]
        [Min(0.1f)] public float minimumSpawnInterval = 2.5f;
        [Min(0.1f)] public float maximumSpawnInterval = 4.5f;

        [Header("Lane Layout")]
        [Min(1)] public int laneCount = 4;
        [Min(0.1f)] public float laneWidth = 3f;

        [Header("Obstacle Types")]
        [Tooltip("Controls traffic-car spawn frequency, direction, speed, impacts, and debris.")]
        public VoxelObstacleCarTuning obstacleCarTuning;
        // Retain old scene references; track traffic pools now own enemy selection.
        [HideInInspector] public VoxelEnemyVehicleTuning enemyCarTuning;
        [HideInInspector] public VoxelEnemyVehicleTuning mineLayerEnemyTuning;
        private VoxelStaticObstacleSpawnEntry[] staticObstacleSpawns;

        private VoxelBossEncounter radarEncounter;
        private VoxelEnemyVehicleTuning radarTuning;
        private VoxelObstacleCar activeRadar;
        private float radarChance;
        public void ConfigureRadarObjective(VoxelBossEncounter encounter, float chance)
        {
            radarEncounter = encounter;
            radarChance = Mathf.Clamp(chance, 1, 100) / 100f;
            radarTuning = Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/RadarInterceptorTuning");
        }

        private VoxelCarController target;
        private VoxelStartCountdown countdown;
        private VoxelRunFinish runFinish;
        private float spawnTimeRemaining;
        private bool trafficSpawnWindowOpened;
        private int activeSpawnWave;
        private int lastEnemySpawnWave;
        private readonly Queue<SpawnRequest> pendingSpawnRequests = new();
        private readonly List<MonoBehaviour> laneOccupants = new();
        private readonly List<VoxelObstacleCar> laneTraffic = new();
        private readonly List<VoxelEnemyCar> laneEnemies = new();
        private readonly List<VoxelObstacle> laneCrates = new();
        private readonly List<VoxelPotholeObstacle> lanePotholes = new();
        private readonly List<VoxelFuelDrumObstacle> laneDrums = new();
        private readonly List<VoxelOilSlickObstacle> laneOil = new();
        private readonly List<float> laneChoices = new();
        private readonly List<(float offset, float speed)> civilianLaneChoices = new();
        private static readonly ProfilerMarker spawnMarker = new ProfilerMarker("VoxelObstacleSpawner.SpawnObject");
        private static readonly ProfilerMarker lanesMarker = new ProfilerMarker("VoxelObstacleSpawner.LaneSnapshot");

        // Refresh once per decision, so new spawns, inactive objects, reparenting
        // and reserved enemy lanes retain their original occupancy semantics.
        private void RefreshLaneOccupants()
        {
            using var profile = lanesMarker.Auto();
            GetComponentsInChildren(false, laneOccupants);
            laneTraffic.Clear(); laneEnemies.Clear(); laneCrates.Clear();
            lanePotholes.Clear(); laneDrums.Clear(); laneOil.Clear();
            foreach (var occupant in laneOccupants)
            {
                switch (occupant)
                {
                    case VoxelObstacleCar car: laneTraffic.Add(car); break;
                    case VoxelEnemyCar enemy: laneEnemies.Add(enemy); break;
                    case VoxelObstacle crate: laneCrates.Add(crate); break;
                    case VoxelPotholeObstacle pothole: lanePotholes.Add(pothole); break;
                    case VoxelFuelDrumObstacle drums: laneDrums.Add(drums); break;
                    case VoxelOilSlickObstacle oil: laneOil.Add(oil); break;
                }
            }
        }

        private struct SpawnRequest
        {
            public readonly EndlessVoxelRoad Path;
            public readonly float Distance;
            public readonly int Wave;

            public SpawnRequest(EndlessVoxelRoad path, float distance, int wave)
            {
                Path = path;
                Distance = distance;
                Wave = wave;
            }
        }

        public void SetTarget(VoxelCarController player) => target = player;
        public void StopSpawning()
        {
            pendingSpawnRequests.Clear();
            enabled=false;
        }
        public void SetStartCountdown(VoxelStartCountdown value) => countdown = value;
        public void SetRunFinish(VoxelRunFinish value) => runFinish = value;
        public void SetStaticObstacleSpawns(VoxelStaticObstacleSpawnEntry[] entries) => staticObstacleSpawns = entries;

        private void Start()
        {
            ScheduleNextSpawn();
        }

        private void Update()
        {
            if (VoxelPauseMenu.IsPaused) return;
            if (!Application.isPlaying || target == null || target.IsDestroyed)
                return;

            if (countdown != null && !countdown.IsTrafficSpawnWindowOpen)
                return;

            // Never allow a queued item from the last traffic wave to appear
            // during the mission-complete presentation.
            if (runFinish != null && runFinish.HasFinished || VoxelMissionProgress.Active?.IsComplete == true)
            {
                pendingSpawnRequests.Clear();
                return;
            }

            // Force the first wave when the countdown changes to "1", rather than
            // waiting for a spawn interval that may otherwise elapse after "GO!".
            if (countdown != null && !trafficSpawnWindowOpened)
            {
                trafficSpawnWindowOpened = true;
                spawnTimeRemaining = 0f;
            }

            // Vehicle visual construction creates hundreds of voxel GameObjects.
            // Spread a wave across frames so a dense road does not produce one
            // large allocation/collider-registration spike in the Editor or build.
            if (pendingSpawnRequests.Count > 0)
            {
                SpawnRequest request = pendingSpawnRequests.Dequeue();
                SpawnWaveObject(request.Path, request.Distance, request.Wave);
            }

            // Actual travel above the engine-adjusted normal maximum brings the
            // next wave forward. Braking and initial acceleration keep normal pacing.
            spawnTimeRemaining -= Time.deltaTime * WaveCountdownRate;
            if (spawnTimeRemaining > 0f)
                return;

            float spawnDistanceAhead = obstacleCarTuning != null ? obstacleCarTuning.spawnDistanceAhead : 220f;
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

                pendingSpawnRequests.Enqueue(new SpawnRequest(path, objectDistance, WavesSpawned + 1));
            }

            WavesSpawned++;
            ScheduleNextSpawn();
        }

        private void SpawnWaveObject(EndlessVoxelRoad path, float distance, int wave)
        {
            // Requests retain their wave identity even when a wave spans frames or
            // another wave is queued before it drains. Only successful spawns reset drought.
            activeSpawnWave = wave;
            try
            {
                if (obstacleCarTuning != null && obstacleCarTuning.enemyDroughtWaveLimit > 0 &&
                    wave - lastEnemySpawnWave > obstacleCarTuning.enemyDroughtWaveLimit &&
                    TrySpawnDroughtEnemy(path, distance)) return;
                SpawnObject(path, distance);
            }
            finally { activeSpawnWave = 0; }
        }

        private void CreateEnemy(EndlessVoxelRoad path, float distance, float offset, VoxelEnemyVehicleTuning selected)
        {
            var enemy = new GameObject(selected.displayName).AddComponent<VoxelEnemyCar>();
            enemy.transform.SetParent(transform);
            enemy.Configure(target, obstacleCarTuning, selected, path, distance, offset);
            enemy.gameObject.AddComponent<VoxelFadeIn>();
            VoxelSpawnScaleIn.Play(enemy.gameObject);
            if (activeSpawnWave > 0) lastEnemySpawnWave = activeSpawnWave;
        }

        private bool TrySpawnDroughtEnemy(EndlessVoxelRoad path, float distance)
        {
            if (obstacleCarTuning.enemyVehicleSpawns == null) return false;
            RefreshLaneOccupants();
            VoxelEnemyVehicleTuning selected = null;
            float selectedOffset = 0f;
            double totalWeight = 0;
            foreach (var entry in obstacleCarTuning.enemyVehicleSpawns)
            {
                if (entry?.vehicle == null || entry.vehicle.modelPrefab == null ||
                    !(entry.spawnWeight > 0f) || float.IsInfinity(entry.spawnWeight)) continue;
                laneChoices.Clear();
                for (int lane = 0; lane < laneCount; lane++)
                {
                    float offset = GetLaneOffset(lane);
                    if (IsDroughtEnemyLaneSafe(entry.vehicle, offset, distance)) laneChoices.Add(offset);
                }
                if (laneChoices.Count == 0) continue;
                // Weighted reservoir draw among eligible types; an unsafe selected
                // model must not hide a smaller, safe model from the track pool.
                totalWeight += entry.spawnWeight;
                if (selected == null || Random.value < entry.spawnWeight / totalWeight)
                {
                    selected = entry.vehicle;
                    selectedOffset = laneChoices[Random.Range(0, laneChoices.Count)];
                }
            }
            if (selected == null) return false;
            CreateEnemy(path, distance, selectedOffset, selected);
            return true;
        }

        private bool IsDroughtEnemyLaneSafe(VoxelEnemyVehicleTuning vehicle, float offset, float distance)
        {
            float length = Mathf.Max(.1f, vehicle.collisionHalfLength);
            float width = Mathf.Max(.1f, vehicle.collisionHalfWidth);
            const float margin = VoxelObstacleCar.TrafficGap;
            bool OverlapsWidth(float otherOffset, float otherWidth) => Mathf.Abs(offset - otherOffset) < width + otherWidth;
            bool Passed(float otherDistance, float otherLength) => distance - otherDistance > length + otherLength + margin;

            // Static hazards ahead are reachable. Passed hazards must also clear the
            // complete rear envelope, rather than just having their centre behind us.
            foreach (var crate in laneCrates)
                if (OverlapsWidth(crate.LaneOffset, 1f) && !Passed(crate.TrackDistance, 1f)) return false;
            foreach (var drums in laneDrums)
                if (OverlapsWidth(drums.LaneOffset, 2.2f) && !Passed(drums.TrackDistance, 2.4f)) return false;
            foreach (var pothole in lanePotholes)
                if (OverlapsWidth(pothole.LaneOffset, laneWidth * .42f) && !Passed(pothole.TrackDistance, 1.6f)) return false;
            foreach (var oil in laneOil)
                if (OverlapsWidth(oil.LaneOffset, laneWidth * .4f) && !Passed(oil.TrackDistance, 1.85f)) return false;

            // Preserve existing enemies' merge/attack lane reservations. Their
            // acceleration, reversing and ram responses make a shared lane unsafe.
            foreach (var enemy in laneEnemies)
                if (enemy.OccupiesLane(offset, laneWidth) ||
                    OverlapsWidth(enemy.LaneOffset, enemy.Tuning != null ? enemy.Tuning.collisionHalfWidth : width)) return false;

            foreach (var civilian in laneTraffic)
            {
                if (!OverlapsWidth(civilian.LaneOffset, civilian.TrafficHalfWidth)) continue;
                if (!civilian.IsDrivingTraffic || !Passed(civilian.TrackDistance, civilian.TrafficHalfLength)) return false;
                // A passed oncoming car moves away. A same-direction car is safe
                // only if none of its later phase speeds can catch the enemy's floor.
                // Bugs may stop/reverse while positioning, so don't assume a floor.
                if (civilian.TravelsWithPlayer && civilian.MaximumPhaseTravelSpeed >
                    (vehicle.psychoBug != null ? 0f : Mathf.Max(0f, vehicle.minimumDrivingSpeed))) return false;
            }
            return true;
        }

        private void SpawnObject(EndlessVoxelRoad path, float distance)
        {
            using var profile = spawnMarker.Auto();
            bool spawnTrafficCar = obstacleCarTuning != null && Random.value < obstacleCarTuning.obstacleCarSpawnChance;
            if (spawnTrafficCar)
            {
                if (Random.value < obstacleCarTuning.enemyCarSpawnChance)
                {
                    var selected = obstacleCarTuning.ChooseEnemyVehicle();
                    if (selected != null)
                    {
                        if (!TryFindEmptyVehicleLane(out float enemyLaneOffset)) return;
                        CreateEnemy(path, distance, enemyLaneOffset, selected);
                        return;
                    }
                }

                bool spawnRadar = radarEncounter != null && radarEncounter.CurrentStage == VoxelBossEncounter.Stage.Traffic &&
                    activeRadar == null && radarTuning != null && Random.value < radarChance;
                if (!spawnRadar && !obstacleCarTuning.HasCivilianVehicles) return;
                bool sameDirection = Random.value >= obstacleCarTuning.oppositeDirectionChance;
                if (!TryFindCivilianLane(sameDirection, distance, out float civilianLaneOffset, out float matchingSpeed))
                    return;

                var obstacle = new GameObject(sameDirection ? "Red Traffic Car (Same Direction)" : "Red Traffic Car (Oncoming)")
                    .AddComponent<VoxelObstacleCar>();
                obstacle.transform.SetParent(transform);
                if (!obstacle.Configure(target, obstacleCarTuning, sameDirection, path, distance, civilianLaneOffset, matchingSpeed, spawnRadar ? radarTuning : null))
                {
                    Destroy(obstacle.gameObject);
                    return;
                }
                if (spawnRadar)
                {
                    obstacle.name = "Radar Interceptor";
                    activeRadar = obstacle;
                    obstacle.Defeated += radarEncounter.RadarDefeated;
                }
                obstacle.gameObject.AddComponent<VoxelFadeIn>();
                VoxelSpawnScaleIn.Play(obstacle.gameObject);
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
                    case VoxelStaticObstacleType.OilSlick:
                    {
                        var oil = new GameObject(definition.displayName).AddComponent<VoxelOilSlickObstacle>();
                        oil.transform.SetParent(transform);
                        oil.Configure(target, path, definition, distance, laneOffset, laneWidth);
                        oil.gameObject.AddComponent<VoxelFadeIn>();
                        VoxelSpawnScaleIn.Play(oil.gameObject);
                        break;
                    }
                    case VoxelStaticObstacleType.Pothole:
                    {
                        var pothole = new GameObject(definition.displayName).AddComponent<VoxelPotholeObstacle>();
                        pothole.transform.SetParent(transform);
                        pothole.Configure(target, path, definition, distance, laneOffset, laneWidth);
                        pothole.gameObject.AddComponent<VoxelFadeIn>();
                        VoxelSpawnScaleIn.Play(pothole.gameObject);
                        break;
                    }
                    case VoxelStaticObstacleType.FuelDrums:
                    {
                        var drums = new GameObject(definition.displayName).AddComponent<VoxelFuelDrumObstacle>();
                        drums.transform.SetParent(transform);
                        drums.Configure(target, path, definition, distance, laneOffset);
                        drums.gameObject.AddComponent<VoxelFadeIn>();
                        VoxelSpawnScaleIn.Play(drums.gameObject);
                        break;
                    }
                    default:
                    {
                        var obstacle = new GameObject(definition.displayName).AddComponent<VoxelObstacle>();
                        obstacle.transform.SetParent(transform);
                        obstacle.Configure(target, path, definition, distance, laneOffset);
                        obstacle.gameObject.AddComponent<VoxelFadeIn>();
                        VoxelSpawnScaleIn.Play(obstacle.gameObject);
                        break;
                    }
                }
            }
        }

        private bool TryFindEmptyVehicleLane(out float laneOffset)
        {
            RefreshLaneOccupants();
            var availableLanes = laneChoices; availableLanes.Clear();
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

        private bool TryFindCivilianLane(bool travelsWithPlayer, float spawnDistance, out float laneOffset, out float matchingSpeed)
        {
            RefreshLaneOccupants();
            float spawnHalfLength=2.3f;
            if (obstacleCarTuning.CivilianVehiclePool != null)
                foreach (var vehicle in obstacleCarTuning.CivilianVehiclePool)
                    if (vehicle != null && vehicle.modelPrefab != null) spawnHalfLength = Mathf.Max(spawnHalfLength, vehicle.collisionHalfLength);
            if (radarTuning != null) spawnHalfLength = Mathf.Max(spawnHalfLength, radarTuning.collisionHalfLength);
            var availableLanes = civilianLaneChoices; availableLanes.Clear();
            for (int laneIndex = 0; laneIndex < laneCount; laneIndex++)
            {
                float candidateOffset = GetLaneOffset(laneIndex);
                if (HasEnemyInLane(candidateOffset) || HasStaticObstacleInLane(candidateOffset))
                    continue;

                bool hasCivilian = false;
                bool compatible = true;
                float laneSpeed = 0f;
                foreach (var civilian in laneTraffic)
                {
                    if (!IsInLane(civilian.LaneOffset, candidateOffset))
                        continue;
                    if(Mathf.Abs(civilian.TrackDistance-spawnDistance)<spawnHalfLength+civilian.TrafficHalfLength+VoxelObstacleCar.TrafficGap)
                    {
                        compatible=false;
                        break;
                    }
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
            foreach (var civilian in laneTraffic)
                if (IsInLane(civilian.LaneOffset, candidateOffset))
                    return true;

            foreach (var enemy in laneEnemies)
                if (enemy.OccupiesLane(candidateOffset, laneWidth))
                    return true;

            return false;
        }

        private bool HasEnemyInLane(float candidateOffset)
        {
            foreach (var enemy in laneEnemies)
                if (enemy.OccupiesLane(candidateOffset, laneWidth))
                    return true;
            return false;
        }

        /// <summary>Finds a genuinely clear adjacent lane for a damaged interceptor to evade into.</summary>
        public bool TryFindSafeEnemyLane(VoxelEnemyCar requester, out float laneOffset)
        {
            RefreshLaneOccupants();
            var safeLanes = laneChoices; safeLanes.Clear();
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

            foreach (var civilian in laneTraffic)
                if (IsInLane(civilian.LaneOffset, candidateOffset) &&
                    civilian.TrackDistance >= requester.TrackDistance)
                    return false;

            foreach (var enemy in laneEnemies)
                if (enemy != requester && enemy.OccupiesLane(candidateOffset, laneWidth) &&
                    enemy.TrackDistance >= requester.TrackDistance)
                    return false;

            foreach (var obstacle in laneCrates)
                if (IsInLane(obstacle.LaneOffset, candidateOffset) &&
                    obstacle.TrackDistance >= requester.TrackDistance)
                    return false;
            foreach (var pothole in lanePotholes)
                if (IsInLane(pothole.LaneOffset, candidateOffset) &&
                    pothole.TrackDistance >= requester.TrackDistance)
                    return false;
            foreach (var drums in laneDrums)
                if (IsInLane(drums.LaneOffset, candidateOffset) &&
                    drums.TrackDistance >= requester.TrackDistance)
                    return false;

            return true;
        }

        private bool TryFindCompletelyEmptyLane(out float laneOffset)
        {
            RefreshLaneOccupants();
            var availableLanes = laneChoices; availableLanes.Clear();
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
            foreach (var oil in laneOil)
                if (IsInLane(oil.LaneOffset, candidateOffset)) return true;
            foreach (var obstacle in laneCrates)
                if (IsInLane(obstacle.LaneOffset, candidateOffset))
                    return true;
            foreach (var pothole in lanePotholes)
                if (IsInLane(pothole.LaneOffset, candidateOffset))
                    return true;
            foreach (var drums in laneDrums)
                if (IsInLane(drums.LaneOffset, candidateOffset))
                    return true;
            return false;
        }

        /// <summary>Used by roadside hazards to avoid creating an unavoidable cross-road wall beside a static obstacle.</summary>
        public bool HasStaticObstacleNearTrackDistance(float candidateDistance, float clearance)
        {
            RefreshLaneOccupants();
            foreach (var oil in laneOil)
                if (Mathf.Abs(oil.TrackDistance - candidateDistance) <= clearance) return true;
            foreach (var obstacle in laneCrates)
                if (Mathf.Abs(obstacle.TrackDistance - candidateDistance) <= clearance)
                    return true;
            foreach (var pothole in lanePotholes)
                if (Mathf.Abs(pothole.TrackDistance - candidateDistance) <= clearance)
                    return true;
            foreach (var drums in laneDrums)
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
            spawnTimeRemaining = Random.Range(minimumSpawnInterval, maximumSpawnInterval);
        }
    }
}
