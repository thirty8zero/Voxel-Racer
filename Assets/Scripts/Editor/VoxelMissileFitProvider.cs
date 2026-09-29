using System.Collections.Generic;
namespace VoxelRacer.Editor
{
    public sealed class VoxelMissileFitProvider : IVoxelUpgradeFitProvider
    {
        public IEnumerable<VoxelUpgradeFitEntry> Discover()
        {
            foreach (var tuning in VoxelUpgradeFitCatalog.Assets<VoxelMissileLauncherTuning>())
                for (int i = 0; i < 2; i++)
                {
                    bool right = i == 1;
                    yield return new VoxelUpgradeFitEntry { Asset = tuning, Label = "MISSILE LAUNCHER — " + (right ? "Right" : "Left") + " roof edge",
                        Fits = tuning.Fits, Build = car => VoxelMissileUpgradeState.CreateVisual(car, tuning, right) };
                }
        }
    }
}
