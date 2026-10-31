using UnityEngine;

namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Underbody Mine Armour", fileName = "UnderbodyArmorTuning")]
    public sealed class VoxelUnderbodyArmorTuning : ScriptableObject
    {
        public string displayName = "UNDERBODY ARMOUR";
        public GameObject armorPrefab;
        public GameObject compatibleCarPrefab;
        [Min(0)] public int purchasePrice = 700;
        [Tooltip("Percentage reduction applied only to mine damage. Fractional voxel damage carries between mine hits.")]
        [Range(0, 100)] public float mineDamageReductionPercent = 30f;
        public Vector3 mountPosition;
        public Vector3 mountRotation;
        public Vector3 mountScale = Vector3.one;
        public bool Fits(VoxelCarDefinition car) => car != null && armorPrefab != null && compatibleCarPrefab != null && car.visualPrefab == compatibleCarPrefab;
        public static VoxelUnderbodyArmorTuning Load() => Resources.Load<VoxelUnderbodyArmorTuning>("Upgrades/UnderbodyArmorTuning");
    }
}
