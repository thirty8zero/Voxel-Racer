using UnityEngine;
namespace VoxelRacer
{
    public sealed partial class VoxelEnemyCar
    {
        public enum SpikeAttackPhase { Normal, Warning, Braking, Holding, Retreating }
        public SpikeAttackPhase SpikePhase { get; private set; }
        public bool SpikeAttackActive => IsBoss && spikeAttackTuning != null && SpikePhase != SpikeAttackPhase.Normal;
        // The rear slam attempt ends when braking reaches the player and enters Holding.
        // From there, ordinary body collisions must remain available for a side ram.
        private bool SpikeAttackDangerous => SpikePhase == SpikeAttackPhase.Braking;
        private float spikePhaseTime, spikeCheckTime;
        private bool spikeRamApplied;
        private bool spikeAttackDodged;
        private VoxelBossSpikeRig spikeRig;
        private float spikePlayerFront = 2.5f, spikePlayerHalfWidth = 1.3f;
        private float CollisionHalfWidth => IsSpikeDodgeCornerContact
            ? Mathf.Min(Tuning.collisionHalfWidth,
                bossLaneWidth * Mathf.Clamp(spikeAttackTuning.dodgeLaneFraction, .5f, 1.2f))
            : Tuning.collisionHalfWidth;
        private bool IsSpikeDodgeCornerContact => spikeAttackDodged &&
            Mathf.Abs(target.CollisionTrackPosition.x - LaneOffset) >=
            bossLaneWidth * Mathf.Clamp(spikeAttackTuning.dodgeLaneFraction, .5f, 1.2f);

        private bool IsSpikeAttackActivePart(Transform hitVoxel)
        {
            if(!IsBoss || !SpikeAttackActive || hitVoxel == null) return false;
            if(spikeRig == null) spikeRig = GetComponentInChildren<VoxelBossSpikeRig>();
            return IsSameOrChild(hitVoxel, spikeRig != null ? spikeRig.leftDoor : null) ||
                   IsSameOrChild(hitVoxel, spikeRig != null ? spikeRig.rightDoor : null) ||
                   IsSameOrChild(hitVoxel, spikeRig != null ? spikeRig.spikes : null);
        }

        private static bool IsSameOrChild(Transform candidate, Transform parent) =>
            candidate != null && parent != null && (candidate == parent || candidate.IsChildOf(parent));

        private void LateUpdate()
        {
            // Run after both vehicles move, including the player's boost offset.
            if(SpikeAttackActive && !hasBeenRammed && target != null) KeepSpikeBodiesSeparated();
        }

        private void KeepSpikeBodiesSeparated()
        {
            if (IsSpikeDodgeCornerContact) return;
            if(Mathf.Abs(target.CollisionTrackPosition.x - LaneOffset) > Tuning.collisionHalfWidth + spikePlayerHalfWidth) return;
            float minimumDistance = target.CollisionTrackPosition.y + SpikeBodyClearance;
            if(TrackDistance >= minimumDistance) return;
            trackDistance = minimumDistance - rearRamForwardOffset;
            currentSpeed = Mathf.Max(currentSpeed, target.CurrentSpeed);
            ApplyTrackPose();
            previousCollisionRelative = target.CollisionTrackPosition - new Vector2(LaneOffset, TrackDistance);
        }

