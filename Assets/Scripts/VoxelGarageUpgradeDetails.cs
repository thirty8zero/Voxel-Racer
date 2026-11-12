using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    public sealed partial class VoxelRepairUpgradeSceneController
    {
        private const float UpgradeDetailsWidth = 414f;
        private const float UpgradeDetailsHeight = 280f;
        private RectTransform upgradeDetails;
        private Text upgradeDetailsTitle, upgradeDetailsEffect, upgradeDetailsStats, upgradeDetailsMount, upgradeDetailsActionLabel;
        private VoxelGaragePanel upgradeDetailsFrame, upgradeDetailsActionFrame;
        private VoxelGarageUpgradeIcon upgradeDetailsIcon;
        private Button upgradeDetailsAction;
        private VoxelGarageUpgradeCard inspectedUpgrade;
        private Vector3[] upgradeTurntableRim;
        private Matrix4x4 upgradeDetailsViewProjection;
        private float upgradeTurntableLeft = 1f;
        private float upgradeDetailsHeightScale;

        private void BuildUpgradeDetails(Transform parent)
        {
            var panel = new GameObject("Upgrade Details", typeof(RectTransform), typeof(VoxelGaragePanel));
            panel.transform.SetParent(parent, false);
            upgradeDetails = (RectTransform)panel.transform;
            upgradeDetails.pivot = new Vector2(0, 1);
            upgradeDetailsFrame = panel.GetComponent<VoxelGaragePanel>();
            upgradeDetailsFrame.raycastTarget = false;

            var topLeft = new Vector2(0, 1);
            var icon = new GameObject("Detail Icon", typeof(RectTransform), typeof(VoxelGarageUpgradeIcon));
            icon.transform.SetParent(panel.transform, false);
            Place((RectTransform)icon.transform, topLeft, new Vector2(46, -45), new Vector2(42, 42));
            upgradeDetailsIcon = icon.GetComponent<VoxelGarageUpgradeIcon>();
            upgradeDetailsIcon.raycastTarget = false;
            upgradeDetailsIcon.color = new Color(.87f, .90f, .98f);
            upgradeDetailsTitle = DetailLabel("Detail Title", GarageHeading, 34, new Vector2(234, -45), new Vector2(310, 60));
            upgradeDetailsEffect = DetailLabel("Upgrade Effect", GarageMono, 23, new Vector2(207, -107), new Vector2(366, 61));
            upgradeDetailsStats = DetailLabel("Upgrade Stats", GarageMono, 22, new Vector2(207, -172), new Vector2(366, 62));
            upgradeDetailsStats.color = new Color(.48f, .95f, .64f);
            upgradeDetailsMount = DetailLabel("Upgrade Mount", GarageMono, 20, new Vector2(98, -237), new Vector2(148, 52));
            upgradeDetailsMount.color = new Color(.67f, .73f, .82f);
            upgradeDetailsAction = VoxelMenuUi.CreateButton(panel.transform, "Selected Upgrade Action", "", 23,
                topLeft, new Vector2(288, -237), new Vector2(204, 58), ActivateSelectedUpgrade);
            Frame(upgradeDetailsAction.gameObject, Color.gray, new Color(.06f, .10f, .15f, .95f));
            upgradeDetailsActionFrame = upgradeDetailsAction.GetComponentInChildren<VoxelGaragePanel>();
            upgradeDetailsActionLabel = upgradeDetailsAction.GetComponentInChildren<Text>();
            upgradeDetailsActionLabel.font = GarageMono;
            upgradeDetailsActionLabel.resizeTextForBestFit = true;
            upgradeDetailsActionLabel.resizeTextMinSize = 17;
            upgradeDetailsActionLabel.resizeTextMaxSize = 23;
            Place(upgradeDetailsActionLabel.rectTransform, new Vector2(.5f, .5f), Vector2.zero, new Vector2(184, 50));

            var rim = GetComponentInChildren<VoxelCarTurntable>()?.transform.Find("Stationary outer rim");
            var mesh = rim != null ? rim.GetComponent<MeshFilter>()?.sharedMesh : null;
            if (mesh != null)
            {
                upgradeTurntableRim = mesh.vertices;
                for (int i = 0; i < upgradeTurntableRim.Length; i++)
                    upgradeTurntableRim[i] = rim.TransformPoint(upgradeTurntableRim[i]);
            }
        }

        private Text DetailLabel(string name, Font font, int size, Vector2 position, Vector2 bounds)
        {
            var label = VoxelMenuUi.CreateText(upgradeDetails, name, "", size, TextAnchor.MiddleLeft,
                new Vector2(0, 1), position, bounds);
            label.font = font;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 16;
            label.resizeTextMaxSize = size;
            label.supportRichText = false;
            return label;
        }

        private void SelectUpgradeDetails(VoxelGarageUpgradeCard card)
        {
            if (isLoading || GarageSettingsVisible || MissionStartWarningVisible) return;
            inspectedUpgrade?.SetSelected(false);
            inspectedUpgrade = card;
            inspectedUpgrade?.SetSelected(true);
            RefreshUpgradeDetails();
        }

        private void RefreshUpgradeDetails()
        {
            if (upgradeDetails == null || inspectedUpgrade == null) return;
            var kind = inspectedUpgrade.UpgradeKind;
            var (effect, stats, mount) = UpgradeDetailsCopy(kind);
            upgradeDetailsTitle.text = UpgradeDisplayName(kind);
            upgradeDetailsEffect.text = effect;
            upgradeDetailsStats.text = stats;
            upgradeDetailsMount.text = mount;
            upgradeDetailsIcon.kind = inspectedUpgrade.IconKind;
            upgradeDetailsIcon.SetVerticesDirty();
            int pending = PendingCount(kind);
            bool offered = UpgradeOffer(kind, out int price, out _);
            upgradeDetailsAction.interactable = pending > 0 || (offered && AvailableUpgradeCash >= price);
            upgradeDetailsActionLabel.text = pending > 0 ? "REMOVE X" + pending :
                inspectedUpgrade.State == VoxelGarageUpgradeState.Equipped ? "EQUIPPED" :
                !offered ? "UNAVAILABLE" : AvailableUpgradeCash < price ? "NO FUNDS" : "PLACE + $" + price.ToString("N0");
            upgradeDetailsActionLabel.color = pending > 0 ? new Color(1, .81f, .35f) :
                upgradeDetailsAction.interactable ? new Color(.48f, .95f, .64f) : new Color(.62f, .69f, .77f);
            Color fill = inspectedUpgrade.SurfaceColor;
            fill.a = .9f;
            upgradeDetailsFrame.color = fill;
            upgradeDetailsFrame.edgeColor = upgradeDetailsActionFrame.edgeColor = inspectedUpgrade.EdgeColor;
            upgradeDetailsFrame.SetVerticesDirty();
            upgradeDetailsActionFrame.SetVerticesDirty();
        }

        private void ActivateSelectedUpgrade()
        {
            if (inspectedUpgrade == null || !upgradeDetailsAction.interactable || !garageUpgradePanel.activeInHierarchy ||
                GarageSettingsVisible || MissionStartWarningVisible) return;
            BeginUpgradePlacement(inspectedUpgrade.UpgradeKind);
        }

        private void LayoutUpgradeDetails(Vector2 canvasSize, float dockTop)
        {
            if (upgradeDetails == null) return;
            float top = 352f;
            var dial = transform.Find("Player Integrity HUD/Integrity Dial Backplate") as RectTransform;
            if (dial != null)
            {
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(garageCanvas, dial);
                top = Mathf.Max(top, canvasSize.y * .5f - bounds.min.y + 26f);
            }
            upgradeDetails.anchorMin = upgradeDetails.anchorMax = new Vector2(0, 1);
            upgradeDetails.anchoredPosition = new Vector2(12, canvasSize.y - top - dockTop);
            upgradeDetailsHeightScale = Mathf.Clamp01((canvasSize.y - top - dockTop - 16f) / UpgradeDetailsHeight);
            ApplyUpgradeDetailClearance();
        }

        private void ApplyUpgradeDetailClearance()
        {
            if (upgradeDetails == null || garageCanvas == null) return;
            float availableWidth = UpgradeDetailsWidth;
            if (workshopCamera != null && upgradeDetails.gameObject.activeInHierarchy && upgradeTurntableRim != null)
            {
                Matrix4x4 viewProjection = workshopCamera.projectionMatrix * workshopCamera.worldToCameraMatrix;
                if (viewProjection != upgradeDetailsViewProjection)
                {
                    upgradeDetailsViewProjection = viewProjection;
                    upgradeTurntableLeft = 1f;
                    foreach (var vertex in upgradeTurntableRim)
                    {
                        Vector4 clip = viewProjection * new Vector4(vertex.x, vertex.y, vertex.z, 1f);
                        if (clip.w > 0f) upgradeTurntableLeft = Mathf.Min(upgradeTurntableLeft, (clip.x / clip.w + 1f) * .5f);
                    }
                }
                var dock = (RectTransform)upgradeDetails.parent;
                float left = dock.offsetMin.x + upgradeDetails.anchoredPosition.x;
                availableWidth = Mathf.Max(0f, upgradeTurntableLeft * garageCanvas.rect.width - left - 16f);
            }
            // On a narrow screen, stack the action under the mount text before reducing the type.
            const float compactWidth = 200f;
            float scale = Mathf.Min(upgradeDetailsHeightScale, availableWidth / compactWidth);
            float width = scale > 0f ? Mathf.Min(UpgradeDetailsWidth, availableWidth / scale) : compactWidth;
            if (Mathf.Approximately(upgradeDetails.localScale.x, scale) && Mathf.Approximately(upgradeDetails.rect.width, width)) return;
            upgradeDetails.localScale = Vector3.one * scale;
            upgradeDetails.sizeDelta = new Vector2(width, UpgradeDetailsHeight);
            LayoutUpgradeDetailContents(width);
            LayoutGarageFeedback();
        }

        private void LayoutUpgradeDetailContents(float width)
        {
            bool compact = width < 330f;
            var anchor = new Vector2(0, 1);
            float margin = compact ? 16f : 24f;
            float labelWidth = width - 2f * margin;
            upgradeDetailsIcon.gameObject.SetActive(!compact);
            float titleLeft = compact ? margin : 79f;
            Place(upgradeDetailsTitle.rectTransform, anchor, new Vector2((titleLeft + width - margin) * .5f, compact ? -34f : -45f),
                new Vector2(width - margin - titleLeft, compact ? 51f : 60f));
            Place(upgradeDetailsEffect.rectTransform, anchor, new Vector2(width * .5f, compact ? -91f : -107f), new Vector2(labelWidth, 61));
            Place(upgradeDetailsStats.rectTransform, anchor, new Vector2(width * .5f, compact ? -150f : -172f), new Vector2(labelWidth, compact ? 52f : 57f));
            float actionWidth = compact ? labelWidth : Mathf.Min(204f, width * .49f);
            float mountWidth = compact ? labelWidth : labelWidth - actionWidth - 14f;
            Place(upgradeDetailsMount.rectTransform, anchor, new Vector2(margin + mountWidth * .5f, compact ? -192f : -237f),
                new Vector2(mountWidth, compact ? 26f : 52f));
            Place((RectTransform)upgradeDetailsAction.transform, anchor, new Vector2(width - margin - actionWidth * .5f, -237f),
                new Vector2(actionWidth, 58f));
            upgradeDetailsActionLabel.rectTransform.sizeDelta = new Vector2(actionWidth - 18f, 50f);
        }

        private static (string effect, string stats, string mount) UpgradeDetailsCopy(VoxelGarageUpgradeKind kind)
        {
            switch (kind)
            {
                case VoxelGarageUpgradeKind.Armour:
                case VoxelGarageUpgradeKind.ExtendedArmour:
                    var armour = VoxelGarageUpgradePlacement.ArmorTuning(kind);
                    return (DescriptionOr(armour?.description, kind == VoxelGarageUpgradeKind.Armour ?
                            "Protects the door you fit it to." : "A wider, tougher panel for one door."),
                        armour != null ? armour.voxelHitPoints + " HP PER ARMOUR VOXEL" : "", "LEFT / RIGHT DOOR");
                case VoxelGarageUpgradeKind.UnderbodyArmour:
                    var underbody = VoxelUnderbodyArmorTuning.Load();
                    return (DescriptionOr(underbody?.description, "Shields the chassis from mines."),
                        underbody != null ? $"-{underbody.mineDamageReductionPercent:0.#}% MINE DAMAGE" : "", "UNDERBODY");
                case VoxelGarageUpgradeKind.Wheels:
                case VoxelGarageUpgradeKind.GoldWheels:
                    var wheels = VoxelGarageUpgradePlacement.WheelTuning(kind);
                    return (DescriptionOr(wheels?.description, kind == VoxelGarageUpgradeKind.GoldWheels ?
                        "Larger tyres on ten-spoke gold alloys." : "Quicker starts, stops and lane changes."), wheels != null
                        ? $"+{wheels.accelerationBonusPercent:0.#}% ACCEL · +{wheels.brakingBonusPercent:0.#}% BRAKE\n+{wheels.laneChangeBonusPercent:0.#}% LANE CHANGE" : "", "ALL FOUR WHEELS");
                case VoxelGarageUpgradeKind.Spikes:
                case VoxelGarageUpgradeKind.StarSpikes:
                    var spikes = kind == VoxelGarageUpgradeKind.StarSpikes ? VoxelWheelSpikeTuning.LoadStar() : VoxelWheelSpikeTuning.Load();
                    return (DescriptionOr(spikes?.description, "Hit harder when ramming from the side."),
                        spikes != null ? $"+{spikes.sideRamDamageBonusPercent:0.#}% SIDE-RAM DAMAGE" : "", "ALL FOUR WHEELS");
                case VoxelGarageUpgradeKind.Boost:
                case VoxelGarageUpgradeKind.DualBoost:
                    var boost = kind == VoxelGarageUpgradeKind.DualBoost ? VoxelBoostUpgradeTuning.LoadDualUpgrade() : VoxelBoostUpgradeTuning.LoadUpgrade();
                    return (DescriptionOr(boost?.description, kind == VoxelGarageUpgradeKind.Boost ?
                        "Adds a rechargeable burst of speed." : "Upgrades the boost with a twin-bottle setup."),
                        boost != null ? $"{boost.boostLength:0.#}S BURST · {boost.rechargeCooldownLength:0.#}S RECHARGE" : "", "REAR MOUNT");
                case VoxelGarageUpgradeKind.Plough:
                case VoxelGarageUpgradeKind.BullBar:
                    var plough = VoxelGarageUpgradePlacement.PloughTuning(kind);
                    return (DescriptionOr(plough?.description, "Ram harder and take less front-impact damage."), plough != null
                        ? $"+{plough.impactDamageBonusPercent:0.#}% FRONT RAM DAMAGE\n-{plough.playerDamageReductionPercent:0.#}% FRONT DAMAGE TAKEN" : "", "FRONT MOUNT");
                case VoxelGarageUpgradeKind.Guns:
                case VoxelGarageUpgradeKind.Minigun:
                    var gun = VoxelGarageUpgradePlacement.GunTuning(kind);
                    return (DescriptionOr(gun?.description, kind == VoxelGarageUpgradeKind.Minigun ?
                        "Six rotating barrels fire in sequence, each with a short tracer." : "Adds a forward-firing hood cannon."), gun != null
                        ? $"{gun.shotsPerSecond:0.#} SHOTS/S · {gun.damagePerBullet:0.#} DAMAGE/HIT\n{gun.DamagePerSecondLabel}" : "", "LEFT / RIGHT HOOD");
                case VoxelGarageUpgradeKind.Missiles:
                    var launcher = VoxelMissileLauncherTuning.Load();
                    var weapon = launcher?.weapon;
                    return (DescriptionOr(launcher?.description, "Fires missiles from a roof launcher."), weapon != null
                        ? $"{weapon.damagePerBullet:0.#} DAMAGE · {weapon.SecondsPerShot:0.#}S COOLDOWN\n{weapon.DamagePerSecondLabel}" : "", "LEFT / RIGHT ROOF");
                case VoxelGarageUpgradeKind.Engine:
                    var engine = VoxelEngineUpgradeTuning.Load();
                    return (DescriptionOr(engine?.description, "Increases top speed and acceleration."), engine != null
                        ? $"+{engine.topSpeedBonusPercent:0.#}% TOP SPEED\n+{engine.accelerationBonusPercent:0.#}% ACCELERATION" : "", "ENGINE BAY");
                default:
                    return ("", "", "");
            }
        }

        private static string DescriptionOr(string description, string fallback) =>
            string.IsNullOrWhiteSpace(description) ? fallback : description.Trim();
    }
}
