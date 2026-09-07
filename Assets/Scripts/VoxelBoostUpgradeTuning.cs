using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Purchased bottle with the complete standard boost settings inherited directly.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Boost Bottle Upgrade", fileName = "BoostBottleUpgradeTuning")]
    public sealed class VoxelBoostUpgradeTuning : VoxelBoostTuning
    {
        [Header("Shop and Model")]
        public string displayName = "NITRO BOOST BOTTLE";
        [Min(0)] public int purchasePrice = 300;
        public GameObject bottlePrefab;
        public GameObject compatibleCarPrefab;
        [Header("Right Rear Window Panel Mount")]
        public Vector3 mountPosition = new Vector3(.77f, 1.28f, -1.68f);
        public Vector3 mountEulerAngles = new Vector3(63.435f, 0, 0);
        public bool Fits(VoxelCarDefinition car) => car != null && bottlePrefab != null &&
            compatibleCarPrefab != null && car.visualPrefab == compatibleCarPrefab;
        public static VoxelBoostUpgradeTuning LoadUpgrade() =>
            Resources.Load<VoxelBoostUpgradeTuning>("Boost/BoostBottleUpgradeTuning");
    }
}
