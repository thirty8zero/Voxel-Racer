using UnityEngine;

namespace VoxelRacer
{
    /// <summary>A static, lane-independent hazard which fires bursts across the road.</summary>
    public sealed class VoxelRoadsideTurret : MonoBehaviour
    {
        private VoxelCarController target;
        private EndlessVoxelRoad path;
        private VoxelRoadsideTurretTuning tuning;
        private float trackDistance;
        private float side;
        private float aimAngle;
        private float nextShotTime;
        private float firingUntil;
        private float nextBurstTime;
        private bool firing;
        private Transform barrel;

        public float TrackDistance => trackDistance;

        public void Configure(VoxelCarController player, EndlessVoxelRoad road, VoxelRoadsideTurretTuning value,
            float distance, float roadsideSide)
        {
            target = player;
            path = road;
            tuning = value;
            trackDistance = distance;
            side = Mathf.Sign(roadsideSide);
            if (Mathf.Approximately(side, 0f))
                side = 1f;
            aimAngle = Random.Range(Mathf.Min(value.minimumAimAngle, value.maximumAimAngle),
                Mathf.Max(value.minimumAimAngle, value.maximumAimAngle));
            BuildVisuals();
            ApplyTrackPose();
            nextBurstTime = Time.time + Random.Range(0.15f, value.pauseDuration);
        }

        private void Update()
        {
            if (target == null || path == null || tuning == null || target.IsDestroyed)
            {
                Destroy(gameObject);
                return;
            }

            ApplyTrackPose();
            if (trackDistance < target.TrackDistance - 35f || trackDistance > target.TrackDistance + 180f)
            {
                Destroy(gameObject);
                return;
            }

            VoxelStartCountdown countdown = GetComponentInParent<VoxelStartCountdown>();
            if (countdown != null && !countdown.IsComplete)
                return;
            if (VoxelMissionProgress.Active?.IsComplete == true)
                return;

            if (!firing)
            {
                if (Time.time < nextBurstTime)
                    return;
                firing = true;
                firingUntil = Time.time + tuning.firingDuration;
                nextShotTime = Time.time;
            }

            if (Time.time >= firingUntil)
            {
                firing = false;
                nextBurstTime = Time.time + tuning.pauseDuration;
                return;
            }

            if (Time.time >= nextShotTime)
            {
                FireVolley();
                nextShotTime = Time.time + tuning.fireRate;
            }
        }

        private void ApplyTrackPose()
        {
            VoxelTrackPose pose = path.Evaluate(trackDistance);
            float roadsideOffset = path.roadWidth * 0.5f + tuning.distanceFromRoadEdge;
            transform.position = pose.position + pose.right * roadsideOffset * side;
            Vector3 acrossRoad = -pose.right * side;
            Vector3 trackUp = pose.rotation * Vector3.up;
            Vector3 aim = Quaternion.AngleAxis(aimAngle, trackUp) * acrossRoad;
            transform.rotation = Quaternion.LookRotation(aim, trackUp);
        }

        private void FireVolley()
        {
            Transform muzzle = barrel != null ? barrel : transform;
            int count = Mathf.Max(1, tuning.bulletsPerVolley);
            for (int index = 0; index < count; index++)
            {
                float normalizedIndex = count == 1 ? 0f : index / (float)(count - 1) - 0.5f;
                Vector3 direction = Quaternion.AngleAxis(normalizedIndex * tuning.volleySpreadDegrees, transform.up) * transform.forward;
                VoxelHostileProjectile.Create(muzzle.position + direction * 0.7f, direction, tuning, target);
            }
        }

        private void BuildVisuals()
        {
            Material body = Resources.Load<Material>("CarMaterials/FormulaBlack");
            Material detail = VoxelRacerBootstrap.ObstacleCarTrimMaterial;
            Material accent = VoxelRacerBootstrap.ObstacleCarPaintMaterial;
            if (body == null)
                body = detail;

            GameObject baseBlock = VoxelRacerBootstrap.CreateBlock("Turret Voxel Base", transform,
                new Vector3(0f, 0.30f, 0f), new Vector3(1.1f, 0.60f, 1.1f), body);
            GameObject pivot = VoxelRacerBootstrap.CreateBlock("Turret Voxel Pivot", transform,
                new Vector3(0f, 0.83f, 0f), new Vector3(0.72f, 0.55f, 0.72f), detail);
            GameObject housing = VoxelRacerBootstrap.CreateBlock("Turret Voxel Housing", transform,
                new Vector3(0f, 1.12f, 0.18f), new Vector3(0.94f, 0.58f, 1.08f), body);
            barrel = VoxelRacerBootstrap.CreateBlock("Turret Voxel Barrel", transform,
                new Vector3(0f, 1.18f, 0.93f), new Vector3(0.35f, 0.30f, 1.00f), accent).transform;
            GameObject muzzle = VoxelRacerBootstrap.CreateBlock("Turret Voxel Muzzle", transform,
                new Vector3(0f, 1.18f, 1.43f), new Vector3(0.50f, 0.42f, 0.18f), detail);

            // Turrets are hazards, not current weapon targets; disabling their colliders
            // prevents player shots being absorbed by the decorative voxel model.
            foreach (BoxCollider collider in GetComponentsInChildren<BoxCollider>())
                collider.enabled = false;
        }
    }
}
