using UnityEngine;
namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Missile Launcher Upgrade", fileName = "MissileLauncherTuning")]
    public sealed class VoxelMissileLauncherTuning : ScriptableObject
    {
        public VoxelGunTuning weapon;
        public GameObject compatibleCarPrefab;
        public Vector3 mountPosition = new(.77f, 1.48f, -.56f);
        [Tooltip("Right launcher rotation; yaw and roll are mirrored for the left launcher.")]
        public Vector3 mountRotation = new(0f, 0f, -45f);
        public bool Fits(VoxelCarDefinition car) => car != null && car.visualPrefab == compatibleCarPrefab &&
            compatibleCarPrefab != null && weapon != null && weapon.visualPrefab != null && weapon.missilePrefab != null;
        public static VoxelMissileLauncherTuning Load() => Resources.Load<VoxelMissileLauncherTuning>("Weapons/MissileLauncherTuning");
    }
}
