using UnityEngine;
namespace VoxelRacer
{
    public static class VoxelSphericalBlast
    {
        public static bool Contains(MeshRenderer voxel, Vector3 centre, float radius) => voxel != null && voxel.enabled &&
            voxel.gameObject.activeInHierarchy && voxel.GetComponentInParent<VoxelIndestructiblePart>() == null &&
            voxel.GetComponentInParent<VoxelEnemyHealthBar>() == null && (voxel.bounds.center - centre).sqrMagnitude <= radius * radius;
    }
    public sealed partial class VoxelEnemyCar
    {
        public void TakeMissileBlast(Vector3 centre, float radius, float damage, Vector3 direction, MeshRenderer[] pieces)
        {
            if (hasBeenRammed || Tuning == null || damage <= 0) return;
            int removed = 0; bool touched = false;
            foreach (var r in pieces)
            {
                if (!VoxelSphericalBlast.Contains(r, centre, radius)) continue;
                touched = true;
                if (IsSpikeAttackActivePart(r.transform)) continue;
                voxelHealth.Remove(r.transform);
                if (Application.isPlaying && removed < 24) SpawnDebris(r.transform, (r.bounds.center - centre).normalized, DebrisStyle.Weapon);
                r.gameObject.SetActive(false); removed++;
            }
            if (!touched) return;
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
        public void TakeMissileBlast(Vector3 centre, float radius, float damage, Vector3 direction, MeshRenderer[] pieces)
        {
            if (hasBeenHit || EnemyTuning == null || damage <= 0) return;
            int removed = 0;
            foreach (var r in pieces)
            {
                if (!VoxelSphericalBlast.Contains(r, centre, radius)) continue;
                projectileVoxelHealth.Remove(r.transform);
                if (Application.isPlaying && removed < 24) SpawnDebris(r.transform, (r.bounds.center - centre).normalized, DebrisStyle.Weapon);
                r.gameObject.SetActive(false); removed++;
            }
            if (removed == 0) return;
            CurrentHealth = Mathf.Max(0, CurrentHealth - damage);
            ReportVoxelDamage(removed); ReportVoxelDestroyed(removed, centre);
            if (CurrentHealth <= 0) DestroyFromWeaponHit(centre, direction);
        }
    }
}
