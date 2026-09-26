using System.Collections.Generic;
using UnityEngine;

namespace VoxelRacer
{
    /// <summary>A destructible traffic car that can drive with or against the player.</summary>
    public sealed class VoxelObstacleCar : MonoBehaviour
    {
        public event System.Action<VoxelObstacleCar> Defeated;
        public bool IsEnemyTraffic { get; private set; }
        private enum DebrisStyle { Weapon, Ram, Explosion }
        [Header("Persistent Tuning")]
        public VoxelObstacleCarTuning tuning;

        public VoxelEnemyVehicleTuning EnemyTuning { get; private set; }
        public float VoxelHealth => EnemyTuning != null ? EnemyTuning.voxelHealth : 1f;
        public float CurrentHealth { get; private set; }
        public float LaneOffset => laneOffset;
        public float TrackDistance => trackDistance;
        public bool TravelsWithPlayer => travelsWithPlayer;
        public float TravelSpeed => travelSpeed;
        public float TrafficHalfLength => collisionHalfLength;
        public bool IsDrivingTraffic => !hasBeenHit;
        public const float TrafficGap = 1.5f;
        private static readonly List<VoxelObstacleCar> ActiveTraffic = new();
        private void OnEnable() { if(!ActiveTraffic.Contains(this)) ActiveTraffic.Add(this); }
        private void OnDisable() => ActiveTraffic.Remove(this);

        private VoxelCarController target;
        private bool travelsWithPlayer;
        private bool isSemiTrailer;
        private Vector2 previousCollisionRelative;
        private float travelSpeed;
        private float spawnSpeed;
        private float approachSpeed;
        private float engageSpeed;
        private float collisionHalfWidth = 1.35f;
        private float collisionHalfLength = 2.3f;
        private bool hasBeenHit;
        private Vector3 velocity;
        private float destroyTime;
        private float nextCollisionTime;
        private EndlessVoxelRoad path;
        private float trackDistance;
        private float laneOffset;
        private readonly Dictionary<Transform, float> projectileVoxelHealth = new();
        private bool nearMissCandidate;
        private Transform[] modelWheels = System.Array.Empty<Transform>();
        private MeshRenderer[] projectileRenderers;
        private bool nearMissAwarded;
        private float closestNearMissDistance = float.PositiveInfinity;

        public void Configure(VoxelCarController player, VoxelObstacleCarTuning value, bool sameDirection,
            EndlessVoxelRoad road, float distance, float offset, float matchingTravelSpeed = -1f, VoxelEnemyVehicleTuning vehicleOverride = null)
        {
            target = player;
            tuning = value;
            travelsWithPlayer = sameDirection;
            path = road;
            trackDistance = distance;
            laneOffset = offset;
            float playerMaximumSpeed = Mathf.Max(0f, player.topSpeed);
            if (matchingTravelSpeed >= 0f)
            {
                // Start at the existing lane speed. Following clearance below also
                // handles later phase changes in the leading vehicle's speed.
                spawnSpeed = matchingTravelSpeed;
                approachSpeed = matchingTravelSpeed;
                engageSpeed = matchingTravelSpeed;
            }
            else
            {
                GetPhaseMultiplierRange(tuning, sameDirection, 0, out float spawnMin, out float spawnMax);
                GetPhaseMultiplierRange(tuning, sameDirection, 1, out float approachMin, out float approachMax);
                GetPhaseMultiplierRange(tuning, sameDirection, 2, out float engageMin, out float engageMax);
                spawnSpeed = playerMaximumSpeed * Random.Range(spawnMin, spawnMax);
                approachSpeed = playerMaximumSpeed * Random.Range(approachMin, approachMax);
                engageSpeed = playerMaximumSpeed * Random.Range(engageMin, engageMax);
            }
            travelSpeed = spawnSpeed;
            IsEnemyTraffic = vehicleOverride != null;
            isSemiTrailer = !IsEnemyTraffic && Random.value < tuning.semiTrailerSpawnChance;
            EnemyTuning = vehicleOverride != null ? vehicleOverride : (isSemiTrailer ? tuning.semiTrailerEnemyTuning : tuning.trafficCarEnemyTuning);
            if (IsEnemyTraffic)
            {
                collisionHalfWidth = EnemyTuning.collisionHalfWidth;
                collisionHalfLength = EnemyTuning.collisionHalfLength;
            }
            CurrentHealth = EnemyTuning != null ? EnemyTuning.vehicleHealth : 1f;
            if (isSemiTrailer)
            {
                collisionHalfWidth = EnemyTuning != null ? EnemyTuning.collisionHalfWidth : 1.4f;
                collisionHalfLength = EnemyTuning != null ? EnemyTuning.collisionHalfLength : 2.65f;
                gameObject.name = sameDirection ? "Civilian Van (Same Direction)" : "Civilian Van (Oncoming)";
            }
            BuildVisuals();
            if (!IsEnemyTraffic) ApplyRandomPaintColour();
            ApplyTrackPose();
            previousCollisionRelative=target.CollisionTrackPosition-new Vector2(laneOffset,trackDistance);
            gameObject.AddComponent<VoxelVehicleDamageEffects>().Configure();
        }

