using System.Collections.Generic;

namespace VoxelRacer.Editor
{
    public sealed class VoxelPloughFitProvider : IVoxelUpgradeFitProvider
    {
        public IEnumerable<VoxelUpgradeFitEntry> Discover()
        {
            foreach (var tuning in VoxelUpgradeFitCatalog.Assets<VoxelPloughTuning>())
                yield return new VoxelUpgradeFitEntry {
                    Asset = tuning, Label = tuning.displayName + " — Front bumper (complete blade)",
                    Fits = tuning.Fits, Build = car => VoxelPloughUpgradeState.CreateVisual(car, tuning)
                };
        }
    }
}
