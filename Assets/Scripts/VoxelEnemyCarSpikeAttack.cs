using System.Collections.Generic;
using UnityEngine;
namespace VoxelRacer
{
    public sealed partial class VoxelEnemyCar
    {
        public enum SpikeAttackPhase { Normal, Warning, Braking, Holding, Retreating, SideRamImpact }
        public SpikeAttackPhase SpikePhase { get; private set; }
        public bool SpikeAttackActive => IsBoss && spikeAttackTuning != null && SpikePhase != SpikeAttackPhase.Normal;
        // The rear slam attempt ends when braking reaches the player and enters Holding.
        // From there, a successful dodge can be followed by a player side ram.
        private bool SpikeAttackDangerous => SpikePhase == SpikeAttackPhase.Braking;
        private float spikePhaseTime, spikeCheckTime;
        private bool spikeRamApplied;
        private bool spikeAttackDodged;
        private bool spikeDodgeCheckPending;
        private bool spikeSideRamConsumed;
        private VoxelBossSpikeRig spikeRig;
        private float spikePlayerFront = 2.5f, spikePlayerRear = 2.5f, spikePlayerHalfWidth = 1.3f;
        private float spikePlayerHeight;
        private float[] spikePlayerHeightWidths;
        private float spikeBossProfileHalfLength;
        private float[] spikeSideContactWidths;
        private const float SpikeBodyMargin = .02f;
        private const float SpikeHeightSlice = .15f;
        private const float SpikeSideReboundScale = 1.65f;
        private float spikeImpactSide, spikeImpactVanOffset;
        private float spikeResumeRetreatTime;
        private Vector3 spikeSideImpactPoint;
        private float SpikeImpactSparkDuration => Mathf.Clamp(spikeAttackTuning.sideRamSparkDuration, .08f, .35f);
        private float SpikeImpactRebound => Mathf.Clamp(spikeAttackTuning.sideRamReboundDuration, .12f, .5f);
        private Vector2 SpikeBodyHalfSize
        {
            get
            {
                float playerLength = Mathf.Max(spikePlayerFront, spikePlayerRear);
                float playerWidth = spikePlayerHalfWidth + .35f; // Room for ordinary steering yaw and roll.
                if (target.IsOilSpinning)
                    playerLength = playerWidth = new Vector2(playerWidth, playerLength).magnitude;
                return new Vector2(
                    Mathf.Max(Tuning.collisionHalfWidth, bossLaneWidth * .9f + playerWidth),
                    SpikeBodyClearance + playerLength - spikePlayerFront);
            }
        }
        private float SpikeDodgeOffset => bossLaneWidth * Mathf.Clamp(spikeAttackTuning.dodgeLaneFraction, .5f, 1.2f);

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
            UpdatePsychoBugAfterPlayer();
            ResolveRearContact();
            if (spikeAttackTuning != null && Tuning != null && !bossEntranceActive && !hasBeenRammed &&
                target != null && !target.IsDestroyed && !VoxelPauseMenu.IsPaused && Time.deltaTime > 0f &&
                VoxelMissionProgress.Active?.IsComplete != true && VoxelMissionProgress.Active?.IsFailed != true)
                ResolveSpikeBossContact();
        }

        private void ResolveSpikeBossContact()
        {
            // Use both cars' final movement, including this frame's steering and boost.
            if (spikeDodgeCheckPending) LatchSpikeDodge();
            Vector2 previous = previousCollisionRelative;
            bool impactInProgress = SpikePhase == SpikeAttackPhase.SideRamImpact;
            if (!impactInProgress) CheckVehicleContact();
            // Keep the exact mesh contact on the impact frame. Use the swept body envelope as they part.
            if (!hasBeenRammed && !target.IsDestroyed && (impactInProgress || SpikePhase != SpikeAttackPhase.SideRamImpact))
                KeepSpikeBodiesSeparated(previous);
            previousCollisionRelative = target.CollisionTrackPosition - new Vector2(LaneOffset, TrackDistance);
        }

        private static bool IsSpikeBodySide(Vector2 contact, Vector2 halfSize) =>
            halfSize.x - Mathf.Abs(contact.x) < halfSize.y - Mathf.Abs(contact.y);

