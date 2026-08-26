using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Compact live speed readout positioned beneath the repair controls.</summary>
    public sealed class VoxelSpeedometer : MonoBehaviour
    {
        public VoxelCarController target;

        [Min(0.1f)]
        [Tooltip("Multiplier applied to the speed shown on the HUD. This does not change vehicle physics.")]
        public float displaySpeedMultiplier = 2.5f;

        public static float CalculateDisplaySpeed(float currentSpeed, float multiplier)
        {
            return Mathf.Max(0f, currentSpeed) * Mathf.Max(0f, multiplier);
        }

        private void OnGUI()
        {
            if (!Application.isPlaying || target == null)
                return;

            const float width = 172f;
            const float height = 58f;
            var area = new Rect(Screen.width - width - 20f, 244f, width, height);
            float displayedSpeed = CalculateDisplaySpeed(target.CurrentSpeed, displaySpeedMultiplier);
            GUI.Box(area, $"SPEED\n{Mathf.RoundToInt(displayedSpeed)}", VoxelHudStyles.Box(22));
        }
    }
}
