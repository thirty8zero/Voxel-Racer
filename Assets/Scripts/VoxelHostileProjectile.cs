using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Lightweight hostile projectile. It uses raycasts rather than rigidbodies and never reports mission score.</summary>
    public sealed class VoxelHostileProjectile : MonoBehaviour
    {
        private const float BulletLength = .38f;
        private const float TracerLength = 1.2f;
        private Vector3 direction;
        private Vector3 launchPosition;
        private float speed;
        private float remainingLifetime;
        private VoxelRoadsideTurretTuning tuning;
        private VoxelCarController target;
        private LineRenderer tracer;
        private static Mesh bulletMesh;
        private static Material tracerMaterial;
        private static RaycastHit[] hitBuffer = new RaycastHit[64];

        public static VoxelHostileProjectile Create(Vector3 position, Vector3 firingDirection,
            VoxelRoadsideTurretTuning value, VoxelCarController player)
        {
            var projectileObject = new GameObject("Turret Projectile");
            projectileObject.transform.SetPositionAndRotation(position, Quaternion.LookRotation(firingDirection));
            projectileObject.transform.localScale = new Vector3(0.13f, 0.13f, BulletLength);
            if (bulletMesh == null)
                bulletMesh = Resources.Load<Mesh>("EnemyTurrets/Model/RoadsideTurretBulletMesh");
            projectileObject.AddComponent<MeshFilter>().sharedMesh = bulletMesh;
            var renderer = projectileObject.AddComponent<MeshRenderer>();

            Material material = Resources.Load<Material>("CarMaterials/FormulaWhite");
            if (material == null)
                material = VoxelRacerBootstrap.ObstacleCarPaintMaterial;
            if (material != null)
                renderer.sharedMaterial = material;
            var projectile = projectileObject.AddComponent<VoxelHostileProjectile>();
            projectile.direction = firingDirection.normalized;
            projectile.launchPosition = position;
            projectile.speed = value.projectileSpeed;
            projectile.remainingLifetime = value.projectileLifetime;
            projectile.tuning = value;
            projectile.target = player;
            projectile.CreateTracer();
            return projectile;
        }

        private void CreateTracer()
        {
            if (tracerMaterial == null)
                tracerMaterial = Resources.Load<Material>("Effects/YellowVoxelImpactSpark");
            if (tracerMaterial == null)
                return;

            var visual = new GameObject("Yellow Tracer");
            visual.transform.SetParent(transform, false);
            tracer = visual.AddComponent<LineRenderer>();
            tracer.sharedMaterial = tracerMaterial;
            tracer.useWorldSpace = true;
            tracer.startWidth = .08f;
            tracer.endWidth = 0f;
            tracer.positionCount = 2;
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.receiveShadows = false;
            UpdateTracer();
        }

        private void UpdateTracer()
        {
            if (tracer == null)
                return;

            // Straight shots only need the last stretch of their path. Grow it from the muzzle.
            Vector3 rear = transform.position - direction * (BulletLength * .5f);
            float length = Mathf.Clamp(Vector3.Dot(rear - launchPosition, direction), 0f, TracerLength);
            tracer.SetPosition(0, rear);
            tracer.SetPosition(1, rear - direction * length);
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
            UpdateTracer();
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
