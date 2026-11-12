using UnityEngine;

namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Plough Upgrade", fileName = "PloughTuning")]
    public sealed class VoxelPloughTuning : ScriptableObject
    {
        public string displayName = "PLOUGH";
        [TextArea(2, 5), Tooltip("Overview shown in the upgrade dashboard and garage. Leave blank to use the default copy.")]
        public string description;
        public GameObject ploughPrefab;
        public GameObject compatibleCarPrefab;
        [Min(1), Tooltip("A higher level replaces the lower-level front ram in the same slot.")]
        public int upgradeLevel = 2;
        [Min(0)] public int purchasePrice = 300;
        [Min(0)] public float impactDamageBonusPercent = 25f;
        [Range(0, 100)] public float playerDamageReductionPercent = 25f;
        public Vector3 mountPosition = new Vector3(0, .16f, 2.95f);
        public Vector3 mountRotation;
        public Vector3 mountScale = Vector3.one;
        public bool Fits(VoxelCarDefinition car) => car != null && ploughPrefab != null &&
            compatibleCarPrefab != null && car.visualPrefab == compatibleCarPrefab;
        public static VoxelPloughTuning Load() => Resources.Load<VoxelPloughTuning>("Upgrades/PloughTuning");
        public static VoxelPloughTuning LoadBullBar() => Resources.Load<VoxelPloughTuning>("Upgrades/BullBarTuning");
    }
}
