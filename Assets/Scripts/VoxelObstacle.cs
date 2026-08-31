using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>A temporary brown voxel crate that launches away after contact.</summary>
    public sealed class VoxelObstacle : MonoBehaviour
    {
        public float LaneOffset => laneOffset;
        public float TrackDistance => trackDistance;

        private VoxelCarController target;
        private VoxelStaticObstacleDefinition definition;
        private bool hasBeenHit;
        private Vector3 velocity;
        private float destroyTime;
        private EndlessVoxelRoad path;
        private float trackDistance;
        private float laneOffset;
        private int currentHealth;
        private const float ProjectileDebrisForce = 6f;

        public void Configure(VoxelCarController player, EndlessVoxelRoad road, VoxelStaticObstacleDefinition value,
            float distance, float offset)
        {
            target = player;
            definition = value;
            path = road;
            trackDistance = distance;
            laneOffset = offset;
            currentHealth = Mathf.Max(1, definition != null ? definition.hitPoints : 5);
            ApplyTrackPose();
            BuildVoxelBox();
        }

        private void Update()
        {
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }

            if (!hasBeenHit)
            {
                bool overlapsLane = Mathf.Abs(target.CurrentLaneOffset - laneOffset) < 1.35f;
                bool overlapsDepth = Mathf.Abs(target.TrackDistance - trackDistance) < 2.2f;
                if (overlapsLane && overlapsDepth)
                    HitCar();
                else if (trackDistance < target.TrackDistance - 25f)
                    Destroy(gameObject);
                return;
            }

            velocity += Physics.gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            transform.Rotate(velocity.normalized * 300f * Time.deltaTime, Space.World);
            if (Time.time >= destroyTime)
                Destroy(gameObject);
        }

        private void ApplyTrackPose()
        {
            if (path == null)
                return;
            VoxelTrackPose pose = path.Evaluate(trackDistance);
            transform.position = pose.position + pose.right * laneOffset;
            transform.rotation = pose.rotation;
        }

        private void HitCar()
        {
            hasBeenHit = true;
            Vector3 hitDirection = (transform.position - target.transform.position).normalized;
            int originalDamage = target.damageVoxelsPerHit;
            if (definition != null)
                target.damageVoxelsPerHit = Random.Range(definition.playerDamageVoxelsMin, definition.playerDamageVoxelsMax + 1);
            target.ApplyDamage(target.GetDamageSurfacePoint(transform.position), hitDirection);
            target.damageVoxelsPerHit = originalDamage;
            velocity = hitDirection * 15f + Vector3.up * 6f;
            destroyTime = Time.time + 2.5f;
        }

        /// <summary>Bullets remove individual crate voxels until the obstacle breaks apart.</summary>
        public void TakeProjectileHit(Transform hitVoxel, Vector3 hitPoint, Vector3 impactDirection)
        {
            if (hasBeenHit || hitVoxel == null || !hitVoxel.gameObject.activeSelf)
                return;

            SpawnWeaponDebris(hitVoxel, impactDirection);
            hitVoxel.gameObject.SetActive(false);
            currentHealth--;
            if (currentHealth <= 0)
                Explode(hitPoint, impactDirection);
        }

        /// <summary>Chooses an intact voxel on the struck rear surface so repeated fire peels the box inward instead of clearing one horizontal row.</summary>
        public bool TryGetNextProjectileVoxel(Vector3 segmentStart, Vector3 direction, float segmentLength,
            out Transform hitVoxel)
        {
            hitVoxel = null;
            if (hasBeenHit || segmentLength <= 0f)
                return false;

            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            float rearSurfaceDistance = float.PositiveInfinity;
            foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>())
            {
                Transform voxel = renderer.transform;
                if (!voxel.gameObject.activeInHierarchy)
                    continue;

                Vector3 offset = voxel.position - segmentStart;
                float forwardDistance = Vector3.Dot(offset, direction);
                if (forwardDistance < 0f || forwardDistance > segmentLength ||
                    Mathf.Abs(Vector3.Dot(offset, right)) > 1.8f)
                    continue;
                rearSurfaceDistance = Mathf.Min(rearSurfaceDistance, forwardDistance);
            }

            if (float.IsPositiveInfinity(rearSurfaceDistance))
                return false;

            const float rearSurfaceDepth = 0.55f;
            float bestScore = float.PositiveInfinity;
            float randomness = definition != null ? definition.rearSurfaceHitRandomness : 0.8f;
            foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>())
            {
                Transform voxel = renderer.transform;
                if (!voxel.gameObject.activeInHierarchy)
                    continue;

                Vector3 offset = voxel.position - segmentStart;
                float forwardDistance = Vector3.Dot(offset, direction);
                if (forwardDistance < rearSurfaceDistance || forwardDistance > rearSurfaceDistance + rearSurfaceDepth)
                    continue;

                float lateralDistance = Mathf.Abs(Vector3.Dot(offset, right));
                if (lateralDistance > 1.8f)
                    continue;

                Vector3 pointOnPath = segmentStart + direction * forwardDistance;
                float score = Mathf.Abs(voxel.position.y - pointOnPath.y) * 2f + lateralDistance + Random.value * randomness;
                if (score >= bestScore)
                    continue;
                bestScore = score;
                hitVoxel = voxel;
            }

            return hitVoxel != null;
        }

        private void BuildVoxelBox()
        {
            const float voxelSize = 0.55f;
            for (int x = -1; x <= 1; x++)
            for (int y = 0; y < 3; y++)
            for (int z = -1; z <= 1; z++)
            {
                VoxelRacerBootstrap.CreateBlock("Brown Box Voxel", transform,
                    new Vector3(x * voxelSize, 0.28f + y * voxelSize, z * voxelSize),
                    Vector3.one * voxelSize, VoxelRacerBootstrap.ObstacleMaterial);
            }
        }

        private void SpawnWeaponDebris(Transform source, Vector3 impactDirection)
        {
            var debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.name = "Box Damage Voxel";
            debris.transform.position = source.position + impactDirection.normalized * 0.2f + Random.insideUnitSphere * 0.08f;
            debris.transform.rotation = Random.rotation;
            float debrisScale = definition != null ? definition.weaponDebrisScale : 0.65f;
            debris.transform.localScale = source.lossyScale * Random.Range(0.75f, 1.15f) * debrisScale;
            debris.GetComponent<MeshRenderer>().sharedMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
            Destroy(debris.GetComponent<BoxCollider>());
            float forwardForce = definition != null ? definition.weaponDebrisForwardForce : ProjectileDebrisForce;
            float upwardForce = definition != null ? definition.weaponDebrisUpwardForce : 2.2f;
            float spreadForce = definition != null ? definition.weaponDebrisSpreadForce : 1.2f;
            float lifetime = definition != null ? definition.weaponDebrisLifetime : 1.1f;
            debris.AddComponent<VoxelDebris>().Launch(impactDirection.normalized * forwardForce +
                Random.insideUnitSphere * spreadForce + Vector3.up * upwardForce, lifetime);
        }

        private void Explode(Vector3 hitPoint, Vector3 impactDirection)
        {
            hasBeenHit = true;
            VoxelDestructionExplosion.Play(transform.position + Vector3.up * 0.6f,
                definition != null ? definition.explosionEffectScale : 0.75f);
            var voxels = new List<Transform>();
            foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>())
                if (renderer.gameObject.activeInHierarchy)
                    voxels.Add(renderer.transform);

            voxels.Sort((first, second) => (first.position - hitPoint).sqrMagnitude.CompareTo((second.position - hitPoint).sqrMagnitude));
            int debrisCount = Mathf.Min(voxels.Count, definition != null ? definition.explosionDebrisCount : 18);
            for (int index = 0; index < debrisCount; index++)
            {
                SpawnExplosionDebris(voxels[index], impactDirection);
                voxels[index].gameObject.SetActive(false);
            }

            float forwardForce = definition != null ? definition.explosionForwardForce : 8f;
            float upwardForce = definition != null ? definition.explosionUpwardForce : 4f;
            velocity = impactDirection.normalized * forwardForce + Vector3.up * upwardForce;
            destroyTime = Time.time + (definition != null ? definition.destroyedLifetime : 1.8f);
        }

        private void SpawnExplosionDebris(Transform source, Vector3 impactDirection)
        {
            var debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.name = "Box Explosion Voxel";
            debris.transform.position = source.position + Random.insideUnitSphere * 0.12f;
            debris.transform.rotation = Random.rotation;
            float debrisScale = definition != null ? definition.explosionDebrisScale : 0.7f;
            debris.transform.localScale = source.lossyScale * Random.Range(0.7f, 1.1f) * debrisScale;
            debris.GetComponent<MeshRenderer>().sharedMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
            Destroy(debris.GetComponent<BoxCollider>());
            float forwardForce = definition != null ? definition.explosionForwardForce : 8f;
            float upwardForce = definition != null ? definition.explosionUpwardForce : 4f;
            float spreadForce = definition != null ? definition.explosionSpreadForce : 3f;
            float lifetime = definition != null ? definition.explosionDebrisLifetime : 1.5f;
            debris.AddComponent<VoxelDebris>().Launch(impactDirection.normalized * forwardForce +
                Random.insideUnitSphere * spreadForce + Vector3.up * upwardForce, lifetime);
        }
    }
}
