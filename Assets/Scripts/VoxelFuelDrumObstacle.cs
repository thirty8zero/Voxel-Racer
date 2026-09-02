using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Three explosive voxel fuel drums: destroy one with weapons or avoid the whole group.</summary>
    public sealed class VoxelFuelDrumObstacle : MonoBehaviour
    {
        public float LaneOffset => laneOffset;
        public float TrackDistance => trackDistance;

        private static Material drumMaterial;
        private static Material stripeMaterial;
        private readonly Dictionary<Transform, int> drumHealth = new();
        private VoxelCarController target;
        private VoxelStaticObstacleDefinition definition;
        private EndlessVoxelRoad path;
        private float trackDistance;
        private float laneOffset;
        private bool hasExploded;
        private Vector3 velocity;
        private float destroyTime;

        public void Configure(VoxelCarController player, EndlessVoxelRoad road, VoxelStaticObstacleDefinition value,
            float distance, float offset)
        {
            target = player;
            path = road;
            definition = value;
            trackDistance = distance;
            laneOffset = offset;
            transform.localScale = Vector3.one * 2f;
            ApplyTrackPose();
            BuildDrums();
        }

        private void Update()
        {
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }

            if (!hasExploded)
            {
                bool overlapsLane = Mathf.Abs(target.CurrentLaneOffset - laneOffset) < 2f;
                bool overlapsDepth = Mathf.Abs(target.TrackDistance - trackDistance) < 3.5f;
                if (overlapsLane && overlapsDepth)
                    Explode(true, target.transform.forward);
                else if (trackDistance < target.TrackDistance - 25f)
                    Destroy(gameObject);
                return;
            }

            velocity += Physics.gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            if (velocity.sqrMagnitude > 0.001f)
                transform.Rotate(velocity.normalized * 260f * Time.deltaTime, Space.World);
            if (Time.time >= destroyTime)
                Destroy(gameObject);
        }

        public void TakeProjectileHit(Transform hitVoxel, Vector3 hitPoint, Vector3 impactDirection)
        {
            if (hasExploded || hitVoxel == null)
                return;

            Transform drum = FindDrumRoot(hitVoxel);
            if (drum == null)
                return;

            SpawnWeaponDebris(hitVoxel, impactDirection);
            hitVoxel.gameObject.SetActive(false);
            drumHealth.TryGetValue(drum, out int health);
            health = health <= 0 ? Mathf.Max(1, definition != null ? definition.hitPoints : 3) : health;
            health--;
            if (health > 0)
            {
                drumHealth[drum] = health;
                return;
            }
            Explode(false, impactDirection);
        }

        /// <summary>Continues selecting intact rear-surface drum voxels after the directly hit row has been removed.</summary>
        public bool TryGetNextProjectileVoxel(Vector3 segmentStart, Vector3 direction, float segmentLength,
            out Transform hitVoxel)
        {
            hitVoxel = null;
            if (hasExploded || segmentLength <= 0f)
                return false;

            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            float rearSurfaceDistance = float.PositiveInfinity;
            foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.gameObject.activeInHierarchy)
                    continue;
                Vector3 offset = renderer.transform.position - segmentStart;
                float forwardDistance = Vector3.Dot(offset, direction);
                if (forwardDistance < 0f || forwardDistance > segmentLength ||
                    Mathf.Abs(Vector3.Dot(offset, right)) > 2.5f)
                    continue;
                rearSurfaceDistance = Mathf.Min(rearSurfaceDistance, forwardDistance);
            }

            if (float.IsPositiveInfinity(rearSurfaceDistance))
                return false;

            const float rearSurfaceDepth = 0.6f;
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
                if (lateralDistance > 2.5f)
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

        private void Explode(bool damagedPlayer, Vector3 direction)
        {
            if (hasExploded)
                return;
            hasExploded = true;
            VoxelDestructionExplosion.Play(transform.position + Vector3.up * 1.1f,
                definition != null ? definition.explosionEffectScale : 1.2f);
            if (!damagedPlayer)
            {
                VoxelMissionProgress.ReportFuelDrumDestroyed();
                float popupDuration = VoxelMissionProgress.Active?.Tuning != null
                    ? VoxelMissionProgress.Active.Tuning.fuelDrumDestroyedPopupDuration
                    : 2f;
                VoxelScorePopup.ShowFuelDrumDestroyed(transform.position + Vector3.up * 2.8f,
                    VoxelMissionProgress.GetFuelDrumDestroyedPoints(), popupDuration);
            }
            if (damagedPlayer)
            {
                int originalDamage = target.damageVoxelsPerHit;
                int minimum = definition != null ? definition.playerDamageVoxelsMin : 12;
                int maximum = definition != null ? definition.playerDamageVoxelsMax : 18;
                target.damageVoxelsPerHit = Random.Range(Mathf.Min(minimum, maximum), Mathf.Max(minimum, maximum) + 1);
                target.ApplyDamage(target.GetDamageSurfacePoint(transform.position), direction);
                target.damageVoxelsPerHit = originalDamage;
            }

            var voxels = new List<Transform>();
            foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>())
                if (renderer.gameObject.activeInHierarchy)
                    voxels.Add(renderer.transform);
            // Fuel drums should completely disintegrate. Reuse each existing
            // voxel as debris instead of instantiating a duplicate for it.
            for (int index = 0; index < voxels.Count; index++)
            {
                Transform voxel = voxels[index];
                LaunchExplosionVoxel(voxel, direction);
            }
            velocity = direction.normalized * 8f + Vector3.up * 5f;
            destroyTime = Time.time + (definition != null ? definition.destroyedLifetime : 1.8f);
        }

        private void ApplyTrackPose()
        {
            if (path == null)
                return;
            VoxelTrackPose pose = path.Evaluate(trackDistance);
            transform.position = pose.position + pose.right * laneOffset;
            transform.rotation = pose.rotation;
        }

        private void BuildDrums()
        {
            Material paint = GetDrumMaterial();
            Material stripe = GetStripeMaterial();
            Vector3[] positions = { new Vector3(-0.62f, 0f, 0.18f), new Vector3(0.62f, 0f, 0.18f), new Vector3(0f, 0f, -0.72f) };
            foreach (Vector3 position in positions)
            {
                Transform drum = new GameObject("Fuel Drum").transform;
                drum.SetParent(transform, false);
                drum.localPosition = position;
                drumHealth[drum] = Mathf.Max(1, definition != null ? definition.hitPoints : 3);
                BuildVoxelDrum(drum, paint, stripe);
            }
        }

        private static void BuildVoxelDrum(Transform drum, Material paint, Material stripe)
        {
            // The drum group is scaled to 2x, so these become approximately
            // 0.23-unit world voxels—close to the 0.25-unit car body voxels.
            const float voxelSpacing = 0.125f;
            const float voxelSize = 0.115f;
            const int layerCount = 7;

            // Twenty-nine voxels per layer form a much smoother stepped circle.
            // Every visible piece owns its own collider and can be selected,
            // detached, and launched as debris.
            for (int layer = 0; layer < layerCount; layer++)
            for (int x = -3; x <= 3; x++)
            for (int z = -3; z <= 3; z++)
            {
                if (x * x + z * z > 9)
                    continue;

                bool isStripe = layer == 3;
                Material material = isStripe ? stripe : paint;
                string voxelName = isStripe ? "Fuel Drum Stripe Voxel" : "Fuel Drum Body Voxel";
                Vector3 position = new Vector3(x * voxelSpacing,
                    voxelSize * 0.5f + layer * voxelSpacing, z * voxelSpacing);
                VoxelRacerBootstrap.CreateBlock(voxelName, drum, position,
                    Vector3.one * voxelSize, material);
            }
        }

        private static Transform FindDrumRoot(Transform child)
        {
            Transform current = child;
            while (current != null)
            {
                if (current.name == "Fuel Drum")
                    return current;
                current = current.parent;
            }
            return null;
        }

        private static Material GetDrumMaterial()
        {
            if (drumMaterial == null)
            {
                drumMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(0.72f, 0.13f, 0.05f) };
                drumMaterial.SetColor("_BaseColor", drumMaterial.color);
            }
            return drumMaterial;
        }

        private static Material GetStripeMaterial()
        {
            if (stripeMaterial == null)
            {
                stripeMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = new Color(1f, 0.72f, 0.06f) };
                stripeMaterial.SetColor("_BaseColor", stripeMaterial.color);
            }
            return stripeMaterial;
        }

        private void SpawnWeaponDebris(Transform source, Vector3 direction)
        {
            GameObject debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.name = "Fuel Drum Damage Voxel";
            debris.transform.position = source.position + direction.normalized * 0.16f + Random.insideUnitSphere * 0.08f;
            debris.transform.rotation = Random.rotation;
            float debrisScale = definition != null ? definition.weaponDebrisScale : 0.65f;
            debris.transform.localScale = source.lossyScale * Random.Range(0.75f, 1.15f) * debrisScale;
            debris.GetComponent<MeshRenderer>().sharedMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
            Destroy(debris.GetComponent<BoxCollider>());
            float forwardForce = definition != null ? definition.weaponDebrisForwardForce : 6f;
            float upwardForce = definition != null ? definition.weaponDebrisUpwardForce : 2.2f;
            float spreadForce = definition != null ? definition.weaponDebrisSpreadForce : 1.2f;
            float lifetime = definition != null ? definition.weaponDebrisLifetime : 1.1f;
            debris.AddComponent<VoxelDebris>().Launch(direction.normalized * forwardForce +
                Random.insideUnitSphere * spreadForce + Vector3.up * upwardForce, lifetime);
        }

        private void LaunchExplosionVoxel(Transform source, Vector3 direction)
        {
            Vector3 worldScale = source.lossyScale;
            source.SetParent(null, true);
            source.name = "Fuel Drum Explosion Voxel";
            source.position += Random.insideUnitSphere * 0.12f;
            source.rotation = Random.rotation;
            float debrisScale = definition != null ? definition.explosionDebrisScale : 0.7f;
            source.localScale = worldScale * Random.Range(0.7f, 1.1f) * debrisScale;
            Destroy(source.GetComponent<BoxCollider>());
            float forwardForce = definition != null ? definition.explosionForwardForce : 8f;
            float upwardForce = definition != null ? definition.explosionUpwardForce : 4f;
            float spreadForce = definition != null ? definition.explosionSpreadForce : 3f;
            float lifetime = definition != null ? definition.explosionDebrisLifetime : 1.5f;
            source.gameObject.AddComponent<VoxelDebris>().Launch(direction.normalized * forwardForce +
                Random.insideUnitSphere * spreadForce + Vector3.up * upwardForce, lifetime);
        }
    }
}
