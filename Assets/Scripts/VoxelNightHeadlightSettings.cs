using UnityEngine;

namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Night Headlight Settings", fileName = "NightHeadlightSettings")]
    public sealed class VoxelNightHeadlightSettings : ScriptableObject
    {
        [Header("Night Headlights")]
        public bool headlightsEnabled = true;
        public Material lampMaterial;
        public Material enemyLampMaterial;
        public Material beamMaterial;
        [Header("Beam Cards")]
        [Min(.1f)] public float beamLength = 14f;
        [Min(.1f)] public float beamEndWidth = 4f;
        public Color beamColour = new(1f, .82f, .12f, 1f);
        [ColorUsage(false, true)] public Color enemyBeamColour = new(2f, .025f, .01f, 1f);
        [Range(0f, 1f)] public float beamOpacity = .22f;
        [Min(1f)] public float maximumDrawDistance = 140f;
        public static VoxelNightHeadlightSettings Load() => Resources.Load<VoxelNightHeadlightSettings>("NightHeadlightSettings");
    }
}
