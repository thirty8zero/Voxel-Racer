using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>A destructible traffic car that can drive with or against the player.</summary>
    public sealed class VoxelObstacleCar : MonoBehaviour
    {
        [Header("Persistent Tuning")]
        public VoxelObstacleCarTuning tuning;

        private VoxelCarController target;
        private bool travelsWithPlayer;
        private bool isSemiTrailer;
        private float travelSpeed;
        private float collisionHalfWidth = 1.35f;
        private float collisionHalfLength = 2.3f;
        private bool hasBeenHit;
        private Vector3 velocity;
        private float destroyTime;
        private float nextCollisionTime;
        private EndlessVoxelRoad path;
        private float trackDistance;
        private float laneOffset;

        public void Configure(VoxelCarController player, VoxelObstacleCarTuning value, bool sameDirection,
            EndlessVoxelRoad road, float distance, float offset)
        {
            target = player;
            tuning = value;
            travelsWithPlayer = sameDirection;
            path = road;
            trackDistance = distance;
            laneOffset = offset;
            float min = sameDirection ? tuning.sameDirectionSpeedMin : tuning.oppositeDirectionSpeedMin;
            float max = sameDirection ? tuning.sameDirectionSpeedMax : tuning.oppositeDirectionSpeedMax;
            travelSpeed = Random.Range(Mathf.Min(min, max), Mathf.Max(min, max));
            isSemiTrailer = Random.value < tuning.semiTrailerSpawnChance;
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
                trackDistance += direction * travelSpeed * Time.deltaTime;
                ApplyTrackPose();
                RotateWheels(direction * travelSpeed);

                bool overlapsLane = Mathf.Abs(target.CurrentLaneOffset - laneOffset) < collisionHalfWidth;
                bool overlapsDepth = Mathf.Abs(target.TrackDistance - trackDistance) < collisionHalfLength;
                if (overlapsLane && overlapsDepth && Time.time >= nextCollisionTime)
                    HitCar();
                else if (trackDistance < target.TrackDistance - 30f ||
                         trackDistance > target.TrackDistance + 110f)
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
            ApplyVoxelDamage(obstacleImpactPoint, -hitDirection, obstacleDamageCount);
            velocity = hitDirection * tuning.launchForce + Vector3.up * tuning.launchUpwardForce;
            destroyTime = Time.time + tuning.destroyedLifetime;
        }

        private void ApplyTrackPose()
        {
            if (path == null)
                return;
            VoxelTrackPose pose = path.Evaluate(trackDistance);
            transform.position = pose.position + pose.right * laneOffset;
            transform.rotation = travelsWithPlayer ? pose.rotation : pose.rotation * Quaternion.Euler(0f, 180f, 0f);
        }

        private void ApplyVoxelDamage(Vector3 hitPoint, Vector3 impactDirection, int voxelCount)
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
