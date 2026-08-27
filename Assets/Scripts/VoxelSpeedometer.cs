using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Compact live speed readout positioned beside the integrity HUD.</summary>
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
            if (!Application.isPlaying || target == null || VoxelPlayerDeathScreen.IsShowing)
                return;

            float alpha = VoxelStartCountdown.CurrentGameplayHudAlpha;
            if (alpha <= 0f)
                return;

            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            const float width = 230f;
            const float height = 80f;
            var area = new Rect(Screen.width - width - 20f, 20f, width, height);
            float displayedSpeed = CalculateDisplaySpeed(target.CurrentSpeed, displaySpeedMultiplier);
            GUI.Box(area, $"SPEED\n{Mathf.RoundToInt(displayedSpeed)}", VoxelHudStyles.Box(33));
            GUI.color = previousColor;
        }
    }
}
