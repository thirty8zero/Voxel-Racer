using UnityEngine;
namespace VoxelRacer
{
    public static class VoxelSphericalBlast
    {
        public static bool Contains(MeshRenderer voxel, Vector3 centre, float radius) => voxel != null && voxel.enabled &&
            voxel.gameObject.activeInHierarchy && (voxel.bounds.center - centre).sqrMagnitude <= radius * radius &&
            voxel.GetComponentInParent<VoxelIndestructiblePart>() == null && voxel.GetComponentInParent<VoxelEnemyHealthBar>() == null;
    }
    public sealed partial class VoxelEnemyCar
    {
        public void TakeMissileBlast(Vector3 centre, float radius, float damage, Vector3 direction, MeshRenderer[] pieces, bool directImpact = false)
        {
            if (hasBeenRammed || Tuning == null || damage <= 0 || IsBodyShielded) return;
            if (IsBoss)
            {
                damage *= Mathf.Max(0, bossSettings.missileHealthDamageMultiplier);
                radius *= Mathf.Max(0, bossSettings.missileBlastRadiusMultiplier);
                if (damage <= 0) return;
            }
            int removed = 0; bool touched = false;
            int debrisLimit = IsBoss ? Mathf.Max(0, bossSettings.maximumMissileDebris) : 24;
            if (IsBoss && bossSettings.useFixedMissileVoxelDamage)
            {
                var candidates = new System.Collections.Generic.List<Transform>();
                bool protectedBodyRemains = false;
                foreach (var r in pieces)
                {
                    if (VoxelSphericalBlast.Contains(r, centre, radius)) touched = true;
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy ||
                        r.GetComponentInParent<VoxelIndestructiblePart>() != null ||
                        r.GetComponentInParent<VoxelEnemyHealthBar>() != null) continue;
                    if (IsSpikeAttackActivePart(r.transform))
                    {
                        if (!DamageParts.IsWheel(r.transform)) protectedBodyRemains = true;
                        continue;
                    }
                    candidates.Add(r.transform);
                }
                if (!touched && !directImpact) return;
                if (protectedBodyRemains) candidates.RemoveAll(DamageParts.IsWheel);
                DamageParts.Sort(candidates, centre);
                removed = VoxelVehicleDamageParts.DetachSorted(candidates, Mathf.Max(0, bossSettings.missileVoxelsRemoved), debrisLimit,
                    voxel => { if (Application.isPlaying) SpawnDebris(voxel, (voxel.position - centre).normalized, DebrisStyle.Weapon); });
                foreach (var voxel in candidates) if (!voxel.gameObject.activeInHierarchy) voxelHealth.Remove(voxel);
            }
            else
            {
                // Blast bodywork first. Wheels survive while bodywork remains elsewhere.
                for (int pass = 0; pass < 2; pass++)
                {
                    bool allowWheels = pass == 1 && !DamageParts.HasBody;
                    foreach (var r in pieces)
                    {
                        if (!VoxelSphericalBlast.Contains(r, centre, radius) || DamageParts.IsWheel(r.transform) != (pass == 1)) continue;
                        touched = true;
                        if ((pass == 1 && !allowWheels) || IsSpikeAttackActivePart(r.transform)) continue;
                        voxelHealth.Remove(r.transform);
                        if (Application.isPlaying && removed < debrisLimit) SpawnDebris(r.transform, (r.bounds.center - centre).normalized, DebrisStyle.Weapon);
                        r.gameObject.SetActive(false); removed++;
                    }
                }
            }
            if (!touched && !directImpact) return;
            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            VoxelMissionProgress.ReportEnemyVoxelDestroyed(removed, centre);
            VoxelMissionProgress.ReportEnemyVoxelDamage(removed);
            TryRequestEvasiveLaneChange(); CheckBossBodyDestroyed();
            if (healthBar != null) healthBar.SetHealth(HealthPercent);
            if (CurrentHealth <= 0) Explode(centre, direction);
        }
    }
    public sealed partial class VoxelObstacleCar
    {
        public void TakeMissileBlast(Vector3 centre, float radius, float damage, Vector3 direction, MeshRenderer[] pieces, bool directImpact = false)
        {
            if (hasBeenHit || EnemyTuning == null || damage <= 0) return;
            int removed = 0; bool touched = false;
            for (int pass = 0; pass < 2; pass++)
            {
                bool allowWheels = pass == 1 && !DamageParts.HasBody;
                foreach (var r in pieces)
                {
                    if (!VoxelSphericalBlast.Contains(r, centre, radius) || DamageParts.IsWheel(r.transform) != (pass == 1)) continue;
                    touched = true;
                    if (pass == 1 && !allowWheels) continue;
                    projectileVoxelHealth.Remove(r.transform);
                    if (Application.isPlaying && removed < 24) SpawnDebris(r.transform, (r.bounds.center - centre).normalized, DebrisStyle.Weapon);
                    r.gameObject.SetActive(false); removed++;
                }
            }
            if (!touched && !directImpact) return;
            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            ReportVoxelDamage(removed); ReportVoxelDestroyed(removed, centre);
            if (CurrentHealth <= 0) DestroyFromWeaponHit(centre, direction);
        }
    }
}
