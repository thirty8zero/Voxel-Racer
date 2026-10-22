using UnityEngine;
using UnityEngine.InputSystem;

namespace VoxelRacer
{
    /// <summary>Uses one ready bottle at a time; each spent charge recharges independently.</summary>
    public sealed class VoxelBoostController : MonoBehaviour
    {
        public VoxelCarController Target { get; private set; }
        public VoxelBoostTuning Tuning { get; private set; }
        public int ChargeCount { get; private set; } = 1;
        public int ActiveChargeIndex { get; private set; } = -1;
        public float ChargePercent => IsBoosting ? GetChargePercent(ActiveChargeIndex) :
            ChargeCount > 1 ? Mathf.Max(charges[0], charges[1]) : charges[0];
        public float GetChargePercent(int index) => index >= 0 && index < ChargeCount ? charges[index] : 0f;
        public bool IsBoosting { get; private set; }
        public bool IsReady => !IsBoosting && ChargePercent >= 0.999f;
        public float RamDamageMultiplier
        {
            get
            {
                if (!isActiveAndEnabled || !IsBoosting || Tuning == null || Target == null ||
                    Target.IsDestroyed || VoxelPauseMenu.IsPaused) return 1f;
                float bonus = VoxelPloughUpgradeState.HasMountedPlough(Target.transform)
                    ? Tuning.boostRamDamageWithPloughBonusPercent : Tuning.boostRamDamageBonusPercent;
                return 1f + Mathf.Max(0f, bonus) / 100f;
            }
        }

        private float boostEndsAt;
        private readonly float[] charges = { 1f, 1f };
        private int nextCharge;
        private ParticleSystem[] exhaustFireEffects;

        public void Configure(VoxelCarController player, VoxelBoostTuning tuning)
        {
            if (Target != null && Target.BoostController == this) Target.BoostController = null;
            Target = player;
            if (Target != null) Target.BoostController = this;
            Tuning = tuning;
            ChargeCount = tuning is VoxelBoostUpgradeTuning upgrade ? Mathf.Clamp(upgrade.bottleCount, 1, 2) : 1;
            charges[0] = charges[1] = 1f;
            nextCharge = 0;
            ActiveChargeIndex = -1;
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
            for (int offset = 0; offset < ChargeCount; offset++)
            {
                int index = (nextCharge + offset) % ChargeCount;
                if (charges[index] < .999f) continue;
                ActiveChargeIndex = index;
                nextCharge = (index + 1) % ChargeCount;
                break;
            }
            boostEndsAt = Time.time + Tuning.boostLength;
            Target.SetBoostSpeedBonus(Tuning.boostSpeed, Tuning.boostAccelerationMultiplier);
            SetBoostForwardOffset(true);
            EnsureExhaustFireEffects();
            SetExhaustFireEmission(true);
            return true;
        }

        private void Update()
        {
            if (!Application.isPlaying || VoxelPauseMenu.IsPaused || Target == null || Tuning == null)
                return;

            if (Keyboard.current != null && Keyboard.current.altKey.wasPressedThisFrame)
                TryActivateBoost();

            AdvanceCharges(Time.time, Time.deltaTime);
        }

        private void AdvanceCharges(float now, float deltaTime)
        {
            // Cooldowns continue on idle bottles even while another bottle is boosting.
            for (int index = 0; index < ChargeCount; index++)
                if (index != ActiveChargeIndex && charges[index] < 1f)
                    charges[index] = Mathf.Clamp01(charges[index] + deltaTime / Mathf.Max(.05f, Tuning.rechargeCooldownLength));

            if (IsBoosting)
            {
                charges[ActiveChargeIndex] = Mathf.Clamp01((boostEndsAt - now) / Mathf.Max(.05f, Tuning.boostLength));
                if (now < boostEndsAt && !ShouldCancelBoost())
                    return;

                IsBoosting = false;
                charges[ActiveChargeIndex] = 0f;
                ActiveChargeIndex = -1;
                Target.SetBoostSpeedBonus(0f);
                SetBoostForwardOffset(false);
                SetExhaustFireEmission(false);
            }
        }

        private bool CanUseBoost()
        {
            return isActiveAndEnabled && !VoxelLoadingScreen.IsLoading && !VoxelPauseMenu.IsPaused && Target != null && Tuning != null && !Target.IsDestroyed && IsReady &&
                (VoxelStartCountdown.Active == null || VoxelStartCountdown.Active.IsComplete) &&
                (VoxelMissionProgress.Active == null || !VoxelMissionProgress.Active.IsComplete);
        }

        private bool ShouldCancelBoost() => Target.IsDestroyed ||
            VoxelMissionProgress.Active?.IsComplete == true ||
            (VoxelStartCountdown.Active != null && !VoxelStartCountdown.Active.IsComplete);

        private void OnDisable()
        {
            if (IsBoosting && ActiveChargeIndex >= 0) charges[ActiveChargeIndex] = 0f;
            IsBoosting = false;
            ActiveChargeIndex = -1;
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
                        outlet, outlet.position, outlet.forward, Tuning != null ? Tuning.exhaustEffectPrefab : null);
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
                    -Target.transform.forward, Tuning != null ? Tuning.exhaustEffectPrefab : null);
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
