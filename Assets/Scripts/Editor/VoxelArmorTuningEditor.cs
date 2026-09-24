using UnityEditor;
using UnityEngine;

namespace VoxelRacer.Editor
{
    [CustomEditor(typeof(VoxelArmorTuning)), CanEditMultipleObjects]
    public sealed class VoxelArmorTuningEditor : VoxelOdinTuningEditor { }

    // Preserve existing Inspector and menu entry points.
    public static class VoxelArmorFitPreview
    {
        [MenuItem("Tools/Voxel Racer/Preview Door Armour Fit")]
        public static void OpenDefault() => Open(VoxelArmorTuning.Load());
        public static void Open(VoxelArmorTuning value) => VoxelUpgradeFitPreview.Open(value);
    }
}