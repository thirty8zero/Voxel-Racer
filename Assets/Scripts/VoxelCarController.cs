using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace VoxelRacer
{
    /// <summary>Simple arcade lane driving. Tune the public values directly in the Inspector.</summary>
    public sealed class VoxelCarController : MonoBehaviour
    {
        [Header("Persistent Tuning")]
        [Tooltip("This car reads from the shared asset. Edit the asset itself to make persistent changes.")]
        public VoxelCarTuning tuning;

        [Header("Speed")]
        [Min(0f)] public float acceleration = 12f;
        [Min(0f)] public float topSpeed = 32f;
        [Min(0f)] public float brakingForce = 42f;

        [Header("Damage")]
        [Min(1)] public int damageVoxelsPerHit = 8;
        [Min(0)] public int debrisVoxelsPerDamagedVoxel = 2;
        [Min(0f)] public float explosionSpawnOffset = 0.45f;
        [Min(0f)] public float explosionUpwardBias = 0.75f;
        [Min(0f)] public float explosionForwardForceMin = 7f;
        [Min(0f)] public float explosionForwardForceMax = 10f;
        [Min(0f)] public float explosionUpwardForce = 2.5f;
        [Min(0f)] public float explosionSpreadForce = 1.5f;

        [Header("Lanes")]
        [Min(1)] public int laneCount = 4;
        [Min(0.1f)] public float laneWidth = 3f;
        [Min(0.1f)] public float laneChangeSpeed = 14f;
        [SerializeField] private int currentLane = 1;

        [Header("Visuals")]
        [Min(0f)] public float frontWheelTurnDegrees = 24f;
        [Min(0f)] public float wheelSpinDegreesPerUnit = 130f;

        [Header("Lane Change Visuals")]
        [Min(0f)] public float laneChangeBodyRollDegrees = 4f;
        [Min(0f)] public float laneChangeYawDegrees = 6f;
        [Min(0f)] public float laneChangeVisualRotationSpeed = 90f;

        public float CurrentSpeed { get; private set; }
        public EndlessVoxelRoad TrackPath { get; private set; }
        public float TrackDistance { get; private set; }
        public float CurrentLaneOffset { get; private set; }
        public float PlannedFinishStopDuration { get; private set; }
        public bool IsDestroyed { get; private set; }
        /// <summary>Live count of the player's remaining destructible visual voxels.</summary>
        public int RemainingIntegrityVoxels => IsDestroyed ? 0 : CountDestructibleVoxels();
        /// <summary>Total damageable voxels that make up this car at full integrity.</summary>
        public int TotalIntegrityVoxels
        {
            get
            {
                EnsureIntegrityBaseline();
                return initialIntegrityVoxels;
            }
        }
        public int MissingIntegrityVoxels => Mathf.Max(0, TotalIntegrityVoxels - RemainingIntegrityVoxels);
        public float IntegrityPercent => initialIntegrityVoxels == 0 ? 100f :
            Mathf.Clamp(100f * RemainingIntegrityVoxels / initialIntegrityVoxels, 0f, 100f);
        private float nextDamageTime;
        private bool drivingEnabled = true;
        private bool finishingRun;
        private float finishDeceleration;
        private float visualYaw;
        private float visualRoll;
        private int initialIntegrityVoxels;
        private Vector3 destroyedVelocity;
        private float wreckGroundHeight;
        private bool wreckResting;
        private const float RepairAttachmentDistance = 0.65f;
        private const float FourSecondStopBrakingReference = 30f;
        private const float MinimumFinishStopDuration = 1.5f;
        private const float MaximumFinishStopDuration = 4f;

        public void SetTuning(VoxelCarTuning value)
        {
            tuning = value;
            if (tuning != null)
            {
                tuning.ApplyTo(this);
            }
        }

        public void SetLaneLayout(int lanes, float width)
        {
            laneCount = Mathf.Max(1, lanes);
            laneWidth = Mathf.Max(0.1f, width);
            currentLane = Mathf.Clamp(currentLane, 0, laneCount - 1);

            CurrentLaneOffset = (currentLane - (laneCount - 1) * 0.5f) * laneWidth;
            ApplyTrackPose();
        }

        public void SetTrack(EndlessVoxelRoad path, float distance)
        {
            TrackPath = path;
            TrackDistance = distance;
            CurrentLaneOffset = (currentLane - (laneCount - 1) * 0.5f) * laneWidth;
            ApplyTrackPose();
        }

        /// <summary>Enables or pauses forward acceleration without disabling lane input.</summary>
        public void SetDrivingEnabled(bool enabled)
        {
            drivingEnabled = enabled;
            if (!enabled)
                CurrentSpeed = 0f;
        }

        /// <summary>Stops naturally according to this car's braking performance.</summary>
        public void BeginFinishStop()
        {
            PlannedFinishStopDuration = CalculateFinishStopDuration(CurrentSpeed, brakingForce);
            finishDeceleration = PlannedFinishStopDuration > 0.001f
                ? CurrentSpeed / PlannedFinishStopDuration
                : Mathf.Max(0.1f, brakingForce);
            finishingRun = true;
        }

        /// <summary>
        /// Converts braking performance into a presentation-friendly finish stop.
        /// Weak brakes never exceed four seconds; strong brakes retain at least
        /// 1.5 seconds so the synchronized finish camera remains comfortable.
        /// </summary>
        public static float CalculateFinishStopDuration(float currentSpeed, float brakePerformance)
        {
            if (currentSpeed <= 0.05f)
                return 0f;

            // Treat Braking Force as the car's performance rating for presentation
            // timing. This ensures a faster car with better brakes still stops sooner
            // than a slower car with poor brakes; actual deceleration is calculated
            // from its speed after the duration has been selected.
            float effectiveBraking = Mathf.Max(0.1f, Mathf.Max(0f, brakePerformance));
            float naturalDuration = MaximumFinishStopDuration *
                FourSecondStopBrakingReference / effectiveBraking;
            return Mathf.Clamp(naturalDuration, MinimumFinishStopDuration,
                MaximumFinishStopDuration);
        }

        private void Update()
        {
            if (!Application.isPlaying)
                return;

            if (IsDestroyed)
            {
                UpdateDestroyedWreck();
                return;
            }

            var keyboard = Keyboard.current;
            bool braking = keyboard != null && keyboard.spaceKey.isPressed;
            float targetSpeed = !drivingEnabled || braking || finishingRun ? 0f : topSpeed;
            float rate = braking ? brakingForce : finishingRun ? finishDeceleration : acceleration;
            CurrentSpeed = Mathf.MoveTowards(CurrentSpeed, targetSpeed, rate * Time.deltaTime);
            if (TrackPath != null)
                TrackDistance += CurrentSpeed * Time.deltaTime;
            else
                transform.position += Vector3.forward * (CurrentSpeed * Time.deltaTime);

            if (keyboard != null)
            {
                if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
                    currentLane = Mathf.Max(0, currentLane - 1);
                if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                    currentLane = Mathf.Min(laneCount - 1, currentLane + 1);
            }

            float targetLaneOffset = (currentLane - (laneCount - 1) * 0.5f) * laneWidth;
            CurrentLaneOffset = Mathf.MoveTowards(CurrentLaneOffset, targetLaneOffset, laneChangeSpeed * Time.deltaTime);
            float laneOffset = targetLaneOffset - CurrentLaneOffset;
            float steeringTarget = Mathf.Abs(laneOffset) > 0.01f
                ? Mathf.Sign(laneOffset) * frontWheelTurnDegrees
                : 0f;

            float laneDirection = Mathf.Abs(laneOffset) > 0.01f ? Mathf.Sign(laneOffset) : 0f;
            float targetYaw = laneDirection * laneChangeYawDegrees;
            float targetRoll = -laneDirection * laneChangeBodyRollDegrees;
            float visualStep = laneChangeVisualRotationSpeed * Time.deltaTime;
            visualYaw = Mathf.MoveTowardsAngle(visualYaw, targetYaw, visualStep);
            visualRoll = Mathf.MoveTowardsAngle(visualRoll, targetRoll, visualStep);
            ApplyTrackPose();

            foreach (Transform child in GetComponentsInChildren<Transform>())
            {
                if (child == transform)
                    continue;

                if (child.name == "Front Wheel Steering")
                {
                    float currentAngle = Mathf.DeltaAngle(0f, child.localEulerAngles.y);
                    float newAngle = Mathf.MoveTowards(currentAngle, steeringTarget, 240f * Time.deltaTime);
                    child.localRotation = Quaternion.Euler(0f, newAngle, 0f);
                }

                if (child.name == "Voxel Wheel")
                    child.Rotate(Vector3.right, CurrentSpeed * wheelSpinDegreesPerUnit * Time.deltaTime, Space.Self);
            }
        }

        private void ApplyTrackPose()
        {
            if (TrackPath == null)
                return;
            VoxelTrackPose pose = TrackPath.Evaluate(TrackDistance);
            transform.position = pose.position + pose.right * CurrentLaneOffset;
            transform.rotation = pose.rotation * Quaternion.Euler(0f, visualYaw, visualRoll);
        }

        public void ApplyDamage(Vector3 hitPoint, Vector3 impactDirection)
        {
            if (IsDestroyed || Time.time < nextDamageTime)
                return;

            nextDamageTime = Time.time + 0.35f;
            var candidates = new List<Transform>();
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.transform != transform && renderer.GetComponentInParent<VoxelIndestructiblePart>() == null)
                    candidates.Add(renderer.transform);
            }
            candidates.Sort((first, second) =>
                (first.position - hitPoint).sqrMagnitude.CompareTo((second.position - hitPoint).sqrMagnitude));

            var blocksToDestroy = new List<Transform>();
            var plannedWheelLosses = new Dictionary<VoxelWheelIntegrity, int>();
            foreach (var candidate in candidates)
            {
                if (blocksToDestroy.Count >= damageVoxelsPerHit)
                    break;

                var wheelIntegrity = candidate.GetComponentInParent<VoxelWheelIntegrity>();
                if (wheelIntegrity != null)
                {
                    plannedWheelLosses.TryGetValue(wheelIntegrity, out int plannedLosses);
                    if (!wheelIntegrity.CanLoseVoxel(plannedLosses))
                        continue;
                    plannedWheelLosses[wheelIntegrity] = plannedLosses + 1;
                }

                blocksToDestroy.Add(candidate);
            }

            // Normal hits preserve a small wheel core for readable driving damage.
            // Once that is all that remains, the next hit becomes terminal instead
            // of leaving the integrity meter permanently above zero.
            if (blocksToDestroy.Count == 0 && candidates.Count > 0)
                blocksToDestroy.AddRange(candidates);

            bool isLethalHit = blocksToDestroy.Count >= candidates.Count && candidates.Count > 0;
            if (isLethalHit)
            {
                // Keep a visible shell for the death wreck rather than removing every
                // final voxel. Integrity still reports zero once the car is destroyed.
                int shellVoxelCount = Mathf.Clamp(Mathf.CeilToInt(candidates.Count * 0.25f), 1, candidates.Count);
                int debrisVoxelCount = Mathf.Max(0, candidates.Count - shellVoxelCount);
                if (blocksToDestroy.Count > debrisVoxelCount)
                    blocksToDestroy.RemoveRange(debrisVoxelCount, blocksToDestroy.Count - debrisVoxelCount);
            }

            foreach (var block in blocksToDestroy)
            {
                SpawnDebris(block, impactDirection, debrisVoxelsPerDamagedVoxel);
                block.gameObject.SetActive(false);
            }

            if (isLethalHit)
                DestroyCar(impactDirection);
        }

        private void DestroyCar(Vector3 impactDirection)
        {
            if (IsDestroyed)
                return;

            IsDestroyed = true;
            drivingEnabled = false;
            finishingRun = false;
            CurrentSpeed = 0f;
            wreckGroundHeight = transform.position.y;
            wreckResting = false;

            Vector3 launchDirection = impactDirection.sqrMagnitude > 0.001f
                ? impactDirection.normalized
                : transform.forward;
            destroyedVelocity = launchDirection * Random.Range(explosionForwardForceMin, explosionForwardForceMax)
                + Vector3.up * Mathf.Max(2f, explosionUpwardForce * 2f);

            foreach (var gun in GetComponentsInChildren<VoxelGunMount>())
                gun.enabled = false;
        }

        private void UpdateDestroyedWreck()
        {
            if (wreckResting)
                return;

            destroyedVelocity += Physics.gravity * Time.deltaTime;
            transform.position += destroyedVelocity * Time.deltaTime;
            if (destroyedVelocity.sqrMagnitude > 0.001f)
                transform.Rotate(destroyedVelocity.normalized * 220f * Time.deltaTime, Space.World);

            if (transform.position.y > wreckGroundHeight || destroyedVelocity.y > 0f)
                return;

            Vector3 position = transform.position;
            position.y = wreckGroundHeight;
            transform.position = position;
            destroyedVelocity = Vector3.zero;
            wreckResting = true;
        }

        /// <summary>
        /// Finds the outermost active damageable surface facing an impact source.
        /// This supports player cars with very different lengths and widths without
        /// relying on a fixed offset from the car's origin.
        /// </summary>
        public Vector3 GetDamageSurfacePoint(Vector3 worldImpactSource)
        {
            Vector3 direction = worldImpactSource - transform.position;
            if (direction.sqrMagnitude < 0.0001f)
                direction = transform.forward;
            direction.Normalize();

            Vector3 distantPoint = transform.position + direction * 1000f;
            Vector3 bestPoint = transform.position;
            float bestProjection = float.NegativeInfinity;
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.transform == transform ||
                    renderer.GetComponentInParent<VoxelIndestructiblePart>() != null)
                    continue;

                Vector3 surfacePoint = renderer.bounds.ClosestPoint(distantPoint);
                float projection = Vector3.Dot(surfacePoint - transform.position, direction);
                if (projection <= bestProjection)
                    continue;

                bestProjection = projection;
                bestPoint = surfacePoint;
            }

            return bestPoint;
        }

        public void RepairToFull()
        {
            RepairPercent(100f);
        }

        /// <summary>Restores up to the requested percentage of the original car voxel count.</summary>
        public int RepairPercent(float amountPercent)
        {
            EnsureIntegrityBaseline();
            int restoreCount = Mathf.CeilToInt(initialIntegrityVoxels * Mathf.Clamp01(amountPercent / 100f));
            var activeVoxels = new List<Transform>();
            var missingVoxels = new List<Transform>();

            foreach (var renderer in GetComponentsInChildren<MeshRenderer>(true))
            {
                var voxel = renderer.transform;
                if (voxel == transform || voxel.GetComponentInParent<VoxelIndestructiblePart>() != null)
                    continue;

                if (voxel.gameObject.activeInHierarchy)
                    activeVoxels.Add(voxel);
                else
                    missingVoxels.Add(voxel);
            }

            int restored = 0;
            while (restored < restoreCount && missingVoxels.Count > 0)
            {
                int connectedIndex = FindConnectedMissingVoxel(missingVoxels, activeVoxels);
                if (connectedIndex < 0)
                    break;

                var voxel = missingVoxels[connectedIndex];
                voxel.gameObject.SetActive(true);
                activeVoxels.Add(voxel);
                missingVoxels.RemoveAt(connectedIndex);
                restored++;
            }
            return restored;
        }

        public void ResetIntegrityBaseline()
        {
            initialIntegrityVoxels = CountDestructibleVoxels();
        }

        public void EnsureIntegrityBaseline()
        {
            if (initialIntegrityVoxels == 0)
                ResetIntegrityBaseline();
        }

        private int CountDestructibleVoxels()
        {
            int count = 0;
            foreach (var renderer in GetComponentsInChildren<MeshRenderer>())
            {
                if (renderer.transform != transform && renderer.GetComponentInParent<VoxelIndestructiblePart>() == null)
                    count++;
            }
            return count;
        }

        private static int FindConnectedMissingVoxel(List<Transform> missingVoxels, List<Transform> activeVoxels)
        {
            float maxDistanceSqr = RepairAttachmentDistance * RepairAttachmentDistance;
            for (int missingIndex = 0; missingIndex < missingVoxels.Count; missingIndex++)
            {
                Vector3 position = missingVoxels[missingIndex].position;
                foreach (var activeVoxel in activeVoxels)
                {
                    if ((activeVoxel.position - position).sqrMagnitude <= maxDistanceSqr)
                        return missingIndex;
                }
            }
            return -1;
        }

        private void SpawnDebris(Transform source, Vector3 impactDirection, int count)
        {
            var renderer = source.GetComponent<MeshRenderer>();
            Vector3 burstDirection = (impactDirection.normalized + Vector3.up * explosionUpwardBias).normalized;
            for (int index = 0; index < count; index++)
            {
                var debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
                debris.name = "Damage Voxel";
                debris.transform.position = source.position + burstDirection * explosionSpawnOffset + Random.insideUnitSphere * 0.16f;
                debris.transform.rotation = Random.rotation;
                debris.transform.localScale = Vector3.one * Random.Range(0.16f, 0.30f);
                debris.GetComponent<MeshRenderer>().sharedMaterial = renderer.sharedMaterial;
                Destroy(debris.GetComponent<BoxCollider>());

                var burst = burstDirection * Random.Range(explosionForwardForceMin, explosionForwardForceMax)
                    + Random.insideUnitSphere * explosionSpreadForce
                    + Vector3.up * explosionUpwardForce;
                debris.AddComponent<VoxelDebris>().Launch(burst);
            }
        }
    }
}
