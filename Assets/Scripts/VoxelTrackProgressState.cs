namespace VoxelRacer
{
    /// <summary>Remembers which track should be generated as scenes change.</summary>
    public static class VoxelTrackProgressState
    {
        private static VoxelTrackSequence sequence;
        private static int currentTrackIndex;

        public static int CurrentTrackIndex => currentTrackIndex;
        public static int NextTrackIndex
        {
            get
            {
                EnsureSequence();
                if (sequence == null || sequence.tracks == null || sequence.tracks.Length == 0) return 0;
                int current = System.Math.Max(0, System.Math.Min(currentTrackIndex, sequence.tracks.Length - 1));
                return current + 1 < sequence.tracks.Length ? current + 1 : sequence.loopSequence ? 0 : current;
            }
        }
        public static VoxelTrackDefinition NextTrack
        {
            get
            {
                EnsureSequence();
                return sequence == null || sequence.tracks == null || sequence.tracks.Length == 0
                    ? null : sequence.tracks[NextTrackIndex];
            }
        }
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

            currentTrackIndex = NextTrackIndex;
            return CurrentTrack;
        }

        private static void EnsureSequence()
        {
            if (sequence == null)
                sequence = VoxelTrackSequence.Load();
        }
    }
}