        private Vector2 GetSpikeBodyHalfSize(Vector2 previous, Vector2 relative)
        {
            Vector2 halfSize = SpikeBodyHalfSize;
            if (!spikeAttackDodged || spikeSideContactWidths == null) return halfSize;

            float playerLength = Mathf.Max(spikePlayerFront, spikePlayerRear);
            Vector3 playerExtents = new(spikePlayerHalfWidth, spikePlayerHeight, playerLength);
            Quaternion inverseRotation = path != null
                ? Quaternion.Inverse(target.transform.rotation) * transform.rotation
                : Quaternion.Inverse(target.RoadVisualRotation);
            float lateralReach = ProjectSpikeExtent(inverseRotation * Vector3.right, playerExtents);
            float forwardReach = ProjectSpikeExtent(inverseRotation * Vector3.forward, playerExtents);
            // Include every slice the player's body crosses this frame, including its
            // overhang. A long diagonal frame cannot skip a wider wheel or bumper.
            int first = SpikeBossProfileIndex(Mathf.Min(previous.y, relative.y) - forwardReach);
            int last = SpikeBossProfileIndex(Mathf.Max(previous.y, relative.y) + forwardReach);
            float contactWidth = 0f;
            for (int slice = first; slice <= last; slice++)
                contactWidth = Mathf.Max(contactWidth, spikeSideContactWidths[slice]);
            if (contactWidth > 0f)
                halfSize.x = contactWidth + Mathf.Max(0f, lateralReach - spikePlayerHalfWidth);
            return halfSize;
        }

        private static float ProjectSpikeExtent(Vector3 axis, Vector3 extents) =>
            Mathf.Abs(axis.x) * extents.x + Mathf.Abs(axis.y) * extents.y + Mathf.Abs(axis.z) * extents.z;

        private void CheckDodgedSpikeContact(Vector2 relative)
        {
            Vector2 halfSize = GetSpikeBodyHalfSize(previousCollisionRelative, relative);
            if (Time.time < nextCollisionTime ||
                !VoxelVehicleCollision.Sweep(previousCollisionRelative, relative, halfSize, out var contact))
                return;

            if (!IsSpikeBodySide(contact, halfSize))
            {
                if (!SpikeAttackDangerous && Mathf.Abs(contact.x) < SpikeDodgeOffset)
                    RamByPlayer(-Mathf.Sign(contact.y) * transform.forward);
                return;
            }

            float side = Mathf.Sign(contact.x);
            // Require inward movement through the side face. Pulling alongside
            // longitudinally during a dodge must not spend the counterattack.
            if (spikeSideRamConsumed || !CanCounterSpikeAttack ||
                side * previousCollisionRelative.x < halfSize.x - .001f || side * relative.x >= halfSize.x ||
                !TryPlaceSpikeSideContact(side, halfSize.x))
                return;

            BeginSpikeSideImpact(side);
        }

        private bool TryPlaceSpikeSideContact(float side, float broadWidth)
        {
            if (path == null)
            {
                target.ResolveBossSideContact(LaneOffset + side * (broadWidth + SpikeBodyMargin));
                spikeSideImpactPoint = transform.position;
                return true;
            }

            // The swept envelope finds candidates. Match visible pieces at the same
            // height and depth before committing to the hit or stopping the approach.
            Matrix4x4 worldToBoss = transform.worldToLocalMatrix;
            var playerPieces = new List<Bounds>();
            Bounds playerEnvelope = default;
            foreach (var piece in target.GetComponentsInChildren<MeshRenderer>())
            {
                if (!piece.enabled) continue;
                Bounds bounds = TransformSpikeBounds(piece.localBounds, worldToBoss * piece.localToWorldMatrix);
                if (playerPieces.Count == 0) playerEnvelope = bounds;
                else playerEnvelope.Encapsulate(bounds);
                playerPieces.Add(bounds);
            }
            if (playerPieces.Count == 0) return false;

            Vector3 playerMin = playerEnvelope.min, playerMax = playerEnvelope.max;
            float correction = float.NegativeInfinity;
            Vector3 contact = default;
            foreach (var piece in GetComponentsInChildren<MeshRenderer>())
            {
                if (!piece.enabled) continue;
                Bounds van = TransformSpikeBounds(piece.localBounds, worldToBoss * piece.localToWorldMatrix);
                Vector3 vanMin = van.min, vanMax = van.max;
                float face = side > 0f ? vanMax.x : -vanMin.x;
                if (face < bossLaneWidth * .35f || playerMin.y > vanMax.y || playerMax.y < vanMin.y ||
                    playerMin.z > vanMax.z || playerMax.z < vanMin.z) continue;
                foreach (Bounds car in playerPieces)
                {
                    Vector3 carMin = car.min, carMax = car.max;
                    float gapCorrection = face - (side > 0f ? carMin.x : -carMax.x);
                    if (gapCorrection <= correction || carMin.y > vanMax.y || carMax.y < vanMin.y ||
                        carMin.z > vanMax.z || carMax.z < vanMin.z) continue;
                    correction = gapCorrection;
                    contact = new Vector3(side * face, (Mathf.Max(carMin.y, vanMin.y) + Mathf.Min(carMax.y, vanMax.y)) * .5f,
                        (Mathf.Max(carMin.z, vanMin.z) + Mathf.Min(carMax.z, vanMax.z)) * .5f);
                }
            }
            if (float.IsNegativeInfinity(correction)) return false;

            float move = side * (correction + SpikeBodyMargin);
            spikeSideImpactPoint = transform.TransformPoint(contact);
            target.SetBossSideRamPose(target.CollisionTrackPosition.x + move,
                target.transform.position + transform.right * move, target.transform.rotation);
            return true;
        }

