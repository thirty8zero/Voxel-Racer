using UnityEngine;

namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Plough Upgrade", fileName = "PloughTuning")]
    public sealed class VoxelPloughTuning : ScriptableObject
    {
        public string displayName = "PLOUGH";
        public GameObject ploughPrefab;
        public GameObject compatibleCarPrefab;
        [Min(0)] public int purchasePrice = 300;
        [Min(0)] public float impactDamageBonusPercent = 25f;
        [Range(0, 100)] public float playerDamageReductionPercent = 25f;
        public Vector3 mountPosition = new Vector3(0, .16f, 2.95f);
        public Vector3 mountRotation;
        public Vector3 mountScale = Vector3.one;
        public bool Fits(VoxelCarDefinition car) => car != null && ploughPrefab != null &&
            compatibleCarPrefab != null && car.visualPrefab == compatibleCarPrefab;
        public static VoxelPloughTuning Load() => Resources.Load<VoxelPloughTuning>("Upgrades/PloughTuning");
    }
}
