using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Lightweight hostile projectile. It uses raycasts rather than rigidbodies and never reports mission score.</summary>
    public sealed class VoxelHostileProjectile : MonoBehaviour
    {
        private Vector3 direction;
        private float speed;
        private float remainingLifetime;
        private VoxelRoadsideTurretTuning tuning;
        private VoxelCarController target;
        private static RaycastHit[] hitBuffer = new RaycastHit[64];

        public static VoxelHostileProjectile Create(Vector3 position, Vector3 firingDirection,
            VoxelRoadsideTurretTuning value, VoxelCarController player)
        {
            GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            projectileObject.name = "Turret Projectile";
            projectileObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(firingDirection));
            projectileObject.transform.localScale = new Vector3(0.13f, 0.13f, 0.38f);
            var collider = projectileObject.GetComponent<BoxCollider>();
            collider.enabled = false;
            Object.Destroy(collider);

            Material material = Resources.Load<Material>("CarMaterials/FormulaWhite");
            if (material == null)
                material = VoxelRacerBootstrap.ObstacleCarPaintMaterial;
            if (material != null)
                projectileObject.GetComponent<MeshRenderer>().sharedMaterial = material;
            var projectile = projectileObject.AddComponent<VoxelHostileProjectile>();
            projectile.direction = firingDirection.normalized;
            projectile.speed = value.projectileSpeed;
            projectile.remainingLifetime = value.projectileLifetime;
            projectile.tuning = value;
            projectile.target = player;
            return projectile;
        }

        private void Update()
        {
            if (VoxelPauseMenu.IsPaused) return;
            // A completed mission shuts down turret hazards immediately, including
            // projectiles that were fired just before the completion frame.
            if (tuning == null || VoxelMissionProgress.Active?.IsComplete == true)
            {
                Destroy(gameObject);
                return;
            }

            float distance = speed * Time.deltaTime;
            int hitCount;
            while ((hitCount = Physics.RaycastNonAlloc(transform.position, direction, hitBuffer, distance)) == hitBuffer.Length)
                hitBuffer = new RaycastHit[hitBuffer.Length * 2];
            RaycastHit closest = default;
            bool hasHit = false;
            for (int i = 0; i < hitCount; i++)
            {
                var hit = hitBuffer[i];
                if (hit.collider == null || (hasHit && hit.distance >= closest.distance))
                    continue;
                closest = hit;
                hasHit = true;
            }

            // The primary player prefab has no colliders. Compare its exact mesh
            // hit with physics obstructions so bullets cannot pass through walls.
            if (target != null && target.TryFindHostileProjectileHit(transform.position, direction, distance,
                out Vector3 playerPoint, out float playerDistance) && (!hasHit || playerDistance < closest.distance))
            {
                DamagePlayer(target, playerPoint);
                Destroy(gameObject);
                return;
            }

            if (hasHit)
            {
                HandleHit(closest);
                Destroy(gameObject);
                return;
            }

            transform.position += direction * distance;
            remainingLifetime -= Time.deltaTime;
            if (remainingLifetime <= 0f)
                Destroy(gameObject);
        }

        private void HandleHit(RaycastHit hit)
        {
            VoxelCarController player = hit.collider.GetComponentInParent<VoxelCarController>();
            if (player != null && player == target)
            {
                DamagePlayer(player, hit.point);
                return;
            }

            VoxelEnemyCar enemy = hit.collider.GetComponentInParent<VoxelEnemyCar>();
            if (enemy != null)
            {
                enemy.TakeHostileProjectileHit(hit.collider.transform, tuning.enemyHealthDamage, hit.point, direction);
                return;
            }

            VoxelObstacleCar civilian = hit.collider.GetComponentInParent<VoxelObstacleCar>();
            if (civilian != null)
                civilian.TakeHostileProjectileHit(hit.collider.transform, tuning.enemyHealthDamage, hit.point, direction);
        }

        private void DamagePlayer(VoxelCarController player, Vector3 point)
        {
            int originalDamage = player.damageVoxelsPerHit;
            try
            {
                player.damageVoxelsPerHit = tuning.playerDamageVoxels;
                player.ApplyDamage(point, direction, "Roadside turret fire");
            }
            finally { player.damageVoxelsPerHit = originalDamage; }
        }
    }
}
