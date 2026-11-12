using UnityEngine;
using UnityEngine.InputSystem;

namespace VoxelRacer
{
    /// <summary>A weapon hardpoint associated with reusable gun tuning and a forward muzzle.</summary>
    public sealed class VoxelGunMount : MonoBehaviour
    {
        public VoxelGunTuning tuning;
        public Transform muzzle;
        [SerializeField] private Transform barrelRotor;
        [SerializeField] private Transform[] rotaryMuzzles;
        [SerializeField, HideInInspector] internal int upgradeSlot = -1;
        [Tooltip("One-shot flame from a missile launcher's rear opening.")]
        public ParticleSystem missileLaunchBurst;

        public Vector3 MuzzlePosition => muzzle != null ? muzzle.position : transform.position;
        public Vector3 FireDirection => muzzle != null ? muzzle.forward : transform.forward;
        public bool HasRotor => barrelRotor != null;
        public bool HasAmmunition => tuning != null && (tuning.ammunitionPerStage == 0 ||
            remainingAmmunition >= tuning.ProjectilesPerShot);
        /// <summary>Uses the same scaled game clock and reserved interval as firing.</summary>
        public float CooldownProgress => shotInterval <= 0f ? 1f :
            Mathf.Clamp01(1f - (nextFireTime - Time.time) / shotInterval);
        private bool CanFire => !VoxelPauseMenu.IsPaused && isActiveAndEnabled && tuning != null && DriverAllowsFire &&
            (VoxelStartCountdown.Active == null || VoxelStartCountdown.Active.IsComplete) &&
            (VoxelMissionProgress.Active == null || !VoxelMissionProgress.Active.IsComplete) &&
            HasAmmunition;
        public bool IsReady => CanFire && Time.time >= nextFireTime;

        private float nextFireTime;
        private float shotInterval;
        private int remainingAmmunition;
        private VoxelCarController owningCar;
        private int nextMuzzle;

        private bool DriverAllowsFire
        {
            get
            {
                // Visuals may be instantiated before their car controller is added.
                // Workshop cars disable that controller; menu displays have none.
                if (owningCar == null)
                    owningCar = GetComponentInParent<VoxelCarController>();
                return owningCar != null && owningCar.isActiveAndEnabled && !owningCar.IsDestroyed &&
                    !VoxelBossIntroduction.BlocksControls(owningCar);
            }
        }

        private void OnEnable()
        {
            remainingAmmunition = tuning != null ? tuning.ammunitionPerStage : 0;
        }

        public void ConfigureRotor(Transform rotor, Transform[] muzzles)
        {
            barrelRotor = rotor;
            rotaryMuzzles = muzzles;
        }

        private void Update() => UpdateFiring(IsFireHeld(), Time.deltaTime);

        internal void UpdateFiring(bool held, float seconds)
        {
            if (!Application.isPlaying || !held || tuning == null)
                return;
            if (HasRotor && CanFire)
                barrelRotor.Rotate(Vector3.forward, tuning.barrelSpinDegreesPerSecond * seconds, Space.Self);
            if (!TryBeginShot(out int bulletCount))
                return;

            for (int bulletIndex = 0; bulletIndex < bulletCount; bulletIndex++)
                FireProjectile();
        }

        /// <summary>Reserves one firing event for a future weapon controller.</summary>
        public bool TryBeginShot(out int bulletCount)
        {
            bulletCount = 0;
            if (!IsReady)
                return false;

            shotInterval = tuning.SecondsPerShot;
            nextFireTime = Time.time + shotInterval;
            bulletCount = tuning.ProjectilesPerShot;
            if (tuning.ammunitionPerStage > 0)
                remainingAmmunition -= bulletCount;
            return true;
        }

        private bool IsFireHeld()
        {
            var keyboard = Keyboard.current;
            if (tuning != null && tuning.projectileKind == VoxelProjectileKind.Missile)
                return (keyboard != null && keyboard.xKey.isPressed) || VoxelMobileControls.IsMissileHeld;
            return (keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed)) ||
                VoxelMobileControls.IsFireHeld;
        }

        private void FireProjectile()
        {
            if (tuning.projectileKind == VoxelProjectileKind.Missile)
            {
                float side = Mathf.Sign(owningCar.transform.InverseTransformPoint(MuzzlePosition).x);
                VoxelMissileProjectile.Create(MuzzlePosition, owningCar, side, tuning);
                if (missileLaunchBurst != null)
                {
                    missileLaunchBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    missileLaunchBurst.Play(true);
                }
                return;
            }
            Transform outlet = muzzle != null ? muzzle : transform;
            bool rotary = rotaryMuzzles != null && rotaryMuzzles.Length > 0;
            if (rotary)
            {
                outlet = rotaryMuzzles[nextMuzzle] != null ? rotaryMuzzles[nextMuzzle] : outlet;
                nextMuzzle = (nextMuzzle + 1) % rotaryMuzzles.Length;
            }
            Vector3 direction = Quaternion.AngleAxis(Random.Range(-tuning.spreadDegrees, tuning.spreadDegrees), Vector3.up) *
                outlet.forward;
            if (rotary)
                VoxelFireEffects.PlayMinigunMuzzleFire(outlet, direction);
            else
                VoxelFireEffects.PlayMuzzleFire(outlet, direction);
            VoxelProjectile.Create(outlet.position + direction * (rotary ? .028f : .2f), direction, tuning);
        }
    }
}
