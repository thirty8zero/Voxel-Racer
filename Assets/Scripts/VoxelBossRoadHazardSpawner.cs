using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Spawns boss-configured roadside hazards only while its encounter is in the boss stage.</summary>
    public sealed class VoxelBossRoadHazardSpawner : MonoBehaviour
    {
        private VoxelCarController player;
        private EndlessVoxelRoad road;
        private VoxelObstacleSpawner obstacleSpawner;
        private VoxelBossEncounter encounter;
        private VoxelBossRoadHazardAttackTuning tuning;
        private float nextTurretCheck;
        private float nextOilCheck;

        public void Configure(VoxelCarController target, EndlessVoxelRoad path, VoxelObstacleSpawner trafficSpawner,
            VoxelBossEncounter bossEncounter, VoxelBossRoadHazardAttackTuning settings)
        {
            player = target;
            road = path;
            obstacleSpawner = trafficSpawner;
            encounter = bossEncounter;
            tuning = settings;
            nextTurretCheck = Time.time + (tuning != null ? tuning.turretSpawnCheckInterval : 0f);
            nextOilCheck = Time.time + (tuning != null ? tuning.oilSlickSpawnCheckInterval : 0f);
        }

        private void Update()
        {
            if (player == null || road == null || obstacleSpawner == null || encounter == null || tuning == null ||
                player.IsDestroyed || encounter.CurrentStage != VoxelBossEncounter.Stage.Boss ||
                VoxelMissionProgress.Active?.IsComplete == true || VoxelMissionProgress.Active?.IsFailed == true)
                return;

            if (Time.time >= nextTurretCheck)
            {
                nextTurretCheck = Time.time + tuning.turretSpawnCheckInterval;
                TrySpawnTurret();
            }

            if (Time.time >= nextOilCheck)
            {
                nextOilCheck = Time.time + tuning.oilSlickSpawnCheckInterval;
                TrySpawnOilSlick();
            }
        }

        private void TrySpawnTurret()
        {
            VoxelRoadsideTurretTuning turretTuning = tuning.turretTuning;
            if (turretTuning == null || CountTurrets() >= tuning.maximumActiveTurrets ||
                Random.value >= tuning.turretSpawnChance)
                return;

            float distance = player.TrackDistance + tuning.turretSpawnDistanceAhead;
            if (obstacleSpawner.HasStaticObstacleNearTrackDistance(distance, turretTuning.staticObstacleClearance))
                return;

            road.EnsurePathCovers(distance + 10f);
            var turretObject = new GameObject(turretTuning.displayName);
            turretObject.transform.SetParent(transform);
            var turret = turretObject.AddComponent<VoxelRoadsideTurret>();
            turret.Configure(player, road, turretTuning, distance, Random.value < .5f ? -1f : 1f);
            turretObject.AddComponent<VoxelFadeIn>();
        }

        private void TrySpawnOilSlick()
        {
            if (CountOilSlicks() >= tuning.maximumActiveOilSlicks ||
                Random.value >= tuning.oilSlickSpawnChance)
                return;

            VoxelStaticObstacleDefinition definition = ChooseOilSlick();
            if (definition == null)
                return;

            float distance = player.TrackDistance + tuning.oilSlickSpawnDistanceAhead;
            if (obstacleSpawner.HasStaticObstacleNearTrackDistance(distance, tuning.oilSlickClearance) ||
                !TryChooseOilLane(distance, out float laneOffset))
                return;

            road.EnsurePathCovers(distance + 10f);
            var oil = new GameObject(definition.displayName).AddComponent<VoxelOilSlickObstacle>();
            oil.transform.SetParent(obstacleSpawner.transform);
            oil.Configure(player, road, definition, distance, laneOffset, obstacleSpawner.laneWidth);
            oil.gameObject.AddComponent<VoxelFadeIn>();
        }

        private VoxelStaticObstacleDefinition ChooseOilSlick()
        {
            float totalWeight = 0f;
            if (tuning.oilSlickSpawns != null)
                foreach (VoxelStaticObstacleSpawnEntry entry in tuning.oilSlickSpawns)
                    if (entry != null && entry.obstacle != null &&
                        entry.obstacle.obstacleType == VoxelStaticObstacleType.OilSlick)
                        totalWeight += Mathf.Max(0f, entry.spawnWeight);

            if (totalWeight <= 0f)
                return null;

            float selectedWeight = Random.value * totalWeight;
            foreach (VoxelStaticObstacleSpawnEntry entry in tuning.oilSlickSpawns)
            {
                if (entry == null || entry.obstacle == null ||
                    entry.obstacle.obstacleType != VoxelStaticObstacleType.OilSlick)
                    continue;
                selectedWeight -= Mathf.Max(0f, entry.spawnWeight);
                if (selectedWeight <= 0f)
                    return entry.obstacle;
            }
            return null;
        }

        private bool TryChooseOilLane(float distance, out float laneOffset)
        {
            var availableLanes = new List<float>();
            for (int lane = 0; lane < obstacleSpawner.laneCount; lane++)
            {
                float candidate = (lane - (obstacleSpawner.laneCount - 1) * .5f) * obstacleSpawner.laneWidth;
                if (Mathf.Abs(candidate - player.CurrentLaneOffset) < obstacleSpawner.laneWidth * .3f)
                    continue;

                bool occupied = false;
                foreach (var oil in obstacleSpawner.GetComponentsInChildren<VoxelOilSlickObstacle>())
                    if (Mathf.Abs(oil.LaneOffset - candidate) <= obstacleSpawner.laneWidth * .25f &&
                        Mathf.Abs(oil.TrackDistance - distance) <= tuning.oilSlickClearance)
                    {
                        occupied = true;
                        break;
                    }
                if (!occupied)
                    availableLanes.Add(candidate);
            }

            if (availableLanes.Count == 0)
            {
                laneOffset = 0f;
                return false;
            }

            laneOffset = availableLanes[Random.Range(0, availableLanes.Count)];
            return true;
        }

        private int CountTurrets() => GetComponentsInChildren<VoxelRoadsideTurret>().Length;
        private int CountOilSlicks() => obstacleSpawner.GetComponentsInChildren<VoxelOilSlickObstacle>().Length;
    }
}