        private void BeginSpikeSideImpact(float side)
        {
            spikeSideRamConsumed = true;
            spikeImpactSide = side;
            spikeImpactVanOffset = sideRamOffset;
            spikeResumeRetreatTime = SpikePhase == SpikeAttackPhase.Retreating ? spikePhaseTime : 0f;
            sideRamStartedAt = -1f;
            SetSpikePhase(SpikeAttackPhase.SideRamImpact);
            RamByPlayer(-side * transform.right);
            if (!hasBeenRammed)
                currentSpeed = Mathf.Max(currentSpeed, target.CurrentSpeed);
            if (!target.IsDestroyed)
                target.BeginBossSideRamRecovery(this, side, SpikeImpactRebound, Tuning.playerSideRamBounceDistance * SpikeSideReboundScale);
            if (Application.isPlaying)
            {
                VoxelSpikeImpact.PlaySideRamSparks(transform, spikeSideImpactPoint, side, SpikeImpactSparkDuration, path);
                Camera.main?.GetComponent<VoxelCameraFollow>()?.ShakeFromBossSideRam();
            }
        }

        private void UpdateSpikeSideRebound()
        {
            float progress = Mathf.Clamp01(spikePhaseTime / SpikeImpactRebound);
            float rebound = VoxelEasing.Evaluate(VoxelEasingType.EaseOutCubic, progress);
            sideRamOffset = spikeImpactVanOffset - spikeImpactSide * Tuning.sideRamEnemyLaneShiftDistance * SpikeSideReboundScale * rebound;
            float retreatTime = spikeResumeRetreatTime + spikePhaseTime;
            UpdateSpikeRetreatRig(retreatTime);
            if (progress < 1f) return;

            sideRamOffsetStart = sideRamOffset;
            sideRamDuration = Tuning.sideRamEnemyLaneShiftDuration;
            sideRamEasing = Tuning.sideRamEnemyLaneShiftEasing;
            sideRamStartedAt = Time.time;
            SetSpikePhase(SpikeAttackPhase.Retreating);
            spikePhaseTime = retreatTime;
        }

        internal bool IsAlongsideForSideRamRecovery() => isActiveAndEnabled && !hasBeenRammed && CurrentHealth > 0f &&
            SpikeAttackActive && spikeSideRamConsumed && HasSpikeBodyLongitudinalOverlap();

        internal bool IsApproachingSpikeSideCounter()
        {
            if (!spikeAttackDodged || spikeSideRamConsumed || !CanCounterSpikeAttack || !HasSpikeBodyLongitudinalOverlap())
                return false;

            float playerLane = target.CollisionTrackPosition.x;
            return (playerLane - LaneOffset) * (target.TargetLaneOffset - playerLane) < -.001f;
        }

        private bool HasSpikeBodyLongitudinalOverlap() => target != null &&
            Mathf.Abs(TrackDistance - target.CollisionTrackPosition.y) <= SpikeBodyHalfSize.y + .5f;

