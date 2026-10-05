using UnityEngine;
using UnityEngine.UI;
namespace VoxelRacer
{
    public sealed partial class VoxelRepairUpgradeSceneController
    {
        private Button leftMissileButton, rightMissileButton;
        private void BuildMissileShop(Transform panel)
        {
            leftMissileButton = VoxelMenuUi.CreateButton(panel, "Left Missile Purchase Button", "", 30,
                new Vector2(.5f, .5f), Vector2.zero, new Vector2(560, 100), () => BeginUpgradePlacement(VoxelGarageUpgradeKind.Missiles));
            rightMissileButton = VoxelMenuUi.CreateButton(panel, "Right Missile Purchase Button", "", 30,
                new Vector2(.5f, .5f), Vector2.zero, new Vector2(560, 100), () => BeginUpgradePlacement(VoxelGarageUpgradeKind.Missiles));
        }
        private void PurchaseMissile(bool right)
        {
            var tuning = VoxelMissileLauncherTuning.Load();
            if (DisplayedCar == null || !VoxelMissileUpgradeState.TryPurchase(tuning, definition, right)) return;
            VoxelMissileUpgradeState.ApplyTo(DisplayedCar.transform, definition);
            VoxelCarRunState.Capture(DisplayedCar, definition);
            feedbackText.text = "MISSILE LAUNCHER INSTALLED"; RefreshUi();
        }
        private void RefreshMissileShop()
        {
            var tuning = VoxelMissileLauncherTuning.Load();
            for (int i = 0; i < 2; i++)
            {
                bool right = i == 1; var button = right ? rightMissileButton : leftMissileButton;
                if (button == null) continue;
                bool fits = tuning != null && tuning.Fits(definition), owned = VoxelMissileUpgradeState.IsPurchased(right);
                button.interactable = fits && !owned && VoxelCurrencyState.Balance >= tuning.weapon.purchasePrice;
                button.GetComponentInChildren<Text>().text = (right ? "RIGHT" : "LEFT") + " MISSILE LAUNCHER\n" +
                    (!fits ? "UNAVAILABLE FOR THIS CAR" : owned ? "INSTALLED" :
                    tuning.weapon.damagePerBullet + " DAMAGE  " + tuning.weapon.areaOfEffectRadius + "m BLAST\nCOST <color=#FFD12A>" + tuning.weapon.purchasePrice + "</color>");
            }
        }
    }
}
