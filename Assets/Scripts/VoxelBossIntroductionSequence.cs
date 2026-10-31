using System;
using UnityEngine;

namespace VoxelRacer
{
    public enum VoxelBossIntroSide { Left, Right }

    [CreateAssetMenu(menuName = "Voxel Racer/Boss Introduction Sequence", fileName = "BossIntroductionSequence")]
    public sealed class VoxelBossIntroductionSequence : ScriptableObject
    {
        [Header("Panel Entries")]
        [Tooltip("Drag rows to change entry order. Draw Order controls visual stacking independently.")]
        public VoxelBossIntroductionPanel[] panels = Array.Empty<VoxelBossIntroductionPanel>();

        [Header("Entry Timing")]
        [Tooltip("Side the first panel enters from. Later panels alternate left/right.")]
        public VoxelBossIntroSide firstEntrySide = VoxelBossIntroSide.Right;
        [Min(.01f)] public float slideInDuration = .55f;
        [Tooltip("Seconds after one panel lands before the next starts entering.")]
        [Min(0)] public float delayBetweenPanels = .2f;

        [Header("Completed Poster And Exit")]
        [Tooltip("Seconds after the FINAL panel lands before the whole composition slides out together, opposite the first entry side.")]
        [Min(0)] public float holdDuration = 1.5f;
        [Min(.01f)] public float slideOutDuration = .45f;

        [Header("Landing Shake")]
        [Tooltip("A brief shake of the composition each time a panel lands. Zero disables it.")]
        [Min(0)] public float shakeDuration = .5f;
        [Min(0)] public float shakePixels = 20;

        [Header("Artwork Layout")]
        public Vector2 referenceResolution = new(1672, 941);

        public int PanelCount
        {
            get
            {
                int count = 0;
                foreach (var panel in panels ?? Array.Empty<VoxelBossIntroductionPanel>())
                    if (panel != null && panel.texture != null) count++;
                return count;
            }
        }
        public bool HasPanels => PanelCount > 0;
        public float AssembledAt => PanelCount * Mathf.Max(.01f, slideInDuration) +
            Mathf.Max(0, PanelCount - 1) * Mathf.Max(0, delayBetweenPanels);
        public float Duration => AssembledAt + Mathf.Max(0, holdDuration) + Mathf.Max(.01f, slideOutDuration);
    }

    [Serializable]
    public sealed class VoxelBossIntroductionPanel
    {
        public string label;
        public Texture2D texture;
        [Tooltip("Higher values draw in front. Independent of entry order.")]
        public int drawOrder;
        [Tooltip("Fill the whole viewport, including wide-screen margins. Use for the opaque background.")]
        public bool fillScreen;
        [Min(.01f)] public float scale = 1;
        [Tooltip("Position in artwork reference pixels. Scales with the fitted artwork; +Y is up.")]
        public Vector2 position;
    }
}
