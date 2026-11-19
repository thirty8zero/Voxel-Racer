using UnityEngine;

namespace VoxelRacer
{
    /// <summary>Shared part-tier colours for upgrade cards and weapon hit feedback.</summary>
    public static class VoxelUpgradeRarity
    {
        // Sampled from the supplied uncommon / rare / epic / legendary reference.
        public static Color ForLevel(int level) => Mathf.Clamp(level, 1, 4) switch
        {
            1 => new Color32(80, 208, 72, 255),
            2 => new Color32(0, 160, 224, 255),
            3 => new Color32(184, 72, 224, 255),
            _ => new Color32(248, 232, 0, 255)
        };
    }
}
