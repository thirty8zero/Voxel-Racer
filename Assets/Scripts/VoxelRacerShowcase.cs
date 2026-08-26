using UnityEngine;

namespace VoxelRacer
{
    /// <summary>
    /// Makes the initial car prototype visible as soon as its showcase scene is opened.
    /// </summary>
    [ExecuteAlways]
    public sealed class VoxelRacerShowcase : MonoBehaviour
    {
        private void OnEnable()
        {
            VoxelRacerBootstrap.BuildPrototype(transform);
        }
    }
}
