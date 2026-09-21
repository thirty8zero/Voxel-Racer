using UnityEngine;
namespace VoxelRacer
{
    [CreateAssetMenu(menuName="Voxel Racer/Engine Upgrade",fileName="V6EngineUpgradeTuning")]
    public sealed class VoxelEngineUpgradeTuning : ScriptableObject
    {
        public string displayName="V6 ENGINE";
        public GameObject enginePrefab;
        public GameObject compatibleCarPrefab;
        [Min(0)] public int purchasePrice=1500;
        [Min(0)] public float topSpeedBonusPercent=15;
        [Min(0)] public float accelerationBonusPercent=25;
        public bool Fits(VoxelCarDefinition car) => car!=null && enginePrefab!=null && compatibleCarPrefab!=null && car.visualPrefab==compatibleCarPrefab;
        public static VoxelEngineUpgradeTuning Load() => Resources.Load<VoxelEngineUpgradeTuning>("Upgrades/V6EngineUpgradeTuning");
    }
}
