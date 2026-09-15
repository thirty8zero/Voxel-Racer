using UnityEngine;
namespace VoxelRacer
{
    /// <summary>Placement footprint only; decorative scenery has no physics or damage cost.</summary>
    public sealed class VoxelSceneryInstance : MonoBehaviour
    {
        [HideInInspector] public float radius;
    }
}