        private void KeepSpikeBodiesSeparated(Vector2 previous)
        {
            Vector2 relative = target.CollisionTrackPosition - new Vector2(LaneOffset, TrackDistance);
            Vector2 halfSize = GetSpikeBodyHalfSize(previous, relative);
            if (!VoxelVehicleCollision.Sweep(previous, relative, halfSize, out var contact))
                return;

            bool sideContact = IsSpikeBodySide(contact, halfSize);
            float side = Mathf.Sign(contact.x);
            float end = Mathf.Sign(contact.y);
            bool crossedBody = sideContact
                ? side * previous.x >= halfSize.x && side * relative.x <= -halfSize.x
                : end * previous.y >= halfSize.y && end * relative.y <= -halfSize.y;
            if (!crossedBody && (Mathf.Abs(relative.x) >= halfSize.x || Mathf.Abs(relative.y) >= halfSize.y))
                return;

            float clearLaneOffset = LaneOffset + side * (halfSize.x + SpikeBodyMargin);
            float outerLaneOffset = (bossLaneCount - 1) * .5f * bossLaneWidth;
            bool dodgeCorner = spikeAttackDodged && Mathf.Abs(contact.x) >= SpikeDodgeOffset;
            if ((sideContact || dodgeCorner) && Mathf.Abs(contact.x) > .001f && Mathf.Abs(clearLaneOffset) <= outerLaneOffset)
                target.ResolveBossSideContact(clearLaneOffset);
            else
                PushSpikeBossAhead(halfSize.y + SpikeBodyMargin);
        }

        private void PushSpikeBossAhead(float clearance)
        {
            float minimumDistance = target.CollisionTrackPosition.y + clearance;
            if(TrackDistance >= minimumDistance) return;
            trackDistance = minimumDistance - rearRamForwardOffset;
            currentSpeed = Mathf.Max(currentSpeed, target.CurrentSpeed);
            ApplyTrackPose();
        }

        private void LatchSpikeDodge()
        {
            if (Mathf.Abs(target.CollisionTrackPosition.x - LaneOffset) >= SpikeDodgeOffset)
                spikeAttackDodged = true;
        }

        private void UpdateSpikeAttack(float dt)
        {
            if (dt <= 0) return;
            if (target.IsDestroyed || VoxelMissionProgress.Active?.IsComplete == true || VoxelMissionProgress.Active?.IsFailed == true)
            {
                SpikePhase = SpikeAttackPhase.Normal;
                spikeAttackDodged = false;
                spikeDodgeCheckPending = false;
                if (spikeRig != null) spikeRig.SetPose(0, 0);
                spikeCheckTime = spikeAttackTuning != null ? spikeAttackTuning.checkInterval : float.PositiveInfinity;
                return;
            }
            if (spikeRig == null) spikeRig = GetComponentInChildren<VoxelBossSpikeRig>();
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
            // Keep the final dodge check available for LateUpdate even if this
            // step changes Braking to Holding before the player finishes moving.
            spikeDodgeCheckPending = SpikePhase == SpikeAttackPhase.Warning || SpikePhase == SpikeAttackPhase.Braking;
            if (spikeDodgeCheckPending) LatchSpikeDodge();
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
                case SpikeAttackPhase.SideRamImpact:
                    UpdateSpikeSideRebound();
                    break;
                case SpikeAttackPhase.Retreating:
                    if (UpdateSpikeRetreatRig(spikePhaseTime) && gap >= SpikeRetreatGap - 1)
                    {
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

        private bool UpdateSpikeRetreatRig(float seconds)
        {
            float retract = Mathf.Clamp01(seconds / Mathf.Max(.1f, spikeAttackTuning.retractDuration));
            spikeRig.SetPose(1f - retract, 1f - retract);
            return retract >= 1f;
        }
        private float SpikeBodyClearance => Mathf.Max(Tuning.collisionHalfLength, 2.65f * bossVisualScale) + spikePlayerFront + .2f;
        private float SpikeHitDistance => Mathf.Max(Tuning.collisionHalfLength + 1.7f * bossVisualScale, SpikeBodyClearance + .7f * bossVisualScale);
        private float SpikeRetreatGap => Mathf.Clamp(spikeAttackTuning.retreatDistance, 20, bossSettings.WarningDistance - 5);
        private void SetSpikePhase(SpikeAttackPhase phase) { SpikePhase = phase; spikePhaseTime = 0; }
        private bool CanCounterSpikeAttack => SpikePhase == SpikeAttackPhase.Holding || SpikePhase == SpikeAttackPhase.Retreating;

        private void CacheSpikePlayerBounds()
        {
            spikePlayerFront = spikePlayerRear = 2.5f;
            spikePlayerHalfWidth = 1.3f;
            spikePlayerHeight = 0f;
            spikePlayerHeightWidths = new float[24];
            Matrix4x4 worldToPlayer = target.transform.worldToLocalMatrix;
            foreach (var renderer in target.GetComponentsInChildren<MeshRenderer>())
            {
                Bounds bounds = TransformSpikeBounds(renderer.localBounds, worldToPlayer * renderer.localToWorldMatrix);
                float width = Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x));
                spikePlayerFront = Mathf.Max(spikePlayerFront, bounds.max.z);
                spikePlayerRear = Mathf.Max(spikePlayerRear, -bounds.min.z);
                spikePlayerHalfWidth = Mathf.Max(spikePlayerHalfWidth, width);
                spikePlayerHeight = Mathf.Max(spikePlayerHeight, bounds.max.y);
                int first = SpikeHeightIndex(bounds.min.y);
                int last = SpikeHeightIndex(bounds.max.y);
                for (int slice = first; slice <= last; slice++)
                    spikePlayerHeightWidths[slice] = Mathf.Max(spikePlayerHeightWidths[slice], width);
            }
        }

