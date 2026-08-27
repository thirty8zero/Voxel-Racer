using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Prototype HUD control for restoring the player's voxel car.</summary>
    public sealed class VoxelRepairButton : MonoBehaviour
    {
        public VoxelCarController target;

        private void OnGUI()
        {
            if (!Application.isPlaying || target == null || VoxelPlayerDeathScreen.IsShowing)
                return;

            float alpha = VoxelStartCountdown.CurrentGameplayHudAlpha;
            if (alpha <= 0f)
                return;

            Color previousColor = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha);
            const float width = 172f;
            const float height = 46f;
            var area = new Rect(Screen.width - width - 20f, 20f, width, height);
            var buttonStyle = VoxelHudStyles.Button(24);
            if (GUI.Button(area, "FULL REPAIR", buttonStyle))
                target.RepairToFull();

            if (GUI.Button(new Rect(area.x, area.y + 54f, width, height), "REPAIR 10%", buttonStyle))
                target.RepairPercent(10f);
            if (GUI.Button(new Rect(area.x, area.y + 108f, width, height), "REPAIR 25%", buttonStyle))
                target.RepairPercent(25f);
            if (GUI.Button(new Rect(area.x, area.y + 162f, width, height), "REPAIR 50%", buttonStyle))
                target.RepairPercent(50f);
            GUI.color = previousColor;
        }
    }
}
