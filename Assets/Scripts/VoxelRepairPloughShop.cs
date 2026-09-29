using UnityEngine;

namespace VoxelRacer
{
    public sealed partial class VoxelRepairUpgradeSceneController
    {
        private void TryPurchasePlough()
        {
            var tuning = VoxelPloughTuning.Load();
            if (DisplayedCar == null || !VoxelPloughUpgradeState.TryPurchase(tuning, definition)) return;
            VoxelPloughUpgradeState.ApplyTo(DisplayedCar.transform, definition);
            VoxelCarRunState.Capture(DisplayedCar, definition);
            feedbackText.text = "PLOUGH INSTALLED";
            RefreshUi();
        }

        private void RefreshPloughShop()
        {
            if (ploughUpgradeButton == null) return;
            var tuning = VoxelPloughTuning.Load();
            bool fits = tuning != null && tuning.Fits(definition);
            ploughUpgradeButton.interactable = fits && !VoxelPloughUpgradeState.IsPurchased &&
                VoxelCurrencyState.Balance >= tuning.purchasePrice;
            ploughUpgradeLabel.text = !fits ? "PLOUGH\nUNAVAILABLE FOR THIS CAR" : tuning.displayName +
                (VoxelPloughUpgradeState.IsPurchased ? "\nINSTALLED" :
                "\n+" + tuning.impactDamageBonusPercent + "% FRONT DAMAGE  -" + tuning.playerDamageReductionPercent +
                "% TAKEN\nCOST <color=#FFD12A>" + tuning.purchasePrice + "</color>");
        }
    }
}
