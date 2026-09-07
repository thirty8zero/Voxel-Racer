using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Shop and combat values for the four-piece wheel spike upgrade.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Wheel Spike Upgrade", fileName = "WheelSpikeTuning")]
    public sealed class VoxelWheelSpikeTuning : ScriptableObject
    {
        public string displayName = "WHEEL SPIKES";
        [Tooltip("The single visual model instantiated once on each of the car's four wheels.")]
        public GameObject spikePrefab;
        [Min(0)] public int purchasePrice = 125;
        [Tooltip("Extra enemy damage dealt only when the player rams from the side.")]
        [Min(0f)] public float sideRamDamageBonus = 12f;

        public static VoxelWheelSpikeTuning Load() =>
            Resources.Load<VoxelWheelSpikeTuning>("Upgrades/WheelSpikeTuning");
    }
}
