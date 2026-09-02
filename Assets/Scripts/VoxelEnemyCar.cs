using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>A same-direction traffic-car variant that can be damaged by player projectiles.</summary>
    public sealed class VoxelEnemyCar : MonoBehaviour
    {
        private enum DebrisStyle { Weapon, Ram, Explosion }
        public VoxelEnemyVehicleTuning Tuning { get; private set; }
        public float CurrentHealth { get; private set; }
        public float HealthPercent => Tuning == null ? 0f : Mathf.Clamp01(CurrentHealth / Tuning.vehicleHealth);
        public float LaneOffset => laneOffset + sideRamOffset;
        public float TrackDistance => trackDistance + rearRamForwardOffset;

        private readonly Dictionary<Transform, float> voxelHealth = new();
        private VoxelCarController target;
        private VoxelObstacleCarTuning trafficTuning;
        private EndlessVoxelRoad path;
        private float trackDistance;
        private float laneOffset;
        private float currentSpeed;
        private float spawnSpeed;
        private float approachSpeed;
        private float engageSpeed;
        private bool hasBeenRammed;
        private float destroyTime;
        private float nextCollisionTime;
        private Vector3 velocity;
        private VoxelEnemyHealthBar healthBar;
        private VoxelObstacleSpawner spawner;
        private float targetLaneOffset;
        private bool evasiveChanceRolled;
        private bool evasiveLaneChangePending;
        private float laneChangeSpeedBoostUntil = -1f;
        private float speedMatchUntil = -1f;
        private float rearRamForwardOffset;
        private float rearRamPushDistance;
        private float rearRamPushStartedAt = -1f;
        private float rearRamPushDuration;
        private VoxelEasingType rearRamPushEasing;
        private float sideRamOffset;
        private float sideRamOffsetStart;
        private float sideRamStartedAt = -1f;
        private float sideRamDuration;
        private VoxelEasingType sideRamEasing;

        public void Configure(VoxelCarController player, VoxelObstacleCarTuning traffic, VoxelEnemyVehicleTuning enemy,
            EndlessVoxelRoad road, float distance, float offset)
        {
            target = player;
            trafficTuning = traffic;
            Tuning = enemy;
            path = road;
            trackDistance = distance;
            laneOffset = offset;
            targetLaneOffset = offset;
            spawner = GetComponentInParent<VoxelObstacleSpawner>();
            CurrentHealth = enemy.vehicleHealth;
            float minimumMultiplier = Mathf.Min(enemy.minimumSpawnSpeedMultiplier, enemy.maximumSpawnSpeedMultiplier);
            float maximumMultiplier = Mathf.Max(enemy.minimumSpawnSpeedMultiplier, enemy.maximumSpawnSpeedMultiplier);
            // Enemy speed is selected once from the player's tuned maximum rather than
            // its live acceleration speed, so early spawns cannot become stationary.
            float playerMaximumSpeed = Mathf.Max(0f, player.topSpeed);
            spawnSpeed = playerMaximumSpeed * Random.Range(minimumMultiplier, maximumMultiplier);
            approachSpeed = playerMaximumSpeed * Random.Range(
                Mathf.Min(enemy.minimumApproachSpeedMultiplier, enemy.maximumApproachSpeedMultiplier),
                Mathf.Max(enemy.minimumApproachSpeedMultiplier, enemy.maximumApproachSpeedMultiplier));
            engageSpeed = playerMaximumSpeed * Random.Range(
                Mathf.Min(enemy.minimumEngageSpeedMultiplier, enemy.maximumEngageSpeedMultiplier),
                Mathf.Max(enemy.minimumEngageSpeedMultiplier, enemy.maximumEngageSpeedMultiplier));
            currentSpeed = spawnSpeed;
            VoxelRacerBootstrap.CreateObstacleCarVisuals(transform);
            ApplyBlackPaint();
            healthBar = VoxelEnemyHealthBar.Create(transform, enemy);
            ApplyTrackPose();
        }

        private void Update()
        {
            if (target == null || Tuning == null)
            {
                Destroy(gameObject);
                return;
            }

            if (hasBeenRammed)
            {
                velocity += Physics.gravity * Time.deltaTime;
                transform.position += velocity * Time.deltaTime;
                if (velocity.sqrMagnitude > 0.001f)
                    transform.Rotate(velocity.normalized * 300f * Time.deltaTime, Space.World);
                if (Time.time >= destroyTime)
                    Destroy(gameObject);
                return;
            }

            UpdateRamResponse();
            currentSpeed = GetCurrentDriveSpeed();
            trackDistance += currentSpeed * Time.deltaTime;
            UpdateEvasiveLaneChange();
            ApplyTrackPose();
            RotateWheels();

            bool overlapsLane = Mathf.Abs(target.CurrentLaneOffset - LaneOffset) < Tuning.collisionHalfWidth;
            bool overlapsDepth = Mathf.Abs(target.TrackDistance - TrackDistance) < Tuning.collisionHalfLength;
            if (overlapsLane && overlapsDepth && Time.time >= nextCollisionTime)
                RamByPlayer();

            if (TrackDistance < target.TrackDistance - 30f || TrackDistance > target.TrackDistance + GetMaximumDistanceAhead())
                Destroy(gameObject);
        }

        public void TakeProjectileHit(Transform hitVoxel, float damage, Vector3 hitPoint, Vector3 impactDirection)
        {
            TakeProjectileHit(hitVoxel, damage, hitPoint, impactDirection, true);
        }

        /// <summary>Used by hostile hazards. Damage is physical only and never becomes player mission credit.</summary>
        public void TakeHostileProjectileHit(Transform hitVoxel, float damage, Vector3 hitPoint, Vector3 impactDirection)
        {
            TakeProjectileHit(hitVoxel, damage, hitPoint, impactDirection, false);
        }

        private void TakeProjectileHit(Transform hitVoxel, float damage, Vector3 hitPoint, Vector3 impactDirection,
            bool awardMissionPoints)
        {
            if (hasBeenRammed || damage <= 0f)
                return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            TryRequestEvasiveLaneChange();
            if (hitVoxel != null)
            {
                if (awardMissionPoints)
                {
                    VoxelMissionProgress.ReportEnemyVoxelDamage();
                    VoxelScorePopup.Show(transform.position + Vector3.up * (Tuning.healthBarHeightOffset + 0.45f),
                        VoxelMissionProgress.GetEnemyVoxelDamagePoints(), VoxelScorePopup.Style.WeaponDamage);
                }
                voxelHealth.TryGetValue(hitVoxel, out float remainingVoxelHealth);
                remainingVoxelHealth = remainingVoxelHealth <= 0f ? Tuning.voxelHealth : remainingVoxelHealth;
                remainingVoxelHealth -= damage;
                if (remainingVoxelHealth <= 0f)
                {
                    voxelHealth.Remove(hitVoxel);
                    SpawnDebris(hitVoxel, impactDirection, DebrisStyle.Weapon);
                    hitVoxel.gameObject.SetActive(false);
                }
                else
                    voxelHealth[hitVoxel] = remainingVoxelHealth;
            }

            healthBar.SetHealth(HealthPercent);
            if (CurrentHealth <= 0f)
                Explode(hitPoint, impactDirection, awardMissionPoints);
        }

        /// <summary>
        /// Finds an intact voxel on the rearmost surface reached by a bullet path.
        /// Sustained forward fire therefore peels each surface vertically before it
        /// advances into the vehicle, rather than drilling a narrow horizontal tunnel.
        /// </summary>
        public bool TryGetNextProjectileVoxel(Vector3 segmentStart, Vector3 direction, float segmentLength,
            out Transform hitVoxel)
        {
            hitVoxel = null;
            if (hasBeenRammed || segmentLength <= 0f)
                return false;

            Vector3 right = Vector3.Cross(Vector3.up, direction).normalized;
            float rearSurfaceDistance = float.PositiveInfinity;
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                Transform voxel = renderer.transform;
                if (voxel == transform || voxel.GetComponentInParent<VoxelEnemyHealthBar>() != null)
                    continue;

                Vector3 offset = voxel.position - segmentStart;
                float forwardDistance = Vector3.Dot(offset, direction);
                if (forwardDistance < 0f || forwardDistance > segmentLength)
                    continue;

                float lateralDistance = Mathf.Abs(Vector3.Dot(offset, right));
                if (lateralDistance > 1.45f)
                    continue;

                rearSurfaceDistance = Mathf.Min(rearSurfaceDistance, forwardDistance);
            }

            if (float.IsPositiveInfinity(rearSurfaceDistance))
                return false;

            const float RearSurfaceDepth = 0.4f;
            float bestScore = float.PositiveInfinity;
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                Transform voxel = renderer.transform;
                if (voxel == transform || voxel.GetComponentInParent<VoxelEnemyHealthBar>() != null)
                    continue;

                Vector3 offset = voxel.position - segmentStart;
                float forwardDistance = Vector3.Dot(offset, direction);
                if (forwardDistance < rearSurfaceDistance || forwardDistance > rearSurfaceDistance + RearSurfaceDepth)
                    continue;

                float lateralDistance = Mathf.Abs(Vector3.Dot(offset, right));
                if (lateralDistance > 1.45f)
                    continue;

                Vector3 pointOnPath = segmentStart + direction * forwardDistance;
                float verticalDistance = Mathf.Abs(voxel.position.y - pointOnPath.y);
                float score = verticalDistance * 2f + lateralDistance + Random.value * Tuning.rearSurfaceHitRandomness;
                if (score >= bestScore)
                    continue;

                bestScore = score;
                hitVoxel = voxel;
            }

            return hitVoxel != null;
        }

        // Match the traffic spawner's configurable lead distance so an enemy created
        // offscreen is not immediately removed by its lifetime culling.
        private float GetMaximumDistanceAhead() => Mathf.Max(110f,
            (trafficTuning != null ? trafficTuning.spawnDistanceAhead : 110f) + 30f);

        /// <summary>Detonates a previously damaged interceptor when the mission ends, without awarding extra points.</summary>
        public void DetonateForMissionCompletion()
        {
            if (hasBeenRammed || Tuning == null || CurrentHealth >= Tuning.vehicleHealth)
                return;

            Explode(transform.position, transform.forward, false);
        }

        private void Explode(Vector3 hitPoint, Vector3 impactDirection, bool awardMissionPoints = true)
        {
            if (awardMissionPoints)
            {
                VoxelMissionProgress.ReportEnemyVehicleDestroyed();
                VoxelScorePopup.Show(transform.position + Vector3.up * (Tuning.healthBarHeightOffset + 0.55f),
                    VoxelMissionProgress.GetEnemyVehicleDestroyedPoints(), VoxelScorePopup.Style.EnemyDestroyed);
            }
            VoxelDestructionExplosion.Play(transform.position + Vector3.up * 0.8f, Tuning.explosionEffectScale);
            healthBar.gameObject.SetActive(false);
            var voxels = new List<Transform>();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
                if (renderer.transform != transform && renderer.transform != healthBar.transform)
                    voxels.Add(renderer.transform);
            voxels.Sort((first, second) => (first.position - hitPoint).sqrMagnitude.CompareTo((second.position - hitPoint).sqrMagnitude));
            int maximumDetachedVoxels = Mathf.FloorToInt(voxels.Count * Tuning.maximumExplosionVoxelRemovalPercent);
            int debrisCount = Mathf.Min(Tuning.explosionVoxelCount, maximumDetachedVoxels);
            for (int index = 0; index < debrisCount; index++)
            {
                SpawnDebris(voxels[index], impactDirection, DebrisStyle.Explosion);
                voxels[index].gameObject.SetActive(false);
            }

            hasBeenRammed = true;
            velocity = impactDirection.normalized * Random.Range(Tuning.explosionForwardForceMin, Tuning.explosionForwardForceMax)
                + Vector3.up * Tuning.explosionUpwardForce;
            destroyTime = Time.time + Tuning.destroyedLifetime;
        }

        private void RamByPlayer()
        {
            nextCollisionTime = Time.time + trafficTuning.collisionCooldown;
            Vector3 hitDirection = (transform.position - target.transform.position).normalized;
            if (hitDirection.sqrMagnitude < 0.001f)
                hitDirection = target.transform.forward;

            int originalPlayerDamage = target.damageVoxelsPerHit;
            target.damageVoxelsPerHit = Random.Range(
                Mathf.Min(Tuning.playerDamageVoxelsMin, Tuning.playerDamageVoxelsMax),
                Mathf.Max(Tuning.playerDamageVoxelsMin, Tuning.playerDamageVoxelsMax) + 1);
            target.ApplyDamage(target.GetDamageSurfacePoint(transform.position), hitDirection);
            target.damageVoxelsPerHit = originalPlayerDamage;

            ApplyVoxelDamage(transform.position - hitDirection * trafficTuning.impactVoxelDamageSurfaceOffset,
                -hitDirection, Random.Range(
                    Mathf.Min(trafficTuning.obstacleDamageVoxelsMin, trafficTuning.obstacleDamageVoxelsMax),
                    Mathf.Max(trafficTuning.obstacleDamageVoxelsMin, trafficTuning.obstacleDamageVoxelsMax) + 1));
            CurrentHealth = Mathf.Max(0f, CurrentHealth - Tuning.playerRamDamage);
            VoxelMissionProgress.ReportEnemyRamDamage(Tuning.playerRamDamage);
            VoxelScorePopup.Show(transform.position + Vector3.up * (Tuning.healthBarHeightOffset + 0.45f),
                VoxelMissionProgress.GetEnemyRamDamagePoints(Tuning.playerRamDamage), VoxelScorePopup.Style.RamDamage);
            healthBar.SetHealth(HealthPercent);
            if (CurrentHealth <= 0f)
                Explode(transform.position - hitDirection * trafficTuning.impactVoxelDamageSurfaceOffset, hitDirection);
            else
            {
                bool rearImpact = IsRearImpact(hitDirection);
                BeginRamResponse(rearImpact, hitDirection);
                target.ApplyRamResponse(rearImpact, hitDirection, Tuning);
            }
        }

        private int ApplyVoxelDamage(Vector3 hitPoint, Vector3 impactDirection, int voxelCount)
        {
            var candidates = new List<Transform>();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
                if (renderer.transform != transform && renderer.transform != healthBar.transform)
                    candidates.Add(renderer.transform);

            candidates.Sort((first, second) => (first.position - hitPoint).sqrMagnitude.CompareTo((second.position - hitPoint).sqrMagnitude));
            int destroyCount = Mathf.Min(voxelCount, candidates.Count);
            for (int index = 0; index < destroyCount; index++)
            {
                SpawnDebris(candidates[index], impactDirection, DebrisStyle.Ram);
                candidates[index].gameObject.SetActive(false);
            }
            return destroyCount;
        }

        private void ApplyTrackPose()
        {
            if (path == null)
                return;
            VoxelTrackPose pose = path.Evaluate(TrackDistance);
            transform.position = pose.position + pose.right * (laneOffset + sideRamOffset);
            transform.rotation = pose.rotation;
        }

        /// <summary>Returns true for the current and reserved destination lanes while an evasive move is in progress.</summary>
        public bool OccupiesLane(float candidateOffset, float laneWidth)
        {
            float tolerance = laneWidth * 0.25f;
            return Mathf.Abs(laneOffset - candidateOffset) <= tolerance ||
                Mathf.Abs(targetLaneOffset - candidateOffset) <= tolerance;
        }

        private void TryRequestEvasiveLaneChange()
        {
            if (evasiveChanceRolled || Tuning == null)
                return;

            float damagePercent = 1f - HealthPercent;
            if (damagePercent < Tuning.laneChangeDamagePercent)
                return;

            evasiveChanceRolled = true;
            evasiveLaneChangePending = Random.value <= Tuning.laneChangeChance;
        }

        private void UpdateEvasiveLaneChange()
        {
            if (Tuning == null)
                return;

            if (evasiveLaneChangePending && spawner != null &&
                spawner.TryFindSafeEnemyLane(this, out float safeLaneOffset))
            {
                targetLaneOffset = safeLaneOffset;
                evasiveLaneChangePending = false;
                if (Random.value <= Tuning.laneChangeSpeedBoostChance)
                    laneChangeSpeedBoostUntil = Time.time + Tuning.laneChangeSpeedBoostDuration;
            }

            laneOffset = Mathf.MoveTowards(laneOffset, targetLaneOffset,
                Tuning.laneChangeSpeed * Time.deltaTime);
        }

        private float GetPhaseSpeed()
        {
            float distanceAhead = TrackDistance - target.TrackDistance;
            if (distanceAhead <= Tuning.engageSpeedDistance)
                return engageSpeed;
            if (distanceAhead <= Tuning.approachSpeedDistance)
                return approachSpeed;
            return spawnSpeed;
        }

        private float GetCurrentDriveSpeed()
        {
            float speed = Time.time < speedMatchUntil ? target.CurrentSpeed : GetPhaseSpeed();
            if (Time.time < laneChangeSpeedBoostUntil)
                speed *= 1f + Tuning.laneChangeSpeedBoostMultiplier;
            return speed;
        }

        private bool IsRearImpact(Vector3 playerToEnemyDirection)
        {
            float forward = Vector3.Dot(playerToEnemyDirection, transform.forward);
            float lateral = Mathf.Abs(Vector3.Dot(playerToEnemyDirection, transform.right));
            return forward > lateral;
        }

        private void BeginRamResponse(bool rearImpact, Vector3 playerToEnemyDirection)
        {
            speedMatchUntil = Mathf.Max(speedMatchUntil, Time.time + Tuning.playerRamSpeedMatchDuration);
            if (rearImpact)
            {
                rearRamPushDistance = Tuning.rearRamEnemyForwardPushDistance;
                rearRamPushDuration = Tuning.rearRamEnemyForwardPushDuration;
                rearRamPushEasing = Tuning.rearRamEnemyForwardPushEasing;
                rearRamPushStartedAt = Time.time;
                rearRamForwardOffset = 0f;
                return;
            }

            float side = Mathf.Sign(Vector3.Dot(playerToEnemyDirection, transform.right));
            sideRamOffsetStart = side * Tuning.sideRamEnemyLaneShiftDistance;
            sideRamOffset = sideRamOffsetStart;
            sideRamDuration = Tuning.sideRamEnemyLaneShiftDuration;
            sideRamEasing = Tuning.sideRamEnemyLaneShiftEasing;
            sideRamStartedAt = Time.time;
        }

        private void UpdateRamResponse()
        {
            if (rearRamPushStartedAt >= 0f)
            {
                float progress = rearRamPushDuration <= 0.001f ? 1f :
                    Mathf.Clamp01((Time.time - rearRamPushStartedAt) / rearRamPushDuration);
                rearRamForwardOffset = Mathf.Lerp(0f, rearRamPushDistance,
                    VoxelEasing.Evaluate(rearRamPushEasing, progress));
                if (progress >= 1f)
                {
                    trackDistance += rearRamPushDistance;
                    rearRamForwardOffset = 0f;
                    rearRamPushStartedAt = -1f;
                }
            }

            if (sideRamStartedAt < 0f)
                return;

            float sideProgress = sideRamDuration <= 0.001f ? 1f :
                Mathf.Clamp01((Time.time - sideRamStartedAt) / sideRamDuration);
            sideRamOffset = Mathf.Lerp(sideRamOffsetStart, 0f,
                VoxelEasing.Evaluate(sideRamEasing, sideProgress));
            if (sideProgress >= 1f)
                sideRamStartedAt = -1f;
        }

        private void RotateWheels()
        {
            foreach (Transform child in transform)
                if (child.name == "Obstacle Voxel Wheel")
                    child.Rotate(Vector3.right, currentSpeed * trafficTuning.wheelSpinDegreesPerUnit * Time.deltaTime, Space.Self);
        }

        private void ApplyBlackPaint()
        {
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                bool isPaint = renderer.sharedMaterial == VoxelRacerBootstrap.ObstacleCarPaintMaterial;
                bool isTrim = renderer.sharedMaterial == VoxelRacerBootstrap.ObstacleCarTrimMaterial;
                if (!isPaint && !isTrim)
                    continue;

                Color colour = isPaint
                    ? new Color(0.025f, 0.025f, 0.03f)
                    : new Color(0.34f, 0.37f, 0.42f);
                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", colour);
                properties.SetColor("_Color", colour);
                renderer.SetPropertyBlock(properties);
            }
        }

        private void SpawnDebris(Transform source, Vector3 impactDirection, DebrisStyle style)
        {
            float scale;
            float forwardForceMin;
            float forwardForceMax;
            float upwardForce;
            float spreadForce;
            float lifetime;
            if (style == DebrisStyle.Weapon)
            {
                scale = Tuning.weaponDebrisScale;
                forwardForceMin = Tuning.weaponDebrisForwardForceMin;
                forwardForceMax = Tuning.weaponDebrisForwardForceMax;
                upwardForce = Tuning.weaponDebrisUpwardForce;
                spreadForce = Tuning.weaponDebrisSpreadForce;
                lifetime = Tuning.weaponDebrisLifetime;
            }
            else if (style == DebrisStyle.Ram)
            {
                scale = Tuning.ramDebrisScale;
                forwardForceMin = Tuning.ramDebrisForwardForceMin;
                forwardForceMax = Tuning.ramDebrisForwardForceMax;
                upwardForce = Tuning.ramDebrisUpwardForce;
                spreadForce = Tuning.ramDebrisSpreadForce;
                lifetime = Tuning.ramDebrisLifetime;
            }
            else
            {
                scale = Tuning.explosionDebrisScale;
                forwardForceMin = Tuning.explosionForwardForceMin;
                forwardForceMax = Tuning.explosionForwardForceMax;
                upwardForce = Tuning.explosionUpwardForce;
                spreadForce = Tuning.explosionSpreadForce;
                lifetime = Tuning.explosionDebrisLifetime;
            }

            var debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.name = "Enemy Car Damage Voxel";
            debris.transform.position = source.position + impactDirection.normalized * 0.25f + Random.insideUnitSphere * 0.16f;
            debris.transform.rotation = Random.rotation;
            debris.transform.localScale = source.lossyScale * scale;
            var sourceRenderer = source.GetComponent<MeshRenderer>();
            var debrisRenderer = debris.GetComponent<MeshRenderer>();
            debrisRenderer.sharedMaterial = sourceRenderer.sharedMaterial;
            var colourOverrides = new MaterialPropertyBlock();
            sourceRenderer.GetPropertyBlock(colourOverrides);
            debrisRenderer.SetPropertyBlock(colourOverrides);
            Destroy(debris.GetComponent<BoxCollider>());
            Vector3 burst = impactDirection.normalized * Random.Range(forwardForceMin, forwardForceMax)
                + Random.insideUnitSphere * spreadForce + Vector3.up * upwardForce;
            debris.AddComponent<VoxelDebris>().Launch(burst, lifetime);
        }
    }
}
