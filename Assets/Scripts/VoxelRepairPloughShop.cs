namespace VoxelRacer
{
    public sealed partial class VoxelRepairUpgradeSceneController
    {
        private void TryPurchasePlough(VoxelPloughTuning tuning)
        {
            if (DisplayedCar == null || !VoxelPloughUpgradeState.TryPurchase(tuning, definition)) return;
            VoxelPloughUpgradeState.ApplyTo(DisplayedCar.transform, definition);
            VoxelCarRunState.Capture(DisplayedCar, definition);
            feedbackText.text = tuning.displayName + " INSTALLED";
            RefreshUi();
        }

    }
}
