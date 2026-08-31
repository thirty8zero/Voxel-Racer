using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>A destructible traffic car that can drive with or against the player.</summary>
    public sealed class VoxelObstacleCar : MonoBehaviour
    {
        [Header("Persistent Tuning")]
        public VoxelObstacleCarTuning tuning;

        public VoxelEnemyVehicleTuning EnemyTuning { get; private set; }
        public float VoxelHealth => EnemyTuning != null ? EnemyTuning.voxelHealth : 1f;
        public float CurrentHealth { get; private set; }
        public float LaneOffset => laneOffset;
        public float TrackDistance => trackDistance;
        public bool TravelsWithPlayer => travelsWithPlayer;
        public float TravelSpeed => travelSpeed;

        private VoxelCarController target;
        private bool travelsWithPlayer;
        private bool isSemiTrailer;
        private float travelSpeed;
        private float spawnSpeed;
        private float approachSpeed;
        private float engageSpeed;
        private float collisionHalfWidth = 1.35f;
        private float collisionHalfLength = 2.3f;
        private bool hasBeenHit;
        private Vector3 velocity;
        private float destroyTime;
        private float nextCollisionTime;
        private EndlessVoxelRoad path;
        private float trackDistance;
        private float laneOffset;
        private readonly Dictionary<Transform, float> projectileVoxelHealth = new();

        public void Configure(VoxelCarController player, VoxelObstacleCarTuning value, bool sameDirection,
            EndlessVoxelRoad road, float distance, float offset, float matchingTravelSpeed = -1f)
        {
            target = player;
            tuning = value;
            travelsWithPlayer = sameDirection;
            path = road;
            trackDistance = distance;
            laneOffset = offset;
            float playerMaximumSpeed = Mathf.Max(0f, player.topSpeed);
            if (matchingTravelSpeed >= 0f)
            {
                // Preserve the existing same-lane traffic rule: shared-lane
                // civilians retain a matching profile and cannot catch each other.
                spawnSpeed = matchingTravelSpeed;
                approachSpeed = matchingTravelSpeed;
                engageSpeed = matchingTravelSpeed;
            }
            else
            {
                GetPhaseMultiplierRange(tuning, sameDirection, 0, out float spawnMin, out float spawnMax);
                GetPhaseMultiplierRange(tuning, sameDirection, 1, out float approachMin, out float approachMax);
                GetPhaseMultiplierRange(tuning, sameDirection, 2, out float engageMin, out float engageMax);
                spawnSpeed = playerMaximumSpeed * Random.Range(spawnMin, spawnMax);
                approachSpeed = playerMaximumSpeed * Random.Range(approachMin, approachMax);
                engageSpeed = playerMaximumSpeed * Random.Range(engageMin, engageMax);
            }
            travelSpeed = spawnSpeed;
            isSemiTrailer = Random.value < tuning.semiTrailerSpawnChance;
            EnemyTuning = isSemiTrailer ? tuning.semiTrailerEnemyTuning : tuning.trafficCarEnemyTuning;
            CurrentHealth = EnemyTuning != null ? EnemyTuning.vehicleHealth : 1f;
            if (isSemiTrailer)
            {
                collisionHalfWidth = 1.55f;
                collisionHalfLength = 4.8f;
                gameObject.name = sameDirection ? "Traffic Semi (Same Direction)" : "Traffic Semi (Oncoming)";
            }
            BuildVisuals();
            ApplyRandomPaintColour();
            ApplyTrackPose();
        }

        private void Update()
        {
            if (target == null || tuning == null)
            {
                Destroy(gameObject);
                return;
            }

            if (!hasBeenHit)
            {
                float direction = travelsWithPlayer ? 1f : -1f;
                travelSpeed = GetPhaseSpeed();
                trackDistance += direction * travelSpeed * Time.deltaTime;
                ApplyTrackPose();
                RotateWheels(direction * travelSpeed);

                bool overlapsLane = Mathf.Abs(target.CurrentLaneOffset - laneOffset) < collisionHalfWidth;
                bool overlapsDepth = Mathf.Abs(target.TrackDistance - trackDistance) < collisionHalfLength;
                if (overlapsLane && overlapsDepth && Time.time >= nextCollisionTime)
                    HitCar();
                else if (trackDistance < target.TrackDistance - 30f ||
                         trackDistance > target.TrackDistance + GetMaximumDistanceAhead())
                    Destroy(gameObject);
                return;
            }

            velocity += Physics.gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            if (velocity.sqrMagnitude > 0.001f)
                transform.Rotate(velocity.normalized * 300f * Time.deltaTime, Space.World);
            if (Time.time >= destroyTime)
                Destroy(gameObject);
        }

        private void HitCar()
        {
            nextCollisionTime = Time.time + tuning.collisionCooldown;
            hasBeenHit = true;
            Camera.main?.GetComponent<VoxelCameraFollow>()?.ShakeFromPlayerVehicleImpact();
            Vector3 hitDirection = (transform.position - target.transform.position).normalized;
            if (hitDirection.sqrMagnitude < 0.001f)
                hitDirection = travelsWithPlayer ? target.transform.forward : -target.transform.forward;

            int originalPlayerDamage = target.damageVoxelsPerHit;
            target.damageVoxelsPerHit = Random.Range(
                Mathf.Min(tuning.playerDamageVoxelsMin, tuning.playerDamageVoxelsMax),
                Mathf.Max(tuning.playerDamageVoxelsMin, tuning.playerDamageVoxelsMax) + 1);
            target.ApplyDamage(target.GetDamageSurfacePoint(transform.position), hitDirection);
            target.damageVoxelsPerHit = originalPlayerDamage;

            // Begin damage on the surface facing the player, rather than from the traffic
            // car's centre, so detached voxels consistently identify the collision point.
            float damageSurfaceOffset = isSemiTrailer
                ? tuning.semiImpactVoxelDamageSurfaceOffset
                : tuning.impactVoxelDamageSurfaceOffset;
            Vector3 obstacleImpactPoint = transform.position - hitDirection * damageSurfaceOffset;
            int obstacleDamageCount = Random.Range(
                Mathf.Min(tuning.obstacleDamageVoxelsMin, tuning.obstacleDamageVoxelsMax),
                Mathf.Max(tuning.obstacleDamageVoxelsMin, tuning.obstacleDamageVoxelsMax) + 1);
            int damagedVoxelCount = ApplyVoxelDamage(obstacleImpactPoint, -hitDirection, obstacleDamageCount);
            VoxelMissionProgress.ReportCivilianVoxelDamage(damagedVoxelCount);
            VoxelMissionProgress.ReportCivilianVehicleDestroyed();
            VoxelDestructionExplosion.Play(transform.position + Vector3.up * 0.8f,
                EnemyTuning != null ? EnemyTuning.explosionEffectScale : (isSemiTrailer ? 1.35f : 1f));
            velocity = hitDirection * tuning.launchForce + Vector3.up * tuning.launchUpwardForce;
            destroyTime = Time.time + tuning.destroyedLifetime;
        }

        /// <summary>Applies weapon damage without giving civilians an enemy health bar.</summary>
        public void TakeProjectileHit(Transform hitVoxel, float damage, Vector3 hitPoint, Vector3 impactDirection)
        {
            if (hasBeenHit || EnemyTuning == null || damage <= 0f)
                return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            if (hitVoxel != null)
            {
                VoxelMissionProgress.ReportCivilianVoxelDamage();
                projectileVoxelHealth.TryGetValue(hitVoxel, out float remainingVoxelHealth);
                remainingVoxelHealth = remainingVoxelHealth <= 0f ? EnemyTuning.voxelHealth : remainingVoxelHealth;
                remainingVoxelHealth -= damage;
                if (remainingVoxelHealth <= 0f)
                {
                    projectileVoxelHealth.Remove(hitVoxel);
                    SpawnDebris(hitVoxel, impactDirection);
                    hitVoxel.gameObject.SetActive(false);
                }
                else
                    projectileVoxelHealth[hitVoxel] = remainingVoxelHealth;
            }

            if (CurrentHealth <= 0f)
                DestroyFromWeaponHit(hitPoint, impactDirection);
        }

        private void DestroyFromWeaponHit(Vector3 hitPoint, Vector3 impactDirection)
        {
            hasBeenHit = true;
            VoxelMissionProgress.ReportCivilianVehicleDestroyed();
            VoxelDestructionExplosion.Play(transform.position + Vector3.up * 0.8f,
                EnemyTuning != null ? EnemyTuning.explosionEffectScale : (isSemiTrailer ? 1.35f : 1f));
            ApplyVoxelDamage(hitPoint, impactDirection, EnemyTuning.explosionVoxelCount);
            velocity = impactDirection.normalized * tuning.launchForce + Vector3.up * tuning.launchUpwardForce;
            destroyTime = Time.time + EnemyTuning.destroyedLifetime;
        }

        private void ApplyTrackPose()
        {
            if (path == null)
                return;
            VoxelTrackPose pose = path.Evaluate(trackDistance);
            transform.position = pose.position + pose.right * laneOffset;
            transform.rotation = travelsWithPlayer ? pose.rotation : pose.rotation * Quaternion.Euler(0f, 180f, 0f);
        }

        private float GetPhaseSpeed()
        {
            float distanceAhead = trackDistance - target.TrackDistance;
            if (distanceAhead <= tuning.engageSpeedDistance)
                return engageSpeed;
            if (distanceAhead <= tuning.approachSpeedDistance)
                return approachSpeed;
            return spawnSpeed;
        }

        private static void GetPhaseMultiplierRange(VoxelObstacleCarTuning value, bool sameDirection,
            int phase, out float minimum, out float maximum)
        {
            if (sameDirection)
            {
                minimum = phase == 0 ? value.sameDirectionSpawnSpeedMultiplierMin
                    : phase == 1 ? value.sameDirectionApproachSpeedMultiplierMin
                    : value.sameDirectionEngageSpeedMultiplierMin;
                maximum = phase == 0 ? value.sameDirectionSpawnSpeedMultiplierMax
                    : phase == 1 ? value.sameDirectionApproachSpeedMultiplierMax
                    : value.sameDirectionEngageSpeedMultiplierMax;
            }
            else
            {
                minimum = phase == 0 ? value.oncomingSpawnSpeedMultiplierMin
                    : phase == 1 ? value.oncomingApproachSpeedMultiplierMin
                    : value.oncomingEngageSpeedMultiplierMin;
                maximum = phase == 0 ? value.oncomingSpawnSpeedMultiplierMax
                    : phase == 1 ? value.oncomingApproachSpeedMultiplierMax
                    : value.oncomingEngageSpeedMultiplierMax;
            }

            minimum = Mathf.Max(0f, Mathf.Min(minimum, maximum));
            maximum = Mathf.Max(minimum, maximum);
        }

        // Vehicles may be spawned farther ahead than the original 110-unit prototype
        // distance. Keep them alive until the player can reasonably reach them.
        private float GetMaximumDistanceAhead() => Mathf.Max(110f,
            (tuning != null ? tuning.spawnDistanceAhead : 110f) + 30f);

        private int ApplyVoxelDamage(Vector3 hitPoint, Vector3 impactDirection, int voxelCount)
        {
            var candidates = new List<Transform>();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
                if (renderer.transform != transform)
                    candidates.Add(renderer.transform);

            candidates.Sort((a, b) => (a.position - hitPoint).sqrMagnitude.CompareTo((b.position - hitPoint).sqrMagnitude));
            int destroyCount = Mathf.Min(voxelCount, candidates.Count);
            for (int index = 0; index < destroyCount; index++)
            {
                Transform voxel = candidates[index];
                SpawnDebris(voxel, impactDirection);
                voxel.gameObject.SetActive(false);
            }
            return destroyCount;
        }

        private void SpawnDebris(Transform source, Vector3 impactDirection)
        {
            var debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.name = "Obstacle Car Damage Voxel";
            Vector3 burstDirection = (impactDirection.normalized + Vector3.up * tuning.explosionUpwardBias).normalized;
            debris.transform.position = source.position + burstDirection * tuning.explosionSpawnOffset + Random.insideUnitSphere * 0.16f;
            debris.transform.rotation = Random.rotation;
            debris.transform.localScale = Vector3.one * Random.Range(0.16f, 0.30f);
            debris.GetComponent<MeshRenderer>().sharedMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
            Destroy(debris.GetComponent<BoxCollider>());
            Vector3 burst = burstDirection * Random.Range(tuning.explosionForwardForceMin, tuning.explosionForwardForceMax)
                + Random.insideUnitSphere * tuning.explosionSpreadForce + Vector3.up * tuning.explosionUpwardForce;
            debris.AddComponent<VoxelDebris>().Launch(burst);
        }

        private void BuildVisuals()
        {
            if (isSemiTrailer)
                VoxelRacerBootstrap.CreateObstacleSemiTrailerVisuals(transform);
            else
                VoxelRacerBootstrap.CreateObstacleCarVisuals(transform);
        }

        private void ApplyRandomPaintColour()
        {
            if (tuning.paintColours == null || tuning.paintColours.Length == 0)
                return;

            Color paintColour = tuning.paintColours[Random.Range(0, tuning.paintColours.Length)];
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.sharedMaterial != VoxelRacerBootstrap.ObstacleCarPaintMaterial)
                    continue;

                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", paintColour);
                renderer.SetPropertyBlock(properties);
            }
        }

        private void RotateWheels(float speed)
        {
            foreach (Transform child in transform)
                if (child.name == "Obstacle Voxel Wheel")
                    child.Rotate(Vector3.right, speed * tuning.wheelSpinDegreesPerUnit * Time.deltaTime, Space.Self);
        }
    }
}
