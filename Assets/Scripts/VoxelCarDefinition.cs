using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Catalogue entry used by the car-selection screen.</summary>
    [CreateAssetMenu(menuName = "Voxel Racer/Car Definition", fileName = "VoxelCarDefinition")]
    public sealed class VoxelCarDefinition : ScriptableObject
    {
        public string displayName = "Voxel Car";
        public int selectionOrder;
        [Tooltip("Whether this car is currently shown in vehicle selection screens.")]
        public bool availableForSelection = true;
        public GameObject visualPrefab;
        public Texture2D previewImage;
        public VoxelCarTuning tuning;
    }
}
