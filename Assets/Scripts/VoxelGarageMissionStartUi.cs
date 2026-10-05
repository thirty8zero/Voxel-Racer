using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    public sealed partial class VoxelRepairUpgradeSceneController
    {
        private GameObject missionStartWarning;
        private bool MissionStartWarningVisible => missionStartWarning != null && missionStartWarning.activeSelf;

        private void ShowMissionStartWarning()
        {
            if (missionStartWarning == null)
            {
                var overlay = VoxelMenuUi.CreatePanel(garageCanvas, "Unfitted Parts Warning",
                    new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
                overlay.rectTransform.anchorMin = Vector2.zero; overlay.rectTransform.anchorMax = Vector2.one;
                overlay.rectTransform.offsetMin = overlay.rectTransform.offsetMax = Vector2.zero;
                overlay.color = new Color(0, 0, 0, .72f);
                overlay.raycastTarget = true;
                missionStartWarning = overlay.gameObject;
                var panel = VoxelMenuUi.CreatePanel(overlay.transform, "Unfitted Parts Dialog",
                    new Vector2(.5f, .5f), Vector2.zero, new Vector2(760, 390));
                Frame(panel.gameObject, GoldAccent, new Color(.025f, .035f, .05f, .98f));
                var heading = Caption(panel.transform, "Unfitted Parts Heading", "UNFITTED PARTS", 38,
                    new Vector2(.5f, .5f), new Vector2(0, 125), new Vector2(700, 56));
                heading.color = GoldAccent;
                var message = Caption(panel.transform, "Unfitted Parts Message",
                    "You have selected parts that haven't been fitted yet.\n\nGo back to purchase them, or start the mission without them.",
                    26, new Vector2(.5f, .5f), new Vector2(0, 20), new Vector2(690, 150));
                message.color = Color.white;
                var back = VoxelMenuUi.CreateButton(panel.transform, "Go Back to Upgrades", "GO BACK", 30,
                    new Vector2(.5f, .5f), new Vector2(-180, -125), new Vector2(320, 76), HideMissionStartWarning);
                Frame(back.gameObject, new Color(.48f, .55f, .63f), new Color(.07f, .09f, .12f));
                var start = VoxelMenuUi.CreateButton(panel.transform, "Start Mission Without Parts", "START MISSION", 30,
                    new Vector2(.5f, .5f), new Vector2(180, -125), new Vector2(320, 76), StartNextRaceConfirmed);
                Frame(start.gameObject, GoldAccent, new Color(.26f, .17f, .045f));
                foreach (var label in panel.GetComponentsInChildren<Text>()) label.font = GarageMono;
            }
            placementPointerHeld = rotatingCar = pinching = false;
            missionStartWarning.transform.SetAsLastSibling();
            missionStartWarning.SetActive(true);
        }

        private void HideMissionStartWarning()
        {
            if (missionStartWarning != null) missionStartWarning.SetActive(false);
        }
    }
}
