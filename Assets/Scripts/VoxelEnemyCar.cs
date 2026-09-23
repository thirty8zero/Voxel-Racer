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
        private Transform[] modelWheels;
        public event System.Action<VoxelEnemyCar> Defeated;
        public bool IsBoss => bossSettings!=null;
        private VoxelBossSettings bossSettings;
        private float bossLaneWidth, bossVisualScale, nextBossLaneChange;
        private int bossLaneCount, bossLanePair;
        private bool bossMovingAway;
        private bool bossCatchingUp;
        public void ConfigureBoss(VoxelBossSettings settings,float laneWidth,int laneCount)
        {
            bossSettings=settings;bossLaneWidth=laneWidth;bossLaneCount=Mathf.Max(2,laneCount);
            bossVisualScale=laneWidth*1.8f/2.23f;
            bossLanePair=(bossLaneCount-2)/2;
            nextBossLaneChange=Time.time+Mathf.Max(.1f,settings.minimumLaneChangeInterval);
        }
        private Vector2 previousCollisionRelative;
        private float nextMineTime;
        private float postDropLaneChangeAt = float.PositiveInfinity;

        public void Configure(VoxelCarController player, VoxelObstacleCarTuning traffic, VoxelEnemyVehicleTuning enemy,
            EndlessVoxelRoad road, float distance, float offset)
        {
            target = player;
            trafficTuning = traffic;
            Tuning = enemy;
            path = road;
            trackDistance = distance;
            laneOffset = offset;
            if(IsBoss) laneOffset=(bossLanePair-(bossLaneCount-2)*.5f)*bossLaneWidth;
            targetLaneOffset = laneOffset;
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
            if(IsBoss) currentSpeed=GetBossDriveSpeed();
            CreateModel(enemy);
            if(!IsBoss) healthBar = VoxelEnemyHealthBar.Create(transform, enemy);
            ApplyTrackPose();
            previousCollisionRelative=target.CollisionTrackPosition-new Vector2(LaneOffset,TrackDistance);
            gameObject.AddComponent<VoxelVehicleDamageEffects>().Configure();
            if(IsBoss)
            {
                var effects=GetComponent<VoxelVehicleDamageEffects>();
                effects.smokeDamageThreshold=bossSettings.smokeDamageThreshold;
                effects.fireDamageThreshold=bossSettings.fireDamageThreshold;
                foreach(var ps in GetComponentsInChildren<ParticleSystem>())
                    ps.transform.localPosition=new Vector3(0,ps.name=="Damage Fire"?1.4f:1.25f,1.5f)*bossVisualScale;
            }
            nextMineTime = Time.time + (enemy.mineLayer != null ? Mathf.Max(.1f, enemy.mineLayer.dropInterval) : 0f);
        }

        private void CreateModel(VoxelEnemyVehicleTuning enemy)
        {
            if (enemy.modelPrefab != null)
            {
                var visual=Instantiate(enemy.modelPrefab, transform, false);
                if(IsBoss)
                {
                    visual.transform.localScale*=bossVisualScale;
                    visual.AddComponent<VoxelBossEntrance>().Configure(bossSettings.entranceDuration);
                }
            }
            else
            {
                VoxelRacerBootstrap.CreateObstacleCarVisuals(transform);
                ApplyBlackPaint();
            }
            var wheels = new List<Transform>();
            foreach (var child in GetComponentsInChildren<Transform>())
                if (child.name == "Obstacle Voxel Wheel") wheels.Add(child);
            modelWheels = wheels.ToArray();
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
            currentSpeed = IsBoss ? AdvanceBossSpeed(Time.deltaTime) : GetCurrentDriveSpeed();
            trackDistance += currentSpeed * Time.deltaTime;
            if(IsBoss) UpdateBossLaneChange(); else UpdateEvasiveLaneChange();
            ApplyTrackPose();
            RotateWheels();
            UpdateMineLayer();

            var relative=target.CollisionTrackPosition-new Vector2(LaneOffset,TrackDistance);
            bool contactFound=VoxelVehicleCollision.Sweep(previousCollisionRelative,relative,
                new Vector2(Tuning.collisionHalfWidth,Tuning.collisionHalfLength),out var contact);
            previousCollisionRelative=relative;
            if (contactFound && Time.time >= nextCollisionTime)
                RamByPlayer(VoxelVehicleCollision.ImpactDirection(contact,target.transform));

            if (!IsBoss && (TrackDistance < target.TrackDistance - 30f || TrackDistance > target.TrackDistance + GetMaximumDistanceAhead()))
                Destroy(gameObject);
        }

        private float GetBossDriveSpeed()
        {
            float minimum=Mathf.Max(Tuning.collisionHalfLength+5f,Mathf.Min(bossSettings.minimumDistanceAhead,bossSettings.maximumDistanceAhead));
            float maximum=Mathf.Max(minimum+2f,Mathf.Max(bossSettings.minimumDistanceAhead,bossSettings.maximumDistanceAhead));
            float gap=TrackDistance-target.CollisionTrackPosition.y;
            float catchDistance=Mathf.Clamp(bossSettings.catchUpDistance,2,bossSettings.WarningDistance);
            if(gap>=catchDistance) bossCatchingUp=true;
            else if(bossCatchingUp && gap<=Mathf.Clamp(bossSettings.catchUpResumeDistance,1,catchDistance-1))
            {
                bossCatchingUp=false;
                bossMovingAway=true;
            }
            float topSpeed=target.EffectiveTopSpeed;
            float cruise=topSpeed*Mathf.Clamp(bossSettings.minimumCruiseSpeedFraction,.1f,1f);
            if(bossCatchingUp) return Mathf.Max(cruise,topSpeed*Mathf.Clamp(bossSettings.catchUpSpeedFraction,.1f,1f));
            // Start recovering before the player closes the minimum gap while we accelerate.
            float closingSpeed=Mathf.Max(0,topSpeed-currentSpeed);
            float recoveryDistance=closingSpeed*closingSpeed/(2f*Mathf.Max(.1f,bossSettings.acceleration));
            if(gap<=minimum+1f+recoveryDistance) bossMovingAway=true;
            if(gap>=maximum-1f) bossMovingAway=false;
            float desired=bossMovingAway?maximum:minimum;
            return Mathf.Clamp(topSpeed+Mathf.Clamp((desired-gap)*2f,-bossSettings.distanceAdjustmentSpeed,bossSettings.distanceAdjustmentSpeed),cruise,topSpeed+bossSettings.distanceAdjustmentSpeed);
        }
        private float AdvanceBossSpeed(float deltaTime)
        {
            float desired=GetBossDriveSpeed();
            float rate=desired>currentSpeed ? bossSettings.acceleration : bossSettings.braking;
            return Mathf.MoveTowards(currentSpeed,desired,Mathf.Max(.1f,rate)*Mathf.Max(0,deltaTime));
        }
        private void UpdateBossLaneChange()
        {
            if(Time.time>=nextBossLaneChange && Mathf.Abs(laneOffset-targetLaneOffset)<.02f)
            {
                int pairs=bossLaneCount-1;
                if(pairs>1)
                {
                    int candidate=Random.Range(0,pairs-1);
                    if(candidate>=bossLanePair)candidate++;
                    bossLanePair=candidate;
                    targetLaneOffset=(candidate-(bossLaneCount-2)*.5f)*bossLaneWidth;
                }
                nextBossLaneChange=float.PositiveInfinity;
            }
            laneOffset=Mathf.MoveTowards(laneOffset,targetLaneOffset,Mathf.Max(.1f,bossSettings.laneChangeSpeed)*Time.deltaTime);
            if(float.IsPositiveInfinity(nextBossLaneChange) && Mathf.Abs(laneOffset-targetLaneOffset)<.02f)
            {
                float min=Mathf.Max(.1f,bossSettings.minimumLaneChangeInterval);
                nextBossLaneChange=Time.time+Random.Range(min,Mathf.Max(min,bossSettings.maximumLaneChangeInterval));
            }
        }

        private void UpdateMineLayer()
        {
            var mines = Tuning.mineLayer;
            if (mines == null || target.IsDestroyed || VoxelMissionProgress.Active?.IsComplete == true ||
                (VoxelStartCountdown.Active != null && !VoxelStartCountdown.Active.IsComplete)) return;
            if (Time.time < nextMineTime) return;
            nextMineTime = Time.time + Mathf.Max(.1f, mines.dropInterval);
            if (Random.value >= Mathf.Clamp01(mines.dropChance) || mines.minePrefab == null) return;
            int count=IsBoss?2:1;
            for(int i=0;i<count;i++)
            {
                var mine = new GameObject("Enemy Road Mine").AddComponent<VoxelRoadMine>();
                mine.transform.SetParent(transform.parent, false);
                float offset=LaneOffset+(IsBoss?(i==0?-.5f:.5f)*bossLaneWidth:0);
                mine.Configure(target, path, mines, TrackDistance-(IsBoss?3f*bossVisualScale:2.9f),offset);
            }
            if(!IsBoss) SchedulePostDropLaneChange(mines, Random.value, Time.time);
        }

        private void SchedulePostDropLaneChange(VoxelMineLayerTuning mines, float roll, float now)
        {
            float chance = Mathf.Clamp01(mines.postDropLaneChangeChance);
            if (chance <= 0f || (chance < 1f && roll >= chance)) return;
            // Further drops never postpone an already pending move or build up a queue of swerves.
            postDropLaneChangeAt = Mathf.Min(postDropLaneChangeAt, now + Mathf.Max(0f, mines.postDropLaneChangeDelay));
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
            if (hasBeenRammed || damage <= 0f || (hitVoxel != null && !hitVoxel.gameObject.activeInHierarchy))
                return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            TryRequestEvasiveLaneChange();
            if (hitVoxel != null)
            {
                if (awardMissionPoints)
                {
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
                    if (awardMissionPoints) VoxelMissionProgress.ReportEnemyVoxelDestroyed(1, hitPoint);
                }
                else
                    voxelHealth[hitVoxel] = remainingVoxelHealth;
            }

            CheckBossBodyDestroyed();
            if(healthBar!=null) healthBar.SetHealth(HealthPercent);
            if (CurrentHealth <= 0f)
                Explode(hitPoint, impactDirection, awardMissionPoints);
            if (awardMissionPoints && hitVoxel != null) VoxelMissionProgress.ReportEnemyVoxelDamage();
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
            if(hasBeenRammed) return;
            if (awardMissionPoints)
            {
                VoxelMissionProgress.ReportEnemyVehicleDestroyed(transform.position, Tuning);
                VoxelScorePopup.Show(transform.position + Vector3.up * (Tuning.healthBarHeightOffset + 0.55f),
                    VoxelMissionProgress.GetEnemyVehicleDestroyedPoints(Tuning), VoxelScorePopup.Style.EnemyDestroyed);
            }
            VoxelDestructionExplosion.Play(transform.position + Vector3.up * 0.8f, Tuning.explosionEffectScale);
            if(healthBar!=null) healthBar.gameObject.SetActive(false);
            var voxels = new List<Transform>();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
                if (renderer.transform != transform && renderer.GetComponentInParent<VoxelEnemyHealthBar>() == null)
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
            if(IsBoss) Defeated?.Invoke(this);
        }

        private void CheckBossBodyDestroyed()
        {
            // An extreme total-health setting must not leave an invisible, unhittable boss.
            if(!IsBoss) return;
            foreach(var renderer in GetComponentsInChildren<MeshRenderer>())
                if(renderer.GetComponentInParent<VoxelEnemyHealthBar>()==null) return;
            CurrentHealth=0;
        }

        private void RamByPlayer(Vector3? sweptDirection = null)
        {
            nextCollisionTime = Time.time + trafficTuning.collisionCooldown;
            Vector3 hitDirection = sweptDirection ?? (transform.position - target.transform.position).normalized;
            if (hitDirection.sqrMagnitude < 0.001f)
                hitDirection = target.transform.forward;
            bool rearImpact = IsRearImpact(hitDirection);

            int originalPlayerDamage = target.damageVoxelsPerHit;
            target.damageVoxelsPerHit = Random.Range(
                Mathf.Min(Tuning.playerDamageVoxelsMin, Tuning.playerDamageVoxelsMax),
                Mathf.Max(Tuning.playerDamageVoxelsMin, Tuning.playerDamageVoxelsMax) + 1);
            target.ApplyDamage(target.GetDamageSurfacePoint(transform.position), hitDirection, "Enemy collision");
            target.damageVoxelsPerHit = originalPlayerDamage;

            int removedVoxels = ApplyVoxelDamage(transform.position - hitDirection * trafficTuning.impactVoxelDamageSurfaceOffset,
                -hitDirection, Random.Range(
                    Mathf.Min(trafficTuning.obstacleDamageVoxelsMin, trafficTuning.obstacleDamageVoxelsMax),
                    Mathf.Max(trafficTuning.obstacleDamageVoxelsMin, trafficTuning.obstacleDamageVoxelsMax) + 1));
            float ramDamage = Tuning.playerRamDamage + (rearImpact ? 0f : VoxelWheelSpikeUpgradeState.SideRamDamageBonus);
            VoxelMissionProgress.ReportEnemyVoxelDestroyed(removedVoxels, transform.position);
            CurrentHealth = Mathf.Max(0f, CurrentHealth - ramDamage);
            CheckBossBodyDestroyed();
            VoxelScorePopup.Show(transform.position + Vector3.up * (Tuning.healthBarHeightOffset + 0.45f),
                VoxelMissionProgress.GetEnemyRamDamagePoints(ramDamage), VoxelScorePopup.Style.RamDamage);
            if(healthBar!=null) healthBar.SetHealth(HealthPercent);
            if (CurrentHealth <= 0f)
                Explode(transform.position - hitDirection * trafficTuning.impactVoxelDamageSurfaceOffset, hitDirection);
            else
            {
                BeginRamResponse(rearImpact, hitDirection);
                target.ApplyRamResponse(rearImpact, hitDirection, Tuning);
            }
            VoxelMissionProgress.ReportEnemyRamDamage(ramDamage);
        }

        private int ApplyVoxelDamage(Vector3 hitPoint, Vector3 impactDirection, int voxelCount)
        {
            var candidates = new List<Transform>();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
                if (renderer.transform != transform && renderer.GetComponentInParent<VoxelEnemyHealthBar>() == null)
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

            bool postDropReady = Time.time >= postDropLaneChangeAt;
            bool settledInLane = Mathf.Abs(laneOffset - targetLaneOffset) < .01f;
            if ((evasiveLaneChangePending || postDropReady) && settledInLane && spawner != null &&
                spawner.TryFindSafeEnemyLane(this, out float safeLaneOffset))
            {
                targetLaneOffset = safeLaneOffset;
                evasiveLaneChangePending = false;
                if (postDropReady) postDropLaneChangeAt = float.PositiveInfinity;
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
            foreach (Transform child in modelWheels)
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
