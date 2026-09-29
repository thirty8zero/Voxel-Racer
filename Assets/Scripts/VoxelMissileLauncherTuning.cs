using UnityEngine;
namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Missile Launcher Upgrade", fileName = "MissileLauncherTuning")]
    public sealed class VoxelMissileLauncherTuning : ScriptableObject
    {
        public VoxelGunTuning weapon;
        public GameObject compatibleCarPrefab;
        public Vector3 mountPosition = new(.77f, 1.48f, -.22f);
        public Vector3 mountRotation;
        public bool Fits(VoxelCarDefinition car) => car != null && car.visualPrefab == compatibleCarPrefab &&
            compatibleCarPrefab != null && weapon != null && weapon.visualPrefab != null && weapon.missilePrefab != null;
        public static VoxelMissileLauncherTuning Load() => Resources.Load<VoxelMissileLauncherTuning>("Weapons/MissileLauncherTuning");
    }
}
