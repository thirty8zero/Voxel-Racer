using System.Collections.Generic;
namespace VoxelRacer.Editor
{
    public sealed class VoxelEngineFitProvider : IVoxelUpgradeFitProvider
    {
        public IEnumerable<VoxelUpgradeFitEntry> Discover()
        {
            foreach(var tuning in VoxelUpgradeFitCatalog.Assets<VoxelEngineUpgradeTuning>())
                yield return new VoxelUpgradeFitEntry {
                    Asset=tuning, Label=tuning.displayName+" — Engine bay + left/right exhausts", Fits=tuning.Fits,
                    Build=car=>VoxelEngineUpgradeState.CreateVisual(car,tuning)
                };
        }
    }
}
