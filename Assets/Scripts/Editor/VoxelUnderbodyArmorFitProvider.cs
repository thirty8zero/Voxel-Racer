using System.Collections.Generic;

namespace VoxelRacer.Editor
{
    public sealed class VoxelUnderbodyArmorFitProvider : IVoxelUpgradeFitProvider
    {
        public IEnumerable<VoxelUpgradeFitEntry> Discover()
        {
            foreach (var tuning in VoxelUpgradeFitCatalog.Assets<VoxelUnderbodyArmorTuning>())
                yield return new VoxelUpgradeFitEntry {
                    Asset = tuning, Label = tuning.displayName + " — Underbody (complete four-plate kit)",
                    Fits = tuning.Fits, Build = car => VoxelUnderbodyArmorUpgradeState.CreateVisual(car, tuning)
                };
        }
    }
}
