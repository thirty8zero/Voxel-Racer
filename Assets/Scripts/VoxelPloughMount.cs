using UnityEngine;

namespace VoxelRacer
{
    public sealed class VoxelPloughMount : MonoBehaviour
    {
        [SerializeField] private VoxelPloughTuning tuning;
        public VoxelPloughTuning Tuning => tuning;

        internal void Configure(VoxelPloughTuning value) => tuning = value;
    }
}
