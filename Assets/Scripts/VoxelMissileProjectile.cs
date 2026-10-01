using UnityEngine;
namespace VoxelRacer
{
    public sealed class VoxelMissileProjectile : MonoBehaviour
    {
        private VoxelGunTuning tuning;
        private Transform owner;
        private EndlessVoxelRoad road;
        private Vector3 start, forward, right;
        private float startDistance, laneOffset, launchOffset, launchHeight, side, speed, travelled, laneHalfWidth, edgeOffset;
        private bool finished, settled;
        private Vector3 settledPosition, settledForward;
        private readonly RaycastHit[] hits = new RaycastHit[64];
        public static VoxelMissileProjectile Create(Vector3 muzzle, VoxelCarController car, float side, VoxelGunTuning tuning)
        {
            if (tuning.missilePrefab == null) return null;
            var go = Instantiate(tuning.missilePrefab, muzzle, car.transform.rotation); go.name = "Player Missile";
            var missile = go.AddComponent<VoxelMissileProjectile>();
            missile.tuning = tuning; missile.owner = car.transform; missile.road = car.TrackPath;
            missile.start = muzzle; missile.side = side < 0 ? -1 : 1;
            missile.forward = Vector3.ProjectOnPlane(car.transform.forward, Vector3.up).normalized;
            missile.right = Vector3.Cross(Vector3.up, missile.forward);
            missile.speed = Mathf.Max(1, tuning.projectileSpeed) + Mathf.Max(0, car.CurrentSpeed);
            missile.launchHeight = muzzle.y - car.transform.position.y;
            missile.launchOffset = Vector3.Dot(muzzle - car.transform.position, missile.right);
            missile.laneHalfWidth = Mathf.Max(.05f, car.laneWidth * .5f);
            missile.edgeOffset = Mathf.Max(0, missile.laneHalfWidth - tuning.missileLaneEdgeInset);
            missile.laneOffset = car.CollisionTrackPosition.x;
            if (missile.road != null)
            {
                float index = Mathf.Clamp(Mathf.Round(missile.laneOffset / car.laneWidth + (car.laneCount - 1) * .5f), 0, car.laneCount - 1);
                float centre = (index - (car.laneCount - 1) * .5f) * car.laneWidth;
                missile.launchOffset += missile.laneOffset - centre;
                missile.laneOffset = centre;
            }
            missile.startDistance = car.CollisionTrackPosition.y + Vector3.Dot(muzzle - car.transform.position, missile.forward);
            return missile;
        }
        private Vector3 PositionAt(float distance)
        {
            float descent = Mathf.Max(.1f, tuning.missileDescentDistance);
            if (road != null && !tuning.missileFollowRoad && distance >= descent)
            {
                if (!settled)
                {
                    var exitPose = road.Evaluate(startDistance + descent);
                    settledPosition = exitPose.position + exitPose.right * (laneOffset + side * edgeOffset) + Vector3.up * tuning.missileCruiseHeight;
                    settledForward = exitPose.forward;
                    settled = true;
                }
                return settledPosition + settledForward * (distance - descent);
            }
            float blend = Mathf.SmoothStep(0, 1, distance / descent);
            float height = Mathf.Lerp(launchHeight, tuning.missileCruiseHeight, blend);
            float lateral = Mathf.Lerp(launchOffset, side * edgeOffset, blend);
            if (road != null)
            {
                var pose = road.Evaluate(startDistance + distance);
                return pose.position + pose.right * (laneOffset + lateral) + Vector3.up * height;
            }
            return start + forward * distance + right * (lateral - launchOffset) + Vector3.up * (height - launchHeight);
        }
        private void Update() => Advance(Time.deltaTime);
        private void Advance(float dt)
        {
            if (VoxelPauseMenu.IsPaused || finished || dt <= 0) return;
            float end = Mathf.Min(travelled + speed * dt, tuning.maximumRange);
            // Short swept segments follow the descent and road curves even at low frame rates.
            while (travelled < end)
            {
                float nextDistance = Mathf.Min(travelled + .75f, end);
                Vector3 next = PositionAt(nextDistance), from = transform.position;
                Vector3 delta = next - from; float length = delta.magnitude;
                Vector3 direction = length > .0001f ? delta / length : forward;
                bool struck = VoxelMissileTarget.Trace(from, direction, length, out var target, out var voxel, out var point, out float nearest, laneHalfWidth);
                int count = Physics.RaycastNonAlloc(from, direction, hits, length, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    var hit = hits[i];
                    if ((owner != null && hit.transform.IsChildOf(owner)) || hit.transform.IsChildOf(transform) ||
                        hit.collider.GetComponentInParent<VoxelMissileTarget>() != null || hit.distance >= nearest) continue;
                    struck = true; nearest = hit.distance; point = hit.point; target = null; voxel = null;
                }
                if (struck) { Detonate(point, direction, target, voxel, true); return; }
                transform.SetPositionAndRotation(next, Quaternion.LookRotation(direction)); travelled = nextDistance;
            }
            if (travelled >= tuning.maximumRange) Detonate(transform.position, transform.forward, null, null, false);
        }
        private void Detonate(Vector3 point, Vector3 direction, VoxelMissileTarget target, Transform voxel, bool impact)
        {
            if (finished) return;
            transform.position = point;
            VoxelMissileTarget.Blast(point, tuning.areaOfEffectRadius, tuning.damagePerBullet, direction, target, voxel);
            VoxelDestructionExplosion.Play(point, Mathf.Max(.5f, tuning.areaOfEffectRadius), shakeCamera: impact);
            Finish();
        }
        private void Finish()
        {
            finished = true;
            foreach (var particles in GetComponentsInChildren<ParticleSystem>())
            {
                particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                particles.transform.SetParent(null, true);
                Destroy(particles.gameObject, 2f);
            }
            Destroy(gameObject);
        }
    }
}
