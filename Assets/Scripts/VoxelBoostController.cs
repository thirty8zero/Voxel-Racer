using UnityEngine;
using UnityEngine.InputSystem;

namespace VoxelRacer
{
    /// <summary>Consumes one full boost charge per activation, then refills it over the configured cooldown.</summary>
    public sealed class VoxelBoostController : MonoBehaviour
    {
        public VoxelCarController Target { get; private set; }
        public VoxelBoostTuning Tuning { get; private set; }
        public float ChargePercent { get; private set; } = 1f;
        public bool IsBoosting { get; private set; }
        public bool IsReady => !IsBoosting && ChargePercent >= 0.999f;

        private float boostEndsAt;
        private ParticleSystem[] exhaustFireEffects;

        public void Configure(VoxelCarController player, VoxelBoostTuning tuning)
        {
            Target = player;
            Tuning = tuning;
            ChargePercent = 1f;
            IsBoosting = false;
            Target?.SetBoostSpeedBonus(0f);
            SetBoostForwardOffset(false);
            EnsureExhaustFireEffects();
            SetExhaustFireEmission(false);
        }

        public bool TryActivateBoost()
        {
            if (!CanUseBoost())
                return false;

            IsBoosting = true;
            ChargePercent = 1f;
            boostEndsAt = Time.time + Tuning.boostLength;
            Target.SetBoostSpeedBonus(Tuning.boostSpeed);
            SetBoostForwardOffset(true);
            EnsureExhaustFireEffects();
            SetExhaustFireEmission(true);
            return true;
        }

        private void Update()
        {
            if (!Application.isPlaying || Target == null || Tuning == null)
                return;

            if (Keyboard.current != null && Keyboard.current.altKey.wasPressedThisFrame)
                TryActivateBoost();

            if (IsBoosting)
            {
                ChargePercent = Mathf.Clamp01((boostEndsAt - Time.time) / Tuning.boostLength);
                if (Time.time < boostEndsAt && !ShouldCancelBoost())
                    return;

                IsBoosting = false;
                ChargePercent = 0f;
                Target.SetBoostSpeedBonus(0f);
                SetBoostForwardOffset(false);
                SetExhaustFireEmission(false);
            }

            if (ChargePercent < 1f)
                ChargePercent = Mathf.Clamp01(ChargePercent + Time.deltaTime / Tuning.rechargeCooldownLength);
        }

        private bool CanUseBoost()
        {
            return Target != null && Tuning != null && !Target.IsDestroyed && IsReady &&
                (VoxelStartCountdown.Active == null || VoxelStartCountdown.Active.IsComplete) &&
                (VoxelMissionProgress.Active == null || !VoxelMissionProgress.Active.IsComplete);
        }

        private bool ShouldCancelBoost() => Target.IsDestroyed ||
            VoxelMissionProgress.Active?.IsComplete == true ||
            (VoxelStartCountdown.Active != null && !VoxelStartCountdown.Active.IsComplete);

        private void OnDisable()
        {
            if (Target != null)
            {
                Target.SetBoostSpeedBonus(0f);
                SetBoostForwardOffset(false);
            }
            SetExhaustFireEmission(false);
        }

        private void SetBoostForwardOffset(bool active)
        {
            if (Target == null)
                return;

            float offset = active && Tuning != null ? Tuning.boostForwardOffset : 0f;
            float duration = Tuning != null ? Tuning.boostForwardMovementDuration : 0.22f;
            VoxelEasingType easing = Tuning != null
                ? Tuning.boostForwardMovementEasing
                : VoxelEasingType.EaseInOutCubic;
            Target.SetBoostForwardOffset(offset, duration, easing);
        }

        private void EnsureExhaustFireEffects()
        {
            if (exhaustFireEffects != null)
                foreach (var effect in exhaustFireEffects)
                    if (effect != null)
                    {
                        effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        if (Application.isPlaying) Destroy(effect.gameObject);
                        else DestroyImmediate(effect.gameObject);
                    }
            exhaustFireEffects = null;
            if (Target == null)
                return;

            // Resolve the current engine on activation, including engines replaced in the shop.
            var outlets = Target.GetComponentsInChildren<VoxelEngineExhaustOutlet>();
            if (outlets.Length > 0)
            {
                exhaustFireEffects = new ParticleSystem[outlets.Length];
                for (int index = 0; index < outlets.Length; index++)
                {
                    Transform outlet = outlets[index].transform;
                    exhaustFireEffects[index] = VoxelFireEffects.CreateBoostExhaustFire(
                        outlet, outlet.position, outlet.forward);
                }
                return;
            }

            var exhausts = new System.Collections.Generic.List<Transform>();
            foreach (Transform child in Target.GetComponentsInChildren<Transform>(true))
            {
                if (child != Target.transform && child.GetComponent<ParticleSystem>() == null &&
                    child.name.IndexOf("Exhaust", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    exhausts.Add(child);
            }

            if (exhausts.Count == 0)
            {
                // A safe fallback for a future car visual that has not named its exhaust voxels yet.
                exhausts.Add(Target.transform);
                exhausts.Add(Target.transform);
            }

            int effectCount = Mathf.Min(2, exhausts.Count);
            exhaustFireEffects = new ParticleSystem[effectCount];
            for (int index = 0; index < effectCount; index++)
            {
                Transform exhaust = exhausts[index];
                Vector3 position = exhaust == Target.transform
                    ? Target.transform.TransformPoint(new Vector3(index == 0 ? -0.48f : 0.48f, 0.22f, -2.9f))
                    : exhaust.position - Target.transform.forward * 0.10f;
                exhaustFireEffects[index] = VoxelFireEffects.CreateBoostExhaustFire(Target.transform, position,
                    -Target.transform.forward);
            }
        }

        private void SetExhaustFireEmission(bool active)
        {
            if (exhaustFireEffects == null)
                return;
            foreach (ParticleSystem effect in exhaustFireEffects)
            {
                if (effect == null)
                    continue;
                var emission = effect.emission;
                emission.enabled = active;
                if (active && !effect.isPlaying)
                    effect.Play(true);
                else if (!active)
                    effect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }
    }
}