        private void Update()
        {
            if (target == null || tuning == null)
            {
                Destroy(gameObject);
                return;
            }

            if (!hasBeenHit)
            {
                float direction = travelsWithPlayer ? 1f : -1f;
                travelSpeed = GetSafeTrafficSpeed(GetPhaseSpeed(),Time.deltaTime);
                trackDistance += direction * travelSpeed * Time.deltaTime;
                ApplyTrackPose();
                RotateWheels(direction * travelSpeed);

                var relative=target.CollisionTrackPosition-new Vector2(laneOffset,trackDistance);
                bool contactFound=VoxelVehicleCollision.Sweep(previousCollisionRelative,relative,
                    new Vector2(collisionHalfWidth,collisionHalfLength),out var contact);
                previousCollisionRelative=relative;
                if (contactFound && Time.time >= nextCollisionTime)
                    HitCar(VoxelVehicleCollision.ImpactDirection(contact,target.transform));
                else
                    UpdateNearMiss();

                if (trackDistance < target.TrackDistance - 30f ||
                         trackDistance > target.TrackDistance + GetMaximumDistanceAhead())
                    Destroy(gameObject);
                return;
            }

            velocity += Physics.gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            if (velocity.sqrMagnitude > 0.001f)
                transform.Rotate(velocity.normalized * 300f * Time.deltaTime, Space.World);
            if (Time.time >= destroyTime)
                Destroy(gameObject);
        }

        public void GetPlayerCollisionDamageRange(out int minimum, out int maximum)
        {
            int first = IsEnemyTraffic && EnemyTuning != null ? EnemyTuning.playerDamageVoxelsMin : tuning.playerDamageVoxelsMin;
            int second = IsEnemyTraffic && EnemyTuning != null ? EnemyTuning.playerDamageVoxelsMax : tuning.playerDamageVoxelsMax;
            minimum = Mathf.Max(0, Mathf.Min(first, second));
            maximum = Mathf.Max(minimum, Mathf.Max(first, second));
        }

        private void HitCar(Vector3? sweptDirection = null)
        {
            nextCollisionTime = Time.time + tuning.collisionCooldown;
            hasBeenHit = true;
            Vector3 hitDirection = sweptDirection ?? (transform.position - target.transform.position).normalized;
            if (hitDirection.sqrMagnitude < 0.001f)
                hitDirection = travelsWithPlayer ? target.transform.forward : -target.transform.forward;

            GetPlayerCollisionDamageRange(out int damageMin, out int damageMax);
            int selectedPlayerDamage = Random.Range(damageMin, damageMax + 1);
            int originalPlayerDamage = target.damageVoxelsPerHit;
#if UNITY_EDITOR
            int integrityBeforeHit = target.RemainingIntegrityVoxels;
#endif
            target.damageVoxelsPerHit = selectedPlayerDamage;
            target.ApplyDamage(target.GetDamageSurfacePoint(transform.position), hitDirection, IsEnemyTraffic ? EnemyTuning.displayName + " collision" : (isSemiTrailer ? "Civilian van collision" : "Civilian car collision"));
            target.damageVoxelsPerHit = originalPlayerDamage;
#if UNITY_EDITOR
            int integrityAfterHit = target.RemainingIntegrityVoxels;
            Debug.Log($"Civilian collision: tuning={tuning.name}, configured={damageMin}-{damageMax}, " +
                $"selected={selectedPlayerDamage}, removed={integrityBeforeHit - integrityAfterHit}, " +
                $"integrity={target.IntegrityPercent:F1}%.", this);
#endif

            // Begin damage on the surface facing the player, rather than from the traffic
            // car's centre, so detached voxels consistently identify the collision point.
            float damageSurfaceOffset = isSemiTrailer
                ? tuning.semiImpactVoxelDamageSurfaceOffset
                : tuning.impactVoxelDamageSurfaceOffset;
            Vector3 obstacleImpactPoint = transform.position - hitDirection * damageSurfaceOffset;
            int obstacleDamageCount = Random.Range(
                Mathf.Min(tuning.obstacleDamageVoxelsMin, tuning.obstacleDamageVoxelsMax),
                Mathf.Max(tuning.obstacleDamageVoxelsMin, tuning.obstacleDamageVoxelsMax) + 1);
            int damagedVoxelCount = ApplyVoxelDamage(obstacleImpactPoint, -hitDirection, obstacleDamageCount, DebrisStyle.Ram);
            ReportVoxelDamage(damagedVoxelCount);
            ReportVoxelDestroyed(damagedVoxelCount, transform.position);
            ReportDestroyed(true);
            CurrentHealth = 0f;
            VoxelDestructionExplosion.Play(transform.position + Vector3.up * 0.8f,
                EnemyTuning != null ? EnemyTuning.explosionEffectScale : (isSemiTrailer ? 1.35f : 1f));
            velocity = hitDirection * tuning.launchForce + Vector3.up * tuning.launchUpwardForce;
            destroyTime = Time.time + tuning.destroyedLifetime;
        }

