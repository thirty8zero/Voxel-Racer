using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    public sealed class VoxelMissionBriefingEnemy
    {
        public Object Source;
        public GameObject Model;
        public string Name, Trait;
    }

    /// <summary>Read-only mission information; never samples gameplay RNG or edits tuning.</summary>
    public static class VoxelMissionBriefingContent
    {
        public static List<VoxelMissionBriefingEnemy> Enemies(VoxelTrackDefinition track)
        {
            var result = new List<VoxelMissionBriefingEnemy>();
            if (track == null) return result;
            if (track.boss != null)
            {
                var radar = Resources.Load<VoxelEnemyVehicleTuning>("EnemyVehicles/RadarInterceptorTuning");
                AddVehicle(result, radar, "RADAR TARGET");
                result.Add(new VoxelMissionBriefingEnemy { Source = track.boss, Model = track.boss.bossPrefab,
                    Name = track.boss.bossName, Trait = "BOSS" });
                return result; // Boss approach suppresses ordinary enemies and roadside turrets.
            }
            var traffic = track.obstacleCarTuning != null ? track.obstacleCarTuning : VoxelObstacleCarTuning.Load();
            if (traffic != null && (traffic.enemyDroughtWaveLimit > 0 ||
                traffic.obstacleCarSpawnChance > 0 && traffic.enemyCarSpawnChance > 0))
                foreach (var entry in traffic.enemyVehicleSpawns ?? System.Array.Empty<VoxelEnemyVehicleSpawnEntry>())
                    if (entry != null && entry.vehicle != null && entry.vehicle.modelPrefab != null &&
                        entry.spawnWeight > 0 && !float.IsInfinity(entry.spawnWeight))
                        AddVehicle(result, entry.vehicle, null);
            var mission = track.missionTuning != null ? track.missionTuning : VoxelMissionTuning.Load();
            if (mission != null && mission.roadsideTurretTuning != null && mission.roadsideTurretSpawnChance > 0 &&
                mission.maximumActiveRoadsideTurrets > 0)
                result.Add(new VoxelMissionBriefingEnemy { Source = mission.roadsideTurretTuning,
                    Model = Resources.Load<GameObject>("EnemyTurrets/Model/RoadsideTurretModel"),
                    Name = "ROADSIDE TURRET", Trait = "ROADSIDE FIRE" });
            return result;
        }

        private static void AddVehicle(List<VoxelMissionBriefingEnemy> entries, VoxelEnemyVehicleTuning vehicle, string trait)
        {
            if (vehicle == null) return;
            foreach (var entry in entries) if (entry.Source == vehicle) return;
            entries.Add(new VoxelMissionBriefingEnemy { Source = vehicle, Model = vehicle.modelPrefab,
                Name = vehicle.psychoBug != null ? "PSYCHO BUG" : vehicle.displayName,
                Trait = trait ?? (vehicle.psychoBug != null ? "SIDE RAMMER" : vehicle.mineLayer != null ? "DROPS MINES" : "PURSUIT") });
        }

        public static string Objective(VoxelTrackDefinition track) => track != null && track.boss != null
            ? "FIND THE RADAR. DEFEAT THE BOSS."
            : ((track != null && track.missionTuning != null ? track.missionTuning : VoxelMissionTuning.Load())?.requiredPoints ?? 0).ToString("N0");
    }
}
