using UnityEngine;
namespace VoxelRacer
{
    public static class VoxelMissileUpgradeState
    {
        private static bool leftPurchased, rightPurchased;
        public static bool IsPurchased(bool right) => right ? rightPurchased : leftPurchased;
        public static void BeginNewRun() { leftPurchased = rightPurchased = false; }
        public static bool TryPurchase(VoxelMissileLauncherTuning tuning, VoxelCarDefinition car, bool right)
        {
            if (IsPurchased(right) || tuning == null || !tuning.Fits(car) || !VoxelCurrencyState.TrySpend(tuning.weapon.purchasePrice)) return false;
            if (right) rightPurchased = true; else leftPurchased = true;
            return true;
        }
        public static string MountName(bool right) => right ? "Right Roof Missile Launcher" : "Left Roof Missile Launcher";
        public static void ApplyTo(Transform car, VoxelCarDefinition definition)
        {
            var tuning = VoxelMissileLauncherTuning.Load();
            if (car == null || tuning == null || !tuning.Fits(definition)) return;
            if (leftPurchased) CreateVisual(car, tuning, false);
            if (rightPurchased) CreateVisual(car, tuning, true);
        }
        public static GameObject CreateVisual(Transform car, VoxelMissileLauncherTuning tuning, bool right)
        {
            var existing = car.Find(MountName(right));
            if (existing != null) return existing.gameObject;
            var launcher = Object.Instantiate(tuning.weapon.visualPrefab, car);
            launcher.name = MountName(right);
            var position = tuning.mountPosition; position.x = Mathf.Abs(position.x) * (right ? 1 : -1);
            launcher.transform.localPosition = position;
            var rotation = tuning.mountRotation;
            if (!right) { rotation.y = -rotation.y; rotation.z = -rotation.z; }
            launcher.transform.localRotation = Quaternion.Euler(rotation);
            return launcher;
        }
    }
}