        private void CacheSpikeBossSideProfile(GameObject model)
        {
            spikeSideContactWidths = null;
            if (model == null) return;

            // Use the authored, closed body before the spawn animation scales it to
            // zero. Mirrors and other pieces above the player cannot stop a side ram.
            spikeBossProfileHalfLength = Mathf.Max(Tuning.collisionHalfLength, 2.65f * bossVisualScale);
            spikeSideContactWidths = new float[48];
            Transform root = model.transform;
            Matrix4x4 worldToRoad = Matrix4x4.TRS(root.localPosition, root.localRotation, root.localScale * bossVisualScale)
                * root.worldToLocalMatrix;
            foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
            {
                if (!renderer.enabled) continue;
                Bounds bounds = TransformSpikeBounds(renderer.localBounds, worldToRoad * renderer.localToWorldMatrix);
                const float rollClearance = .1f;
                if (bounds.max.y < -rollClearance || bounds.min.y > spikePlayerHeight + rollClearance) continue;
                float playerWidth = 0f;
                for (int slice = SpikeHeightIndex(bounds.min.y - rollClearance);
                    slice <= SpikeHeightIndex(bounds.max.y + rollClearance); slice++)
                    playerWidth = Mathf.Max(playerWidth, spikePlayerHeightWidths[slice]);
                if (playerWidth <= 0f) continue;
                int first = SpikeBossProfileIndex(bounds.min.z);
                int last = SpikeBossProfileIndex(bounds.max.z);
                float width = Mathf.Max(Mathf.Abs(bounds.min.x), Mathf.Abs(bounds.max.x)) + playerWidth;
                for (int slice = first; slice <= last; slice++)
                    spikeSideContactWidths[slice] = Mathf.Max(spikeSideContactWidths[slice], width);
            }
        }

        private static Bounds TransformSpikeBounds(Bounds source, Matrix4x4 matrix)
        {
            Vector3 size = source.size;
            return new Bounds(matrix.MultiplyPoint3x4(source.center), new Vector3(
                ProjectSpikeExtent(matrix.GetRow(0), size),
                ProjectSpikeExtent(matrix.GetRow(1), size),
                ProjectSpikeExtent(matrix.GetRow(2), size)));
        }

        private int SpikeHeightIndex(float height) =>
            Mathf.Clamp(Mathf.FloorToInt(height / SpikeHeightSlice), 0, spikePlayerHeightWidths.Length - 1);

        private int SpikeBossProfileIndex(float distance) => Mathf.Clamp(
            Mathf.FloorToInt((distance / spikeBossProfileHalfLength + 1f) * .5f * spikeSideContactWidths.Length),
            0, spikeSideContactWidths.Length - 1);

        private void BeginSpikeAttack()
        {
            spikeRamApplied = false;
            spikeAttackDodged = false;
            spikeSideRamConsumed = false;
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
            if (SpikePhase == SpikeAttackPhase.Retreating || SpikePhase == SpikeAttackPhase.SideRamImpact)
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
            PushSpikeBossAhead(SpikeBodyClearance);
            nextCollisionTime = Time.time + Mathf.Max(.1f, trafficTuning.collisionCooldown);
            VoxelSpikeImpact.Play(hitPoint, target.transform.forward, target.CurrentSpeed);
            int previousDamage = target.damageVoxelsPerHit;
            try
            {
                int min = Mathf.Max(0, spikeAttackTuning.damageMin);
                target.damageVoxelsPerHit = Random.Range(min, Mathf.Max(min, spikeAttackTuning.damageMax) + 1);
                if (target.damageVoxelsPerHit > 0)
                    target.ApplyCollisionDamage(hitPoint, -target.transform.forward, "Boss spike ram", transform.position - target.transform.position);
            }
            finally { target.damageVoxelsPerHit = previousDamage; }
        }
    }
}
