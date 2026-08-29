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
        private static ParticleSystem voxelSparkPrefab;
        private static ParticleSystem voxelMissCloudPrefab;
        private static ParticleSystem voxelGroundDustPrefab;
        private static EndlessVoxelRoad cachedRoad;

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
            RaycastHit closestObstacleHit = default;
            RaycastHit closestFuelDrumHit = default;
            RaycastHit closestHit = default;
            bool hitVehicle = false;
            bool hitDestructibleObstacle = false;
            bool hitFuelDrums = false;
            bool hitSomething = false;
            foreach (var hit in hits)
            {
                if (!hitSomething || hit.distance < closestHit.distance)
                {
                    closestHit = hit;
                    hitSomething = true;
                }

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

                var obstacle = hit.collider.GetComponentInParent<VoxelObstacle>();
                if (obstacle != null && (!hitDestructibleObstacle || hit.distance < closestObstacleHit.distance))
                {
                    closestObstacleHit = hit;
                    hitDestructibleObstacle = true;
                }

                var fuelDrums = hit.collider.GetComponentInParent<VoxelFuelDrumObstacle>();
                if (fuelDrums != null && (!hitFuelDrums || hit.distance < closestFuelDrumHit.distance))
                {
                    closestFuelDrumHit = hit;
                    hitFuelDrums = true;
                }
            }

            // A wall, road object, or any other collider closer than a vehicle absorbs
            // the shot as well, so every visible impact produces the same feedback.
            if (hitSomething && (!hitVehicle || closestHit.distance < closestVehicleHit.distance) &&
                (!hitDestructibleObstacle || closestHit.distance < closestObstacleHit.distance) &&
                (!hitFuelDrums || closestHit.distance < closestFuelDrumHit.distance))
            {
                CreateImpactSparks(closestHit.point, direction);
                Object.Destroy(gameObject);
                return;
            }

            if (hitFuelDrums && (!hitVehicle || closestFuelDrumHit.distance < closestVehicleHit.distance) &&
                (!hitDestructibleObstacle || closestFuelDrumHit.distance < closestObstacleHit.distance))
            {
                VoxelFuelDrumObstacle drums = closestFuelDrumHit.collider.GetComponentInParent<VoxelFuelDrumObstacle>();
                Transform targetVoxel = closestFuelDrumHit.collider.transform;
                if (drums.TryGetNextProjectileVoxel(transform.position, direction, distance, out Transform guidedVoxel))
                    targetVoxel = guidedVoxel;
                drums.TakeProjectileHit(targetVoxel, closestFuelDrumHit.point, direction);
                CreateImpactSparks(closestFuelDrumHit.point, direction);
                Object.Destroy(gameObject);
                return;
            }

            if (hitDestructibleObstacle && (!hitVehicle || closestObstacleHit.distance < closestVehicleHit.distance))
            {
                VoxelObstacle obstacle = closestObstacleHit.collider.GetComponentInParent<VoxelObstacle>();
                Transform targetVoxel = closestObstacleHit.collider.transform;
                if (obstacle.TryGetNextProjectileVoxel(transform.position, direction, distance, out Transform guidedVoxel))
                    targetVoxel = guidedVoxel;
                obstacle.TakeProjectileHit(targetVoxel, closestObstacleHit.point, direction);
                CreateImpactSparks(closestObstacleHit.point, direction);
                Object.Destroy(gameObject);
                return;
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
                CreateImpactSparks(closestVehicleHit.point, direction);
                Object.Destroy(gameObject);
                return;
            }

            foreach (var enemy in FindObjectsByType<VoxelEnemyCar>(FindObjectsSortMode.None))
            {
                if (!enemy.TryGetNextProjectileVoxel(transform.position, direction, distance, out Transform fallbackVoxel))
                    continue;

                enemy.TakeProjectileHit(fallbackVoxel, Damage, fallbackVoxel.position, direction);
                CreateImpactSparks(fallbackVoxel.position, direction);
                Object.Destroy(gameObject);
                return;
            }

            foreach (VoxelObstacle obstacle in FindObjectsByType<VoxelObstacle>(FindObjectsSortMode.None))
            {
                if (!obstacle.TryGetNextProjectileVoxel(transform.position, direction, distance, out Transform fallbackVoxel))
                    continue;

                obstacle.TakeProjectileHit(fallbackVoxel, fallbackVoxel.position, direction);
                CreateImpactSparks(fallbackVoxel.position, direction);
                Object.Destroy(gameObject);
                return;
            }

            foreach (VoxelFuelDrumObstacle drums in FindObjectsByType<VoxelFuelDrumObstacle>(FindObjectsSortMode.None))
            {
                if (!drums.TryGetNextProjectileVoxel(transform.position, direction, distance, out Transform fallbackVoxel))
                    continue;

                drums.TakeProjectileHit(fallbackVoxel, fallbackVoxel.position, direction);
                CreateImpactSparks(fallbackVoxel.position, direction);
                Object.Destroy(gameObject);
                return;
            }

            transform.position += direction * distance;
            remainingRange -= distance;
            if (remainingRange <= 0f)
            {
                CreateMissCloud(transform.position, IsOverRoad(transform.position));
                Object.Destroy(gameObject);
            }
        }

        /// <summary>Creates a brief, low-count burst of glowing cube sparks at a shot impact.</summary>
        private static void CreateImpactSparks(Vector3 position, Vector3 impactDirection)
        {
            if (voxelSparkPrefab == null)
                voxelSparkPrefab = Resources.Load<ParticleSystem>("Effects/YellowVoxelImpactSparks");
            if (voxelSparkPrefab == null)
                return;

            Vector3 direction = impactDirection.sqrMagnitude > 0.0001f
                ? impactDirection.normalized
                : Vector3.forward;
            ParticleSystem burst = Object.Instantiate(voxelSparkPrefab,
                position + direction * 0.03f, Quaternion.LookRotation(-direction));
            burst.name = "Yellow Voxel Impact Sparks";
            burst.Play(true);
            Object.Destroy(burst.gameObject, 0.8f);
        }

        /// <summary>Creates a small voxel puff when a projectile reaches its maximum range.</summary>
        private static void CreateMissCloud(Vector3 position, bool overRoad)
        {
            ParticleSystem prefab;
            if (overRoad)
            {
                if (voxelMissCloudPrefab == null)
                    voxelMissCloudPrefab = Resources.Load<ParticleSystem>("Effects/WhiteVoxelMissCloud");
                prefab = voxelMissCloudPrefab;
            }
            else
            {
                if (voxelGroundDustPrefab == null)
                    voxelGroundDustPrefab = Resources.Load<ParticleSystem>("Effects/BrownVoxelGroundDust");
                prefab = voxelGroundDustPrefab;
            }

            if (prefab == null)
                return;

            ParticleSystem puff = Object.Instantiate(prefab, position, Quaternion.identity);
            puff.name = overRoad ? "White Voxel Miss Cloud" : "Brown Voxel Ground Dust";
            puff.Play(true);
            Object.Destroy(puff.gameObject, 1f);
        }

        private static bool IsOverRoad(Vector3 worldPosition)
        {
            if (cachedRoad == null)
                cachedRoad = Object.FindFirstObjectByType<EndlessVoxelRoad>();

            EndlessVoxelRoad road = cachedRoad;
            if (road == null)
                return true;

            float closestDistance = road.FindClosestDistance(worldPosition);
            VoxelTrackPose closestPose = road.Evaluate(closestDistance);
            float lateralOffset = Vector3.Dot(worldPosition - closestPose.position, closestPose.right);
            return Mathf.Abs(lateralOffset) <= road.roadWidth * 0.5f;
        }
    }
}
