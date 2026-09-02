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

        public static VoxelHostileProjectile Create(Vector3 position, Vector3 firingDirection,
            VoxelRoadsideTurretTuning value, VoxelCarController player)
        {
            GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            projectileObject.name = "Turret Projectile";
            projectileObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(firingDirection));
            projectileObject.transform.localScale = new Vector3(0.13f, 0.13f, 0.38f);
            Object.Destroy(projectileObject.GetComponent<BoxCollider>());

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
            if (tuning == null)
            {
                Destroy(gameObject);
                return;
            }

            float distance = speed * Time.deltaTime;
            RaycastHit[] hits = Physics.RaycastAll(transform.position, direction, distance);
            RaycastHit closest = default;
            bool hasHit = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || (hasHit && hit.distance >= closest.distance))
                    continue;
                closest = hit;
                hasHit = true;
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
                int originalDamage = player.damageVoxelsPerHit;
                player.damageVoxelsPerHit = tuning.playerDamageVoxels;
                player.ApplyDamage(hit.point, direction);
                player.damageVoxelsPerHit = originalDamage;
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
    }
}