        /// <summary>Finds the nearest intact traffic voxel along a finite bullet segment, without per-voxel colliders.</summary>
        public static bool TryFindProjectileHit(Vector3 start, Vector3 direction, float distance,
            out VoxelObstacleCar vehicle, out Transform voxel, out Vector3 point, out float hitDistance)
        {
            vehicle = null; voxel = null; point = default; hitDistance = float.PositiveInfinity;
            if (distance <= 0 || direction.sqrMagnitude < .000001f) return false;
            direction.Normalize();
            foreach (var car in ActiveTraffic)
            {
                if (car == null || car.hasBeenHit || !car.gameObject.activeInHierarchy) continue;
                car.projectileRenderers ??= car.GetComponentsInChildren<MeshRenderer>(true);
                foreach (var renderer in car.projectileRenderers)
                {
                    if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                        renderer.GetComponentInParent<VoxelIndestructiblePart>() != null) continue;
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    // Test in the voxel's own space, so rotated cars and the spinning dish remain hittable.
                    var localStart = renderer.transform.InverseTransformPoint(start);
                    var localDirection = renderer.transform.InverseTransformVector(direction).normalized;
                    var bounds = filter.sharedMesh.bounds;
                    float entry = 0;
                    if (!bounds.Contains(localStart) && !bounds.IntersectRay(new Ray(localStart, localDirection), out entry)) continue;
                    var worldPoint = renderer.transform.TransformPoint(localStart + localDirection * entry);
                    float along = Vector3.Dot(worldPoint - start, direction);
                    if (along < 0 || along > distance || along >= hitDistance) continue;
                    vehicle = car; voxel = renderer.transform; point = worldPoint; hitDistance = along;
                }
            }
            return vehicle != null;
        }

        /// <summary>Applies weapon damage without giving civilians an enemy health bar.</summary>
        public void TakeProjectileHit(Transform hitVoxel, float damage, Vector3 hitPoint, Vector3 impactDirection)
        {
            TakeProjectileHit(hitVoxel, damage, hitPoint, impactDirection, true);
        }

        /// <summary>Used by hostile hazards. Civilian damage remains physical but does not penalise or reward the player.</summary>
        public void TakeHostileProjectileHit(Transform hitVoxel, float damage, Vector3 hitPoint, Vector3 impactDirection)
        {
            TakeProjectileHit(hitVoxel, damage, hitPoint, impactDirection, false);
        }

        private void TakeProjectileHit(Transform hitVoxel, float damage, Vector3 hitPoint, Vector3 impactDirection,
            bool awardMissionPoints)
        {
            if (hasBeenHit || EnemyTuning == null || damage <= 0f || (hitVoxel != null && !hitVoxel.gameObject.activeInHierarchy))
                return;

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damage);
            if (hitVoxel != null)
            {
                if (awardMissionPoints)
                    ReportVoxelDamage(1);
                projectileVoxelHealth.TryGetValue(hitVoxel, out float remainingVoxelHealth);
                remainingVoxelHealth = remainingVoxelHealth <= 0f ? EnemyTuning.voxelHealth : remainingVoxelHealth;
                remainingVoxelHealth -= damage;
                if (remainingVoxelHealth <= 0f)
                {
                    projectileVoxelHealth.Remove(hitVoxel);
                    SpawnDebris(hitVoxel, impactDirection, DebrisStyle.Weapon);
                    hitVoxel.gameObject.SetActive(false);
                    if (awardMissionPoints) ReportVoxelDestroyed(1, hitPoint);
                }
                else
                    projectileVoxelHealth[hitVoxel] = remainingVoxelHealth;
            }

