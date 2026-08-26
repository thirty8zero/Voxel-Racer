using UnityEngine;
using UnityEngine.InputSystem;

namespace VoxelRacer
{
    /// <summary>A weapon hardpoint associated with reusable gun tuning and a forward muzzle.</summary>
    public sealed class VoxelGunMount : MonoBehaviour
    {
        public VoxelGunTuning tuning;
        public Transform muzzle;

        public Vector3 MuzzlePosition => muzzle != null ? muzzle.position : transform.position;
        public Vector3 FireDirection => muzzle != null ? muzzle.forward : transform.forward;
        public bool IsReady => tuning != null && Time.time >= nextFireTime &&
            (tuning.ammunitionPerStage == 0 || remainingAmmunition >= Mathf.Max(1, tuning.bulletsPerShot));

        private float nextFireTime;
        private int remainingAmmunition;

        private void OnEnable()
        {
            remainingAmmunition = tuning != null ? tuning.ammunitionPerStage : 0;
        }

        private void Update()
        {
            if (!Application.isPlaying || !IsFireHeld())
                return;

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

            nextFireTime = Time.time + tuning.SecondsPerShot;
            bulletCount = Mathf.Max(1, tuning.bulletsPerShot);
            if (tuning.ammunitionPerStage > 0)
                remainingAmmunition -= bulletCount;
            return true;
        }

        private static bool IsFireHeld()
        {
            var keyboard = Keyboard.current;
            return keyboard != null && (keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed);
        }

        private void FireProjectile()
        {
            Vector3 direction = Quaternion.AngleAxis(Random.Range(-tuning.spreadDegrees, tuning.spreadDegrees), Vector3.up) *
                FireDirection;
            VoxelProjectile.Create(MuzzlePosition + direction * 0.2f, direction, tuning);
        }
    }
}
