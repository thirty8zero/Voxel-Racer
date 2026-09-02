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

        public void Configure(VoxelCarController player, VoxelBoostTuning tuning)
        {
            Target = player;
            Tuning = tuning;
            ChargePercent = 1f;
            IsBoosting = false;
            Target?.SetBoostSpeedBonus(0f);
        }

        public bool TryActivateBoost()
        {
            if (!CanUseBoost())
                return false;

            IsBoosting = true;
            ChargePercent = 1f;
            boostEndsAt = Time.time + Tuning.boostLength;
            Target.SetBoostSpeedBonus(Tuning.boostSpeed);
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
                Target.SetBoostSpeedBonus(0f);
        }
    }
}
