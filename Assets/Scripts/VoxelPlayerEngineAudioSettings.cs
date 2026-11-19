using UnityEngine;

namespace VoxelRacer
{
    [CreateAssetMenu(menuName = "Voxel Racer/Player Engine Audio Settings", fileName = "PlayerEngineAudioSettings")]
    public sealed class VoxelPlayerEngineAudioSettings : ScriptableObject
    {
        [Range(0f, 1f)] public float volume = .45f;

        [Header("Ignition and Idle")]
        public AudioClip startup;
        public AudioClip idle;

        [Header("Driving Loops")]
        public AudioClip lowOn, lowOff;
        public AudioClip mediumOn, mediumOff;
        public AudioClip highOn, highOff;
        public AudioClip maxRpm;
    }
}
