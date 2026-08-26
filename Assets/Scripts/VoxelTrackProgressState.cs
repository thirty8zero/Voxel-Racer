namespace VoxelRacer
{
    /// <summary>Remembers which track should be generated as scenes change.</summary>
    public static class VoxelTrackProgressState
    {
        private static VoxelTrackSequence sequence;
        private static int currentTrackIndex;

        public static int CurrentTrackIndex => currentTrackIndex;
        public static VoxelTrackDefinition CurrentTrack
        {
            get
            {
                EnsureSequence();
                if (sequence == null || sequence.tracks == null || sequence.tracks.Length == 0)
                    return null;
                currentTrackIndex = System.Math.Max(0,
                    System.Math.Min(currentTrackIndex, sequence.tracks.Length - 1));
                return sequence.tracks[currentTrackIndex];
            }
        }

        public static void BeginSequence()
        {
            EnsureSequence();
            currentTrackIndex = 0;
        }

        public static VoxelTrackDefinition AdvanceToNextTrack()
        {
            EnsureSequence();
            if (sequence == null || sequence.tracks == null || sequence.tracks.Length == 0)
                return null;

            if (currentTrackIndex + 1 < sequence.tracks.Length)
                currentTrackIndex++;
            else if (sequence.loopSequence)
                currentTrackIndex = 0;
            return CurrentTrack;
        }

        private static void EnsureSequence()
        {
            if (sequence == null)
                sequence = VoxelTrackSequence.Load();
        }
    }
}
