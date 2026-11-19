using UnityEngine;
using UnityEngine.UI;

namespace VoxelRacer
{
    public sealed partial class VoxelRepairUpgradeSceneController
    {
        private const float UpgradeDetailsWidth = 414f;
        private const float UpgradeDetailsHeight = 280f;
        private RectTransform upgradeDetails;
        private Text upgradeDetailsTitle, upgradeDetailsEffect, upgradeStatsHeading, upgradeStats;
        private VoxelGaragePanel upgradeDetailsFrame, upgradeStatsFrame;
        private VoxelGarageUpgradeIcon upgradeDetailsIcon;
        private VoxelGarageUpgradeCard inspectedUpgrade;
        private Vector3[] upgradeTurntableRim;
        private Matrix4x4 upgradeDetailsViewProjection;
        private float upgradeTurntableLeft = 1f;
        private float upgradeDetailsHeightScale;

        private void BuildUpgradeDetails(Transform parent)
        {
            var panel = new GameObject("Upgrade Details", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            upgradeDetails = (RectTransform)panel.transform;
            upgradeDetails.pivot = new Vector2(0, 1);
            var info = new GameObject("Part Information", typeof(RectTransform), typeof(VoxelGaragePanel));
            info.transform.SetParent(upgradeDetails, false);
            upgradeDetailsFrame = info.GetComponent<VoxelGaragePanel>();
            upgradeDetailsFrame.raycastTarget = false;

            var topLeft = new Vector2(0, 1);
            var icon = new GameObject("Detail Icon", typeof(RectTransform), typeof(VoxelGarageUpgradeIcon));
            icon.transform.SetParent(panel.transform, false);
            Place((RectTransform)icon.transform, topLeft, new Vector2(46, -45), new Vector2(42, 42));
            upgradeDetailsIcon = icon.GetComponent<VoxelGarageUpgradeIcon>();
            upgradeDetailsIcon.raycastTarget = false;
            upgradeDetailsIcon.color = new Color(.87f, .90f, .98f);
            upgradeDetailsTitle = DetailLabel("Detail Title", GarageHeading, 34, new Vector2(234, -45), new Vector2(310, 60));
            upgradeDetailsEffect = DetailLabel("Upgrade Effect", GarageMono, 30, Vector2.zero, Vector2.zero);
            var statsPanel = new GameObject("Part Stats", typeof(RectTransform), typeof(VoxelGaragePanel));
            statsPanel.transform.SetParent(upgradeDetails, false);
            upgradeStatsFrame = statsPanel.GetComponent<VoxelGaragePanel>();
            upgradeStatsFrame.color = new Color(.025f, .075f, .06f, .94f);
            upgradeStatsFrame.edgeColor = new Color(.38f, .78f, .55f);
            upgradeStatsFrame.raycastTarget = false;
            upgradeStatsHeading = DetailLabel("Part Stats Heading", GarageMono, 20, Vector2.zero, Vector2.zero, statsPanel.transform);
            upgradeStatsHeading.text = "PART STATS";
            upgradeStatsHeading.color = new Color(.65f, .79f, .72f);
            upgradeStats = DetailLabel("Upgrade Stats", GarageMono, 26, Vector2.zero, Vector2.zero, statsPanel.transform);
            upgradeStats.color = new Color(.61f, 1f, .73f);
            panel.SetActive(false);

            var rim = GetComponentInChildren<VoxelCarTurntable>()?.transform.Find("Stationary outer rim");
            var mesh = rim != null ? rim.GetComponent<MeshFilter>()?.sharedMesh : null;
            if (mesh != null)
            {
                upgradeTurntableRim = mesh.vertices;
                for (int i = 0; i < upgradeTurntableRim.Length; i++)
                    upgradeTurntableRim[i] = rim.TransformPoint(upgradeTurntableRim[i]);
            }
        }

        private Text DetailLabel(string name, Font font, int size, Vector2 position, Vector2 bounds, Transform parent = null)
        {
            var label = VoxelMenuUi.CreateText(parent ?? upgradeDetails, name, "", size, TextAnchor.MiddleLeft,
                new Vector2(0, 1), position, bounds);
            label.font = font;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 16;
            label.resizeTextMaxSize = size;
            label.supportRichText = false;
            return label;
        }

        private void SelectUpgradeCard(VoxelGarageUpgradeCard card)
        {
            if (isLoading || GarageSettingsVisible || MissionStartWarningVisible) return;
            SetInspectedUpgrade(card);
            BeginUpgradePlacement(card.UpgradeKind);
        }

        private void SetInspectedUpgrade(VoxelGarageUpgradeCard card)
        {
            inspectedUpgrade?.SetSelected(false);
            inspectedUpgrade = card;
            inspectedUpgrade?.SetSelected(true);
            RefreshUpgradeDetails();
        }

        private void RefreshUpgradeDetails()
        {
            if (upgradeDetails == null) return;
            upgradeDetails.gameObject.SetActive(inspectedUpgrade != null);
            if (inspectedUpgrade != null)
            {
                var kind = inspectedUpgrade.UpgradeKind;
                upgradeDetailsTitle.text = UpgradeDisplayName(kind);
                string requiredPurchase = inspectedUpgrade.RequiredPurchase;
                upgradeDetailsEffect.text = requiredPurchase != null
                    ? $"Requires purchase of {requiredPurchase}."
                    : UpgradeDescription(kind);
                upgradeStats.text = requiredPurchase != null ? string.Empty : UpgradeStats(kind);
                upgradeStatsFrame.gameObject.SetActive(!string.IsNullOrEmpty(upgradeStats.text));
                upgradeDetailsIcon.kind = inspectedUpgrade.IconKind;
                upgradeDetailsIcon.SetVerticesDirty();
                Color fill = inspectedUpgrade.SurfaceColor;
                fill.a = .9f;
                upgradeDetailsFrame.color = fill;
                upgradeDetailsFrame.edgeColor = inspectedUpgrade.EdgeColor;
                upgradeDetailsFrame.SetVerticesDirty();
                upgradeStatsFrame.edgeColor = inspectedUpgrade.EdgeColor;
                upgradeStatsFrame.SetVerticesDirty();
            }
            ApplyUpgradeDetailClearance();
            LayoutUpgradeDetailContents(upgradeDetails.rect.width);
            LayoutGarageFeedback();
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
            // Keep the description readable on narrow screens before scaling the card.
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
            float labelWidth = Mathf.Max(1, width - 2f * margin);
            float infoHeight = upgradeStatsFrame.gameObject.activeSelf ? 142f : 190f;
            upgradeDetailsIcon.gameObject.SetActive(inspectedUpgrade != null && !compact);
            var frame = upgradeDetailsFrame.rectTransform;
            frame.anchorMin = new Vector2(0, 1);
            frame.anchorMax = new Vector2(1, 1);
            frame.offsetMin = new Vector2(0, -infoHeight);
            frame.offsetMax = Vector2.zero;
            float titleLeft = compact ? margin : 79f;
            Place(upgradeDetailsTitle.rectTransform, anchor, new Vector2((titleLeft + width - margin) * .5f, -40),
                new Vector2(Mathf.Max(1, width - margin - titleLeft), 53));
            Place(upgradeDetailsEffect.rectTransform, anchor, new Vector2(width * .5f, -(65f + infoHeight) * .5f + 5f),
                new Vector2(labelWidth, infoHeight - 72f));
            var stats = upgradeStatsFrame.rectTransform;
            stats.anchorMin = new Vector2(0, 1);
            stats.anchorMax = new Vector2(1, 1);
            stats.offsetMin = new Vector2(0, -UpgradeDetailsHeight);
            stats.offsetMax = new Vector2(0, -infoHeight - 10);
            Place(upgradeStatsHeading.rectTransform, anchor, new Vector2(width * .5f, -23), new Vector2(labelWidth, 26));
            Place(upgradeStats.rectTransform, anchor, new Vector2(width * .5f, -78), new Vector2(labelWidth, 79));
        }

        private static string UpgradeStats(VoxelGarageUpgradeKind kind)
        {
            switch (kind)
            {
                case VoxelGarageUpgradeKind.Armour:
                case VoxelGarageUpgradeKind.ExtendedArmour:
                    var armour = VoxelGarageUpgradePlacement.ArmorTuning(kind);
                    return armour != null ? $"{armour.voxelHitPoints} HP PER ARMOUR VOXEL" : "";
                case VoxelGarageUpgradeKind.UnderbodyArmour:
                    var underbody = VoxelUnderbodyArmorTuning.Load();
                    return underbody != null ? $"-{underbody.mineDamageReductionPercent:0.#}% MINE DAMAGE TAKEN" : "";
                case VoxelGarageUpgradeKind.Wheels:
                case VoxelGarageUpgradeKind.GoldWheels:
                    var wheels = VoxelGarageUpgradePlacement.WheelTuning(kind);
                    return wheels != null ? $"+{wheels.accelerationBonusPercent:0.#}% ACCELERATION\n+{wheels.brakingBonusPercent:0.#}% BRAKING\n+{wheels.laneChangeBonusPercent:0.#}% LANE CHANGE SPEED" : "";
                case VoxelGarageUpgradeKind.Spikes:
                case VoxelGarageUpgradeKind.StarSpikes:
                    var spikes = kind == VoxelGarageUpgradeKind.StarSpikes ? VoxelWheelSpikeTuning.LoadStar() : VoxelWheelSpikeTuning.Load();
                    return spikes != null ? $"+{spikes.sideRamDamageBonusPercent:0.#}% SIDE RAM DAMAGE" : "";
                case VoxelGarageUpgradeKind.Boost:
                case VoxelGarageUpgradeKind.DualBoost:
                    var boost = kind == VoxelGarageUpgradeKind.DualBoost ? VoxelBoostUpgradeTuning.LoadDualUpgrade() : VoxelBoostUpgradeTuning.LoadUpgrade();
                    return boost != null ? $"{boost.bottleCount} CHARGE{(boost.bottleCount == 1 ? "" : "S")} · {boost.boostLength:0.#}S EACH\n{boost.rechargeCooldownLength:0.#}S RECHARGE" : "";
                case VoxelGarageUpgradeKind.Plough:
                case VoxelGarageUpgradeKind.BullBar:
                    var plough = VoxelGarageUpgradePlacement.PloughTuning(kind);
                    return plough != null ? $"+{plough.impactDamageBonusPercent:0.#}% FRONT RAM DAMAGE\n-{plough.playerDamageReductionPercent:0.#}% FRONT DAMAGE TAKEN" : "";
                case VoxelGarageUpgradeKind.Guns:
                case VoxelGarageUpgradeKind.Minigun:
                    var gun = VoxelGarageUpgradePlacement.GunTuning(kind);
                    return gun != null ? $"{gun.shotsPerSecond:0.#} SHOTS/S\n{gun.damagePerBullet:0.#} DAMAGE/HIT\n{gun.DamagePerSecondLabel}" : "";
                case VoxelGarageUpgradeKind.Missiles:
                    var weapon = VoxelMissileLauncherTuning.Load()?.weapon;
                    return weapon != null ? $"{weapon.damagePerBullet:0.#} DAMAGE/HIT\n{weapon.SecondsPerShot:0.#}S COOLDOWN\n{weapon.DamagePerSecondLabel}" : "";
                case VoxelGarageUpgradeKind.Engine:
                    var engine = VoxelEngineUpgradeTuning.Load();
                    return engine != null ? $"+{engine.topSpeedBonusPercent:0.#}% TOP SPEED\n+{engine.accelerationBonusPercent:0.#}% ACCELERATION" : "";
                default:
                    return "";
            }
        }

        private static string UpgradeDescription(VoxelGarageUpgradeKind kind) => kind switch
        {
            VoxelGarageUpgradeKind.Armour => DescriptionOr(VoxelArmorTuning.Load()?.description, "Protects the door you fit it to."),
            VoxelGarageUpgradeKind.ExtendedArmour => DescriptionOr(VoxelArmorTuning.LoadExtended()?.description, "A wider, tougher panel for one door."),
            VoxelGarageUpgradeKind.UnderbodyArmour => DescriptionOr(VoxelUnderbodyArmorTuning.Load()?.description, "Shields the chassis from mines."),
            VoxelGarageUpgradeKind.Wheels => DescriptionOr(VoxelPerformanceWheelTuning.Load()?.description, "Quicker starts, stops and lane changes."),
            VoxelGarageUpgradeKind.GoldWheels => DescriptionOr(VoxelPerformanceWheelTuning.LoadGold()?.description, "Larger tyres on ten-spoke gold alloys."),
            VoxelGarageUpgradeKind.Spikes => DescriptionOr(VoxelWheelSpikeTuning.Load()?.description, "Hit harder when ramming from the side."),
            VoxelGarageUpgradeKind.StarSpikes => DescriptionOr(VoxelWheelSpikeTuning.LoadStar()?.description, "Hit harder when ramming from the side."),
            VoxelGarageUpgradeKind.Boost => DescriptionOr(VoxelBoostUpgradeTuning.LoadUpgrade()?.description, "Adds a rechargeable burst of speed."),
            VoxelGarageUpgradeKind.DualBoost => DescriptionOr(VoxelBoostUpgradeTuning.LoadDualUpgrade()?.description, "Upgrades the boost with a twin-bottle setup."),
            VoxelGarageUpgradeKind.Plough or VoxelGarageUpgradeKind.BullBar => DescriptionOr(
                VoxelGarageUpgradePlacement.PloughTuning(kind)?.description, "Ram harder and take less front-impact damage."),
            VoxelGarageUpgradeKind.Guns => DescriptionOr(VoxelGarageUpgradePlacement.GunTuning(kind)?.description, "Adds a forward-firing hood cannon."),
            VoxelGarageUpgradeKind.Minigun => DescriptionOr(VoxelGarageUpgradePlacement.GunTuning(kind)?.description, "Six rotating barrels fire in sequence, each with a short tracer."),
            VoxelGarageUpgradeKind.Missiles => DescriptionOr(VoxelMissileLauncherTuning.Load()?.description, "Fires missiles from a roof launcher."),
            VoxelGarageUpgradeKind.Engine => DescriptionOr(VoxelEngineUpgradeTuning.Load()?.description, "Increases top speed and acceleration."),
            _ => ""
        };

        private static string DescriptionOr(string description, string fallback) =>
            string.IsNullOrWhiteSpace(description) ? fallback : description.Trim();
    }
}
