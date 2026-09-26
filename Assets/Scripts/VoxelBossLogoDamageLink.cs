using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Hides a detached boss logo piece when its owning voxel is destroyed.</summary>
    public sealed class VoxelBossLogoDamageLink : MonoBehaviour
    {
        public Transform ownerVoxel;

        private void LateUpdate()
        {
            if (ownerVoxel == null || !ownerVoxel.gameObject.activeInHierarchy)
                gameObject.SetActive(false);
        }
    }
}
