using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Catalogue entry used by the car-selection screen.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Car Definition", fileName = "VoxelCarDefinition")]
    public sealed class VoxelCarDefinition : ScriptableObject
    {
        public string displayName = "Voxel Car";
        public int selectionOrder;
        public GameObject visualPrefab;
        public Texture2D previewImage;
        public VoxelCarTuning tuning;
    }
}
