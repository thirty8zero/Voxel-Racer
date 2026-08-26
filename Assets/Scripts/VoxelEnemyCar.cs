using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>A same-direction traffic-car variant that can be damaged by player projectiles.</summary>
    public sealed class VoxelEnemyCar : MonoBehaviour
    {
        public VoxelEnemyVehicleTuning Tuning { get; private set; }
        public float CurrentHealth { get; private set; }
        public float HealthPercent => Tuning == null ? 0f : Mathf.Clamp01(CurrentHealth / Tuning.vehicleHealth);
        public float LaneOffset => laneOffset;

        private readonly Dictionary<Transform, float> voxelHealth = new();
        private VoxelCarController target;
        private VoxelObstacleCarTuning trafficTuning;
        private EndlessVoxelRoad path;
        private float trackDistance;
        private float laneOffset;
        private float currentSpeed;
        private bool hasBeenRammed;
        private float destroyTime;
        private float nextCollisionTime;
        private Vector3 velocity;
        private VoxelEnemyHealthBar healthBar;

        public void Configure(VoxelCarController player, VoxelObstacleCarTuning traffic, VoxelEnemyVehicleTuning enemy,
            EndlessVoxelRoad road, float distance, float offset)
        {
            target = player;
            trafficTuning = traffic;
            Tuning = enemy;
            path = road;
            trackDistance = distance;
            laneOffset = offset;
            CurrentHealth = enemy.vehicleHealth;
            VoxelRacerBootstrap.CreateObstacleCarVisuals(transform);
            ApplyBlackPaint();
            healthBar = VoxelEnemyHealthBar.Create(transform, enemy);
            ApplyTrackPose();
        }

        private void Update()
        {
            if (target == null || Tuning == null)
            {
                Destroy(gameObject);
                return;
            }

            if (hasBeenRammed)
            {
                velocity += Physics.gravity * Time.deltaTime;
                transform.position += velocity * Time.deltaTime;
                if (velocity.sqrMagnitude > 0.001f)
                    transform.Rotate(velocity.normalized * 300f * Time.deltaTime, Space.World);
                if (Time.time >= destroyTime)
                    Destroy(gameObject);
                return;
            }

            float desiredSpeed = target.CurrentSpeed * Tuning.playerSpeedMultiplier;
            currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, Tuning.speedMatchRate * Time.deltaTime);
            trackDistance += currentSpeed * Time.deltaTime;
            ApplyTrackPose();
            RotateWheels();

            bool overlapsLane = Mathf.Abs(target.CurrentLaneOffset - laneOffset) < Tuning.collisionHalfWidth;
            bool overlapsDepth = Mathf.Abs(target.TrackDistance - trackDistance) < Tuning.collisionHalfLength;
            if (overlapsLane && overlapsDepth && Time.time >= nextCollisionTime)
                RamByPlayer();

            if (trackDistance < target.TrackDistance - 30f || trackDistance > target.TrackDistance + 110f)
                Destroy(gameObject);
        }

        public void TakeProjectileHit(Transform hitVoxel, float damage, Vector3 hitPoint, Vector3 impactDirection)
        {
            if (hasBeenRammed || damage <= 0f)
                return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            if (hitVoxel != null)
            {
                voxelHealth.TryGetValue(hitVoxel, out float remainingVoxelHealth);
                remainingVoxelHealth = remainingVoxelHealth <= 0f ? Tuning.voxelHealth : remainingVoxelHealth;
                remainingVoxelHealth -= damage;
                if (remainingVoxelHealth <= 0f)
                {
                    voxelHealth.Remove(hitVoxel);
                    SpawnDebris(hitVoxel, impactDirection);
                    hitVoxel.gameObject.SetActive(false);
                }
                else
                    voxelHealth[hitVoxel] = remainingVoxelHealth;
            }

            healthBar.SetHealth(HealthPercent);
            if (CurrentHealth <= 0f)
                Explode(hitPoint, impactDirection);
        }

        /// <summary>
        /// Finds an intact voxel on the rearmost surface reached by a bullet path.
        /// Sustained forward fire therefore peels each surface vertically before it
        /// advances into the vehicle, rather than drilling a narrow horizontal tunnel.
        /// </summary>
        public bool TryGetNextProjectileVoxel(Vector3 segmentStart, Vector3 direction, float segmentLength,
            out Transform hitVoxel)
        {
            hitVoxel = null;
            if (hasBeenRammed || segmentLength <= 0f)
                return false;

            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            float rearSurfaceDistance = float.PositiveInfinity;
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                Transform voxel = renderer.transform;
                if (voxel == transform || voxel.GetComponentInParent<VoxelEnemyHealthBar>() != null)
                    continue;

                Vector3 offset = voxel.position - segmentStart;
                float forwardDistance = Vector3.Dot(offset, direction);
                if (forwardDistance < 0f || forwardDistance > segmentLength)
                    continue;

                float lateralDistance = Mathf.Abs(Vector3.Dot(offset, right));
                if (lateralDistance > 1.45f)
                    continue;

                rearSurfaceDistance = Mathf.Min(rearSurfaceDistance, forwardDistance);
            }

            if (float.IsPositiveInfinity(rearSurfaceDistance))
                return false;

            const float RearSurfaceDepth = 0.4f;
            float bestScore = float.PositiveInfinity;
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                Transform voxel = renderer.transform;
                if (voxel == transform || voxel.GetComponentInParent<VoxelEnemyHealthBar>() != null)
                    continue;

                Vector3 offset = voxel.position - segmentStart;
                float forwardDistance = Vector3.Dot(offset, direction);
                if (forwardDistance < rearSurfaceDistance || forwardDistance > rearSurfaceDistance + RearSurfaceDepth)
                    continue;

                float lateralDistance = Mathf.Abs(Vector3.Dot(offset, right));
                if (lateralDistance > 1.45f)
                    continue;

                Vector3 pointOnPath = segmentStart + direction * forwardDistance;
                float verticalDistance = Mathf.Abs(voxel.position.y - pointOnPath.y);
                float score = verticalDistance * 2f + lateralDistance + Random.value * Tuning.rearSurfaceHitRandomness;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                hitVoxel = voxel;
            }

            return hitVoxel != null;
        }

        private void Explode(Vector3 hitPoint, Vector3 impactDirection)
        {
            healthBar.gameObject.SetActive(false);
            var voxels = new List<Transform>();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
                if (renderer.transform != transform && renderer.transform != healthBar.transform)
                    voxels.Add(renderer.transform);
            voxels.Sort((first, second) => (first.position - hitPoint).sqrMagnitude.CompareTo((second.position - hitPoint).sqrMagnitude));
            int maximumDetachedVoxels = Mathf.FloorToInt(voxels.Count * Tuning.maximumExplosionVoxelRemovalPercent);
            int debrisCount = Mathf.Min(Tuning.explosionVoxelCount, maximumDetachedVoxels);
            for (int index = 0; index < debrisCount; index++)
            {
                SpawnDebris(voxels[index], impactDirection);
                voxels[index].gameObject.SetActive(false);
            }

            hasBeenRammed = true;
            velocity = impactDirection.normalized * Random.Range(Tuning.explosionForwardForceMin, Tuning.explosionForwardForceMax)
                + Vector3.up * Tuning.explosionUpwardForce;
            destroyTime = Time.time + Tuning.destroyedLifetime;
        }

        private void RamByPlayer()
        {
            nextCollisionTime = Time.time + trafficTuning.collisionCooldown;
            hasBeenRammed = true;
            healthBar.gameObject.SetActive(false);

            Vector3 hitDirection = (transform.position - target.transform.position).normalized;
            if (hitDirection.sqrMagnitude < 0.001f)
                hitDirection = target.transform.forward;

            int originalPlayerDamage = target.damageVoxelsPerHit;
            target.damageVoxelsPerHit = Random.Range(
                Mathf.Min(trafficTuning.playerDamageVoxelsMin, trafficTuning.playerDamageVoxelsMax),
                Mathf.Max(trafficTuning.playerDamageVoxelsMin, trafficTuning.playerDamageVoxelsMax) + 1);
            target.ApplyDamage(target.GetDamageSurfacePoint(transform.position), hitDirection);
            target.damageVoxelsPerHit = originalPlayerDamage;

            ApplyVoxelDamage(transform.position - hitDirection * trafficTuning.impactVoxelDamageSurfaceOffset,
                -hitDirection, Random.Range(
                    Mathf.Min(trafficTuning.obstacleDamageVoxelsMin, trafficTuning.obstacleDamageVoxelsMax),
                    Mathf.Max(trafficTuning.obstacleDamageVoxelsMin, trafficTuning.obstacleDamageVoxelsMax) + 1));
            velocity = hitDirection * trafficTuning.launchForce + Vector3.up * trafficTuning.launchUpwardForce;
            destroyTime = Time.time + trafficTuning.destroyedLifetime;
        }

        private void ApplyVoxelDamage(Vector3 hitPoint, Vector3 impactDirection, int voxelCount)
        {
            var candidates = new List<Transform>();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
                if (renderer.transform != transform && renderer.transform != healthBar.transform)
                    candidates.Add(renderer.transform);

            candidates.Sort((first, second) => (first.position - hitPoint).sqrMagnitude.CompareTo((second.position - hitPoint).sqrMagnitude));
            int destroyCount = Mathf.Min(voxelCount, candidates.Count);
            for (int index = 0; index < destroyCount; index++)
            {
                SpawnDebris(candidates[index], impactDirection);
                candidates[index].gameObject.SetActive(false);
            }
        }

        private void ApplyTrackPose()
        {
            if (path == null)
                return;
            VoxelTrackPose pose = path.Evaluate(trackDistance);
            transform.position = pose.position + pose.right * laneOffset;
            transform.rotation = pose.rotation;
        }

        private void RotateWheels()
        {
            foreach (Transform child in transform)
                if (child.name == "Obstacle Voxel Wheel")
                    child.Rotate(Vector3.right, currentSpeed * trafficTuning.wheelSpinDegreesPerUnit * Time.deltaTime, Space.Self);
        }

        private void ApplyBlackPaint()
        {
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                bool isPaint = renderer.sharedMaterial == VoxelRacerBootstrap.ObstacleCarPaintMaterial;
                bool isTrim = renderer.sharedMaterial == VoxelRacerBootstrap.ObstacleCarTrimMaterial;
                if (!isPaint && !isTrim)
                    continue;

                Color colour = isPaint
                    ? new Color(0.025f, 0.025f, 0.03f)
                    : new Color(0.34f, 0.37f, 0.42f);
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", colour);
                properties.SetColor("_Color", colour);
                renderer.SetPropertyBlock(properties);
            }
        }

        private void SpawnDebris(Transform source, Vector3 impactDirection)
        {
            var debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.name = "Enemy Car Damage Voxel";
            debris.transform.position = source.position + impactDirection.normalized * 0.25f + Random.insideUnitSphere * 0.16f;
            debris.transform.rotation = Random.rotation;
            debris.transform.localScale = source.lossyScale * Tuning.explosionDebrisScale;
            var sourceRenderer = source.GetComponent<MeshRenderer>();
            var debrisRenderer = debris.GetComponent<MeshRenderer>();
            debrisRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            var colourOverrides = new MaterialPropertyBlock();
            sourceRenderer.GetPropertyBlock(colourOverrides);
            debrisRenderer.SetPropertyBlock(colourOverrides);
            Destroy(debris.GetComponent<BoxCollider>());
            Vector3 burst = impactDirection.normalized * Random.Range(Tuning.explosionForwardForceMin, Tuning.explosionForwardForceMax)
                + Random.insideUnitSphere * Tuning.explosionSpreadForce
                + Vector3.up * Tuning.explosionUpwardForce;
            debris.AddComponent<VoxelDebris>().Launch(burst, Tuning.explosionDebrisLifetime);
        }
    }
}
