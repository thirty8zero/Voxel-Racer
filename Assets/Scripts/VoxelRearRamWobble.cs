using UnityEngine;

namespace VoxelRacer
{
    /// <summary>A short counter-steering wobble on the visual model, independent of driving/contact coordinates.</summary>
    public sealed class VoxelRearRamWobble : MonoBehaviour
    {
        private const float TransitionDuration = .32f;
        private const float SettleDuration = .18f;
        private static readonly float[] RecoveryAngles = { 12f, -10f, 8f, -6f, 4f, -2f };
        private const string SteeringPivotName = "Rear Recovery Front Steering";
        private Transform body;
        private Quaternion restRotation;
        private float elapsed;
        private float playbackSpeed = 1f;
        private Transform[] steeringPivots = System.Array.Empty<Transform>();

        public static VoxelRearRamWobble Bind(GameObject owner, Transform visual)
        {
            var effect = owner.GetComponent<VoxelRearRamWobble>();
            if (effect == null) effect = owner.AddComponent<VoxelRearRamWobble>();
            effect.RestorePose();
            effect.enabled = false;
            effect.body = visual;
            effect.restRotation = visual.localRotation;
            effect.BindFrontSteering();
            return effect;
        }

        public void Play(VoxelCarController rammer = null)
        {
            if (body == null) return;
            var boost = rammer != null ? rammer.BoostController : null;
            // Capture boost at impact so its expiry cannot change the recovery rhythm.
            playbackSpeed = boost != null && boost.isActiveAndEnabled && boost.Target == rammer && boost.IsBoosting ? 1.5f : 1f;
            elapsed = 0f;
            RestorePose();
            enabled = true;
        }

        private void LateUpdate() => Advance(Time.deltaTime);

        private void Advance(float seconds)
        {
            if (VoxelPauseMenu.IsPaused || seconds <= 0f) return;
            if (body == null) { enabled = false; return; }
            float stagesDuration = RecoveryAngles.Length * TransitionDuration;
            float duration = stagesDuration + SettleDuration;
            elapsed = Mathf.Min(duration, elapsed + seconds * playbackSpeed);
            int stage = Mathf.FloorToInt(elapsed / TransitionDuration);
            float yaw;
            if (stage < RecoveryAngles.Length)
            {
                float from = stage == 0 ? 0f : RecoveryAngles[stage - 1];
                float stageElapsed = elapsed - stage * TransitionDuration;
                yaw = Mathf.Lerp(from, RecoveryAngles[stage], EaseArc(stageElapsed / TransitionDuration));
            }
            else
                yaw = Mathf.Lerp(RecoveryAngles[RecoveryAngles.Length - 1], 0f,
                    EaseArc((elapsed - stagesDuration) / SettleDuration));
            body.localRotation = restRotation * Quaternion.Euler(0f, yaw, -yaw * .25f);
            // Steering pivots own yaw; the original child wheels retain their independent axle spin.
            foreach (var pivot in steeringPivots)
                if (pivot != null) pivot.localRotation = Quaternion.Euler(0f, -yaw * 1.5f, 0f);
            if (elapsed >= duration) { RestorePose(); enabled = false; }
        }

        // Slow naturally into each reversal, then flow straight into the next arc without a hold.
        private static float EaseArc(float progress) => .5f - .5f * Mathf.Cos(Mathf.Clamp01(progress) * Mathf.PI);

        private void BindFrontSteering()
        {
            var wheels = new System.Collections.Generic.List<Transform>();
            float front = float.NegativeInfinity, rear = float.PositiveInfinity;
            foreach (var part in body.GetComponentsInChildren<Transform>(true))
            {
                if (part.name != "Obstacle Voxel Wheel") continue;
                wheels.Add(part);
                float depth = body.InverseTransformPoint(part.position).z;
                front = Mathf.Max(front, depth); rear = Mathf.Min(rear, depth);
            }
            var pivots = new System.Collections.Generic.List<Transform>();
            foreach (var wheel in wheels)
            {
                if (body.InverseTransformPoint(wheel.position).z <= (front + rear) * .5f) continue;
                Transform pivot = wheel.parent;
                if (pivot.name != SteeringPivotName)
                {
                    pivot = new GameObject(SteeringPivotName).transform;
                    pivot.SetParent(wheel.parent, false);
                    pivot.localPosition = wheel.localPosition;
                    wheel.SetParent(pivot, true);
                }
                pivots.Add(pivot);
            }
            steeringPivots = pivots.ToArray();
        }

        private void RestorePose()
        {
            if (body != null) body.localRotation = restRotation;
            foreach (var pivot in steeringPivots)
                if (pivot != null) pivot.localRotation = Quaternion.identity;
        }

        private void OnDisable() => RestorePose();
    }
}
