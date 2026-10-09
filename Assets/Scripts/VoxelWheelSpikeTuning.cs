using UnityEngine;
using UnityEngine.Serialization;

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
        [FormerlySerializedAs("sideRamDamageBonus")]
        [Tooltip("Percentage added to the struck enemy's player ram damage for side impacts. 15 means +15% (20 becomes 23). Rear impacts are unchanged.")]
        [Min(0f)] public float sideRamDamageBonusPercent = 15f;
        [Tooltip("Higher levels replace lower-level wheel spikes; their damage bonuses do not stack.")]
        [Min(1)] public int upgradeLevel = 1;

        public bool Fits(VoxelCarDefinition car)
        {
            if (spikePrefab == null || car == null || car.visualPrefab == null) return false;
            int wheels = 0;
            foreach (var part in car.visualPrefab.GetComponentsInChildren<Transform>(true))
                if (part.name == "Voxel Wheel") wheels++;
            return wheels == 4;
        }

        public static VoxelWheelSpikeTuning Load() =>
            Resources.Load<VoxelWheelSpikeTuning>("Upgrades/WheelSpikeTuning");

        public static VoxelWheelSpikeTuning LoadStar() =>
            Resources.Load<VoxelWheelSpikeTuning>("Upgrades/StarWheelSpikeTuning");
    }
}
