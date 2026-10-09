using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    public static class VoxelWheelSpikeDamageValidation
    {
        [MenuItem("Tools/Voxel Racer/Validate Wheel Spike Damage")]
        public static void Run()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Run in Edit Mode.");
            var ownership = typeof(VoxelWheelSpikeUpgradeState).GetField("purchased", BindingFlags.Static | BindingFlags.NonPublic);
            var selection = typeof(VoxelWheelSpikeUpgradeState).GetField("installedTuning", BindingFlags.Static | BindingFlags.NonPublic);
            var savedSelection = selection.GetValue(null);
            bool wasPurchased = VoxelWheelSpikeUpgradeState.IsPurchased;
            int cash = VoxelCurrencyState.Balance;
            int checks = 0;
            try
            {
                foreach (var tuning in Resources.LoadAll<VoxelWheelSpikeTuning>("Upgrades"))
                {
                selection.SetValue(null,tuning);
                float multiplier = 1f + Mathf.Max(0f, tuning.sideRamDamageBonusPercent) / 100f;
                foreach (bool installed in new[] { false, true })
                {
                    ownership.SetValue(null, installed);
                    void Check(float baseDamage)
                    {
                        float side = VoxelWheelSpikeUpgradeState.CalculateRamDamage(baseDamage, false);
                        float rear = VoxelWheelSpikeUpgradeState.CalculateRamDamage(baseDamage, true);
                        if (!Mathf.Approximately(side, baseDamage * (installed ? multiplier : 1f)) ||
                            !Mathf.Approximately(rear, baseDamage))
                            throw new Exception($"Incorrect wheel spike damage for base {baseDamage}, installed {installed}.");
                        checks++;
                    }
                    Check(0f); Check(.5f);
                    foreach (var enemy in Resources.LoadAll<VoxelEnemyVehicleTuning>("EnemyVehicles")) Check(enemy.playerRamDamage);
                    foreach (var boss in Resources.LoadAll<VoxelBossDefinition>("Bosses")) Check(boss.playerRamDamage);
                }
                }
            }
            finally { ownership.SetValue(null, wasPurchased); selection.SetValue(null,savedSelection); }
            if (cash != VoxelCurrencyState.Balance || wasPurchased != VoxelWheelSpikeUpgradeState.IsPurchased)
                throw new Exception("Validation changed purchase state.");
            Debug.Log($"Wheel spike percentage checks passed: {checks} cases across all spike variants and enemy/boss tunings, installed/unowned, side/rear, zero/fractional base damage; cash and ownership preserved.");
        }
    }
}
