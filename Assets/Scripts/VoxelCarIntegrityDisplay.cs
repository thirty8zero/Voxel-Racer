using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Simple prototype HUD readout for remaining destructible car voxels.</summary>
    public sealed class VoxelCarIntegrityDisplay : MonoBehaviour
    {
        public VoxelCarController target;

        private void OnGUI()
        {
            if (!Application.isPlaying || target == null)
                return;

            var area = new Rect(20f, 20f, 190f, 68f);
            GUI.Box(area, $"CAR INTEGRITY  {Mathf.CeilToInt(target.IntegrityPercent)}%\nINT  {target.RemainingIntegrityVoxels}", VoxelHudStyles.Box(15));
        }
    }
}
