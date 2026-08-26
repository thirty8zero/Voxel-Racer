using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Prevents a wheel from being completely stripped by repeated damage.</summary>
    public sealed class VoxelWheelIntegrity : MonoBehaviour
    {
        [SerializeField] private int startingVoxelCount;
        [SerializeField] private int minimumRemainingVoxels;

        private void Awake() => Initialise();
        private void OnValidate() => Initialise();

        public bool CanLoseVoxel(int pendingLosses = 0)
        {
            Initialise();
            int currentVoxels = GetComponentsInChildren<MeshRenderer>().Length;
            return currentVoxels - pendingLosses > minimumRemainingVoxels;
        }

        private void Initialise()
        {
            if (startingVoxelCount > 0)
                return;

            startingVoxelCount = GetComponentsInChildren<MeshRenderer>().Length;
            minimumRemainingVoxels = Mathf.Max(1, Mathf.CeilToInt(startingVoxelCount * 0.1f));
        }
    }
}
