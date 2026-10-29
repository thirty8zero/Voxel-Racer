using UnityEngine;

namespace VoxelRacer
{
    public sealed partial class VoxelEnemyCar
    {
        private VoxelBichoLocoRig bichoRig;
        public VoxelBichoLocoRig HopperRig => bichoRig;
        public bool IsBodyShielded => bichoRig != null && bichoRig.HasLiveHoppers;
        private bool bichoPsychoDriving;
        private VoxelPsychoBugTuning bichoBodyBehaviour;
        public bool IsBichoPsychoPhase => bichoPsychoDriving;

        internal void RefreshBichoBodyPhase()
        {
            if (bichoRig == null || hasBeenRammed) return;
            bool enable = bichoRig.Settings.usePsychoBehaviourAfterHoppers && !bichoRig.HasLiveHoppers && !IsBossEntering;
            if (enable == bichoPsychoDriving) return;
            if (enable)
            {
                bichoBodyBehaviour = bichoRig.Settings.bodyPhaseBehaviour ??
                    Resources.Load<VoxelPsychoBugTuning>("EnemyVehicles/PsychoBugBehaviour");
                if (bichoBodyBehaviour == null) return;
                bichoPsychoDriving = true;
                targetLaneOffset = NearestPsychoLane(laneOffset);
                ConfigurePsychoBug();
            }
            else
            {
                SetPsychoPhase(PsychoBugPhase.Hunting);
                bichoPsychoDriving = false;
                bossLanePair = Mathf.Clamp(Mathf.RoundToInt(laneOffset / bossLaneWidth + (bossLaneCount - 2) * .5f), 0, bossLaneCount - 2);
                targetLaneOffset = (bossLanePair - (bossLaneCount - 2) * .5f) * bossLaneWidth;
                nextBossLaneChange = Time.time + Mathf.Max(.1f, bossSettings.minimumLaneChangeInterval);
                bichoRig.ResumeApproachRetreat();
            }
            previousCollisionRelative = target.CollisionTrackPosition - new Vector2(LaneOffset, TrackDistance);
        }

        internal void ApplyHopperHealthDamage(float damage)
        {
            if (!hasBeenRammed) CurrentHealth = Mathf.Max(0, CurrentHealth - Mathf.Max(0, damage));
        }

        private bool ConsumeBichoProjectile(Transform voxel, float damage, Vector3 point, Vector3 direction, bool awardPoints)
        {
            var hopper = voxel != null ? voxel.GetComponentInParent<VoxelBichoHopper>() : null;
            if (hopper != null && hopper.Rig == bichoRig)
            {
                hopper.TakeDamage(damage * Mathf.Max(0, bossSettings.gunHealthDamageMultiplier), point, direction, awardPoints);
                return true;
            }
            // A body shot is absorbed until every hopper is gone, with no health/voxel loss or player credit.
            return bichoRig.HasLiveHoppers;
        }

        private bool TryBichoHopperContact(Vector2 relative)
        {
            if (bichoRig == null || !bichoRig.HasLiveHoppers || hasBeenRammed) return false;
            var hopper = bichoRig.FindRamContact(previousCollisionRelative, relative, out var direction);
            if (hopper == null) return false;
            if (Time.time >= nextCollisionTime) ApplyBichoRam(hopper, direction);
            return true;
        }

        private void ApplyBichoRam(VoxelBichoHopper hopper, Vector3 direction)
        {
            nextCollisionTime = Time.time + trafficTuning.collisionCooldown;
            if (direction.sqrMagnitude < .001f) direction = target.transform.forward;
            bool rear = IsRearImpact(direction);
            int previousDamage = target.damageVoxelsPerHit;
            try
            {
                target.damageVoxelsPerHit = Random.Range(Mathf.Min(Tuning.playerDamageVoxelsMin, Tuning.playerDamageVoxelsMax),
                    Mathf.Max(Tuning.playerDamageVoxelsMin, Tuning.playerDamageVoxelsMax) + 1);
                target.ApplyCollisionDamage(target.GetDamageSurfacePoint(transform.position), direction, "Enemy collision");
            }
            finally { target.damageVoxelsPerHit = previousDamage; }
            if (hopper != null)
            {
                float requested = VoxelPloughUpgradeState.ImpactDamage(VoxelWheelSpikeUpgradeState.CalculateRamDamage(
                    Tuning.playerRamDamage, rear), target.transform, direction) * target.BoostRamDamageMultiplier;
                float applied = hopper.TakeDamage(requested, hopper.transform.position, direction, false);
                if (applied > 0) VoxelMissionProgress.ReportEnemyRamDamage(applied);
            }
            // Keep the full-width assembly inside the road: only the player recoils while hoppers remain.
            target.ApplyRamResponse(rear, direction, Tuning);
        }
    }
}
