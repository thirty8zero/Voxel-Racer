using UnityEngine;

namespace VoxelRacer
{
    /// <summary>A temporary brown voxel crate that launches away after contact.</summary>
    public sealed class VoxelObstacle : MonoBehaviour
    {
        private VoxelCarController target;
        private bool hasBeenHit;
        private Vector3 velocity;
        private float destroyTime;
        private EndlessVoxelRoad path;
        private float trackDistance;
        private float laneOffset;

        public void Configure(VoxelCarController player, EndlessVoxelRoad road, float distance, float offset)
        {
            target = player;
            path = road;
            trackDistance = distance;
            laneOffset = offset;
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
            target.ApplyDamage(target.GetDamageSurfacePoint(transform.position), hitDirection);
            velocity = hitDirection * 15f + Vector3.up * 6f;
            destroyTime = Time.time + 2.5f;
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
    }
}
