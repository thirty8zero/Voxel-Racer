using UnityEngine;

namespace VoxelRacer
{
    public sealed partial class VoxelCameraFollow
    {
        private VoxelEnemyCar cameraBoss;
        private Bounds bossCameraBounds;
        private bool bossBoundsReady, bossCameraLatched;
        private float bossCameraBlend;

        public void SetBossCameraTarget(VoxelEnemyCar boss)
        {
            cameraBoss = boss;
            bossBoundsReady = false;
            bossCameraLatched = false;
        }

        private Vector3 GetBossAttackCameraOffset(VoxelCarController player, Vector3 cameraTarget,
            Quaternion heading, Vector3 normalOffset, float deltaTime)
        {
            var settings = Tuning;
            bool active = settings != null && settings.bossAttackCameraEnabled && player != null && !player.IsDestroyed &&
                cameraBoss != null && cameraBoss.isActiveAndEnabled && cameraBoss.CurrentHealth > 0 &&
                Mathf.Abs(cameraBoss.TrackDistance - player.CollisionTrackPosition.y) <= settings.bossAttackCameraTriggerDistance &&
                player.CollisionTrackPosition.x - cameraBoss.LaneOffset >= Mathf.Max(.1f, player.laneWidth) &&
                cameraBoss.SpikeAttackActive && cameraBoss.SpikePhase != VoxelEnemyCar.SpikeAttackPhase.Retreating &&
                VoxelMissionProgress.Active?.IsComplete != true && VoxelMissionProgress.Active?.IsFailed != true;
            // Recheck lane separation even after latching, so a left dodge releases the right view.
            // Track offsets avoid mistaking a distant van on a bend for one in a different lane.
            if (!active) bossCameraLatched = false;
            else if (!bossCameraLatched)
            {
                CacheBossCameraBounds();
                Vector3 normalCamera = cameraTarget + heading * normalOffset;
                Vector3 focus = player.transform.position + Vector3.up * .7f;
                bossCameraLatched = BossBlocksView(normalCamera, focus, Vector3.zero, settings.bossAttackCameraClearance);

                // Check the committed attack's alongside position during its warning, once it is
                // within the configured distance, so the lane-validated swing can start in time.
                Vector3 relative = Quaternion.Inverse(heading) * (cameraBoss.transform.position - player.transform.position);
                if (!bossCameraLatched && relative.x < -1f && relative.z >= 0)
                    bossCameraLatched = BossBlocksView(normalCamera, focus,
                        -(heading * Vector3.forward) * relative.z, settings.bossAttackCameraClearance);
            }

            float duration = settings != null ? (bossCameraLatched ? settings.bossAttackCameraSwingDuration :
                settings.bossAttackCameraReturnDuration) : .8f;
            bossCameraBlend = Mathf.MoveTowards(bossCameraBlend, bossCameraLatched ? 1f : 0f,
                Mathf.Max(0, deltaTime) / Mathf.Max(.01f, duration));
            float eased = Mathf.SmoothStep(0, 1, bossCameraBlend);
            Vector3 alternate = settings != null ? settings.bossAttackCameraOffset : new Vector3(8.5f, 11, -11);
            return OrbitOffset(normalOffset, alternate, eased);
        }

        private void CacheBossCameraBounds()
        {
            if (bossBoundsReady) return;
            // Cache once after the entrance has finished, when the first attack begins. Test a single
            // local-space box thereafter: no physics colliders, per-frame renderer scans or allocations.
            var rig = cameraBoss.GetComponentInChildren<VoxelBossSpikeRig>();
            foreach (var renderer in cameraBoss.GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.GetComponentInParent<VoxelEnemyHealthBar>() != null) continue;
                // Open doors and extended spikes must not turn empty space beside the van into
                // a giant solid obstruction box. Its opaque body is what hides the player.
                if (rig != null && ((rig.leftDoor != null && renderer.transform.IsChildOf(rig.leftDoor)) ||
                    (rig.rightDoor != null && renderer.transform.IsChildOf(rig.rightDoor)) ||
                    (rig.spikes != null && renderer.transform.IsChildOf(rig.spikes)))) continue;
                var b = renderer.localBounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 point = cameraBoss.transform.InverseTransformPoint(renderer.transform.TransformPoint(corner));
                    if (!bossBoundsReady) { bossCameraBounds = new Bounds(point, Vector3.zero); bossBoundsReady = true; }
                    else bossCameraBounds.Encapsulate(point);
                }
            }
        }

        private bool BossBlocksView(Vector3 cameraPosition, Vector3 focus, Vector3 predictedMovement, float clearance)
        {
            if (!bossBoundsReady) return false;
            var bounds = bossCameraBounds;
            bounds.Expand(Mathf.Max(0, clearance) * 2f);
            Vector3 from = cameraBoss.transform.InverseTransformPoint(cameraPosition - predictedMovement);
            Vector3 to = cameraBoss.transform.InverseTransformPoint(focus - predictedMovement);
            Vector3 direction = to - from;
            if (bounds.Contains(from)) return true;
            return direction.sqrMagnitude > .001f && bounds.IntersectRay(new Ray(from, direction.normalized), out float distance) &&
                distance < direction.magnitude;
        }

        private static Vector3 OrbitOffset(Vector3 from, Vector3 to, float blend)
        {
            float fromAngle = Mathf.Atan2(from.x, from.z) * Mathf.Rad2Deg;
            float toAngle = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            float angle = Mathf.LerpAngle(fromAngle, toAngle, blend) * Mathf.Deg2Rad;
            float radius = Mathf.Lerp(new Vector2(from.x, from.z).magnitude, new Vector2(to.x, to.z).magnitude, blend);
            return new Vector3(Mathf.Sin(angle) * radius, Mathf.Lerp(from.y, to.y, blend), Mathf.Cos(angle) * radius);
        }
    }
}