        private void UpdateSpikeAttack(float dt)
        {
            if (dt <= 0) return;
            if (target.IsDestroyed || VoxelMissionProgress.Active?.IsComplete == true || VoxelMissionProgress.Active?.IsFailed == true)
            {
                SpikePhase = SpikeAttackPhase.Normal;
                spikeAttackDodged = false;
                if (spikeRig != null) spikeRig.SetPose(0, 0);
                spikeCheckTime = spikeAttackTuning != null ? spikeAttackTuning.checkInterval : float.PositiveInfinity;
                return;
            }
            if (spikeRig == null) spikeRig = GetComponentInChildren<VoxelBossSpikeRig>();
            if ((SpikePhase == SpikeAttackPhase.Warning || SpikePhase == SpikeAttackPhase.Braking) &&
                Mathf.Abs(target.CollisionTrackPosition.x - LaneOffset) >=
                bossLaneWidth * Mathf.Clamp(spikeAttackTuning.dodgeLaneFraction, .5f, 1.2f))
            {
                // Latch a successful dodge so the spike impact is ignored, but keep the
                // committed attack running through its normal hold and retreat phases.
                spikeAttackDodged = true;
            }
            if (SpikePhase == SpikeAttackPhase.Normal)
            {
                spikeCheckTime -= dt;
                if (spikeAttackTuning == null || spikeCheckTime > 0 || spikeRig == null) return;
                // Finish the current lane change before telegraphing; never chase the player's lane during the slam.
                if (Mathf.Abs(laneOffset - targetLaneOffset) > .02f) return;
                spikeCheckTime = Mathf.Max(1, spikeAttackTuning.checkInterval);
                if (TrackDistance - target.CollisionTrackPosition.y < Tuning.collisionHalfLength + 12 || Random.value >= spikeAttackTuning.chance) return;
                BeginSpikeAttack();
            }
            spikePhaseTime += dt;
            float gap = TrackDistance - target.CollisionTrackPosition.y;
            switch (SpikePhase)
            {
                case SpikeAttackPhase.Warning:
                    float warning = Mathf.Max(.5f, spikeAttackTuning.warningDuration);
                    spikeRig.SetPose(Mathf.Clamp01(spikePhaseTime / (warning * .6f)), Mathf.Clamp01((spikePhaseTime / warning - .6f) / .4f));
                    if (spikePhaseTime >= warning) SetSpikePhase(SpikeAttackPhase.Braking);
                    break;
                case SpikeAttackPhase.Braking:
                    spikeRig.SetPose(1, 1);
                    if (gap <= Mathf.Max(0,spikeAttackTuning.missDistance) + .4f) SetSpikePhase(SpikeAttackPhase.Holding);
                    else if (spikePhaseTime >= Mathf.Max(1, spikeAttackTuning.approachTimeout)) SetSpikePhase(SpikeAttackPhase.Retreating);
                    break;
                case SpikeAttackPhase.Holding:
                    if (spikePhaseTime >= Mathf.Max(.1f, spikeAttackTuning.holdDuration)) SetSpikePhase(SpikeAttackPhase.Retreating);
                    break;
                case SpikeAttackPhase.Retreating:
                    float retract = spikePhaseTime / Mathf.Max(.1f, spikeAttackTuning.retractDuration);
                    // Retract the spikes and close the doors together from the start of retreat.
                    spikeRig.SetPose(1 - Mathf.Clamp01(retract), 1 - Mathf.Clamp01(retract));
                    if (gap >= SpikeRetreatGap - 1 && retract >= 1)
                    {
                        spikeRig.SetPose(0, 0);
                        SetSpikePhase(SpikeAttackPhase.Normal);
                        spikeAttackDodged = false;
                        bossCatchingUp = false; bossMovingAway = false;
                        if(Tuning.mineLayer!=null) nextMineTime = Time.time + Mathf.Max(.1f, Tuning.mineLayer.dropInterval);
                        nextBossLaneChange = Time.time + Mathf.Max(.1f, bossSettings.minimumLaneChangeInterval);
                        spikeCheckTime = Mathf.Max(1, spikeAttackTuning.checkInterval);
                    }
                    break;
            }
        }
        private float SpikeBodyClearance => Mathf.Max(Tuning.collisionHalfLength, 2.65f * bossVisualScale) + spikePlayerFront + .2f;
        private float SpikeHitDistance => Mathf.Max(Tuning.collisionHalfLength + 1.7f * bossVisualScale, SpikeBodyClearance + .7f * bossVisualScale);
        private float SpikeRetreatGap => Mathf.Clamp(spikeAttackTuning.retreatDistance, 20, bossSettings.WarningDistance - 5);
        private void SetSpikePhase(SpikeAttackPhase phase) { SpikePhase = phase; spikePhaseTime = 0; }
        private void BeginSpikeAttack()
        {
            // Capture intact dimensions before damage removes parts of the player's body.
            spikePlayerFront = 2.5f; spikePlayerHalfWidth = 1.3f;
            foreach(var renderer in target.GetComponentsInChildren<MeshRenderer>())
            {
                var bounds = renderer.localBounds;
                for(int corner=0; corner<8; corner++)
                {
                    Vector3 local = bounds.center + Vector3.Scale(bounds.extents, new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                    Vector3 point = target.transform.InverseTransformPoint(renderer.transform.TransformPoint(local));
                    spikePlayerFront = Mathf.Max(spikePlayerFront, point.z);
                    spikePlayerHalfWidth = Mathf.Max(spikePlayerHalfWidth, Mathf.Abs(point.x));
                }
            }
            spikeRamApplied = false;
            rearRamPushStartedAt = sideRamStartedAt = -1;
            rearRamForwardOffset = sideRamOffset = 0;
            targetLaneOffset = laneOffset;
            SetSpikePhase(SpikeAttackPhase.Warning);
            nextMineTime = float.PositiveInfinity;
        }
        private float GetSpikeAttackSpeed()
        {
            float playerSpeed = Mathf.Max(0, target.CurrentSpeed);
            float gap = TrackDistance - target.CollisionTrackPosition.y;
            if (SpikePhase == SpikeAttackPhase.Warning) return Mathf.Max(playerSpeed, target.EffectiveTopSpeed);
            if (SpikePhase == SpikeAttackPhase.Retreating)
            {
                float remaining = Mathf.Max(0, SpikeRetreatGap - gap);
                float relative = Mathf.Min(Mathf.Max(1, spikeAttackTuning.pullAwaySpeed), Mathf.Sqrt(2 * Mathf.Max(1, spikeAttackTuning.braking) * remaining));
                return playerSpeed + relative;
            }
            // Attack-only reverse movement removes the player's speed as the closing-speed limit.
            // Start recovering early enough to settle alongside a dodging player without overshooting.
            float remainingGap = gap - Mathf.Max(0,spikeAttackTuning.missDistance);
            float closing = Mathf.Min(Mathf.Max(1,spikeAttackTuning.closingSpeed),
                Mathf.Abs(remainingGap) * Mathf.Max(.1f,spikeAttackTuning.closingResponse),
                Mathf.Sqrt(2 * Mathf.Max(1,spikeAttackTuning.acceleration) * Mathf.Abs(remainingGap)));
            return playerSpeed - Mathf.Sign(remainingGap) * closing;
        }
        private void ApplySpikeRam()
        {
            if (spikeRamApplied || spikeAttackDodged) return;
            spikeRamApplied = true;
            Vector3 hitPoint = target.GetDamageSurfacePoint(target.transform.position + target.transform.forward * 100f);
            SetSpikePhase(SpikeAttackPhase.Retreating);
            KeepSpikeBodiesSeparated();
            nextCollisionTime = Time.time + Mathf.Max(.1f, trafficTuning.collisionCooldown);
            VoxelSpikeImpact.Play(hitPoint, target.transform.forward, target.CurrentSpeed);
            int previousDamage = target.damageVoxelsPerHit;
            try
            {
                int min = Mathf.Max(0, spikeAttackTuning.damageMin);
                target.damageVoxelsPerHit = Random.Range(min, Mathf.Max(min, spikeAttackTuning.damageMax) + 1);
                if (target.damageVoxelsPerHit > 0)
                    target.ApplyDamage(hitPoint, -target.transform.forward, "Boss spike ram");
            }
            finally { target.damageVoxelsPerHit = previousDamage; }
        }
    }
}
