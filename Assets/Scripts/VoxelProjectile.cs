using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Visible forward-moving gun projectile. Damage is retained for hit handling added later.</summary>
    public sealed class VoxelProjectile : MonoBehaviour
    {
        public float Damage { get; private set; }
        public float AreaOfEffectRadius { get; private set; }

        private Vector3 direction;
        private float speed;
        private float remainingRange;

        public static VoxelProjectile Create(Vector3 position, Vector3 direction, VoxelGunTuning tuning)
        {
            var projectileObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            projectileObject.name = "Gun Projectile";
            projectileObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            projectileObject.transform.localScale = new Vector3(0.09f, 0.09f, 0.26f);
            Object.Destroy(projectileObject.GetComponent<BoxCollider>());

            var projectile = projectileObject.AddComponent<VoxelProjectile>();
            projectile.Initialize(direction, tuning);
            return projectile;
        }

        private void Initialize(Vector3 firingDirection, VoxelGunTuning tuning)
        {
            direction = firingDirection.normalized;
            speed = tuning.projectileSpeed;
            remainingRange = tuning.maximumRange;
            Damage = tuning.damagePerBullet;
            AreaOfEffectRadius = tuning.areaOfEffectRadius;
        }

        private void Update()
        {
            float distance = Mathf.Min(speed * Time.deltaTime, remainingRange);
            RaycastHit[] hits = Physics.RaycastAll(transform.position, direction, distance);
            RaycastHit closestVehicleHit = default;
            bool hitVehicle = false;
            foreach (var hit in hits)
            {
                var enemy = hit.collider.GetComponentInParent<VoxelEnemyCar>();
                if (enemy != null)
                {
                    if (!hitVehicle || hit.distance < closestVehicleHit.distance)
                    {
                        closestVehicleHit = hit;
                        hitVehicle = true;
                    }
                    continue;
                }

                var civilian = hit.collider.GetComponentInParent<VoxelObstacleCar>();
                if (civilian != null && (!hitVehicle || hit.distance < closestVehicleHit.distance))
                {
                    closestVehicleHit = hit;
                    hitVehicle = true;
                }
            }

            if (hitVehicle)
            {
                var enemy = closestVehicleHit.collider.GetComponentInParent<VoxelEnemyCar>();
                if (enemy != null)
                {
                    Transform targetVoxel = closestVehicleHit.collider.transform;
                    if (enemy.TryGetNextProjectileVoxel(transform.position, direction, distance, out Transform guidedVoxel))
                        targetVoxel = guidedVoxel;
                    enemy.TakeProjectileHit(targetVoxel, Damage, targetVoxel.position, direction);
                }
                else
                    closestVehicleHit.collider.GetComponentInParent<VoxelObstacleCar>()
                        .TakeProjectileHit(closestVehicleHit.collider.transform, Damage, closestVehicleHit.point, direction);
                Object.Destroy(gameObject);
                return;
            }

            foreach (var enemy in FindObjectsByType<VoxelEnemyCar>(FindObjectsSortMode.None))
            {
                if (!enemy.TryGetNextProjectileVoxel(transform.position, direction, distance, out Transform fallbackVoxel))
                    continue;

                enemy.TakeProjectileHit(fallbackVoxel, Damage, fallbackVoxel.position, direction);
                Object.Destroy(gameObject);
                return;
            }

            transform.position += direction * distance;
            remainingRange -= distance;
            if (remainingRange <= 0f)
                Object.Destroy(gameObject);
        }
    }
}
