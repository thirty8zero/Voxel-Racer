using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    public enum PsychoBugPhase { PositioningForShot, Exposed, Hunting, Aligning, Warning, Slamming, Recovering, Staggered }

    public sealed partial class VoxelEnemyCar
    {
        public bool IsPsychoBug => !IsBoss && Tuning != null && Tuning.psychoBug != null;
        public PsychoBugPhase PsychoPhase { get; private set; }
        public float DriveSpeed => currentSpeed;
        public bool IsDrivingEnemy => Tuning != null && !hasBeenRammed && CurrentHealth > 0;
        private VoxelPsychoBugTuning Psycho => Tuning.psychoBug;
        private float psychoClock, psychoPhaseTime, psychoNextDecision, psychoNextScan, psychoNextAttack;
        private float psychoExposureDeadline, psychoExposureDuration, psychoAlignedTime;
        private float psychoWarningLane, psychoAttackLane, psychoAttackSpeed, psychoRecoveryLane;
        private float psychoEvasionAt = float.PositiveInfinity, psychoNextEvasion;
        private bool psychoRamApplied, psychoFeint;
        private readonly List<MonoBehaviour> psychoTraffic = new();

        private void ConfigurePsychoBug()
        {
            if (!IsPsychoBug) return;
            psychoClock = psychoNextDecision = psychoNextScan = 0;
            psychoNextAttack = Mathf.Max(0, Psycho.minimumAttackCooldown);
            psychoEvasionAt = float.PositiveInfinity; psychoNextEvasion = 0;
            ResetPsychoExposureDeadline();
            // Reach the player for an attack first; exposure windows are periodic, not a spawn prerequisite.
            SetPsychoPhase(PsychoBugPhase.Hunting);
        }

        private void ResetPsychoExposureDeadline() => psychoExposureDeadline = psychoClock + Mathf.Max(0, Psycho.maximumTimeBetweenShootingWindows);
        private void SetPsychoPhase(PsychoBugPhase phase)
        {
            PsychoPhase = phase; psychoPhaseTime = psychoAlignedTime = 0;
        }
        private void FinishPsychoAttack(bool staggered = false)
        {
            psychoNextAttack = psychoClock + Random.Range(Mathf.Max(0, Psycho.minimumAttackCooldown),
                Mathf.Max(Psycho.minimumAttackCooldown, Psycho.maximumAttackCooldown));
            // Recover to the lane occupied before the attack, never to the player's live lane.
            targetLaneOffset = psychoRecoveryLane = PsychoAttacking ? psychoRecoveryLane : NearestPsychoLane(laneOffset);
            SetPsychoPhase(staggered ? PsychoBugPhase.Staggered : PsychoBugPhase.Recovering);
        }

        private float PsychoLaneWidth => path != null ? path.roadWidth / Mathf.Max(1, path.laneCount) : 3f;
        private int PsychoLaneCount => path != null ? Mathf.Max(1, path.laneCount) : 4;
        private float PsychoLane(int index) => (index - (PsychoLaneCount - 1) * .5f) * PsychoLaneWidth;
        private float NearestPsychoLane(float offset) => PsychoLane(Mathf.Clamp(
            Mathf.RoundToInt(offset / PsychoLaneWidth + (PsychoLaneCount - 1) * .5f), 0, PsychoLaneCount - 1));
        private bool PsychoSettled => Mathf.Abs(laneOffset - targetLaneOffset) < .04f && Mathf.Abs(sideRamOffset) < .04f;
        private bool PsychoAttacking => PsychoPhase == PsychoBugPhase.Warning || PsychoPhase == PsychoBugPhase.Slamming;
        private Vector2 PsychoCollisionSize => new(Tuning.collisionHalfWidth + Psycho.playerCollisionHalfWidth,
            Tuning.collisionHalfLength + Psycho.playerCollisionHalfLength);

        private void AdvancePsychoBug(float seconds)
        {
            if (seconds <= 0 || !IsPsychoBug) return;
            psychoClock += seconds; psychoPhaseTime += seconds;
            if (psychoClock >= psychoNextScan)
            {
                Transform owner = spawner != null ? spawner.transform : transform.parent != null ? transform.parent : transform;
                owner.GetComponentsInChildren(false, psychoTraffic);
                psychoNextScan = psychoClock + Mathf.Max(.01f, Psycho.trafficScanInterval);
            }
            Vector2 player = target.CollisionTrackPosition;
            bool trafficDecisionDue = psychoClock >= psychoNextDecision;
            // Alignment must never park beside a stopped player, or lock a zero launch speed.
            // A committed attack retains its launch speed when the player brakes, unless the
            // Bug itself is slowed below the configured threshold (for example by traffic).
            if (Psycho.minimumRamSpeed > 0 &&
                ((PsychoPhase == PsychoBugPhase.Aligning && target.CurrentSpeed < Psycho.minimumRamSpeed) ||
                 (PsychoAttacking && currentSpeed < Psycho.minimumRamSpeed)))
                FinishPsychoAttack();
            if (Psycho.ramOnPassingAlignment && Psycho.sideRamEnabled &&
                (Psycho.minimumRamSpeed <= 0 || target.CurrentSpeed >= Psycho.minimumRamSpeed) &&
                (PsychoPhase == PsychoBugPhase.Hunting || PsychoPhase == PsychoBugPhase.PositioningForShot) &&
                psychoClock >= psychoNextAttack && PsychoSettled && IsPsychoSideAligned(player) && CanReservePsychoAttack())
                SetPsychoPhase(PsychoBugPhase.Aligning);
            float desiredGap;
            switch (PsychoPhase)
            {
                case PsychoBugPhase.PositioningForShot:
                    desiredGap = Mathf.Max(PsychoCollisionSize.y + Psycho.trafficClearance, Psycho.shootingDistanceAhead);
                    if (psychoClock >= psychoNextDecision && PsychoSettled)
                        ChoosePsychoLane(NearestPsychoLane(player.x), false);
                    if (Mathf.Abs(player.x - laneOffset) < .15f && PsychoSettled &&
                        Mathf.Abs(TrackDistance - player.y - desiredGap) < 2f)
                    {
                        psychoExposureDuration = Random.Range(Mathf.Max(0, Psycho.minimumShootingDuration),
                            Mathf.Max(Psycho.minimumShootingDuration, Psycho.maximumShootingDuration));
                        SetPsychoPhase(PsychoBugPhase.Exposed);
                    }
                    else if (psychoPhaseTime >= Mathf.Max(.1f, Psycho.positioningTimeout))
                    {
                        // A blocked shooting lane must not suppress attacks indefinitely.
                        ResetPsychoExposureDeadline(); SetPsychoPhase(PsychoBugPhase.Hunting);
                    }
                    break;
                case PsychoBugPhase.Exposed:
                    desiredGap = Mathf.Max(PsychoCollisionSize.y + Psycho.trafficClearance, Psycho.shootingDistanceAhead);
                    // No reactive dodges or attacks in the promised shooting window.
                    if (psychoPhaseTime >= psychoExposureDuration)
                    {
                        ResetPsychoExposureDeadline(); SetPsychoPhase(PsychoBugPhase.Hunting);
                    }
                    break;
                case PsychoBugPhase.Hunting:
                    desiredGap = 0;
                    if (Psycho.shootingWindows && psychoClock >= psychoExposureDeadline)
                        SetPsychoPhase(PsychoBugPhase.PositioningForShot);
                    else if (psychoClock >= psychoNextDecision && PsychoSettled)
                    {
                        psychoNextDecision = psychoClock + Mathf.Max(.05f, Psycho.decisionInterval);
                        if (Psycho.sideRamEnabled && (Psycho.minimumRamSpeed <= 0 || target.CurrentSpeed >= Psycho.minimumRamSpeed) &&
                            psychoClock >= psychoNextAttack && Random.value < Psycho.sideRamChance &&
                            TryChoosePsychoSide(player.x)) SetPsychoPhase(PsychoBugPhase.Aligning);
                        else if (Psycho.weaveThroughTraffic && Random.value < Psycho.weaveChance)
                            ChoosePsychoLane(laneOffset + (Random.value < .5f ? -1 : 1) * PsychoLaneWidth, false);
                    }
                    break;
                case PsychoBugPhase.Aligning:
                    // Clear the player's lateral envelope before braking back to level. Otherwise
                    // a merge from the shooting lane can stop halfway when its predicted path closes.
                    desiredGap = PsychoSettled ? 0 : Mathf.Max(PsychoCollisionSize.y + Psycho.trafficClearance, Psycho.shootingDistanceAhead);
                    if (PsychoSettled && IsPsychoSideAligned(player) &&
                        (Psycho.minimumRamSpeed <= 0 || currentSpeed >= Psycho.minimumRamSpeed) &&
                        Mathf.Abs(currentSpeed - target.CurrentSpeed) <= Mathf.Max(.1f, Psycho.ramSpeedTolerance))
                        psychoAlignedTime += seconds;
                    else psychoAlignedTime = 0;
                    if (psychoAlignedTime >= Mathf.Max(0, Psycho.alignmentHoldDuration) && CanReservePsychoAttack())
                        BeginPsychoRam();
                    else if (psychoPhaseTime >= Mathf.Max(.1f, Psycho.positioningTimeout)) FinishPsychoAttack();
                    break;
                case PsychoBugPhase.Warning:
                    desiredGap = 0;
                    // Fixed lane target and launch speed make a boost/lane escape meaningful.
                    float away = Mathf.Sign(psychoWarningLane - psychoAttackLane);
                    float boundary = Mathf.Max(0, (PsychoLaneCount * PsychoLaneWidth) * .5f - Tuning.collisionHalfWidth);
                    float warningEnd = Mathf.Clamp(psychoWarningLane + away * Mathf.Max(0, Psycho.pullAwayDistance), -boundary, boundary);
                    float progress = Mathf.Clamp01(psychoPhaseTime / Mathf.Max(.05f, Psycho.warningDuration));
                    laneOffset = Mathf.Lerp(psychoWarningLane, warningEnd, Mathf.SmoothStep(0, 1, progress));
                    if (!PsychoRouteSafe(laneOffset, false) || Mathf.Abs(TrackDistance - player.y) > PsychoCollisionSize.y + Psycho.alignmentTolerance)
                        FinishPsychoAttack();
                    else if (progress >= 1)
                    {
                        if (psychoFeint) FinishPsychoAttack();
                        else SetPsychoPhase(PsychoBugPhase.Slamming);
                    }
                    break;
                case PsychoBugPhase.Slamming:
                    desiredGap = 0;
                    if (!PsychoRouteSafe(psychoAttackLane, false)) FinishPsychoAttack();
                    else
                    {
                        laneOffset = Mathf.MoveTowards(laneOffset, psychoAttackLane, Mathf.Max(.1f, Psycho.slamSpeed) * seconds);
                        if (psychoPhaseTime >= Mathf.Max(.05f, Psycho.slamTimeout)) FinishPsychoAttack();
                    }
                    break;
                default:
                    desiredGap = Mathf.Max(PsychoCollisionSize.y + Psycho.trafficClearance, Psycho.recoveryDistanceAhead);
                    float duration = PsychoPhase == PsychoBugPhase.Staggered ? Psycho.staggerDuration : Psycho.recoveryDuration;
                    if (psychoPhaseTime >= Mathf.Max(0, duration))
                        SetPsychoPhase(PsychoBugPhase.Hunting);
                    break;
            }

            bool positioning = PsychoPhase == PsychoBugPhase.Aligning || PsychoPhase == PsychoBugPhase.PositioningForShot ||
                PsychoPhase == PsychoBugPhase.Exposed;
            float cruiseSpeed = target.EffectiveTopSpeed * Psycho.cruiseSpeedMultiplier;
            if (PsychoPhase == PsychoBugPhase.Recovering || PsychoPhase == PsychoBugPhase.Staggered)
                cruiseSpeed += Mathf.Clamp((desiredGap - (TrackDistance - player.y)) * Psycho.positioningResponse, 0, Psycho.positioningSpeed);
            float desiredSpeed = PsychoAttacking ? psychoAttackSpeed : positioning ? GetPsychoPositioningSpeed(desiredGap) :
                Mathf.Clamp(cruiseSpeed, 0, target.EffectiveTopSpeed * Psycho.maximumSpeedMultiplier);
            // Shooting windows hold a steady lane, but do not park just because the player has.
            if (!PsychoAttacking && !(positioning && Psycho.allowReverse))
                desiredSpeed = Mathf.Max(desiredSpeed, Mathf.Min(Mathf.Max(0, Psycho.minimumDrivingSpeed),
                    target.EffectiveTopSpeed * Psycho.maximumSpeedMultiplier));
            desiredSpeed = LimitPsychoTrafficSpeed(desiredSpeed);
            float rate = desiredSpeed > currentSpeed ? Psycho.acceleration : Psycho.braking;
            currentSpeed = Mathf.MoveTowards(currentSpeed, desiredSpeed, Mathf.Max(0, rate) * seconds);
            currentSpeed = LimitPsychoStepSpeed(currentSpeed, seconds);
            if (PsychoAttacking && Psycho.minimumRamSpeed > 0 && currentSpeed < Psycho.minimumRamSpeed)
                FinishPsychoAttack();
            trackDistance += currentSpeed * seconds;

            if (!PsychoAttacking)
            {
                if (PsychoPhase != PsychoBugPhase.Exposed && psychoClock >= psychoEvasionAt && PsychoSettled)
                {
                    ChoosePsychoLane(laneOffset + (Random.value < .5f ? -1 : 1) * PsychoLaneWidth, false);
                    psychoEvasionAt = float.PositiveInfinity;
                }
                if (PsychoRouteSafe(targetLaneOffset, true))
                    laneOffset = Mathf.MoveTowards(laneOffset, targetLaneOffset, Mathf.Max(.1f, Psycho.laneChangeSpeed) * seconds);
                // Hunting can consume the same decision tick even when its random weave
                // roll does nothing. A blocked route still gets its safety decision.
                else if (PsychoPhase != PsychoBugPhase.Exposed && PsychoSettled && Psycho.weaveThroughTraffic && trafficDecisionDue)
                    ChoosePsychoLane(targetLaneOffset, true);
            }
        }

        private bool IsPsychoSideAligned(Vector2 player)
        {
            float sideDistance = Mathf.Abs(laneOffset - player.x);
            return Mathf.Abs(TrackDistance - player.y) <= Mathf.Max(.1f, Psycho.alignmentTolerance) &&
                sideDistance >= PsychoCollisionSize.x && sideDistance <= PsychoLaneWidth * 1.3f;
        }

        private float GetPsychoPositioningSpeed(float desiredGap)
        {
            float difference = desiredGap - (TrackDistance - target.CollisionTrackPosition.y);
            float adjustment = Mathf.Clamp(difference * Mathf.Max(.01f, Psycho.positioningResponse) -
                (currentSpeed - target.CurrentSpeed) * Mathf.Max(0, Psycho.positioningDamping),
                -Mathf.Max(0, Psycho.positioningSpeed), Mathf.Max(0, Psycho.positioningSpeed));
            float minimum = Psycho.allowReverse ? -Mathf.Max(0, Psycho.maximumReverseSpeed) : 0;
            // Chase uses normal engine-adjusted top speed as the ceiling, never the boost bonus.
            float maximum = Mathf.Max(0, target.EffectiveTopSpeed) * Mathf.Max(0, Psycho.maximumSpeedMultiplier);
            return Mathf.Clamp(target.CurrentSpeed + adjustment, minimum, maximum);
        }

        private void BeginPsychoRam()
        {
            psychoWarningLane = laneOffset; psychoAttackLane = target.CollisionTrackPosition.x;
            psychoRecoveryLane = targetLaneOffset = NearestPsychoLane(laneOffset);
            psychoAttackSpeed = currentSpeed; psychoRamApplied = false; psychoFeint = Random.value < Psycho.feintChance;
            psychoEvasionAt = float.PositiveInfinity;
            SetPsychoPhase(PsychoBugPhase.Warning);
        }
        private bool CanReservePsychoAttack()
        {
            int attackers = 0;
            foreach (var enemy in ActiveProjectileTargets)
                if (enemy != this && enemy != null && enemy.target == target && enemy.IsPsychoBug && enemy.IsDrivingEnemy && enemy.PsychoAttacking)
                    attackers++;
            return attackers < Mathf.Max(1, Psycho.maximumSimultaneousRams);
        }
        private bool TryChoosePsychoSide(float playerLane)
        {
            float left = NearestPsychoLane(playerLane) - PsychoLaneWidth, right = NearestPsychoLane(playerLane) + PsychoLaneWidth;
            float first = Mathf.Abs(laneOffset - left) < Mathf.Abs(laneOffset - right) ? left : right;
            float second = first == left ? right : left;
            if (ValidPsychoLane(first) && PsychoRouteSafe(first, true)) { targetLaneOffset = first; return true; }
            if (ValidPsychoLane(second) && PsychoRouteSafe(second, true)) { targetLaneOffset = second; return true; }
            return false;
        }
        private bool ValidPsychoLane(float value) => value >= PsychoLane(0) - .01f && value <= PsychoLane(PsychoLaneCount - 1) + .01f;
        private void ChoosePsychoLane(float goal, bool excludeCurrent)
        {
            psychoNextDecision = psychoClock + Mathf.Max(.05f, Psycho.decisionInterval);
            float best = float.PositiveInfinity;
            for (int lane = 0; lane < PsychoLaneCount; lane++)
            {
                float candidate = PsychoLane(lane);
                if (Mathf.Abs(candidate - laneOffset) > PsychoLaneWidth * 1.25f ||
                    (excludeCurrent && Mathf.Abs(candidate - laneOffset) < .1f) || !PsychoRouteSafe(candidate, true)) continue;
                float score = Mathf.Abs(candidate - goal);
                if (score < best) { best = score; targetLaneOffset = candidate; }
            }
        }

        private void OnPsychoProjectileDamage()
        {
            if (!IsPsychoBug || !Psycho.damageEvasion || PsychoAttacking || PsychoPhase == PsychoBugPhase.Exposed ||
                psychoClock < psychoNextEvasion || Random.value >= Psycho.damageEvasionChance) return;
            psychoEvasionAt = psychoClock + Mathf.Max(0, Psycho.damageReactionDelay);
            psychoNextEvasion = psychoEvasionAt + Mathf.Max(0, Psycho.damageEvasionCooldown);
        }
        private void OnPsychoPlayerRam()
        {
            if (!IsPsychoBug || !Psycho.playerRamInterrupts) return;
            psychoEvasionAt = float.PositiveInfinity;
            FinishPsychoAttack(true);
        }
        private void ApplyPsychoRam(Vector2 contact)
        {
            if (psychoRamApplied) return;
            psychoRamApplied = true;
            Vector3 enemyDirection = VoxelVehicleCollision.ImpactDirection(contact, target.transform);
            int original = target.damageVoxelsPerHit;
            try
            {
                int minimum = Mathf.Max(0, Psycho.sideRamDamageMin);
                target.damageVoxelsPerHit = Random.Range(minimum, Mathf.Max(minimum, Psycho.sideRamDamageMax) + 1);
                if (target.damageVoxelsPerHit > 0)
                    target.ApplyCollisionDamage(target.GetDamageSurfacePoint(transform.position), -enemyDirection,
                        "Psycho Bug side ram", enemyDirection);
                target.ApplyRamResponse(false, enemyDirection, Tuning);
            }
            finally { target.damageVoxelsPerHit = original; }
            nextCollisionTime = Time.time + Mathf.Max(.1f, trafficTuning != null ? trafficTuning.collisionCooldown : .5f);
            FinishPsychoAttack();
            // Restore side clearance immediately rather than parking inside the player after impact.
            laneOffset = psychoRecoveryLane;
        }

        private bool PsychoRouteSafe(float destination, bool includePlayer)
        {
            float horizon = Mathf.Max(.1f, Psycho.trafficPredictionSeconds,
                Mathf.Abs(destination - laneOffset) / Mathf.Max(.1f, Psycho.laneChangeSpeed));
            if (includePlayer && !PsychoMotionClear(destination, target.CollisionTrackPosition.x, target.CollisionTrackPosition.y,
                    Psycho.playerCollisionHalfWidth, Psycho.playerCollisionHalfLength, target.CurrentSpeed, horizon, 0)) return false;
            foreach (var occupant in psychoTraffic)
            {
                if (!TryPsychoOccupant(occupant, out float offset, out float distance, out float width, out float length, out float speed)) continue;
                if (!PsychoMotionClear(destination, offset, distance, width, length, speed, horizon, Psycho.trafficClearance)) return false;
                if (occupant is VoxelEnemyCar enemy && Mathf.Abs(enemy.targetLaneOffset - offset) > .1f &&
                    !PsychoMotionClear(destination, enemy.targetLaneOffset, distance, width, length, speed, horizon, Psycho.trafficClearance)) return false;
            }
            return true;
        }
        private bool PsychoMotionClear(float destination, float otherLane, float distance, float width, float length, float speed, float horizon, float clearance)
        {
            if (Mathf.Abs(distance - TrackDistance) > Psycho.trafficLookAhead + Mathf.Abs(speed - currentSpeed) * horizon + length) return true;
            Vector2 previous = new(laneOffset - otherLane, TrackDistance - distance);
            Vector2 size = new(Tuning.collisionHalfWidth + width, Tuning.collisionHalfLength + length + Mathf.Max(0, clearance));
            float lateralSpeed = PsychoPhase == PsychoBugPhase.Slamming ? Psycho.slamSpeed : Psycho.laneChangeSpeed;
            float mergeSeconds = Mathf.Min(horizon, Mathf.Abs(destination - laneOffset) / Mathf.Max(.1f, lateralSpeed));
            // Predict the real merge duration, then the straight drive in the destination lane.
            // Stretching the diagonal across the entire horizon falsely rejected quick escapes
            // around barrels and could leave the Bug braking in an otherwise clear lane.
            Vector2 merge = new(Mathf.MoveTowards(laneOffset, destination, Mathf.Max(.1f, lateralSpeed) * mergeSeconds) - otherLane,
                previous.y + (currentSpeed - speed) * mergeSeconds);
            if (mergeSeconds > 0 && VoxelVehicleCollision.Sweep(previous, merge, size, out _)) return false;
            Vector2 next = new(merge.x, previous.y + (currentSpeed - speed) * horizon);
            return !VoxelVehicleCollision.Sweep(merge, next, size, out _);
        }
        private float LimitPsychoTrafficSpeed(float desired)
        {
            foreach (var occupant in psychoTraffic)
            {
                if (!TryPsychoOccupant(occupant, out float offset, out float distance, out float width, out float length, out float speed)) continue;
                bool overlaps = Mathf.Abs(offset - LaneOffset) < Tuning.collisionHalfWidth + width;
                if (occupant is VoxelEnemyCar enemy) overlaps |= Mathf.Abs(enemy.targetLaneOffset - LaneOffset) < Tuning.collisionHalfWidth + width;
                if (!overlaps) continue;
                float gap = distance - TrackDistance;
                float room = Mathf.Max(0, Mathf.Abs(gap) - Tuning.collisionHalfLength - length - Psycho.trafficClearance);
                // Reserve reaction distance between snapshots rather than brake at the last instant.
                float reactionRoom = Mathf.Max(0, room - Mathf.Abs(currentSpeed - speed) * Mathf.Max(.01f, Psycho.trafficScanInterval));
                float closing = Mathf.Sqrt(2 * Mathf.Max(.01f, Psycho.braking) * reactionRoom);
                if (gap >= 0) desired = Mathf.Min(desired, speed + closing);
                else if (desired < speed) desired = Mathf.Max(desired, speed - closing);
            }
            // Stopping for an oncoming car is preferable to reversing into unknown traffic.
            return Mathf.Clamp(desired, Psycho.allowReverse ? -Psycho.maximumReverseSpeed : 0,
                Mathf.Max(0, target.EffectiveTopSpeed * Psycho.maximumSpeedMultiplier));
        }
        private float LimitPsychoStepSpeed(float speed, float seconds)
        {
            // Emergency movement cap for a late spawn or long frame: normal braking cannot
            // consume more front/rear clearance than exists during this step.
            foreach (var occupant in psychoTraffic)
            {
                if (!TryPsychoOccupant(occupant, out float offset, out float distance, out float width, out float length, out float otherSpeed) ||
                    Mathf.Abs(offset - LaneOffset) >= Tuning.collisionHalfWidth + width) continue;
                float gap = distance - TrackDistance;
                float room = Mathf.Max(0, Mathf.Abs(gap) - Tuning.collisionHalfLength - length - Mathf.Max(0, Psycho.trafficClearance));
                if (gap >= 0 && speed > otherSpeed) speed = Mathf.Min(speed, Mathf.Max(0, otherSpeed + room / seconds));
                else if (gap < 0 && speed < otherSpeed) speed = Mathf.Max(speed, Mathf.Min(0, otherSpeed - room / seconds));
            }
            return speed;
        }
        private bool TryPsychoOccupant(MonoBehaviour occupant, out float offset, out float distance, out float width, out float length, out float speed)
        {
            offset = distance = width = length = speed = 0;
            if (occupant == null || occupant == this || !occupant.gameObject.activeInHierarchy) return false;
            switch (occupant)
            {
                case VoxelObstacleCar civilian when civilian.IsDrivingTraffic:
                    offset = civilian.LaneOffset; distance = civilian.TrackDistance; length = civilian.TrafficHalfLength;
                    width = civilian.EnemyTuning != null ? civilian.EnemyTuning.collisionHalfWidth : 1.35f;
                    speed = civilian.TravelSpeed * (civilian.TravelsWithPlayer ? 1 : -1); return true;
                case VoxelEnemyCar enemy when enemy.IsDrivingEnemy:
                    offset = enemy.LaneOffset; distance = enemy.TrackDistance; width = enemy.Tuning.collisionHalfWidth;
                    length = enemy.Tuning.collisionHalfLength; speed = enemy.DriveSpeed; return true;
                case VoxelObstacle crate when crate.IsRoadHazard: offset = crate.LaneOffset; distance = crate.TrackDistance; width = 1; length = 1; return true;
                case VoxelFuelDrumObstacle drums when drums.IsRoadHazard: offset = drums.LaneOffset; distance = drums.TrackDistance; width = 2; length = 2; return true;
                case VoxelPotholeObstacle pothole: offset = pothole.LaneOffset; distance = pothole.TrackDistance; width = PsychoLaneWidth * .42f; length = 1.35f; return true;
                case VoxelOilSlickObstacle oil when !oil.HasTriggered: offset = oil.LaneOffset; distance = oil.TrackDistance; width = PsychoLaneWidth * .4f; length = 1.5f; return true;
                case VoxelRoadMine mine: offset = mine.LaneOffset; distance = mine.TrackDistance; width = .6f; length = .6f; return true;
                default: return false;
            }
        }
    }
}