            if (CurrentHealth <= 0f)
                DestroyFromWeaponHit(hitPoint, impactDirection, awardMissionPoints);
        }

        private void DestroyFromWeaponHit(Vector3 hitPoint, Vector3 impactDirection, bool awardMissionPoints = true)
        {
            hasBeenHit = true;
            ReportDestroyed(awardMissionPoints);
            VoxelDestructionExplosion.Play(transform.position + Vector3.up * 0.8f,
                EnemyTuning != null ? EnemyTuning.explosionEffectScale : (isSemiTrailer ? 1.35f : 1f));
            ApplyVoxelDamage(hitPoint, impactDirection, EnemyTuning.explosionVoxelCount, DebrisStyle.Explosion);
            velocity = impactDirection.normalized * tuning.launchForce + Vector3.up * tuning.launchUpwardForce;
            destroyTime = Time.time + EnemyTuning.destroyedLifetime;
        }

        private void ReportVoxelDamage(int count)
        {
            if (IsEnemyTraffic) VoxelMissionProgress.ReportEnemyVoxelDamage(count);
            else VoxelMissionProgress.ReportCivilianVoxelDamage(count);
        }
        private void ReportVoxelDestroyed(int count, Vector3 position)
        {
            if (IsEnemyTraffic) VoxelMissionProgress.ReportEnemyVoxelDestroyed(count, position);
            else VoxelMissionProgress.ReportCivilianVoxelDestroyed(count, position);
        }
        private void ReportDestroyed(bool award)
        {
            CurrentHealth = 0;
            if (award)
            {
                if (IsEnemyTraffic) VoxelMissionProgress.ReportEnemyVehicleDestroyed(transform.position, EnemyTuning);
                else VoxelMissionProgress.ReportCivilianVehicleDestroyed(transform.position);
            }
            Defeated?.Invoke(this);
        }

        private void ApplyTrackPose()
        {
            if (path == null)
                return;
            VoxelTrackPose pose = path.Evaluate(trackDistance);
            transform.position = pose.position + pose.right * laneOffset;
            transform.rotation = travelsWithPlayer ? pose.rotation : pose.rotation * Quaternion.Euler(0f, 180f, 0f);
        }

        private float GetPhaseSpeed()
        {
            float distanceAhead = trackDistance - target.TrackDistance;
            if (distanceAhead <= tuning.engageSpeedDistance)
                return engageSpeed;
            if (distanceAhead <= tuning.approachSpeedDistance)
                return approachSpeed;
            return spawnSpeed;
        }

        private float GetSafeTrafficSpeed(float desiredSpeed,float deltaTime)
        {
            if(deltaTime<=0) return 0;
            float direction=travelsWithPlayer?1f:-1f;
            float allowedDistance=Mathf.Max(0,desiredSpeed)*deltaTime;
            foreach(var other in ActiveTraffic)
            {
                if(other==this || other==null || !other.IsDrivingTraffic || other.target==null || other.path!=path ||
                    other.transform.parent!=transform.parent || other.travelsWithPlayer!=travelsWithPlayer ||
                    Mathf.Abs(other.laneOffset-laneOffset)>=collisionHalfWidth+other.collisionHalfWidth) continue;
                float ahead=(other.trackDistance-trackDistance)*direction;
                if(ahead<=0) continue;
                float clearance=collisionHalfLength+other.collisionHalfLength+TrafficGap;
                // Bound this frame's movement against the leader's current position:
                // safe even if it brakes, or Unity updates the follower first.
                allowedDistance=Mathf.Min(allowedDistance,Mathf.Max(0,ahead-clearance));
            }
            return allowedDistance/deltaTime;
        }

        private static void GetPhaseMultiplierRange(VoxelObstacleCarTuning value, bool sameDirection,
            int phase, out float minimum, out float maximum)
        {
            if (sameDirection)
            {
                minimum = phase == 0 ? value.sameDirectionSpawnSpeedMultiplierMin
                    : phase == 1 ? value.sameDirectionApproachSpeedMultiplierMin
                    : value.sameDirectionEngageSpeedMultiplierMin;
                maximum = phase == 0 ? value.sameDirectionSpawnSpeedMultiplierMax
                    : phase == 1 ? value.sameDirectionApproachSpeedMultiplierMax
                    : value.sameDirectionEngageSpeedMultiplierMax;
            }
            else
            {
                minimum = phase == 0 ? value.oncomingSpawnSpeedMultiplierMin
                    : phase == 1 ? value.oncomingApproachSpeedMultiplierMin
                    : value.oncomingEngageSpeedMultiplierMin;
                maximum = phase == 0 ? value.oncomingSpawnSpeedMultiplierMax
                    : phase == 1 ? value.oncomingApproachSpeedMultiplierMax
                    : value.oncomingEngageSpeedMultiplierMax;
            }

            minimum = Mathf.Max(0f, Mathf.Min(minimum, maximum));
            maximum = Mathf.Max(minimum, maximum);
        }

        // Vehicles may be spawned farther ahead than the original 110-unit prototype
        // distance. Keep them alive until the player can reasonably reach them.
        private float GetMaximumDistanceAhead() => Mathf.Max(110f,
            (tuning != null ? tuning.spawnDistanceAhead : 110f) + 30f);

        private void UpdateNearMiss()
        {
            VoxelMissionTuning mission = VoxelMissionProgress.Active?.Tuning;
            if (mission == null || nearMissAwarded)
                return;

            float lateralGap = Mathf.Max(0f, Mathf.Abs(target.CollisionTrackPosition.x - laneOffset) -
                (collisionHalfWidth + mission.civilianNearMissPlayerHalfWidth));
            float longitudinalGap = Mathf.Max(0f, Mathf.Abs(target.CollisionTrackPosition.y - trackDistance) -
                (collisionHalfLength + mission.civilianNearMissPlayerHalfLength));
            float clearDistance = Mathf.Sqrt(lateralGap * lateralGap + longitudinalGap * longitudinalGap);
            if (clearDistance <= mission.civilianNearMissDistance)
            {
                nearMissCandidate = true;
                closestNearMissDistance = Mathf.Min(closestNearMissDistance, clearDistance);
            }

            // A close call is resolved only after the two vehicle bounds have
            // separated longitudinally.  This deliberately accepts either pass
            // direction: the player can overtake a slow semi, or a faster/oncoming
            // vehicle can pass the player. The previous behind-player-only test
            // silently rejected the latter case.
            float safePassDistance = collisionHalfLength + mission.civilianNearMissPlayerHalfLength +
                mission.civilianNearMissPassClearance;
            bool safelyPassed = Mathf.Abs(target.CollisionTrackPosition.y - trackDistance) > safePassDistance;
            if (!nearMissCandidate || !safelyPassed)
                return;

            float closeness = 1f - Mathf.Clamp01(closestNearMissDistance / mission.civilianNearMissDistance);
            float stepFraction = mission.civilianNearMissScoreStepPercent * 0.01f;
            int closerSteps = Mathf.FloorToInt(closeness / stepFraction + 0.0001f);
            int points = Mathf.Clamp(mission.civilianNearMissMinPoints + closerSteps,
                mission.civilianNearMissMinPoints, mission.civilianNearMissMaxPoints);
            nearMissAwarded = true;
            VoxelMissionProgress.ReportCivilianNearMiss(points, transform.position);
            VoxelScorePopup.ShowNearMiss(transform.position + Vector3.up * 2.8f, points,
                mission.civilianNearMissPopupDuration);
        }

        private int ApplyVoxelDamage(Vector3 hitPoint, Vector3 impactDirection, int voxelCount, DebrisStyle style)
        {
            var candidates = new List<Transform>();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
                if (renderer.transform != transform)
                    candidates.Add(renderer.transform);

            candidates.Sort((a, b) => (a.position - hitPoint).sqrMagnitude.CompareTo((b.position - hitPoint).sqrMagnitude));
            int destroyCount = Mathf.Min(voxelCount, candidates.Count);
            for (int index = 0; index < destroyCount; index++)
            {
                Transform voxel = candidates[index];
                SpawnDebris(voxel, impactDirection, style);
                voxel.gameObject.SetActive(false);
            }
            return destroyCount;
        }

        private void SpawnDebris(Transform source, Vector3 impactDirection, DebrisStyle style)
        {
            VoxelEnemyVehicleTuning vehicleTuning = EnemyTuning;
            if (vehicleTuning == null)
                return;

            float scale;
            float forwardForceMin;
            float forwardForceMax;
            float upwardForce;
            float spreadForce;
            float lifetime;
            if (style == DebrisStyle.Weapon)
            {
                scale = vehicleTuning.weaponDebrisScale;
                forwardForceMin = vehicleTuning.weaponDebrisForwardForceMin;
                forwardForceMax = vehicleTuning.weaponDebrisForwardForceMax;
                upwardForce = vehicleTuning.weaponDebrisUpwardForce;
                spreadForce = vehicleTuning.weaponDebrisSpreadForce;
                lifetime = vehicleTuning.weaponDebrisLifetime;
            }
            else if (style == DebrisStyle.Ram)
            {
                scale = vehicleTuning.ramDebrisScale;
                forwardForceMin = vehicleTuning.ramDebrisForwardForceMin;
                forwardForceMax = vehicleTuning.ramDebrisForwardForceMax;
                upwardForce = vehicleTuning.ramDebrisUpwardForce;
                spreadForce = vehicleTuning.ramDebrisSpreadForce;
                lifetime = vehicleTuning.ramDebrisLifetime;
            }
            else
            {
                scale = vehicleTuning.explosionDebrisScale;
                forwardForceMin = vehicleTuning.explosionForwardForceMin;
                forwardForceMax = vehicleTuning.explosionForwardForceMax;
                upwardForce = vehicleTuning.explosionUpwardForce;
                spreadForce = vehicleTuning.explosionSpreadForce;
                lifetime = vehicleTuning.explosionDebrisLifetime;
            }

            var debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.name = "Obstacle Car Damage Voxel";
            Vector3 burstDirection = (impactDirection.normalized + Vector3.up * 0.75f).normalized;
            debris.transform.position = source.position + burstDirection * 0.45f + Random.insideUnitSphere * 0.16f;
            debris.transform.rotation = Random.rotation;
            debris.transform.localScale = source.lossyScale * Random.Range(0.75f, 1.15f) * scale;
            debris.GetComponent<MeshRenderer>().sharedMaterial = source.GetComponent<MeshRenderer>().sharedMaterial;
            var paintProperties = new MaterialPropertyBlock();
            source.GetComponent<MeshRenderer>().GetPropertyBlock(paintProperties);
            debris.GetComponent<MeshRenderer>().SetPropertyBlock(paintProperties);
            Destroy(debris.GetComponent<BoxCollider>());
            Vector3 burst = burstDirection * Random.Range(forwardForceMin, forwardForceMax)
                + Random.insideUnitSphere * spreadForce + Vector3.up * upwardForce;
            debris.AddComponent<VoxelDebris>().Launch(burst, lifetime);
        }

        private void BuildVisuals()
        {
            if (EnemyTuning != null && EnemyTuning.modelPrefab != null)
                Instantiate(EnemyTuning.modelPrefab, transform, false);
            else
                VoxelRacerBootstrap.CreateObstacleCarVisuals(transform);
            projectileRenderers = GetComponentsInChildren<MeshRenderer>(true);
            var wheels = new List<Transform>();
            foreach (var child in GetComponentsInChildren<Transform>())
                if (child.name == "Obstacle Voxel Wheel") wheels.Add(child);
            modelWheels = wheels.ToArray();
        }

        private void ApplyRandomPaintColour()
        {
            if (tuning.paintColours == null || tuning.paintColours.Length == 0)
                return;

            Color paintColour = tuning.paintColours[Random.Range(0, tuning.paintColours.Length)];
            var modelPaint = GetComponentInChildren<VoxelTrafficPaint>();
            Material paintMaterial = modelPaint != null ? modelPaint.bodyMaterial : VoxelRacerBootstrap.ObstacleCarPaintMaterial;
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.sharedMaterial != paintMaterial)
                    continue;

                var properties = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(properties);
                properties.SetColor("_BaseColor", paintColour);
                renderer.SetPropertyBlock(properties);
            }
        }

        private void RotateWheels(float speed)
        {
            foreach (Transform child in modelWheels)
                if (child != null)
                    child.Rotate(Vector3.right, speed * tuning.wheelSpinDegreesPerUnit * Time.deltaTime, Space.Self);
        }
    }
}
