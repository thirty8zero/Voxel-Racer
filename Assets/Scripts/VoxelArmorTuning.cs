using UnityEngine;
using UnityEngine.Serialization;

namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Armor Upgrade", fileName = "DoorArmorTuning")]
    public sealed class VoxelArmorTuning : ScriptableObject
    {
        public string displayName = "DOOR ARMOUR";
        public GameObject panelPrefab;
        [FormerlySerializedAs("purchasePrice")]
        [Min(0)] public int panelPurchasePrice = 100;
        [Tooltip("Damage points needed to remove one armour voxel. A normal body voxel costs one point.")]
        [Min(1)] public int voxelHitPoints = 2;
        [Tooltip("The car this panel was fitted to. Other cars cannot purchase it.")]
        public GameObject compatibleCarPrefab;
        [Tooltip("Right panel position relative to the car visual. Left mirrors X and rotates 180 degrees.")]
        public Vector3 rightMountPosition = new Vector3(1.19f, 0.42f, -0.175f);

        public bool Fits(VoxelCarDefinition car) => car != null && compatibleCarPrefab != null &&
            car.visualPrefab == compatibleCarPrefab;

        public static VoxelArmorTuning Load() => Resources.Load<VoxelArmorTuning>("Armor/DoorArmorTuning");
    }
}
