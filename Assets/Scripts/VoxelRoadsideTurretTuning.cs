using UnityEngine;
using UnityEngine.Serialization;

namespace VoxelRacer
{
    /// <summary>Reusable firing and placement settings for a static roadside gun turret.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Enemies/Roadside Turret Tuning", fileName = "RoadsideTurretTuning")]
    public sealed class VoxelRoadsideTurretTuning : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Roadside Gun Turret";

        [Header("Placement & Aim")]
        [Tooltip("How far beyond the edge of the road the turret base is placed.")]
        [Min(0f)] public float distanceFromRoadEdge = 2f;
        [Tooltip("The firing angle, in degrees, sampled for each turret. Zero shoots straight across the road.")]
        [Range(-45f, 45f)] public float minimumAimAngle = 0f;
        [Range(-45f, 45f)] public float maximumAimAngle = 0f;
        [Tooltip("Longitudinal clear space required from static obstacles when spawning.")]
        [Min(0f)] public float staticObstacleClearance = 12f;

        [Header("Firing")]
        [Tooltip("Seconds between successive volleys while the turret is firing.")]
        [Min(0.02f)] public float fireRate = 0.25f;
        [Tooltip("Projectiles released together by each volley.")]
        [Min(1)] public int bulletsPerVolley = 1;
        [Tooltip("How long the turret continues firing before it pauses.")]
        [Min(0.02f)] public float firingDuration = 1.5f;
        [Tooltip("How long the turret waits after firing before beginning another burst.")]
        [Min(0.02f)] public float pauseDuration = 2f;

        [Header("Projectile")]
        [Min(0.1f)] public float projectileSpeed = 30f;
        [Min(0.1f)] public float projectileLifetime = 2f;
        [Tooltip("Player voxels removed by one hostile projectile hit.")]
        [Min(1)] public int playerDamageVoxels = 10;
        [FormerlySerializedAs("vehicleDamage")]
        [Tooltip("Health damage dealt to enemy vehicles by one hostile projectile. Civilian traffic uses the same health model; this never affects player score.")]
        [Min(0.1f)] public float enemyHealthDamage = 1f;
        [Tooltip("Small horizontal fan applied to multiple projectiles in one volley.")]
        [Range(0f, 20f)] public float volleySpreadDegrees = 5f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            maximumAimAngle = Mathf.Max(minimumAimAngle, maximumAimAngle);
            fireRate = Mathf.Max(0.02f, fireRate);
            bulletsPerVolley = Mathf.Max(1, bulletsPerVolley);
            firingDuration = Mathf.Max(0.02f, firingDuration);
            pauseDuration = Mathf.Max(0.02f, pauseDuration);
            projectileSpeed = Mathf.Max(0.1f, projectileSpeed);
            projectileLifetime = Mathf.Max(0.1f, projectileLifetime);
            playerDamageVoxels = Mathf.Max(1, playerDamageVoxels);
            enemyHealthDamage = Mathf.Max(0.1f, enemyHealthDamage);
            VoxelAssetSaveQueue.Request(this);
        }
#endif
    }
}
